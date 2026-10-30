using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace HKBusWP
{
    public static class CacheService
    {
        private const string FastCacheFileName = "metroeta_fast_cache_v7.json";

        public static void ClearDiskCacheFile()
        {
            try
            {
                var task = ApplicationData.Current.LocalFolder.CreateFileAsync(
                    FastCacheFileName,
                    CreationCollisionOption.ReplaceExisting);
            }
            catch (Exception)
            {
            }
        }

        public static async Task<bool> TryRestoreFromLocalDiskCacheAsync(
            List<KnownHkStop> kmbStopsList,
            Dictionary<string, KnownHkStop> kmbStopsMap,
            List<NearbyRouteItem> allRoutesCatalog,
            Dictionary<string, NearbyRouteItem> kmbRouteLookup)
        {
            try
            {
                StorageFolder folder = ApplicationData.Current.LocalFolder;
                StorageFile file = await folder.GetFileAsync(FastCacheFileName);
                string content = await FileIO.ReadTextAsync(file);
                if (string.IsNullOrEmpty(content)) return false;

                JsonObject root = JsonObject.Parse(content);
                bool stopsRestored = false;

                if (root.ContainsKey("stops") && root["stops"].ValueType == JsonValueType.Array)
                {
                    JsonArray sArr = root["stops"].GetArray();
                    if (sArr.Count > 200)
                    {
                        kmbStopsList.Clear();
                        kmbStopsMap.Clear();
                        for (int i = 0; i < sArr.Count; i++)
                        {
                            JsonObject o = sArr[i].GetObject();
                            KnownHkStop s = new KnownHkStop
                            {
                                StopId = BusRouteDatabase.GetJsonString(o, "id"),
                                NameTc = BusRouteDatabase.GetJsonString(o, "tc"),
                                NameEn = BusRouteDatabase.GetJsonString(o, "en"),
                                Lat = BusRouteDatabase.GetJsonDouble(o, "lat", 22.3019),
                                Lon = BusRouteDatabase.GetJsonDouble(o, "lon", 114.1772)
                            };
                            kmbStopsList.Add(s);
                            kmbStopsMap[s.StopId] = s;
                        }
                        stopsRestored = true;
                    }
                }

                if (root.ContainsKey("routes") && root["routes"].ValueType == JsonValueType.Array)
                {
                    JsonArray rArr = root["routes"].GetArray();
                    List<NearbyRouteItem> restored = new List<NearbyRouteItem>();
                    bool hasNumericRoutes = false;

                    for (int i = 0; i < rArr.Count; i++)
                    {
                        JsonObject o = rArr[i].GetObject();
                        string rNo = BusRouteDatabase.GetJsonString(o, "no");
                        if (!string.IsNullOrEmpty(rNo) && char.IsDigit(rNo[0])) hasNumericRoutes = true;

                        NearbyRouteItem item = new NearbyRouteItem
                        {
                            RouteId = BusRouteDatabase.GetJsonString(o, "id"),
                            RouteNumber = rNo,
                            Direction = BusRouteDatabase.GetJsonString(o, "dir"),
                            ServiceType = BusRouteDatabase.GetJsonString(o, "st"),
                            SpecialNote = BusRouteDatabase.GetJsonString(o, "sp"),
                            CompanyTc = BusRouteDatabase.GetJsonString(o, "ctc"),
                            CompanyEn = BusRouteDatabase.GetJsonString(o, "cen"),
                            TransportType = BusRouteDatabase.GetJsonString(o, "tt"),
                            IsJointKmbCtb = BusRouteDatabase.GetJsonString(o, "jt") == "1",
                            HasOppositeDirection = BusRouteDatabase.GetJsonString(o, "op") != "0",
                            OriginTc = BusRouteDatabase.GetJsonString(o, "otc"),
                            OriginEn = BusRouteDatabase.GetJsonString(o, "oen"),
                            DestinationTc = BusRouteDatabase.GetJsonString(o, "dtc"),
                            DestinationEn = BusRouteDatabase.GetJsonString(o, "den"),
                            StopNameTc = BusRouteDatabase.GetJsonString(o, "otc"),
                            StopNameEn = BusRouteDatabase.GetJsonString(o, "oen"),
                            FareText = BusRouteDatabase.GetJsonString(o, "fare"),
                            GmbRegion = BusRouteDatabase.GetJsonString(o, "greg")
                        };
                        restored.Add(item);
                        if (item.RouteId.StartsWith("KMB_")) kmbRouteLookup[item.RouteId] = item;
                    }

                    if (restored.Count > 800 && hasNumericRoutes)
                    {
                        allRoutesCatalog.Clear();
                        allRoutesCatalog.AddRange(restored);
                        return stopsRestored;
                    }
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        public static async Task SaveToLocalDiskCacheAsync(
            List<KnownHkStop> kmbStopsList,
            List<NearbyRouteItem> allRoutesCatalog)
        {
            try
            {
                JsonObject root = new JsonObject();
                JsonArray sArr = new JsonArray();
                for (int i = 0; i < kmbStopsList.Count; i++)
                {
                    KnownHkStop s = kmbStopsList[i];
                    JsonObject o = new JsonObject();
                    o["id"] = JsonValue.CreateStringValue(s.StopId ?? "");
                    o["tc"] = JsonValue.CreateStringValue(s.NameTc ?? "");
                    o["en"] = JsonValue.CreateStringValue(s.NameEn ?? "");
                    o["lat"] = JsonValue.CreateNumberValue(s.Lat);
                    o["lon"] = JsonValue.CreateNumberValue(s.Lon);
                    sArr.Add(o);
                }
                root["stops"] = sArr;

                JsonArray rArr = new JsonArray();
                for (int i = 0; i < allRoutesCatalog.Count; i++)
                {
                    NearbyRouteItem r = allRoutesCatalog[i];
                    JsonObject o = new JsonObject();
                    o["id"] = JsonValue.CreateStringValue(r.RouteId ?? "");
                    o["no"] = JsonValue.CreateStringValue(r.RouteNumber ?? "");
                    o["dir"] = JsonValue.CreateStringValue(r.Direction ?? "O");
                    o["st"] = JsonValue.CreateStringValue(r.ServiceType ?? "1");
                    o["sp"] = JsonValue.CreateStringValue(r.SpecialNote ?? "");
                    o["ctc"] = JsonValue.CreateStringValue(r.CompanyTc ?? "");
                    o["cen"] = JsonValue.CreateStringValue(r.CompanyEn ?? "");
                    o["tt"] = JsonValue.CreateStringValue(r.TransportType ?? "巴士");
                    o["jt"] = JsonValue.CreateStringValue(r.IsJointKmbCtb ? "1" : "0");
                    o["op"] = JsonValue.CreateStringValue(r.HasOppositeDirection ? "1" : "0");
                    o["otc"] = JsonValue.CreateStringValue(r.OriginTc ?? "");
                    o["oen"] = JsonValue.CreateStringValue(r.OriginEn ?? "");
                    o["dtc"] = JsonValue.CreateStringValue(r.DestinationTc ?? "");
                    o["den"] = JsonValue.CreateStringValue(r.DestinationEn ?? "");
                    o["fare"] = JsonValue.CreateStringValue(r.FareText ?? "");
                    o["greg"] = JsonValue.CreateStringValue(r.GmbRegion ?? "");
                    rArr.Add(o);
                }
                root["routes"] = rArr;

                StorageFolder folder = ApplicationData.Current.LocalFolder;
                StorageFile file = await folder.CreateFileAsync(FastCacheFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, root.Stringify());
            }
            catch (Exception)
            {
            }
        }
    }
}