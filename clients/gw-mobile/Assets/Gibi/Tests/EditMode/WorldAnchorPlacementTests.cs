using System.Threading;
using System.Threading.Tasks;
using Gibi.Core;
using Gibi.Gameplay;
using Gibi.Pets;
using Gibi.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Gibi.Tests.EditMode
{
    public sealed class FakeWorldAnchorHost : MonoBehaviour, IWorldAnchorHost
    {
        public string FailureCode = "ANCHOR_CREATE_FAILED";
        public int CreateCalls;
        public int ResetCalls;

        public Task<WorldAnchorResult> TryCreateAnchorAsync(
            Pose pose, CancellationToken cancellationToken)
        {
            CreateCalls++;
            return Task.FromResult(WorldAnchorResult.Failed(FailureCode));
        }

        public void ResetAnchor() => ResetCalls++;
    }

    public class WorldAnchorPlacementTests
    {
        [Test]
        public void Accepted_surface_does_not_reveal_world_when_anchor_creation_fails()
        {
            var sceneRoot = new GameObject("test-root");
            var placement = sceneRoot.AddComponent<PlacementController>();
            placement.ConfigureForTest(
                new FakeSurfaceProbe
                {
                    NextResult = new SurfaceProbeResult(
                        hit: true,
                        position: new Vector3(0f, 0f, 1.5f),
                        rotation: Quaternion.identity,
                        tag: SemanticTag.Floor,
                        slopeDegrees: 0f,
                        clearanceRadiusM: 1f,
                        clearanceHeightM: 3f,
                        lightingConfidence: 1f),
                    CameraDistance = 1.5f,
                },
                new FakeClock(),
                AnchorState.LocalReady);

            var anchorHost = sceneRoot.AddComponent<FakeWorldAnchorHost>();
            var world = new GameObject("PlacedWorldRoot");
            world.transform.SetParent(sceneRoot.transform, false);
            world.SetActive(false);
            var boundary = world.AddComponent<SandboxBoundary>();

            var sessionObject = new GameObject("P0Session");
            sessionObject.transform.SetParent(sceneRoot.transform, false);
            var session = sessionObject.AddComponent<P0SessionDriver>();
            session.ConfigurePlacement(placement);
            session.ConfigureWorld(world.transform, boundary, null,
                                   autoSpawn: false, anchorHostBehaviour: anchorHost);

            PlacementStatus status = placement.Evaluate(Vector2.zero, 0f);
            Assert.IsTrue(status.CanPlace,
                $"Test setup did not reach the anchor gate: {status.RejectionCode}");
            Assert.IsTrue(session.CanPlace, "Session should accept its first placement attempt.");

            bool placed = session.TryPlaceAt(Vector2.zero, 0f).GetAwaiter().GetResult();

            Assert.IsFalse(placed);
            Assert.AreEqual("ANCHOR_CREATE_FAILED", session.LastFailureCode);
            Assert.AreEqual(1, anchorHost.CreateCalls);
            Assert.IsFalse(world.activeSelf,
                "The dog, toy, and house must remain hidden when no AR anchor exists.");

            Object.DestroyImmediate(sceneRoot);
        }
    }
}
