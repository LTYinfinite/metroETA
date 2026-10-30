using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace HKBusWP
{
    public sealed partial class RouteDetailPage : Page
    {
        private NearbyRouteItem _currentRoute;
        private ObservableCollection<BusStopItem> _stopsCollection;
        private ObservableCollection<NearbyRouteItem> _portalRoutesCollection;
        private List<BasicGeoposition> _roadGeometryPoints;
        private List<List<BasicGeoposition>> _railwayBranchesGeometry;

        private int _zoomLevel = 16;
        private double _mapCenterLat = 22.3019;
        private double _mapCenterLon = 114.1772;

        private static readonly Dictionary<string, BitmapImage> _tileImageCache = new Dictionary<string, BitmapImage>();

        public RouteDetailPage()
        {
            this.InitializeComponent();
            _stopsCollection = new ObservableCollection<BusStopItem>();
            _portalRoutesCollection = new ObservableCollection<NearbyRouteItem>();
            _roadGeometryPoints = new List<BasicGeoposition>();
            _railwayBranchesGeometry = new List<List<BasicGeoposition>>();

            StopsListView.ItemsSource = _stopsCollection;
            StopPortalListView.ItemsSource = _portalRoutesCollection;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            ThemeManager.ApplyThemeToRootFrame(ThemeManager.CurrentTheme);
            ApplyLocalizedLabels();

            App.CustomBackPressHandler = HandleOverlayBackPress;

            _currentRoute = BusRouteDatabase.CurrentSelectedRoute;
            if (_currentRoute == null)
            {
                List<NearbyRouteItem> all = await BusRouteDatabase.GetOrFetchAllRoutesAsync();
                if (all.Count > 0) _currentRoute = all[0];
            }

            await LoadRouteIntoViewAsync(_currentRoute);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            App.CustomBackPressHandler = null;
        }

        private bool HandleOverlayBackPress()
        {
            if (TimetableOverlay.Visibility == Visibility.Visible)
            {
                TimetableOverlay.Visibility = Visibility.Collapsed;
                return true;
            }
            if (StopPortalOverlay.Visibility == Visibility.Visible)
            {
                StopPortalOverlay.Visibility = Visibility.Collapsed;
                return true;
            }
            return false;
        }

        private void ApplyLocalizedLabels()
        {
            bool en = ThemeManager.IsEnglish;
            ReverseBtnLabel.Text = en ? "opposite" : "對頭線";
            TimetableBtnLabel.Text = en ? "timetable" : "時間表";
            UnpinBannerButton.Content = en ? "unpin" : "取消釘選";
            SearchOtherRouteBtn.Label = en ? "search" : "搜尋路線";
            RefreshRouteEtaBtn.Label = en ? "refresh" : "更新預報";
            RouteSettingsMenuBtn.Label = en ? "settings & about" : "設定及關於";
        }

        private void RefreshPinnedBannerAndButtons()
        {
            if (_currentRoute != null && TileService.IsRoutePinned(_currentRoute.RouteId))
            {
                PinnedRouteBanner.Visibility = Visibility.Visible;
                PinnedBannerText.Text = TileService.GetPinnedBannerSummaryText();
            }
            else
            {
                PinnedRouteBanner.Visibility = Visibility.Collapsed;
            }

            for (int i = 0; i < _stopsCollection.Count; i++)
            {
                _stopsCollection[i].RefreshEtaBindings();
            }
        }

        private async Task LoadRouteIntoViewAsync(NearbyRouteItem route)
        {
            if (route == null) return;

            StopPortalOverlay.Visibility = Visibility.Collapsed;
            TimetableOverlay.Visibility = Visibility.Collapsed;

            string toPrefix = ThemeManager.Tr("往 ", "to ");
            HeaderRouteNumberText.Text = route.RouteNumber;
            HeaderDestinationText.Text = toPrefix + route.Destination;
            JourneyTimeText.Text = ThemeManager.Tr("正在載入車站及實時預報...", "loading stops & live etas...");

            if (route.Stops == null || route.Stops.Count == 0)
            {
                await BusRouteDatabase.FetchRouteStopsLiveAsync(route);
            }

            ReverseDirectionButton.IsEnabled = route.HasOppositeDirection;
            ReverseDirectionButton.Opacity = route.HasOppositeDirection ? 1.0 : 0.35;

            bool hasTimetable = TimetableService.HasOfficialTimetable(route);
            OpenTimetableTopButton.Visibility = hasTimetable ? Visibility.Visible : Visibility.Collapsed;

            _stopsCollection.Clear();
            BusStopItem expandedStop = null;

            for (int i = 0; i < route.Stops.Count; i++)
            {
                BusStopItem stop = route.Stops[i];
                stop.RouteId = route.RouteId;
                stop.HasTimetable = hasTimetable;
                stop.IsFavouriteStop = ThemeManager.IsFavourite(route.RouteId, stop.StopId, stop.Sequence);
                stop.RefreshEtaBindings();

                if (stop.IsExpanded)
                {
                    if (expandedStop == null)
                    {
                        expandedStop = stop;
                    }
                    else
                    {
                        stop.IsExpanded = false;
                    }
                }
                _stopsCollection.Add(stop);
            }

            if (expandedStop == null && _stopsCollection.Count > 0)
            {
                expandedStop = _stopsCollection[0];
                expandedStop.IsExpanded = true;
            }

            if (expandedStop != null)
            {
                if (expandedStop.Latitude > 20.0)
                {
                    _mapCenterLat = expandedStop.Latitude;
                    _mapCenterLon = expandedStop.Longitude;
                }

                StopsListView.UpdateLayout();
                StopsListView.ScrollIntoView(expandedStop, ScrollIntoViewAlignment.Leading);

                await BusRouteDatabase.FetchStopEtaLiveAsync(route, expandedStop);
                await TileService.SyncTileWhenShowingRouteEtaAsync(route, expandedStop);
            }

            RefreshPinnedBannerAndButtons();

            HeaderDestinationText.Text = toPrefix + route.Destination;
            JourneyTimeText.Text = ThemeManager.IsEnglish
                ? string.Format("{0} | {1} stops", route.Company, _stopsCollection.Count)
                : string.Format("{0} | 共 {1} 個車站", route.Company, _stopsCollection.Count);

            _roadGeometryPoints.Clear();
            _railwayBranchesGeometry.Clear();
            RenderSlippyMap();

            if (route.Stops != null && route.Stops.Count >= 2)
            {
                if (route.TransportType == "鐵路")
                {
                    _railwayBranchesGeometry = GeoMapService.BuildRailwayPolylinesForRoute(route.RouteId, route.Stops);
                }
                else
                {
                    _roadGeometryPoints = await GeoMapService.FetchRouteMapGeometryAsync(route.RouteId, route.Stops);
                }
                RenderSlippyMap();
            }
        }

        private async void StopsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            BusStopItem clickedStop = e.ClickedItem as BusStopItem;
            if (clickedStop == null) return;

            bool willExpand = !clickedStop.IsExpanded;

            for (int i = 0; i < _stopsCollection.Count; i++)
            {
                if (_stopsCollection[i] != clickedStop && _stopsCollection[i].IsExpanded)
                {
                    _stopsCollection[i].IsExpanded = false;
                }
            }

            clickedStop.IsExpanded = willExpand;

            if (clickedStop.IsExpanded)
            {
                if (clickedStop.Latitude > 20.0)
                {
                    _mapCenterLat = clickedStop.Latitude;
                    _mapCenterLon = clickedStop.Longitude;
                    RenderSlippyMap();
                }

                await BusRouteDatabase.FetchStopEtaLiveAsync(_currentRoute, clickedStop);
                await TileService.SyncTileWhenShowingRouteEtaAsync(_currentRoute, clickedStop);
                RefreshPinnedBannerAndButtons();
            }
        }

        private void RenderSlippyMap()
        {
            if (SlippyMapCanvas == null || MapViewportGrid == null) return;

            MapDragTransform.X = 0;
            MapDragTransform.Y = 0;

            double viewW = MapViewportGrid.ActualWidth;
            double viewH = MapViewportGrid.ActualHeight;
            if (viewW <= 10) viewW = 400;
            if (viewH <= 10) viewH = 215;

            if (MapClipGeometry != null)
            {
                MapClipGeometry.Rect = new Rect(0, 0, viewW, viewH);
            }

            SlippyMapCanvas.Children.Clear();

            double n = Math.Pow(2.0, _zoomLevel);
            double centerExactX = ((_mapCenterLon + 180.0) / 360.0) * n;
            double latRad = _mapCenterLat * Math.PI / 180.0;
            double centerExactY = ((1.0 - (Math.Log(Math.Tan(latRad) + (1.0 / Math.Cos(latRad))) / Math.PI)) / 2.0) * n;

            int centerTileX = (int)Math.Floor(centerExactX);
            int centerTileY = (int)Math.Floor(centerExactY);

            double offsetX = (centerExactX - centerTileX) * 256.0;
            double offsetY = (centerExactY - centerTileY) * 256.0;

            string labelLang = ThemeManager.IsEnglish ? "en" : "tc";

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int tx = centerTileX + dx;
                    int ty = centerTileY + dy;
                    double tileLeft = Math.Round((viewW / 2.0) - offsetX + (dx * 256.0));
                    double tileTop = Math.Round((viewH / 2.0) - offsetY + (dy * 256.0));

                    string hkBaseUrl = string.Format("https://mapapi.geodata.gov.hk/gs/api/v1.0.0/xyz/basemap/WGS84/{0}/{1}/{2}.png", _zoomLevel, tx, ty);
                    Image baseTileImg = new Image
                    {
                        Width = 256,
                        Height = 256,
                        Stretch = Stretch.Fill,
                        Source = GetOrCreateTileBitmap(hkBaseUrl)
                    };
                    Canvas.SetLeft(baseTileImg, tileLeft);
                    Canvas.SetTop(baseTileImg, tileTop);
                    SlippyMapCanvas.Children.Add(baseTileImg);

                    string hkLabelUrl = string.Format("https://mapapi.geodata.gov.hk/gs/api/v1.0.0/xyz/label/hk/{0}/WGS84/{1}/{2}/{3}.png", labelLang, _zoomLevel, tx, ty);
                    Image labelTileImg = new Image
                    {
                        Width = 256,
                        Height = 256,
                        Stretch = Stretch.Fill,
                        Source = GetOrCreateTileBitmap(hkLabelUrl)
                    };
                    Canvas.SetLeft(labelTileImg, tileLeft);
                    Canvas.SetTop(labelTileImg, tileTop);
                    SlippyMapCanvas.Children.Add(labelTileImg);
                }
            }

            if (ThemeManager.HasLastKnownLocation)
            {
                double uLat = ThemeManager.LastKnownLatitude;
                double uLon = ThemeManager.LastKnownLongitude;
                double ux = ((uLon + 180.0) / 360.0) * n;
                double uLatRad = uLat * Math.PI / 180.0;
                double uy = ((1.0 - (Math.Log(Math.Tan(uLatRad) + (1.0 / Math.Cos(uLatRad))) / Math.PI)) / 2.0) * n;

                double upx = (viewW / 2.0) + ((ux - centerExactX) * 256.0);
                double upy = (viewH / 2.0) + ((uy - centerExactY) * 256.0);

                if (upx >= -80 && upx <= viewW + 80 && upy >= -80 && upy <= viewH + 80)
                {
                    Ellipse halo = new Ellipse
                    {
                        Width = 34,
                        Height = 34,
                        Fill = new SolidColorBrush(Color.FromArgb(65, 2, 136, 209)),
                        Stroke = new SolidColorBrush(Color.FromArgb(150, 2, 136, 209)),
                        StrokeThickness = 1.5
                    };
                    Canvas.SetLeft(halo, upx - 17);
                    Canvas.SetTop(halo, upy - 17);
                    SlippyMapCanvas.Children.Add(halo);

                    Ellipse gpsDot = new Ellipse
                    {
                        Width = 14,
                        Height = 14,
                        Fill = new SolidColorBrush(Color.FromArgb(255, 0, 176, 255)),
                        Stroke = new SolidColorBrush(Colors.White),
                        StrokeThickness = 2.5
                    };
                    Canvas.SetLeft(gpsDot, upx - 7);
                    Canvas.SetTop(gpsDot, upy - 7);
                    SlippyMapCanvas.Children.Add(gpsDot);
                }
            }

            if (_currentRoute != null && _currentRoute.Stops != null && _currentRoute.Stops.Count > 0)
            {
                bool isRailway = (_currentRoute.TransportType == "鐵路");
                bool isTram = (_currentRoute.TransportType == "電車");
                bool isFerry = (_currentRoute.TransportType == "渡輪");

                Color lineColor = isRailway
                    ? GetMtrLineColor(_currentRoute.RouteNumber)
                    : (isTram
                        ? Color.FromArgb(240, 46, 125, 50)
                        : (isFerry ? Color.FromArgb(240, 2, 119, 189) : Color.FromArgb(230, 211, 47, 47)));

                if (isRailway && _railwayBranchesGeometry != null && _railwayBranchesGeometry.Count > 0)
                {
                    // Render each railway branch (Main trunk + Spur branches) separately so spur lines never zig-zag
                    for (int b = 0; b < _railwayBranchesGeometry.Count; b++)
                    {
                        List<BasicGeoposition> branchPts = _railwayBranchesGeometry[b];
                        Polyline branchLine = new Polyline
                        {
                            Stroke = new SolidColorBrush(lineColor),
                            StrokeThickness = 6,
                            StrokeLineJoin = PenLineJoin.Round
                        };

                        for (int i = 0; i < branchPts.Count; i++)
                        {
                            BasicGeoposition pt = branchPts[i];
                            double sx = ((pt.Longitude + 180.0) / 360.0) * n;
                            double sLatRad = pt.Latitude * Math.PI / 180.0;
                            double sy = ((1.0 - (Math.Log(Math.Tan(sLatRad) + (1.0 / Math.Cos(sLatRad))) / Math.PI)) / 2.0) * n;

                            double px = (viewW / 2.0) + ((sx - centerExactX) * 256.0);
                            double py = (viewH / 2.0) + ((sy - centerExactY) * 256.0);
                            branchLine.Points.Add(new Point(px, py));
                        }
                        SlippyMapCanvas.Children.Add(branchLine);
                    }
                }
                else
                {
                    Polyline routeLine = new Polyline
                    {
                        Stroke = new SolidColorBrush(lineColor),
                        StrokeThickness = (isRailway || isTram) ? 6 : 5,
                        StrokeLineJoin = PenLineJoin.Round
                    };

                    if (_roadGeometryPoints != null && _roadGeometryPoints.Count >= 2)
                    {
                        for (int i = 0; i < _roadGeometryPoints.Count; i++)
                        {
                            BasicGeoposition pt = _roadGeometryPoints[i];
                            double sx = ((pt.Longitude + 180.0) / 360.0) * n;
                            double sLatRad = pt.Latitude * Math.PI / 180.0;
                            double sy = ((1.0 - (Math.Log(Math.Tan(sLatRad) + (1.0 / Math.Cos(sLatRad))) / Math.PI)) / 2.0) * n;

                            double px = (viewW / 2.0) + ((sx - centerExactX) * 256.0);
                            double py = (viewH / 2.0) + ((sy - centerExactY) * 256.0);
                            routeLine.Points.Add(new Point(px, py));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < _currentRoute.Stops.Count; i++)
                        {
                            BusStopItem stop = _currentRoute.Stops[i];
                            if (stop.Latitude <= 20.0) continue;

                            double sx = ((stop.Longitude + 180.0) / 360.0) * n;
                            double sLatRad = stop.Latitude * Math.PI / 180.0;
                            double sy = ((1.0 - (Math.Log(Math.Tan(sLatRad) + (1.0 / Math.Cos(sLatRad))) / Math.PI)) / 2.0) * n;

                            double px = (viewW / 2.0) + ((sx - centerExactX) * 256.0);
                            double py = (viewH / 2.0) + ((sy - centerExactY) * 256.0);
                            routeLine.Points.Add(new Point(px, py));
                        }
                    }
                    SlippyMapCanvas.Children.Add(routeLine);
                }

                for (int i = 0; i < _currentRoute.Stops.Count; i++)
                {
                    BusStopItem stop = _currentRoute.Stops[i];
                    if (stop.Latitude <= 20.0) continue;

                    double sx = ((stop.Longitude + 180.0) / 360.0) * n;
                    double sLatRad = stop.Latitude * Math.PI / 180.0;
                    double sy = ((1.0 - (Math.Log(Math.Tan(sLatRad) + (1.0 / Math.Cos(sLatRad))) / Math.PI)) / 2.0) * n;

                    double px = (viewW / 2.0) + ((sx - centerExactX) * 256.0);
                    double py = (viewH / 2.0) + ((sy - centerExactY) * 256.0);

                    if (px < -120 || px > viewW + 120 || py < -120 || py > viewH + 120) continue;

                    double dotSize = stop.IsExpanded ? 16 : 11;
                    Ellipse dot = new Ellipse
                    {
                        Width = dotSize,
                        Height = dotSize,
                        Fill = stop.IsExpanded
                            ? new SolidColorBrush(Color.FromArgb(255, 25, 118, 210))
                            : new SolidColorBrush(Colors.White),
                        Stroke = new SolidColorBrush(lineColor),
                        StrokeThickness = 3
                    };
                    Canvas.SetLeft(dot, px - (dotSize / 2.0));
                    Canvas.SetTop(dot, py - (dotSize / 2.0));
                    SlippyMapCanvas.Children.Add(dot);

                    if (stop.IsExpanded)
                    {
                        Border pinLabel = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(230, 31, 31, 31)),
                            BorderBrush = new SolidColorBrush(Colors.White),
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(6, 2, 6, 2),
                            Child = new TextBlock
                            {
                                Text = string.Format("{0}. {1}", stop.Sequence, stop.StopNameAndCode),
                                Foreground = new SolidColorBrush(Colors.White),
                                FontSize = 11,
                                FontWeight = Windows.UI.Text.FontWeights.Bold
                            }
                        };
                        Canvas.SetLeft(pinLabel, Math.Max(4, Math.Min(viewW - 150, px - 60)));
                        Canvas.SetTop(pinLabel, Math.Max(4, py - 32));
                        SlippyMapCanvas.Children.Add(pinLabel);
                    }
                }
            }
        }

        private Color GetMtrLineColor(string code)
        {
            if (code == "EAL") return Color.FromArgb(240, 83, 183, 232);
            if (code == "TML") return Color.FromArgb(240, 154, 59, 38);
            if (code == "KTL") return Color.FromArgb(240, 0, 160, 64);
            if (code == "TWL") return Color.FromArgb(240, 226, 35, 26);
            if (code == "ISL") return Color.FromArgb(240, 0, 113, 206);
            if (code == "TKL") return Color.FromArgb(240, 125, 73, 157);
            if (code == "TCL") return Color.FromArgb(240, 243, 139, 0);
            if (code == "AEL") return Color.FromArgb(240, 0, 112, 120);
            if (code == "SIL") return Color.FromArgb(240, 182, 194, 0);
            if (code == "DRL") return Color.FromArgb(240, 235, 110, 165);
            return Color.FromArgb(240, 211, 168, 9);
        }

        private static BitmapImage GetOrCreateTileBitmap(string url)
        {
            if (_tileImageCache.ContainsKey(url)) return _tileImageCache[url];
            if (_tileImageCache.Count > 180) _tileImageCache.Clear();
            BitmapImage bmp = new BitmapImage(new Uri(url));
            _tileImageCache[url] = bmp;
            return bmp;
        }

        private void MapViewportGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (MapClipGeometry != null)
            {
                MapClipGeometry.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
            }
            RenderSlippyMap();
        }

        private void MapViewportGrid_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            MapDragTransform.X += e.Delta.Translation.X;
            MapDragTransform.Y += e.Delta.Translation.Y;

            if (Math.Abs(MapDragTransform.X) > 180 || Math.Abs(MapDragTransform.Y) > 120)
            {
                CommitDragOffsetToMapCenter();
            }
        }

        private void MapViewportGrid_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            if (Math.Abs(MapDragTransform.X) > 2 || Math.Abs(MapDragTransform.Y) > 2)
            {
                CommitDragOffsetToMapCenter();
            }
        }

        private void CommitDragOffsetToMapCenter()
        {
            double dx = MapDragTransform.X;
            double dy = MapDragTransform.Y;
            MapDragTransform.X = 0;
            MapDragTransform.Y = 0;

            double n = Math.Pow(2.0, _zoomLevel);
            double lonPerPixel = 360.0 / (256.0 * n);
            double latPerPixel = lonPerPixel * Math.Cos(_mapCenterLat * Math.PI / 180.0);

            _mapCenterLon -= dx * lonPerPixel;
            _mapCenterLat += dy * latPerPixel;
            RenderSlippyMap();
        }

        private void ZoomInMap_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (_zoomLevel < 18)
            {
                _zoomLevel++;
                RenderSlippyMap();
            }
        }

        private void ZoomOutMap_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (_zoomLevel > 11)
            {
                _zoomLevel--;
                RenderSlippyMap();
            }
        }

        private async void CenterMapOnUserGps_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                if (ThemeManager.HasLastKnownLocation)
                {
                    _mapCenterLat = ThemeManager.LastKnownLatitude;
                    _mapCenterLon = ThemeManager.LastKnownLongitude;
                    RenderSlippyMap();
                }

                Geolocator geolocator = new Geolocator { DesiredAccuracyInMeters = 50 };
                Geoposition pos = await geolocator.GetGeopositionAsync(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));
                ThemeManager.SaveLastKnownLocation(pos.Coordinate.Point.Position.Latitude, pos.Coordinate.Point.Position.Longitude);
                _mapCenterLat = pos.Coordinate.Point.Position.Latitude;
                _mapCenterLon = pos.Coordinate.Point.Position.Longitude;
                RenderSlippyMap();
            }
            catch (Exception)
            {
            }
        }

        private void CenterMapOnStop_Tapped(object sender, TappedRoutedEventArgs e)
        {
            for (int i = 0; i < _stopsCollection.Count; i++)
            {
                if (_stopsCollection[i].IsExpanded && _stopsCollection[i].Latitude > 20.0)
                {
                    _mapCenterLat = _stopsCollection[i].Latitude;
                    _mapCenterLon = _stopsCollection[i].Longitude;
                    break;
                }
            }
            RenderSlippyMap();
        }

        private async void OsmAttribution_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.openstreetmap.org/copyright"));
        }

        private async void LandsDeptAttribution_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://api.portal.hkmapservice.gov.hk/disclaimer"));
        }

        // Toggles Pin / Unpin on Start Screen Tile, shows Pinned Banner, and updates button label to "取消釘選 / unpin"
        private async void PinEtaButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null || _currentRoute == null) return;

            BusStopItem stop = btn.DataContext as BusStopItem;
            if (stop == null) return;

            int etaIndex = 0;
            if (btn.Tag != null) int.TryParse(btn.Tag.ToString(), out etaIndex);

            await TileService.TogglePinRouteCountdownAsync(_currentRoute, stop, etaIndex);
            RefreshPinnedBannerAndButtons();
        }

        private void UnpinBannerButton_Click(object sender, RoutedEventArgs e)
        {
            TileService.UnpinCurrentRouteTile();
            RefreshPinnedBannerAndButtons();
        }

        private async void StopPortalButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            BusStopItem stop = btn.DataContext as BusStopItem;
            if (stop == null) return;

            StopPortalTitleText.Text = "🚏 " + stop.StopNameAndCode;
            StopPortalSubText.Text = ThemeManager.Tr("正在載入途經此車站的所有路線及實時預報...", "loading all routes serving this stop...");
            StopPortalOverlay.Visibility = Visibility.Visible;
            App.PlayTurnstileFlipAnimation(StopPortalOverlay, 82.0, 0.0, 340);

            _portalRoutesCollection.Clear();
            List<NearbyRouteItem> routesAtStop = await BusRouteDatabase.FetchRoutesForStopPortalAsync(stop, false);
            for (int i = 0; i < routesAtStop.Count; i++)
            {
                _portalRoutesCollection.Add(routesAtStop[i]);
            }

            StopPortalSubText.Text = ThemeManager.IsEnglish
                ? string.Format("{0} routes serving this stop (tap any route to switch):", _portalRoutesCollection.Count)
                : string.Format("共 {0} 條路線途經此站（點擊任何路線即可切換）：", _portalRoutesCollection.Count);
        }

        private async void StopPortalListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            NearbyRouteItem selected = e.ClickedItem as NearbyRouteItem;
            if (selected != null)
            {
                StopPortalOverlay.Visibility = Visibility.Collapsed;
                _currentRoute = selected;
                BusRouteDatabase.CurrentSelectedRoute = selected;
                await LoadRouteIntoViewAsync(_currentRoute);
            }
        }

        private void CloseStopPortalButton_Click(object sender, RoutedEventArgs e)
        {
            StopPortalOverlay.Visibility = Visibility.Collapsed;
        }

        private void ToggleFavouriteStopButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null || _currentRoute == null) return;

            BusStopItem stop = btn.DataContext as BusStopItem;
            if (stop == null) return;

            bool isNowFav = ThemeManager.ToggleFavourite(_currentRoute, stop);
            stop.IsFavouriteStop = isNowFav;
            stop.RefreshEtaBindings();

            JourneyTimeText.Text = isNowFav
                ? ThemeManager.Tr("★ 已將此路線及車站加入主頁「收藏」分頁", "★ added route & stop to favourites tab")
                : ThemeManager.Tr("☆ 已從「收藏」分頁移除此車站", "☆ removed stop from favourites tab");
        }

        private async void OpenTimetableButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRoute == null || !TimetableService.HasOfficialTimetable(_currentRoute)) return;

            TimetableTitleText.Text = ThemeManager.IsEnglish
                ? string.Format("🕒 {0} timetable ({1})", _currentRoute.RouteNumber, _currentRoute.Company)
                : string.Format("🕒 {0} 服務時間及班次表 ({1})", _currentRoute.RouteNumber, _currentRoute.Company);

            TimetableOverlay.Visibility = Visibility.Visible;
            App.PlayTurnstileFlipAnimation(TimetableOverlay, 82.0, 0.0, 340);

            TimetableListView.ItemsSource = await TimetableService.FetchRouteTimetableLiveAsync(_currentRoute);
        }

        private void CloseTimetableButton_Click(object sender, RoutedEventArgs e)
        {
            TimetableOverlay.Visibility = Visibility.Collapsed;
        }

        private async void ReverseDirectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRoute != null && _currentRoute.HasOppositeDirection)
            {
                bool reversed = await BusRouteDatabase.ReverseRouteDirectionAsync(_currentRoute);
                if (reversed)
                {
                    await LoadRouteIntoViewAsync(_currentRoute);
                }
            }
        }

        private void OpenSearchButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SearchPage));
        }

        private async void RefreshEtasButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRoute != null)
            {
                await LoadRouteIntoViewAsync(_currentRoute);
            }
        }

        private void OpenSettingsMenuButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SettingsPage));
        }
    }
}