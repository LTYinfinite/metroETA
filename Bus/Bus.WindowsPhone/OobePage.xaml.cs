using System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;

namespace HKBusWP
{
    public sealed partial class OobePage : Page
    {
        private bool _isInitializing = true;

        public OobePage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _isInitializing = true;

            LangComboBox.SelectedIndex = ThemeManager.IsEnglish ? 1 : 0;

            if (ThemeManager.CurrentTheme == ElementTheme.Light)
            {
                LightRadio.IsChecked = true;
            }
            else
            {
                DarkRadio.IsChecked = true;
            }

            OobeRadiusSlider.Value = ThemeManager.SearchRadiusMeters;
            StartTabComboBox.SelectedIndex = Math.Min(StartTabComboBox.Items.Count - 1, ThemeManager.DefaultStartTabIndex);
            OobeHideStopCodeCheckBox.IsChecked = ThemeManager.HideStopCode;

            ApplyLocalizedText();
            _isInitializing = false;
        }

        private void ApplyLocalizedText()
        {
            bool en = ThemeManager.IsEnglish;
            int radius = ThemeManager.SearchRadiusMeters;

            OobeTitleText.Text = en ? "welcome to MetroETA" : "歡迎使用 MetroETA";
            OobeSubTitleText.Text = en
                ? "initial setup (you can change these anytime in settings):"
                : "初次啟動設定（稍後可於設定中隨時更改）：";

            LangHeaderText.Text = en ? "1. language" : "1. 介面語言";
            ThemeHeaderText.Text = en ? "2. theme" : "2. 外觀主題";
            LightRadio.Content = en ? "light theme" : "淺色主題 (白底黑字)";
            DarkRadio.Content = en ? "dark theme (classic metro)" : "深色主題 (黑底白字 / 省電)";

            RadiusHeaderText.Text = en ? "3. nearby route search radius (50m - 500m)" : "3. 附近路線搜尋半徑 (50m - 500m)";
            OobeRadiusValueText.Text = en
                ? string.Format("search radius: {0}m", radius)
                : string.Format("目前搜尋半徑: {0} 米", radius);

            StartTabHeaderText.Text = en ? "4. default starting page" : "4. 預設啟動頁面";

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

            StopCodeHeaderText.Text = en ? "5. stop name format" : "5. 車站名稱顯示格式";
            OobeHideStopCodeCheckBox.Content = en
                ? "hide stop code behind stop name (e.g. YT615)"
                : "隱藏車站名稱後的代碼 (如 YT615、KC400)";

            StartButton.Content = en ? "get started" : "開始使用";
        }

        private void LangComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || LangComboBox == null) return;

            ThemeManager.IsEnglish = (LangComboBox.SelectedIndex == 1);
            ApplyLocalizedText();
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || LightRadio == null || DarkRadio == null) return;

            if (LightRadio.IsChecked == true)
            {
                ThemeManager.CurrentTheme = ElementTheme.Light;
            }
            else if (DarkRadio.IsChecked == true)
            {
                ThemeManager.CurrentTheme = ElementTheme.Dark;
            }
        }

        private void OobeRadiusSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isInitializing || OobeRadiusValueText == null) return;

            int radius = (int)Math.Round(e.NewValue);
            ThemeManager.SearchRadiusMeters = radius;
            OobeRadiusValueText.Text = ThemeManager.IsEnglish
                ? string.Format("search radius: {0}m", radius)
                : string.Format("目前搜尋半徑: {0} 米", radius);
        }

        private void StartTabComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || StartTabComboBox == null) return;

            if (StartTabComboBox.SelectedIndex >= 0)
            {
                ThemeManager.DefaultStartTabIndex = StartTabComboBox.SelectedIndex;
            }
        }

        private void OobeHideStopCodeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || OobeHideStopCodeCheckBox == null) return;
            ThemeManager.HideStopCode = (OobeHideStopCodeCheckBox.IsChecked == true);
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.IsOobeCompleted = true;
            App.ForceStandbyStartTilePrompt();

            try
            {
                bool en = ThemeManager.IsEnglish;
                string msg = en
                    ? "Tip: Please pin MetroETA to your Start Screen and resize it to a Wide (Big) Tile so you can view live bus countdowns directly on your Start Screen!"
                    : "提示：請將 MetroETA 釘選至「開始」畫面並調整為「寬廣大動態磚 (Big Tile)」，即可直接在開始畫面查看實時班次倒數！";
                string title = en ? "Pin Big Tile to Start Screen" : "釘選大動態磚至開始畫面";

                MessageDialog dialog = new MessageDialog(msg, title);
                await dialog.ShowAsync();
            }
            catch (Exception)
            {
            }

            this.Frame.Navigate(typeof(MainPage));
            this.Frame.BackStack.Clear();
        }
    }
}