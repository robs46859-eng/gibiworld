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

        // Clearance is PURPOSE-DEPENDENT, and conflating the two was a misreading.
        //
        // Section 5.3's clearanceRadiusM: 1.5 appears inside the SPATIAL OBJECT contract,
        // which governs course content published at a VPS site. A 1.5 m radius is 3 m of
        // clear floor -- correct for an agility gate in a park.
        //
        // Section 13.3's on-device validation for PET placement requires only "a clear
        // camera start volume, traversable floor, acceptable slope, lighting confidence,
        // and no currently detected person/vehicle intersection." It never restates 1.5 m.
        //
        // Applying the course-object figure to standing a 0.5 m dog on a living-room floor
        // made placement impossible indoors: ARCore fragments floors, so no single plane
        // ever reaches 3 m across.
        public const float MinClearanceRadiusCourseObjectM = 1.5f;
        public const float MinClearanceRadiusPetM          = 0.45f;  // ~ the pet's own footprint
        public const float MinClearanceHeightM             = 2.0f;

        /// <summary>Kept for callers that mean the course-object figure explicitly.</summary>
        public const float MinClearanceRadiusM = MinClearanceRadiusCourseObjectM;

        public static float RequiredClearanceRadius(PlacementPurpose purpose)
            => purpose == PlacementPurpose.RankedGate
                ? MinClearanceRadiusCourseObjectM
                : MinClearanceRadiusPetM;

        /// <summary>
        /// Shared by provider readiness and final placement so both gates interpret
        /// clearanceRadiusM in the same units and cannot report ready at different sizes.
        /// </summary>
        public static bool HasRequiredClearanceRadius(float clearanceRadiusM,
                                                      PlacementPurpose purpose)
            => clearanceRadiusM >= RequiredClearanceRadius(purpose);

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

            if (!HasRequiredClearanceRadius(s.ClearanceRadiusM, purpose))
                return "CLEARANCE_RADIUS";
            if (s.ClearanceHeightM < MinClearanceHeightM) return "CLEARANCE_HEIGHT";

            return null;
        }

        public static bool IsAccepted(in SurfaceSample s, PlacementPurpose purpose)
            => Reject(s, purpose) == null;
    }
}
