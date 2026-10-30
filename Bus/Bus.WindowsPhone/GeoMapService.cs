using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Devices.Geolocation;

namespace HKBusWP
{
    public static class GeoMapService
    {
        private static readonly Dictionary<string, List<BasicGeoposition>> _osrmRoadCache = new Dictionary<string, List<BasicGeoposition>>();

        // Real-world intermediate railway curve waypoints between stations so MTR lines follow real-life tunnels, bridges & coastlines
        private static readonly Dictionary<string, double[,]> _railSegmentWaypoints = new Dictionary<string, double[,]>
        {
            // East Rail Line (Cross-Harbour Tunnel, Beacon Hill Tunnel, Tolo Harbour Coastline, Fanling curve, Lok Ma Chau spur)
            { "EXC_HUH", new double[,] { { 22.2865, 114.1785 }, { 22.2950, 114.1812 } } },
            { "KOT_TAW", new double[,] { { 22.3480, 114.1745 }, { 22.3620, 114.1755 } } },
            { "UNI_TAP", new double[,] { { 22.4235, 114.2055 }, { 22.4335, 114.1920 }, { 22.4405, 114.1810 } } },
            { "TWO_FAN", new double[,] { { 22.4635, 114.1525 }, { 22.4785, 114.1455 } } },
            { "SHS_LOW", new double[,] { { 22.5155, 114.1215 } } },
            { "SHS_LMC", new double[,] { { 22.5065, 114.1085 }, { 22.5110, 114.0850 } } },

            // Tuen Ma Line (Tai Lam Tunnel, Tsuen Wan West coast, Lion Rock / Hin Keng Tunnel)
            { "HIK_DIH", new double[,] { { 22.3515, 114.1855 } } },
            { "MEF_TWW", new double[,] { { 22.3525, 114.1225 } } },
            { "TWW_KSR", new double[,] { { 22.3885, 114.0950 }, { 22.4125, 114.0750 } } },
            { "TIS_SIH", new double[,] { { 22.4350, 113.9910 } } },

            // Tung Chung Line & Airport Express (Western Harbour Crossing, Tsing Ma Bridge, North Lantau Coast)
            { "HOK_KOW", new double[,] { { 22.2950, 114.1575 } } },
            { "LAK_TSY", new double[,] { { 22.3540, 114.1165 } } },
            { "TSY_SUN", new double[,] { { 22.3515, 114.0760 }, { 22.3440, 114.0540 } } },
            { "SUN_TUC", new double[,] { { 22.3180, 114.0010 }, { 22.3015, 113.9710 } } },
            { "SUN_AIR", new double[,] { { 22.3180, 114.0010 }, { 22.3015, 113.9710 }, { 22.3025, 113.9400 } } },

            // Tseung Kwan O Line (Eastern Harbour Crossing, LOHAS Park Spur bifurcation)
            { "QUB_YAT", new double[,] { { 22.2930, 114.2230 } } },
            { "TKO_LHP", new double[,] { { 22.3030, 114.2660 } } },

            // Tsuen Wan Line & South Island Line
            { "ADM_TST", new double[,] { { 22.2880, 114.1695 } } },
            { "ADM_OCP", new double[,] { { 22.2640, 114.1715 } } }
        };

        public static void ClearCache()
        {
            _osrmRoadCache.Clear();
        }

        public static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            double r = 6371000.0;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
            return r * (2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a)));
        }

        public static List<KnownHkStop> FindNearestStopsWithinRadius(double userLat, double userLon, int radiusMeters, int maxStopsToQuery)
        {
            List<KnownHkStop> masterList = BusRouteDatabase.GetKmbStopsListInternal();
            List<KeyValuePair<double, KnownHkStop>> withinRadius = new List<KeyValuePair<double, KnownHkStop>>();
            KnownHkStop singleClosest = null;
            double minDistance = double.MaxValue;

            for (int i = 0; i < masterList.Count; i++)
            {
                KnownHkStop s = masterList[i];
                double dist = CalculateDistanceMeters(userLat, userLon, s.Lat, s.Lon);
                if (dist < minDistance) { minDistance = dist; singleClosest = s; }
                if (dist <= radiusMeters) withinRadius.Add(new KeyValuePair<double, KnownHkStop>(dist, s));
            }

            withinRadius.Sort((a, b) => a.Key.CompareTo(b.Key));
            List<KnownHkStop> results = new List<KnownHkStop>();
            int count = Math.Min(maxStopsToQuery, withinRadius.Count);
            for (int i = 0; i < count; i++) results.Add(withinRadius[i].Value);
            if (results.Count == 0 && singleClosest != null) results.Add(singleClosest);
            return results;
        }

        public static int SelectNearestOrFirstStopIndex(List<BusStopItem> stops, string preferredStopId)
        {
            if (stops == null || stops.Count == 0) return 0;

            if (!string.IsNullOrEmpty(preferredStopId))
            {
                for (int i = 0; i < stops.Count; i++)
                {
                    if (stops[i].StopId == preferredStopId) return i;
                }
            }

            double userLat = ThemeManager.LastKnownLatitude;
            double userLon = ThemeManager.LastKnownLongitude;
            double minDist = double.MaxValue;
            int bestIdx = 0;

            for (int i = 0; i < stops.Count; i++)
            {
                if (stops[i].Latitude > 20.0)
                {
                    double d = CalculateDistanceMeters(userLat, userLon, stops[i].Latitude, stops[i].Longitude);
                    if (d < minDist) { minDist = d; bestIdx = i; }
                }
            }

            return (minDist > 900.0) ? 0 : bestIdx;
        }

        // Builds authentic multi-branch railway polylines so EAL (Lo Wu / Lok Ma Chau / Racecourse) and TKL (Po Lam / LOHAS Park) plot with true Y-branches & track curves
        public static List<List<BasicGeoposition>> BuildRailwayPolylinesForRoute(string routeId, List<BusStopItem> stops)
        {
            List<List<BasicGeoposition>> branches = new List<List<BasicGeoposition>>();
            if (stops == null || stops.Count == 0) return branches;

            if (!string.IsNullOrEmpty(routeId) && routeId.StartsWith("MTR_EAL"))
            {
                // 1. EAL Main Trunk: ADM -> LOW (via FOT)
                branches.Add(BuildStationChainCurve(new string[] { "ADM", "EXC", "HUH", "MKK", "KOT", "TAW", "SHT", "FOT", "UNI", "TAP", "TWO", "FAN", "SHS", "LOW" }));
                // 2. EAL Lok Ma Chau Spur Branch: SHS -> LMC
                branches.Add(BuildStationChainCurve(new string[] { "SHS", "LMC" }));
                // 3. EAL Racecourse Spur Branch: SHT -> RAC -> UNI
                branches.Add(BuildStationChainCurve(new string[] { "SHT", "RAC", "UNI" }));
                return branches;
            }

            if (!string.IsNullOrEmpty(routeId) && routeId.StartsWith("MTR_TKL"))
            {
                // 1. TKL Main Trunk: NOP -> POA
                branches.Add(BuildStationChainCurve(new string[] { "NOP", "QUB", "YAT", "TIK", "TKO", "HAH", "POA" }));
                // 2. TKL LOHAS Park Spur Branch: TKO -> LHP
                branches.Add(BuildStationChainCurve(new string[] { "TKO", "LHP" }));
                return branches;
            }

            if (!string.IsNullOrEmpty(routeId) && routeId.StartsWith("MTR_"))
            {
                List<string> codes = new List<string>();
                for (int i = 0; i < stops.Count; i++) codes.Add(stops[i].StopId);
                branches.Add(BuildStationChainCurve(codes.ToArray()));
                return branches;
            }

            // Light Rail
            List<BasicGeoposition> raw = new List<BasicGeoposition>();
            for (int i = 0; i < stops.Count; i++)
            {
                if (stops[i].Latitude > 20.0)
                {
                    raw.Add(new BasicGeoposition { Latitude = stops[i].Latitude, Longitude = stops[i].Longitude });
                }
            }
            branches.Add(BuildSmoothTrackGeometry(raw));
            return branches;
        }

        private static List<BasicGeoposition> BuildStationChainCurve(string[] stationCodes)
        {
            List<BasicGeoposition> controlPoints = new List<BasicGeoposition>();

            for (int i = 0; i < stationCodes.Length; i++)
            {
                string code = stationCodes[i];
                double lat, lon;
                if (RailwayService.TryGetStationCoords(code, out lat, out lon))
                {
                    controlPoints.Add(new BasicGeoposition { Latitude = lat, Longitude = lon });
                }

                if (i < stationCodes.Length - 1)
                {
                    string nextCode = stationCodes[i + 1];
                    string fwdKey = code + "_" + nextCode;
                    string revKey = nextCode + "_" + code;

                    if (_railSegmentWaypoints.ContainsKey(fwdKey))
                    {
                        double[,] wps = _railSegmentWaypoints[fwdKey];
                        for (int w = 0; w < wps.GetLength(0); w++)
                        {
                            controlPoints.Add(new BasicGeoposition { Latitude = wps[w, 0], Longitude = wps[w, 1] });
                        }
                    }
                    else if (_railSegmentWaypoints.ContainsKey(revKey))
                    {
                        double[,] wps = _railSegmentWaypoints[revKey];
                        for (int w = wps.GetLength(0) - 1; w >= 0; w--)
                        {
                            controlPoints.Add(new BasicGeoposition { Latitude = wps[w, 0], Longitude = wps[w, 1] });
                        }
                    }
                }
            }

            return BuildSmoothTrackGeometry(controlPoints);
        }

        public static async Task<List<BasicGeoposition>> FetchRouteMapGeometryAsync(string routeId, List<BusStopItem> stops)
        {
            List<BasicGeoposition> fallback = new List<BasicGeoposition>();
            if (stops == null || stops.Count == 0) return fallback;

            for (int i = 0; i < stops.Count; i++)
            {
                if (stops[i].Latitude > 20.0)
                {
                    fallback.Add(new BasicGeoposition { Latitude = stops[i].Latitude, Longitude = stops[i].Longitude });
                }
            }

            if (fallback.Count < 2) return fallback;

            if (!string.IsNullOrEmpty(routeId) && (routeId.StartsWith("MTR_") || routeId.StartsWith("LRT_")))
            {
                List<List<BasicGeoposition>> railBranches = BuildRailwayPolylinesForRoute(routeId, stops);
                return (railBranches.Count > 0) ? railBranches[0] : BuildSmoothTrackGeometry(fallback);
            }

            if (!string.IsNullOrEmpty(routeId) &&
                (routeId.StartsWith("SF_") || routeId.StartsWith("SUN_") || routeId.StartsWith("HKKF_") || routeId.StartsWith("FF_")))
            {
                return TramAndFerryService.BuildMaritimeFerryGeometry(routeId, fallback);
            }

            if (!string.IsNullOrEmpty(routeId) && _osrmRoadCache.ContainsKey(routeId))
            {
                return _osrmRoadCache[routeId];
            }

            try
            {
                List<BasicGeoposition> waypoints = new List<BasicGeoposition>();
                int maxWaypoints = (!string.IsNullOrEmpty(routeId) && routeId.StartsWith("TRAM_")) ? 22 : 16;
                int step = Math.Max(1, fallback.Count / maxWaypoints);
                for (int i = 0; i < fallback.Count; i += step) waypoints.Add(fallback[i]);

                BasicGeoposition lastPt = fallback[fallback.Count - 1];
                if (waypoints[waypoints.Count - 1].Latitude != lastPt.Latitude || waypoints[waypoints.Count - 1].Longitude != lastPt.Longitude)
                {
                    waypoints.Add(lastPt);
                }

                List<string> coordPairs = new List<string>();
                for (int i = 0; i < waypoints.Count; i++)
                {
                    coordPairs.Add(string.Format(CultureInfo.InvariantCulture, "{0:F5},{1:F5}", waypoints[i].Longitude, waypoints[i].Latitude));
                }

                string url = string.Format("http://router.project-osrm.org/route/v1/driving/{0}?overview=full&geometries=geojson",
                    string.Join(";", coordPairs));

                string jsonText = await BusRouteDatabase.HttpGetStringAsync(url, false);
                if (!string.IsNullOrEmpty(jsonText))
                {
                    JsonObject root = JsonObject.Parse(jsonText);
                    if (root.ContainsKey("routes") && root["routes"].ValueType == JsonValueType.Array)
                    {
                        JsonArray routesArr = root["routes"].GetArray();
                        if (routesArr.Count > 0)
                        {
                            JsonObject r0 = routesArr[0].GetObject();
                            if (r0.ContainsKey("geometry") && r0["geometry"].ValueType == JsonValueType.Object)
                            {
                                JsonObject geom = r0["geometry"].GetObject();
                                if (geom.ContainsKey("coordinates") && geom["coordinates"].ValueType == JsonValueType.Array)
                                {
                                    JsonArray coords = geom["coordinates"].GetArray();
                                    List<BasicGeoposition> roadPoints = new List<BasicGeoposition>();
                                    for (int i = 0; i < coords.Count; i++)
                                    {
                                        JsonArray pair = coords[i].GetArray();
                                        if (pair.Count >= 2)
                                        {
                                            roadPoints.Add(new BasicGeoposition { Latitude = pair[1].GetNumber(), Longitude = pair[0].GetNumber() });
                                        }
                                    }

                                    if (roadPoints.Count >= 2)
                                    {
                                        if (!string.IsNullOrEmpty(routeId)) _osrmRoadCache[routeId] = roadPoints;
                                        return roadPoints;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return fallback;
        }

        public static List<BasicGeoposition> BuildSmoothTrackGeometry(List<BasicGeoposition> stations)
        {
            if (stations == null || stations.Count < 2) return stations;
            List<BasicGeoposition> track = new List<BasicGeoposition>();

            for (int i = 0; i < stations.Count - 1; i++)
            {
                BasicGeoposition p0 = (i == 0) ? stations[0] : stations[i - 1];
                BasicGeoposition p1 = stations[i];
                BasicGeoposition p2 = stations[i + 1];
                BasicGeoposition p3 = (i + 2 < stations.Count) ? stations[i + 2] : stations[i + 1];

                for (int s = 0; s < 6; s++)
                {
                    double t = s / 6.0;
                    double t2 = t * t;
                    double t3 = t2 * t;

                    double lat = 0.5 * ((2.0 * p1.Latitude) +
                        (-p0.Latitude + p2.Latitude) * t +
                        (2.0 * p0.Latitude - 5.0 * p1.Latitude + 4.0 * p2.Latitude - p3.Latitude) * t2 +
                        (-p0.Latitude + 3.0 * p1.Latitude - 3.0 * p2.Latitude + p3.Latitude) * t3);

                    double lon = 0.5 * ((2.0 * p1.Longitude) +
                        (-p0.Longitude + p2.Longitude) * t +
                        (2.0 * p0.Longitude - 5.0 * p1.Longitude + 4.0 * p2.Longitude - p3.Longitude) * t2 +
                        (-p0.Longitude + 3.0 * p1.Longitude - 3.0 * p2.Longitude + p3.Longitude) * t3);

                    track.Add(new BasicGeoposition { Latitude = lat, Longitude = lon });
                }
            }
            track.Add(stations[stations.Count - 1]);
            return track;
        }
    }
}