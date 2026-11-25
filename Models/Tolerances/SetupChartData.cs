using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace MechCore.Models.Tolerances
{
    public class SetupChartData
    {
        public bool IsShaft { get; }

        public GridLength AboveNominalHeight { get; }
        public GridLength BelowNominalHeight { get; }

        public GridLength AboveToleranceHeight { get; }
        public GridLength ToleranceZoneHeight { get; }
        public GridLength BelowToleranceHeight { get; }

        public GridLength AboveMeanHeight { get; }
        public GridLength BelowMeanHeight { get; }

        public GridLength AboveMeasuredHeight { get; }
        public GridLength BelowMeasuredHeight { get; }

        public bool HasMeasuredLine { get; }
        public bool IsMeasuredInTolerance { get; }
        public bool IsMeasuredReworkable { get; }

        public string NominalLabel { get; }
        public string MinLabel { get; }
        public string MeanLabel { get; }
        public string MaxLabel { get; }
        public string MeasuredLabel { get; }

        public SetupChartData(ToleranceZoneResult zone, double? measuredSize)
        {
            IsShaft = zone.Kind == ElementKind.Shaft;

            double nom = zone.NominalSize;
            double min = zone.MinSize;
            double max = zone.MaxSize;
            double mean = zone.MeanSize;

            NominalLabel = IsShaft
                ? $"Номинал вала: {nom:0.0000}"
                : $"Номинал отверстия: {nom:0.0000}";

            MinLabel = $"Min: {min:0.0000}";
            MeanLabel = $"Середина: {mean:0.0000}";
            MaxLabel = $"Max: {max:0.0000}";

            var points = new List<double> { nom, min, max };

            if (measuredSize.HasValue)
            {
                HasMeasuredLine = true;
                double m = measuredSize.Value;
                points.Add(m);

                IsMeasuredInTolerance = m >= min && m <= max;
                IsMeasuredReworkable = !IsMeasuredInTolerance && (IsShaft ? m > max : m < min);

                MeasuredLabel = $"Замер: {m:0.0000}";
            }
            else
            {
                MeasuredLabel = string.Empty;
            }

            double rawMin = points.Min();
            double rawMax = points.Max();
            double span = rawMax - rawMin;

            if (span < 0.0005) span = 0.002;
            double chartMin = rawMin - span * 0.22;
            double chartMax = rawMax + span * 0.22;
            double totalRange = chartMax - chartMin;

            double Ratio(double val) => Math.Clamp((val - chartMin) / totalRange, 0.03, 0.97);

            double rNom = Ratio(nom);
            double rMin = Ratio(min);
            double rMax = Ratio(max);
            double rMean = Ratio(mean);

            AboveNominalHeight = new GridLength(1.0 - rNom, GridUnitType.Star);
            BelowNominalHeight = new GridLength(rNom, GridUnitType.Star);

            AboveToleranceHeight = new GridLength(1.0 - rMax, GridUnitType.Star);
            ToleranceZoneHeight = new GridLength(Math.Max(rMax - rMin, 0.02), GridUnitType.Star);
            BelowToleranceHeight = new GridLength(rMin, GridUnitType.Star);

            AboveMeanHeight = new GridLength(1.0 - rMean, GridUnitType.Star);
            BelowMeanHeight = new GridLength(rMean, GridUnitType.Star);

            if (HasMeasuredLine)
            {
                double rMeas = Ratio(measuredSize!.Value);
                AboveMeasuredHeight = new GridLength(1.0 - rMeas, GridUnitType.Star);
                BelowMeasuredHeight = new GridLength(rMeas, GridUnitType.Star);
            }
            else
            {
                AboveMeasuredHeight = new GridLength(1, GridUnitType.Star);
                BelowMeasuredHeight = new GridLength(0, GridUnitType.Star);
            }
        }
    }
}
