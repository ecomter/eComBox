using System;
using System.Threading.Tasks;

using eComBox.Views;

using Microsoft.Toolkit.Uwp.Helpers;

using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace eComBox.Services
{
    public static class FirstRunDisplayService
    {
        private static bool shown = false;

        internal static async Task ShowIfAppropriateAsync()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, () =>
                {
                    if (SystemInformation.Instance.IsFirstRun && !shown)
                    {
                        shown = true;
                        NavigationService.Navigate(typeof(FirstRunPage));
                    }
                });
        }
    }
}
