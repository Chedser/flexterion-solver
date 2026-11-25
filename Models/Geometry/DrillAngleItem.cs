namespace MechCore.Models.Geometry;

/// <summary>
/// Элемент справочника стандартных углов при вершине сверла.
/// </summary>
public class DrillAngleItem
{
    public string Name { get; set; } = string.Empty;
    public double AngleDegrees { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
}
