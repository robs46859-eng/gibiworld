// GW-ARCH-001 section 4.2 — Frame update order:
//   1. read platform input and AR provider frame
//   2. update trackables and anchor quality snapshot
//   3. apply floating-origin and anchor corrections with smoothing limits
//   ... (4-6 owned by Pets/Gameplay) ...
//   8. sample telemetry AFTER presentation; telemetry must never block the frame
//
// This driver owns steps 1-3. It is the ONLY place AR Foundation types are read, so
// AnchorEligibility stays a pure deterministic machine that unit tests can drive
// without a device (section 4 dependency rule).
using Gibi.Core;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Gibi.Spatial
{
    [DisallowMultipleComponent]
    public sealed class ARSessionDriver : MonoBehaviour
    {
        [Header("Providers")]
        [SerializeField] private ARSession arSession;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private AROcclusionManager occlusionManager;

        private AnchorEligibility _eligibility;
        private IMonotonicClock _clock;

        private ARSessionState _lastLoggedState = (ARSessionState)(-1);
        private int _lastPlaneCount = -1;
        private Vector3 _lastAnchorPosition;
        private bool _hasLastAnchorPosition;
        private bool _planeVisualsVisible = true;

        /// <summary>Current per-anchor state. Read by placement, scoring, and UI.</summary>
        public AnchorState State => _eligibility?.State ?? AnchorState.Unavailable;
        public ScoringMode Scoring => _eligibility?.Scoring ?? ScoringMode.Disabled;
        public bool RunInvalidated => _eligibility?.RunInvalidated ?? false;
        public float LastPoseJumpM { get; private set; }

        /// <summary>
        /// ARCore's OWN explanation for why tracking has not established, mapped to a
        /// localisation key. Section 13.3 requires lighting confidence be part of
        /// on-device validation, and section 5.3 requires status be communicated -- so a
        /// dark room must read as coaching, not as a broken app.
        ///
        /// These are provider facts we cannot tune away: ARCore needs visual texture and
        /// contrast, and ordinary indoor evening light is often marginal. Telling the
        /// player what to change is the only useful response.
        /// </summary>
        public string NotTrackingCoachingKey => ARSession.notTrackingReason switch
        {
            NotTrackingReason.InsufficientLight    => "coach.more_light",
            NotTrackingReason.InsufficientFeatures => "coach.point_at_texture",
            NotTrackingReason.ExcessiveMotion      => "coach.move_slower",
            NotTrackingReason.Relocalizing         => "coach.relocalizing",
            NotTrackingReason.Initializing         => "coach.initializing",
            NotTrackingReason.CameraUnavailable    => "coach.camera_unavailable",
            _ => null
        };

        /// <summary>Raw reason, for diagnostics only — never shown to a player.</summary>
        public NotTrackingReason RawNotTrackingReason => ARSession.notTrackingReason;

        /// <summary>
        /// The tracked anchor this session is scoring against. Null means local play.
        /// Assigned when a VPS site anchor is resolved.
        /// </summary>
        public ARAnchor TargetSiteAnchor { get; set; }

        private void Awake()
        {
            if (arSession == null) arSession = FindAnyObjectByType<ARSession>();
            if (planeManager == null) planeManager = GetComponentInParent<ARPlaneManager>();
            if (anchorManager == null) anchorManager = GetComponentInParent<ARAnchorManager>();
            if (occlusionManager == null) occlusionManager = FindAnyObjectByType<AROcclusionManager>();

            _clock = GibiBootstrap.Services != null
                ? GibiBootstrap.Services.Resolve<IMonotonicClock>()
                : new MonotonicClock();

            _eligibility = new AnchorEligibility(_clock);
        }

        private void Update()
        {
            // --- step 1: provider frame ---
            bool sessionTracking = ARSession.state == ARSessionState.SessionTracking;

            // Diagnostic: report the PROVIDER's own state on change. Without this a
            // rejected placement is indistinguishable from a session that never started,
            // and the two have completely different fixes.
            if (ARSession.state != _lastLoggedState)
            {
                _lastLoggedState = ARSession.state;
                int planes = planeManager != null ? planeManager.trackables.count : -1;
                Debug.Log($"[GibiWorld] ARSession.state = {ARSession.state}, " +
                          $"planes = {planes}, notTrackingReason = {ARSession.notTrackingReason}");
            }

            // --- step 2: anchor quality snapshot ---
            bool usingVpsSite = TargetSiteAnchor != null;
            bool anchorTracked = usingVpsSite &&
                                 TargetSiteAnchor.trackingState == TrackingState.Tracking;

            // --- step 3: pose-correction magnitude, before smoothing is applied ---
            LastPoseJumpM = MeasurePoseJump(usingVpsSite);

            if (planeManager != null && planeManager.trackables.count != _lastPlaneCount)
            {
                _lastPlaneCount = planeManager.trackables.count;
                int horizontal = 0;
                foreach (var pl in planeManager.trackables)
                    if (pl.alignment == PlaneAlignment.HorizontalUp) horizontal++;
                Debug.Log($"[GibiWorld] planes = {_lastPlaneCount} " +
                          $"({horizontal} horizontal-up, which is what section 5.3 accepts)");

                // A provider can add planes after placement. Keep later visualizers in
                // the same state without disabling tracking or disturbing the anchor.
                if (!_planeVisualsVisible) ApplyPlaneVisualizationVisibility();
            }

            bool surfaceAccepted = HasAcceptedSurface();

            _eligibility.Tick(sessionTracking, surfaceAccepted, usingVpsSite,
                              anchorTracked, LastPoseJumpM);
        }

        /// <summary>
        /// Magnitude of this frame's correction to the tracked anchor. A jump beyond
        /// AnchorEligibility.PoseJumpThresholdM degrades the anchor even while the
        /// provider still claims Tracking, because relocalisation can snap content
        /// without the tracking state ever dropping.
        /// </summary>
        private float MeasurePoseJump(bool usingVpsSite)
        {
            if (!usingVpsSite) { _hasLastAnchorPosition = false; return 0f; }

            Vector3 p = TargetSiteAnchor.transform.position;
            if (!_hasLastAnchorPosition)
            {
                _lastAnchorPosition = p;
                _hasLastAnchorPosition = true;
                return 0f;
            }

            float jump = Vector3.Distance(p, _lastAnchorPosition);
            _lastAnchorPosition = p;
            return jump;
        }

        /// <summary>
        /// A plane counts only once its available radius is large enough for pet placement.
        /// This readiness check must use the same purpose-specific gate as the final tap.
        /// </summary>
        private bool HasAcceptedSurface()
        {
            if (planeManager == null) return false;

            foreach (var plane in planeManager.trackables)
            {
                if (plane.trackingState != TrackingState.Tracking) continue;
                if (plane.alignment != PlaneAlignment.HorizontalUp) continue;

                // ARPlane.extents are half-dimensions, so the smaller extent is already
                // the available radius. Keep readiness identical to the final tap gate.
                float clearanceRadius = Mathf.Min(plane.extents.x, plane.extents.y);
                if (SurfaceAcceptance.HasRequiredClearanceRadius(
                        clearanceRadius, PlacementPurpose.PetIdleOrTraining))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Keeps tracked planes available for AR state and raycasts while hiding only
        /// their cyan coaching mesh after the world has been placed.
        /// </summary>
        public void SetPlaneVisualizationVisible(bool visible)
        {
            _planeVisualsVisible = visible;
            ApplyPlaneVisualizationVisibility();
        }

        private void ApplyPlaneVisualizationVisibility()
        {
            if (planeManager == null) return;
            foreach (var plane in planeManager.trackables)
            {
                var meshVisualizer = plane.GetComponent<ARPlaneMeshVisualizer>();
                if (meshVisualizer != null) meshVisualizer.enabled = _planeVisualsVisible;

                var meshRenderer = plane.GetComponent<MeshRenderer>();
                if (meshRenderer != null) meshRenderer.enabled = _planeVisualsVisible;
            }
        }

        /// <summary>Section 7: reduce hidden-geometry interactions when depth is unavailable.</summary>
        public bool DepthOcclusionActive =>
            occlusionManager != null &&
            occlusionManager.currentEnvironmentDepthMode != EnvironmentDepthMode.Disabled;
    }
}
