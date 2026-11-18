#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HeavyVehiclesWorld.Systems;

namespace HeavyVehiclesWorld.EditorTools
{
    public static class HVW_SceneGenerator
    {
        [MenuItem("Tools/HeavyVehiclesWorld/🏗️ Setup MainWorld Scene")]
        public static void GenerateAndSaveMainWorld()
        {
            string scenePath = "Assets/Scenes/MainWorld.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Ensure WorldInitializer component is in the scene
            var initGo = GameObject.Find("WorldInitializer");
            if (initGo == null)
            {
                initGo = new GameObject("WorldInitializer");
            }
            if (initGo.GetComponent<HVW_WorldInitializer>() == null)
            {
                initGo.AddComponent<HVW_WorldInitializer>();
            }
            if (initGo.GetComponent<HVW_VisualSystem>() == null)
            {
                initGo.AddComponent<HVW_VisualSystem>();
            }

            // Ensure Main Camera has Follow Camera
            var camGo = GameObject.Find("Main Camera");
            if (camGo != null)
            {
                if (camGo.GetComponent<HVW_FollowCamera>() == null)
                {
                    camGo.AddComponent<HVW_FollowCamera>();
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[HVW_SceneGenerator] ✅ MainWorld.unity successfully configured and saved!");
        }
    }
}
#endif
