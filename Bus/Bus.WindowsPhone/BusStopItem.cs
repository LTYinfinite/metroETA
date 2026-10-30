using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Windows.UI.Xaml;

namespace HKBusWP
{
    public class BusStopItem : INotifyPropertyChanged
    {
        public string RouteId { get; set; }
        public string StopId { get; set; }
        public int Sequence { get; set; }
        public string StopNameTc { get; set; }
        public string StopNameEn { get; set; }
        public string NormalFare { get; set; }
        public string JoyYouFare { get; set; }
        public bool IsSectionFare { get; set; }
        public bool IsRailwayStop { get; set; }
        public bool HasTimetable { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public List<int> LiveEtaMinutes { get; set; }
        public List<string> LiveEtaRemarks { get; set; }
        public bool IsLoadingEta { get; set; }
        public string NoEtaCustomMessage { get; set; }

        private bool _isFavouriteStop;
        public bool IsFavouriteStop
        {
            get { return _isFavouriteStop; }
            set
            {
                _isFavouriteStop = value;
                OnPropertyChanged("IsFavouriteStop");
                OnPropertyChanged("FavouriteStarGlyph");
                OnPropertyChanged("FavouriteButtonLabel");
            }
        }

        public BusStopItem()
        {
            RouteId = string.Empty;
            StopId = string.Empty;
            StopNameTc = string.Empty;
            StopNameEn = string.Empty;
            NormalFare = string.Empty;
            JoyYouFare = string.Empty;
            IsSectionFare = false;
            IsRailwayStop = false;
            HasTimetable = false;
            NoEtaCustomMessage = string.Empty;
            LiveEtaMinutes = new List<int>();
            LiveEtaRemarks = new List<string>();
        }

        public static string CalculateJoyYouFare(string normalFareText)
        {
            if (string.IsNullOrEmpty(normalFareText)) return string.Empty;
            string cleaned = normalFareText.Replace("$", "").Trim();
            double price = 0;
            if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out price))
            {
                if (price <= 0) return "$0.0";
                if (price <= 2.0) return string.Format("${0:F1}", price);
                if (price >= 10.0)
                {
                    double twentyPercent = Math.Round(price * 0.2, 1);
                    return string.Format("${0:F1}", Math.Max(2.0, twentyPercent));
                }
                return "$2.0";
            }
            return "$2.0";
        }

        public string StopNameAndCode
        {
            get
            {
                string raw = (ThemeManager.IsEnglish && !string.IsNullOrEmpty(StopNameEn))
                    ? StopNameEn
                    : (!string.IsNullOrEmpty(StopNameTc) ? StopNameTc : (StopNameEn ?? string.Empty));
                return ThemeManager.FormatStopNameWithCodeRule(raw);
            }
            set
            {
                StopNameTc = value;
                if (string.IsNullOrEmpty(StopNameEn))
                {
                    StopNameEn = value;
                }
            }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                _isExpanded = value;
                RefreshEtaBindings();
            }
        }

        public string HeaderText
        {
            get { return string.Format("{0}. {1}", Sequence, StopNameAndCode); }
        }

        public string FareSummaryText
        {
            get
            {
                if (IsRailwayStop || string.IsNullOrEmpty(NormalFare) || NormalFare == "$0.0")
                {
                    return string.Empty;
                }

                string joy = string.IsNullOrEmpty(JoyYouFare) ? CalculateJoyYouFare(NormalFare) : JoyYouFare;
                string secTag = IsSectionFare
                    ? (ThemeManager.IsEnglish ? "   [section fare]" : "   [分段收費]")
                    : string.Empty;

                if (ThemeManager.IsEnglish)
                {
                    if (string.IsNullOrEmpty(joy))
                    {
                        return string.Format("fare: {0}{1}", NormalFare, secTag);
                    }
                    return string.Format("fare: {0}     joyyou: {1}{2}", NormalFare, joy, secTag);
                }
                else
                {
                    if (string.IsNullOrEmpty(joy))
                    {
                        return string.Format("車費: {0}{1}", NormalFare, secTag);
                    }
                    return string.Format("車費: {0}     樂悠車費: {1}{2}", NormalFare, joy, secTag);
                }
            }
        }

        public string BracketFareText
        {
            get
            {
                if (IsRailwayStop || string.IsNullOrEmpty(NormalFare) || NormalFare == "$0.0")
                {
                    return string.Empty;
                }
                string joy = string.IsNullOrEmpty(JoyYouFare) ? CalculateJoyYouFare(NormalFare) : JoyYouFare;
                if (string.IsNullOrEmpty(joy))
                {
                    return string.Format("({0})", NormalFare);
                }
                return string.Format("({0}, {1})", NormalFare, joy);
            }
        }

        public Visibility FareSummaryVisibility
        {
            get
            {
                if (IsRailwayStop || string.IsNullOrEmpty(NormalFare) || NormalFare == "$0.0")
                {
                    return Visibility.Collapsed;
                }
                return Visibility.Visible;
            }
        }

        public Visibility StopPortalButtonVisibility
        {
            get { return IsRailwayStop ? Visibility.Collapsed : Visibility.Visible; }
        }

        public Visibility ExpandedPanelVisibility
        {
            get { return IsExpanded ? Visibility.Visible : Visibility.Collapsed; }
        }

        public List<int> GetValidEtasWithin60Minutes()
        {
            List<int> valid = new List<int>();
            if (LiveEtaMinutes == null)
            {
                return valid;
            }

            for (int i = 0; i < LiveEtaMinutes.Count; i++)
            {
                int mins = LiveEtaMinutes[i];
                if (mins >= 0 && mins <= 60)
                {
                    valid.Add(mins);
                    if (valid.Count == 3)
                    {
                        break;
                    }
                }
            }
            return valid;
        }

        private string FormatEtaNumber(int index)
        {
            if (IsLoadingEta && index == 0)
            {
                return ThemeManager.Tr("載入中...", "loading...");
            }

            List<int> valid = GetValidEtasWithin60Minutes();
            if (index >= valid.Count)
            {
                if (index == 0)
                {
                    if (!string.IsNullOrEmpty(NoEtaCustomMessage))
                    {
                        return NoEtaCustomMessage;
                    }
                    int hour = DateTime.Now.Hour;
                    if (hour >= 1 && hour < 5)
                    {
                        return ThemeManager.Tr("尾班車已過本站", "last bus has departed");
                    }
                    return ThemeManager.Tr("未有班次資料", "no eta data available");
                }
                return string.Empty;
            }

            return ThemeManager.IsEnglish
                ? string.Format("{0} mins", valid[index])
                : string.Format("{0} 分鐘", valid[index]);
        }

        private string FormatEtaRemark(int index)
        {
            if (IsLoadingEta && index == 0)
            {
                return string.Empty;
            }

            List<int> valid = GetValidEtasWithin60Minutes();
            if (index >= valid.Count)
            {
                return string.Empty;
            }

            if (LiveEtaRemarks != null && index < LiveEtaRemarks.Count)
            {
                string rmk = LiveEtaRemarks[index];
                if (!string.IsNullOrEmpty(rmk) && rmk != "實時班次" && rmk.ToLowerInvariant() != "real-time")
                {
                    return string.Format("  -  {0}", rmk);
                }
            }
            return string.Empty;
        }

        private string GetPinButtonTextForIndex(int etaIdx)
        {
            bool isPinned = TileService.IsStopEtaPinned(RouteId, Sequence, etaIdx);
            if (isPinned)
            {
                return ThemeManager.Tr("📌 取消釘選", "📌 unpin");
            }
            return ThemeManager.Tr("📌 釘選", "📌 pin");
        }

        public string EtaLine1Number { get { return FormatEtaNumber(0); } }
        public string EtaLine1Remark { get { return FormatEtaRemark(0); } }
        public Visibility EtaPin1Visibility
        {
            get { return (!IsLoadingEta && GetValidEtasWithin60Minutes().Count >= 1) ? Visibility.Visible : Visibility.Collapsed; }
        }
        public string PinButton1Label { get { return GetPinButtonTextForIndex(0); } }

        public string EtaLine2Number { get { return FormatEtaNumber(1); } }
        public string EtaLine2Remark { get { return FormatEtaRemark(1); } }
        public Visibility EtaLine2Visibility
        {
            get { return (!IsLoadingEta && GetValidEtasWithin60Minutes().Count >= 2) ? Visibility.Visible : Visibility.Collapsed; }
        }
        public string PinButton2Label { get { return GetPinButtonTextForIndex(1); } }

        public string EtaLine3Number { get { return FormatEtaNumber(2); } }
        public string EtaLine3Remark { get { return FormatEtaRemark(2); } }
        public Visibility EtaLine3Visibility
        {
            get { return (!IsLoadingEta && GetValidEtasWithin60Minutes().Count >= 3) ? Visibility.Visible : Visibility.Collapsed; }
        }
        public string PinButton3Label { get { return GetPinButtonTextForIndex(2); } }

        public Visibility NoEtaTimetableButtonVisibility
        {
            get
            {
                return (!IsLoadingEta && GetValidEtasWithin60Minutes().Count == 0 && HasTimetable)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        public string NoEtaTimetableButtonLabel
        {
            get { return ThemeManager.Tr("🕒 查看路線時間表", "🕒 view route timetable"); }
        }

        public string PinButtonLabel
        {
            get { return GetPinButtonTextForIndex(0); }
        }

        public string StopPortalButtonLabel
        {
            get { return ThemeManager.Tr("ℹ 同站路線", "ℹ stop routes"); }
        }

        public string FavouriteStarGlyph
        {
            get { return IsFavouriteStop ? "★" : "☆"; }
        }

        public string FavouriteButtonLabel
        {
            get
            {
                if (IsFavouriteStop)
                {
                    return ThemeManager.Tr("★ 已收藏", "★ saved");
                }
                return ThemeManager.Tr("☆ 收藏本站", "☆ save stop");
            }
        }

        public void RefreshEtaBindings()
        {
            OnPropertyChanged("StopNameAndCode");
            OnPropertyChanged("HeaderText");
            OnPropertyChanged("FareSummaryText");
            OnPropertyChanged("BracketFareText");
            OnPropertyChanged("FareSummaryVisibility");
            OnPropertyChanged("StopPortalButtonVisibility");
            OnPropertyChanged("IsExpanded");
            OnPropertyChanged("ExpandedPanelVisibility");
            OnPropertyChanged("EtaLine1Number");
            OnPropertyChanged("EtaLine1Remark");
            OnPropertyChanged("EtaPin1Visibility");
            OnPropertyChanged("PinButton1Label");
            OnPropertyChanged("EtaLine2Number");
            OnPropertyChanged("EtaLine2Remark");
            OnPropertyChanged("EtaLine2Visibility");
            OnPropertyChanged("PinButton2Label");
            OnPropertyChanged("EtaLine3Number");
            OnPropertyChanged("EtaLine3Remark");
            OnPropertyChanged("EtaLine3Visibility");
            OnPropertyChanged("PinButton3Label");
            OnPropertyChanged("NoEtaTimetableButtonVisibility");
            OnPropertyChanged("NoEtaTimetableButtonLabel");
            OnPropertyChanged("PinButtonLabel");
            OnPropertyChanged("StopPortalButtonLabel");
            OnPropertyChanged("FavouriteStarGlyph");
            OnPropertyChanged("FavouriteButtonLabel");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}