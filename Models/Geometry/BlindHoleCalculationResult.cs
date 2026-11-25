using System.Windows.Media;

namespace MechCore.Models.Geometry;

/// <summary>
/// Результат расчёта глухого отверстия, заборного конуса сверла и циклов G81 / G83.
/// </summary>
public class BlindHoleCalculationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>Мягкое предупреждение (например, если полная глубина меньше высоты конуса сверла).</summary>
    public bool HasWarning { get; set; }
    public string WarningMessage { get; set; } = string.Empty;

    /// <summary>false — ввод глубины по цилиндру L_цил, true — ввод полной глубины L_полн.</summary>
    public bool IsTotalDepthInput { get; set; }

    /// <summary>false — горизонтально (токарный ЧПУ), true — вертикально (фрезерный ЧПУ).</summary>
    public bool IsVertical { get; set; }

    /// <summary>Диаметр сверла D (мм).</summary>
    public double Diameter { get; set; }

    /// <summary>Угол при вершине сверла 2φ (градусы).</summary>
    public double TipAngleDegrees { get; set; }

    /// <summary>Высота заборного конуса сверла h (мм).</summary>
    public double ConeHeight { get; set; }

    /// <summary>Длина цилиндрической части отверстия L_цил (мм).</summary>
    public double CylindricalDepth { get; set; }

    /// <summary>Полная глубина сверления по кончику сверла L_полн (мм).</summary>
    public double TotalDepth { get; set; }

    /// <summary>Координата торца заготовки Z_нач (мм).</summary>
    public double StartZ { get; set; }

    /// <summary>Конечная координата вершины сверла Z_кон (мм).</summary>
    public double EndZ { get; set; }

    /// <summary>Рекомендуемый шаг глубокого сверления Q = 0.5·D (мм).</summary>
    public double PeckStepQ { get; set; }

    /// <summary>Готовый кадр простого цикла сверления G81.</summary>
    public string G81Snippet { get; set; } = string.Empty;

    /// <summary>Готовый кадр глубокого прерывистого сверления G83.</summary>
    public string G83Snippet { get; set; } = string.Empty;
    /// <summary>Обороты шпинделя S, использованные при расчёте времени (об/мин).</summary>
    public int SpindleRpmUsed { get; set; }

    /// <summary>Машинное время сверления Tм для прохода G81 (секунды).</summary>
    public double MachiningTimeSeconds { get; set; }

    /// <summary>Отформатированная строка времени сверления.</summary>
    public string MachiningTimeText { get; set; } = "—";

    // Цветовая индикация контура отверстия (синяя в штатном режиме, янтарная при предупреждении)
    public Brush HoleStrokeBrush => HasWarning
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F57C00"))
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));

    // =========================================================================
    // ДАННЫЕ ДЛЯ ВЕКТОРНОГО РАЗРЕЗА НА CANVAS (320 x 200)
    // =========================================================================

    /// <summary>Полигон заливки металла детали вокруг отверстия.</summary>
    public string PartBodyPoints { get; set; } = string.Empty;

    /// <summary>Ломаная линия контура просверленного отверстия.</summary>
    public string HoleOutlinePoints { get; set; } = string.Empty;

    /// <summary>Линия перехода цилиндрической части в конус сверла (координаты отрезка).</summary>
    public double ShoulderLineX1 { get; set; }
    public double ShoulderLineY1 { get; set; }
    public double ShoulderLineX2 { get; set; }
    public double ShoulderLineY2 { get; set; }

    /// <summary>Осевая линия отверстия (красная штрихпунктирная).</summary>
    public double AxisLineX1 { get; set; }
    public double AxisLineY1 { get; set; }
    public double AxisLineX2 { get; set; }
    public double AxisLineY2 { get; set; }

    /// <summary>Зелёная линия ноля детали (Z0 на торце).</summary>
    public double ZeroLineX1 { get; set; }
    public double ZeroLineY1 { get; set; }
    public double ZeroLineX2 { get; set; }
    public double ZeroLineY2 { get; set; }
    public double ZeroLabelX { get; set; }
    public double ZeroLabelY { get; set; }

    // Размерная линия полной глубины L (синяя)
    public double DimLineLX1 { get; set; }
    public double DimLineLY1 { get; set; }
    public double DimLineLX2 { get; set; }
    public double DimLineLY2 { get; set; }
    public string ExtLinesLPoints { get; set; } = string.Empty; // Path Data для выносных линий и засечек L

    // Размерная линия высоты конуса h (оранжевая)
    public double DimLineHX1 { get; set; }
    public double DimLineHY1 { get; set; }
    public double DimLineHX2 { get; set; }
    public double DimLineHY2 { get; set; }
    public string ExtLinesHPoints { get; set; } = string.Empty; // Path Data для выносных линий и засечек h

    // Размерная и выносные линии цилиндрической части L_цил (бирюзовая)
    public double DimLineCylX1 { get; set; }
    public double DimLineCylY1 { get; set; }
    public double DimLineCylX2 { get; set; }
    public double DimLineCylY2 { get; set; }
    public string ExtLinesCylPoints { get; set; } = string.Empty;
    public string CylDepthLabelText { get; set; } = string.Empty;
    public double LabelCylX { get; set; }
    public double LabelCylY { get; set; }


    // Подписи ключевых размеров прямо на чертеже
    public string ConeHeightLabelText { get; set; } = string.Empty;
    public double LabelConeX { get; set; }
    public double LabelConeY { get; set; }

    public string TotalDepthLabelText { get; set; } = string.Empty;
    public double LabelTotalX { get; set; }
    public double LabelTotalY { get; set; }

    public string DiameterLabelText { get; set; } = string.Empty;
    public double LabelDiamX { get; set; }
    public double LabelDiamY { get; set; }
}
