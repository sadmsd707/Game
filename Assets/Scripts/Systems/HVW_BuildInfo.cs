using System;
using UnityEngine;

namespace HeavyVehiclesWorld.Systems
{
    /// <summary>
    /// Compile-time & runtime build stamp and version tracking.
    /// Loaded dynamically from Assets/Resources/hvw_build_info.json or defaults.
    /// Updated by HVW_BatchBuild before every APK build.
    /// </summary>
    public static class HVW_BuildInfo
    {
        [Serializable]
        public class BuildData
        {
            public int buildNumber = 2;
            public string buildDate = "2026-11-20 01:45";
            public string versionString = "v1.36-b2";
        }

        private static BuildData _data;

        public static void EnsureLoaded()
        {
            if (_data != null) return;
            var json = Resources.Load<TextAsset>("hvw_build_info");
            if (json != null && !string.IsNullOrEmpty(json.text))
            {
                try
                {
                    _data = JsonUtility.FromJson<BuildData>(json.text);
                }
                catch
                {
                    _data = new BuildData();
                }
            }
            if (_data == null)
            {
                _data = new BuildData();
            }
        }

        public static int BuildNumber
        {
            get
            {
                EnsureLoaded();
                return _data.buildNumber;
            }
        }

        public static string BuildDate
        {
            get
            {
                EnsureLoaded();
                return _data.buildDate;
            }
        }

        public static string VersionString
        {
            get
            {
                EnsureLoaded();
                return _data.versionString;
            }
        }

        public static string FullStamp => $"BUILD #{BuildNumber} | {BuildDate} | {VersionString}";
    }
}
