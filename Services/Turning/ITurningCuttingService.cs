using MechCore.Models.Materials;
using MechCore.Models.Turning;

namespace MechCore.Services.Turning
{
    public interface ITurningCuttingService
    {
        TurningCuttingResult Calculate(TurningCuttingRequest request);
        RoughnessCalculationResult CalculateRoughness(RoughnessCalculationRequest request);

        SpindleSimulationResult SimulateSpindle(SpindleSimulationRequest request);

        /// <summary>
        /// Удобный метод для автоподбора режимов из других модулей (например, G75, G81/G83, фаски).
        /// </summary>
        TurningCuttingResult QuickRecommend(
            IsoMaterialGroupItem isoGroup,
            MaterialGradeItem? grade,
            TurningOperationKind operation,
            CuttingStageKind stage,
            double diameter,
            double toolOrGrooveWidth = 3.0,
            double overhangRatio = 3.0,
            ToolMaterialKind toolMaterial = ToolMaterialKind.CoatedCarbide);
    }
}
