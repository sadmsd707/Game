# Phase 6 Setup Guide — Graphics Polish, Optimization, Missions & Save System

## Overview
Phase 6 finalises the game experience:
- URP post-processing stack fully tuned per quality tier
- Vehicle LOD groups (reduce draw calls at distance)
- Particle system pooling (no runtime instantiation spikes)
- Mission completion reward UI with animation
- Full save/load integration across all systems
- Performance monitor for mobile QA
- Currency system for mission rewards

---

## Step 1 — URP Post-Processing Stack

### A. Create URP Renderer Data
1. In `Assets/Settings/`, create four **Universal Render Pipeline Asset** files:
   - `URP_Low.asset`, `URP_Medium.asset`, `URP_High.asset`, `URP_Ultra.asset`
2. Configure each:
   | Setting | Low | Medium | High | Ultra |
   |---------|-----|--------|------|-------|
   | Shadows | Off | Hard | Soft | Soft |
   | Shadow Distance | 0 | 80m | 150m | 250m |
   | SSAO | Off | Off | On | On |
   | Bloom | Off | On | On | On |
   | Motion Blur | Off | Off | Off | On |
   | DOF | Off | Off | On | On |

### B. Post-Processing Volume
1. Create a **Global Volume** in the scene (GameObject → Volume → Global Volume)
2. Create a **Volume Profile** (`Assets/Settings/GlobalPostProcess.asset`)
3. Add overrides: **Bloom**, **Depth Of Field**, **Motion Blur**, **Color Adjustments**, **Vignette**
4. Assign to `GraphicsSettingsManager.globalPostProcessVolume`
5. Assign the four URP assets to `GraphicsSettingsManager.urpLow/Medium/High/Ultra`

### C. Color Grading Tips
- **Farmland**: warm, golden (+10 temperature)
- **Quarry**: cool, desaturated (-15 saturation)
- Use post-exposure `+0.3` for night scenes

---

## Step 2 — Vehicle LOD Groups

> [!TIP]
> Use primitive placeholder meshes for LOD1/LOD2 until real high-poly models are imported.

For each of the 6 vehicle prefabs:
1. Add `VehicleLODSetup` component to the vehicle root
2. Assign renderers:
   - **LOD0**: All full-detail renderers (all MeshRenderer children)
   - **LOD1**: Duplicate the GO, reduce mesh (or use a simplified version), assign those renderers
   - **LOD2**: A single low-poly cube/capsule approximating the silhouette
3. Right-click the component → **Auto Configure LOD Group**
4. Verify the LODGroup component appears with 3 LOD bands

> [!NOTE]
> Recommended thresholds: LOD0 ≥15%, LOD1 ≥5%, LOD2 ≥1%, culled below 1%

---

## Step 3 — Particle Pool Manager

1. Create an empty GameObject named `ParticlePoolManager`
2. Add `ParticlePoolManager` component
3. Create particle prefabs (or reuse existing ones from VehicleEffects):
   - `Dust` — wheel dust cloud
   - `ExhaustSmoke` — engine exhaust
   - `MudSplash` — mud particles
   - `DirtSpray` — JCB/Excavator digging
   - `GrainDust` — Harvester unload
4. Configure each entry in `ParticlePoolManager.entries[]`:
   - `poolId`: string key (must match what code passes to `Play()`)
   - `prefab`: particle system prefab
   - `preWarmCount`: 5 (pre-create 5 at startup)
   - `maxCount`: 20 (never exceed this)
5. Update `VehicleEffects.cs` to call `ParticlePoolManager.Instance.Play("Dust", pos)` instead of `Instantiate()`

---

## Step 4 — Mission Complete Panel

1. Create a full-screen Canvas overlay (Sort Order: 100)
2. Build the layout:
   ```
   Canvas (overlay, sort 100)
   └── MissionCompletePanel (full screen, CanvasGroup)
       ├── Background (dark semi-transparent panel)
       ├── SubText ("MISSION COMPLETE!" label)
       ├── MissionNameLabel (mission description)
       ├── RewardLabel ("🪙 +500")
       ├── Stars (HorizontalLayoutGroup)
       │   ├── Star1 (Image)
       │   ├── Star2 (Image)
       │   └── Star3 (Image)
       ├── ContinueButton
       └── MainMenuButton
   ```
3. Add `MissionCompletePanel` component, assign all references
4. Subscribe in `MissionManager.OnMissionCompleted`:
   - In Inspector: add `MissionCompletePanel.ShowMission` to the event

---

## Step 5 — Performance Monitor

1. Create a small corner Canvas (top-left, Sort Order: 200)
2. Add a vertical layout with 4 TextMeshPro labels:
   - FPS label (with colored text)
   - Memory label
   - Draw calls label
   - Quality label
3. Add a thin Image (width fill) for the FPS bar
4. Add `PerformanceMonitor` component
5. Set `showOnStart = false` (toggle with **P key** during testing)
6. For final release build: disable the whole GameObject

---

## Step 6 — Currency Manager

1. Create an empty persistent GameObject named `CurrencyManager`
2. Add `CurrencyManager` component
3. It auto-persists coins via PlayerPrefs
4. The existing `MissionManager.CompleteMission()` already calls `GameManager.Instance?.AddCurrency(reward)`, which now delegates to `CurrencyManager.Instance.AddCoins()`
5. Display current coins in `VehicleHUD` by subscribing to `CurrencyManager.OnCoinsChanged`

---

## Step 7 — Full Save / Load Setup

1. Create an empty persistent GameObject named `FullSaveLoadManager`
2. Add `FullSaveLoadManager` component
3. Assign:
   - `playerTransform` → Player GameObject transform
   - `allVehicles[]` → drag all 6 vehicle root GameObjects
   - `dayNight` → DayNightCycle GameObject
4. Set `autoSaveEnabled = true`, `autoSaveInterval = 300` (5 minutes)
5. Wire PauseMenu buttons:
   - **Save** button → `FullSaveLoadManager.Instance.SaveAll("slot1")`
   - **Load** button → `FullSaveLoadManager.Instance.LoadAll("slot1")`
6. On **Main Menu** → New Game: call `FullSaveLoadManager.Instance.DeleteSave("slot1")`
7. On scene load: check `FullSaveLoadManager.Instance.HasSave("slot1")` and auto-load if true

### Save file locations (Android):
```
/data/user/0/com.heavyvehicles.world/files/saves/slot1.json
/data/user/0/com.heavyvehicles.world/files/saves/world_state.json
```

---

## Step 8 — Occlusion Culling (Editor Bake)

> [!IMPORTANT]
> Occlusion culling must be baked in the Unity Editor — it cannot be generated at runtime.

1. Go to **Window → Rendering → Occlusion Culling**
2. Click the **Object** tab — mark large static world objects as **Occluder Static** and **Occludee Static**:
   - Terrain chunks, buildings, walls, rocks → Occluder + Occludee
   - Vehicles, characters → Occludee only (they move)
3. Click **Bake** (may take 5–15 minutes for a 4km map)
4. Set **Smallest Occluder** = 5m, **Smallest Hole** = 0.25m for outdoor mobile use
5. Verify with the camera gizmo — objects behind buildings should disappear in the Scene view

> [!TIP]
> For mobile: combine Occlusion Culling with `Camera.farClipPlane = 1500f` and LOD groups for maximum draw call reduction.

---

## Step 9 — GPU Instancing

Enable GPU instancing on all shared materials:
1. Select a material in the Project view
2. In the Inspector, check **Enable GPU Instancing**
3. Priority materials to instance:
   - Terrain grass/detail meshes
   - Trees (if using SpeedTree or prefab trees)
   - Road markings, rocks, props

---

## Step 10 — Texture Compression (ASTC for Android)

1. Select all texture assets in `Assets/Textures/`
2. In Texture Import Settings → **Android** platform override:
   - Set **Format** = `ASTC 6x6` (good balance for mid-range phones)
   - For UI/icons: `ASTC 4x4`
   - For normal maps: `ASTC 6x6` with **Normal Map** type
3. Max size per texture: 2048×2048
4. Enable **Generate Mipmaps** for all world textures

---

## Phase 6 Test Checklist

- [ ] Post-processing volume: bloom visible on headlights at night
- [ ] Motion blur only active on Ultra quality
- [ ] DOF only active on High and Ultra quality
- [ ] Setting Low quality disables shadows and post-processing
- [ ] Vehicle LOD groups switch mesh at correct distances
- [ ] `ParticlePoolManager.Play("Dust", pos)` works without Instantiate
- [ ] No particle GC spikes in Profiler during heavy digging/driving
- [ ] MissionCompletePanel shows with coin count-up animation
- [ ] Star animation plays one star at a time with sound
- [ ] PerformanceMonitor toggles with P key
- [ ] FPS bar turns red below 30 FPS
- [ ] Save game: drive to new location → save → reload → player at saved position
- [ ] Vehicle fuel persists across save/load
- [ ] Completed missions remain completed after save/load
- [ ] Day time restores correctly after load
- [ ] CurrencyManager: completing a mission adds coins, persists after restart
- [ ] `FullSaveLoadManager.HasSave("slot1")` returns true after first save

---

## Performance Targets (mid-range Android — Snapdragon 720G class)

| Quality | Target FPS | Shadow Distance | Bloom |
|---------|-----------|----------------|-------|
| Low     | 60 FPS    | Off            | Off   |
| Medium  | 45–60 FPS | 80m            | On    |
| High    | 30–45 FPS | 150m           | On    |
| Ultra   | 30 FPS    | 250m           | On    |

> [!WARNING]
> Test on a **real device** — Unity Editor performance does not reflect mobile performance. The 4km terrain with 8×8 chunk streaming is the biggest memory bottleneck; profile with Android Logcat and Unity Memory Profiler.

---

## Files Added / Modified in Phase 6

| File | Location | Purpose |
|------|----------|---------|
| `MissionCompletePanel.cs` | `Scripts/UI/` | Animated reward UI with coin count-up + stars |
| `PerformanceMonitor.cs` | `Scripts/UI/` | In-game FPS/memory/draw call HUD |
| `ParticlePoolManager.cs` | `Scripts/Effects/` | Object pool for particle systems |
| `VehicleLODSetup.cs` | `Scripts/Vehicles/` | Editor tool to auto-configure LOD groups |
| `FullSaveLoadManager.cs` | `Scripts/Systems/` | Unified save/load for all game systems |
| `CurrencyManager.cs` | `Scripts/Systems/` | Coin/currency singleton with PlayerPrefs |
| `GameManager.cs` *(modified)* | `Scripts/Systems/` | Added `AddCurrency()` → delegates to CurrencyManager |
