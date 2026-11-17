# HeavyVehiclesWorld – Phase 3 Setup Guide

> Phase 3 adds the **Tractor + Plough**, **Harvester**, **Crop Field**, **Grain Silo**, and **Harvest Mission** systems. Follow each section in order. All prefabs save to `Assets/Prefabs/Vehicles/`.

---

## Section 1: Tractor Prefab Setup

1. **Create the root GameObject**
   - Hierarchy → **Create Empty** → rename to `Tractor`
   - Add component **PrimitiveMeshBuilder**: set `Template = Tractor`, click **Build Now**

2. **Add Rigidbody**
   - Mass = `4000`
   - Drag = `0.08`
   - Angular Drag = `3`
   - Use Gravity = ✓, Is Kinematic = ✗

3. **Add 4 WheelColliders** (create 4 child Empties, add WheelCollider to each)

   | Name | Radius | Suspension Distance | Spring | Damper |
   |---|---|---|---|---|
   | FrontLeft | 0.6 | 0.4 | 50 000 | 5 000 |
   | FrontRight | 0.6 | 0.4 | 50 000 | 5 000 |
   | RearLeft | 0.9 | 0.5 | 65 000 | 6 000 |
   | RearRight | 0.9 | 0.5 | 65 000 | 6 000 |

   > [!TIP] Set the WheelCollider's **Force App Point Distance** to `0.1` on all four wheels for realistic rolling without floating.

4. **Add TractorController**
   - Assign all 4 WheelCollider references (`frontLeftWheel`, `frontRightWheel`, `rearLeftWheel`, `rearRightWheel`)
   - Assign matching mesh transforms for each wheel
   - Set `is4WD = true`

5. **Rear Hitch**
   - Create child Empty → rename `RearHitch`
   - Local position: `(0, 0.8, -2.8)`
   - Assign to TractorController → `rearHitch` field

6. **Front Hitch**
   - Create child Empty → rename `FrontHitch`
   - Local position: `(0, 1.0, 2.5)`
   - Assign to TractorController → `frontHitch` field

7. **Hitch Arm**
   - Create child Empty → rename `HitchArm`
   - Add a Cube child inside it, scale `(0.1, 0.1, 1.5)`
   - Assign `HitchArm` to TractorController → `hitchArmTransform` field

8. **Add remaining components** to the Tractor root:
   - `VehiclePhysicsSetup` (auto-configures suspension from preset)
   - `EngineAudio` (assign engine loop AudioClip)
   - `VehicleEffects` (assign exhaust particle and dust trail prefabs)
   - `CameraController` (assign third-person anchor and driver seat transforms)
   - `VehicleInteractionTrigger` (enter/exit vehicle trigger)

9. **Save prefab**: drag the Tractor GameObject into `Assets/Prefabs/Vehicles/Tractor.prefab`

---

## Section 2: Plough Prefab Setup

1. **Create the root GameObject**
   - Create Empty → rename `PloughImplement`

2. **Add Rigidbody**
   - Mass = `800`, Drag = `0.5`, Angular Drag = `5`

3. **Create 3 blade children**
   - `Blade0` at local X = `-0.8`
   - `Blade1` at local X = `0`
   - `Blade2` at local X = `0.8`
   - Each blade: add a Cube child scaled `(0.1, 0.5, 0.8)`, rotated `15°` on the **Z axis**

4. **Add BoxCollider** to the root
   - Is Trigger = ✗
   - Size = `(2.4, 0.6, 0.9)`

5. **Add PloughImplement script**
   - Assign `bladeTransforms` array to [Blade0, Blade1, Blade2]
   - Assign scene `TerrainDeformer` reference

6. **Add Soil Dust ParticleSystem** as a child named `SoilDust`
   - Shape: **Box**
   - Start Color: brown (`#6B4226`)
   - Emission Rate Over Time: `50`
   - Start Size: `0.3–0.6` (random between two constants)
   - Start Lifetime: `1.5`

7. **Add 3 TrailRenderers** (one on each blade child)
   - Width: `0.4 m`
   - Material: brown/soil material
   - Time: `0.8` seconds

8. **Save prefab**: `Assets/Prefabs/Vehicles/PloughImplement.prefab`

---

## Section 3: Harvester Prefab Setup

1. **Create the root GameObject**
   - Create Empty → rename `Harvester`
   - Add **PrimitiveMeshBuilder**: Template = `Harvester`, click **Build Now**

2. **Add Rigidbody**
   - Mass = `12000`, Drag = `0.1`, Angular Drag = `4`

3. **Add 4 WheelColliders**
   - All wheels: Radius = `0.7`, Suspension Distance = `0.6`
   - Spring = `80 000`, Damper = `7 000`

4. **Add HarvesterController**
   - Assign all 4 wheel colliders and mesh transforms

5. **Header child**
   - Create child Empty → rename `Header`
   - Local position: `(0, -0.4, 3.5)`
   - Assign to HarvesterController → `headerTransform` field

6. **Header trigger**
   - Add **BoxCollider** to the `Header` child
   - Is Trigger = ✓, Size = `(3.0, 0.5, 1.5)`
   - Assign to HarvesterController → `headerTriggerZone` field

7. **CropManager**
   - Either add `CropManager` MonoBehaviour to an existing empty in the scene, or create a new Empty `CropManager`
   - Assign scene reference to HarvesterController → `cropManager` field

8. **Auger child**
   - Create child Empty → rename `Auger`, local position `(2.5, 2.0, 0)`
   - Add Cylinder child: scale `(0.15, 2.0, 0.15)`
   - Assign to HarvesterController → `augerTransform` field

9. **Particle Systems** (3 children):
   - `CropDust` — parent: Header. Shape: Cone, green-yellow, emission 80/s while cutting
   - `Chaff` — parent: Harvester root rear. Shape: Box, pale yellow, emission 30/s while cutting
   - `GrainStream` — parent: Auger tip. Shape: Cone (narrow), golden, emission 100/s while unloading

10. **Harvester HUD**
    - Add `HarvesterUIController` to your UI Canvas
    - Assign the Harvester GameObject reference

---

## Section 4: Crop Field Setup

1. **Create CropField_1**
   - Create Empty → rename `CropField_1`
   - Position in your farmland zone

2. **Add CropField script**
   - `rows = 20`, `columns = 40`
   - `rowSpacing = 0.75`, `columnSpacing = 0.5`

3. **Create crop stage prefabs** (3 total)
   - Stage 0 (seedling): Plane, scale `(0.1, 0.1, 0.1)`, green material (dark)
   - Stage 1 (growing): Plane, scale `(0.1, 0.1, 0.3)`, green material
   - Stage 2 (mature): Plane, scale `(0.1, 0.1, 0.6)`, golden-green material

4. **Configure CropField**
   - Assign `cropStagePrefabs` array: [Stage0Prefab, Stage1Prefab, Stage2Prefab]
   - Set `currentStage = 2` (mature, ready to harvest)

5. **Add Crop tag**
   - Edit → Project Settings → Tags & Layers → Tags → add `"Crop"`
   - All crop GameObjects generated by CropField should have this tag

6. **Register fields with CropManager**
   - Either add CropManager to the scene and register via Inspector, or CropField.Awake() calls `CropManager.Instance.RegisterField(this)` automatically

> [!IMPORTANT] Make sure the **CropManager** singleton exists in the scene before the CropField runs its `Awake()`. Use the **SceneBootstrapper** execution order or place CropManager higher in the hierarchy.

---

## Section 5: Grain Delivery Silo

1. Create Empty → rename `GrainSilo`, place near farm road

2. **Visuals**: Add two Cylinder children
   - Lower bin: scale `(4, 6, 4)`, position `(0, 3, 0)` — silver/grey material
   - Upper cone cap: scale `(4, 2, 4)`, position `(0, 7.5, 0)` — darker grey

3. **Add SphereCollider** to the root
   - Is Trigger = ✓
   - Radius = `8`

4. **Add GrainDeliveryPoint** script

5. **World-space UI label**
   - Create Canvas child (Render Mode = World Space), scale `(0.01, 0.01, 0.01)`
   - Add `TextMeshPro - Text` → text: `"Drive here to unload"`
   - Position it at `(0, 5, 0)` relative to the silo

---

## Section 6: Harvest Mission Setup

1. Create Empty → rename `HarvestMission`

2. **Add HarvestMission script**

3. **Inspector assignments**:
   - `targetField` → drag in `CropField_1`
   - `deliveryPoint` → drag in the `GrainDeliveryPoint` on `GrainSilo`
   - `grainRequired = 50`

4. **Mission status text**
   - Add a TMP_Text element to your main UI Canvas for mission status display
   - Assign to HarvestMission → `missionStatusText`

5. **Start the mission**
   - Option A: Call `HarvestMission.StartMission(harvesterRef)` from a UI Button `OnClick()`
   - Option B: Call it in `Start()` / from `GameManager` on scene load

> [!NOTE] The mission transitions through states: **Idle → Harvesting → Delivering → Complete**. Each state change fires an event you can hook into for audio/UI feedback.

---

## Section 7: Controls for Tractor

| Control | Keyboard | Mobile (Touch) |
|---|---|---|
| Throttle | `W` | Left joystick up |
| Brake / Reverse | `S` | Left joystick down |
| Steer left | `A` | Left joystick X– |
| Steer right | `D` | Left joystick X+ |
| Clutch | `Left Ctrl` | Clutch button |
| PTO toggle | `P` | PTO button |
| Hitch raise | `R` | Hitch up button |
| Hitch lower | `F` | Hitch down button |
| 4WD toggle | `4` | 4WD button |
| Attach / Detach implement | `X` | Implement button |

---

## Section 8: Controls for Harvester

| Control | Keyboard | Mobile (Touch) |
|---|---|---|
| Drive forward | `W` | Left joystick up |
| Drive reverse | `S` | Left joystick down |
| Steer | `A` / `D` | Left joystick X |
| Lower header | `H` | Header button |
| Start cutting | `C` | Cut button |
| Extend auger | `U` | Auger button |
| Start unload | `I` | Unload button |

---

## Section 9: Testing Checklist

- [ ] Tractor drives on terrain with all 4 wheels touching the ground
- [ ] PTO button sets `ptoActive = true`; `ptoRPM` shows a non-zero value in the Inspector
- [ ] Lowering hitch to `0` + `ptoActive` + moving forward = PloughImplement deforms terrain visibly
- [ ] `SoilDust` particles emit while ploughing; stop when stationary
- [ ] Harvester drives forward capped at `topSpeedKmh = 15 km/h`
- [ ] Lowering header + entering a CropField trigger zone sets `isCutting = true`
- [ ] Grain bar in HUD fills while cutting; `grainTankAmount` increments
- [ ] Pressing `U` extends auger; `GrainStream` particles play while `I` is held
- [ ] Driving into `GrainSilo` trigger zone: `GrainDeliveryPoint.totalGrainReceived` increments
- [ ] HarvestMission transitions: **Harvesting → Delivering → Complete** (check Console logs)

---

*End of Phase 3 Setup Guide*
