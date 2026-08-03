// Objects advertise what they are FOR. The pet reads affordances, not object types.
//
// This is the spine of the placeable-object slice. A doghouse is not special-cased
// anywhere in the pet's logic -- it is an object advertising REST with a threshold and an
// interior anchor. A bowl advertising FOOD, a bed advertising REST, a ball advertising
// PLAY all reuse the same seek-approach-engage machinery. Adding a prop becomes a data
// change rather than a controller change.
//
// THE THRESHOLD RULE (measured, not assumed):
//
//   luxurydoghouse ships a 0.294 x 0.446 m doorway. The reference dog measures 0.54 W x
//   0.74 H. The dog does not fit, and no scale factor fixes that without turning the
//   kennel into a 2.4 m shed -- the opening is decorative, sized for looks rather than
//   for an occupant.
//
//   So an affordance entry point is a THRESHOLD, not a traversable aperture. The pet
//   paths to the sill and is then concealed and held at an interior anchor. Because the
//   shell is opaque, concealment reads as entry and avoids the wall clipping a literal
//   walk-through would show. ConcealsOccupant makes that explicit per object, so a bed
//   or an open crate can set it false and keep the pet visible.
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

    /// <summary>
    /// A shelter the pet rests in. Markers are authored on the prefab rather than derived
    /// from bounds, because a generated mesh's doorway is not reliably at its centre --
    /// the measured aperture on luxurydoghouse sits 0.029 m off-axis.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RestAffordance : MonoBehaviour, IAffordance
    {
        [Header("Markers")]
        [Tooltip("Sill. The pet paths here, then is concealed.")]
        [SerializeField] private Transform threshold;

        [Tooltip("Interior rest position. The pet is held here while concealed.")]
        [SerializeField] private Transform interiorAnchor;

        [Header("Behaviour")]
        [SerializeField] private bool concealsOccupant = true;
        [SerializeField] private string engagedClipKey = "sleep";
        [SerializeField] private bool available = true;

        public AffordanceKind Kind => AffordanceKind.Rest;
        public bool ConcealsOccupant => concealsOccupant;
        public string EngagedClipKey => engagedClipKey;
        public bool IsAvailable => available && isActiveAndEnabled;

        public Vector3 ApproachPointWorld
            => threshold != null ? threshold.position : transform.position;

        public Quaternion ApproachFacingWorld
            => threshold != null ? threshold.rotation : transform.rotation;

        public Vector3 EngagedAnchorWorld
            => interiorAnchor != null ? interiorAnchor.position : ApproachPointWorld;

        private void OnEnable() => AffordanceRegistry.Register(this);
        private void OnDisable() => AffordanceRegistry.Unregister(this);

        /// <summary>
        /// Build markers from measured aperture data when a prefab was not hand-authored.
        /// Offsets are local to this transform, in metres.
        /// </summary>
        public void ConfigureFromAperture(float apertureCentreX, float shellDepth)
        {
            if (threshold == null)
            {
                var t = new GameObject("Threshold").transform;
                t.SetParent(transform, worldPositionStays: false);
                threshold = t;
            }
            // Sill sits just outside the front face, on the doorway's own centreline.
            threshold.localPosition = new Vector3(apertureCentreX, 0f, -shellDepth * 0.5f - 0.10f);
            threshold.localRotation = Quaternion.identity;

            if (interiorAnchor == null)
            {
                var a = new GameObject("InteriorAnchor").transform;
                a.SetParent(transform, worldPositionStays: false);
                interiorAnchor = a;
            }
            // Held a little back from centre so a peek through the door reads as occupied.
            interiorAnchor.localPosition = new Vector3(apertureCentreX, 0f, shellDepth * 0.12f);
            interiorAnchor.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
    }
}
