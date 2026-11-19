# HeavyVehiclesWorld – 3D Asset Integration & Visual Setup Guide

> **Important**: All primitive/placeholder art generation has been removed. HeavyVehiclesWorld uses the **VehicleModelSlot** and **PBR/TerrainLayer** architecture. When an asset is missing, the engine displays a clear warning in the console indicating the exact folder and slot required.

---

## 1. Directory Structure

Download and place your imported 3D models and PBR textures into the designated project folders:

```
Assets/
├── Models/
│   ├── Vehicles/
│   │   ├── Car/            <-- Car_SUV.fbx
│   │   ├── Truck/          <-- Truck_Tipper.fbx
│   │   ├── Tractor/        <-- Tractor_Agri.fbx
│   │   ├── JCB/            <-- JCB_Backhoe.fbx
│   │   ├── Excavator/      <-- Excavator_Tracked.fbx
│   │   └── Harvester/      <-- Harvester_Combine.fbx
│   └── Environment/
│       ├── Trees/          <-- Tree_Oak.fbx, Tree_Pine.fbx
│       └── Props/          <-- Barrier.fbx, Cone.fbx, Crate.fbx
├── Textures/
│   ├── Vehicles/           <-- Vehicle Albedo, Normal, Metallic/Smoothness maps
│   ├── Terrain/            <-- Grass, Dirt, Asphalt, Rock PBR textures
│   └── Skybox/             <-- Clean HDRI Skybox panorama (.hdr / .exr / .png)
└── Materials/
    └── PBR/                <-- PBR URP/Lit Material assets
```

---

## 2. Vehicle Models & Rigging Requirements

Each vehicle has an empty child GameObject named **`Model`** with a **`VehicleModelSlot`** component. Drop the imported FBX directly under `Model`.

### 2.1 Car (Sport SUV)
- **Folder**: `Assets/Models/Vehicles/Car/`
- **Recommended Free Models**:
  - Kenney Car Kit (Kenney.nl) – *SUV / Sedan*
  - Sketchfab (CC-BY): *Low Poly SUV* or *Generic Compact SUV*
- **Pivot Points**:
  - `Root/Chassis`: Pivot at ground level or center of mass `(0, 0, 0)`.
  - `Wheel_FL`, `Wheel_FR`, `Wheel_RL`, `Wheel_RR`: **Pivot must be centered exactly at the hub axis** so wheel rotation is true.
- **Wiring to Script**:
  - In `VehicleModelSlot` (and `CarController`):
    - Drag `Wheel_FL` to `frontLeftMesh`
    - Drag `Wheel_FR` to `frontRightMesh`
    - Drag `Wheel_RL` to `rearLeftMesh`
    - Drag `Wheel_RR` to `rearRightMesh`

---

### 2.2 Truck (Heavy Tipper)
- **Folder**: `Assets/Models/Vehicles/Truck/`
- **Recommended Free Models**:
  - Kenney Commercial Vehicles – *Dump Truck / Tipper*
  - Sketchfab (CC-BY): *Construction Dump Truck*
- **Pivot Points**:
  - `Chassis`: Base center.
  - `TipperBed`: **Pivot must be at the rear hinge** (rotation axis on local X) so tilting rotates up.
  - `Wheels` (6 wheels): Pivots centered at each wheel axle.
- **Wiring to Script**:
  - In `VehicleModelSlot`:
    - Drag `TipperBed` to `tipperBed` slot.
  - In `TruckController`:
    - `tipperBedTransform` = `TipperBed`
    - Max tilt angle: `45°`.

---

### 2.3 Tractor (Agricultural)
- **Folder**: `Assets/Models/Vehicles/Tractor/`
- **Recommended Free Models**:
  - Sketchfab (CC-BY): *Farm Tractor Low Poly*
  - CGTrader (Free): *Utility Farm Tractor*
- **Pivot Points**:
  - `Chassis`: Base center.
  - `RearHitchArm`: Pivot at rear lower chassis mount.
  - `FrontWheels` (small): Hub center.
  - `RearWheels` (large): Hub center.
- **Wiring to Script**:
  - In `VehicleModelSlot`:
    - Drag `RearHitchArm` to `threePointHitch` slot.
  - In `TractorController`:
    - `hitchArmTransform` = `RearHitchArm`.

---

### 2.4 JCB (Backhoe Loader)
- **Folder**: `Assets/Models/Vehicles/JCB/`
- **Recommended Free Models**:
  - Sketchfab (CC-BY): *Backhoe Loader JCB 3CX*
  - Unity Asset Store (Free): *Construction Backhoe*
- **Pivot Points**:
  - `Chassis`: Base center.
  - `FrontLoaderBoom`: Pivot at front chassis hinge.
  - `FrontLoaderBucket`: Pivot at boom tip hinge.
  - `BackhoeSwing`: Pivot on rear vertical axis (Y rotation).
  - `BackhoeBoom`: Pivot at swing hinge (X rotation).
  - `BackhoeStick`: Pivot between boom and stick (X rotation).
  - `BackhoeBucket`: Pivot between stick and bucket (X rotation).
- **Wiring to Script**:
  - In `VehicleModelSlot`:
    - `frontLoaderBoom` = `FrontLoaderBoom`
    - `frontLoaderBucket` = `FrontLoaderBucket`
    - `boomArm` = `BackhoeBoom`
    - `stickArm` = `BackhoeStick`
    - `bucket` = `BackhoeBucket`
  - In `JCBController`:
    - Wire `loaderBoom`, `loaderBucket`, `backhoeBoom`, `backhoeStick`, `backhoeBucket`.

---

### 2.5 Excavator (Tracked Digger)
- **Folder**: `Assets/Models/Vehicles/Excavator/`
- **Recommended Free Models**:
  - Sketchfab (CC-BY): *Tracked Excavator CAT / Komatsu Style*
  - CGTrader (Free): *Low Poly Crawler Excavator*
- **Pivot Points**:
  - `TrackBase`: Ground center (stationary during cabin rotation).
  - `Cabin`: **Pivot at base center on vertical axis (Y rotation)** for 360° slew.
  - `Boom`: Pivot at base of cabin arm bracket.
  - `Stick`: Pivot at joint between boom and stick.
  - `Bucket`: Pivot at tip of stick.
- **Wiring to Script**:
  - In `VehicleModelSlot`:
    - `rotatingCabin` = `Cabin`
    - `boomArm` = `Boom`
    - `stickArm` = `Stick`
    - `bucket` = `Bucket`
  - In `ExcavatorController`:
    - `cabinRoot` = `Cabin`
    - `boomArm` = `Boom`
    - `stickArm` = `Stick`
    - `bucket` = `Bucket`

---

### 2.6 Harvester (Combine)
- **Folder**: `Assets/Models/Vehicles/Harvester/`
- **Recommended Free Models**:
  - Sketchfab (CC-BY): *Combine Harvester CLAAS / John Deere Style*
  - CGTrader (Free): *Agricultural Combine Harvester*
- **Pivot Points**:
  - `CutterHeader`: Pivot at front feeder house hinge (raises/lowers).
  - `UnloadAuger`: Pivot at rear grain tank outlet (swings out 90° for grain transfer).
- **Wiring to Script**:
  - In `VehicleModelSlot`:
    - `cutterHeader` = `CutterHeader`
    - `unloadAuger` = `UnloadAuger`
  - In `HarvesterController`:
    - `cutterHeaderTransform` = `CutterHeader`
    - `unloadAugerTransform` = `UnloadAuger`

---

## 3. PBR Materials & Texture Slots

All materials use the **Universal Render Pipeline/Lit** (or Standard) PBR shader with physically-calibrated parameters:

| Material Asset | Albedo (`_BaseMap`) | Normal (`_BumpMap`) | Metallic / Smoothness | Purpose |
|---|---|---|---|---|
| `M_PBR_Car_Body.mat` | Cobalt Blue paint | Fine car panel normal | Met: 0.75, Sm: 0.85 | Car metallic coat |
| `M_PBR_Truck_Body.mat` | Commercial White | Heavy metal panel normal | Met: 0.35, Sm: 0.65 | Truck chassis & tipper |
| `M_PBR_Tractor_Body.mat` | Agricultural Green | Sheet metal normal | Met: 0.25, Sm: 0.60 | Tractor hood & cab |
| `M_PBR_JCB_Body.mat` | Industrial Yellow | Construction equipment normal | Met: 0.20, Sm: 0.55 | JCB body & loader arms |
| `M_PBR_Excavator_Body.mat` | Industrial Orange | Heavy machinery normal | Met: 0.25, Sm: 0.50 | Excavator body & boom |
| `M_PBR_Harvester_Body.mat` | Harvest Green | Grain harvester panel normal | Met: 0.30, Sm: 0.60 | Combine body & header |
| `M_PBR_Rubber_Tires.mat` | Deep dark grey | Tire tread normal map | Met: 0.02, Sm: 0.25 | All vehicle wheel treads |
| `M_PBR_MetalParts.mat` | Dark gunmetal | Scratched steel normal | Met: 0.85, Sm: 0.65 | Rims, pistons, PTO, hitch |
| `M_PBR_Glass.mat` | Tinted transparent | Smooth flat normal | Met: 0.10, Sm: 0.95 | Windshields & windows |
| `M_PBR_Asphalt_Road.mat` | Dark asphalt grain | Rough asphalt normal map | Met: 0.05, Sm: 0.20 | Driving road network |
| `M_PBR_Concrete_Curbs.mat` | Red/White curb | Concrete pore normal | Met: 0.02, Sm: 0.15 | Roadside barrier curbs |

Place textures in: `Assets/Textures/Vehicles/` and `Assets/Textures/Terrain/`.

---

## 4. Terrain System & TerrainLayers

The game terrain uses Unity's multi-layer **TerrainLayer** system with 4 distinct PBR splat layers:

1. **`Layer_Grass.terrainlayer`**:
   - `diffuseTexture`: `Assets/Textures/Terrain/Grass_Albedo.png` (512x512 or 1024x1024)
   - `normalMapTexture`: `Assets/Textures/Terrain/Grass_Normal.png`
   - `tileSize`: `10 x 10`
2. **`Layer_Dirt.terrainlayer`**:
   - `diffuseTexture`: `Assets/Textures/Terrain/Dirt_Albedo.png`
   - `normalMapTexture`: `Assets/Textures/Terrain/Dirt_Normal.png`
   - `tileSize`: `8 x 8`
3. **`Layer_Asphalt.terrainlayer`**:
   - `diffuseTexture`: `Assets/Textures/Terrain/Asphalt_Albedo.png`
   - `normalMapTexture`: `Assets/Textures/Terrain/Asphalt_Normal.png`
   - `tileSize`: `12 x 12`
4. **`Layer_Rock.terrainlayer`**:
   - `diffuseTexture`: `Assets/Textures/Terrain/Rock_Albedo.png`
   - `normalMapTexture`: `Assets/Textures/Terrain/Rock_Normal.png`
   - `tileSize`: `14 x 14`

### Terrain Trees & Grass Detail
- **Trees**: Drop `Tree_Oak.fbx` or `Tree_Pine.fbx` into `Assets/Models/Environment/Trees/`. The `HVW_TerrainSetup` utility assigns them to the Terrain Tree Prototype list with automated wind and LOD billboards.
- **Grass**: Drop `Grass_Detail.png` (transparent billboard) into `Assets/Textures/Terrain/` to populate 3D swaying grass blades across open fields.

---

## 5. Lighting & Post-Processing Calibration

- **Directional Light**:
  - Color: Balanced pure daylight `Color(1.0f, 0.98f, 0.95f)`.
  - Intensity: `1.0f` (removes blown-out yellow exposure).
  - Shadow Type: `Soft Shadows`, resolution High, strength `0.78f`.
- **Skybox**:
  - Drop a 360° panoramic HDRI into `Assets/Textures/Skybox/Skybox_Daylight.hdr`.
- **Light Probes**:
  - A 3D grid of LightProbes is auto-generated across the vehicle operational zone (`-120m` to `+120m` in X and Z, `0.6m` to `6m` in Y) for dynamic vehicle ambient illumination.
- **Post-Processing**:
  - Tone Mapping: Neutral / ACES.
  - Bloom: Low intensity (`0.20f`), threshold `1.1f`.
  - Color Grading: Subtle daylight contrast (`+8`) and saturation (`+5`).

---

## 6. ASTC Mobile Texture Import Standards

To achieve optimal performance and zero visual compression artifacts on Android devices, all textures are imported using ASTC (Adaptive Scalable Texture Compression):

| Texture Role | Target Format | Mipmaps | sRGB Color | Normal Map Flag | Destination Directory |
|---|---|---|---|---|---|
| **Vehicle Albedo** | `ASTC_4x4` | Enabled | `True` | `False` | `Assets/Textures/Vehicles/` |
| **Vehicle Normal Map** | `ASTC_4x4` | Enabled | `False` | `True` | `Assets/Textures/Vehicles/` |
| **Vehicle Metallic/Smooth**| `ASTC_4x4` | Enabled | `False` | `False` | `Assets/Textures/Vehicles/` |
| **Terrain Albedo Maps** | `ASTC_6x6` | Enabled | `True` | `False` | `Assets/Terrain/` or `Assets/Textures/Terrain/` |
| **Terrain Normal Maps** | `ASTC_6x6` | Enabled | `False` | `True` | `Assets/Terrain/` or `Assets/Textures/Terrain/` |
| **Props & Foliage** | `ASTC_6x6` | Enabled | `True` | `False` | `Assets/Textures/Environment/` |
| **Skybox Panorama** | `ASTC_6x6` or `RGB_24` | Disabled | `True` | `False` | `Assets/Textures/Skybox/` |

> Automated by `Assets/Editor/HVW_TextureOptimizer.cs` (`HeavyVehiclesWorld/Optimize All Textures (ASTC Android)`).

---

## 7. Graphics Quality Tiers & Performance Targets

HeavyVehiclesWorld provides 4 distinct quality tiers, each driven by its own dedicated Universal Render Pipeline asset:

| Tier | Target Frame Rate | Resolution / Render Scale | MSAA | Shadows | Shadow Distance | Post-Processing & Special Features |
|---|---|---|---|---|---|---|
| **LOW** | 30 FPS (Battery Saver) | Native, Render Scale `1.0` | Disabled (`1x`) | Hard Only | `40 m` | ACES tonemapping, subtle vignette |
| **MEDIUM** | 30 - 45 FPS (Balanced) | Native, Render Scale `1.0` | `2x` MSAA | Soft Shadows (2 cascades) | `60 m` | ACES, subtle bloom (`0.15`), vignette, color grading |
| **HIGH** | 60 FPS (PBR Crisp) | Native, Render Scale `1.0` | `4x` MSAA | Soft Shadows (3 cascades) | `100 m` | ACES, subtle bloom (`0.20`), vignette, color grading, anisotropic filtering forced ON |
| **ULTRA** | 60 FPS (Flagship) | Native, Render Scale `1.15` | `4x` MSAA | Soft Shadows (4 cascades) | `120 m` | ACES, bloom (`0.25`), vignette, color grading, **SSAO (Screen Space Ambient Occlusion)** |

- **Hardware Auto-Detection**: At first boot, device RAM (`SystemInfo.systemMemorySize`) and GPU determine the optimal default tier (<3.5GB $\rightarrow$ Low, <5.5GB $\rightarrow$ Medium, <7.5GB $\rightarrow$ High, $\ge$7.5GB $\rightarrow$ Ultra).
- **In-Game Override**: Tap `⚙ SETTINGS` in the top bar to switch between Low, Medium, High, and Ultra at any time.

---

## 8. Fallback Architecture & Real-Time Warning System

When a 3D model asset has not yet been placed in `Assets/Models/Vehicles/<Vehicle>/`:
1. The engine automatically displays a **clean stylized low-poly fallback mesh** with physically calibrated PBR materials (never unshaded pink, bright red, or blank white).
2. The HUD Diagnostics Overlay (`🔧 DBG` button) prominently displays an on-screen warning:
   ```
   ⚠️ ASSET STATUS: Using Fallback Low-Poly geometry for [Vehicle].
      (Place 3D FBX into Assets/Models/Vehicles/[Vehicle]/ - see ASSET_INTEGRATION_GUIDE.md)
   ```
3. Once you place the corresponding `.fbx` model into the vehicle's folder, the fallback geometry automatically hides and the real 3D model takes over without requiring code changes.
