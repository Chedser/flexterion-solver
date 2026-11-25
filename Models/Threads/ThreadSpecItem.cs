using System;

namespace MechCore.Models.Threads
{
    public class ThreadSpecItem
    {
        public ThreadType Type { get; }
        public string SizeDesignation { get; }  // Например: "M16", "G 1/2\"", "Tr 24"
        public double NominalDiameter { get; }  // Базовый наружный диаметр в мм
        public double MinorDiameter
        {
            get
            {
                double minor = Type switch
                {
                    ThreadType.Metric => NominalDiameter - 1.082532 * Pitch,
                    ThreadType.PipeG or ThreadType.TaperedR => NominalDiameter - 1.280654 * Pitch,
                    ThreadType.TaperedNpt => NominalDiameter - 1.6 * Pitch,
                    ThreadType.Trapezoidal => NominalDiameter - Pitch,
                    ThreadType.Buttress => NominalDiameter - 1.5 * Pitch,
                    _ => NominalDiameter - 1.082532 * Pitch
                };
                return Math.Round(minor, 3);
            }
        }

        public double Pitch { get; }            // Шаг P в мм
        public double? Tpi { get; }             // Число ниток на дюйм (для дюймовых G, R, NPT)
        public string PitchSeries { get; }      // "основной", "мелкий", "крупный" или ""
        public bool IsCoarse => PitchSeries == "основной";

        public string FullDesignation => Tpi.HasValue
            ? $"{SizeDesignation} ({Tpi:0.##} ниток/дюйм, P={Pitch:0.###} мм)"
            : string.IsNullOrEmpty(PitchSeries)
                ? $"{SizeDesignation} × {Pitch:0.###}"
                : $"{SizeDesignation} × {Pitch:0.###} ({PitchSeries})";

        public string PitchDisplay => Tpi.HasValue
            ? $"{Pitch:0.###} мм ({Tpi:0.##} TPI)"
            : $"{Pitch:0.###} мм";

        public ThreadSpecItem(
            ThreadType type,
            string sizeDesignation,
            double nominalDiameter,
            double pitch,
            string pitchSeries = "основной",
            double? tpi = null)
        {
            Type = type;
            SizeDesignation = sizeDesignation;
            NominalDiameter = nominalDiameter;
            Pitch = Math.Round(pitch, 4);
            PitchSeries = pitchSeries;
            Tpi = tpi;
        }
    }
}
