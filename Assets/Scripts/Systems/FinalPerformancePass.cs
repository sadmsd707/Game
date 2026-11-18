using UnityEngine;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// FinalPerformancePass – applied at game startup on Android.
    /// Runs a one-time warm-up sequence to prevent first-frame hitches:
    ///   1. Shader warm-up (pre-compiles all shaders in the warm-up list)
    ///   2. Forces Physics.Simulate(0) to settle rigidbodies
    ///   3. Forces a GC.Collect() before gameplay starts (clean slate)
    ///   4. Logs device capabilities for QA reports
    ///
    /// Place this on the same GO as AndroidPerformanceSettings.
    /// Call WarmUp() from SceneBootstrapper.Start() or MainMenu.OnGameStart().
    /// </summary>
    public class FinalPerformancePass : MonoBehaviour
    {
        [Header("Shader Warm-up")]
        [Tooltip("Assign a ShaderVariantCollection asset containing all shaders used in the game.")]
        [SerializeField] private UnityEngine.Rendering.ShaderVariantCollection shaderVariants;

        [Header("Warm-up Settings")]
        [SerializeField] private bool warmUpOnAwake       = true;
        [SerializeField] private bool logDeviceCapabilities = true;
        [SerializeField] private bool forceGCOnWarmup     = true;

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (warmUpOnAwake) WarmUp();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        public void WarmUp()
        {
            // 1. Shader warm-up
            if (shaderVariants != null)
            {
                shaderVariants.WarmUp();
                Debug.Log("[FinalPerformancePass] Shader variants warmed up.");
            }
            else
            {
                // Fallback: warm up all shaders currently loaded
                Shader.WarmupAllShaders();
                Debug.Log("[FinalPerformancePass] All shaders warmed up (no collection assigned).");
            }

            // 2. Force GC before gameplay
            if (forceGCOnWarmup)
            {
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                Debug.Log("[FinalPerformancePass] GC.Collect() done — heap cleaned before gameplay.");
            }

            // 3. Log device capabilities
            if (logDeviceCapabilities) LogDeviceInfo();
        }

        // ── Device logging (for QA) ────────────────────────────────────────────────

        private void LogDeviceInfo()
        {
            Debug.Log(
                $"═══ HeavyVehiclesWorld – Device Capabilities ═══\n" +
                $"Device:       {SystemInfo.deviceModel}\n" +
                $"OS:           {SystemInfo.operatingSystem}\n" +
                $"CPU:          {SystemInfo.processorType} × {SystemInfo.processorCount} cores\n" +
                $"RAM:          {SystemInfo.systemMemorySize} MB\n" +
                $"GPU:          {SystemInfo.graphicsDeviceName}\n" +
                $"GPU RAM:      {SystemInfo.graphicsMemorySize} MB\n" +
                $"GPU API:      {SystemInfo.graphicsDeviceType}\n" +
                $"GPU Level:    {SystemInfo.graphicsDeviceVersion}\n" +
                $"Max Tex Size: {SystemInfo.maxTextureSize}\n" +
                $"Supports RT:  {SystemInfo.supportsRenderTextures}\n" +
                $"Supports 3D:  {SystemInfo.supports3DTextures}\n" +
                $"Vulkan:       {SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan}\n" +
                $"Screen:       {Screen.width}×{Screen.height} @ {Screen.dpi:F0} DPI\n" +
                $"Persistent:   {Application.persistentDataPath}\n" +
                $"═══════════════════════════════════════════════"
            );
        }
    }
}
