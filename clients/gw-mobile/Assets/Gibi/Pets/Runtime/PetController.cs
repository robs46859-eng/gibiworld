// GW-ARCH-001 section 4.2 — frame update order, steps 4-6:
//   4. deterministic perception and behaviour intent arbitration at 10 Hz
//   5. navigation/motion at FixedUpdate 50 Hz; NEVER couple locomotion distance to
//      render frame rate
//   6. Animator and Animation Rigging foot/gaze constraints
//
// The split between Update and FixedUpdate here is the whole point of GW-GAME-002:
// arbitration is allowed to run on render cadence because it only chooses INTENT;
// translation happens exclusively in FixedUpdate.
using Gibi.Core;
using UnityEngine;

namespace Gibi.Pets
{
    [DisallowMultipleComponent]
    public sealed class PetController : MonoBehaviour
    {
        [Header("Rig")]
        [SerializeField] private Transform petAssetRoot;
        [SerializeField] private CapsuleCollider bodyCapsule;
        [SerializeField] private AudioSource voice;

        [Header("Tuning")]
        [SerializeField] private float arriveRadiusM = 0.25f;
        [SerializeField] private float yawGainDegPerS = 180f;

        private BehaviorArbiter _arbiter;
        private DeterministicMotion _motion;
        private FixedStepAccumulator _accumulator;
        private IMonotonicClock _clock;

        private Vector3 _targetPosition;
        private bool _hasTarget;

        public string CurrentAction => _arbiter?.CurrentActionKey ?? "CALM_IDLE";
        public double DistanceTravelledM => _motion?.DistanceTravelledM ?? 0.0;
        public Gait CurrentGait => _motion?.CurrentGait ?? Gait.Idle;

        private void Awake()
        {
            _clock = GibiBootstrap.Services != null
                ? GibiBootstrap.Services.Resolve<IMonotonicClock>()
                : new MonotonicClock();

            _arbiter = new BehaviorArbiter(_clock);
            _motion = new DeterministicMotion();
            _accumulator = new FixedStepAccumulator();

            if (petAssetRoot == null) petAssetRoot = transform.Find("PetAssetRoot");
            if (bodyCapsule == null) bodyCapsule = GetComponentInChildren<CapsuleCollider>();
        }

        // ---------------- step 4: arbitration at 10 Hz ----------------
        private void Update()
        {
            _arbiter.Tick();

            // Needs scheduler and ambient layers only propose; they can never preempt a
            // locked safety, session, or player-cue action.
            if (!_hasTarget)
                _arbiter.Propose(BehaviorLayer.NeedsScheduler, "IDLE_LOOK_AROUND", 4000);
        }

        // ---------------- steps 5-6: 50 Hz motion, then rig ----------------
        private void FixedUpdate()
        {
            // Time.fixedDeltaTime is the ONLY time source that reaches motion, and the
            // accumulator converts it into a whole number of fixed steps. There is no
            // code path from Time.deltaTime to DeterministicMotion.Step.
            int steps = _accumulator.Consume(Time.fixedDeltaTime);

            for (int i = 0; i < steps; i++)
            {
                float yawCommand = 0f;

                if (_hasTarget)
                {
                    Vector3 flat = _targetPosition - transform.position;
                    flat.y = 0f;

                    if (flat.sqrMagnitude <= arriveRadiusM * arriveRadiusM)
                    {
                        OnArrived();
                    }
                    else
                    {
                        float desiredYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                        float error = Mathf.DeltaAngle((float)_motion.HeadingDeg, desiredYaw);
                        yawCommand = Mathf.Clamp(error * 4f, -yawGainDegPerS, yawGainDegPerS);
                    }
                }

                double before = _motion.DistanceTravelledM;
                _motion.Step(yawCommand);
                double advanced = _motion.DistanceTravelledM - before;

                if (advanced > 0.0)
                {
                    var heading = Quaternion.Euler(0f, (float)_motion.HeadingDeg, 0f);
                    transform.rotation = heading;
                    transform.position += heading * Vector3.forward * (float)advanced;
                }
            }
        }

        // ---------------- P0 player cues ----------------

        /// <summary>Direct player cue (priority 2). Interrupts AI and needs, not safety.</summary>
        public bool CueSit()
        {
            _hasTarget = false;
            _motion.SetGait(Gait.Idle);
            return _arbiter.Propose(BehaviorLayer.PlayerCue, "SIT", 4000, interruptible: false);
        }

        public bool CueCome(Vector3 playerPosition)
        {
            if (!_arbiter.Propose(BehaviorLayer.PlayerCue, "COME", 8000)) return false;
            _targetPosition = playerPosition;
            _hasTarget = true;
            _motion.SetGait(Gait.Trot);
            return true;
        }

        /// <summary>Fetch: run out, pick up, carry back, drop. Uses the section 6.3 clip set.</summary>
        public bool CueFetch(Vector3 toyPosition)
        {
            if (!_arbiter.Propose(BehaviorLayer.PlayerCue, "FETCH_OUTBOUND", 12000)) return false;
            _targetPosition = toyPosition;
            _hasTarget = true;
            _motion.SetGait(Gait.Run);
            return true;
        }

        /// <summary>
        /// Section 8.1 priority 0. Bypasses tick cadence entirely, so the interrupt
        /// lands well inside the one-tick bound GW-GAME-001 requires.
        /// </summary>
        public void SafetyStop(string reasonCode)
        {
            _hasTarget = false;
            _motion.SetGait(Gait.Idle);
            _arbiter.ForceSafety("STOP", 3000);
            Debug.Log($"[GibiWorld] Safety override: {reasonCode}");
        }

        private void OnArrived()
        {
            _hasTarget = false;
            _motion.SetGait(Gait.Idle);

            if (_arbiter.CurrentActionKey == "FETCH_OUTBOUND")
                _arbiter.Propose(BehaviorLayer.PlayerCue, "PICKUP", 2000, interruptible: false);
        }

        /// <summary>Called once the verified GLB has been instantiated under PetAssetRoot.</summary>
        public void AttachVerifiedAsset(GameObject instantiated)
        {
            if (petAssetRoot == null) return;
            instantiated.transform.SetParent(petAssetRoot, worldPositionStays: false);
            instantiated.transform.localPosition = Vector3.zero;
            instantiated.transform.localRotation = Quaternion.identity;

            // Section 6.3: model mesh colliders are forbidden. Strip any the asset
            // shipped with rather than trusting it not to have them.
            foreach (var mc in instantiated.GetComponentsInChildren<MeshCollider>(true))
                Destroy(mc);
        }
    }
}
