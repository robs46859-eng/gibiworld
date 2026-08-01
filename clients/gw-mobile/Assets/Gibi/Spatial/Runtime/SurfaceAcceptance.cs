// GW-ARCH-001 section 5.3 — Surface and hazard gates.
// GW-AR-006: hazard semantic regions reject destinations and obstacles.
// GW-AR-007: slope and clearance gates match published thresholds.
// NORMATIVE: "The runtime SHALL block sky, person, vehicle, water, road, rail, and
// unknown hazard regions from pet destination and obstacle placement."
using System;

namespace Gibi.Spatial
{
    public enum SemanticTag
    {
        Ground, Outdoor, Indoor, Path, Grass, Floor, Sand, Gravel, Pavement,
        // Hazard classes — never valid placement targets.
        Sky, Person, Vehicle, Water, Road, Rail, Unknown
    }

    public enum PlacementPurpose { PetIdleOrTraining, RankedGate }

    public readonly struct SurfaceSample
    {
        public readonly SemanticTag Tag;
        public readonly float SlopeDegrees;
        public readonly float ClearanceRadiusM;
        public readonly float ClearanceHeightM;
        public readonly bool  PermitsRamp;

        public SurfaceSample(SemanticTag tag, float slopeDegrees,
                             float clearanceRadiusM, float clearanceHeightM,
                             bool permitsRamp = false)
        { Tag = tag; SlopeDegrees = slopeDegrees; ClearanceRadiusM = clearanceRadiusM;
          ClearanceHeightM = clearanceHeightM; PermitsRamp = permitsRamp; }
    }

    public static class SurfaceAcceptance
    {
        public const float MaxSlopeIdleTrainingDeg = 12f;
        public const float MaxSlopeRankedGateDeg   = 7f;
        public const float MinClearanceRadiusM     = 1.5f;
        public const float MinClearanceHeightM     = 2.0f;

        /// <summary>
        /// Hazard set is an explicit allowlist inverse: anything not in the safe set is
        /// treated as hazardous, so a newly added provider tag fails CLOSED.
        /// </summary>
        public static bool IsHazard(SemanticTag tag) => tag switch
        {
            SemanticTag.Ground or SemanticTag.Outdoor or SemanticTag.Indoor or
            SemanticTag.Path   or SemanticTag.Grass   or SemanticTag.Floor  or
            SemanticTag.Sand   or SemanticTag.Gravel  or SemanticTag.Pavement => false,
            _ => true
        };

        /// <summary>Returns null when accepted, otherwise a stable rejection code.</summary>
        public static string Reject(in SurfaceSample s, PlacementPurpose purpose)
        {
            if (IsHazard(s.Tag)) return $"HAZARD_{s.Tag.ToString().ToUpperInvariant()}";

            if (float.IsNaN(s.SlopeDegrees)) return "SLOPE_UNKNOWN";

            float maxSlope = purpose == PlacementPurpose.RankedGate
                ? MaxSlopeRankedGateDeg
                : MaxSlopeIdleTrainingDeg;

            // "unless the obstacle profile explicitly permits a ramp"
            if (!s.PermitsRamp && s.SlopeDegrees > maxSlope) return "SLOPE_EXCEEDED";

            if (s.ClearanceRadiusM < MinClearanceRadiusM) return "CLEARANCE_RADIUS";
            if (s.ClearanceHeightM < MinClearanceHeightM) return "CLEARANCE_HEIGHT";

            return null;
        }

        public static bool IsAccepted(in SurfaceSample s, PlacementPurpose purpose)
            => Reject(s, purpose) == null;
    }
}
