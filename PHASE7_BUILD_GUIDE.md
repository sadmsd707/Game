# Phase 7 — Android Build, Testing & Final Cleanup

## Overview
Phase 7 is the final phase. It covers the complete pipeline to produce a signed APK (debug testing) and AAB (Play Store release), plus final optimization and QA steps.

---

## Step 1 — Unity Module Check

In **Unity Hub → Installs → (your Unity 2022.3 LTS) → ⋮ → Add Modules**, ensure these are installed:

- ✅ **Android Build Support**
- ✅ **Android SDK & NDK Tools** (bundled)
- ✅ **OpenJDK** (bundled)

> [!IMPORTANT]
> If you use a custom JDK/SDK, set the paths at:
> **Edit → Preferences → External Tools → Android**

---

## Step 2 — Project Settings (Android Tab)

Open **Edit → Project Settings → Player → Android icon tab**

### 2.1 Identification
| Setting | Value |
|---------|-------|
| Company Name | `Shivraj Deshmukh` |
| Product Name | `HeavyVehiclesWorld` |
| Package Name | `com.heavyvehicles.world` |
| Version | `1.0` |
| Bundle Version Code | `1` |

### 2.2 API Levels
| Setting | Value |
|---------|-------|
| Minimum API Level | **API 24** (Android 7.0 Nougat) |
| Target API Level | **Automatic (highest installed)** |

### 2.3 Rendering
| Setting | Value |
|---------|-------|
| Auto Graphics API | **OFF** (uncheck) |
| Graphics APIs | **Vulkan** (top), **OpenGLES3** (fallback) |
| Multithreaded Rendering | ✅ ON |
| Static Batching | ✅ ON |
| Dynamic Batching | ✅ ON |
| GPU Skinning | ✅ ON |

### 2.4 Configuration
| Setting | Value |
|---------|-------|
| Scripting Backend | **IL2CPP** |
| Target Architectures | ✅ **ARM64** only (remove ARMv7 for smaller APK) |
| C++ Compiler Config | **Release** |
| Use incremental GC | ✅ ON |
| Internet Access | Auto |
| Default Orientation | **Landscape Left** |

### 2.5 Optimization
| Setting | Value |
|---------|-------|
| Managed Stripping Level | **High** (removes unused code) |
| Enable Engine Code Stripping | ✅ ON |
| Texture Compression Format | **ASTC** |

---

## Step 3 — Validate Build Settings (Automated)

Run the built-in validator before every build:

```
Tools → HeavyVehiclesWorld → ✅ Validate Android Build Settings
```

This checks all 14 critical settings and shows a dialog with any failures and their fixes.

---

## Step 4 — Keystore Setup (Release Signing)

> [!CAUTION]
> **Back up your keystore file and password!** If you lose it you can NEVER update your Play Store app — you'd have to publish under a new package name.

### 4.1 Create a Keystore

**Option A — Unity GUI (easiest):**
1. Project Settings → Player → Android → **Publishing Settings**
2. Click **Keystore Manager** (top-right button near the keystore field)
3. Click **Create New → Anywhere...**
4. Save as: `HeavyVehiclesWorld/Keystore/HVW_Release.keystore`
5. Fill in:
   | Field | Suggested value |
   |-------|----------------|
   | Password | (strong, memorable) |
   | Alias | `hvw_key` |
   | Alias Password | (same or different) |
   | Validity | 25 years |
   | First and Last Name | Shivraj Deshmukh |
   | Organizational Unit | HeavyVehiclesWorld |
   | Organization | Shivraj Deshmukh |
   | City | (your city) |
   | Country Code | IN |
6. Click **Add Key**

**Option B — Command line (keytool):**
```bash
keytool -genkey -v -keystore HVW_Release.keystore \
  -alias hvw_key -keyalg RSA -keysize 2048 -validity 9125 \
  -dname "CN=Shivraj Deshmukh, OU=HeavyVehiclesWorld, O=Shivraj Deshmukh, L=YourCity, C=IN"
```

### 4.2 Assign Keystore in Unity
1. Project Settings → Player → Android → **Publishing Settings**
2. **Keystore** → Browse → select `HVW_Release.keystore`
3. Enter **Keystore password**
4. **Key Alias** → `hvw_key`
5. Enter **Key password**

> [!TIP]
> Add `Keystore/` to `.gitignore` to avoid committing your keystore. It's already included in the project's `.gitignore`.

---

## Step 5 — Texture Compression (ASTC)

1. Select all textures in `Assets/Textures/`
2. In the Inspector → **Android** platform tab:
   - Format: `ASTC 6x6` (balanced — use 4x4 for UI, 8x8 for distant terrain)
   - Max Size: 2048
   - Generate Mipmaps: ✅
3. For normal maps: set Texture Type = **Normal Map** first, then set ASTC 6x6

> [!NOTE]
> ASTC is only supported on Android 5.0+ (API 21+). Since our min is API 24, all target devices support it.

---

## Step 6 — Shader Variant Collection

Before building, capture all shader variants:
1. Play the game in Editor and drive every vehicle in every weather condition
2. **Edit → Project Settings → Graphics → Shader Preloading**
3. Click **Save to asset** → save as `Assets/Settings/HVW_ShaderVariants.shadervariants`
4. Assign this asset to `FinalPerformancePass.shaderVariants` in the Inspector

This eliminates shader compilation hitches (stutters) on first load on device.

---

## Step 7 — Build Debug APK (Test on Device)

### Via Menu (easiest):
```
Tools → HeavyVehiclesWorld → 🔨 Build Debug APK
```
Output: `Builds/Android/HeavyVehiclesWorld_Debug.apk`

### Via File → Build Settings:
1. **File → Build Settings**
2. Ensure **Android** is selected
3. Check **Development Build** ✅ and **Allow Debugging** ✅
4. **Build** → choose output folder
5. Click **Build**

### Install on device via ADB:
```bash
adb install -r Builds/Android/HeavyVehiclesWorld_Debug.apk
```

### Build & Run directly on connected device:
```
Tools → HeavyVehiclesWorld → 🚀 Build & Run on Device
```
(Device must be in Developer Mode with USB debugging ON)

---

## Step 8 — Build Release AAB (Google Play)

### Via Menu:
```
Tools → HeavyVehiclesWorld → 📦 Build Release AAB
```
Output: `Builds/Android/HeavyVehiclesWorld_Release.aab`

### Via File → Build Settings:
1. **File → Build Settings → Android**
2. Uncheck **Development Build**
3. Check **Build App Bundle (Google Play)** ✅
4. **Build** → choose output folder

> [!IMPORTANT]
> The AAB requires your keystore to be configured (Step 4). Without it, the build will fail or produce an unsigned bundle.

---

## Step 9 — Performance Testing on Device

### Tools to use:
| Tool | What to check |
|------|---------------|
| **Android Logcat** (Unity package) | Runtime errors, Debug.Log output |
| **Unity Profiler** (over USB) | CPU/GPU time per frame, GC allocations |
| **Android GPU Inspector** | GPU overdraw, shader hotspots |
| **Unity Memory Profiler** | Texture memory, mesh memory, GC heap |

### In-game P key performance monitor:
- Press **P** in-game to toggle the `PerformanceMonitor` HUD
- Shows FPS (color-coded), memory, draw calls
- Disable this GO in the final release build

### Target performance:
| Device Tier | RAM | Expected FPS (Medium quality) |
|-------------|-----|-------------------------------|
| Low-end | 3–4 GB | 30 FPS |
| Mid-range | 6 GB | 45–60 FPS |
| Flagship | 8–12 GB | 60 FPS |

---

## Step 10 — Device Tier Auto-Detection

In your first scene's `Start()`, call:
```csharp
FindObjectOfType<AndroidPerformanceSettings>()?.AutoApplyDeviceTier();
```

This auto-detects the device tier (RAM + VRAM) and applies Low/Medium/High quality + physics settings accordingly. The player can override via the in-game Settings menu.

---

## Step 11 — Final Cleanup Checklist

Before the release build:

- [ ] Remove `PerformanceMonitor` GameObject (or disable it) from all scenes
- [ ] Set `AndroidBuildValidator` — it runs automatically as `IPreprocessBuildWithReport`
- [ ] Strip `Debug.Log` calls: **Edit → Project Settings → Player → Other → Strip Engine Code** ✅
- [ ] Confirm `FinalPerformancePass.shaderVariants` is assigned
- [ ] Confirm `FullSaveLoadManager.allVehicles[]` has all 6 vehicles
- [ ] Confirm all 6 `VehicleSpawnPointMarker` GOs are in the scene
- [ ] All scenes added to **File → Build Settings** scene list
- [ ] Bundle Version Code incremented to `1`
- [ ] Test cold start (app launch from scratch) — no crash
- [ ] Test save → kill app → relaunch → load → correct state
- [ ] Test all 6 vehicles: enter/drive/exit
- [ ] Test weather cycle: Clear → Rain → Fog → Storm
- [ ] Test day/night cycle completes full 24h loop
- [ ] Test minimap toggle and zoom
- [ ] Complete at least 1 mission end-to-end
- [ ] Verify MissionCompletePanel shows with animation
- [ ] Verify coins added after mission complete
- [ ] Check no unhandled NullReferenceExceptions in Logcat

---

## Step 12 — Google Play Upload

1. Go to [play.google.com/console](https://play.google.com/console)
2. Create new app → **HeavyVehiclesWorld**
3. Upload `HeavyVehiclesWorld_Release.aab` to **Internal Testing** track first
4. Fill in store listing: title, description, screenshots, icon
5. Submit for review

> [!NOTE]
> Google Play requires a 512×512 icon and feature graphic (1024×500). Create these in any image editor.

---

## Build Automation Reference

| Menu Item | Output | Use When |
|-----------|--------|----------|
| `🔨 Build Debug APK` | `.apk` with debug symbols | Testing on physical device |
| `📦 Build Release AAB` | Signed `.aab` | Google Play upload |
| `🚀 Build & Run` | `.apk` → auto-installs | Rapid iteration with Profiler |
| `✅ Validate Settings` | Dialog | Before any build |
| `📋 Print Settings` | Console log | Sharing build config |

---

## Files Added in Phase 7

| File | Location | Purpose |
|------|----------|---------|
| `AndroidBuildValidator.cs` | `Scripts/Systems/` | 14-check pre-build validator, menu item |
| `AndroidPerformanceSettings.cs` | `Scripts/Systems/` | Runtime Android tuning + device tier auto-detect |
| `BuildAutomation.cs` | `Scripts/Systems/` | One-click APK/AAB build menu items |
| `FinalPerformancePass.cs` | `Scripts/Systems/` | Shader warm-up, GC clean, device capability log |
| `PHASE7_BUILD_GUIDE.md` | Root | This file |

---

## 🎉 Project Complete — All 7 Phases Done

```
Phase 1  ✅  Project setup, base scripts, touch UI
Phase 2  ✅  Car + Truck (WheelCollider physics, engine audio)
Phase 3  ✅  Tractor + Harvester (implements, crop system)
Phase 4  ✅  JCB + Excavator (hydraulic arms, terrain digging)
Phase 5  ✅  4km × 4km open world (zones, minimap, day/night, weather)
Phase 6  ✅  Graphics polish, LOD, particle pooling, save/load, rewards UI
Phase 7  ✅  Android build, signing, automation, final QA
```

**© 2026 Shivraj Deshmukh — All Rights Reserved**
