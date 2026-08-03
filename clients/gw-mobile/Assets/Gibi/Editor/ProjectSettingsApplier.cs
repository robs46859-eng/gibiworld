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
            // --- section 8.2.1: stable application identity ---
            // Never let Unity synthesize com.DefaultCompany.* from placeholder fields.
            // Besides being unsuitable for distribution, changing this implicitly later
            // breaks Android update identity and ARCore/Play release continuity.
            PlayerSettings.companyName = "GibiWorld";
            PlayerSettings.productName = "GibiWorld";
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android, "com.gibiworld.mobile");
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.iOS, "com.gibiworld.mobile");

            // --- section 3.1: IL2CPP ---
            // Unity 6 exposes a single ApiCompatibilityLevel.NET_Standard member;
            // the versioned NET_Standard_2_1 name was removed.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.iOS, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);

            // glTFast and JSON contract binding rely on reflection; aggressive stripping
            // removes types the asset runtime resolves by name at load time.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);

            // The shipping variant is ARM64. A separate compatibility build may opt
            // into ARMv7, but must not silently turn this release into a fat APK.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // --- section 7: URP requires linear ---
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            // NSDK setup docs (Android): "Uncheck Auto Graphics API. If Vulkan appears
            // in the Graphics API list, remove it." NSDK does not support the Vulkan path,
            // so OpenGLES3 is the only entry.
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);

            PlayerSettings.MTRendering = true;
            PlayerSettings.gpuSkinning = true;

            // --- platform minimums for AR ---
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;  // NSDK floor is 24; 29 kept for ARCore depth + section 17.1 device matrix
            PlayerSettings.iOS.targetOSVersionString = "14.0";   // NSDK minimum
            PlayerSettings.iOS.requiresFullScreen = true;

            // --- section 13.2: minimise sensor and location capture ---
            PlayerSettings.accelerometerFrequency = 0;
            PlayerSettings.iOS.locationUsageDescription =
                "GibiWorld uses your location only while the app is open, to find nearby play sites.";
            PlayerSettings.iOS.cameraUsageDescription =
                "GibiWorld uses the camera to place your pet in the room around you.";

            // Microphone stays unset: section 8.2 forbids sending raw voice, and §14
            // makes voice optional, so the permission is not requested at all in P0.

            // Active Input Handling. 0 = legacy only, 1 = Input System only, 2 = both.
            //
            // BOTH IS UNSUPPORTED ON ANDROID -- Unity blocks the build with a warning about
            // input correctness and performance. The project depends on
            // com.unity.inputsystem, NSDK prompts to enable it, and nothing in Gibi.*
            // touches the legacy Input class, so 1 is correct. An earlier revision of this
            // file left it at 2 because the value looked "already set"; it was set wrong.
            //
            // Not exposed on PlayerSettings, so it is written through SerializedObject.
            // Changing it requires an editor restart to take effect.
            var playerSettings = AssetDatabase.LoadAllAssetsAtPath(
                "ProjectSettings/ProjectSettings.asset");
            if (playerSettings != null && playerSettings.Length > 0)
            {
                var so = new SerializedObject(playerSettings[0]);
                var handler = so.FindProperty("activeInputHandler");
                if (handler != null && handler.intValue != 1)
                {
                    handler.intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[GibiWorld] Active Input Handling set to Input System only " +
                              "(Both is unsupported on Android). RESTART THE EDITOR.");
                }
            }

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Section 7 draw-call budget: dynamic batching helps, static batching bloats
            // the build for scenes that are almost entirely runtime-instantiated.
            PlayerSettings.bakeCollisionMeshes = false;

            // ADR-012: the local P0 uses standard ARCore. NSDK remains installed for
            // future authenticated services but its 4.1.0 loader is not device-safe on
            // the Pixel 9a due to its zNear=0 native projection loop.
            AndroidXrProviderConfigurator.ApplyArCoreP0();

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
