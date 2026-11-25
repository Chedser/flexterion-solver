using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace MechCore.Models.Charts
{
    public readonly record struct ChartPoint(double X, double Y);

    public enum ChartZoneOrientation
    {
        Horizontal, // По оси Y (например, зона оптимальной скорости Vc)
        Vertical    // По оси X (например, зона высокой твёрдости HB)
    }

    /// <summary>
    /// Технологическая зона на графике (зелёная — оптимум, янтарная/красная — риск).
    /// </summary>
    public class ChartZone
    {
        public string Label { get; set; } = string.Empty;
        public ChartZoneOrientation Orientation { get; set; } = ChartZoneOrientation.Vertical;
        public double Start { get; set; }
        public double End { get; set; }
        public Brush FillBrush { get; set; } = new SolidColorBrush(Color.FromArgb(30, 76, 175, 80));
        public Brush LabelBrush { get; set; } = new SolidColorBrush(Color.FromRgb(46, 125, 50));
    }

    /// <summary>
    /// Пороговая линия (ограничение станка, инструмента или допуска).
    /// </summary>
    public class ChartThresholdLine
    {
        public string Label { get; set; } = string.Empty;
        public ChartZoneOrientation Orientation { get; set; } = ChartZoneOrientation.Horizontal;
        public double Value { get; set; }
        public Brush StrokeBrush { get; set; } = new SolidColorBrush(Color.FromRgb(198, 40, 40)); // #C62828
        public DoubleCollection StrokeDashArray { get; set; } = new() { 4, 3 };
    }

    /// <summary>
    /// Кривая зависимости (например, Vc(HB), kc(HB) или f(HB) для группы ISO).
    /// </summary>
    public class ChartSeries
    {
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public Brush StrokeBrush { get; set; } = Brushes.DodgerBlue;
        public double StrokeThickness { get; set; } = 2.2;
        public bool IsDashed { get; set; }
        public bool IsHighlighted { get; set; } = true;
        public bool IsEngineerOnly { get; set; }
        public List<ChartPoint> Points { get; set; } = new();
    }

    /// <summary>
    /// Текущая рабочая точка выбранного материала или режима резания.
    /// </summary>
    public class WorkingPointMarker
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public Brush FillBrush { get; set; } = new SolidColorBrush(Color.FromRgb(198, 40, 40));
        public bool ShowGuideLines { get; set; } = true;
    }

    /// <summary>
    /// Настройка координатной оси.
    /// </summary>
    public class ChartAxisConfig
    {
        public string Title { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public double Min { get; set; }
        public double Max { get; set; } = 100;
        public double Step { get; set; } = 20;
        public string NumberFormat { get; set; } = "F0";
    }

    /// <summary>
    /// Полная модель инженерного графика для привязки к EngineeringChartControl.
    /// </summary>
    public class EngineeringChartModel
    {
        public string ChartTitle { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ActiveMaterialBadge { get; set; } = string.Empty;

        public ChartAxisConfig XAxis { get; set; } = new();
        public ChartAxisConfig YAxis { get; set; } = new();

        public List<ChartZone> Zones { get; set; } = new();
        public List<ChartThresholdLine> Thresholds { get; set; } = new();
        public List<ChartSeries> Series { get; set; } = new();
        public List<WorkingPointMarker> Markers { get; set; } = new();
    }
}
