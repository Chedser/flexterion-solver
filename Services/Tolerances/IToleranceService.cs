using MechCore.Models.Tolerances;

namespace MechCore.Services.Tolerances
{
    public interface IToleranceService
    {
        double CalculateStandardTolerance(double size, string itGrade);
        ToleranceZoneResult CalculateElement(double nominalSize, string toleranceClass);
        FitCalculationResult CalculateFit(double nominalSize, string holeClass, string shaftClass);
    }
}
