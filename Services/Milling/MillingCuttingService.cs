using System;
using System.Collections.Generic;
using System.Globalization;
using MechCore.Models.Materials;
using MechCore.Models.Milling;

namespace MechCore.Services.Milling
{
    public class MillingCuttingService : IMillingCuttingService
    {
        public MillingCuttingResult Calculate(MillingCuttingRequest request)
        {
            if (request.IsoGroup == null)
                return ErrorCutting("Выберите группу обрабатываемости материала по ISO 513.");

            if (request.CutterDiameter < 0.5 || request.CutterDiameter > 500.0)
                return ErrorCutting("Диаметр фрезы или сверла D должен быть в пределах от 0,5 до 500 мм.");

            if (request.FluteCount < 1 || request.FluteCount > 32)
                return ErrorCutting("Число зубьев z должно быть в пределах от 1 до 32.");

            if (request.OverhangRatio < 0.5 || request.OverhangRatio > 20.0)
                return ErrorCutting("Относительный вылет инструмента L/D должен быть в пределах от 0,5 до 20.");

            var warnings = new List<string>();
            double d = request.CutterDiameter;
            int z = request.FluteCount;

            // 1. Базовые нормативы Vc (м/мин) и доли съёма ap/D, ae/D для эталонной стали ISO P (Сталь 45)
            var (baseVcMin, baseVcMax, apRatio, aeRatio) =
                GetBaseVcAndEngagementForSteelP(request.Operation, request.Stage);

            // 2. Коэффициенты обрабатываемости группы ISO и марки стали
            double km = request.IsoGroup.MachinabilityFactor;
            double gradeFactor = GetGradeSpecificFactor(request.SelectedGrade);
            double totalMatFactor = km * gradeFactor;

            // 3. Поправочные коэффициенты по материалу режущей части фрезы
            var (toolVcFactor, toolFzFactor, toolWarning) = GetToolMaterialFactors(
                request.ToolMaterial, request.IsoGroup.Code, request.Operation, request.Stage);

            if (!string.IsNullOrEmpty(toolWarning))
                warnings.Add(toolWarning);

            // 4. Поправка на жёсткость (вылет оправки L/D)
            var (rigidityVcFactor, rigidityFzFactor, rigidityDepthFactor, rigidityWarning) =
                GetOverhangFactors(request.Operation, request.OverhangRatio);

            if (!string.IsNullOrEmpty(rigidityWarning))
                warnings.Add(rigidityWarning);

            // 5. Скорость резания Vc (м/мин)
            double vcMin = Math.Round(Math.Max(5.0, baseVcMin * totalMatFactor * toolVcFactor * rigidityVcFactor), 0);
            double vcMax = Math.Round(Math.Max(vcMin + 3.0, baseVcMax * totalMatFactor * toolVcFactor * rigidityVcFactor), 0);
            double vcRec = Math.Round((vcMin + vcMax) / 2.0, 0);

            // 6. Подача на зуб fz (мм/зуб) с учётом диаметра фрезы D, операции, этапа и материала
            var (baseFzMin, baseFzMax) = GetBaseFeedPerTooth(request.Operation, request.Stage, d);
            double matFzFactor = GetMaterialFeedFactor(request.IsoGroup.Code, request.SelectedGrade);

            double fzMin = Math.Round(Math.Max(0.005, baseFzMin * matFzFactor * toolFzFactor * rigidityFzFactor), 3);
            double fzMax = Math.Round(Math.Max(fzMin, baseFzMax * matFzFactor * toolFzFactor * rigidityFzFactor), 3);
            double fzRec = Math.Round((fzMin + fzMax) / 2.0, 3);

            // 7. Обороты шпинделя n = (1000 * Vc) / (pi * D)
            int rpmRec = (int)Math.Round((1000.0 * vcRec) / (Math.PI * d));
            rpmRec = Math.Clamp(rpmRec, 30, 30000);

            if (rpmRec > 15000)
            {
                warnings.Add($"Высокая расчётная частота вращения ({rpmRec} об/мин) для фрезы Ø{d:0.##} мм: убедитесь, что шпиндель станка и балансировка оправки (G2.5) рассчитаны на такие обороты.");
            }

            // 8. Подача на оборот fn = fz * z и минутная подача Vf = fn * n
            double fnRec = Math.Round(fzRec * z, 3);
            double vfRec = Math.Round(fnRec * rpmRec, 0);

            // 9. Рекомендуемые осевая глубина ap и радиальная ширина ae
            double apRec = Math.Round(Math.Max(0.1, d * apRatio * rigidityDepthFactor), 2);
            double aeRec = request.Operation == MillingOperationKind.Drilling
                ? d
                : Math.Round(Math.Max(0.1, d * aeRatio * (request.Operation == MillingOperationKind.SlotMilling ? 1.0 : rigidityDepthFactor)), 2);

            if (request.Operation == MillingOperationKind.SlotMilling && z > 3 && request.IsoGroup.Code == IsoGroupCode.N)
            {
                warnings.Add("Для пазования алюминия (ISO N) на полную ширину лучше использовать 2- или 3-зубые фрезы с полированной канавкой во избежание пакетирования стружки.");
            }

            var inv = CultureInfo.InvariantCulture;
            string vfStr = vfRec.ToString("0", inv);

            var result = new MillingCuttingResult
            {
                IsValid = true,
                VcMin = vcMin,
                VcMax = vcMax,
                RecommendedVc = vcRec,
                RecommendedRpm = rpmRec,
                FeedPerToothMin = fzMin,
                FeedPerToothMax = fzMax,
                RecommendedFeedPerTooth = fzRec,
                RecommendedFeedPerRev = fnRec,
                RecommendedFeedPerMin = vfRec,
                RecommendedAp = apRec,
                RecommendedAe = aeRec,

                VcRangeText = string.Format(inv, "{0:0} – {1:0} м/мин", vcMin, vcMax),
                RecommendedVcText = string.Format(inv, "{0:0} м/мин", vcRec),
                SpindleRpmText = string.Format(inv, "{0} об/мин", rpmRec),
                FeedPerRevText = string.Format(inv, "fn = {0:0.###} мм/об", fnRec),
                FeedPerToothRangeText = string.Format(inv, "{0:0.000} – {1:0.000} мм/зуб", fzMin, fzMax),
                RecommendedFeedPerToothText = string.Format(inv, "{0:0.000} мм/зуб", fzRec),
                FeedPerMinText = string.Format(inv, "Vf = {0:0} мм/мин", vfRec),
                DepthApAeText = request.Operation == MillingOperationKind.Drilling
                    ? string.Format(inv, "Ø {0:0.##} мм", d)
                    : string.Format(inv, "ap {0:0.##} × ae {1:0.##} мм", apRec, aeRec),
                DepthShareNoteText = request.Operation == MillingOperationKind.Drilling
                    ? "Осевое сверление (z = " + z + ")"
                    : string.Format(inv, "ap = {0:0.##}D, ae = {1:0}% D", apRatio * rigidityDepthFactor, Math.Round((aeRec / d) * 100.0, 0)),

                StrategyAdvice = BuildStrategyAdvice(request),
                CoolantAdvice = request.IsoGroup.CoolantAdvice,
                SampleGCode = $"S{rpmRec} M03{Environment.NewLine}G94 F{vfStr}{Environment.NewLine}M08",

                HasWarning = warnings.Count > 0,
                WarningMessage = string.Join(Environment.NewLine, warnings)
            };

            return result;
        }

        public MillingFeedCalcResult CalculateFeed(MillingFeedCalcRequest request)
        {
            if (request.CutterDiameter < 0.5 || request.CutterDiameter > 500.0)
                return ErrorFeed("Диаметр фрезы Dc должен быть в пределах от 0,5 до 500 мм.");

            if (request.FluteCount < 1 || request.FluteCount > 32)
                return ErrorFeed("Число зубьев z должно быть в пределах от 1 до 32.");

            if (request.AxialDepthAp <= 0 || request.AxialDepthAp > 300.0)
                return ErrorFeed("Осевая глубина резания ap должна быть больше 0 мм.");

            if (request.RadialWidthAe <= 0 || request.RadialWidthAe > request.CutterDiameter)
                return ErrorFeed($"Радиальная ширина фрезерования ae должна быть больше 0 и не превышать диаметр фрезы Dc ({request.CutterDiameter:0.##} мм).");

            if (request.CutterGeometry == MillingCutterGeometryKind.RoundInsert)
            {
                if (request.InsertDiameterIc < 3.0 || request.InsertDiameterIc > request.CutterDiameter)
                    return ErrorFeed($"Диаметр круглой пластины iC должен быть от 3 мм и не превышать диаметр фрезы Dc ({request.CutterDiameter:0.##} мм).");

                if (request.AxialDepthAp > request.InsertDiameterIc * 0.5)
                    return ErrorFeed($"Для фрезы с круглыми пластинами глубина резания ap не должна превышать половину диаметра пластины iC/2 ({request.InsertDiameterIc * 0.5:0.##} мм).");
            }

            if (request.MachineSpindlePowerKw <= 0.1 || request.MachineSpindlePowerKw > 200.0)
                return ErrorFeed("Паспортная мощность шпинделя станка должна быть в пределах от 0,1 до 200 кВт.");

            if (request.MachineEfficiency < 0.5 || request.MachineEfficiency > 1.0)
                return ErrorFeed("КПД привода шпинделя η должен быть в пределах от 0,50 до 1,00.");

            var warnings = new List<string>();
            var inv = CultureInfo.InvariantCulture;

            double dc = request.CutterDiameter;
            int z = request.FluteCount;
            double ap = request.AxialDepthAp;
            double ae = request.RadialWidthAe;

            // 1. Расчёт эффективного диаметра Deff и осевого коэффициента утонения (для сферы и круглых пластин)
            double deff = dc;
            double axialThinningFactor = 1.0;

            if (request.CutterGeometry == MillingCutterGeometryKind.BallNose)
            {
                double radius = dc / 2.0;
                if (ap < radius)
                {
                    double chord = 2.0 * Math.Sqrt(ap * (dc - ap));
                    deff = Math.Max(0.2, chord);
                    axialThinningFactor = dc / deff;
                }
            }
            else if (request.CutterGeometry == MillingCutterGeometryKind.RoundInsert)
            {
                double ic = request.InsertDiameterIc;
                double insertRadius = ic / 2.0;
                if (ap < insertRadius)
                {
                    double chord = 2.0 * Math.Sqrt(ap * (ic - ap));
                    deff = Math.Max(0.5, dc - ic + chord);
                    if (chord > 0.2)
                    {
                        axialThinningFactor = ic / chord;
                    }
                }
            }

            // 2. Радиальное утонение стружки при ae < 0.5 * Dc
            double aeRatio = ae / dc;
            double engagementPct = Math.Round(aeRatio * 100.0, 1);

            double radialThinningFactor = 1.0;
            if (aeRatio < 0.5)
            {
                double geomRatio = 2.0 * Math.Sqrt(aeRatio * (1.0 - aeRatio));
                if (geomRatio > 0.1)
                {
                    radialThinningFactor = Math.Clamp(1.0 / geomRatio, 1.0, 2.5);
                }
            }

            // Суммарный коэффициент утонения стружки (ограничиваем сверху 3.5x для защиты кромки)
            double totalThinningFactor = Math.Clamp(radialThinningFactor * axialThinningFactor, 1.0, 3.5);

            int rpm;
            double vc;
            double baseFz;
            double adjustedFz;
            double hm;
            double vf;

            if (request.Mode == MillingFeedCalcMode.ByCatalogVcAndFz)
            {
                if (request.InputVc <= 0 || request.InputVc > 3000.0)
                    return ErrorFeed("Скорость резания Vc должна быть больше 0 м/мин.");

                if (request.InputFz <= 0 || request.InputFz > 5.0)
                    return ErrorFeed("Подача на зуб fz должна быть больше 0 мм/зуб.");

                vc = request.InputVc;
                // Обороты считаем по эффективному диаметру Deff
                rpm = (int)Math.Round((1000.0 * vc) / (Math.PI * deff));
                rpm = Math.Clamp(rpm, 10, 40000);

                baseFz = request.InputFz;
                double appliedFactor = request.CompensateChipThinning ? totalThinningFactor : 1.0;
                adjustedFz = Math.Round(baseFz * appliedFactor, 4);

                hm = Math.Round(adjustedFz / totalThinningFactor, 4);
                vf = Math.Round(adjustedFz * z * rpm, 0);
            }
            else
            {
                if (request.InputRpm < 10 || request.InputRpm > 40000)
                    return ErrorFeed("Частота вращения шпинделя S должна быть в пределах от 10 до 40000 об/мин.");

                if (request.InputVf <= 0 || request.InputVf > 50000.0)
                    return ErrorFeed("Минутная подача Vf должна быть больше 0 мм/мин.");

                rpm = request.InputRpm;
                vf = request.InputVf;
                vc = Math.Round((Math.PI * deff * rpm) / 1000.0, 1);

                adjustedFz = Math.Round(vf / (z * rpm), 4);
                hm = Math.Round(adjustedFz / totalThinningFactor, 4);
                baseFz = hm;
            }

            // Объёмный съём металла Q (см³/мин)
            double mrrQ = Math.Round((ap * ae * vf) / 1000.0, 2);

            // Мощность резания на шпинделе P_cut (кВт) и потребляемая мощность двигателя P_mot (кВт) с учётом КПД η
            double kc = request.SpecificCuttingForceKc > 100 ? request.SpecificCuttingForceKc : 2100.0;
            double eta = request.MachineEfficiency;
            double cuttingPowerKw = (ap * ae * vf * kc) / 60_000_000.0;
            double motorPowerKw = Math.Round(cuttingPowerKw / eta, 2);

            // Крутящий момент на шпинделе Mкр (Н·м) = 9550 * P_cut / n
            double torqueNm = rpm > 0 ? Math.Round((9550.0 * cuttingPowerKw) / rpm, 1) : 0.0;

            // Загрузка шпинделя (%) от паспортной мощности станка
            double loadPercent = Math.Round((motorPowerKw / request.MachineSpindlePowerKw) * 100.0, 0);
            bool isOverloaded = loadPercent > 100.0;

            // Технологические предупреждения
            if (isOverloaded)
            {
                warnings.Add(string.Format(inv,
                    "ПЕРЕГРУЗ ШПИНДЕЛЯ ({0:0}%): требуемая мощность ({1:0.##} кВт) превышает паспортную мощность станка ({2:0.##} кВт)! Шпиндель может заглохнуть — уменьшите глубину ap, ширину ae или подачу Vf.",
                    loadPercent, motorPowerKw, request.MachineSpindlePowerKw));
            }
            else if (loadPercent >= 85.0)
            {
                warnings.Add(string.Format(inv,
                    "Высокая загрузка шпинделя ({0:0}%, {1:0.##} из {2:0.##} кВт): работа на пределе мощности двигателя — следите за нагрузкой на стойке ЧПУ и износом пластин.",
                    loadPercent, motorPowerKw, request.MachineSpindlePowerKw));
            }

            if (request.CutterGeometry != MillingCutterGeometryKind.FlatEndMill && deff < dc * 0.98)
            {
                warnings.Add(string.Format(inv,
                    "Эффективный диаметр резания Deff = {0:0.##} мм (меньше номинального Dc = {1:0.##} мм): обороты шпинделя автоматически пересчитаны по Deff для поддержания заданной скорости Vc.",
                    deff, dc));
            }

            if (rpm >= 24000)
            {
                warnings.Add($"Высокие расчётные обороты ({rpm} об/мин) из-за малого эффективного диаметра Deff = {deff:0.##} мм: проверьте максимальные обороты шпинделя станка.");
            }

            if (hm < 0.012)
            {
                warnings.Add(string.Format(inv,
                    "Слишком тонкая стружка (hm = {0:0.000} мм): режущая кромка будет скоблить и наклёпывать поверхность вместо резания. Увеличьте fz или включите компенсацию утонения.",
                    hm));
            }

            if (request.ReferenceVcMax.HasValue && vc > request.ReferenceVcMax.Value * 1.05)
            {
                string matName = string.IsNullOrWhiteSpace(request.MaterialContextLabel)
                    ? "выбранного материала"
                    : request.MaterialContextLabel;

                warnings.Add(string.Format(inv,
                    "Скорость резания Vc ({0:0.#} м/мин) выше рекомендуемого диапазона для {1} ({2:0}–{3:0} м/мин) — риск быстрого износа зубьев фрезы.",
                    vc, matName, request.ReferenceVcMin ?? 0, request.ReferenceVcMax.Value));
            }

            string baseNote = totalThinningFactor > 1.01 && request.Mode == MillingFeedCalcMode.ByCatalogVcAndFz && request.CompensateChipThinning
                ? string.Format(inv, "Поправка утонения ×{0:0.00} (база {1:0.###})", totalThinningFactor, baseFz)
                : string.Format(inv, "Число зубьев: z = {0}", z);

            string deffNote = request.CutterGeometry != MillingCutterGeometryKind.FlatEndMill
                ? string.Format(inv, "Deff = {0:0.##} мм (Vc {1:0.#} м/мин)", deff, vc)
                : string.Format(inv, "Vc: {0:0.#} м/мин (Deff = Dc)", vc);

            string vfStr = vf.ToString("0", inv);

            return new MillingFeedCalcResult
            {
                IsValid = true,
                CalculatedRpm = rpm,
                CalculatedVc = vc,
                EffectiveDiameterDeff = Math.Round(deff, 2),
                BaseFeedPerTooth = baseFz,
                AdjustedFeedPerTooth = adjustedFz,
                AverageChipThicknessHm = hm,
                ChipThinningFactor = Math.Round(totalThinningFactor, 2),
                CalculatedFeedPerMin = vf,
                MetalRemovalRateQ = mrrQ,
                EstimatedPowerKw = motorPowerKw,
                SpindleTorqueNm = torqueNm,
                SpindleLoadPercent = loadPercent,
                IsSpindleOverloaded = isOverloaded,
                SpindleRpmText = string.Format(inv, "{0} об/мин", rpm),
                CuttingSpeedVcText = deffNote,
                EffectiveDiameterText = string.Format(inv, "{0:0.##} мм", deff),
                FeedPerMinText = string.Format(inv, "{0:0} мм/мин", vf),
                BaseFeedNoteText = baseNote,
                FeedPerToothText = string.Format(inv, "{0:0.000} мм/зуб", adjustedFz),
                ChipThicknessText = string.Format(inv, "Стружка hm: {0:0.000} мм", hm),
                RemovalAndPowerText = string.Format(inv, "{0:0.##} см³/мин • {1:0.##} кВт", mrrQ, motorPowerKw),
                SpindleLoadAndTorqueText = string.Format(inv, "Загрузка: {0:0}% • Mкр = {1:0.#} Н·м", loadPercent, torqueNm),
                EngagementPercentText = string.Format(inv, "Охват ae: {0:0.#}% Dc • kc = {1:0} МПа", engagementPct, kc),
                SampleGCode = $"S{rpm} M03{Environment.NewLine}G94 F{vfStr}",
                HasWarning = warnings.Count > 0,
                WarningMessage = string.Join(Environment.NewLine, warnings)
            };
        }

        private static MillingCuttingResult ErrorCutting(string message) =>
        new() { IsValid = false, HasError = true, ErrorMessage = message };
        private static MillingFeedCalcResult ErrorFeed(string message) =>
        new() { IsValid = false, HasError = true, ErrorMessage = message };
        private static (double vcMin, double vcMax, double apRatio, double aeRatio)
        GetBaseVcAndEngagementForSteelP(MillingOperationKind op, MillingStageKind stage)
        {
            return op switch
            {
                MillingOperationKind.FaceMilling => stage switch
                {
                    MillingStageKind.Roughing => (180, 240, 0.08, 0.75),
                    MillingStageKind.SemiFinishing => (220, 280, 0.03, 0.75),
                    MillingStageKind.Finishing => (260, 340, 0.01, 0.80),
                    _ => (200, 260, 0.05, 0.75)
                },
                MillingOperationKind.ShoulderMilling => stage switch
                {
                    MillingStageKind.Roughing => (150, 200, 1.00, 0.35),
                    MillingStageKind.SemiFinishing => (180, 235, 1.00, 0.15),
                    MillingStageKind.Finishing => (210, 270, 1.20, 0.05),
                    _ => (170, 220, 1.00, 0.25)
                },
                MillingOperationKind.SlotMilling => stage switch
                {
                    MillingStageKind.Roughing => (120, 165, 0.60, 1.00),
                    MillingStageKind.SemiFinishing => (145, 190, 0.40, 1.00),
                    MillingStageKind.Finishing => (165, 220, 0.20, 1.00),
                    _ => (140, 185, 0.50, 1.00)
                },
                MillingOperationKind.PocketAndHsm => stage switch
                {
                    MillingStageKind.Roughing => (210, 280, 1.50, 0.12),
                    MillingStageKind.SemiFinishing => (230, 300, 1.50, 0.08),
                    MillingStageKind.Finishing => (250, 320, 1.50, 0.04),
                    _ => (220, 290, 1.50, 0.10)
                },
                MillingOperationKind.Drilling => stage switch
                {
                    MillingStageKind.Roughing => (75, 110, 0.50, 1.00),
                    MillingStageKind.SemiFinishing => (85, 120, 0.50, 1.00),
                    MillingStageKind.Finishing => (95, 130, 0.50, 1.00),
                    _ => (80, 115, 0.50, 1.00)
                },
                MillingOperationKind.Chamfering => stage switch
                {
                    MillingStageKind.Roughing => (140, 190, 0.15, 0.25),
                    MillingStageKind.SemiFinishing => (160, 210, 0.10, 0.20),
                    MillingStageKind.Finishing => (180, 240, 0.05, 0.15),
                    _ => (160, 210, 0.10, 0.20)
                },
                _ => (160, 210, 0.80, 0.30)
            };
        }
        private static (double fzMin, double fzMax) GetBaseFeedPerTooth(
        MillingOperationKind op, MillingStageKind stage, double d)
        {
            // Базовая подача на зуб масштабируется от диаметра фрезы D
            double diamFactor = d switch
            {
                <= 4.0 => 0.35,
                <= 6.0 => 0.55,
                <= 10.0 => 0.80,
                <= 16.0 => 1.00,
                <= 25.0 => 1.25,
                <= 50.0 => 1.60,
                _ => 2.00
            };
            var (refMin, refMax) = op switch
            {
                MillingOperationKind.FaceMilling => stage switch
                {
                    MillingStageKind.Roughing => (0.10, 0.18),
                    MillingStageKind.SemiFinishing => (0.07, 0.12),
                    MillingStageKind.Finishing => (0.04, 0.08),
                    _ => (0.08, 0.14)
                },
                MillingOperationKind.SlotMilling => stage switch
                {
                    MillingStageKind.Roughing => (0.035, 0.060),
                    MillingStageKind.SemiFinishing => (0.030, 0.050),
                    MillingStageKind.Finishing => (0.020, 0.038),
                    _ => (0.035, 0.055)
                },
                MillingOperationKind.PocketAndHsm => stage switch
                {
                    MillingStageKind.Roughing => (0.065, 0.110),
                    MillingStageKind.SemiFinishing => (0.050, 0.085),
                    MillingStageKind.Finishing => (0.030, 0.055),
                    _ => (0.055, 0.095)
                },
                MillingOperationKind.Drilling => stage switch
                {
                    MillingStageKind.Roughing => (0.060, 0.100),
                    MillingStageKind.SemiFinishing => (0.050, 0.085),
                    MillingStageKind.Finishing => (0.040, 0.070),
                    _ => (0.055, 0.090)
                },
                _ => stage switch // ShoulderMilling / Chamfering
                {
                    MillingStageKind.Roughing => (0.050, 0.085),
                    MillingStageKind.SemiFinishing => (0.040, 0.065),
                    MillingStageKind.Finishing => (0.025, 0.045),
                    _ => (0.045, 0.075)
                }
            };
            return (refMin * diamFactor, refMax * diamFactor);
        }
        private static double GetMaterialFeedFactor(IsoGroupCode iso, MaterialGradeItem? grade)
        {
            double baseIso = iso switch
            {
                IsoGroupCode.P => 1.00,
                IsoGroupCode.M => 0.78,
                IsoGroupCode.K => 1.15,
                IsoGroupCode.N => 1.30,
                IsoGroupCode.S => 0.60,
                IsoGroupCode.H => 0.65,
                _ => 1.00
            };
            if (grade == null) return baseIso;
            double gradeShift = grade.GostGrade switch
            {
                "Ст3сп" or "Сталь 20" or "ЛС59-1" => 1.08,
                "40Х" or "30ХГСА" => 0.94,
                "40ХН2МА" or "08Х17Н13М2" => 0.88,
                "ВТ6" or "ХН77ТЮР" => 0.82,
                _ => 1.00
            };
            return baseIso * gradeShift;
        }
        private static double GetGradeSpecificFactor(MaterialGradeItem? grade)
        {
            if (grade == null) return 1.0;
            return grade.GostGrade switch
            {
                "Ст3сп" or "Сталь 20" => 1.12,
                "40Х" => 0.92,
                "30ХГСА" => 0.85,
                "40ХН2МА" => 0.80,
                "У8А" => 0.88,
                "12Х18Н10Т" or "08Х18Н10" => 0.95,
                "08Х17Н13М2" => 0.85,
                "ВЧ50" => 0.90,
                "ЛС59-1" => 1.25,
                "ВТ6" => 0.75,
                "ХН77ТЮР" => 0.65,
                _ => 1.0
            };
        }
        private static (double vcFactor, double fzFactor, string warning) GetToolMaterialFactors(
        MillingToolMaterialKind tool, IsoGroupCode iso, MillingOperationKind op, MillingStageKind stage)
        {
            return tool switch
            {
                MillingToolMaterialKind.SolidCarbide => iso == IsoGroupCode.H
                ? (0.75, 0.70, "Для закалённой стали (ISO H) используйте цельные фрезы с покрытием AlTiN / TiSiN и малым радиальным съёмом ae.")
                : (1.00, 1.00, string.Empty),
                MillingToolMaterialKind.IndexableCarbide => (0.88, 1.15,
                "Корпусная фреза со СМП позволяет повысить подачу на зуб fz, но требует жёсткой оправки и шпинделя."),
                MillingToolMaterialKind.UncoatedCarbide => (0.80, 0.95,
                iso == IsoGroupCode.N
                ? string.Empty
                : "Твёрдый сплав без покрытия не имеет термобарьерного слоя — скорость резания снижена на 20%."),
                MillingToolMaterialKind.HssCo => (0.32, 0.75,
                "Быстрорежущая сталь (HSS-Co / Р6М5К5) требует низких скоростей резания и обильного охлаждения СОЖ."),
                _ => (1.0, 1.0, string.Empty)
            };
        }
        private static (double vcFactor, double fzFactor, double depthFactor, string warning) GetOverhangFactors(
        MillingOperationKind op, double ld)
        {
            if (ld <= 3.0)
                return (1.0, 1.0, 1.0, string.Empty);
            if (ld <= 5.0)
            {
                string msg = op == MillingOperationKind.Drilling
                ? $"Глубина сверления L/D = {ld:0.#}: рекомендуется цикл глубокого сверления с выводом стружки (G83)."
                : $"Повышенный вылет фрезы (L/D = {ld:0.#}): скорость и глубина снижены на 15% для подавления вибраций.";
                return (0.85, 0.85, 0.80, msg);
            }
            string highWarn = op == MillingOperationKind.Drilling
            ? $"Глубокое сверление (L/D = {ld:0.#}): обязателен прерывистый цикл G83 и подача СОЖ под давлением!"
            : $"Критический вылет фрезы (L/D = {ld:0.#}): высокий риск дробления и поломки! Уменьшите радиальный съём ae и используйте цанговый / термопатрон.";
            return (0.65, 0.70, 0.55, highWarn);
        }
        private static string BuildStrategyAdvice(MillingCuttingRequest request)
        {
            return request.Operation switch
            {
                MillingOperationKind.FaceMilling =>
                "Располагайте фрезу со смещением от центра заготовки (попутное фрезерование) и используйте плавный радиальный вход в металл.",
                MillingOperationKind.ShoulderMilling =>
                "Попутное фрезерование (G41 при обходе снаружи) обеспечивает лучшую чистоту стенки и меньший износ зубьев, чем встречное.",
                MillingOperationKind.SlotMilling =>
                "Избегайте вертикального врезания в сплошной металл: заходите по спирали (хеликс) или под углом (рампинг 2–5°).",
                MillingOperationKind.PocketAndHsm =>
                "Трохоидальная / высокоскоростная стратегия: большая осевая глубина ap при малом радиальном шаге ae (8–15% D) равномерно нагружает всю режущую кромку.",
                MillingOperationKind.Drilling =>
                "Для спиральных свёрл обязательна предварительная центровка; твердосплавные свёрла с внутренним подводом СОЖ работают без расфрезеровки.",
                MillingOperationKind.Chamfering =>
                "Задавайте смещение по глубине так, чтобы резание велось средней частью конуса фрезы, а не самым кончиком.",
                _ => request.IsoGroup.ToolingTip
            };
        }
    }
}