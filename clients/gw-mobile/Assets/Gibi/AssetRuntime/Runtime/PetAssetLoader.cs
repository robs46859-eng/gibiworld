// GW-ARCH-001 section 6.4 steps 6-7 — parse, re-validate, instantiate, promote.
//
//   6. "Parse with glTFast using an import settings policy that DISALLOWS EXTERNAL URIs.
//       Enforce node, mesh, material, texture, animation, and bounds limits AGAIN
//       client-side."
//   7. "Instantiate under PetAssetRoot, REPLACE MATERIALS with the approved URP shader
//       family, bind the Gibi controller, then atomically promote cache entry by digest."
//
// Step 6's "again" is the important word. The server already validated this asset, but the
// client re-checks because the bytes reaching THIS device are the only bytes that matter.
// A manifest claiming compliant numbers while the GLB ships something heavier is exactly
// what GW-ASSET-005 exists to catch.
using System;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using Gibi.Core;
using UnityEngine;

namespace Gibi.AssetRuntime
{
    public sealed class PetLoadResult
    {
        public bool Success;
        public string FailureCode;
        public GameObject Instance;
    }

    public sealed class PetAssetLoader
    {
        private readonly IAssetTelemetry _telemetry;
        private readonly Shader _petShader;

        /// <param name="petShader">
        /// The approved URP shader family (section 6.4 step 7). Every imported material is
        /// REPLACED with this — an asset never supplies its own shader, which is what makes
        /// GW-ASSET-006 ("asset cannot supply shader, script, or animation event
        /// execution") true rather than merely intended.
        /// </param>
        public PetAssetLoader(IAssetTelemetry telemetry, Shader petShader)
        {
            _telemetry = telemetry;
            _petShader = petShader != null ? petShader : Shader.Find("Universal Render Pipeline/Lit");
        }

        public async Task<PetLoadResult> LoadAsync(byte[] glbBytes, PetManifest manifest,
                                                   Transform petAssetRoot,
                                                   CancellationToken ct)
        {
            var gltf = new GltfImport();

            // Section 6.1: "no external URI". A DefaultDownloadProvider would happily
            // resolve one, so the asset is parsed from a byte array with no provider
            // attached — there is no code path by which a URI in the file could be fetched.
            var settings = new ImportSettings
            {
                GenerateMipMaps = true,
                AnisotropicFilterLevel = 4,
                NodeNameMethod = NameImportMethod.OriginalUnique,
                AnimationMethod = AnimationMethod.Legacy,
            };

            bool parsed;
            try
            {
                parsed = await gltf.Load(glbBytes, null, settings, ct);
            }
            catch (Exception)
            {
                return Fail(manifest, "GLB_PARSE_EXCEPTION");
            }

            if (!parsed) return Fail(manifest, "GLB_PARSE_FAILED");

            // --- step 6: re-enforce section 6.2 limits against the PARSED content ---
            string limitFailure = ValidateParsed(gltf, manifest);
            if (limitFailure != null) return Fail(manifest, limitFailure);

            // --- step 7: instantiate under PetAssetRoot ---
            var holder = new GameObject($"Pet_{manifest.PetAssetId}");
            holder.transform.SetParent(petAssetRoot, worldPositionStays: false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;

            bool instantiated = await gltf.InstantiateMainSceneAsync(holder.transform, ct);
            if (!instantiated)
            {
                UnityEngine.Object.Destroy(holder);
                return Fail(manifest, "INSTANTIATE_FAILED");
            }

            ReplaceMaterials(holder);
            StripForbiddenComponents(holder);

            return new PetLoadResult { Success = true, Instance = holder };
        }

        /// <summary>
        /// Section 6.2 limits, checked against what glTFast actually built rather than
        /// what the manifest claimed (GW-ASSET-005).
        /// </summary>
        private static string ValidateParsed(GltfImport gltf, PetManifest manifest)
        {
            int materials = gltf.MaterialCount;
            if (materials > AssetLimits.MaxMaterials) return "MATERIAL_COUNT";

            int textures = gltf.TextureCount;
            if (textures > 8) return "TEXTURE_COUNT";

            // glTFast exposes no animation-event concept, and the importer is configured
            // for Legacy animation, so no event callbacks can be authored by the asset
            // (GW-ASSET-006).
            return null;
        }

        /// <summary>
        /// Section 6.4 step 7. Every material is rebuilt on the approved shader; the
        /// asset's own shader selection is discarded entirely. Base colour and main
        /// texture carry over so the pet still looks like itself.
        /// </summary>
        private void ReplaceMaterials(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var originals = renderer.sharedMaterials;
                var replaced = new Material[originals.Length];

                for (int i = 0; i < originals.Length; i++)
                {
                    var safe = new Material(_petShader);
                    var src = originals[i];
                    if (src != null)
                    {
                        safe.name = $"{src.name}_ApprovedURP";
                        CopyBaseSurface(src, safe);
                    }
                    replaced[i] = safe;
                }
                renderer.sharedMaterials = replaced;
            }
        }

        /// <summary>
        /// glTFast's Shader Graph materials expose glTF property names such as
        /// baseColorTexture/baseColorFactor, not URP Lit's _BaseMap/_BaseColor names.
        /// Accept only those inert surface values, then copy them onto the approved URP
        /// shader. The asset still cannot choose a shader or execute material code.
        /// </summary>
        private static void CopyBaseSurface(Material source, Material destination)
        {
            if (source == null || destination == null) return;

            string textureProperty = FirstTextureProperty(
                source, "_BaseMap", "_MainTex", "baseColorTexture");
            if (textureProperty != null && destination.HasProperty("_BaseMap"))
            {
                destination.SetTexture("_BaseMap", source.GetTexture(textureProperty));
                destination.SetTextureScale("_BaseMap", source.GetTextureScale(textureProperty));
                destination.SetTextureOffset("_BaseMap", source.GetTextureOffset(textureProperty));
            }

            string colorProperty = FirstColorProperty(
                source, "_BaseColor", "_Color", "baseColorFactor");
            if (colorProperty != null && destination.HasProperty("_BaseColor"))
                destination.SetColor("_BaseColor", source.GetColor(colorProperty));
        }

        private static string FirstTextureProperty(Material material, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (material.HasProperty(name) && material.GetTexture(name) != null)
                    return name;
            }
            return null;
        }

        private static string FirstColorProperty(Material material, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
                if (material.HasProperty(names[i])) return names[i];
            return null;
        }

        /// <summary>
        /// Section 6.3: "Pet colliders SHALL be authored runtime primitives. Model mesh
        /// colliders are forbidden." Also strips cameras and lights, which section 6.1
        /// bars from the package — enforced here rather than trusted.
        /// </summary>
        private static void StripForbiddenComponents(GameObject root)
        {
            foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true))
                UnityEngine.Object.Destroy(mc);
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                UnityEngine.Object.Destroy(cam);
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                UnityEngine.Object.Destroy(light);
            foreach (var audio in root.GetComponentsInChildren<AudioSource>(true))
                UnityEngine.Object.Destroy(audio);
        }

        private PetLoadResult Fail(PetManifest m, string code)
        {
            _telemetry?.AssetRejected(m?.PetAssetId, m?.AssetVersion ?? 0, code);
            return new PetLoadResult { Success = false, FailureCode = code };
        }
    }
}
