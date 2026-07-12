using System;
using System.Threading.Tasks;
using eComBox.Helpers;
using eComBox.Models;
using eComBox.Services;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.WindowManagement;

namespace eComBox.Views
{
    public sealed partial class FloatingCardPage : Page
    {
        private CountdownCardModel _cardData;
        private DispatcherTimer _refreshTimer;
        private AppWindow _appWindow;
        private TimeCounterCardViewModel _cardViewModel;
        public UIElement DragHandle => DragRegion;

        private const double ASPECT_RATIO = 16.0 / 9.0;
        private const double DEFAULT_WIDTH = 260.0;

        public FloatingCardPage()
        {
            InitializeComponent();
            IsRightTapEnabled = false;
            Unloaded += (_, __) => _refreshTimer?.Stop();
        }

        public void Initialize(CountdownCardModel cardData, AppWindow appWindow)
        {
            _cardData = cardData;
            _appWindow = appWindow;
            _cardViewModel = TimeCounterCardViewModel.FromModel(cardData);
            DataContext = _cardViewModel;
            ElementCompositionPreview.GetElementVisual(RootGrid).Opacity = 0.98f;
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _refreshTimer.Tick += (_, __) => _cardViewModel?.RefreshDisplay();
            _refreshTimer.Start();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var args = e.Parameter as WindowManagerService.SecondaryViewNavigationArgs;
            if (args?.Parameter is CountdownCardModel cardData)
            {
                _cardData = cardData;
                CreateCardFromData(cardData);
            }

            MakeDesktopWindow();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _refreshTimer?.Stop();
            base.OnNavigatedFrom(e);
        }

        private void CreateCardFromData(CountdownCardModel cardData)
        {
            _cardViewModel = TimeCounterCardViewModel.FromModel(cardData);
            DataContext = _cardViewModel;
        }

        private static string GetCountdownText(DateTime targetDate)
        {
            var days = (targetDate - DateTime.Now.Date).Days;
            if (days > 0)
            {
                return string.Format("DatePage_DaysRemaining".GetLocalized(), days);
            }

            if (days == 0)
            {
                return "DatePage_IsToday".GetLocalized();
            }

            return string.Format("DatePage_DaysElapsed".GetLocalized(), -days);
        }

        private async void MakeDesktopWindow()
        {
            try
            {
                var view = ApplicationView.GetForCurrentView();
                var height = DEFAULT_WIDTH / ASPECT_RATIO;

                RootGrid.Background = new SolidColorBrush(Colors.Transparent);
                view.SetPreferredMinSize(new Size(DEFAULT_WIDTH, height));
                view.TryResizeView(new Size(DEFAULT_WIDTH, height));
                await view.TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay);
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Common_Error".GetLocalized(),
                    Content = string.Format("FloatingCard_CreateFailed".GetLocalized(), ex.Message),
                    CloseButtonText = "Common_OK".GetLocalized()
                };
                await dialog.ShowAsync();
            }
        }

        private async Task CloseFloatingWindow()
        {
            try
            {
                ApplicationView.GetForCurrentView().Consolidated += (s, e) => Window.Current.Close();
                await ApplicationView.GetForCurrentView().TryConsolidateAsync();
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Common_Error".GetLocalized(),
                    Content = string.Format("FloatingCard_CloseFailed".GetLocalized(), ex.Message),
                    CloseButtonText = "Common_OK".GetLocalized()
                };
                await dialog.ShowAsync();
            }
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appWindow != null) await _appWindow.CloseAsync();
            else await CloseFloatingWindow();
        }

        private static string GetCompactCountdownText(DateTime targetDate)
        {
            var days = (targetDate - DateTime.Now.Date).Days;
            if (days > 0) return string.Format("FloatingCard_DaysRemainingCompact".GetLocalized(), days);
            if (days == 0) return "DatePage_IsToday".GetLocalized();
            return string.Format("FloatingCard_DaysElapsedCompact".GetLocalized(), -days);
        }

        private async void CloseMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_appWindow != null) await _appWindow.CloseAsync();
        }
    }
}
