using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace MechCore.Models.Tolerances
{
    public class FitChartData
    {
        // Пропорции нулевой линии (0.000)
        public GridLength AboveZeroHeight { get; }
        public GridLength BelowZeroHeight { get; }

        // Пропорции прямоугольника ОТВЕРСТИЯ
        public GridLength AboveHoleHeight { get; }
        public GridLength HoleZoneHeight { get; }
        public GridLength BelowHoleHeight { get; }

        // Пропорции прямоугольника ВАЛА
        public GridLength AboveShaftHeight { get; }
        public GridLength ShaftZoneHeight { get; }
        public GridLength BelowShaftHeight { get; }

        // Координаты середин полей допусков (для дополнительной информации)
        public GridLength AboveHoleMeanHeight { get; }
        public GridLength BelowHoleMeanHeight { get; }
        public string HoleMeanLabel { get; }

        public GridLength AboveShaftMeanHeight { get; }
        public GridLength BelowShaftMeanHeight { get; }
        public string ShaftMeanLabel { get; }

        public string MeanFitSummary { get; }

        // Текстовые подписи
        public string ZeroLabel { get; }
        public string HoleClassLabel { get; }
        public string HoleUpperLabel { get; }
        public string HoleLowerLabel { get; }

        public string ShaftClassLabel { get; }
        public string ShaftUpperLabel { get; }
        public string ShaftLowerLabel { get; }

        // Пропорции закрашенной зоны между серединами Em и em
        public GridLength AboveMeanZoneHeight { get; }
        public GridLength MeanZoneHeight { get; }
        public GridLength BelowMeanZoneHeight { get; }

        public FitChartData(FitCalculationResult fit, bool showNominalSizes = false)
        {
            var hole = fit.Hole;
            var shaft = fit.Shaft;
            double nom = hole.NominalSize;

            ZeroLabel = $"0.000 (Ø{nom:0.####})";

            HoleClassLabel = $"{hole.ToleranceClass}\n({hole.ToleranceValue:0.0000})";
            HoleUpperLabel = FormatDevWithSize(hole.UpperDeviation, nom, showNominalSizes);
            HoleLowerLabel = FormatDevWithSize(hole.LowerDeviation, nom, showNominalSizes);

            ShaftClassLabel = $"{shaft.ToleranceClass}\n({shaft.ToleranceValue:0.0000})";
            ShaftUpperLabel = FormatDevWithSize(shaft.UpperDeviation, nom, showNominalSizes);
            ShaftLowerLabel = FormatDevWithSize(shaft.LowerDeviation, nom, showNominalSizes);

            // Находим общий разброс отклонений (включая нулевую линию 0.0)
            var points = new List<double>
            {
                0.0,
                hole.UpperDeviation,
                hole.LowerDeviation,
                shaft.UpperDeviation,
                shaft.LowerDeviation
            };

            double rawMin = points.Min();
            double rawMax = points.Max();
            double span = rawMax - rawMin;

            // Защита от нулевого разброса + отступы по 22% сверху и снизу для подписей
            if (span < 0.001) span = 0.004;
            double chartMin = rawMin - span * 0.22;
            double chartMax = rawMax + span * 0.22;
            double totalRange = chartMax - chartMin;

            double Ratio(double val) => Math.Clamp((val - chartMin) / totalRange, 0.03, 0.97);

            double rZero = Ratio(0.0);
            double rHoleTop = Ratio(hole.UpperDeviation);
            double rHoleBot = Ratio(hole.LowerDeviation);
            double rShaftTop = Ratio(shaft.UpperDeviation);
            double rShaftBot = Ratio(shaft.LowerDeviation);

            AboveZeroHeight = new GridLength(1.0 - rZero, GridUnitType.Star);
            BelowZeroHeight = new GridLength(rZero, GridUnitType.Star);

            AboveHoleHeight = new GridLength(1.0 - rHoleTop, GridUnitType.Star);
            HoleZoneHeight = new GridLength(Math.Max(rHoleTop - rHoleBot, 0.04), GridUnitType.Star);
            BelowHoleHeight = new GridLength(rHoleBot, GridUnitType.Star);

            AboveShaftHeight = new GridLength(1.0 - rShaftTop, GridUnitType.Star);
            ShaftZoneHeight = new GridLength(Math.Max(rShaftTop - rShaftBot, 0.04), GridUnitType.Star);
            BelowShaftHeight = new GridLength(rShaftBot, GridUnitType.Star);

            double holeMean = (hole.UpperDeviation + hole.LowerDeviation) / 2.0;
            double shaftMean = (shaft.UpperDeviation + shaft.LowerDeviation) / 2.0;
            double rHoleMean = Ratio(holeMean);
            double rShaftMean = Ratio(shaftMean);
            double rTopMean = Math.Max(rHoleMean, rShaftMean);
            double rBotMean = Math.Min(rHoleMean, rShaftMean);

            AboveMeanZoneHeight = new GridLength(1.0 - rTopMean, GridUnitType.Star);
            MeanZoneHeight = new GridLength(Math.Max(rTopMean - rBotMean, 0.005), GridUnitType.Star);
            BelowMeanZoneHeight = new GridLength(rBotMean, GridUnitType.Star);

            AboveHoleMeanHeight = new GridLength(1.0 - rHoleMean, GridUnitType.Star);
            BelowHoleMeanHeight = new GridLength(rHoleMean, GridUnitType.Star);
            HoleMeanLabel = $"Em: {FormatDevWithSize(holeMean, nom, showNominalSizes)}";

            AboveShaftMeanHeight = new GridLength(1.0 - rShaftMean, GridUnitType.Star);
            BelowShaftMeanHeight = new GridLength(rShaftMean, GridUnitType.Star);
            ShaftMeanLabel = $"em: {FormatDevWithSize(shaftMean, nom, showNominalSizes)}";

            double meanClearance = holeMean - shaftMean;
            if (Math.Abs(meanClearance) < 0.00001)
                MeanFitSummary = "Средний (вероятностный) зазор / натяг: 0.0000 мм";
            else if (meanClearance > 0)
                MeanFitSummary = $"Средний (вероятностный) зазор (Sm = Em − em): +{meanClearance:0.0000} мм";
            else
                MeanFitSummary = $"Средний (вероятностный) натяг (Nm = em − Em): +{Math.Abs(meanClearance):0.0000} мм";

        }

        private static string FormatDevWithSize(double dev, double nominal, bool includeSize)
        {
            string devStr = FormatDev(dev);
            if (!includeSize) return devStr;

            double fullSize = nominal + dev;
            return $"{devStr} (Ø{fullSize:0.0000})";
        }

        private static string FormatDev(double val)
        {
            if (Math.Abs(val) < 0.00001) return "0.0000";
            return val > 0 ? $"+{val:0.0000}" : $"{val:0.0000}";
        }
    }
}
