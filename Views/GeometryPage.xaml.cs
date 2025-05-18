using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using eComBox.Services;
using Microsoft.Toolkit.Uwp;

namespace eComBox.Views
{
    public sealed partial class GeometryPage : Page, INotifyPropertyChanged
    {
        public GeometryPage()
        {
            InitializeComponent();

        }
        public int DPlace { get; set; } = 5;
        private async void Submit(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(Tri_a.Text, out double a) || !double.TryParse(Tri_b.Text, out double b) || !double.TryParse(Tri_c.Text, out double c))
            {
                ContentDialog dialog = new ContentDialog()
                {
                    Title = "Geometry_Error_Title".GetLocalized(),
                    Content = "Geometry_Error_InvalidNumber".GetLocalized(),
                    PrimaryButtonText = "DatePage_DeleteAll_PrimaryButton".GetLocalized(),
                    DefaultButton = ContentDialogButton.Primary
                };

                await dialog.ShowAsync();
                return;
            }
            //判断变量Tri_a,Tri_b, Tri_c的值是否大于0，若不是则弹出提示框
            if (a <= 0 || b <= 0 || c <= 0)
            {
                ContentDialog dialog = new ContentDialog()
                {
                    Title = "Geometry_Error_Title".GetLocalized(),
                    Content = "Geometry_Error_NegativeNumber".GetLocalized(),
                    PrimaryButtonText = "DatePage_DeleteAll_PrimaryButton".GetLocalized(),
                    DefaultButton = ContentDialogButton.Primary
                };

                await dialog.ShowAsync();
                return;
            }
            //判断变量Tri_a,Tri_b, Tri_c是否能构成三角形，若不能则弹出提示框
            if (a + b <= c || a + c <= b || b + c <= a)
            {
                ContentDialog dialog = new ContentDialog()
                {
                    Title = "Geometry_Error_NotTriangle".GetLocalized(),
                    Content = "Geometry_Error_TriangleRule".GetLocalized(),
                    PrimaryButtonText = "DatePage_DeleteAll_PrimaryButton".GetLocalized(),
                    DefaultButton = ContentDialogButton.Primary
                };
                await dialog.ShowAsync();
                return;
            }
            //让loaderring开始转动
            loaderring.IsIndeterminate = true;
            //计算三角形的周长
            double perimeter = a + b + c;
            //计算三角形的面积
            double p = perimeter / 2;
            double area = Math.Sqrt(p * (p - a) * (p - b) * (p - c));
            //将计算结果输出到界面
            square.Text = Math.Round(area, DPlace).ToString();
            circumference.Text= Math.Round(perimeter, DPlace).ToString();
            double cosA = (b * b + c * c - a * a) / (2 * b * c);
            cosineA.Text = Math.Round(cosA, DPlace).ToString();
            AngleA.Text= Math.Round((Math.Acos(cosA) * 180 / Math.PI), DPlace).ToString() + "Geometry_Angle_Degrees".GetLocalized();
            double cosB = (a * a + c * c - b * b) / (2 * a * c);
            cosineB.Text = Math.Round(cosB, DPlace).ToString();
            AngleB.Text= Math.Round((Math.Acos(cosB) * 180 / Math.PI), DPlace).ToString() + "Geometry_Angle_Degrees".GetLocalized();
            double cosC = (a * a + b * b - c * c) / (2 * a * b);
            cosineC.Text = Math.Round(cosC, DPlace).ToString();
            AngleC.Text = Math.Round((Math.Acos(cosC) * 180 / Math.PI), DPlace).ToString() + "Geometry_Angle_Degrees".GetLocalized();
            loaderring.Visibility = Visibility.Collapsed;
            //让loaderring停止转动
            loaderring.IsIndeterminate = false;
            //让Triangle绘制三角形
            double kNum = c / 150;
            Point pointB = new Point(350, 30);
            Point pointA = new Point(200, 30);
            PointCollection TrianglePoints = new PointCollection();
            TrianglePoints.Add(pointB);
            TrianglePoints.Add(pointA);

            double bNum = b / kNum;
            double sinA = Math.Sqrt(1 - (cosA * cosA));
            if (cosA >= 0)
            {
                Point pointC = new Point((bNum * cosA)+200, 30+(bNum * sinA));
                TrianglePoints.Add(pointC);
                TriangleShape.Points = TrianglePoints;
            }
            else
            {
                Point pointC = new Point((bNum * cosA) + 200, 30 + (bNum * sinA));
                TrianglePoints.Add(pointC);
                TriangleShape.Points = TrianglePoints;
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
