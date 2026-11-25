using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MechCore.Models.Milling;

namespace MechCore.Services.Milling
{
    public class MillingGeometryService : IMillingGeometryService
    {
        private const double DegToRad = Math.PI / 180.0;

        public HolePatternCalculationResult CalculateHolePattern(
            HolePatternMode mode,
            double centerX,
            double centerY,
            double pcdDiameter,
            int holeCount,
            double startAngleDeg,
            double customStepAngleDeg,
            bool useFullCircle,
            bool isCounterClockwise,
            int gridCountX,
            int gridCountY,
            double gridStepX,
            double gridStepY,
            double gridRotationAngleDeg,
            bool useZigZagOrder,
            bool skipFirstPointInCycleCode)
        {
            var inv = CultureInfo.InvariantCulture;
            var warnings = new List<string>();
            var points = new List<HolePointItem>();

            double stepAngle = 0.0;
            double minPitch = 0.0;

            if (mode == HolePatternMode.CirclePcd)
            {
                if (pcdDiameter <= 0)
                    return ErrorPattern("Диаметр делительной окружности PCD должен быть больше 0.");
                if (holeCount < 1 || holeCount > 360)
                    return ErrorPattern("Количество отверстий N должно быть от 1 до 360.");
                if (!useFullCircle && Math.Abs(customStepAngleDeg) < 1e-5)
                    return ErrorPattern("Угол шага между отверстиями не может быть равен 0°.");

                double radius = pcdDiameter / 2.0;
                double dirSign = isCounterClockwise ? 1.0 : -1.0;

                stepAngle = useFullCircle
                    ? (360.0 / holeCount) * dirSign
                    : Math.Abs(customStepAngleDeg) * dirSign;

                if (!useFullCircle && Math.Abs(stepAngle) * holeCount > 360.001)
                {
                    warnings.Add("Суммарный угол сектора превышает 360° — проверьте число отверстий N или угол шага.");
                }

                for (int i = 0; i < holeCount; i++)
                {
                    double angDeg = startAngleDeg + i * stepAngle;
                    double angRad = angDeg * DegToRad;

                    double px = Math.Round(centerX + radius * Math.Cos(angRad), 3);
                    double py = Math.Round(centerY + radius * Math.Sin(angRad), 3);

                    double normAng = angDeg % 360.0;
                    if (normAng < 0) normAng += 360.0;

                    points.Add(new HolePointItem
                    {
                        Index = i + 1,
                        X = px,
                        Y = py,
                        AngleDeg = Math.Round(normAng, 2),
                        AngleText = string.Format(inv, "{0:0.##}°", normAng),
                        GCodeLine = string.Format(inv, "X{0:0.###} Y{1:0.###}", px, py)
                    });
                }

                if (holeCount >= 2)
                {
                    double absStepRad = Math.Abs(stepAngle) * DegToRad;
                    minPitch = 2.0 * radius * Math.Sin(Math.Min(absStepRad, Math.PI) / 2.0);
                }
            }
            else
            {
                if (gridCountX < 1 || gridCountY < 1 || gridCountX * gridCountY > 500)
                    return ErrorPattern("Число столбцов и строк сетки должно быть ≥ 1 (всего не более 500 отверстий).");
                if (gridCountX > 1 && Math.Abs(gridStepX) < 1e-5)
                    return ErrorPattern("Шаг по оси X (ΔX) не может быть равен 0 при числе столбцов > 1.");
                if (gridCountY > 1 && Math.Abs(gridStepY) < 1e-5)
                    return ErrorPattern("Шаг по оси Y (ΔY) не может быть равен 0 при числе строк > 1.");

                double rotRad = gridRotationAngleDeg * DegToRad;
                double cosA = Math.Cos(rotRad);
                double sinA = Math.Sin(rotRad);

                int idx = 1;
                for (int row = 0; row < gridCountY; row++)
                {
                    bool reverseRow = useZigZagOrder && (row % 2 == 1);
                    for (int c = 0; c < gridCountX; c++)
                    {
                        int col = reverseRow ? (gridCountX - 1 - c) : c;
                        double localX = col * gridStepX;
                        double localY = row * gridStepY;

                        double rotX = localX * cosA - localY * sinA;
                        double rotY = localX * sinA + localY * cosA;

                        double px = Math.Round(centerX + rotX, 3);
                        double py = Math.Round(centerY + rotY, 3);

                        points.Add(new HolePointItem
                        {
                            Index = idx++,
                            X = px,
                            Y = py,
                            AngleDeg = Math.Round(gridRotationAngleDeg, 2),
                            AngleText = string.Format(inv, "R{0}C{1}", row + 1, col + 1),
                            GCodeLine = string.Format(inv, "X{0:0.###} Y{1:0.###}", px, py)
                        });
                    }
                }

                var pitches = new List<double>();
                if (gridCountX > 1) pitches.Add(Math.Abs(gridStepX));
                if (gridCountY > 1) pitches.Add(Math.Abs(gridStepY));
                minPitch = pitches.Count > 0 ? pitches.Min() : 0.0;
            }

            // Расчёт суммарного пути переходов между отверстиями
            double totalTravel = 0.0;
            for (int i = 1; i < points.Count; i++)
            {
                double dx = points[i].X - points[i - 1].X;
                double dy = points[i].Y - points[i - 1].Y;
                totalTravel += Math.Sqrt(dx * dx + dy * dy);
            }

            // Формирование чистого G-кода координат (комментарии строго на английском)
            var sb = new StringBuilder();
            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                if (i == 0 && skipFirstPointInCycleCode)
                {
                    sb.AppendFormat(inv, "(HOLE 1: X{0:0.###} Y{1:0.###} - SET IN CYCLE LINE)\n", pt.X, pt.Y);
                }
                else
                {
                    sb.AppendFormat(inv, "X{0:0.###} Y{1:0.###}\n", pt.X, pt.Y);
                }
            }

            var result = new HolePatternCalculationResult
            {
                IsValid = true,
                HasWarning = warnings.Count > 0,
                WarningMessage = string.Join("\n", warnings),
                IsCircleMode = mode == HolePatternMode.CirclePcd,
                TotalHolesCount = points.Count,
                StepAngleDeg = Math.Round(Math.Abs(stepAngle), 3),
                MinPitchMm = Math.Round(minPitch, 3),
                TotalRapidTravelMm = Math.Round(totalTravel, 2),
                Points = points,
                GCodeSnippet = sb.ToString().TrimEnd()
            };

            BuildHolePatternCanvas(result, centerX, centerY, pcdDiameter);
            return result;
        }

        private static void BuildHolePatternCanvas(
            HolePatternCalculationResult res,
            double centerX,
            double centerY,
            double pcdDiameter)
        {
            var inv = CultureInfo.InvariantCulture;

            const double canvasW = 320.0;
            const double canvasH = 200.0;
            const double pad = 26.0;

            if (res.Points.Count == 0) return;

            double minX = res.Points.Min(p => p.X);
            double maxX = res.Points.Max(p => p.X);
            double minY = res.Points.Min(p => p.Y);
            double maxY = res.Points.Max(p => p.Y);

            if (res.IsCircleMode)
            {
                double r = pcdDiameter / 2.0;
                minX = Math.Min(minX, centerX - r);
                maxX = Math.Max(maxX, centerX + r);
                minY = Math.Min(minY, centerY - r);
                maxY = Math.Max(maxY, centerY + r);
            }

            double spanX = Math.Max(maxX - minX, 1e-3);
            double spanY = Math.Max(maxY - minY, 1e-3);

            double scale = Math.Min((canvasW - 2 * pad) / spanX, (canvasH - 2 * pad) / spanY);
            double midRealX = (minX + maxX) / 2.0;
            double midRealY = (minY + maxY) / 2.0;

            double MapX(double x) => (canvasW / 2.0) + (x - midRealX) * scale;
            double MapY(double y) => (canvasH / 2.0) - (y - midRealY) * scale; // инверсия оси Y для WPF Canvas

            res.CanvasCenterX = MapX(centerX);
            res.CanvasCenterY = MapY(centerY);

            res.CenterLabelText = string.Format(inv, "X{0:0.##} Y{1:0.##}", centerX, centerY);
            res.CenterLabelX = Math.Clamp(res.CanvasCenterX + 6.0, 4.0, canvasW - 76.0);
            res.CenterLabelY = Math.Clamp(res.CanvasCenterY + 4.0, 4.0, canvasH - 18.0);

            if (res.IsCircleMode)
            {
                double diaPx = pcdDiameter * scale;
                res.CanvasCircleDiameter = diaPx;
                res.CanvasCircleLeft = res.CanvasCenterX - diaPx / 2.0;
                res.CanvasCircleTop = res.CanvasCenterY - diaPx / 2.0;
            }

            var polyPts = new List<string>(res.Points.Count);
            var dots = new List<HoleCanvasDot>(res.Points.Count);

            for (int i = 0; i < res.Points.Count; i++)
            {
                var p = res.Points[i];
                double cx = MapX(p.X);
                double cy = MapY(p.Y);
                polyPts.Add(string.Format(inv, "{0:F1},{1:F1}", cx, cy));

                // Подписываем номера точек (если точек не больше 24, чтобы не загромождать схему)
                string label = res.Points.Count <= 24 ? p.Index.ToString(inv) : (i == 0 ? "1" : string.Empty);

                dots.Add(new HoleCanvasDot
                {
                    CanvasX = cx,
                    CanvasY = cy,
                    LabelX = Math.Clamp(cx + 5.0, 4.0, canvasW - 22.0),
                    LabelY = Math.Clamp(cy - 14.0, 2.0, canvasH - 16.0),
                    Label = label,
                    IsFirst = i == 0
                });
            }
            res.PathPolylinePoints = string.Join(" ", polyPts);
            res.CanvasDots = dots;
        }
        private static HolePatternCalculationResult ErrorPattern(string message) =>
        new() { IsValid = false, ErrorMessage = message };

        public HelicalCalculationResult CalculateHelicalInterpolation(
    double holeDiameter,
    double toolDiameter,
    int fluteCount,
    double centerX,
    double centerY,
    double startZ,
    double targetZ,
    double clearanceR,
    double rampAngleDeg,
    double edgeFeedPerMin,
    int spindleRpm,
    bool isClimbMilling,
    bool includeBottomCleanupPass)
        {
            if (holeDiameter <= 0)
                return ErrorHelical("Диаметр целевого отверстия D_отв должен быть больше 0.");
            if (toolDiameter <= 0)
                return ErrorHelical("Диаметр фрезы D_фр должен быть больше 0.");
            if (toolDiameter >= holeDiameter)
                return ErrorHelical("Диаметр фрезы D_фр должен быть строго меньше диаметра отверстия D_отв.");
            if (fluteCount < 1 || fluteCount > 24)
                return ErrorHelical("Число зубьев фрезы z должно быть от 1 до 24.");
            if (rampAngleDeg <= 0.1 || rampAngleDeg >= 45.0)
                return ErrorHelical("Угол винтового врезания α должен быть в пределах (0.1°; 45°).");
            if (edgeFeedPerMin <= 0)
                return ErrorHelical("Минутная подача Vf должна быть больше 0.");
            if (spindleRpm < 10)
                return ErrorHelical("Обороты шпинделя S должны быть больше 0.");

            double totalDepth = startZ - targetZ;
            if (totalDepth <= 0.05)
                return ErrorHelical("Конечная глубина Z_кон должна быть ниже начальной плоскости Z_нач.");

            var warnings = new List<string>();
            var inv = CultureInfo.InvariantCulture;

            double orbitDia = holeDiameter - toolDiameter;
            double orbitRadius = orbitDia / 2.0;

            // Проверка минимального зазора для эвакуации стружки
            if (holeDiameter < toolDiameter * 1.15)
            {
                warnings.Add(string.Format(inv,
                    "Очень тесное отверстие (D_отв / D_фр = {0:0.##} < 1.15): затруднён выход стружки из центра — используйте обдув воздухом или СОЖ под давлением.",
                    holeDiameter / toolDiameter));
            }

            // Проверка на оставление центрального стержня (бобышки) при D_отв > 2 * D_фр
            bool hasCore = holeDiameter > 2.0 * toolDiameter + 1e-4;
            double coreDia = hasCore ? (holeDiameter - 2.0 * toolDiameter) : 0.0;
            if (hasCore)
            {
                warnings.Add(string.Format(inv,
                    "Внимание: D_отв > 2 × D_фр! В центре отверстия останется несрезанный стержень Ø{0:0.##} мм, который при отделении может сломать фрезу!",
                    coreDia));
            }

            // Длина окружности траектории центра фрезы за 1 виток (360°)
            double orbitCircumference = Math.PI * orbitDia;

            // Номинальный и скорректированный (под целое число витков) шаг по Z за оборот
            double nominalPitch = orbitCircumference * Math.Tan(rampAngleDeg * DegToRad);
            int fullRevs = Math.Max(1, (int)Math.Ceiling(totalDepth / nominalPitch));
            double actualPitch = totalDepth / fullRevs;

            // Пересчёт подачи центра инструмента: Vf_центр = Vf_кромки * (D_отв - D_фр) / D_отв
            double centerFeed = edgeFeedPerMin * (orbitDia / holeDiameter);
            centerFeed = Math.Max(1.0, Math.Round(centerFeed, 1));

            // Суммарный путь спирали + плоский зачистной виток по дну
            double helicalLength = Math.Sqrt(Math.Pow(fullRevs * orbitCircumference, 2) + Math.Pow(totalDepth, 2));
            double bottomCleanupLength = includeBottomCleanupPass ? orbitCircumference : 0.0;
            double totalPath = helicalLength + bottomCleanupLength;

            double timeSec = (60.0 * totalPath) / centerFeed;
            string timeText = timeSec < 60.0
                ? string.Format(inv, "{0:0.##} сек", timeSec)
                : string.Format(inv, "{0} мин {1:0.#} сек", (int)(timeSec / 60.0), timeSec % 60.0);

            // Генерация чистого G-кода (G03 — попутное фрезерование внутри отверстия, G02 — встречное)
            string gArc = isClimbMilling ? "G03" : "G02";
            double startX = Math.Round(centerX + orbitRadius, 3);
            double offsetI = Math.Round(-orbitRadius, 3);

            var sb = new StringBuilder();
            sb.AppendFormat(inv, "(HELICAL INTERPOLATION D{0:0.##} / TOOL D{1:0.##})\n", holeDiameter, toolDiameter);
            sb.AppendFormat(inv, "G00 X{0:0.###} Y{1:0.###}\n", centerX, centerY);
            sb.AppendFormat(inv, "G00 Z{0:0.###}\n", startZ + Math.Max(0.5, clearanceR));
            sb.AppendFormat(inv, "G01 Z{0:0.###} F{1:0}\n", startZ, Math.Round(centerFeed * 2.0, 0));
            sb.AppendFormat(inv, "G01 X{0:0.###} Y{1:0.###} F{2:0.#}\n", startX, centerY, centerFeed);

            for (int rev = 1; rev <= fullRevs; rev++)
            {
                double zLevel = Math.Round(startZ - rev * actualPitch, 3);
                if (rev == fullRevs) zLevel = Math.Round(targetZ, 3);

                sb.AppendFormat(inv, "{0} X{1:0.###} Y{2:0.###} Z{3:0.###} I{4:0.###} J0\n",
                    gArc, startX, centerY, zLevel, offsetI);
            }

            if (includeBottomCleanupPass)
            {
                sb.AppendFormat(inv, "{0} X{1:0.###} Y{2:0.###} I{3:0.###} J0 (BOTTOM CLEANUP)\n",
                    gArc, startX, centerY, offsetI);
            }

            sb.AppendFormat(inv, "G01 X{0:0.###} Y{1:0.###}\n", centerX, centerY);
            sb.AppendFormat(inv, "G00 Z{0:0.###}", startZ + Math.Max(2.0, clearanceR));

            var result = new HelicalCalculationResult
            {
                IsValid = true,
                HasWarning = warnings.Count > 0,
                WarningMessage = string.Join("\n", warnings),
                HoleDiameter = Math.Round(holeDiameter, 3),
                ToolDiameter = Math.Round(toolDiameter, 3),
                HelicalOrbitDiameter = Math.Round(orbitDia, 3),
                HelicalOrbitRadius = Math.Round(orbitRadius, 3),
                RampAngleDeg = Math.Round(rampAngleDeg, 2),
                NominalPitchPerRevMm = Math.Round(nominalPitch, 3),
                FullRevolutionsCount = fullRevs,
                ActualPitchPerRevMm = Math.Round(actualPitch, 3),
                TotalDepthMm = Math.Round(totalDepth, 3),
                EdgeFeedPerMin = Math.Round(edgeFeedPerMin, 1),
                CenterFeedPerMin = centerFeed,
                SpindleRpm = spindleRpm,
                TotalHelicalPathMm = Math.Round(totalPath, 2),
                MachiningTimeSeconds = Math.Round(timeSec, 2),
                MachiningTimeText = timeText,
                GCodeSnippet = sb.ToString(),
                ShowCoreWarningZone = hasCore
            };

            BuildHelicalCanvas(result, coreDia);
            return result;
        }

        private static void BuildHelicalCanvas(HelicalCalculationResult res, double coreDia)
        {
            const double cx = 160.0;
            const double cy = 100.0;
            const double maxHoleDiaPx = 152.0;

            double scale = maxHoleDiaPx / res.HoleDiameter;

            double holeDiaPx = maxHoleDiaPx;
            double orbitDiaPx = res.HelicalOrbitDiameter * scale;
            double toolDiaPx = res.ToolDiameter * scale;
            double coreDiaPx = coreDia * scale;

            res.OrbitCenterX = cx;
            res.OrbitCenterY = cy;

            res.HoleCircleDiaPx = holeDiaPx;
            res.HoleCircleLeft = cx - holeDiaPx / 2.0;
            res.HoleCircleTop = cy - holeDiaPx / 2.0;

            res.OrbitCircleDiaPx = orbitDiaPx;
            res.OrbitCircleLeft = cx - orbitDiaPx / 2.0;
            res.OrbitCircleTop = cy - orbitDiaPx / 2.0;

            // Позиционируем фрезу справа на орбите (точка врезания X = X0 + R_орб)
            double toolCx = cx + orbitDiaPx / 2.0;
            double toolCy = cy;
            res.ToolCenterX = toolCx;
            res.ToolCenterY = toolCy;

            res.ToolCircleDiaPx = toolDiaPx;
            res.ToolCircleLeft = toolCx - toolDiaPx / 2.0;
            res.ToolCircleTop = toolCy - toolDiaPx / 2.0;

            res.CoreCircleDiaPx = coreDiaPx;
            res.CoreCircleLeft = cx - coreDiaPx / 2.0;
            res.CoreCircleTop = cy - coreDiaPx / 2.0;

            var inv = CultureInfo.InvariantCulture;
            res.HoleDiaLabelText = string.Format(inv, "D_отв = Ø{0:0.##}", res.HoleDiameter);
            res.ToolDiaLabelText = string.Format(inv, "D_фр = Ø{0:0.##}", res.ToolDiameter);
            res.OrbitRadLabelText = string.Format(inv, "I = -{0:0.##}", res.HelicalOrbitRadius);
        }

        private static HelicalCalculationResult ErrorHelical(string message) =>
            new() { IsValid = false, ErrorMessage = message };

        public MillingThreadCalculationResult CalculateMillingThread(
                                                bool isThreadMillingMode,
                                                bool isExternalThread,
                                                bool isLeftHandThread,
                                                double threadDiameter,
                                                double threadPitch,
                                                double toolDiameter,
                                                int toolFlutes,
                                                double centerX,
                                                double centerY,
                                                double startZ,
                                                double targetZ,
                                                double clearanceR,
                                                double edgeFeedPerMin,
                                                int spindleRpm)
        {
            if (threadDiameter <= 0.5)
                return ErrorMillingThread("Номинальный диаметр резьбы D должен быть больше 0,5 мм.");
            if (threadPitch <= 0.05 || threadPitch > 20.0)
                return ErrorMillingThread("Шаг резьбы P должен быть в пределах (0.05; 20] мм.");
            if (spindleRpm < 10)
                return ErrorMillingThread("Обороты шпинделя S должны быть больше 0.");

            double depth = startZ - targetZ;
            if (depth <= 0.2)
                return ErrorMillingThread("Конечная координата резьбы Z_кон должна быть ниже плоскости Z_нач.");

            var warnings = new List<string>();
            var inv = CultureInfo.InvariantCulture;

            // Рекомендуемый диаметр отверстия под метрическую резьбу (D - 1.0825 * P)
            double preHoleDia = Math.Round(Math.Max(0.2, threadDiameter - 1.0825 * threadPitch), 2);
            double minorRadiusOffset = 0.54127 * threadPitch; // высота профиля резьбы H1

            var sb = new StringBuilder();
            double orbitRadius = 0.0;
            double programmedFeed = 0.0;
            double timeSec = 0.0;
            string directionText;

            if (!isThreadMillingMode)
            {
                // =============================================================
                // РЕЖИМ 1: ЖЁСТКОЕ НАРЕЗАНИЕ РЕЗЬБЫ МЕТЧИКОМ (M29 + G84)
                // =============================================================
                if (isExternalThread)
                    warnings.Add("Цикл G84 применяется для внутренних отверстий (для наружной резьбы переключитесь на «Резьбофрезерование»).");

                // Строгая синхронизация подачи со шпинделем: Vf = S * P (мм/мин)
                programmedFeed = Math.Round(spindleRpm * threadPitch, 2);
                string tapCycle = isLeftHandThread ? "G74" : "G84";
                directionText = isLeftHandThread
                    ? "Левая резьба — левый цикл G74 (реверс M04)"
                    : "Правая резьба — жёсткий цикл M29 + G84";

                // Время: рабочий ход вниз + реверсный выход вверх + ~1.2 сек на разгон/торможение шпинделя
                double stroke = depth + Math.Max(1.0, clearanceR);
                timeSec = ((60.0 * stroke * 2.0) / programmedFeed) + 1.2;

                sb.AppendFormat(inv, "(RIGID TAPPING M{0:0.##}x{1:0.##} - PRE-HOLE D{2:0.##})\n", threadDiameter, threadPitch, preHoleDia);
                sb.AppendFormat(inv, "G00 X{0:0.###} Y{1:0.###}\n", centerX, centerY);
                sb.AppendFormat(inv, "G00 Z{0:0.###}\n", startZ + Math.Max(2.0, clearanceR));
                sb.AppendFormat(inv, "M29 S{0}\n", spindleRpm);
                sb.AppendFormat(inv, "G98 {0} X{1:0.###} Y{2:0.###} Z{3:0.###} R{4:0.###} F{5:0.##}\n",
                    tapCycle, centerX, centerY, targetZ, startZ + Math.Max(2.0, clearanceR), programmedFeed);
                sb.Append("G80");
            }
            else
            {
                // =============================================================
                // РЕЖИМ 2: РЕЗЬБОФРЕЗЕРОВАНИЕ (THREAD MILLING)
                // =============================================================
                if (toolDiameter <= 0.2)
                    return ErrorMillingThread("Диаметр резьбофрезы D_фр должен быть больше 0,2 мм.");
                if (edgeFeedPerMin <= 0)
                    return ErrorMillingThread("Минутная подача Vf кромки должна быть больше 0.");

                if (!isExternalThread)
                {
                    if (toolDiameter >= preHoleDia)
                        return ErrorMillingThread($"Диаметр резьбофрезы D_фр ({toolDiameter:0.##} мм) должен быть меньше диаметра отверстия под резьбу ({preHoleDia:0.##} мм).");

                    if (toolDiameter > threadDiameter * 0.80)
                    {
                        warnings.Add(string.Format(inv,
                            "Крупная резьбофреза (D_фр / D = {0:0.##} > 0.80): возможен подрез профиля витка при выходе по дуге — рекомендуется фреза Ø{1:0.#}–{2:0.#} мм.",
                            toolDiameter / threadDiameter, threadDiameter * 0.55, threadDiameter * 0.72));
                    }

                    orbitRadius = (threadDiameter - toolDiameter) / 2.0;
                    programmedFeed = Math.Max(1.0, Math.Round(edgeFeedPerMin * ((threadDiameter - toolDiameter) / threadDiameter), 1));
                }
                else
                {
                    orbitRadius = (threadDiameter + toolDiameter) / 2.0;
                    programmedFeed = Math.Max(1.0, Math.Round(edgeFeedPerMin * ((threadDiameter + toolDiameter) / threadDiameter), 1));
                }

                // Попутное фрезерование:
                // Внутренняя правая -> G03 снизу вверх (+Z); внутренняя левая -> G02 сверху вниз (-Z)
                // Наружная правая -> G02 сверху вниз (-Z); наружная левая -> G03 снизу вверх (+Z)
                bool bottomUp = (!isExternalThread && !isLeftHandThread) || (isExternalThread && isLeftHandThread);
                string gHelix = bottomUp ? "G03" : "G02";
                string gArcLead = !isExternalThread ? gHelix : (gHelix == "G03" ? "G02" : "G03");

                directionText = bottomUp
                    ? $"Попутное ({gHelix}): врезание снизу вверх (+Z) — стружка не подминается"
                    : $"Попутное ({gHelix}): врезание сверху вниз (-Z)";

                double turns = depth / threadPitch;
                double helixLength = turns * 2.0 * Math.PI * orbitRadius;
                timeSec = ((60.0 * helixLength) / programmedFeed) + 0.8;

                double leadRadius = Math.Max(0.3, orbitRadius / 2.0);
                double arcStartX = Math.Round(centerX + (orbitRadius - leadRadius), 3);
                double targetX = Math.Round(centerX + orbitRadius, 3);
                double zStartHelix = bottomUp ? Math.Round(targetZ, 3) : Math.Round(startZ, 3);
                double zEndHelix = bottomUp ? Math.Round(startZ + threadPitch * 0.5, 3) : Math.Round(targetZ, 3);

                sb.AppendFormat(inv, "(THREAD MILLING {0}{1:0.##}x{2:0.##} - TOOL D{3:0.##})\n",
                    isExternalThread ? "EXT M" : "INT M", threadDiameter, threadPitch, toolDiameter);

                if (!isExternalThread)
                {
                    sb.AppendFormat(inv, "G00 X{0:0.###} Y{1:0.###}\n", centerX, centerY);
                    sb.AppendFormat(inv, "G00 Z{0:0.###}\n", startZ + Math.Max(2.0, clearanceR));
                    sb.AppendFormat(inv, "G01 Z{0:0.###} F{1:0}\n", zStartHelix, Math.Round(programmedFeed * 2.5, 0));
                    sb.AppendFormat(inv, "G01 X{0:0.###} Y{1:0.###} F{2:0.#}\n", arcStartX, centerY, programmedFeed);
                    sb.AppendFormat(inv, "{0} X{1:0.###} Y{2:0.###} I{3:0.###} J0 (ARC LEAD-IN)\n",
                        gArcLead, targetX, centerY, Math.Round(leadRadius / 2.0, 3));
                    sb.AppendFormat(inv, "{0} X{1:0.###} Y{2:0.###} Z{3:0.###} I{4:0.###} J0\n",
                        gHelix, targetX, centerY, zEndHelix, Math.Round(-orbitRadius, 3));
                    sb.AppendFormat(inv, "{0} X{1:0.###} Y{2:0.###} I{3:0.###} J0 (ARC LEAD-OUT)\n",
                        gArcLead, arcStartX, centerY, Math.Round(-leadRadius / 2.0, 3));
                    sb.AppendFormat(inv, "G01 X{0:0.###} Y{1:0.###}\n", centerX, centerY);
                    sb.AppendFormat(inv, "G00 Z{0:0.###}", startZ + Math.Max(2.0, clearanceR));
                }
                else
                {
                    double safeOutX = Math.Round(centerX + orbitRadius + 3.0, 3);
                    sb.AppendFormat(inv, "G00 X{0:0.###} Y{1:0.###}\n", safeOutX, centerY);
                    sb.AppendFormat(inv, "G00 Z{0:0.###}\n", startZ + Math.Max(2.0, clearanceR));
                    sb.AppendFormat(inv, "G01 Z{0:0.###} F{1:0}\n", zStartHelix, Math.Round(programmedFeed * 2.5, 0));
                    sb.AppendFormat(inv, "G01 X{0:0.###} Y{1:0.###} F{2:0.#}\n", targetX, centerY, programmedFeed);
                    sb.AppendFormat(inv, "{0} X{1:0.###} Y{2:0.###} Z{3:0.###} I{4:0.###} J0\n",
                        gHelix, targetX, centerY, zEndHelix, Math.Round(-orbitRadius, 3));
                    sb.AppendFormat(inv, "G01 X{0:0.###} Y{1:0.###}\n", safeOutX, centerY);
                    sb.AppendFormat(inv, "G00 Z{0:0.###}", startZ + Math.Max(2.0, clearanceR));
                }
            }

            string timeText = timeSec < 60.0
                ? string.Format(inv, "{0:0.##} сек", timeSec)
                : string.Format(inv, "{0} мин {1:0.#} сек", (int)(timeSec / 60.0), timeSec % 60.0);

            var result = new MillingThreadCalculationResult
            {
                IsValid = true,
                HasWarning = warnings.Count > 0,
                WarningMessage = string.Join("\n", warnings),
                IsThreadMillingMode = isThreadMillingMode,
                IsExternalThread = isExternalThread,
                IsLeftHandThread = isLeftHandThread,
                ThreadDiameter = Math.Round(threadDiameter, 3),
                ThreadPitch = Math.Round(threadPitch, 3),
                RecommendedPreHoleDiameter = preHoleDia,
                ThreadDepthMm = Math.Round(depth, 3),
                ToolDiameter = isThreadMillingMode ? Math.Round(toolDiameter, 3) : preHoleDia,
                OrbitRadius = Math.Round(orbitRadius, 3),

                EdgeFeedPerMin = Math.Round(edgeFeedPerMin, 1),
                ProgrammedFeedPerMin = programmedFeed,
                SpindleRpm = spindleRpm,
                DirectionSummaryText = directionText,
                MachiningTimeSeconds = Math.Round(timeSec, 2),
                MachiningTimeText = timeText,
                GCodeSnippet = sb.ToString()
            };
            
            BuildMillingThreadCanvas(result, minorRadiusOffset, centerX, centerY);

            return result;
        }
        private static void BuildMillingThreadCanvas(
            MillingThreadCalculationResult res,
            double minorOffset,
            double centerX,
            double centerY)
        {
            const double cx = 160.0;
            const double cy = 100.0;
            double outerRealDia = res.IsThreadMillingMode && res.IsExternalThread
            ? res.ThreadDiameter + 2.0 * res.ToolDiameter
            : res.ThreadDiameter;
            double scale = 148.0 / Math.Max(outerRealDia, 1.0);
            double threadDiaPx = res.ThreadDiameter * scale;
            double coreDiaReal = res.IsExternalThread
            ? Math.Max(0.2, res.ThreadDiameter - 2.0 * minorOffset)
            : res.RecommendedPreHoleDiameter;
            double coreDiaPx = coreDiaReal * scale;
            res.ThreadCircleDiaPx = threadDiaPx;
            res.ThreadCircleLeft = cx - threadDiaPx / 2.0;
            res.ThreadCircleTop = cy - threadDiaPx / 2.0;
            res.CoreCircleDiaPx = coreDiaPx;
            res.CoreCircleLeft = cx - coreDiaPx / 2.0;
            res.CoreCircleTop = cy - coreDiaPx / 2.0;
            if (res.IsThreadMillingMode)
            {
                double toolDiaPx = res.ToolDiameter * scale;
                double orbitPx = res.OrbitRadius * scale;
                res.ToolCircleDiaPx = toolDiaPx;
                res.ToolCircleLeft = (cx + orbitPx) - toolDiaPx / 2.0;
                res.ToolCircleTop = cy - toolDiaPx / 2.0;
            }
            else
            {
                res.ToolCircleDiaPx = coreDiaPx;
                res.ToolCircleLeft = cx - coreDiaPx / 2.0;
                res.ToolCircleTop = cy - coreDiaPx / 2.0;
            }
            
            var inv = CultureInfo.InvariantCulture;
            res.ThreadLabelText = string.Format(inv, "Резьба: M{0:0.##}×{1:0.##}", res.ThreadDiameter, res.ThreadPitch);
            res.PreHoleLabelText = string.Format(inv, "Отв. под резьбу: Ø{0:0.##}", res.RecommendedPreHoleDiameter);
            res.ToolLabelText = res.IsThreadMillingMode
                ? string.Format(inv, "Фреза: Ø{0:0.##}", res.ToolDiameter)
                : "Метчик: G84";
            res.CenterLabelText = string.Format(inv, "X{0:0.##} Y{1:0.##}", centerX, centerY);
        }
        private static MillingThreadCalculationResult ErrorMillingThread(string message) =>
        new() { IsValid = false, ErrorMessage = message };

        public ScallopCalculationResult CalculateScallopHeight(
    bool isByTargetRoughness,
    bool isTargetRaMode,
    bool isBallNose,
    double toolDiameter,
    double cornerRadius,
    double targetValueMicrons,
    double inputStepoverAe,
    double feedPerMin)
        {
            if (toolDiameter <= 0.1 || toolDiameter > 250.0)
                return ErrorScallop("Диаметр фрезы D должен быть в пределах (0.1; 250] мм.");

            double r = isBallNose ? (toolDiameter / 2.0) : cornerRadius;
            if (!isBallNose)
            {
                if (r <= 0.05)
                    return ErrorScallop("Радиус при вершине r для торовой фрезы (Bull Nose) должен быть больше 0,05 мм.");
                if (r >= toolDiameter / 2.0)
                    return ErrorScallop("Для торовой фрезы радиус r должен быть меньше D / 2 (иначе выберите тип «Сферическая Ball Nose»).");
            }

            if (feedPerMin <= 0)
                return ErrorScallop("Минутная подача Vf должна быть больше 0.");

            double flatCenterWidth = isBallNose ? 0.0 : Math.Max(0.0, toolDiameter - 2.0 * r);
            var warnings = new List<string>();
            var inv = CultureInfo.InvariantCulture;

            double stepoverAe;
            double scallopMicrons;

            if (isByTargetRoughness)
            {
                if (targetValueMicrons <= 0.05 || targetValueMicrons > 500.0)
                    return ErrorScallop("Целевая высота гребешка / шероховатость должна быть в пределах (0.05; 500] мкм.");

                // Для 3D-фрезерования сферической фрезой Ra ≈ 0.28 * h
                double targetHMicrons = isTargetRaMode
                    ? (targetValueMicrons / 0.28)
                    : targetValueMicrons;

                double hMm = targetHMicrons / 1000.0;
                if (hMm >= r)
                    return ErrorScallop("Заданная высота гребешка h превышает радиус режущей кромки r.");

                double arcSpan = 2.0 * Math.Sqrt(Math.Max(0.0, 2.0 * r * hMm - hMm * hMm));
                stepoverAe = flatCenterWidth + arcSpan;

                if (stepoverAe > toolDiameter * 0.95)
                {
                    stepoverAe = toolDiameter * 0.95;
                    warnings.Add("Расчётный шаг строчки ограничен 95% от диаметра фрезы D, чтобы избежать несрезанных участков между строками.");
                }

                double effSpan = Math.Max(0.0, stepoverAe - flatCenterWidth);
                double actualHMm = r - Math.Sqrt(Math.Max(0.0, r * r - Math.Pow(effSpan / 2.0, 2)));
                scallopMicrons = actualHMm * 1000.0;
            }
            else
            {
                if (inputStepoverAe <= 0.005 || inputStepoverAe >= toolDiameter)
                    return ErrorScallop("Шаг строчки ae должен быть больше 0 и меньше диаметра фрезы D.");

                stepoverAe = inputStepoverAe;

                if (stepoverAe <= flatCenterWidth)
                {
                    scallopMicrons = 0.0;
                }
                else
                {
                    double effSpan = stepoverAe - flatCenterWidth;
                    if (effSpan >= 2.0 * r)
                        return ErrorScallop("Шаг строчки ae слишком велик для данного радиуса r — дуги соседних проходов не перекрываются.");

                    double hMm = r - Math.Sqrt(Math.Max(0.0, r * r - Math.Pow(effSpan / 2.0, 2)));
                    scallopMicrons = hMm * 1000.0;
                }
            }

            double estRa = scallopMicrons * 0.28;
            double stepPercent = (stepoverAe / toolDiameter) * 100.0;

            if (stepoverAe < 0.03)
            {
                warnings.Add(string.Format(inv,
                    "Очень мелкий шаг строчки ae = {0:0.###} мм: время обработки многократно возрастает, а стойкость кромки снижается из-за трения.",
                    stepoverAe));
            }

            string qualityText = estRa switch
            {
                <= 0.45 => "Зеркальная 3D-поверхность (под полировку, Ra ≤ 0.4)",
                <= 0.85 => "Высокочистовая 3D-обработка (формообразующие, Ra 0.8)",
                <= 1.65 => "Чистовое 3D-фрезерование (Ra 1.6)",
                <= 3.30 => "Получистовое 3D-фрезерование (Ra 3.2)",
                _ => "Черновая / предварительная выборка 3D-профиля"
            };

            // Расчёт пути (в метрах) и машинного времени на квадрат 100 x 100 мм
            double passesPer100Mm = 100.0 / stepoverAe;
            double pathMeters = (passesPer100Mm * 100.0) / 1000.0;
            double timeSec = (60.0 * (pathMeters * 1000.0)) / feedPerMin;

            string timeText;
            if (timeSec < 60.0)
            {
                timeText = string.Format(inv, "{0:0.#} сек", timeSec);
            }
            else if (timeSec < 3600.0)
            {
                timeText = string.Format(inv, "{0} мин {1:0} сек", (int)(timeSec / 60.0), timeSec % 60.0);
            }
            else
            {
                int hrs = (int)(timeSec / 3600.0);
                int mins = (int)((timeSec % 3600.0) / 60.0);
                timeText = string.Format(inv, "{0} ч {1} мин", hrs, mins);
            }

            var result = new ScallopCalculationResult
            {
                IsValid = true,
                HasWarning = warnings.Count > 0,
                WarningMessage = string.Join("\n", warnings),
                IsBallNose = isBallNose,
                ToolDiameter = Math.Round(toolDiameter, 3),
                CornerRadius = Math.Round(r, 3),
                StepoverAeMm = Math.Round(stepoverAe, 3),
                StepoverPercent = Math.Round(stepPercent, 1),
                ScallopHeightMicrons = Math.Round(scallopMicrons, 2),
                EstimatedRaMicrons = Math.Round(estRa, 2),
                SurfaceQualityClassText = qualityText,
                PathLengthPer100x100Meters = Math.Round(pathMeters, 2),
                MachiningTimePer100x100Seconds = Math.Round(timeSec, 1),
                MachiningTimePer100x100Text = timeText
            };

            BuildScallopCanvas(result, r, stepoverAe, flatCenterWidth);
            return result;
        }

        private static void BuildScallopCanvas(
            ScallopCalculationResult res,
            double r,
            double ae,
            double flatW)
        {
            const double canvasW = 320.0;
            const double valleyY = 148.0;
            const double baseMetalY = 176.0;

            double effSpan = Math.Max(0.01, ae - flatW);
            double ratio = Math.Clamp(effSpan / (2.0 * r), 0.15, 0.92);

            double spanPx = Math.Clamp(64.0 + ratio * 56.0, 68.0, 124.0);
            double heightPx = Math.Clamp(16.0 + ratio * 44.0, 16.0, 58.0);
            double peakY = valleyY - heightPx;

            double arcR = (spanPx * spanPx) / (8.0 * heightPx) + (heightPx / 2.0);
            arcR = Math.Max(arcR, spanPx * 0.55);

            double leftCenterX = (canvasW / 2.0) - (spanPx / 2.0);
            double rightCenterX = (canvasW / 2.0) + (spanPx / 2.0);
            double peakX = canvasW / 2.0;

            res.LeftToolCenterX = leftCenterX;
            res.RightToolCenterX = rightCenterX;
            res.ToolCenterY = valleyY - arcR;
            res.ToolArcRadiusPx = arcR;
            res.PeakLineY = peakY;
            res.ValleyLineY = valleyY;

            double startX = leftCenterX - spanPx * 0.55;
            double endX = rightCenterX + spanPx * 0.55;

            var inv = CultureInfo.InvariantCulture;
            res.MetalProfilePathData = string.Format(inv,
                "M {0:F1},{1:F1} L {0:F1},{2:F1} A {3:F1},{3:F1} 0 0 0 {4:F1},{2:F1} A {3:F1},{3:F1} 0 0 0 {5:F1},{2:F1} L {5:F1},{1:F1} Z",
                startX, baseMetalY, peakY, arcR, peakX, endX);

            res.StepoverLabelText = string.Format(inv, "ae = {0:0.###} мм", res.StepoverAeMm);
            res.ScallopLabelText = string.Format(inv, "h = {0:0.##} мкм", res.ScallopHeightMicrons);
            res.RadiusLabelText = string.Format(inv, "r = {0:0.##} мм", res.CornerRadius);
        }

        private static ScallopCalculationResult ErrorScallop(string message) =>
            new() { IsValid = false, ErrorMessage = message };

    }
}