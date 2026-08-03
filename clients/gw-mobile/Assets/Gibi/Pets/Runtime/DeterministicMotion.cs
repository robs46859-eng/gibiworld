// GW-ARCH-001 section 4.2 step 5 — "Run navigation/motion at FixedUpdate 50 Hz;
// NEVER couple locomotion distance to render frame rate."
// GW-GAME-002: locomotion distance is deterministic across 30, 60, and 120 fps.
// Section 6.3: "All locomotion clips SHALL be in-place. The deterministic motion
// controller owns translation and yaw."
using System;

namespace Gibi.Pets
{
    public enum Gait { Idle, Walk, Trot, Run }

    /// <summary>
    /// Pure, engine-free integrator. Consumes a FIXED timestep only — there is no
    /// overload accepting Time.deltaTime, which makes frame-rate coupling a
    /// compile-time impossibility rather than a code-review convention.
    /// </summary>
    public sealed class DeterministicMotion
    {
        public const int   FixedHz = 50;
        public const float FixedDeltaS = 1f / FixedHz; // 0.02 s exactly

        // Reference speeds from the section 6.3 clip table.
        public const float WalkSpeedMps = 1.0f;
        public const float TrotSpeedMps = 2.0f;
        public const float RunSpeedMps  = 3.8f;
        public const float MaxYawRateDegPerS = 180f;

        public double DistanceTravelledM { get; private set; }
        public double HeadingDeg { get; private set; }
        public Gait CurrentGait { get; private set; } = Gait.Idle;

        private int _stepCount;

        public static float SpeedFor(Gait g) => g switch
        {
            Gait.Walk => WalkSpeedMps,
            Gait.Trot => TrotSpeedMps,
            Gait.Run  => RunSpeedMps,
            _         => 0f
        };

        public void SetGait(Gait g) => CurrentGait = g;

        /// <summary>
        /// Advance exactly one 50 Hz simulation step. Called from FixedUpdate, never
        /// from Update. Accumulates in float64 so that a long course does not drift.
        /// </summary>
        public void Step(float yawCommandDeg, bool advance = true)
        {
            _stepCount++;

            double yawDelta = Math.Clamp(yawCommandDeg, -MaxYawRateDegPerS, MaxYawRateDegPerS) * FixedDeltaS;
            HeadingDeg = Wrap360(HeadingDeg + yawDelta);

            if (advance)
                DistanceTravelledM += (double)SpeedFor(CurrentGait) * FixedDeltaS;
        }

        /// <summary>Steps consumed so far — used by the frame-rate variance test.</summary>
        public int StepCount => _stepCount;

        public void Reset() { DistanceTravelledM = 0; HeadingDeg = 0; _stepCount = 0; CurrentGait = Gait.Idle; }

        private static double Wrap360(double d)
        {
            d %= 360.0;
            return d < 0 ? d + 360.0 : d;
        }
    }

    /// <summary>
    /// Converts variable render time into a whole number of fixed steps, carrying the
    /// remainder. This is the ONLY bridge between render time and simulation time.
    /// </summary>
    public sealed class FixedStepAccumulator
    {
        private double _accumulator;
        public const int MaxStepsPerFrame = 5; // spiral-of-death guard

        public int Consume(double frameDeltaSeconds)
        {
            _accumulator += frameDeltaSeconds;
            int steps = 0;
            while (_accumulator >= DeterministicMotion.FixedDeltaS && steps < MaxStepsPerFrame)
            {
                _accumulator -= DeterministicMotion.FixedDeltaS;
                steps++;
            }
            return steps;
        }
    }
}
