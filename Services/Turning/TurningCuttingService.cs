using System;
using System.Collections.Generic;
using System.Globalization;
using MechCore.Models.Materials;
using MechCore.Models.Turning;

namespace MechCore.Services.Turning
{
    public class TurningCuttingService : ITurningCuttingService
    {

        public TurningCuttingResult QuickRecommend(
            IsoMaterialGroupItem isoGroup,
            MaterialGradeItem? grade,
            TurningOperationKind operation,
            CuttingStageKind stage,
            double diameter,
            double toolOrGrooveWidth = 3.0,
            double overhangRatio = 3.0,
            ToolMaterialKind toolMaterial = ToolMaterialKind.CoatedCarbide)
        {
            var request = new TurningCuttingRequest
            {
                IsoGroup = isoGroup,
                SelectedGrade = grade,
                Operation = operation,
                Stage = stage,
                ToolMaterial = toolMaterial,
                Diameter = diameter,
                OverhangRatio = overhangRatio,
                GrooveWidth = toolOrGrooveWidth
            };

            return Calculate(request);
        }

        public TurningCuttingResult Calculate(TurningCuttingRequest request)
        {
            if (request.IsoGroup == null)
                return ErrorCutting("Выберите группу обрабатываемости материала по ISO.");

            if (request.Diameter < 0.5 || request.Diameter > 2500.0)
                return ErrorCutting("Диаметр обработки D должен быть в пределах от 0,5 до 2500 мм.");

            if (request.OverhangRatio < 0.5 || request.OverhangRatio > 25.0)
                return ErrorCutting("Относительный вылет L/D должен быть в пределах от 0,5 до 25.");

            if (request.Operation == TurningOperationKind.PartingAndGrooving &&
                (request.GrooveWidth < 0.5 || request.GrooveWidth > 25.0))
            {
                return ErrorCutting("Ширина отрезного/канавочного резца B должна быть в пределах от 0,5 до 25 мм.");
            }

            var warnings = new List<string>();
            double d = request.Diameter;
            double grooveW = request.GrooveWidth;

            // 1. Базовые нормативы для эталонной стали ISO P (Сталь 45) твёрдым сплавом с покрытием
            var (baseVcMin, baseVcMax, baseFnMin, baseFnMax, baseApMin, baseApMax) =
                GetBaseParametersForSteelP(request.Operation, request.Stage, d, grooveW);

            // 2. Коэффициент обрабатываемости группы ISO и конкретной марки стали/сплава
            double km = request.IsoGroup.MachinabilityFactor;
            double gradeFactor = GetGradeSpecificFactor(request.SelectedGrade);
            double totalMatFactor = km * gradeFactor;

            // 3. Поправочные коэффициенты по материалу режущей части
            var (toolVcFactor, toolFnFactor, toolWarning) = GetToolMaterialFactors(
                request.ToolMaterial, request.IsoGroup.Code, request.Operation, request.Stage);

            if (!string.IsNullOrEmpty(toolWarning))
                warnings.Add(toolWarning);

            // 4. Поправка на жёсткость (вылет оправки или глубину сверления L/D)
            var (rigidityVcFactor, rigidityFnFactor, rigidityApFactor, rigidityWarning) =
                GetOverhangFactors(request.Operation, request.OverhangRatio);

            if (!string.IsNullOrEmpty(rigidityWarning))
                warnings.Add(rigidityWarning);

            // 5. Расчёт скорости резания Vc (м/мин)
            double vcMin = Math.Round(Math.Max(4.0, baseVcMin * totalMatFactor * toolVcFactor * rigidityVcFactor), 0);
            double vcMax = Math.Round(Math.Max(vcMin + 2.0, baseVcMax * totalMatFactor * toolVcFactor * rigidityVcFactor), 0);
            double vcRec = Math.Round((vcMin + vcMax) / 2.0, 0);

            // 6. Расчёт оборотной подачи fn (мм/об) с учётом группы ISO и марки материала
            double matFnFactor = GetMaterialFeedFactor(request.IsoGroup.Code, request.SelectedGrade);
            double fnMin = Math.Round(baseFnMin * matFnFactor * toolFnFactor * rigidityFnFactor, 3);
            double fnMax = Math.Round(baseFnMax * matFnFactor * toolFnFactor * rigidityFnFactor, 3);
            if (fnMax < fnMin) fnMax = fnMin;
            double fnRec = Math.Round((fnMin + fnMax) / 2.0, 3);

            // 7. Расчёт глубины резания ap (мм) на сторону
            double apMin = Math.Round(baseApMin * rigidityApFactor, 2);
            double apMax = Math.Round(baseApMax * rigidityApFactor, 2);
            if (apMax < apMin) apMax = apMin;
            double apRec = Math.Round((apMin + apMax) / 2.0, 2);

            // 8. Расчёт частоты вращения шпинделя n = (1000 * Vc) / (pi * D)
            int rpmRec = (int)Math.Round((1000.0 * vcRec) / (Math.PI * d));
            rpmRec = Math.Clamp(rpmRec, 20, 12000);

            // Рекомендуемое ограничение G50 (с запасом ~25%, округлённое вверх до 100)
            int g50Limit = (int)(Math.Ceiling((rpmRec * 1.25) / 100.0) * 100);
            g50Limit = Math.Clamp(g50Limit, 500, 4500);

            if (rpmRec > 3500)
            {
                warnings.Add($"Высокая расчётная частота вращения ({rpmRec} об/мин) для диаметра {d:0.##} мм: проверьте балансировку заготовки и паспортный предел патрона.");
            }

            // 9. Минутная подача Vf = fn * n (мм/мин)
            double vfRec = Math.Round(fnRec * rpmRec, 0);

            var inv = CultureInfo.InvariantCulture;
            var result = new TurningCuttingResult
            {
                IsValid = true,
                VcMin = vcMin,
                VcMax = vcMax,
                RecommendedVc = vcRec,
                RecommendedRpm = rpmRec,
                RecommendedG50 = g50Limit,
                FeedPerRevMin = fnMin,
                FeedPerRevMax = fnMax,
                RecommendedFeedPerRev = fnRec,
                RecommendedFeedPerMin = vfRec,
                DepthOfCutMin = apMin,
                DepthOfCutMax = apMax,
                RecommendedDepthOfCut = apRec,

                VcRangeText = string.Format(inv, "{0:0} – {1:0} м/мин", vcMin, vcMax),
                RecommendedVcText = string.Format(inv, "{0:0} м/мин", vcRec),
                SpindleRpmText = string.Format(inv, "{0} об/мин", rpmRec),
                RecommendedG50Text = string.Format(inv, "G50 S{0}", g50Limit),

                GeometryAdvice = BuildOperationGeometryAdvice(request),
                CoolantAdvice = request.IsoGroup.CoolantAdvice,
                SampleGCode = BuildCleanSampleGCode(request, (int)vcRec, rpmRec, g50Limit, fnRec),

                HasWarning = warnings.Count > 0,
                WarningMessage = string.Join(Environment.NewLine, warnings)
            };

            if (request.Operation == TurningOperationKind.ThreadTurning)
            {
                result.FeedPerRevRangeText = "Равна шагу P";
                result.RecommendedFeedPerRevText = "F = Шаг P (мм/об)";
                result.FeedPerMinText = "Синхронизация со шпинделем";
                result.DepthOfCutRangeText = string.Format(inv, "{0:0.00} – {1:0.00} мм/проход", apMin, apMax);
                result.RecommendedDepthOfCutText = string.Format(inv, "{0:0.00} мм/проход", apRec);
            }
            else if (request.Operation == TurningOperationKind.Drilling)
            {
                result.FeedPerRevRangeText = string.Format(inv, "{0:0.000} – {1:0.000} мм/об", fnMin, fnMax);
                result.RecommendedFeedPerRevText = string.Format(inv, "{0:0.000} мм/об", fnRec);
                result.FeedPerMinText = string.Format(inv, "{0:0} мм/мин", vfRec);
                result.DepthOfCutRangeText = string.Format(inv, "R = {0:0.##} мм", d / 2.0);
                result.RecommendedDepthOfCutText = string.Format(inv, "Ø {0:0.##} мм", d);
            }
            else
            {
                result.FeedPerRevRangeText = string.Format(inv, "{0:0.000} – {1:0.000} мм/об", fnMin, fnMax);
                result.RecommendedFeedPerRevText = string.Format(inv, "{0:0.000} мм/об", fnRec);
                result.FeedPerMinText = string.Format(inv, "{0:0} мм/мин", vfRec);
                result.DepthOfCutRangeText = string.Format(inv, "{0:0.00} – {1:0.00} мм", apMin, apMax);
                result.RecommendedDepthOfCutText = string.Format(inv, "{0:0.00} мм", apRec);
            }

            return result;
        }

        private static TurningCuttingResult ErrorCutting(string message) =>
            new() { IsValid = false, HasError = true, ErrorMessage = message };

        private static (double vcMin, double vcMax, double fnMin, double fnMax, double apMin, double apMax)
            GetBaseParametersForSteelP(TurningOperationKind op, CuttingStageKind stage, double d, double grooveW)
        {
            return op switch
            {
                TurningOperationKind.ExternalTurning => stage switch
                {
                    CuttingStageKind.Roughing => (150, 210, 0.25, 0.45, 1.5, 4.0),
                    CuttingStageKind.SemiFinishing => (190, 250, 0.15, 0.28, 0.5, 1.5),
                    CuttingStageKind.Finishing => (230, 310, 0.06, 0.15, 0.15, 0.5),
                    _ => (180, 240, 0.20, 0.35, 1.0, 2.5)
                },

                TurningOperationKind.InternalBoring => stage switch
                {
                    CuttingStageKind.Roughing => (120, 170, 0.18, 0.32, 1.0, 2.5),
                    CuttingStageKind.SemiFinishing => (150, 200, 0.12, 0.22, 0.4, 1.0),
                    CuttingStageKind.Finishing => (180, 240, 0.05, 0.12, 0.10, 0.4),
                    _ => (140, 190, 0.15, 0.25, 0.5, 1.5)
                },

                TurningOperationKind.PartingAndGrooving => stage switch
                {
                CuttingStageKind.Roughing => (90, 140, Math.Round(0.03 * grooveW, 3), Math.Round(0.06 * grooveW, 3), grooveW, grooveW),
                CuttingStageKind.SemiFinishing => (110, 160, Math.Round(0.025 * grooveW, 3), Math.Round(0.045 * grooveW, 3), grooveW, grooveW),
                    CuttingStageKind.Finishing => (130, 180, Math.Round(0.015 * grooveW, 3), Math.Round(0.03 * grooveW, 3), 0.15, 0.4),
                    _ => (100, 150, 0.08, 0.15, grooveW, grooveW)
                },
                TurningOperationKind.Drilling => stage switch
                {
                    CuttingStageKind.Roughing => (70, 105, Math.Round(0.012 * d, 3), Math.Round(0.020 * d, 3), d / 2.0, d / 2.0),
                    CuttingStageKind.SemiFinishing => (80, 115, Math.Round(0.010 * d, 3), Math.Round(0.016 * d, 3), d / 2.0, d / 2.0),
                    CuttingStageKind.Finishing => (85, 120, Math.Round(0.008 * d, 3), Math.Round(0.013 * d, 3), d / 2.0, d / 2.0),
                    _ => (75, 110, 0.15, 0.25, d / 2.0, d / 2.0)
                },
                TurningOperationKind.Reaming => stage switch
                {
                    CuttingStageKind.Roughing => (15, 25, Math.Round(0.018 * d, 3), Math.Round(0.030 * d, 3), 0.2, 0.5),
                    CuttingStageKind.SemiFinishing => (12, 20, Math.Round(0.015 * d, 3), Math.Round(0.025 * d, 3), 0.1, 0.25),
                    CuttingStageKind.Finishing => (8, 16, Math.Round(0.012 * d, 3), Math.Round(0.020 * d, 3), 0.05, 0.15),
                    _ => (10, 18, 0.20, 0.35, 0.1, 0.2)
                },
                TurningOperationKind.ThreadTurning => stage switch
                {
                    CuttingStageKind.Roughing => (90, 130, 1.0, 1.0, 0.18, 0.30),
                    CuttingStageKind.SemiFinishing => (100, 145, 1.0, 1.0, 0.10, 0.18),
                    CuttingStageKind.Finishing => (110, 160, 1.0, 1.0, 0.03, 0.07),
                    _ => (100, 140, 1.0, 1.0, 0.10, 0.20)
                },
                _ => (160, 220, 0.20, 0.35, 1.0, 2.5)
            };
        }

        private static double GetMaterialFeedFactor(IsoGroupCode iso, MaterialGradeItem? grade)
        {
            double baseIsoFactor = iso switch
            {
                IsoGroupCode.P => 1.00, // Конструкционные стали (эталон)
                IsoGroupCode.M => 0.80, // Нержавейка (снижаем подачу от вибраций и наклёпа)
                IsoGroupCode.K => 1.15, // Чугуны (короткая стружка позволяет выше подачу)
                IsoGroupCode.N => 1.25, // Цветные сплавы (алюминий, латунь)
                IsoGroupCode.S => 0.65, // Титан и жаропрочные сплавы
                IsoGroupCode.H => 0.75, // Закалённые стали
                _ => 1.00
            };

            if (grade == null) return baseIsoFactor;

            double gradeShift = grade.GostGrade switch
            {
                "Ст3сп" or "Сталь 20" or "ЛС59-1" => 1.08,
                "40Х" or "30ХГСА" => 0.94,
                "40ХН2МА" or "08Х17Н13М2" => 0.88,
                "ВТ6" or "ХН77ТЮР" => 0.85,
                _ => 1.00
            };

            return baseIsoFactor * gradeShift;
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
        private static (double vcFactor, double fnFactor, string warning) GetToolMaterialFactors(
        ToolMaterialKind tool, IsoGroupCode iso, TurningOperationKind op, CuttingStageKind stage)
        {
            return tool switch
            {
                ToolMaterialKind.CoatedCarbide => iso == IsoGroupCode.H
                ? (0.75, 0.70, "Для закалённой стали (группа H) твёрдый сплав требует низких скоростей и жёсткого станка; для чистовой лучше использовать CBN.")
                : (1.00, 1.00, string.Empty),
                ToolMaterialKind.UncoatedCarbide => (0.75, 0.90,
                iso == IsoGroupCode.N
                ? string.Empty
                : "Твёрдый сплав без покрытия имеет меньшую теплостойкость — скорость резания снижена на 25%."),
                ToolMaterialKind.Cermet => stage == CuttingStageKind.Roughing
                ? (1.15, 0.65, "Внимание! Кермет хрупкий и не предназначен для чернового точения с ударной нагрузкой — используйте его на чистовых проходах.")
                : (1.20, 0.85, string.Empty),
                ToolMaterialKind.CbnOrCeramic => iso == IsoGroupCode.H || iso == IsoGroupCode.K
                ? (1.65, 0.75, "CBN / Керамика работают на высоких скоростях без СОЖ (во избежание термоудара пластины).")
                : (1.30, 0.70, "CBN и керамика редко применяются для вязких сталей (P/M) из-за химического износа и выкрашивания."),
                ToolMaterialKind.Hss => op == TurningOperationKind.Drilling || op == TurningOperationKind.Reaming
                ? (0.35, 0.75, string.Empty)
                : (0.25, 0.70, "Быстрорез (HSS / Р6М5) допускает только низкие скорости резания во избежание отпуска кромки."),
                _ => (1.0, 1.0, string.Empty)
            };
        }
        private static (double vcFactor, double fnFactor, double apFactor, string warning) GetOverhangFactors(
        TurningOperationKind op, double ld)
        {
            if (ld <= 3.0)
                return (1.0, 1.0, 1.0, string.Empty);
            if (ld <= 5.0)
            {
                string msg = op == TurningOperationKind.Drilling
                ? $"Глубина сверления L/D = {ld:0.#}: рекомендуется прерывистый цикл сверления (G83 / G74) для вывода стружки."
                : $"Повышенный вылет инструмента (L/D = {ld:0.#}): режимы снижены на 15% для подавления вибраций.";
                return (0.85, 0.85, 0.80, msg);
            }
            string highWarning = op == TurningOperationKind.Drilling
            ? $"Глубокое сверление (L/D = {ld:0.#}): обязателен цикл глубокого сверления с полным выводом сверла (G83) и СОЖ под давлением!"
            : $"Критический вылет оправки (L/D = {ld:0.#}): высокий риск дробления! Используйте антивибрационную державку и уменьшите глубину резания.";
            return (0.65, 0.70, 0.55, highWarning);
        }
        private static string BuildOperationGeometryAdvice(TurningCuttingRequest request)
        {
            return request.Operation switch
            {
                TurningOperationKind.ExternalTurning => request.Stage switch
                {
                    CuttingStageKind.Roughing => "Пластина CNMG / WNMG с радиусом при вершине r = 0.8–1.2 мм и прочным стружколомом.",
                    CuttingStageKind.SemiFinishing => "Пластина CNMG / DNMG с радиусом r = 0.4–0.8 мм для стабильного стружкодробления.",
                    _ => "Пластина DCMT / VBMT / TNMG с малым радиусом r = 0.2–0.4 мм и острым чистовым стружколомом."
                },
                TurningOperationKind.InternalBoring =>
                "Расточная державка с положительным передним углом (CCMT / DCMT, r = 0.2–0.4 мм) для снижения радиального отжатия.",
                TurningOperationKind.PartingAndGrooving =>
                "Минимальный вылет отрезного лезвия строго по центру детали; снижайте подачу на 40% при подходе к центру (X < 4 мм).",
                TurningOperationKind.Drilling =>
                "Предварительная центровка обязательна; при L/D > 3 применяйте цикл прерывистого сверления с отводом стружки.",
                TurningOperationKind.Reaming =>
                "Припуск под развёртывание 0.1–0.25 мм на диаметр; обратный выход развёртки выполнять только при вращающемся шпинделе.",
                TurningOperationKind.ThreadTurning =>
                "Работа строго в режиме постоянных оборотов (G97); используйте метод врезания по боковой стороне профиля (G76).",
                _ => request.IsoGroup.ToolingTip
            };
        }
        private static string BuildCleanSampleGCode(
        TurningCuttingRequest request, int vcRec, int rpmRec, int g50Limit, double fnRec)
        {
            string fnStr = fnRec.ToString("0.###", CultureInfo.InvariantCulture);
            bool forceG97 = request.Operation == TurningOperationKind.Drilling ||
            request.Operation == TurningOperationKind.Reaming ||
            request.Operation == TurningOperationKind.ThreadTurning;
            if (forceG97)
            {
                return request.Operation == TurningOperationKind.ThreadTurning
                ? $"G97 S{rpmRec} M03{Environment.NewLine}M08"
                : $"G97 S{rpmRec} M03{Environment.NewLine}G99 F{fnStr}{Environment.NewLine}M08";
            }
            return $"G50 S{g50Limit}{Environment.NewLine}G96 S{vcRec} M03{Environment.NewLine}G99 F{fnStr}{Environment.NewLine}M08";
        }

        public SpindleSimulationResult SimulateSpindle(SpindleSimulationRequest request)
        {
            if (request.StartDiameter <= 0 || request.StartDiameter > 2500.0)
                return ErrorSimulation("Начальный диаметр D_нач должен быть больше 0 и не превышать 2500 мм.");

            if (request.MinDiameter < 0 || request.MinDiameter > 2500.0)
                return ErrorSimulation("Минимальный диаметр D_мин не может быть отрицательным.");

            if (request.MinDiameter > request.StartDiameter)
                return ErrorSimulation("Минимальный диаметр D_мин не может быть больше начального D_нач.");

            if (request.FeedPerRev <= 0 || request.FeedPerRev > 20.0)
                return ErrorSimulation("Оборотная подача fn должна быть больше 0 (обычно 0.03 – 1.5 мм/об).");

            if (request.IsConstantSurfaceSpeed && (request.MaxChuckRpmG50 < 50 || request.MaxChuckRpmG50 > 15000))
                return ErrorSimulation("Ограничение оборотов патрона G50 должно быть в пределах от 50 до 15000 об/мин.");

            var warnings = new List<string>();
            var inv = CultureInfo.InvariantCulture;

            double dStart = request.StartDiameter;
            double dMin = request.MinDiameter;
            double effectiveDMin = Math.Max(dMin, 0.5); // Защита от деления на ноль при торцевании до X0

            // Определяем целевую скорость резания Vc или фиксированные обороты n
            double targetVc;
            int fixedRpm;

            if (request.InputMode == CuttingCalcInputMode.ByCuttingSpeedVc)
            {
                if (request.InputVc <= 0 || request.InputVc > 3000.0)
                    return ErrorSimulation("Скорость резания Vc должна быть больше 0.");

                targetVc = request.InputVc;
                fixedRpm = (int)Math.Round((1000.0 * targetVc) / (Math.PI * dStart));
                fixedRpm = Math.Clamp(fixedRpm, 10, 15000);
            }
            else
            {
                if (request.InputRpm < 10 || request.InputRpm > 15000)
                    return ErrorSimulation("Частота вращения шпинделя n должна быть в пределах от 10 до 15000 об/мин.");

                fixedRpm = request.InputRpm;
                targetVc = Math.Round((Math.PI * dStart * fixedRpm) / 1000.0, 1);
            }

            int startRpm;
            int minDiaTheorRpm;
            int minDiaActualRpm;
            double startVc;
            double minDiaActualVc;
            bool isG50Triggered = false;
            double cutoffDiameter = 0.0;
            string cutoffText;
            string sampleGCode;

            if (request.IsConstantSurfaceSpeed)
            {
                // РЕЖИМ G96 (Постоянная скорость резания)
                int rawStartRpm = (int)Math.Round((1000.0 * targetVc) / (Math.PI * dStart));
                startRpm = Math.Min(rawStartRpm, request.MaxChuckRpmG50);

                minDiaTheorRpm = dMin < 0.01
                    ? 99999
                    : (int)Math.Round((1000.0 * targetVc) / (Math.PI * dMin));

                minDiaActualRpm = Math.Min(minDiaTheorRpm, request.MaxChuckRpmG50);

                startVc = Math.Round((Math.PI * dStart * startRpm) / 1000.0, 1);
                minDiaActualVc = dMin < 0.01
                    ? 0.0
                    : Math.Round((Math.PI * dMin * minDiaActualRpm) / 1000.0, 1);

                // Диаметр отсечки D_отсечки = (1000 * Vc) / (pi * S_max)
                cutoffDiameter = (1000.0 * targetVc) / (Math.PI * request.MaxChuckRpmG50);

                if (minDiaTheorRpm > request.MaxChuckRpmG50)
                {
                    isG50Triggered = true;
                    cutoffText = string.Format(inv, "Ø {0:0.##} мм", cutoffDiameter);

                    if (dMin < 0.01)
                    {
                        warnings.Add(string.Format(inv,
                            "При торцевании до центра (X0) шпиндель упрётся в потолок G50 S{0} начиная с диаметра Ø{1:0.##} мм. В самом центре Vc упадёт до 0 — убедитесь, что патрон выдержит {0} об/мин!",
                            request.MaxChuckRpmG50, cutoffDiameter));
                    }
                    else
                    {
                        warnings.Add(string.Format(inv,
                            "Сработает ограничитель G50 S{0}: на диаметре Ø{1:0.##} мм обороты достигнут максимума, и на Ø{2:0.##} мм скорость резания снизится до {3:0.#} м/мин.",
                            request.MaxChuckRpmG50, cutoffDiameter, dMin, minDiaActualVc));
                    }
                }
                else
                {
                    cutoffText = "Не достигается";
                }

                if (rawStartRpm > request.MaxChuckRpmG50)
                {
                    warnings.Add(string.Format(inv,
                        "Уже на стартовом диаметре Ø{0:0.##} мм расчётные обороты ({1} об/мин) выше лимита G50 S{2} — шпиндель сразу будет ограничен!",
                        dStart, rawStartRpm, request.MaxChuckRpmG50));
                }

                string fnStr = request.FeedPerRev.ToString("0.###", inv);
                sampleGCode = string.Format(inv,
                    "G50 S{0}{1}G96 S{2:0} M03{1}G99 F{3}",
                    request.MaxChuckRpmG50, Environment.NewLine, targetVc, fnStr);
            }
            else
            {
                // РЕЖИМ G97 (Постоянные обороты)
                startRpm = fixedRpm;
                minDiaTheorRpm = fixedRpm;
                minDiaActualRpm = fixedRpm;

                startVc = Math.Round((Math.PI * dStart * fixedRpm) / 1000.0, 1);
                minDiaActualVc = dMin < 0.01
                    ? 0.0
                    : Math.Round((Math.PI * dMin * fixedRpm) / 1000.0, 1);

                cutoffText = "Не используется (G97)";

                if (dStart - dMin > 15.0 && startVc > 0)
                {
                    warnings.Add(string.Format(inv,
                        "В режиме G97 при переходе с Ø{0:0.##} мм на Ø{1:0.##} мм скорость резания падает с {2:0.#} до {3:0.#} м/мин (ухудшается качество поверхности и стружкодробление).",
                        dStart, dMin, startVc, minDiaActualVc));
                }

                string fnStr = request.FeedPerRev.ToString("0.###", inv);
                sampleGCode = string.Format(inv,
                    "G97 S{0} M03{1}G99 F{2}",
                    fixedRpm, Environment.NewLine, fnStr);
            }

            if (request.ReferenceVcMax.HasValue && targetVc > request.ReferenceVcMax.Value * 1.05)
            {
                string matName = string.IsNullOrWhiteSpace(request.MaterialContextLabel)
                    ? "выбранного материала"
                    : request.MaterialContextLabel;

                warnings.Add(string.Format(inv,
                    "Скорость резания Vc ({0:0.#} м/мин) превышает рекомендуемый диапазон для {1} ({2:0}–{3:0} м/мин) — риск быстрого износа или выкрашивания пластины.",
                    targetVc, matName, request.ReferenceVcMin ?? 0, request.ReferenceVcMax.Value));
            }
            else if (request.ReferenceVcMin.HasValue && targetVc < request.ReferenceVcMin.Value * 0.65)
            {
                string matName = string.IsNullOrWhiteSpace(request.MaterialContextLabel)
                    ? "выбранного материала"
                    : request.MaterialContextLabel;

                warnings.Add(string.Format(inv,
                    "Скорость резания Vc ({0:0.#} м/мин) заметно ниже рекомендуемой для {1} ({2:0}–{3:0} м/мин) — возможно наростообразование на кромке.",
                    targetVc, matName, request.ReferenceVcMin.Value, request.ReferenceVcMax ?? 0));
            }

            double vfStart = Math.Round(request.FeedPerRev * startRpm, 0);
            double vfMinDia = Math.Round(request.FeedPerRev * minDiaActualRpm, 0);

            var result = new SpindleSimulationResult
            {
                IsValid = true,
                StartRpmText = string.Format(inv, "{0} об/мин", startRpm),
                MinDiaTheoreticalRpmText = dMin < 0.01 && request.IsConstantSurfaceSpeed
                    ? "∞ (разгон к центру)"
                    : string.Format(inv, "{0} об/мин", minDiaTheorRpm),
                MinDiaActualRpmText = string.Format(inv, "{0} об/мин", minDiaActualRpm),
                StartVcText = string.Format(inv, "{0:0.#} м/мин", startVc),
                MinDiaActualVcText = string.Format(inv, "{0:0.#} м/мин", minDiaActualVc),
                FeedPerMinRangeText = startRpm == minDiaActualRpm
                    ? string.Format(inv, "{0:0} мм/мин", vfStart)
                    : string.Format(inv, "{0:0} → {1:0} мм/мин", vfStart, vfMinDia),
                CutoffDiameterText = cutoffText,
                IsG50Triggered = isG50Triggered,
                SampleGCode = sampleGCode,
                HasWarning = warnings.Count > 0,
                WarningMessage = string.Join(Environment.NewLine, warnings)
            };

            BuildSpindleChartCoordinates(result, request, targetVc, fixedRpm, effectiveDMin, cutoffDiameter);
            return result;
        }

        private static void BuildSpindleChartCoordinates(
            SpindleSimulationResult res,
            SpindleSimulationRequest req,
            double targetVc,
            int fixedRpm,
            double effectiveDMin,
            double cutoffDia)
        {
            // Область графика на Canvas 320 x 200 (ось X: D от D_нач слева до D_мин справа; ось Y: обороты n)
            const double leftX = 36.0;
            const double rightX = 296.0;
            const double topY = 24.0;
            const double bottomY = 168.0;

            double dStart = req.StartDiameter;
            double dEnd = req.MinDiameter;

            // Определяем потолок шкалы Y графика
            double maxDisplayRpm = req.IsConstantSurfaceSpeed
                ? Math.Max(req.MaxChuckRpmG50 * 1.25, 1000.0)
                : Math.Max(fixedRpm * 1.35, 1000.0);

            double MapY(double rpm)
            {
                double clamped = Math.Clamp(rpm, 0.0, maxDisplayRpm);
                return bottomY - (clamped / maxDisplayRpm) * (bottomY - topY);
            }

            double MapX(double d)
            {
                if (Math.Abs(dStart - dEnd) < 1e-4) return (leftX + rightX) / 2.0;
                double t = (dStart - d) / (dStart - dEnd); // 0 слева (D_нач), 1 справа (D_мин)
                return leftX + Math.Clamp(t, 0.0, 1.0) * (rightX - leftX);
            }

            var inv = CultureInfo.InvariantCulture;
            res.G50LineY = MapY(req.MaxChuckRpmG50);
            res.G50LabelText = string.Format(inv, "G50 S{0}", req.MaxChuckRpmG50);
            res.DStartLabelText = string.Format(inv, "Ø{0:0.#}", dStart);
            res.DMinLabelText = string.Format(inv, "Ø{0:0.#}", dEnd);

            const int steps = 28;
            var actualPts = new List<string>(steps + 1);
            var theorPts = new List<string>(steps + 1);

            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                double curD = dStart - t * (dStart - dEnd);
                double safeD = Math.Max(curD, effectiveDMin);
                double theorRpm = req.IsConstantSurfaceSpeed
                ? (1000.0 * targetVc) / (Math.PI * safeD)
                : fixedRpm;
                double actualRpm = req.IsConstantSurfaceSpeed
                ? Math.Min(theorRpm, req.MaxChuckRpmG50)
                : fixedRpm;
                double px = MapX(curD);
                actualPts.Add(string.Format(inv, "{0:F1},{1:F1}", px, MapY(actualRpm)));
                theorPts.Add(string.Format(inv, "{0:F1},{1:F1}", px, MapY(theorRpm)));
            }
            res.RpmCurvePoints = string.Join(" ", actualPts);
            res.TheoreticalCurvePoints = req.IsConstantSurfaceSpeed ? string.Join(" ", theorPts) : string.Empty;
            if (req.IsConstantSurfaceSpeed && res.IsG50Triggered && cutoffDia >= dEnd && cutoffDia <= dStart)
            {
                res.ShowCutoffDot = true;
                res.CutoffDotX = MapX(cutoffDia);
                res.CutoffDotY = res.G50LineY;
            }
            else
            {
                res.ShowCutoffDot = false;
            }
        }

        public RoughnessCalculationResult CalculateRoughness(RoughnessCalculationRequest request)
        {
            if (request.NoseRadius <= 0.02 || request.NoseRadius > 10.0)
                return ErrorRoughness("Радиус при вершине пластины r_ε должен быть в пределах от 0,05 до 10 мм (стандартный ряд: 0.2, 0.4, 0.8, 1.2, 1.6 мм).");

            var warnings = new List<string>();
            var inv = CultureInfo.InvariantCulture;

            double r = request.NoseRadius;
            double feedFn;
            double rzTheor;
            double raTheor;

            if (request.Mode == RoughnessCalcMode.ByTargetRoughness)
            {
                if (request.TargetRoughnessValue <= 0.05 || request.TargetRoughnessValue > 200.0)
                    return ErrorRoughness("Требуемая шероховатость должна быть в пределах от 0,1 до 200 мкм.");

                // Переводим требуемый параметр в Rz (для синусоидально-дугового профиля точения Rz ≈ 4 * Ra)
                double targetRz = request.ParameterKind == RoughnessParameterKind.Ra
                    ? request.TargetRoughnessValue * 4.0
                    : request.TargetRoughnessValue;

                // Обратная формула: fn = sqrt((8 * r * Rz_мкм) / 1000)
                feedFn = Math.Sqrt((8.0 * r * targetRz) / 1000.0);
                feedFn = Math.Round(feedFn, 3);

                // Пересчитываем точную теоретическую шероховатость для округлённой подачи fn
                rzTheor = ((feedFn * feedFn) / (8.0 * r)) * 1000.0;
                raTheor = rzTheor / 4.0;
            }
            else
            {
                if (request.InputFeedPerRev <= 0.005 || request.InputFeedPerRev > 5.0)
                    return ErrorRoughness("Оборотная подача fn должна быть в пределах от 0,01 до 5,0 мм/об.");

                feedFn = Math.Round(request.InputFeedPerRev, 3);

                // Прямая формула: Rz_теор (мкм) = (fn^2 / (8 * r)) * 1000
                rzTheor = ((feedFn * feedFn) / (8.0 * r)) * 1000.0;
                raTheor = rzTheor / 4.0;
            }

            // Практическое Ra на реальном станке обычно выше теоретического на 15–35% из-за вибраций и пластической деформации
            double raPractMin = Math.Round(raTheor * 1.15, 2);
            double raPractMax = Math.Round(raTheor * 1.35, 2);

            // Проверка геометрического условия применимости формулы (fn не должна превышать ~1.4 * r)
            if (feedFn > r * 1.4)
            {
                warnings.Add(string.Format(inv,
                    "Подача fn ({0:0.###} мм/об) слишком велика для радиуса r_ε = {1:0.##} мм — вершина пластины не перекрывает шаг, формула даёт завышенную погрешность.",
                    feedFn, r));
            }

            // Предупреждение о минимальной толщине срезаемого слоя (скобление и наклёп вместо резания)
            if (feedFn < 0.04)
            {
                warnings.Add(string.Format(inv,
                    "Очень малая подача fn = {0:0.###} мм/об: при радиусе r_ε = {1:0.##} мм кромка может начать мять и наклёпывать металл вместо чистого резания (особенно на нержавейке ISO M). Попробуйте взять пластину с меньшим радиусом r_ε.",
                    feedFn, r));
            }

            // Предупреждение о вибрациях при большом радиусе и вылете L/D > 3
            if (r >= 1.2 && request.OverhangRatioContext.HasValue && request.OverhangRatioContext.Value > 3.0)
            {
                warnings.Add(string.Format(inv,
                    "Большой радиус пластины (r_ε = {0:0.##} мм) при повышенном вылете оправки (L/D = {1:0.#}) резко усиливает радиальное отжатие и вибрации — лучше перейти на r_ε = 0.4 мм.",
                    r, request.OverhangRatioContext.Value));
            }

            string qualityClass = GetSurfaceQualityDescription(raTheor);
            string fnStr = feedFn.ToString("0.###", inv);

            var result = new RoughnessCalculationResult
            {
                IsValid = true,
                CalculatedFeedPerRev = feedFn,
                TheoreticalRa = Math.Round(raTheor, 2),
                TheoreticalRz = Math.Round(rzTheor, 2),
                PracticalRaMin = raPractMin,
                PracticalRaMax = raPractMax,
                FeedPerRevText = string.Format(inv, "{0:0.###} мм/об", feedFn),
                TheoreticalRaText = string.Format(inv, "Ra {0:0.00} мкм", raTheor),
                TheoreticalRzText = string.Format(inv, "Rz {0:0.0} мкм", rzTheor),
                PracticalRaRangeText = string.Format(inv, "Ra {0:0.00} – {1:0.00} мкм", raPractMin, raPractMax),
                SurfaceQualityClassText = qualityClass,
                SampleFeedGCode = $"G99 F{fnStr}",
                HasWarning = warnings.Count > 0,
                WarningMessage = string.Join(Environment.NewLine, warnings)
            };

            BuildRoughnessCanvasCoordinates(result, feedFn, r, rzTheor);
            return result;
        }

        private static RoughnessCalculationResult ErrorRoughness(string message) =>
            new() { IsValid = false, HasError = true, ErrorMessage = message };

        private static string GetSurfaceQualityDescription(double ra)
        {
            return ra switch
            {
                <= 0.45 => "Тонкое чистовое точение (аналог шлифования, 8–9 класс)",
                <= 0.85 => "Высокочистовое точение (посадочные шейки под подшипники, 7 класс)",
                <= 1.70 => "Чистовое точение (типовые сопрягаемые поверхности, 6 класс)",
                <= 3.40 => "Получистовое точение (свободные размеры, 5 класс)",
                <= 6.50 => "Обдирочное / базовое точение (4 класс)",
                _ => "Грубая черновая обработка (3 класс и ниже)"
            };
        }

        private static void BuildRoughnessCanvasCoordinates(
            RoughnessCalculationResult res, double fn, double r, double rzMicrons)
        {
            const double canvasW = 320.0;
            const double valleyY = 132.0; // Линия впадин (дно канавки от вершины резца)

            // Визуально масштабируем шаг и высоту гребешков на Canvas для наглядности
            double ratio = Math.Clamp(fn / Math.Max(r, 0.1), 0.12, 1.35);
            double pitchPx = Math.Clamp(52.0 + ratio * 38.0, 56.0, 96.0);
            double heightPx = Math.Clamp(18.0 + ratio * 36.0, 18.0, 62.0);
            double peakY = valleyY - heightPx;

            res.PeakLineY = peakY;
            res.ValleyLineY = valleyY;

            // Строим 3 полных гребешка по центру Canvas (X от startX до endX)
            const int waves = 3;
            double totalW = waves * pitchPx;
            double startX = (canvasW - totalW) / 2.0;

            var inv = CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();

            // Замкнутый контур тела металла под гребешками (заливка снизу до Y = 166)
            const double baseMetalY = 166.0;
            sb.AppendFormat(inv, "M {0:F1},{1:F1} L {0:F1},{2:F1} ", startX, baseMetalY, peakY);

            double arcRadiusPx = (pitchPx * pitchPx) / (8.0 * heightPx) + (heightPx / 2.0);
            arcRadiusPx = Math.Max(arcRadiusPx, pitchPx * 0.55);

            for (int i = 0; i < waves; i++)
            {
                double xEnd = startX + (i + 1) * pitchPx;
                // Вогнутая дуга следа от радиуса при вершине резца (SweepDirection = 0 -> прогиб вниз)
                sb.AppendFormat(inv, "A {0:F1},{0:F1} 0 0 0 {1:F1},{2:F1} ", arcRadiusPx, xEnd, peakY);
            }

            double endX = startX + totalW;
            sb.AppendFormat(inv, "L {0:F1},{1:F1} Z", endX, baseMetalY);

            res.ProfilePathData = sb.ToString();

            // Координаты размерной стрелки шага подачи fn (на центральном гребешке)
            res.PitchArrowLeftX = startX + pitchPx;
            res.PitchArrowRightX = startX + 2.0 * pitchPx;

            res.PitchLabelText = string.Format(inv, "fn = {0:0.###} мм/об", fn);
            res.HeightLabelText = string.Format(inv, "Rz = {0:0.0} мкм", rzMicrons);
            res.RadiusLabelText = string.Format(inv, "r_ε = {0:0.##} мм", r);
        }


        private static SpindleSimulationResult ErrorSimulation(string message) =>
        new() { IsValid = false, HasError = true, ErrorMessage = message };

    }
}