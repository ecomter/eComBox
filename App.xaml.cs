using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using eComBox.Activation;
using eComBox.Helpers;
using eComBox.Models;
using eComBox.Services;
using eComBox.Views;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Store;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
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
            await Helpers.LoggingHelper.InitializeAsync();
            await Helpers.LoggingHelper.LogAsync("应用启动");
            InitializeDefaultSettings();

            if (!args.PrelaunchActivated)
            {
                await ActivationService.ActivateAsync(args);

                // 只有当应用是直接启动的（而不是被激活）才发送通知
                if (args.PreviousExecutionState == ApplicationExecutionState.NotRunning ||
                    args.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    await CheckDateNotificationsAsync();
                }
            }
        }

        private void InitializeDefaultSettings()
        {
            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("AIEnabled"))
            {
                ApplicationData.Current.LocalSettings.Values["AIEnabled"] = false;
            }

            // Remove credentials persisted by older builds. Provider keys must remain server-side.
            ApplicationData.Current.LocalSettings.Values.Remove("AliBairenApiKey");
            ApplicationData.Current.RoamingSettings.Values.Remove("AliBairenApiKey");

            if (string.IsNullOrWhiteSpace(Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride))
            {
                Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = CultureInfo.CurrentUICulture.Name;
            }
        }

        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);

            // 创建延迟的延期对象，确保后台任务有足够时间完成
            var deferral = args.TaskInstance.GetDeferral();

            try
            {
                // 处理后台任务
                await CheckDateNotificationsAsync();
            }
            finally
            {
                deferral.Complete();
            }
        }

        public async Task CheckDateNotificationsAsync()
        {
            try
            {
                var data = await CountdownStorageService.LoadCardsAsync();
                var today = DateTime.Now.Date;
                var notificationCards = new List<CountdownCardModel>();

                foreach (var card in data)
                {
                    var settings = ApplicationData.Current.LocalSettings;
                    var enableNotification = settings.Values.TryGetValue($"Card_{card.Title}_Notification", out object value) && value is bool enabled && enabled;

                    if (enableNotification && card.TargetDate.HasValue && card.TargetDate.Value >= today)
                    {
                        notificationCards.Add(card);
                    }
                }

                if (notificationCards.Count > 0)
                {
                    await Helpers.LoggingHelper.LogAsync($"发送 {notificationCards.Count} 条通知（共 {data.Count} 卡片）");
                    await SendDateNotificationsAsync(notificationCards);
                }
            }
            catch (Exception ex)
            {
                await Helpers.LoggingHelper.LogAsync($"通知检查失败: {ex.Message}");
            }
        }

        private async Task SendDateNotificationsAsync(List<CountdownCardModel> cards)
        {
            var notifier = Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier();

            if (notifier.Setting != Windows.UI.Notifications.NotificationSetting.DisabledForApplication)
            {
                try
                {
                    foreach (var card in cards)
                    {
                        var today = DateTime.Now.Date;
                        var daysLeft = (card.TargetDate.Value - today).Days;
                        var title = string.IsNullOrEmpty(card.TaskName) ? "Notification_DateReminder".GetLocalized() : card.TaskName;
                        var content = string.Format("Notification_DaysUntil".GetLocalized(), title, daysLeft);

                        var xmlContent = $@"
                        <toast>
                            <visual>
                                <binding template='ToastGeneric'>
                                    <text>{title}</text>
                                    <text>{content}</text>
                                </binding>
                            </visual>
                        </toast>";

                        var doc = new Windows.Data.Xml.Dom.XmlDocument();
                        doc.LoadXml(xmlContent);

                        var notification = new Windows.UI.Notifications.ToastNotification(doc);
                        notification.ExpirationTime = DateTime.Now.AddDays(1);

                        notifier.Show(notification);
                        await Helpers.LoggingHelper.LogAsync($"通知已发送: {title}");

                        // 间隔发送，避免一次性显示太多通知
                        await Task.Delay(500);
                    }
                }
                catch (Exception ex)
                {
                    await Helpers.LoggingHelper.LogAsync($"发送日期通知失败: {ex.Message}", "ERROR");

                    Debug.WriteLine($"发送日期通知失败: {ex.Message}");
                }
            }
            else
            {
                await Helpers.LoggingHelper.LogAsync("通知已被系统禁用，无法发送通知", "WARNING");
            }
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            await ActivationService.ActivateAsync(args);


        }

        private async void OnAppUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            await Helpers.LoggingHelper.LogAsync($"未处理异常: {e.Exception.Message}\n{e.Exception.StackTrace}", "ERROR");

            // 标记异常为已处理
            e.Handled = true;


            // 异步显示错误消息

            try
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "App_UnhandledError_Title".GetLocalized(),
                        Content = string.Format("App_UnhandledError_Content".GetLocalized(), e.Exception.Message),
                        CloseButtonText = "Common_OK".GetLocalized()
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
