using System;

namespace MechCore.Models.Tolerances
{
    public class ToleranceZoneResult
    {
        public ElementKind Kind { get; set; }
        public double NominalSize { get; set; }
        public string ToleranceClass { get; set; } = string.Empty;

        public double UpperDeviation { get; set; }
        public double LowerDeviation { get; set; }
        public double ToleranceValue { get; set; }

        public double MaxSize => Math.Round(NominalSize + UpperDeviation, 4);
        public double MinSize => Math.Round(NominalSize + LowerDeviation, 4);

        // Середина поля допуска для режима "Наладка"
        public double MeanSize => Math.Round((MaxSize + MinSize) / 2.0, 4);
        public double MeanDeviation => Math.Round((UpperDeviation + LowerDeviation) / 2.0, 4);
    }
}
