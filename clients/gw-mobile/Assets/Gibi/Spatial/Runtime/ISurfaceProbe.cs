// GW-ARCH-001 section 4 — "No assembly may call a provider SDK directly except its
// NAMED ADAPTER." Gibi.Spatial is the AR Foundation adapter.
//
// Gibi.Gameplay needs to ask "is there a valid surface under this screen point?" but
// must not import UnityEngine.XR.ARFoundation to find out. This interface is the seam:
// Gameplay depends on Spatial's public contract, and the AR types stop here.
//
// It also makes GW-AR-006 and GW-AR-007 testable in EditMode with a fake probe, no
// device required.
using UnityEngine;

namespace Gibi.Spatial
{
    /// <summary>Provider-neutral result of probing for a placeable surface.</summary>
    public readonly struct SurfaceProbeResult
    {
        public readonly bool Hit;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly SemanticTag Tag;
        public readonly float SlopeDegrees;
        public readonly float ClearanceRadiusM;
        public readonly float ClearanceHeightM;
        public readonly float LightingConfidence;

        public SurfaceProbeResult(bool hit, Vector3 position, Quaternion rotation,
                                  SemanticTag tag, float slopeDegrees,
                                  float clearanceRadiusM, float clearanceHeightM,
                                  float lightingConfidence)
        {
            Hit = hit; Position = position; Rotation = rotation; Tag = tag;
            SlopeDegrees = slopeDegrees; ClearanceRadiusM = clearanceRadiusM;
            ClearanceHeightM = clearanceHeightM; LightingConfidence = lightingConfidence;
        }

        public static readonly SurfaceProbeResult Miss =
            new(false, Vector3.zero, Quaternion.identity, SemanticTag.Unknown,
                float.NaN, 0f, 0f, 0f);
    }

    public interface ISurfaceProbe
    {
        /// <summary>Probe for a placeable surface under a screen point.</summary>
        SurfaceProbeResult Probe(Vector2 screenPoint);

        /// <summary>Distance from the camera to a world point, for the §13.3 start-volume gate.</summary>
        float DistanceFromCamera(Vector3 worldPoint);
    }

    /// <summary>Deterministic probe for EditMode tests and recorded AR playback.</summary>
    public sealed class FakeSurfaceProbe : ISurfaceProbe
    {
        public SurfaceProbeResult NextResult = SurfaceProbeResult.Miss;
        public float CameraDistance = 2.0f;

        public SurfaceProbeResult Probe(Vector2 screenPoint) => NextResult;
        public float DistanceFromCamera(Vector3 worldPoint) => CameraDistance;
    }
}
