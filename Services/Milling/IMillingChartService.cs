using MechCore.Models.Charts;
using MechCore.Models.Materials;
using MechCore.Models.Milling;

namespace MechCore.Services.Milling
{
    public enum MillingChartMetric
    {
        ChipThinningCurve,      // Коэффициент утонения стружки k_hm(ae / Dc)
        SpindlePowerLoad,       // Загрузка шпинделя P(ap) в кВт и % от Pст
        EffectiveDiameterAp,    // Эффективный диаметр Deff(ap) для сферы / СМП
        CutterToolLifeTaylor    // Стойкость зубьев фрезы T(Vc), мин
    }

    public sealed class MillingChartContext
    {
        public IsoGroupCode IsoGroup { get; init; } = IsoGroupCode.P;
        public string MaterialLabel { get; init; } = "ISO P";
        public MillingToolMaterialKind ToolMaterial { get; init; } = MillingToolMaterialKind.SolidCarbide;
        public MillingCutterGeometryKind CutterGeometry { get; init; } = MillingCutterGeometryKind.FlatEndMill;

        public double CutterDiameter { get; init; } = 12.0;
        public double InsertDiameterIc { get; init; } = 10.0;
        public int FluteCount { get; init; } = 4;

        public double AxialDepthAp { get; init; } = 6.0;
        public double RadialWidthAe { get; init; } = 3.0;
        public double ActiveVc { get; init; } = 160.0;
        public double ActiveVf { get; init; } = 1000.0;
        public double BaseFz { get; init; } = 0.06;
        public double AdjustedFz { get; init; } = 0.06;

        public double SpecificCuttingForceKc { get; init; } = 2100.0;
        public double MachineSpindlePowerKw { get; init; } = 11.0;
        public double MachineEfficiency { get; init; } = 0.85;
    }

    public sealed class MillingChartSummary
    {
        public EngineeringChartModel ChartModel { get; init; } = new();
        public string ValueBadge { get; init; } = string.Empty;
        public string Headline { get; init; } = string.Empty;
        public string Advice { get; init; } = string.Empty;
        public bool IsWarning { get; init; }
    }

    public interface IMillingChartService
    {
        MillingChartSummary BuildChart(MillingChartMetric metric, MillingChartContext context);
    }
}
