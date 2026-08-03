using UnityEngine;

namespace Gibi.Pets
{
    public enum AffordanceKind { Rest, Play, Food, Water }

    public interface IAffordance
    {
        AffordanceKind Kind { get; }

        /// <summary>Where the pet stands to engage. For a shelter this is the sill.</summary>
        Vector3 ApproachPointWorld { get; }

        /// <summary>Facing on arrival. A pet backs into a kennel; it faces a bowl.</summary>
        Quaternion ApproachFacingWorld { get; }

        /// <summary>Where the pet is held while engaged. May equal the approach point.</summary>
        Vector3 EngagedAnchorWorld { get; }

        /// <summary>
        /// True when the shell hides the occupant, so the pet's renderers are disabled
        /// while engaged rather than clipped through geometry.
        /// </summary>
        bool ConcealsOccupant { get; }

        /// <summary>Catalog clip key played while engaged.</summary>
        string EngagedClipKey { get; }

        bool IsAvailable { get; }
    }

}
