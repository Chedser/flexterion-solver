using System.Windows.Media;

namespace MechCore.Models.Geometry;

/// <summary>
/// Результат расчёта сопряжения «Конус — Цилиндр» дугой радиуса R (Карточка 6).
/// </summary>
public class ConeCylinderBlendResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public bool HasWarning { get; set; }
    public string WarningMessage { get; set; } = string.Empty;

    /// <summary>false — выпуклый переход «Конус → Цилиндр», true — вогнутая галтель «Цилиндр → Конус».</summary>
    public bool IsConcaveFillet { get; set; }

    /// <summary>false — справа налево (Z-), true — слева направо (Z+).</summary>
    public bool IsLeftToRight { get; set; }

    /// <summary>false — вал (наружная), true — отверстие (внутренняя).</summary>
    public bool IsHole { get; set; }

    /// <summary>false — задний резцедержатель, true — передний резцедержатель.</summary>
    public bool IsFrontTurret { get; set; }

    /// <summary>Диаметр сопрягаемого цилиндра D_цил (мм).</summary>
    public double CylinderDiameter { get; set; }

    /// <summary>Координата Z теоретической вершины пересечения конуса и цилиндра (мм).</summary>
    public double CornerZ { get; set; }

    /// <summary>Угол уклона конуса α к оси детали (градусы).</summary>
    public double ConeAngleDeg { get; set; }

    /// <summary>Радиус сопряжения по чертежу R (мм).</summary>
    public double BlendRadius { get; set; }

    /// <summary>Расчётный радиус траектории с учётом радиуса пластины rε (мм).</summary>
    public double EffectiveArcRadius { get; set; }

    /// <summary>Осевое смещение точек касания ΔZ от теоретического угла (мм).</summary>
    public double TangentOffsetZ { get; set; }

    /// <summary>Диаметральное смещение точки касания на конусе ΔX (мм).</summary>
    public double TangentDeltaX { get; set; }

    /// <summary>Команда круговой интерполяции ("G02" или "G03").</summary>
    public string GCodeCommand { get; set; } = "G03";

    // Координаты точки начала дуги (входа на скругление)
    public double StartX { get; set; }
    public double StartZ { get; set; }

    // Координаты точки конца дуги (выхода со скругления)
    public double EndX { get; set; }
    public double EndZ { get; set; }

    // Инкрементальные координаты центра окружности от точки начала дуги (I — на радиус, K — по Z)
    public double CenterOffsetI { get; set; }
    public double CenterOffsetK { get; set; }

    /// <summary>Длина дуги сопряжения L_дуги (мм).</summary>
    public double ArcLengthMm { get; set; }

    /// <summary>Обороты шпинделя S (об/мин) и машинное время дуги T_м.</summary>
    public int SpindleRpmUsed { get; set; }
    public double MachiningTimeSeconds { get; set; }
    public string MachiningTimeText { get; set; } = "—";

    /// <summary>Готовый G-код (через радиус R).</summary>
    public string GCodeRadiusSnippet { get; set; } = string.Empty;

    /// <summary>Готовый G-код (через координаты центра I, K).</summary>
    public string GCodeIKSnippet { get; set; } = string.Empty;

    public Brush ArcStrokeBrush => HasWarning
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F57C00"))
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));

    // =========================================================================
    // ДАННЫЕ ДЛЯ ВЕКТОРНОГО ЭСКИЗА НА CANVAS (320 x 200)
    // =========================================================================

    public string PartBodyPathData { get; set; } = string.Empty;
    public string PartOutlinePathData { get; set; } = string.Empty;
    public string ArcPathData { get; set; } = string.Empty;

    public double CanvasStartX { get; set; }
    public double CanvasStartY { get; set; }
    public double CanvasEndX { get; set; }
    public double CanvasEndY { get; set; }
    public double CanvasCornerX { get; set; }
    public double CanvasCornerY { get; set; }

    public double CenterLineY { get; set; }
    public double ZeroLineX { get; set; }

    public string StartLabelText { get; set; } = string.Empty;
    public double LabelStartX { get; set; }
    public double LabelStartY { get; set; }

    public string EndLabelText { get; set; } = string.Empty;
    public double LabelEndX { get; set; }
    public double LabelEndY { get; set; }

    public string CornerLabelText { get; set; } = string.Empty;
    public double LabelCornerX { get; set; }
    public double LabelCornerY { get; set; }

    public string EquidistantArcPathData { get; set; } = string.Empty;
    public string EquidistantArrowHeadPoints { get; set; } = string.Empty;
}
