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
        private const string SelectedUrlKey = "https://doc.ecomter.site/baidu";
        private const string SelectedUrlContent = "百度";

        public HomePage()
        {
            InitializeComponent();
            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            TextBlock[] texts = { Trend1, Trend2, Trend3, Trend4, Trend5, Trend6, Trend7, Trend8, Trend9, Trend10 };
            HyperlinkButton[] links = { Nav1, Nav2, Nav3, Nav4, Nav5, Nav6, Nav7, Nav8, Nav9, Nav10 };
            string url = ApplicationData.Current.LocalSettings.Values[SelectedUrlKey]?.ToString() ?? "https://doc.ecomter.site/baidu";
            newsHeader.Text =(ApplicationData.Current.LocalSettings.Values[SelectedUrlContent]?.ToString() ?? "百度").ToString()+"热搜榜";


            using (HttpClient client = new HttpClient())
            {
                try
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        LoadingRing.IsActive = true;
                        LoadingRing.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    });
                    var tasks = new Task[10];
                    for (int i = 0; i < 10; i++)
                    {
                        int index = i; // 避免闭包问题
                        tasks[i] = Task.Run(async () =>
                        {
                            string result = await client.GetStringAsync(url);
                            var json = JObject.Parse(result);
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

        private void ToGeoPage(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(GeometryPage));
        }

        private void ToDatePage(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DatePage));
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
