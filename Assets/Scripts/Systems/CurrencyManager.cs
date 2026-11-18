using UnityEngine;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// CurrencyManager – tracks the player's in-game coins / currency.
    /// Singleton; persists across scenes.
    /// GameManager.AddCurrency() delegates here.
    /// </summary>
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }

        private const string PrefsKey = "PlayerCoins";

        public int CurrentCoins { get; private set; }

        // ── Events ─────────────────────────────────────────────────────────────────
        public System.Action<int> OnCoinsChanged;   // passes new total

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CurrentCoins = PlayerPrefs.GetInt(PrefsKey, 0);
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        public void AddCoins(int amount)
        {
            if (amount == 0) return;
            CurrentCoins = Mathf.Max(0, CurrentCoins + amount);
            PlayerPrefs.SetInt(PrefsKey, CurrentCoins);
            PlayerPrefs.Save();
            OnCoinsChanged?.Invoke(CurrentCoins);
            Debug.Log($"[CurrencyManager] +{amount} coins. Total: {CurrentCoins}");
        }

        public bool SpendCoins(int amount)
        {
            if (CurrentCoins < amount)
            {
                Debug.Log($"[CurrencyManager] Not enough coins ({CurrentCoins}/{amount}).");
                return false;
            }
            AddCoins(-amount);
            return true;
        }

        public void ResetCoins()
        {
            CurrentCoins = 0;
            PlayerPrefs.SetInt(PrefsKey, 0);
            PlayerPrefs.Save();
            OnCoinsChanged?.Invoke(0);
        }
    }
}
