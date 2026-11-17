# HeavyVehiclesWorld – Android Build Guide

> **Game:** HeavyVehiclesWorld  
> **Studio:** HeavyVehiclesStudio  
> **Package:** `com.heavyvehicles.world`

---

## 1. Unity Version Requirements

| Requirement | Details |
|---|---|
| **Minimum** | Unity 2022.3 LTS |
| **Recommended** | Unity **2022.3.20f1** |
| **Render Pipeline** | Universal Render Pipeline (URP) 14.x |
| **Input System** | com.unity.inputsystem 1.7.0 |

> [!IMPORTANT]
> Always use an **LTS** (Long-Term Support) release for production Android builds. Non-LTS versions receive fewer bug-fix patches.

---

## 2. Required Unity Modules

Install these via **Unity Hub → Installs → (your install) → Add Modules**:

- ✅ **Android Build Support**
- ✅ **Android SDK & NDK Tools** (bundled with the module)
- ✅ **OpenJDK** (bundled with the module)

Alternatively, point Unity to a custom NDK/JDK in  
**Edit → Preferences → External Tools → Android**.

---

## 3. Project Settings Checklist

Open **Edit → Project Settings → Player → Android tab** and verify every item below.

### 3.1 Company & Application

| Setting | Value |
|---|---|
| Company Name | `HeavyVehiclesStudio` |
| Product Name | `HeavyVehiclesWorld` |
| Package Name | `com.heavyvehicles.world` |
| Version | `1.0` |
| Bundle Version Code | `1` (increment on each release upload) |

### 3.2 API Levels

| Setting | Value |
|---|---|
| Minimum API Level | **API 24** – Android 7.0 Nougat |
| Target API Level | **API 34** – Android 14 |

### 3.3 Architecture & Backend

| Setting | Value |
|---|---|
| Target Architectures | **ARM64** only (uncheck ARMv7) |
| Scripting Backend | **IL2CPP** |
| API Compatibility Level | **.NET Standard 2.1** |
| C++ Compiler Configuration | Release |

> [!NOTE]
> ARM64 + IL2CPP is mandatory for Google Play's 64-bit requirement and best performance.

### 3.4 Graphics APIs

In **Other Settings → Rendering**:

1. Uncheck **Auto Graphics API**
2. Add **Vulkan** (move to top)
3. Add **OpenGLES3** (fallback)
4. Remove **OpenGLES2** if present

### 3.5 Texture Compression

**Other Settings → Texture Compression:** `ASTC`

ASTC gives the best quality-to-size ratio on modern ARM GPUs (Mali, Adreno, Apple Silicon).

### 3.6 Orientation

**Resolution and Presentation:**

| Setting | Value |
|---|---|
| Default Orientation | Landscape Left |
| Auto-Rotate: Landscape Left | ✅ Enabled |
| Auto-Rotate: Landscape Right | ✅ Enabled |
| Auto-Rotate: Portrait | ❌ Disabled |
| Auto-Rotate: Portrait Upside Down | ❌ Disabled |

### 3.7 Additional Android Settings

| Setting | Value / Note |
|---|---|
| Write Permission | External (SDCard) – for save files |
| Internet Access | Require – if leaderboards / analytics used |
| Multithreaded Rendering | ✅ Enabled |
| Dynamic Batching | ✅ Enabled |
| GPU Skinning | ✅ Enabled |

---

## 4. URP Setup

1. **Create URP Assets** (if not already present):  
   `Assets → Create → Rendering → URP Asset (with Universal Renderer)`.  
   Create one per quality tier: `URP_Low`, `URP_Medium`, `URP_High`, `URP_Ultra`.

2. **Assign to Graphics Settings:**  
   `Edit → Project Settings → Graphics → Scriptable Render Pipeline Settings`  
   → drag in `URP_High` (or your default).

3. **Assign per Quality level:**  
   `Edit → Project Settings → Quality` → for each quality level set  
   **Render Pipeline Asset** to the matching URP asset.

4. **URP Asset Android tweaks** (open each asset):
   - Main Light: `Per Pixel`
   - Additional Lights: `Per Vertex` on Low/Medium, `Per Pixel` on High/Ultra
   - Shadows: tick **Cast Shadows**; Shadow Distance 80 m (Low) → 250 m (Ultra)
   - Post-processing: enabled

---

## 5. Input System Setup

1. Install `com.unity.inputsystem` via **Window → Package Manager**.
2. `Edit → Project Settings → Player → Other Settings → Active Input Handling`  
   → Select **Both** (this triggers a restart).
3. After restart, switch to **Input System Package (New)** only (optional but recommended).
4. Create an **Input Actions asset** (`Assets → Create → Input Actions`) named `HVWInputActions`.
5. Use `PlayerInput` component on the player/vehicle prefabs and reference the asset.

> [!TIP]
> Keep **Both** active during development so legacy `Input.GetAxis` calls still work while migrating.

---

## 6. Build Steps

### 6.1 Development / Debug APK

1. `File → Build Settings → Android`
2. Tick **Development Build** and **Script Debugging** (optional).
3. Click **Switch Platform** (wait for reimport).
4. Add open scenes or click **Add Open Scenes**.
5. Click **Build** → choose output folder → Unity produces `HeavyVehiclesWorld.apk`.
6. Install via ADB:
   ```bat
   adb install -r HeavyVehiclesWorld.apk
   ```

### 6.2 Release AAB (Google Play)

1. Ensure **Development Build** is **unchecked**.
2. Tick **Build App Bundle (Google Play)** in Build Settings.
3. Set the keystore (see §7).
4. Click **Build** → produces `HeavyVehiclesWorld.aab`.
5. Upload to Google Play Console → Internal Testing → Production.

---

## 7. Keystore Creation (Release Signing)

> [!CAUTION]
> **Never lose your keystore file or passwords.** A lost keystore means you cannot update your app on Google Play.

### Using Unity's built-in Keystore Manager

1. `Edit → Project Settings → Player → Android → Publishing Settings`
2. Click **Keystore Manager…**
3. Choose **Create New** → fill in:
   - **Keystore path:** `Keystore/heavyvehicles.keystore` (inside the project)
   - **Keystore password:** (store securely)
   - **Alias:** `heavyvehicles`
   - **Alias password:** (store securely)
   - **Validity:** 25 years
   - **Certificate:** fill in org details
4. Click **Add Key**.
5. Back in Publishing Settings, enable **Custom Keystore** and select the file.

### Using keytool (command line alternative)

```bat
keytool -genkey -v ^
  -keystore heavyvehicles.keystore ^
  -alias heavyvehicles ^
  -keyalg RSA -keysize 2048 ^
  -validity 9125
```

---

## 8. Optimization Checklist Before Final Build

### Assets
- [ ] All textures use **ASTC** compression (Android override in Texture Importer)
- [ ] Audio clips: music = **Vorbis**; SFX = **ADPCM** or **PCM** (short clips)
- [ ] Atlas sprite sheets for all UI elements (reduces draw calls)
- [ ] LOD Groups on all vehicle and environment meshes

### Code & Runtime
- [ ] `IL2CPP` backend confirmed
- [ ] Strip Engine Code: **Minimal** (`Managed Stripping Level = Minimal`)
- [ ] Enable **Incremental GC** (`Project Settings → Player → Other Settings`)
- [ ] Remove all `Debug.Log` calls or wrap in `#if UNITY_EDITOR`

### Rendering
- [ ] Occlusion Culling baked (`Window → Rendering → Occlusion Culling → Bake`)
- [ ] Lightmaps baked for static geometry
- [ ] GPU Instancing enabled on vehicle/terrain materials
- [ ] Particle systems have max-particle limits set

### Build
- [ ] ProGuard / minification enabled for release AAB
- [ ] `minSdkVersion` = 24, `targetSdkVersion` = 34 confirmed
- [ ] Android App Bundle (AAB) size < 150 MB base APK (use Asset Delivery for large assets)

---

## 9. Testing

### USB Debugging (Physical Device)

1. Enable **Developer Options** on the Android device:  
   `Settings → About Phone → tap Build Number 7×`.
2. Enable **USB Debugging** in Developer Options.
3. Connect via USB.
4. Verify ADB detects the device:
   ```bat
   adb devices
   ```
5. Build & Run from Unity (`Ctrl+Shift+B` → **Build and Run**) – Unity installs and launches automatically.

### Android Emulator (Android Studio AVD)

1. Install **Android Studio** and create an **AVD** with:
   - API 34, x86_64 image (for speed; ARM images are slower)
   - RAM ≥ 2 GB, VM Heap ≥ 512 MB
2. Start the emulator.
3. `adb install HeavyVehiclesWorld.apk`

> [!WARNING]
> The emulator does **not** support Vulkan on all host GPUs. Ensure OpenGLES3 fallback is included. Physical devices are always preferred for final QA.

### Remote Profiling

Connect Unity Profiler over USB:
1. Build with **Development Build** + **Autoconnect Profiler**.
2. `Window → Analysis → Profiler` → select the Android device target.
3. Profile CPU, GPU, Memory, and Rendering simultaneously.

---

## Appendix – Quick Command Reference

```bat
# Install APK
adb install -r build\HeavyVehiclesWorld.apk

# Tail logcat (filter by Unity tag)
adb logcat -s Unity

# Clear app data
adb shell pm clear com.heavyvehicles.world

# Uninstall
adb uninstall com.heavyvehicles.world

# Check connected devices
adb devices
```

---

*Last updated: 2026-11-18 | HeavyVehiclesStudio*
