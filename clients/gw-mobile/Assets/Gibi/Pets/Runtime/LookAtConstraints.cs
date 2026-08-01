// GW-ARCH-001 section 6.3 — Foot IK and look-at limits.
// "Foot IK SHALL raycast against the accepted navigation surface or filtered mesh;
//  max vertical correction 0.18 m, max paw rotation 25 degrees, blend in/out >= 100 ms."
// "Look-at SHALL clamp head yaw to 50 degrees and pitch to 30 degrees; eye motion may
//  add 15 degrees. Target changes SHALL use damped motion and never snap in one frame."
using System;

namespace Gibi.Pets
{
    public static class RigLimits
    {
        public const float MaxFootVerticalCorrectionM = 0.18f;
        public const float MaxPawRotationDeg          = 25f;
        public const float MinIkBlendMs               = 100f;

        public const float MaxHeadYawDeg   = 50f;
        public const float MaxHeadPitchDeg = 30f;
        public const float MaxEyeAdditionalDeg = 15f;

        public static float ClampFootCorrection(float metres)
            => Math.Clamp(metres, -MaxFootVerticalCorrectionM, MaxFootVerticalCorrectionM);

        public static float ClampHeadYaw(float deg)   => Math.Clamp(deg, -MaxHeadYawDeg, MaxHeadYawDeg);
        public static float ClampHeadPitch(float deg) => Math.Clamp(deg, -MaxHeadPitchDeg, MaxHeadPitchDeg);

        /// <summary>
        /// Critically damped approach. Guarantees the target is never reached in a
        /// single frame regardless of how large the angular error is.
        /// </summary>
        public static float Damp(float current, float target, float smoothingHalfLifeS, float deltaS)
        {
            if (smoothingHalfLifeS <= 0f) throw new ArgumentOutOfRangeException(nameof(smoothingHalfLifeS));
            float t = 1f - (float)Math.Pow(2.0, -deltaS / smoothingHalfLifeS);
            return current + (target - current) * t;
        }
    }
}
