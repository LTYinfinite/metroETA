using System;
using System.Collections.Generic;
using System.ComponentModel;
using Windows.UI.Xaml;

namespace HKBusWP
{
    public class NearbyRouteItem : INotifyPropertyChanged
    {
        public string RouteId { get; set; }
        public string RouteNumber { get; set; }
        public string Direction { get; set; }     // "O" or "I"
        public string ServiceType { get; set; }   // "1" or special
        public string SpecialNote { get; set; }
        public string CompanyTc { get; set; }
        public string CompanyEn { get; set; }
        public string TransportType { get; set; } // "巴士", "小巴", "鐵路", "電車", "渡輪"

        public bool IsJointKmbCtb { get; set; }
        public bool HasOppositeDirection { get; set; }

        public string GmbRegion { get; set; }
        public string GmbRouteId { get; set; }
        public int GmbRouteSeq { get; set; }
        public int NearestStopSeq { get; set; }

        private string _originTc;
        public string OriginTc
        {
            get { return _originTc; }
            set { _originTc = value; OnPropertyChanged("Origin"); OnPropertyChanged("OriginTc"); }
        }

        private string _originEn;
        public string OriginEn
        {
            get { return _originEn; }
            set { _originEn = value; OnPropertyChanged("Origin"); OnPropertyChanged("OriginEn"); }
        }

        public string Origin
        {
            get { return ThemeManager.IsEnglish && !string.IsNullOrEmpty(_originEn) ? _originEn : (_originTc ?? _originEn ?? ""); }
        }

        private string _destinationTc;
        public string DestinationTc
        {
            get { return _destinationTc; }
            set { _destinationTc = value; OnPropertyChanged("Destination"); OnPropertyChanged("DestinationTc"); OnPropertyChanged("TabHeaderTitle"); }
        }

        private string _destinationEn;
        public string DestinationEn
        {
            get { return _destinationEn; }
            set { _destinationEn = value; OnPropertyChanged("Destination"); OnPropertyChanged("DestinationEn"); OnPropertyChanged("TabHeaderTitle"); }
        }

        public string Destination
        {
            get { return ThemeManager.IsEnglish && !string.IsNullOrEmpty(_destinationEn) ? _destinationEn : (_destinationTc ?? _destinationEn ?? ""); }
        }

        public string NearestStopId { get; set; }

        private string _stopNameTc;
        public string StopNameTc
        {
            get { return _stopNameTc; }
            set { _stopNameTc = value; OnPropertyChanged("StopNameAndCode"); }
        }

        private string _stopNameEn;
        public string StopNameEn
        {
            get { return _stopNameEn; }
            set { _stopNameEn = value; OnPropertyChanged("StopNameAndCode"); }
        }

        public string StopNameAndCode
        {
            get
            {
                string raw = ThemeManager.IsEnglish && !string.IsNullOrEmpty(_stopNameEn)
                    ? _stopNameEn
                    : (_stopNameTc ?? _stopNameEn ?? "");
                return ThemeManager.FormatStopNameWithCodeRule(raw);
            }
        }

        private string _fareText;
        public string FareText
        {
            get
            {
                // Hide bus fare display on railway routes
                if (TransportType == "鐵路") return string.Empty;
                return _fareText;
            }
            set { _fareText = value; OnPropertyChanged("FareText"); }
        }

        public int JourneyTimeMinutes { get; set; }

        public DateTime? FirstArrivalTime { get; set; }
        public DateTime? SecondArrivalTime { get; set; }
        public DateTime? ThirdArrivalTime { get; set; }

        public List<BusStopItem> Stops { get; set; }

        public NearbyRouteItem()
        {
            Direction = "O";
            ServiceType = "1";
            SpecialNote = string.Empty;
            CompanyTc = "九巴";
            CompanyEn = "kmb";
            TransportType = "巴士";
            IsJointKmbCtb = false;
            HasOppositeDirection = true;
            GmbRegion = string.Empty;
            GmbRouteId = string.Empty;
            GmbRouteSeq = 1;
            NearestStopSeq = 1;
            _originTc = string.Empty;
            _originEn = string.Empty;
            _destinationTc = string.Empty;
            _destinationEn = string.Empty;
            _stopNameTc = string.Empty;
            _stopNameEn = string.Empty;
            _fareText = string.Empty;
            JourneyTimeMinutes = 40;
            Stops = new List<BusStopItem>();
        }

        public string Company
        {
            get { return ThemeManager.Tr(CompanyTc, CompanyEn); }
        }

        public string ToPrefixText
        {
            get { return ThemeManager.Tr("往 ", "to "); }
        }

        public string TabHeaderTitle
        {
            get { return string.Format("{0} {1}{2}", RouteNumber, ToPrefixText, Destination); }
        }

        private bool _showClockMode;
        public bool ShowClockMode
        {
            get { return _showClockMode; }
            set
            {
                _showClockMode = value;
                RefreshTimeDisplay();
            }
        }

        public string PrimaryEtaNumber
        {
            get
            {
                if (!FirstArrivalTime.HasValue)
                {
                    return string.Empty;
                }

                double diffMinutes = (FirstArrivalTime.Value - DateTime.Now).TotalMinutes;
                if (diffMinutes < -0.5 || diffMinutes > 60)
                {
                    return string.Empty;
                }

                if (ShowClockMode)
                {
                    return FirstArrivalTime.Value.ToString("HH:mm");
                }

                int mins = (int)Math.Max(0, Math.Floor(diffMinutes));
                return mins.ToString();
            }
        }

        public string PrimaryEtaUnit
        {
            get
            {
                if (!FirstArrivalTime.HasValue)
                {
                    return string.Empty;
                }

                double diffMinutes = (FirstArrivalTime.Value - DateTime.Now).TotalMinutes;
                if (diffMinutes < -0.5 || diffMinutes > 60)
                {
                    return string.Empty;
                }

                if (ShowClockMode)
                {
                    return string.Empty;
                }
                return ThemeManager.Tr(" 分鐘", " mins");
            }
        }

        public string SecondaryEtaText
        {
            get
            {
                if (!SecondArrivalTime.HasValue)
                {
                    return string.Empty;
                }

                double diffMinutes = (SecondArrivalTime.Value - DateTime.Now).TotalMinutes;
                if (diffMinutes < -0.5 || diffMinutes > 60)
                {
                    return string.Empty;
                }

                if (ShowClockMode)
                {
                    return SecondArrivalTime.Value.ToString("HH:mm");
                }

                int mins = (int)Math.Max(0, Math.Floor(diffMinutes));
                return ThemeManager.IsEnglish ? string.Format("{0} mins", mins) : string.Format("{0} 分鐘", mins);
            }
        }

        public Visibility SecondaryEtaVisibility
        {
            get
            {
                if (!SecondArrivalTime.HasValue)
                {
                    return Visibility.Collapsed;
                }
                double diffMinutes = (SecondArrivalTime.Value - DateTime.Now).TotalMinutes;
                return (diffMinutes >= -0.5 && diffMinutes <= 60) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public string ThirdEtaText
        {
            get
            {
                if (!ThirdArrivalTime.HasValue)
                {
                    return string.Empty;
                }

                double diffMinutes = (ThirdArrivalTime.Value - DateTime.Now).TotalMinutes;
                if (diffMinutes < -0.5 || diffMinutes > 60)
                {
                    return string.Empty;
                }

                if (ShowClockMode)
                {
                    return ThirdArrivalTime.Value.ToString("HH:mm");
                }

                int mins = (int)Math.Max(0, Math.Floor(diffMinutes));
                return ThemeManager.IsEnglish ? string.Format("{0} mins", mins) : string.Format("{0} 分鐘", mins);
            }
        }

        public Visibility ThirdEtaVisibility
        {
            get
            {
                if (!ThirdArrivalTime.HasValue)
                {
                    return Visibility.Collapsed;
                }
                double diffMinutes = (ThirdArrivalTime.Value - DateTime.Now).TotalMinutes;
                return (diffMinutes >= -0.5 && diffMinutes <= 60) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void RefreshTimeDisplay()
        {
            OnPropertyChanged("Company");
            OnPropertyChanged("ToPrefixText");
            OnPropertyChanged("Origin");
            OnPropertyChanged("Destination");
            OnPropertyChanged("StopNameAndCode");
            OnPropertyChanged("FareText");
            OnPropertyChanged("PrimaryEtaNumber");
            OnPropertyChanged("PrimaryEtaUnit");
            OnPropertyChanged("SecondaryEtaText");
            OnPropertyChanged("SecondaryEtaVisibility");
            OnPropertyChanged("ThirdEtaText");
            OnPropertyChanged("ThirdEtaVisibility");
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