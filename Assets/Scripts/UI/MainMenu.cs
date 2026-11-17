using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace HeavyVehiclesWorld.UI
{
    /// <summary>
    /// Main menu controller.  Attach to the root Canvas GameObject of the main
    /// menu scene.  Wire up all Button and Panel references in the Inspector.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Inspector References                                               //
        // ------------------------------------------------------------------ //
        [Header("Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Loading UI")]
        [SerializeField] private GameObject    loadingScreen;
        [SerializeField] private Slider        loadingBar;
        [SerializeField] private TMP_Text      loadingLabel;

        [Header("Scene Name")]
        [SerializeField] private string gameSceneName = "GameScene";

        // ------------------------------------------------------------------ //
        //  Lifecycle                                                           //
        // ------------------------------------------------------------------ //
        private void Awake()
        {
            // Guard against missing references gracefully
            if (playButton     != null) playButton.onClick.AddListener(OnPlayClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (quitButton     != null) quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void Start()
        {
            // Ensure the right panel is shown at startup
            ShowMainMenu();
        }

        // ------------------------------------------------------------------ //
        //  Button callbacks                                                    //
        // ------------------------------------------------------------------ //

        /// <summary>Loads the main game scene asynchronously.</summary>
        public void OnPlayClicked()
        {
            StartCoroutine(LoadSceneAsync(gameSceneName));
        }

        /// <summary>Toggles the settings panel.</summary>
        public void OnSettingsClicked()
        {
            bool settingsVisible = settingsPanel != null && settingsPanel.activeSelf;

            if (settingsVisible)
                ShowMainMenu();
            else
                ShowSettings();
        }

        /// <summary>Quits the application (no-op in the Editor).</summary>
        public void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ //
        //  Scene loading coroutine                                             //
        // ------------------------------------------------------------------ //

        private IEnumerator LoadSceneAsync(string sceneName)
        {
            // Show the loading screen
            if (mainMenuPanel  != null) mainMenuPanel.SetActive(false);
            if (settingsPanel  != null) settingsPanel.SetActive(false);
            if (loadingScreen  != null) loadingScreen.SetActive(true);

            if (loadingBar  != null) loadingBar.value = 0f;
            if (loadingLabel != null) loadingLabel.text = "Loading…";

            // Begin async load
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[MainMenu] Scene '{sceneName}' not found in Build Settings.");
                yield break;
            }

            // Don't auto-activate – we control the moment the scene switches
            op.allowSceneActivation = false;

            while (!op.isDone)
            {
                // Progress goes 0 → 0.9 during load, then jumps to 1 when activated
                float progress = Mathf.Clamp01(op.progress / 0.9f);

                if (loadingBar  != null) loadingBar.value      = progress;
                if (loadingLabel != null) loadingLabel.text    = $"Loading… {Mathf.RoundToInt(progress * 100)}%";

                // When load is 90 % complete Unity is waiting for activation
                if (op.progress >= 0.9f)
                {
                    if (loadingBar  != null) loadingBar.value  = 1f;
                    if (loadingLabel != null) loadingLabel.text = "Press any key to continue…";

                    // Auto-activate after a brief display
                    yield return new WaitForSeconds(0.5f);
                    op.allowSceneActivation = true;
                }

                yield return null;
            }
        }

        // ------------------------------------------------------------------ //
        //  Panel helpers                                                       //
        // ------------------------------------------------------------------ //

        private void ShowMainMenu()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        private void ShowSettings()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }
    }
}
