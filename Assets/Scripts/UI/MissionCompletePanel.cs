using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HeavyVehiclesWorld.UI
{
    /// <summary>
    /// MissionCompletePanel – animated full-screen overlay shown when a mission finishes.
    /// Shows mission name, reward coins with count-up animation, and a star rating.
    /// Wire MissionManager.OnMissionCompleted → ShowMission() in Inspector.
    /// </summary>
    public class MissionCompletePanel : MonoBehaviour
    {
        [Header("Root Panel")]
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private RectTransform panelRect;

        [Header("Text Labels")]
        [SerializeField] private TextMeshProUGUI missionNameLabel;
        [SerializeField] private TextMeshProUGUI rewardLabel;        // "🪙 +500"
        [SerializeField] private TextMeshProUGUI subTextLabel;       // "MISSION COMPLETE"

        [Header("Stars (3 star rating)")]
        [SerializeField] private Image star1;
        [SerializeField] private Image star2;
        [SerializeField] private Image star3;
        [SerializeField] private Sprite starFilled;
        [SerializeField] private Sprite starEmpty;

        [Header("Buttons")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Animation")]
        [SerializeField] private float fadeInDuration  = 0.4f;
        [SerializeField] private float rewardCountDuration = 1.2f;
        [SerializeField] private float autoHideDelay   = 6f;         // 0 = never auto-hide

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip completeSFX;
        [SerializeField] private AudioClip starSFX;

        // ── State ──────────────────────────────────────────────────────────────────
        private Coroutine _showCoroutine;
        private bool      _visible;

        // ── Unity lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (panelGroup != null)
            {
                panelGroup.alpha          = 0f;
                panelGroup.interactable   = false;
                panelGroup.blocksRaycasts = false;
            }

            if (continueButton != null) continueButton.onClick.AddListener(Hide);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoMainMenu);
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>Call this from MissionManager.OnMissionCompleted event.</summary>
        public void ShowMission(HeavyVehiclesWorld.Systems.MissionData data)
        {
            if (data == null) return;

            int stars = CalculateStars(data);
            ShowPanel(data.description, data.reward, stars);
        }

        /// <summary>Show the panel with explicit values (for testing).</summary>
        public void ShowPanel(string missionName, int coins, int stars)
        {
            if (_showCoroutine != null) StopCoroutine(_showCoroutine);
            _showCoroutine = StartCoroutine(AnimateShow(missionName, coins, stars));
        }

        public void Hide()
        {
            if (_showCoroutine != null) StopCoroutine(_showCoroutine);
            _showCoroutine = StartCoroutine(FadeOut());
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private IEnumerator AnimateShow(string missionName, int coins, int stars)
        {
            _visible = true;

            // Set text before fade
            if (missionNameLabel != null) missionNameLabel.text = missionName;
            if (subTextLabel     != null) subTextLabel.text     = "MISSION COMPLETE!";
            if (rewardLabel      != null) rewardLabel.text      = "+0";

            // Reset stars
            SetStar(star1, false);
            SetStar(star2, false);
            SetStar(star3, false);

            // Slide in + fade in
            if (panelRect != null)
                panelRect.localScale = Vector3.one * 0.7f;

            yield return StartCoroutine(FadeIn());

            // Scale punch
            if (panelRect != null)
                yield return StartCoroutine(ScalePunch(panelRect, 0.7f, 1f, 0.3f));

            // Play complete SFX
            PlaySFX(completeSFX);

            // Count-up coins
            yield return StartCoroutine(CountUpCoins(coins));

            // Reveal stars one by one
            yield return new WaitForSeconds(0.2f);
            if (stars >= 1) { SetStar(star1, true); PlaySFX(starSFX); yield return new WaitForSeconds(0.25f); }
            if (stars >= 2) { SetStar(star2, true); PlaySFX(starSFX); yield return new WaitForSeconds(0.25f); }
            if (stars >= 3) { SetStar(star3, true); PlaySFX(starSFX); }

            // Auto-hide
            if (autoHideDelay > 0f)
            {
                yield return new WaitForSeconds(autoHideDelay);
                yield return StartCoroutine(FadeOut());
            }
        }

        private IEnumerator FadeIn()
        {
            if (panelGroup == null) yield break;
            panelGroup.interactable   = true;
            panelGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                panelGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }
            panelGroup.alpha = 1f;
        }

        private IEnumerator FadeOut()
        {
            if (panelGroup == null) yield break;
            float start   = panelGroup.alpha;
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                panelGroup.alpha = Mathf.Lerp(start, 0f, elapsed / fadeInDuration);
                yield return null;
            }
            panelGroup.alpha          = 0f;
            panelGroup.interactable   = false;
            panelGroup.blocksRaycasts = false;
            _visible = false;
        }

        private IEnumerator CountUpCoins(int target)
        {
            if (rewardLabel == null) yield break;
            float elapsed = 0f;
            while (elapsed < rewardCountDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                int displayed = Mathf.RoundToInt(Mathf.Lerp(0f, target, elapsed / rewardCountDuration));
                rewardLabel.text = $"🪙 +{displayed}";
                yield return null;
            }
            rewardLabel.text = $"🪙 +{target}";
        }

        private IEnumerator ScalePunch(RectTransform rt, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float scale = Mathf.Lerp(from, to, t < 0.5f ? t * 2f : 1f - (t - 0.5f) * 0.3f);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private void SetStar(Image img, bool filled)
        {
            if (img == null) return;
            img.sprite = filled ? starFilled : starEmpty;
            img.transform.localScale = filled ? Vector3.one * 1.2f : Vector3.one;
        }

        private void PlaySFX(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        private static int CalculateStars(HeavyVehiclesWorld.Systems.MissionData data)
        {
            // Simple rule: always 3 stars on first completion
            // Can be extended with time-based rating
            return data.isComplete ? 3 : 1;
        }

        private void GoMainMenu()
        {
            Hide();
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }
}
