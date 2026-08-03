// GW-ARCH-002 section 7, catalog revision 2. Architecture B.
//
// This replaces LocalBehaviorLibrary.Choose. It is the pet's whole brain: a deterministic
// weighted selection over IntentCatalog, followed by modifier derivation.
//
// THE INVARIANT THAT GOVERNS THIS FILE:
//
//   energy and fatigue modulate HOW the pet responds, never WHETHER.
//
// A pet that becomes "too tired to continue" is a refusal wearing a costume. Section 1.2
// excludes injury, punishment, and deprivation; a soft refusal is the same mechanic with
// better manners. So no state anywhere in this file may drive an intent's weight to zero.
// Only a missing target or a care profile may make an intent unavailable, and the SELF and
// COMFORT groups are unremovable, so the available set is never empty.
//
// Repetition is handled the same way: the hundredth request is honoured exactly like the
// first. Micro-variation is added through modifiers so it does not read as a byte-identical
// loop, but nothing ever becomes unavailable and nothing is ever counted where the player
// can see it.
//
// Pure C#. Deterministic. No allocation on the hot path.
using System;

namespace Gibi.Pets
{
    /// <summary>What the pet can currently act upon. Counts only -- no ids, no positions.</summary>
    public readonly struct AvailableTargets
    {
        public readonly int Toys;
        public readonly int SpatialObjects;
        public readonly int Pets;

        public AvailableTargets(int toys, int spatialObjects, int pets)
        { Toys = toys; SpatialObjects = spatialObjects; Pets = pets; }

        public bool Has(TargetKind kinds)
            => ((kinds & TargetKind.Toy) != 0 && Toys > 0)
            || ((kinds & TargetKind.Spo) != 0 && SpatialObjects > 0)
            || ((kinds & TargetKind.Pet) != 0 && Pets > 0);
    }

    /// <summary>Everything the policy reads. Assembled once per tick by PetController.</summary>
    public readonly struct PolicyContext
    {
        public readonly long   PersonalitySeed;
        public readonly int    Bond;              // 0..100
        public readonly int    Energy;            // 0..100 -- expressive only, never gating
        public readonly EngagementEstimate Engagement;
        public readonly CareProfile Care;
        public readonly AvailableTargets Targets;
        public readonly int    LocalHourOfDay;    // 0..23
        public readonly long   TickIndex;
        public readonly int    LastIntentIndex;   // -1 when none
        public readonly int    RepeatRunLength;   // consecutive repeats of LastIntentIndex

        public PolicyContext(long personalitySeed, int bond, int energy,
                             EngagementEstimate engagement, CareProfile care,
                             AvailableTargets targets, int localHourOfDay,
                             long tickIndex, int lastIntentIndex, int repeatRunLength)
        {
            PersonalitySeed = personalitySeed; Bond = bond; Energy = energy;
            Engagement = engagement; Care = care; Targets = targets;
            LocalHourOfDay = localHourOfDay; TickIndex = tickIndex;
            LastIntentIndex = lastIntentIndex; RepeatRunLength = repeatRunLength;
        }
    }

    public readonly struct IntentSelection
    {
        public readonly int               CatalogIndex;
        public readonly IntentDef         Def;
        public readonly BehaviorModifiers Modifiers;

        public IntentSelection(int index, in IntentDef def, in BehaviorModifiers mods)
        { CatalogIndex = index; Def = def; Modifiers = mods; }

        public string IntentId => Def.Id;
        public string ClipKey  => Def.ClipKey;
    }

    public static class IntentPolicy
    {
        // Scratch buffer. Single-threaded by contract: the policy runs only on the 10 Hz
        // arbiter tick, on the main thread. Avoids a per-tick allocation.
        [ThreadStatic] private static int[] _weights;

        public static IntentSelection Select(in PolicyContext ctx)
        {
            int n = IntentCatalog.Count;
            var w = _weights;
            if (w == null || w.Length < n) w = _weights = new int[n];

            var care = CareModifierLimits.Resolve(ctx.Care);
            long total = 0;

            for (int i = 0; i < n; i++)
            {
                var def = IntentCatalog.At(i);
                int weight = Availability(in def, in ctx, in care, i) ? def.BaseWeight : 0;

                if (weight > 0)
                {
                    weight = ApplyContext(weight, in def, in ctx);
                    weight = ApplyBoost(weight, in def, ctx.Care);
                    weight = ApplyRepetitionTexture(weight, in ctx, i);
                    if (weight < 1) weight = 1;   // never zero once available
                }

                w[i] = weight;
                total += weight;
            }

            int chosen = WeightedPick(w, n, total, ctx.PersonalitySeed, ctx.TickIndex);
            var chosenDef = IntentCatalog.At(chosen);
            var mods = DeriveModifiers(in chosenDef, in ctx, in care, chosen);
            return new IntentSelection(chosen, in chosenDef, in mods);
        }

        // ---------------------------------------------------------------------------
        // Availability. The ONLY two reasons an intent can be unavailable.
        // ---------------------------------------------------------------------------
        private static bool Availability(in IntentDef def, in PolicyContext ctx,
                                         in CareModifierLimits care, int index)
        {
            if (care.Removes(index)) return false;
            if (def.Target == TargetRequirement.Required && !ctx.Targets.Has(def.TargetKinds))
                return false;
            return true;
        }

        // ---------------------------------------------------------------------------
        // Context weighting. Shifts the DISTRIBUTION. Never zeroes an available intent.
        // ---------------------------------------------------------------------------
        private static int ApplyContext(int weight, in IntentDef def, in PolicyContext ctx)
        {
            var e = ctx.Engagement;
            float m = 1.0f;

            switch (def.Group)
            {
                case IntentGroup.Comfort:
                    // Stillness with the pet close is the delicate case: the child may be
                    // seeking comfort. The pet leans in and stays. It does not withdraw.
                    m *= 1.0f + 2.0f * e.Settling + 1.0f * e.Fatigue;
                    m *= 1.0f - 0.5f * e.Arousal;
                    break;

                case IntentGroup.Player:
                    m *= 1.0f + 0.9f * (ctx.Bond / 100f);
                    m *= 1.0f + 0.6f * e.Arousal;
                    m *= 1.0f - 0.4f * e.Settling;
                    break;

                case IntentGroup.Object:
                    m *= 1.0f + 0.7f * e.Arousal;
                    m *= 1.0f - 0.5f * e.Fatigue;
                    break;

                case IntentGroup.Self:
                    m *= 1.0f + 0.8f * e.Fatigue;
                    break;

                case IntentGroup.Environment:
                    m *= 1.0f - 0.4f * e.Settling;
                    break;

                case IntentGroup.Training:
                    m *= 1.0f + 0.5f * (ctx.Bond / 100f);
                    m *= 1.0f - 0.6f * e.Fatigue;
                    break;
            }

            // Late hour: the pet curls up. It never locks anyone out, and there is no
            // message -- the child experiences a sleepy animal, not an app with an opinion.
            bool lateHour = ctx.LocalHourOfDay >= 21 || ctx.LocalHourOfDay < 6;
            if (lateHour)
            {
                if (def.Group == IntentGroup.Comfort || def.Group == IntentGroup.Self) m *= 1.6f;
                else m *= 0.7f;
            }

            // Energy biases toward restful choices. It does NOT remove active ones: a low
            // battery must never become a refusal. See the file header.
            float energy01 = ctx.Energy / 100f;
            if (def.Group == IntentGroup.Self || def.Group == IntentGroup.Comfort)
                m *= 1.0f + 0.6f * (1.0f - energy01);
            else
                m *= 0.6f + 0.4f * energy01;   // floor of 0.6, never 0

            int scaled = (int)(weight * m);
            return scaled < 1 ? 1 : scaled;
        }

        private static int ApplyBoost(int weight, in IntentDef def, CareProfile care)
        {
            if (care.HasFlag(CareProfile.ExtraEncouragement) &&
                (def.Id == "OFFER_KNOWN_TRICK" || def.Id == "GREET" || def.Id == "CHECK_IN"))
                weight = (int)(weight * 1.5f);

            if (care.HasFlag(CareProfile.CompanionBalance) &&
                (def.Id == "PRESENT_ITEM" || def.Id == "CHECK_IN"))
                weight = (int)(weight * 1.4f);

            return weight;
        }

        // ---------------------------------------------------------------------------
        // Repetition. NEVER extinguished -- only given texture.
        //
        // A child asking for the same thing a hundred times gets it a hundred times.
        // Repetitive play is a regulation strategy for many children, and a system that
        // interrupts it is interrupting the thing that is working. Joyful repetition,
        // self-regulating repetition, and distress look identical through a touchscreen,
        // so the response must be safe for the case where interruption would hurt.
        //
        // What this does: after a long run, ADD a small pull toward an additional offer.
        // The repeated intent keeps its full weight. The offer appears alongside it, never
        // instead of it.
        // ---------------------------------------------------------------------------
        private static int ApplyRepetitionTexture(int weight, in PolicyContext ctx, int index)
        {
            if (ctx.RepeatRunLength < 8) return weight;
            if (index == ctx.LastIntentIndex) return weight;   // repeated intent untouched

            var def = IntentCatalog.At(index);
            bool additiveOffer = def.Id == "PRESENT_ITEM" || def.Id == "INVITE_PLAY"
                              || def.Id == "CHECK_IN";
            return additiveOffer ? (int)(weight * 1.35f) : weight;
        }

        // ---------------------------------------------------------------------------
        // Modifier derivation. This is where the pet becomes alive.
        // ---------------------------------------------------------------------------
        private static BehaviorModifiers DeriveModifiers(in IntentDef def, in PolicyContext ctx,
                                                         in CareModifierLimits care, int index)
        {
            var e = ctx.Engagement;

            // Jitter is seeded on the tick, so a repeated intent is never byte-identical
            // while remaining perfectly reproducible for replay and test.
            // unchecked casts: these constants exceed long.MaxValue, so C# types the
            // literals as ulong regardless of the L suffix and '^' has no long/ulong
            // overload. Same trap the original LocalBehaviorLibrary already documented.
            const long GoldenGamma = unchecked((long)0x9E3779B97F4A7C15UL);
            const long MixSaltA    = unchecked((long)0xC2B2AE3D27D4EB4FUL);
            const long TickStride  = 0x2545F4914F6CDD1DL;

            long h = Mix(ctx.PersonalitySeed ^ (ctx.TickIndex * TickStride) ^ index);
            float j0 = Unit(h);
            float j1 = Unit(Mix(h ^ GoldenGamma));
            float j2 = Unit(Mix(h ^ MixSaltA));

            // Intensity: arousal and energy raise it, fatigue and settling lower it.
            float intensity = 0.35f
                            + 0.40f * e.Arousal
                            + 0.20f * (ctx.Energy / 100f)
                            - 0.30f * e.Settling
                            - 0.25f * e.Fatigue
                            + 0.10f * (j0 - 0.5f);

            // Latency -- THE most important field in this file.
            //
            // A calm, settled, tired pet takes its time. An excited one moves almost at
            // once. The hesitation is what makes the action look chosen rather than fired.
            // Base 300 ms is deliberate. An earlier revision used 120 ms with a -500
            // arousal term, which put every neutral-state response at or below zero after
            // clamping -- an instant, identical, machine-like reaction in the most common
            // state in the game. The base must stay high enough that ordinary play still
            // carries visible hesitation.
            float latency = 300f
                          + 900f * e.Settling
                          + 600f * e.Fatigue
                          - 200f * e.Arousal
                          + 300f * (j1 - 0.5f);

            // Deep in a repetition run the pet gets comfortable and answers a little
            // faster -- a happy, practised animal, not a bored one.
            if (ctx.RepeatRunLength >= 8) latency *= 0.85f;

            float durationScale = 1.0f
                                + 0.35f * e.Fatigue
                                + 0.25f * e.Settling
                                - 0.20f * e.Arousal
                                + 0.12f * (j2 - 0.5f);

            Approach approach;
            if (e.Settling > 0.6f)          approach = Approach.Hesitant;
            else if (e.Arousal > 0.7f)      approach = Approach.Direct;
            else if (j0 < 0.34f)            approach = Approach.ArcLeft;
            else if (j0 < 0.68f)            approach = Approach.ArcRight;
            else                            approach = Approach.Direct;

            // High perseveration means the child is repeating happily. The pet stays
            // willing rather than performing once and stopping.
            var persistence = e.Perseveration > 0.6f
                ? Persistence.UntilInterrupted
                : Persistence.Once;

            return BehaviorModifiers.Clamp(in def, in care,
                                           intensity, (int)latency, durationScale,
                                           approach, persistence);
        }

        // ---------------------------------------------------------------------------
        // Deterministic selection primitives.
        // ---------------------------------------------------------------------------
        private static int WeightedPick(int[] weights, int n, long total, long seed, long tick)
        {
            if (total <= 0) return 0;   // unreachable: SELF is unremovable and floors at 1

            long h = Mix(seed ^ (tick * unchecked((long)0x9E3779B97F4A7C15UL)));
            long r = (h & 0x7FFFFFFFFFFFFFFFL) % total;

            long acc = 0;
            for (int i = 0; i < n; i++)
            {
                acc += weights[i];
                if (r < acc) return i;
            }
            return n - 1;
        }

        /// <summary>splitmix64 finalizer. Deterministic across platforms and runtimes.</summary>
        private static long Mix(long x)
        {
            unchecked
            {
                ulong z = (ulong)x + 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return (long)(z ^ (z >> 31));
            }
        }

        private static float Unit(long h)
            => (float)((h & 0xFFFFFF) / (double)0x1000000);
    }
}
