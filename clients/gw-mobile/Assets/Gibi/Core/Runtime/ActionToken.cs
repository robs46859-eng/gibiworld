// GW-ARCH-003 PET-02 — ActionToken.
// Distinguishes action identity by session generation and sequence number so that
// stale completion callbacks cannot clear a newer action with the same name.
using System;

namespace Gibi.Core
{
    public readonly struct ActionToken : IEquatable<ActionToken>
    {
        public readonly int SessionGeneration;
        public readonly int ActionSequence;
        public readonly string PetId;

        public static readonly ActionToken None = new ActionToken(0, 0, string.Empty);

        public ActionToken(int sessionGeneration, int actionSequence, string petId)
        {
            SessionGeneration = sessionGeneration;
            ActionSequence = actionSequence;
            PetId = petId ?? string.Empty;
        }

        public bool IsValid => SessionGeneration > 0 && ActionSequence > 0 && !string.IsNullOrEmpty(PetId);

        public bool Matches(ActionToken other)
            => SessionGeneration == other.SessionGeneration &&
               ActionSequence == other.ActionSequence &&
               string.Equals(PetId, other.PetId, StringComparison.Ordinal);

        public bool Equals(ActionToken other) => Matches(other);

        public override bool Equals(object obj) => obj is ActionToken other && Matches(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SessionGeneration;
                hash = (hash * 397) ^ ActionSequence;
                hash = (hash * 397) ^ (PetId != null ? StringComparer.Ordinal.GetHashCode(PetId) : 0);
                return hash;
            }
        }

        public static bool operator ==(ActionToken left, ActionToken right) => left.Matches(right);
        public static bool operator !=(ActionToken left, ActionToken right) => !left.Matches(right);

        public override string ToString()
            => $"ActionToken(Gen={SessionGeneration}, Seq={ActionSequence}, Pet={PetId})";
    }
}
