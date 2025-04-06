using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private const string DefaultUrl = "https://doc.ecomter.site/baidu?cache=false";
        private const string DefaultUrlContent = "百度热搜榜";
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
                _initialAIToggleState = (bool)aiEnabled;
                AIToggleSwitch.IsOn = _initialAIToggleState;
            }
            else
            {
                // 默认启用AI功能
                _initialAIToggleState = false;
                AIToggleSwitch.IsOn = false;
                ApplicationData.Current.LocalSettings.Values[AIEnabledKey] = false;
            }
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {

            await InitializeAsync();
            base.OnNavigatedTo(e);

            // 检查是否已开启开机启动
            IsStartupEnabled = await StartupService.IsStartupEnabled();
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
  
            var package = Windows.ApplicationModel.Package.Current;
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

        private void SetDefaultUrl()
        {
            foreach (ComboBoxItem item in UrlComboBox.Items)
            {
                if (item.Content.ToString() == DefaultUrlContent)
                {
                    UrlComboBox.SelectedItem = item;
                    ApplicationData.Current.LocalSettings.Values[SelectedUrlKey] = DefaultUrl;
                    break;
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

        private async void AIToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var toggleSwitch = sender as ToggleSwitch;

            ApplicationData.Current.LocalSettings.Values[AIEnabledKey] = toggleSwitch.IsOn;
            _initialAIToggleState = toggleSwitch.IsOn;

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
            // 用户点击了“我已阅读并同意”按钮
        }

        private void TermsOfServiceDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 用户点击了“取消”按钮
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
