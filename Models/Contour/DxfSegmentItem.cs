using System;
using System.Windows.Media;
using MechCore.Core;

namespace MechCore.Models.Contour;

public enum DxfPrimitiveKind
{
    Line,
    Arc
}

public class DxfSegmentItem : ObservableObject
{
    public int Id { get; set; }
    public DxfPrimitiveKind Kind { get; set; }

    // Исходные координаты из DXF (мм)
    public double RawX1 { get; set; }
    public double RawY1 { get; set; }
    public double RawX2 { get; set; }
    public double RawY2 { get; set; }
    public double Radius { get; set; }
    public bool IsCcw { get; set; }

    // Координаты в системе станка ЧПУ (X — диаметр, Z — ось с нулём справа)
    public double StartX { get; set; }
    public double StartZ { get; set; }
    public double EndX { get; set; }
    public double EndZ { get; set; }

    // Экранные координаты для WPF Canvas
    public double ScreenX1 { get; set; }
    public double ScreenY1 { get; set; }
    public double ScreenX2 { get; set; }
    public double ScreenY2 { get; set; }
    public string ArcPathData { get; set; } = string.Empty;

    public bool IsLine => Kind == DxfPrimitiveKind.Line;
    public bool IsArc => Kind == DxfPrimitiveKind.Arc;
    public bool IsHorizontalLine => Kind == DxfPrimitiveKind.Line && Math.Abs(RawY1 - RawY2) < 1e-3;
    public bool IsVerticalLine => Kind == DxfPrimitiveKind.Line && Math.Abs(RawX1 - RawX2) < 1e-3;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(StrokeBrush));
                OnPropertyChanged(nameof(StrokeThickness));
            }
        }
    }

    public Brush StrokeBrush => IsSelected
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100"))
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));

    public double StrokeThickness => IsSelected ? 3.5 : 2.2;

    public string MachineCoordsSummary =>
        $"#{Id}: X{StartX:0.###} Z{StartZ:0.###} → X{EndX:0.###} Z{EndZ:0.###}";
}
