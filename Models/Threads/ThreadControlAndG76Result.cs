using System;
using System.Globalization;

namespace MechCore.Models.Threads
{
    public class ThreadControlAndG76Result
    {
        // Блок 1: Контроль среднего диаметра (d2 / D2)
        public string PitchDiameterRangeText { get; }
        public double PitchDiameterMean { get; }
        public string ControlNote { get; }

        // Блок 2: Расчёт подвода, перебега и кадра Fanuc G76
        public double LeadInDistance { get; }       // Подвод на входе (мм)
        public double ChamferOutDistance { get; }   // Перебег / выход на конце (мм)
        public double StartZ { get; }               // Координата подхода Z+ (мм)
        public double EndZ { get; }                 // Координата конца резьбы Z- (мм)
        public string G76CodeBlock { get; }         // Готовый G-код для стойки Fanuc

        // Блок 3: Данные для визуализации траектории резца (схема цикла G76)
        public bool IsInternal { get; }
        public bool IsExternal => !IsInternal;
        public bool HasReliefGroove { get; }
        public bool HasOvertravel { get; }

        public string StartZBadgeText { get; }      // Например: "Z+2.00"
        public string ThreadStartBadgeText { get; }// Например: "Z0"
        public string ThreadLengthBadgeText { get; }// Например: "L = 25 мм"
        public string OvertravelBadgeText { get; }  // Например: "Перебег 2 мм"
        public string EndZBadgeText { get; }

        public ThreadControlAndG76Result(
            ThreadSpecItem spec,
            ThreadOperatorResult opResult,
            bool isInternal,
            double startZCoord,
            double leadInMm,
            double overtravelMm,
            double threadLength,
            bool hasReliefGroove)
        {
            double p = spec.Pitch;
            double d2 = opResult.PitchDiameter;

            // 1. Поле допуска среднего диаметра (ориентир по 6g для вала и 6H для гайки)
            double tolD2 = Math.Round(0.065 * p + 0.045, 3);
            double esAllowance = isInternal ? 0.0 : Math.Round(0.015 + 0.011 * p, 3);

            double d2Min = isInternal ? d2 : Math.Round(d2 - esAllowance - tolD2, 3);
            double d2Max = isInternal ? Math.Round(d2 + tolD2, 3) : Math.Round(d2 - esAllowance, 3);
            PitchDiameterMean = Math.Round((d2Min + d2Max) / 2.0, 3);

            PitchDiameterRangeText = $"{d2Min:0.000} – {d2Max:0.000} мм";
            ControlNote = isInternal
                ? $"Настроечная середина для калибра-пробки или нутромера: {PitchDiameterMean:0.000} мм (база D₂ = {d2:0.000} мм)."
                : $"Настроечная середина для резьбового микрометра: {PitchDiameterMean:0.000} мм (база d₂ = {d2:0.000} мм).";

            // 2. Явные значения подвода и перебега в миллиметрах
            LeadInDistance = Math.Round(leadInMm, 2);
            ChamferOutDistance = hasReliefGroove ? 0.0 : Math.Round(overtravelMm, 2);

            StartZ = Math.Round(startZCoord + LeadInDistance, 2);
            EndZ = Math.Round(startZCoord - threadLength - ChamferOutDistance, 2);

            // Автоматический перевод перебега из мм в десятые доли шага (00..99) для параметра P стойки Fanuc
            int chamferUnits = 0;
            if (!hasReliefGroove && p > 0)
            {
                chamferUnits = (int)Math.Clamp(Math.Round((overtravelMm / p) * 10.0), 0, 99);
            }

            int angle = spec.Type switch
            {
                ThreadType.PipeG or ThreadType.TaperedR => 55,
                ThreadType.Trapezoidal => 30,
                ThreadType.Buttress => 30,
                _ => 60
            };

            string pWord = $"P02{chamferUnits:00}{angle:00}";
            int h1Microns = (int)Math.Round(opResult.ThreadHeightH1 * 1000.0);
            int firstPassMicrons = (int)Math.Round(opResult.FirstPassDepth * 1000.0);

            double finalX = isInternal
                ? Math.Round(spec.NominalDiameter + 0.02, 3)
                : Math.Round(spec.NominalDiameter - 2.0 * opResult.ThreadHeightH1, 3);

            double safeX = isInternal
                ? Math.Round(spec.MinorDiameter - 2.0, 1)
                : Math.Round(spec.NominalDiameter + 3.0, 1);

            G76CodeBlock = string.Format(
                CultureInfo.InvariantCulture,
                "G00 X{0:0.0} Z{1:0.00}\n" +
                "G76 {2} Q50 R0.02\n" +
                "G76 X{3:0.000} Z{4:0.00} P{5} Q{6} F{7:0.###}",
                safeX,
                StartZ,
                pWord,
                finalX,
                EndZ,
                h1Microns,
                firstPassMicrons,
                p);

            IsInternal = isInternal;
            HasReliefGroove = hasReliefGroove;
            HasOvertravel = overtravelMm > 0.001;

            StartZBadgeText = $"Z{StartZ:+0.00;-0.00;0.00}";
            ThreadStartBadgeText = $"Z{startZCoord:+0.##;-0.##;0}";
            ThreadLengthBadgeText = $"L = {threadLength:0.##} мм";
            OvertravelBadgeText = $"+{overtravelMm:0.##} мм";
            EndZBadgeText = $"Z{EndZ:+0.00;-0.00;0.00}";

        }
    }
}
