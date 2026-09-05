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
        private PetAnimationProfile _animationProfile;
        private Transform _mouthSocket;
        private FetchSequence _fetchSequence;
        private FetchToy _fetchToy;
        private SandboxBoundary _sandboxBoundary;
        private Vector3 _fetchReturnPosition;
        private bool _postFetchCelebration;

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
        public Transform MouthSocket => _mouthSocket;
        public FetchStage CurrentFetchStage => _fetchSequence?.Stage ?? FetchStage.Idle;
        public int CompletedFetches => _fetchSequence?.CompletedCount ?? 0;
        public bool HasNavigationTarget => _hasTarget;
        public Vector3 NavigationTarget => _targetPosition;
        public double NavigationHeadingDeg => _motion?.HeadingDeg ?? 0.0;

        private void Awake()
        {
            _clock = GibiBootstrap.Services != null
                ? GibiBootstrap.Services.Resolve<IMonotonicClock>()
                : new MonotonicClock();

            _arbiter = new BehaviorArbiter(_clock);
            _motion = new DeterministicMotion();
            _accumulator = new FixedStepAccumulator();
            _biometrics = new PetBiometrics();
            _fetchSequence = new FetchSequence();
            _animator = gameObject.GetComponent<PetAnimator>() ?? gameObject.AddComponent<PetAnimator>();

            if (petAssetRoot == null) petAssetRoot = transform.Find("PetAssetRoot");
            if (bodyCapsule == null) bodyCapsule = GetComponentInChildren<CapsuleCollider>();
        }

        // ---------------- step 4: arbitration at 10 Hz ----------------
        private void Update()
        {
            _arbiter.Tick();
            AdvanceFetchPresentation();

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
                bool advance = true;

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
                        // Rotate in place until the target is broadly ahead. Advancing
                        // through a 180-degree turn creates a large circle that can miss
                        // nearby toys and shelter thresholds entirely.
                        advance = Mathf.Abs(error) <= 25f;
                    }
                }

                double before = _motion.DistanceTravelledM;
                _motion.Step(yawCommand, advance);
                double advanced = _motion.DistanceTravelledM - before;

                if (advanced > 0.0)
                {
                    var heading = Quaternion.Euler(0f, (float)_motion.HeadingDeg, 0f);
                    transform.rotation = heading;
                    transform.position += heading * Vector3.forward * (float)advanced;
                    if (_sandboxBoundary != null)
                        transform.position = _sandboxBoundary.ClampWorld(
                            transform.position, bodyCapsule != null ? bodyCapsule.radius : 0.15f);
                }
            }
        }

        // ---------------- P0 player cues ----------------

        /// <summary>Direct player cue (priority 2). Interrupts AI and needs, not safety.</summary>
        public bool CueSit()
        {
            CancelFetch(dropToy: true);
            _hasTarget = false;
            _motion.SetGait(Gait.Idle);
            bool accepted = _arbiter.Propose(
                BehaviorLayer.PlayerCue, "SIT", 4000, interruptible: false);
            if (accepted) _animator?.PlayImmediate("sit", loop: true);
            return accepted;
        }

        public bool CueCome(Vector3 playerPosition)
        {
            CancelFetch(dropToy: true);
            if (!_arbiter.Propose(BehaviorLayer.PlayerCue, "COME", 8000)) return false;
            _targetPosition = playerPosition;
            _hasTarget = true;
            _motion.SetGait(Gait.Trot);
            _animator?.PlayImmediate("walk", loop: true);
            return true;
        }

        /// <summary>Fetch: run out, pick up, carry back, drop. Uses the section 6.3 clip set.</summary>
        public bool CueFetch(Vector3 toyPosition)
        {
            return CueFetchInternal(null, toyPosition, transform.position);
        }

        public bool CueFetch(FetchToy toy, Vector3 returnPosition)
        {
            if (toy == null) return false;
            return CueFetchInternal(toy, toy.transform.position, returnPosition);
        }

        private bool CueFetchInternal(FetchToy toy, Vector3 toyPosition,
                                      Vector3 returnPosition)
        {
            if (_fetchSequence.Stage != FetchStage.Idle) return false;
            if (_engagedAffordance != null) ExitAffordance();
            if (!_arbiter.Propose(BehaviorLayer.PlayerCue, "FETCH", 30000,
                                  interruptible: false)) return false;
            if (!_fetchSequence.Begin()) return false;

            _fetchToy = toy;
            _fetchReturnPosition = returnPosition;
            _postFetchCelebration = false;
            _targetPosition = toyPosition;
            _hasTarget = true;
            // Fatigue changes manner/speed (walk vs trot/run), never whether an otherwise legal repeat action is available
            Gait fetchGait = _biometrics != null && _biometrics.Fatigue > 0.7f ? Gait.Trot : Gait.Run;
            _motion.SetGait(fetchGait);
            _animator?.PlayImmediate(fetchGait == Gait.Trot ? "trot" : "run", loop: true);
            return true;
        }

        /// <summary>Direct player Pet cue (priority 2).</summary>
        public bool CuePet()
        {
            if (_arbiter.CurrentActionKey == "STOP") return false;
            CancelFetch(dropToy: true);
            _hasTarget = false;
            _motion.SetGait(Gait.Idle);
            bool accepted = _arbiter.Propose(BehaviorLayer.PlayerCue, "PET", 3000, interruptible: true);
            if (accepted)
            {
                _animator?.PlayImmediate("pet_react");
            }
            return accepted;
        }

        /// <summary>Player Pause cue: freezes interaction.</summary>
        public void CuePause()
        {
            _hasTarget = false;
            _motion.SetGait(Gait.Idle);
            _arbiter.Propose(BehaviorLayer.PlayerCue, "PAUSE", 10000, interruptible: true);
            _animator?.PlayImmediate("idle_a", loop: true);
        }

        public bool CueRest(RestAffordance shelter)
        {
            if (shelter == null || !shelter.IsAvailable) return false;
            CancelFetch(dropToy: true);
            if (_engagedAffordance != null) ExitAffordance();
            if (!_arbiter.Propose(BehaviorLayer.PlayerCue, "SEEK_REST", 20000)) return false;

            _seekingAffordance = shelter;
            _targetPosition = shelter.ApproachPointWorld;
            _hasTarget = true;
            _motion.SetGait(Gait.Walk);
            _animator?.PlayImmediate("walk", loop: true);
            return true;
        }

        /// <summary>
        /// Section 8.1 priority 0. Bypasses tick cadence entirely, so the interrupt
        /// lands well inside the one-tick bound GW-GAME-001 requires.
        /// </summary>
        public void SafetyStop(string reasonCode)
        {
            CancelFetch(dropToy: true);
            _hasTarget = false;
            _motion.SetGait(Gait.Idle);
            _arbiter.ForceSafety("STOP", 3000);
            Debug.Log($"[GibiWorld] Safety override: {reasonCode}");
        }

        private void OnArrived()
        {
            _hasTarget = false;
            _motion.SetGait(Gait.Idle);

            FetchTransition fetch = _fetchSequence.ReachedTarget();
            if (fetch == FetchTransition.StartPickup)
            {
                _animator?.PlayImmediate("pickup");
                if (_fetchToy != null && !_fetchToy.AttachTo(_mouthSocket))
                    Debug.LogWarning("[GibiWorld] Fetch toy could not attach to MouthSocket.");
                return;
            }
            if (fetch == FetchTransition.StartDrop)
            {
                if (_fetchToy != null)
                    _fetchToy.DropAt(transform.position + transform.forward * 0.30f);
                _animator?.PlayImmediate("drop");
                return;
            }

            if (_seekingAffordance != null)
            {
                EnterAffordance(_seekingAffordance);
                return;
            }

            _animator?.PlayImmediate("idle_a", loop: true);
        }

        private void AdvanceFetchPresentation()
        {
            if (_fetchSequence == null || _animator == null) return;

            if ((_fetchSequence.Stage == FetchStage.Pickup ||
                 _fetchSequence.Stage == FetchStage.Drop) &&
                _animator.IsIdleOrFinished())
            {
                FetchTransition transition = _fetchSequence.ActionFinished();
                if (transition == FetchTransition.StartReturn)
                {
                    _targetPosition = _fetchReturnPosition;
                    _hasTarget = true;
                    _motion.SetGait(Gait.Walk);
                    _animator.PlayImmediate("carry", loop: true);
                }
                else if (transition == FetchTransition.Completed)
                {
                    _fetchToy = null;
                    _arbiter.CompleteIfCurrent("FETCH");
                    _postFetchCelebration = true;
                    _animator.PlayImmediate("success");
                }
            }
            else if (_postFetchCelebration && _animator.IsIdleOrFinished())
            {
                _postFetchCelebration = false;
                _animator.PlayImmediate("idle_a", loop: true);
            }
        }

        private void CancelFetch(bool dropToy)
        {
            if (_fetchSequence == null || !_fetchSequence.Cancel()) return;
            if (dropToy && _fetchToy != null && _fetchToy.IsHeld)
                _fetchToy.DropAt(transform.position + transform.forward * 0.30f);
            _fetchToy = null;
            _postFetchCelebration = false;
            _arbiter?.CompleteIfCurrent("FETCH");
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

            // GW-ARCH-003 HOME-02: Real visible dwelling entry and rest.
            // Do not hide the dog renderer to simulate entry. The pet remains visible.
            SetConcealed(false);
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
            instantiated.transform.localPosition = _animationProfile != null
                ? _animationProfile.AssetLocalPosition
                : Vector3.zero;
            instantiated.transform.localRotation = _animationProfile != null
                ? _animationProfile.AssetLocalRotation
                : Quaternion.identity;
            instantiated.transform.localScale = Vector3.one;

            // Section 6.3: model mesh colliders are forbidden. Strip any the asset
            // shipped with rather than trusting it not to have them.
            foreach (var mc in instantiated.GetComponentsInChildren<MeshCollider>(true))
                Destroy(mc);

            // Renderer list is cached AFTER attachment, otherwise concealment would find
            // nothing and the pet would stay visible inside an opaque shell.
            _concealedRenderers = null;

            CreateMouthSocket(instantiated.transform);

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

        /// <summary>Called before attachment so presentation corrections are deterministic.</summary>
        public void ConfigureProfile(PetAnimationProfile profile)
        {
            _animationProfile = profile;
            _animator?.Configure(profile);
        }

        public void ConfigureBoundary(SandboxBoundary boundary)
            => _sandboxBoundary = boundary;

        private void CreateMouthSocket(Transform instantiatedRoot)
        {
            _mouthSocket = null;
            if (_animationProfile == null || instantiatedRoot == null) return;

            Transform mouthBone = FindDescendant(instantiatedRoot, _animationProfile.MouthBoneName);
            if (mouthBone == null)
            {
                Debug.LogWarning($"[GibiWorld] Mouth bone '{_animationProfile.MouthBoneName}' " +
                                 "was not found; toy attachment remains disabled.");
                return;
            }

            var existing = mouthBone.Find("MouthSocket");
            _mouthSocket = existing != null
                ? existing
                : new GameObject("MouthSocket").transform;
            _mouthSocket.SetParent(mouthBone, worldPositionStays: false);
            _mouthSocket.localPosition = _animationProfile.MouthSocketLocalPosition;
            _mouthSocket.localRotation = _animationProfile.MouthSocketLocalRotation;
            _mouthSocket.localScale = Vector3.one;
        }

        private static Transform FindDescendant(Transform root, string exactName)
        {
            if (root == null || string.IsNullOrEmpty(exactName)) return null;
            var descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
                if (descendants[i].name == exactName) return descendants[i];
            return null;
        }
    }
}
