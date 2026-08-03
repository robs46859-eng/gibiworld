// P0 Android provider policy.
//
// NSDK 4.1.0's SubsystemsDataAcquirer.TryGetCameraTimestampMs constructs an
// XRCameraParams with zNear = 0. On Pixel 9a / Android 17 its native view manager rejects
// that every frame ("near must be greater than zero") and leaves OpenGL with
// GL_INVALID_OPERATION. GibiWorld's P0 uses only AR Foundation planes, raycasts, and
// local anchors; no runtime code consumes an NSDK-only service. ADR-012 therefore keeps
// NSDK pinned for future authenticated features but selects the supported ARCore loader
// for the local P0 runtime.
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;

namespace Gibi.Editor
{
    public static class AndroidXrProviderConfigurator
    {
        private const string ArCoreLoaderPath = "Assets/XR/Loaders/ARCoreLoader.asset";
        private const string NsdkDefine = "NIANTICSPATIAL_NSDK_AR_LOADER_ENABLED";

        [MenuItem("GibiWorld/Use ARCore Provider for P0 Android")]
        public static void ApplyArCoreP0()
        {
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(
                BuildTargetGroup.Android);
            var manager = general != null ? general.Manager : null;
            var arCore = AssetDatabase.LoadAssetAtPath<XRLoader>(ArCoreLoaderPath);

            if (manager == null)
                throw new BuildFailedException("Android XR manager settings are missing.");
            if (arCore == null)
                throw new BuildFailedException($"ARCore loader asset is missing at {ArCoreLoaderPath}.");

            if (!manager.TrySetLoaders(new System.Collections.Generic.List<XRLoader> { arCore }))
                throw new BuildFailedException("Could not assign ARCore as the sole Android XR loader.");

            RemoveAndroidDefine(NsdkDefine);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(general);
            AssetDatabase.SaveAssets();

            Debug.Log("[GibiWorld] Android P0 XR provider = ARCoreLoader (ADR-012). " +
                      "NSDK loader and define are disabled for this local runtime.");
        }

        public static void ApplyArCoreP0ForCI()
        {
            ApplyArCoreP0();
            EditorApplication.Exit(0);
        }

        private static void RemoveAndroidDefine(string symbol)
        {
            string current = PlayerSettings.GetScriptingDefineSymbols(
                NamedBuildTarget.Android);
            string updated = string.Join(";", current
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(value => !string.Equals(value.Trim(), symbol,
                    StringComparison.Ordinal)));
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, updated);
        }
    }
}
