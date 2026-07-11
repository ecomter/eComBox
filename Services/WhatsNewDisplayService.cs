using System;
using System.Threading.Tasks;

using eComBox.Views;

using Microsoft.Toolkit.Uwp.Helpers;

using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace eComBox.Services
{
    // For instructions on testing this service see https://github.com/microsoft/TemplateStudio/blob/main/docs/UWP/features/whats-new-prompt.md
    public static class WhatsNewDisplayService
    {
        private static bool shown = false;

        internal static async Task ShowIfAppropriateAsync()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, () =>
                {
                    if (SystemInformation.Instance.IsAppUpdated && !SystemInformation.Instance.IsFirstRun && !shown)
                    {
                        shown = true;
                        NavigationService.Navigate(typeof(WhatsNewPage));
                    }
                });
        }
    }
}
