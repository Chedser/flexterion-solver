using System.Windows.Media;

namespace MechCore.Models.Geometry;

/// <summary>
/// Результат расчёта прямоугольной канавки и отрезки с циклом G75 (Карточка 5).
/// </summary>
public class GrooveCalculationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public bool HasWarning { get; set; }
    public string WarningMessage { get; set; } = string.Empty;

    public bool HasInfo { get; set; }
    public string InfoMessage { get; set; } = string.Empty;

    /// <summary>false — проточка наружной канавки, true — отрезка детали.</summary>
    public bool IsPartOffMode { get; set; }

    /// <summary>false — справа налево (Z-), true — слева направо (Z+).</summary>
    public bool IsLeftToRight { get; set; }

    /// <summary>Учитывается ли толщина резца B (если true — выводятся 2 варианта кода).</summary>
    public bool ConsiderToolWidth { get; set; }

    /// <summary>Безопасный диаметр подвода G00 X... (мм).</summary>
    public double ApproachDiameterX { get; set; }

    /// <summary>Конечный диаметр X (диаметр дна канавки или X с учётом перебега при отрезке, мм).</summary>
    public double TargetDiameterX { get; set; }

    public double MinBladeOverhangMm { get; set; }
    public double MaxBladeOverhangMm { get; set; }
    public string BladeOverhangRangeText { get; set; } = "—";

    /// <summary>Радиальная глубина канавки на сторону (мм).</summary>
    public double RadialDepth { get; set; }

    /// <summary>Расчётная стартовая координата Z для первого врезания (мм).</summary>
    public double ComputedStartZ { get; set; }

    /// <summary>Расчётная конечная координата Z (мм).</summary>
    public double ComputedEndZ { get; set; }

    /// <summary>Шаг смещения по оси Z в миллиметрах (мм) и в микронах для Fanuc (P/Q).</summary>
    public double StepZMm { get; set; }
    public int StepQMicrons { get; set; }
    public int StepPMicrons { get; set; }

    /// <summary>Количество проходов по ширине канавки.</summary>
    public int PassesCount { get; set; }

    /// <summary>Вариант 1: обычный числовой код G75 (без переменных).</summary>
    public string StandardGCodeSnippet { get; set; } = string.Empty;

    /// <summary>Вариант 2: параметрический код G75 с переменной #N (активен при учёте толщины резца).</summary>
    public string MacroGCodeSnippet { get; set; } = string.Empty;

    public Brush AccentBrush => HasWarning
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F57C00"))
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));

    /// <summary>Суммарная длина рабочего пути врезания по X (мм).</summary>
    public double TotalCuttingPathMm { get; set; }

    /// <summary>Обороты шпинделя S, использованные при расчёте времени (об/мин).</summary>
    public int SpindleRpmUsed { get; set; }

    /// <summary>Машинное время обработки канавки / отрезки Tм (секунды).</summary>
    public double MachiningTimeSeconds { get; set; }

    /// <summary>Отформатированная строка времени обработки.</summary>
    public string MachiningTimeText { get; set; } = "—";
    /// <summary>Включён ли чистовой калибрующий проход (G01).</summary>
    public bool IncludeFinishPass { get; set; }

    /// <summary>true — чистовой проход с припуском (стенки и дно), false — быстрая зачистка дна без припуска.</summary>
    public bool IsFinishPassWithAllowance { get; set; }

    /// <summary>Черновой диаметр X для цикла G75 с учётом припуска (мм).</summary>
    public double RoughTargetDiameterX { get; set; }


    // =========================================================================
    // ДАННЫЕ ДЛЯ ВЕКТОРНОГО ЭСКИЗА НА CANVAS (320 x 200)
    // =========================================================================

    /// <summary>Контур детали с канавкой (или прорезью отрезки).</summary>
    public string PartBodyPoints { get; set; } = string.Empty;
    public string PartOutlinePoints { get; set; } = string.Empty;

    /// <summary>Зелёные оси ноля детали X0 и Z0.</summary>
    public double CenterLineY { get; set; }
    public double ZeroLineX { get; set; }

    /// <summary>Прямоугольник пластины резца (в точке стартового врезания).</summary>
    public double ToolLeftX { get; set; }
    public double ToolTopY { get; set; }
    public double ToolWidthPx { get; set; }
    public double ToolHeightPx { get; set; }

    /// <summary>Маркер привязанной кромки резца (левая или правая вершина пластины).</summary>
    public double AnchorDotX { get; set; }
    public double AnchorDotY { get; set; }

    /// <summary>Траектория первого врезания G75 (вертикальная линия со стрелкой).</summary>
    public double PlungeX { get; set; }
    public double PlungeStartY { get; set; }
    public double PlungeEndY { get; set; }
    public string PlungeArrowPoints { get; set; } = string.Empty;

    /// <summary>Подписи на эскизе.</summary>
    public string ZStartLabelText { get; set; } = string.Empty;
    public double LabelZStartX { get; set; }
    public double LabelZStartY { get; set; }

    public string ZEndLabelText { get; set; } = string.Empty;
    public double LabelZEndX { get; set; }
    public double LabelZEndY { get; set; }

    public string BottomDiaLabelText { get; set; } = string.Empty;
    public double LabelBottomDiaX { get; set; }
    public double LabelBottomDiaY { get; set; }

    /// <summary>Полная ширина канавки W (или ширина прорези при отрезке, мм).</summary>
    public double GrooveWidthMm { get; set; }

    /// <summary>Показывать ли размерную линию ширины W на эскизе.</summary>
    public bool ShowWidthDimension { get; set; }

    /// <summary>Размерная линия ширины канавки W на Canvas.</summary>
    public double WidthDimLeftX { get; set; }
    public double WidthDimRightX { get; set; }
    public double WidthDimY { get; set; }
    public string WidthLeftArrowPoints { get; set; } = string.Empty;
    public string WidthRightArrowPoints { get; set; } = string.Empty;
    public string WidthLabelText { get; set; } = string.Empty;
    public double LabelWidthX { get; set; }
    public double LabelWidthY { get; set; }

    /// <summary>Размерная линия глубины канавки t (на сторону) на Canvas.</summary>
    public double DepthDimX { get; set; }
    public double DepthDimTopY { get; set; }
    public double DepthDimBottomY { get; set; }
    public double DepthExtStartX { get; set; }
    public string DepthTopArrowPoints { get; set; } = string.Empty;
    public string DepthBottomArrowPoints { get; set; } = string.Empty;
    public string DepthLabelText { get; set; } = string.Empty;
    public double LabelDepthX { get; set; }
    public double LabelDepthY { get; set; }

    /// <summary>Подпись координаты Z начальной стенки канавки (по чертежу).</summary>
    public string ZWallStartLabelText { get; set; } = string.Empty;
    public double LabelZWallStartX { get; set; }
    public double LabelZWallStartY { get; set; }
    /// <summary>Показывать ли слой чистового припуска на эскизе канавки.</summary>
    public bool ShowFinishAllowanceLayer { get; set; }

    /// <summary>Полигон заливки припуска по левой стенке, дну и правой стенке на Canvas.</summary>
    public string FinishAllowancePolygonPoints { get; set; } = string.Empty;

    /// <summary>Пунктирная линия чернового контура G75 (внутри чистового профиля).</summary>
    public string RoughOutlinePoints { get; set; } = string.Empty;



}
