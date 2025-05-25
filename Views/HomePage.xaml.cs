using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Windows.UI.Xaml.Controls;

using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml.Documents;
using Windows.Storage;

namespace eComBox.Views
{
    public sealed partial class HomePage : Page, INotifyPropertyChanged
    {
        private const string SelectedUrlKey = "SelectedUrl";
        private const string SelectedUrlContent = "SelectedUrlContent";
        private const string CacheFileName = "newsCache.json";
        private const string CacheTimestampKey = "CacheTimestamp";
        private const string CacheUrlKey = "CacheUrl";

        public HomePage()
        {
            InitializeComponent();
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("HotListEnabled", out object hotListEnabled) && (bool)hotListEnabled)
            {
                LoadDataAsync();
            }
        }

        private async Task LoadDataAsync()
        {
            TextBlock[] texts = { Trend1, Trend2, Trend3, Trend4, Trend5, Trend6, Trend7, Trend8, Trend9, Trend10 };
            HyperlinkButton[] links = { Nav1, Nav2, Nav3, Nav4, Nav5, Nav6, Nav7, Nav8, Nav9, Nav10 };
            string url = ApplicationData.Current.LocalSettings.Values[SelectedUrlKey]?.ToString() ?? "https://doc.ecomter.site/baidu?cache=false";
            newsHeader.Text = (ApplicationData.Current.LocalSettings.Values[SelectedUrlContent]?.ToString() ?? "百度热搜榜");

            var cacheFolder = ApplicationData.Current.LocalCacheFolder;
            StorageFile cacheFile = await cacheFolder.TryGetItemAsync(CacheFileName) as StorageFile;

            if (cacheFile != null)
            {
                var cacheTimestamp = ApplicationData.Current.LocalSettings.Values[CacheTimestampKey] as DateTimeOffset?;
                var cachedUrl = ApplicationData.Current.LocalSettings.Values[CacheUrlKey]?.ToString();
                if (cacheTimestamp.HasValue && cachedUrl == url && (DateTimeOffset.Now - cacheTimestamp.Value).TotalMinutes < 5)
                {
                    // 从缓存加载
                    string cachedData = await FileIO.ReadTextAsync(cacheFile);
                    var json = JObject.Parse(cachedData);
                    UpdateUI(json, texts, links);
                    board.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    return;
                }
                else
                {
                    // 清除缓存
                    await cacheFile.DeleteAsync();
                    ApplicationData.Current.LocalSettings.Values.Remove(CacheTimestampKey);
                    ApplicationData.Current.LocalSettings.Values.Remove(CacheUrlKey); // 移除缓存的 URL
                }
            }


            using (HttpClient client = new HttpClient())
            {
                try
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        LoadingRing.IsActive = true;
                        LoadingRing.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    });

                    string result = await client.GetStringAsync(url);
                    var json = JObject.Parse(result);

                    // 保存到缓存
                    cacheFile = await cacheFolder.CreateFileAsync(CacheFileName, CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteTextAsync(cacheFile, result);
                    ApplicationData.Current.LocalSettings.Values[CacheTimestampKey] = DateTimeOffset.Now;
                    ApplicationData.Current.LocalSettings.Values[CacheUrlKey] = url; // 保存当前的 URL

                    UpdateUI(json, texts, links);
                    board.Visibility = Windows.UI.Xaml.Visibility.Visible;

                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        LoadingRing.IsActive = false;
                        LoadingRing.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    });
                }
                catch (Exception ex)
                {
                    Trend1.Text = $"Error: {ex.Message}";
                    LoadingRing.IsActive = false;
                    LoadingRing.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
            }
        }

        private async void UpdateUI(JObject json, TextBlock[] texts, HyperlinkButton[] links)
        {
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                int index = i; // 避免闭包问题
                tasks[i] = Task.Run(async () =>
                {
                    string title = json["data"]?[index]?["title"]?.ToString();
                    string link = json["data"]?[index]?["url"]?.ToString();

                    // 使用 Dispatcher 在 UI 线程上更新 UI 控件
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        texts[index].Text = title;
                        links[index].NavigateUri = new Uri(link);
                    });
                });
            }
            await Task.WhenAll(tasks);
        }

        private void ToGeoPage(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(GeometryPage));
        }

        private void ToDatePage(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DatePage));
        }
        private void ToTranslatorPage(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(translatorPage));
        }
        private async void refreshUrl(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var cacheFolder = ApplicationData.Current.LocalCacheFolder;
            var cacheFile = await cacheFolder.TryGetItemAsync(CacheFileName) as StorageFile;
            if (cacheFile != null)
            {
                await cacheFile.DeleteAsync();
                ApplicationData.Current.LocalSettings.Values.Remove(CacheTimestampKey);
            }
            board.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            await LoadDataAsync();
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
