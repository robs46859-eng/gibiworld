using Gibi.Core;
using Gibi.Pets;
using NUnit.Framework;
using UnityEngine;

namespace Gibi.Tests
{
    public sealed class SandboxInteractionTests
    {
        [Test]
        public void Fetch_sequence_has_one_legal_deterministic_path()
        {
            var sequence = new FetchSequence();

            Assert.IsTrue(sequence.Begin());
            Assert.AreEqual(FetchStage.Outbound, sequence.Stage);
            Assert.AreEqual(FetchTransition.StartPickup, sequence.ReachedTarget());
            Assert.AreEqual(FetchStage.Pickup, sequence.Stage);
            Assert.AreEqual(FetchTransition.StartReturn, sequence.ActionFinished());
            Assert.AreEqual(FetchStage.Returning, sequence.Stage);
            Assert.AreEqual(FetchTransition.StartDrop, sequence.ReachedTarget());
            Assert.AreEqual(FetchStage.Drop, sequence.Stage);
            Assert.AreEqual(FetchTransition.Completed, sequence.ActionFinished());
            Assert.AreEqual(FetchStage.Idle, sequence.Stage);
            Assert.AreEqual(1, sequence.CompletedCount);
        }

        [Test]
        public void Fetch_sequence_rejects_out_of_order_callbacks()
        {
            var sequence = new FetchSequence();

            Assert.AreEqual(FetchTransition.None, sequence.ReachedTarget());
            Assert.AreEqual(FetchTransition.None, sequence.ActionFinished());
            Assert.IsTrue(sequence.Begin());
            Assert.IsFalse(sequence.Begin());
            Assert.AreEqual(FetchTransition.None, sequence.ActionFinished());
            Assert.AreEqual(FetchStage.Outbound, sequence.Stage);
        }

        [Test]
        public void Stale_fetch_completion_cannot_clear_a_safety_override()
        {
            var arbiter = new BehaviorArbiter(new FakeClock());
            Assert.IsTrue(arbiter.Propose(
                BehaviorLayer.PlayerCue, "FETCH", 30000, interruptible: false));
            arbiter.ForceSafety("STOP", 3000);

            Assert.IsFalse(arbiter.CompleteIfCurrent("FETCH"));
            Assert.AreEqual("STOP", arbiter.CurrentActionKey);
        }

        [Test]
        public void Sandbox_boundary_clamps_only_local_horizontal_axes()
        {
            var root = new GameObject("Boundary");
            try
            {
                root.transform.SetPositionAndRotation(
                    new Vector3(10f, 2f, -4f), Quaternion.Euler(0f, 90f, 0f));
                var boundary = root.AddComponent<SandboxBoundary>();
                boundary.Configure(new Vector2(2f, 1f));

                Vector3 outside = root.transform.TransformPoint(new Vector3(4f, 0.5f, -3f));
                Vector3 clamped = root.transform.InverseTransformPoint(
                    boundary.ClampWorld(outside, 0.2f));

                Assert.That(clamped.x, Is.EqualTo(1.8f).Within(0.0001f));
                Assert.That(clamped.z, Is.EqualTo(-0.8f).Within(0.0001f));
                Assert.That(clamped.y, Is.EqualTo(0.5f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Deterministic_motion_can_turn_without_translating()
        {
            var motion = new DeterministicMotion();
            motion.SetGait(Gait.Run);

            motion.Step(DeterministicMotion.MaxYawRateDegPerS, advance: false);

            Assert.Greater(motion.HeadingDeg, 0d);
            Assert.AreEqual(0d, motion.DistanceTravelledM);
            Assert.AreEqual(1, motion.StepCount);
        }
    }
}
