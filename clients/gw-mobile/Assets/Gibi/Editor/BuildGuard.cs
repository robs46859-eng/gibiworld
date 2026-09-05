// GW-ARCH-003 W01 — BuildGuard.
// Validates build preconditions:
// 1. One provider, one session, one origin, one tracked-pose driver, one audio listener.
// 2. Production scenes SHALL NOT contain or enable SandboxDemoDirector (FETCH-01).
// 3. Android uses direct ARCore per ADR-012; NSDK remains inactive.
// 4. Package manifest matches pinned versions.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gibi.Editor
{
    public static class BuildGuard
    {
        public static readonly string ManifestPath = "Packages/manifest.json";
        public static readonly string PackagesLockPath = "Packages/packages-lock.json";

        public struct GuardResult
        {
            public bool Passed;
            public List<string> Errors;
        }

        [MenuItem("GibiWorld/Run Build Guard")]
        public static void RunGuardMenu()
        {
            var result = ExecuteGuard(isProductionBuild: true);
            if (result.Passed)
                Debug.Log("[GibiWorld] BuildGuard PASSED: all production preconditions satisfied.");
            else
                Debug.LogError("[GibiWorld] BuildGuard FAILED:\n" + string.Join("\n", result.Errors));
        }

        public static void RunGuardForCI()
        {
            var result = ExecuteGuard(isProductionBuild: true);
            if (!result.Passed)
            {
                Debug.LogError("[GibiWorld] BuildGuard CI failure:\n" + string.Join("\n", result.Errors));
                EditorApplication.Exit(1);
            }
            EditorApplication.Exit(0);
        }

        public static GuardResult ExecuteGuard(bool isProductionBuild)
        {
            var errors = new List<string>();

            // 1. Validate package pins
            ValidatePackagePins(errors);

            // 2. Validate scene contracts
            var sceneErrors = SceneValidator.ValidateAll();
            errors.AddRange(sceneErrors);

            // 3. Validate assembly graph
            var graphErrors = AssemblyGraphCheck.Check();
            errors.AddRange(graphErrors);

            return new GuardResult
            {
                Passed = errors.Count == 0,
                Errors = errors
            };
        }

        private static void ValidatePackagePins(List<string> errors)
        {
            if (!File.Exists(ManifestPath))
            {
                errors.Add($"BuildGuard: {ManifestPath} missing.");
                return;
            }

            string manifestText = File.ReadAllText(ManifestPath);
            if (!manifestText.Contains("\"com.unity.xr.arfoundation\": \"6.4.2\""))
                errors.Add("BuildGuard: com.unity.xr.arfoundation must be pinned to 6.4.2.");
            if (!manifestText.Contains("\"com.unity.xr.arcore\": \"6.4.2\""))
                errors.Add("BuildGuard: com.unity.xr.arcore must be pinned to 6.4.2.");
            if (!manifestText.Contains("\"com.unity.render-pipelines.universal\": \"17.0.4\""))
                errors.Add("BuildGuard: com.unity.render-pipelines.universal must be pinned to 17.0.4.");
            if (!manifestText.Contains("\"com.unity.cloud.gltfast\": \"6.16.1\""))
                errors.Add("BuildGuard: com.unity.cloud.gltfast must be pinned to 6.16.1.");
            if (!manifestText.Contains("\"com.unity.addressables\": \"1.22.3\""))
                errors.Add("BuildGuard: com.unity.addressables must be pinned to 1.22.3.");
        }
    }
}
