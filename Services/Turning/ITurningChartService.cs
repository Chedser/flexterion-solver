using MechCore.Models.Charts;
using MechCore.Models.Materials;
using MechCore.Models.Turning;

namespace MechCore.Services.Turning
{
    public enum TurningChartMetric
    {
        ToolLifeTaylor,      // Стойкость инструмента T(Vc), мин
        SurfaceRoughnessRa,  // Шероховатость Ra(fn) по радиусам rε, мкм
        CuttingPowerAp,      // Мощность резания Pc(ap), кВт
        SpindleSpeedProfile  // Разгон шпинделя n(D) в G96 / G97, об/мин
    }

    public sealed class TurningChartContext
    {
        public IsoGroupCode IsoGroup { get; init; } = IsoGroupCode.P;
        public string MaterialLabel { get; init; } = "ISO P";
        public ToolMaterialKind ToolMaterial { get; init; } = ToolMaterialKind.CoatedCarbide;

        // Текущие параметры из справочника (Карточка 1)
        public double RecommendedVc { get; init; } = 180.0;
        public double RecommendedFeed { get; init; } = 0.25;
        public double RecommendedAp { get; init; } = 2.0;

        // Параметры из симулятора G96/G97 (Карточка 2)
        public bool IsG96Mode { get; init; } = true;
        public double SimVc { get; init; } = 180.0;
        public int SimRpm { get; init; } = 1200;
        public double StartDiameter { get; init; } = 80.0;
        public double MinDiameter { get; init; } = 15.0;
        public int G50MaxRpm { get; init; } = 2500;

        // Параметры шероховатости (Карточка 3)
        public double NoseRadius { get; init; } = 0.4;
        public double CurrentRoughnessFeed { get; init; } = 0.12;
        public double TargetRa { get; init; } = 1.6;
    }

    public sealed class TurningChartSummary
    {
        public EngineeringChartModel ChartModel { get; init; } = new();
        public string ValueBadge { get; init; } = string.Empty;
        public string Headline { get; init; } = string.Empty;
        public string Advice { get; init; } = string.Empty;
        public bool IsWarning { get; init; }
    }

    public interface ITurningChartService
    {
        TurningChartSummary BuildChart(TurningChartMetric metric, TurningChartContext context);
    }
}
