# Phase 5 Setup Guide — Open World Map & Environment

## Overview
Phase 5 adds the 4km×4km open world: 7 terrain zones, minimap, day/night cycle, dynamic weather, ambient audio, and world state persistence.

---

## Step 1 — Create the Unity Terrain (4km × 4km)

1. In Unity, go to **GameObject → 3D Object → Terrain**
2. In the **Terrain** Inspector → **Terrain Settings**:
   - Set **Terrain Width** = 4000, **Terrain Length** = 4000, **Terrain Height** = 500
   - Set **Heightmap Resolution** = 2049
   - Set **Detail Resolution** = 1024
3. Position terrain at world origin (0, 0, 0)
4. Rename it `MainTerrain`

> [!TIP]
> For streaming on mobile, use `TerrainChunkLoader.cs` (already in `Assets/Scripts/World/`) to split the world into 8×8 chunks of 500m and load/unload based on player distance. Set `loadRadius` = 1000m in the Inspector.

---

## Step 2 — Sculpt the 7 Zone Areas

Use Unity's Terrain Painting tools (Raise/Lower Height) to create the following zones:

| Zone | World Rect (X, Z, W, H) | Features |
|------|------------------------|----------|
| Farmland | (0, 0, 1500, 1500) | Flat fields, crop rows, grain silos |
| Village | (1500, 0, 1000, 800) | Gentle hills, roads, buildings |
| Forest | (2500, 0, 1500, 1500) | Dense trees, uneven ground |
| Highway | (0, 1500, 4000, 400) | Flat, straight road corridor |
| ConstructionSite | (0, 1900, 1200, 1000) | Excavated pits, ramps, scaffolding |
| Quarry | (1200, 1900, 1300, 1100) | Deep pits, rock faces, steep slopes |
| River+Bridge | (2500, 1500, 1500, 2500) | River channel, single bridge at Z=2000 |

---

## Step 3 — Terrain Texture Painting

1. Add at least 4 terrain layers: **Grass**, **Dirt**, **Rock**, **Asphalt**
2. Use the Paint Texture tool to:
   - Paint **Grass** everywhere as the base
   - Paint **Dirt** in farmland crop rows and construction site
   - Paint **Rock** in quarry zone
   - Paint **Asphalt** along the highway corridor
3. Enable **ASTC** compression on all terrain textures (Android → Texture Import Settings)

---

## Step 4 — Road Network Generator

1. Create a **WorldMapData** asset: **Assets → Create → HeavyVehiclesWorld → WorldMapData**
2. In the Inspector, set up 7 ZoneDefinitions (copy the rects from the table above)
3. Add road waypoints connecting zones
4. Attach `RoadNetworkGenerator` to any editor GameObject
5. Assign your `WorldMapData` asset and the **MainTerrain**
6. Click **[Generate Roads]** in the Inspector — this paints the Asphalt splatmap layer along road waypoints

---

## Step 5 — ZoneManager Setup

1. Create an empty GameObject named `ZoneManager`
2. Add the `ZoneManager` component
3. Assign your `WorldMapData` asset to the `worldMapData` field
4. The ZoneManager auto-detects which zone the player is in each frame based on XZ position within zone Rect bounds
5. Access `ZoneManager.CurrentZone` from any script to get the current `WorldZone` enum value

---

## Step 6 — Day/Night Cycle

1. Create an empty GameObject named `DayNightController`
2. Add `DayNightCycle` component
3. Assign the **Directional Light** (Sun) to the `sun` field
4. Set sky gradient colors:
   - Dawn (time ≈ 6): orange/pink
   - Day (time ≈ 12): blue sky
   - Dusk (time ≈ 20): orange/red
   - Night (time ≈ 0): dark blue/black
5. Set `dayDurationSeconds` = 600 (10 minutes = 1 full game day)
6. The public property `TimeOfDay` (0–24) is accessible from MinimapUI and HUDWeatherIndicator

---

## Step 7 — Weather System

1. Create an empty GameObject named `WeatherSystem`
2. Add `WeatherSystem` component
3. Create a Particle System for **Rain**:
   - Shape: Box, 50×1×50
   - Start velocity: (0, -15, 0)
   - Emission: 500/sec
   - Assign to `rainParticles` field
4. Add a **Volume** component (URP post-processing) for fog and assign it to `fogVolume`
5. Set `randomWeatherTimer` = 10 (minutes) for automatic weather cycles
6. The event `OnWeatherChanged` fires whenever weather transitions complete

---

## Step 8 — Weather Audio Controller

1. Add `WeatherAudioController` to the same GameObject as `WeatherSystem` (or any persistent GO)
2. Create 3 AudioSource components and assign to:
   - `rainSource` — looping rain sound
   - `windSource` — looping wind sound
   - `thunderSource` — one-shot thunder
3. Assign audio clips:
   - `lightRainClip`, `heavyRainClip`
   - `lightWindClip`, `stormWindClip`
   - `thunderClips[]` — array of thunder boom sounds
4. The controller subscribes to `WeatherSystem.OnWeatherChanged` and crossfades volumes automatically

---

## Step 9 — Ambient Audio (ZoneAmbience)

1. Create an empty GameObject named `ZoneAmbience`
2. Add `ZoneAmbience` component (auto-adds AudioSource)
3. For each WorldZone, assign an ambient loop clip in the `zoneClips` array:
   - Farmland → birds + light wind
   - Village → distant traffic + birds
   - Forest → dense bird/insect ambience
   - Highway → wind + distant traffic
   - ConstructionSite → machinery hum
   - Quarry → rock echo + distant rumble
   - River → water flowing
4. Crossfade happens automatically when ZoneManager.CurrentZone changes

---

## Step 10 — Bridge Setup

1. Model a simple bridge spanning the river (use cube primitives as placeholder)
2. Add `BridgeController` component to the bridge root GameObject
3. Set `maxLoadTons` = 20
4. Add a **BoxCollider** (IsTrigger = true) above the bridge deck, assign to `loadZone`
5. Subscribe to `OnBridgeCollapse` UnityEvent to trigger debris particles/sound

---

## Step 11 — Minimap Setup

### A. Minimap Camera
1. Create a Camera named `MinimapCamera`
2. Set to **Orthographic**, size = 150
3. Position high above terrain center, rotation = (90, 0, 0)
4. Set **Culling Mask** to exclude UI layer
5. Create a **RenderTexture** (512×512, R8G8B8A8): `MinimapRT`
6. Assign `MinimapRT` as the camera's **Target Texture**

### B. Minimap UI
1. Create a Canvas overlay in the top-right corner
2. Add a **RawImage** (128×128), assign `MinimapRT` as its texture
3. Add a child **RawImage** for the full-map panel (screen-sized, initially hidden)
4. Add a small **Image** child for the player arrow marker
5. Add `MinimapUI` component and assign all references:
   - `minimapCamera`: MinimapCamera
   - `minimapImage`: corner RawImage
   - `fullMapImage`: fullscreen RawImage
   - `playerMarker`: arrow RectTransform
   - `zoneLabel`, `gameTimeLabel`: TextMeshPro labels
   - `toggleButton`, `zoomInButton`, `zoomOutButton`: UI Buttons

> [!NOTE]
> The MinimapCamera follows the player automatically via `SetTrackedTransform()`. Call `MinimapUI.SetTrackedTransform(playerTransform)` from your SceneBootstrapper after the player spawns.

---

## Step 12 — HUD Weather Indicator

1. Add a small panel to the HUD Canvas (top-left)
2. Add `HUDWeatherIndicator` component
3. Create/import 4 weather sprites (or use colored squares as placeholders):
   - Sun icon (clear), Raindrop (rain), Cloud (fog), Lightning bolt (storm)
4. Create sun and moon sprites
5. Assign all sprite references in the Inspector
6. Set `sunriseHour` = 6 and `sunsetHour` = 20

---

## Step 13 — Vehicle Spawn Points

1. For each of the 6 vehicles, create an empty GameObject at the desired spawn location
2. Add `VehicleSpawnPointMarker` component, set `vehicleType` to match the vehicle
3. These appear as colored gizmo spheres in Scene view:
   - Car → Blue, Truck → Red, Tractor → Green, JCB → Yellow, Excavator → Magenta, Harvester → Cyan
4. Assign spawn points to `VehicleSpawner` component's spawn point list

---

## Step 14 — WorldStateManager

1. Create an empty persistent GameObject named `WorldStateManager`
2. Add `WorldStateManager` component (auto-singletons with DontDestroyOnLoad)
3. It auto-saves world state to `persistent_data_path/saves/world_state.json` on Application.Quit
4. Load world state on game start:
   ```csharp
   string json = SaveSystem.LoadWorldState();
   if (json != null) WorldStateManager.Instance.DeserializeFromJson(json);
   ```

---

## Step 15 — WorldEnvironmentController

1. Create a GameObject named `WorldEnvironmentController`
2. Add `WorldEnvironmentController` component
3. Assign `DayNightCycle`, `WeatherSystem`, `ZoneManager` references in the Inspector
4. This controller handles zone-based fog/ambient transitions and schedules random weather changes

---

## Phase 5 Test Checklist

- [ ] Terrain is 4000×4000 and viewable from above
- [ ] 7 zone textures are distinct and visible
- [ ] Walking from Farmland to Village changes `ZoneManager.CurrentZone`
- [ ] `MinimapUI` shows player position on the minimap corner panel
- [ ] Clicking the toggle button expands minimap to fullscreen
- [ ] Zoom In/Out buttons change minimap camera orthographic size
- [ ] Zone label updates when crossing zone boundaries
- [ ] Time label shows HH:MM and advances correctly
- [ ] DayNightCycle rotates sun and changes sky color over 10 minutes
- [ ] WeatherSystem transitions from Clear → Rain → back to Clear
- [ ] Rain particles appear when weather = Rain
- [ ] Rain audio fades in; wind audio fades in
- [ ] Storm triggers thunder sound at random intervals
- [ ] ZoneAmbience crossfades when moving between zones
- [ ] HUD weather icon updates on weather change
- [ ] HUD shows sun icon during day, moon icon at night
- [ ] Bridge triggers OnBridgeCollapse when >20t vehicle drives over
- [ ] WorldStateManager.IsMissionComplete returns correct state
- [ ] WorldStateManager serializes/deserializes from JSON correctly
- [ ] VehicleSpawnPointMarker gizmos visible per vehicle type in Scene view
- [ ] TerrainChunkLoader loads/unloads terrain chunks as player moves

---

## Performance Tips (Mobile)

> [!TIP]
> - Set **Shadow Distance** to 100m in URP settings for mobile
> - Use **Terrain LOD** (Terrain Settings → LOD Bias) to reduce draw calls at distance
> - Minimap RenderTexture resolution: 256×256 is sufficient for mobile (512 for High/Ultra quality)
> - Weather particles: cap rain emission at 300/sec on Low quality, 600 on High
> - ZoneAmbience: use compressed OGG audio clips, not uncompressed WAV

> [!WARNING]
> The 4km×4km terrain at heightmap resolution 2049 uses ~16MB of RAM. If you see out-of-memory crashes on mid-range phones, reduce to 1025 resolution and use TerrainChunkLoader aggressively with a 1000m load radius.

---

## Files Added in Phase 5

| File | Location | Purpose |
|------|----------|---------|
| `WorldMapData.cs` | `Scripts/World/` | Zone/road/mission ScriptableObject |
| `WorldEnvironmentController.cs` | `Scripts/World/` | Env system glue |
| `BridgeController.cs` | `Scripts/World/` | Load-based bridge collapse |
| `RoadNetworkGenerator.cs` | `Scripts/World/` | Editor road painting tool |
| `VehicleSpawnPointMarker.cs` | `Scripts/World/` | Spawn point gizmos |
| `MinimapUI.cs` | `Scripts/UI/` | Minimap corner↔fullscreen |
| `ZoneAmbience.cs` | `Scripts/World/` | Per-zone audio crossfade |
| `WeatherAudioController.cs` | `Scripts/World/` | Rain/wind/thunder audio |
| `HUDWeatherIndicator.cs` | `Scripts/UI/` | HUD weather+time widget |
| `WorldStateManager.cs` | `Scripts/World/` | Mission/vehicle/field state |
| `DayNightCycle.cs` *(modified)* | `Scripts/World/` | Added `TimeOfDay` property |
| `WeatherSystem.cs` *(modified)* | `Scripts/World/` | Added `OnWeatherChanged` event + `CurrentWeather` |
| `SaveSystem.cs` *(modified)* | `Scripts/Systems/` | Added `SaveWorldState()` / `LoadWorldState()` |

