using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json; // Install Newtonsoft.Json with NuGet
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using static CommunityToolkit.WinUI.Animations.Expressions.ExpressionValues;
using System.Security.Cryptography.X509Certificates;

namespace eComBox.Views
{
    

    public sealed partial class translatorPage : Page, INotifyPropertyChanged
    {
        public translatorPage()
        {
            InitializeComponent();
            LoadTranslatorSettings();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            OnPropertyChanged(propertyName);
        }
        


        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        private string key;
        private string endpoint = "https://api.cognitive.microsofttranslator.com";
        private string location ;
        public string textToTranslate;
        private string source;
        private string target;
        public string route;

        private void LoadTranslatorSettings()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            try
            {
                endpoint = localSettings.Values["TranslatorEndpoint"] as string;
                key = localSettings.Values["TranslatorApiKey"] as string;
                location = localSettings.Values["TranslatorRegion"] as string;

                if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(location))
                {
                    throw new Exception("缺少翻译工具配置信息");
                }

                statusBar.IsOpen = true;
            }
            catch
            {
                statusBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error;
                statusBar.Title = "缺少信息";
                statusBar.Message = "请在设置中输入翻译的配置信息";
                statusBar.IsOpen = true;
                translateButton.IsEnabled = false;
            }
        }

        private async void translateButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
          
            textToTranslate = ContentBox.Text;
            source = (sourceLanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            target = (targetLanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            key ="7PNbpY84BnJXKttlwaB1fgNKwvvj7Snis1yLZghyZUJp67QB6SbdJQQJ99BAACYeBjFXJ3w3AAAbACOGiWzk";
            endpoint="https://api.cognitive.microsofttranslator.com";
            location = "eastus";
            
            if (source== "")
            {
                route = "/translate?api-version=3.0&to="+target;
            }
            else
            {
                route = "/translate?api-version=3.0&from="+source+"&to="+target;
            }
            try
            {
                await Translate(false);
            }
            catch (Exception ex)
            {
                outputBox.Text = ex.Message;
            }

        }
        private async Task Translate( bool isReverse)
        {
            // Input and output languages are defined as parameters.
            
            object[] body = new object[] { new { Text = textToTranslate } };
            var requestBody = JsonConvert.SerializeObject(body);

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                // Build the request.
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(endpoint + route);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", key);
                // location required if you're using a multi-service or regional (not global) resource.
                request.Headers.Add("Ocp-Apim-Subscription-Region", location);

                // Send the request and get response.
                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                // Read response as a string.
                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                // Parse the JSON response
                var jsonResponse = JsonConvert.DeserializeObject<dynamic>(responseString);
                string translatedText = jsonResponse[0].translations[0].text;
                
                // Update UI on the UI thread
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    if (!isReverse) {
                        outputBox.Text = translatedText;
                        reverseTranslateButton.IsEnabled=true;
                        reverseTranslateButton.Visibility= Windows.UI.Xaml.Visibility.Visible;
                    }
                    else
                    {
                        ContentBox.Text = translatedText;
                        reverseTranslateButton.IsEnabled=false;
                    }
                        
                });
            }
        }

        private void ContentBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            reverseTranslateButton.Visibility= Windows.UI.Xaml.Visibility.Collapsed;
        }

        private async void reverseTranslateButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            
            var tempItem = targetLanguageComboBox.SelectedItem;
            
            targetLanguageComboBox.SelectedItem = sourceLanguageComboBox.SelectedItem;
            sourceLanguageComboBox.SelectedItem = tempItem;
            if (source== "")
            {
                
            }
            route = "/translate?api-version=3.0&from="+target+"&to="+source;
           
            try
            {
                await Translate(true);
            }
            catch (Exception ex)
            {
                ContentBox.Text = ex.Message;
            }
        }
    }
}
