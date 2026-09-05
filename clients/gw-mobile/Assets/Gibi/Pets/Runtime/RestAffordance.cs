// Objects advertise what they are FOR. The pet reads affordances, not object types.
// A doghouse is a REST affordance with explicit threshold/interior markers; no controller
// branch depends on the prop's concrete type.
using UnityEngine;

namespace Gibi.Pets
{
    /// <summary>
    /// Shelter markers are authored from measured aperture data. The generated opening is
    /// decorative (0.294 x 0.446 m) and cannot admit the 0.54 x 0.74 m reference dog.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RestAffordance : MonoBehaviour, IAffordance
    {
        [Header("Markers")]
        [Tooltip("Sill. The pet paths here before engaging the shelter.")]
        [SerializeField] private Transform threshold;

        [Tooltip("Rest pose. May sit at the visible sill for the P0 demo.")]
        [SerializeField] private Transform interiorAnchor;

        [Header("Behaviour")]
        [SerializeField] private bool concealsOccupant = false;
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

        public void ConfigureFromAperture(float apertureCentreX, float shellDepth)
        {
            ConfigureMarkers(apertureCentreX, shellDepth,
                             conceal: true, restAtThreshold: false);
        }

        /// <summary>
        /// The P0 dog lies across the sill with its body visibly sheltered instead of
        /// disappearing or clipping through the narrow decorative wall opening.
        /// </summary>
        public void ConfigureVisibleThresholdRest(float apertureCentreX, float shellDepth)
        {
            ConfigureMarkers(apertureCentreX, shellDepth,
                             conceal: false, restAtThreshold: true);
        }

        private void ConfigureMarkers(float apertureCentreX, float shellDepth,
                                      bool conceal, bool restAtThreshold)
        {
            concealsOccupant = conceal;
            if (threshold == null)
            {
                threshold = new GameObject("Threshold").transform;
                threshold.SetParent(transform, worldPositionStays: false);
            }
            threshold.localPosition = new Vector3(
                apertureCentreX, 0f, -shellDepth * 0.5f - 0.10f);
            threshold.localRotation = Quaternion.identity;

            if (interiorAnchor == null)
            {
                interiorAnchor = new GameObject("InteriorAnchor").transform;
                interiorAnchor.SetParent(transform, worldPositionStays: false);
            }
            interiorAnchor.localPosition = restAtThreshold
                ? threshold.localPosition + new Vector3(0f, 0f, 0.12f)
                : new Vector3(apertureCentreX, 0f, shellDepth * 0.12f);
            interiorAnchor.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
    }
}
