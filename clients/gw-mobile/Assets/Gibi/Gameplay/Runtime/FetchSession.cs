// GW-ARCH-003 FETCH-01..06 & W07 — FetchSession.
// Complete player-driven fetch round coordinator.
// States: Ready -> Aiming -> Flight -> Settling -> Outbound -> Pickup -> Returning -> Drop -> Celebrate -> Ready.
// Single toy ownership, ActionToken concurrency guard, timeout recoveries, moving return zone handling.
using System;
using Gibi.Core;
using Gibi.Pets;
using UnityEngine;

namespace Gibi.Gameplay
{
    public enum FetchRoundPhase
    {
        Ready,
        Aiming,
        Flight,
        Settling,
        Outbound,
        Pickup,
        Returning,
        Drop,
        Celebrate,
        Suspended,
        Recovering,
    }

    [DisallowMultipleComponent]
    public sealed class FetchSession : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ToyController toy;
        [SerializeField] private PetController pet;
        [SerializeField] private Transform playAreaCenter;

        [Header("Tuning")]
        [SerializeField] private float flightTimeoutS = 1.5f;
        [SerializeField] private float settleTimeoutS = 0.5f;
        [SerializeField] private float pickupTimeoutS = 2.5f;
        [SerializeField] private float dropTimeoutS = 2.5f;
        [SerializeField] private float returnRoundCapS = 45f;
        [SerializeField] private float celebrateDurationS = 1.2f;

        private FetchRoundPhase _phase = FetchRoundPhase.Ready;
        private ActionToken _activeToken = ActionToken.None;
        private ThrowPlan _activePlan;
        private float _phaseTimer = 0f;
        private float _roundTimer = 0f;
        private Vector3 _currentReturnZone;
        private Vector3 _lastUserPos;
        private float _lastReturnZoneUpdateSec = 0f;
        private int _roundSequence = 0;
        private int _sessionGeneration = 1;

        public FetchRoundPhase Phase => _phase;
        public ActionToken ActiveToken => _activeToken;
        public int CompletedRounds { get; private set; } = 0;
        public bool IsActive => _phase != FetchRoundPhase.Ready;

        public event Action<ActionToken> OnRoundStarted;
        public event Action<ActionToken, int> OnRoundCompleted;
        public event Action<ActionToken, CancelReason> OnRoundCancelled;

        public void Configure(ToyController toyController, PetController petController, int sessionGen = 1)
        {
            toy = toyController;
            pet = petController;
            _sessionGeneration = Mathf.Max(1, sessionGen);
        }

        /// <summary>
        /// Player selects Fetch button to enter Aiming mode.
        /// </summary>
        public bool BeginAim()
        {
            if (_phase != FetchRoundPhase.Ready) return false;
            if (pet == null || toy == null || toy.IsInPlay) return false;

            _roundSequence++;
            _activeToken = new ActionToken(_sessionGeneration, _roundSequence, pet.name);
            _phase = FetchRoundPhase.Aiming;
            _phaseTimer = 0f;
            return true;
        }

        /// <summary>
        /// Cancels aiming and returns to Ready.
        /// </summary>
        public void CancelAim()
        {
            if (_phase != FetchRoundPhase.Aiming) return;
            _phase = FetchRoundPhase.Ready;
            _activeToken = ActionToken.None;
        }

        /// <summary>
        /// Player releases throw or triggers Throw button.
        /// </summary>
        public bool ExecuteThrow(ThrowPlan plan, Vector3 initialUserPos)
        {
            if (_phase != FetchRoundPhase.Aiming || !plan.IsValid)
                return false;

            _activePlan = plan;
            _lastUserPos = initialUserPos;
            _currentReturnZone = CalculateReturnZone(initialUserPos);

            if (!toy.StartFlight(_activeToken, plan.LaunchPoint))
            {
                Cancel(CancelReason.SafetyStop);
                return false;
            }

            _phase = FetchRoundPhase.Flight;
            _phaseTimer = 0f;
            _roundTimer = 0f;
            OnRoundStarted?.Invoke(_activeToken);
            return true;
        }

        private void Update()
        {
            if (_phase == FetchRoundPhase.Ready || _phase == FetchRoundPhase.Aiming)
                return;

            _phaseTimer += Time.deltaTime;
            _roundTimer += Time.deltaTime;

            if (_roundTimer > returnRoundCapS && _phase != FetchRoundPhase.Drop && _phase != FetchRoundPhase.Celebrate)
            {
                Cancel(CancelReason.Timeout);
                return;
            }

            switch (_phase)
            {
                case FetchRoundPhase.Flight:
                    UpdateFlight();
                    break;

                case FetchRoundPhase.Settling:
                    UpdateSettling();
                    break;

                case FetchRoundPhase.Outbound:
                    UpdateOutbound();
                    break;

                case FetchRoundPhase.Pickup:
                    UpdatePickup();
                    break;

                case FetchRoundPhase.Returning:
                    UpdateReturning();
                    break;

                case FetchRoundPhase.Drop:
                    UpdateDrop();
                    break;

                case FetchRoundPhase.Celebrate:
                    UpdateCelebrate();
                    break;

                case FetchRoundPhase.Recovering:
                    UpdateRecovering();
                    break;
            }
        }

        private void UpdateFlight()
        {
            if (_phaseTimer >= _activePlan.FlightDurationS)
            {
                toy.StartSettling(_activeToken, _activePlan.LandingPoint);
                _phase = FetchRoundPhase.Settling;
                _phaseTimer = 0f;
                return;
            }

            if (_phaseTimer > flightTimeoutS)
            {
                Cancel(CancelReason.Timeout);
                return;
            }

            Vector3 flightPos = ThrowSolver.EvaluatePosition(
                _activePlan.LaunchPoint, _activePlan.InitialVelocity, _phaseTimer);
            toy.UpdateFlightPose(flightPos, Quaternion.identity);
        }

        private void UpdateSettling()
        {
            float t = Mathf.Clamp01(_phaseTimer / _activePlan.SettleDurationS);
            Vector3 settlePos = Vector3.Lerp(_activePlan.LandingPoint, _activePlan.SettleEndPoint, t);
            toy.UpdateFlightPose(settlePos, Quaternion.identity);

            if (_phaseTimer >= _activePlan.SettleDurationS || _phaseTimer >= settleTimeoutS)
            {
                toy.CompleteSettle(_activeToken, _activePlan.SettleEndPoint);
                toy.ReserveForPickup(_activeToken);

                // Command pet to navigate to settle position
                if (pet != null)
                {
                    pet.CueFetch(_activePlan.SettleEndPoint);
                }

                _phase = FetchRoundPhase.Outbound;
                _phaseTimer = 0f;
            }
        }

        private void UpdateOutbound()
        {
            float maxOutboundS = Mathf.Min(15f, Mathf.Max(5f, Vector3.Distance(pet.transform.position, _activePlan.SettleEndPoint) / 1.2f + 3f));
            if (_phaseTimer > maxOutboundS)
            {
                Cancel(CancelReason.Timeout);
                return;
            }

            if (pet != null && pet.CurrentFetchStage == FetchStage.Pickup)
            {
                _phase = FetchRoundPhase.Pickup;
                _phaseTimer = 0f;
            }
        }

        private void UpdatePickup()
        {
            if (_phaseTimer > pickupTimeoutS)
            {
                Cancel(CancelReason.Timeout);
                return;
            }

            // Verify contact and attach toy to mouth
            if (toy.Ownership == ToyOwnership.ReservedForPickup && pet.MouthSocket != null)
            {
                float dist = Vector3.Distance(toy.transform.position, pet.MouthSocket.position);
                if (dist <= 0.08f) // within approach contact tolerance
                {
                    toy.CommitPickup(_activeToken, pet.MouthSocket, maxDistanceM: 0.08f);
                }
            }

            if (pet != null && pet.CurrentFetchStage == FetchStage.Returning)
            {
                _phase = FetchRoundPhase.Returning;
                _phaseTimer = 0f;
            }
        }

        private void UpdateReturning()
        {
            // Update return zone if player moves (capped at 2 Hz)
            Camera mainCam = Camera.main;
            if (mainCam != null && Time.time - _lastReturnZoneUpdateSec > 0.5f)
            {
                Vector3 currentCamPos = mainCam.transform.position;
                if (Vector3.Distance(currentCamPos, _lastUserPos) > 0.25f)
                {
                    _lastUserPos = currentCamPos;
                    _lastReturnZoneUpdateSec = Time.time;
                    _currentReturnZone = CalculateReturnZone(currentCamPos);

                    // If pet is not yet in final approach (within 0.35m), update target
                    if (Vector3.Distance(pet.transform.position, _currentReturnZone) > 0.35f)
                    {
                        pet.CueCome(_currentReturnZone);
                    }
                }
            }

            if (pet != null && pet.CurrentFetchStage == FetchStage.Drop)
            {
                _phase = FetchRoundPhase.Drop;
                _phaseTimer = 0f;
            }
        }

        private void UpdateDrop()
        {
            if (_phaseTimer > dropTimeoutS)
            {
                toy.RecoverOrReset(pet != null ? pet.transform.position + pet.transform.forward * 0.30f : (Vector3?)null);
                Cancel(CancelReason.Timeout);
                return;
            }

            if (toy.IsHeld)
            {
                Vector3 dropPos = pet != null
                    ? pet.transform.position + pet.transform.forward * 0.30f
                    : transform.position;
                toy.CommitDrop(_activeToken, dropPos);
            }

            if (pet != null && pet.CurrentFetchStage == FetchStage.Idle)
            {
                _phase = FetchRoundPhase.Celebrate;
                _phaseTimer = 0f;
            }
        }

        private void UpdateCelebrate()
        {
            if (_phaseTimer >= celebrateDurationS)
            {
                CompleteRound();
            }
        }

        private void UpdateRecovering()
        {
            if (_phaseTimer >= 0.5f)
            {
                _phase = FetchRoundPhase.Ready;
                _activeToken = ActionToken.None;
            }
        }

        private void CompleteRound()
        {
            CompletedRounds++;
            ActionToken finishedToken = _activeToken;
            _phase = FetchRoundPhase.Ready;
            _activeToken = ActionToken.None;
            OnRoundCompleted?.Invoke(finishedToken, CompletedRounds);
        }

        /// <summary>
        /// Idempotent cancellation. Safely resets toy and pet state.
        /// </summary>
        public void Cancel(CancelReason reason)
        {
            if (_phase == FetchRoundPhase.Ready) return;

            ActionToken cancelledToken = _activeToken;
            _phase = FetchRoundPhase.Recovering;
            _phaseTimer = 0f;

            if (toy != null)
            {
                Vector3? dropLoc = (pet != null && toy.IsHeld)
                    ? pet.transform.position + pet.transform.forward * 0.30f
                    : (Vector3?)null;
                toy.RecoverOrReset(dropLoc);
            }

            if (pet != null)
            {
                pet.SafetyStop(reason.ToString());
            }

            OnRoundCancelled?.Invoke(cancelledToken, reason);
        }

        public void Suspend()
        {
            if (_phase != FetchRoundPhase.Ready && _phase != FetchRoundPhase.Suspended)
            {
                _phase = FetchRoundPhase.Suspended;
            }
        }

        public void Resume()
        {
            if (_phase == FetchRoundPhase.Suspended)
            {
                // Revalidate or resume to Outbound/Returning
                _phase = toy != null && toy.IsHeld ? FetchRoundPhase.Returning : FetchRoundPhase.Outbound;
            }
        }

        private Vector3 CalculateReturnZone(Vector3 cameraPos)
        {
            Vector3 groundCam = new Vector3(cameraPos.x, 0f, cameraPos.z);
            Vector3 center = playAreaCenter != null ? playAreaCenter.position : Vector3.zero;
            center.y = 0f;

            Vector3 toCenter = (center - groundCam).normalized;
            if (toCenter.sqrMagnitude < 0.01f) toCenter = Vector3.forward;

            // 0.8m in front of camera toward play center
            Vector3 zone = groundCam + toCenter * 0.80f;
            return zone;
        }
    }
}
