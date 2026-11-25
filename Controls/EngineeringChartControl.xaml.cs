using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MechCore.Models.Charts;

namespace MechCore.Controls
{
    public partial class EngineeringChartControl : UserControl
    {
        private const double MarginLeft = 56;
        private const double MarginRight = 20;
        private const double MarginTop = 18;
        private const double MarginBottom = 42;

        public static readonly DependencyProperty ChartModelProperty =
            DependencyProperty.Register(
                nameof(ChartModel),
                typeof(EngineeringChartModel),
                typeof(EngineeringChartControl),
                new PropertyMetadata(null, OnChartDataChanged));

        public static readonly DependencyProperty IsEngineerModeProperty =
            DependencyProperty.Register(
                nameof(IsEngineerMode),
                typeof(bool),
                typeof(EngineeringChartControl),
                new PropertyMetadata(true, OnChartDataChanged));

        public EngineeringChartModel? ChartModel
        {
            get => (EngineeringChartModel?)GetValue(ChartModelProperty);
            set => SetValue(ChartModelProperty, value);
        }

        public bool IsEngineerMode
        {
            get => (bool)GetValue(IsEngineerModeProperty);
            set => SetValue(IsEngineerModeProperty, value);
        }

        public EngineeringChartControl()
        {
            InitializeComponent();
        }

        private static void OnChartDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EngineeringChartControl control)
            {
                control.RenderChart();
            }
        }

        private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderChart();
        }

        public void RenderChart()
        {
            ChartCanvas.Children.Clear();

            var model = ChartModel;
            if (model == null)
            {
                TitleTextBlock.Text = string.Empty;
                DescriptionTextBlock.Text = string.Empty;
                LegendItemsControl.ItemsSource = null;
                MaterialBadgeBorder.Visibility = Visibility.Collapsed;
                return;
            }

            TitleTextBlock.Text = model.ChartTitle;
            DescriptionTextBlock.Text = model.Description;
            DescriptionTextBlock.Visibility = string.IsNullOrWhiteSpace(model.Description)
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (!string.IsNullOrWhiteSpace(model.ActiveMaterialBadge))
            {
                MaterialBadgeTextBlock.Text = $"Материал: {model.ActiveMaterialBadge}";
                MaterialBadgeBorder.Visibility = Visibility.Visible;
            }
            else
            {
                MaterialBadgeBorder.Visibility = Visibility.Collapsed;
            }

            var visibleSeries = model.Series
                .Where(s => IsEngineerMode || !s.IsEngineerOnly)
                .ToList();

            LegendItemsControl.ItemsSource = visibleSeries;

            double width = ChartCanvas.ActualWidth;
            double height = ChartCanvas.ActualHeight;
            if (width <= MarginLeft + MarginRight + 20 || height <= MarginTop + MarginBottom + 20)
                return;

            double plotW = width - MarginLeft - MarginRight;
            double plotH = height - MarginTop - MarginBottom;

            double xMin = model.XAxis.Min;
            double xMax = Math.Max(model.XAxis.Max, xMin + 1e-6);
            double yMin = model.YAxis.Min;
            double yMax = Math.Max(model.YAxis.Max, yMin + 1e-6);

            double ToScreenX(double x) => MarginLeft + (x - xMin) / (xMax - xMin) * plotW;
            double ToScreenY(double y) => MarginTop + plotH - (y - yMin) / (yMax - yMin) * plotH;

            // 1. Технологические зоны
            foreach (var zone in model.Zones)
            {
                if (zone.Orientation == ChartZoneOrientation.Vertical)
                {
                    double sx1 = Math.Clamp(ToScreenX(zone.Start), MarginLeft, MarginLeft + plotW);
                    double sx2 = Math.Clamp(ToScreenX(zone.End), MarginLeft, MarginLeft + plotW);
                    if (sx2 > sx1)
                    {
                        var rect = new Rectangle
                        {
                            Width = sx2 - sx1,
                            Height = plotH,
                            Fill = zone.FillBrush
                        };
                        Canvas.SetLeft(rect, sx1);
                        Canvas.SetTop(rect, MarginTop);
                        ChartCanvas.Children.Add(rect);

                        if (!string.IsNullOrWhiteSpace(zone.Label))
                        {
                            AddText(zone.Label, sx1 + 4, MarginTop + 4, 10, zone.LabelBrush, FontWeights.SemiBold);
                        }
                    }
                }
                else
                {
                    double sy1 = Math.Clamp(ToScreenY(zone.End), MarginTop, MarginTop + plotH);
                    double sy2 = Math.Clamp(ToScreenY(zone.Start), MarginTop, MarginTop + plotH);
                    if (sy2 > sy1)
                    {
                        var rect = new Rectangle
                        {
                            Width = plotW,
                            Height = sy2 - sy1,
                            Fill = zone.FillBrush
                        };
                        Canvas.SetLeft(rect, MarginLeft);
                        Canvas.SetTop(rect, sy1);
                        ChartCanvas.Children.Add(rect);
                    }
                }
            }

            // 2. Координатная сетка и подписи делений
            var gridBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
            var axisBrush = new SolidColorBrush(Color.FromRgb(97, 97, 97));

            double xStep = model.XAxis.Step > 0 ? model.XAxis.Step : (xMax - xMin) / 5.0;
            for (double x = xMin; x <= xMax + xStep * 0.001; x += xStep)
            {
                double sx = ToScreenX(x);
                ChartCanvas.Children.Add(new Line
                {
                    X1 = sx,
                    Y1 = MarginTop,
                    X2 = sx,
                    Y2 = MarginTop + plotH,
                    Stroke = gridBrush,
                    StrokeThickness = 1
                });

                string label = x.ToString(model.XAxis.NumberFormat, CultureInfo.InvariantCulture);
                AddText(label, sx - 14, MarginTop + plotH + 5, 10.5, axisBrush, FontWeights.Normal);
            }

            double yStep = model.YAxis.Step > 0 ? model.YAxis.Step : (yMax - yMin) / 5.0;
            for (double y = yMin; y <= yMax + yStep * 0.001; y += yStep)
            {
                double sy = ToScreenY(y);
                ChartCanvas.Children.Add(new Line
                {
                    X1 = MarginLeft,
                    Y1 = sy,
                    X2 = MarginLeft + plotW,
                    Y2 = sy,
                    Stroke = gridBrush,
                    StrokeThickness = 1
                });

                string label = y.ToString(model.YAxis.NumberFormat, CultureInfo.InvariantCulture);
                AddText(label, 6, sy - 7, 10.5, axisBrush, FontWeights.Normal);
            }

            // 3. Главные оси X и Y + заголовки единиц измерения
            var mainAxisBrush = new SolidColorBrush(Color.FromRgb(66, 66, 66));
            ChartCanvas.Children.Add(new Line
            {
                X1 = MarginLeft,
                Y1 = MarginTop,
                X2 = MarginLeft,
                Y2 = MarginTop + plotH,
                Stroke = mainAxisBrush,
                StrokeThickness = 1.5
            });
            ChartCanvas.Children.Add(new Line
            {
                X1 = MarginLeft,
                Y1 = MarginTop + plotH,
                X2 = MarginLeft + plotW,
                Y2 = MarginTop + plotH,
                Stroke = mainAxisBrush,
                StrokeThickness = 1.5
            });

            string xTitle = string.IsNullOrWhiteSpace(model.XAxis.Unit)
                ? model.XAxis.Title
                : $"{model.XAxis.Title}, {model.XAxis.Unit}";
            AddText(xTitle, MarginLeft + plotW / 2 - 45, height - 18, 11, mainAxisBrush, FontWeights.SemiBold);

            string yTitle = string.IsNullOrWhiteSpace(model.YAxis.Unit)
                ? model.YAxis.Title
                : $"{model.YAxis.Title} ({model.YAxis.Unit})";
            AddText(yTitle, 6, 2, 10.5, mainAxisBrush, FontWeights.SemiBold);

            // 4. Пороговые линии
            foreach (var threshold in model.Thresholds)
            {
                if (threshold.Orientation == ChartZoneOrientation.Horizontal &&
                    threshold.Value >= yMin && threshold.Value <= yMax)
                {
                    double sy = ToScreenY(threshold.Value);
                    ChartCanvas.Children.Add(new Line
                    {
                        X1 = MarginLeft,
                        Y1 = sy,
                        X2 = MarginLeft + plotW,
                        Y2 = sy,
                        Stroke = threshold.StrokeBrush,
                        StrokeThickness = 1.5,
                        StrokeDashArray = threshold.StrokeDashArray
                    });
                    if (!string.IsNullOrWhiteSpace(threshold.Label))
                    {
                        AddText(threshold.Label, MarginLeft + 6, sy - 15, 10, threshold.StrokeBrush, FontWeights.SemiBold);
                    }
                }
                else if (threshold.Orientation == ChartZoneOrientation.Vertical &&
                         threshold.Value >= xMin && threshold.Value <= xMax)
                {
                    double sx = ToScreenX(threshold.Value);
                    ChartCanvas.Children.Add(new Line
                    {
                        X1 = sx,
                        Y1 = MarginTop,
                        X2 = sx,
                        Y2 = MarginTop + plotH,
                        Stroke = threshold.StrokeBrush,
                        StrokeThickness = 1.5,
                        StrokeDashArray = threshold.StrokeDashArray
                    });
                    if (!string.IsNullOrWhiteSpace(threshold.Label))
                    {
                        AddText(threshold.Label, sx + 4, MarginTop + 16, 10, threshold.StrokeBrush, FontWeights.SemiBold);
                    }
                }
            }
            // 5. Кривые данных
            foreach (var series in visibleSeries)
            {
                if (series.Points.Count < 2) continue;
                var polyline = new Polyline
                {
                    Stroke = series.StrokeBrush,
                    StrokeThickness = series.IsHighlighted ? series.StrokeThickness + 0.8 : series.StrokeThickness,
                    Opacity = series.IsHighlighted ? 1.0 : 0.45
                };
                if (series.IsDashed)
                {
                    polyline.StrokeDashArray = new DoubleCollection { 5, 3 };
                }
                foreach (var pt in series.Points)
                {
                    double sx = Math.Clamp(ToScreenX(pt.X), MarginLeft, MarginLeft + plotW);
                    double sy = Math.Clamp(ToScreenY(pt.Y), MarginTop, MarginTop + plotH);
                    polyline.Points.Add(new Point(sx, sy));
                }
                ChartCanvas.Children.Add(polyline);
            }
            // 6. Рабочие точки (маркеры выбранного материала/режима)
            foreach (var marker in model.Markers)
            {
                if (marker.X < xMin || marker.X > xMax || marker.Y < yMin || marker.Y > yMax)
                    continue;
                double sx = ToScreenX(marker.X);
                double sy = ToScreenY(marker.Y);
                if (marker.ShowGuideLines)
                {
                    ChartCanvas.Children.Add(new Line
                    {
                        X1 = sx,
                        Y1 = sy,
                        X2 = sx,
                        Y2 = MarginTop + plotH,
                        Stroke = marker.FillBrush,
                        StrokeThickness = 1.2,
                        StrokeDashArray = new DoubleCollection { 3, 2 }
                    });
                    ChartCanvas.Children.Add(new Line
                    {
                        X1 = MarginLeft,
                        Y1 = sy,
                        X2 = sx,
                        Y2 = sy,
                        Stroke = marker.FillBrush,
                        StrokeThickness = 1.2,
                        StrokeDashArray = new DoubleCollection { 3, 2 }
                    });
                }
                var dot = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = marker.FillBrush,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(dot, sx - 5);
                Canvas.SetTop(dot, sy - 5);
                ChartCanvas.Children.Add(dot);
                if (!string.IsNullOrWhiteSpace(marker.Title))
                {
                    double labelX = sx + 8 > MarginLeft + plotW - 110 ? sx - 115 : sx + 8;
                    double labelY = sy - 22 < MarginTop ? sy + 6 : sy - 22;
                    var badge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                        BorderBrush = marker.FillBrush,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(5, 2, 5, 2),
                        Child = new TextBlock
                        {
                            Text = string.IsNullOrWhiteSpace(marker.Subtitle)
                    ? marker.Title
                    : $"{marker.Title}\n{marker.Subtitle}",
                            FontSize = 10,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = marker.FillBrush
                        }
                    };
                    Canvas.SetLeft(badge, labelX);
                    Canvas.SetTop(badge, labelY);
                    ChartCanvas.Children.Add(badge);
                }
            }
        }
        private void AddText(string text, double left, double top, double fontSize, Brush brush, FontWeight weight)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                Foreground = brush,
                FontWeight = weight
            };
            Canvas.SetLeft(tb, left);
            Canvas.SetTop(tb, top);
            ChartCanvas.Children.Add(tb);
        }
        private void ChartCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var model = ChartModel;
            if (model == null) return;
            double width = ChartCanvas.ActualWidth;
            double height = ChartCanvas.ActualHeight;
            double plotW = width - MarginLeft - MarginRight;
            double plotH = height - MarginTop - MarginBottom;
            Point pos = e.GetPosition(ChartCanvas);
            if (pos.X < MarginLeft || pos.X > MarginLeft + plotW ||
            pos.Y < MarginTop || pos.Y > MarginTop + plotH)
            {
                HideCrosshair();
                return;
            }
            CrosshairV.X1 = pos.X; CrosshairV.Y1 = MarginTop;
            CrosshairV.X2 = pos.X; CrosshairV.Y2 = MarginTop + plotH;
            CrosshairV.Visibility = Visibility.Visible;
            CrosshairH.X1 = MarginLeft; CrosshairH.Y1 = pos.Y;
            CrosshairH.X2 = MarginLeft + plotW; CrosshairH.Y2 = pos.Y;
            CrosshairH.Visibility = Visibility.Visible;
            double dataX = model.XAxis.Min + (pos.X - MarginLeft) / plotW * (model.XAxis.Max - model.XAxis.Min);
            double dataY = model.YAxis.Min + (MarginTop + plotH - pos.Y) / plotH * (model.YAxis.Max - model.YAxis.Min);
            HoverTooltipText.Text =
            $"{model.XAxis.Title}: {dataX.ToString(model.XAxis.NumberFormat, CultureInfo.InvariantCulture)} {model.XAxis.Unit}\n" +
            $"{model.YAxis.Title}: {dataY.ToString(model.YAxis.NumberFormat, CultureInfo.InvariantCulture)} {model.YAxis.Unit}";
            HoverTooltip.Visibility = Visibility.Visible;
            double tipX = pos.X + 12;
            if (tipX + 140 > width) tipX = pos.X - 145;
            double tipY = pos.Y - 38;
            if (tipY < 4) tipY = pos.Y + 12;
            Canvas.SetLeft(HoverTooltip, tipX);
            Canvas.SetTop(HoverTooltip, tipY);
        }
        private void ChartCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            HideCrosshair();
        }
        private void HideCrosshair()
        {
            CrosshairV.Visibility = Visibility.Collapsed;
            CrosshairH.Visibility = Visibility.Collapsed;
            HoverTooltip.Visibility = Visibility.Collapsed;
        }
    }
}