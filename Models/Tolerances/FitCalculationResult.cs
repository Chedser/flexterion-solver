using System;

namespace MechCore.Models.Tolerances
{
    public class FitCalculationResult
    {
        public double NominalSize { get; set; }
        public ToleranceZoneResult Hole { get; set; } = new();
        public ToleranceZoneResult Shaft { get; set; } = new();

        public FitType FitType { get; set; }

        public double MaxClearance => Math.Round(Hole.UpperDeviation - Shaft.LowerDeviation, 4);
        public double MinClearance => Math.Round(Hole.LowerDeviation - Shaft.UpperDeviation, 4);

        public double MaxInterference => Math.Round(Shaft.UpperDeviation - Hole.LowerDeviation, 4);
        public double MinInterference => Math.Round(Shaft.LowerDeviation - Hole.UpperDeviation, 4);

        public string FitTypeDisplayName => FitType switch
        {
            FitType.Clearance => "Посадка с зазором",
            FitType.Interference => "Посадка с натягом",
            _ => "Переходная посадка"
        };
    }
}
