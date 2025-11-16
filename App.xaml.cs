using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using eComBox.Activation;
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

            
                ApplicationData.Current.LocalSettings.Values["AIEnabledKey"] = false;
            
        }
     

        private void InitializeDefaultSettings()
        {
            // ConfigurationService 已经内置了默认值处理，不需要额外设置
            // 但如果需要，可以在这里检查特定配置并设置

            // 例如，确保 AIEnabled 设置存在
            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("AIEnabled"))
            {
                ApplicationData.Current.LocalSettings.Values["AIEnabled"] = false; // 默认关闭AI功能
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
                await Helpers.LoggingHelper.LogAsync("开始检查日期通知...");

                // 加载所有卡片数据
                var data = await DataStorage.LoadDataAsync();
                await Helpers.LoggingHelper.LogAsync($"已加载 {data.Count} 个卡片");

                // 今天的日期
                var today = DateTime.Now.Date;

                // 提取需要发送通知的卡片
                var notificationCards = new List<DatePage.DataBlockModel>();

                foreach (var card in data)
                {
                    // 检查是否启用了通知且日期在未来
                    var settings = ApplicationData.Current.LocalSettings;
                    bool enableNotification = false;

                    if (settings.Values.TryGetValue($"Card_{card.Title}_Notification", out object value) && value is bool)
                    {
                        enableNotification = (bool)value;
                    }

                    await Helpers.LoggingHelper.LogAsync($"卡片 '{card.Title}': 通知已{(enableNotification ? "启用" : "禁用")}, " +
                                      $"目标日期: {(card.TargetDate.HasValue ? card.TargetDate.Value.ToString("yyyy-MM-dd") : "未设置")}");

                    // 只对启用了通知且日期在未来的卡片发送通知
                    if (enableNotification && card.TargetDate.HasValue && card.TargetDate.Value >= today)
                    {
                        notificationCards.Add(card);
                    }
                }

                // 如果有需要通知的卡片，发送通知
                if (notificationCards.Count > 0)
                {
                    await Helpers.LoggingHelper.LogAsync($"找到 {notificationCards.Count} 个需要通知的卡片");
                    // 使用ToastNotifier发送通知
                    await SendDateNotificationsAsync(notificationCards);
                }
                else
                {
                    await Helpers.LoggingHelper.LogAsync("没有需要通知的卡片");
                }
            }
            catch (Exception ex)
            {
                await Helpers.LoggingHelper.LogAsync($"检查日期通知时出错: {ex.Message}", "ERROR");
                Debug.WriteLine($"检查日期通知时出错: {ex.Message}");
            }
        }
        private async Task SendDateNotificationsAsync(List<DatePage.DataBlockModel> cards)
        {
            // 确保应用有权发送通知
            var notifier = Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier();
            await Helpers.LoggingHelper.LogAsync($"通知权限状态: {notifier.Setting}");

            if (notifier.Setting != Windows.UI.Notifications.NotificationSetting.DisabledForApplication)
            {
                try
                {
                    // 为每个卡片创建一个通知
                    foreach (var card in cards)
                    {
                        // 计算剩余天数
                        var today = DateTime.Now.Date;
                        var daysLeft = (card.TargetDate.Value - today).Days;

                        // 创建通知内容
                        var title = string.IsNullOrEmpty(card.TaskName) ? "日期提醒" : card.TaskName;
                        var content = $"距离 {title} 还有 {daysLeft} 天";
                        await Helpers.LoggingHelper.LogAsync($"准备发送通知: 标题='{title}', 内容='{content}'");

                        // 创建通知
                        var toastContent = new Windows.UI.Notifications.ToastNotification(
                            new Windows.Data.Xml.Dom.XmlDocument());

                        // 构建通知XML
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

                        // 发送通知
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
