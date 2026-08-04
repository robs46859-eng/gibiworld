// Section 5.3 / section 14 — every placement state must be legible without colour.
using NUnit.Framework;
using Gibi.Core;
using Gibi.Gameplay;
using Gibi.Spatial;
using Gibi.UI;
using UnityEditor;
using UnityEngine;

namespace Gibi.Tests.EditMode
{
    public class GW_PlacementAccessibility
    {
        private static PlacementController MakeController(SurfaceProbeResult probe,
                                                          out FakeSurfaceProbe fake)
        {
            var go = new UnityEngine.GameObject("placement");
            var pc = go.AddComponent<PlacementController>();
            fake = new FakeSurfaceProbe { NextResult = probe, CameraDistance = 1.5f };
            pc.ConfigureForTest(fake, new FakeClock(), AnchorState.LocalReady);
            return pc;
        }

        [Test]
        public void Every_rejection_carries_an_icon_and_a_label_not_just_a_colour()
        {
            // No AR session -> rejected. The state must still be legible without colour.
            var pc = MakeController(SurfaceProbeResult.Miss, out _);
            var status = pc.Evaluate(new UnityEngine.Vector2(0.5f, 0.5f), playerSpeedMps: 0f);

            Assert.IsFalse(status.CanPlace);
            Assert.IsTrue(PlacementRing.IsAccessiblyEncoded(status),
                "Section 5.3: colour alone is insufficient — icon and label are required.");
        }

        [Test]
        public void Passenger_safe_mode_is_announced_accessibly()
        {
            var pc = MakeController(SurfaceProbeResult.Miss, out _);
            // Sustained speed above 4.5 m/s trips section 13.3.
            var clock = new FakeClock();
            pc.ConfigureForTest(new FakeSurfaceProbe(), clock);
            pc.Evaluate(UnityEngine.Vector2.zero, 5f);
            clock.Advance(10_000);
            var status = pc.Evaluate(UnityEngine.Vector2.zero, 5f);

            Assert.AreEqual("PASSENGER_SAFE_MODE", status.RejectionCode);
            Assert.IsTrue(PlacementRing.IsAccessiblyEncoded(status));
        }

        [Test]
        public void Hazard_rejection_reaches_the_user_as_a_label_not_a_silent_failure()
        {
            var probe = new SurfaceProbeResult(
                hit: true, position: new UnityEngine.Vector3(0, 0, 3),
                rotation: UnityEngine.Quaternion.identity,
                tag: SemanticTag.Water,          // section 5.3 hazard
                slopeDegrees: 0f, clearanceRadiusM: 2f, clearanceHeightM: 3f,
                lightingConfidence: 1f);

            var pc = MakeController(probe, out _);
            var status = pc.Evaluate(UnityEngine.Vector2.zero, 0f);

            Assert.IsFalse(status.CanPlace);
            Assert.IsTrue(PlacementRing.IsAccessiblyEncoded(status));
            Assert.IsNotEmpty(status.LocalizationKey,
                "Section 14: player-visible strings use localisation keys, never raw text.");
        }

        [Test]
        public void Reticle_defaults_to_thirty_five_percent_screen_height()
        {
            var go = new GameObject("tap-to-place");
            var tapToPlace = go.AddComponent<TapToPlace>();
            var serialized = new SerializedObject(tapToPlace);

            Assert.AreEqual(0.35f,
                            serialized.FindProperty("reticleVerticalRatio").floatValue,
                            0.0001f);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Camera_relative_yaw_flattens_pitch_and_ignores_probe_yaw()
        {
            Quaternion cameraRotation = Quaternion.Euler(25f, 42f, 0f);
            Vector3 expectedForward = cameraRotation * Vector3.forward;
            expectedForward.y = 0f;
            expectedForward.Normalize();
            Quaternion result = PlacementController.CameraRelativeYaw(
                cameraRotation * Vector3.forward,
                Quaternion.Euler(0f, 137f, 0f));

            Assert.That(Vector3.Dot(result * Vector3.forward,
                                    expectedForward),
                        Is.GreaterThan(0.999f));
        }

    }
}
