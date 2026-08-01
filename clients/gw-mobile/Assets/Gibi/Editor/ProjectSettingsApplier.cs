// GW-ARCH-001 sections 3.1, 7, 13.2, 16.
//
// Applied from code rather than hand-edited YAML: Unity rewrites ProjectSettings assets
// on open, so a hand-edited file drifts silently. Section 16 requires builds be
// reproducible from a recorded configuration, which means the configuration must be
// executable and diffable.
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gibi.Editor
{
    public static class ProjectSettingsApplier
    {
        [MenuItem("GibiWorld/Apply Required Project Settings")]
        public static void Apply()
        {
            // --- section 3.1: IL2CPP ---
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.iOS, ApiCompatibilityLevel.NET_Standard_2_1);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard_2_1);

            // glTFast and JSON contract binding rely on reflection; aggressive stripping
            // removes types the asset runtime resolves by name at load time.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // --- section 7: URP requires linear ---
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);

            PlayerSettings.MTRendering = true;
            PlayerSettings.gpuSkinning = true;

            // --- platform minimums for AR ---
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.requiresFullScreen = true;

            // --- section 13.2: minimise sensor and location capture ---
            PlayerSettings.accelerometerFrequency = 0;
            PlayerSettings.iOS.locationUsageDescription =
                "GibiWorld uses your location only while the app is open, to find nearby play sites.";
            PlayerSettings.iOS.cameraUsageDescription =
                "GibiWorld uses the camera to place your pet in the room around you.";

            // Microphone stays unset: section 8.2 forbids sending raw voice, and §14
            // makes voice optional, so the permission is not requested at all in P0.

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Section 7 draw-call budget: dynamic batching helps, static batching bloats
            // the build for scenes that are almost entirely runtime-instantiated.
            PlayerSettings.bakeCollisionMeshes = false;

            AssetDatabase.SaveAssets();
            Debug.Log("[GibiWorld] Required project settings applied. See ProjectSettings/GibiBuildSettings.md");
        }

        public static void ApplyForCI()
        {
            Apply();
            EditorApplication.Exit(0);
        }
    }
}
