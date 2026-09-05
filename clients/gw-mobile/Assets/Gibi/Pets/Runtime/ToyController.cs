// GW-ARCH-003 FETCH-04 & W08 — ToyController.
// Single authoritative transform owner for the fetch toy.
// States: Grounded, Flight, ReservedForPickup, HeldByPet, Settling, Recovering.
// Exactly one owner writes pose. Compare-and-transition with ActionToken prevents stale updates.
using System;
using Gibi.Core;
using UnityEngine;

namespace Gibi.Pets
{
    public enum ToyOwnership
    {
        Grounded,
        Flight,
        ReservedForPickup,
        HeldByPet,
        Settling,
        Recovering,
    }

    [DisallowMultipleComponent]
    public sealed class ToyController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float ballRadiusM = 0.0335f;
        [SerializeField] private MeshRenderer toyRenderer;
        [SerializeField] private Collider queryCollider;

        private Transform _worldParent;
        private Vector3 _safeGroundedPosition;
        private Quaternion _safeGroundedRotation;
        private ActionToken _ownerToken = ActionToken.None;

        public ToyOwnership Ownership { get; private set; } = ToyOwnership.Grounded;
        public ActionToken OwnerToken => _ownerToken;
        public float BallRadiusM => ballRadiusM;
        public bool IsHeld => Ownership == ToyOwnership.HeldByPet;
        public bool IsInPlay => Ownership != ToyOwnership.Grounded;

        private void Awake()
        {
            _worldParent = transform.parent;
            _safeGroundedPosition = transform.position;
            _safeGroundedRotation = transform.rotation;
            if (toyRenderer == null) toyRenderer = GetComponentInChildren<MeshRenderer>();
            if (queryCollider == null) queryCollider = GetComponentInChildren<Collider>();
        }

        public void Configure(float radius)
        {
            ballRadiusM = Mathf.Max(0.01f, radius);
        }

        public void SetSafeHome(Vector3 position, Quaternion rotation)
        {
            _safeGroundedPosition = position;
            _safeGroundedRotation = rotation;
        }

        /// <summary>
        /// Transitions to flight under an active throw.
        /// </summary>
        public bool StartFlight(ActionToken token, Vector3 startPos)
        {
            if (Ownership != ToyOwnership.Grounded && Ownership != ToyOwnership.Recovering)
                return false;

            _ownerToken = token;
            Ownership = ToyOwnership.Flight;
            transform.SetParent(_worldParent, worldPositionStays: true);
            transform.position = startPos;
            if (queryCollider != null) queryCollider.enabled = false;
            return true;
        }

        /// <summary>
        /// Updates pose along simulation during flight.
        /// </summary>
        public void UpdateFlightPose(Vector3 pos, Quaternion rot)
        {
            if (Ownership != ToyOwnership.Flight) return;
            transform.position = pos;
            transform.rotation = rot;
        }

        /// <summary>
        /// Ball impacts ground and transitions to Settling.
        /// </summary>
        public bool StartSettling(ActionToken token, Vector3 landingPoint)
        {
            if (Ownership != ToyOwnership.Flight || !_ownerToken.Matches(token))
                return false;

            Ownership = ToyOwnership.Settling;
            transform.position = landingPoint;
            return true;
        }

        /// <summary>
        /// Ball completes settle and rests on ground.
        /// </summary>
        public bool CompleteSettle(ActionToken token, Vector3 finalGroundedPos)
        {
            if (Ownership != ToyOwnership.Settling || !_ownerToken.Matches(token))
                return false;

            Ownership = ToyOwnership.Grounded;
            transform.position = finalGroundedPos;
            _safeGroundedPosition = finalGroundedPos;
            if (queryCollider != null) queryCollider.enabled = true;
            return true;
        }

        /// <summary>
        /// Pet reserves toy before pickup; toy remains grounded while pet approaches.
        /// </summary>
        public bool ReserveForPickup(ActionToken token)
        {
            if (Ownership != ToyOwnership.Grounded)
                return false;

            _ownerToken = token;
            Ownership = ToyOwnership.ReservedForPickup;
            return true;
        }

        /// <summary>
        /// Trusted pickup contact commit. Verifies distance <= 0.04m and token.
        /// Attaches toy to pet MouthSocket.
        /// </summary>
        public bool CommitPickup(ActionToken token, Transform mouthSocket, float maxDistanceM = 0.04f)
        {
            if (Ownership != ToyOwnership.ReservedForPickup || !_ownerToken.Matches(token))
                return false;
            if (mouthSocket == null) return false;

            float dist = Vector3.Distance(transform.position, mouthSocket.position);
            if (dist > maxDistanceM)
            {
                // Misaligned: reject snap
                return false;
            }

            transform.SetParent(mouthSocket, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            Ownership = ToyOwnership.HeldByPet;
            if (queryCollider != null) queryCollider.enabled = false;
            return true;
        }

        /// <summary>
        /// Releases toy at ground point.
        /// </summary>
        public bool CommitDrop(ActionToken token, Vector3 groundPoint)
        {
            if (Ownership != ToyOwnership.HeldByPet || !_ownerToken.Matches(token))
                return false;

            transform.SetParent(_worldParent, worldPositionStays: true);
            transform.position = groundPoint + Vector3.up * ballRadiusM;
            transform.rotation = Quaternion.identity;
            Ownership = ToyOwnership.Grounded;
            _ownerToken = ActionToken.None;
            _safeGroundedPosition = transform.position;
            if (queryCollider != null) queryCollider.enabled = true;
            return true;
        }

        /// <summary>
        /// Idempotent cancellation and safe recovery. Drops toy at fallback location or safe home.
        /// </summary>
        public void RecoverOrReset(Vector3? fallbackGroundPos = null)
        {
            transform.SetParent(_worldParent, worldPositionStays: true);
            Vector3 targetPos = fallbackGroundPos.HasValue
                ? fallbackGroundPos.Value + Vector3.up * ballRadiusM
                : _safeGroundedPosition;

            transform.position = targetPos;
            transform.rotation = _safeGroundedRotation;
            Ownership = ToyOwnership.Grounded;
            _ownerToken = ActionToken.None;
            if (queryCollider != null) queryCollider.enabled = true;
        }
    }
}
