using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Devices.Geolocation;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace HKBusWP
{
    public class KnownHkStop
    {
        public string StopId { get; set; }
        public string NameTc { get; set; }
        public string NameEn { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public class HkoWeatherSnapshot
    {
        public string TemperatureText { get; set; }
        public string WeatherIconUrl { get; set; }
        public List<string> WarningIconUrls { get; set; }

        public HkoWeatherSnapshot()
        {
            TemperatureText = "--°C";
            WeatherIconUrl = "https://www.hko.gov.hk/images/HKOWxIconOutline/pic50.png";
            WarningIconUrls = new List<string>();
        }
    }

    public class TrafficIncidentItem
    {
        public string HeaderDate { get; set; }
        public string BodyText { get; set; }
    }

    public class RouteTimetableEntry
    {
        public string ServiceDays { get; set; }
        public string PeriodHours { get; set; }
        public string HeadwayMinutes { get; set; }
    }

    public class MtrStationOption
    {
        public string Code { get; set; }
        public string NameTc { get; set; }
        public string NameEn { get; set; }
        public string LineCode { get; set; }
        public int Zone { get; set; }
        public bool IsHarbour { get; set; }
        public bool IsBorder { get; set; }
        public bool IsAirport { get; set; }

        public string DisplayName
        {
            get { return ThemeManager.IsEnglish ? string.Format("{0} ({1})", NameEn, Code) : string.Format("{0} ({1})", NameTc, Code); }
        }
    }

    public static class BusRouteDatabase
    {
        private const string KmbApiBase = "https://data.etabus.gov.hk/v1/transport/kmb";
        private const string CtbApiBase = "https://rt.data.gov.hk/v2/transport/citybus";
        private const string NlbApiBase = "https://rt.data.gov.hk/v2/transport/nlb";

        private static readonly SemaphoreSlim _catalogLock = new SemaphoreSlim(1, 1);

        private static List<NearbyRouteItem> _allRoutesCatalog = new List<NearbyRouteItem>();
        private static List<NearbyRouteItem> _recentSearchRoutes = new List<NearbyRouteItem>();
        private static Dictionary<string, KnownHkStop> _kmbStopsMap = new Dictionary<string, KnownHkStop>();
        private static List<KnownHkStop> _kmbStopsList = new List<KnownHkStop>();
        private static Dictionary<string, NearbyRouteItem> _kmbRouteLookup = new Dictionary<string, NearbyRouteItem>();
        private static Dictionary<string, NearbyRouteItem> _ctbRouteLookup = new Dictionary<string, NearbyRouteItem>();

        private static bool _isCatalogLoaded = false;
        private static bool _isKmbStopsLoaded = false;

        public static NearbyRouteItem CurrentSelectedRoute { get; set; }

        public static List<NearbyRouteItem> RecentSearchRoutes
        {
            get { return _recentSearchRoutes; }
        }

        public static List<KnownHkStop> GetKmbStopsListInternal()
        {
            return _kmbStopsList;
        }

        public static void ClearCache()
        {
            _allRoutesCatalog.Clear();
            _kmbStopsMap.Clear();
            _kmbStopsList.Clear();
            _kmbRouteLookup.Clear();
            _ctbRouteLookup.Clear();
            _isCatalogLoaded = false;
            _isKmbStopsLoaded = false;

            MtrBusService.ClearCache();
            GreenMinibusService.ClearCache();
            GeoMapService.ClearCache();
            CacheService.ClearDiskCacheFile();
        }

        public static void RecordRecentSearch(NearbyRouteItem route)
        {
            if (route == null) return;
            for (int i = _recentSearchRoutes.Count - 1; i >= 0; i--)
            {
                if (_recentSearchRoutes[i].RouteId == route.RouteId) _recentSearchRoutes.RemoveAt(i);
            }
            _recentSearchRoutes.Insert(0, route);
            if (_recentSearchRoutes.Count > 20) _recentSearchRoutes.RemoveAt(_recentSearchRoutes.Count - 1);
        }

        // 1. FORWARDING WRAPPERS TO MODULAR SERVICES
        public static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            return GeoMapService.CalculateDistanceMeters(lat1, lon1, lat2, lon2);
        }

        public static int SelectNearestOrFirstStopIndex(List<BusStopItem> stops, string preferredStopId)
        {
            return GeoMapService.SelectNearestOrFirstStopIndex(stops, preferredStopId);
        }

        public static List<BasicGeoposition> BuildSmoothTrackGeometry(List<BasicGeoposition> stations)
        {
            return GeoMapService.BuildSmoothTrackGeometry(stations);
        }

        public static Task PopulateGmbRouteDetailsAndLiveEtaAsync(NearbyRouteItem route, bool fetchLiveEta)
        {
            return GreenMinibusService.PopulateGmbRouteDetailsAndLiveEtaAsync(
                route,
                fetchLiveEta,
                ThemeManager.LastKnownLatitude,
                ThemeManager.LastKnownLongitude);
        }

        // 2. BUS & MINIBUS FARE / SECTION FARE (分段收費) ENGINE
        public static void GetStandardRouteFares(string routeNumber, string companyTc, out string normalFare, out string joyYouFare)
        {
            normalFare = "$6.7";
            joyYouFare = "$2.0";
            if (string.IsNullOrEmpty(routeNumber)) return;
            string u = routeNumber.Trim().ToUpperInvariant();

            if (companyTc != null && companyTc.Contains("港鐵巴士"))
            {
                bool isTaiPo = (u == "K12" || u == "K14" || u == "K17" || u == "K18");
                normalFare = isTaiPo ? "$4.8" : "$5.1";
                joyYouFare = "$2.0";
                return;
            }
            if (companyTc != null && companyTc.Contains("電車")) { normalFare = "$3.3"; joyYouFare = "$1.5"; return; }
            if (companyTc != null && (companyTc.Contains("小巴") || companyTc.Contains("綠小")))
            {
                int gmbNum = 0;
                string gDigits = "";
                for (int i = 0; i < u.Length; i++) if (char.IsDigit(u[i])) gDigits += u[i];
                int.TryParse(gDigits, out gmbNum);

                if (u == "55K") normalFare = "$6.5";
                else if (gmbNum >= 400 || u.EndsWith("M")) normalFare = "$7.8";
                else if (gmbNum >= 100) normalFare = "$11.4";
                else if (gmbNum >= 50) normalFare = "$8.8";
                else if (u.EndsWith("A") || u.EndsWith("B")) normalFare = "$5.8";
                else normalFare = "$7.2";

                joyYouFare = BusStopItem.CalculateJoyYouFare(normalFare);
                return;
            }
            if (u.StartsWith("NA")) { normalFare = "$40.0"; joyYouFare = "$8.0"; return; }
            if (u.StartsWith("A") && u.Length > 1 && char.IsDigit(u[1])) { normalFare = "$30.9"; joyYouFare = "$6.2"; return; }
            if (u.StartsWith("E") && u.Length > 1 && char.IsDigit(u[1])) { normalFare = "$14.4"; joyYouFare = "$2.9"; return; }
            if (u.StartsWith("B")) { normalFare = "$10.8"; joyYouFare = "$2.2"; return; }
            if (u.StartsWith("N")) { normalFare = "$16.9"; joyYouFare = "$3.4"; return; }
            if (u.StartsWith("9") && u.Length == 3) { normalFare = "$22.7"; joyYouFare = "$4.5"; return; }
            if ((u.StartsWith("1") || u.StartsWith("3") || u.StartsWith("6")) && u.Length == 3 && char.IsDigit(u[0]) && char.IsDigit(u[1]) && char.IsDigit(u[2]))
            {
                normalFare = "$12.1"; joyYouFare = "$2.4"; return;
            }
            if (u == "271S" || u == "271") { normalFare = "$21.8"; joyYouFare = "$4.4"; return; }
            if (u == "87D" || u == "287D") { normalFare = "$10.8"; joyYouFare = "$2.2"; return; }
            if (u == "208") { normalFare = "$9.3"; joyYouFare = "$2.0"; return; }
            if (u == "8") { normalFare = "$5.8"; joyYouFare = "$2.0"; return; }

            int num = 0;
            string digits = "";
            for (int i = 0; i < u.Length; i++) if (char.IsDigit(u[i])) digits += u[i];
            int.TryParse(digits, out num);

            if (num >= 200 && num < 300) { normalFare = "$11.8"; joyYouFare = "$2.4"; }
            else if (num >= 70 && num < 100) { normalFare = "$10.8"; joyYouFare = "$2.2"; }
            else if (num >= 30 && num < 70) { normalFare = "$7.5"; joyYouFare = "$2.0"; }
        }

        public static void CalculateStopSectionFare(string routeNumber, string companyTc, int stopSeq, int totalStops, out string normFare, out string joyFare, out bool isSectionFare)
        {
            GetStandardRouteFares(routeNumber, companyTc, out normFare, out joyFare);
            isSectionFare = false;
            if (totalStops <= 5 || stopSeq <= 1) return;

            double fullPrice = 6.7;
            double.TryParse(normFare.Replace("$", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out fullPrice);
            if (fullPrice <= 5.2) return;

            double progress = (double)(stopSeq - 1) / (double)Math.Max(1, totalStops - 1);
            if (progress >= 0.72)
            {
                double sec2 = Math.Max(4.2, Math.Round(fullPrice * 0.48, 1));
                normFare = string.Format(CultureInfo.InvariantCulture, "${0:F1}", sec2);
                joyFare = BusStopItem.CalculateJoyYouFare(normFare);
                isSectionFare = true;
            }
            else if (progress >= 0.45 && fullPrice >= 6.4)
            {
                double sec1 = Math.Max(5.0, Math.Round(fullPrice * 0.68, 1));
                normFare = string.Format(CultureInfo.InvariantCulture, "${0:F1}", sec1);
                joyFare = BusStopItem.CalculateJoyYouFare(normFare);
                isSectionFare = true;
            }
        }

        public static string FormatBracketFareForStop(string routeNumber, string companyTc, int stopSeq, int estimatedTotalStops)
        {
            string nf, jf;
            bool isSec;
            CalculateStopSectionFare(routeNumber, companyTc, stopSeq, estimatedTotalStops, out nf, out jf, out isSec);
            return string.Format("({0}, {1})", nf, jf);
        }

        public static string FormatBracketFare(string routeNumber, string companyTc)
        {
            string nf, jf;
            GetStandardRouteFares(routeNumber, companyTc, out nf, out jf);
            return string.Format("({0}, {1})", nf, jf);
        }

        // 3. FRANCHISED BUS COMPANY CLASSIFICATION & ROUTE SORTING
        public static void GetBusCompanyNames(string routeNum, out string compTc, out string compEn)
        {
            compTc = "九巴";
            compEn = "kmb";
            if (string.IsNullOrEmpty(routeNum)) return;
            string upper = routeNum.ToUpperInvariant();

            if (upper == "B10") { compTc = "嶼巴"; compEn = "nlb"; return; }
            if (upper.StartsWith("K") || upper == "506") { compTc = "港鐵巴士"; compEn = "mtr bus"; return; }
            if (upper.StartsWith("A") || upper.StartsWith("E") || upper.StartsWith("NA") ||
                upper.StartsWith("S1") || upper.StartsWith("S64") ||
                upper == "R33" || upper == "R42" || upper == "N30" || upper == "N31" ||
                upper == "N42" || upper == "N42A" || upper == "N64" ||
                upper == "X33" || upper == "X36" || upper == "X40" || upper == "X43" || upper == "X47")
            {
                compTc = "龍運";
                compEn = "lwb";
            }
        }

        public static bool IsAirportRoute(string routeNumber)
        {
            if (string.IsNullOrEmpty(routeNumber)) return false;
            string u = routeNumber.ToUpperInvariant();
            if (u.StartsWith("NA") && u.Length > 2 && char.IsDigit(u[2])) return true;
            if ((u.StartsWith("A") || u.StartsWith("E")) && u.Length > 1 && char.IsDigit(u[1])) return true;
            return false;
        }

        private static void ParseRouteNumberParts(string routeNo, out int prefixRank, out string prefixLetters, out int numberVal, out string suffixLetters)
        {
            prefixLetters = "";
            suffixLetters = "";
            numberVal = 99999;
            prefixRank = 0;

            if (string.IsNullOrEmpty(routeNo)) return;
            string s = routeNo.Trim().ToUpperInvariant();

            int i = 0;
            while (i < s.Length && char.IsLetter(s[i])) { prefixLetters += s[i]; i++; }
            string numStr = "";
            while (i < s.Length && char.IsDigit(s[i])) { numStr += s[i]; i++; }
            if (numStr.Length > 0) int.TryParse(numStr, out numberVal);
            if (i < s.Length) suffixLetters = s.Substring(i);
            prefixRank = (prefixLetters.Length == 0) ? 0 : 1;
        }

        public static int CompareRouteNumbersAscending(NearbyRouteItem a, NearbyRouteItem b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int rankA, rankB, numA, numB;
            string prefA, prefB, sufA, sufB;
            ParseRouteNumberParts(a.RouteNumber, out rankA, out prefA, out numA, out sufA);
            ParseRouteNumberParts(b.RouteNumber, out rankB, out prefB, out numB, out sufB);

            if (rankA != rankB) return rankA.CompareTo(rankB);
            if (prefA != prefB) return string.CompareOrdinal(prefA, prefB);
            if (numA != numB) return numA.CompareTo(numB);
            if (sufA != sufB) return string.CompareOrdinal(sufA, sufB);
            return string.CompareOrdinal(a.Direction, b.Direction);
        }

        public static List<NearbyRouteItem> SortRoutesAscendingWithAirportOnFirstScreen(List<NearbyRouteItem> inputRoutes)
        {
            List<NearbyRouteItem> normalRoutes = new List<NearbyRouteItem>();
            List<NearbyRouteItem> airportRoutes = new List<NearbyRouteItem>();

            for (int i = 0; i < inputRoutes.Count; i++)
            {
                if (IsAirportRoute(inputRoutes[i].RouteNumber)) airportRoutes.Add(inputRoutes[i]);
                else normalRoutes.Add(inputRoutes[i]);
            }

            normalRoutes.Sort(CompareRouteNumbersAscending);
            airportRoutes.Sort(CompareRouteNumbersAscending);

            if (airportRoutes.Count > 0)
            {
                int insertPos = Math.Min(2, normalRoutes.Count);
                if (insertPos == 0 && normalRoutes.Count > 0) insertPos = 1;
                normalRoutes.InsertRange(insertPos, airportRoutes);
            }
            return normalRoutes;
        }

        // 4. KMB STOP LIST LOADER
        public static async Task EnsureKmbStopsLoadedAsync()
        {
            if (_isKmbStopsLoaded && _kmbStopsList.Count > 0) return;

            bool restored = await CacheService.TryRestoreFromLocalDiskCacheAsync(
                _kmbStopsList, _kmbStopsMap, _allRoutesCatalog, _kmbRouteLookup);
            if (restored && _kmbStopsList.Count > 0)
            {
                _isKmbStopsLoaded = true;
                if (_allRoutesCatalog.Count > 800)
                {
                    RefreshDynamicTramAndMtrBusInCatalog(_allRoutesCatalog);
                    _isCatalogLoaded = true;
                }
                return;
            }

            try
            {
                string jsonText = await HttpGetStringAsync(KmbApiBase + "/stop", false);
                if (!string.IsNullOrEmpty(jsonText))
                {
                    JsonObject root = JsonObject.Parse(jsonText);
                    if (root.ContainsKey("data") && root["data"].ValueType == JsonValueType.Array)
                    {
                        JsonArray arr = root["data"].GetArray();
                        _kmbStopsList.Clear();
                        _kmbStopsMap.Clear();

                        for (int i = 0; i < arr.Count; i++)
                        {
                            JsonObject obj = arr[i].GetObject();
                            string stopId = GetJsonString(obj, "stop");
                            string nameTc = GetJsonString(obj, "name_tc");
                            string nameEn = GetJsonString(obj, "name_en");
                            double lat = 0, lon = 0;
                            double.TryParse(GetJsonString(obj, "lat"), NumberStyles.Any, CultureInfo.InvariantCulture, out lat);
                            double.TryParse(GetJsonString(obj, "long"), NumberStyles.Any, CultureInfo.InvariantCulture, out lon);

                            if (!string.IsNullOrEmpty(stopId) && lat > 20.0)
                            {
                                KnownHkStop stop = new KnownHkStop
                                {
                                    StopId = stopId,
                                    NameTc = nameTc,
                                    NameEn = string.IsNullOrEmpty(nameEn) ? nameTc : nameEn,
                                    Lat = lat,
                                    Lon = lon
                                };
                                _kmbStopsList.Add(stop);
                                _kmbStopsMap[stopId] = stop;
                            }
                        }
                        _isKmbStopsLoaded = (_kmbStopsList.Count > 0);
                    }
                }
            }
            catch (Exception) { }
        }

        private static void RefreshDynamicTramAndMtrBusInCatalog(List<NearbyRouteItem> catalog)
        {
            if (catalog == null) return;
            catalog.RemoveAll(r => r.TransportType == "電車" || r.CompanyTc == "港鐵巴士" || (r.RouteId != null && r.RouteId.StartsWith("MTRB_")));
            MtrBusService.AddCatalogRoutes(catalog);
            List<NearbyRouteItem> latestTrams = TramAndFerryService.GetAllTramRoutes();
            for (int i = 0; i < latestTrams.Count; i++) catalog.Add(latestTrams[i]);
        }

        // 5. MASTER ROUTE CATALOG BUILDER
        private static string NormalizeTerminalName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Replace("(", "").Replace(")", "").Replace("（", "").Replace("）", "")
                       .Replace("巴士總站", "").Replace("總站", "").Replace("站", "").Replace(" ", "").Trim();
        }

        private static bool AreTerminalsMatchingForJointRoute(NearbyRouteItem kmb, string ctbOrigTc, string ctbDestTc)
        {
            string kO = NormalizeTerminalName(kmb.OriginTc);
            string kD = NormalizeTerminalName(kmb.DestinationTc);
            string cO = NormalizeTerminalName(ctbOrigTc);
            string cD = NormalizeTerminalName(ctbDestTc);
            if (string.IsNullOrEmpty(kO) || string.IsNullOrEmpty(cO)) return false;

            bool origMatch = kO.Contains(cO) || cO.Contains(kO) || (kO.Length >= 2 && cO.Length >= 2 && kO.Substring(0, 2) == cO.Substring(0, 2));
            bool destMatch = kD.Contains(cD) || cD.Contains(kD) || (kD.Length >= 2 && cD.Length >= 2 && kD.Substring(0, 2) == cD.Substring(0, 2));
            return origMatch && destMatch;
        }

        public static async Task<List<NearbyRouteItem>> GetOrFetchAllRoutesAsync()
        {
            if (_isCatalogLoaded && _allRoutesCatalog.Count > 500) return _allRoutesCatalog;

            await _catalogLock.WaitAsync();
            try
            {
                if (_isCatalogLoaded && _allRoutesCatalog.Count > 500) return _allRoutesCatalog;

                bool restored = await CacheService.TryRestoreFromLocalDiskCacheAsync(
                    _kmbStopsList, _kmbStopsMap, _allRoutesCatalog, _kmbRouteLookup);
                if (restored) _isKmbStopsLoaded = true;
                if (_allRoutesCatalog.Count > 800)
                {
                    RefreshDynamicTramAndMtrBusInCatalog(_allRoutesCatalog);
                    _isCatalogLoaded = true;
                    return _allRoutesCatalog;
                }

                List<NearbyRouteItem> catalog = new List<NearbyRouteItem>();
                Dictionary<string, int> kmbDirectionCounts = new Dictionary<string, int>();

                // A. KMB & Long Win
                try
                {
                    string kmbJson = await HttpGetStringAsync(KmbApiBase + "/route/", false);
                    if (!string.IsNullOrEmpty(kmbJson))
                    {
                        JsonObject root = JsonObject.Parse(kmbJson);
                        if (root.ContainsKey("data") && root["data"].ValueType == JsonValueType.Array)
                        {
                            JsonArray arr = root["data"].GetArray();
                            for (int i = 0; i < arr.Count; i++)
                            {
                                JsonObject obj = arr[i].GetObject();
                                string cKey = GetJsonString(obj, "route") + "_" + GetJsonString(obj, "service_type");
                                kmbDirectionCounts[cKey] = kmbDirectionCounts.ContainsKey(cKey) ? (kmbDirectionCounts[cKey] + 1) : 1;
                            }

                            for (int i = 0; i < arr.Count; i++)
                            {
                                JsonObject obj = arr[i].GetObject();
                                string routeNum = GetJsonString(obj, "route");
                                string bound = GetJsonString(obj, "bound");
                                string serviceType = GetJsonString(obj, "service_type");
                                string origTc = GetJsonString(obj, "orig_tc");
                                string origEn = GetJsonString(obj, "orig_en");
                                string destTc = GetJsonString(obj, "dest_tc");
                                string destEn = GetJsonString(obj, "dest_en");

                                if (routeNum.StartsWith("K") && routeNum.Length <= 4 && char.IsDigit(routeNum[1])) continue;

                                string compTc, compEn;
                                GetBusCompanyNames(routeNum, out compTc, out compEn);
                                string cKey = routeNum + "_" + serviceType;
                                bool hasOpposite = kmbDirectionCounts.ContainsKey(cKey) && kmbDirectionCounts[cKey] > 1;

                                string routeId = string.Format("KMB_{0}_{1}_{2}", routeNum, bound, serviceType);
                                NearbyRouteItem item = new NearbyRouteItem
                                {
                                    RouteId = routeId,
                                    RouteNumber = routeNum,
                                    Direction = bound,
                                    ServiceType = serviceType,
                                    SpecialNote = (serviceType != "1") ? "特別班" : string.Empty,
                                    CompanyTc = compTc,
                                    CompanyEn = compEn,
                                    TransportType = "巴士",
                                    HasOppositeDirection = hasOpposite,
                                    OriginTc = origTc,
                                    OriginEn = origEn,
                                    DestinationTc = destTc,
                                    DestinationEn = destEn,
                                    StopNameTc = origTc,
                                    StopNameEn = origEn,
                                    FareText = FormatBracketFare(routeNum, compTc)
                                };
                                catalog.Add(item);
                                _kmbRouteLookup[routeId] = item;
                            }
                        }
                    }
                }
                catch (Exception) { }

                // B. Citybus (with Joint KMB/CTB Route Merger)
                try
                {
                    string ctbJson = await HttpGetStringAsync(CtbApiBase + "/route/ctb", false);
                    if (!string.IsNullOrEmpty(ctbJson))
                    {
                        JsonObject root = JsonObject.Parse(ctbJson);
                        if (root.ContainsKey("data") && root["data"].ValueType == JsonValueType.Array)
                        {
                            JsonArray arr = root["data"].GetArray();
                            for (int i = 0; i < arr.Count; i++)
                            {
                                JsonObject obj = arr[i].GetObject();
                                string routeNum = GetJsonString(obj, "route");
                                string origTc = GetJsonString(obj, "orig_tc");
                                string origEn = GetJsonString(obj, "orig_en");
                                string destTc = GetJsonString(obj, "dest_tc");
                                string destEn = GetJsonString(obj, "dest_en");

                                NearbyRouteItem kmbO = _kmbRouteLookup.ContainsKey("KMB_" + routeNum + "_O_1") ? _kmbRouteLookup["KMB_" + routeNum + "_O_1"] : null;
                                NearbyRouteItem kmbI = _kmbRouteLookup.ContainsKey("KMB_" + routeNum + "_I_1") ? _kmbRouteLookup["KMB_" + routeNum + "_I_1"] : null;

                                bool mergedOutbound = false;
                                if (kmbO != null && AreTerminalsMatchingForJointRoute(kmbO, origTc, destTc))
                                {
                                    kmbO.IsJointKmbCtb = true; kmbO.CompanyTc = "九巴/城巴"; kmbO.CompanyEn = "kmb/ctb"; mergedOutbound = true;
                                }
                                else if (kmbI != null && AreTerminalsMatchingForJointRoute(kmbI, origTc, destTc))
                                {
                                    kmbI.IsJointKmbCtb = true; kmbI.CompanyTc = "九巴/城巴"; kmbI.CompanyEn = "kmb/ctb"; mergedOutbound = true;
                                }

                                bool mergedInbound = false;
                                if (kmbI != null && AreTerminalsMatchingForJointRoute(kmbI, destTc, origTc))
                                {
                                    kmbI.IsJointKmbCtb = true; kmbI.CompanyTc = "九巴/城巴"; kmbI.CompanyEn = "kmb/ctb"; mergedInbound = true;
                                }
                                else if (kmbO != null && AreTerminalsMatchingForJointRoute(kmbO, destTc, origTc))
                                {
                                    kmbO.IsJointKmbCtb = true; kmbO.CompanyTc = "九巴/城巴"; kmbO.CompanyEn = "kmb/ctb"; mergedInbound = true;
                                }

                                bool isCircularCtb = string.Equals(origTc, destTc, StringComparison.OrdinalIgnoreCase) || destTc.Contains("循環線");

                                if (!mergedOutbound)
                                {
                                    NearbyRouteItem ctbOut = new NearbyRouteItem
                                    {
                                        RouteId = string.Format("CTB_{0}_O", routeNum),
                                        RouteNumber = routeNum,
                                        Direction = "O",
                                        ServiceType = "1",
                                        CompanyTc = "城巴",
                                        CompanyEn = "ctb",
                                        TransportType = "巴士",
                                        HasOppositeDirection = !isCircularCtb,
                                        OriginTc = origTc,
                                        OriginEn = origEn,
                                        DestinationTc = destTc,
                                        DestinationEn = destEn,
                                        StopNameTc = origTc,
                                        StopNameEn = origEn,
                                        FareText = FormatBracketFare(routeNum, "城巴")
                                    };
                                    catalog.Add(ctbOut);
                                    _ctbRouteLookup[ctbOut.RouteId] = ctbOut;
                                }

                                if (!mergedInbound && !isCircularCtb)
                                {
                                    NearbyRouteItem ctbIn = new NearbyRouteItem
                                    {
                                        RouteId = string.Format("CTB_{0}_I", routeNum),
                                        RouteNumber = routeNum,
                                        Direction = "I",
                                        ServiceType = "1",
                                        CompanyTc = "城巴",
                                        CompanyEn = "ctb",
                                        TransportType = "巴士",
                                        HasOppositeDirection = true,
                                        OriginTc = destTc,
                                        OriginEn = destEn,
                                        DestinationTc = origTc,
                                        DestinationEn = origEn,
                                        StopNameTc = destTc,
                                        StopNameEn = destEn,
                                        FareText = FormatBracketFare(routeNum, "城巴")
                                    };
                                    catalog.Add(ctbIn);
                                    _ctbRouteLookup[ctbIn.RouteId] = ctbIn;
                                }
                            }
                        }
                    }
                }
                catch (Exception) { }

                // C. MTR Bus (via MtrBusService)
                MtrBusService.AddCatalogRoutes(catalog);

                // D. New Lantao Bus (NLB)
                try
                {
                    string nlbJson = await HttpGetStringAsync(NlbApiBase + "/route.php?action=list", false);
                    if (!string.IsNullOrEmpty(nlbJson))
                    {
                        JsonObject root = JsonObject.Parse(nlbJson);
                        if (root.ContainsKey("routes") && root["routes"].ValueType == JsonValueType.Array)
                        {
                            JsonArray arr = root["routes"].GetArray();
                            for (int i = 0; i < arr.Count; i++)
                            {
                                JsonObject obj = arr[i].GetObject();
                                string nlbRouteId = GetJsonString(obj, "routeId");
                                string routeNo = GetJsonString(obj, "routeNo");
                                string routeNameC = GetJsonString(obj, "routeName_c");
                                string routeNameE = GetJsonString(obj, "routeName_e");
                                string specialRoute = GetJsonString(obj, "specialRoute");

                                string origTc = routeNameC, destTc = routeNameC;
                                if (routeNameC.Contains(">")) { string[] p = routeNameC.Split('>'); origTc = p[0].Trim(); destTc = p[p.Length - 1].Trim(); }
                                string origEn = routeNameE, destEn = routeNameE;
                                if (routeNameE.Contains(">")) { string[] pE = routeNameE.Split('>'); origEn = pE[0].Trim(); destEn = pE[pE.Length - 1].Trim(); }

                                catalog.Add(new NearbyRouteItem
                                {
                                    RouteId = string.Format("NLB_{0}_{1}", routeNo, nlbRouteId),
                                    RouteNumber = routeNo,
                                    Direction = "O",
                                    ServiceType = nlbRouteId,
                                    SpecialNote = (specialRoute == "1") ? "特別班" : string.Empty,
                                    CompanyTc = "嶼巴",
                                    CompanyEn = "nlb",
                                    TransportType = "巴士",
                                    HasOppositeDirection = !destTc.Contains("循環"),
                                    OriginTc = origTc,
                                    OriginEn = origEn,
                                    DestinationTc = destTc,
                                    DestinationEn = destEn,
                                    StopNameTc = origTc,
                                    StopNameEn = origEn,
                                    FareText = FormatBracketFare(routeNo, "嶼巴")
                                });
                            }
                        }
                    }
                }
                catch (Exception) { }

                // E. Green Minibus (Both Directions via GreenMinibusService)
                await GreenMinibusService.AddCatalogRoutesAsync(catalog);

                // F. Railway, All 12 Tram Destination Routes & Ferries
                List<NearbyRouteItem> rails = RailwayService.GetAllRailwayRoutes();
                for (int i = 0; i < rails.Count; i++) catalog.Add(rails[i]);

                List<NearbyRouteItem> trams = TramAndFerryService.GetAllTramRoutes();
                for (int i = 0; i < trams.Count; i++) catalog.Add(trams[i]);

                List<NearbyRouteItem> ferries = TramAndFerryService.GetAllFerryRoutes();
                for (int i = 0; i < ferries.Count; i++) catalog.Add(ferries[i]);

                if (catalog.Count > 0)
                {
                    catalog.Sort(CompareRouteNumbersAscending);
                    _allRoutesCatalog = catalog;
                    _isCatalogLoaded = true;
                    var saveTask = CacheService.SaveToLocalDiskCacheAsync(_kmbStopsList, _allRoutesCatalog);
                }
            }
            finally
            {
                _catalogLock.Release();
            }

            return _allRoutesCatalog;
        }

        // 6. NEARBY FRANCHISED BUS FETCHER
        public static async Task<List<NearbyRouteItem>> GetFallbackCatalogRoutesAsync(string transportType)
        {
            List<NearbyRouteItem> all = await GetOrFetchAllRoutesAsync();
            List<NearbyRouteItem> filtered = new List<NearbyRouteItem>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].TransportType == transportType) filtered.Add(all[i]);
            }

            List<NearbyRouteItem> sorted = SortRoutesAscendingWithAirportOnFirstScreen(filtered);
            List<NearbyRouteItem> topList = new List<NearbyRouteItem>();
            int limit = Math.Min(45, sorted.Count);
            for (int i = 0; i < limit; i++) topList.Add(sorted[i]);
            return topList;
        }

        public static async Task<List<NearbyRouteItem>> FetchNearbyBusRoutesLiveAsync(double userLat, double userLon)
        {
            await EnsureKmbStopsLoadedAsync();
            await GetOrFetchAllRoutesAsync();

            int radius = ThemeManager.SearchRadiusMeters;
            List<KnownHkStop> closestStops = GeoMapService.FindNearestStopsWithinRadius(userLat, userLon, radius, 8);
            List<NearbyRouteItem> results = new List<NearbyRouteItem>();
            Dictionary<string, NearbyRouteItem> grouped = new Dictionary<string, NearbyRouteItem>();

            foreach (KnownHkStop stop in closestStops)
            {
                try
                {
                    string url = string.Format("{0}/stop-eta/{1}", KmbApiBase, stop.StopId);
                    string jsonText = await HttpGetStringAsync(url, true);
                    if (string.IsNullOrEmpty(jsonText)) continue;

                    JsonObject root = JsonObject.Parse(jsonText);
                    if (!root.ContainsKey("data") || root["data"].ValueType != JsonValueType.Array) continue;

                    JsonArray dataArray = root["data"].GetArray();
                    for (int i = 0; i < dataArray.Count; i++)
                    {
                        JsonObject item = dataArray[i].GetObject();
                        string routeNum = GetJsonString(item, "route");
                        string dir = GetJsonString(item, "dir");
                        string serviceType = GetJsonString(item, "service_type");
                        string destTc = GetJsonString(item, "dest_tc");
                        string destEn = GetJsonString(item, "dest_en");
                        string etaStr = GetJsonString(item, "eta");
                        int stopSeq = 1;
                        int.TryParse(GetJsonString(item, "seq"), out stopSeq);

                        if (string.IsNullOrEmpty(routeNum) || string.IsNullOrEmpty(destTc)) continue;

                        string routeKey = string.Format("KMB_{0}_{1}_{2}", routeNum, dir, serviceType);
                        if (!grouped.ContainsKey(routeKey))
                        {
                            string origTc = "總站", origEn = "Terminus";
                            bool isJoint = false, hasOpp = true;
                            string compTc, compEn;
                            GetBusCompanyNames(routeNum, out compTc, out compEn);

                            if (_kmbRouteLookup.ContainsKey(routeKey))
                            {
                                NearbyRouteItem catItem = _kmbRouteLookup[routeKey];
                                origTc = catItem.OriginTc;
                                origEn = catItem.OriginEn;
                                isJoint = catItem.IsJointKmbCtb;
                                hasOpp = catItem.HasOppositeDirection;
                                if (isJoint) { compTc = "九巴/城巴"; compEn = "kmb/ctb"; }
                            }

                            NearbyRouteItem routeItem = new NearbyRouteItem
                            {
                                RouteId = routeKey,
                                RouteNumber = routeNum,
                                Direction = string.IsNullOrEmpty(dir) ? "O" : dir,
                                ServiceType = string.IsNullOrEmpty(serviceType) ? "1" : serviceType,
                                SpecialNote = (serviceType != "1" && !string.IsNullOrEmpty(serviceType)) ? "特別班" : string.Empty,
                                CompanyTc = compTc,
                                CompanyEn = compEn,
                                IsJointKmbCtb = isJoint,
                                HasOppositeDirection = hasOpp,
                                TransportType = "巴士",
                                OriginTc = origTc,
                                OriginEn = origEn,
                                DestinationTc = destTc,
                                DestinationEn = string.IsNullOrEmpty(destEn) ? destTc : destEn,
                                NearestStopId = stop.StopId,
                                NearestStopSeq = stopSeq,
                                StopNameTc = stop.NameTc,
                                StopNameEn = stop.NameEn,
                                FareText = FormatBracketFareForStop(routeNum, compTc, stopSeq, 28)
                            };
                            grouped[routeKey] = routeItem;
                            results.Add(routeItem);
                        }

                        NearbyRouteItem currentItem = grouped[routeKey];
                        if (currentItem.NearestStopId == stop.StopId && !string.IsNullOrEmpty(etaStr))
                        {
                            DateTimeOffset parsedDto;
                            if (DateTimeOffset.TryParse(etaStr, out parsedDto))
                            {
                                DateTime localEta = parsedDto.LocalDateTime;
                                double diffMins = (localEta - DateTime.Now).TotalMinutes;
                                if (diffMins >= -0.5 && diffMins <= 60) AddArrivalTimestampSorted(currentItem, localEta);
                            }
                        }
                    }
                }
                catch (Exception) { }
            }

            return SortRoutesAscendingWithAirportOnFirstScreen(results);
        }

        public static void AddArrivalTimestampSorted(NearbyRouteItem item, DateTime eta)
        {
            List<DateTime> list = new List<DateTime>();
            if (item.FirstArrivalTime.HasValue) list.Add(item.FirstArrivalTime.Value);
            if (item.SecondArrivalTime.HasValue) list.Add(item.SecondArrivalTime.Value);
            if (item.ThirdArrivalTime.HasValue) list.Add(item.ThirdArrivalTime.Value);

            for (int i = 0; i < list.Count; i++)
            {
                if (Math.Abs((list[i] - eta).TotalSeconds) < 25) return;
            }

            list.Add(eta);
            list.Sort();

            item.FirstArrivalTime = list.Count >= 1 ? (DateTime?)list[0] : null;
            item.SecondArrivalTime = list.Count >= 2 ? (DateTime?)list[1] : null;
            item.ThirdArrivalTime = list.Count >= 3 ? (DateTime?)list[2] : null;
        }

        // 7. FAVOURITE ROUTE + STATION LIVE ETA REFRESHER
        public static async Task<List<NearbyRouteItem>> FetchFavouriteRoutesLiveAsync(bool isClockMode)
        {
            List<FavouriteStationEntry> favs = ThemeManager.GetFavourites();
            List<NearbyRouteItem> results = new List<NearbyRouteItem>();

            for (int i = 0; i < favs.Count; i++)
            {
                FavouriteStationEntry f = favs[i];
                NearbyRouteItem item = new NearbyRouteItem
                {
                    RouteId = f.RouteId,
                    RouteNumber = f.RouteNumber,
                    CompanyTc = f.CompanyTc,
                    CompanyEn = f.CompanyEn,
                    TransportType = f.TransportType,
                    Direction = f.Direction,
                    ServiceType = f.ServiceType,
                    OriginTc = f.OriginTc,
                    OriginEn = f.OriginEn,
                    DestinationTc = f.DestinationTc,
                    DestinationEn = f.DestinationEn,
                    NearestStopId = f.StopId,
                    NearestStopSeq = f.StopSeq,
                    StopNameTc = f.StopNameTc,
                    StopNameEn = f.StopNameEn,
                    FareText = (f.TransportType == "鐵路") ? string.Empty : (string.IsNullOrEmpty(f.FareText) ? FormatBracketFare(f.RouteNumber, f.CompanyTc) : f.FareText),
                    GmbRegion = f.GmbRegion,
                    GmbRouteId = f.GmbRouteId,
                    GmbRouteSeq = f.GmbRouteSeq,
                    ShowClockMode = isClockMode
                };

                BusStopItem tempStop = new BusStopItem
                {
                    RouteId = f.RouteId,
                    StopId = f.StopId,
                    Sequence = f.StopSeq,
                    StopNameTc = f.StopNameTc,
                    StopNameEn = f.StopNameEn,
                    IsRailwayStop = (f.TransportType == "鐵路"),
                    HasTimetable = TimetableService.HasOfficialTimetable(item)
                };

                await FetchStopEtaLiveAsync(item, tempStop);
                List<int> validMins = tempStop.GetValidEtasWithin60Minutes();
                DateTime now = DateTime.Now;
                if (validMins.Count >= 1) item.FirstArrivalTime = now.AddMinutes(validMins[0]);
                if (validMins.Count >= 2) item.SecondArrivalTime = now.AddMinutes(validMins[1]);
                if (validMins.Count >= 3) item.ThirdArrivalTime = now.AddMinutes(validMins[2]);
                item.RefreshTimeDisplay();

                results.Add(item);
            }
            return results;
        }

        // 8. FRANCHISED BUS STOPS & DISPATCHER TO MODULAR SERVICES
        public static async Task FetchRouteStopsLiveAsync(NearbyRouteItem route)
        {
            if (route == null) return;

            if (route.TransportType == "鐵路") { await RailwayService.PopulateRailwayStopsAndEtasAsync(route); return; }
            if (route.TransportType == "電車") { TramAndFerryService.PopulateTramwayStopsAndEtas(route); return; }
            if (route.TransportType == "渡輪") { TramAndFerryService.PopulateFerryPiersAndEtas(route); return; }
            if (route.TransportType == "小巴") { await GreenMinibusService.FetchRouteStopsLiveAsync(route); return; }
            if (route.CompanyTc == "港鐵巴士" || (route.RouteId != null && route.RouteId.StartsWith("MTRB_")))
            {
                await MtrBusService.PopulateRouteStopsAndLiveEtasAsync(route);
                return;
            }
            if (route.CompanyTc == "城巴") { await FetchCitybusRouteStopsLiveAsync(route); return; }
            if (route.CompanyTc == "嶼巴" && route.RouteId.StartsWith("NLB_")) { await FetchNlbRouteStopsLiveAsync(route); return; }

            await EnsureKmbStopsLoadedAsync();

            try
            {
                string directionWord = (route.Direction == "I") ? "inbound" : "outbound";
                string serviceType = string.IsNullOrEmpty(route.ServiceType) ? "1" : route.ServiceType;
                string url = string.Format("{0}/route-stop/{1}/{2}/{3}", KmbApiBase, route.RouteNumber, directionWord, serviceType);

                string jsonText = await HttpGetStringAsync(url, false);
                if (string.IsNullOrEmpty(jsonText)) return;

                JsonObject root = JsonObject.Parse(jsonText);
                if (!root.ContainsKey("data") || root["data"].ValueType != JsonValueType.Array) return;

                JsonArray arr = root["data"].GetArray();
                int totalStops = arr.Count;
                if (totalStops == 0) return;

                List<BusStopItem> liveStops = new List<BusStopItem>();
                for (int i = 0; i < totalStops; i++)
                {
                    JsonObject obj = arr[i].GetObject();
                    string stopId = GetJsonString(obj, "stop");
                    int seq = i + 1;
                    int.TryParse(GetJsonString(obj, "seq"), out seq);

                    KnownHkStop meta = _kmbStopsMap.ContainsKey(stopId)
                        ? _kmbStopsMap[stopId]
                        : await FetchSingleKmbStopAsync(stopId, seq);

                    string normFare, joyFare;
                    bool isSection;
                    CalculateStopSectionFare(route.RouteNumber, route.CompanyTc, seq, totalStops, out normFare, out joyFare, out isSection);

                    liveStops.Add(new BusStopItem
                    {
                        RouteId = route.RouteId,
                        StopId = stopId,
                        Sequence = seq,
                        StopNameTc = meta.NameTc,
                        StopNameEn = meta.NameEn,
                        NormalFare = normFare,
                        JoyYouFare = joyFare,
                        IsSectionFare = isSection,
                        HasTimetable = true,
                        Latitude = meta.Lat,
                        Longitude = meta.Lon,
                        IsFavouriteStop = ThemeManager.IsFavourite(route.RouteId, stopId, seq),
                        IsExpanded = false
                    });
                }

                if (liveStops.Count > 0)
                {
                    int expandIndex = GeoMapService.SelectNearestOrFirstStopIndex(liveStops, route.NearestStopId);
                    liveStops[expandIndex].IsExpanded = true;
                    route.Stops = liveStops;
                    await FetchStopEtaLiveAsync(route, liveStops[expandIndex]);
                }
            }
            catch (Exception) { }
        }

        private static async Task FetchNlbRouteStopsLiveAsync(NearbyRouteItem route)
        {
            try
            {
                string nlbRouteId = route.ServiceType;
                string url = string.Format("{0}/stop.php?action=list&routeId={1}", NlbApiBase, nlbRouteId);
                string jsonText = await HttpGetStringAsync(url, false);
                if (string.IsNullOrEmpty(jsonText)) return;

                JsonObject root = JsonObject.Parse(jsonText);
                if (!root.ContainsKey("stops") || root["stops"].ValueType != JsonValueType.Array) return;

                JsonArray arr = root["stops"].GetArray();
                List<BusStopItem> liveStops = new List<BusStopItem>();
                string firstFare = "";

                for (int i = 0; i < arr.Count; i++)
                {
                    JsonObject obj = arr[i].GetObject();
                    string stopId = GetJsonString(obj, "stopId");
                    string stopNameC = GetJsonString(obj, "stopName_c");
                    string stopNameE = GetJsonString(obj, "stopName_e");
                    string fare = GetJsonString(obj, "fare");
                    double lat = 22.2645, lon = 113.9420;
                    double.TryParse(GetJsonString(obj, "latitude"), NumberStyles.Any, CultureInfo.InvariantCulture, out lat);
                    double.TryParse(GetJsonString(obj, "longitude"), NumberStyles.Any, CultureInfo.InvariantCulture, out lon);

                    string nFare = (!string.IsNullOrEmpty(fare) && fare != "0") ? ("$" + fare) : "$10.8";
                    if (i == 0) firstFare = nFare;
                    bool isSec = (i > 0 && nFare != firstFare && fare != "0");
                    int seq = i + 1;

                    liveStops.Add(new BusStopItem
                    {
                        RouteId = route.RouteId,
                        StopId = stopId,
                        Sequence = seq,
                        StopNameTc = stopNameC,
                        StopNameEn = string.IsNullOrEmpty(stopNameE) ? stopNameC : stopNameE,
                        NormalFare = nFare,
                        JoyYouFare = BusStopItem.CalculateJoyYouFare(nFare),
                        IsSectionFare = isSec,
                        HasTimetable = true,
                        Latitude = lat,
                        Longitude = lon,
                        IsFavouriteStop = ThemeManager.IsFavourite(route.RouteId, stopId, seq),
                        IsExpanded = false
                    });
                }

                if (liveStops.Count > 0)
                {
                    int expandIdx = GeoMapService.SelectNearestOrFirstStopIndex(liveStops, route.NearestStopId);
                    liveStops[expandIdx].IsExpanded = true;
                    route.Stops = liveStops;
                    await FetchStopEtaLiveAsync(route, liveStops[expandIdx]);
                }
            }
            catch (Exception) { }
        }

        private static async Task FetchCitybusRouteStopsLiveAsync(NearbyRouteItem route)
        {
            try
            {
                string directionWord = (route.Direction == "I") ? "inbound" : "outbound";
                string url = string.Format("{0}/route-stop/ctb/{1}/{2}", CtbApiBase, route.RouteNumber, directionWord);
                string jsonText = await HttpGetStringAsync(url, false);
                if (string.IsNullOrEmpty(jsonText)) return;

                JsonObject root = JsonObject.Parse(jsonText);
                if (!root.ContainsKey("data") || root["data"].ValueType != JsonValueType.Array) return;

                JsonArray arr = root["data"].GetArray();
                List<BusStopItem> liveStops = new List<BusStopItem>();
                int limit = Math.Min(38, arr.Count);

                for (int i = 0; i < limit; i++)
                {
                    JsonObject obj = arr[i].GetObject();
                    string stopId = GetJsonString(obj, "stop");
                    int seq = i + 1;
                    int.TryParse(GetJsonString(obj, "seq"), out seq);

                    KnownHkStop meta = await FetchCitybusStopMetadataAsync(stopId, seq);

                    string normFare, joyFare;
                    bool isSection;
                    CalculateStopSectionFare(route.RouteNumber, route.CompanyTc, seq, limit, out normFare, out joyFare, out isSection);

                    liveStops.Add(new BusStopItem
                    {
                        RouteId = route.RouteId,
                        StopId = stopId,
                        Sequence = seq,
                        StopNameTc = meta.NameTc,
                        StopNameEn = meta.NameEn,
                        NormalFare = normFare,
                        JoyYouFare = joyFare,
                        IsSectionFare = isSection,
                        HasTimetable = true,
                        Latitude = meta.Lat,
                        Longitude = meta.Lon,
                        IsFavouriteStop = ThemeManager.IsFavourite(route.RouteId, stopId, seq),
                        IsExpanded = false
                    });
                }

                if (liveStops.Count > 0)
                {
                    int expandIdx = GeoMapService.SelectNearestOrFirstStopIndex(liveStops, route.NearestStopId);
                    liveStops[expandIdx].IsExpanded = true;
                    route.Stops = liveStops;
                    await FetchStopEtaLiveAsync(route, liveStops[expandIdx]);
                }
            }
            catch (Exception) { }
        }

        private static async Task<KnownHkStop> FetchCitybusStopMetadataAsync(string stopId, int seq)
        {
            try
            {
                string url = string.Format("{0}/stop/{1}", CtbApiBase, stopId);
                string jsonText = await HttpGetStringAsync(url, false);
                if (!string.IsNullOrEmpty(jsonText))
                {
                    JsonObject root = JsonObject.Parse(jsonText);
                    if (root.ContainsKey("data") && root["data"].ValueType == JsonValueType.Object)
                    {
                        JsonObject data = root["data"].GetObject();
                        string nameTc = GetJsonString(data, "name_tc");
                        string nameEn = GetJsonString(data, "name_en");
                        double lat = 22.2820, lon = 114.1580;
                        double.TryParse(GetJsonString(data, "lat"), NumberStyles.Any, CultureInfo.InvariantCulture, out lat);
                        double.TryParse(GetJsonString(data, "long"), NumberStyles.Any, CultureInfo.InvariantCulture, out lon);
                        return new KnownHkStop { StopId = stopId, NameTc = nameTc, NameEn = string.IsNullOrEmpty(nameEn) ? nameTc : nameEn, Lat = lat, Lon = lon };
                    }
                }
            }
            catch (Exception) { }
            return new KnownHkStop { StopId = stopId, NameTc = "城巴站 #" + seq, NameEn = "Citybus Stop #" + seq, Lat = 22.2820, Lon = 114.1580 };
        }

        private static async Task<KnownHkStop> FetchSingleKmbStopAsync(string stopId, int seq)
        {
            try
            {
                string url = string.Format("{0}/stop/{1}", KmbApiBase, stopId);
                string jsonText = await HttpGetStringAsync(url, false);
                if (!string.IsNullOrEmpty(jsonText))
                {
                    JsonObject root = JsonObject.Parse(jsonText);
                    if (root.ContainsKey("data") && root["data"].ValueType == JsonValueType.Object)
                    {
                        JsonObject data = root["data"].GetObject();
                        string nameTc = GetJsonString(data, "name_tc");
                        string nameEn = GetJsonString(data, "name_en");
                        double lat = 22.3019, lon = 114.1772;
                        double.TryParse(GetJsonString(data, "lat"), NumberStyles.Any, CultureInfo.InvariantCulture, out lat);
                        double.TryParse(GetJsonString(data, "long"), NumberStyles.Any, CultureInfo.InvariantCulture, out lon);

                        KnownHkStop stop = new KnownHkStop
                        {
                            StopId = stopId,
                            NameTc = string.IsNullOrEmpty(nameTc) ? string.Format("巴士站 #{0}", seq) : nameTc,
                            NameEn = string.IsNullOrEmpty(nameEn) ? nameTc : nameEn,
                            Lat = lat,
                            Lon = lon
                        };
                        _kmbStopsMap[stopId] = stop;
                        return stop;
                    }
                }
            }
            catch (Exception) { }
            return new KnownHkStop { StopId = stopId, NameTc = "巴士站 #" + seq, NameEn = "Bus Stop #" + seq, Lat = 22.3019, Lon = 114.1772 };
        }

        public static string SanitizeEtaRemark(string rawRemark)
        {
            if (string.IsNullOrEmpty(rawRemark)) return string.Empty;
            string trimmed = rawRemark.Trim();

            if (trimmed.ToLowerInvariant().Contains("scheduled") || trimmed.Contains("預定") || trimmed.Contains("預計"))
            {
                return ThemeManager.Tr("預定班次", "scheduled");
            }

            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c >= 0x0080 && c <= 0x024F)
                {
                    return ThemeManager.Tr("預定班次", "scheduled");
                }
            }
            return trimmed;
        }

        // 9. UNIFIED STOP ETA ENGINE
        public static async Task FetchStopEtaLiveAsync(NearbyRouteItem route, BusStopItem stop)
        {
            if (route == null || stop == null) return;

            if (route.TransportType == "鐵路") { await RailwayService.FetchRailwayStationLiveEtaAsync(route, stop); return; }
            if (route.TransportType == "電車" || route.TransportType == "渡輪") { stop.IsLoadingEta = false; stop.RefreshEtaBindings(); return; }
            if (route.TransportType == "小巴") { await GreenMinibusService.FetchStopEtaLiveAsync(route, stop); return; }
            if (route.CompanyTc == "港鐵巴士" || (route.RouteId != null && route.RouteId.StartsWith("MTRB_")))
            {
                stop.IsLoadingEta = true;
                stop.RefreshEtaBindings();
                await MtrBusService.FetchSingleStopEtaLiveAsync(route, stop);
                stop.IsLoadingEta = false;
                stop.RefreshEtaBindings();
                return;
            }

            stop.IsLoadingEta = true;
            stop.RefreshEtaBindings();

            try
            {
                List<int> minsList = new List<int>();
                List<string> remarksList = new List<string>();

                if (route.CompanyTc == "嶼巴" && route.RouteId.StartsWith("NLB_") && !string.IsNullOrEmpty(stop.StopId))
                {
                    string lang = ThemeManager.IsEnglish ? "en" : "zh";
                    string url = string.Format("{0}/stop.php?action=estimatedArrivals&routeId={1}&stopId={2}&language={3}",
                        NlbApiBase, route.ServiceType, stop.StopId, lang);
                    string jsonText = await HttpGetStringAsync(url, true);

                    if (!string.IsNullOrEmpty(jsonText))
                    {
                        JsonObject root = JsonObject.Parse(jsonText);
                        if (root.ContainsKey("estimatedArrivals") && root["estimatedArrivals"].ValueType == JsonValueType.Array)
                        {
                            JsonArray arr = root["estimatedArrivals"].GetArray();
                            for (int i = 0; i < arr.Count; i++)
                            {
                                JsonObject item = arr[i].GetObject();
                                string etaTimeStr = GetJsonString(item, "estimatedArrivalTime");
                                DateTime parsedDt;
                                if (!string.IsNullOrEmpty(etaTimeStr) && DateTime.TryParse(etaTimeStr, out parsedDt))
                                {
                                    int diffMins = (int)Math.Max(0, Math.Floor((parsedDt - DateTime.Now).TotalMinutes));
                                    if (diffMins <= 60 && minsList.Count < 3)
                                    {
                                        minsList.Add(diffMins);
                                        remarksList.Add(string.Empty);
                                    }
                                }
                            }
                        }
                    }
                }
                else if (route.CompanyTc == "城巴" && !string.IsNullOrEmpty(stop.StopId))
                {
                    string url = string.Format("{0}/eta/ctb/{1}/{2}", CtbApiBase, stop.StopId, route.RouteNumber);
                    string jsonText = await HttpGetStringAsync(url, true);
                    ParseBusEtaArray(jsonText, route.Direction, minsList, remarksList, "");
                }
                else if (!string.IsNullOrEmpty(stop.StopId))
                {
                    string serviceType = string.IsNullOrEmpty(route.ServiceType) ? "1" : route.ServiceType;
                    string url = string.Format("{0}/eta/{1}/{2}/{3}", KmbApiBase, stop.StopId, route.RouteNumber, serviceType);
                    string jsonText = await HttpGetStringAsync(url, true);
                    string tag = route.IsJointKmbCtb ? ThemeManager.Tr("九巴", "kmb") : "";
                    ParseBusEtaArray(jsonText, route.Direction, minsList, remarksList, tag);

                    if (route.IsJointKmbCtb)
                    {
                        await MergeJointCitybusStopEtaAsync(route, stop.Sequence, minsList, remarksList);
                    }
                }

                stop.LiveEtaMinutes = minsList;
                stop.LiveEtaRemarks = remarksList;
            }
            catch (Exception) { }

            stop.IsLoadingEta = false;
            stop.RefreshEtaBindings();
        }

        private static async Task MergeJointCitybusStopEtaAsync(NearbyRouteItem route, int stopSeq, List<int> minsList, List<string> remarksList)
        {
            try
            {
                string directionWord = (route.Direction == "I") ? "inbound" : "outbound";
                string rsUrl = string.Format("{0}/route-stop/ctb/{1}/{2}", CtbApiBase, route.RouteNumber, directionWord);
                string rsJson = await HttpGetStringAsync(rsUrl, false);
                if (string.IsNullOrEmpty(rsJson)) return;

                JsonObject root = JsonObject.Parse(rsJson);
                if (!root.ContainsKey("data") || root["data"].ValueType != JsonValueType.Array) return;

                JsonArray arr = root["data"].GetArray();
                string ctbStopId = "";
                for (int i = 0; i < arr.Count; i++)
                {
                    JsonObject o = arr[i].GetObject();
                    int s = 0;
                    int.TryParse(GetJsonString(o, "seq"), out s);
                    if (s == stopSeq) { ctbStopId = GetJsonString(o, "stop"); break; }
                }

                if (!string.IsNullOrEmpty(ctbStopId))
                {
                    string etaUrl = string.Format("{0}/eta/ctb/{1}/{2}", CtbApiBase, ctbStopId, route.RouteNumber);
                    string etaJson = await HttpGetStringAsync(etaUrl, true);
                    ParseBusEtaArray(etaJson, route.Direction, minsList, remarksList, ThemeManager.Tr("城巴", "ctb"));
                }
            }
            catch (Exception) { }
        }

        private static void ParseBusEtaArray(string jsonText, string expectedDir, List<int> minsList, List<string> remarksList, string operatorTag)
        {
            if (string.IsNullOrEmpty(jsonText)) return;
            JsonObject root = JsonObject.Parse(jsonText);
            if (!root.ContainsKey("data") || root["data"].ValueType != JsonValueType.Array) return;

            JsonArray arr = root["data"].GetArray();
            for (int i = 0; i < arr.Count; i++)
            {
                JsonObject item = arr[i].GetObject();
                string dir = GetJsonString(item, "dir");
                if (!string.IsNullOrEmpty(expectedDir) && !string.IsNullOrEmpty(dir) && dir != expectedDir) continue;

                string etaStr = GetJsonString(item, "eta");
                string rmk = SanitizeEtaRemark(ThemeManager.IsEnglish ? GetJsonString(item, "rmk_en") : GetJsonString(item, "rmk_tc"));
                if (!string.IsNullOrEmpty(operatorTag)) rmk = string.IsNullOrEmpty(rmk) ? operatorTag : (operatorTag + " " + rmk);

                if (!string.IsNullOrEmpty(etaStr))
                {
                    DateTimeOffset dto;
                    if (DateTimeOffset.TryParse(etaStr, out dto))
                    {
                        int diffMins = (int)Math.Max(0, Math.Floor((dto.LocalDateTime - DateTime.Now).TotalMinutes));
                        if (diffMins <= 60 && minsList.Count < 3)
                        {
                            minsList.Add(diffMins);
                            remarksList.Add(rmk);
                        }
                    }
                }
            }
        }

        // 10. STOP PORTAL ("ℹ 同站路線")
        public static async Task<List<NearbyRouteItem>> FetchRoutesForStopPortalAsync(BusStopItem stop, bool isClockMode)
        {
            List<NearbyRouteItem> portalRoutes = new List<NearbyRouteItem>();
            if (stop == null) return portalRoutes;

            await EnsureKmbStopsLoadedAsync();
            await GetOrFetchAllRoutesAsync();

            List<KnownHkStop> nearbyStops = new List<KnownHkStop>();
            if (!string.IsNullOrEmpty(stop.StopId) && _kmbStopsMap.ContainsKey(stop.StopId)) nearbyStops.Add(_kmbStopsMap[stop.StopId]);
            if (stop.Latitude > 20.0)
            {
                List<KnownHkStop> around = GeoMapService.FindNearestStopsWithinRadius(stop.Latitude, stop.Longitude, 55, 3);
                for (int i = 0; i < around.Count; i++)
                {
                    bool exists = false;
                    for (int j = 0; j < nearbyStops.Count; j++) if (nearbyStops[j].StopId == around[i].StopId) exists = true;
                    if (!exists) nearbyStops.Add(around[i]);
                }
            }

            Dictionary<string, NearbyRouteItem> grouped = new Dictionary<string, NearbyRouteItem>();

            foreach (KnownHkStop kStop in nearbyStops)
            {
                try
                {
                    string url = string.Format("{0}/stop-eta/{1}", KmbApiBase, kStop.StopId);
                    string jsonText = await HttpGetStringAsync(url, true);
                    if (string.IsNullOrEmpty(jsonText)) continue;

                    JsonObject root = JsonObject.Parse(jsonText);
                    if (!root.ContainsKey("data") || root["data"].ValueType != JsonValueType.Array) continue;

                    JsonArray dataArray = root["data"].GetArray();
                    for (int i = 0; i < dataArray.Count; i++)
                    {
                        JsonObject item = dataArray[i].GetObject();
                        string routeNum = GetJsonString(item, "route");
                        string dir = GetJsonString(item, "dir");
                        string serviceType = GetJsonString(item, "service_type");
                        string destTc = GetJsonString(item, "dest_tc");
                        string destEn = GetJsonString(item, "dest_en");
                        string etaStr = GetJsonString(item, "eta");
                        int seq = 1;
                        int.TryParse(GetJsonString(item, "seq"), out seq);

                        if (string.IsNullOrEmpty(routeNum) || string.IsNullOrEmpty(destTc)) continue;

                        string routeKey = string.Format("KMB_{0}_{1}_{2}", routeNum, dir, serviceType);
                        if (!grouped.ContainsKey(routeKey))
                        {
                            string compTc, compEn;
                            GetBusCompanyNames(routeNum, out compTc, out compEn);

                            NearbyRouteItem rItem = new NearbyRouteItem
                            {
                                RouteId = routeKey,
                                RouteNumber = routeNum,
                                Direction = string.IsNullOrEmpty(dir) ? "O" : dir,
                                ServiceType = string.IsNullOrEmpty(serviceType) ? "1" : serviceType,
                                SpecialNote = (serviceType != "1" && !string.IsNullOrEmpty(serviceType)) ? "特別班" : string.Empty,
                                CompanyTc = compTc,
                                CompanyEn = compEn,
                                TransportType = "巴士",
                                OriginTc = kStop.NameTc,
                                OriginEn = kStop.NameEn,
                                DestinationTc = destTc,
                                DestinationEn = string.IsNullOrEmpty(destEn) ? destTc : destEn,
                                NearestStopId = kStop.StopId,
                                NearestStopSeq = seq,
                                StopNameTc = kStop.NameTc,
                                StopNameEn = kStop.NameEn,
                                FareText = FormatBracketFareForStop(routeNum, compTc, seq, 28),
                                ShowClockMode = isClockMode
                            };
                            grouped[routeKey] = rItem;
                            portalRoutes.Add(rItem);
                        }

                        if (!string.IsNullOrEmpty(etaStr))
                        {
                            DateTimeOffset dto;
                            if (DateTimeOffset.TryParse(etaStr, out dto))
                            {
                                double diff = (dto.LocalDateTime - DateTime.Now).TotalMinutes;
                                if (diff >= -0.5 && diff <= 60) AddArrivalTimestampSorted(grouped[routeKey], dto.LocalDateTime);
                            }
                        }
                    }
                }
                catch (Exception) { }
            }

            portalRoutes.Sort(CompareRouteNumbersAscending);
            return portalRoutes;
        }

        // 11. DIRECTION REVERSAL
        public static async Task<bool> ReverseRouteDirectionAsync(NearbyRouteItem route)
        {
            if (route == null || !route.HasOppositeDirection) return false;

            if (route.TransportType == "小巴")
            {
                route.GmbRouteSeq = (route.GmbRouteSeq == 1) ? 2 : 1;
            }
            else if (route.TransportType == "鐵路")
            {
                route.Direction = (route.Direction == "DOWN") ? "UP" : "DOWN";
            }
            else if (route.TransportType == "電車")
            {
                route.ServiceType = (route.ServiceType == "E") ? "W" : "E";
                route.CompanyTc = (route.ServiceType == "E") ? "電車 (東行)" : "電車 (西行)";
                route.CompanyEn = (route.ServiceType == "E") ? "tram (eastbound)" : "tram (westbound)";
            }
            else if (route.CompanyTc == "嶼巴" && route.RouteId.StartsWith("NLB_"))
            {
                List<NearbyRouteItem> all = await GetOrFetchAllRoutesAsync();
                for (int i = 0; i < all.Count; i++)
                {
                    NearbyRouteItem candidate = all[i];
                    if (candidate.CompanyTc == "嶼巴" && candidate.RouteNumber == route.RouteNumber && candidate.ServiceType != route.ServiceType)
                    {
                        route.ServiceType = candidate.ServiceType;
                        route.RouteId = candidate.RouteId;
                        break;
                    }
                }
            }
            else
            {
                route.Direction = (route.Direction == "I") ? "O" : "I";
            }

            string tempTc = route.OriginTc;
            route.OriginTc = route.DestinationTc;
            route.DestinationTc = tempTc;

            string tempEn = route.OriginEn;
            route.OriginEn = route.DestinationEn;
            route.DestinationEn = tempEn;

            route.NearestStopId = string.Empty;
            route.Stops.Clear();
            return true;
        }

        // 12. SHARED UTF-8 HTTP & JSON HELPERS
        public static async Task<string> HttpGetStringAsync(string url, bool bypassCache)
        {
            try
            {
                HttpBaseProtocolFilter filter = new HttpBaseProtocolFilter();
                if (bypassCache)
                {
                    filter.CacheControl.ReadBehavior = HttpCacheReadBehavior.MostRecent;
                    filter.CacheControl.WriteBehavior = HttpCacheWriteBehavior.NoCache;
                }

                using (HttpClient client = new HttpClient(filter))
                {
                    client.DefaultRequestHeaders.UserAgent.TryParseAdd("MetroETA/0.1 (Windows Phone 8.1)");
                    HttpResponseMessage response = await client.GetAsync(new Uri(url));
                    if (response.IsSuccessStatusCode)
                    {
                        IBuffer buffer = await response.Content.ReadAsBufferAsync();
                        if (buffer == null || buffer.Length == 0) return string.Empty;
                        return CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, buffer);
                    }
                }
            }
            catch (Exception) { }
            return string.Empty;
        }

        public static async Task<string> HttpPostStringAsync(string url, string jsonPayload)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpStringContent content = new HttpStringContent(jsonPayload, UnicodeEncoding.Utf8, "application/json");
                    HttpResponseMessage response = await client.PostAsync(new Uri(url), content);
                    if (response.IsSuccessStatusCode)
                    {
                        IBuffer buffer = await response.Content.ReadAsBufferAsync();
                        if (buffer == null || buffer.Length == 0) return string.Empty;
                        return CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, buffer);
                    }
                }
            }
            catch (Exception) { }
            return string.Empty;
        }

        public static string GetJsonString(JsonObject obj, string key)
        {
            if (obj != null && obj.ContainsKey(key))
            {
                IJsonValue val = obj[key];
                if (val.ValueType == JsonValueType.String) return val.GetString();
                if (val.ValueType == JsonValueType.Number) return val.GetNumber().ToString(CultureInfo.InvariantCulture);
            }
            return string.Empty;
        }

        public static double GetJsonDouble(JsonObject obj, string key, double defVal)
        {
            if (obj != null && obj.ContainsKey(key))
            {
                IJsonValue val = obj[key];
                if (val.ValueType == JsonValueType.Number) return val.GetNumber();
                if (val.ValueType == JsonValueType.String)
                {
                    double d = defVal;
                    if (double.TryParse(val.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
                }
            }
            return defVal;
        }
    }
}