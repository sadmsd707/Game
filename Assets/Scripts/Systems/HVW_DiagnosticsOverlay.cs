using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Real-time Diagnostics & Safety Net Overlay.
    /// Works in release and development builds on Android and Editor.
    /// Refreshes 4 times per second (every 0.25s) using unscaled delta time.
    /// Toggled via the HUD 'DBG' button.
    /// </summary>
    public class HVW_DiagnosticsOverlay : MonoBehaviour
    {
        public static HVW_DiagnosticsOverlay Instance { get; private set; }

        // Touch Input Pointer Tracking
        public static bool IsGasPointerDown { get; set; }
        public static bool IsBrakePointerDown { get; set; }
        public static bool IsSteerLeftPointerDown { get; set; }
        public static bool IsSteerRightPointerDown { get; set; }

        /// <summary>
        /// Add a warning/info message to the diagnostics ring buffer from any system
        /// (e.g. HVW_ModelLoader reporting missing models). Thread-safe.
        /// </summary>
        public static void AddWarning(string message)
        {
            if (Instance == null) return;
            lock (Instance._logLock)
            {
                string timeStr = DateTime.Now.ToString("HH:mm:ss");
                Instance._lastErrors.Add($"[{timeStr}] WARN: {message}");
                if (Instance._lastErrors.Count > 8)
                    Instance._lastErrors.RemoveAt(0);
            }
        }

        private GameObject _overlayPanel;
        private Text _diagnosticsText;
        private ScrollRect _scrollRect;
        private float _refreshTimer = 0f;
        private const float REFRESH_INTERVAL = 0.25f; // 4 times per second

        // Ring buffer for last 8 errors/exceptions
        private readonly List<string> _lastErrors = new List<string>(8);
        private readonly object _logLock = new object();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.logMessageReceived += HandleLogMessage;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= HandleLogMessage;
        }

        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                lock (_logLock)
                {
                    string timeStr = DateTime.Now.ToString("HH:mm:ss");
                    string snippet = condition.Length > 80 ? condition.Substring(0, 80) + "..." : condition;
                    _lastErrors.Add($"[{timeStr}] {type}: {snippet}");
                    if (_lastErrors.Count > 8)
                    {
                        _lastErrors.RemoveAt(0);
                    }
                }
            }
        }

        public void InitializeUI(GameObject canvasGo)
        {
            if (_overlayPanel != null) return;

            _overlayPanel = new GameObject("Panel_DiagnosticsOverlay");
            _overlayPanel.transform.SetParent(canvasGo.transform, false);
            var rect = _overlayPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, 0.05f);
            rect.anchorMax = new Vector2(0.95f, 0.95f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = _overlayPanel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.07f, 0.95f);

            // Header Bar
            var headerGo = new GameObject("Header");
            headerGo.transform.SetParent(_overlayPanel.transform, false);
            var hRect = headerGo.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.anchoredPosition = new Vector2(0f, -22f);
            hRect.sizeDelta = new Vector2(0f, 44f);

            var titleText = CreateText(headerGo, "Txt_Title", "🔧 REAL-TIME DIAGNOSTICS OVERLAY (4 Hz)", 18, TextAnchor.MiddleLeft, new Vector2(20f, 0f), new Vector2(500f, 36f), new Color(0.3f, 0.85f, 1f));

            // Close Button
            var closeBtn = CreateButton(headerGo, "Btn_CloseDbg", "✕ CLOSE", new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(110f, 32f), new Color(0.75f, 0.2f, 0.2f, 0.95f), () => Toggle());

            // Scroll View Area
            var scrollGo = new GameObject("ScrollView");
            scrollGo.transform.SetParent(_overlayPanel.transform, false);
            var sRect = scrollGo.AddComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.02f, 0.03f);
            sRect.anchorMax = new Vector2(0.98f, 0.90f);
            sRect.offsetMin = Vector2.zero;
            sRect.offsetMax = Vector2.zero;

            _scrollRect = scrollGo.AddComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var mask = scrollGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.3f);

            // Content
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(scrollGo.transform, false);
            var cRect = contentGo.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0f, 1f);
            cRect.anchorMax = new Vector2(1f, 1f);
            cRect.pivot = new Vector2(0f, 1f);
            cRect.anchoredPosition = Vector2.zero;
            cRect.sizeDelta = new Vector2(0f, 950f);

            _scrollRect.content = cRect;

            // Diagnostics Text
            _diagnosticsText = CreateText(contentGo, "Txt_Content", "Loading diagnostics...", 14, TextAnchor.UpperLeft, new Vector2(10f, -10f), new Vector2(1800f, 930f), Color.white);
            _diagnosticsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _diagnosticsText.verticalOverflow = VerticalWrapMode.Overflow;

            _overlayPanel.SetActive(false);
        }

        public void Toggle()
        {
            if (_overlayPanel != null)
            {
                bool next = !_overlayPanel.activeSelf;
                _overlayPanel.SetActive(next);
                if (next) UpdateDiagnosticsText();
            }
        }

        public bool IsVisible => _overlayPanel != null && _overlayPanel.activeSelf;

        private void Update()
        {
            if (_overlayPanel == null || !_overlayPanel.activeSelf) return;

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= REFRESH_INTERVAL)
            {
                _refreshTimer = 0f;
                UpdateDiagnosticsText();
            }
        }

        public void UpdateDiagnosticsText()
        {
            if (_diagnosticsText == null) return;

            var sb = new StringBuilder(1024);
            var worldInit = HVW_WorldInitializer.Instance;
            var activeVeh = worldInit != null ? worldInit.ActiveVehicle : null;

            // 1. System, Build & Pipeline
            sb.AppendLine("=== [1. SYSTEM, BUILD & PIPELINE] ===");
            sb.AppendLine($"Build Stamp:     {HVW_BuildInfo.FullStamp}");
            sb.AppendLine($"Device Model:    {SystemInfo.deviceModel} (OS: {SystemInfo.operatingSystem})");
            sb.AppendLine($"Screen:          {Screen.width}x{Screen.height} @ {Screen.dpi:F0} dpi (Aspect: {(float)Screen.width / Mathf.Max(1, Screen.height):F2})");

            string pipelineName = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : (QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline.name : "Built-in / None");
            float renderScale = 1.0f;
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null) urpAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset != null) renderScale = urpAsset.renderScale;

            int qIdx = QualitySettings.GetQualityLevel();
            string qName = (qIdx >= 0 && qIdx < QualitySettings.names.Length) ? QualitySettings.names[qIdx] : $"Tier {qIdx}";
            sb.AppendLine($"Pipeline Asset:  {pipelineName} | Quality: {qName} (level {qIdx}) | Render Scale: {renderScale:F2}");
            sb.AppendLine($"MSAA:            URP={((urpAsset != null) ? urpAsset.msaaSampleCount : 1)}x | HDR: {((urpAsset != null && urpAsset.supportsHDR) ? "ON" : "OFF")} | Aniso: {QualitySettings.anisotropicFiltering}");
            sb.AppendLine($"Shadows:         Distance={QualitySettings.shadowDistance:F0}m | Cascades={((urpAsset != null) ? urpAsset.shadowCascadeCount : 1)} | Type={QualitySettings.shadows}");

            // 2. Active Vehicle & Rigidbody
            sb.AppendLine("\n=== [2. ACTIVE VEHICLE & RIGIDBODY] ===");
            if (activeVeh != null)
            {
                sb.AppendLine($"Vehicle:         {activeVeh.VehicleName} [{activeVeh.vehicleType}] (Obj: {activeVeh.gameObject.name})");

                // Asset Model Presence Validation
                bool hasReal3DModel = false;
                var modelSlot = activeVeh.GetComponent<VehicleModelSlot>();
                if (modelSlot != null && modelSlot.modelSlot != null && modelSlot.modelSlot.childCount > 0)
                    hasReal3DModel = true;

                if (!hasReal3DModel)
                {
                    sb.AppendLine($"⚠️ ASSET STATUS: Using Fallback Low-Poly geometry for {activeVeh.vehicleType}.");
                    sb.AppendLine($"   (Place 3D FBX into Assets/Models/Vehicles/{activeVeh.vehicleType}/ - see ASSET_INTEGRATION_GUIDE.md)");
                }
                else
                {
                    sb.AppendLine($" 3D ASSET: Real imported model active for {activeVeh.vehicleType}.");
                }

                var rb = activeVeh.rb;
                if (rb != null)
                {
                    float speedKmh = rb.linearVelocity.magnitude * 3.6f;
                    sb.AppendLine($"Speed:           {speedKmh:F1} km/h (LinVel: {rb.linearVelocity.magnitude:F2} m/s, Vec: {rb.linearVelocity:F2})");
                    sb.AppendLine($"Rigidbody State: isKinematic={rb.isKinematic}, IsSleeping={rb.IsSleeping()}, mass={rb.mass}kg, drag={rb.linearDamping:F2}");
                    sb.AppendLine($"Constraints:     {rb.constraints}");
                }
                else
                {
                    sb.AppendLine("Rigidbody:       NULL!");
                }
            }
            else
            {
                sb.AppendLine("Active Vehicle:  NULL!");
            }

            // 3. Controller Inputs & States (After Processing)
            sb.AppendLine("\n=== [3. INPUTS & MECHANISM STATES] ===");
            float th = InputManagerProxy.Throttle;
            float br = InputManagerProxy.Brake;
            float st = InputManagerProxy.Steer;
            float hb = InputManagerProxy.HandBrake;
            sb.AppendLine($"Inputs (Proxy):  Throttle={th:F2}, Brake={br:F2}, Steer={st:F2}, Handbrake={hb:F2}");
            if (activeVeh != null)
            {
                sb.AppendLine($"Engine State:    engineRunning={activeVeh.engineRunning}, isOccupied={activeVeh.isOccupied}, gear={activeVeh.currentGear}");
                if (activeVeh is TruckController tk)
                    sb.AppendLine($"Truck Specific:  tipperAngle={tk.tipperAngle:F1}°, isDumping={tk.isDumping}, cargo={tk.currentPayload:F0}kg");
                else if (activeVeh is TractorController tc)
                    sb.AppendLine($"Tractor Specific:hitchPos={tc.hitchPosition:F2}, pto={tc.ptoActive}, implement={tc.attachedImplement}");
                else if (activeVeh is JCBController jcb)
                    sb.AppendLine($"JCB Specific:    stabilizersDown={jcb.StabilizersDown}, driveInhibited={jcb.StabilizersDown}, loaderBoom={jcb.loaderBoomAngle:F1}°");
                else if (activeVeh is ExcavatorController exc)
                    sb.AppendLine($"Excavator Spec:  cabinAngle={exc.cabinAngle:F1}°, soil={exc.currentSoilVolume:P0}");
                else if (activeVeh is HarvesterController hc)
                    sb.AppendLine($"Harvester Spec:  headerLowered={hc.headerLowered}, isCutting={hc.isCutting}, augerExtended={hc.augerExtended}, grain={hc.grainLevel:F1}%");
            }

            // 4. WheelColliders State
            sb.AppendLine("\n=== [4. WHEEL COLLIDERS] ===");
            if (activeVeh != null && activeVeh.wheelColliders != null)
            {
                for (int i = 0; i < activeVeh.wheelColliders.Length; i++)
                {
                    var wc = activeVeh.wheelColliders[i];
                    if (wc == null)
                    {
                        sb.AppendLine($"Wheel[{i}]: NULL");
                        continue;
                    }
                    sb.AppendLine($"Wheel[{i} - {wc.name}]: grounded={wc.isGrounded}, motor={wc.motorTorque:F0}Nm, brake={wc.brakeTorque:F0}Nm, rpm={wc.rpm:F1}, steer={wc.steerAngle:F1}°");
                }
            }
            else
            {
                sb.AppendLine("WheelColliders: None / Null");
            }

            // 5. EventSystem & Touch Interaction State
            sb.AppendLine("\n=== [5. EVENTSYSTEM & TOUCH STATE] ===");
            var es = EventSystem.current;
            string esName = es != null ? es.name : "NULL";
            string moduleName = es != null && es.currentInputModule != null ? es.currentInputModule.GetType().Name : "NONE";
            sb.AppendLine($"EventSystem:     {esName} (Active Module: {moduleName})");
            sb.AppendLine($"Touch Pointers:  GasHeld={IsGasPointerDown}, BrakeHeld={IsBrakePointerDown}, SteerLeftHeld={IsSteerLeftPointerDown}, SteerRightHeld={IsSteerRightPointerDown}");

            // 6. Rendering Health (Error Shaders, Pink / Unsupported Detection)
            sb.AppendLine("\n=== [6. RENDERING HEALTH] ===");
            int totalRenderers = 0;
            int errorShaderCount = 0;
            var problematicObjects = new List<string>();

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            totalRenderers = renderers.Length;
            foreach (var r in renderers)
            {
                var mat = r.sharedMaterial;
                if (mat == null || mat.shader == null || !mat.shader.isSupported || mat.shader.name.Contains("Error") || mat.shader.name.Contains("InternalErrorShader"))
                {
                    errorShaderCount++;
                    if (problematicObjects.Count < 5)
                    {
                        string sName = mat == null ? "NULL_MAT" : (mat.shader == null ? "NULL_SHADER" : mat.shader.name);
                        problematicObjects.Add($"{r.gameObject.name} -> {sName}");
                    }
                }
            }

            string terrainShader = "None";
            var activeTerrain = Terrain.activeTerrain;
            if (activeTerrain != null && activeTerrain.materialTemplate != null)
            {
                terrainShader = activeTerrain.materialTemplate.shader != null ? activeTerrain.materialTemplate.shader.name : "NULL_SHADER";
                if (!activeTerrain.materialTemplate.shader.isSupported) terrainShader += " (UNSUPPORTED)";
            }

            sb.AppendLine($"Renderers:       {totalRenderers} total, {errorShaderCount} error/unsupported shaders");
            sb.AppendLine($"Terrain Shader:  {terrainShader}");
            if (problematicObjects.Count > 0)
            {
                sb.AppendLine("First Faults:    " + string.Join(" | ", problematicObjects));
            }
            else
            {
                sb.AppendLine("Shader Status:   ALL ACTIVE RENDERERS CLEAN");
            }

            // 7. Last 8 Log Errors / Exceptions
            sb.AppendLine("\n=== [7. LAST 8 ERROR / EXCEPTION LOGS] ===");
            lock (_logLock)
            {
                if (_lastErrors.Count == 0)
                {
                    sb.AppendLine("None (Console clean)");
                }
                else
                {
                    foreach (var err in _lastErrors)
                    {
                        sb.AppendLine(err);
                    }
                }
            }

            _diagnosticsText.text = sb.ToString();
        }

        private Text CreateText(GameObject parent, string name, string text, int fontSize, TextAnchor alignment, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = col;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return t;
        }

        private GameObject CreateButton(GameObject parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color col, Action onClick)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent.transform, false);
            var rect = btnGo.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var img = btnGo.AddComponent<Image>();
            img.color = col;

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRect = txtGo.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var t = txtGo.AddComponent<Text>();
            t.text = label;
            t.fontSize = 14;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return btnGo;
        }
    }
}
