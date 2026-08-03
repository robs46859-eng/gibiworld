using UnityEngine;

namespace Gibi.Pets
{
    /// <summary>
    /// Deterministic transform ownership for a fetch toy. No Rigidbody is used: the dog
    /// controller, not frame-rate-sensitive physics, owns pickup, carry, drop, and reset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FetchToy : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float groundCentreHeightM = 0.0335f;

        private Transform _homeParent;
        private Vector3 _homeLocalPosition;
        private Quaternion _homeLocalRotation;
        private bool _homeCaptured;

        public bool IsHeld { get; private set; }

        private void Awake() => CaptureHome();

        public void Configure(float groundCentreHeight)
            => groundCentreHeightM = Mathf.Max(0f, groundCentreHeight);

        public void CaptureHome()
        {
            if (_homeCaptured) return;
            _homeParent = transform.parent;
            _homeLocalPosition = transform.localPosition;
            _homeLocalRotation = transform.localRotation;
            _homeCaptured = true;
        }

        public bool AttachTo(Transform mouthSocket)
        {
            if (mouthSocket == null) return false;
            CaptureHome();
            transform.SetParent(mouthSocket, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            IsHeld = true;
            return true;
        }

        public void DropAt(Vector3 groundPoint)
        {
            CaptureHome();
            transform.SetParent(_homeParent, worldPositionStays: true);
            transform.position = groundPoint + Vector3.up * groundCentreHeightM;
            transform.rotation = Quaternion.identity;
            IsHeld = false;
        }

        public void ResetToHome()
        {
            CaptureHome();
            transform.SetParent(_homeParent, worldPositionStays: false);
            transform.localPosition = _homeLocalPosition;
            transform.localRotation = _homeLocalRotation;
            IsHeld = false;
        }
    }
}
