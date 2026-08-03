using UnityEngine;

namespace Gibi.Pets
{
    /// <summary>Local XZ bounds shared by the plain sandbox and its AR instance.</summary>
    [DisallowMultipleComponent]
    public sealed class SandboxBoundary : MonoBehaviour
    {
        [SerializeField] private Vector2 halfExtentsM = new(2f, 2f);

        public Vector2 HalfExtentsM => halfExtentsM;

        public void Configure(Vector2 halfExtents)
        {
            halfExtentsM = new Vector2(
                Mathf.Max(0.5f, halfExtents.x),
                Mathf.Max(0.5f, halfExtents.y));
        }

        public Vector3 ClampWorld(Vector3 worldPosition, float marginM = 0f)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            float x = Mathf.Max(0f, halfExtentsM.x - Mathf.Max(0f, marginM));
            float z = Mathf.Max(0f, halfExtentsM.y - Mathf.Max(0f, marginM));
            local.x = Mathf.Clamp(local.x, -x, x);
            local.z = Mathf.Clamp(local.z, -z, z);
            return transform.TransformPoint(local);
        }
    }
}
