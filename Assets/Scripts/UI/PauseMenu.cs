using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using HeavyVehiclesWorld.Systems;

namespace HeavyVehiclesWorld.UI
{
    /// <summary>
    /// Pause menu with smooth fade-in/out.  
    /// Wire the five buttons in the Inspector.  
    /// Requires a <see cref="CanvasGroup"/> on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PauseMenu : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Settings Panel")]
        [SerializeField] private GameObject settingsPanel;

        [Header("Fade")]
        [SerializeField] private float fadeDuration = 0.25f;

        [Header("Main Menu Scene")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        // ── State ─────────────────────────────────────────────────────────────
        private CanvasGroup  _canvasGroup;
        private bool         _isVisible;
        private Coroutine    _fadeCoroutine;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            SetCanvasState(visible: false, instant: true);

            if (resumeButton   != null) resumeButton  .onClick.AddListener(OnResume);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            if (saveButton     != null) saveButton    .onClick.AddListener(OnSave);
            if (loadButton     != null) loadButton    .onClick.AddListener(OnLoad);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);

            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        private void Update()
        {
            // Allow Escape or Back button to toggle pause
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_isVisible) OnResume();
                else            ShowPause();
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void ShowPause()
        {
            if (_isVisible) return;
            _isVisible = true;
            GameManager.Instance?.PauseGame();
            FadeTo(1f);
        }

        public void HidePause()
        {
            if (!_isVisible) return;
            _isVisible = false;
            GameManager.Instance?.ResumeGame();
            FadeTo(0f);
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private void OnResume() => HidePause();

        private void OnSettings()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(!settingsPanel.activeSelf);
        }

        private void OnSave()
        {
            if (FullSaveLoadManager.Instance != null)
                FullSaveLoadManager.Instance.SaveAll();
            else
                SaveSystem.SaveGame(new SaveData());
            Debug.Log("[PauseMenu] Game saved.");
        }

        private void OnLoad()
        {
            if (FullSaveLoadManager.Instance != null)
                FullSaveLoadManager.Instance.LoadAll();
            else
                SaveSystem.LoadGame();
            HidePause();
        }

        private void OnMainMenu()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
        }

        // ── Fade coroutine ────────────────────────────────────────────────────

        private void FadeTo(float targetAlpha)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha));
        }

        private IEnumerator FadeRoutine(float targetAlpha)
        {
            // Make sure the panel is visible during the fade
            _canvasGroup.gameObject.SetActive(true);
            _canvasGroup.blocksRaycasts = true;

            float startAlpha = _canvasGroup.alpha;
            float elapsed    = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed          += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;

            if (Mathf.Approximately(targetAlpha, 0f))
            {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.gameObject.SetActive(false);
            }
        }

        private void SetCanvasState(bool visible, bool instant)
        {
            _canvasGroup.alpha          = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.gameObject.SetActive(visible);
            if (!instant && visible) FadeTo(1f);
        }
    }
}
