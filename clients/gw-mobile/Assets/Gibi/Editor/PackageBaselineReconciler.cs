// Keeps the direct package requests aligned with the versions Unity 6 actually resolved.
//
// Do not hand-edit Packages/manifest.json for this repair. Unity's Package Manager Client
// owns the manifest and lockfile transaction so compatibility and transitive dependencies
// are resolved together.
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Gibi.Editor
{
    public static class PackageBaselineReconciler
    {
        private const double TimeoutSeconds = 600;

        private static readonly string[] BaselinePackages =
        {
            "com.unity.burst@1.8.29",
            "com.unity.test-framework@1.6.0",
            "com.unity.xr.core-utils@2.6.0",
            "com.unity.xr.management@4.5.4",
        };

        private static AddAndRemoveRequest request;
        private static double deadline;

        [MenuItem("GibiWorld/Reconcile Audited Package Baseline")]
        public static void Reconcile()
        {
            if (request != null && !request.IsCompleted)
            {
                Debug.LogWarning("[GibiWorld] Package baseline reconciliation is already running.");
                return;
            }

            Debug.Log("[GibiWorld] Reconciling audited packages: " +
                      string.Join(", ", BaselinePackages));
            request = Client.AddAndRemove(BaselinePackages, Array.Empty<string>());
            deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (request == null)
                return;

            if (!request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup <= deadline)
                    return;

                EditorApplication.update -= Poll;
                Debug.LogError("[GibiWorld] Package baseline reconciliation timed out.");
                request = null;
                return;
            }

            EditorApplication.update -= Poll;

            if (request.Status == StatusCode.Success)
            {
                var resolved = request.Result
                    .Where(package => BaselinePackages.Any(
                        baseline => baseline.StartsWith(package.name + "@", StringComparison.Ordinal)))
                    .Select(package => package.name + "@" + package.version)
                    .OrderBy(value => value);

                Debug.Log("[GibiWorld] Audited package baseline reconciled: " +
                          string.Join(", ", resolved));
            }
            else
            {
                Debug.LogError("[GibiWorld] Package baseline reconciliation failed: " +
                               request.Error?.message);
            }

            request = null;
        }
    }
}
