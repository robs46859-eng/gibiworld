// Tests for the care-context and redirection design
// (docs/design/pet-brain-and-care-context.md, ADR-009).
//
// These assert the properties that must NEVER regress. Several are written as negative
// tests because the failure mode is a system that starts doing something harmful, not
// one that stops doing something useful.
using NUnit.Framework;
using Gibi.Core;
using Gibi.Pets;

namespace Gibi.Tests.EditMode
{
    public class CareProfileComposition
    {
        [Test]
        public void Profiles_compose_to_the_most_conservative_value()
        {
            var both = CareParameters.Resolve(CareProfile.GentleSensory | CareProfile.Predictable);
            // GentleSensory caps at Walk; adding Predictable must never raise it back.
            Assert.AreEqual(Gait.Walk, both.MaxGait);
            Assert.GreaterOrEqual(both.BlendMultiplier, 2.0f);
        }

        [Test]
        public void Adding_a_profile_can_only_calm_the_pet_further()
        {
            var one = CareParameters.Resolve(CareProfile.GentlePacing);
            var many = CareParameters.Resolve(CareProfile.GentlePacing | CareProfile.GentleSensory |
                                              CareProfile.Predictable | CareProfile.CompanionBalance);
            Assert.LessOrEqual(many.RestCueAfterMs, one.RestCueAfterMs);
            Assert.LessOrEqual(many.ParticleBudgetScale, one.ParticleBudgetScale);
            Assert.LessOrEqual((int)many.MaxGait, (int)one.MaxGait);
        }

        [Test]
        public void Wellbeing_pacing_is_on_by_default_not_opt_in()
        {
            // A rest cue must exist even with no profile set, so pacing does not depend
            // on an attentive guardian finding a setting.
            Assert.Less(CareParameters.Default.RestCueAfterMs, long.MaxValue);
            Assert.Greater(CareParameters.Default.RestCueAfterMs, 0);
        }

        [Test]
        public void Care_profile_caps_speed_but_never_availability()
        {
            var care = CareParameters.Resolve(CareProfile.GentleSensory);
            // The pet still goes — it goes gently. Never Idle, which would be a refusal.
            Assert.AreEqual(Gait.Walk, RedirectionPolicy.CapGait(Gait.Run, care));
            Assert.AreNotEqual(Gait.Idle, RedirectionPolicy.CapGait(Gait.Run, care));
        }
    }

    public class RepetitionIsNeverExtinguished
    {
        private static EngagementEstimate Repetitive(float perseveration) =>
            new(arousal: 0.5f, perseveration: perseveration, settling: 0f, fatigue: 0f);

        [Test]
        public void Heavy_repetition_never_produces_a_replacing_redirection()
        {
            var clock = new FakeClock();
            var policy = new RedirectionPolicy(clock);
            var care = CareParameters.Default;

            // Simulate a child asking for the same thing over and over, for a long time.
            for (int i = 0; i < 500; i++)
            {
                clock.Advance(1500);
                var r = policy.Evaluate(Repetitive(1.0f), care);
                Assert.IsFalse(r.ReplacesCurrentActivity,
                    "Redirection must be additive. Repetitive play is often self-regulation, " +
                    "and the system cannot tell that from distress — so it must never interrupt.");
            }
        }

        [Test]
        public void Sustained_repetition_offers_alongside_rather_than_instead()
        {
            var clock = new FakeClock();
            var policy = new RedirectionPolicy(clock);
            clock.Advance(200_000);
            var r = policy.Evaluate(Repetitive(0.95f), CareParameters.Default);
            Assert.AreEqual(RedirectionKind.OfferAlongside, r.Kind);
            Assert.IsFalse(r.ReplacesCurrentActivity);
        }

        [Test]
        public void Moderate_repetition_only_adds_texture()
        {
            var policy = new RedirectionPolicy(new FakeClock());
            var r = policy.Evaluate(Repetitive(0.6f), CareParameters.Default);
            Assert.AreEqual(RedirectionKind.AddFlourish, r.Kind,
                "The hundredth jump still happens — it is just not identical to the first.");
        }

        [Test]
        public void Offers_are_rate_limited_so_the_pet_never_nags()
        {
            var clock = new FakeClock();
            var policy = new RedirectionPolicy(clock);
            var care = CareParameters.Default;
            clock.Advance(200_000);

            int offers = 0;
            for (int i = 0; i < 60; i++)
            {
                clock.Advance(1000);   // one minute of continuous heavy repetition
                if (policy.Evaluate(Repetitive(1.0f), care).Kind == RedirectionKind.OfferAlongside)
                    offers++;
            }
            Assert.LessOrEqual(offers, 1, "At most one offer per two-minute window.");
        }
    }

    public class ComfortIsNeverWithdrawn
    {
        [Test]
        public void Stillness_with_the_pet_close_makes_it_stay()
        {
            var policy = new RedirectionPolicy(new FakeClock());
            var settled = new EngagementEstimate(arousal: 0.05f, perseveration: 0f,
                                                 settling: 0.9f, fatigue: 0.3f);
            var r = policy.Evaluate(settled, CareParameters.Default);

            Assert.AreEqual(RedirectionKind.SettleNearby, r.Kind,
                "A child being quietly comforted must not be interrupted; the pet stays.");
            Assert.IsFalse(r.ReplacesCurrentActivity);
        }

        [Test]
        public void Settling_outranks_fatigue_so_comfort_is_never_cut_short()
        {
            var policy = new RedirectionPolicy(new FakeClock());
            var tiredAndSettled = new EngagementEstimate(arousal: 0f, perseveration: 0f,
                                                         settling: 0.9f, fatigue: 1.0f);
            Assert.AreEqual(RedirectionKind.SettleNearby,
                policy.Evaluate(tiredAndSettled, CareParameters.Default).Kind);
        }
    }

    public class EstimatorPrivacyProperties
    {
        [Test]
        public void Estimate_produces_continuous_values_never_categories()
        {
            var clock = new FakeClock();
            var est = new EngagementEstimator(clock);
            for (int i = 0; i < 10; i++) { clock.Advance(500); est.RecordInteraction("JUMP"); }

            var e = est.Estimate(localHourOfDay: 14);
            foreach (var v in new[] { e.Arousal, e.Perseveration, e.Settling, e.Fatigue })
            {
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
        }

        [Test]
        public void Repeating_one_action_registers_as_perseveration_not_a_label()
        {
            var clock = new FakeClock();
            var est = new EngagementEstimator(clock);
            for (int i = 0; i < 20; i++) { clock.Advance(800); est.RecordInteraction("JUMP"); }
            Assert.Greater(est.Estimate(14).Perseveration, 0.9f);
        }

        [Test]
        public void Varied_play_does_not_register_as_perseveration()
        {
            var clock = new FakeClock();
            var est = new EngagementEstimator(clock);
            string[] mix = { "JUMP", "SIT", "FETCH", "COME" };
            for (int i = 0; i < 20; i++) { clock.Advance(800); est.RecordInteraction(mix[i % 4]); }
            Assert.Less(est.Estimate(14).Perseveration, 0.5f);
        }

        [Test]
        public void A_fresh_estimator_starts_from_nothing_each_session()
        {
            // Ephemeral by construction: no constructor accepts prior state, so a new
            // session cannot inherit a conclusion about the child.
            var est = new EngagementEstimator(new FakeClock());
            var e = est.Estimate(14);
            Assert.AreEqual(0f, e.Perseveration);
            Assert.AreEqual(0f, e.Arousal);
        }
    }
}
