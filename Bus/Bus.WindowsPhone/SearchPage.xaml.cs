using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace HKBusWP
{
    public sealed partial class SearchPage : Page
    {
        private string _currentQuery = "";
        private string _currentCategory = "All";
        private string _tramBoundFilter = ""; // "" = All, "E" = Eastbound, "W" = Westbound
        private ObservableCollection<NearbyRouteItem> _filteredRoutes;
        private List<NearbyRouteItem> _masterRouteList;
        private Button[] _digitButtons;
        private bool _isReady = false;

        public SearchPage()
        {
            this.InitializeComponent();
            _filteredRoutes = new ObservableCollection<NearbyRouteItem>();
            _masterRouteList = new List<NearbyRouteItem>();
            SearchResultsListView.ItemsSource = _filteredRoutes;

            _digitButtons = new Button[]
            {
                NumBtn0, NumBtn1, NumBtn2, NumBtn3, NumBtn4,
                NumBtn5, NumBtn6, NumBtn7, NumBtn8, NumBtn9
            };

            CategoryPivot.SelectedIndex = 1; // Default to "全部 / all"
            _isReady = true;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            ThemeManager.ApplyThemeToRootFrame(ThemeManager.CurrentTheme);
            ApplyLocalizedAndThemeStyles();

            ResultCountText.Text = ThemeManager.Tr("正在載入全港路線...", "loading routes...");
            await MtrBusService.EnsureOpenDataCsvLoadedAsync();
            _masterRouteList = await BusRouteDatabase.GetOrFetchAllRoutesAsync();
            await ApplyFilterAndKeypadStateAsync();
        }

        private void ApplyLocalizedAndThemeStyles()
        {
            bool en = ThemeManager.IsEnglish;
            PivotRecent.Header = en ? "recent" : "最近搜尋";
            PivotAll.Header = en ? "all" : "全部";
            PivotBus.Header = en ? "bus" : "巴士";
            PivotMinibus.Header = en ? "minibus" : "小巴";
            PivotRailway.Header = en ? "railway" : "鐵路";
            PivotTram.Header = en ? "tram" : "電車";
            PivotFerry.Header = en ? "ferry" : "渡輪";
            SearchPrefixLabel.Text = en ? "route / code: " : "搜尋路線號碼: ";

            bool isLight = (ThemeManager.CurrentTheme == ElementTheme.Light);
            KeypadContainerGrid.Background = isLight
                ? new SolidColorBrush(Color.FromArgb(255, 232, 188, 0))
                : new SolidColorBrush(Color.FromArgb(255, 24, 24, 24));
        }

        private void SearchArea_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            double dx = e.Cumulative.Translation.X;
            double dy = e.Cumulative.Translation.Y;

            if (Math.Abs(dx) > 42 && Math.Abs(dx) > Math.Abs(dy) * 1.3)
            {
                int count = CategoryPivot.Items.Count;
                if (dx < 0)
                {
                    CategoryPivot.SelectedIndex = (CategoryPivot.SelectedIndex + 1) % count;
                }
                else
                {
                    CategoryPivot.SelectedIndex = (CategoryPivot.SelectedIndex - 1 + count) % count;
                }
            }
        }

        private async void CategoryPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isReady) return;

            int idx = CategoryPivot.SelectedIndex;
            if (idx == 0) _currentCategory = "Recent";
            else if (idx == 1) _currentCategory = "All";
            else if (idx == 2) _currentCategory = "Bus";
            else if (idx == 3) _currentCategory = "Minibus";
            else if (idx == 4) _currentCategory = "Railway";
            else if (idx == 5) _currentCategory = "Tram";
            else if (idx == 6) _currentCategory = "Ferry";

            _tramBoundFilter = "";
            await ApplyFilterAndKeypadStateAsync();
        }

        private async void KeypadKey_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.Content != null)
            {
                string key = btn.Content.ToString();
                if (_currentQuery.Length < 6)
                {
                    _currentQuery += key;
                    _tramBoundFilter = "";
                    await ApplyFilterAndKeypadStateAsync();
                }
            }
        }

        private async void BackspaceKey_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_tramBoundFilter))
            {
                _tramBoundFilter = "";
                await ApplyFilterAndKeypadStateAsync();
                return;
            }

            if (_currentQuery.Length > 0)
            {
                _currentQuery = _currentQuery.Substring(0, _currentQuery.Length - 1);
                await ApplyFilterAndKeypadStateAsync();
            }
        }

        private async void ClearKey_Click(object sender, RoutedEventArgs e)
        {
            _currentQuery = string.Empty;
            _tramBoundFilter = string.Empty;
            await ApplyFilterAndKeypadStateAsync();
        }

        private bool MatchesCurrentCategory(NearbyRouteItem route)
        {
            if (route == null) return false;
            if (_currentCategory == "All" || _currentCategory == "Recent") return true;
            if (_currentCategory == "Bus") return route.TransportType == "巴士";
            if (_currentCategory == "Minibus") return route.TransportType == "小巴";
            if (_currentCategory == "Railway") return route.TransportType == "鐵路";
            if (_currentCategory == "Tram") return route.TransportType == "電車";
            if (_currentCategory == "Ferry") return route.TransportType == "渡輪";
            return true;
        }

        private async Task ApplyFilterAndKeypadStateAsync()
        {
            string queryDisplay = string.IsNullOrEmpty(_currentQuery)
                ? ThemeManager.Tr("全部路線", "all routes")
                : _currentQuery;

            if (!string.IsNullOrEmpty(_tramBoundFilter))
            {
                string boundText = (_tramBoundFilter == "E")
                    ? ThemeManager.Tr(" (東行)", " (eastbound)")
                    : ThemeManager.Tr(" (西行)", " (westbound)");
                queryDisplay += boundText;
            }

            SearchQueryText.Text = queryDisplay;
            _filteredRoutes.Clear();

            List<NearbyRouteItem> sourceList = (_currentCategory == "Recent")
                ? BusRouteDatabase.RecentSearchRoutes
                : _masterRouteList;

            List<NearbyRouteItem> exactMatches = new List<NearbyRouteItem>();
            List<NearbyRouteItem> prefixMatches = new List<NearbyRouteItem>();
            HashSet<char> validNextChars = new HashSet<char>();

            int qLen = _currentQuery.Length;

            for (int i = 0; i < sourceList.Count; i++)
            {
                NearbyRouteItem route = sourceList[i];
                if (!MatchesCurrentCategory(route) || string.IsNullOrEmpty(route.RouteNumber))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(_tramBoundFilter) && route.TransportType == "電車")
                {
                    if (route.ServiceType != _tramBoundFilter) continue;
                }

                string rNo = route.RouteNumber.Trim().ToUpperInvariant();

                if (qLen == 0)
                {
                    prefixMatches.Add(route);
                    if (rNo.Length > 0)
                    {
                        validNextChars.Add(rNo[0]);
                    }
                }
                else if (rNo.StartsWith(_currentQuery, StringComparison.OrdinalIgnoreCase))
                {
                    if (rNo.Length == qLen)
                    {
                        exactMatches.Add(route);
                    }
                    else
                    {
                        prefixMatches.Add(route);
                        validNextChars.Add(rNo[qLen]);
                    }
                }
            }

            UpdateDynamicKeypadState(validNextChars);

            List<NearbyRouteItem> combined = new List<NearbyRouteItem>(exactMatches);
            combined.AddRange(prefixMatches);

            int displayLimit = string.IsNullOrEmpty(_currentQuery) ? 140 : combined.Count;
            int count = Math.Min(displayLimit, combined.Count);

            List<NearbyRouteItem> gmbToResolve = new List<NearbyRouteItem>();
            for (int i = 0; i < count; i++)
            {
                _filteredRoutes.Add(combined[i]);
                if (combined[i].TransportType == "小巴" && string.IsNullOrEmpty(combined[i].GmbRouteId) && gmbToResolve.Count < 8)
                {
                    gmbToResolve.Add(combined[i]);
                }
            }

            ResultCountText.Text = ThemeManager.IsEnglish
                ? string.Format("{0} routes", combined.Count)
                : string.Format("共 {0} 條路線", combined.Count);

            foreach (NearbyRouteItem gmb in gmbToResolve)
            {
                await BusRouteDatabase.PopulateGmbRouteDetailsAndLiveEtaAsync(gmb, false);
            }
        }

        private void UpdateDynamicKeypadState(HashSet<char> validNextChars)
        {
            bool isLight = (ThemeManager.CurrentTheme == ElementTheme.Light);

            SolidColorBrush enabledBg = isLight
                ? new SolidColorBrush(Colors.White)
                : new SolidColorBrush(Color.FromArgb(255, 45, 45, 45));
            SolidColorBrush disabledBg = isLight
                ? new SolidColorBrush(Color.FromArgb(90, 210, 170, 0))
                : new SolidColorBrush(Color.FromArgb(90, 35, 35, 35));
            SolidColorBrush enabledFg = isLight
                ? new SolidColorBrush(Colors.Black)
                : new SolidColorBrush(Colors.White);
            SolidColorBrush letterFg = isLight
                ? new SolidColorBrush(Colors.Black)
                : new SolidColorBrush(Color.FromArgb(255, 100, 181, 246));
            SolidColorBrush activeBoundBg = new SolidColorBrush(Color.FromArgb(255, 25, 118, 210));

            for (int d = 0; d <= 9; d++)
            {
                char digitChar = (char)('0' + d);
                bool isValid = validNextChars.Contains(digitChar);
                Button btn = _digitButtons[d];
                btn.IsEnabled = isValid;
                btn.Background = isValid ? enabledBg : disabledBg;
                btn.Foreground = enabledFg;
                btn.Opacity = isValid ? 1.0 : 0.32;
            }

            bool hasQuery = !string.IsNullOrEmpty(_currentQuery) || !string.IsNullOrEmpty(_tramBoundFilter);
            ClearKeyBtn.IsEnabled = hasQuery;
            ClearKeyBtn.Background = enabledBg;
            ClearKeyBtn.Foreground = enabledFg;
            ClearKeyBtn.Opacity = hasQuery ? 1.0 : 0.45;

            BackspaceKeyBtn.IsEnabled = hasQuery;
            BackspaceKeyBtn.Background = enabledBg;
            BackspaceKeyBtn.Foreground = enabledFg;
            BackspaceKeyBtn.Opacity = hasQuery ? 1.0 : 0.45;

            DynamicLettersGrid.Children.Clear();
            DynamicLettersGrid.RowDefinitions.Clear();

            // Requirement #2: When TRAM is typed (or on the Tram category), show Eastbound & Westbound filter buttons on the right keypad!
            if (string.Equals(_currentQuery, "TRAM", StringComparison.OrdinalIgnoreCase) ||
                (_currentCategory == "Tram" && string.IsNullOrEmpty(_currentQuery)))
            {
                DynamicLettersGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                DynamicLettersGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Button eastBtn = new Button
                {
                    Content = ThemeManager.Tr("東行 (Eastbound)", "eastbound"),
                    Height = 58,
                    Margin = new Thickness(3, 4, 3, 4),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    FontSize = 15,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Background = (_tramBoundFilter == "E") ? activeBoundBg : enabledBg,
                    Foreground = (_tramBoundFilter == "E") ? new SolidColorBrush(Colors.White) : letterFg,
                    BorderThickness = new Thickness(0)
                };
                eastBtn.Click += async (s, e) =>
                {
                    _tramBoundFilter = (_tramBoundFilter == "E") ? "" : "E";
                    await ApplyFilterAndKeypadStateAsync();
                };
                Grid.SetRow(eastBtn, 0);
                Grid.SetColumn(eastBtn, 0);
                Grid.SetColumnSpan(eastBtn, 2);
                DynamicLettersGrid.Children.Add(eastBtn);

                Button westBtn = new Button
                {
                    Content = ThemeManager.Tr("西行 (Westbound)", "westbound"),
                    Height = 58,
                    Margin = new Thickness(3, 4, 3, 4),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    FontSize = 15,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Background = (_tramBoundFilter == "W") ? activeBoundBg : enabledBg,
                    Foreground = (_tramBoundFilter == "W") ? new SolidColorBrush(Colors.White) : letterFg,
                    BorderThickness = new Thickness(0)
                };
                westBtn.Click += async (s, e) =>
                {
                    _tramBoundFilter = (_tramBoundFilter == "W") ? "" : "W";
                    await ApplyFilterAndKeypadStateAsync();
                };
                Grid.SetRow(westBtn, 1);
                Grid.SetColumn(westBtn, 0);
                Grid.SetColumnSpan(westBtn, 2);
                DynamicLettersGrid.Children.Add(westBtn);
                return;
            }

            List<char> validLetters = new List<char>();
            for (char c = 'A'; c <= 'Z'; c++)
            {
                if (validNextChars.Contains(c))
                {
                    validLetters.Add(c);
                }
            }

            int rowCount = (validLetters.Count + 1) / 2;
            for (int r = 0; r < rowCount; r++)
            {
                DynamicLettersGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            for (int i = 0; i < validLetters.Count; i++)
            {
                int row = i / 2;
                int col = i % 2;

                Button letterBtn = new Button
                {
                    Content = validLetters[i].ToString(),
                    Height = 50,
                    MinWidth = 58,
                    MinHeight = 46,
                    Margin = new Thickness(3),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    FontSize = 22,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Background = enabledBg,
                    Foreground = letterFg,
                    BorderThickness = new Thickness(0)
                };
                letterBtn.Click += KeypadKey_Click;

                Grid.SetRow(letterBtn, row);
                Grid.SetColumn(letterBtn, col);
                DynamicLettersGrid.Children.Add(letterBtn);
            }
        }

        private async void SearchResultsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            NearbyRouteItem selectedRoute = e.ClickedItem as NearbyRouteItem;
            if (selectedRoute != null)
            {
                if (selectedRoute.TransportType == "小巴" && string.IsNullOrEmpty(selectedRoute.GmbRouteId))
                {
                    await BusRouteDatabase.PopulateGmbRouteDetailsAndLiveEtaAsync(selectedRoute, false);
                }

                BusRouteDatabase.RecordRecentSearch(selectedRoute);
                BusRouteDatabase.CurrentSelectedRoute = selectedRoute;
                this.Frame.Navigate(typeof(RouteDetailPage));
            }
        }
    }
}