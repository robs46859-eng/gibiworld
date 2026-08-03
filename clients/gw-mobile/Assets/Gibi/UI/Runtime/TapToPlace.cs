// GW-ARCH-001 section 4.2 step 1 — "Read platform input and AR provider frame."
//
// Uses the new Input System exclusively; Active Input Handling is "Input System only"
// because Both is unsupported on Android. EnhancedTouch is enabled explicitly, since it
// is opt-in and silently reports nothing otherwise.
using Gibi.Gameplay;
using Gibi.Spatial;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Gibi.UI
{
    [DisallowMultipleComponent]
    public sealed class TapToPlace : MonoBehaviour
    {
        [SerializeField] private P0SessionDriver session;
        [SerializeField] private PlacementRing ring;
        [SerializeField] private ARSessionDriver arSession;

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            TouchSimulation.Enable();   // lets a mouse drive it in the editor
        }

        private void OnDisable()
        {
            TouchSimulation.Disable();
            EnhancedTouchSupport.Disable();
        }

        private string _lastStatusCode;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<P0SessionDriver>();
            if (ring == null) ring = FindAnyObjectByType<PlacementRing>();
            if (arSession == null) arSession = FindAnyObjectByType<ARSessionDriver>();
        }

        private async void Update()
        {
            if (session == null) return;
            if (session.PetIsPlaced)
            {
                ring?.Hide();
                return;
            }

            // Section 13.3 wants real device speed; P0 has no locomotion source yet, so
            // it reports stationary. Wiring this to actual movement is a P1 task, and
            // leaving it at 0 means the passenger-safe gate cannot trip on device.
            const float playerSpeedMps = 0f;

            // The RETICLE is the aim; the tap only confirms it.
            //
            // Placing at the raw touch point looks reasonable and is wrong: the player
            // sees a green ring at screen centre, taps it, and the pet is placed wherever
            // their finger landed instead — typically low on the screen, which aims at the
            // floor by their feet and trips section 13.3's 1.5 m camera clearance. The ring
            // then reads as a liar. Centre-reticle plus confirm is the standard AR
            // placement idiom for exactly this reason.
            var reticle = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            bool tapped = false;

            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;
                tapped = true;
                Debug.Log($"[GibiWorld] tap confirmed at reticle {reticle} " +
                          $"(finger was at {touch.screenPosition})");
                bool placed = await session.TryPlaceAt(reticle, playerSpeedMps);
                if (placed) arSession?.SetPlaneVisualizationVisible(false);
                Debug.Log(placed
                    ? "[GibiWorld] PET PLACED"
                    : $"[GibiWorld] placement rejected: {session.LastFailureCode}");
                break;
            }

            // Continuous preview so the ring reflects the CURRENT surface even without a
            // tap -- section 5.3 requires the status be visible before committing.
            if (!tapped && ring != null)
            {
                var status = session.PreviewAt(reticle, playerSpeedMps);

                if (!session.HasHit)
                {
                    // Nothing under the reticle. A ring floating in space with no surface
                    // to sit on is worse than no ring at all.
                    ring.Hide();
                }
                else
                {
                    ring.SetPose(session.LastHitPose);
                    ring.Apply(status, hapticsSupported: SystemInfo.supportsVibration);
                }

                // Log only on CHANGE. Logging every frame at 60 fps drowns logcat and
                // makes the one line that matters impossible to find.
                string code = status.CanPlace ? "READY" : status.RejectionCode;
                if (code != _lastStatusCode)
                {
                    _lastStatusCode = code;
                    Debug.Log($"[GibiWorld] placement: {code} | {session.LastMeasurements}");
                }
            }
        }

        /// <summary>UI button seam for an explicit reset; ordinary taps never relocate.</summary>
        public void ResetPlacement()
        {
            if (session != null && session.ResetPlacedWorld())
            {
                _lastStatusCode = null;
                arSession?.SetPlaneVisualizationVisible(true);
                ring?.Hide();
            }
        }
    }
}
