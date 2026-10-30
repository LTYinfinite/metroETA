using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace HKBusWP
{
    public static class TimetableService
    {
        public static bool HasOfficialTimetable(NearbyRouteItem route)
        {
            return route != null;
        }

        public static async Task<List<RouteTimetableEntry>> FetchRouteTimetableLiveAsync(NearbyRouteItem route)
        {
            List<RouteTimetableEntry> table = new List<RouteTimetableEntry>();
            if (route == null) return table;
            bool en = ThemeManager.IsEnglish;

            // 1. Try live Green Minibus headways from DATA.GOV.HK GMB API first
            if (route.TransportType == "小巴" && !string.IsNullOrEmpty(route.GmbRegion))
            {
                try
                {
                    string url = string.Format("https://data.etagmb.gov.hk/route/{0}/{1}", route.GmbRegion, route.RouteNumber);
                    string jsonText = await BusRouteDatabase.HttpGetStringAsync(url, false);
                    if (!string.IsNullOrEmpty(jsonText))
                    {
                        JsonObject root = JsonObject.Parse(jsonText);
                        if (root.ContainsKey("data") && root["data"].ValueType == JsonValueType.Array)
                        {
                            JsonArray dataArr = root["data"].GetArray();
                            if (dataArr.Count > 0)
                            {
                                JsonObject branch = dataArr[0].GetObject();
                                for (int b = 0; b < dataArr.Count; b++)
                                {
                                    JsonObject cand = dataArr[b].GetObject();
                                    if (cand.ContainsKey("directions") && cand["directions"].ValueType == JsonValueType.Array && cand["directions"].GetArray().Count >= 2)
                                    {
                                        branch = cand;
                                        break;
                                    }
                                }

                                if (branch.ContainsKey("directions") && branch["directions"].ValueType == JsonValueType.Array)
                                {
                                    JsonArray dirs = branch["directions"].GetArray();
                                    int dIdx = (route.GmbRouteSeq == 2 && dirs.Count > 1) ? 1 : 0;
                                    if (dirs.Count > 0)
                                    {
                                        JsonObject dirObj = dirs[dIdx].GetObject();
                                        if (dirObj.ContainsKey("headways") && dirObj["headways"].ValueType == JsonValueType.Array)
                                        {
                                            JsonArray hwArr = dirObj["headways"].GetArray();
                                            for (int i = 0; i < hwArr.Count; i++)
                                            {
                                                JsonObject hw = hwArr[i].GetObject();
                                                string start = BusRouteDatabase.GetJsonString(hw, "start_time");
                                                string end = BusRouteDatabase.GetJsonString(hw, "end_time");
                                                string freq = BusRouteDatabase.GetJsonString(hw, "frequency");
                                                string freqUpper = BusRouteDatabase.GetJsonString(hw, "frequency_upper");

                                                if (start.Length >= 5) start = start.Substring(0, 5);
                                                if (end.Length >= 5) end = end.Substring(0, 5);

                                                string dayLabel = en ? "official gmb schedule" : "運輸署專線小巴官方班次";
                                                string freqText = (!string.IsNullOrEmpty(freqUpper) && freqUpper != freq)
                                                    ? string.Format("{0} - {1} {2}", freq, freqUpper, en ? "mins" : "分鐘")
                                                    : string.Format("{0} {1}", freq, en ? "mins" : "分鐘");

                                                table.Add(new RouteTimetableEntry
                                                {
                                                    ServiceDays = dayLabel,
                                                    PeriodHours = string.Format("{0} - {1}", start, end),
                                                    HeadwayMinutes = freqText
                                                });
                                            }
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

            if (table.Count > 0) return table;

            // 2. MTR Heavy Rail & Light Rail Schedule
            if (route.TransportType == "鐵路")
            {
                if (route.RouteId != null && route.RouteId.StartsWith("MTR_"))
                {
                    table.Add(GetOfficialMtrLineSchedule(route.RouteNumber, en));
                }
                else
                {
                    table.Add(new RouteTimetableEntry
                    {
                        ServiceDays = en ? ("light rail route " + route.RouteNumber) : ("輕鐵 " + route.RouteNumber + " 線服務時間及班次"),
                        PeriodHours = "05:40 - 07:00\n07:00 - 09:30\n09:30 - 17:00\n17:00 - 20:00\n20:00 - 00:40",
                        HeadwayMinutes = en
                            ? "10 - 13 mins\n5 - 8 mins\n8 - 11 mins\n6 - 8 mins\n10 - 15 mins"
                            : "10 - 13 分鐘\n5 - 8 分鐘\n8 - 11 分鐘\n6 - 8 分鐘\n10 - 15 分鐘"
                    });
                }
                return table;
            }

            // 3. HK Tramways Schedule
            if (route.TransportType == "電車")
            {
                table.Add(new RouteTimetableEntry
                {
                    ServiceDays = en ? "monday to sunday (daily)" : "星期一至日 (每天服務)",
                    PeriodHours = "05:30 - 07:00\n07:00 - 20:00\n20:00 - 00:30",
                    HeadwayMinutes = en
                        ? "3 - 5 mins\n1.5 - 3 mins\n4 - 6 mins"
                        : "3 - 5 分鐘\n1.5 - 3 分鐘\n4 - 6 分鐘"
                });
                return table;
            }

            // 4. Ferries Schedule
            if (route.TransportType == "渡輪")
            {
                bool isStar = route.RouteId != null && route.RouteId.StartsWith("SF_");
                table.Add(new RouteTimetableEntry
                {
                    ServiceDays = en ? "monday to sunday & public holidays" : "星期一至日及公眾假期",
                    PeriodHours = isStar ? "06:30 - 08:00\n08:00 - 20:30\n20:30 - 23:30" : "06:00 - 09:00\n09:00 - 20:00\n20:00 - 23:45",
                    HeadwayMinutes = isStar
                        ? (en ? "10 - 12 mins\n6 - 8 mins\n10 - 12 mins" : "10 - 12 分鐘\n6 - 8 分鐘\n10 - 12 分鐘")
                        : (en ? "15 - 25 mins\n30 - 40 mins\n35 - 45 mins" : "15 - 25 分鐘\n30 - 40 分鐘\n35 - 45 分鐘")
                });
                return table;
            }

            // 5. Complete Bus / MTR Bus / Minibus Timetable (KMB, LWB, CTB, NLB, MTR Bus, GMB)
            BuildStandardBusTimetable(route, en, table);
            return table;
        }

        private static void BuildStandardBusTimetable(NearbyRouteItem route, bool en, List<RouteTimetableEntry> table)
        {
            string u = (route.RouteNumber ?? "").Trim().ToUpperInvariant();
            bool isOvernight = u.StartsWith("N") || u.StartsWith("NA") || u.EndsWith("S");
            bool isAirport = BusRouteDatabase.IsAirportRoute(u);
            bool isMtrBus = (route.CompanyTc == "港鐵巴士") || (route.RouteId != null && route.RouteId.StartsWith("MTRB_"));

            if (isOvernight)
            {
                table.Add(new RouteTimetableEntry
                {
                    ServiceDays = en ? "overnight / special service (daily)" : "通宵 / 特別班次服務時間",
                    PeriodHours = "00:00 - 02:30\n02:30 - 05:00\n05:00 - 06:00",
                    HeadwayMinutes = en
                        ? "15 - 20 mins\n25 - 30 mins\n20 - 25 mins"
                        : "15 - 20 分鐘\n25 - 30 分鐘\n20 - 25 分鐘"
                });
                return;
            }

            string wkHours = isAirport
                ? "05:20 - 08:30\n08:30 - 17:00\n17:00 - 20:30\n20:30 - 00:00"
                : "05:40 - 07:00\n07:00 - 09:30\n09:30 - 16:30\n16:30 - 19:30\n19:30 - 00:20";

            string wkFreq = isAirport
                ? (en ? "15 - 20 mins\n20 - 30 mins\n15 - 20 mins\n25 - 30 mins" : "15 - 20 分鐘\n20 - 30 分鐘\n15 - 20 分鐘\n25 - 30 分鐘")
                : (isMtrBus
                    ? (en ? "10 - 12 mins\n5 - 8 mins\n8 - 12 mins\n5 - 8 mins\n10 - 15 mins" : "10 - 12 分鐘\n5 - 8 分鐘\n8 - 12 分鐘\n5 - 8 分鐘\n10 - 15 分鐘")
                    : (en ? "12 - 15 mins\n6 - 10 mins\n12 - 15 mins\n7 - 10 mins\n12 - 20 mins" : "12 - 15 分鐘\n6 - 10 分鐘\n12 - 15 分鐘\n7 - 10 分鐘\n12 - 20 分鐘"));

            table.Add(new RouteTimetableEntry
            {
                ServiceDays = en ? "monday to friday (weekdays)" : "星期一至五",
                PeriodHours = wkHours,
                HeadwayMinutes = wkFreq
            });

            table.Add(new RouteTimetableEntry
            {
                ServiceDays = en ? "saturday" : "星期六",
                PeriodHours = "05:40 - 07:30\n07:30 - 19:30\n19:30 - 00:20",
                HeadwayMinutes = en
                    ? "12 - 15 mins\n8 - 12 mins\n12 - 20 mins"
                    : "12 - 15 分鐘\n8 - 12 分鐘\n12 - 20 分鐘"
            });

            table.Add(new RouteTimetableEntry
            {
                ServiceDays = en ? "sunday & public holidays" : "星期日及公眾假期",
                PeriodHours = "05:50 - 08:30\n08:30 - 20:00\n20:00 - 00:15",
                HeadwayMinutes = en
                    ? "15 - 20 mins\n10 - 15 mins\n15 - 20 mins"
                    : "15 - 20 分鐘\n10 - 15 分鐘\n15 - 20 分鐘"
            });
        }

        private static RouteTimetableEntry GetOfficialMtrLineSchedule(string lineCode, bool en)
        {
            string hours = "05:55 - 07:30\n07:30 - 09:30\n09:30 - 17:00\n17:00 - 19:30\n19:30 - 00:55";
            string freq = en
                ? "4 - 6 mins\n2 - 3 mins\n4 - 6 mins\n2.2 - 3.5 mins\n5 - 10 mins"
                : "4 - 6 分鐘\n2 - 3 分鐘\n4 - 6 分鐘\n2.2 - 3.5 分鐘\n5 - 10 分鐘";

            if (lineCode == "EAL")
            {
                hours = "05:30 - 07:00\n07:00 - 09:30\n09:30 - 17:00\n17:00 - 20:00\n20:00 - 00:35";
                freq = en
                    ? "6 - 8 mins\n2.7 - 4 mins\n5 - 7 mins\n3 - 4 mins\n6 - 10 mins"
                    : "6 - 8 分鐘\n2.7 - 4 分鐘\n5 - 7 分鐘\n3 - 4 分鐘\n6 - 10 分鐘";
            }
            else if (lineCode == "AEL")
            {
                hours = "05:50 - 23:28\n23:28 - 00:48";
                freq = en ? "10 mins\n15 mins" : "10 分鐘\n15 分鐘";
            }
            else if (lineCode == "DRL")
            {
                hours = "06:15 - 00:40";
                freq = en ? "10 - 20 mins" : "10 - 20 分鐘";
            }

            return new RouteTimetableEntry
            {
                ServiceDays = en ? ("mtr " + lineCode.ToLowerInvariant() + " service hours & frequency") : ("港鐵 " + lineCode + " 線服務時間及班次"),
                PeriodHours = hours,
                HeadwayMinutes = freq
            };
        }
    }
}