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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace eComBox.Views
{
    // TODO: Add other settings as necessary. For help see https://github.com/microsoft/TemplateStudio/blob/main/docs/UWP/pages/settings-codebehind.md
    // TODO: Change the URL for your privacy policy in the Resource File, currently set to https://YourPrivacyUrlGoesHere
    public sealed partial class SettingsPage : Page, INotifyPropertyChanged
    {
        private const string SelectedUrlKey = "SelectedUrl";
        private const string SelectedUrlContent = "SelectedUrlContent";
        private const string DefaultUrl = "https://doc.cohelper.tech/baidu?cache=false";
        private const string HotListEnabledKey = "HotListEnabled";
        private const string AIEnabledKey = "AIEnabled";

        private bool _initialHotListToggleState;
        private bool _isInitializingLanguageComboBox;
        private bool _isLoadingAISettings;

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

        private bool _cardEnable;

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

        public SettingsPage()
        {
            InitializeComponent();
            InitializeLanguageComboBox();
            LoadSelectedUrl();
            LoadHotListToggleState();
            LoadAIEnabledState();
        }

        private async void StartupToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (StartupToggle.IsOn)
            {
                var state = await StartupService.EnableStartupAsync();
                if (state == StartupTaskState.Enabled)
                {
                    IsStartupEnabled = true;
                    await StartupService.RegisterBackgroundNotificationTask();
                }
                else
                {
                    StartupToggle.IsOn = false;
                    IsStartupEnabled = false;
                    await errorStartDialog.ShowAsync();
                }
            }
            else
            {
                StartupService.DisableStartup();
                IsStartupEnabled = false;
            }
        }

        private void InitializeLanguageComboBox()
        {
            _isInitializingLanguageComboBox = true;

            string currentLanguage = ApplicationLanguages.PrimaryLanguageOverride;
            if (string.IsNullOrEmpty(currentLanguage))
            {
                currentLanguage = ApplicationLanguages.Languages.FirstOrDefault() ?? "en-US";
                if (currentLanguage.StartsWith("zh"))
                {
                    currentLanguage = "zh-Hans-CN";
                }
                else if (currentLanguage.StartsWith("en"))
                {
                    currentLanguage = "en-US";
                }

                ApplicationLanguages.PrimaryLanguageOverride = currentLanguage;
            }

            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag?.ToString() == currentLanguage)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }

            _isInitializingLanguageComboBox = false;
        }

        private void LoadAIEnabledState()
        {
            _isLoadingAISettings = true;
            try
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(AIEnabledKey, out object aiEnabled) && aiEnabled is bool enabled)
                {
                    AIToggleSwitch.IsOn = enabled;
                    return;
                }

                ApplicationData.Current.LocalSettings.Values[AIEnabledKey] = false;
                AIToggleSwitch.IsOn = false;
            }
            finally
            {
                _isLoadingAISettings = false;
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 快速初始化（同步/轻量操作）
            _ = InitializeAsync();

            // 后台异步刷新，不阻塞页面渲染
            _ = RefreshAIUsageAsync();
            _ = LoadStartupStateAsync();
        }

        private async Task LoadStartupStateAsync()
        {
            try
            {
                var task = StartupService.IsStartupEnabled();
                var timeout = Task.Delay(1500);
                if (await Task.WhenAny(task, timeout) == task)
                {
                    IsStartupEnabled = await task;
                }
            }
            catch { }
        }

        private async Task RefreshAIUsageAsync()
        {
            try
            {
                // 仅本地检查，绝不调用 Store API，确保设置页秒开
                bool isPro = AIUsageService.IsProUserLocally();

                int used = await AIUsageService.GetTodayUsageAsync();
                int limit = ConfigurationService.FreeUsageLimit;
                int remaining = Math.Max(0, limit - used);

                if (isPro)
                {
                    RemainingCountTextBlock.Text = "∞ / ∞";
                    UsageProgressBar.Value = UsageProgressBar.Maximum;
                    PurchaseButton.Visibility = Visibility.Collapsed;
                    PurchaseButton.IsEnabled = false;
                    PurchaseProgressRing.Visibility = Visibility.Collapsed;
                }
                else
                {
                    RemainingCountTextBlock.Text = remaining + " / " + limit;
                    UsageProgressBar.Maximum = limit;
                    UsageProgressBar.Value = used;
                    PurchaseButton.Visibility = Visibility.Visible;
                    PurchaseButton.IsEnabled = false;
                    PurchaseProgressRing.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                RemainingCountTextBlock.Text = "-- / --";
                PurchaseButton.Visibility = Visibility.Collapsed;
                PurchaseProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        private async void PurchaseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PurchaseButton.IsEnabled = false;
                PurchaseProgressRing.Visibility = Visibility.Visible;
                PurchaseProgressRing.IsActive = true;

                var (success, error) = await AIUsageService.RequestPurchaseAIPremiumAsync();

                if (success)
                {
                    await RefreshAIUsageAsync();
                    var dialog = new ContentDialog
                    {
                        Title = "Settings_AIUsage_PurchaseSuccess_Title".GetLocalized(),
                        Content = "Settings_AIUsage_PurchaseSuccess_Content".GetLocalized(),
                        CloseButtonText = "Settings_AIUsage_PurchaseSuccess_CloseButton".GetLocalized()
                    };
                    await dialog.ShowAsync();
                }
                else
                {
                    var message = "Settings_AIUsage_PurchaseFailed_Content".GetLocalized();
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        message += "\n\n" + error;
                    }
                    var dialog = new ContentDialog
                    {
                        Title = "Settings_AIUsage_PurchaseFailed_Title".GetLocalized(),
                        Content = message,
                        CloseButtonText = "Settings_AIUsage_PurchaseFailed_CloseButton".GetLocalized()
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Settings_AIUsage_PurchaseError_Title".GetLocalized(),
                    Content = "Settings_AIUsage_PurchaseError_Content".GetLocalized() + "\n\n" + ex.Message,
                    CloseButtonText = "Settings_AIUsage_PurchaseError_CloseButton".GetLocalized()
                };
                await dialog.ShowAsync();
            }
            finally
            {
                PurchaseButton.IsEnabled = false;
                PurchaseProgressRing.IsActive = false;
                PurchaseProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        private async Task InitializeAsync()
        {
            VersionDescription = GetVersionDescription();
            AppName = GetAppName();
            await Task.CompletedTask;
        }

        private void ReopenOobeButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(typeof(FirstRunPage));
        }

        private string GetVersionDescription()
        {
            var version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }

        private string GetAppName()
        {
            return "AppDisplayName".GetLocalized();
        }

        private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingLanguageComboBox)
            {
                return;
            }

            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                ApplicationLanguages.PrimaryLanguageOverride = selectedItem.Tag?.ToString();
                await CoreApplication.RequestRestartAsync(string.Empty);
            }
        }

        private async void ThemeChanged_CheckedAsync(object sender, RoutedEventArgs e)
        {
            var param = (sender as RadioButton)?.CommandParameter;
            if (param != null)
            {
                await ThemeSelectorService.SetThemeAsync((ElementTheme)param);
            }
        }

        private void AIToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoadingAISettings)
            {
                return;
            }

            ApplicationData.Current.LocalSettings.Values[AIEnabledKey] = AIToggleSwitch.IsOn;
        }

        private void UrlComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UrlComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var selectedUrl = selectedItem.Tag?.ToString();
                var selectedContent = selectedItem.Content?.ToString();
                if (!string.IsNullOrEmpty(selectedUrl) && !string.IsNullOrEmpty(selectedContent))
                {
                    ApplicationData.Current.LocalSettings.Values[SelectedUrlKey] = selectedUrl;
                    ApplicationData.Current.LocalSettings.Values[SelectedUrlContent] = selectedContent;
                }
            }
        }

        private void LoadSelectedUrl()
        {
            if (!ApplicationData.Current.LocalSettings.Values.TryGetValue(SelectedUrlKey, out object selectedUrl))
            {
                return;
            }

            foreach (ComboBoxItem item in UrlComboBox.Items)
            {
                if (item.Tag?.ToString() == selectedUrl?.ToString())
                {
                    UrlComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private async void HotListToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggleSwitch)
            {
                if (toggleSwitch.IsOn && !_initialHotListToggleState)
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
                else if (!toggleSwitch.IsOn)
                {
                    ApplicationData.Current.LocalSettings.Values[HotListEnabledKey] = false;
                    CardEnable = false;
                }

                _initialHotListToggleState = toggleSwitch.IsOn;
            }
        }

        private void LoadHotListToggleState()
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(HotListEnabledKey, out object hotListEnabled) && hotListEnabled is bool enabled)
            {
                _initialHotListToggleState = enabled;
                HotListToggleSwitch.IsOn = enabled;
                CardEnable = enabled;
                return;
            }

            CardEnable = false;
        }

        private async void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StringBuilder diagnosticInfo = new StringBuilder();
                diagnosticInfo.AppendLine("=== eComBox 诊断信息 ===");
                diagnosticInfo.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                diagnosticInfo.AppendLine($"应用版本: {GetVersionDescription()}");

                diagnosticInfo.AppendLine("\n=== 系统信息 ===");
                diagnosticInfo.AppendLine($"设备类型: {Windows.System.Profile.AnalyticsInfo.DeviceForm}");
                diagnosticInfo.AppendLine($"系统语言: {Windows.Globalization.ApplicationLanguages.Languages[0]}");
                diagnosticInfo.AppendLine($"应用语言: {ApplicationLanguages.PrimaryLanguageOverride}");
                diagnosticInfo.AppendLine($"应用主题: {ThemeSelectorService.Theme}");

                diagnosticInfo.AppendLine("\n=== 应用设置 ===");
                diagnosticInfo.AppendLine($"AI功能已启用: {GetBoolSetting(AIEnabledKey)}");
                diagnosticInfo.AppendLine($"开机自启动已启用: {await StartupService.IsStartupEnabled()}");
                diagnosticInfo.AppendLine($"实时热点已启用: {GetBoolSetting(HotListEnabledKey)}");

                diagnosticInfo.AppendLine("\n=== 通知设置 ===");
                diagnosticInfo.AppendLine($"通知权限: {Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier().Setting}");

                diagnosticInfo.AppendLine("\n=== 日期卡片信息 ===");
                try
                {
                    var localFolder = ApplicationData.Current.LocalFolder;
                    var dataFile = await localFolder.TryGetItemAsync("data.json") as StorageFile;
                    if (dataFile != null)
                    {
                        string json = await FileIO.ReadTextAsync(dataFile);
                        diagnosticInfo.AppendLine($"原始卡片数据: {json}");
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

                diagnosticInfo.AppendLine("\n=== 应用日志 ===");
                try
                {
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

                var savePicker = new Windows.Storage.Pickers.FileSavePicker
                {
                    SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
                };
                savePicker.FileTypeChoices.Add("Settings_ExportLog_FileType".GetLocalized(), new List<string> { ".txt" });
                savePicker.SuggestedFileName = $"eComBox_Log_{DateTime.Now:yyyyMMdd_HHmmss}";

                var file = await savePicker.PickSaveFileAsync();
                if (file == null)
                {
                    return;
                }

                CachedFileManager.DeferUpdates(file);
                await FileIO.WriteTextAsync(file, diagnosticInfo.ToString());
                var status = await CachedFileManager.CompleteUpdatesAsync(file);

                ContentDialog dialog = new ContentDialog
                {
                    Title = status == Windows.Storage.Provider.FileUpdateStatus.Complete
                        ? "Settings_ExportLog_Success_Title".GetLocalized()
                        : "Settings_ExportLog_Fail_Title".GetLocalized(),
                    Content = status == Windows.Storage.Provider.FileUpdateStatus.Complete
                        ? string.Format("Settings_ExportLog_Success_Content".GetLocalized(), file.Path)
                        : "Settings_ExportLog_Fail_Content".GetLocalized(),
                    CloseButtonText = "Settings_ExportLog_CloseButton".GetLocalized()
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Settings_ExportLog_Error_Title".GetLocalized(),
                    Content = string.Format("Settings_ExportLog_Error_Content".GetLocalized(), ex.Message),
                    CloseButtonText = "Settings_ExportLog_CloseButton".GetLocalized()
                };
                await dialog.ShowAsync();
            }
        }

        private void TermsHyperlinkButton_Click(object sender, RoutedEventArgs e)
        {
            TermsOfServiceDialog.IsPrimaryButtonEnabled = true;
        }

        private void TermsOfServiceDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void TermsOfServiceDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private bool GetBoolSetting(string key)
        {
            return ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out object value) && value is bool enabled && enabled;
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
    }
}
