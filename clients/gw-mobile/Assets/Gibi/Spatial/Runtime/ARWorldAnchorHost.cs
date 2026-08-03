// GW-ARCH-001 section 4 — AR Foundation stays behind the Spatial adapter.
// Gameplay asks for a stable world transform without naming ARAnchor or provider APIs.
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Gibi.Spatial
{
    public readonly struct WorldAnchorResult
    {
        public readonly bool Success;
        public readonly string FailureCode;
        public readonly Transform AnchorTransform;

        private WorldAnchorResult(bool success, string failureCode, Transform anchorTransform)
        {
            Success = success;
            FailureCode = failureCode;
            AnchorTransform = anchorTransform;
        }

        public static WorldAnchorResult Succeeded(Transform anchorTransform)
            => new(true, null, anchorTransform);

        public static WorldAnchorResult Failed(string failureCode)
            => new(false, failureCode, null);
    }

    public interface IWorldAnchorHost
    {
        Task<WorldAnchorResult> TryCreateAnchorAsync(Pose pose, CancellationToken cancellationToken);
        void ResetAnchor();
    }

    /// <summary>
    /// Owns the one local AR anchor used by the P0 placed world. The dog, toy, and house
    /// are parented beneath that anchor as one composition, so tracking corrections move
    /// them together instead of allowing the three objects to drift independently.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ARAnchorManager))]
    public sealed class ARWorldAnchorHost : MonoBehaviour, IWorldAnchorHost
    {
        [SerializeField] private ARAnchorManager anchorManager;

        private ARAnchor _currentAnchor;

        public bool HasAnchor => _currentAnchor != null;
        public Transform CurrentAnchorTransform =>
            _currentAnchor != null ? _currentAnchor.transform : null;

        private void Awake()
        {
            if (anchorManager == null)
                anchorManager = GetComponent<ARAnchorManager>();
        }

        public void Configure(ARAnchorManager manager)
            => anchorManager = manager;

        public async Task<WorldAnchorResult> TryCreateAnchorAsync(
            Pose pose, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return WorldAnchorResult.Failed("ANCHOR_CANCELLED");

            if (_currentAnchor != null)
                return WorldAnchorResult.Failed("ANCHOR_ALREADY_EXISTS");

            if (anchorManager == null || !anchorManager.isActiveAndEnabled ||
                anchorManager.subsystem == null)
                return WorldAnchorResult.Failed("ANCHOR_PROVIDER_UNAVAILABLE");

            Result<ARAnchor> result;
            try
            {
                result = await anchorManager.TryAddAnchorAsync(pose);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning($"[GibiWorld] AR anchor provider rejected creation: {exception.Message}");
                return WorldAnchorResult.Failed("ANCHOR_PROVIDER_UNAVAILABLE");
            }

            if (!result.status.IsSuccess() || result.value == null)
            {
                Debug.LogWarning(
                    $"[GibiWorld] AR anchor creation failed: {result.status.statusCode} " +
                    $"(native {result.status.nativeStatusCode}).");
                return WorldAnchorResult.Failed("ANCHOR_CREATE_FAILED");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                TryRemove(result.value);
                return WorldAnchorResult.Failed("ANCHOR_CANCELLED");
            }

            _currentAnchor = result.value;
            return WorldAnchorResult.Succeeded(_currentAnchor.transform);
        }

        public void ResetAnchor()
        {
            if (_currentAnchor == null) return;
            TryRemove(_currentAnchor);
            _currentAnchor = null;
        }

        private void TryRemove(ARAnchor anchor)
        {
            bool removed = false;
            if (anchorManager != null && anchorManager.isActiveAndEnabled)
            {
                try
                {
                    removed = anchorManager.TryRemoveAnchor(anchor);
                }
                catch (InvalidOperationException)
                {
                    // The provider can stop during scene teardown. The trackable still
                    // must not retain the placed composition in editor or fallback runs.
                }
            }

            if (!removed && anchor != null)
                Destroy(anchor.gameObject);
        }

        private void OnDestroy()
            => ResetAnchor();
    }
}
