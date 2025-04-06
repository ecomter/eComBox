using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace eComBox.Views
{
    public sealed partial class FloatingCardPage : Page
    {
        private DatePage.DataBlockModel _cardData;
        private DatePage.DataBlock _dataBlock;

        // 16:9比例常量
        private const double ASPECT_RATIO = 16.0 / 9.0;
        private const double DEFAULT_WIDTH = 100.0;

        public FloatingCardPage()
        {
            this.InitializeComponent();

            // 禁用页面级的右键菜单
            this.IsRightTapEnabled = false;

            // 注册加载事件
            this.Loaded += FloatingCardPage_Loaded;
        }

        private void FloatingCardPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 完全隐藏标题栏
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            // 隐藏系统按钮
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = Colors.Transparent;

            // 设置窗口背景透明度
            var rootVisual = ElementCompositionPreview.GetElementVisual(RootGrid);
            rootVisual.Opacity = 0.95f;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is DatePage.DataBlockModel cardData)
            {
                _cardData = cardData;
                CreateCardFromData(cardData);
            }

            // 不再使用紧凑叠加视图，而是调整窗口大小并设置固定位置
            MakeDesktopWindow();
        }

        private void CreateCardFromData(DatePage.DataBlockModel cardData)
        {
            _dataBlock = new DatePage.DataBlock(cardData.Title, null)
            {
                textBox = { Text = cardData.TaskName },
                datePicker = { Date = cardData.TargetDate },
                textBlock2 = { Text = cardData.DisplayText },
                BorderColorHex = cardData.BorderColorHex ?? ""
            };

            if (cardData.TargetDate.HasValue)
            {
                _dataBlock.expressGrid();
            }
            else
            {
                _dataBlock.editGrid();
            }

            // 调整卡片大小以适应悬浮窗
            _dataBlock.MaxWidth = double.PositiveInfinity;
            _dataBlock.HorizontalAlignment = HorizontalAlignment.Stretch;
            _dataBlock.VerticalAlignment = VerticalAlignment.Stretch;
            _dataBlock.Margin = new Thickness(8);

            // 完全禁用DataBlock的所有交互功能
            _dataBlock.IsRightTapEnabled = false;

            // 禁用内部的编辑按钮
            if (_dataBlock.button1r != null)
            {
                _dataBlock.button1r.Visibility = Visibility.Collapsed;
            }

            // 添加到容器
            CardContainer.Children.Clear();
            CardContainer.Children.Add(_dataBlock);

            // 创建透明的覆盖层以拦截所有右键点击等交互
            var overlay = new Grid
            {
                Background = new SolidColorBrush(Colors.Transparent),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsRightTapEnabled = false
            };

            // 阻止所有右键菜单
            overlay.RightTapped += (s, args) =>
            {
                args.Handled = true; // 拦截事件，不做任何处理
            };

            // 添加覆盖层
            CardContainer.Children.Add(overlay);
        }

        private async void MakeDesktopWindow()
        {
            try
            {
                ApplicationView view = ApplicationView.GetForCurrentView();

                // 计算16:9比例的高度
                double height = DEFAULT_WIDTH / ASPECT_RATIO;

                // 设置窗口透明度和无边框效果
                var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
                RootGrid.Background = new SolidColorBrush(Colors.Transparent);

                // 设置合适的大小
                bool success = view.TryResizeView(new Size(DEFAULT_WIDTH, height));

                // 使用默认模式而不是CompactOverlay
                await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(
                    ApplicationViewMode.Default);

                // 移除所有边距
                CardContainer.Margin = new Thickness(0);
                RootGrid.Padding = new Thickness(0);

                // 禁止调整大小
                view.SetPreferredMinSize(new Size(DEFAULT_WIDTH, height));

                // 将窗口放在屏幕某个不常用的角落
                // 注意：UWP不允许直接设置窗口位置，这只能通过系统来控制
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = $"无法创建桌面小部件: {ex.Message}",
                    CloseButtonText = "确定"
                };
                await dialog.ShowAsync();
            }
        }

        private async Task CloseFloatingWindow()
        {
            try
            {
                // 直接关闭视图
                ApplicationView.GetForCurrentView().Consolidated += (s, args) =>
                {
                    Window.Current.Close();
                };

                await ApplicationView.GetForCurrentView().TryConsolidateAsync();
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = $"无法关闭窗口: {ex.Message}",
                    CloseButtonText = "确定"
                };
                await dialog.ShowAsync();
            }
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            await CloseFloatingWindow();
        }
    }
}
