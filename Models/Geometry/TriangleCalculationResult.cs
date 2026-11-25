using System.Windows.Media;

namespace MechCore.Models.Geometry;

/// <summary>
/// Единый результат расчёта для Карточки 4 (Токарный конус ЧПУ / Прямоугольный треугольник).
/// </summary>
public class TriangleCalculationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public bool HasWarning { get; set; }
    public string WarningMessage { get; set; } = string.Empty;

    /// <summary>false — режим "Токарный конус ЧПУ", true — режим "Прямоугольный треугольник".</summary>
    public bool IsTriangleMode { get; set; }
    /// <summary>Обороты шпинделя S, использованные при расчёте времени прохода конуса (об/мин).</summary>
    public int SpindleRpmUsed { get; set; }

    /// <summary>Машинное время прохода конуса Tм (секунды).</summary>
    public double MachiningTimeSeconds { get; set; }

    /// <summary>Отформатированная строка времени прохода конуса.</summary>
    public string MachiningTimeText { get; set; } = "—";


    // =========================================================================
    // РЕЖИМ 1: ТОКАРНЫЙ КОНУС ЧПУ
    // =========================================================================

    public bool IsHole { get; set; }
    public bool IsLeftToRight { get; set; }

    /// <summary>Диаметр начала конуса D_нач (мм).</summary>
    public double ConeStartDiameter { get; set; }

    /// <summary>Диаметр конца конуса D_кон (мм).</summary>
    public double ConeEndDiameter { get; set; }

    /// <summary>Осевая длина конуса L (мм).</summary>
    public double ConeLength { get; set; }

    /// <summary>Угол уклона конуса α (половина угла при вершине, градусы).</summary>
    public double ConeSlopeAngleDeg { get; set; }

    /// <summary>Полный угол конуса 2α (градусы).</summary>
    public double ConeFullAngleDeg { get; set; }

    /// <summary>Конусность K = (D - d) / L (и запись в виде 1 : N).</summary>
    public double ConicityValue { get; set; }
    public string ConicityRatioText { get; set; } = string.Empty;

    /// <summary>Конусность в разных единицах (десятичная дробь, %, мм на 100 мм) и уклон на сторону.</summary>
    public string ConicityMultiUnitsText { get; set; } = string.Empty;
    public string SlopeMultiUnitsText { get; set; } = string.Empty;

    /// <summary>Автоопределение стандартного конуса (ГОСТ 25557 / ГОСТ 8593 / ISO 296).</summary>
    public bool HasStandardConeMatch { get; set; }
    public string StandardConeMatchText { get; set; } = string.Empty;

    /// <summary>Длина образующей конуса (гипотенуза, мм).</summary>
    public double ConeGeneratrixLength { get; set; }

    /// <summary>Координата начала по Z (мм) и конца по Z (мм).</summary>
    public double ConeStartZ { get; set; }
    public double ConeEndZ { get; set; }

    /// <summary>Готовый управляющий кадр G01 для стойки ЧПУ.</summary>
    public string ConeGCodeSnippet { get; set; } = string.Empty;

    public Brush ConeStrokeBrush => HasWarning
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F57C00"))
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));

    // =========================================================================
    // РЕЖИМ 2: ПРЯМОУГОЛЬНЫЙ ТРЕУГОЛЬНИК
    // =========================================================================

    /// <summary>Вертикальный (противолежащий углу α) катет a.</summary>
    public double SideA { get; set; }

    /// <summary>Горизонтальный (прилежащий к углу α) катет b.</summary>
    public double SideB { get; set; }

    /// <summary>Гипотенуза c.</summary>
    public double HypotenuseC { get; set; }

    /// <summary>Острый угол α (противолежащий катету a, градусы).</summary>
    public double AngleAlphaDeg { get; set; }

    /// <summary>Острый угол β (противолежащий катету b, градусы).</summary>
    public double AngleBetaDeg { get; set; }

    /// <summary>Площадь прямоугольного треугольника S.</summary>
    public double Area { get; set; }

    // =========================================================================
    // ДАННЫЕ ДЛЯ ВЕКТОРНОГО ЭСКИЗА НА CANVAS (320 x 200)
    // =========================================================================

    // Для режима конуса ЧПУ
    public string ConePartBodyPoints { get; set; } = string.Empty;
    public string ConePartOutlinePoints { get; set; } = string.Empty;
    public double CanvasStartX { get; set; }
    public double CanvasStartY { get; set; }
    public double CanvasEndX { get; set; }
    public double CanvasEndY { get; set; }
    public double CenterLineY { get; set; }
    public double ZeroLineX { get; set; }

    // Вынесенный вектор подачи G01 вдоль образующей конуса
    public double FeedVectorStartX { get; set; }
    public double FeedVectorStartY { get; set; }
    public double FeedVectorEndX { get; set; }
    public double FeedVectorEndY { get; set; }
    public string FeedArrowHeadPoints { get; set; } = string.Empty;

    // Подписи координат начала и конца конуса
    public string StartLabelText { get; set; } = string.Empty;
    public double LabelStartX { get; set; }
    public double LabelStartY { get; set; }

    public string EndLabelText { get; set; } = string.Empty;
    public double LabelEndX { get; set; }
    public double LabelEndY { get; set; }

    // Для режима прямоугольного треугольника
    public string TrianglePolygonPoints { get; set; } = string.Empty;
    public string RightAngleMarkPoints { get; set; } = string.Empty;
    public string SideALabelText { get; set; } = string.Empty;
    public double LabelSideAX { get; set; }
    public double LabelSideAY { get; set; }
    public string SideBLabelText { get; set; } = string.Empty;
    public double LabelSideBX { get; set; }
    public double LabelSideBY { get; set; }
    public string HypCLabelText { get; set; } = string.Empty;
    public double LabelHypCX { get; set; }
    public double LabelHypCY { get; set; }
    public string AngleAlphaLabelText { get; set; } = string.Empty;
    public double LabelAlphaX { get; set; }
    public double LabelAlphaY { get; set; }
}
