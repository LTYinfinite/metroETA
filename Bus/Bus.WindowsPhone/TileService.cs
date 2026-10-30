using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.UI.Notifications;
using Windows.UI.StartScreen;
using Windows.UI.Xaml;

namespace HKBusWP
{
    public static class TileService
    {
        private const string TileTargetTicksKey = "MetroEta_TileTargetUtcTicks";
        private const string TileRevertTicksKey = "MetroEta_TileRevertUtcTicks";
        private const string TileRouteIdKey = "MetroEta_TileRouteId";
        private const string TileStopSeqKey = "MetroEta_TileStopSeq";
        private const string TileEtaIdxKey = "MetroEta_TileEtaIdx";
        private const string TileRouteNoKey = "MetroEta_TileRouteNo";
        private const string TileDestKey = "MetroEta_TileDest";
        private const string TileStopKey = "MetroEta_TileStop";
        private const string TileCondKey = "MetroEta_TileCondition";
        private const string SingleTileTag = "metroeta";

        private static DispatcherTimer _watchdogTimer;
        private static int _lastPushedMinute = -999;

        public static void Initialize()
        {
            if (_watchdogTimer == null)
            {
                _watchdogTimer = new DispatcherTimer();
                _watchdogTimer.Interval = TimeSpan.FromSeconds(4);
                _watchdogTimer.Tick += WatchdogTimer_Tick;
                _watchdogTimer.Start();
            }
            CheckAndSyncTileState();
        }

        private static void WatchdogTimer_Tick(object sender, object e)
        {
            CheckAndSyncTileState();
        }

        public static bool HasActivePinnedTile()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            if (!settings.ContainsKey(TileRevertTicksKey)) return false;

            long revertTicks = (long)settings[TileRevertTicksKey];
            DateTime revertUtc = new DateTime(revertTicks, DateTimeKind.Utc);
            return DateTime.UtcNow < revertUtc;
        }

        public static bool IsRoutePinned(string routeId)
        {
            if (string.IsNullOrEmpty(routeId) || !HasActivePinnedTile()) return false;
            var settings = ApplicationData.Current.LocalSettings.Values;
            string savedRouteId = settings.ContainsKey(TileRouteIdKey) ? (string)settings[TileRouteIdKey] : "";
            return string.Equals(savedRouteId, routeId, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsStopEtaPinned(string routeId, int stopSeq, int etaIndex)
        {
            if (!IsRoutePinned(routeId)) return false;
            var settings = ApplicationData.Current.LocalSettings.Values;
            int savedSeq = settings.ContainsKey(TileStopSeqKey) ? (int)settings[TileStopSeqKey] : -1;
            int savedIdx = settings.ContainsKey(TileEtaIdxKey) ? (int)settings[TileEtaIdxKey] : -1;
            return savedSeq == stopSeq && savedIdx == etaIndex;
        }

        public static string GetPinnedBannerSummaryText()
        {
            if (!HasActivePinnedTile()) return string.Empty;
            var settings = ApplicationData.Current.LocalSettings.Values;
            string routeNo = settings.ContainsKey(TileRouteNoKey) ? (string)settings[TileRouteNoKey] : "";
            string stopName = settings.ContainsKey(TileStopKey) ? (string)settings[TileStopKey] : "";
            DateTime targetLocal = new DateTime((long)settings[TileTargetTicksKey], DateTimeKind.Utc).ToLocalTime();
            int remMins = (int)Math.Max(0, Math.Floor((targetLocal - DateTime.Now).TotalMinutes));

            return ThemeManager.IsEnglish
                ? string.Format("📌 pinned to start screen: {0} @ {1} ({2} mins)", routeNo, stopName, remMins)
                : string.Format("📌 已釘選至開始畫面：{0} @ {1} ({2} 分鐘)", routeNo, stopName, remMins);
        }

        public static void UnpinCurrentRouteTile()
        {
            ClearSavedPinState();
            ForceStandbyTilePrompt();
        }

        // Automatically updates the Start Screen tile when viewing/refreshing route info ETA in RouteDetailPage
        public static async Task SyncTileWhenShowingRouteEtaAsync(NearbyRouteItem route, BusStopItem stop)
        {
            if (route == null || stop == null) return;
            if (!IsRoutePinned(route.RouteId)) return;

            var settings = ApplicationData.Current.LocalSettings.Values;
            int savedSeq = settings.ContainsKey(TileStopSeqKey) ? (int)settings[TileStopSeqKey] : -1;
            int savedIdx = settings.ContainsKey(TileEtaIdxKey) ? (int)settings[TileEtaIdxKey] : 0;

            if (stop.Sequence == savedSeq)
            {
                List<int> valid = stop.GetValidEtasWithin60Minutes();
                if (valid.Count > 0)
                {
                    int useIdx = Math.Min(savedIdx, valid.Count - 1);
                    await PinRouteCountdownToStartTileAsync(route, stop, useIdx);
                }
            }
        }

        public static void CheckAndSyncTileState()
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings.Values;
                if (!settings.ContainsKey(TileRevertTicksKey) || !settings.ContainsKey(TileTargetTicksKey))
                {
                    return;
                }

                DateTime targetUtc = new DateTime((long)settings[TileTargetTicksKey], DateTimeKind.Utc);
                DateTime revertUtc = new DateTime((long)settings[TileRevertTicksKey], DateTimeKind.Utc);
                DateTime nowUtc = DateTime.UtcNow;

                if (nowUtc >= revertUtc)
                {
                    ClearSavedPinState();
                    ForceStandbyTilePrompt();
                    return;
                }

                double remSeconds = (targetUtc - nowUtc).TotalSeconds;
                int remMins = (remSeconds <= 0) ? 0 : (int)Math.Floor(remSeconds / 60.0);

                if (remMins != _lastPushedMinute)
                {
                    _lastPushedMinute = remMins;
                    string routeNo = settings.ContainsKey(TileRouteNoKey) ? (string)settings[TileRouteNoKey] : "";
                    string dest = settings.ContainsKey(TileDestKey) ? (string)settings[TileDestKey] : "";
                    string stopName = settings.ContainsKey(TileStopKey) ? (string)settings[TileStopKey] : "";
                    string cond = settings.ContainsKey(TileCondKey) ? (string)settings[TileCondKey] : "";
                    string clockStr = targetUtc.ToLocalTime().ToString("HH:mm");

                    XmlDocument currentXml = BuildCountdownTileXml(routeNo, dest, stopName, cond, remMins, clockStr);
                    PushImmediateNotificationToAllTiles(currentXml);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void ClearSavedPinState()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            settings.Remove(TileTargetTicksKey);
            settings.Remove(TileRevertTicksKey);
            settings.Remove(TileRouteIdKey);
            settings.Remove(TileStopSeqKey);
            settings.Remove(TileEtaIdxKey);
            settings.Remove(TileRouteNoKey);
            settings.Remove(TileDestKey);
            settings.Remove(TileStopKey);
            settings.Remove(TileCondKey);
            _lastPushedMinute = -999;
        }

        // Toggle Pin/Unpin: Returns true if newly pinned, false if unpinned
        public static async Task<bool> TogglePinRouteCountdownAsync(NearbyRouteItem route, BusStopItem stop, int etaIndex)
        {
            if (route == null || stop == null) return false;

            if (IsStopEtaPinned(route.RouteId, stop.Sequence, etaIndex))
            {
                UnpinCurrentRouteTile();
                return false;
            }

            await PinRouteCountdownToStartTileAsync(route, stop, etaIndex);
            return true;
        }

        // Silently deducts 30 seconds from predicted ETA (without showing -30s text) and updates Start Screen tile immediately
        public static async Task<int> PinRouteCountdownToStartTileAsync(NearbyRouteItem route, BusStopItem stop, int etaIndex)
        {
            if (route == null || stop == null) return -1;

            List<int> validEtas = stop.GetValidEtasWithin60Minutes();
            if (etaIndex < 0 || etaIndex >= validEtas.Count) return -1;

            int rawEtaMinutes = validEtas[etaIndex];
            string conditionRemark = "";
            if (stop.LiveEtaRemarks != null && etaIndex < stop.LiveEtaRemarks.Count)
            {
                conditionRemark = stop.LiveEtaRemarks[etaIndex] ?? "";
            }

            // Silently deduct 30 seconds for prediction buffer
            double totalPredictedSeconds = Math.Max(0, (rawEtaMinutes * 60.0) - 30.0);
            DateTime adjustedTargetLocal = DateTime.Now.AddSeconds(totalPredictedSeconds);
            DateTime revertLocal = adjustedTargetLocal.AddMinutes(1);

            int initialDisplayMins = (int)Math.Max(0, Math.Floor(totalPredictedSeconds / 60.0));
            _lastPushedMinute = initialDisplayMins;

            var settings = ApplicationData.Current.LocalSettings.Values;
            settings[TileTargetTicksKey] = adjustedTargetLocal.ToUniversalTime().Ticks;
            settings[TileRevertTicksKey] = revertLocal.ToUniversalTime().Ticks;
            settings[TileRouteIdKey] = route.RouteId ?? "";
            settings[TileStopSeqKey] = stop.Sequence;
            settings[TileEtaIdxKey] = etaIndex;
            settings[TileRouteNoKey] = route.RouteNumber ?? "";
            settings[TileDestKey] = route.Destination ?? "";
            settings[TileStopKey] = stop.StopNameAndCode ?? "";
            settings[TileCondKey] = conditionRemark;

            try
            {
                TileUpdater primaryUpdater = TileUpdateManager.CreateTileUpdaterForApplication();
                ScheduleCountdownAndRevertOnUpdater(
                    primaryUpdater,
                    route.RouteNumber,
                    route.Destination,
                    stop.StopNameAndCode,
                    conditionRemark,
                    initialDisplayMins,
                    adjustedTargetLocal,
                    revertLocal);

                var secondaryTiles = await SecondaryTile.FindAllAsync();
                foreach (var sTile in secondaryTiles)
                {
                    TileUpdater secUpdater = TileUpdateManager.CreateTileUpdaterForSecondaryTile(sTile.TileId);
                    ScheduleCountdownAndRevertOnUpdater(
                        secUpdater,
                        route.RouteNumber,
                        route.Destination,
                        stop.StopNameAndCode,
                        conditionRemark,
                        initialDisplayMins,
                        adjustedTargetLocal,
                        revertLocal);
                }
            }
            catch (Exception)
            {
            }

            return initialDisplayMins;
        }

        private static void ScheduleCountdownAndRevertOnUpdater(
            TileUpdater updater,
            string routeNo,
            string dest,
            string stopName,
            string conditionRemark,
            int initialMinutes,
            DateTime adjustedTargetLocal,
            DateTime revertLocal)
        {
            try
            {
                var existing = updater.GetScheduledTileNotifications();
                for (int i = 0; i < existing.Count; i++)
                {
                    try { updater.RemoveFromSchedule(existing[i]); } catch (Exception) { }
                }

                updater.EnableNotificationQueue(false);
                updater.Clear();

                string clockStr = adjustedTargetLocal.ToString("HH:mm");

                // 1. Push immediate tile notification (no ExpirationTime on immediate update so WP8.1 never drops it)
                XmlDocument initialXml = BuildCountdownTileXml(routeNo, dest, stopName, conditionRemark, initialMinutes, clockStr);
                updater.Update(new TileNotification(initialXml) { Tag = SingleTileTag });

                // 2. Schedule each future minute step safely
                DateTime now = DateTime.Now;
                for (int rem = initialMinutes; rem >= 0; rem--)
                {
                    DateTime stepTime = adjustedTargetLocal.AddMinutes(-rem);
                    if (stepTime > now.AddSeconds(8))
                    {
                        try
                        {
                            XmlDocument stepXml = BuildCountdownTileXml(routeNo, dest, stopName, conditionRemark, rem, clockStr);
                            ScheduledTileNotification stepNotif = new ScheduledTileNotification(stepXml, new DateTimeOffset(stepTime))
                            {
                                Tag = SingleTileTag
                            };
                            updater.AddToSchedule(stepNotif);
                        }
                        catch (Exception) { }
                    }
                }

                // 3. Schedule Standby Revert Notification at 0 - 1 minute (revertLocal)
                DateTime safeRevertTime = (revertLocal > now.AddSeconds(12)) ? revertLocal : now.AddSeconds(12);
                XmlDocument standbyXml = BuildStandbyTileXml();

                try
                {
                    ScheduledTileNotification revertNotif1 = new ScheduledTileNotification(standbyXml, new DateTimeOffset(safeRevertTime))
                    {
                        Tag = SingleTileTag
                    };
                    updater.AddToSchedule(revertNotif1);

                    ScheduledTileNotification revertNotif2 = new ScheduledTileNotification(standbyXml, new DateTimeOffset(safeRevertTime.AddSeconds(30)))
                    {
                        Tag = SingleTileTag
                    };
                    updater.AddToSchedule(revertNotif2);
                }
                catch (Exception) { }
            }
            catch (Exception)
            {
            }
        }

        private static async void PushImmediateNotificationToAllTiles(XmlDocument xml)
        {
            try
            {
                TileUpdater primaryUpdater = TileUpdateManager.CreateTileUpdaterForApplication();
                primaryUpdater.Update(new TileNotification(xml) { Tag = SingleTileTag });

                var secondaryTiles = await SecondaryTile.FindAllAsync();
                foreach (var sTile in secondaryTiles)
                {
                    TileUpdater secUpdater = TileUpdateManager.CreateTileUpdaterForSecondaryTile(sTile.TileId);
                    secUpdater.Update(new TileNotification(xml) { Tag = SingleTileTag });
                }
            }
            catch (Exception)
            {
            }
        }

        public static async void ForceStandbyTilePrompt()
        {
            try
            {
                XmlDocument standbyXml = BuildStandbyTileXml();

                TileUpdater primaryUpdater = TileUpdateManager.CreateTileUpdaterForApplication();
                ApplyStandbyToUpdater(primaryUpdater, standbyXml);

                var secondaryTiles = await SecondaryTile.FindAllAsync();
                foreach (var sTile in secondaryTiles)
                {
                    TileUpdater secUpdater = TileUpdateManager.CreateTileUpdaterForSecondaryTile(sTile.TileId);
                    ApplyStandbyToUpdater(secUpdater, standbyXml);
                }
            }
            catch (Exception)
            {
            }
        }

        public static void EnsureStandbyTileIfNoActiveCountdown()
        {
            if (HasActivePinnedTile())
            {
                CheckAndSyncTileState();
            }
            else
            {
                ForceStandbyTilePrompt();
            }
        }

        private static void ApplyStandbyToUpdater(TileUpdater updater, XmlDocument standbyXml)
        {
            var scheduled = updater.GetScheduledTileNotifications();
            for (int i = 0; i < scheduled.Count; i++)
            {
                try { updater.RemoveFromSchedule(scheduled[i]); } catch (Exception) { }
            }

            updater.EnableNotificationQueue(true);
            updater.Clear();
            updater.Update(new TileNotification(standbyXml) { Tag = SingleTileTag });
            updater.Update(new TileNotification(standbyXml) { Tag = "st2" });
        }

        public static XmlDocument BuildStandbyTileXml()
        {
            string prompt = ThemeManager.IsEnglish
                ? "Where do you want to go today?"
                : "今日想去邊度？";

            string xml = string.Format(
                "<tile>" +
                "<visual version='2'>" +
                "<binding template='TileSquare150x150Text04' fallback='TileSquareText04' branding='name'>" +
                "<text id='1'>{0}</text>" +
                "</binding>" +
                "<binding template='TileWide310x150Text03' fallback='TileWideText03' branding='name'>" +
                "<text id='1'>{0}</text>" +
                "</binding>" +
                "</visual>" +
                "</tile>",
                EscapeXml(prompt));

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            return doc;
        }

        // Clean countdown tile XML (No "-30s" text; displays condition remarks like "行車緩慢" when present)
        public static XmlDocument BuildCountdownTileXml(
            string routeNo,
            string dest,
            string stopName,
            string conditionRemark,
            int remainingMinutes,
            string arrivalClock)
        {
            bool en = ThemeManager.IsEnglish;
            string bigNum = remainingMinutes.ToString();

            bool hasCondition = !string.IsNullOrEmpty(conditionRemark) &&
                                conditionRemark != "實時班次" &&
                                conditionRemark.ToLowerInvariant() != "real-time";

            string squareSub = hasCondition
                ? string.Format("{0} | {1}", routeNo, conditionRemark)
                : (en ? string.Format("mins ({0}) | {1}", arrivalClock, routeNo) : string.Format("分鐘 ({0}) | {1}", arrivalClock, routeNo));

            string wideLine1 = en
                ? string.Format("route {0} to {1}", routeNo, dest)
                : string.Format("路線 {0} 往{1}", routeNo, dest);

            string wideLine2 = en
                ? string.Format("stop: {0}", stopName)
                : string.Format("車站: {0}", stopName);

            string baseTimeStatus = (remainingMinutes <= 0)
                ? (en ? "arriving now (" + arrivalClock + ")" : "即將到站 (" + arrivalClock + ")")
                : (en ? "scheduled at " + arrivalClock : "預定到站 " + arrivalClock);

            string wideLine3 = hasCondition
                ? (en ? string.Format("{0} | {1}", conditionRemark, baseTimeStatus) : string.Format("{0} | {1}", conditionRemark, baseTimeStatus))
                : baseTimeStatus;

            string wideRightSub = en ? "mins" : "分鐘";

            string tileXmlString = string.Format(
                "<tile>" +
                "<visual version='2'>" +
                "<binding template='TileSquare150x150Block' fallback='TileSquareBlock' branding='name'>" +
                "<text id='1'>{0}</text>" +
                "<text id='2'>{1}</text>" +
                "</binding>" +
                "<binding template='TileWide310x150BlockAndText01' fallback='TileWideBlockAndText01' branding='name'>" +
                "<text id='1'>{2}</text>" +
                "<text id='2'>{3}</text>" +
                "<text id='3'>{4}</text>" +
                "<text id='4'></text>" +
                "<text id='5'>{0}</text>" +
                "<text id='6'>{5}</text>" +
                "</binding>" +
                "</visual>" +
                "</tile>",
                EscapeXml(bigNum),
                EscapeXml(squareSub),
                EscapeXml(wideLine1),
                EscapeXml(wideLine2),
                EscapeXml(wideLine3),
                EscapeXml(wideRightSub));

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(tileXmlString);
            return xmlDoc;
        }

        private static string EscapeXml(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }
    }
}