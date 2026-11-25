using System.Windows.Media;
namespace MechCore.Models.Geometry;

/// <summary>
/// Результат расчёта токарной фаски и траектории подвода резца для станка ЧПУ.
/// </summary>
public class ChamferCalculationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>Предупреждение наладчику (например, при Z_нач &lt; 0 и активном подводе).</summary>
    public bool HasWarning { get; set; }
    public string WarningMessage { get; set; } = string.Empty;

    // Состояния трёх переключателей
    public bool IsDecreasingX { get; set; }
    public bool IsLeftToRight { get; set; }
    public bool IsHole { get; set; }
    public bool UseApproach { get; set; }

    /// <summary>Осевой катет фаски c (мм).</summary>
    public double AxialLeg { get; set; }

    /// <summary>Радиальный катет фаски ΔR (мм).</summary>
    public double RadialLeg { get; set; }

    /// <summary>Диаметральный перепад ΔX = 2·ΔR (мм).</summary>
    public double DiameterDelta { get; set; }

    /// <summary>Угол фаски α (градусы).</summary>
    public double AngleDegrees { get; set; }

    /// <summary>Длина гипотенузы (образующей) фаски L (мм).</summary>
    public double ChamferHypotenuse { get; set; }

    // Координаты точки начала фаски
    public double StartX { get; set; }
    public double StartZ { get; set; }

    // Координаты точки конца фаски
    public double EndX { get; set; }
    public double EndZ { get; set; }

    // Координаты точки подвода резца
    public double ApproachX { get; set; }
    public double ApproachZ { get; set; }

    /// <summary>Готовый фрагмент G-кода для станка ЧПУ.</summary>
    public string CncGCodeSnippet { get; set; } = string.Empty;
    /// <summary>Суммарная длина рабочего хода по G01 (подвод + фаска, мм).</summary>
    public double CuttingPathLengthMm { get; set; }

    /// <summary>Обороты шпинделя S, использованные при расчёте времени (об/мин).</summary>
    public int SpindleRpmUsed { get; set; }

    /// <summary>Машинное время прохода Tм (секунды).</summary>
    public double MachiningTimeSeconds { get; set; }

    /// <summary>Отформатированная строка времени прохода (например: "1.42 сек" или "1 мин 12 сек").</summary>
    public string MachiningTimeText { get; set; } = "—";

    // =========================================================================
    // ДАННЫЕ ДЛЯ ВЕКТОРНОЙ СХЕМЫ НА CANVAS (320 x 200)
    // =========================================================================

    /// <summary>Точки заливки тела детали (вал снизу или стенка отверстия сверху).</summary>
    public string PartBodyPoints { get; set; } = string.Empty;

    /// <summary>Точки ломаной линии контура детали (без самой фаски или целиком).</summary>
    public string PartOutlinePoints { get; set; } = string.Empty;

    /// <summary>Показывать ли линию вынесенного подвода резца на схеме.</summary>
    public bool ShowApproachLine { get; set; }

    // Координаты линии фаски на Canvas
    public double CanvasStartX { get; set; }
    public double CanvasStartY { get; set; }
    public double CanvasEndX { get; set; }
    public double CanvasEndY { get; set; }

    // Координаты точки подвода резца на Canvas
    public double CanvasApproachX { get; set; }
    public double CanvasApproachY { get; set; }

    // Положение осевой линии (для режима "Вал" или центра отверстия)
    public double CenterLineY { get; set; }
    // Положение вертикальной линии ноля по Z (Z = 0)
    public double ZeroLineX { get; set; }

    // Подписи координат возле трёх точек на схеме
    public string ApproachLabelText { get; set; } = string.Empty;
    public double LabelApproachX { get; set; }
    public double LabelApproachY { get; set; }

    public string StartLabelText { get; set; } = string.Empty;
    public double LabelStartX { get; set; }
    public double LabelStartY { get; set; }

    public string EndLabelText { get; set; } = string.Empty;
    public double LabelEndX { get; set; }
    public double LabelEndY { get; set; }
    public Brush ChamferStrokeBrush => HasWarning
    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F57C00"))
    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));

    // Вынесенный вектор направления подачи G01 (параллельно фаске)
    public double FeedVectorStartX { get; set; }
    public double FeedVectorStartY { get; set; }
    public double FeedVectorEndX { get; set; }
    public double FeedVectorEndY { get; set; }
    public string FeedArrowHeadPoints { get; set; } = string.Empty;
    public double LabelFeedX { get; set; }
    public double LabelFeedY { get; set; }


    public Brush ApproachStrokeBrush => HasWarning
    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F"))
    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100"));

    // Угловая вершина прямоугольного треугольника катетов (проекция)
    public double LegCornerX { get; set; }
    public double LegCornerY { get; set; }

    // Подписи длин горизонтального (c) и вертикального (ΔR) катетов
    public string HorizontalLegText { get; set; } = string.Empty;
    public double LabelHorizLegX { get; set; }
    public double LabelHorizLegY { get; set; }

    public string VerticalLegText { get; set; } = string.Empty;
    public double LabelVertLegX { get; set; }
    public double LabelVertLegY { get; set; }

}
