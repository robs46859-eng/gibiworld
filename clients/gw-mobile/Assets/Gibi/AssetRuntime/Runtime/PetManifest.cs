// GW-ARCH-001 section 6.1/6.2 — Canonical pet package manifest, client mirror.
// NORMATIVE: limits are enforced on the SERVER *and* re-enforced CLIENT-SIDE
// after parse (GW-ASSET-005). This type carries the client-side half.
using System;

namespace Gibi.AssetRuntime
{
    public enum AssetIssuer { Unknown = 0, Pawsome3D, GibiWorldPreset }

    public sealed class PetManifest
    {
        public int SchemaVersion;
        public string PetAssetId;
        public int AssetVersion;
        public AssetIssuer Issuer;
        public string KeyId;
        public string Digest;            // "sha256:<hex>"
        public string Species;
        public string SkeletonProfile;
        public float ShoulderHeightM;
        public long TransferSizeBytes;
        public int MaterialCount;
        public int SkinnedMeshCount;
        public int DeformBoneCount;
        public int MorphTargetCount;
        public int ClipCount;
        public int TotalKeyframes;
        public int Lod0Triangles, Lod0Vertices;
        public int MinClientSchema, MaxClientSchema;

        public string DigestHexOnly =>
            Digest != null && Digest.StartsWith("sha256:", StringComparison.Ordinal)
                ? Digest.Substring(7) : null;

        public bool IsClientCompatible(int clientSchema)
            => clientSchema >= MinClientSchema && clientSchema <= MaxClientSchema;
    }

    /// <summary>
    /// Section 6.2 hard limits, re-checked on device after glTFast reports actual
    /// parsed content. A manifest that CLAIMS compliant numbers but ships a heavier
    /// GLB is caught here.
    /// </summary>
    public static class AssetLimits
    {
        public const int   Lod0MaxTriangles = 35_000;
        public const int   Lod0MaxVertices  = 45_000;
        public const int   Lod1MaxTriangles = 18_000;
        public const int   Lod2MaxTriangles = 7_500;
        public const int   Lod3MaxTriangles = 2_000;
        public const int   MaxSkinnedMeshes = 2;
        public const int   MaxWeightsPerVertex = 4;
        public const int   MaxDeformBones   = 96;
        public const int   MaxMaterials     = 3;
        public const int   MaxBaseNormalTex = 2048;
        public const int   MaxOtherTex      = 1024;
        public const long  MaxDecodedTextureBytes = 50_331_648; // 48 MiB
        public const int   MaxMorphTargets  = 12;
        public const int   MaxActiveMorphs  = 4;
        public const int   MaxClips         = 48;
        public const int   MaxTotalKeyframes = 300_000;
        public const long  MaxTransferBytes = 47_185_920; // 45 MiB

        public static readonly string[] AllowedSpecies =
            { "dog", "cat", "rabbit", "guinea_pig", "ferret", "miniature_pig" };

        public static readonly string[] AllowedProfiles =
            { "GIBI_QUADRUPED_V1", "GIBI_SMALL_MAMMAL_V1" };

        public const float MinShoulderHeightM = 0.12f;
        public const float MaxShoulderHeightM = 1.10f;
        public const float MaxBoundsAnyAxisM  = 2.0f;

        public static string RejectParsed(int triangles, int vertices, int materials,
                                          int skinnedMeshes, int deformBones,
                                          int morphTargets, int clips, bool hasExternalUri,
                                          bool hasAnimationEvents, bool hasCameraOrLight)
        {
            if (hasExternalUri)      return "EXTERNAL_URI_PRESENT";      // GW-ASSET-004
            if (hasAnimationEvents)  return "ANIMATION_EVENT_PRESENT";   // GW-ASSET-006
            if (hasCameraOrLight)    return "CAMERA_OR_LIGHT_PRESENT";
            if (triangles    > Lod0MaxTriangles) return "LOD0_TRIANGLES";
            if (vertices     > Lod0MaxVertices)  return "LOD0_VERTICES";
            if (materials    > MaxMaterials)     return "MATERIAL_COUNT";
            if (skinnedMeshes> MaxSkinnedMeshes) return "SKINNED_MESH_COUNT";
            if (deformBones  > MaxDeformBones)   return "DEFORM_BONE_COUNT";
            if (morphTargets > MaxMorphTargets)  return "MORPH_TARGET_COUNT";
            if (clips        > MaxClips)         return "CLIP_COUNT";
            return null;
        }

        public static bool IsAllowedSpecies(string s) => Array.IndexOf(AllowedSpecies, s) >= 0;
        public static bool IsAllowedProfile(string s) => Array.IndexOf(AllowedProfiles, s) >= 0;
    }
}
