// GW-ARCH-001 section 17 — GW-AR-001: "Exactly one ARSession and XR Origin are active
// in ARWorld." Acceptance test: automated scene validation.
//
// Runs three ways so it cannot be skipped:
//   * menu item, for a developer working locally
//   * batch mode via ValidateAllForCI, for the section 16 CI gate
//   * on every scene save, so a violation is caught the moment it is introduced
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace Gibi.Editor
{
    public static class SceneValidator
    {
        public const string BootstrapScene  = "Assets/Scenes/Bootstrap.unity";
        public const string ARWorldScene    = "Assets/Scenes/ARWorld.unity";
        public const string PetSandboxScene = "Assets/Scenes/PetSandbox.unity";

        [MenuItem("GibiWorld/Validate Scenes  %#v")]
        public static void ValidateMenu()
        {
            var errors = ValidateAll();
            if (errors.Count == 0)
                Debug.Log("[GibiWorld] Scene validation passed (GW-AR-001).");
            else
                Debug.LogError("[GibiWorld] Scene validation FAILED:\n" + string.Join("\n", errors));
        }

        /// <summary>Batch-mode entry point. Exits non-zero so CI fails the build.</summary>
        public static void ValidateAllForCI()
        {
            var errors = ValidateAll();
            if (errors.Count > 0)
            {
                Debug.LogError("[GibiWorld] GW-AR-001 FAILED:\n" + string.Join("\n", errors));
                EditorApplication.Exit(1);
            }
            Debug.Log("[GibiWorld] GW-AR-001 passed.");
            EditorApplication.Exit(0);
        }

        public static List<string> ValidateAll()
        {
            var errors = new List<string>();
            errors.AddRange(ValidateARWorld());
            errors.AddRange(ValidateBootstrap());
            return errors;
        }

        private static List<string> ValidateARWorld()
        {
            var errors = new List<string>();
            if (!System.IO.File.Exists(ARWorldScene))
            {
                errors.Add($"GW-AR-001: {ARWorldScene} does not exist.");
                return errors;
            }

            var scene = EditorSceneManager.OpenScene(ARWorldScene, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();

            int sessions = roots.Sum(r => r.GetComponentsInChildren<ARSession>(true).Length);
            int origins  = roots.Sum(r => r.GetComponentsInChildren<XROrigin>(true).Length);

            // "EXACTLY one" is normative — zero is as much a failure as two.
            if (sessions != 1)
                errors.Add($"GW-AR-001: ARWorld contains {sessions} ARSession components; exactly 1 required.");
            if (origins != 1)
                errors.Add($"GW-AR-001: ARWorld contains {origins} XROrigin components; exactly 1 required.");

            // Section 4.1 lists the managers the ARWorld scene SHALL contain.
            RequireManager<ARCameraManager>(roots, errors);
            RequireManager<AROcclusionManager>(roots, errors);
            RequireManager<ARPlaneManager>(roots, errors);
            RequireManager<ARRaycastManager>(roots, errors);
            RequireManager<ARAnchorManager>(roots, errors);
            RequireManager<ARMeshManager>(roots, errors);

            // A second audio listener silently halves spatial audio quality (section 7).
            int listeners = roots.Sum(r => r.GetComponentsInChildren<AudioListener>(true).Length);
            if (listeners > 1)
                errors.Add($"ARWorld contains {listeners} AudioListeners; at most 1 permitted.");

            return errors;
        }

        private static void RequireManager<T>(GameObject[] roots, List<string> errors)
            where T : Component
        {
            int n = roots.Sum(r => r.GetComponentsInChildren<T>(true).Length);
            if (n == 0)
                errors.Add($"Section 4.1: ARWorld is missing {typeof(T).Name}.");
            else if (n > 1)
                errors.Add($"Section 4.1: ARWorld has {n} {typeof(T).Name}; exactly 1 expected.");
        }

        private static List<string> ValidateBootstrap()
        {
            var errors = new List<string>();
            if (!System.IO.File.Exists(BootstrapScene))
            {
                errors.Add($"Section 4.1: {BootstrapScene} does not exist.");
                return errors;
            }

            var scene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();

            // Section 4.1: Bootstrap holds lifecycle and persistent UI ONLY. AR trackables
            // in Bootstrap would start a session before the safety gates are constructed.
            int sessions = roots.Sum(r => r.GetComponentsInChildren<ARSession>(true).Length);
            if (sessions != 0)
                errors.Add("Section 4.1: Bootstrap must not contain an ARSession.");

            int origins = roots.Sum(r => r.GetComponentsInChildren<XROrigin>(true).Length);
            if (origins != 0)
                errors.Add("Section 4.1: Bootstrap must not contain an XROrigin.");

            return errors;
        }

        /// <summary>Catches a violation at the moment it is authored, not at CI time.</summary>
        private class SaveGuard : UnityEditor.AssetModificationProcessor
        {
            private static string[] OnWillSaveAssets(string[] paths)
            {
                foreach (var p in paths)
                {
                    if (p != ARWorldScene) continue;
                    var scene = SceneManager.GetActiveScene();
                    if (scene.path != ARWorldScene) continue;

                    var roots = scene.GetRootGameObjects();
                    int s = roots.Sum(r => r.GetComponentsInChildren<ARSession>(true).Length);
                    int o = roots.Sum(r => r.GetComponentsInChildren<XROrigin>(true).Length);
                    if (s != 1 || o != 1)
                    {
                        Debug.LogError($"[GibiWorld] GW-AR-001 violated on save: " +
                                       $"{s} ARSession, {o} XROrigin. Exactly 1 of each required.");
                    }
                }
                return paths;
            }
        }
    }
}
