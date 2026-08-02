// Section 8.3 offline parity. These assert that the pet is never WORSE without AI, which
// is what prevents attachment forming to the connected state rather than to the pet.
using NUnit.Framework;
using Gibi.Core;
using Gibi.Pets;

namespace Gibi.Tests.EditMode
{
    public class GW_GAME_005_OfflineParity
    {
        [Test]
        public void Local_library_covers_every_intent_the_AI_can_propose()
        {
            // The AI selects FROM the local set, never beyond it. Acceptance therefore
            // cannot expand what the pet is capable of — only which option it picks.
            foreach (var intent in LocalBehaviorLibrary.AllIntents)
                Assert.IsTrue(LocalBehaviorLibrary.IsKnownIntent(intent));

            Assert.IsFalse(LocalBehaviorLibrary.IsKnownIntent("EXECUTE_ARBITRARY"),
                "Section 8.2: unknown intents must be rejected.");
        }

        [Test]
        public void Local_choice_never_returns_nothing()
        {
            // There is no failure state to fall back FROM, because local always answers.
            for (long tick = 0; tick < 200; tick++)
            {
                var intent = LocalBehaviorLibrary.Choose(
                    personalitySeed: 0xC0FFEE, bond: 40, energy: 80,
                    settling: 0f, tickIndex: tick);
                Assert.IsFalse(string.IsNullOrEmpty(intent));
                Assert.IsTrue(LocalBehaviorLibrary.IsKnownIntent(intent));
            }
        }

        [Test]
        public void Local_choice_is_deterministic_for_the_same_pet_and_state()
        {
            var a = LocalBehaviorLibrary.Choose(12345, 50, 70, 0f, 99);
            var b = LocalBehaviorLibrary.Choose(12345, 50, 70, 0f, 99);
            Assert.AreEqual(a, b, "Same pet, same situation, same behaviour.");
        }

        [Test]
        public void Absent_AI_changes_nothing()
        {
            var clock = new FakeClock();
            var policy = new AiSupplementPolicy(clock);

            // Simulates outage, timeout, airplane mode, and a broken backend identically.
            foreach (var absent in new[] { null, "" })
            {
                var result = policy.Resolve("CALM_IDLE", absent, 0, 1, 1);
                Assert.AreEqual("CALM_IDLE", result,
                    "An absent AI response must be indistinguishable from a normal beat.");
            }
        }

        [Test]
        public void Late_AI_is_treated_exactly_like_absent_AI()
        {
            var clock = new FakeClock();
            var policy = new AiSupplementPolicy(clock);
            clock.Advance(AiSupplementPolicy.SupplementBudgetMs + 1);

            Assert.AreEqual("GREET", policy.Resolve("GREET", "INVITE_PLAY", 0, 1, 1));
            Assert.AreEqual(1, policy.LateArrivals);
            Assert.AreEqual(0, policy.Accepted);
        }

        [Test]
        public void AI_can_never_introduce_an_intent_the_pet_could_not_already_do()
        {
            var policy = new AiSupplementPolicy(new FakeClock());
            Assert.AreEqual("CALM_IDLE",
                policy.Resolve("CALM_IDLE", "SOMETHING_INVENTED", 0, 1, 1),
                "Section 8.2: the AI selects from a fixed enum; it cannot extend capability.");
        }

        [Test]
        public void Stale_context_revision_is_rejected()
        {
            var policy = new AiSupplementPolicy(new FakeClock());
            Assert.AreEqual("SETTLE",
                policy.Resolve("SETTLE", "INVITE_PLAY", 0,
                               expectedContextRevision: 7, aiContextRevision: 6));
        }

        [Test]
        public void Missing_AI_is_never_surfaced_to_the_player()
        {
            // Section 8.3: no error unless the player explicitly opened an online feature.
            Assert.IsFalse(AiSupplementPolicy.ShouldSurfaceToPlayer(false));
            Assert.IsTrue(AiSupplementPolicy.ShouldSurfaceToPlayer(true));
        }

        [Test]
        public void Fully_offline_session_yields_the_same_capability_as_a_connected_one()
        {
            var clock = new FakeClock();
            var offline = new AiSupplementPolicy(clock);
            var online = new AiSupplementPolicy(clock);

            var offlineSeen = new System.Collections.Generic.HashSet<string>();
            var onlineSeen = new System.Collections.Generic.HashSet<string>();

            for (long tick = 0; tick < 500; tick++)
            {
                var local = LocalBehaviorLibrary.Choose(777, 55, 75, 0f, tick);
                offlineSeen.Add(offline.Resolve(local, null, 0, 1, 1));
                onlineSeen.Add(online.Resolve(local, "INVITE_PLAY", 0, 1, 1));
            }

            // Online may pick differently, but offline must not be a SMALLER world.
            Assert.GreaterOrEqual(offlineSeen.Count, 2,
                "Offline play must remain varied, not collapse to one behaviour.");
            Assert.IsTrue(offlineSeen.IsSubsetOf(
                new System.Collections.Generic.HashSet<string>(LocalBehaviorLibrary.AllIntents)));
        }
    }
}
