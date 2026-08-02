// GW-ARCH-001 section 4.2 step 1 — "Read platform input and AR provider frame."
//
// Uses the new Input System exclusively; Active Input Handling is "Input System only"
// because Both is unsupported on Android. EnhancedTouch is enabled explicitly, since it
// is opt-in and silently reports nothing otherwise.
using Gibi.Gameplay;
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

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<P0SessionDriver>();
            if (ring == null) ring = FindAnyObjectByType<PlacementRing>();
        }

        private async void Update()
        {
            if (session == null) return;

            // Section 13.3 wants real device speed; P0 has no locomotion source yet, so
            // it reports stationary. Wiring this to actual movement is a P1 task, and
            // leaving it at 0 means the passenger-safe gate cannot trip on device.
            const float playerSpeedMps = 0f;

            Vector2 point;
            bool tapped = false;

            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;
                point = touch.screenPosition;
                tapped = true;
                await session.TryPlaceAt(point, playerSpeedMps);
                break;
            }

            // Continuous preview so the ring reflects the CURRENT surface even without a
            // tap -- section 5.3 requires the status be visible before committing.
            if (!tapped && ring != null)
            {
                var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                var status = session.PreviewAt(centre, playerSpeedMps);
                ring.Apply(status, hapticsSupported: SystemInfo.supportsVibration);
            }
        }
    }
}
