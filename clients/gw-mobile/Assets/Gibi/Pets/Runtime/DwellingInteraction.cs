// GW-ARCH-003 HOME-02, HOME-03 & W10 — DwellingInteraction.
// Manages real traversable dwelling entry, occupancy, rest and exit state machines.
// States: Available -> Reserved -> Entering -> Occupied -> Exiting -> Available.
// Releases occupancy on cancellation, destruction, or pet switch.
using System;
using Gibi.Core;
using UnityEngine;

namespace Gibi.Pets
{
    public enum DwellingOccupancyState
    {
        Available,
        Reserved,
        Entering,
        Occupied,
        Exiting,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(DwellingDefinition))]
    public sealed class DwellingInteraction : MonoBehaviour
    {
        private DwellingDefinition _definition;
        private ActionToken _reservingToken = ActionToken.None;

        public DwellingOccupancyState State { get; private set; } = DwellingOccupancyState.Available;
        public ActionToken ReservingToken => _reservingToken;
        public bool IsAvailable => State == DwellingOccupancyState.Available && isActiveAndEnabled;
        public bool IsOccupied => State == DwellingOccupancyState.Occupied;
        public DwellingDefinition Definition => _definition;

        private void Awake()
        {
            _definition = GetComponent<DwellingDefinition>();
            _definition.EnsureDefaultMarkers();
        }

        public bool TryReserve(ActionToken token)
        {
            if (State != DwellingOccupancyState.Available) return false;
            _reservingToken = token;
            State = DwellingOccupancyState.Reserved;
            return true;
        }

        public bool BeginEntry(ActionToken token)
        {
            if (State != DwellingOccupancyState.Reserved || !_reservingToken.Matches(token))
                return false;

            State = DwellingOccupancyState.Entering;
            return true;
        }

        public bool CommitRest(ActionToken token)
        {
            if (State != DwellingOccupancyState.Entering || !_reservingToken.Matches(token))
                return false;

            State = DwellingOccupancyState.Occupied;
            return true;
        }

        public bool BeginExit(ActionToken token)
        {
            if (State != DwellingOccupancyState.Occupied || !_reservingToken.Matches(token))
                return false;

            State = DwellingOccupancyState.Exiting;
            return true;
        }

        public bool CompleteExit(ActionToken token)
        {
            if (State != DwellingOccupancyState.Exiting || !_reservingToken.Matches(token))
                return false;

            State = DwellingOccupancyState.Available;
            _reservingToken = ActionToken.None;
            return true;
        }

        /// <summary>
        /// Idempotent cancellation and occupancy release.
        /// </summary>
        public void Release(ActionToken token = default)
        {
            if (token.IsValid && _reservingToken.IsValid && !_reservingToken.Matches(token))
                return; // Not owner

            State = DwellingOccupancyState.Available;
            _reservingToken = ActionToken.None;
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();
    }
}
