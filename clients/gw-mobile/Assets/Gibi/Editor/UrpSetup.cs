// GW-ARCH-001 section 7 — "URP SHALL be used. The exact URP package version SHALL be the
// version resolved by the pinned Unity editor and recorded in packages-lock.json."
//
// Also creates the tiered quality profiles section 7 requires:
//   Tier A/B  60 fps, one main-light pet shadow at 1024
//   Tier C    30 fps, blob/contact shadow fallback (no realtime shadow map)
//
// THE AR BACKGROUND FEATURE IS THE POINT. Without ARBackgroundRendererFeature on the
// renderer, URP never blits the camera texture, so an AR build renders content over a
// blank background — the pet floats in a void instead of the room. Niantic's own setup
// docs link a URP page that currently 404s, so this is written from the AR Foundation
// requirement directly.
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;

namespace Gibi.Editor
{
    public static class UrpSetup
    {
        private const string SettingsDir = "Assets/Settings";
        private const string RendererPath = SettingsDir + "/Gibi_UniversalRenderer.asset";
        private const string TierABPath   = SettingsDir + "/Gibi_URP_TierAB.asset";
        private const string TierCPath    = SettingsDir + "/Gibi_URP_TierC.asset";

        [MenuItem("GibiWorld/Set Up URP for AR")]
        public static void Setup()
        {
            System.IO.Directory.CreateDirectory(SettingsDir);

            var rendererData = CreateOrLoadRenderer();
            EnsureArBackgroundFeature(rendererData);

            var tierAB = CreateOrLoadPipeline(TierABPath, rendererData,
                shadowDistance: 20f, shadowResolution: 1024, softShadows: true, msaa: 2);
            var tierC = CreateOrLoadPipeline(TierCPath, rendererData,
                shadowDistance: 0f, shadowResolution: 256, softShadows: false, msaa: 0);

            // Section 7: URP is the pipeline for every tier. Tier C differs in budget,
            // not in pipeline — a second pipeline would double the shader variant cost.
            GraphicsSettings.defaultRenderPipeline = tierAB;
            QualitySettings.renderPipeline = tierAB;

            AssignPerQualityLevel(tierAB, tierC);

            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[GibiWorld] URP configured for AR. " +
                      $"Renderer features: {rendererData.rendererFeatures.Count}. " +
                      $"Pipeline: {GraphicsSettings.defaultRenderPipeline?.name ?? "NONE"}");
        }

        public static void SetupForCI()
        {
            Setup();
            EditorApplication.Exit(Verify() ? 0 : 1);
        }

        private static UniversalRendererData CreateOrLoadRenderer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (existing != null) return existing;

            var data = ScriptableObject.CreateInstance<UniversalRendererData>();
            data.name = "Gibi_UniversalRenderer";
            AssetDatabase.CreateAsset(data, RendererPath);
            return data;
        }

        /// <summary>
        /// Adds ARBackgroundRendererFeature if absent. Without it the camera feed is never
        /// drawn and every AR build renders against a blank background.
        /// </summary>
        private static void EnsureArBackgroundFeature(UniversalRendererData data)
        {
            if (data.rendererFeatures.Any(f => f is ARBackgroundRendererFeature))
            {
                Debug.Log("[GibiWorld] ARBackgroundRendererFeature already present.");
                return;
            }

            var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
            feature.name = "AR Background";

            // The feature must be a sub-asset of the renderer or it will not survive a
            // domain reload — it would silently vanish and the background would go blank
            // again on the next open.
            AssetDatabase.AddObjectToAsset(feature, data);

            data.rendererFeatures.Add(feature);
            data.SetDirty();

            Debug.Log("[GibiWorld] Added ARBackgroundRendererFeature to the URP renderer.");
        }

        private static UniversalRenderPipelineAsset CreateOrLoadPipeline(
            string path, UniversalRendererData renderer,
            float shadowDistance, int shadowResolution, bool softShadows, int msaa)
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            UniversalRenderPipelineAsset asset = existing;

            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);

            SetIfPresent(so, "m_ShadowDistance", shadowDistance);
            SetIfPresent(so, "m_MainLightShadowmapResolution", shadowResolution);
            SetIfPresent(so, "m_SoftShadowsSupported", softShadows);
            SetIfPresent(so, "m_MSAA", msaa);

            // Section 7: "Dynamic resolution MAY vary 0.70-1.00."
            SetIfPresent(so, "m_RenderScale", 1.0f);

            // Section 7: depth occlusion is handled by AROcclusionManager, so URP's own
            // depth texture is required for the pet to be occluded correctly.
            SetIfPresent(so, "m_RequireDepthTexture", true);
            SetIfPresent(so, "m_RequireOpaqueTexture", false);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void SetIfPresent(SerializedObject so, string prop, object value)
        {
            var p = so.FindProperty(prop);
            if (p == null) return;   // URP renames internals between versions; skip quietly
            switch (value)
            {
                case float f when p.propertyType == SerializedPropertyType.Float: p.floatValue = f; break;
                case int i when p.propertyType == SerializedPropertyType.Integer:  p.intValue = i; break;
                case int i when p.propertyType == SerializedPropertyType.Enum:     p.enumValueIndex = i; break;
                case bool b when p.propertyType == SerializedPropertyType.Boolean: p.boolValue = b; break;
            }
        }

        /// <summary>Section 7 tiering: lower quality levels get the Tier C budget.</summary>
        private static void AssignPerQualityLevel(UniversalRenderPipelineAsset ab,
                                                  UniversalRenderPipelineAsset c)
        {
            int count = QualitySettings.names.Length;
            int original = QualitySettings.GetQualityLevel();

            for (int i = 0; i < count; i++)
            {
                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                QualitySettings.renderPipeline = i == 0 ? c : ab;
            }

            QualitySettings.SetQualityLevel(original, applyExpensiveChanges: false);
        }

        [MenuItem("GibiWorld/Verify URP AR Setup")]
        public static bool VerifyMenu() => Verify();

        public static bool Verify()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                Debug.LogError("[GibiWorld] Section 7 VIOLATED: no URP asset assigned — " +
                               "the project is still on the Built-in Render Pipeline.");
                return false;
            }

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                Debug.LogError("[GibiWorld] URP renderer asset missing.");
                return false;
            }

            bool hasAr = renderer.rendererFeatures.Any(f => f is ARBackgroundRendererFeature);
            if (!hasAr)
            {
                Debug.LogError("[GibiWorld] ARBackgroundRendererFeature missing — the camera " +
                               "feed will not render and AR content will float over a blank background.");
                return false;
            }

            Debug.Log($"[GibiWorld] URP AR setup verified: {pipeline.name}, AR background feature present.");
            return true;
        }
    }
}
