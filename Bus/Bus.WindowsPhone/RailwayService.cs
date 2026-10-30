using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace HKBusWP
{
    public static class RailwayService
    {
        private const string MtrApiBase = "https://rt.data.gov.hk/v1/transport/mtr";

        private static readonly Dictionary<string, string[]> _mtrStationNames = new Dictionary<string, string[]>
        {
            { "ADM", new string[] { "金鐘", "Admiralty", "22.2793", "114.1655", "1", "1" } },
            { "EXC", new string[] { "會展", "Exhibition Centre", "22.2816", "114.1752", "1", "1" } },
            { "HUH", new string[] { "紅磡", "Hung Hom", "22.3029", "114.1816", "2", "0" } },
            { "MKK", new string[] { "旺角東", "Mong Kok East", "22.3222", "114.1715", "2", "0" } },
            { "KOT", new string[] { "九龍塘", "Kowloon Tong", "22.3369", "114.1761", "2", "0" } },
            { "TAW", new string[] { "大圍", "Tai Wai", "22.3731", "114.1786", "3", "0" } },
            { "SHT", new string[] { "沙田", "Sha Tin", "22.3826", "114.1875", "3", "0" } },
            { "FOT", new string[] { "火炭", "Fo Tan", "22.3953", "114.1983", "3", "0" } },
            { "RAC", new string[] { "馬場", "Racecourse", "22.4005", "114.2028", "3", "0" } },
            { "UNI", new string[] { "大學", "University", "22.4131", "114.2102", "3", "0" } },
            { "TAP", new string[] { "大埔墟", "Tai Po Market", "22.4445", "114.1705", "4", "0" } },
            { "TWO", new string[] { "太和", "Tai Wo", "22.4511", "114.1611", "4", "0" } },
            { "FAN", new string[] { "粉嶺", "Fanling", "22.4921", "114.1388", "4", "0" } },
            { "SHS", new string[] { "上水", "Sheung Shui", "22.5011", "114.1278", "4", "0" } },
            { "LOW", new string[] { "羅湖", "Lo Wu", "22.5283", "114.1133", "5", "0" } },
            { "LMC", new string[] { "落馬洲", "Lok Ma Chau", "22.5145", "114.0658", "5", "0" } },
            { "WKS", new string[] { "烏溪沙", "Wu Kai Sha", "22.4292", "114.2439", "4", "0" } },
            { "MOS", new string[] { "馬鞍山", "Ma On Shan", "22.4247", "114.2317", "4", "0" } },
            { "HEO", new string[] { "恆安", "Heng On", "22.4175", "114.2258", "4", "0" } },
            { "TSH", new string[] { "大水坑", "Tai Shui Hang", "22.4089", "114.2231", "3", "0" } },
            { "SHM", new string[] { "石門", "Shek Mun", "22.3881", "114.2083", "3", "0" } },
            { "CIO", new string[] { "第一城", "City One", "22.3825", "114.2039", "3", "0" } },
            { "STW", new string[] { "沙田圍", "Sha Tin Wai", "22.3772", "114.1950", "3", "0" } },
            { "CKT", new string[] { "車公廟", "Che Kung Temple", "22.3747", "114.1861", "3", "0" } },
            { "HIK", new string[] { "顯徑", "Hin Keng", "22.3639", "114.1708", "3", "0" } },
            { "DIH", new string[] { "鑽石山", "Diamond Hill", "22.3401", "114.2016", "2", "0" } },
            { "KAT", new string[] { "啟德", "Kai Tak", "22.3303", "114.1995", "2", "0" } },
            { "SUW", new string[] { "宋皇臺", "Sung Wong Toi", "22.3258", "114.1917", "2", "0" } },
            { "TKW", new string[] { "土瓜灣", "To Kwa Wan", "22.3172", "114.1875", "2", "0" } },
            { "HOM", new string[] { "何文田", "Ho Man Tin", "22.3092", "114.1828", "2", "0" } },
            { "ETS", new string[] { "尖東", "East Tsim Sha Tsui", "22.2956", "114.1742", "2", "0" } },
            { "AUS", new string[] { "柯士甸", "Austin", "22.3048", "114.1665", "2", "0" } },
            { "NAC", new string[] { "南昌", "Nam Cheong", "22.3267", "114.1533", "2", "0" } },
            { "MEF", new string[] { "美孚", "Mei Foo", "22.3381", "114.1375", "3", "0" } },
            { "TWW", new string[] { "荃灣西", "Tsuen Wan West", "22.3686", "114.1097", "3", "0" } },
            { "KSR", new string[] { "錦上路", "Kam Sheung Road", "22.4347", "114.0633", "4", "0" } },
            { "YUL", new string[] { "元朗", "Yuen Long", "22.4461", "114.0345", "4", "0" } },
            { "LOP", new string[] { "朗屏", "Long Ping", "22.4478", "114.0252", "4", "0" } },
            { "TIS", new string[] { "天水圍", "Tin Shui Wai", "22.4481", "114.0042", "4", "0" } },
            { "SIH", new string[] { "兆康", "Siu Hong", "22.4119", "113.9786", "4", "0" } },
            { "TUM", new string[] { "屯門", "Tuen Mun", "22.3952", "113.9731", "4", "0" } },
            { "WHA", new string[] { "黃埔", "Whampoa", "22.3050", "114.1897", "2", "0" } },
            { "YMT", new string[] { "油麻地", "Yau Ma Tei", "22.3128", "114.1708", "2", "0" } },
            { "MOK", new string[] { "旺角", "Mong Kok", "22.3192", "114.1694", "2", "0" } },
            { "PRE", new string[] { "太子", "Prince Edward", "22.3245", "114.1683", "2", "0" } },
            { "SKM", new string[] { "石硤尾", "Shek Kip Mei", "22.3319", "114.1686", "2", "0" } },
            { "LOF", new string[] { "樂富", "Lok Fu", "22.3381", "114.1872", "2", "0" } },
            { "WTS", new string[] { "黃大仙", "Wong Tai Sin", "22.3417", "114.1939", "2", "0" } },
            { "CHH", new string[] { "彩虹", "Choi Hung", "22.3347", "114.2089", "2", "0" } },
            { "KOB", new string[] { "九龍灣", "Kowloon Bay", "22.3236", "114.2142", "3", "0" } },
            { "NTK", new string[] { "牛頭角", "Ngau Tau Kok", "22.3156", "114.2192", "3", "0" } },
            { "KWT", new string[] { "觀塘", "Kwun Tong", "22.3122", "114.2264", "3", "0" } },
            { "LAT", new string[] { "藍田", "Lam Tin", "22.3067", "114.2331", "3", "0" } },
            { "YAT", new string[] { "油塘", "Yau Tong", "22.2978", "114.2372", "3", "0" } },
            { "TIK", new string[] { "調景嶺", "Tiu Keng Leng", "22.3042", "114.2525", "3", "0" } },
            { "CEN", new string[] { "中環", "Central", "22.2819", "114.1581", "1", "1" } },
            { "TST", new string[] { "尖沙咀", "Tsim Sha Tsui", "22.2972", "114.1722", "2", "0" } },
            { "JOR", new string[] { "佐敦", "Jordan", "22.3047", "114.1717", "2", "0" } },
            { "SSP", new string[] { "深水埗", "Sham Shui Po", "22.3308", "114.1622", "2", "0" } },
            { "CSW", new string[] { "長沙灣", "Cheung Sha Wan", "22.3353", "114.1564", "2", "0" } },
            { "LCK", new string[] { "荔枝角", "Lai Chi Kok", "22.3372", "114.1483", "2", "0" } },
            { "LAK", new string[] { "荔景", "Lai King", "22.3483", "114.1261", "3", "0" } },
            { "KWF", new string[] { "葵芳", "Kwai Fong", "22.3569", "114.1261", "3", "0" } },
            { "KWH", new string[] { "葵興", "Kwai Hing", "22.3633", "114.1311", "3", "0" } },
            { "TWH", new string[] { "大窩口", "Tai Wo Hau", "22.3708", "114.1250", "3", "0" } },
            { "TSW", new string[] { "荃灣", "Tsuen Wan", "22.3736", "114.1178", "3", "0" } },
            { "KET", new string[] { "堅尼地城", "Kennedy Town", "22.2811", "114.1289", "1", "1" } },
            { "HKU", new string[] { "香港大學", "HKU", "22.2842", "114.1356", "1", "1" } },
            { "SYP", new string[] { "西營盤", "Sai Ying Pun", "22.2856", "114.1428", "1", "1" } },
            { "SHW", new string[] { "上環", "Sheung Wan", "22.2864", "114.1519", "1", "1" } },
            { "WAC", new string[] { "灣仔", "Wan Chai", "22.2775", "114.1731", "1", "1" } },
            { "CAB", new string[] { "銅鑼灣", "Causeway Bay", "22.2803", "114.1847", "1", "1" } },
            { "TIH", new string[] { "天后", "Tin Hau", "22.2828", "114.1917", "1", "1" } },
            { "FOH", new string[] { "炮台山", "Fortress Hill", "22.2881", "114.1936", "1", "1" } },
            { "NOP", new string[] { "北角", "North Point", "22.2908", "114.2008", "1", "1" } },
            { "QUB", new string[] { "鰂魚涌", "Quarry Bay", "22.2878", "114.2097", "1", "1" } },
            { "TAK", new string[] { "太古", "Tai Koo", "22.2847", "114.2164", "1", "1" } },
            { "SWH", new string[] { "西灣河", "Sai Wan Ho", "22.2822", "114.2219", "1", "1" } },
            { "SKW", new string[] { "筲箕灣", "Shau Kei Wan", "22.2789", "114.2289", "1", "1" } },
            { "HFC", new string[] { "杏花邨", "Heng Fa Chuen", "22.2769", "114.2403", "1", "1" } },
            { "CHW", new string[] { "柴灣", "Chai Wan", "22.2644", "114.2372", "1", "1" } },
            { "TKO", new string[] { "將軍澳", "Tseung Kwan O", "22.3075", "114.2600", "3", "0" } },
            { "HAH", new string[] { "坑口", "Hang Hau", "22.3156", "114.2644", "3", "0" } },
            { "POA", new string[] { "寶琳", "Po Lam", "22.3225", "114.2581", "3", "0" } },
            { "LHP", new string[] { "康城", "LOHAS Park", "22.2958", "114.2689", "3", "0" } },
            { "HOK", new string[] { "香港", "Hong Kong", "22.2847", "114.1581", "1", "1" } },
            { "KOW", new string[] { "九龍", "Kowloon", "22.3047", "114.1614", "2", "0" } },
            { "OLY", new string[] { "奧運", "Olympic", "22.3164", "114.1603", "2", "0" } },
            { "TSY", new string[] { "青衣", "Tsing Yi", "22.3583", "114.1069", "3", "0" } },
            { "SUN", new string[] { "欣澳", "Sunny Bay", "22.3317", "114.0289", "4", "0" } },
            { "DIS", new string[] { "迪士尼", "Disneyland Resort", "22.3156", "114.0450", "4", "0" } },
            { "TUC", new string[] { "東涌", "Tung Chung", "22.2892", "113.9417", "4", "0" } },
            { "AIR", new string[] { "機場", "Airport", "22.3161", "113.9367", "6", "0" } },
            { "AWE", new string[] { "博覽館", "AsiaWorld-Expo", "22.3217", "113.9411", "6", "0" } },
            { "OCP", new string[] { "海洋公園", "Ocean Park", "22.2486", "114.1742", "1", "1" } },
            { "WCH", new string[] { "黃竹坑", "Wong Chuk Hang", "22.2481", "114.1681", "1", "1" } },
            { "LET", new string[] { "利東", "Lei Tung", "22.2422", "114.1561", "1", "1" } },
            { "SOH", new string[] { "海怡半島", "South Horizons", "22.2425", "114.1492", "1", "1" } }
        };

        private static readonly Dictionary<string, string[]> _mtrLineStationsMap = new Dictionary<string, string[]>
        {
            { "EAL", new string[] { "ADM", "EXC", "HUH", "MKK", "KOT", "TAW", "SHT", "FOT", "RAC", "UNI", "TAP", "TWO", "FAN", "SHS", "LOW", "LMC" } },
            { "TML", new string[] { "WKS", "MOS", "HEO", "TSH", "SHM", "CIO", "STW", "CKT", "TAW", "HIK", "DIH", "KAT", "SUW", "TKW", "HOM", "HUH", "ETS", "AUS", "NAC", "MEF", "TWW", "KSR", "YUL", "LOP", "TIS", "SIH", "TUM" } },
            { "KTL", new string[] { "WHA", "HOM", "YMT", "MOK", "PRE", "SKM", "KOT", "LOF", "WTS", "DIH", "CHH", "KOB", "NTK", "KWT", "LAT", "YAT", "TIK" } },
            { "TWL", new string[] { "CEN", "ADM", "TST", "JOR", "YMT", "MOK", "PRE", "SSP", "CSW", "LCK", "MEF", "LAK", "KWF", "KWH", "TWH", "TSW" } },
            { "ISL", new string[] { "KET", "HKU", "SYP", "SHW", "CEN", "ADM", "WAC", "CAB", "TIH", "FOH", "NOP", "QUB", "TAK", "SWH", "SKW", "HFC", "CHW" } },
            { "TKL", new string[] { "NOP", "QUB", "YAT", "TIK", "TKO", "HAH", "POA", "LHP" } },
            { "TCL", new string[] { "HOK", "KOW", "OLY", "NAC", "LAK", "TSY", "SUN", "TUC" } },
            { "AEL", new string[] { "HOK", "KOW", "TSY", "AIR", "AWE" } },
            { "SIL", new string[] { "ADM", "OCP", "WCH", "LET", "SOH" } },
            { "DRL", new string[] { "SUN", "DIS" } }
        };

        public static bool TryGetStationCoords(string stCode, out double lat, out double lon)
        {
            lat = 22.3029;
            lon = 114.1816;
            if (!string.IsNullOrEmpty(stCode) && _mtrStationNames.ContainsKey(stCode))
            {
                lat = double.Parse(_mtrStationNames[stCode][2], CultureInfo.InvariantCulture);
                lon = double.Parse(_mtrStationNames[stCode][3], CultureInfo.InvariantCulture);
                return true;
            }
            return false;
        }

        public static void FindNearestOrFirstMtrStationForLine(string lineCode, string dir, out string stCode, out string stNameTc, out string stNameEn)
        {
            string[] codes = _mtrLineStationsMap.ContainsKey(lineCode) ? _mtrLineStationsMap[lineCode] : _mtrLineStationsMap["EAL"];
            List<string> ordered = new List<string>(codes);
            if (dir == "DOWN" || dir == "I") ordered.Reverse();

            double userLat = ThemeManager.LastKnownLatitude;
            double userLon = ThemeManager.LastKnownLongitude;
            double minDist = double.MaxValue;
            string bestCode = ordered[0];

            for (int i = 0; i < ordered.Count; i++)
            {
                string c = ordered[i];
                if (_mtrStationNames.ContainsKey(c))
                {
                    double lat = double.Parse(_mtrStationNames[c][2], CultureInfo.InvariantCulture);
                    double lon = double.Parse(_mtrStationNames[c][3], CultureInfo.InvariantCulture);
                    double d = BusRouteDatabase.CalculateDistanceMeters(userLat, userLon, lat, lon);
                    if (d < minDist) { minDist = d; bestCode = c; }
                }
            }

            if (minDist > 1200.0) bestCode = ordered[0];

            stCode = bestCode;
            stNameTc = _mtrStationNames[bestCode][0] + "站 (" + bestCode + ")";
            stNameEn = _mtrStationNames[bestCode][1] + " Station (" + bestCode + ")";
        }

        public static List<NearbyRouteItem> GetAllRailwayRoutes()
        {
            List<NearbyRouteItem> list = new List<NearbyRouteItem>();

            string[,] mtrLines = new string[,]
            {
                { "EAL", "UP", "金鐘", "Admiralty", "羅湖 / 落馬洲 / 馬場 / 大埔墟", "Lo Wu / Lok Ma Chau / Racecourse" },
                { "EAL", "DOWN", "羅湖 / 落馬洲", "Lo Wu / Lok Ma Chau", "金鐘 / 紅磡 / 沙田", "Admiralty / Hung Hom" },
                { "TML", "UP", "烏溪沙", "Wu Kai Sha", "屯門", "Tuen Mun" },
                { "TML", "DOWN", "屯門", "Tuen Mun", "烏溪沙", "Wu Kai Sha" },
                { "KTL", "UP", "黃埔 / 何文田", "Whampoa / Ho Man Tin", "調景嶺", "Tiu Keng Leng" },
                { "KTL", "DOWN", "調景嶺", "Tiu Keng Leng", "黃埔 / 何文田", "Whampoa / Ho Man Tin" },
                { "TWL", "UP", "中環", "Central", "荃灣", "Tsuen Wan" },
                { "TWL", "DOWN", "荃灣", "Tsuen Wan", "中環", "Central" },
                { "ISL", "UP", "堅尼地城", "Kennedy Town", "柴灣", "Chai Wan" },
                { "ISL", "DOWN", "柴灣", "Chai Wan", "堅尼地城", "Kennedy Town" },
                { "TKL", "UP", "北角", "North Point", "寶琳 / 康城", "Po Lam / LOHAS Park" },
                { "TKL", "DOWN", "寶琳 / 康城", "Po Lam / LOHAS Park", "北角", "North Point" },
                { "TCL", "UP", "香港", "Hong Kong", "東涌 / 青衣", "Tung Chung / Tsing Yi" },
                { "TCL", "DOWN", "東涌", "Tung Chung", "香港", "Hong Kong" },
                { "AEL", "UP", "香港", "Hong Kong", "機場 / 博覽館", "Airport / AsiaWorld-Expo" },
                { "AEL", "DOWN", "博覽館 / 機場", "AsiaWorld-Expo / Airport", "香港", "Hong Kong" },
                { "SIL", "UP", "金鐘", "Admiralty", "海怡半島", "South Horizons" },
                { "SIL", "DOWN", "海怡半島", "South Horizons", "金鐘", "Admiralty" },
                { "DRL", "UP", "欣澳", "Sunny Bay", "迪士尼", "Disneyland Resort" },
                { "DRL", "DOWN", "迪士尼", "Disneyland Resort", "欣澳", "Sunny Bay" }
            };

            for (int i = 0; i < mtrLines.GetLength(0); i++)
            {
                string code = mtrLines[i, 0];
                string dir = mtrLines[i, 1];
                string stCode, stTc, stEn;
                FindNearestOrFirstMtrStationForLine(code, dir, out stCode, out stTc, out stEn);

                list.Add(new NearbyRouteItem
                {
                    RouteId = string.Format("MTR_{0}_{1}", code, dir),
                    RouteNumber = code,
                    Direction = dir,
                    ServiceType = "1",
                    CompanyTc = "港鐵",
                    CompanyEn = "mtr",
                    TransportType = "鐵路",
                    OriginTc = mtrLines[i, 2],
                    OriginEn = mtrLines[i, 3],
                    DestinationTc = mtrLines[i, 4],
                    DestinationEn = mtrLines[i, 5],
                    NearestStopId = stCode,
                    StopNameTc = stTc,
                    StopNameEn = stEn,
                    FareText = string.Empty,
                    FirstArrivalTime = DateTime.Now.AddMinutes(2),
                    SecondArrivalTime = DateTime.Now.AddMinutes(6),
                    ThirdArrivalTime = DateTime.Now.AddMinutes(10)
                });
            }

            string[,] lrtRoutes = new string[,]
            {
                { "505", "三聖", "Sam Shing", "兆康", "Siu Hong", "295", "1" },
                { "507", "屯門碼頭", "Tuen Mun Ferry Pier", "田景", "Tin King", "1", "1" },
                { "610", "屯門碼頭", "Tuen Mun Ferry Pier", "元朗", "Yuen Long", "1", "1" },
                { "614", "屯門碼頭", "Tuen Mun Ferry Pier", "元朗", "Yuen Long", "1", "1" },
                { "615", "屯門碼頭", "Tuen Mun Ferry Pier", "元朗", "Yuen Long", "1", "1" },
                { "705", "天水圍循環線 (逆時針)", "Tin Shui Wai Circular (CCW)", "天水圍", "Tin Shui Wai", "430", "0" },
                { "706", "天水圍循環線 (順時針)", "Tin Shui Wai Circular (CW)", "天水圍", "Tin Shui Wai", "430", "0" },
                { "751", "天逸", "Tin Yat", "友愛", "Yau Oi", "550", "1" },
                { "761P", "天逸", "Tin Yat", "元朗", "Yuen Long", "550", "1" }
            };

            for (int i = 0; i < lrtRoutes.GetLength(0); i++)
            {
                string code = lrtRoutes[i, 0];
                list.Add(new NearbyRouteItem
                {
                    RouteId = "LRT_" + code,
                    RouteNumber = code,
                    Direction = "O",
                    ServiceType = lrtRoutes[i, 5],
                    CompanyTc = "輕鐵",
                    CompanyEn = "light rail",
                    TransportType = "鐵路",
                    HasOppositeDirection = (lrtRoutes[i, 6] == "1"),
                    OriginTc = lrtRoutes[i, 1],
                    OriginEn = lrtRoutes[i, 2],
                    DestinationTc = lrtRoutes[i, 3],
                    DestinationEn = lrtRoutes[i, 4],
                    StopNameTc = lrtRoutes[i, 1] + "總站",
                    StopNameEn = lrtRoutes[i, 2] + " Stop",
                    FareText = string.Empty,
                    FirstArrivalTime = DateTime.Now.AddMinutes(3),
                    SecondArrivalTime = DateTime.Now.AddMinutes(9)
                });
            }
            return list;
        }

        public static async Task RefreshRailwayTabNearestStationsAsync(IList<NearbyRouteItem> railwayItems)
        {
            if (railwayItems == null) return;

            for (int i = 0; i < railwayItems.Count; i++)
            {
                NearbyRouteItem r = railwayItems[i];
                if (r.RouteId.StartsWith("MTR_"))
                {
                    string stCode, stTc, stEn;
                    FindNearestOrFirstMtrStationForLine(r.RouteNumber, r.Direction, out stCode, out stTc, out stEn);
                    r.NearestStopId = stCode;
                    r.StopNameTc = stTc;
                    r.StopNameEn = stEn;

                    BusStopItem tempStop = new BusStopItem { RouteId = r.RouteId, StopId = stCode, Sequence = 1, IsRailwayStop = true, HasTimetable = true };
                    await FetchRailwayStationLiveEtaAsync(r, tempStop);
                    List<int> valid = tempStop.GetValidEtasWithin60Minutes();
                    DateTime now = DateTime.Now;
                    r.FirstArrivalTime = valid.Count >= 1 ? (DateTime?)now.AddMinutes(valid[0]) : null;
                    r.SecondArrivalTime = valid.Count >= 2 ? (DateTime?)now.AddMinutes(valid[1]) : null;
                    r.ThirdArrivalTime = valid.Count >= 3 ? (DateTime?)now.AddMinutes(valid[2]) : null;
                    r.RefreshTimeDisplay();
                }
            }
        }

        public static async Task PopulateRailwayStopsAndEtasAsync(NearbyRouteItem route)
        {
            if (route.RouteId.StartsWith("LRT_"))
            {
                string[,] lrtStops = new string[,]
                {
                    { "1", "屯門碼頭", "Tuen Mun Ferry Pier", "22.3721", "113.9665" },
                    { "15", "美樂", "Melody Garden", "22.3752", "113.9625" },
                    { "100", "兆麟", "Siu Lun", "22.3842", "113.9748" },
                    { "212", "市中心", "Town Centre", "22.3918", "113.9765" },
                    { "280", "屯門", "Tuen Mun", "22.3952", "113.9731" },
                    { "295", "兆康", "Siu Hong", "22.4119", "113.9786" },
                    { "390", "洪水橋", "Hung Shui Kiu", "22.4338", "113.9975" },
                    { "430", "天水圍", "Tin Shui Wai", "22.4481", "114.0042" },
                    { "480", "天瑞", "Tin Shui", "22.4562", "113.9988" },
                    { "550", "天逸", "Tin Yat", "22.4675", "113.9985" },
                    { "580", "水邊圍", "Shui Pin Wai", "22.4448", "114.0202" },
                    { "600", "元朗", "Yuen Long", "22.4461", "114.0345" }
                };

                List<BusStopItem> stops = new List<BusStopItem>();
                for (int i = 0; i < lrtStops.GetLength(0); i++)
                {
                    stops.Add(new BusStopItem
                    {
                        RouteId = route.RouteId,
                        StopId = lrtStops[i, 0],
                        Sequence = i + 1,
                        StopNameTc = lrtStops[i, 1] + "輕鐵站",
                        StopNameEn = lrtStops[i, 2] + " LRT Stop",
                        IsRailwayStop = true,
                        HasTimetable = true,
                        Latitude = double.Parse(lrtStops[i, 3], CultureInfo.InvariantCulture),
                        Longitude = double.Parse(lrtStops[i, 4], CultureInfo.InvariantCulture),
                        IsFavouriteStop = ThemeManager.IsFavourite(route.RouteId, lrtStops[i, 0], i + 1),
                        IsExpanded = false
                    });
                }
                int expIdx = BusRouteDatabase.SelectNearestOrFirstStopIndex(stops, route.NearestStopId);
                stops[expIdx].IsExpanded = true;
                route.Stops = stops;
                await FetchRailwayStationLiveEtaAsync(route, stops[expIdx]);
                return;
            }

            string lineCode = route.RouteNumber;
            string[] codes = _mtrLineStationsMap.ContainsKey(lineCode) ? _mtrLineStationsMap[lineCode] : _mtrLineStationsMap["EAL"];
            List<string> orderedCodes = new List<string>(codes);
            if (route.Direction == "DOWN" || route.Direction == "I") orderedCodes.Reverse();

            List<BusStopItem> mtrStops = new List<BusStopItem>();
            for (int i = 0; i < orderedCodes.Count; i++)
            {
                string stCode = orderedCodes[i];
                string[] meta = _mtrStationNames.ContainsKey(stCode)
                    ? _mtrStationNames[stCode]
                    : new string[] { stCode, stCode, "22.3029", "114.1816", "2", "0" };

                string spurTagTc = (stCode == "LMC" || stCode == "RAC" || stCode == "LHP") ? " [支線]" : "";
                string spurTagEn = (stCode == "LMC" || stCode == "RAC" || stCode == "LHP") ? " [Spur]" : "";

                mtrStops.Add(new BusStopItem
                {
                    RouteId = route.RouteId,
                    StopId = stCode,
                    Sequence = i + 1,
                    StopNameTc = string.Format("{0}站{1} ({2})", meta[0], spurTagTc, stCode),
                    StopNameEn = string.Format("{0} Station{1} ({2})", meta[1], spurTagEn, stCode),
                    IsRailwayStop = true,
                    HasTimetable = true,
                    Latitude = double.Parse(meta[2], CultureInfo.InvariantCulture),
                    Longitude = double.Parse(meta[3], CultureInfo.InvariantCulture),
                    IsFavouriteStop = ThemeManager.IsFavourite(route.RouteId, stCode, i + 1),
                    IsExpanded = false
                });
            }

            if (mtrStops.Count > 0)
            {
                int expandIdx = BusRouteDatabase.SelectNearestOrFirstStopIndex(mtrStops, route.NearestStopId);
                mtrStops[expandIdx].IsExpanded = true;
                route.Stops = mtrStops;
                await FetchRailwayStationLiveEtaAsync(route, mtrStops[expandIdx]);
            }
        }

        public static async Task FetchRailwayStationLiveEtaAsync(NearbyRouteItem route, BusStopItem stop)
        {
            stop.IsLoadingEta = true;
            stop.RefreshEtaBindings();

            List<int> minsList = new List<int>();
            List<string> remarksList = new List<string>();

            try
            {
                if (route.RouteId.StartsWith("LRT_"))
                {
                    string url = string.Format("{0}/lrt/getSchedule?station_id={1}", MtrApiBase, stop.StopId);
                    string jsonText = await BusRouteDatabase.HttpGetStringAsync(url, true);
                    if (!string.IsNullOrEmpty(jsonText))
                    {
                        JsonObject root = JsonObject.Parse(jsonText);
                        if (root.ContainsKey("platform_list") && root["platform_list"].ValueType == JsonValueType.Array)
                        {
                            JsonArray plats = root["platform_list"].GetArray();
                            for (int p = 0; p < plats.Count; p++)
                            {
                                JsonObject pObj = plats[p].GetObject();
                                string platId = BusRouteDatabase.GetJsonString(pObj, "platform_id");
                                if (pObj.ContainsKey("route_list") && pObj["route_list"].ValueType == JsonValueType.Array)
                                {
                                    JsonArray rList = pObj["route_list"].GetArray();
                                    for (int r = 0; r < rList.Count; r++)
                                    {
                                        JsonObject rObj = rList[r].GetObject();
                                        string rNo = BusRouteDatabase.GetJsonString(rObj, "route_no");
                                        if (rNo != route.RouteNumber && minsList.Count >= 2) continue;

                                        string timeEn = BusRouteDatabase.GetJsonString(rObj, "time_en");
                                        string destTc = BusRouteDatabase.GetJsonString(rObj, "dest_ch");
                                        string destEn = BusRouteDatabase.GetJsonString(rObj, "dest_en");
                                        int m = 1;
                                        if (timeEn.Contains(" min")) int.TryParse(timeEn.Replace(" mins", "").Replace(" min", "").Trim(), out m);
                                        else if (timeEn == "-" || timeEn.ToLowerInvariant().Contains("arriving")) m = 0;

                                        if (minsList.Count < 3)
                                        {
                                            minsList.Add(m);
                                            string destLabel = ThemeManager.IsEnglish
                                                ? string.Format("{0} to {1} (plat {2})", rNo, destEn, platId)
                                                : string.Format("{0}線 往 {1} ({2}號月台)", rNo, destTc, platId);
                                            remarksList.Add(destLabel);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    string lang = ThemeManager.IsEnglish ? "EN" : "TC";
                    string url = string.Format("{0}/getSchedule.php?line={1}&sta={2}&lang={3}",
                        MtrApiBase, route.RouteNumber, stop.StopId, lang);
                    string jsonText = await BusRouteDatabase.HttpGetStringAsync(url, true);

                    if (!string.IsNullOrEmpty(jsonText))
                    {
                        JsonObject root = JsonObject.Parse(jsonText);
                        if (root.ContainsKey("data") && root["data"].ValueType == JsonValueType.Object)
                        {
                            JsonObject dataObj = root["data"].GetObject();
                            string key = string.Format("{0}-{1}", route.RouteNumber, stop.StopId);
                            if (dataObj.ContainsKey(key) && dataObj[key].ValueType == JsonValueType.Object)
                            {
                                JsonObject stObj = dataObj[key].GetObject();
                                string[] dirsToCheck = (route.Direction == "DOWN" || route.Direction == "I")
                                    ? new string[] { "DOWN", "UP" }
                                    : new string[] { "UP", "DOWN" };

                                for (int d = 0; d < dirsToCheck.Length; d++)
                                {
                                    string dirKey = dirsToCheck[d];
                                    if (stObj.ContainsKey(dirKey) && stObj[dirKey].ValueType == JsonValueType.Array)
                                    {
                                        JsonArray trains = stObj[dirKey].GetArray();
                                        for (int t = 0; t < trains.Count; t++)
                                        {
                                            JsonObject tr = trains[t].GetObject();
                                            string destCode = BusRouteDatabase.GetJsonString(tr, "dest");
                                            string plat = BusRouteDatabase.GetJsonString(tr, "plat");
                                            string ttnt = BusRouteDatabase.GetJsonString(tr, "ttnt");
                                            string routeFlag = BusRouteDatabase.GetJsonString(tr, "route");

                                            int mins = 1;
                                            int.TryParse(ttnt, out mins);

                                            string destName = ResolveMtrStationName(destCode);
                                            string viaNote = (routeFlag == "RAC") ? ThemeManager.Tr(" (經馬場)", " (via Racecourse)") : "";

                                            bool isMidwayShortTrain = (destCode == "FOT" || destCode == "TAP" || destCode == "SHS" ||
                                                                       destCode == "SHT" || destCode == "HOM" || destCode == "TSY" ||
                                                                       destCode == "MEF" || destCode == "TAW");
                                            string prefix = isMidwayShortTrain
                                                ? ThemeManager.Tr("中途短途車 往 ", "short-working to ")
                                                : ThemeManager.Tr("往 ", "to ");

                                            string platText = ThemeManager.IsEnglish
                                                ? string.Format(" (plat {0})", plat)
                                                : string.Format(" ({0}號月台)", plat);

                                            if (minsList.Count < 3)
                                            {
                                                minsList.Add(Math.Max(0, mins));
                                                remarksList.Add(prefix + destName + viaNote + platText);
                                            }
                                        }
                                        if (minsList.Count > 0) break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception) { }

            stop.LiveEtaMinutes = minsList;
            stop.LiveEtaRemarks = remarksList;
            stop.IsLoadingEta = false;
            stop.RefreshEtaBindings();
        }

        private static string ResolveMtrStationName(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            if (_mtrStationNames.ContainsKey(code))
            {
                return ThemeManager.IsEnglish ? _mtrStationNames[code][1] : _mtrStationNames[code][0];
            }
            return code;
        }

        public static List<MtrStationOption> GetMtrStationsList()
        {
            List<MtrStationOption> list = new List<MtrStationOption>();
            foreach (var kvp in _mtrStationNames)
            {
                string code = kvp.Key;
                string[] meta = kvp.Value;
                int zone = 2;
                int.TryParse(meta[4], out zone);
                bool isHarbour = (meta[5] == "1");
                bool isBorder = (code == "LOW" || code == "LMC");
                bool isAirport = (code == "AIR" || code == "AWE");

                list.Add(new MtrStationOption
                {
                    Code = code,
                    NameTc = meta[0],
                    NameEn = meta[1],
                    Zone = zone,
                    IsHarbour = isHarbour,
                    IsBorder = isBorder,
                    IsAirport = isAirport
                });
            }
            return list;
        }

        // Fixed MTR Boundary Fares (Lo Wu & Lok Ma Chau): Accurate station-tier pricing + JoyYou boundary exclusion rule
        public static string CalculateMtrStationFare(MtrStationOption fromSt, MtrStationOption toSt)
        {
            if (fromSt == null || toSt == null) return "";
            if (fromSt.Code == toSt.Code)
            {
                return ThemeManager.Tr("同站進出最低車費: 成人 $4.0 | 樂悠/特惠 $2.0", "same station min fare: adult $4.0 | joyyou $2.0");
            }

            double adult = 5.5;
            double student = 2.8;
            double joyYou = 2.0;

            if (fromSt.IsAirport || toSt.IsAirport)
            {
                adult = 110.0;
                student = 55.0;
                joyYou = 110.0;
            }
            else if (fromSt.IsBorder || toSt.IsBorder)
            {
                if (fromSt.IsBorder && toSt.IsBorder)
                {
                    return ThemeManager.Tr(
                        "羅湖 ↔ 落馬洲 (過境線)\n直達車費：成人 / 60-64歲樂悠咭 $49.6 ｜ 65歲+長者 / 特惠 $24.8\n上水轉乘分段：60-64歲樂悠咭 $53.0 ｜ 65歲+長者樂悠咭 $26.6",
                        "Lo Wu ↔ Lok Ma Chau (Boundary)\nDirect: Adult / JoyYou (60-64) $49.6 | Senior 65+ / Concession $24.8");
                }

                MtrStationOption other = fromSt.IsBorder ? toSt : fromSt;
                string c = other.Code;

                // 1. Direct Border Fare (直達不拍卡出閘)
                double directAdult = 41.5;
                double directSenior65 = 20.8;
                // 2. Adult Fare from Origin to Sheung Shui (for「兩蚊兩折」上水出閘再入閘 formula)
                double adultToSheungShui = 14.5;

                if (c == "SHS")
                {
                    directAdult = 26.5;
                    directSenior65 = 13.3;
                    adultToSheungShui = 0.0;
                }
                else if (c == "FAN")
                {
                    directAdult = 26.5;
                    directSenior65 = 13.3;
                    adultToSheungShui = 4.0;
                }
                else if (c == "TWO" || c == "TAP")
                {
                    directAdult = 27.9;
                    directSenior65 = 14.0;
                    adultToSheungShui = 6.2;
                }
                else if (c == "UNI" || c == "FOT" || c == "SHT" || c == "TAW")
                {
                    directAdult = 30.3;
                    directSenior65 = 15.2;
                    adultToSheungShui = 9.9;
                }
                else if (c == "RAC")
                {
                    directAdult = 57.0;
                    directSenior65 = 28.5;
                    adultToSheungShui = 9.9;
                }
                else if (c == "KOT" || c == "MKK" || c == "HUH")
                {
                    directAdult = 41.5;
                    directSenior65 = 20.8;
                    adultToSheungShui = 11.5;
                }
                else if (other.IsHarbour)
                {
                    directAdult = (c == "EXC" || c == "ADM") ? 52.5 : 55.5;
                    directSenior65 = Math.Round(directAdult * 0.5, 1);
                    adultToSheungShui = 22.2;
                }
                else
                {
                    directAdult = 44.5;
                    directSenior65 = 22.3;
                    adultToSheungShui = 15.8;
                }

                // Formula for「兩蚊兩折」via Sheung Shui Re-entry (上水出閘再入閘):
                // Leg 1 (Origin <-> Sheung Shui): $2.0 if <= $10.0, or 20% (兩折) if > $10.0
                // Leg 2 (Sheung Shui <-> Lo Wu / Lok Ma Chau): $26.5 (for 60-64 JoyYou) or $13.3 (for 65+ JoyYou)
                double leg1JoyYou = 0.0;
                if (adultToSheungShui > 0.0)
                {
                    leg1JoyYou = (adultToSheungShui > 10.0)
                        ? Math.Max(2.0, Math.Round(adultToSheungShui * 0.2, 1))
                        : 2.0;
                }

                double splitJoyYou60to64 = Math.Round(leg1JoyYou + 26.5, 1);
                double splitJoyYou65Plus = Math.Round(leg1JoyYou + 13.3, 1);

                return ThemeManager.IsEnglish
                    ? string.Format(
                        "{0} → {1} (boundary line)\n" +
                        "• direct ride: adult / joyyou (60-64) ${2:F1} | senior 65+ / student ${3:F1}\n" +
                        "• via sheung shui (兩蚊兩折 split): joyyou (60-64) ${4:F1} | joyyou (65+) ${5:F1}",
                        fromSt.NameEn, toSt.NameEn, directAdult, directSenior65, splitJoyYou60to64, splitJoyYou65Plus)
                    : string.Format(
                        "{0} → {1} (過境線)\n" +
                        "• 直達不出閘：成人 / 樂悠咭(60-64歲) ${2:F1} ｜ 樂悠咭(65歲+) / 特惠 ${3:F1}\n" +
                        "• 上水出閘「兩蚊兩折」：樂悠咭(60-64歲) ${4:F1} ｜ 樂悠咭(65歲+) ${5:F1}",
                        fromSt.NameTc, toSt.NameTc, directAdult, directSenior65, splitJoyYou60to64, splitJoyYou65Plus);
            }
            else
            {
                int zoneDiff = Math.Abs(fromSt.Zone - toSt.Zone);
                bool crossHarbour = (fromSt.IsHarbour != toSt.IsHarbour);
                adult = 5.0 + (zoneDiff * 3.8) + (crossHarbour ? 6.5 : 0.0);
                student = Math.Round(adult * 0.5, 1);
                joyYou = (adult > 10.0) ? Math.Max(2.0, Math.Round(adult * 0.2, 1)) : Math.Min(adult, 2.0);
            }

            return ThemeManager.IsEnglish
                ? string.Format("{0} → {1}\nadult octopus: ${2:F1}\nstudent / child: ${3:F1}\njoyyou card: ${4:F1}",
                    fromSt.NameEn, toSt.NameEn, adult, student, joyYou)
                : string.Format("{0} → {1}\n成人八達通：${2:F1}\n特惠 / 學生：${3:F1}\n樂悠咭車費：${4:F1}",
                    fromSt.NameTc, toSt.NameTc, adult, student, joyYou);
        }
    }
}