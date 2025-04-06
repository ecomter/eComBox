using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using Windows.UI.Xaml;
using Windows.Foundation;  
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static eComBox.Views.DatePage;
using Windows.Storage;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using eComBox.Services;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Navigation;


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
            foreach (var model in data)
            {
                var settings = ApplicationData.Current.LocalSettings;
                if (settings.Values.TryGetValue($"Card_{model.Title}_Notification", out object value) && value is bool)
                {
                    model.EnableDateNotification = (bool)value;
                }
            }

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
        private async Task CleanupDragVisuals()
        {
            // 立即隐藏指示器
            if (_dragDebounceTimer != null && _dragDebounceTimer.IsEnabled)
            {
                _dragDebounceTimer.Stop();
            }

            // 重置拖拽状态
            currentDragTargetIndex = -1;

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
            {
                if (_dragTargetIndicator != null)
                {
                    // 确保指示器隐藏
                    _dragTargetIndicator.Visibility = Visibility.Collapsed;

                    // 移除并重新添加指示器以确保它在Z轴顺序的正确位置
                    if (ContentArea.Children.Contains(_dragTargetIndicator))
                    {
                        ContentArea.Children.Remove(_dragTargetIndicator);
                    }

                    ContentArea.Children.Add(_dragTargetIndicator);
                    _dragTargetIndicator.Visibility = Visibility.Collapsed;

                    // 强制更新布局
                    ContentArea.UpdateLayout();
                }
            });
        }

        private void InitializeEditDialog()
        {

            editDialog = new ContentDialog()
            {
                Title = "编辑事件",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                SecondaryButtonText = "删除卡片",
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
            TextBlock colorLabel = new TextBlock
            {
                Text = "卡片颜色:",
                Margin = new Thickness(8, 16, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(colorLabel, 8); // 使用新添加的行
            Grid.SetColumn(colorLabel, 0);
            dialogContent.Children.Add(colorLabel);

            // 创建颜色选择面板
            var colorPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 0, 8, 8),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(colorPanel, 9);
            Grid.SetColumn(colorPanel, 0);
            dialogContent.Children.Add(colorPanel);

            // 添加预设颜色选项
            AddColorOption(colorPanel, "#3B82F6", "蓝色");
            AddColorOption(colorPanel, "#10B981", "绿色");
            AddColorOption(colorPanel, "#F59E0B", "黄色");
            AddColorOption(colorPanel, "#EF4444", "红色");
            AddColorOption(colorPanel, "#8B5CF6", "紫色");
            AddColorOption(colorPanel, "#EC4899", "粉色");
            AddColorOption(colorPanel, "#6B7280", "灰色");
            AddColorOption(colorPanel, GRADIENT_STARRY_SKY, "星空");  // 添加星空渐变选项
            AddColorOption(colorPanel, GRADIENT_FESTIVE, "喜庆");    // 添加喜庆渐变选项
            AddColorOption(colorPanel, "", "默认"); // 留空表示使用默认颜色

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
                Text = "\uE781", // AI/机器学习图标
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
            CheckBox notificationCheckBox = new CheckBox
            {
                Content = "开机时通知剩余天数",
                Margin = new Thickness(8, 16, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };

            // 设置新的行定义用于通知选项
            Grid.SetRow(notificationCheckBox, 7);
            Grid.SetColumn(notificationCheckBox, 0);
            dialogContent.Children.Add(notificationCheckBox);

            // 将建议面板的行向后移动一行
            Grid.SetRow(_suggestionPanel, 8);
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
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // 检查是否有来自浮动卡片的编辑请求
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("EditCardRequested", out object cardIdObj))
            {
                // 移除请求标志
                ApplicationData.Current.LocalSettings.Values.Remove("EditCardRequested");

                // 确保能解析卡片ID
                if (int.TryParse(cardIdObj.ToString(), out int cardId))
                {
                    // 查找相应的卡片
                    foreach (var child in ContentArea.Children.OfType<DataBlock>())
                    {
                        if (child.title == cardId)
                        {
                            // 延迟执行编辑操作，确保页面完全加载
                            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                            {
                                await Task.Delay(300); // 等待UI完全渲染
                                await child.ShowEditDialogAsync();
                            });
                            break;
                        }
                    }
                }
            }

            // 执行其他导航逻辑
            LoadData();
        }
        private Brush CreateGradientBrushForButton(string gradientType)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };

            switch (gradientType)
            {
                case GRADIENT_STARRY_SKY:
                    // 星空渐变（深蓝色到紫色，加入星星点缀效果）
                    brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 8, 24, 58), Offset = 0.0 });
                    brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 37, 25, 84), Offset = 0.7 });
                    brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 74, 30, 93), Offset = 1.0 });
                    break;

                case GRADIENT_FESTIVE:
                    // 喜庆渐变（红色到金色）
                    brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 210, 4, 45), Offset = 0.0 });
                    brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 255, 64, 25), Offset = 0.5 });
                    brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 255, 196, 25), Offset = 1.0 });
                    break;

                default:
                    // 默认为浅蓝色渐变
                    brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 59, 130, 246), Offset = 0.0 });
                    brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 37, 99, 235), Offset = 1.0 });
                    break;
            }

            return brush;
        }
        private const string GRADIENT_STARRY_SKY = "gradient:starry_sky";
        private const string GRADIENT_FESTIVE = "gradient:festive";

        private void AddColorOption(Panel parent, string colorHex, string name)
        {
            var button = new Button
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(16),
                Tag = colorHex
            };

            // 正确设置ToolTip附加属性
            ToolTipService.SetToolTip(button, name);

            // 设置按钮样式
            if (!string.IsNullOrEmpty(colorHex))
            {
                try
                {
                    if (colorHex.StartsWith("gradient:"))
                    {
                        // 为渐变色选项创建特殊的背景
                        var gradientBrush = CreateGradientBrushForButton(colorHex);
                        button.Background = gradientBrush;
                    }
                    else
                    {
                        // 普通颜色
                        var color = DataBlock.HexStringToColor(colorHex);
                        button.Background = new SolidColorBrush(color);
                    }

                    // 默认边框设置为透明
                    button.BorderBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);
                }
                catch
                {
                    button.Background = new SolidColorBrush(Windows.UI.Colors.Gray);
                    button.Content = "!";
                }
            }
            else
            {
                // 默认颜色按钮样式
                button.BorderThickness = new Thickness(1);
                button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(120, 128, 128, 128));
                button.Content = "×";
            }

            // 添加点击事件
            button.Click += (s, e) =>
            {
                // 更新当前选中的颜色
                string selectedColor = (string)((Button)s).Tag;

                // 设置选中效果
                foreach (var child in parent.Children)
                {
                    if (child is Button colorButton)
                    {
                        // 重置所有按钮样式
                        if (string.IsNullOrEmpty((string)colorButton.Tag))
                        {
                            // 默认按钮恢复原样式
                            colorButton.BorderThickness = new Thickness(1);
                            colorButton.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(120, 128, 128, 128));
                        }
                        else
                        {
                            // 颜色按钮重置为无边框
                            colorButton.BorderThickness = new Thickness(0);
                            colorButton.BorderBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);
                        }

                        // 选中的按钮设置特殊样式
                        if (colorButton == s)
                        {
                            colorButton.BorderThickness = new Thickness(3);

                            // 根据主题选择高对比度的边框颜色
                            var borderColor = Application.Current.RequestedTheme == ApplicationTheme.Dark
                                ? Windows.UI.Colors.White
                                : Windows.UI.Colors.Black;

                            colorButton.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(200, borderColor.R, borderColor.G, borderColor.B));

                            // 添加轻微缩放动画效果
                            var visual = ElementCompositionPreview.GetElementVisual(colorButton);
                            var compositor = visual.Compositor;

                            var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
                            scaleAnimation.InsertKeyFrame(0.0f, new Vector3(1.0f, 1.0f, 1.0f));
                            scaleAnimation.InsertKeyFrame(0.5f, new Vector3(1.15f, 1.15f, 1.0f));
                            scaleAnimation.InsertKeyFrame(1.0f, new Vector3(1.1f, 1.1f, 1.0f));
                            scaleAnimation.Duration = TimeSpan.FromMilliseconds(300);

                            visual.StartAnimation("Scale", scaleAnimation);
                        }
                    }
                }

                // 如果当前编辑的块不为空，更新预览
                if (currentEditingBlock != null)
                {
                    currentEditingBlock.BorderColorHex = selectedColor;
                }
            };

            parent.Children.Add(button);
        }
        private async Task UpdateDateSuggestionsAsync(string taskName)
        {
            try
            {
                // 检查AI功能是否启用
                bool aiEnabled = true; // 默认启用
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue("AIEnabled", out object aiEnabledValue))
                {
                    aiEnabled = (bool)aiEnabledValue;
                }

                // 如果AI功能被禁用，隐藏建议面板并直接返回
                if (!aiEnabled)
                {
                    _suggestionPanel.Visibility = Visibility.Collapsed;
                    return;
                }

                // 以下是原有的代码逻辑
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
            public string BorderColorHex { get; set; } = string.Empty;
            // 添加通知标志
            public bool EnableDateNotification { get; set; } = false;
        }

        public class DataBlock : Grid
        {
            // elements
            public int title;
            public StackPanel stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

            public Button button1 = new Button { Margin = new Thickness(8, 0, 20, 0) };
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
            // 边框颜色字段
            private string _borderColorHex = string.Empty;

            // 获取或设置卡片边框十六进制颜色
            public string BorderColorHex
            {
                get => _borderColorHex;
                set
                {
                    _borderColorHex = value;
                    ApplyCustomBorderColor();
                }
            }

            // 应用自定义边框颜色
            private Brush CreateGradientBrushForBorder(string gradientType)
            {
                var brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };

                switch (gradientType)
                {
                    case "gradient:starry_sky":
                        // 星空渐变（深蓝色到紫色，加入星星点缀效果）
                        brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 8, 24, 58), Offset = 0.0 });
                        brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 37, 25, 84), Offset = 0.7 });
                        brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 74, 30, 93), Offset = 1.0 });
                        break;

                    case "gradient:festive":
                        // 喜庆渐变（红色到金色）
                        brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 210, 4, 45), Offset = 0.0 });
                        brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 255, 64, 25), Offset = 0.5 });
                        brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 255, 196, 25), Offset = 1.0 });
                        break;

                    default:
                        // 默认为浅蓝色渐变
                        brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 59, 130, 246), Offset = 0.0 });
                        brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 37, 99, 235), Offset = 1.0 });
                        break;
                }

                return brush;
            }
            private void ApplyCustomBorderColor()
            {
                if (!string.IsNullOrEmpty(_borderColorHex))
                {
                    try
                    {
                        // 检查是否是渐变色
                        if (_borderColorHex.StartsWith("gradient:"))
                        {
                            var brush = CreateGradientBrushForBorder(_borderColorHex);
                            this.BorderBrush = brush;
                        }
                        else
                        {
                            // 普通颜色
                            var color = HexStringToColor(_borderColorHex);
                            this.BorderBrush = new SolidColorBrush(color);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"应用边框颜色时出错: {ex.Message}");
                    }
                }
            }

            // 辅助方法：将十六进制颜色字符串转换为 Color 对象
            public static Windows.UI.Color HexStringToColor(string hex)
            {
                hex = hex.Replace("#", string.Empty);
                byte a = 255;
                byte r = 0;
                byte g = 0;
                byte b = 0;

                if (hex.Length == 8)
                {
                    // 带透明度的颜色
                    a = Convert.ToByte(hex.Substring(0, 2), 16);
                    r = Convert.ToByte(hex.Substring(2, 2), 16);
                    g = Convert.ToByte(hex.Substring(4, 2), 16);
                    b = Convert.ToByte(hex.Substring(6, 2), 16);
                }
                else if (hex.Length == 6)
                {
                    // 不带透明度的颜色
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                }

                return Windows.UI.Color.FromArgb(a, r, g, b);
            }

            // 辅助方法：将 Color 对象转换为十六进制颜色字符串
            public static string ColorToHexString(Windows.UI.Color color)
            {
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
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
                //this.CanDrag = true;
                //this.DragStarting += DataBlock_DragStarting;
                
                this.RightTapped += DataBlock_RightTapped;


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
                    ambientShadow.Opacity = _elementTheme==ElementTheme.Light ? 0.1f : 0.3f;
                ambientShadow.Color = Windows.UI.Colors.Gray;
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

            public async void DataBlock_RightTapped(object sender, RightTappedRoutedEventArgs e)
            {
                // 防止事件冒泡
                e.Handled = true;

                // 创建右键菜单
                var flyout = new MenuFlyout();

                // 添加编辑菜单项
                var editItem = new MenuFlyoutItem
                {
                    Text = "编辑卡片",
                    Icon = new FontIcon { Glyph = "\uE70F" }  // 编辑图标
                };
                editItem.Click += (s, args) => showEditGrid();
                flyout.Items.Add(editItem);

                // 添加"固定到桌面"菜单项
                var pinItem = new MenuFlyoutItem
                {
                    Text = "固定为悬浮窗",
                    Icon = new FontIcon { Glyph = "\uE141" }  // 固定图标
                };
                pinItem.Click += async (s, args) => await PinToDesktopAsync();
                flyout.Items.Add(pinItem);

                // 添加删除菜单项
                var deleteItem = new MenuFlyoutItem
                {
                    Text = "删除卡片",
                    Icon = new FontIcon { Glyph = "\uE74D" }  // 删除图标
                };
                deleteItem.Click += async (s, args) => await DeleteCardAsync();
                flyout.Items.Add(deleteItem);

                // 显示右键菜单
                flyout.ShowAt(this, e.GetPosition(this));
            }
            private async Task PinToDesktopAsync()
            {
                try
                {
                    // 创建卡片数据模型
                    var cardData = new DataBlockModel
                    {
                        Title = this.title,
                        TaskName = this.textBox.Text,
                        TargetDate = this.datePicker.Date?.DateTime,
                        DisplayText = this.textBlock2.Text,
                        BorderColorHex = this.BorderColorHex
                    };

                    // 创建新的应用视图
                    var viewId = 0;

                    await CoreApplication.CreateNewView().Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        var frame = new Frame();
                        frame.Navigate(typeof(FloatingCardPage), cardData);
                        Window.Current.Content = frame;
                        Window.Current.Activate();

                        viewId = ApplicationView.GetForCurrentView().Id;

                        // 确保新视图已激活后才继续
                        await Task.Delay(100);
                    });

                    // 从主视图切换到新视图
                    await ApplicationViewSwitcher.TryShowAsViewModeAsync(
                        viewId,
                        ApplicationViewMode.CompactOverlay,
                        ViewModePreferences.CreateDefault(ApplicationViewMode.CompactOverlay));
                }
                catch (Exception ex)
                {
                    // 显示错误信息
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "操作失败",
                        Content = $"无法创建悬浮窗: {ex.Message}",
                        CloseButtonText = "确定"
                    };
                    await dialog.ShowAsync();
                }
            }
            // 添加删除卡片的方法
            private async Task DeleteCardAsync()
            {
                try
                {
                    // 询问用户确认
                    ContentDialog deleteDialog = new ContentDialog
                    {
                        Title = "删除卡片",
                        Content = "确定要删除此卡片吗？此操作不可撤销。",
                        PrimaryButtonText = "删除",
                        CloseButtonText = "取消",
                        DefaultButton = ContentDialogButton.Close
                    };

                    var result = await deleteDialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        // 先获取父面板引用，避免动画完成后可能引用已经改变
                        Panel parentPanel = this.Parent as Panel;
                        if (parentPanel == null)
                        {
                            Debug.WriteLine("无法获取父面板");
                            return; // 无法找到父面板，无法删除
                        }

                        try
                        {
                            // 执行删除前的动画
                            var visual = ElementCompositionPreview.GetElementVisual(this);
                            var compositor = visual.Compositor;

                            // 创建缩小和淡出动画
                            var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
                            scaleAnimation.InsertKeyFrame(0f, new Vector3(1, 1, 1));
                            scaleAnimation.InsertKeyFrame(1f, new Vector3(0.8f, 0.8f, 1));
                            scaleAnimation.Duration = TimeSpan.FromMilliseconds(200);

                            var fadeAnimation = compositor.CreateScalarKeyFrameAnimation();
                            fadeAnimation.InsertKeyFrame(0f, 1f);
                            fadeAnimation.InsertKeyFrame(1f, 0f);
                            fadeAnimation.Duration = TimeSpan.FromMilliseconds(200);

                            visual.StartAnimation("Scale", scaleAnimation);
                            visual.StartAnimation("Opacity", fadeAnimation);

                            // 等待动画完成后再移除
                            await Task.Delay(200);

                            // 获取DatePage实例用于后续操作
                            var page = GetDatePage();

                            // 删除元素并更新布局
                            bool removed = false;
                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                            {
                                try
                                {
                                    int indexToRemove = parentPanel.Children.IndexOf(this);
                                    if (indexToRemove >= 0)
                                    {
                                        parentPanel.Children.RemoveAt(indexToRemove);
                                        removed = true;

                                        // 立即更新UI布局
                                        if (page != null)
                                        {
                                            page.UpdateGridLayout();
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"UI线程上删除卡片失败: {ex.Message}");
                                }
                            });

                            // 如果删除成功，保存数据
                            if (removed && page != null)
                            {
                                try
                                {
                                    await page.SaveData();
                                    Debug.WriteLine("删除卡片后成功保存数据");
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"保存数据失败: {ex.Message}");

                                    // 显示保存错误提示
                                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                                    {
                                        ContentDialog errorDialog = new ContentDialog
                                        {
                                            Title = "保存失败",
                                            Content = "删除卡片后保存数据失败，但卡片已被移除。",
                                            CloseButtonText = "确定"
                                        };
                                        await errorDialog.ShowAsync();
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"删除卡片过程中出错: {ex.Message}");
                            Debug.WriteLine($"错误详细信息: {ex.StackTrace}");

                            // 显示错误消息
                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                            {
                                ContentDialog errorDialog = new ContentDialog
                                {
                                    Title = "操作失败",
                                    Content = $"删除卡片时出现错误: {ex.Message}",
                                    CloseButtonText = "确定"
                                };
                                await errorDialog.ShowAsync();
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"删除对话框显示失败: {ex.Message}");

                    // 如果连对话框都无法显示，可能是UI线程出现了严重问题
                    // 尝试直接删除卡片
                    try
                    {
                        Panel parentPanel = this.Parent as Panel;
                        if (parentPanel != null)
                        {
                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                            {
                                parentPanel.Children.Remove(this);
                            });

                            // 获取DatePage实例
                            var page = GetDatePage();
                            if (page != null)
                            {
                                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                {
                                    page.UpdateGridLayout();
                                });
                                await page.SaveData();
                            }
                        }
                    }
                    catch (Exception innerEx)
                    {
                        Debug.WriteLine($"紧急删除失败: {innerEx.Message}");
                    }
                }
            }

            // 修改为完全匹配的事件处理程序签名
            private async void DataBlock_DragStarting(UIElement sender, Windows.UI.Xaml.DragStartingEventArgs e)
            {
                try
                {
                    // 设置要传递的数据（DataBlock的唯一ID）
                    e.Data.SetData("DataBlockId", this.title.ToString());

                    // 设置拖拽提示信息
                    e.Data.Properties.Title = string.IsNullOrEmpty(textBox.Text) ? "拖动卡片" : textBox.Text;
                    e.Data.Properties.Description = "拖放到新位置";

                    try
                    {
                        // 创建可视化元素的位图表示
                        RenderTargetBitmap renderBitmap = new RenderTargetBitmap();
                        await renderBitmap.RenderAsync(this);

                        // 获取像素数据
                        var pixelBuffer = await renderBitmap.GetPixelsAsync();
                        if (pixelBuffer != null)
                        {
                            // 创建一个BitmapImage并设置为拖动UI内容
                            BitmapImage bitmapImage = new BitmapImage();
                            using (var stream = pixelBuffer.AsStream())
                            {
                                await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
                            }
                            e.DragUI.SetContentFromBitmapImage(bitmapImage);
                        }
                        else
                        {
                            // 回退选项 - 如果无法获取位图
                            e.DragUI.SetContentFromBitmapImage(GetDragUIContent());
                        }
                    }
                    catch (Exception bitmapEx)
                    {
                        Debug.WriteLine($"创建拖拽缩略图失败: {bitmapEx.Message}");
                        // 回退到简单的设置
                        e.DragUI.SetContentFromBitmapImage(GetDragUIContent());
                    }

                    e.DragUI.SetContentFromDataPackage();
                    e.DragUI.SetContentFromBitmapImage(GetDragUIContent());

                    // 当拖拽开始时，降低源卡片的不透明度作为视觉提示
                    this.Opacity = 0.6;

                    // 创建一个定时器，在短时间后恢复原始不透明度
                    var timer = new DispatcherTimer();
                    timer.Interval = TimeSpan.FromMilliseconds(1500); // 较长时间确保拖拽过程中保持半透明
                    timer.Tick += (s, args) =>
                    {
                        if (this.Opacity < 1.0) // 确保仅在需要时恢复
                        {
                            // 使用动画平滑恢复不透明度
                            var visual = ElementCompositionPreview.GetElementVisual(this);
                            var compositor = visual.Compositor;

                            var fadeAnimation = compositor.CreateScalarKeyFrameAnimation();
                            fadeAnimation.InsertKeyFrame(0.0f, 0.6f);
                            fadeAnimation.InsertKeyFrame(1.0f, 1.0f);
                            fadeAnimation.Duration = TimeSpan.FromMilliseconds(300);

                            visual.StartAnimation("Opacity", fadeAnimation);
                        }
                        timer.Stop();
                    };
                    timer.Start();

                    // With these lines
                    e.DragUI.SetContentFromDataPackage();
                }
                catch (Exception ex)
                {
                    // 捕获并记录异常，确保应用不会崩溃
                    Debug.WriteLine($"拖拽初始化错误: {ex.Message}");
                    Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");

                    // 确保不透明度恢复
                    this.Opacity = 1.0;
                }
            }

            private BitmapImage GetDragUIContent()
            {
                try
                {
                    // 创建一个基本的BitmapImage作为拖拽内容的备选方案
                    var image = new BitmapImage(new Uri("ms-appx:///Assets/DragIndicator.png"));
                    return image;
                }
                catch
                {
                    // 如果无法加载图像，返回一个空的BitmapImage
                    return new BitmapImage();
                }
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
                var textBlock6 = new TextBlock
                {
                    Margin = new Thickness(8),
                    FontSize = 40,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
                    Text = $"{Math.Abs(diffTime.Days)}天"
                };
                if (string.IsNullOrEmpty(textBox.Text))
                {
                    textBox.Text = $"{datePicker.Date.Value.Year}年{datePicker.Date.Value.Month}月{datePicker.Date.Value.Day}日";
                }
                else
                {
                    textBlock2.Text = textBox.Text;
                }

                // 使用更加统一的色彩方案，创建递进的视觉效果
                if (diffTime.Days > 0)
                {
                    // 统一设置边框样式和厚度
                    this.BorderThickness = new Thickness(1.5);

                    // 设置文本内容
                    if (diffTime.Days == 1)
                    {
                        textBlock5.Text = $"{textBox.Text}就在";
                        textBlock6.Text = "明天";
                    }
                    else if (diffTime.Days == 2)
                    {
                        textBlock5.Text = $"{textBox.Text}就在";
                        textBlock6.Text = "后天";
                    }
                    else
                    {
                        textBlock5.Text = $"今天距离{textBox.Text}还有";
                        this.BorderThickness = new Thickness(1); // 普通未来日期使用标准边框厚度
                    }

                    // 设置统一的渐变色方案，根据天数变化亮度和色调
                    if (Application.Current.RequestedTheme == ApplicationTheme.Dark)
                    {
                        // 深色主题 - 使用紫蓝到蓝绿到绿色的渐变方案
                        var borderBrush = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1)
                        };

                        if (diffTime.Days == 1) // 明天 - 紫蓝色调
                        {
                            borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 80, 130, 230), Offset = 0.0 });
                            borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 120, 120, 240), Offset = 1.0 });

                            // 文字颜色（更柔和）
                            textBlock5.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 110, 150, 235));
                            textBlock6.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 140, 240));
                        }
                        else if (diffTime.Days == 2) // 后天 - 蓝绿色调
                        {
                            borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 40, 160, 190), Offset = 0.0 });
                            borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 70, 150, 200), Offset = 1.0 });

                            // 文字颜色（更柔和）
                            textBlock5.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 80, 170, 190));
                            textBlock6.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 60, 160, 200));
                        }
                        else // 其他未来日期 - 默认色调
                        {
                            this.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255));
                            textBlock5.ClearValue(TextBlock.ForegroundProperty);
                            textBlock6.ClearValue(TextBlock.ForegroundProperty);
                        }

                        if (diffTime.Days <= 2)
                        {
                            this.BorderBrush = borderBrush;
                        }
                    }
                    else // 浅色主题
                    {
                        // 浅色主题 - 使用同样色调但更深沉的颜色
                        var borderBrush = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1)
                        };

                        if (diffTime.Days == 1) // 明天 - 蓝紫色调
                        {
                            borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 50, 120, 220), Offset = 0.0 });
                            borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 100, 100, 220), Offset = 1.0 });

                            // 文字颜色
                            textBlock5.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 50, 120, 220));
                            textBlock6.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 60, 100, 220));
                        }
                        else if (diffTime.Days == 2) // 后天 - 蓝绿色调
                        {
                            borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 20, 140, 170), Offset = 0.0 });
                            borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 40, 130, 180), Offset = 1.0 });

                            // 文字颜色
                            textBlock5.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 20, 140, 160));
                            textBlock6.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 130, 170));
                        }
                        else // 其他未来日期 - 默认色调
                        {
                            this.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 0, 0));
                            textBlock5.ClearValue(TextBlock.ForegroundProperty);
                            textBlock6.ClearValue(TextBlock.ForegroundProperty);
                        }

                        if (diffTime.Days <= 2)
                        {
                            this.BorderBrush = borderBrush;
                        }
                    }
                }
                else if (diffTime.Days < 0) // 过去的日期
                {
                    textBlock5.Text = $"今天距离{textBox.Text}已经过去了";
                    this.BorderThickness = new Thickness(1);

                    // 设置过去日期的边框和文字颜色 - 保持橙红色调
                    if (Application.Current.RequestedTheme == ApplicationTheme.Dark)
                    {
                        // 深色主题 - 橙红色调
                        var borderBrush = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1)
                        };
                        borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(200, 180, 90, 70), Offset = 0.0 });
                        borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(200, 150, 80, 60), Offset = 1.0 });
                        this.BorderBrush = borderBrush;

                        // 文字颜色也调整为橙红色调
                        textBlock5.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 130, 90));
                        textBlock6.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 140, 90));
                    }
                    else
                    {
                        // 浅色主题 - 橙褐色调
                        var borderBrush = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1)
                        };
                        borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 180, 90, 50), Offset = 0.0 });
                        borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 160, 80, 40), Offset = 1.0 });
                        this.BorderBrush = borderBrush;

                        // 文字颜色调整为深橙色
                        textBlock5.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 180, 80, 40));
                        textBlock6.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 190, 85, 40));
                    }
                }
                else // 当天事件
                {
                    textBlock5.Text = "今天就是";
                    textBlock6.Text = $"{textBox.Text}";
                    this.BorderThickness = new Thickness(2); // 当天用最粗的边框

                    // 当天事件保持高亮的紫色系
                    if (Application.Current.RequestedTheme == ApplicationTheme.Dark)
                    {
                        // 深色主题中的高亮边框
                        var borderBrush = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1)
                        };
                        borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 110, 80, 240), Offset = 0.0 });
                        borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 170, 110, 240), Offset = 1.0 });
                        this.BorderBrush = borderBrush;
                    }
                    else
                    {
                        // 浅色主题中的高亮边框
                        var borderBrush = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1)
                        };
                        borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 100, 70, 220), Offset = 0.0 });
                        borderBrush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(255, 150, 80, 240), Offset = 1.0 });
                        this.BorderBrush = borderBrush;
                    }
                    textBlock5.ClearValue(TextBlock.ForegroundProperty);
                    textBlock6.ClearValue(TextBlock.ForegroundProperty);
                }

                textBlock6.SetValue(Grid.RowProperty, 2);
                this.Children.Clear();
                this.Children.Add(button1r);
                this.Children.Add(textBlock5);
                this.Children.Add(textBlock6);

                if (!string.IsNullOrEmpty(BorderColorHex))
                {
                    ApplyCustomBorderColor();
                }
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
                    Content = "删除卡片",
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
            // 获取特定卡片的数据模型
            public DataBlockModel GetDataBlockModel(int title)
            {
                foreach (var child in Children)
                {
                    if (child is DataBlock dataBlock && dataBlock.title == title)
                    {
                        return new DataBlockModel
                        {
                            Title = dataBlock.title,
                            TaskName = dataBlock.textBox.Text,
                            TargetDate = dataBlock.datePicker.Date?.DateTime,
                            DisplayText = dataBlock.textBlock2.Text,
                            BorderColorHex = dataBlock.BorderColorHex,
                            EnableDateNotification = GetNotificationSetting(title)
                        };
                    }
                }
                return null;
            }

            // 更新特定卡片的通知设置
            public void UpdateDataBlockNotificationSetting(int title, bool enableNotification)
            {
                // 将通知设置保存到本地存储
                var settings = ApplicationData.Current.LocalSettings;
                settings.Values[$"Card_{title}_Notification"] = enableNotification;
            }

            // 获取通知设置
            private bool GetNotificationSetting(int title)
            {
                var settings = ApplicationData.Current.LocalSettings;
                if (settings.Values.TryGetValue($"Card_{title}_Notification", out object value) && value is bool)
                {
                    return (bool)value;
                }
                return false;
            }

            public async Task<ContentDialogResult> ShowEditDialogAsync()
            {
                // 保留原有互斥锁代码...
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

                    // 设置当前颜色选中和通知设置
                    if (page.editDialog.Content is ScrollViewer scrollViewer &&
                        scrollViewer.Content is Grid dialogContent)
                    {
                        // 查找并设置通知复选框的状态
                        foreach (var child in dialogContent.Children)
                        {
                            if (child is CheckBox notificationCheckBox && Grid.GetRow(child as FrameworkElement) == 7)
                            {
                                // 根据本地存储设置复选框状态
                                var settings = ApplicationData.Current.LocalSettings;
                                bool isNotificationEnabled = false;

                                if (settings.Values.TryGetValue($"Card_{this.title}_Notification", out object value) && value is bool)
                                {
                                    isNotificationEnabled = (bool)value;
                                }

                                notificationCheckBox.IsChecked = isNotificationEnabled;
                                break;
                            }
                        }

                        // 设置颜色按钮
                        foreach (var child in dialogContent.Children)
                        {
                            if (child is StackPanel panel && Grid.GetRow(panel) == 9) // 颜色面板所在的行
                            {
                                foreach (var colorButton in panel.Children)
                                {
                                    if (colorButton is Button button)
                                    {
                                        string buttonColorHex = (string)button.Tag;

                                        // 重置所有按钮样式
                                        if (string.IsNullOrEmpty(buttonColorHex))
                                        {
                                            // 默认按钮
                                            button.BorderThickness = new Thickness(1);
                                            button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(120, 128, 128, 128));
                                        }
                                        else
                                        {
                                            // 颜色按钮
                                            button.BorderThickness = new Thickness(0);
                                            button.BorderBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);
                                        }

                                        // 设置选中的按钮样式
                                        if (buttonColorHex == this.BorderColorHex)
                                        {
                                            button.BorderThickness = new Thickness(3);

                                            // 根据主题选择高对比度的边框颜色
                                            var borderColor = Application.Current.RequestedTheme == ApplicationTheme.Dark
                                                ? Windows.UI.Colors.White
                                                : Windows.UI.Colors.Black;

                                            button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(200, borderColor.R, borderColor.G, borderColor.B));
                                        }
                                    }
                                }
                                break;
                            }
                        }
                    }

                    var result = await page.editDialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        // 用户点击了确定按钮，更新DataBlock数据
                        this.textBox.Text = page.dialogTaskNameBox.Text;
                        this.datePicker.Date = page.dialogDatePicker.Date;

                        // 保存通知设置
                        if (page.editDialog.Content is ScrollViewer sv &&
                            sv.Content is Grid dc)
                        {
                            foreach (var child in dc.Children)
                            {
                                if (child is CheckBox notificationCheckBox && Grid.GetRow(child as FrameworkElement) == 7)
                                {
                                    // 将通知设置保存到本地存储
                                    var settings = ApplicationData.Current.LocalSettings;
                                    settings.Values[$"Card_{this.title}_Notification"] = notificationCheckBox.IsChecked ?? false;
                                    break;
                                }
                            }
                        }

                        if (this.datePicker.Date != null)
                        {
                            // 使用简化版的动画方法，避免动画冲突
                            await SafeAnimateGridAsync(delegate () { expressGrid(); });

                            // 确保在expressGrid后应用自定义颜色，因为expressGrid会重设边框
                            if (!string.IsNullOrEmpty(this.BorderColorHex))
                            {
                                ApplyCustomBorderColor();
                            }
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
                        // 用户点击了"删除卡片"按钮
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
        private Border _dragTargetIndicator;
        public DatePage()
        {
            InitializeComponent();

            // 检查AI功能是否启用
            bool aiEnabled = false; 
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("AIEnabled", out object aiEnabledValue))
            {
                aiEnabled = (bool)aiEnabledValue;
            }

            // 仅当AI功能启用时才初始化AI服务
            if (aiEnabled)
            {
                _predictionService = new AzureDatePredictionService(
                    "https://ai-jinqiaoli1752ai485205845953.cognitiveservices.azure.com/",
                    "F37Fkmz1W7kD8veNTpU35sG6HOcU0f84zFr52LBsmbmE0IEPNgVhJQQJ99ALACHYHv6XJ3w3AAAAACOGdqmg");
            }

            ContentArea.SizeChanged += ContentArea_SizeChanged;
            ContentArea_SizeChanged(ContentArea, null);

            // 启用ContentArea的拖放功能
            ContentArea.AllowDrop = true;
            ContentArea.DragOver += ContentArea_DragOver;
            ContentArea.Drop += ContentArea_Drop;

            // 创建拖拽指示器
            _dragTargetIndicator = new Border
            {
                Name = "DragTargetIndicator", // 添加名字方便调试
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 0, 120, 215)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(2),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 120, 215)),
                Visibility = Visibility.Collapsed
            };
            ContentArea.Children.Add(_dragTargetIndicator);

            // 在页面加载时添加一个 DataBlock
            LoadData();
        }
        private void ContentArea_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;

                // 获取拖拽位置，判断目标位置
                var position = e.GetPosition(ContentArea);

                // 更新拖拽位置指示器效果
                UpdateDragPositionIndicator(position);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"拖拽悬停处理错误: {ex.Message}");

                // 在出现错误时立即隐藏指示器
                if (_dragTargetIndicator != null)
                {
                    _dragTargetIndicator.Visibility = Visibility.Collapsed;

                    // 确保指示器回到视觉树的合适位置
                    if (ContentArea.Children.Contains(_dragTargetIndicator))
                    {
                        ContentArea.Children.Remove(_dragTargetIndicator);
                        ContentArea.Children.Add(_dragTargetIndicator); // 重新添加到末尾
                    }
                }

                // 重置拖拽状态
                currentDragTargetIndex = -1;

                // 停止计时器
                if (_dragDebounceTimer != null && _dragDebounceTimer.IsEnabled)
                {
                    _dragDebounceTimer.Stop();
                }
            }
        }
        private DispatcherTimer _dragDebounceTimer;
        private Point _lastDragPosition;
        // 更新拖拽位置指示器
        private int currentDragTargetIndex = -1;
        private void UpdateDragPositionIndicator(Point position)
        {
            // 保存当前拖拽位置
            _lastDragPosition = position;

            // 如果指示器不存在，退出
            if (_dragTargetIndicator == null)
            {
                return;
            }

            // 使用计时器来减少视觉更新频率
            if (_dragDebounceTimer == null)
            {
                _dragDebounceTimer = new DispatcherTimer();
                _dragDebounceTimer.Interval = TimeSpan.FromMilliseconds(50);
                _dragDebounceTimer.Tick += (s, e) =>
                {
                    try
                    {
                        UpdateIndicatorPosition();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"更新指示器位置时出错: {ex.Message}");

                        // 出错时隐藏指示器
                        if (_dragTargetIndicator != null)
                        {
                            _dragTargetIndicator.Visibility = Visibility.Collapsed;
                        }

                        // 停止计时器
                        if (_dragDebounceTimer != null && _dragDebounceTimer.IsEnabled)
                        {
                            _dragDebounceTimer.Stop();
                        }
                    }
                };
                _dragDebounceTimer.Start();
            }
            else if (!_dragDebounceTimer.IsEnabled)
            {
                _dragDebounceTimer.Start();
            }
        }
        private async void ContentArea_Drop(object sender, DragEventArgs e)
        {
            try
            {
                // 立即停止更新计时器
                if (_dragDebounceTimer != null && _dragDebounceTimer.IsEnabled)
                {
                    _dragDebounceTimer.Stop();
                }

                // 首先确保指示器被隐藏
                if (_dragTargetIndicator != null)
                {
                    _dragTargetIndicator.Visibility = Visibility.Collapsed;
                }

                if (e.DataView.Contains("DataBlockId"))
                {
                    // 获取被拖拽的数据块ID
                    string blockIdString = await e.DataView.GetDataAsync("DataBlockId") as string;

                    if (!string.IsNullOrEmpty(blockIdString) && int.TryParse(blockIdString, out int blockId))
                    {
                        // 查找源数据块
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

                        if (sourceBlock != null && sourceIndex >= 0 && currentDragTargetIndex >= 0 && sourceIndex != currentDragTargetIndex)
                        {
                            // 从源位置移除
                            ContentArea.Children.RemoveAt(sourceIndex);

                            // 调整目标索引（移除源项后索引会变化）
                            int targetIndex = currentDragTargetIndex;
                            if (targetIndex > sourceIndex)
                            {
                                targetIndex--;
                            }

                            // 插入到新位置
                            if (targetIndex >= ContentArea.Children.Count)
                            {
                                ContentArea.Children.Add(sourceBlock);
                            }
                            else
                            {
                                ContentArea.Children.Insert(targetIndex, sourceBlock);
                            }

                            // 添加放置动画效果
                            var visual = ElementCompositionPreview.GetElementVisual(sourceBlock);
                            var compositor = visual.Compositor;

                            // 缩放和不透明度动画
                            var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
                            scaleAnimation.InsertKeyFrame(0.0f, new Vector3(0.95f, 0.95f, 1.0f));
                            scaleAnimation.InsertKeyFrame(0.3f, new Vector3(1.05f, 1.05f, 1.0f));
                            scaleAnimation.InsertKeyFrame(1.0f, new Vector3(1.0f, 1.0f, 1.0f));
                            scaleAnimation.Duration = TimeSpan.FromMilliseconds(400);

                            var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
                            opacityAnimation.InsertKeyFrame(0.0f, 0.7f);
                            opacityAnimation.InsertKeyFrame(1.0f, 1.0f);
                            opacityAnimation.Duration = TimeSpan.FromMilliseconds(300);

                            visual.StartAnimation("Scale", scaleAnimation);
                            visual.StartAnimation("Opacity", opacityAnimation);

                            // 更新布局
                            UpdateGridLayout();

                            // 保存新排序
                            await SaveData();

                            // 播放放置音效（如果支持）
                            try
                            {
                                ElementSoundPlayer.Play(ElementSoundKind.Invoke);
                            }
                            catch { /* 忽略声音播放失败 */ }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"拖放操作错误: {ex.Message}");

                try
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "操作提示",
                        Content = "拖放操作未能完成，请重试。",
                        CloseButtonText = "确定"
                    };
                    await dialog.ShowAsync();
                }
                catch { /* 忽略对话框显示失败 */ }
            }
            finally
            {
                // 重置拖拽状态
                await CleanupDragVisuals();
            }


        }
        void UpdateIndicatorPosition()
        {
            // 先停止计时器
            if (_dragDebounceTimer != null && _dragDebounceTimer.IsEnabled)
            {
                _dragDebounceTimer.Stop();
            }

            try
            {
                if (_dragTargetIndicator == null)
                {
                    Debug.WriteLine("拖拽指示器未初始化");
                    return;
                }

                // 计算新的目标位置
                int columnsForItems = ColMax;
                int childCount = ContentArea.Children.Count;

                // 排除拖拽指示器自身
                var realChildren = ContentArea.Children.Cast<UIElement>()
                    .Where(c => c != _dragTargetIndicator && c is DataBlock)
                    .ToList();

                childCount = realChildren.Count;

                // 如果没有有效的数据块，则不显示指示器
                if (childCount == 0)
                {
                    _dragTargetIndicator.Visibility = Visibility.Collapsed;
                    currentDragTargetIndex = -1; // 重置目标索引
                    return;
                }

                // 计算目标位置的列
                double colWidth = ContentArea.ActualWidth / columnsForItems;
                if (colWidth <= 0)
                {
                    _dragTargetIndicator.Visibility = Visibility.Collapsed;
                    return;
                }

                int col = Math.Min(Math.Max((int)(_lastDragPosition.X / colWidth), 0), columnsForItems - 1);

                // 计算鼠标在网格中的行位置
                double vertPosition = _lastDragPosition.Y;
                int targetRow = 0;
                double accumulatedHeight = 0;

                // 计算每行的高度和位置，找出鼠标所在的行
                List<(int RowIndex, double Top, double Height)> rowInfo = new List<(int, double, double)>();

                for (int r = 0; r < (childCount + columnsForItems - 1) / columnsForItems; r++)
                {
                    int startIndex = r * columnsForItems;
                    int endIndex = Math.Min(startIndex + columnsForItems - 1, childCount - 1);

                    double rowHeight = 0;

                    // 找出当前行中最高的项
                    for (int i = startIndex; i <= endIndex; i++)
                    {
                        if (i < realChildren.Count && realChildren[i] is FrameworkElement element)
                        {
                            rowHeight = Math.Max(rowHeight, element.ActualHeight + element.Margin.Top + element.Margin.Bottom);
                        }
                    }

                    rowInfo.Add((r, accumulatedHeight, rowHeight));
                    accumulatedHeight += rowHeight + 10; // 10px是行间距
                }

                // 找出鼠标所在的行
                for (int i = 0; i < rowInfo.Count; i++)
                {
                    var (rowIndex, top, height) = rowInfo[i];

                    // 如果点在此行范围内，或者是最后一行下方
                    if (vertPosition >= top && (vertPosition <= top + height || i == rowInfo.Count - 1))
                    {
                        targetRow = rowIndex;
                        break;
                    }
                }

                // 计算最终索引位置
                int targetIndex = targetRow * columnsForItems + col;

                // 将索引限制在有效范围内
                targetIndex = Math.Min(targetIndex, childCount);

                // 只有当目标索引发生变化时才更新
                if (targetIndex == currentDragTargetIndex && _dragTargetIndicator.Visibility == Visibility.Visible)
                {
                    return; // 如果位置没变且已经显示，则无需更新
                }

                currentDragTargetIndex = targetIndex;

                // 确保有足够的行
                while (ContentArea.RowDefinitions.Count <= targetRow)
                {
                    ContentArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                // 设置指示器在网格中的位置
                Grid.SetRow(_dragTargetIndicator, targetRow);
                Grid.SetColumn(_dragTargetIndicator, col);

                // 调整指示器大小以匹配目标位置的大小
                DataBlock referenceBlock = null;

                // 尝试使用附近的DataBlock作为尺寸参考
                foreach (var child in realChildren)
                {
                    if (child is DataBlock block)
                    {
                        referenceBlock = block;
                        break;
                    }
                }

                if (referenceBlock != null)
                {
                    _dragTargetIndicator.MinHeight = referenceBlock.MinHeight;
                    _dragTargetIndicator.MaxWidth = referenceBlock.MaxWidth;
                    _dragTargetIndicator.Width = referenceBlock.ActualWidth > 0 ? referenceBlock.ActualWidth : referenceBlock.MaxWidth;
                }

                // 显示指示器
                _dragTargetIndicator.Visibility = Visibility.Visible;

                // 简单动画效果增强视觉反馈
                var visual = ElementCompositionPreview.GetElementVisual(_dragTargetIndicator);
                var compositor = visual.Compositor;

                var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
                scaleAnimation.InsertKeyFrame(0.0f, new Vector3(0.97f, 0.97f, 1.0f));
                scaleAnimation.InsertKeyFrame(1.0f, new Vector3(1.0f, 1.0f, 1.0f));
                scaleAnimation.Duration = TimeSpan.FromMilliseconds(150);

                var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
                opacityAnimation.InsertKeyFrame(0.0f, 0.7f);
                opacityAnimation.InsertKeyFrame(1.0f, 0.85f);
                opacityAnimation.Duration = TimeSpan.FromMilliseconds(150);

                visual.StartAnimation("Scale", scaleAnimation);
                visual.StartAnimation("Opacity", opacityAnimation);

                // 确保指示器在最上层
                if (ContentArea.Children.Contains(_dragTargetIndicator))
                {
                    ContentArea.Children.Remove(_dragTargetIndicator);
                }
                ContentArea.Children.Add(_dragTargetIndicator);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"拖拽指示器更新错误: {ex.Message}");

                if (_dragTargetIndicator != null)
                {
                    _dragTargetIndicator.Visibility = Visibility.Collapsed;
                }

                // 出错时重置目标索引
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

            // 只考虑DataBlock类型的元素进行布局
            var dataBlocks = ContentArea.Children.OfType<DataBlock>().ToList();
            int dataBlockCount = dataBlocks.Count;

            int columnsForItems = ColMax;
            int rowCount = (int)Math.Ceiling((double)dataBlockCount / columnsForItems);

            // 添加行定义
            for (int i = 0; i < Math.Max(1, rowCount); i++)  // 确保至少有一行
            {
                ContentArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // 重新排列DataBlock元素
            for (int i = 0; i < dataBlockCount; i++)
            {
                int row = i / columnsForItems;
                int col = i % columnsForItems;

                var dataBlock = dataBlocks[i];
                Grid.SetRow(dataBlock, row);
                Grid.SetColumn(dataBlock, col);
                dataBlock.HorizontalAlignment = HorizontalAlignment.Stretch;
            }

            // 确保拖拽指示器总是位于最上层，且不受上面布局影响
            if (_dragTargetIndicator != null)
            {
                // 如果已经在集合中，先移除它
                if (ContentArea.Children.Contains(_dragTargetIndicator))
                {
                    ContentArea.Children.Remove(_dragTargetIndicator);
                }
                // 重新添加到最后
                ContentArea.Children.Add(_dragTargetIndicator);

                // 确保指示器保持隐藏状态
                _dragTargetIndicator.Visibility = Visibility.Collapsed;
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
                            DisplayText = dataBlock.textBlock2.Text,
                            BorderColorHex = dataBlock.BorderColorHex // 保存边框颜色
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

            // 暂时移除拖拽指示器（如果已存在）
            if (_dragTargetIndicator != null && ContentArea.Children.Contains(_dragTargetIndicator))
            {
                ContentArea.Children.Remove(_dragTargetIndicator);
            }

            var data = await DataStorage.LoadDataAsync();

            // 清空现有的数据块
            ContentArea.Children.Clear();

            // 只加载一次数据
            foreach (var model in data)
            {
                var dataBlock = new DataBlock(model.Title, async () => await SaveData())
                {
                    textBox = { Text = model.TaskName },
                    datePicker = { Date = model.TargetDate },
                    textBlock2 = { Text = model.DisplayText },
                    BorderColorHex = model.BorderColorHex // 加载边框颜色
                };

                if (model.TargetDate.HasValue)
                {
                    dataBlock.expressGrid();
                }
                else
                {
                    dataBlock.editGrid();
                }

                // 限制最大宽度
                dataBlock.MaxWidth = 380;

                ContentArea.Children.Add(dataBlock);
            }

            // 将拖拽指示器添加回来（确保在最上层）
            if (_dragTargetIndicator != null)
            {
                ContentArea.Children.Add(_dragTargetIndicator);
            }
            else
            {
                // 如果指示器为null，重新初始化它
                _dragTargetIndicator = new Border
                {
                    BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 0, 120, 215)),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 120, 215)),
                    Visibility = Visibility.Collapsed
                };
                ContentArea.Children.Add(_dragTargetIndicator);
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
                    // 确保在UI线程上执行，并且在移除前后正确处理布局
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                    {
                        // 先检查卡片是否仍在容器中
                        if (ContentArea.Children.Contains(dataBlock))
                        {
                            ContentArea.Children.Remove(dataBlock);

                            // 过滤有效的子元素，只考虑DataBlock类型的元素
                            var validBlocks = ContentArea.Children.OfType<DataBlock>().ToList();

                            // 强制重绘和重新布局
                            UpdateGridLayout();
                            ContentArea.UpdateLayout();
                        }
                    });

                    // 保存当前状态，确保与UI一致
                    await SaveData();
                }
                else
                {
                    // 用户完成编辑并有有效数据，设置卡片透明度为1，使其可见
                    dataBlock.Opacity = 1;

                    // 强制更新布局
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        UpdateGridLayout();
                        ContentArea.UpdateLayout();
                    });
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
                            DisplayText = dataBlock.textBlock2.Text,
                            BorderColorHex = dataBlock.BorderColorHex 
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
                            textBlock2 = { Text = model.DisplayText },
                            BorderColorHex = model.BorderColorHex ?? "" 
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
