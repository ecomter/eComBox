using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using eComBox.Helpers;
using eComBox.Services;
using Microsoft.UI.Xaml.Controls;

using Microsoft.Web.WebView2.Core;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Resources.Core;
using Windows.Globalization;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace eComBox.Views
{
    // TODO: Add other settings as necessary. For help see https://github.com/microsoft/TemplateStudio/blob/main/docs/UWP/pages/settings-codebehind.md
    // TODO: Change the URL for your privacy policy in the Resource File, currently set to https://YourPrivacyUrlGoesHere
    public sealed partial class SettingsPage : Page, INotifyPropertyChanged
    {
        private const string SelectedUrlKey = "SelectedUrl";
        private const string SelectedUrlContent = "SelectedUrlContent";
        private const string DefaultUrl = "https://doc.ecomter.site/baidu?cache=false";
        private const string HotListEnabledKey = "HotListEnabled";
        private bool _initialHotListToggleState;
        private bool _isInitializingLanguageComboBox = false;

        private ElementTheme _elementTheme = ThemeSelectorService.Theme;

        public ElementTheme ElementTheme
        {
            get { return _elementTheme; }
            set { Set(ref _elementTheme, value); }
        }
        private bool _isStartupEnabled;

        public bool IsStartupEnabled
        {
            get => _isStartupEnabled;
            set
            {
                if (_isStartupEnabled != value)
                {
                    _isStartupEnabled = value;
                    OnPropertyChanged(nameof(IsStartupEnabled));
                }
            }
        }

        private bool _cardEnable = false;

        public bool CardEnable
        {
            get { return _cardEnable; }
            set { Set(ref _cardEnable, value); }
        }



        private string _versionDescription;

        public string VersionDescription
        {
            get { return _versionDescription; }
            set { Set(ref _versionDescription, value); }
        }
        private string _appName;
        public string AppName
        {
            get { return _appName; }
            set { Set(ref _appName, value); }
        }
        private const string AIEnabledKey = "AIEnabled";
        private bool _initialAIToggleState;
        private bool _isAIPremium = false;
        private int _remainingUsageCount = 0;

        private async void StartupToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (StartupToggle.IsOn)
            {
                // 启用开机自启动
                var state = await StartupService.EnableStartupAsync();

                if (state == StartupTaskState.Enabled)
                {
                    IsStartupEnabled = true;
                    await StartupService.RegisterBackgroundNotificationTask();
                }
                else
                {
                    // 如果用户拒绝了权限，更新UI状态
                    StartupToggle.IsOn = false;
                    IsStartupEnabled = false;
                    await errorStartDialog.ShowAsync();
                }
            }
            else
            {
                // 关闭开机自启动
                StartupService.DisableStartup();
                IsStartupEnabled = false;
            }
        }

        private void InitializeLanguageComboBox()
        {
            _isInitializingLanguageComboBox = true;

            // 获取当前语言设置
            string currentLanguage = ApplicationLanguages.PrimaryLanguageOverride;

            // 如果当前没有语言重写设置（首次运行应用程序时）
            if (string.IsNullOrEmpty(currentLanguage))
            {
                // 获取系统当前语言
                currentLanguage = ApplicationLanguages.Languages.FirstOrDefault() ?? "en-US";

                // 标准化语言代码（处理可能的方言差异）
                if (currentLanguage.StartsWith("zh"))
                {
                    // 所有中文方言都默认使用简体中文
                    currentLanguage = "zh-Hans-CN";
                }
                else if (currentLanguage.StartsWith("en"))
                {
                    // 所有英文方言都默认使用美式英语
                    currentLanguage = "en-US";
                }

                // 设置应用的默认语言
                ApplicationLanguages.PrimaryLanguageOverride = currentLanguage;
            }

            // 在ComboBox中选择对应的语言
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag.ToString() == currentLanguage)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }

            _isInitializingLanguageComboBox = false;
        }

        public SettingsPage()
        {
            InitializeComponent();
            InitializeLanguageComboBox();
            LoadSelectedUrl();
            LoadHotListToggleState();
            LoadTranslatorSettings();
            LoadAIToggleState();
        }

        private void LoadAIToggleState()
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(AIEnabledKey, out object aiEnabled))
            {
                _initialAIToggleState = false;
                AIToggleSwitch.IsOn = false;
            }
            else
            {
                // 默认不启用AI功能
                _initialAIToggleState = false;
                AIToggleSwitch.IsOn = false;
                ApplicationData.Current.LocalSettings.Values[AIEnabledKey] = false;
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await InitializeAsync();
            base.OnNavigatedTo(e);
            await LoadAIUsageInfoAsync();
            // 检查是否已开启开机启动
            IsStartupEnabled = await StartupService.IsStartupEnabled();
        }
        private async Task LoadAIUsageInfoAsync()
        {
            try
            {
                // 检查是否购买了高级版
                _isAIPremium = await AIUsageService.IsAIPremiumPurchasedAsync();

                // 获取剩余使用次数
                _remainingUsageCount = await AIUsageService.GetRemainingUsageCountAsync();

                // 更新 UI
                UpdateAIUsageUI();
            }
            catch (Exception ex)
            {
                // 记录错误但不中断用户体验
                System.Diagnostics.Debug.WriteLine($"加载 AI 使用情况时出错: {ex.Message}");
            }
        }

        // 新增的方法：更新 AI 使用情况 UI
        private void UpdateAIUsageUI()
        {
            if (_isAIPremium)
            {
                // 高级版用户 UI
                PremiumBadge.Visibility = Visibility.Visible;
                UsageStatsPanel.Visibility = Visibility.Collapsed;
                PurchaseButton.Visibility = Visibility.Collapsed;

                // 更新描述
                UsageLimitDescription.Text = "Settings_AIUsage_PremiumInfo".GetLocalized();
            }
            else
            {
                // 免费版用户 UI
                PremiumBadge.Visibility = Visibility.Collapsed;
                UsageStatsPanel.Visibility = Visibility.Visible;
                PurchaseButton.Visibility = Visibility.Visible;

                // 更新剩余次数显示
                RemainingCountTextBlock.Text = $"{_remainingUsageCount}/{AIUsageService.FREE_USAGE_LIMIT_PER_DAY}";

                // 更新进度条 - 批量更新减少布局计算
                double max = AIUsageService.FREE_USAGE_LIMIT_PER_DAY;
                double value = max - _remainingUsageCount;

                UsageProgressBar.Maximum = max;
                UsageProgressBar.Value = value;

                // 根据剩余次数设置进度条颜色
                SolidColorBrush progressBrush;
                if (_remainingUsageCount <= 5)
                {
                    progressBrush = new SolidColorBrush(Colors.Red);
                }
                else if (_remainingUsageCount <= 10)
                {
                    progressBrush = new SolidColorBrush(Colors.Orange);
                }
                else
                {
                    progressBrush = new SolidColorBrush(Colors.Green);
                }

                // 仅在颜色变化时更新
                if (!(UsageProgressBar.Foreground is SolidColorBrush currentBrush) ||
                    currentBrush.Color != progressBrush.Color)
                {
                    UsageProgressBar.Foreground = progressBrush;
                }
            }
        }
        // 新增的方法：购买按钮点击事件
        private async void PurchaseButton_Click(object sender, RoutedEventArgs e)
        {
            // 禁用按钮并显示进度环
            PurchaseButton.IsEnabled = false;
            PurchaseProgressRing.IsActive = true;
            PurchaseProgressRing.Visibility = Visibility.Visible;

            try
            {
                // 尝试购买高级版
                bool purchaseResult = await AIUsageService.RequestPurchaseAIPremiumAsync();

                if (purchaseResult)
                {
                    // 购买成功，播放成功动画
                    await PlaySuccessAnimationAsync();

                    // 刷新 AI 使用情况
                    await LoadAIUsageInfoAsync();

                    // 显示成功消息
                    ContentDialog successDialog = new ContentDialog
                    {
                        Title = "Settings_AIUsage_PurchaseSuccess_Title".GetLocalized(),
                        Content = "Settings_AIUsage_PurchaseSuccess_Content".GetLocalized(),
                        CloseButtonText = "Settings_AIUsage_PurchaseSuccess_CloseButton".GetLocalized()
                    };

                    await successDialog.ShowAsync();
                }
                else
                {
                    // 购买失败，显示提示
                    ContentDialog failureDialog = new ContentDialog
                    {
                        Title = "Settings_AIUsage_PurchaseFailed_Title".GetLocalized(),
                        Content = "Settings_AIUsage_PurchaseFailed_Content".GetLocalized(),
                        CloseButtonText = "Settings_AIUsage_PurchaseFailed_CloseButton".GetLocalized()
                    };

                    await failureDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                // 发生错误，显示错误消息
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Settings_AIUsage_PurchaseError_Title".GetLocalized(),
                    Content = $"Settings_AIUsage_PurchaseError_Content".GetLocalized() + $" {ex.Message}",
                    CloseButtonText = "Settings_AIUsage_PurchaseError_CloseButton".GetLocalized()
                };

                await errorDialog.ShowAsync();
            }
            finally
            {
                // 恢复按钮状态
                PurchaseButton.IsEnabled = true;
                PurchaseProgressRing.IsActive = false;
                PurchaseProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        // 播放购买成功动画
        private async Task PlaySuccessAnimationAsync()
        {
            // 创建动画
            var successStoryboard = new Storyboard();

            // 创建颜色动画
            var colorAnimation = new ColorAnimation
            {
                From = Colors.Transparent,
                To = Colors.Green,
                Duration = new Duration(TimeSpan.FromSeconds(1)),
                AutoReverse = true
            };

            // 应用动画
            Storyboard.SetTarget(colorAnimation, AIUsageCard);
            Storyboard.SetTargetProperty(colorAnimation, "(Border.BorderBrush).(SolidColorBrush.Color)");
            successStoryboard.Children.Add(colorAnimation);

            // 开始动画
            successStoryboard.Begin();

            // 等待动画完成
            await Task.Delay(2000);
        }
        private async Task InitializeAsync()
        {
            VersionDescription = GetVersionDescription();
            AppName = GetAppName();
            await Task.CompletedTask;
        }

        private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingLanguageComboBox)
                return;

            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var selectedLanguage = selectedItem.Tag.ToString();
                ApplicationLanguages.PrimaryLanguageOverride = selectedLanguage;
                // 重新启动应用程序以应用语言更改
                await CoreApplication.RequestRestartAsync(string.Empty);
            }
        }

        private string GetVersionDescription()
        {
            var package = Package.Current;
            var packageId = package.Id;
            var version = packageId.Version;

            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }

        private string GetAppName()
        {
            return "AppDisplayName".GetLocalized();
        }

        private async void ThemeChanged_CheckedAsync(object sender, RoutedEventArgs e)
        {
            var param = (sender as RadioButton)?.CommandParameter;

            if (param != null)
            {
                await ThemeSelectorService.SetThemeAsync((ElementTheme)param);
            }
        }

        private void UrlComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = UrlComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                var selectedUrl = selectedItem.Tag.ToString();
                var selectedContent = selectedItem.Content.ToString();
                if (!string.IsNullOrEmpty(selectedUrl) && !string.IsNullOrEmpty(selectedContent))
                {
                    ApplicationData.Current.LocalSettings.Values[SelectedUrlKey] = selectedUrl;
                    ApplicationData.Current.LocalSettings.Values[SelectedUrlContent] = selectedContent;
                }
            }
        }

        private void LoadSelectedUrl()
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(SelectedUrlKey, out object selectedUrl))
            {
                foreach (ComboBoxItem item in UrlComboBox.Items)
                {
                    if (item.Tag.ToString() == selectedUrl.ToString())
                    {
                        UrlComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private async void HotListToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var toggleSwitch = sender as ToggleSwitch;
            if (toggleSwitch != null && toggleSwitch.IsOn && !_initialHotListToggleState)
            {
                var result = await TermsOfServiceDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    ApplicationData.Current.LocalSettings.Values[HotListEnabledKey] = true;
                    CardEnable = true;
                }
                else
                {
                    toggleSwitch.IsOn = false;
                }
            }
            else if (toggleSwitch != null && (!toggleSwitch.IsOn))
            {
                ApplicationData.Current.LocalSettings.Values[HotListEnabledKey] = false;
                CardEnable = false;
            }

            _initialHotListToggleState = toggleSwitch.IsOn;
        }

        private async void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 收集系统诊断信息
                StringBuilder diagnosticInfo = new StringBuilder();

                // 添加基本系统信息
                diagnosticInfo.AppendLine("=== eComBox 诊断信息 ===");
                diagnosticInfo.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                diagnosticInfo.AppendLine($"应用版本: {GetVersionDescription()}");

                // 系统信息
                diagnosticInfo.AppendLine("\n=== 系统信息 ===");
                diagnosticInfo.AppendLine($"操作系统版本: {Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamilyVersion}");
                diagnosticInfo.AppendLine($"设备类型: {Windows.System.Profile.AnalyticsInfo.DeviceForm}");
                diagnosticInfo.AppendLine($"系统语言: {Windows.Globalization.ApplicationLanguages.Languages[0]}");
                diagnosticInfo.AppendLine($"应用语言: {ApplicationLanguages.PrimaryLanguageOverride}");
                diagnosticInfo.AppendLine($"应用主题: {ThemeSelectorService.Theme}");

                // 应用设置
                diagnosticInfo.AppendLine("\n=== 应用设置 ===");
                diagnosticInfo.AppendLine($"AI功能已启用: {ApplicationData.Current.LocalSettings.Values.TryGetValue("AIEnabled", out object aiEnabled) && (bool)aiEnabled}");
                diagnosticInfo.AppendLine($"开机自启动已启用: {await StartupService.IsStartupEnabled()}");
                diagnosticInfo.AppendLine($"实时热点已启用: {ApplicationData.Current.LocalSettings.Values.TryGetValue("HotListEnabled", out object hotListEnabled) && (bool)hotListEnabled}");

                // 通知信息
                diagnosticInfo.AppendLine("\n=== 通知设置 ===");
                diagnosticInfo.AppendLine($"通知权限: {Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier().Setting}");

                // 获取卡片和通知信息
                diagnosticInfo.AppendLine("\n=== 日期卡片信息 ===");
                try
                {
                    // 尝试从 ApplicationData 中读取卡片数据
                    var localFolder = ApplicationData.Current.LocalFolder;
                    var dataFile = await localFolder.TryGetItemAsync("data.json") as StorageFile;

                    if (dataFile != null)
                    {
                        string json = await FileIO.ReadTextAsync(dataFile);
                        diagnosticInfo.AppendLine($"原始卡片数据: {json}");

                        // 这里也可以尝试解析 JSON 数据来获取更具体的信息
                        diagnosticInfo.AppendLine("无法直接访问卡片对象模型，但已导出原始 JSON 数据");
                    }
                    else
                    {
                        diagnosticInfo.AppendLine("未找到卡片数据文件");
                    }
                }
                catch (Exception ex)
                {
                    diagnosticInfo.AppendLine($"获取卡片信息出错: {ex.Message}");
                }

                // 添加日志信息
                diagnosticInfo.AppendLine("\n=== 应用日志 ===");
                try
                {
                    // 读取日志文件（如果存在）
                    var logFile = await ApplicationData.Current.LocalFolder.TryGetItemAsync("app_debug_log.txt") as StorageFile;
                    if (logFile != null)
                    {
                        string logContent = await FileIO.ReadTextAsync(logFile);
                        diagnosticInfo.AppendLine(logContent);
                    }
                    else
                    {
                        diagnosticInfo.AppendLine("未找到日志文件");
                    }
                }
                catch (Exception ex)
                {
                    diagnosticInfo.AppendLine($"读取日志文件失败: {ex.Message}");
                }

                // 使用FileSavePicker让用户选择保存位置
                var savePicker = new Windows.Storage.Pickers.FileSavePicker
                {
                    SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
                };
                savePicker.FileTypeChoices.Add("Settings_ExportLog_FileType".GetLocalized(), new List<string> { ".txt" });
                savePicker.SuggestedFileName = $"eComBox_Log_{DateTime.Now:yyyyMMdd_HHmmss}";

                // 获取用户选择的保存文件
                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    // 防止更新文件时的文件冲突
                    CachedFileManager.DeferUpdates(file);

                    // 写入日志内容到文件
                    await FileIO.WriteTextAsync(file, diagnosticInfo.ToString());

                    // 完成文件更新
                    var status = await CachedFileManager.CompleteUpdatesAsync(file);

                    if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                    {
                        // 显示成功消息
                        ContentDialog dialog = new ContentDialog
                        {
                            Title = "Settings_ExportLog_Success_Title".GetLocalized(),
                            Content = string.Format("Settings_ExportLog_Success_Content".GetLocalized(), file.Path),
                            CloseButtonText = "Settings_ExportLog_CloseButton".GetLocalized()
                        };
                        await dialog.ShowAsync();
                    }
                    else
                    {
                        // 显示失败消息
                        ContentDialog dialog = new ContentDialog
                        {
                            Title = "Settings_ExportLog_Fail_Title".GetLocalized(),
                            Content = "Settings_ExportLog_Fail_Content".GetLocalized(),
                            CloseButtonText = "Settings_ExportLog_CloseButton".GetLocalized()
                        };
                        await dialog.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // 显示错误消息
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Settings_ExportLog_Error_Title".GetLocalized(),
                    Content = string.Format("Settings_ExportLog_Error_Content".GetLocalized(), ex.Message),
                    CloseButtonText = "Settings_ExportLog_CloseButton".GetLocalized()
                };
                await dialog.ShowAsync();
            }
        }

        private async void AIToggleSwitch_Toggled()
        {
            ApplicationData.Current.LocalSettings.Values[AIEnabledKey] = false;
        }

        private void TermsHyperlinkButton_Click(object sender, RoutedEventArgs e)
        {
            TermsOfServiceDialog.IsPrimaryButtonEnabled = true;
        }

        private void LoadHotListToggleState()
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(HotListEnabledKey, out object hotListEnabled))
            {
                _initialHotListToggleState = (bool)hotListEnabled;
                HotListToggleSwitch.IsOn = _initialHotListToggleState;
                CardEnable = HotListToggleSwitch.IsOn;
            }
            else
            {
                CardEnable = false;
            }
        }

        private void TermsOfServiceDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 用户点击了"我已阅读并同意"按钮
        }

        private void TermsOfServiceDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 用户点击了"取消"按钮
        }

        private void LoadTranslatorSettings()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey("TranslatorEndpoint"))
            {
                EndpointTextBox.Text = localSettings.Values["TranslatorEndpoint"] as string;
            }
            if (localSettings.Values.ContainsKey("TranslatorApiKey"))
            {
                ApiTextBox.Password = localSettings.Values["TranslatorApiKey"] as string;
            }
            if (localSettings.Values.ContainsKey("TranslatorRegion"))
            {
                RegionTextBox.Text = localSettings.Values["TranslatorRegion"] as string;
            }
        }

        private void SaveTranslatorSettings()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["TranslatorEndpoint"] = EndpointTextBox.Text;
            localSettings.Values["TranslatorApiKey"] = ApiTextBox.Password;
            localSettings.Values["TranslatorRegion"] = RegionTextBox.Text;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void openBetaPage(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(betaPage));
        }

        private void EndpointTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveTranslatorSettings();
        }

        private void ApiTextBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            SaveTranslatorSettings();
        }

        private void RegionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveTranslatorSettings();
        }
    }
}
