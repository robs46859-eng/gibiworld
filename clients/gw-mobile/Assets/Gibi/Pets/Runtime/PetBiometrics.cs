// Fatigue and hunger. Drive-only.
//
// GW-ARCH-001 section 1.2 excludes "pet injury, punishment, hunger loss" from scope.
// This implements hunger WITHOUT hunger loss, and the distinction is the whole design:
//
//   A DRIVE makes the pet want something. A PUNISHMENT makes the player pay for
//   ignoring it. This file contains drives. There is no code path here that reduces
//   bond, degrades the pet, ends a session, locks an action, or produces a failure
//   state -- and none may be added. A rising drive changes what the pet SEEKS and how
//   it MOVES. It never changes what the player is allowed to do.
//
// This is the same rule energy already follows in IntentPolicy: modulate HOW, never
// WHETHER. A pet that becomes unavailable because a meter emptied is a refusal with a
// progress bar attached, and section 1.2 excludes refusals.
//
// Deterministic and pure C#: given the same elapsed milliseconds and the same events,
// two runs produce identical values. Testable in EditMode with no device.
using System;

namespace Gibi.Pets
{
    /// <summary>
    /// Continuous internal state, 0..1. Deliberately not an enum and not a "status" --
    /// a category would invite UI that shames the player for a number.
    /// </summary>
    public readonly struct BiometricState
    {
        public readonly float Fatigue;   // 0 fresh .. 1 wants to lie down
        public readonly float Hunger;    // 0 satisfied .. 1 wants food

        public BiometricState(float fatigue, float hunger)
        { Fatigue = fatigue; Hunger = hunger; }

        /// <summary>
        /// Energy as IntentPolicy consumes it (0..100). Derived, never stored, so the two
        /// cannot drift apart. Hunger contributes only a third as much as fatigue: a
        /// hungry dog is still a lively dog.
        /// </summary>
        public int EnergyForPolicy()
        {
            float drain = Fatigue * 0.75f + Hunger * 0.25f;
            int e = (int)((1f - drain) * 100f);
            return e < 0 ? 0 : e > 100 ? 100 : e;
        }
    }

    public sealed class PetBiometrics
    {
        // Full-to-empty durations. Chosen so a single sitting never bottoms either drive:
        // a session that ends because a meter ran out is a session the app decided to end.
        public const long FatigueFullDrainMs = 45L * 60L * 1000L;   // 45 min of hard play
        public const long HungerFullDrainMs  = 6L * 60L * 60L * 1000L; // 6 h wall time
        public const long RestRecoveryMs     = 12L * 60L * 1000L;   // 12 min to fully rest

        private float _fatigue;
        private float _hunger;
        private long  _lastTickMs;
        private bool  _started;

        public PetBiometrics(float initialFatigue = 0f, float initialHunger = 0.2f)
        {
            _fatigue = Clamp01(initialFatigue);
            _hunger = Clamp01(initialHunger);
        }

        public BiometricState State => new(_fatigue, _hunger);
        public float Fatigue => _fatigue;
        public float Hunger => _hunger;

        /// <summary>
        /// Advance to <paramref name="nowMs"/> (monotonic). <paramref name="exertion"/> is
        /// 0 for standing still, 1 for sustained running.
        /// </summary>
        public void Tick(long nowMs, float exertion, bool resting)
        {
            if (!_started) { _started = true; _lastTickMs = nowMs; return; }

            long dt = nowMs - _lastTickMs;
            if (dt <= 0) return;                 // monotonic clock; never rewinds
            if (dt > 60_000L) dt = 60_000L;      // app was backgrounded: clamp, don't leap
            _lastTickMs = nowMs;

            if (resting)
            {
                _fatigue -= dt / (float)RestRecoveryMs;
            }
            else
            {
                // Idle still tires, slowly. exertion scales it up to 4x.
                float rate = 0.25f + 0.75f * Clamp01(exertion);
                _fatigue += (dt / (float)FatigueFullDrainMs) * rate * 4f;
            }

            // Hunger tracks wall time and is unaffected by resting -- sleeping through
            // dinner is a real thing and it keeps the two drives independent.
            _hunger += dt / (float)HungerFullDrainMs;

            _fatigue = Clamp01(_fatigue);
            _hunger = Clamp01(_hunger);
        }

        /// <summary>
        /// Feeding. Always accepted. There is deliberately no "not hungry enough" branch:
        /// refusing a child's offer of food is a rejection, and the pet does not reject.
        /// </summary>
        public void Feed(float amount = 0.6f)
            => _hunger = Clamp01(_hunger - Math.Abs(amount));

        /// <summary>Completing a rest at a shelter. Same rule -- always accepted.</summary>
        public void RestedFully() => _fatigue = 0f;

        /// <summary>
        /// Whether the pet WANTS to rest. Not whether it may. Nothing in the codebase is
        /// permitted to branch on this to disable an action.
        /// </summary>
        public bool SeeksRest => _fatigue >= 0.70f;
        public bool SeeksFood => _hunger >= 0.75f;

        /// <summary>
        /// Restore from server-authoritative pet_state. Clamped, so a corrupt or hostile
        /// value cannot push the pet into a state the local rules cannot express.
        /// </summary>
        public void Restore(float fatigue, float hunger)
        {
            _fatigue = Clamp01(fatigue);
            _hunger = Clamp01(hunger);
        }

        private static float Clamp01(float v)
        {
            if (float.IsNaN(v)) return 0f;
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }
    }
}
