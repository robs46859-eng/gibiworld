// GW-ARCH-001 sections 5.2, 5.3, 13.3 — placement gating.
//
// "The user-facing placement ring SHALL encode status with color, icon, label, and
//  optional haptic; COLOR ALONE IS INSUFFICIENT." (section 5.3)
// "On-device validation SHALL require a clear camera start volume, traversable floor,
//  acceptable slope, lighting confidence, and no currently detected person/vehicle
//  intersection." (section 13.3)
//
// NOTE: this file imports NO provider SDK. Section 4 permits only Gibi.Spatial to touch
// AR Foundation, so surfaces arrive through ISurfaceProbe. That seam is also what lets
// GW-AR-006 and GW-AR-007 run in EditMode against FakeSurfaceProbe with no device.
using Gibi.Core;
using Gibi.Spatial;
using UnityEngine;

namespace Gibi.Gameplay
{
    /// <summary>Everything the UI needs to render placement status accessibly.</summary>
    public readonly struct PlacementStatus
    {
        public readonly bool CanPlace;
        public readonly string RejectionCode;   // null when placeable
        public readonly string LocalizationKey; // section 14: no concatenated sentences
        public readonly string IconId;          // shape channel, independent of colour
        public readonly Color RingColor;
        public readonly bool ShouldPulseHaptic;

        public PlacementStatus(bool canPlace, string rejection, string locKey,
                               string iconId, Color color, bool haptic)
        {
            CanPlace = canPlace; RejectionCode = rejection; LocalizationKey = locKey;
            IconId = iconId; RingColor = color; ShouldPulseHaptic = haptic;
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlacementController : MonoBehaviour
    {
        public const float CameraStartVolumeClearanceM = 1.5f;
        public const float MinLightingConfidence = 0.35f;

        // Section 5.3: colour is one channel of four. Each state also carries a distinct
        // icon and a localisation key, so the status is legible without colour vision.
        private static readonly Color ColorReady   = new(0.20f, 0.75f, 0.35f);
        private static readonly Color ColorCaution = new(0.85f, 0.65f, 0.10f);
        private static readonly Color ColorBlocked = new(0.80f, 0.20f, 0.20f);
        private static readonly Color ColorNeutral = new(0.55f, 0.55f, 0.55f);

        private ISurfaceProbe _probe;
        private ARSessionDriver _sessionDriver;
        private PlayerSafetyGate _safetyGate;

        public PlacementStatus Status { get; private set; }
        public Pose CandidatePose { get; private set; }

        /// <summary>
        /// Where the probe last struck a surface, REGARDLESS of whether placement was
        /// allowed. CandidatePose only updates on success, so a ring driven by it stays
        /// frozen at world origin during rejection -- which reads as a giant disc on the
        /// floor beneath the player rather than as feedback.
        /// </summary>
        public Pose LastHitPose { get; private set; }
        public bool HasHit { get; private set; }

        private void Awake()
        {
            // GetComponentInParent only walks UP. In the generated scene SessionDriver is
            // a SIBLING of this object under the XR Origin, so the parent walk finds
            // nothing and the null-guard below silently reports AR_UNAVAILABLE forever --
            // even while the driver is reporting SessionTracking with planes detected.
            // Scene-wide lookup is the correct fallback for a code-built hierarchy.
            _probe = GetComponentInParent<ARSurfaceProbe>()
                     ?? FindAnyObjectByType<ARSurfaceProbe>();
            _sessionDriver = GetComponentInParent<ARSessionDriver>()
                             ?? FindAnyObjectByType<ARSessionDriver>();

            if (_sessionDriver == null)
                Debug.LogError("[GibiWorld] No ARSessionDriver found — placement will " +
                               "report AR_UNAVAILABLE regardless of actual session state.");
            if (_probe == null)
                Debug.LogError("[GibiWorld] No ARSurfaceProbe found — no surface can ever be hit.");

            var clock = GibiBootstrap.Services != null
                ? GibiBootstrap.Services.Resolve<IMonotonicClock>()
                : new MonotonicClock();
            _safetyGate = new PlayerSafetyGate(clock);
        }

        /// <summary>Test seam — inject a FakeSurfaceProbe in EditMode.</summary>
        public void ConfigureForTest(ISurfaceProbe probe, IMonotonicClock clock)
        {
            _probe = probe;
            _safetyGate = new PlayerSafetyGate(clock);
        }

        /// <param name="playerSpeedMps">Device speed; section 13.3 pauses AR above 4.5 m/s.</param>
        public PlacementStatus Evaluate(Vector2 screenPoint, float playerSpeedMps)
        {
            // --- section 13.3: motion gate outranks everything else ---
            if (_safetyGate.Tick(playerSpeedMps) == SafetyMode.PassengerSafe)
                return Set(Reject("PASSENGER_SAFE_MODE", "placement.blocked.moving",
                                  "icon.motion", ColorCaution, haptic: true));

            // --- section 5.2: anchor state gate ---
            var anchorState = _sessionDriver != null ? _sessionDriver.State : AnchorState.Unavailable;
            switch (anchorState)
            {
                case AnchorState.Unavailable:
                    return Set(Reject("AR_UNAVAILABLE", "placement.blocked.no_session",
                                      "icon.no_camera", ColorNeutral, haptic: false));
                case AnchorState.Scanning:
                    return Set(Reject("SCANNING", "placement.scanning",
                                      "icon.scan", ColorNeutral, haptic: false));
                case AnchorState.VpsLimited:
                case AnchorState.Degraded:
                    return Set(Reject("ANCHOR_NOT_TRACKED", "placement.blocked.relocalize",
                                      "icon.relocalize", ColorCaution, haptic: true));
            }

            // --- surface probe, through the adapter seam ---
            var probe = _probe?.Probe(screenPoint) ?? SurfaceProbeResult.Miss;
            HasHit = probe.Hit;
            if (probe.Hit) LastHitPose = new Pose(probe.Position, probe.Rotation);

            if (!probe.Hit)
                return Set(Reject("NO_SURFACE", "placement.blocked.no_surface",
                                  "icon.no_surface", ColorNeutral, haptic: false));

            // --- section 13.3: clear camera start volume ---
            if (_probe.DistanceFromCamera(probe.Position) < CameraStartVolumeClearanceM)
                return Set(Reject("TOO_CLOSE", "placement.blocked.too_close",
                                  "icon.too_close", ColorCaution, haptic: true));

            // --- section 13.3: lighting confidence ---
            if (probe.LightingConfidence < MinLightingConfidence)
                return Set(Reject("LOW_LIGHT", "placement.blocked.low_light",
                                  "icon.low_light", ColorCaution, haptic: false));

            // --- sections 5.3 / 13.3: hazard, slope, clearance. Fails closed. ---
            var sample = new SurfaceSample(probe.Tag, probe.SlopeDegrees,
                                           probe.ClearanceRadiusM, probe.ClearanceHeightM);
            string reject = SurfaceAcceptance.Reject(sample, PlacementPurpose.PetIdleOrTraining);
            if (reject != null)
                return Set(Reject(reject, "placement.blocked.unsafe_surface",
                                  "icon.unsafe", ColorBlocked, haptic: true));

            CandidatePose = new Pose(probe.Position, probe.Rotation);
            return Set(new PlacementStatus(
                canPlace: true, rejection: null,
                locKey: anchorState == AnchorState.LocalReady
                    ? "placement.ready.local"     // section 5.2: LOCAL badge, practice only
                    : "placement.ready.tracked",
                iconId: "icon.ready",
                color: ColorReady,
                haptic: true));
        }

        private PlacementStatus Set(PlacementStatus s) { Status = s; return s; }

        private static PlacementStatus Reject(string code, string locKey, string icon,
                                              Color color, bool haptic)
            => new(false, code, locKey, icon, color, haptic);

        /// <summary>Section 5.2: only a tracked VPS site anchor may persist placement.</summary>
        public bool MayPersist => _sessionDriver != null &&
                                  _sessionDriver.State == AnchorState.VpsTracked;
    }
}
