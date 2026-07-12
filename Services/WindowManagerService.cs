using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml.Hosting;
using Windows.Foundation;
using eComBox.Models;
using eComBox.Views;

namespace eComBox.Services
{
    public delegate void ViewClosedHandler(ViewLifetimeControl viewControl, EventArgs e);

    // For instructions on using this service see https://github.com/microsoft/TemplateStudio/blob/main/docs/UWP/features/multiple-views.md
    // More details about showing multiple views at https://docs.microsoft.com/windows/uwp/design/layout/show-multiple-views
    public class WindowManagerService
    {
        public sealed class SecondaryViewNavigationArgs
        {
            public ViewLifetimeControl Lifetime { get; set; }
            public object Parameter { get; set; }
        }
        private static WindowManagerService _current;

        public static WindowManagerService Current => _current ?? (_current = new WindowManagerService());

        // Contains all the opened secondary views.
        public ObservableCollection<ViewLifetimeControl> SecondaryViews { get; } = new ObservableCollection<ViewLifetimeControl>();

        public int MainViewId { get; private set; }

        public CoreDispatcher MainDispatcher { get; private set; }

        public async Task InitializeAsync()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                MainViewId = ApplicationView.GetForCurrentView().Id;
                MainDispatcher = Window.Current.Dispatcher;
            });
        }

        // Displays a view as a standalone
        // You can use the resulting ViewLifeTileControl to interact with the new window.
        public async Task<ViewLifetimeControl> TryShowAsStandaloneAsync(string windowTitle, Type pageType)
        {
            ViewLifetimeControl viewControl = await CreateViewLifetimeControlAsync(windowTitle, pageType);
            SecondaryViews.Add(viewControl);
            viewControl.Released += SecondaryView_Released;
            viewControl.StartViewInUse();
            await ApplicationViewSwitcher.TryShowAsStandaloneAsync(viewControl.Id, ViewSizePreference.Default, ApplicationView.GetForCurrentView().Id, ViewSizePreference.Default);
            viewControl.StopViewInUse();
            return viewControl;
        }

        // Displays a view in the specified view mode
        public async Task<ViewLifetimeControl> TryShowAsViewModeAsync(string windowTitle, Type pageType, ApplicationViewMode viewMode = ApplicationViewMode.Default, object parameter = null)
        {
            ViewLifetimeControl viewControl = await CreateViewLifetimeControlAsync(windowTitle, pageType, parameter);
            SecondaryViews.Add(viewControl);
            viewControl.Released += SecondaryView_Released;
            viewControl.StartViewInUse();
            await ApplicationViewSwitcher.TryShowAsViewModeAsync(viewControl.Id, viewMode);
            viewControl.StopViewInUse();
            return viewControl;
        }

        private async Task<ViewLifetimeControl> CreateViewLifetimeControlAsync(string windowTitle, Type pageType, object parameter = null)
        {
            ViewLifetimeControl viewControl = null;

            await CoreApplication.CreateNewView().Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                viewControl = ViewLifetimeControl.CreateForCurrentView();
                viewControl.Title = windowTitle;
                viewControl.StartViewInUse();
                var frame = new Frame();
                frame.RequestedTheme = ThemeSelectorService.Theme;
                frame.Navigate(pageType, new SecondaryViewNavigationArgs { Lifetime = viewControl, Parameter = parameter });
                Window.Current.Content = frame;
                Window.Current.Activate();
                ApplicationView.GetForCurrentView().Title = viewControl.Title;
            });

            return viewControl;
        }

        private async void SecondaryView_Released(object sender, EventArgs e)
        {
            var view = sender as ViewLifetimeControl;
            if (view == null || MainDispatcher == null) return;
            await MainDispatcher.RunAsync(CoreDispatcherPriority.Low, () => SecondaryViews.Remove(view));
        }

        public bool IsWindowOpen(string windowTitle) => SecondaryViews.Any(v => v.Title == windowTitle);

        public async Task<AppWindow> ShowCountdownWidgetAsync(CountdownCardModel card)
        {
            var appWindow = await AppWindow.TryCreateAsync();
            var page = new FloatingCardPage();
            page.Initialize(card, appWindow);
            ElementCompositionPreview.SetAppWindowContent(appWindow, page);
            appWindow.Title = string.IsNullOrWhiteSpace(card.TaskName) ? "eComBox" : card.TaskName;
            appWindow.TitleBar.ExtendsContentIntoTitleBar = false;
            appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Colors.Transparent;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Windows.UI.Colors.Transparent;
            appWindow.RequestSize(new Size(312, 154));

            var snapping = false;
            appWindow.Changed += (sender, args) =>
            {
                if (!args.DidFrameChange || snapping) return;
                var placement = sender.GetPlacement();
                var region = placement.DisplayRegion;
                if (region == null) return;
                var position = placement.Offset;
                var workOffset = region.WorkAreaOffset;
                var workSize = region.WorkAreaSize;
                const double threshold = 24;
                var x = position.X;
                var y = position.Y;
                var right = workOffset.X + workSize.Width - placement.Size.Width;
                var bottom = workOffset.Y + workSize.Height - placement.Size.Height;
                if (Math.Abs(x - workOffset.X) <= threshold) x = workOffset.X;
                else if (Math.Abs(x - right) <= threshold) x = right;
                if (Math.Abs(y - workOffset.Y) <= threshold) y = workOffset.Y;
                else if (Math.Abs(y - bottom) <= threshold) y = bottom;
                if (Math.Abs(x - position.X) < 0.5 && Math.Abs(y - position.Y) < 0.5) return;
                snapping = true;
                sender.RequestMoveRelativeToDisplayRegion(region, new Point(x - workOffset.X, y - workOffset.Y));
                snapping = false;
            };

            await appWindow.TryShowAsync();
            return appWindow;
        }
    }
}
