// GW-ARCH-001 section 4.1 — Scene contract.
//
// Scenes are CONSTRUCTED FROM CODE rather than hand-authored, because section 16
// requires client builds be reproducible from a signed commit plus a recorded config.
// A .unity file edited by hand drifts silently and merges badly; a builder is
// reviewable in a diff and regenerates byte-comparable output.
//
// Run from the menu, or in batch mode:
//   Unity -batchmode -quit -projectPath . -executeMethod Gibi.Editor.SceneBuilder.BuildAllForCI
using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Gibi.Editor
{
    public static class SceneBuilder
    {
        private const string SceneDir = "Assets/Scenes";

        [MenuItem("GibiWorld/Build P0 Scenes")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(SceneDir);
            BuildBootstrap();
            BuildARWorld();
            BuildPetSandbox();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GibiWorld] P0 scenes rebuilt. Run GibiWorld/Validate Scenes.");
        }

        public static void BuildAllForCI()
        {
            BuildAll();
            var errors = SceneValidator.ValidateAll();
            EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------------
        // Bootstrap: dependency container, app lifecycle, authentication,
        // remote config, crash handler, and persistent UI root ONLY.
        // No AR trackables — the session must not start before safety gates exist.
        // ------------------------------------------------------------------
        private static void BuildBootstrap()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("GibiBootstrap");
            root.AddComponent<Gibi.Core.GibiBootstrap>();
            root.AddComponent<Gibi.Core.AppLifecycle>();

            var ui = new GameObject("PersistentUIRoot");
            ui.transform.SetParent(root.transform);

            var canvas = new GameObject("SafetyCanvas");
            canvas.transform.SetParent(ui.transform);

            // Section 7: "Critical safety messages SHALL be non-spatial UI audio with
            // captions and haptics." A single listener lives on the persistent root so
            // ARWorld never introduces a second one.
            var audio = new GameObject("NonSpatialAudio");
            audio.transform.SetParent(root.transform);
            audio.AddComponent<AudioListener>();

            EditorSceneManager.SaveScene(scene, SceneValidator.BootstrapScene);
        }

        // ------------------------------------------------------------------
        // ARWorld: EXACTLY one ARSession and EXACTLY one XR Origin (GW-AR-001),
        // plus the section 4.1 manager set.
        // ------------------------------------------------------------------
        private static void BuildARWorld()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- exactly one ARSession ---
            var sessionGo = new GameObject("AR Session");
            sessionGo.AddComponent<ARSession>();
            sessionGo.AddComponent<ARInputManager>();

            // --- exactly one XR Origin (Mobile AR) ---
            var originGo = new GameObject("XR Origin (Mobile AR)");
            var origin = originGo.AddComponent<XROrigin>();

            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originGo.transform);
            origin.CameraFloorOffsetObject = offset;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(offset.transform);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 80f;   // section 5.1 caps course objects at 75 m
            origin.Camera = cam;

            camGo.AddComponent<ARCameraManager>();
            camGo.AddComponent<ARCameraBackground>();
            // Depth occlusion where supported (section 7).
            camGo.AddComponent<AROcclusionManager>();

            // NOTE: deliberately NO AudioListener here. Bootstrap owns the only one,
            // and a second would silently degrade spatial audio.

            originGo.AddComponent<ARPlaneManager>();
            originGo.AddComponent<ARRaycastManager>();
            originGo.AddComponent<ARAnchorManager>();

            // ARMeshManager SHALL be a CHILD of the XR Origin, not a component on it.
            // It parents generated mesh GameObjects under its own transform, so AR
            // Foundation rejects it on the origin itself ("An ARMeshManager must be a
            // child of an XROrigin") and forces component removal on scene open.
            var meshing = new GameObject("Meshing");
            meshing.transform.SetParent(originGo.transform, worldPositionStays: false);
            meshing.AddComponent<ARMeshManager>();

            // Session driver bridges provider state into the deterministic
            // AnchorEligibility machine (section 5.2).
            var driver = new GameObject("SessionDriver");
            driver.transform.SetParent(originGo.transform);
            driver.AddComponent<Gibi.Spatial.ARSessionDriver>();

            // ARSurfaceProbe is the section 4 adapter seam — the only place outside
            // Gibi.Spatial that touches AR Foundation raycasting. PlacementController
            // finds it via GetComponentInParent, so it must live on the XR Origin.
            originGo.AddComponent<Gibi.Spatial.ARSurfaceProbe>();

            var placement = new GameObject("PlacementController");
            placement.transform.SetParent(originGo.transform);
            placement.AddComponent<Gibi.Gameplay.PlacementController>();

            // Without these two the scene renders a camera feed and nothing else — no
            // pet is ever requested and no tap is ever read.
            var session = new GameObject("P0Session");
            session.transform.SetParent(originGo.transform);
            session.AddComponent<Gibi.Gameplay.P0SessionDriver>();

            var input = new GameObject("TapToPlace");
            input.transform.SetParent(originGo.transform);
            input.AddComponent<Gibi.UI.TapToPlace>();

            EditorSceneManager.SaveScene(scene, SceneValidator.ARWorldScene);
        }

        // ------------------------------------------------------------------
        // PetSandbox: PetRoot, deterministic motion controller, animation graph,
        // IK graph, interaction volumes, spatial audio emitter, effects pool.
        // ------------------------------------------------------------------
        private static void BuildPetSandbox()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var petRoot = new GameObject("PetRoot");
            petRoot.AddComponent<Gibi.Pets.PetController>();

            // Section 6.3: "Pet colliders SHALL be authored runtime primitives.
            // Model mesh colliders are forbidden."
            var body = new GameObject("BodyCapsule");
            body.transform.SetParent(petRoot.transform);
            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.direction = 2;        // Z, along the spine
            capsule.height = 0.90f;       // tuned to a 0.50 m-shoulder dog
            capsule.radius = 0.13f;
            capsule.center = new Vector3(0f, 0.28f, 0f);

            // Where the signed, verified GLB is instantiated at runtime.
            var assetRoot = new GameObject("PetAssetRoot");
            assetRoot.transform.SetParent(petRoot.transform);

            var interaction = new GameObject("InteractionVolumes");
            interaction.transform.SetParent(petRoot.transform);
            var petZone = interaction.AddComponent<SphereCollider>();
            petZone.isTrigger = true;
            petZone.radius = 0.6f;

            var audio = new GameObject("SpatialAudioEmitter");
            audio.transform.SetParent(petRoot.transform);
            var src = audio.AddComponent<AudioSource>();
            src.spatialBlend = 1f;                       // fully spatialised
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 0.5f;                      // section 7
            src.maxDistance = 8.0f;                      // section 7
            src.playOnAwake = false;

            var effects = new GameObject("EffectsPool");
            effects.transform.SetParent(petRoot.transform);

            EditorSceneManager.SaveScene(scene, SceneValidator.PetSandboxScene);
        }
    }
}
