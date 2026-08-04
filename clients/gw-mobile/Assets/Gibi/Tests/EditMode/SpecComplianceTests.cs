// GW-ARCH-001 section 17 — Test and acceptance gates.
// Each [Test] names the GW-* requirement it discharges. A release gate reads these
// names, so renaming a test breaks traceability deliberately.
using NUnit.Framework;
using Gibi.Core;
using Gibi.Spatial;
using Gibi.Pets;
using Gibi.Gameplay;
using Gibi.AssetRuntime;

namespace Gibi.Tests.EditMode
{
    public class GW_AR_005_QuaternionNormalisation
    {
        [Test]
        public void Normalised_quaternion_is_accepted()
        {
            var r = AnchorLocalPose.Create(1f, 0f, -3f, 0f, 0f, 0f, 1f);
            Assert.IsTrue(r.Success);
        }

        [Test]
        public void Zero_quaternion_is_rejected()
        {
            var r = AnchorLocalPose.Create(0f, 0f, 0f, 0f, 0f, 0f, 0f);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("POSE_QUATERNION_ZERO", r.ErrorCode);
        }

        [Test]
        public void NaN_quaternion_is_rejected()
        {
            var r = AnchorLocalPose.Create(0f, 0f, 0f, float.NaN, 0f, 0f, 1f);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("POSE_QUATERNION_NAN", r.ErrorCode);
        }

        [Test]
        public void Denormalised_beyond_1e4_is_rejected()
        {
            // norm = 1.001 -> error 1e-3, an order of magnitude over tolerance
            var r = AnchorLocalPose.Create(0f, 0f, 0f, 0f, 0f, 0f, 1.001f);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("POSE_QUATERNION_NOT_NORMALIZED", r.ErrorCode);
        }

        [Test]
        public void Object_beyond_75m_from_anchor_is_rejected()
        {
            var r = AnchorLocalPose.Create(80f, 0f, 0f, 0f, 0f, 0f, 1f);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("POSE_EXCEEDS_75M_FROM_ANCHOR", r.ErrorCode);
        }
    }

    public class GW_AR_002_003_AnchorGating
    {
        [Test]
        public void Ranked_scoring_requires_one_full_second_of_tracking()
        {
            var clock = new FakeClock();
            var a = new AnchorEligibility(clock);

            a.Tick(true, true, usingVpsSite: true, anchorTracked: true, poseJumpM: 0f);
            Assert.AreEqual(AnchorState.VpsLimited, a.State, "must not be eligible immediately");
            Assert.AreEqual(ScoringMode.Disabled, a.Scoring);

            clock.Advance(1000);
            a.Tick(true, true, true, true, 0f);
            Assert.AreEqual(AnchorState.VpsTracked, a.State);
            Assert.AreEqual(ScoringMode.Eligible, a.Scoring);
        }

        [Test]
        public void Tracking_loss_degrades_and_pauses_within_250ms()
        {
            var clock = new FakeClock();
            var a = new AnchorEligibility(clock);
            clock.Advance(1000);
            a.Tick(true, true, true, true, 0f);
            clock.Advance(1000);
            a.Tick(true, true, true, true, 0f);
            Assert.AreEqual(ScoringMode.Eligible, a.Scoring);

            a.Tick(true, true, true, anchorTracked: false, poseJumpM: 0f);
            clock.Advance(251);
            a.Tick(true, true, true, false, 0f);

            Assert.AreEqual(AnchorState.Degraded, a.State);
            Assert.AreEqual(ScoringMode.Paused, a.Scoring, "ranked clock must pause");
        }

        [Test]
        public void Local_anchor_never_authorises_persistence()
        {
            var clock = new FakeClock();
            var a = new AnchorEligibility(clock);
            a.Tick(true, surfaceAccepted: true, usingVpsSite: false, anchorTracked: false, poseJumpM: 0f);

            Assert.AreEqual(AnchorState.LocalReady, a.State);
            Assert.AreEqual(ScoringMode.PracticeOnly, a.Scoring);
            Assert.IsFalse(a.MayPersistPlacement, "GW-AR-004: local anchor must never reach persistence");
        }

        [Test]
        public void Tracking_loss_beyond_three_seconds_invalidates_the_run()
        {
            var clock = new FakeClock();
            var a = new AnchorEligibility(clock);
            clock.Advance(1000); a.Tick(true, true, true, true, 0f);
            clock.Advance(1000); a.Tick(true, true, true, true, 0f);

            a.Tick(true, true, true, false, 0f);
            clock.Advance(3001);
            a.Tick(true, true, true, false, 0f);

            Assert.IsTrue(a.RunInvalidated);
        }
    }

    public class GW_AR_006_007_SurfaceGates
    {
        [Test]
        public void Hazard_tags_reject_placement()
        {
            foreach (var tag in new[] { SemanticTag.Sky, SemanticTag.Person, SemanticTag.Vehicle,
                                        SemanticTag.Water, SemanticTag.Road, SemanticTag.Rail,
                                        SemanticTag.Unknown })
            {
                var s = new SurfaceSample(tag, 0f, 2f, 3f);
                Assert.IsFalse(SurfaceAcceptance.IsAccepted(s, PlacementPurpose.PetIdleOrTraining),
                               $"{tag} must be rejected");
            }
        }

        [Test]
        public void Ranked_gates_use_the_stricter_seven_degree_slope()
        {
            var s = new SurfaceSample(SemanticTag.Grass, 10f, 2f, 3f);
            Assert.IsTrue(SurfaceAcceptance.IsAccepted(s, PlacementPurpose.PetIdleOrTraining),
                          "10 deg is within the 12 deg idle limit");
            Assert.IsFalse(SurfaceAcceptance.IsAccepted(s, PlacementPurpose.RankedGate),
                           "10 deg exceeds the 7 deg ranked limit");
        }

        [Test]
        public void Clearance_requirement_depends_on_purpose()
        {
            // Section 5.3's 1.5 m clearanceRadius belongs to the SPATIAL OBJECT contract,
            // which governs course content published at a VPS site. Section 13.3's
            // on-device validation for PET placement never restates it.
            //
            // Applying the course figure to pet placement made indoor play impossible:
            // 1.5 m radius is 3 m of clear floor, and ARCore fragments floors into many
            // smaller planes, so no single plane ever qualifies in a room.
            Assert.IsNull(
                SurfaceAcceptance.Reject(new SurfaceSample(SemanticTag.Floor, 0f, 0.6f, 3f),
                                         PlacementPurpose.PetIdleOrTraining),
                "A 0.6 m clearance is ample for a 0.5 m dog.");

            Assert.AreEqual("CLEARANCE_RADIUS",
                SurfaceAcceptance.Reject(new SurfaceSample(SemanticTag.Floor, 0f, 0.6f, 3f),
                                         PlacementPurpose.RankedGate),
                "A ranked gate still requires the full section 5.3 course-object clearance.");

            Assert.AreEqual("CLEARANCE_RADIUS",
                SurfaceAcceptance.Reject(new SurfaceSample(SemanticTag.Floor, 0f, 0.2f, 3f),
                                         PlacementPurpose.PetIdleOrTraining),
                "Even a pet needs more room than its own footprint.");
        }

        [Test]
        public void Readiness_and_placement_share_the_same_clearance_radius_boundary()
        {
            float required = SurfaceAcceptance.RequiredClearanceRadius(
                PlacementPurpose.PetIdleOrTraining);

            Assert.IsFalse(SurfaceAcceptance.HasRequiredClearanceRadius(
                required - 0.001f, PlacementPurpose.PetIdleOrTraining));
            Assert.IsTrue(SurfaceAcceptance.HasRequiredClearanceRadius(
                required, PlacementPurpose.PetIdleOrTraining));

            Assert.AreEqual("CLEARANCE_RADIUS",
                SurfaceAcceptance.Reject(
                    new SurfaceSample(SemanticTag.Floor, 0f, required - 0.001f, 3f),
                    PlacementPurpose.PetIdleOrTraining));
            Assert.IsNull(
                SurfaceAcceptance.Reject(
                    new SurfaceSample(SemanticTag.Floor, 0f, required, 3f),
                    PlacementPurpose.PetIdleOrTraining));
        }

        [Test]
        public void Clearance_height_is_unchanged_by_purpose()
        {
            Assert.AreEqual("CLEARANCE_HEIGHT",
                SurfaceAcceptance.Reject(new SurfaceSample(SemanticTag.Floor, 0f, 2f, 1.9f),
                                         PlacementPurpose.PetIdleOrTraining));
        }
    }
}

namespace Gibi.Tests.EditMode
{
    public class GW_GAME_001_SafetyOverride
    {
        [Test]
        public void Safety_interrupts_every_lower_priority_layer_immediately()
        {
            var clock = new FakeClock();
            var arb = new BehaviorArbiter(clock);

            // Occupy the arbiter with a locked, non-interruptible player cue.
            Assert.IsTrue(arb.Propose(BehaviorLayer.PlayerCue, "SIT", 5000, interruptible: false));
            Assert.AreEqual("SIT", arb.CurrentActionKey);

            var safety = arb.ForceSafety("FREEZE", 2000);

            Assert.AreEqual(BehaviorLayer.SafetyOverride, safety.Layer);
            Assert.AreEqual("FREEZE", arb.CurrentActionKey);
        }

        [Test]
        public void Lower_priority_cannot_preempt_a_locked_action()
        {
            var clock = new FakeClock();
            var arb = new BehaviorArbiter(clock);

            arb.ForceSafety("RETURN_TO_SAFE_ZONE", 5000);

            Assert.IsFalse(arb.Propose(BehaviorLayer.AiIntent, "INVITE_PLAY", 1000));
            Assert.IsFalse(arb.Propose(BehaviorLayer.NeedsScheduler, "REST", 1000));
            Assert.IsFalse(arb.Propose(BehaviorLayer.PlayerCue, "FETCH", 1000));
            Assert.AreEqual("RETURN_TO_SAFE_ZONE", arb.CurrentActionKey);
        }

        [Test]
        public void Arbiter_evaluates_at_ten_hertz()
        {
            Assert.AreEqual(10, BehaviorArbiter.TickHz);
            Assert.AreEqual(100, BehaviorArbiter.TickIntervalMs);
        }

        [Test]
        public void Missing_ai_mapping_falls_back_to_calm_idle_without_error()
        {
            var arb = new BehaviorArbiter(new FakeClock());
            Assert.AreEqual("CALM_IDLE", arb.CurrentActionKey);
        }
    }

    public class GW_GAME_002_FrameRateDeterminism
    {
        [Test]
        public void Locomotion_distance_is_identical_at_30_60_and_120_fps()
        {
            double DistanceOver(double seconds, double frameDelta)
            {
                var motion = new DeterministicMotion();
                var acc = new FixedStepAccumulator();
                motion.SetGait(Gait.Trot);

                int frames = (int)(seconds / frameDelta);
                for (int i = 0; i < frames; i++)
                {
                    // Consume() MUST be hoisted. In a loop condition it is re-evaluated
                    // every iteration, feeding the accumulator an extra frame each pass
                    // until MaxStepsPerFrame clamps -- which clamps at different points
                    // for different frame rates and manufactures the exact divergence
                    // this test exists to detect. PetController.FixedUpdate hoists it.
                    int steps = acc.Consume(frameDelta);
                    for (int s = 0; s < steps; s++)
                        motion.Step(0f);
                }

                return motion.DistanceTravelledM;
            }

            double at30  = DistanceOver(10.0, 1.0 / 30.0);
            double at60  = DistanceOver(10.0, 1.0 / 60.0);
            double at120 = DistanceOver(10.0, 1.0 / 120.0);

            // All three must land on the same whole number of 50 Hz steps.
            Assert.AreEqual(at60, at30, 0.021, "30 vs 60 fps must not diverge beyond one step");
            Assert.AreEqual(at120, at60, 0.021, "60 vs 120 fps must not diverge beyond one step");

            // And the absolute value must match the reference trot speed.
            Assert.AreEqual(20.0, at60, 0.05, "2.0 m/s for 10 s");
        }

        [Test]
        public void Fixed_timestep_is_exactly_fifty_hertz()
        {
            Assert.AreEqual(50, DeterministicMotion.FixedHz);
            Assert.AreEqual(0.02f, DeterministicMotion.FixedDeltaS, 1e-9f);
        }
    }

    public class GW_GAME_003_SweptGateCrossing
    {
        private static GatePlane Gate(int order) =>
            new GatePlane(order,
                          centre: new Vec3(0, 0, 0),
                          normal: new Vec3(0, 0, 1),
                          rightAxis: new Vec3(1, 0, 0),
                          halfWidthM: 1.0,
                          heightM: 1.5);

        [Test]
        public void Fast_pass_that_skips_the_plane_between_frames_is_still_detected()
        {
            // 3.8 m/s at 60 fps moves 63 mm per frame; place samples either side.
            var prev = new Vec3(0, 0.2, -0.35);
            var curr = new Vec3(0, 0.2,  0.35);

            Assert.IsTrue(GateCrossingDetector.SweptCrosses(Gate(0), prev, curr, 0.12),
                          "point containment would miss this; sweep must catch it");
        }

        [Test]
        public void Passing_beside_the_gate_post_does_not_count()
        {
            var prev = new Vec3(3.0, 0.2, -0.35);
            var curr = new Vec3(3.0, 0.2,  0.35);
            Assert.IsFalse(GateCrossingDetector.SweptCrosses(Gate(0), prev, curr, 0.12));
        }

        [Test]
        public void Out_of_order_crossing_is_flagged()
        {
            var det = new GateCrossingDetector();
            var gates = new[] { Gate(0), new GatePlane(1, new Vec3(0,0,5), new Vec3(0,0,1),
                                                       new Vec3(1,0,0), 1.0, 1.5) };

            // Cross gate 1 first, skipping gate 0.
            det.Observe(gates, new Vec3(0, 0.2, 4.65), new Vec3(0, 0.2, 5.35), 0.12);
            Assert.IsTrue(det.OutOfOrderDetected);
        }
    }

    public class GW_SEC_ConstantTimeDigest
    {
        [Test]
        public void Digest_comparison_rejects_mismatch()
        {
            Assert.IsTrue(AssetVerifier.ConstantTimeEquals(new string('a', 64), new string('a', 64)));
            Assert.IsFalse(AssetVerifier.ConstantTimeEquals(new string('a', 64), new string('b', 64)));
            Assert.IsFalse(AssetVerifier.ConstantTimeEquals(null, new string('a', 64)));
            Assert.IsFalse(AssetVerifier.ConstantTimeEquals("abc", "abcd"));
        }

        [Test]
        public void Transfer_limit_is_45_mebibytes()
        {
            Assert.AreEqual(45L * 1024 * 1024, AssetVerifier.MaxTransferBytes);
        }
    }

    public class GW_SafetyGate_PassengerMode
    {
        [Test]
        public void Sustained_speed_above_threshold_enters_passenger_safe_mode()
        {
            var clock = new FakeClock();
            var gate = new PlayerSafetyGate(clock);

            gate.Tick(5.0f);
            Assert.AreEqual(SafetyMode.Normal, gate.Mode, "must not trip instantly");

            clock.Advance(10_000);
            gate.Tick(5.0f);

            Assert.AreEqual(SafetyMode.PassengerSafe, gate.Mode);
            Assert.IsFalse(gate.AllowsPlacement);
            Assert.IsFalse(gate.AllowsTraining);
            Assert.IsFalse(gate.AllowsCourseRun);
            Assert.IsTrue(gate.AllowsMapNavigation, "map browsing stays available");
        }
    }

    public class GW_API_OpaqueIdentifiers
    {
        [Test]
        public void Sequential_database_ids_are_recognised_and_rejectable()
        {
            Assert.IsTrue(GibiId.LooksLikeSequentialDbId("12345"));
            Assert.IsFalse(GibiId.LooksLikeSequentialDbId("pet_01J8ZQK5T7VN2MXR4WD6GHYAB3"));
        }

        [Test]
        public void Prefixed_ulid_pattern_is_enforced_per_entity()
        {
            Assert.IsTrue(GibiId.IsPet("pet_01J8ZQK5T7VN2MXR4WD6GHYAB3"));
            Assert.IsFalse(GibiId.IsPet("asset_01J8ZQK5T7VN2MXR4WD6GHYAB3"), "prefix must match entity");
            Assert.IsFalse(GibiId.IsPet("pet_01J8ZQK5T7VN2MXR4WD6GHYABI"), "I is not in Crockford base32");
        }
    }
}
