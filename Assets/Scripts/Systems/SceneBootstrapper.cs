using UnityEngine;
using HeavyVehiclesWorld.Vehicles;
using HeavyVehiclesWorld.Controls;
using HeavyVehiclesWorld.Systems;
using HeavyVehiclesWorld.UI;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Place one of these in every scene. It wires up the singletons on Awake
    /// and ensures GameManager, InputManager, UIManager, and SaveSystem are ready.
    /// </summary>
    public class SceneBootstrapper : MonoBehaviour
    {
        [Header("Prefabs (drag from Project)")]
        [SerializeField] private GameObject gameManagerPrefab;
        [SerializeField] private GameObject inputManagerPrefab;
        [SerializeField] private GameObject uiManagerPrefab;

        [Header("Scene References")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private VehicleController[] sceneVehicles;

        private void Awake()
        {
            EnsureSingleton(ref gameManagerPrefab, "GameManager");
            EnsureSingleton(ref inputManagerPrefab, "InputManager");
            EnsureSingleton(ref uiManagerPrefab, "UIManager");
        }

        private void Start()
        {
            // Register all vehicles with the game manager
            if (GameManager.Instance != null && sceneVehicles != null)
            {
                foreach (var v in sceneVehicles)
                    GameManager.Instance.RegisterVehicle(v);
            }
        }

        private void EnsureSingleton(ref GameObject prefab, string objectName)
        {
            if (GameObject.Find(objectName) != null) return; // already in scene
            if (prefab != null)
            {
                Instantiate(prefab);
            }
            else
            {
                Debug.LogWarning($"[SceneBootstrapper] No prefab assigned for '{objectName}'. " +
                                 $"Please drag the prefab into the Inspector.");
            }
        }
    }
}
