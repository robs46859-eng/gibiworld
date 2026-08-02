// Sections 6.4, 9.2, 10.2, 16 — connectivity is required even though sessions degrade
// gracefully. Mostly negative assertions: the failure mode is play CONTINUING when it
// should have stopped.
using NUnit.Framework;
using Gibi.Core;
using Gibi.AssetRuntime;

namespace Gibi.Tests.EditMode
{
    public class GW_Connectivity
    {
        private const long Hour = 60L * 60 * 1000;

        [Test]
        public void Session_cannot_start_without_a_successful_check()
        {
            var p = new ConnectivityPolicy(new FakeClock());
            Assert.IsFalse(p.MayStartSession(),
                "Section 6.4 step 3: no instantiation without a current entitlement.");
        }

        [Test]
        public void Unreachable_server_is_not_treated_as_permission()
        {
            var p = new ConnectivityPolicy(new FakeClock());
            p.RecordCheck(EntitlementCheckResult.Unreachable);
            Assert.IsFalse(p.MayStartSession(),
                "Unreachable says nothing about entitlement and must not read as yes.");
        }

        [Test]
        public void Revocation_takes_effect_immediately_with_no_grace()
        {
            var clock = new FakeClock();
            var p = new ConnectivityPolicy(clock);
            p.RecordCheck(EntitlementCheckResult.Valid);
            Assert.IsTrue(p.MayContinuePlaying());

            p.RecordCheck(EntitlementCheckResult.Revoked);
            Assert.IsFalse(p.MayContinuePlaying(),
                "Section 10.2: revocation takes effect IMMEDIATELY. Grace exists for " +
                "unreachable servers, never for a definitive negative answer.");
        }

        [Test]
        public void Play_survives_a_network_drop_mid_session()
        {
            var clock = new FakeClock();
            var p = new ConnectivityPolicy(clock);
            p.RecordCheck(EntitlementCheckResult.Valid);

            clock.Advance(12 * Hour);          // a day offline
            Assert.IsTrue(p.MayContinuePlaying(),
                "Section 8.3: an outage is a degradation, not a gameplay outage.");
        }

        [Test]
        public void Offline_play_does_not_extend_forever()
        {
            var clock = new FakeClock();
            var p = new ConnectivityPolicy(clock);
            p.RecordCheck(EntitlementCheckResult.Valid);

            clock.Advance(ConnectivityPolicy.RevalidationWindowMs +
                          ConnectivityPolicy.GracePeriodMs + 1);
            Assert.IsFalse(p.MayContinuePlaying(),
                "A pet that never phones home is a pet whose revocation never lands.");
        }

        [Test]
        public void Grace_period_is_silent_and_bounded()
        {
            var clock = new FakeClock();
            var p = new ConnectivityPolicy(clock);
            p.RecordCheck(EntitlementCheckResult.Valid);

            clock.Advance(ConnectivityPolicy.RevalidationWindowMs + Hour);
            Assert.IsTrue(p.IsInGracePeriod());
            Assert.IsTrue(p.MayContinuePlaying(), "Retry quietly; the player sees nothing yet.");
        }

        [Test]
        public void Kill_switch_stops_play_at_the_next_poll_not_the_next_window()
        {
            var clock = new FakeClock();
            var p = new ConnectivityPolicy(clock);
            p.RecordUpdateCheck(reachedServer: true, killSwitchEngaged: false,
                                safetyRevision: 1, entitlement: EntitlementCheckResult.Valid);
            Assert.IsTrue(p.MayContinuePlaying());

            clock.Advance(ConnectivityPolicy.UpdateCheckIntervalMs);
            Assert.IsTrue(p.ShouldCheckForUpdates(justForegrounded: false));

            p.RecordUpdateCheck(reachedServer: true, killSwitchEngaged: true,
                                safetyRevision: 1, entitlement: EntitlementCheckResult.Valid);
            Assert.IsFalse(p.MayContinuePlaying(),
                "Section 16: a kill switch that waits for the 72h window is not a kill switch.");
            Assert.IsFalse(p.MayStartSession());
        }

        [Test]
        public void Update_polling_is_faster_than_entitlement_revalidation()
        {
            Assert.Less(ConnectivityPolicy.UpdateCheckIntervalMs,
                        ConnectivityPolicy.RevalidationWindowMs,
                        "Kill switches and revocation lists must arrive far sooner than " +
                        "the hard entitlement deadline.");
        }

        [Test]
        public void Foregrounding_triggers_a_prompt_update_check()
        {
            var clock = new FakeClock();
            var p = new ConnectivityPolicy(clock);
            p.RecordUpdateCheck(true, false, 1, EntitlementCheckResult.Valid);

            clock.Advance(ConnectivityPolicy.UpdateCheckOnForegroundMs + 1);
            Assert.IsTrue(p.ShouldCheckForUpdates(justForegrounded: true));
            Assert.IsFalse(p.ShouldCheckForUpdates(justForegrounded: false),
                "Background cadence stays slow; resume gets a fast check.");
        }

        [Test]
        public void A_successful_update_poll_also_refreshes_entitlement_freshness()
        {
            var clock = new FakeClock();
            var p = new ConnectivityPolicy(clock);
            p.RecordCheck(EntitlementCheckResult.Valid);

            clock.Advance(60 * Hour);
            p.RecordUpdateCheck(true, false, 1, EntitlementCheckResult.Valid);
            clock.Advance(60 * Hour);

            Assert.IsTrue(p.MayContinuePlaying(),
                "A device playing normally never drifts toward the revalidation deadline.");
        }

        [Test]
        public void Failed_poll_does_not_refresh_anything()
        {
            var clock = new FakeClock();
            var p = new ConnectivityPolicy(clock);
            p.RecordCheck(EntitlementCheckResult.Valid);

            clock.Advance(ConnectivityPolicy.RevalidationWindowMs +
                          ConnectivityPolicy.GracePeriodMs + 1);
            p.RecordUpdateCheck(reachedServer: false, killSwitchEngaged: false,
                                safetyRevision: 1, entitlement: EntitlementCheckResult.Unreachable);
            Assert.IsFalse(p.MayContinuePlaying());
        }

        [Test]
        public void Safety_revision_propagates_for_section_9_2_invalidation()
        {
            var p = new ConnectivityPolicy(new FakeClock());
            p.RecordUpdateCheck(true, false, safetyRevision: 7, entitlement: EntitlementCheckResult.Valid);
            Assert.AreEqual(7, p.SafetyRevision);
        }
    }
}
