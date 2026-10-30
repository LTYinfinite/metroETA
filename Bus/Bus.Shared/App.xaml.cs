using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace HKBusWP
{
    public sealed partial class App : Application
    {
        public static Func<bool> CustomBackPressHandler;

        public App()
        {
            this.InitializeComponent();
            this.Suspending += this.OnSuspending;
            HardwareButtons.BackPressed += this.HardwareButtons_BackPressed;
        }

        private void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            if (CustomBackPressHandler != null && CustomBackPressHandler.Invoke())
            {
                e.Handled = true;
                return;
            }

            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame != null && rootFrame.CanGoBack)
            {
                e.Handled = true;
                rootFrame.GoBack();
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.CacheSize = 5;
                rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];
                rootFrame.NavigationFailed += this.OnNavigationFailed;
                rootFrame.Navigated += this.RootFrame_Navigated;

                ApplyNativeTurnstileTransitionToFrame(rootFrame);

                Window.Current.Content = rootFrame;
            }

            ThemeManager.ApplyThemeToRootFrame(ThemeManager.CurrentTheme);
            TileService.Initialize();
            TileService.EnsureStandbyTileIfNoActiveCountdown();

            if (rootFrame.Content == null)
            {
                Type startPage = ThemeManager.IsOobeCompleted ? typeof(MainPage) : typeof(OobePage);
                if (!rootFrame.Navigate(startPage, e.Arguments))
                {
                    throw new Exception("Failed to create initial page");
                }
            }

            Window.Current.Activate();
        }

        private static void ApplyNativeTurnstileTransitionToFrame(Frame frame)
        {
            if (frame == null) return;
            try
            {
                TransitionCollection collection = new TransitionCollection();
                collection.Add(new NavigationThemeTransition());
                frame.ContentTransitions = collection;
            }
            catch (Exception)
            {
            }
        }

        private void RootFrame_Navigated(object sender, NavigationEventArgs e)
        {
            Page page = e.Content as Page;
            if (page == null) return;

            try
            {
                if (page.Transitions == null || page.Transitions.Count == 0)
                {
                    TransitionCollection tc = new TransitionCollection();
                    tc.Add(new NavigationThemeTransition());
                    page.Transitions = tc;
                }

                UIElement target = page.Content as UIElement ?? page;
                double startAngle = (e.NavigationMode == NavigationMode.Back) ? -36.0 : 36.0;
                PlayTurnstileFlipAnimation(target, startAngle, 0.0, 260);
            }
            catch (Exception)
            {
            }
        }

        public static void PlayTurnstileFlipAnimation(UIElement target, double fromAngle, double toAngle, int durationMs)
        {
            if (target == null) return;

            try
            {
                PlaneProjection proj = new PlaneProjection
                {
                    CenterOfRotationX = 0.0,
                    CenterOfRotationY = 0.5,
                    RotationY = fromAngle
                };
                target.Projection = proj;

                DoubleAnimation rotAnim = new DoubleAnimation
                {
                    From = fromAngle,
                    To = toAngle,
                    Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                    EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 4.0 }
                };
                Storyboard.SetTarget(rotAnim, proj);
                Storyboard.SetTargetProperty(rotAnim, "RotationY");

                DoubleAnimation opacityAnim = new DoubleAnimation
                {
                    From = 0.78,
                    To = 1.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(durationMs))
                };
                Storyboard.SetTarget(opacityAnim, target);
                Storyboard.SetTargetProperty(opacityAnim, "Opacity");

                Storyboard sb = new Storyboard();
                sb.Children.Add(rotAnim);
                sb.Children.Add(opacityAnim);
                sb.Begin();
            }
            catch (Exception)
            {
            }
        }

        public static void RestartAppSession()
        {
            Frame newFrame = new Frame();
            newFrame.CacheSize = 5;
            newFrame.Navigated += (s, ev) =>
            {
                Page p = ev.Content as Page;
                if (p != null)
                {
                    UIElement el = p.Content as UIElement ?? p;
                    PlayTurnstileFlipAnimation(el, 36.0, 0.0, 260);
                }
            };
            ApplyNativeTurnstileTransitionToFrame(newFrame);

            Window.Current.Content = newFrame;
            ThemeManager.ApplyThemeToRootFrame(ThemeManager.CurrentTheme);
            TileService.ForceStandbyTilePrompt();

            Type targetPage = ThemeManager.IsOobeCompleted ? typeof(MainPage) : typeof(OobePage);
            newFrame.Navigate(targetPage);
            Window.Current.Activate();
        }

        public static void ForceStandbyStartTilePrompt()
        {
            TileService.ForceStandbyTilePrompt();
        }

        public static void UpdateDefaultPrimaryStartTilePrompt()
        {
            TileService.EnsureStandbyTileIfNoActiveCountdown();
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}