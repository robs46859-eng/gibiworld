// GW-ARCH-001 section 6.4 — the full verification path for a preset asset.
//
// GW-ASSET-008: "Preset and Pawsome3D assets use the SAME runtime verifier." This class
// loads from StreamingAssets rather than the network, but every gate below is the gate a
// downloaded Pawsome3D asset passes. The only difference is where the bytes came from.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gibi.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Gibi.AssetRuntime
{
    public sealed class PresetCatalog
    {
        private readonly PinnedKeyStore _keys = new();
        private readonly Ed25519Verifier _verifier;
        private readonly IAssetTelemetry _telemetry;

        public PresetCatalog(IAssetTelemetry telemetry)
        {
            _verifier = new Ed25519Verifier(_keys);
            _telemetry = telemetry;
        }

        /// <summary>Section 6.1: keys are pinned. Loaded once at bootstrap.</summary>
        public async Task<int> LoadTrustedKeysAsync(CancellationToken ct)
        {
            string json = await ReadStreamingAssetAsync("presets/trusted-keys.json", ct);
            if (json == null) return 0;

            var doc = JsonUtility.FromJson<TrustedKeyDoc>(json);
            int n = 0;
            foreach (var k in doc.keys ?? Array.Empty<TrustedKey>())
            {
                if (k.algorithm != "Ed25519") continue;
                _keys.Pin(k.keyId, HexToBytes(k.publicKeyHex));
                n++;
            }
            return n;
        }

        /// <summary>
        /// Runs section 6.4 steps 1-5 for a preset, then hands off to PetAssetLoader for
        /// steps 6-7. Returns null on any rejection — a pet that fails ANY gate does not
        /// render (section 0).
        /// </summary>
        public async Task<PetLoadResult> LoadPresetAsync(string petAssetId,
                                                         Transform petAssetRoot,
                                                         PetAssetLoader loader,
                                                         IEntitlementGate entitlement,
                                                         CancellationToken ct)
        {
            // --- fetch manifest ---
            string manifestJson = await ReadStreamingAssetAsync(
                $"presets/{petAssetId}.manifest.json", ct);
            if (manifestJson == null) return Reject(petAssetId, "MANIFEST_NOT_FOUND");

            var raw = JsonUtility.FromJson<RawManifest>(manifestJson);
            if (raw == null) return Reject(petAssetId, "MANIFEST_UNPARSEABLE");

            // --- step 1: schema, issuer, key, compatibility ---
            if (raw.schemaVersion != 1) return Reject(petAssetId, "SCHEMA_VERSION_UNKNOWN");
            if (raw.issuer != "GIBIWORLD_PRESET" && raw.issuer != "PAWSOME3D")
                return Reject(petAssetId, "ISSUER_UNTRUSTED");
            if (!_verifier.IsPinnedKey(raw.keyId)) return Reject(petAssetId, "KEY_ID_UNKNOWN");
            if (!AssetLimits.IsAllowedSpecies(raw.species))
                return Reject(petAssetId, "SPECIES_NOT_ALLOWED");
            if (!AssetLimits.IsAllowedProfile(raw.skeletonProfile))
                return Reject(petAssetId, "SKELETON_PROFILE_INVALID");

            // --- step 2: Ed25519 over canonical manifest bytes, signature EXCLUDED ---
            byte[] canonical = CanonicalizeExcludingSignature(manifestJson);
            byte[] signature = HexToBytes(raw.signature);
            if (!_verifier.Verify(canonical, signature, raw.keyId))
                return Reject(petAssetId, "SIGNATURE_INVALID");

            // --- step 3: entitlement must be ACTIVE for this exact id + version ---
            if (entitlement != null && !entitlement.IsActive(raw.petAssetId, raw.assetVersion))
                return Reject(petAssetId, "ENTITLEMENT_NOT_ACTIVE");

            // --- step 4: read bytes, enforcing the 45 MiB hard limit ---
            byte[] glb = await ReadStreamingAssetBytesAsync($"presets/{petAssetId}.glb", ct);
            if (glb == null) return Reject(petAssetId, "GLB_NOT_FOUND");
            if (glb.LongLength > AssetVerifier.MaxTransferBytes)
                return Reject(petAssetId, "SIZE_LIMIT_EXCEEDED");

            // --- step 5: SHA-256, compared in constant time ---
            string actual;
            using (var sha = SHA256.Create())
                actual = BytesToHex(sha.ComputeHash(glb));

            string expected = raw.digest != null && raw.digest.StartsWith("sha256:")
                ? raw.digest.Substring(7) : null;

            if (!AssetVerifier.ConstantTimeEquals(actual, expected))
            {
                _telemetry?.AssetIntegrityFailed(petAssetId, raw.assetVersion);
                return Reject(petAssetId, "DIGEST_MISMATCH");
            }

            // --- steps 6-7 ---
            var manifest = new PetManifest
            {
                SchemaVersion = raw.schemaVersion,
                PetAssetId = raw.petAssetId,
                AssetVersion = raw.assetVersion,
                Issuer = raw.issuer == "PAWSOME3D" ? AssetIssuer.Pawsome3D : AssetIssuer.GibiWorldPreset,
                KeyId = raw.keyId,
                Digest = raw.digest,
                Species = raw.species,
                SkeletonProfile = raw.skeletonProfile,
                ShoulderHeightM = raw.shoulderHeightM,
                TransferSizeBytes = raw.transferSizeBytes,
                MaterialCount = raw.materialCount,
                SkinnedMeshCount = raw.skinnedMeshCount,
                DeformBoneCount = raw.deformBoneCount,
                MinClientSchema = 1,
                MaxClientSchema = 1,
            };

            return await loader.LoadAsync(glb, manifest, petAssetRoot, ct);
        }

        /// <summary>
        /// RFC 8785 canonical bytes with `signature` removed. The signer excluded it, so
        /// the verifier must too — a document cannot contain its own signature.
        ///
        /// The manifest is written by our own signer with a known key set, so keys are
        /// re-serialised in sorted order with minimal separators. A general-purpose JCS
        /// implementation is unnecessary here and would be more to get wrong.
        /// </summary>
        internal static byte[] CanonicalizeExcludingSignature(string manifestJson)
        {
            var fields = new System.Collections.Generic.SortedDictionary<string, string>(
                StringComparer.Ordinal);

            foreach (var (key, value) in JsonFlattener.TopLevelFields(manifestJson))
                if (key != "signature") fields[key] = value;

            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kv in fields)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(kv.Key).Append("\":").Append(kv.Value);
            }
            sb.Append('}');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private PetLoadResult Reject(string id, string code)
        {
            _telemetry?.AssetRejected(id, 0, code);
            Debug.LogWarning($"[GibiWorld] Preset rejected: {id} -> {code}");
            return new PetLoadResult { Success = false, FailureCode = code };
        }

        // ---- StreamingAssets access (platform-dependent path handling) ----

        private static async Task<string> ReadStreamingAssetAsync(string rel, CancellationToken ct)
        {
            byte[] b = await ReadStreamingAssetBytesAsync(rel, ct);
            return b == null ? null : Encoding.UTF8.GetString(b);
        }

        private static async Task<byte[]> ReadStreamingAssetBytesAsync(string rel, CancellationToken ct)
        {
            string path = Path.Combine(Application.streamingAssetsPath, rel);

            // Android serves StreamingAssets from inside the APK, so it must go through
            // UnityWebRequest rather than File IO.
            if (path.Contains("://"))
            {
                using var req = UnityWebRequest.Get(path);
                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    if (ct.IsCancellationRequested) { req.Abort(); return null; }
                    await Task.Yield();
                }
                return req.result == UnityWebRequest.Result.Success ? req.downloadHandler.data : null;
            }

            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        private static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex) || (hex.Length & 1) != 0) return Array.Empty<byte>();
            var b = new byte[hex.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return b;
        }

        private static string BytesToHex(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 2);
            foreach (var x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        [Serializable] private class TrustedKeyDoc { public TrustedKey[] keys; }
        [Serializable] private class TrustedKey
        { public string keyId; public string algorithm; public string publicKeyHex; }

        [Serializable]
        private class RawManifest
        {
            public int schemaVersion;
            public string petAssetId;
            public int assetVersion;
            public string issuer;
            public string keyId;
            public string digest;
            public string species;
            public string skeletonProfile;
            public float shoulderHeightM;
            public long transferSizeBytes;
            public int materialCount;
            public int skinnedMeshCount;
            public int deformBoneCount;
            public string signature;
        }
    }
}
