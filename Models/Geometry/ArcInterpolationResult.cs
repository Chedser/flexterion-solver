using System.Windows.Media;

namespace MechCore.Models.Geometry;

/// <summary>
/// Способ задания координат дуги скругления или галтели.
/// </summary>
public enum ArcCoordinateInputMode
{
    /// <summary>По вершине острого угла пересечения прямых.</summary>
    ByCornerVertex,

    /// <summary>По точке начала дуги (врезания).</summary>
    ByStartPoint,

    /// <summary>По точке конца дуги (выхода).</summary>
    ByEndPoint
}

/// <summary>
/// Результат расчёта круговой интерполяции (G02 / G03) для токарного станка ЧПУ.
/// </summary>
public class ArcInterpolationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>Мягкое предупреждение (например, переход диаметра через X = 0).</summary>
    public bool HasWarning { get; set; }
    public string WarningMessage { get; set; } = string.Empty;

    // Состояния переключателей
    public bool IsConcaveFillet { get; set; }
    public bool IsDecreasingX { get; set; }
    public bool IsLeftToRight { get; set; }
    public bool IsHole { get; set; }
    public bool IsFrontTurret { get; set; }
    public ArcCoordinateInputMode InputMode { get; set; }

    /// <summary>Радиус скругления или галтели R (мм).</summary>
    public double Radius { get; set; }

    /// <summary>Команда круговой интерполяции ("G02" или "G03").</summary>
    public string GCodeCommand { get; set; } = "G03";

    // Координаты точки начала дуги (подход G01)
    public double StartX { get; set; }
    public double StartZ { get; set; }

    // Координаты точки конца дуги
    public double EndX { get; set; }
    public double EndZ { get; set; }

    // Координаты вершины острого угла (пересечение касательных)
    public double CornerX { get; set; }
    public double CornerZ { get; set; }

    // Инкрементальные координаты центра окружности от точки старта дуги
    /// <summary>Смещение центра по оси X на радиус (мм).</summary>
    public double CenterOffsetI { get; set; }

    /// <summary>Смещение центра по оси Z (мм).</summary>
    public double CenterOffsetK { get; set; }

    /// <summary>Готовый G-код в формате через радиус R.</summary>
    public string GCodeRadiusSnippet { get; set; } = string.Empty;

    /// <summary>Готовый G-код в формате через координаты центра I и K.</summary>
    public string GCodeIKSnippet { get; set; } = string.Empty;
    /// <summary>Длина дуги круговой интерполяции L_дуги = π·R / 2 (мм).</summary>
    public double ArcLengthMm { get; set; }

    /// <summary>Обороты шпинделя S, использованные при расчёте времени (об/мин).</summary>
    public int SpindleRpmUsed { get; set; }

    /// <summary>Машинное время прохода дуги Tм (секунды).</summary>
    public double MachiningTimeSeconds { get; set; }

    /// <summary>Отформатированная строка времени прохода дуги.</summary>
    public string MachiningTimeText { get; set; } = "—";

    // Цветовая индикация дуги (синяя в штатном режиме, янтарная при предупреждении)
    public Brush ArcStrokeBrush => HasWarning
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F57C00"))
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));

    // =========================================================================
    // ДАННЫЕ ДЛЯ ВЕКТОРНОЙ СХЕМЫ НА CANVAS (320 x 200)
    // =========================================================================

    /// <summary>Полигон заливки тела детали.</summary>
    public string PartBodyPoints { get; set; } = string.Empty;
  
    /// <summary>Геометрия заливки тела детали с учётом кривизны дуги (M ... A ... Z).</summary>
    public string PartBodyPathData { get; set; } = string.Empty;

    /// <summary>Геометрия контурных линий детали до и после дуги.</summary>
    public string PartOutlinePathData { get; set; } = string.Empty;

    // Подпись координат в вершине сопрягаемого угла
    public string CornerLabelText { get; set; } = string.Empty;
    public double LabelCornerX { get; set; }
    public double LabelCornerY { get; set; }

    /// <summary>Контур детали до и после скругления.</summary>
    public string PartOutlinePoints { get; set; } = string.Empty;

    /// <summary>SVG/WPF Path Data (M ... A ...) для плавной отрисовки дуги на Canvas.</summary>
    public string ArcPathData { get; set; } = string.Empty;

    /// <summary>Координаты дуги и центра на Canvas.</summary>
    public double CanvasStartX { get; set; }
    public double CanvasStartY { get; set; }
    public double CanvasEndX { get; set; }
    public double CanvasEndY { get; set; }
    public double CanvasCornerX { get; set; }
    public double CanvasCornerY { get; set; }
    public double CanvasCenterX { get; set; }
    public double CanvasCenterY { get; set; }

    /// <summary>Положение зелёных осей ноля детали X0 и Z0.</summary>
    public double CenterLineY { get; set; }
    public double ZeroLineX { get; set; }

    // Подписи координат возле точек начала и конца дуги
    public string StartLabelText { get; set; } = string.Empty;
    public double LabelStartX { get; set; }
    public double LabelStartY { get; set; }

    public string EndLabelText { get; set; } = string.Empty;
    public double LabelEndX { get; set; }
    public double LabelEndY { get; set; }

    // Эквидистантная дуга направления круговой интерполяции (G02 / G03)
    public string EquidistantArcPathData { get; set; } = string.Empty;
    public string EquidistantArrowHeadPoints { get; set; } = string.Empty;
    public double LabelFeedX { get; set; }
    public double LabelFeedY { get; set; }

}
