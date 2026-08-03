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
        private PetAnimator _animator;
        private PetBiometrics _biometrics;

        private Vector3 _targetPosition;
        private bool _hasTarget;

        // Affordance engagement. Seeking and engaged are distinct: a pet walking toward
        // the kennel is still fully interruptible by a player cue; a pet inside it is too.
        private IAffordance _seekingAffordance;
        private IAffordance _engagedAffordance;
        private Renderer[] _concealedRenderers;
        private long _nextNeedsCheckMs;

        public string CurrentAction => _arbiter?.CurrentActionKey ?? "CALM_IDLE";
        public double DistanceTravelledM => _motion?.DistanceTravelledM ?? 0.0;
        public Gait CurrentGait => _motion?.CurrentGait ?? Gait.Idle;
        public BiometricState Biometrics => _biometrics?.State ?? default;
        public bool IsEngaged => _engagedAffordance != null;

        private void Awake()
        {
            _clock = GibiBootstrap.Services != null
                ? GibiBootstrap.Services.Resolve<IMonotonicClock>()
                : new MonotonicClock();

            _arbiter = new BehaviorArbiter(_clock);
            _motion = new DeterministicMotion();
            _accumulator = new FixedStepAccumulator();
            _biometrics = new PetBiometrics();
            _animator = gameObject.GetComponent<PetAnimator>() ?? gameObject.AddComponent<PetAnimator>();

            if (petAssetRoot == null) petAssetRoot = transform.Find("PetAssetRoot");
            if (bodyCapsule == null) bodyCapsule = GetComponentInChildren<CapsuleCollider>();
        }

        // ---------------- step 4: arbitration at 10 Hz ----------------
        private void Update()
        {
            _arbiter.Tick();

            long now = _clock.ElapsedMilliseconds;
            _biometrics.Tick(now, ExertionFromGait(), resting: _engagedAffordance != null);

            // Needs are evaluated at 1 Hz, not per frame. A pet that re-decides where to
            // go sixty times a second reads as twitchy, and nothing here changes fast
            // enough to justify it.
            if (now >= _nextNeedsCheckMs)
            {
                _nextNeedsCheckMs = now + 1000L;
                EvaluateNeeds();
            }

            // Needs scheduler and ambient layers only propose; they can never preempt a
            // locked safety, session, or player-cue action.
            if (!_hasTarget && _engagedAffordance == null)
                _arbiter.Propose(BehaviorLayer.NeedsScheduler, "IDLE_LOOK_AROUND", 4000);
        }

        private float ExertionFromGait() => _motion.CurrentGait switch
        {
            Gait.Run  => 1.0f,
            Gait.Trot => 0.6f,
            Gait.Walk => 0.3f,
            _         => 0.0f,
        };

        /// <summary>
        /// Drives propose; they never compel. Every branch here is a Propose the arbiter
        /// may refuse, so a player cue always outranks the pet's own wants.
        /// </summary>
        private void EvaluateNeeds()
        {
            if (_engagedAffordance != null)
            {
                // Rested enough, or the shelter went away with its anchor.
                if (!_engagedAffordance.IsAvailable || _biometrics.Fatigue <= 0.05f)
                    ExitAffordance();
                return;
            }

            if (_hasTarget || !_biometrics.SeeksRest) return;

            var shelter = AffordanceRegistry.FindNearest(AffordanceKind.Rest, transform.position);
            if (shelter == null) return;

            if (!_arbiter.Propose(BehaviorLayer.NeedsScheduler, "SEEK_REST", 20000)) return;

            _seekingAffordance = shelter;
            _targetPosition = shelter.ApproachPointWorld;
            _hasTarget = true;
            _motion.SetGait(Gait.Walk);   // a tired dog walks; it does not sprint to bed
            _animator?.PlayImmediate("walk", loop: true);
        }

        // ---------------- steps 5-6: 50 Hz motion, then rig ----------------
        private void FixedUpdate()
        {
            // Time.fixedDeltaTime is the ONLY time source that reaches motion, and the
            // accumulator converts it into a whole number of fixed steps. There is no
            // code path from Time.deltaTime to DeterministicMotion.Step.
            int steps = _accumulator.Consume(Time.fixedDeltaTime);

            // Held at an interior anchor. Motion is not merely skipped -- the accumulator
            // is still drained above, so re-entry does not replay a burst of banked steps.
            if (_engagedAffordance != null) return;

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

            if (_seekingAffordance != null)
            {
                EnterAffordance(_seekingAffordance);
                return;
            }

            if (_arbiter.CurrentActionKey == "FETCH_OUTBOUND")
                _arbiter.Propose(BehaviorLayer.PlayerCue, "PICKUP", 2000, interruptible: false);
            else
                _animator?.PlayImmediate("idle_a", loop: true);
        }

        // ---------------- affordance engagement ----------------

        /// <summary>
        /// The pet has reached the threshold. It is NOT walked through the doorway: the
        /// measured aperture on luxurydoghouse is 0.294 x 0.446 m against a dog measuring
        /// 0.54 x 0.74, so a literal walk-through would clip straight through the walls.
        /// Concealing instead reads as entry and costs nothing.
        /// </summary>
        private void EnterAffordance(IAffordance a)
        {
            _seekingAffordance = null;
            _engagedAffordance = a;

            transform.SetPositionAndRotation(a.EngagedAnchorWorld, a.ApproachFacingWorld);
            _animator?.PlayImmediate(a.EngagedClipKey, loop: true);

            if (a.ConcealsOccupant) SetConcealed(true);
        }

        /// <summary>Leaves at the threshold, facing out, never inside the geometry.</summary>
        public void ExitAffordance()
        {
            if (_engagedAffordance == null) return;

            var a = _engagedAffordance;
            _engagedAffordance = null;

            SetConcealed(false);
            transform.SetPositionAndRotation(a.ApproachPointWorld, a.ApproachFacingWorld);

            _biometrics.RestedFully();
            _motion.SetGait(Gait.Idle);
            _animator?.PlayImmediate("rise");
        }

        private void SetConcealed(bool concealed)
        {
            if (concealed)
            {
                if (_concealedRenderers == null && petAssetRoot != null)
                    _concealedRenderers = petAssetRoot.GetComponentsInChildren<Renderer>(true);
            }
            if (_concealedRenderers == null) return;

            for (int i = 0; i < _concealedRenderers.Length; i++)
                if (_concealedRenderers[i] != null)
                    _concealedRenderers[i].enabled = !concealed;
        }

        /// <summary>Called once the verified GLB has been instantiated under PetAssetRoot.</summary>
        public void AttachVerifiedAsset(GameObject instantiated)
        {
            if (petAssetRoot == null || instantiated == null) return;
            instantiated.transform.SetParent(petAssetRoot, worldPositionStays: false);
            instantiated.transform.localPosition = Vector3.zero;
            instantiated.transform.localRotation = Quaternion.identity;

            // Section 6.3: model mesh colliders are forbidden. Strip any the asset
            // shipped with rather than trusting it not to have them.
            foreach (var mc in instantiated.GetComponentsInChildren<MeshCollider>(true))
                Destroy(mc);

            // Renderer list is cached AFTER attachment, otherwise concealment would find
            // nothing and the pet would stay visible inside an opaque shell.
            _concealedRenderers = null;

            if (_animator != null && _animator.Bind(instantiated))
            {
                _animator.PlayImmediate("idle_a", loop: true);
                Debug.Log($"[GibiWorld] {_animator.DescribeCoverage()}");
            }
            else
            {
                Debug.LogWarning("[GibiWorld] Asset carries no animation clips; pet will be static.");
            }
        }
    }
}
