// GW-ARCH-002 section 7, catalog revision 2. Architecture B acceptance tests.
//
// Most of these are NEGATIVE tests. The failure mode for a pet companion is not that it
// stops doing something useful -- it is that it starts doing something harmful: refusing,
// withdrawing, tiring out, or going silent at exactly the wrong moment. Those are the
// properties asserted here.
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Gibi.Pets;

namespace Gibi.Tests.EditMode
{
    internal static class Ctx
    {
        public static EngagementEstimate Neutral =>
            new(arousal: 0.5f, perseveration: 0f, settling: 0f, fatigue: 0f);

        public static PolicyContext Make(
            int energy = 70, int bond = 50, CareProfile care = CareProfile.None,
            EngagementEstimate? engagement = null, int toys = 1, int spos = 1,
            int hour = 14, long tick = 100, long seed = 0x5EEDL,
            int lastIntent = -1, int repeatRun = 0)
            => new(seed, bond, energy, engagement ?? Neutral, care,
                   new AvailableTargets(toys, spos, 0), hour, tick, lastIntent, repeatRun);
    }

    public class PolicyDeterminism
    {
        [Test]
        public void Identical_inputs_produce_an_identical_selection()
        {
            var a = IntentPolicy.Select(Ctx.Make());
            var b = IntentPolicy.Select(Ctx.Make());

            Assert.AreEqual(a.CatalogIndex, b.CatalogIndex);
            Assert.AreEqual(a.Modifiers.LatencyMs, b.Modifiers.LatencyMs);
            Assert.AreEqual(a.Modifiers.Intensity, b.Modifiers.Intensity);
            Assert.AreEqual(a.Modifiers.DurationScale, b.Modifiers.DurationScale);
            Assert.AreEqual(a.Modifiers.Approach, b.Modifiers.Approach);
        }

        [Test]
        public void Different_ticks_vary_the_result_so_the_pet_is_not_a_loop()
        {
            int distinct = 0;
            int last = -1;
            for (long t = 0; t < 200; t++)
            {
                var s = IntentPolicy.Select(Ctx.Make(tick: t));
                if (s.CatalogIndex != last) distinct++;
                last = s.CatalogIndex;
            }
            Assert.Greater(distinct, 20, "A pet that picks the same thing every tick reads as frozen.");
        }

        [Test]
        public void Different_pets_behave_differently_under_identical_conditions()
        {
            // Sampled across ticks rather than compared at a single one: latency lands in
            // a few hundred integer buckets, so a one-shot comparison would collide by
            // chance and make this test flaky rather than wrong.
            int differences = 0;
            for (long t = 0; t < 50; t++)
            {
                var a = IntentPolicy.Select(Ctx.Make(seed: 1, tick: t));
                var b = IntentPolicy.Select(Ctx.Make(seed: 999_999, tick: t));
                if (a.Modifiers.LatencyMs != b.Modifiers.LatencyMs) differences++;
            }
            Assert.Greater(differences, 40,
                "personality_seed must give two pets in identical circumstances their own timing.");
        }
    }

    public class EnergyIsExpressiveNeverGating
    {
        // THE core invariant of IntentPolicy. A pet that becomes "too tired to continue"
        // is a soft refusal wearing a costume, and section 1.2 excludes refusal mechanics.

        [Test]
        public void Every_intent_available_at_full_energy_is_available_at_zero_energy()
        {
            var full  = Reachable(energy: 100);
            var empty = Reachable(energy: 0);

            for (int i = 0; i < IntentCatalog.Count; i++)
                if (full[i])
                    Assert.IsTrue(empty[i],
                        $"{IntentCatalog.At(i).Id} became unavailable at low energy. " +
                        "Energy may change HOW the pet responds, never WHETHER.");
        }

        [Test]
        public void Exhausted_pet_still_reaches_active_intents()
        {
            var reachable = Reachable(energy: 0, fatigue: 1.0f);
            Assert.IsTrue(reachable[IndexOf("RETRIEVE")]);
            Assert.IsTrue(reachable[IndexOf("INVITE_PLAY")]);
            Assert.IsTrue(reachable[IndexOf("GREET")]);
        }

        [Test]
        public void Low_energy_lowers_intensity_rather_than_removing_the_action()
        {
            float hi = AverageIntensity(energy: 100);
            float lo = AverageIntensity(energy: 0);
            Assert.Less(lo, hi, "Tiredness should read as a softer action, not a missing one.");
        }

        private static bool[] Reachable(int energy, float fatigue = 0f)
        {
            var seen = new bool[IntentCatalog.Count];
            var e = new EngagementEstimate(0.5f, 0f, 0f, fatigue);
            for (long t = 0; t < 40_000; t++)
                seen[IntentPolicy.Select(Ctx.Make(energy: energy, engagement: e, tick: t)).CatalogIndex] = true;
            return seen;
        }

        private static float AverageIntensity(int energy)
        {
            float sum = 0;
            for (long t = 0; t < 2000; t++)
                sum += IntentPolicy.Select(Ctx.Make(energy: energy, tick: t)).Modifiers.Intensity;
            return sum / 2000f;
        }

        internal static int IndexOf(string id)
        {
            for (int i = 0; i < IntentCatalog.Count; i++)
                if (IntentCatalog.At(i).Id == id) return i;
            Assert.Fail($"{id} not in catalog");
            return -1;
        }
    }

    public class RepetitionKeepsItsFullWeight
    {
        [Test]
        public void A_long_repetition_run_never_reduces_the_repeated_intent()
        {
            int retrieve = EnergyIsExpressiveNeverGating.IndexOf("RETRIEVE");

            bool stillReachable = false;
            for (long t = 0; t < 20_000 && !stillReachable; t++)
                stillReachable = IntentPolicy.Select(
                    Ctx.Make(tick: t, lastIntent: retrieve, repeatRun: 500)).CatalogIndex == retrieve;

            Assert.IsTrue(stillReachable,
                "The hundredth request is honoured exactly like the first. Repetition is " +
                "often self-regulation and must never be extinguished.");
        }

        [Test]
        public void Sustained_repetition_only_adds_an_alongside_offer()
        {
            int retrieve = EnergyIsExpressiveNeverGating.IndexOf("RETRIEVE");
            int present  = EnergyIsExpressiveNeverGating.IndexOf("PRESENT_ITEM");

            int presentCount = 0, retrieveCount = 0;
            for (long t = 0; t < 5000; t++)
            {
                int c = IntentPolicy.Select(
                    Ctx.Make(tick: t, lastIntent: retrieve, repeatRun: 40)).CatalogIndex;
                if (c == present) presentCount++;
                if (c == retrieve) retrieveCount++;
            }

            Assert.Greater(presentCount, 0, "The offer should appear...");
            Assert.Greater(retrieveCount, 0, "...alongside the repeated action, never instead of it.");
        }

        [Test]
        public void High_perseveration_keeps_the_pet_willing_rather_than_one_and_done()
        {
            var persevering = new EngagementEstimate(0.5f, 0.9f, 0f, 0f);
            var s = IntentPolicy.Select(Ctx.Make(engagement: persevering));
            Assert.AreEqual(Persistence.UntilInterrupted, s.Modifiers.Persistence);
        }
    }

    public class ComfortAndTiming
    {
        [Test]
        public void A_settled_child_gets_a_pet_that_slows_down_and_stays()
        {
            var settled = new EngagementEstimate(arousal: 0.05f, perseveration: 0f,
                                                 settling: 0.9f, fatigue: 0.3f);
            var s = IntentPolicy.Select(Ctx.Make(engagement: settled));

            Assert.Greater(s.Modifiers.LatencyMs, 400,
                "Comfort should read as unhurried. A snappy pet breaks the moment.");
            Assert.Less(s.Modifiers.Intensity, 0.5f);
        }

        [Test]
        public void An_excited_child_gets_a_pet_that_answers_almost_at_once()
        {
            var excited = new EngagementEstimate(arousal: 1.0f, perseveration: 0f,
                                                 settling: 0f, fatigue: 0f);
            var s = IntentPolicy.Select(Ctx.Make(engagement: excited));
            Assert.Less(s.Modifiers.LatencyMs, 400);
        }

        [Test]
        public void Latency_varies_across_ticks_so_no_two_responses_land_identically()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (long t = 0; t < 300; t++)
                seen.Add(IntentPolicy.Select(Ctx.Make(tick: t)).Modifiers.LatencyMs);

            Assert.Greater(seen.Count, 30,
                "Timing variance is what reads as interiority. Identical latency reads as a machine.");
        }

        [Test]
        public void Late_hour_favours_settling_without_locking_anyone_out()
        {
            var reachable = new bool[IntentCatalog.Count];
            for (long t = 0; t < 20_000; t++)
                reachable[IntentPolicy.Select(Ctx.Make(hour: 23, tick: t)).CatalogIndex] = true;

            Assert.IsTrue(reachable[EnergyIsExpressiveNeverGating.IndexOf("INVITE_PLAY")],
                "The pet gets sleepy. It never becomes unavailable.");
        }
    }

    public class CareProfilesOnlyNarrow
    {
        [Test]
        public void Adding_a_profile_never_raises_a_cap()
        {
            var one  = CareModifierLimits.Resolve(CareProfile.GentlePacing);
            var many = CareModifierLimits.Resolve(CareProfile.GentlePacing | CareProfile.GentleSensory |
                                                  CareProfile.Predictable | CareProfile.ExtraEncouragement);

            Assert.LessOrEqual(many.MaxIntensity, one.MaxIntensity);
            Assert.GreaterOrEqual(many.MinLatencyMs, one.MinLatencyMs);
            Assert.GreaterOrEqual(many.MinDurationScale, one.MinDurationScale);
        }

        [Test]
        public void Self_and_comfort_intents_survive_every_profile_combination()
        {
            var all = CareProfile.GentleSensory | CareProfile.Predictable | CareProfile.GentlePacing |
                      CareProfile.ExtraEncouragement | CareProfile.CompanionBalance;
            var care = CareModifierLimits.Resolve(all);

            bool self = false, comfort = false;
            for (int i = 0; i < IntentCatalog.Count; i++)
            {
                if (care.Removes(i)) continue;
                var g = IntentCatalog.At(i).Group;
                if (g == IntentGroup.Self) self = true;
                if (g == IntentGroup.Comfort) comfort = true;
            }

            Assert.IsTrue(self && comfort,
                "The available set must never empty out. A frozen pet reads to a child as " +
                "broken, or worse, as upset with them.");
        }

        [Test]
        public void Gentle_sensory_caps_intensity_across_every_selection()
        {
            for (long t = 0; t < 3000; t++)
            {
                var s = IntentPolicy.Select(Ctx.Make(care: CareProfile.GentleSensory, tick: t));
                Assert.LessOrEqual(s.Modifiers.Intensity, 0.5f + 1e-5f);
                Assert.GreaterOrEqual(s.Modifiers.LatencyMs, 250);
            }
        }
    }

    public class ModifierClamping
    {
        [Test]
        public void An_intent_cap_beats_a_requested_intensity()
        {
            Assert.IsTrue(IntentCatalog.TryGet("SETTLE", out var settle));
            var m = BehaviorModifiers.Clamp(in settle, CareModifierLimits.Default,
                                            intensity: 1.0f, latencyMs: 0, durationScale: 1.0f,
                                            Approach.Direct, Persistence.Once);
            Assert.LessOrEqual(m.Intensity, settle.MaxIntensity);
        }

        [Test]
        public void NaN_intensity_falls_to_the_calmest_value()
        {
            Assert.IsTrue(IntentCatalog.TryGet("GREET", out var greet));
            var m = BehaviorModifiers.Clamp(in greet, CareModifierLimits.Default,
                                            intensity: float.NaN, latencyMs: 100, durationScale: 1f,
                                            Approach.Direct, Persistence.Once);
            Assert.AreEqual(0f, m.Intensity);
        }

        [Test]
        public void A_hesitant_approach_cannot_have_zero_latency()
        {
            Assert.IsTrue(IntentCatalog.TryGet("SEEK_PROXIMITY", out var seek));
            var m = BehaviorModifiers.Clamp(in seek, CareModifierLimits.Default,
                                            intensity: 0.5f, latencyMs: 0, durationScale: 1f,
                                            Approach.Hesitant, Persistence.Once);
            Assert.GreaterOrEqual(m.LatencyMs, 200);
        }

        [Test]
        public void Out_of_range_values_are_narrowed_never_honoured()
        {
            Assert.IsTrue(IntentCatalog.TryGet("RETRIEVE", out var r));
            var m = BehaviorModifiers.Clamp(in r, CareModifierLimits.Default,
                                            intensity: 99f, latencyMs: 999_999, durationScale: 99f,
                                            Approach.Direct, Persistence.Once);
            Assert.LessOrEqual(m.Intensity, 1.0f);
            Assert.LessOrEqual(m.LatencyMs, BehaviorModifiers.MaxLatencyMs);
            Assert.LessOrEqual(m.DurationScale, BehaviorModifiers.MaxDuration);
        }
    }

    public class TargetRequirements
    {
        [Test]
        public void Required_target_missing_makes_the_intent_unreachable()
        {
            int retrieve = EnergyIsExpressiveNeverGating.IndexOf("RETRIEVE");
            for (long t = 0; t < 20_000; t++)
                Assert.AreNotEqual(retrieve,
                    IntentPolicy.Select(Ctx.Make(toys: 0, tick: t)).CatalogIndex,
                    "RETRIEVE requires a toy and there is none.");
        }

        [Test]
        public void With_no_targets_at_all_the_pet_still_always_acts()
        {
            for (long t = 0; t < 5000; t++)
            {
                var s = IntentPolicy.Select(Ctx.Make(toys: 0, spos: 0, tick: t));
                Assert.IsTrue(IntentCatalog.IsKnown(s.IntentId));
            }
        }
    }

    public class CatalogContractParity
    {
        // The JSON contract is the source of truth. This test is what makes that true
        // rather than aspirational -- the exact failure mode that produced GW-ARCH-002.

        [Test]
        public void Csharp_catalog_matches_the_contract_file_exactly()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "../../../contracts/schemas/intent-catalog.json"));

            if (!File.Exists(path))
                Assert.Ignore($"Contract not reachable from this working directory: {path}");

            var ids = new System.Collections.Generic.List<string>();
            foreach (Match m in Regex.Matches(File.ReadAllText(path), "\"id\"\\s*:\\s*\"([A-Z_]+)\""))
                ids.Add(m.Groups[1].Value);

            Assert.AreEqual(ids.Count, IntentCatalog.Count,
                "Contract and C# catalog disagree on intent count.");

            for (int i = 0; i < ids.Count; i++)
                Assert.AreEqual(ids[i], IntentCatalog.At(i).Id,
                    $"Order or id mismatch at index {i}. Catalog order is load-bearing: " +
                    "the deterministic selector indexes into it, so reordering changes every pet.");
        }

        [Test]
        public void No_duplicate_intent_ids()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < IntentCatalog.Count; i++)
                Assert.IsTrue(seen.Add(IntentCatalog.At(i).Id), $"Duplicate id at index {i}");
        }

        [Test]
        public void Catalog_fits_the_care_mask_width()
        {
            Assert.LessOrEqual(IntentCatalog.Count, CareModifierLimits.MaxMaskableIntents,
                "Appending past the mask width would silently stop care-profile removal.");
        }

        [Test]
        public void Every_intent_declares_a_clip_and_a_sane_cap()
        {
            for (int i = 0; i < IntentCatalog.Count; i++)
            {
                var d = IntentCatalog.At(i);
                Assert.IsFalse(string.IsNullOrEmpty(d.ClipKey), $"{d.Id} has no clip binding");
                Assert.Greater(d.MaxDurationMs, 0, $"{d.Id} has no duration");
                Assert.GreaterOrEqual(d.MaxIntensity, 0f);
                Assert.LessOrEqual(d.MaxIntensity, 1f);
                if (d.Target == TargetRequirement.Required)
                    Assert.AreNotEqual(TargetKind.None, d.TargetKinds,
                        $"{d.Id} requires a target but names no kind");
            }
        }
    }
}
