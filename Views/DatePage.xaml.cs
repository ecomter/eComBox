using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using Windows.Devices.PointOfService;
using Windows.UI.Xaml;
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

namespace eComBox.Views
{

    public static class DataStorage
    {
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
            private TextBlock button3Text1 = new TextBlock
            {
                Margin = new Thickness(0, 2, 5, 0),
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Text = "\uED62", // Unicode 字符
                FontSize = 18
            };
            private TextBlock button3Text2 = new TextBlock
            {
                Text = "清除日期"
            };

            private readonly Action saveDataAction;

            public DataBlock(int title, Action saveDataAction)
            {
                this.Name = "DataBlock" + title;
                this.Margin = new Thickness(20);
                this.BorderBrush = (Brush)Application.Current.Resources["SurfaceStrokeColorFlyoutBrush"];
                this.BorderThickness = new Thickness(2);
                this.Padding = new Thickness(8);
                this.CornerRadius = new CornerRadius(8);
                this.Height = 223;
                this.title = title;
                this.saveDataAction = saveDataAction;
            }

            private void CreateAndBeginStoryboard(DoubleAnimation animation, EventHandler<object> completedAction)
            {
                var storyboard = new Storyboard();
                Storyboard.SetTarget(animation, this);
                Storyboard.SetTargetProperty(animation, "Opacity");
                storyboard.Children.Add(animation);
                if (completedAction != null)
                {
                    animation.Completed += completedAction;
                }
                storyboard.Begin();
            }

            public void animateGrid(System.Action action)
            {
                DoubleAnimation fadeOutAnimation = new DoubleAnimation
                {
                    Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                    From = 1,
                    To = 0
                };

                EventHandler<object> fadeOutCompleted = null;
                fadeOutCompleted = (s, a) =>
                {
                    action.Invoke();
                    DoubleAnimation fadeInAnimation = new DoubleAnimation
                    {
                        Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                        From = 0,
                        To = 1
                    };
                    CreateAndBeginStoryboard(fadeInAnimation, null);
                    fadeOutAnimation.Completed -= fadeOutCompleted;
                };

                CreateAndBeginStoryboard(fadeOutAnimation, fadeOutCompleted);
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
                if (diffTime.Days == 0)
                {
                    textBlock5.Text = "今天就是";
                    textBlock6.Text = $"{textBox.Text}";
                    this.BorderBrush = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];
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
                // 定义行
                this.ColumnDefinitions.Clear();
                this.RowDefinitions.Clear();
                this.Children.Clear();

                this.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                this.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                this.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                this.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // 定义列
                this.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150), MinWidth = 50 });
                this.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250), MinWidth = 100 });

                // 创建 StackPanel

                button1.Content = button1Text;
                button2.Content = button2StackPanel;
                stackPanel.SetValue(Grid.RowProperty, 0);
                stackPanel.SetValue(Grid.ColumnProperty, 0);
                stackPanel.SetValue(Grid.ColumnSpanProperty, 2);

                textBlock2.Text = "配置";

                // 添加 StackPanel 到 Grid

                // 添加其他控件到 Grid

                textBlock3.SetValue(Grid.RowProperty, 1);
                textBlock3.SetValue(Grid.ColumnProperty, 0);

                textBox.SetValue(Grid.RowProperty, 1);
                textBox.SetValue(Grid.ColumnProperty, 1);

                textBlock4.SetValue(Grid.RowProperty, 2);
                textBlock4.SetValue(Grid.ColumnProperty, 0);

                datePicker.SetValue(Grid.RowProperty, 2);
                datePicker.SetValue(Grid.ColumnProperty, 1);

                button2.SetValue(Grid.RowProperty, 3);
                button2.SetValue(Grid.ColumnProperty, 1);
                button2.SetValue(StyleProperty, Application.Current.Resources["AccentButtonStyle"]);

                button3.Content = button3StackPanel;
                button3StackPanel.Children.Add(button3Text1);
                button3StackPanel.Children.Add(button3Text2);
                stackPanel.Children.Add(textBlock1);
                stackPanel.Children.Add(textBlock2);
                button2StackPanel.Children.Add(button2Text1);
                button2StackPanel.Children.Add(button2Text2);
                this.Children.Add(stackPanel);
                this.Children.Add(textBlock3);
                this.Children.Add(textBox);
                this.Children.Add(textBlock4);
                this.Children.Add(datePicker);
                this.Children.Add(button2);

                button2.Click += async (sender, e) =>
                {
                    if (datePicker.Date != null)
                    {
                        animateGrid(delegate () { expressGrid(); });
                        try
                        {
                            saveDataAction?.Invoke();
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
                            dialog.Background = (Brush)Application.Current.Resources["ContentDialogBackgroundThemeBrush"];

                            await dialog.ShowAsync();

                        }
                    }
                };
                button3.Click += async (sender, e) =>
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
                        dialog.Background = (Brush)Application.Current.Resources["ContentDialogBackgroundThemeBrush"];

                        await dialog.ShowAsync();
                    }
                };


                button3.SetValue(Grid.RowProperty, 3);
                button3.SetValue(Grid.ColumnProperty, 0);
                this.Children.Add(button3);
            }

            public void showEditGrid()
            {
                animateGrid(delegate () { editGrid(); });
            }
        }


        public DatePage()
        {
            InitializeComponent();

            ContentArea.SizeChanged += ContentArea_SizeChanged;
            ContentArea_SizeChanged(ContentArea, null);

            // 在页面加载时添加一个 DataBlock
            LoadData();
        }

        private void ContentArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double gridWidth = ContentArea.ActualWidth;
            int desiredColumnWidth = 400;

            int newColMax = Math.Max(1, (int)(gridWidth / desiredColumnWidth));

            if (newColMax != ColMax)
            {
                ColMax = newColMax;
                UpdateGridLayout();
            }
        }

        private void UpdateGridLayout()
        {
            ContentArea.ColumnDefinitions.Clear();
            ContentArea.RowDefinitions.Clear();


            // 添加列定义
            for (int i = 0; i < ColMax; i++)
            {
                ContentArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            int childCount = ContentArea.Children.Count;

            int columnsForItems = ColMax;
            int rowCount = (int)Math.Ceiling((double)childCount / columnsForItems);

            // 添加行定义
            for (int i = 0; i < rowCount; i++)
            {
                ContentArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // 重新排列子元素的位置
            for (int i = 0; i < childCount; i++)
            {
                int row = i / columnsForItems;
                int col = i % columnsForItems;

                var element = ContentArea.Children[i];
                element.SetValue(Grid.RowProperty, row);
                element.SetValue(Grid.ColumnProperty, col);
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

            UpdateGridLayout();
        }
        public void printAddRow()
        {
            title++;
            DataBlock dataBlock = new DataBlock(title, async () => await SaveData());
            dataBlock.editGrid();
            ContentArea.Children.Add(dataBlock);
            UpdateGridLayout();
        }

        private void newItemBar(object sender, RoutedEventArgs e)
        {
            printAddRow();
        }

        private async void deleteall(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog()
            {
                Title = "删除全部",
                Content = "此操作不可撤销",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary
            };
            dialog.Background = (Brush)Application.Current.Resources["ContentDialogBackgroundThemeBrush"];

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                ContentArea.Children.Clear();
                title = 0;
                printAddRow();
                await SaveData();
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
