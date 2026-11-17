# HeavyVehiclesWorld – Phase 4 Setup Guide
## JCB Backhoe Loader & Excavator

---

## Section 1: JCB Prefab Assembly (Step by Step)

### Step 1 – Root Object

- **Create Empty** → name it `JCB` → set position to your spawn point
- Add **Rigidbody**:
  - Mass: `7500`
  - Drag: `0.08`
  - Angular Drag: `4`
  - Interpolate: `Interpolate`
- Add script: **JCBController**
- Add script: **JCBDiggingSystem**
- Add script: **JCBPrefabSetupGuide** *(displays Console warnings for any missing references)*

---

### Step 2 – Chassis Mesh

- Add **PrimitiveMeshBuilder** to the `JCB` root
  - Template: `JCB`
  - Tick **Build Now**
- This auto-generates:
  - `JCBBody` — yellow cube (4 × 2 × 2)
  - Front loader arm cubes
  - Rear backhoe arm cubes
  - 4 wheel visual cubes

---

### Step 3 – WheelColliders

Create **4 Empty children** under `JCB`:

| Name | Position |
|------|----------|
| `WheelFL` | Front-Left |
| `WheelFR` | Front-Right |
| `WheelRL` | Rear-Left |
| `WheelRR` | Rear-Right |

Each WheelCollider settings:

| Property | Value |
|----------|-------|
| Radius | `0.5` |
| Suspension Distance | `0.4` |
| Spring | `60000` |
| Damper | `6000` |
| Target Position | `0.5` |

- Assign each to **JCBController** fields: `wheelFL`, `wheelFR`, `wheelRL`, `wheelRR`

---

### Step 4 – Front Loader Arm Chain

```
JCB
└─ LoaderRoot         (Empty, pos: 0, 1.5, 2)
   └─ LoaderBoom      (Empty) → HydraulicArm: min=-20, max=70, speed=50
      └─ LoaderArm    (Empty) → HydraulicArm: min=-40, max=60, speed=55
         └─ LoaderBucket (Empty) → HydraulicArm: min=-90, max=45, speed=60
```

- Add **cube children** for visuals: narrow rectangles for arm segments, wider cube for bucket
- Assign `LoaderBoom`, `LoaderArm`, `LoaderBucket` HydraulicArm references in **JCBController**

---

### Step 5 – Rear Backhoe Arm Chain

```
JCB
└─ BackhoeSwing       (Empty, pos: 0, 1.2, -1.8) → HydraulicArm: axis=(0,1,0), min=-90, max=90, speed=45
   └─ BackhoeBoom     → HydraulicArm: min=0, max=80, speed=40
      └─ BackhoeDipper → HydraulicArm: min=-10, max=130, speed=45
         └─ BackhoeBucket → HydraulicArm: min=-110, max=40, speed=55
            └─ BucketTip  (Empty, local pos: 0, -0.8, 0.5)
```

> [!IMPORTANT]
> `BackhoeSwing` uses **Y-axis rotation** (`rotationAxis = (0,1,0)`) for left/right swing. All other joints rotate on the X axis.

- Assign `BucketTip` to **JCBDiggingSystem.backhoeBucketTip**

---

### Step 6 – Stabilizer Legs

Create **2 Empty children** under `JCB`:

| Name | Position |
|------|----------|
| `StabLeft` | (-1.5, 0.5, 0) |
| `StabRight` | (1.5, 0.5, 0) |

- Each: add a rotated cube child **(0.2 × 1.0 × 0.2)** as the visual leg
- Assign to **JCBController**: `stabilizerLeft` / `stabilizerRight`

---

### Step 7 – Additional Components

| Component | Configuration |
|-----------|---------------|
| **VehiclePhysicsSetup** | WheelSuspensionData → JCB preset (spring=60000, damper=6000) |
| **EngineAudio** | Assign engine audio clips |
| **VehicleEffects** | Assign dust + exhaust particle systems |
| **HydraulicSoundManager** | Assign hydraulic loop AudioClip |
| **CameraController** | Set up 3rd-person and 1st-person anchors |
| **VehicleInteractionTrigger** | SphereCollider trigger, radius=`4` |

---

### Step 8 – JCB UI Canvas

- Add **JCBUIController** to your Canvas
- Assign references: `jcb` and `diggingSystem`

**Layout:**

| Region | Element |
|--------|---------|
| Left side | Left virtual joystick *(loader control)* |
| Right side | Right virtual joystick *(backhoe control)* |
| Bottom row | **Lower Stabs** button \| **Raise Stabs** button \| **Dump Soil** button \| **4WD toggle** button |
| Overlay | Soil bar slider + `"Soil: X%"` label |

---

### Step 9 – Save Prefab

- Drag the `JCB` root into **Assets/Prefabs/Vehicles/JCB.prefab**

---

## Section 2: Excavator Prefab Assembly

### Step 1 – Root Object

- **Create Empty** → name it `Excavator`
- Add **Rigidbody**:
  - Mass: `22000`
  - Drag: `0.15`
  - Angular Drag: `6`
- Add scripts: **ExcavatorController**, **ExcavatorArmPresets**, **ExcavatorPrefabSetupGuide**

---

### Step 2 – Track WheelColliders (8 total)

**Left Track** (`LeftTrack` Empty at X = -0.9):

| Child Name | Z Offset | WheelCollider Radius | Suspension Dist |
|------------|----------|----------------------|-----------------|
| `BogieFront` | -1.2 | 0.4 | 0.1 |
| `BogieMiddle1` | -0.4 | 0.4 | 0.1 |
| `BogieMiddle2` | 0.4 | 0.4 | 0.1 |
| `BogieRear` | 1.2 | 0.4 | 0.1 |

**Right Track** (`RightTrack` Empty at X = +0.9): mirror of above.

> [!NOTE]
> Suspension distance is intentionally minimal (`0.1`) — tracks are rigid; the bogies provide ground-following, not soft suspension.

- Assign `leftTrackWheels[0–3]` and `rightTrackWheels[0–3]` in **ExcavatorController**

---

### Step 3 – Cabin

- Create Empty child **`Cabin`** at position (0, 1.5, 0)
- Add a **PrimitiveMeshBuilder** body under `Cabin` for the cabin mesh
- Assign `cabinTransform` in **ExcavatorController** *(this transform rotates 360° during operation)*

---

### Step 4 – Arm Chain (attached to Cabin)

```
Cabin
└─ Boom    (child of Cabin, pos: 0, 0.5, 1.2)  → HydraulicArm: min=-10, max=70,  speed=35
   └─ Stick (pos: 0, 0, 2.5)                   → HydraulicArm: min=0,   max=120, speed=40
      └─ Bucket (pos: 0, 0, 1.8)               → HydraulicArm: min=-40, max=90,  speed=45
         └─ BucketTip (pos: 0, -0.5, 0.6)
```

- Assign `BucketTip` to **ExcavatorController.bucketTip**

---

### Step 5 – Terrain Deformer

- Create Empty in scene → name it `TerrainDeformer` → add **TerrainDeformer** script
- Assign the scene **Terrain** to the TerrainDeformer component
- Assign the `TerrainDeformer` reference in **ExcavatorController**

---

### Step 6 – Track Visual Effects

- Add **TrackEffects** component to the `Excavator` root
- Create **4 Empty `TrackPoint` children** along left and right tracks at ground level
- Assign `leftTrackGroundPoints[0–3]` and `rightTrackGroundPoints[0–3]`

**Particle Systems:**

| Object | Settings |
|--------|---------|
| `LeftDust` | Brown, box emission |
| `RightDust` | Brown, box emission |

**Trail Renderers:**

| Object | Settings |
|--------|---------|
| `LeftTrail` | Width 0.5 m, mud texture |
| `RightTrail` | Width 0.5 m, mud texture |

---

### Step 7 – Save Prefab

- Drag `Excavator` root into **Assets/Prefabs/Vehicles/Excavator.prefab**

---

## Section 3: JCB Controls Reference

| Control | Keyboard | Gamepad | Mobile |
|---------|----------|---------|--------|
| Drive forward / back | `W` / `S` | Left stick Y | Left joystick Y |
| Drive left / right | `A` / `D` | Left stick X | Left joystick X |
| Loader arm raise / lower | `↑` / `↓` (numpad) | — | Left virtual joystick Y |
| Loader bucket tilt | `←` / `→` (numpad) | — | Left virtual joystick X |
| Backhoe swing | — | Right stick X | Right virtual joystick X |
| Backhoe dipper | — | Right stick Y | Right virtual joystick Y |
| Lower stabilizers | `G` | DPad Down | Stab button |
| Raise stabilizers | `T` | DPad Up | Stab button |
| Dump soil | `B` | B button | Dump button |
| Engine toggle | `F` | Start | Engine button |
| Camera switch | `C` | Right stick click | Camera button |

> [!WARNING]
> When `StabilizersDown = true`, **drive input is completely BLOCKED**. Lower stabs first, then work. Raise stabs before driving away.

---

## Section 4: Excavator Controls Reference

| Control | Keyboard | Gamepad | Mobile |
|---------|----------|---------|--------|
| Left track forward | `W` | — | Left joystick up |
| Right track forward | `W` | — | Left joystick up |
| Turn left | `A` | Left stick left | Left joystick left |
| Turn right | `D` | Left stick right | Left joystick right |
| Cabin rotate L / R | `Q` / `E` | Right stick X | Right joystick X |
| Boom up / down | `Z` / `X` | — | Dedicated buttons |
| Stick in / out | Right stick Y | Right stick Y | Right joystick Y |
| Bucket curl / dump | — | — | Bucket buttons |
| Preset 1 (Travel) | `1` | — | Preset 1 button |
| Preset 2 (Dig) | `2` | — | Preset 2 button |
| Preset 3 (Dump) | `3` | — | Preset 3 button |
| Dump soil | `B` | B button | Dump button |

---

## Section 5: Terrain Deformation Explained

### How Digging Works

```
ExcavatorController.CheckDigging()   ← runs every frame while isOccupied + engineRunning
        │
        ▼
Terrain.activeTerrain.SampleHeight(bucketTip.position)
        │
        ├─ if bucketTip.y < terrainY + digGroundOffset
        │          → DIGGING
        │
        ▼
TerrainDeformer.DigAt(bucketTip.position, digRadius, digDepth * Time.deltaTime)
        │
        ▼
Convert world pos → heightmap UV
Modify heights[x,y] downward in a circle
SetHeights() → writes back to TerrainData
        │
        ▼
currentSoilVolume += soilCollectRate * Time.deltaTime  (capped at 1.0)
```

- `DumpSoil()` calls `RaiseAt()` *(reverse of DigAt)* and resets `soilVolume` to `0`
- Terrain changes are **permanent within the session** but are **not saved to the asset on disk**

> [!TIP]
> `TerrainDeformer.DigAt()` calls `SetHeights()` which is expensive. For better performance, **batch changes** — collect all height modifications for a frame and call `SetHeights()` once per frame, not once per raycast hit.

---

## Section 6: Trench Mission Setup

1. Create Empty → name `TrenchMission` → add **TrenchMission** script
2. Create **8 Empty children** named `Waypoint0` through `Waypoint7` along a straight line, **20 m long**
3. Assign `trenchWaypoints` array; set all `waypointDepths` to `1.0`
4. Add a **TMP_Text** for `progressText`
5. Add **LineRenderer**: positions from waypoints, colour = green, width = `0.1`
6. Call `StartMission(jcbController)` from a Button or EventTrigger

---

## Section 7: Excavation Mission Setup

1. Create Empty → name `ExcavationMission` → add **ExcavationMission** script
2. Create Empty **`PitCentre`** at the centre of the target dig area
3. Assign `pitCentre`; set `pitSize = (10, 2, 10)`
4. Original terrain heights are **automatically sampled** on `StartMission()` — no manual setup needed
5. Progress is checked every **2 seconds** (no per-frame cost)
6. A **yellow gizmo box** in Scene view shows the target pit boundary

---

## Section 8: Phase 4 Testing Checklist

### JCB

- [ ] JCB drives on all 4 wheels (stabilizers UP)
- [ ] Press `G` → stabilizers animate DOWN, drive input disabled
- [ ] Left joystick Y → loader boom raises / lowers
- [ ] Left joystick X → loader bucket tilts
- [ ] Right joystick X → backhoe swings left / right
- [ ] Right joystick Y → backhoe dipper extends / retracts
- [ ] HydraulicArm audio plays while arm moves, stops when still
- [ ] Backhoe bucket tip near terrain → terrain depresses (DigAt called)
- [ ] Soil bar fills while digging
- [ ] Press Dump → soil bar empties, terrain raises slightly at dump point
- [ ] JCBPrefabSetupGuide → no warnings in Console

### Excavator

- [ ] Left joystick controls left track, right joystick right track (tank steering)
- [ ] Right joystick X rotates cabin 360° smoothly
- [ ] Boom arm responds to left joystick Y
- [ ] Stick arm responds to right joystick Y
- [ ] Pressing `1`–`4` recalls arm presets (Travel / Dig / DumpHigh / Curl)
- [ ] Bucket tip near terrain → DigAt called, terrain drops
- [ ] Soil bar fills, Dump button empties it
- [ ] TrackEffects dust emits when moving
- [ ] Track trails show on dirt terrain
- [ ] ExcavatorPrefabSetupGuide → no warnings in Console
