// GW-ARCH-003 FETCH-01, FETCH-02 & W06 — FetchAimView.
// Visualizes trajectory arc preview and landing target disc.
// The preview is generated using the exact same ThrowSolver as execution.
using Gibi.Gameplay;
using UnityEngine;

namespace Gibi.UI
{
    [DisallowMultipleComponent]
    public sealed class FetchAimView : MonoBehaviour
    {
        [SerializeField] private LineRenderer trajectoryLine;
        [SerializeField] private Transform landingMarker;
        [SerializeField] private MeshRenderer landingRenderer;
        [SerializeField] private Color validColor = new Color(0.2f, 0.8f, 0.3f, 0.8f);
        [SerializeField] private Color invalidColor = new Color(0.9f, 0.2f, 0.2f, 0.8f);

        private void Awake()
        {
            if (trajectoryLine == null) trajectoryLine = GetComponentInChildren<LineRenderer>();
            if (landingMarker == null && transform.childCount > 0)
                landingMarker = transform.GetChild(0);
            if (landingRenderer == null && landingMarker != null)
                landingRenderer = landingMarker.GetComponentInChildren<MeshRenderer>();
            Hide();
        }

        public void RenderPlan(ThrowPlan plan)
        {
            gameObject.SetActive(true);

            Color targetColor = plan.IsValid ? validColor : invalidColor;

            if (trajectoryLine != null)
            {
                trajectoryLine.enabled = true;
                trajectoryLine.startColor = targetColor;
                trajectoryLine.endColor = targetColor;

                Vector3[] points = plan.TrajectoryPoints;
                if (points != null && points.Length > 0)
                {
                    trajectoryLine.positionCount = points.Length;
                    trajectoryLine.SetPositions(points);
                }
                else
                {
                    trajectoryLine.positionCount = 0;
                }
            }

            if (landingMarker != null)
            {
                landingMarker.gameObject.SetActive(true);
                Vector3 markerPos = plan.IsValid ? plan.SettleEndPoint : plan.TargetPoint;
                landingMarker.position = markerPos;
                if (landingRenderer != null && landingRenderer.sharedMaterial != null)
                {
                    landingRenderer.sharedMaterial.color = targetColor;
                }
            }
        }

        public void Hide()
        {
            if (trajectoryLine != null) trajectoryLine.enabled = false;
            if (landingMarker != null) landingMarker.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }
    }
}
