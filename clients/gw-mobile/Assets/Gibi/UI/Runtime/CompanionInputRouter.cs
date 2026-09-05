// GW-ARCH-003 W06 & FETCH-01, SYS-02 — CompanionInputRouter.
// Directs touch and accessible UI commands to the companion session.
// Supported commands: Fetch, Come, Sit, Home, Pet, Pause.
// Features: Drag-to-throw, accessible tap-target + 3-step Throw button,
// UI pointer exclusion to prevent touches through buttons.
using Gibi.Gameplay;
using Gibi.Pets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Gibi.UI
{
    public enum InputMode
    {
        DirectCommands,
        AimingThrow,
    }

    [DisallowMultipleComponent]
    public sealed class CompanionInputRouter : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private P0SessionDriver session;
        [SerializeField] private FetchSession fetchSession;
        [SerializeField] private FetchAimView aimView;
        [SerializeField] private PetController pet;
        [SerializeField] private RestAffordance dogHouse;

        [Header("Aiming Parameters")]
        [SerializeField] private float launchHeightOffsetM = 0.20f;
        [SerializeField] private float minDragRatio = 0.05f;
        [SerializeField] private float maxDragRatio = 0.30f;
        [SerializeField] private float minThrowDistM = 0.6f;
        [SerializeField] private float maxThrowDistM = 2.5f;

        private InputMode _mode = InputMode.DirectCommands;
        private Vector2 _dragStartScreenPos;
        private bool _isDragging = false;
        private ThrowPlan _currentPlan;
        private int _accessibleDistanceStep = 1; // 0=Short, 1=Medium, 2=Long

        public InputMode Mode => _mode;
        public bool IsAiming => _mode == InputMode.AimingThrow;

        private void Awake()
        {
            if (session == null) session = FindAnyObjectByType<P0SessionDriver>();
            if (fetchSession == null) fetchSession = FindAnyObjectByType<FetchSession>();
            if (aimView == null) aimView = FindAnyObjectByType<FetchAimView>();
            if (pet == null) pet = FindAnyObjectByType<PetController>();
            if (dogHouse == null) dogHouse = FindAnyObjectByType<RestAffordance>();
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            TouchSimulation.Enable();
        }

        private void OnDisable()
        {
            TouchSimulation.Disable();
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            if (_mode != InputMode.AimingThrow) return;

            HandleAimingTouches();
        }

        // ---------------- Player Commands ----------------

        public void CommandFetch()
        {
            if (fetchSession == null) return;
            if (fetchSession.BeginAim())
            {
                _mode = InputMode.AimingThrow;
                _isDragging = false;
                ComputeAimFromReticle(GetReticleCenterScreen());
            }
        }

        public void CommandCome()
        {
            if (_mode == InputMode.AimingThrow) CancelAim();
            Camera cam = Camera.main;
            Vector3 targetPos = cam != null ? cam.transform.position : transform.position;
            session?.CueCome(targetPos);
        }

        public void CommandSit()
        {
            if (_mode == InputMode.AimingThrow) CancelAim();
            session?.CueSit();
        }

        public void CommandHome()
        {
            if (_mode == InputMode.AimingThrow) CancelAim();
            if (dogHouse != null && pet != null)
            {
                pet.CueRest(dogHouse);
            }
        }

        public void CommandPet()
        {
            if (_mode == InputMode.AimingThrow) CancelAim();
            pet?.CuePet();
        }

        public void CommandPause()
        {
            if (_mode == InputMode.AimingThrow) CancelAim();
            fetchSession?.Suspend();
        }

        public void CancelAim()
        {
            _mode = InputMode.DirectCommands;
            _isDragging = false;
            aimView?.Hide();
            fetchSession?.CancelAim();
        }

        // ---------------- Accessible Throw Controls ----------------

        public void CycleAccessibleDistance()
        {
            _accessibleDistanceStep = (_accessibleDistanceStep + 1) % 3;
            ComputeAccessibleThrowPlan();
        }

        public void CommandAccessibleThrow()
        {
            if (_mode != InputMode.AimingThrow) return;
            ComputeAccessibleThrowPlan();

            if (_currentPlan.IsValid)
            {
                Camera cam = Camera.main;
                Vector3 userPos = cam != null ? cam.transform.position : transform.position;
                fetchSession?.ExecuteThrow(_currentPlan, userPos);
                _mode = InputMode.DirectCommands;
                aimView?.Hide();
            }
        }

        private void ComputeAccessibleThrowPlan()
        {
            Camera cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : transform.position;
            Vector3 camFwd = cam != null ? cam.transform.forward : transform.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude < 0.01f) camFwd = Vector3.forward;
            camFwd.Normalize();

            float dist = _accessibleDistanceStep switch
            {
                0 => 0.8f,
                1 => 1.5f,
                2 => 2.2f,
                _ => 1.5f
            };

            Vector3 launchPoint = camPos + camFwd * 0.2f + Vector3.up * launchHeightOffsetM;
            Vector3 groundTarget = new Vector3(camPos.x, 0f, camPos.z) + camFwd * dist;

            _currentPlan = ThrowSolver.Solve(launchPoint, groundTarget);
            aimView?.RenderPlan(_currentPlan);
        }

        // ---------------- Gesture Aiming ----------------

        private void HandleAimingTouches()
        {
            foreach (var touch in Touch.activeTouches)
            {
                // UI touch exclusion (SYS-02)
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.touchId))
                    continue;

                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    _dragStartScreenPos = touch.screenPosition;
                    _isDragging = true;
                }
                else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved && _isDragging)
                {
                    UpdatePlanFromDrag(touch.screenPosition);
                }
                else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended && _isDragging)
                {
                    _isDragging = false;
                    UpdatePlanFromDrag(touch.screenPosition);

                    if (_currentPlan.IsValid)
                    {
                        Camera cam = Camera.main;
                        Vector3 userPos = cam != null ? cam.transform.position : transform.position;
                        fetchSession?.ExecuteThrow(_currentPlan, userPos);
                        _mode = InputMode.DirectCommands;
                        aimView?.Hide();
                    }
                    else
                    {
                        CancelAim();
                    }
                    break;
                }
            }
        }

        private void UpdatePlanFromDrag(Vector2 currentScreenPos)
        {
            Vector2 delta = currentScreenPos - _dragStartScreenPos;
            float verticalFraction = Mathf.Clamp(delta.y / Screen.height, minDragRatio, maxDragRatio);
            float distRatio = Mathf.Clamp01((verticalFraction - minDragRatio) / (maxDragRatio - minDragRatio));
            float throwDist = Mathf.Lerp(minThrowDistM, maxThrowDistM, distRatio);

            Camera cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : transform.position;
            Vector3 camFwd = cam != null ? cam.transform.forward : transform.forward;
            Vector3 camRight = cam != null ? cam.transform.right : transform.right;

            camFwd.y = 0f;
            camRight.y = 0f;
            camFwd.Normalize();
            camRight.Normalize();

            // Horizontal drag offsets bearing angle
            float bearingAngleDeg = Mathf.Clamp((delta.x / Screen.width) * 45f, -30f, 30f);
            Vector3 throwDir = Quaternion.Euler(0f, bearingAngleDeg, 0f) * camFwd;

            Vector3 launchPoint = camPos + camFwd * 0.2f + Vector3.up * launchHeightOffsetM;
            Vector3 groundTarget = new Vector3(camPos.x, 0f, camPos.z) + throwDir * throwDist;

            _currentPlan = ThrowSolver.Solve(launchPoint, groundTarget);
            aimView?.RenderPlan(_currentPlan);
        }

        private void ComputeAimFromReticle(Vector2 reticle)
        {
            ComputeAccessibleThrowPlan();
        }

        private static Vector2 GetReticleCenterScreen()
            => new Vector2(Screen.width * 0.5f, Screen.height * 0.35f);
    }
}
