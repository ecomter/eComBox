using System;
using System.Threading.Tasks;
using eComBox.Helpers;
using eComBox.Models;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace eComBox.Views
{
    public sealed partial class FloatingCardPage : Page
    {
        private CountdownCardModel _cardData;

        private const double ASPECT_RATIO = 16.0 / 9.0;
        private const double DEFAULT_WIDTH = 260.0;

        public FloatingCardPage()
        {
            InitializeComponent();
            IsRightTapEnabled = false;
            Loaded += FloatingCardPage_Loaded;
        }

        private void FloatingCardPage_Loaded(object sender, RoutedEventArgs e)
        {
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = Colors.Transparent;

            ElementCompositionPreview.GetElementVisual(RootGrid).Opacity = 0.95f;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is CountdownCardModel cardData)
            {
                _cardData = cardData;
                CreateCardFromData(cardData);
            }

            MakeDesktopWindow();
        }

        private void CreateCardFromData(CountdownCardModel cardData)
        {
            var targetDate = cardData.TargetDate?.ToString("yyyy-MM-dd") ?? "FloatingCard_NoDate".GetLocalized();
            var countdown = cardData.TargetDate.HasValue
                ? GetCountdownText(cardData.TargetDate.Value.Date)
                : "DatePage_WaitingForDate".GetLocalized();

            var card = new Border
            {
                Margin = new Thickness(8),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                Background = (Brush)Application.Current.Resources["AcrylicBackgroundFillColorDefaultBrush"],
                Padding = new Thickness(12),
                Child = new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = cardData.TaskName ?? "FloatingCard_Untitled".GetLocalized(), Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], TextWrapping = TextWrapping.WrapWholeWords },
                        new TextBlock { Text = targetDate, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] },
                        new TextBlock { Text = countdown, Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"] }
                    }
                }
            };

            CardContainer.Children.Clear();
            CardContainer.Children.Add(card);
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
                view.TryResizeView(new Size(DEFAULT_WIDTH, height));
                await view.TryEnterViewModeAsync(ApplicationViewMode.Default);
                view.SetPreferredMinSize(new Size(DEFAULT_WIDTH, height));
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
            await CloseFloatingWindow();
        }
    }
}
