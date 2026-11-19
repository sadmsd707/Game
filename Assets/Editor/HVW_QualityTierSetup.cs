using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HeavyVehiclesWorld.Editor
{
    public static class HVW_QualityTierSetup
    {
        [MenuItem("HeavyVehiclesWorld/Setup Quality Tiers (Phase 3)")]
        public static void SetupAllQualityTiers()
        {
            Debug.Log("[HVW_QualityTierSetup] Building URP Quality Tiers (Low, Medium, High, Ultra)...");

            Directory.CreateDirectory("Assets/Settings");
            Directory.CreateDirectory("Assets/Resources/Settings");

            // 1. Setup Low Tier
            var lowUrp = CreateQualityTierAsset(
                "URP_Quality_Low",
                msaa: 1,
                renderScale: 1.0f,
                shadowDistance: 40f,
                cascades: 1,
                softShadows: false,
                hdr: true,
                addSSAO: false
            );

            // 2. Setup Medium Tier
            var medUrp = CreateQualityTierAsset(
                "URP_Quality_Medium",
                msaa: 2,
                renderScale: 1.0f,
                shadowDistance: 60f,
                cascades: 2,
                softShadows: true,
                hdr: true,
                addSSAO: false
            );

            // 3. Setup High Tier
            var highUrp = CreateQualityTierAsset(
                "URP_Quality_High",
                msaa: 4,
                renderScale: 1.0f,
                shadowDistance: 100f,
                cascades: 3,
                softShadows: true,
                hdr: true,
                addSSAO: false
            );

            // 4. Setup Ultra Tier (with SSAO)
            var ultraUrp = CreateQualityTierAsset(
                "URP_Quality_Ultra",
                msaa: 4,
                renderScale: 1.15f,
                shadowDistance: 120f,
                cascades: 4,
                softShadows: true,
                hdr: true,
                addSSAO: true
            );

            // Copy or mirror references into Assets/Resources/Settings so release builds can load them
            CopyAssetToResources(lowUrp, "URP_Quality_Low");
            CopyAssetToResources(medUrp, "URP_Quality_Medium");
            CopyAssetToResources(highUrp, "URP_Quality_High");
            CopyAssetToResources(ultraUrp, "URP_Quality_Ultra");

            // Assign across QualitySettings
            // Level 0: Very Low -> lowUrp
            // Level 1: Low -> lowUrp
            // Level 2: Medium -> medUrp
            // Level 3: High -> highUrp
            // Level 4: Very High -> highUrp
            // Level 5: Ultra -> ultraUrp
            AssignPipelineToQualityLevel(0, lowUrp);
            AssignPipelineToQualityLevel(1, lowUrp);
            AssignPipelineToQualityLevel(2, medUrp);
            AssignPipelineToQualityLevel(3, highUrp);
            AssignPipelineToQualityLevel(4, highUrp);
            AssignPipelineToQualityLevel(5, ultraUrp);

            // Default defaultRenderPipeline = highUrp
            GraphicsSettings.defaultRenderPipeline = highUrp;
            QualitySettings.SetQualityLevel(3, true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HVW_QualityTierSetup] Successfully created and mapped all 4 Quality Tier URP Assets!");
        }

        private static UniversalRenderPipelineAsset CreateQualityTierAsset(
            string name,
            int msaa,
            float renderScale,
            float shadowDistance,
            int cascades,
            bool softShadows,
            bool hdr,
            bool addSSAO)
        {
            string rendererPath = $"Assets/Settings/{name}_Renderer.asset";
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, rendererPath);
            }

            if (addSSAO)
            {
                AddSSAOFeatureIfSupported(rendererData);
            }

            EditorUtility.SetDirty(rendererData);

            string urpPath = $"Assets/Settings/{name}.asset";
            var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
            if (urpAsset == null)
            {
                urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urpAsset, urpPath);
            }

            // Set serialized properties
            var so = new SerializedObject(urpAsset);
            SetSerializedProperty(so, "m_SupportsHDR", hdr);
            SetSerializedProperty(so, "m_MSAA", msaa);
            SetSerializedProperty(so, "m_RenderScale", renderScale);
            SetSerializedProperty(so, "m_ShadowDistance", shadowDistance);
            SetSerializedProperty(so, "m_ShadowCascadeCount", cascades);
            SetSerializedProperty(so, "m_SoftShadowsSupported", softShadows ? 1 : 0);
            so.ApplyModifiedProperties();

            urpAsset.supportsHDR = hdr;
            urpAsset.msaaSampleCount = msaa;
            urpAsset.renderScale = renderScale;
            urpAsset.shadowDistance = shadowDistance;
            urpAsset.shadowCascadeCount = cascades;

            EditorUtility.SetDirty(urpAsset);
            return urpAsset;
        }

        private static void AddSSAOFeatureIfSupported(UniversalRendererData rendererData)
        {
            try
            {
                // Find ScreenSpaceAmbientOcclusion type in URP assembly
                var ssaoType = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                if (ssaoType != null)
                {
                    // Check if already present
                    bool alreadyExists = false;
                    foreach (var rf in rendererData.rendererFeatures)
                    {
                        if (rf != null && rf.GetType() == ssaoType)
                        {
                            alreadyExists = true;
                            break;
                        }
                    }

                    if (!alreadyExists)
                    {
                        var ssaoInstance = ScriptableObject.CreateInstance(ssaoType) as ScriptableRendererFeature;
                        if (ssaoInstance != null)
                        {
                            ssaoInstance.name = "ScreenSpaceAmbientOcclusion";
                            AssetDatabase.AddObjectToAsset(ssaoInstance, rendererData);

                            var so = new SerializedObject(rendererData);
                            var featuresProp = so.FindProperty("m_RendererFeatures");
                            if (featuresProp != null)
                            {
                                int idx = featuresProp.arraySize;
                                featuresProp.InsertArrayElementAtIndex(idx);
                                featuresProp.GetArrayElementAtIndex(idx).objectReferenceValue = ssaoInstance;
                                so.ApplyModifiedProperties();
                            }
                            Debug.Log("[HVW_QualityTierSetup] Added SSAO Feature to Ultra Renderer.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HVW_QualityTierSetup] SSAO configuration notice: {ex.Message}");
            }
        }

        private static void SetSerializedProperty(SerializedObject so, string propName, object val)
        {
            var p = so.FindProperty(propName);
            if (p != null)
            {
                if (val is bool b) p.boolValue = b;
                else if (val is int i) p.intValue = i;
                else if (val is float f) p.floatValue = f;
            }
        }

        private static void CopyAssetToResources(UniversalRenderPipelineAsset asset, string resourceName)
        {
            string dest = $"Assets/Resources/Settings/{resourceName}.asset";
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(dest) == null)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(asset), dest);
            }
        }

        private static void AssignPipelineToQualityLevel(int level, UniversalRenderPipelineAsset asset)
        {
            if (level >= 0 && level < QualitySettings.names.Length)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = asset;
            }
        }
    }
}
