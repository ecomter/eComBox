using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using Windows.UI.Xaml.Controls;
using static CommunityToolkit.WinUI.Animations.Expressions.ExpressionValues;
using Windows.UI.Xaml;

namespace eComBox.Views
{
    public class LanguageItem : INotifyPropertyChanged
    {
        private ComboBoxItem _selectedLanguage;
        private int _index;

        public int Index
        {
            get => _index;
            set
            {
                _index = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ComboBoxItem> LanguageOptions { get; } = new ObservableCollection<ComboBoxItem>();

        public ComboBoxItem SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (_selectedLanguage != value)
                {
                    _selectedLanguage = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed partial class translatorPage : Page, INotifyPropertyChanged
    {
        private ObservableCollection<LanguageItem> _translationSequence = new ObservableCollection<LanguageItem>();
        private const int MaxTranslationSteps = 20;
        private List<ComboBoxItem> _allLanguages = new List<ComboBoxItem>();

        public translatorPage()
        {
            InitializeComponent();
            LoadTranslatorSettings();
            TranslationSequencePanel.ItemsSource = _translationSequence; // 确保这一行存在
            if (targetLanguageComboBox.Items.Count == 0)
            {
                // 如果没有语言项，说明XAML中的ComboBox初始化不完整
                statusBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error;
                statusBar.Title = "初始化错误";
                statusBar.Message = "语言选择菜单未能正确加载";
                statusBar.IsOpen = true;
            }
            else
            {
                // 初始化翻译序列
                InitializeTranslationSequence();

                // 尝试加载已保存的设置
                LoadSequenceSettings();
            }
        }

        private void InitializeTranslationSequence()
        {
            // 收集所有语言选项
            foreach (ComboBoxItem item in targetLanguageComboBox.Items)
            {
                _allLanguages.Add(new ComboBoxItem { Content = item.Content, Tag = item.Tag });
            }

            // 创建20个翻译步骤
            for (int i = 0; i < MaxTranslationSteps; i++)
            {
                var languageItem = new LanguageItem { Index = i + 1 };

                // 复制所有语言选项
                foreach (var lang in _allLanguages)
                {
                    languageItem.LanguageOptions.Add(new ComboBoxItem
                    {
                        Content = lang.Content,
                        Tag = lang.Tag
                    });
                }

                // 默认选择一些常用语言作为示例顺序
                if (i < _allLanguages.Count)
                {
                    // 生成一个不重复的顺序，使用常用语言
                    string tagToSelect = "";
                    switch (i % 8)
                    {
                        case 0: tagToSelect = "en"; break;      // 英语
                        case 1: tagToSelect = "fr"; break;      // 法语
                        case 2: tagToSelect = "es"; break;      // 西班牙语
                        case 3: tagToSelect = "de"; break;      // 德语
                        case 4: tagToSelect = "ja"; break;      // 日语
                        case 5: tagToSelect = "ru"; break;      // 俄语
                        case 6: tagToSelect = "ar"; break;      // 阿拉伯语
                        case 7: tagToSelect = "zh-Hans"; break; // 中文(简体)
                    }

                    languageItem.SelectedLanguage = languageItem.LanguageOptions.FirstOrDefault(l =>
                        l.Tag.ToString() == tagToSelect) ?? languageItem.LanguageOptions.FirstOrDefault();
                }

                _translationSequence.Add(languageItem);
            }

            // 设置列表源
            TranslationSequencePanel.ItemsSource = _translationSequence;
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
        private string key;
        private string endpoint = "https://api.cognitive.microsofttranslator.com";
        private string location;
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

        private async void translateButton_Click(object sender, RoutedEventArgs e)
        {
            // 显示进度条
            translationProgressBar.Visibility = Visibility.Visible;

            textToTranslate = ContentBox.Text;
            source = (sourceLanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            target = (targetLanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            if (source == "")
            {
                route = "/translate?api-version=3.0&to=" + target;
            }
            else
            {
                route = "/translate?api-version=3.0&from=" + source + "&to=" + target;
            }
            try
            {
                await Translate(false);
            }
            catch (Exception ex)
            {
                outputBox.Text = ex.Message;
            }
            // 隐藏进度条
            translationProgressBar.Visibility = Visibility.Collapsed;

        }

        private async void sequenceTranslateButton_Click(object sender, RoutedEventArgs e)
        {
            // 禁用按钮，防止重复操作
            sequenceTranslateButton.IsEnabled = false;
            translateButton.IsEnabled = false;
            statusBar.Message = "顺序翻译进行中...";
            statusBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;

            // 确保反向翻译按钮隐藏
            reverseTranslateButton.Visibility = Visibility.Collapsed;

            // 显示进度条并设置为确定模式
            translationProgressBar.IsIndeterminate = false;
            translationProgressBar.Value = 0;
            translationProgressBar.Maximum = _translationSequence.Count;
            translationProgressBar.Visibility = Visibility.Visible;

            try
            {
                // 清空历史记录
                translationHistoryBox.Text = "";

                // 获取原始文本
                string originalText = ContentBox.Text;
                string currentText = originalText;

                // 获取初始语言（源语言）
                string initialLanguage = (sourceLanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
                string initialLanguageName = (sourceLanguageComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "自动检测";

                // 记录翻译历史
                StringBuilder history = new StringBuilder();
                history.AppendLine($"原始文本 ({initialLanguageName}): {originalText}\n");

                

                // 计算有效的翻译步骤数量（排除未选择语言的步骤）
                int validStepsCount = _translationSequence.Count(s => s.SelectedLanguage != null);
                int completedSteps = 0;

                // 顺序执行翻译
                for (int i = 0; i < _translationSequence.Count; i++)
                {
                    if (_translationSequence[i].SelectedLanguage == null)
                        continue;

                    string targetLang = _translationSequence[i].SelectedLanguage.Tag.ToString();
                    string targetName = _translationSequence[i].SelectedLanguage.Content.ToString();

                    // 设置翻译路由
                    string sourceLang = (i == 0) ? initialLanguage :
                        _translationSequence[i - 1].SelectedLanguage.Tag.ToString();

                    if (string.IsNullOrEmpty(sourceLang) && i == 0)
                    {
                        route = $"/translate?api-version=3.0&to={targetLang}";
                    }
                    else
                    {
                        route = $"/translate?api-version=3.0&from={sourceLang}&to={targetLang}";
                    }

                    // 更新状态栏和进度条
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        statusBar.Message = $"顺序翻译中：步骤 {i + 1}/{_translationSequence.Count} - {targetName}";

                        // 更新进度条
                        completedSteps++;
                        double progressPercentage = (double)completedSteps / validStepsCount * 100;
                        translationProgressBar.Value = i + 1;
                    });

                    // 执行翻译
                    textToTranslate = currentText;
                    string translatedText = await TranslateSequence();
                    currentText = translatedText;

                    // 记录每一步的翻译结果
                    history.AppendLine($"步骤 {i + 1}: {targetName} -> {translatedText}\n");
                }

                // 显示最终翻译结果
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    outputBox.Text = currentText;
                    translationHistoryBox.Text = history.ToString();
                    statusBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success;
                    statusBar.Message = "顺序翻译完成！";

                    // 完成时将进度条设置为100%
                    translationProgressBar.Value = translationProgressBar.Maximum;
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    outputBox.Text = ex.Message;
                    statusBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error;
                    statusBar.Message = "翻译过程中出现错误！";
                });
            }
            finally
            {
                // 重新启用按钮
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    sequenceTranslateButton.IsEnabled = true;
                    translateButton.IsEnabled = true;
                    // 保持反向翻译按钮隐藏状态
                    reverseTranslateButton.Visibility = Visibility.Collapsed;
                    // 隐藏进度条
                    translationProgressBar.Visibility = Visibility.Collapsed;
                });
            }
        }

        private async Task<string> TranslateSequence()
        {
            // 创建请求体
            object[] body = new object[] { new { Text = textToTranslate } };
            var requestBody = JsonConvert.SerializeObject(body);

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                // 构建请求
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(endpoint + route);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", key);
                request.Headers.Add("Ocp-Apim-Subscription-Region", location);

                // 发送请求并获取响应
                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);

                // 读取响应字符串
                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                // 解析JSON响应
                var jsonResponse = JsonConvert.DeserializeObject<dynamic>(responseString);
                return jsonResponse[0].translations[0].text.ToString();
            }
        }

        private async Task Translate(bool isReverse)
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
                    if (!isReverse)
                    {
                        outputBox.Text = translatedText;

                        // 仅在源语言不是"自动检测"时才显示反向翻译按钮
                        if (!string.IsNullOrEmpty(source))
                        {
                            reverseTranslateButton.IsEnabled = true;
                            reverseTranslateButton.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            reverseTranslateButton.Visibility = Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        ContentBox.Text = translatedText;
                        reverseTranslateButton.IsEnabled = false;
                    }
                });
            }
        }

        private void ContentBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            reverseTranslateButton.Visibility = Visibility.Collapsed;
        }

        private async void reverseTranslateButton_Click(object sender, RoutedEventArgs e)
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

        // 在设置窗口中保存设置
        public void SaveSequenceSettings()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                var container = localSettings.CreateContainer("TranslationSequence", ApplicationDataCreateDisposition.Always);

                // 保存每个语言项目的设置
                for (int i = 0; i < _translationSequence.Count; i++)
                {
                    if (_translationSequence[i].SelectedLanguage != null)
                    {
                        localSettings.Values[$"TransSeq_{i}_Tag"] = _translationSequence[i].SelectedLanguage.Tag.ToString();
                    }
                }

                localSettings.Values["TranslationSequenceSaved"] = true;
            }
            catch (Exception ex)
            {
                // 记录错误但不中断用户操作
                System.Diagnostics.Debug.WriteLine($"保存翻译序列设置失败: {ex.Message}");
            }
        }

        // 加载保存的翻译序列设置
        private void LoadSequenceSettings()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;

                // 检查是否有保存的设置
                if (localSettings.Values.ContainsKey("TranslationSequenceSaved") &&
                    (bool)localSettings.Values["TranslationSequenceSaved"])
                {
                    // 加载每个语言项目的设置
                    for (int i = 0; i < _translationSequence.Count; i++)
                    {
                        if (localSettings.Values.ContainsKey($"TransSeq_{i}_Tag"))
                        {
                            string savedTag = localSettings.Values[$"TransSeq_{i}_Tag"] as string;
                            if (!string.IsNullOrEmpty(savedTag))
                            {
                                var language = _translationSequence[i].LanguageOptions.FirstOrDefault(l =>
                                    l.Tag.ToString() == savedTag);

                                if (language != null)
                                {
                                    _translationSequence[i].SelectedLanguage = language;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载翻译序列设置失败: {ex.Message}");
            }
        }

        // 确保最后一个翻译步骤返回源语言
        private void EnsureReturnToSource()
        {
            if (_translationSequence.Count > 0 && sourceLanguageComboBox.SelectedItem is ComboBoxItem sourceItem)
            {
                string sourceTag = sourceItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(sourceTag))
                {
                    // 寻找匹配源语言的选项
                    var lastStepOptions = _translationSequence[_translationSequence.Count - 1].LanguageOptions;
                    var returnLanguage = lastStepOptions.FirstOrDefault(l => l.Tag.ToString() == sourceTag);

                    if (returnLanguage != null)
                    {
                        _translationSequence[_translationSequence.Count - 1].SelectedLanguage = returnLanguage;
                    }
                }
            }
        }

        // 重置翻译序列为默认值
        private void ResetSequence()
        {
            for (int i = 0; i < _translationSequence.Count; i++)
            {
                string tagToSelect = "";
                switch (i % 8)
                {
                    case 0: tagToSelect = "en"; break;      // 英语
                    case 1: tagToSelect = "fr"; break;      // 法语
                    case 2: tagToSelect = "es"; break;      // 西班牙语
                    case 3: tagToSelect = "de"; break;      // 德语
                    case 4: tagToSelect = "ja"; break;      // 日语
                    case 5: tagToSelect = "ru"; break;      // 俄语
                    case 6: tagToSelect = "ar"; break;      // 阿拉伯语
                    case 7: tagToSelect = "zh-Hans"; break; // 中文(简体)
                }

                _translationSequence[i].SelectedLanguage = _translationSequence[i].LanguageOptions.FirstOrDefault(l =>
                    l.Tag.ToString() == tagToSelect) ?? _translationSequence[i].LanguageOptions.FirstOrDefault();
            }
        }

        // 获取语言名称
        private string GetLanguageName(string languageTag)
        {
            foreach (ComboBoxItem item in targetLanguageComboBox.Items)
            {
                if (item.Tag.ToString() == languageTag)
                {
                    return item.Content.ToString();
                }
            }
            return languageTag;
        }

        // 在页面离开时保存翻译序列设置
        protected override void OnNavigatingFrom(Windows.UI.Xaml.Navigation.NavigatingCancelEventArgs e)
        {
            SaveSequenceSettings();
            base.OnNavigatingFrom(e);
        }
    }
}
