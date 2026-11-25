namespace MechCore.Models.Contour;

public enum StockKind
{
    Bar,  // Сплошной пруток
    Tube  // Труба / полая заготовка
}

public class StockParameters
{
    public StockKind Kind { get; set; } = StockKind.Bar;

    /// <summary>Наружный диаметр заготовки D_заг (мм).</summary>
    public double OuterDiameter { get; set; }

    /// <summary>Внутренний диаметр заготовки d_заг для трубы (мм, 0 для прутка).</summary>
    public double InnerDiameter { get; set; }

    /// <summary>Общая длина заготовки L_заг (мм).</summary>
    public double Length { get; set; }

    /// <summary>Припуск на правый торец от нуля Z0 до детали (мм).</summary>
    public double RightFaceAllowance { get; set; }

    /// <summary>Глубина зажима в кулачках патрона слева (мм).</summary>
    public double ChuckGripLength { get; set; } = 15.0;

    // =========================================================================
    // ЭКРАННЫЕ КООРДИНАТЫ ДЛЯ ОТРИСОВКИ НА CANVAS
    // =========================================================================

    public double ScreenLeft { get; set; }
    public double ScreenTop { get; set; }
    public double ScreenWidth { get; set; }
    public double ScreenHeight { get; set; }

    // Координаты кулачков патрона слева
    public double ChuckLeft { get; set; }
    public double ChuckTop { get; set; }
    public double ChuckWidth { get; set; }
    public double ChuckHeight { get; set; }

    // Текстовые выноски на чертеже
    public string OuterDiaLabel { get; set; } = string.Empty;
    public string LengthLabel { get; set; } = string.Empty;
}
