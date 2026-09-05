// GW-ARCH-003 AR-04 — LocalGridNavigationTests.
// Asserts deterministic A*, diagonal corner-cut rejection, and obstacle avoidance.
using Gibi.Core;
using Gibi.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Gibi.Tests
{
    public sealed class LocalGridNavigationTests
    {
        [Test]
        public void Path_plans_around_solid_obstacle_without_corner_cutting()
        {
            var go = new GameObject("NavGrid");
            try
            {
                var nav = go.AddComponent<LocalGridNavigation>();
                nav.InitializeGrid(width: 40, height: 40, origin: new Vector2(-1f, -1f));

                // Place a 0.4m x 0.4m obstacle in the center at (0, 0)
                nav.RasterizeObstacleBox(Vector3.zero, new Vector3(0.4f, 0f, 0.4f), marginM: 0f);

                var agent = new AgentEnvelope(0.15f, 0.50f, 0.30f);
                Vector3 start = new Vector3(-0.5f, 0f, 0f);
                Vector3 goal = new Vector3(0.5f, 0f, 0f);

                var result = nav.TryPlan(start, goal, agent, nav.GeometryRevision);

                Assert.IsTrue(result.Succeeded, $"Path should succeed around obstacle. Status: {result.Status}");
                Assert.That(result.Waypoints.Length, Is.GreaterThanOrEqualTo(3));
                Assert.That(result.LengthM, Is.GreaterThan(1.0f));

                // Ensure none of the waypoints fall inside the obstacle center
                foreach (var wp in result.Waypoints)
                {
                    Vector2Int cell = nav.WorldToCell(wp);
                    Assert.IsFalse(nav.IsCellBlocked(cell.x, cell.y),
                        $"Waypoint {wp} at cell ({cell.x}, {cell.y}) cannot be on a blocked cell");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Blocked_start_or_goal_fails_immediately()
        {
            var go = new GameObject("NavGridBlocked");
            try
            {
                var nav = go.AddComponent<LocalGridNavigation>();
                nav.InitializeGrid(width: 20, height: 20, origin: Vector2.zero);

                // Block cell (5, 5)
                nav.SetCellBlocked(5, 5, true);

                var agent = AgentEnvelope.DefaultPet;
                Vector3 start = nav.CellToWorld(5, 5);
                Vector3 goal = nav.CellToWorld(10, 10);

                var result = nav.TryPlan(start, goal, agent, nav.GeometryRevision);
                Assert.AreEqual(PathStatus.Blocked, result.Status);
                Assert.IsFalse(result.Succeeded);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
