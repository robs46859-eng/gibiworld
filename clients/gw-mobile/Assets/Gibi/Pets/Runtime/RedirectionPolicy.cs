// GW-ARCH-001 section 8.1 (proposed amendment). Positive redirection.
//
// Sits at priority 3.5 -- above the needs scheduler, below direct player cues. It can
// never interrupt a child mid-action and can never override safety, but it outranks AI
// intent, so a redirection always beats a model suggestion.
//
// THE THREE HARD RULES:
//   1. Always TOWARD, never away. Redirection proposes an activity; it never removes one.
//   2. Never shame. No output implies the child did anything wrong.
//   3. Never a substitute for a person. The pet does not counsel, does not claim to
//      understand feelings, and never positions itself as someone to talk to instead of
//      a human.
//
// Everything here is in-fiction. The pet does something. There is no modal, no banner,
// no timer, and no question directed at the child.
using Gibi.Core;

namespace Gibi.Pets
{
    public enum RedirectionKind
    {
        None,
        AddFlourish,      // same action, small variation, keeps repetition alive
        OfferAlongside,   // bring a toy WHILE remaining willing to repeat
        SettleNearby,     // lie down close; comfort widens, never withdraws
        DriftToRest,      // sleepy, in-fiction only
    }

    public readonly struct Redirection
    {
        public readonly RedirectionKind Kind;
        public readonly string ActionKey;
        public readonly long MaxDurationMs;
        /// <summary>False for every outward offer: the pet must stay available.</summary>
        public readonly bool ReplacesCurrentActivity;

        public Redirection(RedirectionKind kind, string actionKey, long maxDurationMs)
        { Kind = kind; ActionKey = actionKey; MaxDurationMs = maxDurationMs;
          ReplacesCurrentActivity = false; }

        public static readonly Redirection None =
            new(RedirectionKind.None, null, 0);
    }

    public sealed class RedirectionPolicy
    {
        // Repetition thresholds add TEXTURE. They are not caps -- nothing here can refuse.
        private const float PerseverationFlourishAt = 0.55f;
        private const float PerseverationOfferAt    = 0.80f;
        private const long  MinOfferIntervalMs      = 120_000; // never nag

        private readonly IMonotonicClock _clock;
        // NOT long.MinValue: `now - long.MinValue` overflows silently in C#'s unchecked
        // context and yields a negative result, so the rate-limit test would never pass
        // and the FIRST offer could never fire. Offset by one interval instead, which
        // makes the first evaluation eligible and every subsequent one correctly gated.
        private long _lastOfferMs = -(MinOfferIntervalMs + 1);

        /// <summary>Session-scoped counters. Never surfaced, never gamified, never persisted.</summary>
        public int FlourishCount { get; private set; }
        public int OfferCount { get; private set; }
        public int SettleCount { get; private set; }

        public RedirectionPolicy(IMonotonicClock clock) { _clock = clock; }

        /// <summary>
        /// Decide whether the pet should offer something. Returns None most of the time —
        /// that is the correct and common outcome.
        /// </summary>
        public Redirection Evaluate(in EngagementEstimate estimate, in CareParameters care)
        {
            long now = _clock.ElapsedMilliseconds;

            // --- Stillness with the pet close: the most delicate case. ---
            // A child being quietly comforted is not doing anything wrong, and abrupt
            // withdrawal at exactly that moment is the cruelest possible response.
            // The pet STAYS. It softens over minutes; it does not leave.
            if (estimate.Settling > 0.6f)
            {
                SettleCount++;
                return new Redirection(RedirectionKind.SettleNearby, "SETTLE_CLOSE", 60_000);
            }

            // --- Repetition: add texture, never extinguish. ---
            // Repetitive play is often self-regulation. The system cannot distinguish
            // joyful, regulating, and distressed repetition through a touchscreen, so the
            // response is identical for all three and safe for the one where interruption
            // would do harm. See docs/design section 6a.
            if (estimate.Perseveration >= PerseverationOfferAt &&
                now - _lastOfferMs > MinOfferIntervalMs)
            {
                _lastOfferMs = now;
                OfferCount++;
                // OfferAlongside: the ball appears NEXT TO the repetition, not instead of
                // it. If the child ignores it and asks again, they are answered again.
                return new Redirection(RedirectionKind.OfferAlongside, "BRING_TOY", 6_000);
            }

            if (estimate.Perseveration >= PerseverationFlourishAt)
            {
                FlourishCount++;
                // Micro-variation keeps the loop alive without breaking it. The hundredth
                // jump happens; it just is not byte-identical to the first.
                return new Redirection(RedirectionKind.AddFlourish, "FLOURISH", 1_200);
            }

            // --- Fatigue: expressive, never restrictive. ---
            // Section 1.2 excludes punishment and injury. A pet "too tired to continue"
            // is a refusal in costume, so tiredness changes HOW the pet responds, never
            // WHETHER it responds.
            long restAfter = care.RestCueAfterMs;
            if (estimate.Fatigue > 0.75f && now > restAfter &&
                now - _lastOfferMs > MinOfferIntervalMs)
            {
                _lastOfferMs = now;
                return new Redirection(RedirectionKind.DriftToRest, "DRIFT_TO_REST", 20_000);
            }

            return Redirection.None;
        }

        /// <summary>
        /// Section 6.3 blend scaling under a care profile. Applied to every transition so
        /// the pet reads calmer without any behaviour becoming unavailable.
        /// </summary>
        public static float ScaledBlendMs(float baseBlendMs, in CareParameters care)
            => baseBlendMs * care.BlendMultiplier;

        /// <summary>
        /// Gait ceiling from the care profile. Caps SPEED, never availability — the pet
        /// still goes; it goes gently.
        /// </summary>
        public static Gait CapGait(Gait requested, in CareParameters care)
            => (int)requested > (int)care.MaxGait ? care.MaxGait : requested;
    }
}
