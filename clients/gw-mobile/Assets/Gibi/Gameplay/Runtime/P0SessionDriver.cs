// GW-ARCH-001 section 18 — P0 vertical slice.
//   "One preset dog + one Pawsome3D pet; local placement; sit/fetch; one local course;
//    offline fallback. Internal devices only."
//
// This is the thin layer that turns verified bytes into a dog standing on a real floor.
// Everything it touches was built and tested separately; this only sequences it.
using System.Threading;
using System.Threading.Tasks;
using Gibi.Core;
using Gibi.Pets;
using Gibi.Spatial;
using UnityEngine;

namespace Gibi.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class P0SessionDriver : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private PlacementController placement;
        [SerializeField] private Transform petSandboxRoot;
        [SerializeField] private Transform placedWorldRoot;
        [SerializeField] private SandboxBoundary sandboxBoundary;
        [SerializeField] private SandboxDemoDirector demoDirector;
        [SerializeField] private MonoBehaviour worldAnchorHostBehaviour;
        [SerializeField] private Shader petShader;
        [SerializeField] private bool autoSpawnForSandbox;

        [Header("Preset")]
        [SerializeField] private string presetAssetId = "asset_01J8ZQK5T7VN2MXR4WD6GHYAB3";
        [SerializeField] private PetAnimationProfile petAnimationProfile;

        private PetSpawner _spawner;
        private PetController _pet;
        private CancellationTokenSource _cts;
        private bool _ownsRuntimeProfile;
        private bool _isSpawning;
        private bool _isPlacing;
        private IWorldAnchorHost _worldAnchorHost;
        private Transform _worldRootStagingParent;

        public bool PetIsPlaced => _pet != null;
        public bool CanPlace => _pet == null && !_isSpawning && !_isPlacing;
        public Pose CandidatePose => placement != null ? placement.CandidatePose : default;
        public Pose LastHitPose  => placement != null ? placement.LastHitPose : default;
        public bool HasHit       => placement != null && placement.HasHit;
        public string LastMeasurements => placement != null ? placement.LastMeasurements : "";
        public string LastFailureCode { get; private set; }

        private void Awake()
        {
            // Scenes are generated from code (section 16 reproducibility), so there is no
            // inspector pass to assign these. Resolve them at runtime instead of relying
            // on serialised references that a regenerated scene would not carry.
            if (placement == null) placement = GetComponentInParent<PlacementController>()
                                             ?? FindAnyObjectByType<PlacementController>();
            if (petSandboxRoot == null) petSandboxRoot = transform;
            if (placedWorldRoot == null) placedWorldRoot = petSandboxRoot;
            _worldRootStagingParent = placedWorldRoot != null ? placedWorldRoot.parent : null;
            if (sandboxBoundary == null && placedWorldRoot != null)
                sandboxBoundary = placedWorldRoot.GetComponent<SandboxBoundary>();
            if (demoDirector == null && placedWorldRoot != null)
                demoDirector = placedWorldRoot.GetComponentInChildren<SandboxDemoDirector>(true);
            if (petShader == null) petShader = Shader.Find("Universal Render Pipeline/Lit");
            _worldAnchorHost = worldAnchorHostBehaviour as IWorldAnchorHost;
            if (_worldAnchorHost == null)
            {
                foreach (var candidate in GetComponentsInParent<MonoBehaviour>(true))
                {
                    if (candidate is not IWorldAnchorHost host) continue;
                    worldAnchorHostBehaviour = candidate;
                    _worldAnchorHost = host;
                    break;
                }
            }
            if (petAnimationProfile == null)
            {
                petAnimationProfile = PetAnimationProfile.CreateRandy11P0Runtime();
                _ownsRuntimeProfile = true;
            }
        }

        private async void Start()
        {
            _cts = new CancellationTokenSource();

            // Gameplay asks Pets for a pet; it never touches the verification pipeline
            // itself, because section 4 forbids Gameplay from referencing AssetRuntime.
            _spawner = new PetSpawner(petShader, petAnimationProfile);

            int keys = await _spawner.LoadTrustedKeysAsync(_cts.Token);
            Debug.Log($"[GibiWorld] Pinned {keys} trusted signing key(s).");

            if (keys == 0)
                Debug.LogError("[GibiWorld] No pinned keys — every asset will reject (section 6.4 step 1).");

            if (autoSpawnForSandbox && keys > 0)
            {
                if (placedWorldRoot != null) placedWorldRoot.gameObject.SetActive(true);
                Pose pose = placedWorldRoot != null
                    ? new Pose(placedWorldRoot.position, placedWorldRoot.rotation)
                    : new Pose(transform.position, transform.rotation);
                await SpawnVerifiedPet(pose);
            }
        }

        /// <summary>
        /// Evaluate placement WITHOUT committing. Drives the section 5.3 ring so the
        /// player can see whether a surface is acceptable before they commit to it.
        /// </summary>
        public PlacementStatus PreviewAt(Vector2 screenPoint, float playerSpeedMps)
            => placement != null
                ? placement.Evaluate(screenPoint, playerSpeedMps)
                : default;

        /// <summary>
        /// Called on tap. Places the pet only if the §5.3 gates pass; a rejected placement
        /// leaves the world untouched and the ring explains why.
        /// </summary>
        public async Task<bool> TryPlaceAt(Vector2 screenPoint, float playerSpeedMps)
        {
            if (placement == null || !CanPlace)
            {
                LastFailureCode = _isSpawning || _isPlacing
                    ? "PLACEMENT_IN_PROGRESS"
                    : "ALREADY_PLACED";
                return false;
            }

            var status = placement.Evaluate(screenPoint, playerSpeedMps);
            if (!status.CanPlace)
            {
                LastFailureCode = status.RejectionCode;
                return false;
            }

            if (_worldAnchorHost == null)
            {
                LastFailureCode = "ANCHOR_HOST_UNAVAILABLE";
                return false;
            }

            _isPlacing = true;
            try
            {
                WorldAnchorResult anchor = await _worldAnchorHost.TryCreateAnchorAsync(
                    placement.CandidatePose,
                    _cts != null ? _cts.Token : CancellationToken.None);
                if (!anchor.Success)
                {
                    LastFailureCode = anchor.FailureCode;
                    return false;
                }

                AttachWorldToAnchor(anchor.AnchorTransform);
                bool spawned = await SpawnVerifiedPet(placement.CandidatePose);
                if (!spawned)
                    RestoreUnplacedWorld();
                return spawned;
            }
            finally
            {
                _isPlacing = false;
            }
        }

        private void AttachWorldToAnchor(Transform anchorTransform)
        {
            if (placedWorldRoot == null || anchorTransform == null) return;
            placedWorldRoot.SetParent(anchorTransform, worldPositionStays: false);
            placedWorldRoot.localPosition = Vector3.zero;
            placedWorldRoot.localRotation = Quaternion.identity;
            placedWorldRoot.gameObject.SetActive(true);
        }

        private async Task<bool> SpawnVerifiedPet(Pose pose)
        {
            if (_spawner == null || _isSpawning) return false;
            _isSpawning = true;

            PetSpawnResult result;
            try
            {
                result = await _spawner.SpawnAsync(
                    presetAssetId, pose, petSandboxRoot, _cts.Token);
            }
            finally
            {
                _isSpawning = false;
            }

            if (!result.Success)
            {
                if (placedWorldRoot != null && !autoSpawnForSandbox)
                    placedWorldRoot.gameObject.SetActive(false);
                LastFailureCode = result.FailureCode;
                Debug.LogError($"[GibiWorld] Pet failed verification: {result.FailureCode}. " +
                               "Section 0: a pet SHALL render only if every gate passes.");
                return false;
            }

            _pet = result.Pet;
            _pet.ConfigureBoundary(sandboxBoundary);
            demoDirector?.BindPet(_pet);
            LastFailureCode = null;
            Debug.Log($"[GibiWorld] Pet placed and verified at {pose.position}.");
            return true;
        }

        // ---- P0 player cues ----
        public void CueSit()   => _pet?.CueSit();
        public void CueCome(Vector3 playerPos) => _pet?.CueCome(playerPos);
        public void CueFetch(Vector3 toyPos)   => _pet?.CueFetch(toyPos);

        public bool ResetPlacedWorld()
        {
            if (_isSpawning || _isPlacing) return false;
            demoDirector?.UnbindPet();
            if (_pet != null) Destroy(_pet.gameObject);
            _pet = null;
            LastFailureCode = null;
            if (!autoSpawnForSandbox)
                RestoreUnplacedWorld();
            return true;
        }

        private void RestoreUnplacedWorld()
        {
            if (placedWorldRoot != null)
            {
                placedWorldRoot.SetParent(_worldRootStagingParent, worldPositionStays: false);
                placedWorldRoot.localPosition = Vector3.zero;
                placedWorldRoot.localRotation = Quaternion.identity;
                placedWorldRoot.gameObject.SetActive(false);
            }
            _worldAnchorHost?.ResetAnchor();
        }

        public void ConfigureAnimationProfile(PetAnimationProfile profile)
        {
            petAnimationProfile = profile;
            _ownsRuntimeProfile = false;
        }

        /// <summary>Explicit composition seam for generated scenes and EditMode tests.</summary>
        public void ConfigurePlacement(PlacementController placementController)
            => placement = placementController;

        public void ConfigureWorld(Transform worldRoot, SandboxBoundary boundary,
                                   SandboxDemoDirector director, bool autoSpawn,
                                   MonoBehaviour anchorHostBehaviour = null)
        {
            placedWorldRoot = worldRoot;
            petSandboxRoot = worldRoot;
            sandboxBoundary = boundary;
            demoDirector = director;
            autoSpawnForSandbox = autoSpawn;
            worldAnchorHostBehaviour = anchorHostBehaviour;
            _worldAnchorHost = anchorHostBehaviour as IWorldAnchorHost;
            _worldRootStagingParent = worldRoot != null ? worldRoot.parent : null;
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            if (_ownsRuntimeProfile && petAnimationProfile != null)
                Destroy(petAnimationProfile);
        }



        
    }
}
