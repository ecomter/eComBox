using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using eComBox.Helpers;
using eComBox.Models;
using eComBox.Services;
using Newtonsoft.Json;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

namespace eComBox.Views
{
    public sealed partial class TimeCounter : Page, INotifyPropertyChanged
    {
        private const string ViewModeSettingKey = "TimeCounter_ViewMode";
        private const string SortModeSettingKey = "TimeCounter_SortMode";

        // 延迟初始化 AI 服务，避免页面首次加载时的 I/O 阻塞
        private Lazy<QwenDatePredictionService> _lazyPredictionService = new Lazy<QwenDatePredictionService>(() => new QwenDatePredictionService());
        private Lazy<IAIService> _lazyAIService = new Lazy<IAIService>(() => new AIService());

        private QwenDatePredictionService _predictionService => _lazyPredictionService.Value;
        private IAIService _aiService => _lazyAIService.Value;

        private TimeCounterViewMode _viewMode = TimeCounterViewMode.Card;
        private TimeCounterSortMode _sortMode = TimeCounterSortMode.DateNearest;

        public ObservableCollection<TimeCounterCardViewModel> Cards { get; } = new ObservableCollection<TimeCounterCardViewModel>();

        public bool IsCardView => _viewMode == TimeCounterViewMode.Card;

        public bool IsListView => _viewMode == TimeCounterViewMode.List;

        public string SortModeDisplayText
        {
            get { return _sortMode == TimeCounterSortMode.Alphabetical ? "当前排序：A-Z" : "当前排序：日期由近到远"; }
        }

        public Visibility CardViewVisibility => IsCardView ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ListViewVisibility => IsListView ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EmptyStateVisibility => Cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public TimeCounter()
        {
            InitializeComponent();
            DataContext = this;
            _viewMode = GetSavedViewMode();
            _sortMode = GetSavedSortMode();
            UpdateViewButtons();
            Loaded += OnLoaded;
        }

        private TimeCounterViewMode GetSavedViewMode()
        {
            var raw = ApplicationData.Current.LocalSettings.Values[ViewModeSettingKey] as string;
            return string.Equals(raw, TimeCounterViewMode.List.ToString(), StringComparison.OrdinalIgnoreCase)
                ? TimeCounterViewMode.List
                : TimeCounterViewMode.Card;
        }

        private TimeCounterSortMode GetSavedSortMode()
        {
            var raw = ApplicationData.Current.LocalSettings.Values[SortModeSettingKey] as string;
            return string.Equals(raw, TimeCounterSortMode.Alphabetical.ToString(), StringComparison.OrdinalIgnoreCase)
                ? TimeCounterSortMode.Alphabetical
                : TimeCounterSortMode.DateNearest;
        }

        private void SetViewMode(TimeCounterViewMode mode)
        {
            _viewMode = mode;
            ApplicationData.Current.LocalSettings.Values[ViewModeSettingKey] = mode.ToString();

            OnPropertyChanged(nameof(IsCardView));
            OnPropertyChanged(nameof(IsListView));
            OnPropertyChanged(nameof(CardViewVisibility));
            OnPropertyChanged(nameof(ListViewVisibility));
            UpdateViewButtons();
        }

        private void SetSortMode(TimeCounterSortMode mode)
        {
            _sortMode = mode;
            ApplicationData.Current.LocalSettings.Values[SortModeSettingKey] = mode.ToString();
            OnPropertyChanged(nameof(SortModeDisplayText));
            ApplySorting();
        }

        private void UpdateViewButtons()
        {
            if (CardViewButton != null)
            {
                CardViewButton.IsChecked = IsCardView;
            }

            if (ListViewButton != null)
            {
                ListViewButton.IsChecked = IsListView;
            }
        }

        private void CardView_Click(object sender, RoutedEventArgs e)
        {
            SetViewMode(TimeCounterViewMode.Card);
        }

        private void ListView_Click(object sender, RoutedEventArgs e)
        {
            SetViewMode(TimeCounterViewMode.List);
        }

        private void SortByDate_Click(object sender, RoutedEventArgs e)
        {
            SetSortMode(TimeCounterSortMode.DateNearest);
        }

        private void SortByName_Click(object sender, RoutedEventArgs e)
        {
            SetSortMode(TimeCounterSortMode.Alphabetical);
        }

        private void ApplySorting()
        {
            var ordered = SortCards(Cards.ToList());
            Cards.Clear();
            foreach (var card in ordered)
            {
                Cards.Add(card);
            }

            AnimateListRefresh();
            OnPropertyChanged(nameof(EmptyStateVisibility));
        }

        private List<TimeCounterCardViewModel> SortCards(List<TimeCounterCardViewModel> cards)
        {
            if (cards == null || cards.Count == 0)
            {
                return new List<TimeCounterCardViewModel>();
            }

            if (_sortMode == TimeCounterSortMode.Alphabetical)
            {
                return cards
                    .OrderBy(card => card.TaskName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(card => card.TargetDate ?? DateTimeOffset.MaxValue)
                    .ThenBy(card => card.Title)
                    .ToList();
            }

            return cards
                .OrderBy(card => card.TargetDate.HasValue ? 0 : 1)
                .ThenBy(card => card.TargetDate ?? DateTimeOffset.MaxValue)
                .ThenBy(card => card.TaskName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(card => card.Title)
                .ToList();
        }

        private void AnimateListRefresh()
        {
            AnimatePanelPulse(CardCountdownList);
            AnimatePanelPulse(CountdownList);
        }

        private static void AnimatePanelPulse(FrameworkElement element)
        {
            if (element == null)
            {
                return;
            }

            var transform = element.RenderTransform as CompositeTransform;
            if (transform == null)
            {
                transform = new CompositeTransform();
                element.RenderTransform = transform;
                element.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            var storyboard = new Storyboard();

            var scaleX = new DoubleAnimation
            {
                From = 0.985,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleX, element);
            Storyboard.SetTargetProperty(scaleX, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");

            var scaleY = new DoubleAnimation
            {
                From = 0.985,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleY, element);
            Storyboard.SetTargetProperty(scaleY, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");

            var opacity = new DoubleAnimation
            {
                From = 0.85,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(220)
            };
            Storyboard.SetTarget(opacity, element);
            Storyboard.SetTargetProperty(opacity, "Opacity");

            storyboard.Children.Add(scaleX);
            storyboard.Children.Add(scaleY);
            storyboard.Children.Add(opacity);
            storyboard.Begin();
        }

        private void CardItem_Loaded(object sender, RoutedEventArgs e)
        {
            AnimateItemEntrance(sender as FrameworkElement, 0, 18);
        }

        private void ListItem_Loaded(object sender, RoutedEventArgs e)
        {
            AnimateItemEntrance(sender as FrameworkElement, 0, 14);
        }

        private void CardItem_Unloaded(object sender, RoutedEventArgs e)
        {
            AnimateItemExit(sender as FrameworkElement, 0, 10);
        }

        private void ListItem_Unloaded(object sender, RoutedEventArgs e)
        {
            AnimateItemExit(sender as FrameworkElement, 0, 8);
        }

        private static void AnimateItemEntrance(FrameworkElement element, double fromX, double fromY)
        {
            if (element == null)
            {
                return;
            }

            var transform = element.RenderTransform as CompositeTransform;
            if (transform == null)
            {
                transform = new CompositeTransform();
                element.RenderTransform = transform;
                element.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            element.Opacity = 0;
            transform.TranslateX = fromX;
            transform.TranslateY = fromY;
            transform.ScaleX = 0.96;
            transform.ScaleY = 0.96;

            var storyboard = new Storyboard();
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var opacity = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(280), EasingFunction = ease };
            Storyboard.SetTarget(opacity, element);
            Storyboard.SetTargetProperty(opacity, "Opacity");

            var translateY = new DoubleAnimation { From = fromY, To = 0, Duration = TimeSpan.FromMilliseconds(320), EasingFunction = ease };
            Storyboard.SetTarget(translateY, element);
            Storyboard.SetTargetProperty(translateY, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");

            var scaleX = new DoubleAnimation { From = 0.96, To = 1.0, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = ease };
            Storyboard.SetTarget(scaleX, element);
            Storyboard.SetTargetProperty(scaleX, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");

            var scaleY = new DoubleAnimation { From = 0.96, To = 1.0, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = ease };
            Storyboard.SetTarget(scaleY, element);
            Storyboard.SetTargetProperty(scaleY, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");

            storyboard.Children.Add(opacity);
            storyboard.Children.Add(translateY);
            storyboard.Children.Add(scaleX);
            storyboard.Children.Add(scaleY);
            storyboard.Begin();
        }

        private static async void AnimateItemExit(FrameworkElement element, double toX, double toY)
        {
            if (element == null)
            {
                return;
            }

            var transform = element.RenderTransform as CompositeTransform;
            if (transform == null)
            {
                transform = new CompositeTransform();
                element.RenderTransform = transform;
                element.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            var storyboard = new Storyboard();
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

            var opacity = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(180), EasingFunction = ease };
            Storyboard.SetTarget(opacity, element);
            Storyboard.SetTargetProperty(opacity, "Opacity");

            var translateY = new DoubleAnimation { To = toY, Duration = TimeSpan.FromMilliseconds(180), EasingFunction = ease };
            Storyboard.SetTarget(translateY, element);
            Storyboard.SetTargetProperty(translateY, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");

            var scaleX = new DoubleAnimation { To = 0.92, Duration = TimeSpan.FromMilliseconds(180), EasingFunction = ease };
            Storyboard.SetTarget(scaleX, element);
            Storyboard.SetTargetProperty(scaleX, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");

            var scaleY = new DoubleAnimation { To = 0.92, Duration = TimeSpan.FromMilliseconds(180), EasingFunction = ease };
            Storyboard.SetTarget(scaleY, element);
            Storyboard.SetTargetProperty(scaleY, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");

            storyboard.Children.Add(opacity);
            storyboard.Children.Add(translateY);
            storyboard.Children.Add(scaleX);
            storyboard.Children.Add(scaleY);
            storyboard.Begin();
            await Task.Delay(200);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            await LoadCardsAsync();
        }

        private async Task LoadCardsAsync()
        {
            Cards.Clear();
            foreach (var card in await CountdownStorageService.LoadCardsAsync())
            {
                Cards.Add(TimeCounterCardViewModel.FromModel(card));
            }

            ApplySorting();
        }

        private void SyncNotificationSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;
            foreach (var card in Cards)
            {
                settings.Values[$"Card_{card.Title}_Notification"] = card.EnableDateNotification;
            }
        }

        private async Task PersistCardsAsync()
        {
            SyncNotificationSettings();
            ApplySorting();
            var models = Cards.Select(card => card.ToModel()).ToList();
            await CountdownStorageService.SaveCardsAsync(models);
        }

        private async void AddCard_Click(object sender, RoutedEventArgs e)
        {
            var created = await ShowEditorDialogAsync();
            if (created == null)
            {
                return;
            }

            Cards.Add(created);
            await PersistCardsAsync();
        }

        private async void EditCard_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var card = button?.CommandParameter as TimeCounterCardViewModel;
            if (card == null)
            {
                return;
            }

            var edited = await ShowEditorDialogAsync(card);
            if (edited == null)
            {
                return;
            }

            card.TaskName = edited.TaskName;
            card.TargetDate = edited.TargetDate;
            card.EnableDateNotification = edited.EnableDateNotification;
            card.BorderColorHex = edited.BorderColorHex;
            card.RefreshDisplay();
            await PersistCardsAsync();
        }

        private async void DeleteCard_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var card = button?.CommandParameter as TimeCounterCardViewModel;
            if (card == null)
            {
                return;
            }

            var border = FindAncestor<Border>(button);
            AnimateItemExit(border, 0, 12);
            await Task.Delay(180);

            Cards.Remove(card);
            await PersistCardsAsync();
        }

        private async void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "DatePage_DeleteAll_Title".GetLocalized(),
                Content = "DatePage_DeleteAll_Content".GetLocalized(),
                PrimaryButtonText = "DatePage_DeleteAll_PrimaryButton".GetLocalized(),
                CloseButtonText = "DatePage_DeleteAll_CloseButton".GetLocalized(),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            Cards.Clear();
            await PersistCardsAsync();
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            var file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return;
            }

            var json = await FileIO.ReadTextAsync(file);
            var importedCards = JsonConvert.DeserializeObject<List<CountdownCardModel>>(json) ?? new List<CountdownCardModel>();
            if (importedCards.Count == 0)
            {
                await ShowSimpleDialogAsync("DatePage_importWarning_Title".GetLocalized(), "DatePage_importWarning_Content".GetLocalized());
                return;
            }

            Cards.Clear();
            foreach (var card in importedCards)
            {
                Cards.Add(TimeCounterCardViewModel.FromModel(card));
            }

            await PersistCardsAsync();
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("DatePage_Export_FileType".GetLocalized(), new List<string> { ".json" });
            picker.SuggestedFileName = "DatePage_Export_FileName".GetLocalized();

            var file = await picker.PickSaveFileAsync();
            if (file == null)
            {
                return;
            }

            var json = JsonConvert.SerializeObject(Cards.Select(card => card.ToModel()).ToList(), Formatting.Indented);
            await FileIO.WriteTextAsync(file, json);
        }

        private async void PinCard_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var card = button?.CommandParameter as TimeCounterCardViewModel;
            if (card == null)
            {
                return;
            }

            Frame.Navigate(typeof(FloatingCardPage), card.ToModel());
            await Task.CompletedTask;
        }

        private List<ColorOption> GetColorOptions()
        {
            return new List<ColorOption>
            {
                new ColorOption("gradient:aurora", "极光青蓝"),
                new ColorOption("gradient:sunset", "落日暖橙"),
                new ColorOption("gradient:starry", "星夜深蓝"),
                new ColorOption("gradient:forest", "森林青绿"),
                new ColorOption("gradient:lavender", "薰衣草紫"),
                new ColorOption("gradient:ocean", "深海蓝"),
                new ColorOption("gradient:candy", "糖果粉蓝"),
                new ColorOption("gradient:festive", "节日红金")
            };
        }

        private static Border CreateColorPreview(ColorOption option)
        {
            return new Border
            {
                Width = 56,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                Background = option.PreviewBrush,
                Margin = new Thickness(0, 0, 8, 0)
            };
        }

        private async Task<TimeCounterCardViewModel> ShowEditorDialogAsync(TimeCounterCardViewModel source = null)
        {
            try
            {
                var nameBox = new TextBox
                {
                    Text = source?.TaskName ?? string.Empty,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var picker = new CalendarDatePicker
                {
                    Date = source?.TargetDate,
                    DateFormat = "{year.full}-{month.integer}-{day.integer}",
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var colorOptions = GetColorOptions();
                var selectedColor = string.IsNullOrWhiteSpace(source?.BorderColorHex) ? "gradient:aurora" : source.BorderColorHex;
                var colorSelector = new ComboBox
                {
                    ItemsSource = colorOptions,
                    DisplayMemberPath = nameof(ColorOption.Name),
                    SelectedValuePath = nameof(ColorOption.Key),
                    SelectedValue = selectedColor,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var selectedOption = colorOptions.FirstOrDefault(x => x.Key == selectedColor) ?? colorOptions[0];
                var colorPreview = CreateColorPreview(selectedOption);

                colorSelector.SelectionChanged += (_, __) =>
                {
                    var option = colorSelector.SelectedItem as ColorOption;
                    if (option != null)
                    {
                        colorPreview.Background = option.PreviewBrush;
                    }
                };

                var colorRow = new Grid
                {
                    ColumnSpacing = 12,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                colorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                colorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Grid.SetColumn(colorPreview, 0);
                Grid.SetColumn(colorSelector, 1);
                colorRow.Children.Add(colorPreview);
                colorRow.Children.Add(colorSelector);

                var notifySwitch = new ToggleSwitch
                {
                    IsOn = source?.EnableDateNotification ?? false,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                var aiCard = new Border
                {
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(16),
                    Background = (Brush)Application.Current.Resources["AcrylicBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1)
                };

                var aiLoading = new ProgressRing
                {
                    Width = 24,
                    Height = 24,
                    IsActive = false,
                    Visibility = Visibility.Collapsed
                };

                bool aiEnabled = IsAiEnabled();

                var aiSummary = new TextBlock
                {
                    Text = aiEnabled ? "输入名称后，点击生成建议。" : "AI 功能未启用，请在设置中开启。",
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    TextWrapping = TextWrapping.Wrap
                };

                var suggestionsPanel = new StackPanel
                {
                    Spacing = 8,
                    Margin = new Thickness(0, 8, 0, 0)
                };

                var aiButton = new Button
                {
                    Content = "生成 AI 建议",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    IsEnabled = aiEnabled
                };

                aiButton.Click += async (_, __) =>
                {
                    if (!IsAiEnabled())
                    {
                        aiSummary.Text = "AI 功能未启用，请在设置中开启。";
                        return;
                    }

                    try
                    {
                        aiLoading.Visibility = Visibility.Visible;
                        aiLoading.IsActive = true;
                        suggestionsPanel.Children.Clear();
                        aiSummary.Text = "正在生成建议...";

                        var candidates = await GetAiSuggestionsAsync(nameBox.Text);
                        if (candidates.Count == 0)
                        {
                            aiSummary.Text = "AI 暂无可用建议，请手动选择日期。";
                            return;
                        }

                        aiSummary.Text = "已为你生成以下推荐，点击即可直接套用。";
                        BuildAiSuggestions(suggestionsPanel, picker, candidates);
                    }
                    catch (HttpRequestException)
                    {
                        aiSummary.Text = "AI 服务当前不可用，请检查网络或接口配置。";
                    }
                    catch (Exception ex)
                    {
                        aiSummary.Text = $"AI 预测失败：{ex.Message}";
                    }
                    finally
                    {
                        aiLoading.IsActive = false;
                        aiLoading.Visibility = Visibility.Collapsed;
                    }
                };

                var aiHeader = new Grid();
                aiHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                aiHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                aiHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var aiTitle = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalAlignment = VerticalAlignment.Center
                };
                aiTitle.Children.Add(new SymbolIcon { Symbol = Symbol.Favorite, Foreground = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"] });
                aiTitle.Children.Add(new TextBlock
                {
                    Text = "AI 智能建议",
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                });
                Grid.SetColumn(aiTitle, 0);
                Grid.SetColumnSpan(aiTitle, 2);
                aiHeader.Children.Add(aiTitle);
                Grid.SetColumn(aiLoading, 2);
                aiHeader.Children.Add(aiLoading);

                var aiContent = new StackPanel
                {
                    Spacing = 10
                };
                aiContent.Children.Add(aiHeader);
                aiContent.Children.Add(new TextBlock
                {
                    Text = "推荐结果",
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold
                });
                aiContent.Children.Add(aiSummary);
                aiContent.Children.Add(aiButton);
                aiContent.Children.Add(suggestionsPanel);
                aiCard.Child = aiContent;

                var form = new Grid
                {
                    MinWidth = 560,
                    MaxWidth = 760,
                    RowSpacing = 16,
                    ColumnSpacing = 12,
                    Margin = new Thickness(0, 4, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                AddFormRow(form, 0, "事件名称", nameBox);
                AddFormRow(form, 1, "目标日期", picker);
                AddFormRow(form, 2, "主题色", colorRow);
                AddFormRow(form, 3, "通知", notifySwitch);
                AddFormRow(form, 4, "AI 预测", aiCard, false);

                var availableHeight = Math.Max(420.0, Window.Current.Bounds.Height - 220.0);
                var scroll = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    MaxHeight = availableHeight,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Content = form
                };

                var dialogRoot = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    MinWidth = 560,
                    MaxWidth = 920,
                    Padding = new Thickness(38, 12, 24, 12)
                };
                dialogRoot.Children.Add(scroll);

                var dialog = new ContentDialog
                {
                    Title = source == null ? "DatePage_NewButton".GetLocalized() : "DatePage_EditDialog_Title".GetLocalized(),
                    PrimaryButtonText = "DatePage_EditDialog_PrimaryButton".GetLocalized(),
                    CloseButtonText = "DatePage_EditDialog_CloseButton".GetLocalized(),
                    DefaultButton = ContentDialogButton.Primary,
                    Content = dialogRoot,
                    FullSizeDesired = false
                };

                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    return null;
                }

                var nextId = source?.Title ?? (Cards.Count == 0 ? 1 : Cards.Max(card => card.Title) + 1); // 保持原有 ID 生成逻辑，不影响展示行为
                var colorKey = colorSelector.SelectedValue as string;
                if (string.IsNullOrWhiteSpace(colorKey))
                {
                    colorKey = "gradient:aurora";
                }

                return new TimeCounterCardViewModel
                {
                    Title = nextId,
                    TaskName = string.IsNullOrWhiteSpace(nameBox.Text) ? string.Empty : nameBox.Text.Trim(),
                    TargetDate = picker.Date,
                    EnableDateNotification = notifySwitch.IsOn,
                    BorderColorHex = colorKey
                };
            }
            catch (Exception ex)
            {
                await ShowSimpleDialogAsync("编辑失败", ex.Message);
                return null;
            }
        }

        private static void AddFormRow(Grid grid, int rowIndex, string label, UIElement control, bool addLabel = true)
        {
            var item = new StackPanel
            {
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            if (addLabel)
            {
                item.Children.Add(new TextBlock
                {
                    Text = label,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold
                });
            }

            if (control != null)
            {
                if (control is FrameworkElement frameworkElement)
                {
                    frameworkElement.HorizontalAlignment = HorizontalAlignment.Stretch;
                }

                item.Children.Add(control);
            }

            Grid.SetRow(item, rowIndex);
            grid.Children.Add(item);
        }

        private bool IsAiEnabled()
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue("AIEnabled", out object aiEnabled) && aiEnabled is bool enabled)
                {
                    return enabled;
                }
            }
            catch
            {
                // 忽略任何读取配置时的异常，默认返回 false
            }

            return false;
        }

        private async Task<List<DateSuggestion>> GetAiSuggestionsAsync(string taskName)
        {
            if (string.IsNullOrWhiteSpace(taskName))
            {
                return new List<DateSuggestion>();
            }

            // 检查每日使用限制
            if (!await AIUsageService.CanUseAIAsync())
            {
                throw new InvalidOperationException("DatePage_AI_Limit".GetLocalized());
            }

            var results = new List<DateSuggestion>();
            var aiResult = await _aiService.PredictDateFromTaskNameAsync(taskName);
            if (aiResult != null)
            {
                results.AddRange(aiResult.GetSortedSuggestions());
            }

            // AI 调用成功后增加使用计数
            await AIUsageService.IncrementUsageAsync();

            var distinct = new List<DateSuggestion>();
            foreach (var item in results)
            {
                if (!distinct.Any(x => x.SuggestedDate.Date == item.SuggestedDate.Date))
                {
                    distinct.Add(item);
                }
            }

            return distinct;
        }

        private void BuildAiSuggestions(StackPanel suggestionsPanel, CalendarDatePicker picker, List<DateSuggestion> candidates)
        {
            suggestionsPanel.Children.Clear();

            foreach (var suggestion in candidates.Take(3))
            {
                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(12, 10, 12, 10),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    Tag = suggestion,
                    MinWidth = 440,
                    MaxWidth = 640
                };

                var row = new Grid
                {
                    ColumnSpacing = 10
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var accent = new Border
                {
                    Width = 10,
                    CornerRadius = new CornerRadius(999),
                    Background = suggestion.Confidence >= 0.8
                        ? (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"]
                        : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                };
                Grid.SetColumn(accent, 0);
                row.Children.Add(accent);

                var text = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 2
                };
                text.Children.Add(new TextBlock
                {
                    Text = suggestion.SuggestedDate.ToString("yyyy-MM-dd"),
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold
                });
                text.Children.Add(new TextBlock
                {
                    Text = suggestion.Reason,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    TextWrapping = TextWrapping.Wrap
                });
                Grid.SetColumn(text, 1);
                row.Children.Add(text);

                button.Content = row;
                button.Click += (_, __) =>
                {
                    picker.Date = new DateTimeOffset(suggestion.SuggestedDate);
                };

                suggestionsPanel.Children.Add(button);
            }
        }

        private async Task<DateSuggestion> SuggestDateAsync(string taskName)
        {
            if (string.IsNullOrWhiteSpace(taskName))
            {
                return null;
            }

            // 检查每日使用限制
            if (!await AIUsageService.CanUseAIAsync())
            {
                return null;
            }

            var prediction = await _aiService.PredictDateFromTaskNameAsync(taskName);

            // AI 调用成功后增加使用计数
            await AIUsageService.IncrementUsageAsync();

            var best = prediction?.GetSortedSuggestions()?.FirstOrDefault();
            if (best != null)
            {
                return best;
            }

            return null;
        }

        private async Task ShowSimpleDialogAsync(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }

        private static T FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            var current = start;
            while (current != null)
            {
                if (current is T typed)
                {
                    return typed;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public enum TimeCounterViewMode
    {
        Card,
        List
    }

    public enum TimeCounterSortMode
    {
        DateNearest,
        Alphabetical
    }

    public sealed class ColorOption
    {
        public ColorOption(string key, string name)
        {
            Key = key;
            Name = name;
            PreviewBrush = TimeCounterCardViewModel.CreatePreviewBrush(key);
        }

        public string Key { get; }

        public string Name { get; }

        public Brush PreviewBrush { get; }
    }

    public sealed class TimeCounterCardViewModel : INotifyPropertyChanged
    {
        private static readonly Dictionary<string, Color[]> GradientPalette = new Dictionary<string, Color[]>
        {
            ["gradient:aurora"] = new[] { Color.FromArgb(255, 0, 210, 153), Color.FromArgb(255, 86, 152, 214), Color.FromArgb(255, 124, 95, 189) },
            ["gradient:sunset"] = new[] { Color.FromArgb(255, 255, 126, 95), Color.FromArgb(255, 254, 180, 123), Color.FromArgb(255, 152, 80, 160) },
            ["gradient:starry"] = new[] { Color.FromArgb(255, 8, 24, 58), Color.FromArgb(255, 37, 25, 84), Color.FromArgb(255, 74, 30, 93) },
            ["gradient:forest"] = new[] { Color.FromArgb(255, 16, 85, 43), Color.FromArgb(255, 50, 142, 69), Color.FromArgb(255, 130, 180, 64) },
            ["gradient:lavender"] = new[] { Color.FromArgb(255, 182, 154, 255), Color.FromArgb(255, 128, 90, 213), Color.FromArgb(255, 87, 54, 153) },
            ["gradient:ocean"] = new[] { Color.FromArgb(255, 86, 171, 199), Color.FromArgb(255, 55, 109, 138), Color.FromArgb(255, 18, 52, 86) },
            ["gradient:candy"] = new[] { Color.FromArgb(255, 255, 107, 160), Color.FromArgb(255, 255, 154, 187), Color.FromArgb(255, 118, 168, 255) },
            ["gradient:festive"] = new[] { Color.FromArgb(255, 210, 4, 45), Color.FromArgb(255, 255, 64, 25), Color.FromArgb(255, 255, 196, 25) }
        };

        private string _taskName;
        private DateTimeOffset? _targetDate;
        private bool _enableDateNotification;
        private string _borderColorHex = "gradient:aurora";

        public int Title { get; set; }

        public string TaskName
        {
            get { return _taskName; }
            set
            {
                if (_taskName == value)
                {
                    return;
                }

                _taskName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPastDue));
                OnPropertyChanged(nameof(PastMarkerVisibility));
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged(nameof(TargetDateText));
                OnPropertyChanged(nameof(TargetDateSecondaryVisibility));
                OnPropertyChanged(nameof(DisplayTitleFontSize));
            }
        }

        public DateTimeOffset? TargetDate
        {
            get { return _targetDate; }
            set
            {
                if (_targetDate == value)
                {
                    return;
                }

                _targetDate = value;
                RefreshDisplay();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPastDue));
                OnPropertyChanged(nameof(PastMarkerVisibility));
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged(nameof(DisplayTitleFontSize));
            }
        }

        public bool EnableDateNotification
        {
            get { return _enableDateNotification; }
            set
            {
                if (_enableDateNotification == value)
                {
                    return;
                }

                _enableDateNotification = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NotificationMarkerVisibility));
            }
        }

        public string BorderColorHex
        {
            get { return _borderColorHex; }
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "gradient:aurora" : value;
                if (_borderColorHex == next)
                {
                    return;
                }

                _borderColorHex = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CardAccentBrush));
            }
        }

        public Visibility NotificationMarkerVisibility => EnableDateNotification ? Visibility.Visible : Visibility.Collapsed;

        public bool IsPastDue => TargetDate.HasValue && TargetDate.Value.Date < DateTimeOffset.Now.Date;

        public Visibility PastMarkerVisibility => IsPastDue ? Visibility.Visible : Visibility.Collapsed;

        public string RelativeHintText => GetRelativeHintText(TargetDate);

        public Visibility RelativeHintVisibility => TargetDate.HasValue ? Visibility.Visible : Visibility.Collapsed;
 
         public Brush CardAccentBrush => CreateGradientBrush(BorderColorHex);

        public string TargetDateText => HasCustomTaskName ? (TargetDate?.ToString("yyyy-MM-dd") ?? "未设置目标日期") : string.Empty;

        public string CountdownText => GetCountdownText(TargetDate?.Date);

        public string DisplayTitle => HasCustomTaskName
            ? _taskName
            : TargetDate?.ToString("yyyy-MM-dd") ?? "未设置目标日期";

        public double DisplayTitleFontSize => HasCustomTaskName ? (double)16 : (double)20;

        public Visibility TargetDateSecondaryVisibility => TargetDate.HasValue ? Visibility.Visible : Visibility.Collapsed;

        private bool HasCustomTaskName => !string.IsNullOrWhiteSpace(_taskName) && !string.Equals(_taskName, "未命名事件", StringComparison.Ordinal);

        public static TimeCounterCardViewModel FromModel(CountdownCardModel model)
        {
            var viewModel = new TimeCounterCardViewModel
            {
                Title = model.Title,
                TaskName = string.IsNullOrWhiteSpace(model.TaskName) ? string.Empty : model.TaskName,
                TargetDate = model.TargetDate.HasValue ? new DateTimeOffset(model.TargetDate.Value) : (DateTimeOffset?)null,
                BorderColorHex = string.IsNullOrWhiteSpace(model.BorderColorHex) ? "gradient:aurora" : model.BorderColorHex,
                EnableDateNotification = model.EnableDateNotification
            };

            viewModel.RefreshDisplay();
            return viewModel;
        }

        public CountdownCardModel ToModel()
        {
            return new CountdownCardModel
            {
                Title = Title,
                TaskName = TaskName,
                TargetDate = TargetDate?.Date,
                BorderColorHex = BorderColorHex,
                EnableDateNotification = EnableDateNotification,
                DisplayText = CountdownText
            };
        }

        public void RefreshDisplay()
        {
            OnPropertyChanged(nameof(TargetDateText));
            OnPropertyChanged(nameof(CountdownText));
            OnPropertyChanged(nameof(CardAccentBrush));
            OnPropertyChanged(nameof(IsPastDue));
            OnPropertyChanged(nameof(PastMarkerVisibility));
            OnPropertyChanged(nameof(RelativeHintText));
            OnPropertyChanged(nameof(RelativeHintVisibility));
        }

        public static Brush CreatePreviewBrush(string gradientKey)
        {
            return CreateGradientBrush(gradientKey);
        }

        private static Brush CreateGradientBrush(string gradientKey)
        {
            if (!GradientPalette.TryGetValue(gradientKey ?? string.Empty, out Color[] colors))
            {
                colors = GradientPalette["gradient:aurora"];
            }

            var brush = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0)
            };

            var step = colors.Length > 1 ? 1.0 / (colors.Length - 1) : 1.0;
            for (var i = 0; i < colors.Length; i++)
            {
                brush.GradientStops.Add(new GradientStop
                {
                    Color = colors[i],
                    Offset = step * i
                });
            }

            return brush;
        }

        private static string GetCountdownText(DateTimeOffset? targetDate)
        {
            if (!targetDate.HasValue)
            {
                return "等待设置日期";
            }

            var days = (targetDate.Value.Date - DateTimeOffset.Now.Date).Days;
            if (days > 0)
            {
                return $"还有 {days} 天";
            }

            if (days == 0)
            {
                return "就是今天";
            }

            return $"已过 {-days} 天";
        }

        private static string GetRelativeHintText(DateTimeOffset? targetDate)
        {
            if (!targetDate.HasValue)
            {
                return string.Empty;
            }

            var days = (targetDate.Value.Date - DateTimeOffset.Now.Date).Days;
            var absDays = Math.Abs(days);

            if (days == 0)
            {
                return "今天";
            }

            if (days == 1)
            {
                return "明天";
            }

            if (days == 2)
            {
                return "后天";
            }

            if (days == -1)
            {
                return "昨天";
            }

            if (days == -2)
            {
                return "前天";
            }

            if (absDays <= 30)
            {
                return days > 0 ? "1月内" : "1月前";
            }

            if (absDays <= 90)
            {
                return days > 0 ? "3月内" : "3月前";
            }

            if (absDays <= 180)
            {
                return days > 0 ? "半年内" : "半年前";
            }

            if (absDays <= 365)
            {
                return days > 0 ? "1年内" : "1年前";
            }

            return days > 0 ? "1年以上" : "1年以上前";
        }

         public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
