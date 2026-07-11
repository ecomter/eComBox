using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using Windows.UI.Xaml;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.UI;
using Windows.ApplicationModel.DataTransfer;
using eComBox.Services;
using Microsoft.Toolkit.Uwp;

namespace eComBox.Views
{
    public sealed partial class GeometryPage : Page, INotifyPropertyChanged
    {
        public enum GeometryEntityType { Line, Circle }

        public sealed class GeometryEntity
        {
            public string Name { get; set; }
            public string Alias { get; set; }
            public GeometryEntityType Type { get; set; }
            public string KindName => (Type == GeometryEntityType.Line ? "Geometry_Kind_Line" : "Geometry_Kind_Circle").GetLocalized();
            public string Summary { get; set; }

            public LineData Line { get; set; }
            public CircleData Circle { get; set; }
        }

        public sealed class LineData
        {
            public double A { get; set; }
            public double B { get; set; }
            public double C { get; set; }

            public static LineData FromTwoPoints(double x1, double y1, double x2, double y2)
            {
                return new LineData { A = y2 - y1, B = x1 - x2, C = x2 * y1 - x1 * y2 };
            }

            public static LineData FromPointSlope(double x, double y, double slope)
            {
                return double.IsInfinity(slope)
                    ? new LineData { A = 1, B = 0, C = -x }
                    : new LineData { A = slope, B = -1, C = y - slope * x };
            }

            public static LineData FromGeneral(double a, double b, double c) => new LineData { A = a, B = b, C = c };

            public bool IsVertical => Math.Abs(B) < 1e-10;

            public double? Slope => IsVertical ? (double?)null : -A / B;
            public double? Intercept => IsVertical ? (double?)null : -C / B;
            public double XIntercept => Math.Abs(A) < 1e-10 ? double.NaN : -C / A;

            public double YAt(double x)
            {
                if (IsVertical) return double.NaN;
                return (-A * x - C) / B;
            }

            public string ToGeneralString(int digits)
            {
                return $"{Math.Round(A, digits)}x + {Math.Round(B, digits)}y + {Math.Round(C, digits)} = 0";
            }

            public string ToSlopeString(int digits)
            {
                if (IsVertical) return $"x = {Math.Round(XIntercept, digits)}";
                var m = Math.Round(Slope ?? 0, digits);
                var b = Math.Round(Intercept ?? 0, digits);
                return $"y = {m}x + {b}";
            }
        }

        public sealed class CircleData
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double R { get; set; }

            public string ToEquation(int digits) => $"(x - {Math.Round(X, digits)})² + (y - {Math.Round(Y, digits)})² = {Math.Round(R * R, digits)}";
        }

        public ObservableCollection<GeometryEntity> Entities { get; } = new ObservableCollection<GeometryEntity>();
        public ObservableCollection<string> IntersectionResults { get; } = new ObservableCollection<string>();
        private readonly List<Point> _intersectionPoints = new List<Point>();
        private double _plotZoom = 1.0;
        private bool _showGrid = true;
        private object _selectedEntity;

        public object SelectedEntity
        {
            get => _selectedEntity;
            set
            {
                _selectedEntity = value;
                OnPropertyChanged(nameof(SelectedEntity));
            }
        }

        public GeometryPage()
        {
            InitializeComponent();
            DataContext = this;
            Entities.CollectionChanged += (_, __) => RefreshWorkspace();
            IntersectionResults.CollectionChanged += (_, __) => RefreshCounts();
            GetListView("EntityList").ItemsSource = Entities;
            GetListView("IntersectionList").ItemsSource = IntersectionResults;
            Loaded += (_, __) =>
            {
                RefreshWorkspace();
            };

        }
        public int DPlace { get; set; } = 5;
        private async void Submit(object sender, RoutedEventArgs e)
        {
            DPlace = Math.Max(1, Math.Min(10, (int)DemPlace.Value));
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
            var angleA = Math.Acos(cosA) * 180 / Math.PI;
            var angleB = Math.Acos(cosB) * 180 / Math.PI;
            var angleC = Math.Acos(cosC) * 180 / Math.PI;
            var sideType = NearlyEqual(a, b) && NearlyEqual(b, c)
                ? "Geometry_Triangle_Equilateral".GetLocalized()
                : NearlyEqual(a, b) || NearlyEqual(a, c) || NearlyEqual(b, c)
                    ? "Geometry_Triangle_Isosceles".GetLocalized()
                    : "Geometry_Triangle_Scalene".GetLocalized();
            var maxAngle = Math.Max(angleA, Math.Max(angleB, angleC));
            var angleType = Math.Abs(maxAngle - 90) < 1e-7
                ? "Geometry_Triangle_Right".GetLocalized()
                : maxAngle > 90
                    ? "Geometry_Triangle_Obtuse".GetLocalized()
                    : "Geometry_Triangle_Acute".GetLocalized();
            var inradius = area / p;
            var circumradius = a * b * c / (4 * area);
            TriangleAnalysis.Text = string.Format(
                "Geometry_Triangle_AnalysisResult".GetLocalized(),
                sideType,
                angleType,
                Format(inradius),
                Format(circumradius),
                Format(2 * area / a),
                Format(2 * area / b),
                Format(2 * area / c));
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

        private async void AddShapeViaDialog(object sender, RoutedEventArgs e) => await ShowGeometryDialog();

        private void EntityList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = GetListView("EntityList")?.SelectedItem as GeometryEntity;
            DuplicateEntityButton.IsEnabled = item != null;
            if (item != null)
            {
                UpdateSelectionDetails(item);
            }
        }

        private void DeleteSelectedEntity(object sender, RoutedEventArgs e)
        {
            if (GetListView("EntityList")?.SelectedItem is GeometryEntity item)
                Entities.Remove(item);
            UpdateSelectionDetails(null);
            RefreshWorkspace();
        }

        private void DuplicateSelectedEntity(object sender, RoutedEventArgs e)
        {
            if (!(GetListView("EntityList")?.SelectedItem is GeometryEntity source)) return;

            var copyIndex = Entities.Count(entity => entity.Name.StartsWith(source.Name + " ", StringComparison.CurrentCultureIgnoreCase)) + 1;
            var copy = new GeometryEntity
            {
                Name = string.Format("Geometry_CopyName".GetLocalized(), source.Name, copyIndex),
                Alias = source.Alias,
                Type = source.Type
            };

            if (source.Type == GeometryEntityType.Line)
            {
                copy.Line = new LineData { A = source.Line.A, B = source.Line.B, C = source.Line.C - source.Line.A - source.Line.B };
                copy.Summary = copy.Line.ToSlopeString(DPlace);
            }
            else
            {
                copy.Circle = new CircleData { X = source.Circle.X + 1, Y = source.Circle.Y + 1, R = source.Circle.R };
                copy.Summary = $"r = {Math.Round(copy.Circle.R, DPlace)}";
            }

            Entities.Add(copy);
            EntityList.SelectedItem = copy;
        }

        private void ClearEntities(object sender, RoutedEventArgs e)
        {
            Entities.Clear();
            IntersectionResults.Clear();
            _intersectionPoints.Clear();
            UpdateSelectionDetails(null);
            RefreshWorkspace();
        }

        private async System.Threading.Tasks.Task ShowGeometryDialog()
        {
            var dialog = new CustomDialog();

            var content = new ContentDialog
            {
                Title = "ShapeDialog_Title".GetLocalized(),
                Content = dialog,
                PrimaryButtonText = "Common_OK".GetLocalized(),
                CloseButtonText = "DatePage_DeleteAll_CloseButton".GetLocalized(),
                DefaultButton = ContentDialogButton.Primary,
                FullSizeDesired = false
            };

            var result = await content.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            if (dialog.IsCircle)
                CreateCircleFromDialog(dialog);
            else
                CreateLineFromDialog(dialog);
        }

        private void CreateLineFromDialog(CustomDialog dialog)
        {
            var lineMode = dialog.FindName("LineModeBox") as Microsoft.UI.Xaml.Controls.RadioButtons;
            if (lineMode == null) return;

            LineData line;
            if (lineMode.SelectedIndex == 0)
            {
                line = LineData.FromTwoPoints(GetDialogNumber(dialog, "LineX1Box"), GetDialogNumber(dialog, "LineY1Box"), GetDialogNumber(dialog, "LineX2Box"), GetDialogNumber(dialog, "LineY2Box"));
            }
            else if (lineMode.SelectedIndex == 1)
            {
                line = LineData.FromPointSlope(GetDialogNumber(dialog, "LinePXBox"), GetDialogNumber(dialog, "LinePYBox"), GetDialogNumber(dialog, "SlopeBox"));
            }
            else
            {
                line = LineData.FromGeneral(GetDialogNumber(dialog, "LineABox"), GetDialogNumber(dialog, "LineBBox"), GetDialogNumber(dialog, "LineCBox"));
            }

            if (line == null || Math.Abs(line.A) + Math.Abs(line.B) < 1e-10)
            {
                ShowOperationMessage("Geometry_InvalidLine".GetLocalized());
                return;
            }
            var alias = dialog.GetAlias();
            var name = string.IsNullOrWhiteSpace(alias) ? $"L{Entities.Count(x => x.Type == GeometryEntityType.Line) + 1}" : alias;
            Entities.Add(new GeometryEntity
            {
                Name = name,
                Alias = alias,
                Type = GeometryEntityType.Line,
                Line = line,
                Summary = line.ToSlopeString(DPlace)
            });
            EntityList.SelectedItem = Entities.Last();
            RefreshWorkspace();
        }

        private void CreateCircleFromDialog(CustomDialog dialog)
        {
            var circle = new CircleData
            {
                X = GetDialogNumber(dialog, "CircleXBox"),
                Y = GetDialogNumber(dialog, "CircleYBox"),
                R = GetDialogNumber(dialog, "RadiusBox")
            };

            if (double.IsNaN(circle.R) || double.IsInfinity(circle.R) || circle.R <= 0)
            {
                ShowOperationMessage("Geometry_InvalidRadius".GetLocalized());
                return;
            }
            var alias = dialog.GetAlias();
            var name = string.IsNullOrWhiteSpace(alias) ? $"C{Entities.Count(x => x.Type == GeometryEntityType.Circle) + 1}" : alias;
            Entities.Add(new GeometryEntity
            {
                Name = name,
                Alias = alias,
                Type = GeometryEntityType.Circle,
                Circle = circle,
                Summary = $"r = {Math.Round(circle.R, DPlace)}"
            });
            EntityList.SelectedItem = Entities.Last();
            RefreshWorkspace();
        }

        private double GetDialogNumber(CustomDialog dialog, string name) => dialog.FindName(name) is Microsoft.UI.Xaml.Controls.NumberBox box ? box.Value : 0;

        private void ComputeIntersections(object sender, RoutedEventArgs e)
        {
            IntersectionResults.Clear();
            _intersectionPoints.Clear();
            var lines = Entities.Where(x => x.Type == GeometryEntityType.Line).ToList();
            var circles = Entities.Where(x => x.Type == GeometryEntityType.Circle).ToList();

            for (int i = 0; i < lines.Count; i++)
            for (int j = i + 1; j < lines.Count; j++)
            {
                var p = Intersect(lines[i].Line, lines[j].Line);
                if (p.HasValue) AddIntersection(lines[i].Name, lines[j].Name, p.Value);
            }

            for (int i = 0; i < lines.Count; i++)
            for (int j = 0; j < circles.Count; j++)
            {
                foreach (var p in Intersect(lines[i].Line, circles[j].Circle))
                    AddIntersection(lines[i].Name, circles[j].Name, p);
            }

            for (int i = 0; i < circles.Count; i++)
            for (int j = i + 1; j < circles.Count; j++)
            {
                foreach (var p in Intersect(circles[i].Circle, circles[j].Circle))
                    AddIntersection(circles[i].Name, circles[j].Name, p);
            }
            IntersectionEmptyText.Visibility = IntersectionResults.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RefreshWorkspace();
            RefreshCounts();
        }

        private void AddIntersection(string firstName, string secondName, Point point)
        {
            _intersectionPoints.Add(point);
            IntersectionResults.Add($"{firstName} ∩ {secondName} = ({Format(point.X)}, {Format(point.Y)})");
        }

        private void CopyIntersectionResults(object sender, RoutedEventArgs e)
        {
            if (IntersectionResults.Count == 0) return;
            var package = new DataPackage();
            package.SetText(string.Join(Environment.NewLine, IntersectionResults));
            Clipboard.SetContent(package);
        }

        private void ReflectPointAcrossSelectedLine(object sender, RoutedEventArgs e)
        {
            var lineEntity = GetListView("EntityList")?.SelectedItem as GeometryEntity;
            var line = lineEntity != null && lineEntity.Type == GeometryEntityType.Line ? lineEntity.Line : Entities.LastOrDefault(it => it.Type == GeometryEntityType.Line)?.Line;
            if (line == null) return;

            var px = GetBoxValue("OpPointX");
            var py = GetBoxValue("OpPointY");
            var d = (line.A * px + line.B * py + line.C) / (line.A * line.A + line.B * line.B);
            var rx = px - 2 * line.A * d;
            var ry = py - 2 * line.B * d;
            ShowOperationMessage(string.Format("Geometry_ReflectedPointResult".GetLocalized(), Format(rx), Format(ry)));
        }

        private void InvertPointInSelectedCircle(object sender, RoutedEventArgs e)
        {
            var circleEntity = GetListView("EntityList")?.SelectedItem as GeometryEntity;
            var circle = circleEntity != null && circleEntity.Type == GeometryEntityType.Circle ? circleEntity.Circle : Entities.LastOrDefault(it => it.Type == GeometryEntityType.Circle)?.Circle;
            if (circle == null) return;

            var dx = GetBoxValue("OpPointX") - circle.X;
            var dy = GetBoxValue("OpPointY") - circle.Y;
            var dist2 = dx * dx + dy * dy;
            if (dist2 < 1e-10) { ShowOperationMessage("Geometry_InversionAtCenter".GetLocalized()); return; }
            var k = circle.R * circle.R / dist2;
            ShowOperationMessage(string.Format("Geometry_InvertedPointResult".GetLocalized(), Format(circle.X + dx * k), Format(circle.Y + dy * k)));
        }

        private void BuildTangentAtPoint(object sender, RoutedEventArgs e)
        {
            var selectedCircle = GetListView("EntityList")?.SelectedItem as GeometryEntity;
            var circle = selectedCircle != null && selectedCircle.Type == GeometryEntityType.Circle ? selectedCircle.Circle : Entities.LastOrDefault(ent => ent.Type == GeometryEntityType.Circle)?.Circle;
            if (circle == null) return;

            var px = GetBoxValue("OpPointX");
            var py = GetBoxValue("OpPointY");
            var dx = px - circle.X;
            var dy = py - circle.Y;
            var onCircle = Math.Abs(dx * dx + dy * dy - circle.R * circle.R) < 1e-4;
            if (!onCircle) { ShowOperationMessage("Geometry_PointNotOnCircle".GetLocalized()); return; }

            var a = dx;
            var b = dy;
            var c = -(a * px + b * py);
            ShowOperationMessage(string.Format("Geometry_TangentResult".GetLocalized(), Format(a), Format(b), Format(c)));
        }

        private void MeasurePointDistance(object sender, RoutedEventArgs e)
        {
            var selected = GetListView("EntityList")?.SelectedItem as GeometryEntity;
            if (selected == null)
            {
                ShowOperationMessage("Geometry_SelectObjectFirst".GetLocalized());
                return;
            }

            var px = GetBoxValue("OpPointX");
            var py = GetBoxValue("OpPointY");
            double distance;
            if (selected.Type == GeometryEntityType.Line)
            {
                distance = Math.Abs(selected.Line.A * px + selected.Line.B * py + selected.Line.C) /
                    Math.Sqrt(selected.Line.A * selected.Line.A + selected.Line.B * selected.Line.B);
            }
            else
            {
                var centerDistance = Math.Sqrt(Math.Pow(px - selected.Circle.X, 2) + Math.Pow(py - selected.Circle.Y, 2));
                distance = Math.Abs(centerDistance - selected.Circle.R);
            }

            ShowOperationMessage(string.Format("Geometry_PointDistanceResult".GetLocalized(), selected.Name, Format(distance)));
        }

        private void ShowOperationMessage(string message)
        {
            if (TryGetTextBox("OperationResult", out var result)) result.Text = message;
            if (WorkspaceInfoBar != null)
            {
                WorkspaceInfoBar.Content = message;
                WorkspaceInfoBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning;
            }
        }

        private void GridToggle_Click(object sender, RoutedEventArgs e)
        {
            _showGrid = GridToggle.IsChecked == true;
            RefreshWorkspace();
        }

        private void PlotZoomSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            _plotZoom = e.NewValue;
            RefreshWorkspace();
        }

        private void FitView_Click(object sender, RoutedEventArgs e)
        {
            PlotZoomSlider.Value = 1;
            _plotZoom = 1;
            RefreshWorkspace();
        }

        private void PlotCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RefreshWorkspace();

        private void RefreshCounts()
        {
            if (TryGetTextBlock("WorkspaceCounts", out var counts))
                counts.Text = string.Format("Geometry_WorkspaceCounts".GetLocalized(), Entities.Count(x => x.Type == GeometryEntityType.Line), Entities.Count(x => x.Type == GeometryEntityType.Circle), IntersectionResults.Count);
        }

        private void UpdateSelectionDetails(GeometryEntity item)
        {
            SelectedEntity = item;
            if (item == null)
            {
                if (TryGetTextBox("SelectedEntityInfo", out var emptyInfo)) emptyInfo.Text = string.Empty;
                if (TryGetTextBox("SelectedEntityEquation", out var emptyEquation)) emptyEquation.Text = string.Empty;
                if (TryGetTextBox("SelectedEntityStats", out var emptyStats)) emptyStats.Text = string.Empty;
                return;
            }

            if (TryGetTextBox("SelectedEntityInfo", out var info)) info.Text = $"{item.Name} · {item.KindName}";
            if (TryGetTextBox("SelectedEntityEquation", out var equation)) equation.Text = item.Type == GeometryEntityType.Line ? item.Line.ToGeneralString(DPlace) : item.Circle.ToEquation(DPlace);
            if (TryGetTextBox("SelectedEntityStats", out var stats))
            {
                stats.Text = item.Type == GeometryEntityType.Line
                    ? string.Format("Geometry_LineStats".GetLocalized(), item.Line.Slope.HasValue ? Math.Round(item.Line.Slope.Value, DPlace).ToString() : "Geometry_Infinity".GetLocalized(), Format(item.Line.XIntercept))
                    : string.Format("Geometry_CircleStats".GetLocalized(), Math.Round(Math.PI * item.Circle.R * item.Circle.R, DPlace), Math.Round(2 * Math.PI * item.Circle.R, DPlace));
            }
        }

        private bool TryGetTextBox(string name, out TextBox control)
        {
            control = FindName(name) as TextBox;
            return control != null;
        }

        private bool TryGetTextBlock(string name, out TextBlock control)
        {
            control = FindName(name) as TextBlock;
            return control != null;
        }

        private string Format(double value) => double.IsNaN(value) || double.IsInfinity(value) ? value.ToString() : Math.Round(value, DPlace).ToString();

        private static bool NearlyEqual(double first, double second) => Math.Abs(first - second) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(first), Math.Abs(second)));

        public event PropertyChangedEventHandler PropertyChanged;

        private void RefreshWorkspace()
        {
            var plotCanvas = GetCanvas("PlotCanvas");
            if (plotCanvas == null) return;
            plotCanvas.Children.Clear();

            var width = Math.Max(plotCanvas.ActualWidth, 300);
            var height = Math.Max(plotCanvas.ActualHeight, 300);
            var halfSpan = GetAutomaticHalfSpan() / Math.Max(_plotZoom, 0.25);
            var scale = Math.Min(width, height) / (halfSpan * 2);
            var cx = width / 2;
            var cy = height / 2;

            DrawCoordinateSystem(plotCanvas, width, height, cx, cy, scale, halfSpan);

            foreach (var item in Entities)
            {
                if (item.Type == GeometryEntityType.Line) DrawLine(plotCanvas, item.Line, cx, cy, scale);
                else DrawCircle(plotCanvas, item.Circle, cx, cy, scale);
                DrawEntityLabel(plotCanvas, item, cx, cy, scale);
            }

            for (var index = 0; index < _intersectionPoints.Count; index++)
                DrawIntersectionPoint(plotCanvas, _intersectionPoints[index], index + 1, cx, cy, scale);

            RefreshCounts();
        }

        private double GetAutomaticHalfSpan()
        {
            var extent = 5.0;
            foreach (var circle in Entities.Where(item => item.Type == GeometryEntityType.Circle).Select(item => item.Circle))
                extent = Math.Max(extent, Math.Max(Math.Abs(circle.X) + circle.R, Math.Abs(circle.Y) + circle.R));
            foreach (var point in _intersectionPoints)
                extent = Math.Max(extent, Math.Max(Math.Abs(point.X), Math.Abs(point.Y)));
            return Math.Ceiling(extent * 1.2);
        }

        private void DrawCoordinateSystem(Canvas plotCanvas, double width, double height, double cx, double cy, double scale, double halfSpan)
        {
            var gridBrush = new SolidColorBrush(Color.FromArgb(28, 128, 128, 128));
            var axisBrush = new SolidColorBrush(Color.FromArgb(115, 128, 128, 128));
            var step = halfSpan > 25 ? 10 : halfSpan > 12 ? 5 : halfSpan > 6 ? 2 : 1;

            if (_showGrid)
            {
                for (var world = -Math.Ceiling(halfSpan / step) * step; world <= halfSpan; world += step)
                {
                    var x = cx + world * scale;
                    var y = cy - world * scale;
                    plotCanvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = height, Stroke = gridBrush, StrokeThickness = 1 });
                    plotCanvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = width, Y2 = y, Stroke = gridBrush, StrokeThickness = 1 });
                    if (Math.Abs(world) > 1e-10)
                    {
                        AddCanvasText(plotCanvas, Format(world), x + 3, cy + 3, 11);
                        AddCanvasText(plotCanvas, Format(world), cx + 4, y - 15, 11);
                    }
                }
            }

            plotCanvas.Children.Add(new Line { X1 = 0, Y1 = cy, X2 = width, Y2 = cy, Stroke = axisBrush, StrokeThickness = 1.5 });
            plotCanvas.Children.Add(new Line { X1 = cx, Y1 = 0, X2 = cx, Y2 = height, Stroke = axisBrush, StrokeThickness = 1.5 });
            AddCanvasText(plotCanvas, "x", width - 18, cy + 5, 12);
            AddCanvasText(plotCanvas, "y", cx + 7, 3, 12);
        }

        private void DrawLine(Canvas plotCanvas, LineData line, double cx, double cy, double scale)
        {
            var x1 = -cx / scale;
            var x2 = (plotCanvas.ActualWidth - cx) / scale;
            if (line.IsVertical)
            {
                var x = cx + line.XIntercept * scale;
                plotCanvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = plotCanvas.ActualHeight, Stroke = new SolidColorBrush(Colors.DeepSkyBlue), StrokeThickness = 2 });
                return;
            }
            var y1 = line.YAt(x1);
            var y2 = line.YAt(x2);
            plotCanvas.Children.Add(new Line { X1 = cx + x1 * scale, Y1 = cy - y1 * scale, X2 = cx + x2 * scale, Y2 = cy - y2 * scale, Stroke = new SolidColorBrush(Colors.DeepSkyBlue), StrokeThickness = 2 });
        }

        private void DrawCircle(Canvas plotCanvas, CircleData circle, double cx, double cy, double scale)
        {
            var ellipse = new Ellipse
            {
                Width = circle.R * 2 * scale,
                Height = circle.R * 2 * scale,
                Stroke = new SolidColorBrush(Colors.OrangeRed),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(18, 255, 69, 0))
            };
            Canvas.SetLeft(ellipse, cx + circle.X * scale - circle.R * scale);
            Canvas.SetTop(ellipse, cy - circle.Y * scale - circle.R * scale);
            plotCanvas.Children.Add(ellipse);
        }

        private void DrawEntityLabel(Canvas plotCanvas, GeometryEntity item, double cx, double cy, double scale)
        {
            double x;
            double y;
            if (item.Type == GeometryEntityType.Circle)
            {
                x = cx + (item.Circle.X + item.Circle.R) * scale + 5;
                y = cy - item.Circle.Y * scale - 18;
            }
            else if (item.Line.IsVertical)
            {
                x = cx + item.Line.XIntercept * scale + 5;
                y = 8;
            }
            else
            {
                var worldX = (plotCanvas.ActualWidth * 0.35 - cx) / scale;
                x = cx + worldX * scale + 5;
                y = cy - item.Line.YAt(worldX) * scale - 20;
            }

            AddCanvasText(plotCanvas, item.Name, x, y, 12, true);
        }

        private void DrawIntersectionPoint(Canvas plotCanvas, Point point, int index, double cx, double cy, double scale)
        {
            var x = cx + point.X * scale;
            var y = cy - point.Y * scale;
            var marker = new Ellipse
            {
                Width = 9,
                Height = 9,
                Fill = new SolidColorBrush(Colors.Crimson),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(marker, x - 4.5);
            Canvas.SetTop(marker, y - 4.5);
            plotCanvas.Children.Add(marker);
            AddCanvasText(plotCanvas, $"P{index}", x + 7, y - 18, 11, true);
        }

        private static void AddCanvasText(Canvas canvas, string text, double left, double top, double fontSize, bool strong = false)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = strong ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            Canvas.SetLeft(label, Math.Max(0, left));
            Canvas.SetTop(label, Math.Max(0, top));
            canvas.Children.Add(label);
        }

        private double GetBoxValue(string name)
        {
            var box = FindName(name) as Microsoft.UI.Xaml.Controls.NumberBox;
            return box?.Value ?? 0;
        }

        private ListView GetListView(string name) => FindName(name) as ListView;

        private Canvas GetCanvas(string name) => FindName(name) as Canvas;

        private static Point? Intersect(LineData a, LineData b)
        {
            var det = a.A * b.B - b.A * a.B;
            if (Math.Abs(det) < 1e-10) return null;
            var x = (a.B * b.C - b.B * a.C) / det;
            var y = (b.A * a.C - a.A * b.C) / det;
            return new Point(x, y);
        }

        private static IEnumerable<Point> Intersect(LineData line, CircleData circle)
        {
            var results = new List<Point>();
            if (line.IsVertical)
            {
                var x = line.XIntercept;
                var t = circle.R * circle.R - (x - circle.X) * (x - circle.X);
                if (t < -1e-10) return results;
                var s = Math.Sqrt(Math.Max(0, t));
                results.Add(new Point(x, circle.Y + s));
                if (s > 1e-10) results.Add(new Point(x, circle.Y - s));
                return results;
            }

            var m = line.Slope.Value;
            var d = line.Intercept.Value;
            var a = 1 + m * m;
            var b = 2 * (m * (d - circle.Y) - circle.X);
            var c = circle.X * circle.X + (d - circle.Y) * (d - circle.Y) - circle.R * circle.R;
            var disc = b * b - 4 * a * c;
            if (disc < -1e-10) return results;
            var sdisc = Math.Sqrt(Math.Max(0, disc));
            var x1 = (-b + sdisc) / (2 * a);
            var x2 = (-b - sdisc) / (2 * a);
            results.Add(new Point(x1, m * x1 + d));
            if (sdisc > 1e-10) results.Add(new Point(x2, m * x2 + d));
            return results;
        }

        private static IEnumerable<Point> Intersect(CircleData a, CircleData b)
        {
            var results = new List<Point>();
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-10 || d > a.R + b.R + 1e-10 || d < Math.Abs(a.R - b.R) - 1e-10) return results;

            var x = (a.R * a.R - b.R * b.R + d * d) / (2 * d);
            var h2 = a.R * a.R - x * x;
            if (h2 < -1e-10) return results;
            var h = Math.Sqrt(Math.Max(0, h2));
            var xm = a.X + x * dx / d;
            var ym = a.Y + x * dy / d;
            var rx = -dy * (h / d);
            var ry = dx * (h / d);
            results.Add(new Point(xm + rx, ym + ry));
            if (h > 1e-10) results.Add(new Point(xm - rx, ym - ry));
            return results;
        }

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

    public sealed class EntityTypeToColorConverter : Windows.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is GeometryPage.GeometryEntityType type)
                return type == GeometryPage.GeometryEntityType.Line
                    ? new Windows.UI.Color { A = 255, R = 0, G = 149, B = 218 }   // 蓝色（直线）
                    : new Windows.UI.Color { A = 255, R = 230, G = 80, B = 50 };   // 橙红（圆）
            return new Windows.UI.Color { A = 255, R = 128, G = 128, B = 128 };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }

    public sealed class EntityTypeToGlyphConverter : Windows.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is GeometryPage.GeometryEntityType type)
                return type == GeometryPage.GeometryEntityType.Line ? "\uf7af" : "\uea3b";  // ╱ 直线, ○ 圆
            return "\uf142";  // ❓
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
