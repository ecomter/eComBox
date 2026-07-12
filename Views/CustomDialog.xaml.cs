using System;
using System.Collections.Generic;
using eComBox.Helpers;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Controls;

//https://go.microsoft.com/fwlink/?LinkId=234236 上介绍了“用户控件”项模板

namespace eComBox.Views
{
    public sealed partial class CustomDialog : UserControl
    {
        public event EventHandler<bool> DialogResult;
        public string ObjectKind => IsCircle ? "Circle" : IsEllipse ? "Ellipse" : IsHyperbola ? "Hyperbola" : "Line";
        public string Alias => (FindName("AliasBox") as TextBox)?.Text?.Trim();

        private static readonly List<string> TypeButtons = new List<string>
        {
            "LineTypeBtn", "CircleTypeBtn", "EllipseTypeBtn", "HyperbolaTypeBtn"
        };

        public CustomDialog()
        {
            this.InitializeComponent();
            Loaded += (_, __) => UpdatePreview();
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            // 用户点击“确定”，传递结果并关闭对话框
            DialogResult?.Invoke(this, true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 用户点击“取消”，传递结果并关闭对话框
            DialogResult?.Invoke(this, false);
        }

        private void TypeButton_Click(object sender, RoutedEventArgs e)
        {
            var clicked = sender as ToggleButton;
            if (clicked == null || clicked.IsChecked != true) return;

            foreach (var name in TypeButtons)
            {
                if (FindName(name) is ToggleButton btn && btn != clicked)
                    btn.IsChecked = false;
            }
            clicked.IsChecked = true;

            UpdateTypeVisibility();
        }

        private void LineModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateLineModeVisibility();
        private void ConicOrientation_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();
        public void LineInput_ValueChanged(Microsoft.UI.Xaml.Controls.NumberBox sender, Microsoft.UI.Xaml.Controls.NumberBoxValueChangedEventArgs args) => UpdatePreview();

        private void UpdateTypeVisibility()
        {
            var isLine = (FindName("LineTypeBtn") as ToggleButton)?.IsChecked == true;
            var lineSection = FindName("LineSection") as FrameworkElement;
            var circleSection = FindName("CircleSection") as FrameworkElement;
            var conicSection = FindName("ConicSection") as FrameworkElement;
            if (lineSection != null) lineSection.Visibility = isLine ? Visibility.Visible : Visibility.Collapsed;
            if (circleSection != null) circleSection.Visibility = IsCircle ? Visibility.Visible : Visibility.Collapsed;
            if (conicSection != null) conicSection.Visibility = IsEllipse || IsHyperbola ? Visibility.Visible : Visibility.Collapsed;
            if (isLine) UpdateLineModeVisibility();
            UpdatePreview();
        }

        private void UpdateLineModeVisibility()
        {
            var mode = GetRadioButtonsIndex("LineModeBox");
            var twoPoint = FindName("TwoPointSection") as FrameworkElement;
            var pointSlope = FindName("PointSlopeSection") as FrameworkElement;
            var general = FindName("GeneralSection") as FrameworkElement;
            if (twoPoint != null) twoPoint.Visibility = mode == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (pointSlope != null) pointSlope.Visibility = mode == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (general != null) general.Visibility = mode == 2 ? Visibility.Visible : Visibility.Collapsed;
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var previewBox = FindName("PreviewBox") as TextBox;
            if (previewBox == null) return;

            if (IsCircle)
            {
                previewBox.Text = string.Format("ShapeDialog_CirclePreview".GetLocalized(), GetNumber("CircleXBox"), GetNumber("CircleYBox"), GetNumber("RadiusBox"));
                return;
            }
            if (IsEllipse || IsHyperbola)
            {
                var key = IsEllipse ? "ShapeDialog_EllipsePreview" : "ShapeDialog_HyperbolaPreview";
                previewBox.Text = string.Format(key.GetLocalized(), GetNumber("ConicXBox"), GetNumber("ConicYBox"), GetNumber("ConicABox"), GetNumber("ConicBBox"), IsConicVertical ? "ShapeDialog_VerticalValue".GetLocalized() : "ShapeDialog_HorizontalValue".GetLocalized());
                return;
            }

            var mode = GetRadioButtonsIndex("LineModeBox");
            previewBox.Text = mode == 0
                ? string.Format("ShapeDialog_TwoPointPreview".GetLocalized(), GetNumber("LineX1Box"), GetNumber("LineY1Box"), GetNumber("LineX2Box"), GetNumber("LineY2Box"))
                : mode == 1
                    ? string.Format("ShapeDialog_PointSlopePreview".GetLocalized(), GetNumber("LinePXBox"), GetNumber("LinePYBox"), GetNumber("SlopeBox"))
                    : string.Format("ShapeDialog_GeneralPreview".GetLocalized(), GetNumber("LineABox"), GetNumber("LineBBox"), GetNumber("LineCBox"));
        }

        private double GetNumber(string name)
        {
            return FindName(name) is NumberBox box ? box.Value : 0;
        }

        private int GetRadioButtonsIndex(string name) => FindName(name) is Microsoft.UI.Xaml.Controls.RadioButtons rb ? rb.SelectedIndex : 0;

        public string GetAlias() => Alias;

        public bool IsCircle => (FindName("CircleTypeBtn") as ToggleButton)?.IsChecked == true;
        public bool IsEllipse => (FindName("EllipseTypeBtn") as ToggleButton)?.IsChecked == true;
        public bool IsHyperbola => (FindName("HyperbolaTypeBtn") as ToggleButton)?.IsChecked == true;
        public bool IsConicVertical => GetRadioButtonsIndex("ConicOrientationBox") == 1;

        public void SetInitialKind(bool circle)
        {
            if (FindName("LineTypeBtn") is ToggleButton lineBtn)
                lineBtn.IsChecked = !circle;
            if (FindName("CircleTypeBtn") is ToggleButton circleBtn)
                circleBtn.IsChecked = circle;
            UpdateTypeVisibility();
        }
    }
}
