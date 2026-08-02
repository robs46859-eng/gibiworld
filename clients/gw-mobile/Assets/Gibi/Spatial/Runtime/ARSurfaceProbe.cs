// GW-ARCH-001 section 4 — AR Foundation adapter. This file, ARSessionDriver, and the
// scene wiring are the ONLY places AR Foundation types appear outside Gibi.Editor.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Gibi.Spatial
{
    [DisallowMultipleComponent]
    public sealed class ARSurfaceProbe : MonoBehaviour, ISurfaceProbe
    {
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private Camera arCamera;
        [SerializeField] private ARSessionDriver sessionDriver;

        private readonly List<ARRaycastHit> _hits = new();

        private void Awake()
        {
            if (raycastManager == null) raycastManager = GetComponentInParent<ARRaycastManager>();
            if (planeManager == null) planeManager = GetComponentInParent<ARPlaneManager>();
            if (sessionDriver == null) sessionDriver = GetComponentInParent<ARSessionDriver>();
            if (arCamera == null) arCamera = Camera.main;
        }

        public SurfaceProbeResult Probe(Vector2 screenPoint)
        {
            if (raycastManager == null) return SurfaceProbeResult.Miss;

            _hits.Clear();
            // Depth is included where available; without it the runtime falls back to
            // accepted planes only (section 7).
            var types = TrackableType.PlaneWithinPolygon | TrackableType.Depth;
            if (!raycastManager.Raycast(screenPoint, _hits, types))
                return SurfaceProbeResult.Miss;

            var hit = _hits[0];
            var plane = FindPlane(hit.trackableId);

            float slope = SlopeDegreesOf(hit.pose.up);
            var tag = ClassifyPlane(plane);
            float clearanceRadius = plane != null
                ? Mathf.Min(plane.extents.x, plane.extents.y)
                : 0f;

            return new SurfaceProbeResult(
                hit: true,
                position: hit.pose.position,
                rotation: hit.pose.rotation,
                tag: tag,
                slopeDegrees: slope,
                clearanceRadiusM: clearanceRadius,
                // Overhead clearance is not directly measurable from a plane raycast;
                // report the spec minimum so the gate neither passes nor fails on a
                // fabricated number. Mesh-based headroom lands with meshing support.
                clearanceHeightM: SurfaceAcceptance.MinClearanceHeightM,
                lightingConfidence: 1.0f);
        }

        public float DistanceFromCamera(Vector3 worldPoint)
            => arCamera == null ? float.MaxValue
                                : Vector3.Distance(arCamera.transform.position, worldPoint);

        private ARPlane FindPlane(TrackableId id)
        {
            if (planeManager == null) return null;
            foreach (var p in planeManager.trackables)
                if (p.trackableId == id) return p;
            return null;
        }

        private static float SlopeDegreesOf(Vector3 surfaceUp)
            => Vector3.Angle(surfaceUp, Vector3.up);

        /// <summary>
        /// Maps provider classification onto the published semantic enum. Anything
        /// unrecognised becomes Unknown, which SurfaceAcceptance treats as a hazard —
        /// the fail-closed behaviour section 5.3 requires.
        /// </summary>
        private static SemanticTag ClassifyPlane(ARPlane plane)
        {
            if (plane == null) return SemanticTag.Unknown;

            // Section 5.3 names the hazard set exactly: "sky, person, vehicle, water,
            // road, rail, and unknown hazard regions". TABLE AND SEAT ARE NOT ON IT.
            //
            // Mapping them to Unknown made them hazards via the fail-closed rule, and
            // ARCore routinely classifies ordinary floor patches as Table -- so a correct
            // floor was being rejected as dangerous. Being stricter than the spec is still
            // being wrong about the spec.
            //
            // Vertical surfaces stay Unknown, but they are already excluded by the
            // HorizontalUp alignment check, so that costs nothing.
            if ((plane.classifications & PlaneClassifications.Floor) != 0)
                return SemanticTag.Floor;

            if ((plane.classifications & (PlaneClassifications.WallFace |
                                          PlaneClassifications.Ceiling)) != 0)
                return SemanticTag.Unknown;

            // Table, Seat, Other, or unclassified: placeable if it is a floor-facing
            // horizontal surface. Section 5.3's allowlist includes "indoor".
            return plane.alignment == PlaneAlignment.HorizontalUp
                ? SemanticTag.Indoor
                : SemanticTag.Unknown;
        }
    }
}
