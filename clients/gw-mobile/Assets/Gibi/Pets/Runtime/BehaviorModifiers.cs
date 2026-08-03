// GW-ARCH-002 section 7, catalog revision 2.
//
// THE EXPRESSIVE LAYER. Step 6 chose architecture B, which means this file -- not a model
// -- is where the pet's sense of life is produced.
//
// The claim being implemented: variance in TIMING reads as interiority; variance in
// CATEGORY reads as randomness. A pet that pauses before coming looks like it decided
// something. A pet that picks a different action every time looks broken. So latency is
// the highest-value field here and it is derived from state, never randomised for its
// own sake.
//
// Pure C#. No UnityEngine reference, so every rule below is EditMode-testable.
using System;

namespace Gibi.Pets
{
    public enum Approach { Direct, ArcLeft, ArcRight, Hesitant }
    public enum Persistence { Once, UntilInterrupted }

    /// <summary>
    /// How an intent is performed. Always produced through <see cref="Clamp"/>, never
    /// constructed raw at a call site -- the clamp is what guarantees an out-of-range
    /// value is narrowed rather than honoured.
    /// </summary>
    public readonly struct BehaviorModifiers
    {
        public readonly float       Intensity;      // 0..1, amplitude -- not speed
        public readonly int         LatencyMs;      // 0..2000, hesitation before acting
        public readonly float       DurationScale;  // 0.5..2.0
        public readonly Approach    Approach;
        public readonly Persistence Persistence;

        public const float MinIntensity = 0.0f, MaxIntensity = 1.0f;
        public const int   MinLatencyMs = 0,    MaxLatencyMs = 2000;
        public const float MinDuration  = 0.5f, MaxDuration  = 2.0f;

        private BehaviorModifiers(float intensity, int latencyMs, float durationScale,
                                  Approach approach, Persistence persistence)
        {
            Intensity = intensity; LatencyMs = latencyMs; DurationScale = durationScale;
            Approach = approach; Persistence = persistence;
        }

        /// <summary>
        /// The only constructor. Clamps to the global range, then to the intent's own cap,
        /// then to the care profile's cap. Each stage may only narrow.
        /// </summary>
        public static BehaviorModifiers Clamp(in IntentDef def, in CareModifierLimits care,
                                              float intensity, int latencyMs, float durationScale,
                                              Approach approach, Persistence persistence)
        {
            float i = ClampRange(intensity, MinIntensity, MaxIntensity);
            i = Math.Min(i, def.MaxIntensity);          // per-intent cap: SETTLE cannot be intense
            i = Math.Min(i, care.MaxIntensity);         // care cap: only ever narrows

            int l = latencyMs < MinLatencyMs ? MinLatencyMs
                  : latencyMs > MaxLatencyMs ? MaxLatencyMs : latencyMs;
            l = Math.Max(l, care.MinLatencyMs);         // gentler profiles slow the onset

            float d = ClampRange(durationScale, MinDuration, MaxDuration);
            d = Math.Max(d, care.MinDurationScale);     // longer, softer transitions
            d = Math.Min(d, MaxDuration);               // care floor may not exceed the ceiling

            // A hesitant approach with near-zero latency is a contradiction the animator
            // cannot express. Resolve it here rather than letting it reach the rig.
            if (approach == Approach.Hesitant && l < 200) l = 200;

            return new BehaviorModifiers(i, l, d, approach, persistence);
        }

        private static float ClampRange(float v, float lo, float hi)
        {
            if (float.IsNaN(v)) return lo;              // NaN fails to the calmest value
            return v < lo ? lo : v > hi ? hi : v;
        }

        /// <summary>Effective action duration after scaling, still bounded by the intent cap.</summary>
        public int EffectiveDurationMs(in IntentDef def)
        {
            long scaled = (long)(def.MaxDurationMs * DurationScale);
            long ceiling = def.MaxDurationMs * 2L;
            return (int)(scaled > ceiling ? ceiling : scaled);
        }
    }

    /// <summary>
    /// Care-profile caps on the modifier layer. Separate from <see cref="CareParameters"/>
    /// so that type keeps its existing shape and callers.
    ///
    /// Composition rule: MOST CONSERVATIVE WINS. Adding a profile can only calm the pet.
    /// There is deliberately no path by which a profile raises a cap -- a guardian setting
    /// that made the pet more intense would be a footgun aimed at the child it was meant
    /// to accommodate.
    /// </summary>
    public readonly struct CareModifierLimits
    {
        public readonly float MaxIntensity;
        public readonly int   MinLatencyMs;
        public readonly float MinDurationScale;

        // 64-bit mask, one bit per catalog index. long rather than int deliberately: the
        // catalog is 29 entries and an int would silently stop working at 33. Appending an
        // intent is meant to be safe.
        private readonly long _removedMask;

        public const int MaxMaskableIntents = 64;

        private CareModifierLimits(float maxIntensity, int minLatencyMs,
                                   float minDurationScale, long removedMask)
        {
            MaxIntensity = maxIntensity; MinLatencyMs = minLatencyMs;
            MinDurationScale = minDurationScale; _removedMask = removedMask;
        }

        public static CareModifierLimits Default =>
            new(maxIntensity: 1.0f, minLatencyMs: 0, minDurationScale: 1.0f, removedMask: 0L);

        public static CareModifierLimits Resolve(CareProfile profile)
        {
            float maxIntensity = 1.0f;
            int   minLatency   = 0;
            float minDuration  = 1.0f;
            long  removed      = 0L;

            if (profile.HasFlag(CareProfile.GentleSensory))
            {
                maxIntensity = Math.Min(maxIntensity, 0.5f);
                minLatency   = Math.Max(minLatency, 250);
                minDuration  = Math.Max(minDuration, 1.2f);
                removed |= Bit("SHAKE_OFF") | Bit("NUDGE_OBJECT");
            }
            if (profile.HasFlag(CareProfile.Predictable))
            {
                maxIntensity = Math.Min(maxIntensity, 0.8f);
                // Fewer surprises: the pet does not spontaneously investigate or relocate.
                removed |= Bit("CURIOUS_SNIFF") | Bit("SEEK_SHADE") | Bit("ORIENT_TO_SOUND");
            }
            if (profile.HasFlag(CareProfile.GentlePacing))
            {
                maxIntensity = Math.Min(maxIntensity, 0.7f);
                minLatency   = Math.Max(minLatency, 150);
                minDuration  = Math.Max(minDuration, 1.15f);
            }
            // ExtraEncouragement and CompanionBalance reweight rather than cap; they are
            // applied in IntentPolicy.BoostFor. Listed here so the set is exhaustive.

            return new CareModifierLimits(maxIntensity, minLatency, minDuration, removed);
        }

        /// <summary>
        /// True when the profile removes this intent. SELF and COMFORT are unremovable, so
        /// the available set is never empty and the pet can never freeze -- a frozen pet
        /// reads to a child as broken, or worse, as upset with them.
        /// </summary>
        public bool Removes(int catalogIndex)
        {
            if (catalogIndex < 0 || catalogIndex >= MaxMaskableIntents) return false;
            if (IntentCatalog.IsUnremovable(IntentCatalog.At(catalogIndex).Group)) return false;
            return (_removedMask & (1L << catalogIndex)) != 0L;
        }

        private static long Bit(string intentId)
        {
            int n = IntentCatalog.Count < MaxMaskableIntents ? IntentCatalog.Count : MaxMaskableIntents;
            for (int i = 0; i < n; i++)
                if (string.Equals(IntentCatalog.At(i).Id, intentId, StringComparison.Ordinal))
                    return 1L << i;
            return 0L;
        }
    }
}
