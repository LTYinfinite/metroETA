using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.Devices.Geolocation;

namespace HKBusWP
{
    public static class TramAndFerryService
    {
        // All 12 Official HK Tramways Destination Routes (6 Eastbound + 6 Westbound)
        // Columns: RouteId, Bound ("E"/"W"), OrigTc, OrigEn, DestTc, DestEn, StartStopIdx, EndStopIdx, IncludesHappyValley ("1"/"0")
        private static readonly string[,] _tramRoutesTable = new string[,]
        {
            // Eastbound (電車 (東行))
            { "TRAM_E_KT_SKW", "E", "堅尼地城", "Kennedy Town", "筲箕灣", "Shau Kei Wan", "0", "35", "0" },
            { "TRAM_E_KT_HV",  "E", "堅尼地城", "Kennedy Town", "跑馬地", "Happy Valley", "0", "19", "1" },
            { "TRAM_E_KT_NP",  "E", "堅尼地城", "Kennedy Town", "北角", "North Point", "0", "25", "0" },
            { "TRAM_E_WM_SKW", "E", "上環 (西港城)", "Western Market", "筲箕灣", "Shau Kei Wan", "7", "35", "0" },
            { "TRAM_E_HV_SKW", "E", "跑馬地", "Happy Valley", "筲箕灣", "Shau Kei Wan", "19", "35", "1" },
            { "TRAM_E_WD_CWB", "E", "屈地街電車廠", "Whitty St Depot", "銅鑼灣", "Causeway Bay", "3", "20", "0" },

            // Westbound (電車 (西行))
            { "TRAM_W_SKW_KT", "W", "筲箕灣", "Shau Kei Wan", "堅尼地城", "Kennedy Town", "35", "0", "0" },
            { "TRAM_W_SKW_WM", "W", "筲箕灣", "Shau Kei Wan", "上環 (西港城)", "Western Market", "35", "7", "0" },
            { "TRAM_W_SKW_HV", "W", "筲箕灣", "Shau Kei Wan", "跑馬地", "Happy Valley", "35", "19", "1" },
            { "TRAM_W_HV_KT",  "W", "跑馬地", "Happy Valley", "堅尼地城", "Kennedy Town", "19", "0", "1" },
            { "TRAM_W_NP_KT",  "W", "北角", "North Point", "堅尼地城", "Kennedy Town", "25", "0", "0" },
            { "TRAM_W_CWB_WD", "W", "銅鑼灣", "Causeway Bay", "屈地街電車廠", "Whitty St Depot", "20", "3", "0" }
        };

        private static readonly string[,] _eastboundRoadTrack = new string[,]
        {
            { "01E", "堅尼地城總站", "Kennedy Town Terminus", "22.2818", "114.1282" },
            { "03E", "北街", "North Street", "22.2830", "114.1298" },
            { "05E", "山道", "Hill Road", "22.2852", "114.1355" },
            { "09E", "屈地街電車廠", "Whitty Street Depot", "22.2868", "114.1392" },
            { "11E", "水街", "Water Street", "22.2872", "114.1415" },
            { "15E", "東邊街", "Eastern Street", "22.2875", "114.1445" },
            { "19E", "修打蘭街", "Sutherland Street", "22.2874", "114.1475" },
            { "21E", "上環 (西港城) 總站", "Western Market Terminus", "22.2872", "114.1505" },
            { "23E", "港澳碼頭", "Macau Ferry Terminal", "22.2865", "114.1525" },
            { "25E", "租庇利街", "Jubilee Street", "22.2845", "114.1552" },
            { "27E", "畢打街 (中環)", "Pedder Street (Central)", "22.2819", "114.1578" },
            { "31E", "銀行街", "Bank Street", "22.2801", "114.1598" },
            { "33E", "紅棉道", "Cotton Tree Drive", "22.2794", "114.1622" },
            { "35E", "金鐘港鐵站", "Admiralty MTR Station", "22.2791", "114.1652" },
            { "39E", "軍器廠街", "Arsenal Street", "22.2778", "114.1695" },
            { "41E", "分域街", "Fenwick Street", "22.2776", "114.1712" },
            { "43E", "柯布連道 (灣仔)", "O'Brien Road (Wan Chai)", "22.2775", "114.1731" },
            { "45E", "菲林明道", "Fleming Road", "22.2778", "114.1748" },
            { "47E", "杜老誌道", "Tonnochy Road", "22.2782", "114.1768" },
            { "49E", "堅拿道西 (時代廣場)", "Canal Road West (Times Sq)", "22.2788", "114.1812" },
            { "53E", "富明街 / 百德新街 (銅鑼灣)", "Causeway Bay (Paterson St)", "22.2798", "114.1848" },
            { "55E", "維多利亞公園", "Victoria Park", "22.2812", "114.1892" },
            { "57E", "留仙街 (天后)", "Lau Sin Street (Tin Hau)", "22.2835", "114.1922" },
            { "61E", "炮台山", "Fortress Hill", "22.2881", "114.1938" },
            { "65E", "北角道", "North Point Road", "22.2905", "114.1975" },
            { "67E", "春秧街 / 北角總站", "Chun Yeung St / North Point", "22.2912", "114.2005" },
            { "71E", "書局街", "Shu Kuk Street", "22.2915", "114.2025" },
            { "73E", "健康西街", "Healthy Street West", "22.2918", "114.2058" },
            { "77E", "模範邨", "Model Housing Estate", "22.2902", "114.2082" },
            { "79E", "芬尼街 (鰂魚涌)", "Finnie Street (Quarry Bay)", "22.2878", "114.2102" },
            { "81E", "太古坊", "Taikoo Place", "22.2862", "114.2128" },
            { "83E", "太古康怡廣場", "Kornhill Plaza (Tai Koo)", "22.2845", "114.2162" },
            { "87E", "太古城道", "Taikoo Shing Road", "22.2835", "114.2195" },
            { "89E", "太安街 (西灣河)", "Tai On Street (Sai Wan Ho)", "22.2825", "114.2222" },
            { "93E", "西灣河電車廠", "Sai Wan Ho Depot", "22.2808", "114.2252" },
            { "99E", "筲箕灣總站", "Shau Kei Wan Terminus", "22.2788", "114.2289" }
        };

        public static List<NearbyRouteItem> GetAllTramRoutes()
        {
            List<NearbyRouteItem> list = new List<NearbyRouteItem>();
            DateTime now = DateTime.Now;

            for (int i = 0; i < _tramRoutesTable.GetLength(0); i++)
            {
                string rId = _tramRoutesTable[i, 0];
                string bound = _tramRoutesTable[i, 1];
                string oTc = _tramRoutesTable[i, 2];
                string oEn = _tramRoutesTable[i, 3];
                string dTc = _tramRoutesTable[i, 4];
                string dEn = _tramRoutesTable[i, 5];
                bool isEast = (bound == "E");

                int f1 = 2 + (i % 4);
                int f2 = f1 + 4;
                int f3 = f2 + 5;

                list.Add(new NearbyRouteItem
                {
                    RouteId = rId,
                    RouteNumber = "TRAM",
                    Direction = isEast ? "O" : "I",
                    ServiceType = bound,
                    CompanyTc = isEast ? "電車 (東行)" : "電車 (西行)",
                    CompanyEn = isEast ? "tram (eastbound)" : "tram (westbound)",
                    TransportType = "電車",
                    HasOppositeDirection = true,
                    OriginTc = oTc,
                    OriginEn = oEn,
                    DestinationTc = dTc,
                    DestinationEn = dEn,
                    StopNameTc = oTc + "電車站",
                    StopNameEn = oEn + " Tram Stop",
                    FareText = "($3.3, $1.5)",
                    FirstArrivalTime = now.AddMinutes(f1),
                    SecondArrivalTime = now.AddMinutes(f2),
                    ThirdArrivalTime = now.AddMinutes(f3)
                });
            }

            return list;
        }

        public static void PopulateTramwayStopsAndEtas(NearbyRouteItem route)
        {
            bool isEast = (route.ServiceType == "E" || route.Direction == "O");
            bool en = ThemeManager.IsEnglish;

            int startIdx = isEast ? 0 : (_eastboundRoadTrack.GetLength(0) - 1);
            int endIdx = isEast ? (_eastboundRoadTrack.GetLength(0) - 1) : 0;
            bool includeHv = false;

            for (int r = 0; r < _tramRoutesTable.GetLength(0); r++)
            {
                if (_tramRoutesTable[r, 0] == route.RouteId)
                {
                    int.TryParse(_tramRoutesTable[r, 6], out startIdx);
                    int.TryParse(_tramRoutesTable[r, 7], out endIdx);
                    includeHv = (_tramRoutesTable[r, 8] == "1");
                    break;
                }
            }

            List<BusStopItem> stops = new List<BusStopItem>();
            int step = (startIdx <= endIdx) ? 1 : -1;
            int seq = 1;

            List<string> destRmks = new List<string>
            {
                en ? ("to " + route.DestinationEn) : ("往 " + route.DestinationTc),
                en ? ("to " + route.DestinationEn) : ("往 " + route.DestinationTc),
                en ? ("to " + route.DestinationEn) : ("往 " + route.DestinationTc)
            };

            // If origin is Happy Valley, prepend Happy Valley Terminus
            if (includeHv && startIdx == 19)
            {
                stops.Add(new BusStopItem
                {
                    RouteId = route.RouteId,
                    StopId = "HV01",
                    Sequence = seq++,
                    StopNameTc = "跑馬地總站 (HV01)",
                    StopNameEn = "Happy Valley Terminus (HV01)",
                    NormalFare = "$3.3",
                    JoyYouFare = "$1.5",
                    HasTimetable = true,
                    Latitude = 22.2695,
                    Longitude = 114.1845,
                    LiveEtaMinutes = new List<int> { 2, 6, 11 },
                    LiveEtaRemarks = destRmks,
                    IsFavouriteStop = ThemeManager.IsFavourite(route.RouteId, "HV01", 1),
                    IsExpanded = false
                });
            }

            for (int idx = startIdx; ; idx += step)
            {
                string code = isEast ? _eastboundRoadTrack[idx, 0] : _eastboundRoadTrack[idx, 0].Replace("E", "W");
                stops.Add(new BusStopItem
                {
                    RouteId = route.RouteId,
                    StopId = code,
                    Sequence = seq,
                    StopNameTc = string.Format("{0} ({1})", _eastboundRoadTrack[idx, 1], code),
                    StopNameEn = string.Format("{0} ({1})", _eastboundRoadTrack[idx, 2], code),
                    NormalFare = "$3.3",
                    JoyYouFare = "$1.5",
                    HasTimetable = true,
                    Latitude = double.Parse(_eastboundRoadTrack[idx, 3], CultureInfo.InvariantCulture),
                    Longitude = double.Parse(_eastboundRoadTrack[idx, 4], CultureInfo.InvariantCulture),
                    LiveEtaMinutes = new List<int> { 2, 5, 9 },
                    LiveEtaRemarks = destRmks,
                    IsFavouriteStop = ThemeManager.IsFavourite(route.RouteId, code, seq),
                    IsExpanded = false
                });
                seq++;

                if (idx == endIdx) break;
            }

            // If destination is Happy Valley, append Happy Valley Terminus
            if (includeHv && endIdx == 19)
            {
                stops.Add(new BusStopItem
                {
                    RouteId = route.RouteId,
                    StopId = "HV02",
                    Sequence = seq,
                    StopNameTc = "跑馬地總站 (HV02)",
                    StopNameEn = "Happy Valley Terminus (HV02)",
                    NormalFare = "$3.3",
                    JoyYouFare = "$1.5",
                    HasTimetable = true,
                    Latitude = 22.2695,
                    Longitude = 114.1845,
                    LiveEtaMinutes = new List<int> { 3, 7, 12 },
                    LiveEtaRemarks = destRmks,
                    IsFavouriteStop = ThemeManager.IsFavourite(route.RouteId, "HV02", seq),
                    IsExpanded = false
                });
            }

            if (stops.Count > 0)
            {
                int expandIdx = GeoMapService.SelectNearestOrFirstStopIndex(stops, route.NearestStopId);
                stops[expandIdx].IsExpanded = true;
                route.Stops = stops;
            }
        }

        // FERRIES & MARITIME SEA GEOMETRY
        public static List<NearbyRouteItem> GetAllFerryRoutes()
        {
            List<NearbyRouteItem> list = new List<NearbyRouteItem>();
            DateTime now = DateTime.Now;
            int minToNext10 = 10 - (now.Minute % 10);
            int minToNext20 = 20 - (now.Minute % 20);
            bool en = ThemeManager.IsEnglish;

            string[,] ferries = new string[,]
            {
                { "SF_TST_CEN", "天星", "STAR", "天星小輪", "star ferry", "尖沙咀", "Tsim Sha Tsui", "中環", "Central", "($5.0, $2.0)", "22.2938", "114.1688", "22.2868", "114.1615" },
                { "SF_CEN_TST", "天星", "STAR", "天星小輪", "star ferry", "中環", "Central", "尖沙咀", "Tsim Sha Tsui", "($5.0, $2.0)", "22.2868", "114.1615", "22.2938", "114.1688" },
                { "SF_TST_WAC", "天星", "STAR", "天星小輪", "star ferry", "尖沙咀", "Tsim Sha Tsui", "灣仔", "Wan Chai", "($5.0, $2.0)", "22.2938", "114.1688", "22.2838", "114.1758" },
                { "SF_WAC_TST", "天星", "STAR", "天星小輪", "star ferry", "灣仔", "Wan Chai", "尖沙咀", "Tsim Sha Tsui", "($5.0, $2.0)", "22.2838", "114.1758", "22.2938", "114.1688" },
                { "SUN_CEN_CC", "中環-長洲", "CEN-CC", "新渡輪", "sun ferry", "中環5號碼頭", "Central Pier 5", "長洲", "Cheung Chau", "($14.8, $3.0)", "22.2865", "114.1605", "22.2095", "114.0285" },
                { "SUN_CC_CEN", "長洲-中環", "CC-CEN", "新渡輪", "sun ferry", "長洲", "Cheung Chau", "中環5號碼頭", "Central Pier 5", "($14.8, $3.0)", "22.2095", "114.0285", "22.2865", "114.1605" },
                { "SUN_CEN_MW", "中環-梅窩", "CEN-MW", "新渡輪", "sun ferry", "中環6號碼頭", "Central Pier 6", "梅窩", "Mui Wo", "($17.2, $3.4)", "22.2870", "114.1598", "22.2645", "114.0015" },
                { "HKKF_CEN_YSW", "中環-榕樹灣", "CEN-YSW", "港九小輪", "hkkf", "中環4號碼頭", "Central Pier 4", "南丫島(榕樹灣)", "Yung Shue Wan (Lamma)", "($19.8, $4.0)", "22.2862", "114.1590", "22.2265", "114.1115" },
                { "HKKF_CEN_PC", "中環-坪洲", "CEN-PC", "港九小輪", "hkkf", "中環6號碼頭", "Central Pier 6", "坪洲", "Peng Chau", "($17.1, $3.4)", "22.2870", "114.1598", "22.2855", "114.0375" },
                { "FF_NP_HH", "北角-紅磡", "NP-HH", "新渡輪", "sun ferry", "北角碼頭", "North Point Pier", "紅磡碼頭", "Hung Hom Pier", "($8.5, $2.0)", "22.2935", "114.1995", "22.3005", "114.1885" },
                { "FF_NP_KC", "北角-九龍城", "NP-KC", "新渡輪", "sun ferry", "北角碼頭", "North Point Pier", "九龍城碼頭", "Kowloon City Pier", "($8.5, $2.0)", "22.2935", "114.1995", "22.3175", "114.1955" }
            };

            for (int i = 0; i < ferries.GetLength(0); i++)
            {
                bool isStar = ferries[i, 0].StartsWith("SF_");
                int f1 = isStar ? minToNext10 : minToNext20;
                int f2 = isStar ? (f1 + 8) : (f1 + 30);
                int f3 = isStar ? (f1 + 16) : (f1 + 60);

                list.Add(new NearbyRouteItem
                {
                    RouteId = ferries[i, 0],
                    RouteNumber = en ? ferries[i, 2] : ferries[i, 1],
                    Direction = "O",
                    ServiceType = ferries[i, 10] + "," + ferries[i, 11] + ";" + ferries[i, 12] + "," + ferries[i, 13],
                    CompanyTc = ferries[i, 3],
                    CompanyEn = ferries[i, 4],
                    TransportType = "渡輪",
                    OriginTc = ferries[i, 5],
                    OriginEn = ferries[i, 6],
                    DestinationTc = ferries[i, 7],
                    DestinationEn = ferries[i, 8],
                    StopNameTc = ferries[i, 5] + "渡輪碼頭",
                    StopNameEn = ferries[i, 6] + " Ferry Pier",
                    FareText = ferries[i, 9],
                    FirstArrivalTime = now.AddMinutes(f1),
                    SecondArrivalTime = now.AddMinutes(f2),
                    ThirdArrivalTime = (f3 <= 60) ? (DateTime?)now.AddMinutes(f3) : null
                });
            }

            return list;
        }

        public static void PopulateFerryPiersAndEtas(NearbyRouteItem route)
        {
            double oLat = 22.2938, oLon = 114.1688, dLat = 22.2868, dLon = 114.1615;
            if (!string.IsNullOrEmpty(route.ServiceType) && route.ServiceType.Contains(";"))
            {
                string[] ends = route.ServiceType.Split(';');
                string[] p1 = ends[0].Split(',');
                string[] p2 = ends[1].Split(',');
                if (p1.Length == 2 && p2.Length == 2)
                {
                    double.TryParse(p1[0], NumberStyles.Any, CultureInfo.InvariantCulture, out oLat);
                    double.TryParse(p1[1], NumberStyles.Any, CultureInfo.InvariantCulture, out oLon);
                    double.TryParse(p2[0], NumberStyles.Any, CultureInfo.InvariantCulture, out dLat);
                    double.TryParse(p2[1], NumberStyles.Any, CultureInfo.InvariantCulture, out dLon);
                }
            }

            List<BusStopItem> piers = new List<BusStopItem>
            {
                new BusStopItem
                {
                    RouteId = route.RouteId,
                    StopId = route.RouteId + "_P1",
                    Sequence = 1,
                    StopNameTc = route.OriginTc + "渡輪碼頭",
                    StopNameEn = route.OriginEn + " Ferry Pier",
                    NormalFare = "$5.0",
                    JoyYouFare = "$2.0",
                    HasTimetable = true,
                    Latitude = oLat,
                    Longitude = oLon,
                    LiveEtaMinutes = new List<int> { 6, 15, 25 },
                    LiveEtaRemarks = new List<string>
                    {
                        ThemeManager.Tr("普通渡輪", "ordinary ferry"),
                        ThemeManager.Tr("高速船", "fast ferry"),
                        ThemeManager.Tr("普通渡輪", "ordinary ferry")
                    },
                    IsFavouriteStop = ThemeManager.IsFavourite(route.RouteId, route.RouteId + "_P1", 1),
                    IsExpanded = true
                },
                new BusStopItem
                {
                    RouteId = route.RouteId,
                    StopId = route.RouteId + "_P2",
                    Sequence = 2,
                    StopNameTc = route.DestinationTc + "渡輪碼頭",
                    StopNameEn = route.DestinationEn + " Ferry Pier",
                    NormalFare = "$0.0",
                    JoyYouFare = "$0.0",
                    HasTimetable = true,
                    Latitude = dLat,
                    Longitude = dLon,
                    LiveEtaMinutes = new List<int> { 12, 24, 36 },
                    LiveEtaRemarks = new List<string>
                    {
                        ThemeManager.Tr("終點碼頭", "destination pier"),
                        ThemeManager.Tr("終點碼頭", "destination pier"),
                        ThemeManager.Tr("終點碼頭", "destination pier")
                    },
                    IsFavouriteStop = ThemeManager.IsFavourite(route.RouteId, route.RouteId + "_P2", 2),
                    IsExpanded = false
                }
            };
            route.Stops = piers;
        }

        public static List<BasicGeoposition> BuildMaritimeFerryGeometry(string routeId, List<BasicGeoposition> pierEndpoints)
        {
            List<BasicGeoposition> seaWaypoints = new List<BasicGeoposition>();
            BasicGeoposition start = pierEndpoints[0];
            BasicGeoposition end = pierEndpoints[pierEndpoints.Count - 1];

            seaWaypoints.Add(start);

            if (routeId.Contains("CEN_CC") || routeId.Contains("CC_CEN"))
            {
                List<BasicGeoposition> mids = new List<BasicGeoposition>
                {
                    new BasicGeoposition { Latitude = 22.2905, Longitude = 114.1500 },
                    new BasicGeoposition { Latitude = 22.2885, Longitude = 114.1220 },
                    new BasicGeoposition { Latitude = 22.2750, Longitude = 114.1050 },
                    new BasicGeoposition { Latitude = 22.2450, Longitude = 114.0750 },
                    new BasicGeoposition { Latitude = 22.2220, Longitude = 114.0450 }
                };
                if (routeId.StartsWith("SUN_CC")) mids.Reverse();
                seaWaypoints.AddRange(mids);
            }
            else if (routeId.Contains("CEN_MW"))
            {
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.2905, Longitude = 114.1480 });
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.2890, Longitude = 114.1150 });
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.2820, Longitude = 114.0650 });
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.2700, Longitude = 114.0250 });
            }
            else if (routeId.Contains("CEN_PC"))
            {
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.2905, Longitude = 114.1480 });
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.2895, Longitude = 114.1120 });
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.2865, Longitude = 114.0680 });
            }
            else if (routeId.Contains("CEN_YSW"))
            {
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.2902, Longitude = 114.1500 });
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.2880, Longitude = 114.1220 });
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.2650, Longitude = 114.1120 });
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.2420, Longitude = 114.1095 });
            }
            else if (routeId.Contains("TST_CEN") || routeId.Contains("CEN_TST"))
            {
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.2902, Longitude = 114.1652 });
            }
            else if (routeId.Contains("TST_WAC") || routeId.Contains("WAC_TST"))
            {
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.2888, Longitude = 114.1725 });
            }
            else if (routeId.Contains("NP_HH"))
            {
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.2968, Longitude = 114.1940 });
            }
            else if (routeId.Contains("NP_KC"))
            {
                seaWaypoints.Add(new BasicGeoposition { Latitude = 22.3050, Longitude = 114.1975 });
            }

            seaWaypoints.Add(end);
            return GeoMapService.BuildSmoothTrackGeometry(seaWaypoints);
        }
    }
}