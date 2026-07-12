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
        public enum GeometryEntityType { Line, Circle, Ellipse, Hyperbola }

        public sealed class GeometryEntity
        {
            public string Name { get; set; }
            public string Alias { get; set; }
            public GeometryEntityType Type { get; set; }
            public string KindName => (Type == GeometryEntityType.Line ? "Geometry_Kind_Line" : Type == GeometryEntityType.Circle ? "Geometry_Kind_Circle" : Type == GeometryEntityType.Ellipse ? "Geometry_Kind_Ellipse" : "Geometry_Kind_Hyperbola").GetLocalized();
            public string Summary { get; set; }

            public LineData Line { get; set; }
            public CircleData Circle { get; set; }
            public ConicData Conic { get; set; }
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
        public ObservableCollection<OperationChoice> OperationChoices { get; } = new ObservableCollection<OperationChoice>();
        public ObservableCollection<GeometryEntity> OperationTargets { get; } = new ObservableCollection<GeometryEntity>();
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
            OperationChoices.Add(new OperationChoice(OperationKind.Reflect, "Geometry_Operation_Reflect".GetLocalized(), GeometryEntityType.Line));
            OperationChoices.Add(new OperationChoice(OperationKind.Invert, "Geometry_Operation_Invert".GetLocalized(), GeometryEntityType.Circle));
            OperationChoices.Add(new OperationChoice(OperationKind.Tangent, "Geometry_Operation_Tangent".GetLocalized(), GeometryEntityType.Circle));
            OperationChoices.Add(new OperationChoice(OperationKind.Distance, "Geometry_Operation_Distance".GetLocalized(), null));
            Entities.CollectionChanged += (_, __) => { RefreshWorkspace(); RefreshOperationTargets(); };
            IntersectionResults.CollectionChanged += (_, __) => RefreshCounts();
            GetListView("EntityList").ItemsSource = Entities;
            GetListView("IntersectionList").ItemsSource = IntersectionResults;
            Loaded += (_, __) =>
            {
                RefreshWorkspace();
                OperationKindBox.SelectedIndex = 0;
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

        public sealed class ConicData
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double A { get; set; }
            public double B { get; set; }
            public bool IsVertical { get; set; }

            public string ToEquation(bool hyperbola, int digits)
            {
                var xTerm = $"(x - {Math.Round(X, digits)})²";
                var yTerm = $"(y - {Math.Round(Y, digits)})²";
                var a2 = Math.Round(A * A, digits);
                var b2 = Math.Round(B * B, digits);
                if (!hyperbola)
                    return IsVertical ? $"{xTerm} / {b2} + {yTerm} / {a2} = 1" : $"{xTerm} / {a2} + {yTerm} / {b2} = 1";
                return IsVertical ? $"{yTerm} / {a2} - {xTerm} / {b2} = 1" : $"{xTerm} / {a2} - {yTerm} / {b2} = 1";
            }
        }

        public enum OperationKind { Reflect, Invert, Tangent, Distance }

        public sealed class OperationChoice
        {
            public OperationChoice(OperationKind kind, string name, GeometryEntityType? targetType)
            {
                Kind = kind;
                Name = name;
                TargetType = targetType;
            }

            public OperationKind Kind { get; }
            public string Name { get; }
            public GeometryEntityType? TargetType { get; }
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
            else if (source.Type == GeometryEntityType.Circle)
            {
                copy.Circle = new CircleData { X = source.Circle.X + 1, Y = source.Circle.Y + 1, R = source.Circle.R };
                copy.Summary = $"r = {Math.Round(copy.Circle.R, DPlace)}";
            }
            else
            {
                copy.Conic = new ConicData { X = source.Conic.X + 1, Y = source.Conic.Y + 1, A = source.Conic.A, B = source.Conic.B, IsVertical = source.Conic.IsVertical };
                copy.Summary = copy.Conic.ToEquation(source.Type == GeometryEntityType.Hyperbola, DPlace);
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
            else if (dialog.IsEllipse || dialog.IsHyperbola)
                CreateConicFromDialog(dialog);
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
            var conics = Entities.Where(x => x.Type == GeometryEntityType.Ellipse || x.Type == GeometryEntityType.Hyperbola).ToList();

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
            foreach (var line in lines)
            foreach (var conic in conics)
            foreach (var point in Intersect(line.Line, conic.Conic, conic.Type == GeometryEntityType.Hyperbola))
                AddIntersection(line.Name, conic.Name, point);

            var ellipses = conics.Where(item => item.Type == GeometryEntityType.Ellipse).ToList();
            var hyperbolas = conics.Where(item => item.Type == GeometryEntityType.Hyperbola).ToList();
            foreach (var ellipse in ellipses)
            foreach (var hyperbola in hyperbolas)
            foreach (var point in IntersectEllipseHyperbola(ellipse.Conic, hyperbola.Conic))
                AddIntersection(ellipse.Name, hyperbola.Name, point);
            IntersectionEmptyText.Visibility = IntersectionResults.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RefreshWorkspace();
            RefreshCounts();
        }

        private void CreateConicFromDialog(CustomDialog dialog)
        {
            var conic = new ConicData
            {
                X = GetDialogNumber(dialog, "ConicXBox"), Y = GetDialogNumber(dialog, "ConicYBox"),
                A = GetDialogNumber(dialog, "ConicABox"), B = GetDialogNumber(dialog, "ConicBBox"),
                IsVertical = dialog.IsConicVertical
            };
            if (!IsPositiveFinite(conic.A) || !IsPositiveFinite(conic.B))
            {
                ShowOperationMessage("Geometry_InvalidSemiAxes".GetLocalized());
                return;
            }
            var type = dialog.IsEllipse ? GeometryEntityType.Ellipse : GeometryEntityType.Hyperbola;
            var prefix = dialog.IsEllipse ? "E" : "H";
            var alias = dialog.GetAlias();
            var name = string.IsNullOrWhiteSpace(alias) ? $"{prefix}{Entities.Count(x => x.Type == type) + 1}" : alias;
            Entities.Add(new GeometryEntity { Name = name, Alias = alias, Type = type, Conic = conic, Summary = conic.ToEquation(type == GeometryEntityType.Hyperbola, DPlace) });
            EntityList.SelectedItem = Entities.Last();
        }

        private static bool IsPositiveFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

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

        private void OperationKindBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshOperationTargets();

        private void OperationTargetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RunOperationButton.IsEnabled = OperationTargetBox.SelectedItem is GeometryEntity;
        }

        private void RefreshOperationTargets()
        {
            if (!(OperationKindBox?.SelectedItem is OperationChoice operation)) return;

            var previous = OperationTargetBox.SelectedItem as GeometryEntity;
            OperationTargets.Clear();
            foreach (var entity in Entities.Where(item => !operation.TargetType.HasValue || item.Type == operation.TargetType.Value))
                OperationTargets.Add(entity);

            OperationTargetBox.SelectedItem = previous != null && OperationTargets.Contains(previous) ? previous : OperationTargets.FirstOrDefault();
            RunOperationButton.IsEnabled = OperationTargetBox.SelectedItem != null;
            OperationRequirementText.Text = OperationTargets.Count == 0
                ? (operation.TargetType == GeometryEntityType.Line ? "Geometry_NeedLine" : operation.TargetType == GeometryEntityType.Circle ? "Geometry_NeedCircle" : "Geometry_NeedObject").GetLocalized()
                : string.Empty;
        }

        private void RunSelectedOperation(object sender, RoutedEventArgs e)
        {
            if (!(OperationKindBox.SelectedItem is OperationChoice operation) || !(OperationTargetBox.SelectedItem is GeometryEntity))
                return;

            switch (operation.Kind)
            {
                case OperationKind.Reflect: ReflectPointAcrossSelectedLine(); break;
                case OperationKind.Invert: InvertPointInSelectedCircle(); break;
                case OperationKind.Tangent: BuildTangentAtPoint(); break;
                case OperationKind.Distance: MeasurePointDistance(); break;
            }
        }

        private void ReflectPointAcrossSelectedLine()
        {
            var line = (OperationTargetBox.SelectedItem as GeometryEntity)?.Line;
            if (line == null) return;

            var px = GetBoxValue("OpPointX");
            var py = GetBoxValue("OpPointY");
            var d = (line.A * px + line.B * py + line.C) / (line.A * line.A + line.B * line.B);
            var rx = px - 2 * line.A * d;
            var ry = py - 2 * line.B * d;
            ShowOperationMessage(string.Format("Geometry_ReflectedPointResult".GetLocalized(), Format(rx), Format(ry)));
        }

        private void InvertPointInSelectedCircle()
        {
            var circle = (OperationTargetBox.SelectedItem as GeometryEntity)?.Circle;
            if (circle == null) return;

            var dx = GetBoxValue("OpPointX") - circle.X;
            var dy = GetBoxValue("OpPointY") - circle.Y;
            var dist2 = dx * dx + dy * dy;
            if (dist2 < 1e-10) { ShowOperationMessage("Geometry_InversionAtCenter".GetLocalized()); return; }
            var k = circle.R * circle.R / dist2;
            ShowOperationMessage(string.Format("Geometry_InvertedPointResult".GetLocalized(), Format(circle.X + dx * k), Format(circle.Y + dy * k)));
        }

        private void BuildTangentAtPoint()
        {
            var circle = (OperationTargetBox.SelectedItem as GeometryEntity)?.Circle;
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

        private void MeasurePointDistance()
        {
            var selected = OperationTargetBox.SelectedItem as GeometryEntity;
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
            else if (selected.Type == GeometryEntityType.Circle)
            {
                var centerDistance = Math.Sqrt(Math.Pow(px - selected.Circle.X, 2) + Math.Pow(py - selected.Circle.Y, 2));
                distance = Math.Abs(centerDistance - selected.Circle.R);
            }
            else
            {
                distance = ApproximateConicDistance(selected.Conic, selected.Type == GeometryEntityType.Hyperbola, px, py);
            }

            ShowOperationMessage(string.Format("Geometry_PointDistanceResult".GetLocalized(), selected.Name, Format(distance)));
        }

        private static double ApproximateConicDistance(ConicData conic, bool hyperbola, double px, double py)
        {
            var minimum = double.PositiveInfinity;
            var branchStart = hyperbola ? -1 : 1;
            for (var branch = branchStart; branch <= 1; branch += 2)
            for (var i = 0; i <= 2400; i++)
            {
                var t = hyperbola ? -3.5 + 7.0 * i / 2400 : Math.PI * 2 * i / 2400;
                var primary = hyperbola ? branch * conic.A * Math.Cosh(t) : conic.A * Math.Cos(t);
                var secondary = hyperbola ? conic.B * Math.Sinh(t) : conic.B * Math.Sin(t);
                var x = conic.X + (conic.IsVertical ? secondary : primary);
                var y = conic.Y + (conic.IsVertical ? primary : secondary);
                minimum = Math.Min(minimum, Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py)));
            }
            return minimum;
        }

        private void ShowOperationMessage(string message)
        {
            if (OperationResultBar != null)
            {
                OperationResultBar.Title = "Geometry_ResultTitle".GetLocalized();
                OperationResultBar.Message = message;
                OperationResultBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success;
            }
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
                counts.Text = string.Format("Geometry_WorkspaceCounts".GetLocalized(), Entities.Count(x => x.Type == GeometryEntityType.Line), Entities.Count(x => x.Type == GeometryEntityType.Circle), Entities.Count(x => x.Type == GeometryEntityType.Ellipse), Entities.Count(x => x.Type == GeometryEntityType.Hyperbola), IntersectionResults.Count);
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
                else if (item.Type == GeometryEntityType.Circle) DrawCircle(plotCanvas, item.Circle, cx, cy, scale);
                else DrawConic(plotCanvas, item.Conic, item.Type == GeometryEntityType.Hyperbola, cx, cy, scale, halfSpan);
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
            foreach (var item in Entities.Where(item => item.Type == GeometryEntityType.Ellipse || item.Type == GeometryEntityType.Hyperbola))
                extent = Math.Max(extent, Math.Max(Math.Abs(item.Conic.X), Math.Abs(item.Conic.Y)) + Math.Max(item.Conic.A, item.Conic.B) * (item.Type == GeometryEntityType.Hyperbola ? 2.5 : 1));
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

        private void DrawConic(Canvas canvas, ConicData conic, bool hyperbola, double cx, double cy, double scale, double halfSpan)
        {
            var brush = new SolidColorBrush(hyperbola ? Colors.MediumPurple : Colors.MediumSeaGreen);
            var fill = hyperbola ? null : new SolidColorBrush(Color.FromArgb(16, 60, 179, 113));
            if (!hyperbola)
            {
                var shape = new Ellipse { Width = (conic.IsVertical ? conic.B : conic.A) * 2 * scale, Height = (conic.IsVertical ? conic.A : conic.B) * 2 * scale, Stroke = brush, StrokeThickness = 2, Fill = fill };
                Canvas.SetLeft(shape, cx + conic.X * scale - shape.Width / 2);
                Canvas.SetTop(shape, cy - conic.Y * scale - shape.Height / 2);
                canvas.Children.Add(shape);
                return;
            }

            for (var branch = -1; branch <= 1; branch += 2)
            {
                var polyline = new Polyline { Stroke = brush, StrokeThickness = 2 };
                for (var i = 0; i <= 120; i++)
                {
                    var t = -2.2 + 4.4 * i / 120.0;
                    var primary = branch * conic.A * Math.Cosh(t);
                    var secondary = conic.B * Math.Sinh(t);
                    var x = conic.X + (conic.IsVertical ? secondary : primary);
                    var y = conic.Y + (conic.IsVertical ? primary : secondary);
                    if (Math.Abs(x) <= halfSpan * 1.5 && Math.Abs(y) <= halfSpan * 1.5)
                        polyline.Points.Add(new Point(cx + x * scale, cy - y * scale));
                }
                canvas.Children.Add(polyline);
            }
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
            else if (item.Type == GeometryEntityType.Ellipse || item.Type == GeometryEntityType.Hyperbola)
            {
                x = cx + (item.Conic.X + (item.Conic.IsVertical ? item.Conic.B : item.Conic.A)) * scale + 5;
                y = cy - item.Conic.Y * scale - 18;
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

        private static IEnumerable<Point> Intersect(LineData line, ConicData conic, bool hyperbola)
        {
            var results = new List<Point>();
            var norm = line.A * line.A + line.B * line.B;
            if (norm < 1e-12) return results;
            var x0 = -line.A * line.C / norm;
            var y0 = -line.B * line.C / norm;
            var dx = line.B;
            var dy = -line.A;
            var xDen = Math.Pow(conic.IsVertical ? conic.B : conic.A, 2);
            var yDen = Math.Pow(conic.IsVertical ? conic.A : conic.B, 2);
            var xSign = hyperbola && conic.IsVertical ? -1.0 : 1.0;
            var ySign = hyperbola && !conic.IsVertical ? -1.0 : 1.0;
            var ux = x0 - conic.X;
            var uy = y0 - conic.Y;
            var qa = xSign * dx * dx / xDen + ySign * dy * dy / yDen;
            var qb = 2 * (xSign * ux * dx / xDen + ySign * uy * dy / yDen);
            var qc = xSign * ux * ux / xDen + ySign * uy * uy / yDen - 1;
            if (Math.Abs(qa) < 1e-12)
            {
                if (Math.Abs(qb) > 1e-12)
                {
                    var t = -qc / qb;
                    results.Add(new Point(x0 + dx * t, y0 + dy * t));
                }
                return results;
            }
            var discriminant = qb * qb - 4 * qa * qc;
            if (discriminant < -1e-10) return results;
            var root = Math.Sqrt(Math.Max(0, discriminant));
            var t1 = (-qb + root) / (2 * qa);
            var t2 = (-qb - root) / (2 * qa);
            results.Add(new Point(x0 + dx * t1, y0 + dy * t1));
            if (root > 1e-10) results.Add(new Point(x0 + dx * t2, y0 + dy * t2));
            return results;
        }

        private static IEnumerable<Point> IntersectEllipseHyperbola(ConicData ellipse, ConicData hyperbola)
        {
            const int samples = 4096;
            const double rootTolerance = 1e-9;
            var roots = new List<double>();
            var previousT = 0.0;
            var previousValue = EvaluateHyperbola(PointOnEllipse(ellipse, previousT), hyperbola);

            for (var i = 1; i <= samples; i++)
            {
                var t = Math.PI * 2 * i / samples;
                var value = EvaluateHyperbola(PointOnEllipse(ellipse, t), hyperbola);
                if (Math.Abs(value) < 1e-8)
                    AddDistinctRoot(roots, t);
                else if (previousValue * value < 0)
                    AddDistinctRoot(roots, BisectRoot(ellipse, hyperbola, previousT, t, rootTolerance));
                else
                {
                    var middle = (previousT + t) / 2;
                    var middleValue = Math.Abs(EvaluateHyperbola(PointOnEllipse(ellipse, middle), hyperbola));
                    if (middleValue < 1e-7 && middleValue <= Math.Abs(previousValue) && middleValue <= Math.Abs(value))
                    {
                        var candidate = RefineMinimum(ellipse, hyperbola, previousT, t);
                        if (Math.Abs(EvaluateHyperbola(PointOnEllipse(ellipse, candidate), hyperbola)) < 1e-7)
                            AddDistinctRoot(roots, candidate);
                    }
                }
                previousT = t;
                previousValue = value;
            }

            return roots.Select(t => PointOnEllipse(ellipse, t)).ToList();
        }

        private static Point PointOnEllipse(ConicData ellipse, double t)
        {
            var primary = ellipse.A * Math.Cos(t);
            var secondary = ellipse.B * Math.Sin(t);
            return ellipse.IsVertical
                ? new Point(ellipse.X + secondary, ellipse.Y + primary)
                : new Point(ellipse.X + primary, ellipse.Y + secondary);
        }

        private static double EvaluateHyperbola(Point point, ConicData hyperbola)
        {
            var dx = point.X - hyperbola.X;
            var dy = point.Y - hyperbola.Y;
            return hyperbola.IsVertical
                ? dy * dy / (hyperbola.A * hyperbola.A) - dx * dx / (hyperbola.B * hyperbola.B) - 1
                : dx * dx / (hyperbola.A * hyperbola.A) - dy * dy / (hyperbola.B * hyperbola.B) - 1;
        }

        private static double BisectRoot(ConicData ellipse, ConicData hyperbola, double left, double right, double tolerance)
        {
            var leftValue = EvaluateHyperbola(PointOnEllipse(ellipse, left), hyperbola);
            for (var i = 0; i < 60 && right - left > tolerance; i++)
            {
                var middle = (left + right) / 2;
                var middleValue = EvaluateHyperbola(PointOnEllipse(ellipse, middle), hyperbola);
                if (leftValue * middleValue <= 0) right = middle;
                else { left = middle; leftValue = middleValue; }
            }
            return (left + right) / 2;
        }

        private static double RefineMinimum(ConicData ellipse, ConicData hyperbola, double left, double right)
        {
            for (var i = 0; i < 45; i++)
            {
                var first = left + (right - left) / 3;
                var second = right - (right - left) / 3;
                var firstValue = Math.Abs(EvaluateHyperbola(PointOnEllipse(ellipse, first), hyperbola));
                var secondValue = Math.Abs(EvaluateHyperbola(PointOnEllipse(ellipse, second), hyperbola));
                if (firstValue < secondValue) right = second; else left = first;
            }
            return (left + right) / 2;
        }

        private static void AddDistinctRoot(List<double> roots, double candidate)
        {
            var normalized = candidate % (Math.PI * 2);
            if (normalized < 0) normalized += Math.PI * 2;
            if (roots.All(root => Math.Abs(root - normalized) > 1e-5 && Math.Abs(Math.Abs(root - normalized) - Math.PI * 2) > 1e-5))
                roots.Add(normalized);
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
                return type == GeometryPage.GeometryEntityType.Line ? Colors.DeepSkyBlue
                    : type == GeometryPage.GeometryEntityType.Circle ? Colors.OrangeRed
                    : type == GeometryPage.GeometryEntityType.Ellipse ? Colors.MediumSeaGreen
                    : Colors.MediumPurple;
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
                return type == GeometryPage.GeometryEntityType.Line ? "\uf7af"
                    : type == GeometryPage.GeometryEntityType.Circle ? "\ue91f"
                    : type == GeometryPage.GeometryEntityType.Ellipse ? "\uea3b" : "\ue9d2";
            return "\uf142";  // ❓
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
