using System.Collections;
using System.IO;
using UnityEngine;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Automatically takes proof screenshots on game start (Windows standalone only).
    ///
    /// Activated by command-line flag: -hvwScreenshots
    /// Screenshots are saved to <exeDir>/Screenshots/
    ///
    /// For each of the 6 vehicles: switches to it, orbits the photo camera to
    /// front-3/4, side, and rear angles, and captures a PNG at each angle.
    /// </summary>
    public class HVW_StartupScreenshot : MonoBehaviour
    {
        private static readonly string[] ANGLES_LABEL = { "front34", "side", "rear" };
        private static readonly float[]  ANGLES_AZ    = { 45f, 90f, 180f };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            bool wantScreenshots = false;
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg == "-hvwScreenshots") { wantScreenshots = true; break; }
            }
            if (!wantScreenshots) return;

            var go = new GameObject("HVW_StartupScreenshotRunner");
            go.AddComponent<HVW_StartupScreenshot>();
            DontDestroyOnLoad(go);
            Debug.Log("[Screenshot] AutoBootstrap started with -hvwScreenshots flag.");
        }

        private void Start()
        {
#if UNITY_EDITOR
            return; // Only runs in standalone builds
#endif

            // Only activate when -hvwScreenshots flag is present
            bool wantScreenshots = false;
            foreach (var arg in System.Environment.GetCommandLineArgs())
                if (arg == "-hvwScreenshots") { wantScreenshots = true; break; }

            if (!wantScreenshots) return;

            StartCoroutine(TakeAllScreenshots());
        }

        private IEnumerator TakeAllScreenshots()
        {
            yield return new WaitForSeconds(5f); // Let world finish loading

            string exeDir = Path.GetDirectoryName(System.Environment.GetCommandLineArgs()[0]);
            string shotDir = Path.GetFullPath(Path.Combine(exeDir, "..", "Screenshots"));
            Directory.CreateDirectory(shotDir);
            Debug.Log($"[Screenshot] Saving to: {shotDir}");

            var initializer = HVW_WorldInitializer.Instance ?? FindAnyObjectByType<HVW_WorldInitializer>();
            float waitTimer = 15f;
            while (initializer == null && waitTimer > 0f)
            {
                yield return new WaitForSeconds(0.5f);
                waitTimer -= 0.5f;
                initializer = HVW_WorldInitializer.Instance ?? FindAnyObjectByType<HVW_WorldInitializer>();
            }

            if (initializer == null)
            {
                Debug.LogError("[Screenshot] HVW_WorldInitializer not found after waiting.");
                yield break;
            }

            var photoCamera = HVW_PhotoCamera.Instance ?? FindAnyObjectByType<HVW_PhotoCamera>();

            // Iterate over all vehicle types in order
            var vehicleTypes = System.Enum.GetValues(typeof(Vehicles.VehicleType));
            foreach (Vehicles.VehicleType vType in vehicleTypes)
            {
                // Switch active vehicle
                initializer.SwitchToVehicle(vType);
                yield return new WaitForSeconds(1.5f);

                // For each camera angle
                for (int a = 0; a < ANGLES_AZ.Length; a++)
                {
                    if (photoCamera != null)
                    {
                        photoCamera.SetAzimuth(ANGLES_AZ[a]);
                        yield return new WaitForSeconds(0.5f);
                    }

                    string filename = $"Step4_{vType}_{ANGLES_LABEL[a]}.png";
                    string path     = Path.Combine(shotDir, filename);
                    ScreenCapture.CaptureScreenshot(path, 2); // 2× super-sampling
                    yield return new WaitForEndOfFrame();
                    Debug.Log($"[Screenshot] Saved: {filename}");
                }
            }

            Debug.Log("[Screenshot] All screenshots complete. Quitting.");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }
    }
}
