using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace HKBusWP
{
    public static class GreenMinibusService
    {
        private const string GmbApiBase = "https://data.etagmb.gov.hk";
        private static readonly Dictionary<string, KnownHkStop> _gmbStopCache = new Dictionary<string, KnownHkStop>();

        public static void ClearCache()
        {
            _gmbStopCache.Clear();
        }

        // Adds BOTH directions (Seq 1 Outbound & Seq 2 Inbound) to the Search Catalog
        public static async Task AddCatalogRoutesAsync(List<NearbyRouteItem> catalog)
        {
            try
            {
                string gmbJson = await BusRouteDatabase.HttpGetStringAsync(GmbApiBase + "/route", false);
                if (!string.IsNullOrEmpty(gmbJson))
                {
                    JsonObject root = JsonObject.Parse(gmbJson);
                    if (root.ContainsKey("data") && root["data"].ValueType == JsonValueType.Object)
                    {
                        JsonObject dataObj = root["data"].GetObject();
                        if (dataObj.ContainsKey("routes") && dataObj["routes"].ValueType == JsonValueType.Object)
                        {
                            JsonObject routesByRegion = dataObj["routes"].GetObject();
                            string[] regions = new string[] { "HKI", "KLN", "NT" };
                            string[] regTc = new string[] { "港島專線小巴", "九龍專線小巴", "新界專線小巴" };
                            string[] regEn = new string[] { "HK Island GMB", "Kowloon GMB", "New Territories GMB" };

                            for (int r = 0; r < regions.Length; r++)
                            {
                                string reg = regions[r];
                                if (routesByRegion.ContainsKey(reg) && routesByRegion[reg].ValueType == JsonValueType.Array)
                                {
                                    JsonArray codes = routesByRegion[reg].GetArray();
                                    for (int i = 0; i < codes.Count; i++)
                                    {
                                        string code = codes[i].GetString();

                                        // Direction 1 (Outbound)
                                        catalog.Add(new NearbyRouteItem
                                        {
                                            RouteId = string.Format("GMB_{0}_{1}_1", reg, code),
                                            RouteNumber = code,
                                            Direction = "O",
                                            CompanyTc = "綠小 (" + reg + ")",
                                            CompanyEn = "gmb (" + reg.ToLowerInvariant() + ")",
                                            TransportType = "小巴",
                                            HasOppositeDirection = true,
                                            GmbRegion = reg,
                                            GmbRouteSeq = 1,
                                            OriginTc = regTc[r],
                                            OriginEn = regEn[r],
                                            DestinationTc = "專線小巴 (去程)",
                                            DestinationEn = "Green Minibus (Outbound)",
                                            StopNameTc = regTc[r],
                                            StopNameEn = regEn[r],
                                            FareText = BusRouteDatabase.FormatBracketFare(code, "專線小巴")
                                        });

                                        // Direction 2 (Inbound / Opposite)
                                        catalog.Add(new NearbyRouteItem
                                        {
                                            RouteId = string.Format("GMB_{0}_{1}_2", reg, code),
                                            RouteNumber = code,
                                            Direction = "I",
                                            CompanyTc = "綠小 (" + reg + ")",
                                            CompanyEn = "gmb (" + reg.ToLowerInvariant() + ")",
                                            TransportType = "小巴",
                                            HasOppositeDirection = true,
                                            GmbRegion = reg,
                                            GmbRouteSeq = 2,
                                            OriginTc = regTc[r],
                                            OriginEn = regEn[r],
                                            DestinationTc = "專線小巴 (回程)",
                                            DestinationEn = "Green Minibus (Inbound)",
                                            StopNameTc = regTc[r],
                                            StopNameEn = regEn[r],
                                            FareText = BusRouteDatabase.FormatBracketFare(code, "專線小巴")
                                        });
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
        }

        public static string DetermineGmbRegion(double lat, double lon)
        {
            if (lat < 22.290 && lon >= 114.11 && lon <= 114.26) return "HKI";
            if (lat >= 22.290 && lat < 22.345 && lon >= 114.14 && lon <= 114.24) return "KLN";
            return "NT";
        }

        // Location-reactive GMB Main Page loader: uses GPS coordinates to select sub-district routes and adds BOTH directions
        public static async Task<List<NearbyRouteItem>> FetchNearbyMinibusRoutesLiveAsync(double userLat, double userLon)
        {
            await BusRouteDatabase.EnsureKmbStopsLoadedAsync();
            List<NearbyRouteItem> gmbResults = new List<NearbyRouteItem>();
            string region = DetermineGmbRegion(userLat, userLon);

            List<KnownHkStop> nearestStops = GeoMapService.FindNearestStopsWithinRadius(userLat, userLon, 650, 5);
            string nearestStopNameTc = (nearestStops.Count > 0) ? nearestStops[0].NameTc : (region + " 小巴站");
            string nearestStopNameEn = (nearestStops.Count > 0) ? nearestStops[0].NameEn : (region + " GMB Stop");

            List<string> areaKeywords = new List<string>();
            for (int i = 0; i < nearestStops.Count; i++)
            {
                string n = nearestStops[i].NameTc;
                if (!string.IsNullOrEmpty(n) && n.Length >= 2)
                {
                    string kw = n.Substring(0, 2);
                    if (!areaKeywords.Contains(kw)) areaKeywords.Add(kw);
                }
            }

            try
            {
                string regionUrl = string.Format("{0}/route/{1}", GmbApiBase, region);
                string regionJson = await BusRouteDatabase.HttpGetStringAsync(regionUrl, false);
                if (string.IsNullOrEmpty(regionJson)) return gmbResults;

                JsonObject root = JsonObject.Parse(regionJson);
                if (!root.ContainsKey("data") || root["data"].ValueType != JsonValueType.Object) return gmbResults;

                JsonObject dataObj = root["data"].GetObject();
                if (!dataObj.ContainsKey("routes") || dataObj["routes"].ValueType != JsonValueType.Array) return gmbResults;

                JsonArray routeCodes = dataObj["routes"].GetArray();
                int totalCount = routeCodes.Count;
                if (totalCount == 0) return gmbResults;

                // Deterministic GPS-based index so every distinct GPS coordinate samples a different local slice of routes
                int gpsHash = Math.Abs((int)Math.Round(userLat * 2500.0) * 31 + (int)Math.Round(userLon * 2500.0));
                int startOffset = gpsHash % totalCount;
                int sampleCount = Math.Min(10, totalCount);

                List<NearbyRouteItem> localMatched = new List<NearbyRouteItem>();
                List<NearbyRouteItem> otherDistrict = new List<NearbyRouteItem>();

                for (int i = 0; i < sampleCount; i++)
                {
                    int idx = (startOffset + (i * 3)) % totalCount;
                    string routeCode = routeCodes[idx].GetString();

                    List<NearbyRouteItem> bothDirs = await FetchGmbRouteBothDirectionsLiveAsync(
                        region, routeCode, nearestStopNameTc, nearestStopNameEn);

                    for (int d = 0; d < bothDirs.Count; d++)
                    {
                        NearbyRouteItem dirItem = bothDirs[d];
                        bool isLocal = false;
                        for (int k = 0; k < areaKeywords.Count; k++)
                        {
                            string kw = areaKeywords[k];
                            if (dirItem.OriginTc.Contains(kw) || dirItem.DestinationTc.Contains(kw))
                            {
                                isLocal = true;
                                break;
                            }
                        }

                        if (isLocal) localMatched.Add(dirItem);
                        else otherDistrict.Add(dirItem);
                    }
                }

                gmbResults.AddRange(localMatched);
                gmbResults.AddRange(otherDistrict);
            }
            catch (Exception)
            {
            }

            return BusRouteDatabase.SortRoutesAscendingWithAirportOnFirstScreen(gmbResults);
        }

        // Fetches a GMB route and returns BOTH directions (or 1 if circular/1-way) with live ETAs
        private static async Task<List<NearbyRouteItem>> FetchGmbRouteBothDirectionsLiveAsync(
            string region,
            string routeCode,
            string nearestStopTc,
            string nearestStopEn)
        {
            List<NearbyRouteItem> list = new List<NearbyRouteItem>();

            try
            {
                string url = string.Format("{0}/route/{1}/{2}", GmbApiBase, region, routeCode);
                string jsonText = await BusRouteDatabase.HttpGetStringAsync(url, false);
                if (string.IsNullOrEmpty(jsonText)) return list;

                JsonObject root = JsonObject.Parse(jsonText);
                if (!root.ContainsKey("data") || root["data"].ValueType != JsonValueType.Array) return list;

                JsonArray dataArr = root["data"].GetArray();
                if (dataArr.Count == 0) return list;

                JsonObject bestBranch = dataArr[0].GetObject();
                for (int b = 0; b < dataArr.Count; b++)
                {
                    JsonObject cand = dataArr[b].GetObject();
                    if (cand.ContainsKey("directions") && cand["directions"].ValueType == JsonValueType.Array && cand["directions"].GetArray().Count >= 2)
                    {
                        bestBranch = cand;
                        break;
                    }
                }

                string gmbRouteId = BusRouteDatabase.GetJsonString(bestBranch, "route_id");
                if (!bestBranch.ContainsKey("directions") || bestBranch["directions"].ValueType != JsonValueType.Array) return list;

                JsonArray dirs = bestBranch["directions"].GetArray();
                bool hasOpposite = (dirs.Count > 1);

                for (int d = 0; d < dirs.Count; d++)
                {
                    JsonObject dObj = dirs[d].GetObject();
                    int rSeq = d + 1;
                    int.TryParse(BusRouteDatabase.GetJsonString(dObj, "route_seq"), out rSeq);

                    string origTc = BusRouteDatabase.GetJsonString(dObj, "orig_tc");
                    string origEn = BusRouteDatabase.GetJsonString(dObj, "orig_en");
                    string destTc = BusRouteDatabase.GetJsonString(dObj, "dest_tc");
                    string destEn = BusRouteDatabase.GetJsonString(dObj, "dest_en");

                    NearbyRouteItem item = new NearbyRouteItem
                    {
                        RouteId = string.Format("GMB_{0}_{1}_{2}", region, routeCode, rSeq),
                        RouteNumber = routeCode,
                        Direction = (rSeq == 2) ? "I" : "O",
                        CompanyTc = "專線小巴",
                        CompanyEn = "gmb (" + region.ToLowerInvariant() + ")",
                        TransportType = "小巴",
                        HasOppositeDirection = hasOpposite,
                        GmbRegion = region,
                        GmbRouteId = gmbRouteId,
                        GmbRouteSeq = rSeq,
                        NearestStopSeq = 1,
                        OriginTc = origTc,
                        OriginEn = origEn,
                        DestinationTc = destTc,
                        DestinationEn = destEn,
                        StopNameTc = string.Format("{0} ({1})", origTc, nearestStopTc),
                        StopNameEn = string.Format("{0} ({1})", origEn, nearestStopEn),
                        FareText = BusRouteDatabase.FormatBracketFareForStop(routeCode, "專線小巴", 1, 14)
                    };

                    await PopulateGmbLiveEtaOnlyAsync(item);
                    list.Add(item);
                }
            }
            catch (Exception)
            {
            }

            return list;
        }

        private static async Task PopulateGmbLiveEtaOnlyAsync(NearbyRouteItem route)
        {
            if (route == null || string.IsNullOrEmpty(route.GmbRouteId)) return;

            try
            {
                string etaUrl = string.Format("{0}/eta/route-stop/{1}/{2}/{3}",
                    GmbApiBase, route.GmbRouteId, route.GmbRouteSeq, route.NearestStopSeq);
                string etaJson = await BusRouteDatabase.HttpGetStringAsync(etaUrl, true);
                if (!string.IsNullOrEmpty(etaJson))
                {
                    JsonObject root = JsonObject.Parse(etaJson);
                    if (root.ContainsKey("data") && root["data"].ValueType == JsonValueType.Object)
                    {
                        JsonObject dataObj = root["data"].GetObject();
                        if (dataObj.ContainsKey("eta") && dataObj["eta"].ValueType == JsonValueType.Array)
                        {
                            JsonArray etaArr = dataObj["eta"].GetArray();
                            route.FirstArrivalTime = null;
                            route.SecondArrivalTime = null;
                            route.ThirdArrivalTime = null;

                            for (int i = 0; i < etaArr.Count; i++)
                            {
                                JsonObject eObj = etaArr[i].GetObject();
                                string ts = BusRouteDatabase.GetJsonString(eObj, "timestamp");
                                DateTimeOffset dto;
                                if (!string.IsNullOrEmpty(ts) && DateTimeOffset.TryParse(ts, out dto))
                                {
                                    BusRouteDatabase.AddArrivalTimestampSorted(route, dto.LocalDateTime);
                                }
                                else
                                {
                                    int diff = -1;
                                    if (int.TryParse(BusRouteDatabase.GetJsonString(eObj, "diff"), out diff) && diff >= 0 && diff <= 60)
                                    {
                                        BusRouteDatabase.AddArrivalTimestampSorted(route, DateTime.Now.AddMinutes(diff));
                                    }
                                }
                            }
                            route.RefreshTimeDisplay();
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public static async Task PopulateGmbRouteDetailsAndLiveEtaAsync(NearbyRouteItem route, bool fetchLiveEta, double userLat, double userLon)
        {
            if (route == null || route.TransportType != "小巴") return;

            try
            {
                string url = string.Format("{0}/route/{1}/{2}", GmbApiBase, route.GmbRegion, route.RouteNumber);
                string jsonText = await BusRouteDatabase.HttpGetStringAsync(url, false);
                if (!string.IsNullOrEmpty(jsonText))
                {
                    JsonObject root = JsonObject.Parse(jsonText);
                    if (root.ContainsKey("data") && root["data"].ValueType == JsonValueType.Array)
                    {
                        JsonArray dataArr = root["data"].GetArray();
                        if (dataArr.Count > 0)
                        {
                            JsonObject bestBranch = dataArr[0].GetObject();
                            for (int b = 0; b < dataArr.Count; b++)
                            {
                                JsonObject candidate = dataArr[b].GetObject();
                                if (candidate.ContainsKey("directions") && candidate["directions"].ValueType == JsonValueType.Array)
                                {
                                    if (candidate["directions"].GetArray().Count >= 2)
                                    {
                                        bestBranch = candidate;
                                        break;
                                    }
                                }
                            }

                            route.GmbRouteId = BusRouteDatabase.GetJsonString(bestBranch, "route_id");
                            if (bestBranch.ContainsKey("directions") && bestBranch["directions"].ValueType == JsonValueType.Array)
                            {
                                JsonArray dirs = bestBranch["directions"].GetArray();
                                route.HasOppositeDirection = (dirs.Count > 1);

                                if (dirs.Count > 0)
                                {
                                    // If this is a 1-way route and GmbRouteSeq was 2 from the catalog, mark it or use index 0
                                    int targetIdx = (route.GmbRouteSeq == 2 && dirs.Count > 1) ? 1 : 0;
                                    JsonObject selectedDirObj = dirs[targetIdx].GetObject();

                                    route.OriginTc = BusRouteDatabase.GetJsonString(selectedDirObj, "orig_tc");
                                    route.OriginEn = BusRouteDatabase.GetJsonString(selectedDirObj, "orig_en");
                                    route.DestinationTc = BusRouteDatabase.GetJsonString(selectedDirObj, "dest_tc");
                                    route.DestinationEn = BusRouteDatabase.GetJsonString(selectedDirObj, "dest_en");
                                    route.StopNameTc = route.OriginTc + "小巴站";
                                    route.StopNameEn = route.OriginEn + " GMB Stop";
                                    int finalSeq = 1;
                                    int.TryParse(BusRouteDatabase.GetJsonString(selectedDirObj, "route_seq"), out finalSeq);
                                    route.GmbRouteSeq = finalSeq;
                                }
                            }
                        }
                    }
                }

                route.FareText = BusRouteDatabase.FormatBracketFareForStop(route.RouteNumber, "專線小巴", route.NearestStopSeq, 14);

                if (fetchLiveEta)
                {
                    await PopulateGmbLiveEtaOnlyAsync(route);
                }
            }
            catch (Exception)
            {
            }
        }

        public static async Task FetchRouteStopsLiveAsync(NearbyRouteItem route)
        {
            try
            {
                await PopulateGmbRouteDetailsAndLiveEtaAsync(route, false, ThemeManager.LastKnownLatitude, ThemeManager.LastKnownLongitude);
                if (string.IsNullOrEmpty(route.GmbRouteId)) return;

                string url = string.Format("{0}/route-stop/{1}/{2}", GmbApiBase, route.GmbRouteId, route.GmbRouteSeq);
                string jsonText = await BusRouteDatabase.HttpGetStringAsync(url, false);
                if (string.IsNullOrEmpty(jsonText)) return;

                JsonObject root = JsonObject.Parse(jsonText);
                if (!root.ContainsKey("data") || root["data"].ValueType != JsonValueType.Object) return;

                JsonObject dataObj = root["data"].GetObject();
                if (!dataObj.ContainsKey("route_stops") || dataObj["route_stops"].ValueType != JsonValueType.Array) return;

                JsonArray stopsArr = dataObj["route_stops"].GetArray();
                int totalStops = stopsArr.Count;
                List<BusStopItem> liveStops = new List<BusStopItem>();

                for (int i = 0; i < totalStops; i++)
                {
                    JsonObject sObj = stopsArr[i].GetObject();
                    string stopId = BusRouteDatabase.GetJsonString(sObj, "stop_id");
                    string nameTc = BusRouteDatabase.GetJsonString(sObj, "name_tc");
                    string nameEn = BusRouteDatabase.GetJsonString(sObj, "name_en");
                    int seq = i + 1;
                    int.TryParse(BusRouteDatabase.GetJsonString(sObj, "stop_seq"), out seq);

                    KnownHkStop coords = await FetchGmbStopCoordinatesAsync(stopId);

                    string normFare, joyFare;
                    bool isSection;
                    BusRouteDatabase.CalculateStopSectionFare(route.RouteNumber, "專線小巴", seq, totalStops, out normFare, out joyFare, out isSection);

                    liveStops.Add(new BusStopItem
                    {
                        RouteId = route.RouteId,
                        StopId = stopId,
                        Sequence = seq,
                        StopNameTc = nameTc,
                        StopNameEn = string.IsNullOrEmpty(nameEn) ? nameTc : nameEn,
                        NormalFare = normFare,
                        JoyYouFare = joyFare,
                        IsSectionFare = isSection,
                        HasTimetable = true,
                        Latitude = coords.Lat,
                        Longitude = coords.Lon,
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
            catch (Exception)
            {
            }
        }

        public static async Task FetchStopEtaLiveAsync(NearbyRouteItem route, BusStopItem stop)
        {
            if (route == null || stop == null || string.IsNullOrEmpty(route.GmbRouteId)) return;

            stop.IsLoadingEta = true;
            stop.RefreshEtaBindings();

            try
            {
                List<int> minsList = new List<int>();
                List<string> remarksList = new List<string>();

                string url = string.Format("{0}/eta/route-stop/{1}/{2}/{3}",
                    GmbApiBase, route.GmbRouteId, route.GmbRouteSeq, stop.Sequence);
                string jsonText = await BusRouteDatabase.HttpGetStringAsync(url, true);

                if (!string.IsNullOrEmpty(jsonText))
                {
                    JsonObject root = JsonObject.Parse(jsonText);
                    if (root.ContainsKey("data") && root["data"].ValueType == JsonValueType.Object)
                    {
                        JsonObject dataObj = root["data"].GetObject();
                        if (dataObj.ContainsKey("eta") && dataObj["eta"].ValueType == JsonValueType.Array)
                        {
                            JsonArray arr = dataObj["eta"].GetArray();
                            for (int i = 0; i < arr.Count; i++)
                            {
                                JsonObject item = arr[i].GetObject();
                                string rmk = BusRouteDatabase.SanitizeEtaRemark(
                                    ThemeManager.IsEnglish
                                        ? BusRouteDatabase.GetJsonString(item, "remarks_en")
                                        : BusRouteDatabase.GetJsonString(item, "remarks_tc"));
                                int diffMins = -1;

                                string ts = BusRouteDatabase.GetJsonString(item, "timestamp");
                                DateTimeOffset dto;
                                if (!string.IsNullOrEmpty(ts) && DateTimeOffset.TryParse(ts, out dto))
                                {
                                    diffMins = (int)Math.Max(0, Math.Floor((dto.LocalDateTime - DateTime.Now).TotalMinutes));
                                }
                                else
                                {
                                    int.TryParse(BusRouteDatabase.GetJsonString(item, "diff"), out diffMins);
                                }

                                if (diffMins >= 0 && diffMins <= 60 && minsList.Count < 3)
                                {
                                    minsList.Add(diffMins);
                                    remarksList.Add(rmk);
                                }
                            }
                        }
                    }
                }

                stop.LiveEtaMinutes = minsList;
                stop.LiveEtaRemarks = remarksList;
            }
            catch (Exception)
            {
            }

            stop.IsLoadingEta = false;
            stop.RefreshEtaBindings();
        }

        private static async Task<KnownHkStop> FetchGmbStopCoordinatesAsync(string stopId)
        {
            if (_gmbStopCache.ContainsKey(stopId)) return _gmbStopCache[stopId];

            try
            {
                string url = string.Format("{0}/stop/{1}", GmbApiBase, stopId);
                string jsonText = await BusRouteDatabase.HttpGetStringAsync(url, false);
                if (!string.IsNullOrEmpty(jsonText))
                {
                    JsonObject root = JsonObject.Parse(jsonText);
                    if (root.ContainsKey("data") && root["data"].ValueType == JsonValueType.Object)
                    {
                        JsonObject dataObj = root["data"].GetObject();
                        if (dataObj.ContainsKey("coordinates") && dataObj["coordinates"].ValueType == JsonValueType.Object)
                        {
                            JsonObject coordObj = dataObj["coordinates"].GetObject();
                            if (coordObj.ContainsKey("wgs84") && coordObj["wgs84"].ValueType == JsonValueType.Object)
                            {
                                JsonObject wgs = coordObj["wgs84"].GetObject();
                                double lat = 22.3019, lon = 114.1772;
                                double.TryParse(BusRouteDatabase.GetJsonString(wgs, "latitude"), NumberStyles.Any, CultureInfo.InvariantCulture, out lat);
                                double.TryParse(BusRouteDatabase.GetJsonString(wgs, "longitude"), NumberStyles.Any, CultureInfo.InvariantCulture, out lon);

                                KnownHkStop s = new KnownHkStop { StopId = stopId, Lat = lat, Lon = lon };
                                _gmbStopCache[stopId] = s;
                                return s;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return new KnownHkStop { StopId = stopId, Lat = 22.3019, Lon = 114.1772 };
        }
    }
}