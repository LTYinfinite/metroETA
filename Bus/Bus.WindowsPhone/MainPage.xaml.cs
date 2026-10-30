using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace HKBusWP
{
    public sealed partial class MainPage : Page
    {
        private ObservableCollection<NearbyRouteItem> _favouriteRoutes;
        private ObservableCollection<NearbyRouteItem> _nearbyBusRoutes;
        private ObservableCollection<NearbyRouteItem> _nearbyMinibusRoutes;
        private ObservableCollection<NearbyRouteItem> _railwayRoutes;
        private ObservableCollection<NearbyRouteItem> _tramRoutes;
        private ObservableCollection<NearbyRouteItem> _ferryRoutes;
        private ObservableCollection<TrafficIncidentItem> _trafficNewsList;

        private bool _isClockMode = false;
        private bool _isFirstEntry = true;
        private bool _hasValidGps = false;
        private bool _isRefreshing = false;
        private double _lastLat = 22.3019;
        private double _lastLon = 114.1772;
        private int _lastUsedRadius = 250;
        private bool _lastLanguageEnglish = false;

        private DispatcherTimer _uiTickTimer;
        private DispatcherTimer _liveApiRefreshTimer;

        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;

            _favouriteRoutes = new ObservableCollection<NearbyRouteItem>();
            _nearbyBusRoutes = new ObservableCollection<NearbyRouteItem>();
            _nearbyMinibusRoutes = new ObservableCollection<NearbyRouteItem>();
            _railwayRoutes = new ObservableCollection<NearbyRouteItem>();
            _tramRoutes = new ObservableCollection<NearbyRouteItem>();
            _ferryRoutes = new ObservableCollection<NearbyRouteItem>();
            _trafficNewsList = new ObservableCollection<TrafficIncidentItem>();

            FavouritesListView.ItemsSource = _favouriteRoutes;
            NearbyBusListView.ItemsSource = _nearbyBusRoutes;
            NearbyMinibusListView.ItemsSource = _nearbyMinibusRoutes;
            RailwayListView.ItemsSource = _railwayRoutes;
            TramListView.ItemsSource = _tramRoutes;
            FerryListView.ItemsSource = _ferryRoutes;
            TrafficNewsListView.ItemsSource = _trafficNewsList;

            InitializeMtrFareCalculator();

            _uiTickTimer = new DispatcherTimer();
            _uiTickTimer.Interval = TimeSpan.FromSeconds(5);
            _uiTickTimer.Tick += UiTickTimer_Tick;
            _uiTickTimer.Start();

            _liveApiRefreshTimer = new DispatcherTimer();
            _liveApiRefreshTimer.Interval = TimeSpan.FromSeconds(25);
            _liveApiRefreshTimer.Tick += LiveApiRefreshTimer_Tick;
            _liveApiRefreshTimer.Start();
        }

        private void InitializeMtrFareCalculator()
        {
            List<MtrStationOption> stations = RailwayService.GetMtrStationsList();
            MtrFromCombo.ItemsSource = stations;
            MtrToCombo.ItemsSource = stations;
            if (stations.Count >= 6)
            {
                MtrFromCombo.SelectedIndex = 0; // Admiralty
                MtrToCombo.SelectedIndex = 2; // Hung Hom
            }
        }

        private void MtrStationCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MtrStationOption fromSt = MtrFromCombo.SelectedItem as MtrStationOption;
            MtrStationOption toSt = MtrToCombo.SelectedItem as MtrStationOption;
            if (fromSt != null && toSt != null && MtrFareResultText != null)
            {
                MtrFareResultText.Text = RailwayService.CalculateMtrStationFare(fromSt, toSt);
            }
        }

        private void UiTickTimer_Tick(object sender, object e)
        {
            RefreshCollectionTimes(_favouriteRoutes);
            RefreshCollectionTimes(_nearbyBusRoutes);
            RefreshCollectionTimes(_nearbyMinibusRoutes);
            RefreshCollectionTimes(_railwayRoutes);
            RefreshCollectionTimes(_tramRoutes);
            RefreshCollectionTimes(_ferryRoutes);
            TileService.CheckAndSyncTileState();
        }

        private void RefreshCollectionTimes(ObservableCollection<NearbyRouteItem> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                list[i].RefreshTimeDisplay();
            }
        }

        private async void LiveApiRefreshTimer_Tick(object sender, object e)
        {
            if (!_isRefreshing)
            {
                await RefreshActiveTabEtasAsync(false);
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            ThemeManager.ApplyThemeToRootFrame(ThemeManager.CurrentTheme);
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            App.CustomBackPressHandler = HandleMainPageOverlayBackPress;

            bool langChanged = (_lastLanguageEnglish != ThemeManager.IsEnglish);
            _lastLanguageEnglish = ThemeManager.IsEnglish;
            ApplyLocalizedUiStrings();

            UiTickTimer_Tick(null, null);

            if (_isFirstEntry)
            {
                _isFirstEntry = false;
                MainPivot.SelectedIndex = Math.Min(MainPivot.Items.Count - 1, ThemeManager.DefaultStartTabIndex);

                PopulateRailwayTramAndFerryTabs();
                var weatherTask = RefreshHkoWeatherBadgeAsync();
                var favTask = RefreshFavouritesTabAsync();

                if (ThemeManager.HasLastKnownLocation)
                {
                    _lastLat = ThemeManager.LastKnownLatitude;
                    _lastLon = ThemeManager.LastKnownLongitude;
                    _hasValidGps = true;
                    await UpdateRoutesForCoordinatesAsync(_lastLat, _lastLon, false);
                }

                await RefreshLocationAndRoutesAsync(false);
            }
            else
            {
                await RefreshFavouritesTabAsync();

                if (langChanged)
                {
                    InitializeMtrFareCalculator();
                    PopulateRailwayTramAndFerryTabs();
                }

                if (_lastUsedRadius != ThemeManager.SearchRadiusMeters || langChanged)
                {
                    _lastUsedRadius = ThemeManager.SearchRadiusMeters;
                    if (_hasValidGps)
                    {
                        await UpdateRoutesForCoordinatesAsync(_lastLat, _lastLon, false);
                    }
                    else
                    {
                        await LoadGpsDeniedFallbackRoutesAsync();
                    }
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            App.CustomBackPressHandler = null;
        }

        private bool HandleMainPageOverlayBackPress()
        {
            if (MtrFareCalculatorOverlay.Visibility == Visibility.Visible)
            {
                MtrFareCalculatorOverlay.Visibility = Visibility.Collapsed;
                return true;
            }
            return false;
        }

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Search || args.VirtualKey == VirtualKey.F3)
            {
                args.Handled = true;
                this.Frame.Navigate(typeof(SearchPage));
            }
        }

        private void ApplyLocalizedUiStrings()
        {
            bool en = ThemeManager.IsEnglish;
            MainPivot.Title = en ? "metroeta" : "METROETA 香港交通預報";

            TabFavourites.Header = en ? "favourites" : "收藏";
            TabBus.Header = en ? "bus" : "巴士";
            TabMinibus.Header = en ? "minibus" : "小巴";
            TabRailway.Header = en ? "railway" : "鐵路";
            TabTram.Header = en ? "tram" : "電車";
            TabFerry.Header = en ? "ferry" : "渡輪";
            TabTraffic.Header = en ? "traffic" : "交通消息";

            FavouritesStatusText.Text = en
                ? "saved routes & stops (tap ☆ on any stop in route details to pin)"
                : "已收藏的常用路線及車站（可於路線頁點擊 ☆ 收藏本站）";
            RailwayStatusText.Text = en
                ? "live mtr & light rail arrivals (mtr fare calculator is in '...' menu)"
                : "港鐵及輕鐵實時班次（港鐵車費計算機可於右下角 ... 選單開啟）";
            TramStatusText.Text = en
                ? "hk tramways eastbound & westbound 36-stop corridor"
                : "香港電車東行及西行全線 36 站實時班次預報";
            FerryStatusText.Text = en
                ? "star ferry, sun ferry & hkkf pier departures"
                : "天星小輪、新渡輪及港九小輪航班預報";
            TrafficStatusText.Text = en
                ? "transport department special traffic & road closure news"
                : "運輸署實時特別交通及封路消息 (www.td.gov.hk)";

            MtrCalcOverlayTitle.Text = en ? "mtr fare calculator" : "港鐵站對站車費計算機";
            MtrFromLabel.Text = en ? "origin station" : "起點站";
            MtrToLabel.Text = en ? "destination station" : "終點站";
            MtrFareResultHeader.Text = en ? "fare details (adult / student / joyyou)" : "車費詳情 (成人 / 特惠 / 樂悠咭兩元或兩折)";

            SearchRouteButton.Label = en ? "search" : "搜尋路線";
            RefreshEtaButton.Label = en ? "refresh" : "更新班次";
            ToggleTimeFormatButton.Label = en ? "countdown/clock" : "倒數/時鐘";
            RefreshLocationButton.Label = en ? "locate" : "重新定位";
            OpenMtrFareCalcMenuButton.Label = en ? "mtr fare calculator" : "港鐵車費計算機";
            OpenSettingsMenuButton.Label = en ? "settings & about" : "設定及關於";
        }

        private async Task RefreshHkoWeatherBadgeAsync()
        {
            try
            {
                HkoWeatherSnapshot snap = await WeatherAndTrafficService.FetchHkoWeatherSnapshotAsync();
                TemperatureBadgeText.Text = snap.TemperatureText;

                if (!string.IsNullOrEmpty(snap.WeatherIconUrl))
                {
                    CurrentWeatherIconImage.Source = new BitmapImage(new Uri(snap.WeatherIconUrl));
                }

                WarningIconsPanel.Children.Clear();
                for (int i = 0; i < snap.WarningIconUrls.Count; i++)
                {
                    Image warnImg = new Image
                    {
                        Width = 22,
                        Height = 22,
                        Stretch = Stretch.Uniform,
                        Margin = new Thickness(3, 0, 0, 0),
                        Source = new BitmapImage(new Uri(snap.WarningIconUrls[i]))
                    };
                    WarningIconsPanel.Children.Add(warnImg);
                }
            }
            catch (Exception)
            {
            }
        }

        private void PopulateRailwayTramAndFerryTabs()
        {
            _railwayRoutes.Clear();
            List<NearbyRouteItem> rails = RailwayService.GetAllRailwayRoutes();
            for (int i = 0; i < rails.Count; i++)
            {
                rails[i].ShowClockMode = _isClockMode;
                _railwayRoutes.Add(rails[i]);
            }

            _tramRoutes.Clear();
            List<NearbyRouteItem> trams = TramAndFerryService.GetAllTramRoutes();
            for (int i = 0; i < trams.Count; i++)
            {
                trams[i].ShowClockMode = _isClockMode;
                _tramRoutes.Add(trams[i]);
            }

            _ferryRoutes.Clear();
            List<NearbyRouteItem> ferries = TramAndFerryService.GetAllFerryRoutes();
            for (int i = 0; i < ferries.Count; i++)
            {
                ferries[i].ShowClockMode = _isClockMode;
                _ferryRoutes.Add(ferries[i]);
            }
        }

        private async Task RefreshFavouritesTabAsync()
        {
            List<NearbyRouteItem> favItems = await BusRouteDatabase.FetchFavouriteRoutesLiveAsync(_isClockMode);
            _favouriteRoutes.Clear();
            for (int i = 0; i < favItems.Count; i++)
            {
                _favouriteRoutes.Add(favItems[i]);
            }

            if (_favouriteRoutes.Count == 0)
            {
                FavouritesStatusText.Text = ThemeManager.Tr(
                    "尚未收藏任何車站。請點選任何路線並展開車站，再按「☆ 收藏本站」。",
                    "no saved stops yet. open any route, tap a stop, and press '☆ save stop'.");
            }
            else
            {
                FavouritesStatusText.Text = ThemeManager.IsEnglish
                    ? string.Format("{0} saved route stops | updated {1:HH:mm:ss}", _favouriteRoutes.Count, DateTime.Now)
                    : string.Format("共 {0} 個已收藏路線車站 | 更新於 {1:HH:mm:ss}", _favouriteRoutes.Count, DateTime.Now);
            }
        }

        private async Task RefreshLocationAndRoutesAsync(bool isManualClick)
        {
            if (_isRefreshing && !isManualClick) return;
            _isRefreshing = true;

            string locatingMsg = ThemeManager.Tr("正在重新讀取 GPS 位置及更新附近路線...", "acquiring gps location & updating nearby routes...");
            LocationStatusText.Text = locatingMsg;
            MinibusStatusText.Text = locatingMsg;

            try
            {
                Geolocator geolocator = new Geolocator();
                geolocator.DesiredAccuracyInMeters = 50;

                Geoposition pos = await geolocator.GetGeopositionAsync(
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(6));

                _lastLat = pos.Coordinate.Point.Position.Latitude;
                _lastLon = pos.Coordinate.Point.Position.Longitude;
                _hasValidGps = true;
                _lastUsedRadius = ThemeManager.SearchRadiusMeters;
                ThemeManager.SaveLastKnownLocation(_lastLat, _lastLon);

                await UpdateRoutesForCoordinatesAsync(_lastLat, _lastLon, false);
            }
            catch (Exception)
            {
                if (ThemeManager.HasLastKnownLocation)
                {
                    _lastLat = ThemeManager.LastKnownLatitude;
                    _lastLon = ThemeManager.LastKnownLongitude;
                    _hasValidGps = true;
                    await UpdateRoutesForCoordinatesAsync(_lastLat, _lastLon, false);
                }
                else
                {
                    await LoadGpsDeniedFallbackRoutesAsync();
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private async Task LoadGpsDeniedFallbackRoutesAsync()
        {
            LocationStatusText.Text = ThemeManager.Tr(
                "未授權定位權限或無法取得 GPS：已自動按路線號碼由 1 號起順序列出全港路線",
                "location unavailable: showing all routes in ascending order from route 1");
            MinibusStatusText.Text = ThemeManager.Tr(
                "未授權定位權限：已按路線號碼由 1 號起順序列出專線小巴路線",
                "location unavailable: showing minibus routes in ascending order from route 1");

            List<NearbyRouteItem> fallbackBuses = await BusRouteDatabase.GetFallbackCatalogRoutesAsync("巴士");
            _nearbyBusRoutes.Clear();
            for (int i = 0; i < fallbackBuses.Count; i++)
            {
                fallbackBuses[i].ShowClockMode = _isClockMode;
                _nearbyBusRoutes.Add(fallbackBuses[i]);
            }

            List<NearbyRouteItem> fallbackGmb = await BusRouteDatabase.GetFallbackCatalogRoutesAsync("小巴");
            _nearbyMinibusRoutes.Clear();
            for (int i = 0; i < fallbackGmb.Count; i++)
            {
                fallbackGmb[i].ShowClockMode = _isClockMode;
                _nearbyMinibusRoutes.Add(fallbackGmb[i]);
            }
        }

        private async Task UpdateRoutesForCoordinatesAsync(double lat, double lon, bool isBackgroundRefresh)
        {
            try
            {
                int radius = ThemeManager.SearchRadiusMeters;
                List<NearbyRouteItem> liveBus = await BusRouteDatabase.FetchNearbyBusRoutesLiveAsync(lat, lon);
                _nearbyBusRoutes.Clear();
                for (int i = 0; i < liveBus.Count; i++)
                {
                    liveBus[i].ShowClockMode = _isClockMode;
                    _nearbyBusRoutes.Add(liveBus[i]);
                }

                List<KnownHkStop> nearestStops = GeoMapService.FindNearestStopsWithinRadius(lat, lon, radius, 1);
                string nearestStopName = (nearestStops.Count > 0)
                    ? ThemeManager.FormatStopNameWithCodeRule(ThemeManager.IsEnglish ? nearestStops[0].NameEn : nearestStops[0].NameTc)
                    : ThemeManager.Tr("未知車站", "unknown stop");
                double distMeters = (nearestStops.Count > 0)
                    ? GeoMapService.CalculateDistanceMeters(lat, lon, nearestStops[0].Lat, nearestStops[0].Lon)
                    : 0;

                LocationStatusText.Text = ThemeManager.IsEnglish
                    ? string.Format("nearest: {0} ({1:F0}m) | radius: {2}m | {3} routes ({4:HH:mm:ss})",
                        nearestStopName, distMeters, radius, _nearbyBusRoutes.Count, DateTime.Now)
                    : string.Format("最近: {0} ({1:F0}m) | 搜尋半徑: {2}m | 共 {3} 條路線 ({4:HH:mm:ss})",
                        nearestStopName, distMeters, radius, _nearbyBusRoutes.Count, DateTime.Now);

                if (!isBackgroundRefresh || _nearbyMinibusRoutes.Count == 0)
                {
                    List<NearbyRouteItem> liveMinibus = await GreenMinibusService.FetchNearbyMinibusRoutesLiveAsync(lat, lon);
                    _nearbyMinibusRoutes.Clear();
                    for (int i = 0; i < liveMinibus.Count; i++)
                    {
                        liveMinibus[i].ShowClockMode = _isClockMode;
                        _nearbyMinibusRoutes.Add(liveMinibus[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < _nearbyMinibusRoutes.Count; i++)
                    {
                        await GreenMinibusService.PopulateGmbRouteDetailsAndLiveEtaAsync(_nearbyMinibusRoutes[i], true, lat, lon);
                        _nearbyMinibusRoutes[i].ShowClockMode = _isClockMode;
                    }
                }

                string region = GreenMinibusService.DetermineGmbRegion(lat, lon);
                MinibusStatusText.Text = ThemeManager.IsEnglish
                    ? string.Format("nearest: {0} ({1}) | {2} gmb routes | updated {3:HH:mm:ss}",
                        nearestStopName, region.ToLowerInvariant(), _nearbyMinibusRoutes.Count, DateTime.Now)
                    : string.Format("附近: {0} ({1}) | 共 {2} 條專線小巴路線 | 更新於 {3:HH:mm:ss}",
                        nearestStopName, region, _nearbyMinibusRoutes.Count, DateTime.Now);

                var railTraceTask = RailwayService.RefreshRailwayTabNearestStationsAsync(_railwayRoutes);
            }
            catch (Exception)
            {
            }
        }

        private async void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainPivot.SelectedIndex == 0)
            {
                await RefreshFavouritesTabAsync();
            }
            else if (MainPivot.SelectedIndex == 3)
            {
                await RailwayService.RefreshRailwayTabNearestStationsAsync(_railwayRoutes);
            }
            else if (MainPivot.SelectedIndex == 6 && _trafficNewsList.Count == 0)
            {
                await RefreshTrafficNewsTabAsync();
            }
        }

        private async Task RefreshTrafficNewsTabAsync()
        {
            TrafficStatusText.Text = ThemeManager.Tr(
                "正在從運輸署下載最新特別交通及封路消息...",
                "downloading special traffic news from transport department...");

            List<TrafficIncidentItem> items = await WeatherAndTrafficService.FetchTrafficNewsAsync();
            _trafficNewsList.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                _trafficNewsList.Add(items[i]);
            }

            TrafficStatusText.Text = ThemeManager.IsEnglish
                ? string.Format("transport department live traffic news ({0} notices) | {1:HH:mm:ss}", _trafficNewsList.Count, DateTime.Now)
                : string.Format("運輸署實時特別交通及封路消息 (共 {0} 則) | 更新於 {1:HH:mm:ss}", _trafficNewsList.Count, DateTime.Now);
        }

        private async Task RefreshActiveTabEtasAsync(bool isManualClick)
        {
            if (_isRefreshing && !isManualClick) return;
            _isRefreshing = true;

            try
            {
                int idx = MainPivot.SelectedIndex;
                if (idx == 0)
                {
                    FavouritesStatusText.Text = ThemeManager.Tr("正在更新收藏路線實時班次...", "refreshing favourite routes...");
                    await RefreshFavouritesTabAsync();
                }
                else if (idx == 1 || idx == 2)
                {
                    if (idx == 1) LocationStatusText.Text = ThemeManager.Tr("正在更新巴士實時班次...", "refreshing live bus etas...");
                    if (idx == 2) MinibusStatusText.Text = ThemeManager.Tr("正在更新專線小巴實時班次...", "refreshing live minibus etas...");

                    double lat = _hasValidGps ? _lastLat : ThemeManager.LastKnownLatitude;
                    double lon = _hasValidGps ? _lastLon : ThemeManager.LastKnownLongitude;
                    await UpdateRoutesForCoordinatesAsync(lat, lon, !isManualClick);
                }
                else if (idx == 3)
                {
                    RailwayStatusText.Text = ThemeManager.Tr("正在更新港鐵及輕鐵實時班次...", "refreshing live railway trains...");
                    await RailwayService.RefreshRailwayTabNearestStationsAsync(_railwayRoutes);
                    RailwayStatusText.Text = ThemeManager.IsEnglish
                        ? string.Format("railway live arrivals | updated {0:HH:mm:ss}", DateTime.Now)
                        : string.Format("港鐵及輕鐵實時班次 | 更新於 {0:HH:mm:ss}", DateTime.Now);
                }
                else if (idx == 4 || idx == 5)
                {
                    PopulateRailwayTramAndFerryTabs();
                    TramStatusText.Text = ThemeManager.IsEnglish
                        ? string.Format("hk tramways live arrivals | updated {0:HH:mm:ss}", DateTime.Now)
                        : string.Format("香港電車實時班次預報 | 更新於 {0:HH:mm:ss}", DateTime.Now);
                    FerryStatusText.Text = ThemeManager.IsEnglish
                        ? string.Format("ferry pier departures | updated {0:HH:mm:ss}", DateTime.Now)
                        : string.Format("渡輪航班預報 | 更新於 {0:HH:mm:ss}", DateTime.Now);
                }
                else if (idx == 6)
                {
                    await RefreshTrafficNewsTabAsync();
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void RouteListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            NearbyRouteItem clickedRoute = e.ClickedItem as NearbyRouteItem;
            if (clickedRoute != null)
            {
                BusRouteDatabase.CurrentSelectedRoute = clickedRoute;
                this.Frame.Navigate(typeof(RouteDetailPage));
            }
        }

        private void SearchRouteButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SearchPage));
        }

        private async void RefreshEtaButton_Click(object sender, RoutedEventArgs e)
        {
            var weatherTask = RefreshHkoWeatherBadgeAsync();
            await RefreshActiveTabEtasAsync(true);
        }

        private void ToggleTimeFormatButton_Click(object sender, RoutedEventArgs e)
        {
            _isClockMode = !_isClockMode;
            SetClockMode(_favouriteRoutes);
            SetClockMode(_nearbyBusRoutes);
            SetClockMode(_nearbyMinibusRoutes);
            SetClockMode(_railwayRoutes);
            SetClockMode(_tramRoutes);
            SetClockMode(_ferryRoutes);
        }

        private void SetClockMode(ObservableCollection<NearbyRouteItem> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                list[i].ShowClockMode = _isClockMode;
            }
        }

        private async void RefreshLocationButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshLocationAndRoutesAsync(true);
        }

        private void OpenMtrFareCalcMenuButton_Click(object sender, RoutedEventArgs e)
        {
            MtrFareCalculatorOverlay.Visibility = Visibility.Visible;
            App.PlayTurnstileFlipAnimation(MtrFareCalculatorOverlay, -75.0, 0.0, 340);
        }

        private void CloseMtrFareCalcOverlay_Click(object sender, RoutedEventArgs e)
        {
            MtrFareCalculatorOverlay.Visibility = Visibility.Collapsed;
        }

        private void OpenSettingsMenuButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SettingsPage));
        }
    }
}