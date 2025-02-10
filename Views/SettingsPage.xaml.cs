using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using eComBox.Helpers;
using eComBox.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

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
        private const string DefaultUrlContent = "百度";
        private const string HotListEnabledKey = "HotListEnabled";
        private bool _initialHotListToggleState;

        private ElementTheme _elementTheme = ThemeSelectorService.Theme;

        public ElementTheme ElementTheme
        {
            get { return _elementTheme; }
            set { Set(ref _elementTheme, value); }
        }
        private Visibility _hotListVisibility = Visibility.Visible;

        public Visibility HotListVisibility
        {
            get { return _hotListVisibility; }
            set { Set(ref _hotListVisibility, value); }
        }
        private string _versionDescription;

        public string VersionDescription
        {
            get { return _versionDescription; }
            set { Set(ref _versionDescription, value); }
        }

        public SettingsPage()
        {
            InitializeComponent();
            LoadSelectedUrl();
            LoadHotListToggleState();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            VersionDescription = GetVersionDescription();
            await Task.CompletedTask;
        }

        private string GetVersionDescription()
        {
            var appName = "AppDisplayName".GetLocalized();
            var package = Windows.ApplicationModel.Package.Current;
            var packageId = package.Id;
            var version = packageId.Version;

            return $"{appName} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
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
            var selectedUrl = (UrlComboBox.SelectedItem as ComboBoxItem)?.Tag.ToString();
            var selectedContent = (UrlComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (!string.IsNullOrEmpty(selectedUrl) && !string.IsNullOrEmpty(selectedContent))
            {
                ApplicationData.Current.LocalSettings.Values[SelectedUrlKey] = selectedUrl;
                ApplicationData.Current.LocalSettings.Values[SelectedUrlContent] = selectedContent;
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
                    HotListVisibility = Visibility.Visible;
                }
                else
                {
                    toggleSwitch.IsOn = false;
                }
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values[HotListEnabledKey] = false;
                HotListVisibility = Visibility.Collapsed;
            }
            _initialHotListToggleState = toggleSwitch.IsOn;
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
                HotListVisibility = HotListToggleSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
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


        private void WebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (e.TryGetWebMessageAsString() == "scrolledToBottom")
            {
                TermsOfServiceDialog.IsPrimaryButtonEnabled = true;
            }
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
    }
}
