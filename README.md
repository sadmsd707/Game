# HeavyVehiclesWorld

A Unity-based Android game featuring a rich open-world environment with six drivable heavy vehicles. Explore vast landscapes, complete missions, and experience realistic vehicle physics on mobile.

---

## 🚗 Vehicle Roster

| Vehicle | Description |
|---------|-------------|
| **Car** | Fast, agile passenger vehicle for quick traversal |
| **Truck** | Heavy hauler, capable of carrying large loads across terrain |
| **Tractor** | Agricultural powerhouse, slow but highly capable off-road |
| **JCB** | Wheeled loader/backhoe for construction site interactions |
| **Excavator** | Tracked heavy excavator with articulated arm and bucket |
| **Harvester** | Agricultural combine harvester with wide-span cutting head |

---

## 📁 Folder Structure

```
HeavyVehiclesWorld/
├── Assets/
│   ├── Scripts/
│   │   ├── Vehicles/      # Vehicle controllers, physics, engine audio
│   │   ├── Controls/      # Input handling (new Input System), touch controls
│   │   ├── UI/            # HUD, menus, minimap, vehicle selector
│   │   ├── World/         # Terrain generation, environment, day-night cycle
│   │   └── Systems/       # Game manager, save/load, notifications, pooling
│   ├── Scenes/            # Unity scene files
│   ├── Prefabs/
│   │   ├── Vehicles/      # Vehicle prefabs (one per vehicle type)
│   │   └── UI/            # UI prefab panels and widgets
│   ├── Materials/         # URP materials
│   ├── Textures/          # Albedo, normal, roughness maps
│   ├── Audio/
│   │   ├── Engines/       # Per-vehicle engine loop audio clips
│   │   └── Ambience/      # Wind, nature, environment ambience
│   ├── Animations/        # Animator controllers and clips
│   ├── Shaders/           # Custom URP shader graphs
│   ├── Settings/          # URP renderer assets, input action assets
│   └── Resources/         # Runtime-loaded addressable fallbacks
├── Packages/
│   └── manifest.json      # Unity package dependencies
└── ProjectSettings/
    └── ProjectSettings.asset  # Android build & player settings
```

---

## ⚙️ Unity Version Requirements

| Requirement | Version |
|---|---|
| **Unity Editor** | 2022.3 LTS (minimum 2022.3.20f1) |
| **Universal Render Pipeline (URP)** | 14.0.9 |
| **Input System** | 1.7.0 |
| **Burst Compiler** | 1.8.9 |
| **Cinemachine** | 2.9.7 |
| **TextMeshPro** | 3.0.6 |
| **Addressables** | 1.21.19 |

> [!IMPORTANT]
> Always open the project with **Unity 2022.3 LTS**. Using a newer major version may break URP 14 package compatibility.

---

## 📦 Package Dependencies (`Packages/manifest.json`)

```json
{
  "com.unity.inputsystem": "1.7.0",
  "com.unity.render-pipelines.universal": "14.0.9",
  "com.unity.cinemachine": "2.9.7",
  "com.unity.textmeshpro": "3.0.6",
  "com.unity.terrain-tools": "5.0.2",
  "com.unity.burst": "1.8.9",
  "com.unity.collections": "2.1.4",
  "com.unity.mathematics": "1.2.6",
  "com.unity.mobile.notifications": "2.3.2",
  "com.unity.addressables": "1.21.19",
  "com.unity.nuget.newtonsoft-json": "3.2.1"
}
```

---

## 🏗️ Development Phases

### Phase 1 – Project Scaffold *(current)*
- Initialize folder structure
- Configure Unity packages and project settings
- Set up URP renderer and Input Action Assets
- Establish HISTORY.md change log

### Phase 2 – Vehicle Physics
- Implement base `VehicleController.cs` using `WheelCollider`
- Per-vehicle derived classes: `CarController`, `TruckController`, `TractorController`, `JCBController`, `ExcavatorController`, `HarvesterController`
- Engine audio manager with RPM-driven pitch blending

### Phase 3 – Controls & Input
- New Input System action maps for touch joysticks, steering, throttle/brake
- Mobile virtual joystick UI overlays
- Accelerometer-based steering option
- Vehicle switch input binding

### Phase 4 – World & Environment
- Terrain authoring with Terrain Tools
- Day/night cycle shader + skybox rotation
- Dynamic weather (rain, fog) using URP volume overrides
- Road spline system

### Phase 5 – UI / HUD
- In-game HUD: speedometer, gear indicator, minimap
- Main menu, vehicle selector carousel
- Settings panel (graphics quality, control sensitivity)
- Loading screen with progress bar

### Phase 6 – Systems & Polish
- Save/load system (JSON + PlayerPrefs hybrid)
- Mobile push notifications (`com.unity.mobile.notifications`)
- Object pooling for particles and audio
- Addressable asset loading for large vehicle meshes

### Phase 7 – Android Build & Release
- IL2CPP + ARM64 build
- Keystore signing configuration
- APK and AAB export
- Play Store submission checklist

---

## 🤖 Android Build Instructions

### Prerequisites
1. Install **Android Build Support** module in Unity Hub for Unity 2022.3 LTS
2. Install **Android SDK**, **NDK**, and **JDK** via Unity Hub → Installs → (your install) → Add Modules
3. Set paths in Unity: *Edit → Preferences → External Tools → Android*

### Build Settings

| Setting | Value |
|---|---|
| Platform | Android |
| Scripting Backend | IL2CPP |
| Target Architecture | ARM64 (only) |
| Graphics API | Vulkan (primary), OpenGL ES 3.1 (fallback) |
| Min SDK | API 24 (Android 7.0) |
| Target SDK | API 34 (Android 14) |
| Orientation | Landscape (auto-rotate left/right) |
| Color Space | Linear |

### Build Steps

```
1. File → Build Settings → Switch Platform → Android
2. Player Settings:
   - Company Name: Shivraj Deshmukh
   - Product Name: HeavyVehiclesWorld
   - Package Name: com.heavyvehicles.world
   - Version: 1.0 / Bundle Version Code: 1
3. For APK: uncheck "Build App Bundle (AAB)"
   For AAB: check  "Build App Bundle (AAB)"
4. Click "Build" or "Build And Run"
```

> [!TIP]
> Use **Build And Run** with a connected Android device (USB debugging on) for faster iteration during Phase 2–5 development.

---

## 🎮 Control Scheme

### Touch Controls (default mobile)

| Control | Action |
|---|---|
| Left virtual joystick | Steering (left / right) |
| Right side – throttle button | Accelerate |
| Right side – brake button | Brake / Reverse |
| Swipe up on camera area | Zoom in |
| Swipe down on camera area | Zoom out |
| Double-tap | Switch camera angle |
| Vehicle selector button (HUD) | Open vehicle switcher |

### Accelerometer Steering (optional)
- Tilt device left/right to steer
- Toggle in Settings panel

### Keyboard Controls (Editor / PC testing)

| Key | Action |
|---|---|
| W / ↑ | Throttle |
| S / ↓ | Brake / Reverse |
| A / ← | Steer Left |
| D / → | Steer Right |
| Space | Handbrake |
| Tab | Switch vehicle |
| Esc | Pause menu |

---

## 📋 Contribution Guidelines

- **Scripts:** Follow C# naming conventions; one class per file, namespace `HVW`.
- **Change log:** Every code change must append an entry to `HISTORY.md` (see template inside).
- **Prefabs:** Never modify a shared prefab's root transform; use instance overrides.
- **Assets:** All textures must be power-of-two, max 2048×2048 for mobile.

---

*HeavyVehiclesWorld © 2026 Shivraj Deshmukh. All rights reserved. Unauthorized copying, distribution, or use of this project is strictly prohibited.*
