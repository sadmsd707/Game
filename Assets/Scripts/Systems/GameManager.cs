using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.Systems
{
    public enum GameMode
    {
        FreeRoam,
        Mission
    }

    /// <summary>
    /// Central singleton that manages game state: active vehicle, game mode,
    /// day time, fuel, damage, and broadcasts state-change events.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ── State ─────────────────────────────────────────────────────────
        [Header("Game State")]
        public GameMode  CurrentGameMode { get; private set; } = GameMode.FreeRoam;
        public VehicleController CurrentVehicle { get; private set; }
        public PlayerController  PlayerController { get; private set; }

        [Header("Time")]
        [Range(0f, 24f)] public float dayTime   = 6f;
        public float TimeScale = 1f;

        // ── Registered vehicles ───────────────────────────────────────────
        private readonly List<VehicleController> _vehicles = new();

        // ── Events ────────────────────────────────────────────────────────
        [Header("Events")]
        public UnityEvent<VehicleController> OnVehicleChanged;
        public UnityEvent<GameMode>          OnGameModeChanged;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            PlayerController = FindObjectOfType<PlayerController>();
        }

        private void Update()
        {
            // Advance in-game clock
            dayTime += (Time.deltaTime / 86400f) * 24f * TimeScale * (86400f / 600f);
            if (dayTime >= 24f) dayTime -= 24f;
        }

        // ── Vehicle management ────────────────────────────────────────────
        public void RegisterVehicle(VehicleController v)
        {
            if (!_vehicles.Contains(v))
                _vehicles.Add(v);
        }

        public void SwitchVehicle(VehicleController v)
        {
            CurrentVehicle = v;
            OnVehicleChanged?.Invoke(v);
        }

        // ── Game mode ─────────────────────────────────────────────────────
        public void SetGameMode(GameMode m)
        {
            CurrentGameMode = m;
            OnGameModeChanged?.Invoke(m);
        }

        // ── Pause / resume ────────────────────────────────────────────────
        public void PauseGame()
        {
            Time.timeScale = 0f;
        }

        public void ResumeGame()
        {
            Time.timeScale = TimeScale;
        }

        // ── Fuel & damage ─────────────────────────────────────────────────
        /// <summary>delta > 0 refuels; delta < 0 consumes fuel on the current vehicle.</summary>
        public void UpdateFuel(float delta)
        {
            if (CurrentVehicle == null) return;
            CurrentVehicle.fuelLevel = Mathf.Clamp(CurrentVehicle.fuelLevel + delta, 0f, 100f);

            if (CurrentVehicle.fuelLevel <= 0f && CurrentVehicle.engineRunning)
                CurrentVehicle.ToggleEngine();
        }

        public void ApplyDamage(VehicleController v, float amount)
        {
            if (v == null) return;
            v.damageLevel = Mathf.Clamp(v.damageLevel + amount, 0f, 100f);
        }
    }
}
