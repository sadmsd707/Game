using System.Collections.Generic;
using UnityEngine;
using HeavyVehiclesWorld.Vehicles;

namespace HeavyVehiclesWorld.World
{
    [CreateAssetMenu(fileName = "WorldMapData", menuName = "HVW/World Map Data")]
    public class WorldMapData : ScriptableObject
    {
        [Header("World Size")]
        public float worldSize     = 4000f;   // 4 km × 4 km
        public float terrainHeight = 600f;

        [Header("Zone Definitions")]
        public ZoneDefinition[] zones;

        [Header("Road Network")]
        public RoadSegment[] roads;

        [Header("Vehicle Spawn Points")]
        public VehicleSpawnConfig[] vehicleSpawns;

        [Header("Mission Points")]
        public MissionPoint[] missionPoints;

        /// <summary>Returns the default 4km world layout with all 7 zones pre-configured.</summary>
        public static WorldMapData CreateDefaultLayout()
        {
            var data  = CreateInstance<WorldMapData>();
            data.zones = new ZoneDefinition[]
            {
                // ── Zone layout (approximate 4km×4km, origin at world centre) ──
                // Farmland: SW quadrant
                new ZoneDefinition { zoneName="Farmland",        zoneType=WorldZone.Farmland,        bounds=new Rect(-2000,-2000,1600,1600), minimapColour=new Color(0.6f,0.85f,0.3f), fogDensity=0.002f, ambientIntensity=1.0f },
                // Construction: NW quadrant upper
                new ZoneDefinition { zoneName="Construction",    zoneType=WorldZone.ConstructionSite, bounds=new Rect(-2000, 400, 900,800),  minimapColour=new Color(0.8f,0.6f,0.2f), fogDensity=0.004f, ambientIntensity=0.9f },
                // Quarry: N centre
                new ZoneDefinition { zoneName="Quarry",          zoneType=WorldZone.Quarry,           bounds=new Rect(-400,  600,1200,900),  minimapColour=new Color(0.5f,0.4f,0.3f), fogDensity=0.005f, ambientIntensity=0.85f },
                // Highway: horizontal strip through centre
                new ZoneDefinition { zoneName="Highway",         zoneType=WorldZone.Highway,          bounds=new Rect(-2000,-200,4000,400),  minimapColour=new Color(0.3f,0.3f,0.3f), fogDensity=0.001f, ambientIntensity=1.1f },
                // Village: SE
                new ZoneDefinition { zoneName="Village",         zoneType=WorldZone.Village,          bounds=new Rect( 800,-1200, 800,800),  minimapColour=new Color(0.9f,0.75f,0.5f), fogDensity=0.003f, ambientIntensity=0.95f },
                // Forest: NE
                new ZoneDefinition { zoneName="Forest",          zoneType=WorldZone.Forest,           bounds=new Rect( 600, 200,1400,1200), minimapColour=new Color(0.2f,0.55f,0.1f), fogDensity=0.008f, ambientIntensity=0.7f },
                // River: strip S→N on east side
                new ZoneDefinition { zoneName="River",           zoneType=WorldZone.River,            bounds=new Rect( 350,-2000, 300,4000), minimapColour=new Color(0.2f,0.4f,0.9f), fogDensity=0.006f, ambientIntensity=0.9f },
            };

            // Vehicle spawns – one per vehicle type, in appropriate zone
            data.vehicleSpawns = new VehicleSpawnConfig[]
            {
                new VehicleSpawnConfig { vehicleType=VehicleType.Car,       position=new Vector3( 100,  0,  -300), eulerRotation=Vector3.zero,         label="Car" },
                new VehicleSpawnConfig { vehicleType=VehicleType.Truck,     position=new Vector3(-300,  0,  -400), eulerRotation=new Vector3(0,90,0),  label="Truck" },
                new VehicleSpawnConfig { vehicleType=VehicleType.Tractor,   position=new Vector3(-800,  0, -1000), eulerRotation=Vector3.zero,         label="Tractor" },
                new VehicleSpawnConfig { vehicleType=VehicleType.JCB,       position=new Vector3(-600,  0,   700), eulerRotation=new Vector3(0,180,0), label="JCB" },
                new VehicleSpawnConfig { vehicleType=VehicleType.Excavator, position=new Vector3( 200,  0,   900), eulerRotation=Vector3.zero,         label="Excavator" },
                new VehicleSpawnConfig { vehicleType=VehicleType.Harvester, position=new Vector3(-1200, 0, -1200), eulerRotation=new Vector3(0,45,0),  label="Harvester" },
            };

            // Default empty road list – populate in the editor via RoadNetworkGenerator
            data.roads = new RoadSegment[0];

            // Default empty mission points – populated at runtime by MissionManager
            data.missionPoints = new MissionPoint[0];

            return data;
        }
    }

    [System.Serializable]
    public class ZoneDefinition
    {
        public string      zoneName;
        public WorldZone   zoneType;
        public Rect        bounds;         // in world XZ space (origin at world centre)
        public Color       minimapColour;
        public float       fogDensity;
        public float       ambientIntensity;
    }

    [System.Serializable]
    public class RoadSegment
    {
        public Vector3[] waypoints;        // path of the road
        public float     width = 8f;
        public bool      isHighway;
    }

    [System.Serializable]
    public class VehicleSpawnConfig
    {
        public VehicleType vehicleType;    // from HeavyVehiclesWorld.Vehicles namespace
        public Vector3     position;
        public Vector3     eulerRotation;
        public string      label;          // shown on minimap
    }

    [System.Serializable]
    public class MissionPoint
    {
        public string   missionId;
        public string   description;
        public Vector3  position;
        public Sprite   icon;
    }
}
