using UnityEngine;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Editor utility that builds primitive placeholder vehicle meshes from Unity primitives.
    /// Set 'buildNow = true' in the Inspector to trigger a build.
    /// Runs in Edit Mode so designers can preview vehicle shapes without imported models.
    /// </summary>
    [ExecuteInEditMode]
    public class PrimitiveMeshBuilder : MonoBehaviour
    {
        public enum VehicleTemplate
        {
            Car,
            Truck,
            Tractor,
            JCB,
            Excavator,
            Harvester
        }

        [SerializeField] private VehicleTemplate template = VehicleTemplate.Car;
        [SerializeField] private bool buildNow = false;

        // ------------------------------------------------------------------ //
        //  Shared material colours                                             //
        // ------------------------------------------------------------------ //
        private static readonly Color BodyGrey  = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color DarkGrey  = new Color(0.20f, 0.20f, 0.20f);
        private static readonly Color YellowJCB = new Color(0.98f, 0.80f, 0.00f);
        private static readonly Color GreenHarv = new Color(0.10f, 0.55f, 0.15f);
        private static readonly Color OrangeExc = new Color(0.95f, 0.45f, 0.05f);

        private void Update()
        {
#if UNITY_EDITOR
            if (!buildNow) return;
            buildNow = false;

            // Remove previously generated children
            ClearChildren();

            switch (template)
            {
                case VehicleTemplate.Car:       BuildCar();       break;
                case VehicleTemplate.Truck:     BuildTruck();     break;
                case VehicleTemplate.Tractor:   BuildTractor();   break;
                case VehicleTemplate.JCB:       BuildJCB();       break;
                case VehicleTemplate.Excavator: BuildExcavator(); break;
                case VehicleTemplate.Harvester: BuildHarvester(); break;
            }

            Debug.Log($"[PrimitiveMeshBuilder] Built placeholder mesh for '{template}'.");
#endif
        }

        // ------------------------------------------------------------------ //
        //  Helpers                                                             //
        // ------------------------------------------------------------------ //

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        private GameObject MakePrimitive(string primName, PrimitiveType type,
                                         Vector3 localPos, Vector3 scale,
                                         Color colour, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = primName;
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = scale;

            var mat = new Material(Shader.Find("Standard")) { color = colour };
            go.GetComponent<Renderer>().sharedMaterial = mat;

            // Remove colliders on sub-meshes to avoid physics noise in editor
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            return go;
        }

        private GameObject MakeEmpty(string goName, Vector3 localPos, Transform parent = null)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = localPos;
            return go;
        }

        // ------------------------------------------------------------------ //
        //  CAR                                                                 //
        // ------------------------------------------------------------------ //
        private void BuildCar()
        {
            // Body
            MakePrimitive("Car_Body",  PrimitiveType.Cube,
                          new Vector3(0, 0.7f, 0), new Vector3(4f, 1.4f, 1.8f), BodyGrey);

            // Roof
            MakePrimitive("Car_Roof",  PrimitiveType.Cube,
                          new Vector3(0, 1.5f, 0), new Vector3(3f, 0.8f, 1.6f), BodyGrey);

            // Wheels – cylinder, rotated 90° on Z so it lies flat
            Vector3[] wheelPositions =
            {
                new Vector3(-1.6f, 0.35f,  0.9f),  // FL
                new Vector3(-1.6f, 0.35f, -0.9f),  // FR
                new Vector3( 1.6f, 0.35f,  0.9f),  // RL
                new Vector3( 1.6f, 0.35f, -0.9f),  // RR
            };
            string[] wheelNames = { "Car_WheelFL", "Car_WheelFR", "Car_WheelRL", "Car_WheelRR" };

            for (int i = 0; i < 4; i++)
            {
                var w = MakePrimitive(wheelNames[i], PrimitiveType.Cylinder,
                                      wheelPositions[i], new Vector3(0.70f, 0.35f, 0.70f), DarkGrey);
                w.transform.localEulerAngles = new Vector3(0, 0, 90);
            }
        }

        // ------------------------------------------------------------------ //
        //  TRUCK                                                               //
        // ------------------------------------------------------------------ //
        private void BuildTruck()
        {
            // Cab
            MakePrimitive("Truck_Cab",     PrimitiveType.Cube,
                          new Vector3(-3.75f, 1.25f, 0), new Vector3(2.5f, 2.5f, 2.2f), BodyGrey);

            // Chassis
            MakePrimitive("Truck_Chassis", PrimitiveType.Cube,
                          new Vector3(1f, 0.5f, 0), new Vector3(7f, 1f, 2f), DarkGrey);

            // Tipper Bed
            MakePrimitive("Truck_TipperBed", PrimitiveType.Cube,
                          new Vector3(2.5f, 1.15f, 0), new Vector3(5f, 0.3f, 2f), BodyGrey);

            // 6 Wheels (dual rear)
            var wheelDefs = new (string name, Vector3 pos)[]
            {
                ("Truck_WheelFL", new Vector3(-3.5f, 0.5f,  1.1f)),
                ("Truck_WheelFR", new Vector3(-3.5f, 0.5f, -1.1f)),
                ("Truck_WheelML", new Vector3( 1.5f, 0.5f,  1.1f)),
                ("Truck_WheelMR", new Vector3( 1.5f, 0.5f, -1.1f)),
                ("Truck_WheelRL", new Vector3( 3.5f, 0.5f,  1.1f)),
                ("Truck_WheelRR", new Vector3( 3.5f, 0.5f, -1.1f)),
            };
            foreach (var (wname, pos) in wheelDefs)
            {
                var w = MakePrimitive(wname, PrimitiveType.Cylinder, pos,
                                     new Vector3(1.0f, 0.5f, 1.0f), DarkGrey);
                w.transform.localEulerAngles = new Vector3(0, 0, 90);
            }
        }

        // ------------------------------------------------------------------ //
        //  TRACTOR                                                             //
        // ------------------------------------------------------------------ //
        private void BuildTractor()
        {
            // Body
            MakePrimitive("Tractor_Body", PrimitiveType.Cube,
                          new Vector3(0, 0.9f, 0), new Vector3(3f, 1.8f, 2f), BodyGrey);

            // Large Rear Wheels
            var rearWheelDefs = new (string name, Vector3 pos)[]
            {
                ("Tractor_WheelRL", new Vector3( 1.0f, 1.1f,  1.1f)),
                ("Tractor_WheelRR", new Vector3( 1.0f, 1.1f, -1.1f)),
            };
            foreach (var (wname, pos) in rearWheelDefs)
            {
                var w = MakePrimitive(wname, PrimitiveType.Cylinder, pos,
                                     new Vector3(1.1f, 0.55f, 1.1f), DarkGrey);
                w.transform.localEulerAngles = new Vector3(0, 0, 90);
            }

            // Small Front Wheels
            var frontWheelDefs = new (string name, Vector3 pos)[]
            {
                ("Tractor_WheelFL", new Vector3(-1.3f, 0.6f,  0.65f)),
                ("Tractor_WheelFR", new Vector3(-1.3f, 0.6f, -0.65f)),
            };
            foreach (var (wname, pos) in frontWheelDefs)
            {
                var w = MakePrimitive(wname, PrimitiveType.Cylinder, pos,
                                     new Vector3(0.60f, 0.40f, 0.60f), DarkGrey);
                w.transform.localEulerAngles = new Vector3(0, 0, 90);
            }

            // Exhaust Pipe
            MakePrimitive("Tractor_Exhaust", PrimitiveType.Cylinder,
                          new Vector3(-0.7f, 2.0f, 0.6f), new Vector3(0.1f, 0.6f, 0.1f), DarkGrey);

            // Roll Bar
            MakePrimitive("Tractor_RollBar", PrimitiveType.Cube,
                          new Vector3(0.5f, 2.2f, 0), new Vector3(0.1f, 1.0f, 2.0f), BodyGrey);
        }

        // ------------------------------------------------------------------ //
        //  JCB                                                                 //
        // ------------------------------------------------------------------ //
        private void BuildJCB()
        {
            // Body
            MakePrimitive("JCB_Body", PrimitiveType.Cube,
                          new Vector3(0, 1f, 0), new Vector3(4f, 2f, 2f), YellowJCB);

            // --- Front Loader Arm (3 cubes) ---
            var loaderRoot = MakeEmpty("JCB_FrontLoaderArm", new Vector3(-2.5f, 0.8f, 0));
            MakePrimitive("JCB_LoaderArm1", PrimitiveType.Cube,
                          new Vector3(-0.6f, 0.5f, 0), new Vector3(1.2f, 0.2f, 0.3f), YellowJCB, loaderRoot.transform);
            MakePrimitive("JCB_LoaderArm2", PrimitiveType.Cube,
                          new Vector3(-1.3f, 0.9f, 0), new Vector3(0.3f, 0.8f, 0.3f), YellowJCB, loaderRoot.transform);
            MakePrimitive("JCB_LoaderBucket", PrimitiveType.Cube,
                          new Vector3(-1.3f, 0.3f, 0), new Vector3(1.5f, 0.15f, 1.8f), DarkGrey, loaderRoot.transform);

            // --- Rear Backhoe Arm (3 cubes) ---
            var backhoeRoot = MakeEmpty("JCB_BackhoeArm", new Vector3(2.3f, 1.5f, 0));
            MakePrimitive("JCB_BackhoeArm1", PrimitiveType.Cube,
                          new Vector3(0.5f, 0, 0), new Vector3(1.0f, 0.2f, 0.2f), YellowJCB, backhoeRoot.transform);
            MakePrimitive("JCB_BackhoeArm2", PrimitiveType.Cube,
                          new Vector3(1.2f, -0.5f, 0), new Vector3(0.2f, 1.0f, 0.2f), YellowJCB, backhoeRoot.transform);
            MakePrimitive("JCB_BackhoeBucket", PrimitiveType.Cube,
                          new Vector3(1.2f, -1.1f, 0), new Vector3(0.7f, 0.15f, 0.8f), DarkGrey, backhoeRoot.transform);

            // 4 Wheels
            var jcbWheels = new (string name, Vector3 pos)[]
            {
                ("JCB_WheelFL", new Vector3(-1.6f, 0.5f,  1.1f)),
                ("JCB_WheelFR", new Vector3(-1.6f, 0.5f, -1.1f)),
                ("JCB_WheelRL", new Vector3( 1.6f, 0.5f,  1.1f)),
                ("JCB_WheelRR", new Vector3( 1.6f, 0.5f, -1.1f)),
            };
            foreach (var (wname, pos) in jcbWheels)
            {
                var w = MakePrimitive(wname, PrimitiveType.Cylinder, pos,
                                     new Vector3(1.0f, 0.5f, 1.0f), DarkGrey);
                w.transform.localEulerAngles = new Vector3(0, 0, 90);
            }
        }

        // ------------------------------------------------------------------ //
        //  EXCAVATOR                                                           //
        // ------------------------------------------------------------------ //
        private void BuildExcavator()
        {
            // Tracked Base
            MakePrimitive("Excavator_TrackedBase", PrimitiveType.Cube,
                          new Vector3(0, 0.5f, 0), new Vector3(4f, 1f, 2.5f), OrangeExc);

            // Cabin
            MakePrimitive("Excavator_Cabin", PrimitiveType.Cube,
                          new Vector3(0, 2.0f, 0), new Vector3(2f, 2f, 2f), OrangeExc);

            // --- Boom + Stick + Bucket (each smaller) ---
            var armRoot = MakeEmpty("Excavator_Arm", new Vector3(1.2f, 2.5f, 0));

            MakePrimitive("Excavator_Boom", PrimitiveType.Cube,
                          new Vector3(0.8f, 0, 0), new Vector3(2.0f, 0.3f, 0.3f), DarkGrey, armRoot.transform);

            MakePrimitive("Excavator_Stick", PrimitiveType.Cube,
                          new Vector3(2.0f, -0.4f, 0), new Vector3(1.4f, 0.25f, 0.25f), DarkGrey, armRoot.transform);

            MakePrimitive("Excavator_Bucket", PrimitiveType.Cube,
                          new Vector3(2.9f, -0.9f, 0), new Vector3(0.8f, 0.15f, 0.9f), BodyGrey, armRoot.transform);

            // Track decals (flat cubes on each side)
            MakePrimitive("Excavator_TrackL", PrimitiveType.Cube,
                          new Vector3(0, 0.2f, 1.4f), new Vector3(4.2f, 0.6f, 0.4f), DarkGrey);
            MakePrimitive("Excavator_TrackR", PrimitiveType.Cube,
                          new Vector3(0, 0.2f, -1.4f), new Vector3(4.2f, 0.6f, 0.4f), DarkGrey);
        }

        // ------------------------------------------------------------------ //
        //  HARVESTER                                                           //
        // ------------------------------------------------------------------ //
        private void BuildHarvester()
        {
            // Cab
            MakePrimitive("Harvester_Cab", PrimitiveType.Cube,
                          new Vector3(0, 1.5f, 0), new Vector3(3f, 3f, 2.5f), GreenHarv);

            // Header Bar (front cutting bar)
            MakePrimitive("Harvester_Header", PrimitiveType.Cube,
                          new Vector3(-2.5f, 0.5f, 0), new Vector3(6f, 0.5f, 1.5f), DarkGrey);

            // Grain Tank
            MakePrimitive("Harvester_GrainTank", PrimitiveType.Cube,
                          new Vector3(1.2f, 2.5f, 0), new Vector3(2f, 3f, 2f), GreenHarv);

            // Rear Unload Auger
            var auger = MakePrimitive("Harvester_UnloadAuger", PrimitiveType.Cylinder,
                          new Vector3(2.5f, 3.5f, 0), new Vector3(0.3f, 1.5f, 0.3f), DarkGrey);
            auger.transform.localEulerAngles = new Vector3(0, 0, 45);

            // Wheels
            var harvWheels = new (string name, Vector3 pos, Vector3 scale)[]
            {
                ("Harvester_WheelFL", new Vector3(-1.4f, 0.7f,  1.4f), new Vector3(0.90f, 0.50f, 0.90f)),
                ("Harvester_WheelFR", new Vector3(-1.4f, 0.7f, -1.4f), new Vector3(0.90f, 0.50f, 0.90f)),
                ("Harvester_WheelRL", new Vector3( 1.6f, 1.0f,  1.4f), new Vector3(1.30f, 0.65f, 1.30f)),
                ("Harvester_WheelRR", new Vector3( 1.6f, 1.0f, -1.4f), new Vector3(1.30f, 0.65f, 1.30f)),
            };
            foreach (var (wname, pos, sc) in harvWheels)
            {
                var w = MakePrimitive(wname, PrimitiveType.Cylinder, pos, sc, DarkGrey);
                w.transform.localEulerAngles = new Vector3(0, 0, 90);
            }
        }
    }
}
