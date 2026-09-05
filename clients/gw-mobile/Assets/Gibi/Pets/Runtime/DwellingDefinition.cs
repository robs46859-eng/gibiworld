// GW-ARCH-003 HOME-01, HOME-02 & W10 — DwellingDefinition.
// Authors traversable dwelling envelope, collision geometry and spatial markers.
// Enforces pet fit: doorway >= 0.70m W x 0.90m H; interior >= 1.30m W x 1.50m D x 1.00m H.
using Gibi.Core;
using UnityEngine;

namespace Gibi.Pets
{
    [DisallowMultipleComponent]
    public sealed class DwellingDefinition : MonoBehaviour
    {
        [Header("Doorway Envelope (m)")]
        [SerializeField] private float doorWidthM = 0.70f;
        [SerializeField] private float doorHeightM = 0.90f;

        [Header("Interior Envelope (m)")]
        [SerializeField] private float interiorWidthM = 1.30f;
        [SerializeField] private float interiorDepthM = 1.50f;
        [SerializeField] private float interiorHeightM = 1.00f;

        [Header("Markers")]
        [SerializeField] private Transform exteriorApproach;
        [SerializeField] private Transform doorThreshold;
        [SerializeField] private Transform interiorTurn;
        [SerializeField] private Transform interiorRest;
        [SerializeField] private Transform exitClear;

        public float DoorWidthM => doorWidthM;
        public float DoorHeightM => doorHeightM;
        public float InteriorWidthM => interiorWidthM;
        public float InteriorDepthM => interiorDepthM;
        public float InteriorHeightM => interiorHeightM;

        public Transform ExteriorApproach => exteriorApproach;
        public Transform DoorThreshold => doorThreshold;
        public Transform InteriorTurn => interiorTurn;
        public Transform InteriorRest => interiorRest;
        public Transform ExitClear => exitClear;

        private void Awake()
        {
            EnsureDefaultMarkers();
        }

        public void EnsureDefaultMarkers()
        {
            if (exteriorApproach == null)
            {
                var go = new GameObject("ExteriorApproach");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.90f);
                go.transform.localRotation = Quaternion.identity;
                exteriorApproach = go.transform;
            }

            if (doorThreshold == null)
            {
                var go = new GameObject("DoorThreshold");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.45f);
                go.transform.localRotation = Quaternion.identity;
                doorThreshold = go.transform;
            }

            if (interiorTurn == null)
            {
                var go = new GameObject("InteriorTurn");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, 0.20f);
                go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                interiorTurn = go.transform;
            }

            if (interiorRest == null)
            {
                var go = new GameObject("InteriorRest");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, 0.40f);
                go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                interiorRest = go.transform;
            }

            if (exitClear == null)
            {
                var go = new GameObject("ExitClear");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -1.10f);
                go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                exitClear = go.transform;
            }
        }

        /// <summary>
        /// HOME-01: Validates that the pet envelope can physically fit through doorway and into interior.
        /// </summary>
        public bool CanFitPet(AgentEnvelope petEnvelope, float marginM = 0.05f)
        {
            float petWidth = petEnvelope.RadiusM * 2f;
            float petHeight = petEnvelope.HeightM;

            if (doorWidthM < petWidth + marginM * 2f) return false;
            if (doorHeightM < petHeight + marginM) return false;
            if (interiorWidthM < petWidth + marginM * 2f) return false;
            if (interiorHeightM < petHeight + marginM) return false;

            return true;
        }
    }
}
