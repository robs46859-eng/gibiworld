// GW-ARCH-003 FETCH-02 & W07 — ThrowSolverTests.
// Validates throw preview parity, speed/apex bounds, swept obstacle rejection, and analytic determinism.
using Gibi.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Gibi.Tests
{
    public sealed class ThrowSolverTests
    {
        [Test]
        public void Preview_endpoint_matches_analytic_flight_landing_within_one_centimeter()
        {
            Vector3 launchPoint = new Vector3(0f, 0.20f, 0f);
            Vector3 groundTarget = new Vector3(0f, 0f, 1.80f);

            ThrowPlan plan = ThrowSolver.Solve(launchPoint, groundTarget);

            Assert.IsTrue(plan.IsValid, $"Plan should be valid. Rejection: {plan.RejectionReason}");
            Vector3 analyticFinalPos = ThrowSolver.EvaluatePosition(
                plan.LaunchPoint, plan.InitialVelocity, plan.FlightDurationS);

            float error = Vector3.Distance(analyticFinalPos, plan.LandingPoint);
            Assert.That(error, Is.LessThanOrEqualTo(0.01f), "Landing error must be <= 1 cm");
        }

        [Test]
        public void Speed_and_apex_remain_strictly_bounded_across_distance_envelope()
        {
            Vector3 launch = new Vector3(0f, 0.20f, 0f);
            float[] testDistances = { 0.6f, 1.0f, 1.5f, 2.0f, 2.5f };

            foreach (float dist in testDistances)
            {
                Vector3 target = new Vector3(0f, 0f, dist);
                ThrowPlan plan = ThrowSolver.Solve(launch, target);

                Assert.IsTrue(plan.IsValid, $"Valid distance {dist} failed: {plan.RejectionReason}");
                Assert.That(plan.InitialSpeedMps, Is.LessThanOrEqualTo(ThrowSolver.MaxSpeedMps),
                    $"Speed at distance {dist} exceeded maximum ({plan.InitialSpeedMps} > {ThrowSolver.MaxSpeedMps})");
                Assert.That(plan.ApexHeightM, Is.LessThanOrEqualTo(ThrowSolver.MaxApexAboveSupportM),
                    $"Apex at distance {dist} exceeded maximum ({plan.ApexHeightM} > {ThrowSolver.MaxApexAboveSupportM})");
            }
        }

        [Test]
        public void Obstacle_intersection_rejects_throw_plan()
        {
            Vector3 launch = new Vector3(0f, 0.20f, 0f);
            Vector3 target = new Vector3(0f, 0f, 2.0f);

            // Obstacle squarely in the flight path at z = 1.0m, y = 0.4m
            bool ObstaclePredicate(Vector3 pos, float radius)
            {
                return Vector3.Distance(pos, new Vector3(0f, 0.40f, 1.0f)) <= 0.25f;
            }

            ThrowPlan plan = ThrowSolver.Solve(launch, target, ObstaclePredicate);

            Assert.IsFalse(plan.IsValid);
            Assert.AreEqual("TRAJECTORY_OBSTACLE_INTERSECTION", plan.RejectionReason);
        }

        [Test]
        public void Out_of_bounds_and_degenerate_distances_reject_safely()
        {
            Vector3 launch = new Vector3(0f, 0.20f, 0f);

            // Too short (< 0.4m)
            ThrowPlan tooShort = ThrowSolver.Solve(launch, new Vector3(0f, 0f, 0.2f));
            Assert.IsFalse(tooShort.IsValid);
            Assert.AreEqual("DISTANCE_TOO_SHORT", tooShort.RejectionReason);

            // Too long (> 3.0m)
            ThrowPlan tooLong = ThrowSolver.Solve(launch, new Vector3(0f, 0f, 4.5f));
            Assert.IsFalse(tooLong.IsValid);
            Assert.AreEqual("DISTANCE_TOO_LONG", tooLong.RejectionReason);

            // NaN coordinate
            ThrowPlan nanPlan = ThrowSolver.Solve(new Vector3(float.NaN, 0f, 0f), new Vector3(0f, 0f, 1.5f));
            Assert.IsFalse(nanPlan.IsValid);
            Assert.AreEqual("COORDINATES_NAN", nanPlan.RejectionReason);
        }
    }
}
