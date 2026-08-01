// GW-ARCH-001 section 13.3 — Player safety gates.
// "AR interactions SHALL pause when speed exceeds 4.5 m/s for 10 seconds. Map
//  navigation may continue in passenger-safe mode, but no collection, placement,
//  training, or course actions are enabled."
// "The app SHALL remind players that computer vision and map data are imperfect and
//  surroundings outrank game prompts."
using Gibi.Core;

namespace Gibi.Gameplay
{
    public enum SafetyMode { Normal, PassengerSafe }

    public sealed class PlayerSafetyGate
    {
        public const float SpeedThresholdMps = 4.5f;
        public const long  SustainedMs       = 10_000;
        public const long  ReleaseHysteresisMs = 3_000;

        private readonly IMonotonicClock _clock;
        private long _aboveSinceMs = -1;
        private long _belowSinceMs = -1;

        public SafetyMode Mode { get; private set; } = SafetyMode.Normal;

        public PlayerSafetyGate(IMonotonicClock clock) { _clock = clock; }

        public SafetyMode Tick(float speedMps)
        {
            long now = _clock.ElapsedMilliseconds;

            if (speedMps > SpeedThresholdMps)
            {
                _belowSinceMs = -1;
                if (_aboveSinceMs < 0) _aboveSinceMs = now;
                if (now - _aboveSinceMs >= SustainedMs) Mode = SafetyMode.PassengerSafe;
            }
            else
            {
                _aboveSinceMs = -1;
                if (_belowSinceMs < 0) _belowSinceMs = now;
                // Hysteresis prevents flicker at a stoplight or in stop-and-go traffic.
                if (now - _belowSinceMs >= ReleaseHysteresisMs) Mode = SafetyMode.Normal;
            }
            return Mode;
        }

        /// <summary>All of these are disabled in passenger-safe mode.</summary>
        public bool AllowsCollection => Mode == SafetyMode.Normal;
        public bool AllowsPlacement  => Mode == SafetyMode.Normal;
        public bool AllowsTraining   => Mode == SafetyMode.Normal;
        public bool AllowsCourseRun  => Mode == SafetyMode.Normal;
        /// <summary>Map browsing remains available so a passenger is not stranded.</summary>
        public bool AllowsMapNavigation => true;
    }

    /// <summary>
    /// Section 13.3 course publication setbacks. Enforced server-side at publication
    /// and re-checked client-side before a ranked run starts.
    /// </summary>
    public static class SitePublicationRules
    {
        public const double MinRoadCentrelineSetbackM = 10.0;
        public const double MinRailCorridorSetbackM   = 10.0;
        public const double CameraStartVolumeClearanceM = 1.5;

        public static string Reject(bool insideApprovedPolygon, bool insideExclusionPolygon,
                                    double metresToRoadCentreline, double metresToRailCorridor,
                                    double metresToNearestObstacleFromCameraStart)
        {
            // Fail closed: absence of an approving polygon is a rejection, not a pass.
            if (!insideApprovedPolygon) return "OUTSIDE_APPROVED_SITE_POLYGON";
            if (insideExclusionPolygon) return "INSIDE_EXCLUSION_POLYGON";
            if (metresToRoadCentreline < MinRoadCentrelineSetbackM) return "ROAD_SETBACK";
            if (metresToRailCorridor   < MinRailCorridorSetbackM)   return "RAIL_SETBACK";
            if (metresToNearestObstacleFromCameraStart < CameraStartVolumeClearanceM)
                return "CAMERA_START_VOLUME_CLEARANCE";
            return null;
        }
    }
}
