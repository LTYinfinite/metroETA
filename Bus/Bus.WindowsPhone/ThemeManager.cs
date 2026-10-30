using System;
using System.Collections.Generic;
using Windows.Data.Json;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HKBusWP
{
    public class FavouriteStationEntry
    {
        public string FavKey { get; set; }
        public string RouteId { get; set; }
        public string RouteNumber { get; set; }
        public string CompanyTc { get; set; }
        public string CompanyEn { get; set; }
        public string TransportType { get; set; }
        public string Direction { get; set; }
        public string ServiceType { get; set; }
        public string OriginTc { get; set; }
        public string OriginEn { get; set; }
        public string DestinationTc { get; set; }
        public string DestinationEn { get; set; }
        public string StopId { get; set; }
        public int StopSeq { get; set; }
        public string StopNameTc { get; set; }
        public string StopNameEn { get; set; }
        public string FareText { get; set; }
        public string GmbRegion { get; set; }
        public string GmbRouteId { get; set; }
        public int GmbRouteSeq { get; set; }
    }

    public static class ThemeManager
    {
        private const string ThemeKey = "AppTheme";
        private const string OobeCompletedKey = "OobeCompleted";
        private const string SearchRadiusKey = "SearchRadiusMeters";
        private const string LanguageEnglishKey = "AppLangEnglish";
        private const string StartTabIndexKey = "DefaultStartTabIndex";
        private const string HideStopCodeKey = "HideStopCodeSuffix";
        private const string LastLatKey = "LastKnownLat";
        private const string LastLonKey = "LastKnownLon";
        private const string FavouritesJsonKey = "SavedFavouritesJson";

        private static List<FavouriteStationEntry> _cachedFavourites;

        // 1. OOBE Completion State
        public static bool IsOobeCompleted
        {
            get
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                if (settings.ContainsKey(OobeCompletedKey))
                {
                    return (bool)settings[OobeCompletedKey];
                }
                return false;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[OobeCompletedKey] = value;
            }
        }

        // 2. Bilingual Language Setting (false = 繁體中文, true = english in WP8.1 lowercase style)
        public static bool IsEnglish
        {
            get
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                if (settings.ContainsKey(LanguageEnglishKey))
                {
                    return (bool)settings[LanguageEnglishKey];
                }
                return false;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[LanguageEnglishKey] = value;
            }
        }

        public static string Tr(string tc, string en)
        {
            return IsEnglish ? en : tc;
        }

        // 3. Option to Hide Trailing Bus Stop Code (e.g. strips "(YT615)" from "香港歷史博物館 (YT615)" while keeping sequence "26.")
        public static bool HideStopCode
        {
            get
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                if (settings.ContainsKey(HideStopCodeKey))
                {
                    return (bool)settings[HideStopCodeKey];
                }
                return false;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[HideStopCodeKey] = value;
            }
        }

        public static string FormatStopNameWithCodeRule(string rawStopName)
        {
            if (string.IsNullOrEmpty(rawStopName)) return string.Empty;
            if (!HideStopCode) return rawStopName;

            string trimmed = rawStopName.TrimEnd();
            if (trimmed.EndsWith(")"))
            {
                int openIdx = trimmed.LastIndexOf('(');
                if (openIdx > 0)
                {
                    string inside = trimmed.Substring(openIdx + 1, trimmed.Length - openIdx - 2).Trim();
                    // Check if the bracket at the end is a Stop/Station Code (ASCII letters/digits/hyphen like YT615, HH979, KC400, PA125-U010)
                    if (IsAlphanumericStopCode(inside))
                    {
                        return trimmed.Substring(0, openIdx).TrimEnd();
                    }
                }
            }
            return rawStopName;
        }

        private static bool IsAlphanumericStopCode(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length < 2 || token.Length > 14) return false;

            bool hasAsciiLetterOrDigit = false;
            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                if (c > 127)
                {
                    // Contains Chinese characters (e.g. "(太和)" or "(循環線)") -> NOT a stop code, keep it!
                    return false;
                }
                if (char.IsLetterOrDigit(c))
                {
                    hasAsciiLetterOrDigit = true;
                }
            }

            // In English mode, only strip if it has digits OR is an uppercase station code (2-5 chars like ADM,YT615)
            bool hasDigit = false;
            bool allUpperOrDigit = true;
            for (int i = 0; i < token.Length; i++)
            {
                if (char.IsDigit(token[i])) hasDigit = true;
                if (char.IsLower(token[i])) allUpperOrDigit = false;
            }

            return hasAsciiLetterOrDigit && (hasDigit || (allUpperOrDigit && token.Length <= 5));
        }

        // 4. Configurable Route Search Radius (50m to 500m, Default: 250m)
        public static int SearchRadiusMeters
        {
            get
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                if (settings.ContainsKey(SearchRadiusKey))
                {
                    int val = (int)settings[SearchRadiusKey];
                    if (val >= 50 && val <= 500)
                    {
                        return val;
                    }
                }
                return 250;
            }
            set
            {
                int clamped = Math.Max(50, Math.Min(500, value));
                ApplicationData.Current.LocalSettings.Values[SearchRadiusKey] = clamped;
            }
        }

        // 5. Configurable Default Starting Tab on MainPage (0..5)
        public static int DefaultStartTabIndex
        {
            get
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                if (settings.ContainsKey(StartTabIndexKey))
                {
                    int idx = (int)settings[StartTabIndexKey];
                    if (idx >= 0 && idx <= 5)
                    {
                        return idx;
                    }
                }
                return 1;
            }
            set
            {
                int clamped = Math.Max(0, Math.Min(5, value));
                ApplicationData.Current.LocalSettings.Values[StartTabIndexKey] = clamped;
            }
        }

        // 6. Last Known GPS Coordinates
        public static bool HasLastKnownLocation
        {
            get
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                return settings.ContainsKey(LastLatKey) && settings.ContainsKey(LastLonKey);
            }
        }

        public static double LastKnownLatitude
        {
            get
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                if (settings.ContainsKey(LastLatKey))
                {
                    return (double)settings[LastLatKey];
                }
                return 22.3019;
            }
        }

        public static double LastKnownLongitude
        {
            get
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                if (settings.ContainsKey(LastLonKey))
                {
                    return (double)settings[LastLonKey];
                }
                return 114.1772;
            }
        }

        public static void SaveLastKnownLocation(double lat, double lon)
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            settings[LastLatKey] = lat;
            settings[LastLonKey] = lon;
        }

        // 7. Theme & System StatusBar Management
        public static ElementTheme CurrentTheme
        {
            get
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                if (settings.ContainsKey(ThemeKey))
                {
                    return (ElementTheme)((int)settings[ThemeKey]);
                }
                return ElementTheme.Dark;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[ThemeKey] = (int)value;
                ApplyThemeToRootFrame(value);
            }
        }

        public static void ApplyThemeToRootFrame(ElementTheme theme)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame != null)
            {
                rootFrame.RequestedTheme = theme;
            }

            try
            {
                StatusBar statusBar = StatusBar.GetForCurrentView();
                if (statusBar != null)
                {
                    statusBar.BackgroundOpacity = 1.0;
                    if (theme == ElementTheme.Light)
                    {
                        statusBar.BackgroundColor = Colors.White;
                        statusBar.ForegroundColor = Colors.Black;
                    }
                    else
                    {
                        statusBar.BackgroundColor = Colors.Black;
                        statusBar.ForegroundColor = Colors.White;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // 8. Persistent Favourite Route + Station Storage
        public static string BuildFavKey(string routeId, string stopId, int stopSeq)
        {
            return string.Format("{0}@{1}@{2}", routeId ?? "", stopId ?? "", stopSeq);
        }

        public static List<FavouriteStationEntry> GetFavourites()
        {
            if (_cachedFavourites != null)
            {
                return _cachedFavourites;
            }

            _cachedFavourites = new List<FavouriteStationEntry>();
            try
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                if (settings.ContainsKey(FavouritesJsonKey))
                {
                    string json = settings[FavouritesJsonKey] as string;
                    if (!string.IsNullOrEmpty(json))
                    {
                        JsonArray arr = JsonArray.Parse(json);
                        for (int i = 0; i < arr.Count; i++)
                        {
                            JsonObject obj = arr[i].GetObject();
                            FavouriteStationEntry entry = new FavouriteStationEntry
                            {
                                FavKey = GetStr(obj, "FavKey"),
                                RouteId = GetStr(obj, "RouteId"),
                                RouteNumber = GetStr(obj, "RouteNumber"),
                                CompanyTc = GetStr(obj, "CompanyTc"),
                                CompanyEn = GetStr(obj, "CompanyEn"),
                                TransportType = GetStr(obj, "TransportType"),
                                Direction = GetStr(obj, "Direction"),
                                ServiceType = GetStr(obj, "ServiceType"),
                                OriginTc = GetStr(obj, "OriginTc"),
                                OriginEn = GetStr(obj, "OriginEn"),
                                DestinationTc = GetStr(obj, "DestinationTc"),
                                DestinationEn = GetStr(obj, "DestinationEn"),
                                StopId = GetStr(obj, "StopId"),
                                StopSeq = (int)GetNum(obj, "StopSeq", 1),
                                StopNameTc = GetStr(obj, "StopNameTc"),
                                StopNameEn = GetStr(obj, "StopNameEn"),
                                FareText = GetStr(obj, "FareText"),
                                GmbRegion = GetStr(obj, "GmbRegion"),
                                GmbRouteId = GetStr(obj, "GmbRouteId"),
                                GmbRouteSeq = (int)GetNum(obj, "GmbRouteSeq", 1)
                            };
                            _cachedFavourites.Add(entry);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return _cachedFavourites;
        }

        public static bool IsFavourite(string routeId, string stopId, int stopSeq)
        {
            string key = BuildFavKey(routeId, stopId, stopSeq);
            List<FavouriteStationEntry> list = GetFavourites();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].FavKey == key)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool ToggleFavourite(NearbyRouteItem route, BusStopItem stop)
        {
            if (route == null || stop == null) return false;

            string key = BuildFavKey(route.RouteId, stop.StopId, stop.Sequence);
            List<FavouriteStationEntry> list = GetFavourites();

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].FavKey == key)
                {
                    list.RemoveAt(i);
                    SaveFavouritesToSettings(list);
                    return false;
                }
            }

            FavouriteStationEntry newEntry = new FavouriteStationEntry
            {
                FavKey = key,
                RouteId = route.RouteId ?? "",
                RouteNumber = route.RouteNumber ?? "",
                CompanyTc = string.IsNullOrEmpty(route.CompanyTc) ? (route.Company ?? "九巴") : route.CompanyTc,
                CompanyEn = string.IsNullOrEmpty(route.CompanyEn) ? "kmb" : route.CompanyEn,
                TransportType = route.TransportType ?? "巴士",
                Direction = route.Direction ?? "O",
                ServiceType = route.ServiceType ?? "1",
                OriginTc = string.IsNullOrEmpty(route.OriginTc) ? (route.Origin ?? "") : route.OriginTc,
                OriginEn = string.IsNullOrEmpty(route.OriginEn) ? (route.Origin ?? "") : route.OriginEn,
                DestinationTc = string.IsNullOrEmpty(route.DestinationTc) ? (route.Destination ?? "") : route.DestinationTc,
                DestinationEn = string.IsNullOrEmpty(route.DestinationEn) ? (route.Destination ?? "") : route.DestinationEn,
                StopId = stop.StopId ?? "",
                StopSeq = stop.Sequence,
                StopNameTc = string.IsNullOrEmpty(stop.StopNameTc) ? (stop.StopNameAndCode ?? "") : stop.StopNameTc,
                StopNameEn = string.IsNullOrEmpty(stop.StopNameEn) ? (stop.StopNameAndCode ?? "") : stop.StopNameEn,
                FareText = stop.BracketFareText,
                GmbRegion = route.GmbRegion ?? "",
                GmbRouteId = route.GmbRouteId ?? "",
                GmbRouteSeq = route.GmbRouteSeq
            };

            list.Insert(0, newEntry);
            SaveFavouritesToSettings(list);
            return true;
        }

        private static void SaveFavouritesToSettings(List<FavouriteStationEntry> list)
        {
            try
            {
                JsonArray arr = new JsonArray();
                for (int i = 0; i < list.Count; i++)
                {
                    FavouriteStationEntry e = list[i];
                    JsonObject obj = new JsonObject();
                    obj["FavKey"] = JsonValue.CreateStringValue(e.FavKey ?? "");
                    obj["RouteId"] = JsonValue.CreateStringValue(e.RouteId ?? "");
                    obj["RouteNumber"] = JsonValue.CreateStringValue(e.RouteNumber ?? "");
                    obj["CompanyTc"] = JsonValue.CreateStringValue(e.CompanyTc ?? "");
                    obj["CompanyEn"] = JsonValue.CreateStringValue(e.CompanyEn ?? "");
                    obj["TransportType"] = JsonValue.CreateStringValue(e.TransportType ?? "");
                    obj["Direction"] = JsonValue.CreateStringValue(e.Direction ?? "");
                    obj["ServiceType"] = JsonValue.CreateStringValue(e.ServiceType ?? "");
                    obj["OriginTc"] = JsonValue.CreateStringValue(e.OriginTc ?? "");
                    obj["OriginEn"] = JsonValue.CreateStringValue(e.OriginEn ?? "");
                    obj["DestinationTc"] = JsonValue.CreateStringValue(e.DestinationTc ?? "");
                    obj["DestinationEn"] = JsonValue.CreateStringValue(e.DestinationEn ?? "");
                    obj["StopId"] = JsonValue.CreateStringValue(e.StopId ?? "");
                    obj["StopSeq"] = JsonValue.CreateNumberValue(e.StopSeq);
                    obj["StopNameTc"] = JsonValue.CreateStringValue(e.StopNameTc ?? "");
                    obj["StopNameEn"] = JsonValue.CreateStringValue(e.StopNameEn ?? "");
                    obj["FareText"] = JsonValue.CreateStringValue(e.FareText ?? "");
                    obj["GmbRegion"] = JsonValue.CreateStringValue(e.GmbRegion ?? "");
                    obj["GmbRouteId"] = JsonValue.CreateStringValue(e.GmbRouteId ?? "");
                    obj["GmbRouteSeq"] = JsonValue.CreateNumberValue(e.GmbRouteSeq);
                    arr.Add(obj);
                }

                ApplicationData.Current.LocalSettings.Values[FavouritesJsonKey] = arr.Stringify();
            }
            catch (Exception)
            {
            }
        }

        private static string GetStr(JsonObject obj, string key)
        {
            if (obj != null && obj.ContainsKey(key) && obj[key].ValueType == JsonValueType.String)
            {
                return obj[key].GetString();
            }
            return string.Empty;
        }

        private static double GetNum(JsonObject obj, string key, double defVal)
        {
            if (obj != null && obj.ContainsKey(key) && obj[key].ValueType == JsonValueType.Number)
            {
                return obj[key].GetNumber();
            }
            return defVal;
        }
    }
}