using System;

using eComBox.Services;

using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Store;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace eComBox
{
    public sealed partial class App : Application
    {
        private Lazy<ActivationService> _activationService;

        private ActivationService ActivationService
        {
            get { return _activationService.Value; }
        }

        public App()
        {
            InitializeComponent();
            UnhandledException += OnAppUnhandledException;

            // Deferred execution until used. Check https://docs.microsoft.com/dotnet/api/system.lazy-1 for further info on Lazy<T> class.
            _activationService = new Lazy<ActivationService>(CreateActivationService);
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            if (!args.PrelaunchActivated)
            {
                await ActivationService.ActivateAsync(args);
            }
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            await ActivationService.ActivateAsync(args);
        }

        private async void OnAppUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // 标记异常为已处理
            e.Handled = true;

            // 记录详细的异常信息
            

            // 异步显示错误消息
            
                try
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "应用遇到了问题",
                        Content = $"很抱歉，应用遇到了意外错误。请重试您的操作，如果问题持续存在，请重启应用。错误信息：{e.Exception.Message}",
                        CloseButtonText = "确定"
                    };

                    await dialog.ShowAsync();
                }
                catch
                {
                    // 忽略显示对话框时的错误
                }
            
        }

        private ActivationService CreateActivationService()
        {
            return new ActivationService(this, typeof(Views.HomePage), new Lazy<UIElement>(CreateShell));
        }

        private UIElement CreateShell()
        {
            return new Views.ShellPage();
        }
    }
}
