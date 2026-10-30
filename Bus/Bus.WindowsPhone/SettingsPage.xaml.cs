using System;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace HKBusWP
{
    public sealed partial class SettingsPage : Page
    {
        private bool _isInitializing = true;
        private int _iconTapCount = 0;

        public SettingsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _isInitializing = true;
            ThemeManager.ApplyThemeToRootFrame(ThemeManager.CurrentTheme);

            App.CustomBackPressHandler = HandleCreditsBackPress;

            LangComboBox.SelectedIndex = ThemeManager.IsEnglish ? 1 : 0;
            RestartAppPanel.Visibility = Visibility.Collapsed;

            if (ThemeManager.CurrentTheme == ElementTheme.Light)
            {
                LightThemeRadio.IsChecked = true;
            }
            else
            {
                DarkThemeRadio.IsChecked = true;
            }

            HideStopCodeCheckBox.IsChecked = ThemeManager.HideStopCode;
            RadiusSlider.Value = ThemeManager.SearchRadiusMeters;
            StartTabComboBox.SelectedIndex = Math.Min(StartTabComboBox.Items.Count - 1, ThemeManager.DefaultStartTabIndex);

            ApplyLocalizedText();
            _isInitializing = false;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            App.CustomBackPressHandler = null;
        }

        private bool HandleCreditsBackPress()
        {
            if (CreditsOverlay.Visibility == Visibility.Visible)
            {
                CreditsOverlay.Visibility = Visibility.Collapsed;
                return true;
            }
            return false;
        }

        private void ApplyLocalizedText()
        {
            bool en = ThemeManager.IsEnglish;
            int radius = ThemeManager.SearchRadiusMeters;

            SettingsPivot.Title = en ? "metroeta" : "METROETA 香港交通預報";
            TabGeneralSettings.Header = en ? "settings" : "設定";
            TabAbout.Header = en ? "about" : "關於";

            LangSectionHeader.Text = en ? "language" : "介面語言";
            RestartPromptText.Text = en
                ? "language changed. tap below to restart the app and apply the new language:"
                : "已更改語言設定。請點擊下方按鈕重新啟動應用程式以套用新語言：";
            RestartAppButton.Content = en ? "🔄 restart app now" : "🔄 立即重新啟動應用程式";

            StartTabSectionHeader.Text = en ? "default starting page" : "預設啟動頁面";
            if (StartTabComboBox != null && StartTabComboBox.Items.Count == 7)
            {
                ((ComboBoxItem)StartTabComboBox.Items[0]).Content = en ? "favourites" : "收藏";
                ((ComboBoxItem)StartTabComboBox.Items[1]).Content = en ? "bus" : "巴士";
                ((ComboBoxItem)StartTabComboBox.Items[2]).Content = en ? "minibus" : "小巴";
                ((ComboBoxItem)StartTabComboBox.Items[3]).Content = en ? "railway" : "鐵路";
                ((ComboBoxItem)StartTabComboBox.Items[4]).Content = en ? "tram" : "電車";
                ((ComboBoxItem)StartTabComboBox.Items[5]).Content = en ? "ferry" : "渡輪";
                ((ComboBoxItem)StartTabComboBox.Items[6]).Content = en ? "traffic news" : "交通消息";
            }

            StopCodeSectionHeader.Text = en ? "stop name format" : "車站名稱顯示格式";
            HideStopCodeCheckBox.Content = en
                ? "hide stop code behind stop name (e.g. YT615)"
                : "隱藏車站名稱後的代碼 (如 YT615、KC400)";

            RadiusSectionHeader.Text = en ? "nearby search radius (50m - 500m)" : "附近路線搜尋半徑 (50m - 500m)";
            RadiusValueText.Text = en
                ? string.Format("current radius: {0}m", radius)
                : string.Format("目前搜尋半徑: {0} 米", radius);

            ThemeSectionHeader.Text = en ? "theme" : "外觀主題";
            LightThemeRadio.Content = en ? "light theme (syncs top status bar)" : "淺色主題 (頂部狀態列同步變白)";
            DarkThemeRadio.Content = en ? "dark theme (classic windows phone)" : "深色主題 (Windows Phone 經典)";

            OobeSectionHeader.Text = en ? "first-time setup (oobe)" : "初次體驗設定 (OOBE)";
            ResetOobeButton.Content = en ? "reset oobe wizard" : "重設初次體驗 (Reset OOBE)";

            CacheSectionHeader.Text = en ? "local route & stop cache" : "實時資料庫快取";
            ClearApiCacheButton.Content = en ? "clear cache & reload from api" : "清除路線及車站快取並重新下載";

            // Requirement #6: Version 0.1 without WP8.1/W10M native text
            AboutVersionText.Text = en ? "version 0.1" : "版本 0.1";
            EasterEggText.Text = "We are 0.001% !!!";

            AboutIntroHeader.Text = en ? "overview" : "應用程式簡介";
            AboutIntroBody.Text = en
                ? "supports kmb, long win, citybus, new lantao bus, mtr bus (k routes), green minibus, all 98 mtr stations, light rail, hk tramways, ferries, section fares, transport department traffic news, hko live weather, and crisp interactive maps."
                : "提供全港九巴、龍運、城巴、新大嶼山巴士、港鐵巴士(K線)、專線小巴、港鐵全線、輕鐵、香港電車及渡輪實時到站預報、分段收費標示、98站港鐵車費計算、運輸署特別交通消息、香港天文台實時天氣及互動式地圖。";
            OpenCreditsPageButton.Content = en ? "credits & attribution" : "鳴謝及資料來源版權";

            CreditsTitleText.Text = en ? "credits & attribution" : "鳴謝及資料來源版權";
            Credit1Title.Text = en ? "1. map tiles & labels — hk lands department" : "1. 地圖圖磚及中文地名標籤 — 香港地政總署";
            Credit1Desc.Text = en
                ? "street map tiles and traditional chinese / english map labels are provided by the hong kong lands department (csdi geodata store api)."
                : "街道底圖及繁體中文/英文地名標籤由香港特別行政區政府地政總署 (空間數據共享平台 CSDI GeoData Store API) 提供。";
            Credit1Btn.Content = en ? "open lands department map disclaimer ↗" : "開啟地政總署地圖版權及免責聲明 ↗";

            Credit2Title.Text = en ? "2. road routing geometry — openstreetmap & osrm" : "2. 道路軌跡繪製數據 — OpenStreetMap 及 OSRM";
            Credit2Desc.Text = en
                ? "bus, minibus, and tramway road geometry paths are powered by © openstreetmap contributors and project osrm under the open database license (odbl)."
                : "巴士、小巴及電車路線沿真實道路彎位繪製之幾何數據由 © OpenStreetMap 貢獻者及 OSRM 提供（依 ODbL 開放資料庫授權條款使用）。";
            Credit2Btn.Content = en ? "open © openstreetmap copyright page ↗" : "開啟 © OpenStreetMap 版權聲明網頁 ↗";

            Credit3Title.Text = en ? "3. public transport & traffic apis — data.gov.hk & td" : "3. 實時公共交通及封路數據 — 資料一線通及運輸署";
            Credit3Desc.Text = en
                ? "real-time arrival data for kmb, long win, citybus, new lantao bus, mtr bus, green minibus, mtr, light rail, and special traffic news are provided by data.gov.hk and the transport department."
                : "九巴、龍運、城巴、嶼巴、港鐵巴士、專線小巴、港鐵、輕鐵實時到站數據及特別交通消息由「資料一線通 (DATA.GOV.HK)」及香港運輸署提供。";
            Credit3Btn.Content = en ? "open data.gov.hk terms & conditions ↗" : "開啟 DATA.GOV.HK 條款及細則 ↗";

            Credit4Title.Text = en ? "4. weather & warning icons — hong kong observatory" : "4. 實時氣溫及天氣警告圖示 — 香港天文台";
            Credit4Desc.Text = en
                ? "live regional temperature and active weather warning signal icons are provided by the hong kong observatory open data api."
                : "實時氣溫及生效天氣警告信號圖示由香港天文台開放數據 API 提供。";
            Credit4Btn.Content = en ? "open hko intellectual property notice ↗" : "開啟香港天文台知識產權公告 ↗";
        }

        private void LangComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || LangComboBox == null) return;

            bool newLangEnglish = (LangComboBox.SelectedIndex == 1);
            if (ThemeManager.IsEnglish != newLangEnglish)
            {
                ThemeManager.IsEnglish = newLangEnglish;
                ApplyLocalizedText();
                RestartAppPanel.Visibility = Visibility.Visible;
            }
        }

        private void RestartAppButton_Click(object sender, RoutedEventArgs e)
        {
            App.RestartAppSession();
        }

        private void StartTabComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || StartTabComboBox == null) return;
            if (StartTabComboBox.SelectedIndex >= 0)
            {
                ThemeManager.DefaultStartTabIndex = StartTabComboBox.SelectedIndex;
            }
        }

        private void HideStopCodeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || HideStopCodeCheckBox == null) return;
            ThemeManager.HideStopCode = (HideStopCodeCheckBox.IsChecked == true);
        }

        private void RadiusSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isInitializing || RadiusValueText == null) return;

            int radius = (int)Math.Round(e.NewValue);
            ThemeManager.SearchRadiusMeters = radius;
            RadiusValueText.Text = ThemeManager.IsEnglish
                ? string.Format("current radius: {0}m", radius)
                : string.Format("目前搜尋半徑: {0} 米", radius);
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || LightThemeRadio == null || DarkThemeRadio == null) return;

            if (LightThemeRadio.IsChecked == true)
            {
                ThemeManager.CurrentTheme = ElementTheme.Light;
            }
            else if (DarkThemeRadio.IsChecked == true)
            {
                ThemeManager.CurrentTheme = ElementTheme.Dark;
            }
        }

        private void ResetOobeButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.IsOobeCompleted = false;
            this.Frame.Navigate(typeof(OobePage));
            this.Frame.BackStack.Clear();
        }

        private void ClearApiCacheButton_Click(object sender, RoutedEventArgs e)
        {
            BusRouteDatabase.ClearCache();
            MtrBusService.ClearCache();
            CacheStatusText.Text = ThemeManager.Tr(
                "已清除所有快取，返回主頁或搜尋頁時將重新下載最新資料。",
                "cache cleared. routes will reload from live apis.");
        }

        // Requirement #6: No countdown hint during clicks; unlocks "We are 0.001% !!!" on 5th tap
        private void AppIconTemplateBox_Tapped(object sender, TappedRoutedEventArgs e)
        {
            _iconTapCount++;
            if (_iconTapCount >= 5)
            {
                EasterEggText.Text = "We are 0.001% !!!";
                EasterEggBanner.Visibility = Visibility.Visible;
            }
        }

        private void OpenCreditsPageButton_Click(object sender, RoutedEventArgs e)
        {
            CreditsOverlay.Visibility = Visibility.Visible;
            App.PlayTurnstileFlipAnimation(CreditsOverlay, 82.0, 0.0, 340);
        }

        private void CloseCreditsOverlay_Click(object sender, RoutedEventArgs e)
        {
            CreditsOverlay.Visibility = Visibility.Collapsed;
        }

        private async void Credit1Btn_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://api.portal.hkmapservice.gov.hk/disclaimer"));
        }

        private async void Credit2Btn_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.openstreetmap.org/copyright"));
        }

        private async void Credit3Btn_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://data.gov.hk/tc/terms-and-conditions"));
        }

        private async void Credit4Btn_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.hko.gov.hk/tc/publica/ipr.htm"));
        }
    }
}