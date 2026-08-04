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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Gibi.Editor
{
    public static class SceneBuilder
    {
        private const string SceneDir = "Assets/Scenes";
        private const string RandyProfilePath =
            "Assets/Gibi/Pets/Profiles/Randy11P0.asset";
        private const string DogHouseAssetPath =
            "Assets/Gibi/Art/P0/luxurydoghouse.glb";
        private const string ToyAssetPath =
            "Assets/Gibi/Art/P0/toyball-930.glb";

        private sealed class SandboxComposition
        {
            public Transform Root;
            public Gibi.Pets.SandboxBoundary Boundary;
            public Gibi.Pets.SandboxDemoDirector Director;
        }

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

            // Without this the origin defaults to Device, so world content is positioned
            // relative to where the phone was at start rather than to the detected floor.
            origin.RequestedTrackingOriginMode = Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Floor;

            camGo.AddComponent<ARCameraManager>();
            camGo.AddComponent<ARCameraBackground>();
            // ARCameraBackground supplies pixels, but it does not drive the camera
            // transform. Without this pose driver, anchors briefly render for the launch
            // pose and disappear as soon as the phone moves because the virtual camera
            // remains at its scene-authored origin.
            var trackedPoseDriver = camGo.AddComponent<TrackedPoseDriver>();
            var positionAction = new InputAction(
                "Position",
                binding: "<XRHMD>/centerEyePosition",
                expectedControlType: "Vector3");
            positionAction.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rotationAction = new InputAction(
                "Rotation",
                binding: "<XRHMD>/centerEyeRotation",
                expectedControlType: "Quaternion");
            rotationAction.AddBinding("<HandheldARInputDevice>/deviceRotation");
            trackedPoseDriver.positionInput = new InputActionProperty(positionAction);
            trackedPoseDriver.rotationInput = new InputActionProperty(rotationAction);
            // Depth occlusion where supported (section 7).
            camGo.AddComponent<AROcclusionManager>();

            // NOTE: deliberately NO AudioListener here. Bootstrap owns the only one,
            // and a second would silently degrade spatial audio.

            var planeManager = originGo.AddComponent<ARPlaneManager>();
            // P0 places the complete dog sandbox on the floor. Asking ARCore for every
            // plane orientation adds wall/ceiling noise without helping this flow and
            // makes the first useful horizontal result slower and harder to recognize.
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            // Without a plane prefab, detected planes are INVISIBLE. AR then appears
            // broken even when tracking is perfect, because the player has no way to see
            // what the device has found or where it is safe to tap.
            planeManager.planePrefab = CreatePlaneVisualizerPrefab();
            originGo.AddComponent<ARRaycastManager>();
            var anchorManager = originGo.AddComponent<ARAnchorManager>();
            var anchorHost = originGo.AddComponent<Gibi.Spatial.ARWorldAnchorHost>();
            anchorHost.Configure(anchorManager);

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

            // The complete sandbox moves as one placement unit. It is hidden until the
            // first accepted tap, and ordinary later taps can no longer relocate it.
            var sandbox = BuildSandboxComposition(originGo.transform, includeValidationFloor: false);
            sandbox.Root.gameObject.SetActive(false);

            // Without these two the scene renders a camera feed and nothing else — no
            // pet is ever requested and no tap is ever read.
            var session = new GameObject("P0Session");
            session.transform.SetParent(originGo.transform);
            var p0 = session.AddComponent<Gibi.Gameplay.P0SessionDriver>();
            p0.ConfigureAnimationProfile(EnsureRandy11Profile());
            p0.ConfigureWorld(sandbox.Root, sandbox.Boundary, sandbox.Director,
                              autoSpawn: false, anchorHostBehaviour: anchorHost);

            // Section 5.3: the placement ring needs actual geometry to tint. A ring with
            // no renderer encodes status into nothing at all.
            var ringGo = new GameObject("PlacementRing");
            ringGo.transform.SetParent(originGo.transform);
            var ring = ringGo.AddComponent<Gibi.UI.PlacementRing>();
            BuildRingMesh(ringGo, ring);

            var input = new GameObject("TapToPlace");
            input.transform.SetParent(originGo.transform);
            input.AddComponent<Gibi.UI.TapToPlace>();

            EditorSceneManager.SaveScene(scene, SceneValidator.ARWorldScene);
        }

        // ------------------------------------------------------------------
        // PetSandbox: the same composition used under AR placement, plus a visible
        // validation floor/boundary and automatic verified-pet spawn.
        // ------------------------------------------------------------------
        private static void BuildPetSandbox()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sandbox = BuildSandboxComposition(null, includeValidationFloor: true);

            var session = new GameObject("SandboxSession");
            var p0 = session.AddComponent<Gibi.Gameplay.P0SessionDriver>();
            p0.ConfigureAnimationProfile(EnsureRandy11Profile());
            p0.ConfigureWorld(sandbox.Root, sandbox.Boundary, sandbox.Director,
                              autoSpawn: true);

            BuildSandboxPreviewRig();

            EditorSceneManager.SaveScene(scene, SceneValidator.PetSandboxScene);
        }

        private static void BuildSandboxPreviewRig()
        {
            var cameraGo = new GameObject("ValidationCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(2.65f, 1.55f, -3.15f);
            cameraGo.transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 0.38f, 0.75f) - cameraGo.transform.position,
                Vector3.up);
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 52f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.075f, 0.09f, 0.10f, 1f);
            cameraGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("ValidationSun");
            lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.94f, 0.84f);
        }

        private static SandboxComposition BuildSandboxComposition(
            Transform parent, bool includeValidationFloor)
        {
            var rootGo = new GameObject("PlacedWorldRoot");
            if (parent != null) rootGo.transform.SetParent(parent, worldPositionStays: false);

            var boundary = rootGo.AddComponent<Gibi.Pets.SandboxBoundary>();
            boundary.Configure(new Vector2(2.1f, 2.1f));

            var content = new GameObject("SandboxContent");
            content.transform.SetParent(rootGo.transform, false);

            if (includeValidationFloor)
                BuildValidationGroundAndBoundary(content.transform);

            var returnPoint = new GameObject("FetchReturnPoint");
            returnPoint.transform.SetParent(content.transform, false);
            returnPoint.transform.localPosition = Vector3.zero;

            var toy = InstantiateArtAsset(ToyAssetPath, "FetchToy", content.transform);
            toy.transform.localPosition = new Vector3(0.85f, 0f, 0.30f);
            toy.transform.localRotation = Quaternion.identity;
            var fetchToy = toy.AddComponent<Gibi.Pets.FetchToy>();
            // This GLB's pivot is already on the bottom of its measured 0.067 m bounds.
            fetchToy.Configure(0f);

            var house = new GameObject("DogHouse");
            house.transform.SetParent(content.transform, false);
            house.transform.localPosition = new Vector3(0f, 0f, 1.55f);

            var houseVisual = InstantiateArtAsset(
                DogHouseAssetPath, "DogHouseVisual", house.transform);
            // Blender's -Y front exports to Unity +Z; rotate the visual so its entrance
            // faces the dog and the affordance's local -Z threshold.
            houseVisual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var rest = house.AddComponent<Gibi.Pets.RestAffordance>();
            // Measured: centre X 0.0287 m; Unity depth 1.084 m.
            rest.ConfigureVisibleThresholdRest(0.0287f, 1.084f);

            var directorGo = new GameObject("SandboxDemoDirector");
            directorGo.transform.SetParent(content.transform, false);
            var director = directorGo.AddComponent<Gibi.Pets.SandboxDemoDirector>();
            director.Configure(fetchToy, rest, returnPoint.transform, shouldLoop: true);

            return new SandboxComposition
            {
                Root = rootGo.transform,
                Boundary = boundary,
                Director = director,
            };
        }

        private static GameObject InstantiateArtAsset(
            string assetPath, string instanceName, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
                throw new FileNotFoundException(
                    $"Required sandbox art did not import as a GameObject: {assetPath}");

            var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (instance == null) instance = Object.Instantiate(asset);
            instance.name = instanceName;
            instance.transform.SetParent(parent, worldPositionStays: false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static void BuildValidationGroundAndBoundary(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ValidationGround";
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = new Vector3(0.42f, 1f, 0.42f);

            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.name = "ValidationGroundMaterial";
            if (groundMat.HasProperty("_BaseColor"))
                groundMat.SetColor("_BaseColor", new Color(0.16f, 0.20f, 0.18f, 1f));
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            BuildBoundaryRail(parent, "BoundaryNorth", new Vector3(0f, 0.06f, 2.1f),
                              new Vector3(4.2f, 0.12f, 0.04f));
            BuildBoundaryRail(parent, "BoundarySouth", new Vector3(0f, 0.06f, -2.1f),
                              new Vector3(4.2f, 0.12f, 0.04f));
            BuildBoundaryRail(parent, "BoundaryEast", new Vector3(2.1f, 0.06f, 0f),
                              new Vector3(0.04f, 0.12f, 4.2f));
            BuildBoundaryRail(parent, "BoundaryWest", new Vector3(-2.1f, 0.06f, 0f),
                              new Vector3(0.04f, 0.12f, 4.2f));
        }

        private static void BuildBoundaryRail(
            Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = name;
            rail.transform.SetParent(parent, false);
            rail.transform.localPosition = position;
            rail.transform.localScale = scale;
            var renderer = rail.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        private static Gibi.Pets.PetAnimationProfile EnsureRandy11Profile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<Gibi.Pets.PetAnimationProfile>(
                RandyProfilePath);
            if (profile != null) return profile;

            Directory.CreateDirectory(Path.GetDirectoryName(RandyProfilePath));
            profile = ScriptableObject.CreateInstance<Gibi.Pets.PetAnimationProfile>();
            profile.ApplyRandy11P0Defaults();
            AssetDatabase.CreateAsset(profile, RandyProfilePath);
            return profile;
        }
    
        /// <summary>
        /// A minimal translucent plane visualizer. AR Foundation drives the mesh through
        /// ARPlaneMeshVisualizer; this only supplies geometry and a material to tint.
        /// </summary>
        private static GameObject CreatePlaneVisualizerPrefab()
        {
            const string dir = "Assets/Prefabs";
            const string path = dir + "/ARPlaneVisualizer.prefab";
            var planeColor = new Color(0.12f, 0.85f, 0.95f, 0.45f);
            Directory.CreateDirectory(dir);

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var existingRenderer = existing.GetComponent<MeshRenderer>();
                if (existingRenderer != null && existingRenderer.sharedMaterial != null)
                {
                    SetTransparent(existingRenderer.sharedMaterial, planeColor);
                    EditorUtility.SetDirty(existingRenderer.sharedMaterial);
                }
                return existing;
            }

            var go = new GameObject("ARPlaneVisualizer");
            go.AddComponent<ARPlane>();
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshCollider>();
            var mr = go.AddComponent<MeshRenderer>();
            go.AddComponent<ARPlaneMeshVisualizer>();

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.name = "ARPlaneMaterial";
            SetTransparent(mat, planeColor);
            AssetDatabase.CreateAsset(mat, dir + "/ARPlaneMaterial.mat");
            mr.sharedMaterial = mat;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>Flat disc the PlacementRing tints per section 5.3.</summary>
        private static void BuildRingMesh(GameObject parent, Gibi.UI.PlacementRing ring)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "RingMesh";
            disc.transform.SetParent(parent.transform, false);
            // 0.22 m across — comfortably smaller than the 0.45 m pet clearance so the
            // ring reads as a marker rather than as the placement volume itself.
            disc.transform.localScale = new Vector3(0.22f, 0.003f, 0.22f);
            Object.DestroyImmediate(disc.GetComponent<Collider>());

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.name = "PlacementRingMaterial";
            SetTransparent(mat, new Color(0.2f, 0.75f, 0.35f, 0.75f));
            disc.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Start hidden: before the first successful probe there is nothing to mark.
            disc.GetComponent<MeshRenderer>().enabled = false;

            var so = new SerializedObject(ring);
            var prop = so.FindProperty("ringRenderer");
            if (prop != null) prop.objectReferenceValue = disc.GetComponent<MeshRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetTransparent(Material m, Color c)
        {
            m.SetFloat("_Surface", 1f);                 // transparent
            m.SetFloat("_Blend", 0f);                   // alpha
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            // AR provider mesh winding is an implementation detail. Rendering both
            // sides keeps the discovered floor visible from above on every provider.
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
    }
}
