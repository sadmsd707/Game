# HeavyVehiclesWorld

[![Unity](https://img.shields.io/badge/Unity-6000.6.9f1%20(Unity%206)-blue.svg)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/URP-Universal%20Render%20Pipeline-lightgrey.svg)](https://unity.com/srp/universal-render-pipeline)
[![Platform](https://img.shields.io/badge/Platform-Android%20(ARM64%20IL2CPP)-green.svg)](https://www.android.com/)
[![Version](https://img.shields.io/badge/Release-v1.36%20(Production)-orange.svg)](https://github.com/sadmsd707/Game)
[![License](https://img.shields.io/badge/License-Proprietary-red.svg)](LICENSE.md)

A production-ready open-world mobile simulation game built with **Unity 6 (6000.6.9f1)** and the **Universal Render Pipeline (URP)**. Explore an expansive world featuring a multi-lane highway, construction zones, agricultural crop fields, and quarry dig sites across six drivable, physically-calibrated heavy vehicles.

---

## 📦 Production Release APK

- **Artifact Name:** `HeavyVehiclesWorld_Production.apk`
- **Location:** `Builds/Android/HeavyVehiclesWorld_Production.apk`
- **File Size:** 35.02 MB (36,720,312 bytes)
- **SHA-256 Checksum:** `73fffd94402d9c702ef69a735daedc0edfc8846547b05340c78e00a5f1f639ab`
- **Architecture:** ARM64 (`v8a`, IL2CPP)
- **Minimum OS:** Android 8.0 Oreo (API Level 26)
- **Target OS:** Android 14 (API Level 34)
- **Signing:** Signed with official production release keystore (`HVW_Production.keystore`)

---

## 🚗 Vehicle Roster & Mechanical Systems

| Vehicle | Class | Suspension & Locomotion | Special Mechanism & Controls | Audio Synthesis |
|---|---|---|---|---|
| **Car (SUV)** | Passenger Traversal | 6,000 Nm anti-roll sway bars, speed-sensitive steering (35° $\rightarrow$ 14°), service brake (<1 km/h), auto-reverse gear | Aerodynamic downforce, 12,000 Nm parking handbrake | Procedural 4-cylinder engine audio (`Synthesized_Car_Engine`) |
| **Truck (Tipper)** | Commercial Hauler | 15,000 Nm heavy anti-roll, progressive air-brakes (16,000 Nm/s), auto-reverse gear | 56° hydraulic tipper bed tilt, automated 6,000 kg cargo dump to 0 kg | Procedural 6-cylinder diesel engine audio (`Synthesized_Truck_Diesel`) |
| **Tractor (Agri)** | Agriculture Utility | 4WD traction, 9,000 Nm agricultural anti-roll, tight steering (40° $\rightarrow$ 18°) | Working 3-point hitch (-20° to +20°), attached 3-share soil plough implement | Procedural 3-cylinder agricultural diesel audio (`Synthesized_Tractor_Diesel`) |
| **JCB (Backhoe)** | Construction Earthmover | 4WD traction, 12,000 Nm anti-roll, drive-inhibiting ground outrigger stabilizers | Front loader boom raise & bucket tilt; rear 4-axis backhoe digging stroke with real-time terrain deformation | Procedural turbo diesel engine audio (`Synthesized_JCB_Diesel`) |
| **Excavator** | Heavy Excavation | Dual-track differential tank drive, in-place counter-rotation spin, 25,000 Nm track handbrake | 360° continuous cabin slew, multi-joint articulated boom/stick/bucket, active terrain digging and bucket dump | Procedural heavy diesel engine audio (`Synthesized_Excavator_Diesel`) |
| **Harvester** | Combine Harvesting | 4WD traction, 14,000 Nm combine anti-roll, wide chassis stabilization | Multi-height header raise/lower (0.75m to 0.15m), 144° spinning reel, active wheat cutting, 90° swing-out unload auger | Procedural agricultural combine diesel audio (`Synthesized_Harvester_Diesel`) |

---

## 📜 Version History & Changelog

Every modification in project development is tracked below, corresponding directly to the audit log in `HISTORY.md`:

### Version 1.0 | Change #1
- **Project Kickoff & Directory Scaffold:** Established complete Unity project architecture across `Assets/Scripts/`, `Prefabs/`, `Scenes/`, `Materials/`, `Textures/`, `Audio/`, and initialized the append-only `HISTORY.md` audit log.

### Version 1.1 | Change #2
- **Core Vehicle Base Classes:** Created `VehicleController.cs`, `WheelColliderSetup.cs`, and `VehicleTelemetry.cs` defining physics, center of mass, motor torque, and wheel synchronization interfaces.

### Version 1.2 | Change #3
- **Primary Vehicle Implementations:** Implemented physics controllers for `CarController.cs` (SUV), `TruckController.cs` (Tipper), and `TractorController.cs` (Agricultural 4WD).

### Version 1.3 | Change #4
- **Heavy Machinery Implementations:** Implemented specialized controllers for `JCBController.cs` (Backhoe Loader), `ExcavatorController.cs` (Tracked Digger), and `HarvesterController.cs` (Agricultural Combine).

### Version 1.4 | Change #5
- **Open World Infrastructure:** Created `WorldManager.cs`, `TerrainManager.cs`, and base mission architecture for open-world exploration and task tracking.

### Version 1.5 | Change #6
- **Physics Tuning & Aerodynamics:** Implemented suspension spring/damper calculations, longitudinal and lateral friction curves, and aerodynamic downforce to prevent vehicle flipping.

### Version 1.6 | Change #7
- **Mobile Touch Controls & Input System:** Integrated Unity's new Input System (`InputManager.cs`, `InputManagerProxy.cs`) supporting virtual on-screen steering joysticks, pedals, and touch event triggers.

### Version 1.7 | Change #8
- **Scene Infrastructure & Follow Camera:** Implemented `HVW_FollowCamera.cs` with smooth damping, collision occlusion avoidance, and dynamic multi-vehicle target switching.

### Version 1.8 | Change #9
- **Agricultural Tractor 3-Point Hitch:** Added 3-point hitch hydraulics and power take-off (PTO) drive systems to `TractorController.cs` for agricultural implement attachments.

### Version 1.9 | Change #10
- **Combine Harvester Cutting Header:** Added cutting header raise/lower hydraulics, grain hopper storage telemetry, and unloading pipe rotation to `HarvesterController.cs`.

### Version 1.10 | Change #11
- **Crop Field Management:** Implemented `CropManager.cs` and `CropField.cs` for agricultural field registration and wheat stalk harvesting delegation.

### Version 1.11 | Change #12
- **JCB Backhoe Dual-Mechanism Controls:** Implemented independent controls for front loader arms/bucket and rear 4-axis backhoe digging mechanism with outrigger ground stabilizers.

### Version 1.12 | Change #13
- **Tracked Excavator 360° Slew & Articulated Arm:** Implemented continuous cabin slew rotation on vertical axis, 3-joint articulated arm hierarchy (boom, stick, bucket), and trench digging physics.

### Version 1.13 | Change #14
- **Assembly & Rigging Documentation:** Authored comprehensive developer setup documentation for heavy machinery rigging, pivot alignments, and hierarchy constraints.

### Version 1.14 | Change #15
- **World Map Data & Environment Controller:** Created `WorldMapData.cs`, `WorldEnvironmentController.cs`, and `BridgeController.cs` managing environmental day/night cycles and bridge physics.

### Version 1.15 | Change #16
- **Procedural Road Network Generator:** Implemented `RoadNetworkGenerator.cs` and `VehicleSpawnPointMarker.cs` generating connected asphalt highways, intersections, and spawn markers.

### Version 1.16 | Change #17
- **Minimap Navigation UI:** Implemented `MinimapUI.cs` featuring corner-to-fullscreen modal toggle, zoom controls, compass heading indicator, and real-time GPS vehicle tracking.

### Version 1.17 | Change #18
- **Mission Reward & Currency Systems:** Implemented `MissionCompletePanel.cs` and `CurrencyManager.cs` displaying mission task summaries, completion rewards, and persistent coin balance.

### Version 1.18 | Change #19
- **Particle System Object Pooling:** Created `ObjectPooler.cs` and particle pooling for vehicle exhaust fumes, wheel dust trails, mud splatter, and crop harvesting chaff.

### Version 1.19 | Change #20
- **Game Lifecycle & State Management:** Created `GameManager.cs` with game loop state machine, pause menu controller, and cross-session state persistence.

### Version 1.20 | Change #21
- **Android Pre-Build Validator:** Authored `AndroidBuildValidator.cs` verifying 13 Android technical parameters (package identifier, SDK versions, IL2CPP backend, ARM64 architecture, Vulkan/GLES3 APIs, keystore).

### Version 1.21 | Change #22
- **Build Automation Pipeline:** Created `BuildAutomation.cs` managing pre-build asset checks, player settings automation, and Gradle packaging hooks.

### Version 1.22 | Change #23
- **Android Build Guide & Documentation:** Formulated complete step-by-step build guide (`BUILD_GUIDE.md`) and verified licensing metadata.

### Version 1.23 | Change #24
- **Cross-Namespace Compiler Fixes:** Resolved all namespace mismatches and API deprecations across 82 scripts for full Unity 6 (6000.6.9f1) compatibility.

### Version 1.24 | Change #25
- **Production Batch Build Utility:** Created `HVW_BatchBuild.cs` enabling automated headless compilation of signed ARM64 IL2CPP Android release APKs.

### Version 1.25 | Change #26
- **TypeTree Serialization Alignment:** Eliminated duplicate `maxRPM` field definitions from child vehicle controllers, resolving Unity TypeTree serialization conflicts; generated first release APK.

### Version 1.26 | Change #27
- **Runtime World Initializer Integration:** Created `HVW_WorldInitializer.cs` dynamically bootstrapping world terrain, roads, lighting, props, all 6 vehicles, follow camera, and touch HUD at scene startup.

### Version 1.27 | Change #28
- **VehicleModelSlot Architecture:** Replaced legacy `PrimitiveMeshBuilder` with clean `VehicleModelSlot.cs` drop-in slot system allowing external 3D FBX models to be placed directly into vehicles.

### Version 1.28 | Change #29 (Step 0 - Recovery)
- **Visual Recovery Pass:** Restored colored fallback bodies and cylinder wheels for all 6 vehicles whenever the Model slot is empty; synced wheels to `WheelCollider.GetWorldPose` (0% offset/rotation error); eliminated pink/white shader artifacts with procedural textures.

### Version 1.29 | Change #30 (Step 1 - Car Only)
- **Car Handling & Physics Perfection:** Implemented 6000 Nm front/rear anti-roll sway bars, speed-sensitive steering (35° at low speed $\rightarrow$ 14° at high speed), progressive service brakes (<1 km/h stop), auto-reverse gear [R], 12,000 Nm handbrake, procedural 4-cylinder engine audio, and tree/prop collision physics.

### Version 1.30 | Change #31 (Step 2 - Truck & Tractor)
- **Truck & Tractor Perfection:**
  - *Truck:* 15,000 Nm anti-roll sway bars, air brakes, auto-reverse gear, 20,000 Nm handbrake, 56° hydraulic tipper bed tilt, 6,000 kg cargo dump to 0 kg, procedural 6-cylinder diesel audio.
  - *Tractor:* 4WD traction, 9,000 Nm anti-roll, 3-point hitch (-20° to +20°), attached working 3-share Plough implement (`PloughImplement.cs`), procedural 3-cylinder diesel audio.
  - *Touch UI:* Added dynamic vehicle action button (`Btn_VehicleAction`) to mobile HUD.

### Version 1.31 | Change #32 (Step 3 - JCB Backhoe)
- **JCB Backhoe Loader Perfection:** 4WD anti-roll (12,000 Nm), ground outrigger stabilizers with drive inhibit, front loader boom raise & bucket tilt, rear 4-axis backhoe digging stroke, real-time terrain deformation (`TerrainDeformer.DigAt`), soil accumulation (35%), turbo diesel audio, dual-action touch HUD (`Btn_VehicleAction` + `Btn_VehicleAction2`).

### Version 1.32 | Change #33 (Step 3 - Tracked Excavator)
- **Tracked Excavator Perfection:** Dual-track differential tank drive in `FixedUpdate()`, in-place counter-rotation spin (68.2°), track bogie brakes & 25,000 Nm handbrake, continuous 360° cabin slew, multi-joint articulated arm chain (boom, stick, bucket, penetration tip), active terrain digging & bucket dump, heavy diesel audio, dual-action touch HUD.

### Version 1.33 | Change #34 (Step 3 - Combine Harvester)
- **Combine Harvester Perfection:** 4WD anti-roll (14,000 Nm), multi-height cutting header raise/lower (0.75m to 0.15m), revolving reel animation (144.0° spinning), procedural golden wheat field (`CropField.cs`, 600 standing stalks), active crop cutting accumulating grain (0% $\rightarrow$ 100%), 90° swing-out unloading auger pipe, grain discharge (25% $\rightarrow$ 0%), combine diesel audio, dual-action touch HUD.

### Version 1.34 | Change #35 (Step 4 - Visuals & Graphics Pass)
- **Master Visuals & PBR Architecture:**
  - Balanced natural daylight: Sun intensity calibrated to 1.0f with soft shadows, removing yellow exposure blowout.
  - PBR URP/Lit materials with texture slots (Albedo, Normal, Smoothness/Metallic) across vehicle paint, rubber tires, steel machinery, and asphalt.
  - 4 PBR TerrainLayers (`Layer_Grass`, `Layer_Dirt`, `Layer_Asphalt`, `Layer_Rock`) with painted splatmaps.
  - Terrain Tree system (`Prefab_Tree_Oak`) with solid `CapsuleCollider` trunk physics; Detail Grass billboard system.
  - 147-point LightProbeGroup dynamic daylight grid.
  - Global URP Volume post-processing: ACES Tonemapping, low Bloom (0.18 intensity, 1.10 threshold), Color Adjustments.
  - UI Deconfliction: Separated Top Toolbar (Row 1, Y = -22f) and Speedometer / Dashboard HUD (Row 2, Y = -72f) with 9.0px vertical gap, eliminating top UI overlap.

### Version 1.35 | Change #36 (Step 5 - Mobile Quality Pass)
- **Multi-Resolution UI & In-Game Settings Modal:**
  - Tested and verified 0 overlaps across all 17 interactive touch UI elements at 1280x576 (20:9 compact), 1920x1080 (16:9 standard), and 2400x1080 (20:9 modern flagship).
  - Added in-game Settings trigger button (`Btn_Settings`) in Row 1.
  - Built centered in-game Settings Modal (`Panel_SettingsModal`, 560x380px) supporting:
    - Graphics Quality tiers: LOW (Fast, shadows off), MEDIUM (Balanced, 75m shadows), HIGH (PBR, 150m soft shadows).
    - Frame Rate Capping: 30 FPS (Battery Saver), 60 FPS (Smooth), UNCAPPED (-1).
    - Real-time status display and responsive modal toggle.

### Version 1.36 | Change #37 (Step 6 - Android Release Build)
- **Production Signed Release APK:**
  - Pre-build validation check: 13/13 technical checks passed.
  - Upgraded minimum SDK to API Level 26 (Android 8.0 Oreo), target SDK to API Level 34 (Android 14).
  - Compiled and packaged `HeavyVehiclesWorld_Production.apk` (35.02 MB) signed with `HVW_Production.keystore`.
  - SHA-256 cryptographic checksum verified: `73fffd94402d9c702ef69a735daedc0edfc8846547b05340c78e00a5f1f639ab`.
  - Comprehensive on-phone test checklist provided.

---

## 🎮 Mobile Touch Controls

The mobile UI adapts automatically to any aspect ratio with non-overlapping touch controls:

| Screen Region | Control Element | Function |
|---|---|---|
| **Bottom-Left** | `Btn_SteerLeft` / `Btn_SteerRight` | Smooth analog steering (left/right) or differential tank steering |
| **Bottom-Right** | `Btn_Gas` (Green) | Progressive vehicle throttle & forward acceleration |
| **Bottom-Right** | `Btn_Brake` (Red) | Progressive service brakes / auto-reverse gear [R] from standstill |
| **Bottom-Right** | `Btn_Handbrake` (Yellow) | High-torque parking handbrake (locks driven wheels) |
| **Right Side** | `Btn_VehicleAction` (Orange) | Vehicle Action 1: Tipper bed dump / Plough raise-lower / Loader / Harvester header |
| **Right Side** | `Btn_VehicleAction2` (Blue) | Vehicle Action 2: Outriggers / Backhoe dig / Excavator 360° slew / Unload auger |
| **Top Bar (Row 1)**| `Btn_Select_Car` .. `Harvester` | Instant one-tap vehicle selector buttons |
| **Top Bar (Row 1)**| `Btn_Settings` (Gear) | Opens in-game Graphics Quality & FPS Cap settings modal |
| **Top Bar (Row 1)**| `Btn_CamView` (Camera) | Toggles chase camera framing and zoom angles |
| **Top Bar (Row 1)**| `Btn_ResetVeh` (Reset) | Rights vehicle upright if tipped over |
| **Top Bar (Row 2)**| `HUD_Dashboard` | Real-time digital speedometer (KM/H), gear state, and vehicle model telemetry |

---

## ⚙️ Engine & Technical Specifications

| Parameter | Specification |
|---|---|
| **Game Engine** | Unity 6000.6.9f1 (Unity 6) |
| **Render Pipeline** | Universal Render Pipeline (URP 17) |
| **Physics Simulation** | FixedUpdate @ 50 Hz (0.02s fixed timestep) |
| **Scripting Backend** | IL2CPP (ARM64 `v8a`) |
| **Graphics APIs** | Vulkan (preferred), OpenGL ES 3.0 (fallback) |
| **Input Architecture** | Unity Input System 1.7.0 + EventTrigger Mobile Canvas |
| **Audio** | Real-time DSP procedural audio synthesis (engine cylinders & mechanical whines) |
| **Terrain System** | Unity Terrain with 4 PBR TerrainLayers, painted splatmaps, tree prototypes, detail grass |

---

## 📄 License & Intellectual Property

All source code, assets, audio synthesis algorithms, physical models, and documentation for **HeavyVehiclesWorld** are the exclusive intellectual property of **Shivraj Deshmukh**.

© 2026 Shivraj Deshmukh. All rights reserved. Unauthorized copying, modification, or distribution is strictly prohibited. See [LICENSE.md](LICENSE.md) for details.
