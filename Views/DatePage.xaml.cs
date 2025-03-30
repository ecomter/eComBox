using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using Windows.Devices.PointOfService;
using Windows.UI.Xaml;
using Windows.Foundation;  // 只保留一个 Windows.Foundation
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static eComBox.Views.DatePage;
using Windows.Storage;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using eComBox.Services;


namespace eComBox.Views
{

    public static class DataStorage
    {
        private static readonly string dateHistoryPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "date_history.json");

        public static async Task SaveDateHistoryAsync(List<UserDateSelection> history)
        {
            var json = JsonConvert.SerializeObject(history);
            await File.WriteAllTextAsync(dateHistoryPath, json);
        }

        public static async Task<List<UserDateSelection>> LoadDateHistoryAsync()
        {
            if (!File.Exists(dateHistoryPath))
            {
                return new List<UserDateSelection>();
            }

            var json = await File.ReadAllTextAsync(dateHistoryPath);
            return JsonConvert.DeserializeObject<List<UserDateSelection>>(json);
        }
        private static readonly string filePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "data.json");

        public static async Task SaveDataAsync(List<DataBlockModel> data)
        {
            var json = JsonConvert.SerializeObject(data);
            var fileLock = new object();
            string tempFilePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "temp_data.json");

            bool fileAccessed = false;
            int retryCount = 0;
            int maxRetries = 3;
            int delay = 1000; // 1 second delay

            while (!fileAccessed && retryCount < maxRetries)
            {
                try
                {
                    lock (fileLock)
                    {
                        using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                        using (var writer = new StreamWriter(fileStream))
                        {
                            writer.Write(json);
                        }
                    }
                    fileAccessed = true;
                }
                catch (IOException)
                {
                    retryCount++;
                    await Task.Delay(delay);
                }
            }

            if (!fileAccessed)
            {
                throw new IOException("Failed to access the file after multiple retries.");
            }

            await Task.Run(() =>
            {
                lock (fileLock)
                {
                    File.Copy(tempFilePath, filePath, true);
                    File.Delete(tempFilePath);
                }
            });
        }

        public static async Task<List<DataBlockModel>> LoadDataAsync()
        {
            if (!File.Exists(filePath))
            {
                return new List<DataBlockModel>();
            }

            var json = await File.ReadAllTextAsync(filePath);
            return JsonConvert.DeserializeObject<List<DataBlockModel>>(json);
        }
    }

    public sealed partial class DatePage : Page, INotifyPropertyChanged
    {
        
        public int ColMax = 1, title = 0;
        private ContentDialog editDialog;
        private TextBox dialogTaskNameBox;
        private CalendarDatePicker dialogDatePicker;
        private DataBlock currentEditingBlock;
        private void InitializeEditDialog()
        {
            editDialog = new ContentDialog()
            {
                Title = "编辑事件",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                SecondaryButtonText = "清除日期",
                DefaultButton = ContentDialogButton.Primary,
            };

            // 创建一个ScrollViewer作为主容器
            ScrollViewer scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = ScrollMode.Disabled,
                VerticalScrollMode = ScrollMode.Auto,
                ZoomMode = ZoomMode.Disabled,
                Padding = new Thickness(0, 0, 4, 0),  // 右侧添加一点padding，为滚动条留出空间
                MaxHeight = 550  // 设置最大高度，确保在小屏幕上也能正常显示
            };

            // 创建Grid作为ScrollViewer的内容
            Grid dialogContent = new Grid();
            dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 创建建议面板
            _suggestionPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(8),
                Visibility = Visibility.Collapsed,
                BorderThickness = new Thickness(1),  // 添加边框到整个面板
                CornerRadius = new CornerRadius(6),  // 为面板添加圆角
                Padding = new Thickness(10, 8, 10, 12)
            };

            // 根据主题设置不同的样式
            if (Application.Current.RequestedTheme == ApplicationTheme.Dark)
            {
                // 深色主题下的科技风格渐变边框
                var borderBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };
                borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 64, 128, 255), Offset = 0.0 });
                borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(200, 100, 80, 255), Offset = 1.0 });
                _suggestionPanel.BorderBrush = borderBrush;

                // 为建议面板添加微妙的背景
                _suggestionPanel.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 50, 80, 180));
            }
            else
            {
                // 浅色主题下的科技风格渐变边框
                var borderBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };
                borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 0, 120, 215), Offset = 0.0 });
                borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 80, 100, 230), Offset = 1.0 });
                _suggestionPanel.BorderBrush = borderBrush;

                // 为建议面板添加微妙的背景
                _suggestionPanel.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(15, 0, 120, 215));
            }

            // 创建标题栏，直接使用StackPanel而不是使用Border
            StackPanel headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };

            TextBlock aiIcon = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Text = "\uE1E3", // AI/机器学习图标
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Application.Current.RequestedTheme == ApplicationTheme.Dark
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 180, 255))
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 215))
            };

            TextBlock suggestionLabel = new TextBlock
            {
                Text = "AI 推荐日期",
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };

            headerPanel.Children.Add(aiIcon);
            headerPanel.Children.Add(suggestionLabel);
            _suggestionPanel.Children.Add(headerPanel);

            // 为推荐日期创建内容容器
            StackPanel suggestionContent = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(4, 0, 4, 0)
            };
            _suggestionPanel.Children.Add(suggestionContent);

            Grid.SetRow(_suggestionPanel, 7);
            Grid.SetColumn(_suggestionPanel, 0);
            dialogContent.Children.Add(_suggestionPanel);


            TextBlock taskNameLabel = new TextBlock
            {
                Text = "事件名称:",
                Margin = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(taskNameLabel, 0);
            Grid.SetColumn(taskNameLabel, 0);

            dialogTaskNameBox = new TextBox
            {
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 18
            };
            Grid.SetRow(dialogTaskNameBox, 1);
            Grid.SetColumn(dialogTaskNameBox, 0);

            TextBlock dateLabel = new TextBlock
            {
                Text = "目标日期:",
                Margin = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(dateLabel, 2);
            Grid.SetColumn(dateLabel, 0);

            dialogDatePicker = new CalendarDatePicker
            {
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FontSize = 18,
                PlaceholderText = "",
                DateFormat = "{year.full}年{month.integer}月{day.integer}日"
            };
            Grid.SetRow(dialogDatePicker, 3);
            Grid.SetColumn(dialogDatePicker, 0);

            // 添加快速未来日期选择
            TextBlock quickDaysLabel = new TextBlock
            {
                Text = "快速设置:",
                Margin = new Thickness(8, 16, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(quickDaysLabel, 4);
            Grid.SetColumn(quickDaysLabel, 0);
            dialogContent.Children.Add(quickDaysLabel);

            // 创建一个水平StackPanel放置快速日期按钮
            var quickDaysPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 0, 8, 8),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(quickDaysPanel, 5);
            Grid.SetColumn(quickDaysPanel, 0);

            // 添加常用的天数按钮
            int[] commonDays = { 1, 3, 7, 14, 30, 90 };
            foreach (int days in commonDays)
            {
                Button dayButton = new Button
                {
                    Content = $"{days}天后",
                    Margin = new Thickness(4),
                    Padding = new Thickness(8, 4, 8, 4),
                    MinWidth = 60
                };

                dayButton.Click += (s, e) =>
                {
                    // 设置日期为当前日期加上指定天数
                    dialogDatePicker.Date = DateTime.Now.Date.AddDays(days);
                };

                quickDaysPanel.Children.Add(dayButton);
            }

            // 添加自定义按钮
            Button customDaysButton = new Button
            {
                Content = "自定义",
                Margin = new Thickness(4),
                Padding = new Thickness(8, 4, 8, 4),
                MinWidth = 50
            };
            quickDaysPanel.Children.Add(customDaysButton);
            dialogContent.Children.Add(quickDaysPanel);

            // 创建自定义天数面板，默认隐藏
            var customDaysPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 0, 8, 16),
                HorizontalAlignment = HorizontalAlignment.Left,
                Visibility = Visibility.Collapsed // 初始为隐藏状态
            };
            Grid.SetRow(customDaysPanel, 6);
            Grid.SetColumn(customDaysPanel, 0);
            dialogContent.Children.Add(customDaysPanel);

            var customDaysBox = new NumberBox
            {
                Margin = new Thickness(4),
                MinWidth = 100,
                Header = "自定义天数",
                PlaceholderText = "输入天数",
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Minimum = 1,
                Maximum = 3650, // 约10年
                Value = 7,
                SmallChange = 1,
                LargeChange = 10
            };

            Button applyCustomDays = new Button
            {
                Content = "应用",
                Margin = new Thickness(8, 24, 4, 0),
                Padding = new Thickness(12, 4, 12, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            customDaysPanel.Children.Add(customDaysBox);
            customDaysPanel.Children.Add(applyCustomDays);

            // 自定义天数按钮点击事件
            customDaysButton.Click += (s, e) =>
            {
                // 切换自定义天数面板的可见性
                customDaysPanel.Visibility = customDaysPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            // 应用自定义天数按钮点击事件
            applyCustomDays.Click += (s, e) =>
            {
                int days = (int)customDaysBox.Value;
                if (days > 0)
                {
                    dialogDatePicker.Date = DateTime.Now.Date.AddDays(days);
                    // 应用后隐藏自定义面板
                    customDaysPanel.Visibility = Visibility.Collapsed;
                }
            };

            dialogContent.Children.Add(taskNameLabel);
            dialogContent.Children.Add(dialogTaskNameBox);
            dialogContent.Children.Add(dateLabel);
            dialogContent.Children.Add(dialogDatePicker);

            dialogTaskNameBox.TextChanged += async (s, e) =>
            {
                await UpdateDateSuggestionsAsync(dialogTaskNameBox.Text);
            };

            // 把Grid设置为ScrollViewer的内容
            scrollViewer.Content = dialogContent;

            // 将ScrollViewer设置为对话框的内容
            editDialog.Content = scrollViewer;
        }
        private async Task UpdateDateSuggestionsAsync(string taskName)
        {
            try
            {
                // 任务名称太短则不预测
                if (string.IsNullOrWhiteSpace(taskName) || taskName.Length < 2)
                {
                    _suggestionPanel.Visibility = Visibility.Collapsed;
                    return;
                }

                // 调用 Azure AI 获取预测
                var predictionResult = await _predictionService.PredictDateFromTaskNameAsync(taskName);

                // 获取或创建内容面板（第二个子元素是内容面板）
                StackPanel suggestionContent = _suggestionPanel.Children.Count > 1 && _suggestionPanel.Children[1] is StackPanel
                    ? _suggestionPanel.Children[1] as StackPanel
                    : new StackPanel { Orientation = Orientation.Vertical };

                // 清除当前建议
                suggestionContent.Children.Clear();

                if (predictionResult != null && predictionResult.IsSuccessful && predictionResult.GetSortedSuggestions().Count > 0)
                {
                    _suggestionPanel.Visibility = Visibility.Visible;

                    // 添加表示AI思考中的小提示文本
                    TextBlock aiProcessing = new TextBlock
                    {
                        Text = "根据事件名称分析，以下日期可能与您的计划相关:",
                        FontStyle = Windows.UI.Text.FontStyle.Italic,
                        Opacity = 0.7,
                        Margin = new Thickness(2, 4, 2, 8),
                        TextWrapping = TextWrapping.Wrap
                    };
                    suggestionContent.Children.Add(aiProcessing);

                    // 为每个预测创建按钮
                    foreach (var suggestion in predictionResult.GetSortedSuggestions())
                    {
                        // 验证建议是否有效
                        if (suggestion == null || suggestion.SuggestedDate == DateTime.MinValue)
                        {
                            continue;
                        }

                        // 创建建议容器
                        Border suggestionBorder = new Border
                        {
                            BorderThickness = new Thickness(0, 0, 0, 1),
                            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)),
                            Padding = new Thickness(0, 4, 0, 8),
                            Margin = new Thickness(0, 0, 0, 6)
                        };

                        Button suggestionButton = new Button
                        {
                            Margin = new Thickness(0),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            Padding = new Thickness(8, 6, 8, 6),
                            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(10, 0, 120, 215))
                        };

                        // 给按钮添加鼠标悬停效果
                        suggestionButton.PointerEntered += (s, e) =>
                        {
                            suggestionButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 0, 120, 215));
                        };
                        suggestionButton.PointerExited += (s, e) =>
                        {
                            suggestionButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(10, 0, 120, 215));
                        };

                        // 按钮内容
                        StackPanel buttonContent = new StackPanel
                        {
                            Orientation = Orientation.Vertical // 改为垂直布局，避免文本截断
                        };

                        // 日期和信心度区域
                        Grid dateGrid = new Grid();
                        dateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        dateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        TextBlock dateText = new TextBlock
                        {
                            Text = suggestion.SuggestedDate.ToString("yyyy年MM月dd日"),
                            FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                            Margin = new Thickness(0, 0, 0, 4)
                        };
                        Grid.SetColumn(dateText, 0);
                        dateGrid.Children.Add(dateText);

                        // 添加一个标签显示置信度或相关性
                        TextBlock confidenceText = new TextBlock
                        {
                            Text = suggestion.Confidence > 0.8 ? "高相关" : (suggestion.Confidence > 0.5 ? "相关" : "可能相关"),
                            FontSize = 12,
                            Opacity = 0.6,
                            Margin = new Thickness(0, 0, 0, 4),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        Grid.SetColumn(confidenceText, 1);
                        dateGrid.Children.Add(confidenceText);

                        buttonContent.Children.Add(dateGrid);

                        // 确保 Reason 不为空
                        string reason = suggestion.Reason ?? "推荐日期";
                        TextBlock reasonText = new TextBlock
                        {
                            Text = reason,
                            Opacity = 0.7,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 280,
                            FontSize = 13
                        };
                        buttonContent.Children.Add(reasonText);

                        suggestionButton.Content = buttonContent;

                        // 点击按钮设置日期
                        suggestionButton.Click += (s, e) =>
                        {
                            try
                            {
                                dialogDatePicker.Date = suggestion.SuggestedDate;
                            }
                            catch (Exception clickEx)
                            {
                                Debug.WriteLine($"设置日期时出错: {clickEx.Message}");
                            }
                        };

                        suggestionBorder.Child = suggestionButton;
                        suggestionContent.Children.Add(suggestionBorder);
                    }

                    // 添加一个重新显示内容的小动画，增强用户体验
                    var contentVisual = ElementCompositionPreview.GetElementVisual(suggestionContent);
                    var compositor = contentVisual.Compositor;

                    var fadeInAnimation = compositor.CreateScalarKeyFrameAnimation();
                    fadeInAnimation.InsertKeyFrame(0f, 0.6f);
                    fadeInAnimation.InsertKeyFrame(1f, 1f);
                    fadeInAnimation.Duration = TimeSpan.FromMilliseconds(300);

                    contentVisual.Opacity = 0.6f;
                    contentVisual.StartAnimation("Opacity", fadeInAnimation);

                    // 确保内容面板已添加
                    if (_suggestionPanel.Children.Count <= 1)
                    {
                        _suggestionPanel.Children.Add(suggestionContent);
                    }
                }
                else
                {
                    _suggestionPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新预测错误: {ex.Message}");
                Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
                _suggestionPanel.Visibility = Visibility.Collapsed;
            }
        }
        public class DataBlockModel
        {
            public int Title { get; set; }
            public string TaskName { get; set; }
            public DateTime? TargetDate { get; set; }
            public string DisplayText { get; set; }
        }

        public class DataBlock : Grid
        {
            // elements
            public int title;
            public StackPanel stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            public Button button1 = new Button
            {
                Margin = new Thickness(8, 0, 20, 0)
            };
            public TextBlock button1Text = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Text = "\uE72B", // Unicode 字符
                FontSize = 18
            };
            public TextBlock textBlock1 = new TextBlock
            {
                Margin = new Thickness(5, 11, 5, 0),
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Text = "\uE70F", // Unicode 字符
                FontSize = 25
            };
            public TextBlock textBlock2 = new TextBlock
            {
                Margin = new Thickness(5),
                FontSize = 25
            };
            public TextBlock textBlock3 = new TextBlock
            {
                Margin = new Thickness(8),
                Text = "事件名称:",
                FontSize = 18
            };
            public TextBox textBox = new TextBox
            {
                Name = "TaskName",
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 18
            };
            public TextBlock textBlock4 = new TextBlock
            {
                Margin = new Thickness(8),
                Text = "目标日期:",
                FontSize = 18
            };
            public CalendarDatePicker datePicker = new CalendarDatePicker
            {
                Name = "DatePicker",
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FontSize = 18,
                PlaceholderText = "",
                DateFormat = "{year.full}年{month.integer}月{day.integer}日"
            };

            public Button button2 = new Button
            {
                Height = 40,
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            public StackPanel button2StackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            public TextBlock button2Text1 = new TextBlock
            {
                Margin = new Thickness(0, 2, 5, 0),
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Text = "\uE73E", // Unicode 字符
                FontSize = 18
            };
            public TextBlock button2Text2 = new TextBlock
            {
                Text = "确定"
            };
            public Button button1r = new Button
            {
                Margin = new Thickness(8, 10,8 , 0),

            };

            public TextBlock button1Textr = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Text = "\uE70F", // Unicode 字符
                FontSize = 18
            };
            public TextBlock textBlock5 = new TextBlock
            {
                Margin = new Thickness(8),
                FontSize = 18,
                TextAlignment = TextAlignment.Center,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
            };
            public Button button3 = new Button
            {
                Height = 40,
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Stretch
             
            };
            public StackPanel button3StackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            
            private readonly Action saveDataAction;

            // 修改DataBlock构造函数中的背景和边框设置
            private ElementTheme _elementTheme = ThemeSelectorService.Theme;
            public DataBlock(int title, Action saveDataAction)
            {
               
                this.Name = "DataBlock" + title;
                this.Margin = new Thickness(6, 10, 6, 10);
                this.BorderThickness = new Thickness(1);
                this.Padding = new Thickness(12);
                this.CornerRadius = new CornerRadius(8);
                this.MinHeight = 223;
                this.MaxWidth = 380;
                this.Height = Double.NaN;
                this.title = title;
                this.saveDataAction = saveDataAction;
                this.CanDrag = true;
                this.DragStarting += DataBlock_DragStarting;

           
                this.Background = (Brush)Application.Current.Resources["AcrylicBackgroundFillColorDefaultBrush"];


                // 创建更精细的阴影效果
                var visual = ElementCompositionPreview.GetElementVisual(this);
                var compositor = visual.Compositor;

                // 创建阴影
                var dropShadow = compositor.CreateDropShadow();
                dropShadow.BlurRadius = 30.0f;
                dropShadow.Opacity = 0.05f;
                dropShadow.Color = Windows.UI.Colors.Black;
                dropShadow.Offset = new Vector3(0, 4, 0);

                var ambientShadow = compositor.CreateDropShadow();
                // 添加更自然的环境光阴影
        
                    
                    ambientShadow.BlurRadius = 30.0f;
                    ambientShadow.Opacity = _elementTheme==ElementTheme.Light ? 0.3f : 0.1f;
                ambientShadow.Color = Windows.UI.Colors.Black;
                    ambientShadow.Offset = new Vector3(0.5f, 0, 0);
               
               
                   

                // 创建阴影视觉对象并将其附加到元素
                var shadowVisual = compositor.CreateSpriteVisual();
                shadowVisual.Size = new Vector2((float)this.ActualWidth, (float)this.ActualHeight);
                shadowVisual.Shadow = dropShadow;
                var ambientShadowVisual = compositor.CreateSpriteVisual();
                ambientShadowVisual.Size = new Vector2((float)this.ActualWidth, (float)this.ActualHeight);
                ambientShadowVisual.Shadow = ambientShadow;

                // 将阴影视觉对象连接到元素
                ElementCompositionPreview.SetElementChildVisual(this, shadowVisual);

                // 更新阴影大小
                this.SizeChanged += (s, e) =>
                {
                    shadowVisual.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
                    ambientShadowVisual.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
                };

                // 添加轻微的变换效果，增加层次感
                visual.Scale = new Vector3(1, 1, 1);

                // 添加鼠标悬停效果
                this.PointerEntered += (s, e) =>
                {
                    var pointerEnterAnimation = compositor.CreateVector3KeyFrameAnimation();
                    pointerEnterAnimation.InsertKeyFrame(0f, new Vector3(1, 1, 1));
                    pointerEnterAnimation.InsertKeyFrame(1f, new Vector3(1.02f, 1.02f, 1f));
                    pointerEnterAnimation.Duration = TimeSpan.FromMilliseconds(200);

                    var shadowAnimation = compositor.CreateScalarKeyFrameAnimation();
                    shadowAnimation.InsertKeyFrame(0f, 20.0f);
                    shadowAnimation.InsertKeyFrame(1f, 28.0f);
                    shadowAnimation.Duration = TimeSpan.FromMilliseconds(200);

                    var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
                    offsetAnimation.InsertKeyFrame(0f, new Vector3(0, 4, 0));
                    offsetAnimation.InsertKeyFrame(1f, new Vector3(0, 6, 0));
                    offsetAnimation.Duration = TimeSpan.FromMilliseconds(200);

                    visual.StartAnimation("Scale", pointerEnterAnimation);
                    dropShadow.StartAnimation("BlurRadius", shadowAnimation);
                    dropShadow.StartAnimation("Offset", offsetAnimation);
                };

                this.PointerExited += (s, e) =>
                {
                    var pointerExitAnimation = compositor.CreateVector3KeyFrameAnimation();
                    pointerExitAnimation.InsertKeyFrame(0f, new Vector3(1.02f, 1.02f, 1f));
                    pointerExitAnimation.InsertKeyFrame(1f, new Vector3(1, 1, 1));
                    pointerExitAnimation.Duration = TimeSpan.FromMilliseconds(200);

                    var shadowAnimation = compositor.CreateScalarKeyFrameAnimation();
                    shadowAnimation.InsertKeyFrame(0f, 28.0f);
                    shadowAnimation.InsertKeyFrame(1f, 20.0f);
                    shadowAnimation.Duration = TimeSpan.FromMilliseconds(200);

                    var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
                    offsetAnimation.InsertKeyFrame(0f, new Vector3(0, 6, 0));
                    offsetAnimation.InsertKeyFrame(1f, new Vector3(0, 4, 0));
                    offsetAnimation.Duration = TimeSpan.FromMilliseconds(200);

                    visual.StartAnimation("Scale", pointerExitAnimation);
                    dropShadow.StartAnimation("BlurRadius", shadowAnimation);
                    dropShadow.StartAnimation("Offset", offsetAnimation);
                };
            }
            
            // 修改为完全匹配的事件处理程序签名
            private void DataBlock_DragStarting(UIElement sender, Windows.UI.Xaml.DragStartingEventArgs e)
            {
                try
                {
                    // 不传递完整对象，而是传递唯一 ID 或者索引
                    // 将 DataBlock 的 ID 或唯一标识符作为字符串传递
                    e.Data.SetData("DataBlockId", this.title.ToString());

                    // 设置拖拽提示
                    e.Data.Properties.Title = textBox.Text ?? "拖动卡片";
                    e.Data.Properties.Description = "拖放到新位置";

                    // 设置拖拽视觉效果
                    e.DragUI.SetContentFromBitmapImage(GetDragUIContent());

                    this.Opacity = 0.7; // 使原始卡片半透明

                    var timer = new DispatcherTimer();
                    timer.Interval = TimeSpan.FromMilliseconds(800);
                    timer.Tick += (s, args) =>
                    {
                        this.Opacity = 1.0;
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    // 记录异常，不让应用崩溃
                    Debug.WriteLine($"拖拽开始错误: {ex.Message}");
                }
            }

            private Windows.UI.Xaml.Media.Imaging.BitmapImage GetDragUIContent()
            {
                // 简单实现，实际项目中可以创建卡片的截图
                var image = new Windows.UI.Xaml.Media.Imaging.BitmapImage();
                return image;
            }
            
            public void animateGrid(System.Action action)
            {
                // 创建连接到Composition API的对象
                var visual = ElementCompositionPreview.GetElementVisual(this);
                var compositor = visual.Compositor;

                // 简化的淡出动画
                var fadeOutAnimation = compositor.CreateScalarKeyFrameAnimation();
                fadeOutAnimation.InsertKeyFrame(0f, 1f);
                fadeOutAnimation.InsertKeyFrame(1f, 0.5f);
                fadeOutAnimation.Duration = TimeSpan.FromMilliseconds(150);

                // 应用淡出动画
                visual.StartAnimation("Opacity", fadeOutAnimation);

                // 使用Timer等待动画完成
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(150);
                timer.Tick += (s, e) =>
                {
                    timer.Stop();

                    // 执行操作
                    action.Invoke();

                    // 创建淡入动画
                    var fadeInAnimation = compositor.CreateScalarKeyFrameAnimation();
                    fadeInAnimation.InsertKeyFrame(0f, 0.5f);
                    fadeInAnimation.InsertKeyFrame(1f, 1f);
                    fadeInAnimation.Duration = TimeSpan.FromMilliseconds(150);

                    // 应用淡入动画
                    visual.StartAnimation("Opacity", fadeInAnimation);
                };
                timer.Start();
            }


            public void expressGrid()
            {
                button3StackPanel.Children.Clear();
                stackPanel.Children.Clear();
                button2StackPanel.Children.Clear();
                DateTime ArrTime = new DateTime(datePicker.Date.Value.Year, datePicker.Date.Value.Month, datePicker.Date.Value.Day);
                DateTime NowTime = DateTime.Now.Date;
                TimeSpan diffTime = ArrTime.Subtract(NowTime);

                this.ColumnDefinitions.Clear();
                this.RowDefinitions.Clear();
                this.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                this.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                this.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                button1r.Content = button1Textr;
                button1r.SetValue(Grid.RowProperty, 0);

                textBlock5.SetValue(Grid.RowProperty, 1);

                if (string.IsNullOrEmpty(textBox.Text))
                {
                    textBox.Text = $"{datePicker.Date.Value.Year}年{datePicker.Date.Value.Month}月{datePicker.Date.Value.Day}日";
                }
                else
                {
                    textBlock2.Text = textBox.Text;
                }

                if (diffTime.Days > 0)
                {
                    textBlock5.Text = $"今天距离{textBox.Text}还有";
                }
                else if (diffTime.Days < 0)
                {
                    textBlock5.Text = $"今天距离{textBox.Text}已经过去了";
                }

                var textBlock6 = new TextBlock
                {
                    Margin = new Thickness(8),
                    FontSize = 40,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
                    Text = $"{Math.Abs(diffTime.Days)}天"
                };
                // 修改expressGrid方法中的当天事件高亮部分
                if (diffTime.Days == 0)
                {
                    textBlock5.Text = "今天就是";
                    textBlock6.Text = $"{textBox.Text}";

                    // 当天事件使用更吸引人的渐变色边框
                    if (Application.Current.RequestedTheme == ApplicationTheme.Dark)
                    {
                        // 深色主题中的高亮边框 - 使用更明亮的颜色
                        var borderBrush = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1)
                        };
                        borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 90, 150, 230), Offset = 0.0 });
                        borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 170, 110, 240), Offset = 1.0 });
                        this.BorderBrush = borderBrush;
                    }
                    else
                    {
                        // 浅色主题中的高亮边框 - 使用更活泼的颜色
                        var borderBrush = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1)
                        };
                        borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 50, 140, 240), Offset = 0.0 });
                        borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 150, 80, 240), Offset = 1.0 });
                        this.BorderBrush = borderBrush;
                    }

                    this.BorderThickness = new Thickness(2);
                
            }


                textBlock6.SetValue(Grid.RowProperty, 2);
                this.Children.Clear();
                this.Children.Add(button1r);
                this.Children.Add(textBlock5);
                this.Children.Add(textBlock6);

                button1r.Click += (sender, e) =>
                {
                    showEditGrid();
                };
            }

            public void editGrid()
            {
                button3StackPanel.Children.Clear();
                stackPanel.Children.Clear();
                button2StackPanel.Children.Clear();

                // 定义行和列
                this.ColumnDefinitions.Clear();
                this.RowDefinitions.Clear();
                this.Children.Clear();

                // 添加两行，每行显示一个按钮
                this.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                this.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // 创建一个按钮用于编辑
                Button editButton = new Button
                {
                    Content = "编辑事件",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(8)
                };
                editButton.SetValue(Grid.RowProperty, 0);  // 设置为第一行
                editButton.SetValue(StyleProperty, Application.Current.Resources["AccentButtonStyle"]);
                this.Children.Add(editButton);

                // 清除按钮
                Button clearButton = new Button
                {
                    Content = "清除日期",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(8)
                };
                clearButton.SetValue(Grid.RowProperty, 1);  // 设置为第二行
                this.Children.Add(clearButton);

                // 编辑按钮点击事件
                editButton.Click += (sender, e) => showEditGrid();

                // 清除按钮点击事件
                clearButton.Click += async (sender, e) =>
                {
                    // 清除当前 DataBlock 的内容
                    datePicker.Date = null;
                    textBox.Text = "";

                    // 从 ContentArea 中移除当前 DataBlock
                    Panel parentPanel = null;
                    if (this.Parent is Panel panel)
                    {
                        parentPanel = panel;
                    }
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        if (parentPanel != null)
                        {
                            parentPanel.Children.Remove(this);
                        }
                    });

                    // 重新保存数据
                    try
                    {
                        if (saveDataAction != null)
                        {
                            await Task.Run(() => saveDataAction.Invoke());
                        }
                    }
                    catch
                    {
                        ContentDialog dialog = new ContentDialog()
                        {
                            Title = "警告",
                            Content = "文件读写出现错误，不过您还可以使用本软件，但是会丢失保存配置功能。点击删除全部可能会解决此问题",
                            CloseButtonText = "知道了",
                            DefaultButton = ContentDialogButton.Close
                        };

                        await dialog.ShowAsync();
                    }
                };
            }
            private static object _editLock = new object();
            private static bool _isEditing = false;

            public async Task<ContentDialogResult> ShowEditDialogAsync()
            {
                // 添加互斥锁，防止并发编辑
                if (_isEditing)
                {
                    return ContentDialogResult.None; // 如果已经在编辑，直接返回
                }

                try
                {
                    _isEditing = true;

                    var page = GetDatePage();
                    if (page.editDialog == null)
                    {
                        page.InitializeEditDialog();
                    }

                    page.currentEditingBlock = this;
                    page.dialogTaskNameBox.Text = this.textBox.Text;
                    page.dialogDatePicker.Date = this.datePicker.Date;

                    var result = await page.editDialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                     
                        
                      
                        // 用户点击了确定按钮，更新DataBlock数据
                        this.textBox.Text = page.dialogTaskNameBox.Text;
                        this.datePicker.Date = page.dialogDatePicker.Date;

                        if (this.datePicker.Date != null)
                        {
                            
                            // 使用简化版的动画方法，避免动画冲突
                            await SafeAnimateGridAsync(delegate () { expressGrid(); });

                            try
                            {
                                // 确保SaveData操作完成后再返回
                                await page.SaveData();
                            }
                            catch (Exception ex)
                            {
                                ContentDialog errorDialog = new ContentDialog()
                                {
                                    Title = "警告",
                                    Content = "文件读写出现错误，不过您还可以使用本软件，但是会丢失保存配置功能。点击删除全部可能会解决此问题",
                                    CloseButtonText = "知道了",
                                    DefaultButton = ContentDialogButton.Close
                                };

                                await errorDialog.ShowAsync();
                            }

                            // 处理显示动画
                            if (this.Visibility == Visibility.Collapsed)
                            {
                                this.Visibility = Visibility.Visible;

                                // 使用更简单的动画，避免复杂度
                                var visual = ElementCompositionPreview.GetElementVisual(this);
                                visual.Opacity = 0f;

                                var fadeInAnimation = visual.Compositor.CreateScalarKeyFrameAnimation();
                                fadeInAnimation.InsertKeyFrame(0f, 0f);
                                fadeInAnimation.InsertKeyFrame(1f, 1f);
                                fadeInAnimation.Duration = TimeSpan.FromMilliseconds(300);

                                visual.StartAnimation("Opacity", fadeInAnimation);
                            }
                        }
                    }
                    else if (result == ContentDialogResult.Secondary)
                    {
                        // 用户点击了"清除日期"按钮
                        // 清除当前 DataBlock 的内容
                        this.datePicker.Date = null;
                        this.textBox.Text = "";

                        // 从 ContentArea 中移除当前 DataBlock
                        Panel parentPanel = null;
                        if (this.Parent is Panel panel)
                        {
                            parentPanel = panel;
                        }
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            if (parentPanel != null)
                            {
                                parentPanel.Children.Remove(this);

                                // 在删除后立即更新网格布局，确保空出来的位置能被其他卡片填补
                                if (page != null)
                                {
                                    page.UpdateGridLayout();
                                }
                            }
                        });

                        // 重新保存数据
                        try
                        {
                            if (saveDataAction != null)
                            {
                                await Task.Run(() => saveDataAction.Invoke());
                            }
                        }
                        catch
                        {
                            ContentDialog dialog = new ContentDialog()
                            {
                                Title = "警告",
                                Content = "文件读写出现错误，不过您还可以使用本软件，但是会丢失保存配置功能。点击删除全部可能会解决此问题",
                                CloseButtonText = "知道了",
                                DefaultButton = ContentDialogButton.Close
                            };

                            await dialog.ShowAsync();
                        }
                    }

                    return result;
                }
                finally
                {
                    _isEditing = false; // 确保编辑状态被重置
                }
            }

            private async Task SafeAnimateGridAsync(System.Action action)
            {
                // 创建连接到Composition API的对象
                var visual = ElementCompositionPreview.GetElementVisual(this);
                var compositor = visual.Compositor;

                // 简化的淡出动画
                var fadeOutAnimation = compositor.CreateScalarKeyFrameAnimation();
                fadeOutAnimation.InsertKeyFrame(0f, 1f);
                fadeOutAnimation.InsertKeyFrame(1f, 0.3f);
                fadeOutAnimation.Duration = TimeSpan.FromMilliseconds(200);

                // 应用淡出动画
                visual.StartAnimation("Opacity", fadeOutAnimation);

                // 等待动画完成
                await Task.Delay(200);

                // 执行操作
                action.Invoke();

                // 创建淡入动画
                var fadeInAnimation = compositor.CreateScalarKeyFrameAnimation();
                fadeInAnimation.InsertKeyFrame(0f, 0.3f);
                fadeInAnimation.InsertKeyFrame(1f, 1f);
                fadeInAnimation.Duration = TimeSpan.FromMilliseconds(200);

                // 应用淡入动画
                visual.StartAnimation("Opacity", fadeInAnimation);
            }
            private DatePage GetDatePage()
            {
                // 通过VisualTreeHelper查找父元素
                DependencyObject parent = VisualTreeHelper.GetParent(this);
                while (parent != null && !(parent is DatePage))
                {
                    parent = VisualTreeHelper.GetParent(parent);
                }

                return parent as DatePage;
            }
            public void showEditGrid()
            {
                if (_isEditing) return;
                // 查找DatePage实例的正确方式
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    await ShowEditDialogAsync();
                });
            }

        }

        private AzureDatePredictionService _predictionService;
        private StackPanel _suggestionPanel;
        public DatePage()
        {
            InitializeComponent();
            _predictionService = new AzureDatePredictionService(
        "https://ai-jinqiaoli1752ai485205845953.cognitiveservices.azure.com/",
        "F37Fkmz1W7kD8veNTpU35sG6HOcU0f84zFr52LBsmbmE0IEPNgVhJQQJ99ALACHYHv6XJ3w3AAAAACOGdqmg");
            ContentArea.SizeChanged += ContentArea_SizeChanged;
            ContentArea_SizeChanged(ContentArea, null);

            // 启用ContentArea的拖放功能
            ContentArea.AllowDrop = true;
            ContentArea.DragOver += ContentArea_DragOver;
            ContentArea.Drop += ContentArea_Drop;

            // 在页面加载时添加一个 DataBlock
            LoadData();
        }
        private void ContentArea_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        
        // 获取拖拽位置，判断目标位置
        var position = e.GetPosition(ContentArea);
        
        // 更新拖拽位置指示器效果
        UpdateDragPositionIndicator(position);
    }
    
    // 更新拖拽位置指示器
    private int currentDragTargetIndex = -1;
    private void UpdateDragPositionIndicator(Point position)
    {
        int columnsForItems = ColMax;
        int childCount = ContentArea.Children.Count;
        
        // 计算目标索引
        int col = Math.Min(Math.Max((int)(position.X / (ContentArea.ActualWidth / columnsForItems)), 0), columnsForItems - 1);
        int row = 0;
        
        // 计算行数
        double accumulatedHeight = 0;
        int itemsPerRow = Math.Min(columnsForItems, childCount);
        for (int i = 0; i < childCount; i += itemsPerRow)
        {
            int itemsInThisRow = Math.Min(itemsPerRow, childCount - i);
            double rowHeight = 0;
            
            // 找出当前行中最高的项目
            for (int j = 0; j < itemsInThisRow; j++)
            {
                if (i + j < childCount && ContentArea.Children[i + j] is FrameworkElement element)
                {
                    rowHeight = Math.Max(rowHeight, element.ActualHeight);
                }
            }
            
            accumulatedHeight += rowHeight + 20; // 20是项目之间的垂直间距
            
            if (position.Y < accumulatedHeight)
            {
                row = i / itemsPerRow;
                break;
            }
        }
        
        // 计算目标索引
        int targetIndex = row * columnsForItems + col;
        if (targetIndex >= childCount) targetIndex = childCount - 1;
        
        // 更新当前拖拽目标索引
        if (targetIndex != currentDragTargetIndex)
        {
            currentDragTargetIndex = targetIndex;
            
            // 在实际应用中，可以添加视觉提示，比如高亮显示目标位置
        }
    }
    
// 处理放置事件
        private async void ContentArea_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.DataView.Contains("DataBlockId"))
                {
                    // 获取 DataBlock 的 ID
                    string blockIdString = await e.DataView.GetDataAsync("DataBlockId") as string;

                    if (!string.IsNullOrEmpty(blockIdString) && int.TryParse(blockIdString, out int blockId))
                    {
                        // 在 ContentArea 中查找对应的 DataBlock
                        DataBlock sourceBlock = null;
                        int sourceIndex = -1;

                        for (int i = 0; i < ContentArea.Children.Count; i++)
                        {
                            if (ContentArea.Children[i] is DataBlock block && block.title == blockId)
                            {
                                sourceBlock = block;
                                sourceIndex = i;
                                break;
                            }
                        }

                        if (sourceBlock != null && sourceIndex >= 0 && currentDragTargetIndex >= 0
                            && sourceIndex != currentDragTargetIndex)
                        {
                            // 移动卡片
                            ContentArea.Children.RemoveAt(sourceIndex);

                            // 计算目标索引
                            int targetIndex = (currentDragTargetIndex > sourceIndex)
                                ? currentDragTargetIndex - 1
                                : currentDragTargetIndex;

                            if (targetIndex >= ContentArea.Children.Count)
                            {
                                ContentArea.Children.Add(sourceBlock);
                            }
                            else
                            {
                                ContentArea.Children.Insert(targetIndex, sourceBlock);
                            }

                            // 更新布局
                            UpdateGridLayout();

                            // 保存新的排序
                            await SaveData();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录异常信息
                Debug.WriteLine($"拖放操作出错: {ex.Message}");

                // 显示友好的错误消息
                ContentDialog dialog = new ContentDialog
                {
                    Title = "操作失败",
                    Content = "拖放操作未能完成，请重试。",
                    CloseButtonText = "确定"
                };

                await dialog.ShowAsync();
            }
            finally
            {
                // 重置当前拖拽目标索引
                currentDragTargetIndex = -1;
            }
        }
        private void ContentArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double gridWidth = ContentArea.ActualWidth;
            int desiredColumnWidth = 360;

            if (gridWidth > 0)
            {
                int newColMax = Math.Max(1, (int)(gridWidth / desiredColumnWidth));
                Debug.WriteLine($"计算得到的列数: {newColMax}");

                if (newColMax != ColMax)
                {
                    ColMax = newColMax;
                    UpdateGridLayout();
                }
            }
        }

        // 修复UpdateGridLayout方法，恢复原始布局逻辑，同时保留改进
        private void UpdateGridLayout()
        {
            ContentArea.ColumnDefinitions.Clear();
            ContentArea.RowDefinitions.Clear();

            // 添加列定义
            for (int i = 0; i < ColMax; i++)
            {
                // 使用Star而不是固定宽度，确保列可以适应屏幕宽度
                ContentArea.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });
            }

            int childCount = ContentArea.Children.Count;
            int columnsForItems = ColMax;
            int rowCount = (int)Math.Ceiling((double)childCount / columnsForItems);

            // 添加行定义
            for (int i = 0; i < rowCount; i++)
            {
                ContentArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // 重新排列子元素
            // 重新排列子元素
            for (int i = 0; i < childCount; i++)
            {
                int row = i / columnsForItems;
                int col = i % columnsForItems;

                var element = ContentArea.Children[i];
                Grid.SetRow((FrameworkElement)element, row);
                Grid.SetColumn((FrameworkElement)element, col);

                // 明确检查DataBlock类型
                if (element is DataBlock dataBlock)
                {
                    dataBlock.HorizontalAlignment = HorizontalAlignment.Stretch;
                }
            }

        }


        public async Task SaveData()
        {
            var data = new List<DataBlockModel>();

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                foreach (var child in ContentArea.Children)
                {
                    if (child is DataBlock dataBlock)
                    {
                        var model = new DataBlockModel
                        {
                            Title = dataBlock.title,
                            TaskName = dataBlock.textBox.Text,
                            TargetDate = dataBlock.datePicker.Date?.DateTime,
                            DisplayText = dataBlock.textBlock2.Text
                        };
                        data.Add(model);
                    }
                }
            });

            await DataStorage.SaveDataAsync(data);
        }
        private async void LoadData()
        {
            ContentArea.Padding = new Thickness(12, 8, 12, 20);

            // 非常重要：设置为Stretch而不是Center，这样ContentArea会充满整个可用宽度
            ContentArea.HorizontalAlignment = HorizontalAlignment.Stretch;


            var data = await DataStorage.LoadDataAsync();

            foreach (var model in data)
            {
                var dataBlock = new DataBlock(model.Title, async () => await SaveData())
                {
                    textBox = { Text = model.TaskName },
                    datePicker = { Date = model.TargetDate },
                    textBlock2 = { Text = model.DisplayText }
                };

                if (model.TargetDate.HasValue)
                {
                    dataBlock.expressGrid();
                }
                else
                {
                    dataBlock.editGrid();
                }

                ContentArea.Children.Add(dataBlock);
            }
            foreach (var child in ContentArea.Children)
            {
                if (child is FrameworkElement element)
                {
                    element.MaxWidth = 380; // 限制最大宽度
                }
            }
            UpdateGridLayout();
        }
        public void printAddRow()
        {
            title++;
            // 创建DataBlock
            DataBlock dataBlock = new DataBlock(title, async () => await SaveData());

            // 设置可见性，但不影响初始布局计算
            dataBlock.Opacity = 0;

            // 添加到ContentArea
            ContentArea.Children.Add(dataBlock);
            UpdateGridLayout();

            // 打开编辑对话框
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                // 调用编辑对话框
                var result = await dataBlock.ShowEditDialogAsync();

                // 如果用户取消编辑或未设置日期，则从UI中移除此卡片
                if (result != ContentDialogResult.Primary || dataBlock.datePicker.Date == null)
                {
                    ContentArea.Children.Remove(dataBlock);
                    UpdateGridLayout();
                }
                else
                {
                    // 用户完成编辑并有有效数据，设置卡片透明度为1，使其可见
                    dataBlock.Opacity = 1;
                }
            });
        }


        private void newItemBar(object sender, RoutedEventArgs e)
        {
            printAddRow();
        }

        private async void deleteall(object sender, RoutedEventArgs e)
        {
            
            var result = await deleteAll.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                ContentArea.Children.Clear();
                title = 0;
                await SaveData();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
   
        private async void exportItem(object sender, RoutedEventArgs e)
        {
            var data = new List<DataBlockModel>();

            // 收集当前页面的配置数据
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                foreach (var child in ContentArea.Children)
                {
                    if (child is DataBlock dataBlock)
                    {
                        var model = new DataBlockModel
                        {
                            Title = dataBlock.title,
                            TaskName = dataBlock.textBox.Text,
                            TargetDate = dataBlock.datePicker.Date?.DateTime,
                            DisplayText = dataBlock.textBlock2.Text
                        };
                        data.Add(model);
                    }
                }
            });

            // 序列化数据为 JSON
            var json = JsonConvert.SerializeObject(data);

            // 使用 FileSavePicker 让用户选择保存文件的位置
            var savePicker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            savePicker.FileTypeChoices.Add("JSON 文件", new List<string> { ".json" });
            savePicker.SuggestedFileName = "config";

            Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                // 防止更新文件时的文件冲突
                CachedFileManager.DeferUpdates(file);
                // 将 JSON 数据写入文件
                await FileIO.WriteTextAsync(file, json);
                // 完成文件更新
                Windows.Storage.Provider.FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                if (status != Windows.Storage.Provider.FileUpdateStatus.Complete)
                {
                    ContentDialog dialog = new ContentDialog()
                    {
                        Title = "错误",
                        Content = "无法保存文件。",
                        CloseButtonText = "确定"
                    };
                    await dialog.ShowAsync();
                }
            }
        }

        private async void importItem(object sender, RoutedEventArgs e)
        {
            // 使用 FileOpenPicker 让用户选择要导入的文件
            var openPicker = new Windows.Storage.Pickers.FileOpenPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            openPicker.FileTypeFilter.Add(".json");

            Windows.Storage.StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                // 如果当前有内容，提示用户导入会清除原有内容
                if (ContentArea.Children.Count > 0)
                {
                    

                    var result = await importWarning.ShowAsync();
                    if (result != ContentDialogResult.Primary)
                    {
                        return; // 用户取消导入
                    }
                }

                try
                {
                    // 读取文件内容
                    string json = await FileIO.ReadTextAsync(file);

                    // 反序列化 JSON 数据
                    var data = JsonConvert.DeserializeObject<List<DataBlockModel>>(json);

                    // 清除当前的内容
                    ContentArea.Children.Clear();

                    // 更新 UI
                    foreach (var model in data)
                    {
                        var dataBlock = new DataBlock(model.Title, async () => await SaveData())
                        {
                            textBox = { Text = model.TaskName },
                            datePicker = { Date = model.TargetDate },
                            textBlock2 = { Text = model.DisplayText }
                        };

                        if (model.TargetDate.HasValue)
                        {
                            dataBlock.expressGrid();
                        }
                        else
                        {
                            dataBlock.editGrid();
                        }

                        ContentArea.Children.Add(dataBlock);
                    }

                    // 更新布局
                    UpdateGridLayout();

                    // 保存导入的数据
                    await SaveData();
                }
                catch (Exception ex)
                {
                    ContentDialog dialog = new ContentDialog()
                    {
                        Title = "错误",
                        Content = $"导入文件时发生错误: {ex.Message}",
                        CloseButtonText = "确定"
                    };
                    await dialog.ShowAsync();
                }
            }
        }


    }
}
