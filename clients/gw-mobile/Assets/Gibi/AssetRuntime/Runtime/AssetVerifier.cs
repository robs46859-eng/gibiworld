// GW-ARCH-001 section 6.4 — Runtime verification algorithm (8 steps, in order).
// GW-ASSET-001 unknown issuer/key/schema/species/rig rejects BEFORE render.
// GW-ASSET-002 digest mismatch deletes temporary bytes and does NOT retry the URL.
// GW-ASSET-003 expired or revoked entitlement blocks instantiation.
// GW-ASSET-007 cache promotion is atomic and keyed by SHA-256 digest.
// GW-ASSET-008 preset and Pawsome3D assets use the SAME runtime verifier.
// GW-API-005  tokens and signed URLs never appear in logs or crash payloads.
//
// NORMATIVE (section 0): "A pet SHALL render only if its immutable manifest names
// issuer PAWSOME3D or GIBIWORLD_PRESET, the Ed25519 signature verifies against a
// pinned trusted key, the SHA-256 digest matches the downloaded GLB, and the
// authenticated user has a current entitlement."
using System;
using System.Threading;
using System.Threading.Tasks;
using Gibi.Core;

namespace Gibi.AssetRuntime
{
    public enum VerificationFailure
    {
        None,
        SchemaVersionUnknown,
        IssuerUntrusted,
        KeyIdUnknown,
        SignatureInvalid,
        CompatibilityOutOfRange,
        EntitlementNotActive,
        EntitlementVersionMismatch,
        SizeLimitExceeded,
        ContentLengthMismatch,
        DigestMismatch,
        ExternalUriPresent,
        GeometryLimitExceeded,
        SkeletonProfileInvalid,
        AnimationEventPresent
    }

    public interface ISignatureVerifier
    {
        /// <summary>Ed25519 verify over RFC 8785 canonical manifest bytes (signature excluded).</summary>
        bool Verify(ReadOnlySpan<byte> canonicalManifest, ReadOnlySpan<byte> signature, string keyId);
        bool IsPinnedKey(string keyId);
    }

    public interface IEntitlementGate
    {
        bool IsActive(string petAssetId, int assetVersion);
    }

    public interface IQuarantineCache
    {
        Task<string> BeginTempAsync(string tempKey, CancellationToken ct);
        Task DeleteTempAsync(string tempKey);
        /// <summary>Atomic rename into the digest-keyed slot. Crash-safe (GW-ASSET-007).</summary>
        Task<bool> PromoteAtomicAsync(string tempKey, string digest);
        bool TryGetByDigest(string digest, out string path);
    }

    public sealed class AssetVerifier
    {
        public const long MaxTransferBytes = 47_185_920; // 45 MiB hard limit
        public const int  SupportedSchemaVersion = 1;

        private readonly ISignatureVerifier _sig;
        private readonly IEntitlementGate _entitlement;
        private readonly IQuarantineCache _cache;
        private readonly IAssetTelemetry _telemetry;

        public AssetVerifier(ISignatureVerifier sig, IEntitlementGate entitlement,
                             IQuarantineCache cache, IAssetTelemetry telemetry)
        { _sig = sig; _entitlement = entitlement; _cache = cache; _telemetry = telemetry; }

        /// <summary>
        /// Steps 1-5 of section 6.4. Steps 6-7 (glTFast parse under a no-external-URI
        /// import policy, material replacement, atomic promotion) are driven by
        /// PetAssetLoader once this gate returns Ok.
        /// </summary>
        public async Task<Result<string>> VerifyAsync(PetManifest manifest,
                                                      byte[] canonicalManifestBytes,
                                                      byte[] signature,
                                                      IAssetStream stream,
                                                      CancellationToken ct)
        {
            // --- Step 1: reject unknown schema, issuer, key, version, compatibility ---
            if (manifest.SchemaVersion != SupportedSchemaVersion)
                return Fail(VerificationFailure.SchemaVersionUnknown, manifest);

            if (manifest.Issuer != AssetIssuer.Pawsome3D && manifest.Issuer != AssetIssuer.GibiWorldPreset)
                return Fail(VerificationFailure.IssuerUntrusted, manifest);

            if (!_sig.IsPinnedKey(manifest.KeyId))
                return Fail(VerificationFailure.KeyIdUnknown, manifest);

            if (!manifest.IsClientCompatible(SupportedSchemaVersion))
                return Fail(VerificationFailure.CompatibilityOutOfRange, manifest);

            // --- Step 2: Ed25519 over canonical manifest bytes ---
            if (!_sig.Verify(canonicalManifestBytes, signature, manifest.KeyId))
                return Fail(VerificationFailure.SignatureInvalid, manifest);

            // --- Step 3: entitlement must be ACTIVE for this exact id + version ---
            if (!_entitlement.IsActive(manifest.PetAssetId, manifest.AssetVersion))
                return Fail(VerificationFailure.EntitlementNotActive, manifest);

            // Cache hit by digest short-circuits the download entirely.
            if (_cache.TryGetByDigest(manifest.Digest, out string cached))
                return Result<string>.Ok(cached);

            // --- Step 4: stream to temp, hashing during download, enforcing limits ---
            if (manifest.TransferSizeBytes > MaxTransferBytes)
                return Fail(VerificationFailure.SizeLimitExceeded, manifest);

            string tempKey = Guid.NewGuid().ToString("N");
            await _cache.BeginTempAsync(tempKey, ct).ConfigureAwait(false);

            StreamResult dl;
            try
            {
                dl = await stream.DownloadHashingAsync(tempKey, MaxTransferBytes, ct).ConfigureAwait(false);
            }
            catch
            {
                await _cache.DeleteTempAsync(tempKey).ConfigureAwait(false);
                throw;
            }

            if (dl.DeclaredContentLength != dl.BytesWritten)
            {
                await _cache.DeleteTempAsync(tempKey).ConfigureAwait(false);
                return Fail(VerificationFailure.ContentLengthMismatch, manifest);
            }

            // --- Step 5: constant-time digest comparison ---
            if (!ConstantTimeEquals(dl.Sha256Hex, manifest.DigestHexOnly))
            {
                await _cache.DeleteTempAsync(tempKey).ConfigureAwait(false);
                _telemetry.AssetIntegrityFailed(manifest.PetAssetId, manifest.AssetVersion);
                // "disable automatic retry for that URL" — surfaced as non-retryable.
                return Result<string>.Fail(nameof(VerificationFailure.DigestMismatch));
            }

            // --- Step 7 (promotion half): atomic, digest-keyed ---
            await _cache.PromoteAtomicAsync(tempKey, manifest.Digest).ConfigureAwait(false);
            _cache.TryGetByDigest(manifest.Digest, out string promoted);
            return Result<string>.Ok(promoted);
        }

        private Result<string> Fail(VerificationFailure f, PetManifest m)
        {
            // Step 8: never log signed URLs, tokens, or authorization headers.
            _telemetry.AssetRejected(m.PetAssetId, m.AssetVersion, f.ToString());
            return Result<string>.Fail(f.ToString());
        }

        /// <summary>
        /// Length-independent, data-independent comparison. Prevents a timing oracle on
        /// the expected digest.
        /// </summary>
        public static bool ConstantTimeEquals(string a, string b)
        {
            if (a is null || b is null) return false;
            int diff = a.Length ^ b.Length;
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }

    public readonly struct StreamResult
    {
        public readonly string Sha256Hex;
        public readonly long BytesWritten;
        public readonly long DeclaredContentLength;
        public StreamResult(string sha256Hex, long bytesWritten, long declaredContentLength)
        { Sha256Hex = sha256Hex; BytesWritten = bytesWritten; DeclaredContentLength = declaredContentLength; }
    }

    public interface IAssetStream
    {
        Task<StreamResult> DownloadHashingAsync(string tempKey, long hardLimitBytes, CancellationToken ct);
    }

    public interface IAssetTelemetry
    {
        void AssetIntegrityFailed(string petAssetId, int version);
        void AssetRejected(string petAssetId, int version, string failureCode);
    }
}
