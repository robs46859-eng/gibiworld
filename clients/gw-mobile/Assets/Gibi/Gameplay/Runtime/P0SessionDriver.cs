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
        [SerializeField] private Shader petShader;

        [Header("Preset")]
        [SerializeField] private string presetAssetId = "asset_01J8ZQK5T7VN2MXR4WD6GHYAB3";

        private PetSpawner _spawner;
        private PetController _pet;
        private CancellationTokenSource _cts;

        public bool PetIsPlaced => _pet != null;
        public string LastFailureCode { get; private set; }

        private void Awake()
        {
            // Scenes are generated from code (section 16 reproducibility), so there is no
            // inspector pass to assign these. Resolve them at runtime instead of relying
            // on serialised references that a regenerated scene would not carry.
            if (placement == null) placement = GetComponentInParent<PlacementController>()
                                             ?? FindAnyObjectByType<PlacementController>();
            if (petSandboxRoot == null) petSandboxRoot = transform;
            if (petShader == null) petShader = Shader.Find("Universal Render Pipeline/Lit");
        }

        private async void Start()
        {
            _cts = new CancellationTokenSource();

            // Gameplay asks Pets for a pet; it never touches the verification pipeline
            // itself, because section 4 forbids Gameplay from referencing AssetRuntime.
            _spawner = new PetSpawner(petShader);

            int keys = await _spawner.LoadTrustedKeysAsync(_cts.Token);
            Debug.Log($"[GibiWorld] Pinned {keys} trusted signing key(s).");

            if (keys == 0)
                Debug.LogError("[GibiWorld] No pinned keys — every asset will reject (section 6.4 step 1).");
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
            if (placement == null) return false;

            var status = placement.Evaluate(screenPoint, playerSpeedMps);
            if (!status.CanPlace)
            {
                LastFailureCode = status.RejectionCode;
                return false;
            }

            if (_pet != null)
            {
                // Already placed — move rather than spawn a second pet.
                _pet.transform.SetPositionAndRotation(
                    placement.CandidatePose.position, placement.CandidatePose.rotation);
                return true;
            }

            return await SpawnVerifiedPet(placement.CandidatePose);
        }

        private async Task<bool> SpawnVerifiedPet(Pose pose)
        {
            var result = await _spawner.SpawnAsync(presetAssetId, pose, petSandboxRoot, _cts.Token);

            if (!result.Success)
            {
                LastFailureCode = result.FailureCode;
                Debug.LogError($"[GibiWorld] Pet failed verification: {result.FailureCode}. " +
                               "Section 0: a pet SHALL render only if every gate passes.");
                return false;
            }

            _pet = result.Pet;
            Debug.Log($"[GibiWorld] Pet placed and verified at {pose.position}.");
            return true;
        }

        // ---- P0 player cues ----
        public void CueSit()   => _pet?.CueSit();
        public void CueCome(Vector3 playerPos) => _pet?.CueCome(playerPos);
        public void CueFetch(Vector3 toyPos)   => _pet?.CueFetch(toyPos);

        private void OnDestroy() { _cts?.Cancel(); _cts?.Dispose(); }



        
    }
}
