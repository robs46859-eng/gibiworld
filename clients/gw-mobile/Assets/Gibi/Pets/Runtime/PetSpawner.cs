// GW-ARCH-001 section 4 — Gibi.Pets is the assembly permitted to reference
// Gibi.AssetRuntime ("Gibi.Pets | Core, AssetRuntime, Animation").
//
// Gibi.Gameplay may reference only Core, Spatial, and Pets, so gameplay CANNOT load an
// asset itself. It asks for a pet and receives one, or receives a failure code. The
// verification pipeline stays behind this boundary, which is precisely the layering the
// section 4 table describes.
using System.Threading;
using System.Threading.Tasks;
using Gibi.AssetRuntime;
using UnityEngine;

namespace Gibi.Pets
{
    public sealed class PetSpawnResult
    {
        public bool Success;
        public string FailureCode;
        public PetController Pet;
    }

    /// <summary>
    /// Owns the section 6.4 verification path and hands gameplay a finished pet.
    /// Gameplay never sees a manifest, a signature, or a byte array.
    /// </summary>
    public sealed class PetSpawner
    {
        private readonly PresetCatalog _catalog;
        private readonly PetAssetLoader _loader;
        private readonly IEntitlementGate _entitlement;
        private readonly PetAnimationProfile _animationProfile;

        /// <summary>
        /// P0 constructor. Takes no AssetRuntime types, because Gibi.Gameplay may not
        /// reference that assembly (section 4) and therefore cannot name them in a call.
        /// Hiding the dependencies here is what keeps the boundary real rather than
        /// nominal — a signature that mentioned IEntitlementGate would force Gameplay to
        /// reference AssetRuntime just to compile.
        /// </summary>
        public PetSpawner(Shader petShader, PetAnimationProfile animationProfile = null)
            : this(petShader, new PresetShippedEntitlement(), new DebugAssetTelemetry(),
                   animationProfile) { }

        internal PetSpawner(Shader petShader, IEntitlementGate entitlement,
                            IAssetTelemetry telemetry, PetAnimationProfile animationProfile = null)
        {
            _catalog = new PresetCatalog(telemetry);
            _loader = new PetAssetLoader(telemetry, petShader);
            _entitlement = entitlement;
            _animationProfile = animationProfile ?? PetAnimationProfile.CreateRandy11P0Runtime();
        }

        public Task<int> LoadTrustedKeysAsync(CancellationToken ct)
            => _catalog.LoadTrustedKeysAsync(ct);

        public async Task<PetSpawnResult> SpawnAsync(string presetAssetId, Pose pose,
                                                     Transform parent, CancellationToken ct)
        {
            var petRoot = new GameObject("PetRoot");
            petRoot.transform.SetParent(parent, worldPositionStays: false);
            petRoot.transform.SetPositionAndRotation(pose.position, pose.rotation);

            // Section 6.3: authored runtime primitive, never a mesh collider.
            var capsule = petRoot.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.height = 0.90f;
            capsule.radius = 0.13f;
            capsule.center = new Vector3(0f, 0.28f, 0f);

            var interaction = new GameObject("InteractionVolumes");
            interaction.transform.SetParent(petRoot.transform, worldPositionStays: false);
            var petZone = interaction.AddComponent<SphereCollider>();
            petZone.isTrigger = true;
            petZone.radius = 0.6f;

            var audio = new GameObject("SpatialAudioEmitter");
            audio.transform.SetParent(petRoot.transform, worldPositionStays: false);
            var source = audio.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 0.5f;
            source.maxDistance = 8.0f;
            source.playOnAwake = false;

            var effects = new GameObject("EffectsPool");
            effects.transform.SetParent(petRoot.transform, worldPositionStays: false);

            var assetRoot = new GameObject("PetAssetRoot");
            assetRoot.transform.SetParent(petRoot.transform, worldPositionStays: false);

            var result = await _catalog.LoadPresetAsync(
                presetAssetId, assetRoot.transform, _loader, _entitlement, ct);

            if (!result.Success)
            {
                // Section 0: a pet SHALL render only if every gate passes. Nothing
                // partially-verified is left in the scene.
                Object.Destroy(petRoot);
                return new PetSpawnResult { Success = false, FailureCode = result.FailureCode };
            }

            var controller = petRoot.AddComponent<PetController>();

            if (_animationProfile != null && _animationProfile.Supports(presetAssetId))
                controller.ConfigureProfile(_animationProfile);

            // Hand the controller the instantiated asset so it can bind the Animation
            // component glTFast attached. Without this the pet loads, verifies, renders --
            // and stands perfectly still, which is exactly how it shipped until now.
            controller.AttachVerifiedAsset(result.Instance);

            return new PetSpawnResult { Success = true, Pet = controller };
        }
    }

    /// <summary>
    /// P0 only: presets ship inside the app, so entitlement is implicit. P1 replaces this
    /// with the server-issued receipt from section 6.1. A distinct type so the swap is one
    /// line and cannot be quietly forgotten.
    /// </summary>
    internal sealed class PresetShippedEntitlement : IEntitlementGate
    {
        public bool IsActive(string petAssetId, int assetVersion) => true;
    }

    internal sealed class DebugAssetTelemetry : IAssetTelemetry
    {
        public void AssetIntegrityFailed(string petAssetId, int version)
            => Debug.LogError($"[GibiWorld] asset_integrity_failed {petAssetId} v{version}");

        public void AssetRejected(string petAssetId, int version, string failureCode)
            => Debug.LogWarning($"[GibiWorld] asset_rejected {petAssetId} v{version}: {failureCode}");
    }
}
