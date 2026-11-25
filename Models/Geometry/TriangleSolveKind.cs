namespace MechCore.Models.Geometry;

/// <summary>
/// Способ задания известных величин для прямоугольного треугольника.
/// </summary>
public enum TriangleSolveKind
{
    /// <summary>По двум катетам (a и b).</summary>
    TwoLegs,

    /// <summary>По катету (a) и гипотенузе (c).</summary>
    LegAndHypotenuse,

    /// <summary>По катету (a) и противолежащему углу (α).</summary>
    LegAndOppositeAngle,

    /// <summary>По катету (a) и прилежащему углу (β).</summary>
    LegAndAdjacentAngle,

    /// <summary>По гипотенузе (c) и острому углу (α).</summary>
    HypotenuseAndAngle
}

/// <summary>
/// Способ задания параметров токарного конуса ЧПУ.
/// </summary>
public enum ConeSolveKind
{
    /// <summary>По двум диаметрам (D_нач, D_кон) и длине конуса (L).</summary>
    ByTwoDiametersAndLength,

    /// <summary>По начальному диаметру (D_нач), длине конуса (L) и углу уклона (α).</summary>
    ByStartDiameterLengthAndAngle,

    /// <summary>По двум диаметрам (D_нач, D_кон) и углу уклона (α).</summary>
    ByTwoDiametersAndAngle
}
