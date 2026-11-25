using System;
using System.Collections.Generic;
using System.Globalization;
using MechCore.Models.Geometry;

namespace MechCore.Services.Geometry;

public class GeometrySolverService : IGeometryService
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    private static (double TimeSeconds, string FormattedText) ComputeMachiningTime(
    double pathLengthMm,
    double feedPerRevMm,
    int spindleRpm)
    {
        if (pathLengthMm <= 0 || feedPerRevMm <= 0 || spindleRpm <= 0)
            return (0.0, "—");

        // Минутная подача Vf = f * S (мм/мин), время в секундах T = 60 * L / Vf
        double vfMmPerMin = feedPerRevMm * spindleRpm;
        double seconds = (60.0 * pathLengthMm) / vfMmPerMin;

        var inv = CultureInfo.InvariantCulture;
        if (seconds < 60.0)
        {
            return (Math.Round(seconds, 2), string.Format(inv, "{0:0.##} сек", seconds));
        }

        int mins = (int)(seconds / 60.0);
        double remSec = seconds - mins * 60.0;
        return (Math.Round(seconds, 1), string.Format(inv, "{0} мин {1:0.#} сек", mins, remSec));
    }


    public IReadOnlyList<DrillAngleItem> GetDrillAnglePresets()
    {
        return new List<DrillAngleItem>
        {
            new()
            {
                Name = "118° — Стандарт (HSS)",
                AngleDegrees = 118.0,
                Description = "Углеродистые и конструкционные стали, чугун, цветные металлы",
                IsCustom = false
            },
            new()
            {
                Name = "135° — Нержавейка / Твёрдые",
                AngleDegrees = 135.0,
                Description = "Нержавеющие, жаропрочные и легированные стали (HSS-Co)",
                IsCustom = false
            },
            new()
            {
                Name = "140° — Твёрдый сплав (VHM)",
                AngleDegrees = 140.0,
                Description = "Монолитные твёрдосплавные свёрла с внутренним подводом СОЖ",
                IsCustom = false
            },
            new()
            {
                Name = "Произвольный угол...",
                AngleDegrees = 120.0,
                Description = "Ручной ввод угла при вершине (например, 90° для центровок или 150°)",
                IsCustom = true
            }
        };
    }
    // =========================================================================
    // РАЗДЕЛ 4: ТОКАРНЫЙ КОНУС ЧПУ И ПРЯМОУГОЛЬНЫЙ ТРЕУГОЛЬНИК (КАРТОЧКА 4)
    // =========================================================================

    public TriangleCalculationResult CalculateCncCone(
                                    ConeSolveKind solveKind,
                                    double startDiameter,
                                    double endDiameter,
                                    double length,
                                    double slopeAngleDeg,
                                    double startZ,
                                    bool isDecreasingX,
                                    bool isLeftToRight,
                                    bool isHole,
                                    bool useFeed = true,
                                    double feedRate = 0.15,
                                    int spindleRpm = 1200)
    {
        if (startDiameter < 0)
            return ErrorTriangle("Начальный диаметр D_нач не может быть отрицательным.");
        if (useFeed && feedRate <= 0)
            return ErrorTriangle("Рабочая подача F должна быть больше 0.");

        double d1 = startDiameter;
        double d2 = endDiameter;
        double len = length;
        double alphaDeg = slopeAngleDeg;
        double signX = isDecreasingX ? -1.0 : 1.0;

        switch (solveKind)
        {
            case ConeSolveKind.ByTwoDiametersAndLength:
                if (d2 < 0) return ErrorTriangle("Конечный диаметр D_кон не может быть отрицательным.");
                if (len <= 0) return ErrorTriangle("Длина конуса L должна быть больше 0.");
                double radialDiff1 = Math.Abs(d2 - d1) / 2.0;
                alphaDeg = Math.Atan2(radialDiff1, len) * RadToDeg;
                break;

            case ConeSolveKind.ByStartDiameterLengthAndAngle:
                if (len <= 0) return ErrorTriangle("Длина конуса L должна быть больше 0.");
                if (alphaDeg <= 0 || alphaDeg >= 89.9)
                    return ErrorTriangle("Угол уклона α должен быть в пределах (0°; 90°).");
                double deltaR2 = len * Math.Tan(alphaDeg * DegToRad);
                d2 = d1 + 2.0 * deltaR2 * signX;
                break;

            case ConeSolveKind.ByTwoDiametersAndAngle:
                if (d2 < 0) return ErrorTriangle("Конечный диаметр D_кон не может быть отрицательным.");
                if (alphaDeg <= 0 || alphaDeg >= 89.9)
                    return ErrorTriangle("Угол уклона α должен быть в пределах (0°; 90°).");
                double radialDiff3 = Math.Abs(d2 - d1) / 2.0;
                if (radialDiff3 < 1e-6)
                    return ErrorTriangle("Для расчёта длины по углу диаметры D_нач и D_кон должны различаться.");
                len = radialDiff3 / Math.Tan(alphaDeg * DegToRad);
                break;
        }

        double dirZ = isLeftToRight ? 1.0 : -1.0;
        double endZ = startZ + len * dirZ;

        double deltaD = Math.Abs(d2 - d1);
        double conicity = len > 1e-6 ? deltaD / len : 0.0;
        double slope = conicity / 2.0; // Уклон на сторону S = tg(α) = K / 2
        var inv = CultureInfo.InvariantCulture;

        string conicityRatio = conicity > 1e-6
            ? string.Format(inv, "1 : {0:0.###}", 1.0 / conicity)
            : "0 (цилиндр)";

        string conicityMultiUnits = conicity > 1e-6
            ? string.Format(inv, "{0:0.####}  |  {1:0.##} %  |  {2:0.##} мм / 100 мм",
                conicity, conicity * 100.0, conicity * 100.0)
            : "0.0000  |  0.00 %  |  0.00 мм / 100 мм";

        string slopeMultiUnits = slope > 1e-6
            ? string.Format(inv, "1 : {0:0.###}  ({1:0.####}  |  {2:0.##} %)",
                1.0 / slope, slope, slope * 100.0)
            : "0 (цилиндр)";

        string standardMatch = DetectStandardCone(conicity, alphaDeg);

        double radialStep = deltaD / 2.0;
        double generatrix = Math.Sqrt(len * len + radialStep * radialStep);

        var warnings = new List<string>();
        if (d2 < 0)
            warnings.Add($"Конечный диаметр D_кон ({d2:0.###} мм) уходит за ось симметрии (< 0).");
        if (Math.Abs(d2 - d1) < 1e-6)
            warnings.Add("Начальный и конечный диаметры равны (уклон 0° — цилиндрический участок).");

        if (useFeed && feedRate >= 0.8)
            warnings.Add($"Критически высокая подача F = {feedRate:0.###} мм/об — проверьте десятичную точку (например, 0.15 вместо 15).");
        else if (useFeed && feedRate > 0.35)
            warnings.Add($"Повышенная подача F = {feedRate:0.###} мм/об при точении конуса — возможны следы дробления и ступенчатость профиля.");

        double approachFeed = Math.Round(feedRate * 1.5, 3);
        string linearFeedPart = useFeed ? string.Format(inv, " F{0:0.###}", approachFeed) : string.Empty;
        string coneFeedPart = useFeed ? string.Format(inv, " F{0:0.###}", feedRate) : string.Empty;

        string gcode = string.Format(inv, "G01 X{0:0.###} Z{1:0.###}{4}\nG01 X{2:0.###} Z{3:0.###}{5}",
            d1, startZ, d2, endZ, linearFeedPart, coneFeedPart);

        int safeRpm = spindleRpm > 0 ? spindleRpm : 1200;
        double effectiveFeed = useFeed ? feedRate : 0.15;
        var (machiningSec, machiningText) = ComputeMachiningTime(generatrix, effectiveFeed, safeRpm);

        var result = new TriangleCalculationResult
        {
            IsValid = true,
            IsTriangleMode = false,
            HasWarning = warnings.Count > 0,
            WarningMessage = string.Join("\n", warnings),
            IsHole = isHole,
            IsLeftToRight = isLeftToRight,
            ConeStartDiameter = Math.Round(d1, 3),
            ConeEndDiameter = Math.Round(d2, 3),
            ConeLength = Math.Round(len, 3),
            ConeSlopeAngleDeg = Math.Round(alphaDeg, 3),
            ConeFullAngleDeg = Math.Round(alphaDeg * 2.0, 3),
            ConicityValue = Math.Round(conicity, 5),
            ConicityRatioText = conicityRatio,
            ConicityMultiUnitsText = conicityMultiUnits,
            SlopeMultiUnitsText = slopeMultiUnits,
            HasStandardConeMatch = !string.IsNullOrEmpty(standardMatch),
            StandardConeMatchText = standardMatch,
            ConeGeneratrixLength = Math.Round(generatrix, 3),
            ConeStartZ = Math.Round(startZ, 3),
            ConeEndZ = Math.Round(endZ, 3),
            ConeGCodeSnippet = gcode,
            SpindleRpmUsed = safeRpm,
            MachiningTimeSeconds = machiningSec,
            MachiningTimeText = machiningText
        };

        BuildConeCanvasCoordinates(result);
        return result;
    }

    private static string DetectStandardCone(double conicity, double alphaDeg)
    {
        if (conicity <= 1e-6) return string.Empty;

        // База стандартных конусностей: ГОСТ 25557 (Морзе), ГОСТ 8593, ГОСТ 15945 (7:24), ГОСТ 6211
        var standards = new (string Name, double StdConicity)[]
        {
            ("Конус Морзе №0 (КМ0, ГОСТ 25557)", 1.0 / 19.212),
            ("Конус Морзе №1 (КМ1, ГОСТ 25557)", 1.0 / 20.047),
            ("Конус Морзе №2 (КМ2, ГОСТ 25557)", 1.0 / 20.020),
            ("Конус Морзе №3 (КМ3, ГОСТ 25557)", 1.0 / 19.922),
            ("Конус Морзе №4 (КМ4, ГОСТ 25557)", 1.0 / 19.254),
            ("Конус Морзе №5 (КМ5, ГОСТ 25557)", 1.0 / 19.002),
            ("Конус Морзе №6 (КМ6, ГОСТ 25557)", 1.0 / 19.180),
            ("Метрический конус 1:20 (ГОСТ 25557)", 1.0 / 20.0),
            ("Инструментальный конус 7:24 (ISO 30/40/50, ГОСТ 15945)", 7.0 / 24.0),
            ("Конус 1:5 (ГОСТ 8593)", 1.0 / 5.0),
            ("Конус 1:10 (ГОСТ 8593)", 1.0 / 10.0),
            ("Трубный конус 1:16 (ГОСТ 6211 / NPT)", 1.0 / 16.0),
            ("Конический штифт 1:50 (ГОСТ 3129 / ГОСТ 8593)", 1.0 / 50.0)
        };

        // Сначала ищем точное совпадение (отклонение конусности < 0.15%)
        foreach (var item in standards)
        {
            double relErr = Math.Abs(conicity - item.StdConicity) / item.StdConicity;
            if (relErr <= 0.0015)
                return $"Соответствует стандарту: {item.Name}";
        }

        // Дополнительно проверяем популярные угловые стандарты (60°, 90°, 120° полного угла)
        if (Math.Abs(alphaDeg - 30.0) <= 0.03)
            return "Стандартный угол конуса 60° (центровые отверстия ГОСТ 14034)";
        if (Math.Abs(alphaDeg - 45.0) <= 0.03)
            return "Стандартный угол конуса 90° (фаски и зенковки ГОСТ 8593)";
        if (Math.Abs(alphaDeg - 60.0) <= 0.03)
            return "Стандартный угол конуса 120° (ГОСТ 8593)";

        return string.Empty;
    }

    private static void BuildConeCanvasCoordinates(TriangleCalculationResult res)
    {
        const double canvasW = 320.0;
        const double canvasH = 200.0;

        bool decreasing = res.ConeEndDiameter < res.ConeStartDiameter;
        double startY = decreasing ? 62.0 : 122.0;
        double endY = decreasing ? 122.0 : 62.0;
        if (Math.Abs(res.ConeEndDiameter - res.ConeStartDiameter) < 1e-6)
        {
            startY = 90.0;
            endY = 90.0;
        }

        double startX = 240.0;
        double endX = 85.0;

        // Положение зелёной линии ноля детали X = 0
        double deltaX = res.ConeEndDiameter - res.ConeStartDiameter;
        double exactZeroY = Math.Abs(deltaX) > 1e-6
            ? startY + (0.0 - res.ConeStartDiameter) * ((endY - startY) / deltaX)
            : 186.0;
        res.CenterLineY = Math.Clamp(exactZeroY, 14.0, 186.0);

        double leftWallX = 32.0;
        double rightWallX = startX;
        double bodyBoundY = res.IsHole
            ? 24.0
            : Math.Min(168.0, Math.Max(exactZeroY, Math.Max(startY, endY)));

        if (res.IsLeftToRight)
        {
            startX = canvasW - startX;
            endX = canvasW - endX;
            leftWallX = canvasW - 32.0;
            rightWallX = startX;
        }

        res.CanvasStartX = startX;
        res.CanvasStartY = startY;
        res.CanvasEndX = endX;
        res.CanvasEndY = endY;

        // Положение вертикальной зелёной линии Z = 0
        double deltaZ = res.ConeEndZ - res.ConeStartZ;
        double exactZeroX = Math.Abs(deltaZ) > 1e-6
            ? res.CanvasStartX + (0.0 - res.ConeStartZ) * ((res.CanvasEndX - res.CanvasStartX) / deltaZ)
            : res.CanvasStartX;
        res.ZeroLineX = Math.Clamp(exactZeroX, 14.0, canvasW - 14.0);

        var inv = CultureInfo.InvariantCulture;
        res.ConePartBodyPoints = string.Format(inv,
            "{0:F1},{1:F1} {0:F1},{2:F1} {3:F1},{4:F1} {5:F1},{4:F1} {5:F1},{1:F1}",
            rightWallX, bodyBoundY, startY, endX, endY, leftWallX);

        res.ConePartOutlinePoints = string.Format(inv,
            "{0:F1},{1:F1} {0:F1},{2:F1} {3:F1},{4:F1} {5:F1},{4:F1}",
            rightWallX, bodyBoundY, startY, endX, endY, leftWallX);

        // Подписи координат
        res.StartLabelText = string.Format(inv, "X{0:0.##} Z{1:0.##}", res.ConeStartDiameter, res.ConeStartZ);
        res.EndLabelText = string.Format(inv, "X{0:0.##} Z{1:0.##}", res.ConeEndDiameter, res.ConeEndZ);

        double offsetY = res.IsHole ? 8.0 : -20.0;
        res.LabelStartX = Math.Clamp(res.CanvasStartX + (res.IsLeftToRight ? -66.0 : -14.0), 6.0, canvasW - 84.0);
        res.LabelStartY = Math.Clamp(res.CanvasStartY + offsetY, 4.0, canvasH - 22.0);

        res.LabelEndX = Math.Clamp(res.CanvasEndX + (res.IsLeftToRight ? -10.0 : -62.0), 6.0, canvasW - 84.0);
        res.LabelEndY = Math.Clamp(res.CanvasEndY + offsetY, 4.0, canvasH - 22.0);

        // Вынесенный вектор подачи G01 (параллельно образующей конуса)
        double dx = res.CanvasEndX - res.CanvasStartX;
        double dy = res.CanvasEndY - res.CanvasStartY;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len > 1e-3)
        {
            double ux = dx / len;
            double uy = dy / len;
            double n1x = -uy, n1y = ux;
            double n2x = uy, n2y = -ux;

            double nx, ny;
            if (!res.IsHole)
            {
                if (n1y < -0.1) { nx = n1x; ny = n1y; }
                else { nx = n2x; ny = n2y; }
            }
            else
            {
                if (n1y > 0.1) { nx = n1x; ny = n1y; }
                else { nx = n2x; ny = n2y; }
            }

            const double offsetDist = 16.0;
            const double vectorLength = 54.0;
            double midX = (res.CanvasStartX + res.CanvasEndX) / 2.0 + nx * offsetDist;
            double midY = (res.CanvasStartY + res.CanvasEndY) / 2.0 + ny * offsetDist;

            double vStartX = midX - ux * (vectorLength / 2.0);
            double vStartY = midY - uy * (vectorLength / 2.0);
            double vEndX = midX + ux * (vectorLength / 2.0);
            double vEndY = midY + uy * (vectorLength / 2.0);

            res.FeedVectorStartX = vStartX;
            res.FeedVectorStartY = vStartY;
            res.FeedVectorEndX = vEndX;
            res.FeedVectorEndY = vEndY;

            const double arrowLen = 8.0;
            const double arrowHalfW = 3.5;
            double baseX = vEndX - ux * arrowLen;
            double baseY = vEndY - uy * arrowLen;
            double p1X = baseX + nx * arrowHalfW;
            double p1Y = baseY + ny * arrowHalfW;
            double p2X = baseX - nx * arrowHalfW;
            double p2Y = baseY - ny * arrowHalfW;

            res.FeedArrowHeadPoints = string.Format(inv, "{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{5:F1}",
                vEndX, vEndY, p1X, p1Y, p2X, p2Y);
        }
    }

    public TriangleCalculationResult SolveTriangle(
    TriangleSolveKind kind,
    double val1,
    double val2)
    {
        if (val1 <= 0 || val2 <= 0)
            return ErrorTriangle("Оба задаваемых параметра треугольника должны быть больше 0.");

        double a = 0, b = 0, c = 0, alphaDeg = 0, betaDeg = 0;

        switch (kind)
        {
            case TriangleSolveKind.TwoLegs:
                a = val1;
                b = val2;
                c = Math.Sqrt(a * a + b * b);
                alphaDeg = Math.Atan2(a, b) * RadToDeg;
                betaDeg = 90.0 - alphaDeg;
                break;

            case TriangleSolveKind.LegAndHypotenuse:
                a = val1;
                c = val2;
                if (a >= c)
                    return ErrorTriangle("Катет a должен быть строго меньше гипотенузы c.");
                b = Math.Sqrt(c * c - a * a);
                alphaDeg = Math.Asin(a / c) * RadToDeg;
                betaDeg = 90.0 - alphaDeg;
                break;

            case TriangleSolveKind.LegAndOppositeAngle:
                a = val1;
                alphaDeg = val2;
                if (alphaDeg >= 90.0)
                    return ErrorTriangle("Острый угол α должен быть меньше 90°.");
                betaDeg = 90.0 - alphaDeg;
                double sinA = Math.Sin(alphaDeg * DegToRad);
                c = a / sinA;
                b = a / Math.Tan(alphaDeg * DegToRad);
                break;

            case TriangleSolveKind.LegAndAdjacentAngle:
                a = val1;
                betaDeg = val2;
                if (betaDeg >= 90.0)
                    return ErrorTriangle("Острый угол β должен быть меньше 90°.");
                alphaDeg = 90.0 - betaDeg;
                b = a * Math.Tan(betaDeg * DegToRad);
                c = a / Math.Cos(betaDeg * DegToRad);
                break;

            case TriangleSolveKind.HypotenuseAndAngle:
                c = val1;
                alphaDeg = val2;
                if (alphaDeg >= 90.0)
                    return ErrorTriangle("Острый угол α должен быть меньше 90°.");
                betaDeg = 90.0 - alphaDeg;
                a = c * Math.Sin(alphaDeg * DegToRad);
                b = c * Math.Cos(alphaDeg * DegToRad);
                break;
        }

        double area = 0.5 * a * b;

        var result = new TriangleCalculationResult
        {
            IsValid = true,
            IsTriangleMode = true,
            SideA = Math.Round(a, 3),
            SideB = Math.Round(b, 3),
            HypotenuseC = Math.Round(c, 3),
            AngleAlphaDeg = Math.Round(alphaDeg, 3),
            AngleBetaDeg = Math.Round(betaDeg, 3),
            Area = Math.Round(area, 3)
        };

        BuildTriangleCanvasCoordinates(result);
        return result;
    }
   
    private static TriangleCalculationResult ErrorTriangle(string msg) =>
        new() { IsValid = false, ErrorMessage = msg };

    public BlindHoleCalculationResult CalculateBlindHole(
     double diameter,
     double tipAngleDegrees,
     double inputDepth,
     double startZ,
     double retractR,
     bool isTotalDepthInput,
     bool isVertical,
     bool roundSchemeValues = false,
     bool useFeed = true,
     double feedRate = 0.15,
     int spindleRpm = 1000)
    {
        if (diameter <= 0)
            return ErrorHole("Диаметр сверла D должен быть больше 0.");
        if (tipAngleDegrees <= 0 || tipAngleDegrees >= 180)
            return ErrorHole("Угол при вершине сверла 2φ должен быть в пределах (0°; 180°).");
        if (inputDepth <= 0)
            return ErrorHole("Глубина сверления должна быть больше 0.");
        if (useFeed && feedRate <= 0)
            return ErrorHole("Рабочая подача F должна быть больше 0.");

        double halfAngleRad = (tipAngleDegrees / 2.0) * DegToRad;
        double coneHeight = (diameter / 2.0) / Math.Tan(halfAngleRad);

        double cylDepth, totalDepth;
        var warnings = new List<string>();

        if (isTotalDepthInput)
        {
            totalDepth = inputDepth;
            cylDepth = totalDepth - coneHeight;
            if (cylDepth < -1e-6)
            {
                warnings.Add($"Внимание: заданная полная глубина ({totalDepth:0.###} мм) меньше высоты заборного конуса сверла h ({coneHeight:0.###} мм) — отверстие не выйдет на полный диаметр D.");
                cylDepth = 0;
            }
        }
        else
        {
            cylDepth = inputDepth;
            totalDepth = cylDepth + coneHeight;
        }

        if (useFeed && feedRate >= 1.0)
        {
            warnings.Add($"Критически высокая подача F = {feedRate:0.###} мм/об — проверьте десятичную точку (например, 0.15 вместо 15).");
        }
        else if (useFeed && feedRate > diameter * 0.045)
        {
            warnings.Add($"Повышенная подача F = {feedRate:0.###} мм/об для сверла Ø{diameter:0.##} мм — высокий риск поломки сверла или увода оси.");
        }

        double endZ = startZ - totalDepth;
        double safeR = retractR > 0 ? retractR : 2.0;
        double peckQ = Math.Round(diameter * 0.5, 2);

        var inv = CultureInfo.InvariantCulture;
        string feedPart = useFeed ? string.Format(inv, " F{0:0.###}", feedRate) : string.Empty;

        string g81 = string.Format(inv, "G81 Z{0:0.###} R{1:0.###}{2}", endZ, safeR, feedPart);
        string g83 = string.Format(inv, "G83 Z{0:0.###} R{1:0.###} Q{2:0.###}{3}", endZ, safeR, peckQ, feedPart);

        int safeRpm = spindleRpm > 0 ? spindleRpm : 1000;
        double effectiveFeed = useFeed ? feedRate : 0.15;
        var (machiningSec, machiningText) = ComputeMachiningTime(totalDepth, effectiveFeed, safeRpm);

        var result = new BlindHoleCalculationResult
        {
            IsValid = true,
            HasWarning = warnings.Count > 0,
            WarningMessage = string.Join("\n", warnings),
            IsTotalDepthInput = isTotalDepthInput,
            IsVertical = isVertical,
            Diameter = Math.Round(diameter, 3),
            TipAngleDegrees = Math.Round(tipAngleDegrees, 2),
            ConeHeight = Math.Round(coneHeight, 3),
            CylindricalDepth = Math.Round(cylDepth, 3),
            TotalDepth = Math.Round(totalDepth, 3),
            StartZ = Math.Round(startZ, 3),
            EndZ = Math.Round(endZ, 3),
            PeckStepQ = peckQ,
            G81Snippet = g81,
            G83Snippet = g83,
            SpindleRpmUsed = safeRpm,
            MachiningTimeSeconds = machiningSec,
            MachiningTimeText = machiningText
        };

        BuildBlindHoleCanvasCoordinates(result, roundSchemeValues);
        return result;
    }


    private static void BuildTriangleCanvasCoordinates(TriangleCalculationResult res)
    {
        // Рабочая область Canvas: 320 x 200 (отступы 42 и 32 px по краям)
        const double canvasW = 320.0;
        const double canvasH = 200.0;
        const double padX = 42.0;
        const double padY = 32.0;

        double a = res.SideA;
        double b = res.SideB;

        // Классическое расположение прямоугольного треугольника: прямой угол C снизу слева
        double pCx = 0, pCy = 0;
        double pBx = b, pBy = 0; // горизонтальный катет b
        double pAx = 0, pAy = a; // вертикальный катет a

        double shapeW = Math.Max(b, 1e-6);
        double shapeH = Math.Max(a, 1e-6);

        double scale = Math.Min((canvasW - 2 * padX) / shapeW, (canvasH - 2 * padY) / shapeH);

        double offsetX = (canvasW - shapeW * scale) / 2.0;
        double offsetY = canvasH - padY; // инверсия оси Y для WPF Canvas

        double ax = offsetX + pAx * scale;
        double ay = offsetY - pAy * scale;
        double bx = offsetX + pBx * scale;
        double by = offsetY - pBy * scale;
        double cx = offsetX + pCx * scale;
        double cy = offsetY - pCy * scale;

        var inv = CultureInfo.InvariantCulture;
        res.TrianglePolygonPoints = string.Format(inv, "{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{5:F1}",
            ax, ay, cx, cy, bx, by);

        // Квадратик прямого угла 90° в вершине C (снизу слева)
        const double sq = 12.0;
        res.RightAngleMarkPoints = string.Format(inv, "{0:F1},{1:F1} {2:F1},{1:F1} {2:F1},{3:F1}",
            cx, cy - sq, cx + sq, cy);

        // Центроид треугольника для смещения подписей наружу
        double centX = (ax + bx + cx) / 3.0;
        double centY = (ay + by + cy) / 3.0;

        // Подписи сторон: вертикальный катет a (слева, отрезок AC), горизонтальный b (снизу, отрезок CB), гипотенуза c (сверху-справа, отрезок AB)
        (double laX, double laY) = OffsetAwayFromCenter((ax + cx) / 2.0, (ay + cy) / 2.0, centX, centY, 24.0);
        res.SideALabelText = string.Format(inv, "a={0:0.###}", res.SideA);
        res.LabelSideAX = Math.Clamp(laX - 18.0, 4.0, canvasW - 60.0);
        res.LabelSideAY = Math.Clamp(laY - 7.0, 4.0, canvasH - 18.0);

        (double lbX, double lbY) = OffsetAwayFromCenter((bx + cx) / 2.0, (by + cy) / 2.0, centX, centY, 16.0);
        res.SideBLabelText = string.Format(inv, "b={0:0.###}", res.SideB);
        res.LabelSideBX = Math.Clamp(lbX - 18.0, 4.0, canvasW - 60.0);
        res.LabelSideBY = Math.Clamp(lbY - 5.0, 4.0, canvasH - 18.0);

        (double lcX, double lcY) = OffsetAwayFromCenter((ax + bx) / 2.0, (ay + by) / 2.0, centX, centY, 20.0);
        res.HypCLabelText = string.Format(inv, "c={0:0.###}", res.HypotenuseC);
        res.LabelHypCX = Math.Clamp(lcX - 16.0, 4.0, canvasW - 60.0);
        res.LabelHypCY = Math.Clamp(lcY - 7.0, 4.0, canvasH - 18.0);

        // Подпись острого угла α (в вершине B справа снизу, напротив катета a)
        (double lAlphaX, double lAlphaY) = OffsetAwayFromCenter(bx, by, centX, centY, 18.0);
        res.AngleAlphaLabelText = string.Format(inv, "α={0:0.##}°", res.AngleAlphaDeg);
        res.LabelAlphaX = Math.Clamp(lAlphaX - 10.0, 4.0, canvasW - 60.0);
        res.LabelAlphaY = Math.Clamp(lAlphaY - 6.0, 4.0, canvasH - 18.0);
    }

    private static (double X, double Y) OffsetAwayFromCenter(double px, double py, double cx, double cy, double dist)
    {
        double dx = px - cx;
        double dy = py - cy;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return (px, py);
        return (px + (dx / len) * dist, py + (dy / len) * dist);
    }


    private static void BuildBlindHoleCanvasCoordinates(BlindHoleCalculationResult res, bool roundSchemeValues)
    {
        var inv = CultureInfo.InvariantCulture;
        string fmt = roundSchemeValues ? "0.#" : "0.###";

        // Пропорция конуса к общей глубине на чертеже (ограничиваем для наглядности)
        double ratio = res.TotalDepth > 1e-6 ? Math.Clamp(res.ConeHeight / res.TotalDepth, 0.14, 0.85) : 0.25;
        if (res.HasWarning) ratio = 1.0;

        if (!res.IsVertical)
        {
            // =================================================================
            // РЕЖИМ 1: ГОРИЗОНТАЛЬНО (ТОКАРНЫЙ ЧПУ — торец Z0 справа)
            // =================================================================
            const double centerY = 100.0;
            const double halfD = 38.0;       // полуширина отверстия в пикселях
            const double totalLenPx = 175.0; // общая длина отверстия в пикселях
            const double faceX = 250.0;      // координата правого торца заготовки

            double conePx = totalLenPx * ratio;
            double shoulderX = faceX - (totalLenPx - conePx);
            double tipX = faceX - totalLenPx;

            double topWallY = centerY - halfD;
            double botWallY = centerY + halfD;
            const double partLeftX = 36.0;
            const double partTopY = 26.0;
            const double partBotY = 174.0;

            // Заливка металла вокруг отверстия
            res.PartBodyPoints = string.Format(inv,
                "{0:F1},{1:F1} {2:F1},{1:F1} {2:F1},{4:F1} {3:F1},{4:F1} {5:F1},{6:F1} {3:F1},{7:F1} {2:F1},{7:F1} {2:F1},{8:F1} {0:F1},{8:F1}",
                partLeftX, partTopY, faceX, shoulderX, topWallY, tipX, centerY, botWallY, partBotY);

            // Контур просверленного отверстия
            res.HoleOutlinePoints = string.Format(inv,
                "{0:F1},{1:F1} {2:F1},{1:F1} {3:F1},{4:F1} {2:F1},{5:F1} {0:F1},{5:F1}",
                faceX, topWallY, shoulderX, tipX, centerY, botWallY);

            // Линия перехода цилиндра в конус
            res.ShoulderLineX1 = shoulderX;
            res.ShoulderLineY1 = topWallY;
            res.ShoulderLineX2 = shoulderX;
            res.ShoulderLineY2 = botWallY;

            // Красная осевая линия
            res.AxisLineX1 = 22.0;
            res.AxisLineY1 = centerY;
            res.AxisLineX2 = 285.0;
            res.AxisLineY2 = centerY;

            // Зелёная линия ноля детали Z0 на торце
            res.ZeroLineX1 = faceX;
            res.ZeroLineY1 = 14.0;
            res.ZeroLineX2 = faceX;
            res.ZeroLineY2 = 186.0;
            res.ZeroLabelX = faceX + 4.0;
            res.ZeroLabelY = 14.0;

            // Размерная цепь полной глубины L (снизу под отверстием)
            double dimLY = botWallY + 14.0;
            res.DimLineLX1 = tipX;
            res.DimLineLY1 = dimLY;
            res.DimLineLX2 = faceX;
            res.DimLineLY2 = dimLY;
            res.ExtLinesLPoints = string.Format(inv,
                "M {0:F1},{1:F1} L {0:F1},{2:F1} M {3:F1},{4:F1} L {3:F1},{2:F1} M {5:F1},{6:F1} L {7:F1},{8:F1} M {9:F1},{6:F1} L {10:F1},{8:F1}",
                tipX, centerY + 4.0, dimLY + 3.0,
                faceX, botWallY + 2.0,
                tipX - 2.5, dimLY + 2.5, tipX + 2.5, dimLY - 2.5,
                faceX - 2.5, faceX + 2.5);

            // Размерная цепь высоты конуса h (сверху над конусом)
            double dimHY = topWallY - 12.0;
            res.DimLineHX1 = tipX;
            res.DimLineHY1 = dimHY;
            res.DimLineHX2 = shoulderX;
            res.DimLineHY2 = dimHY;
            res.ExtLinesHPoints = string.Format(inv,
                "M {0:F1},{1:F1} L {0:F1},{2:F1} M {3:F1},{4:F1} L {3:F1},{2:F1} M {5:F1},{6:F1} L {7:F1},{8:F1} M {9:F1},{6:F1} L {10:F1},{8:F1}",
                tipX, centerY - 4.0, dimHY - 3.0,
                shoulderX, topWallY - 2.0,
                tipX - 2.5, dimHY + 2.5, tipX + 2.5, dimHY - 2.5,
                shoulderX - 2.5, shoulderX + 2.5);

            // Размерная цепь цилиндрической части L_ц (сверху на одной линии с h)
            res.DimLineCylX1 = shoulderX;
            res.DimLineCylY1 = dimHY;
            res.DimLineCylX2 = faceX;
            res.DimLineCylY2 = dimHY;
            res.ExtLinesCylPoints = string.Format(inv,
                "M {0:F1},{1:F1} L {0:F1},{2:F1} M {3:F1},{4:F1} L {5:F1},{6:F1}",
                faceX, topWallY - 2.0, dimHY - 3.0,
                faceX - 2.5, dimHY + 2.5, faceX + 2.5, dimHY - 2.5);

            res.CylDepthLabelText = string.Format(inv, "L_ц={0:" + fmt + "}", res.CylindricalDepth);
            res.LabelCylX = (shoulderX + faceX) / 2.0 - 22.0;
            res.LabelCylY = dimHY - 15.0;

            // Подписи размеров на эскизе
            res.DiameterLabelText = string.Format(inv, "Ø{0:0.##}", res.Diameter);
            res.LabelDiamX = faceX + 6.0;
            res.LabelDiamY = centerY - 16.0;

            res.ConeHeightLabelText = string.Format(inv, "h={0:" + fmt + "}", res.ConeHeight);
            res.LabelConeX = Math.Clamp((shoulderX + tipX) / 2.0 - 18.0, 20.0, 240.0);
            res.LabelConeY = dimHY - 15.0;

            res.TotalDepthLabelText = string.Format(inv, "L={0:" + fmt + "}", res.TotalDepth);
            res.LabelTotalX = (faceX + tipX) / 2.0 - 20.0;
            res.LabelTotalY = dimLY + 2.0;
        }
        else
        {
            // =================================================================
            // РЕЖИМ 2: ВЕРТИКАЛЬНО (ФРЕЗЕРНЫЙ ЧПУ — плоскость Z0 сверху)
            // =================================================================
            const double centerX = 160.0;
            const double halfD = 38.0;       // полуширина отверстия
            const double totalLenPx = 115.0; // общая глубина по вертикали
            const double topFaceY = 38.0;    // верхняя плоскость заготовки

            double conePx = totalLenPx * ratio;
            double shoulderY = topFaceY + (totalLenPx - conePx);
            double tipY = topFaceY + totalLenPx;

            double leftWallX = centerX - halfD;
            double rightWallX = centerX + halfD;
            const double partLeftX = 56.0;
            const double partRightX = 264.0;
            const double partBotY = 182.0;

            // Заливка металла вокруг вертикального отверстия
            res.PartBodyPoints = string.Format(inv,
                "{0:F1},{1:F1} {2:F1},{1:F1} {2:F1},{3:F1} {4:F1},{5:F1} {6:F1},{3:F1} {6:F1},{1:F1} {7:F1},{1:F1} {7:F1},{8:F1} {0:F1},{8:F1}",
                partLeftX, topFaceY, leftWallX, shoulderY, centerX, tipY, rightWallX, partRightX, partBotY);

            // Контур отверстия
            res.HoleOutlinePoints = string.Format(inv,
                "{0:F1},{1:F1} {0:F1},{2:F1} {3:F1},{4:F1} {5:F1},{2:F1} {5:F1},{1:F1}",
                leftWallX, topFaceY, shoulderY, centerX, tipY, rightWallX);

            // Линия перехода цилиндра в конус
            res.ShoulderLineX1 = leftWallX;
            res.ShoulderLineY1 = shoulderY;
            res.ShoulderLineX2 = rightWallX;
            res.ShoulderLineY2 = shoulderY;

            // Красная вертикальная осевая линия
            res.AxisLineX1 = centerX;
            res.AxisLineY1 = 16.0;
            res.AxisLineX2 = centerX;
            res.AxisLineY2 = 192.0;

            // Зелёная горизонтальная линия Z0 на верхней плоскости
            res.ZeroLineX1 = 30.0;
            res.ZeroLineY1 = topFaceY;
            res.ZeroLineX2 = 290.0;
            res.ZeroLineY2 = topFaceY;
            res.ZeroLabelX = 30.0;
            res.ZeroLabelY = topFaceY - 14.0;

            // Размерная цепь полной глубины L (слева от отверстия)
            double dimLX = leftWallX - 14.0;
            res.DimLineLX1 = dimLX;
            res.DimLineLY1 = topFaceY;
            res.DimLineLX2 = dimLX;
            res.DimLineLY2 = tipY;
            res.ExtLinesLPoints = string.Format(inv,
                "M {0:F1},{1:F1} L {2:F1},{1:F1} M {3:F1},{4:F1} L {2:F1},{4:F1} M {5:F1},{6:F1} L {7:F1},{8:F1} M {5:F1},{9:F1} L {7:F1},{10:F1}",
                leftWallX - 2.0, topFaceY, dimLX - 3.0,
                centerX - 4.0, tipY,
                dimLX - 2.5, topFaceY + 2.5, dimLX + 2.5, topFaceY - 2.5,
                tipY + 2.5, tipY - 2.5);

            // Размерная цепь высоты конуса h (справа от конуса)
            double dimHX = rightWallX + 14.0;
            res.DimLineHX1 = dimHX;
            res.DimLineHY1 = shoulderY;
            res.DimLineHX2 = dimHX;
            res.DimLineHY2 = tipY;
            res.ExtLinesHPoints = string.Format(inv,
                "M {0:F1},{1:F1} L {2:F1},{1:F1} M {3:F1},{4:F1} L {2:F1},{4:F1} M {5:F1},{6:F1} L {7:F1},{8:F1} M {5:F1},{9:F1} L {7:F1},{10:F1}",
                rightWallX + 2.0, shoulderY, dimHX + 3.0,
                centerX + 4.0, tipY,
                dimHX - 2.5, shoulderY + 2.5, dimHX + 2.5, shoulderY - 2.5,
                tipY + 2.5, tipY - 2.5);

            // Размерная цепь цилиндрической части L_ц (справа на одной вертикали с h)
            res.DimLineCylX1 = dimHX;
            res.DimLineCylY1 = topFaceY;
            res.DimLineCylX2 = dimHX;
            res.DimLineCylY2 = shoulderY;
            res.ExtLinesCylPoints = string.Format(inv,
                "M {0:F1},{1:F1} L {2:F1},{1:F1} M {3:F1},{4:F1} L {5:F1},{6:F1}",
                rightWallX + 2.0, topFaceY, dimHX + 3.0,
                dimHX - 2.5, topFaceY + 2.5, dimHX + 2.5, topFaceY - 2.5);

            res.CylDepthLabelText = string.Format(inv, "L_ц={0:" + fmt + "}", res.CylindricalDepth);
            res.LabelCylX = dimHX + 5.0;
            res.LabelCylY = (topFaceY + shoulderY) / 2.0 - 7.0;

            // Подписи размеров
            res.DiameterLabelText = string.Format(inv, "Ø{0:0.##}", res.Diameter);
            res.LabelDiamX = centerX + 5.0;
            res.LabelDiamY = topFaceY + 4.0;

            res.ConeHeightLabelText = string.Format(inv, "h={0:" + fmt + "}", res.ConeHeight);
            res.LabelConeX = dimHX + 5.0;
            res.LabelConeY = (shoulderY + tipY) / 2.0 - 7.0;

            res.TotalDepthLabelText = string.Format(inv, "L={0:" + fmt + "}", res.TotalDepth);
            res.LabelTotalX = dimLX - 44.0;
            res.LabelTotalY = (topFaceY + tipY) / 2.0 - 7.0;
        }
    }

    private static double ClampCos(double value) => Math.Clamp(value, -1.0, 1.0);

    public ChamferCalculationResult CalculateChamfer(
                                 double axialLeg,
                                 double angleDegrees,
                                 double startX,
                                 double startZ,
                                 double approachZOffset,
                                 bool useApproach,
                                 bool isDecreasingX,
                                 bool isLeftToRight,
                                 bool isHole,
                                 bool useFeed = true,
                                 double feedRate = 0.12,
                                 int spindleRpm = 1200)
    {
        if (axialLeg <= 0)
            return ErrorChamfer("Катет фаски c должен быть больше 0.");
        if (angleDegrees <= 0 || angleDegrees >= 90)
            return ErrorChamfer("Угол фаски α должен быть в пределах (0°; 90°).");
        if (startX < 0)
            return ErrorChamfer("Диаметр начала фаски X_нач не может быть отрицательным.");
        if (useApproach && approachZOffset < 0)
            return ErrorChamfer("Величина подвода по Z не может быть отрицательной.");
        if (useFeed && feedRate <= 0)
            return ErrorChamfer("Рабочая подача F должна быть больше 0.");

        double angleRad = angleDegrees * DegToRad;
        double radialLeg = axialLeg * Math.Tan(angleRad);
        double diameterDelta = 2.0 * radialLeg;
        double hypotenuse = axialLeg / Math.Cos(angleRad);

        // Конечные координаты фаски (стандартный ноль детали справа, резание в -Z)
        double endZ = startZ - axialLeg;
        double endX = isDecreasingX ? startX - diameterDelta : startX + diameterDelta;

        // Расчёт точки подвода по продолжению линии фаски
        bool activeApproach = useApproach && approachZOffset > 0;
        double approachZ = startZ;
        double approachX = startX;

        if (activeApproach)
        {
            approachZ = startZ + approachZOffset;
            double approachDeltaX = 2.0 * approachZOffset * Math.Tan(angleRad);
            approachX = isDecreasingX ? startX + approachDeltaX : startX - approachDeltaX;
        }

        // Формирование мягких предупреждений без блокировки эскиза
        var warnings = new List<string>();

        if (endX < 0)
            warnings.Add($"Конечный диаметр X_кон ({endX:0.###} мм) уходит за ось симметрии (< 0).");

        if (activeApproach && approachX < 0)
            warnings.Add($"Диаметр подвода X_подв ({approachX:0.###} мм) уходит за ось симметрии (< 0).");

        if (startZ < -1e-6 && activeApproach)
            warnings.Add("Внимание: Z_нач < 0 (фаска не на торце). Проверьте, не задевает ли точка подвода соседнюю ступень.");

        if (useFeed && feedRate >= 0.8)
        {
            warnings.Add($"Критически высокая подача F = {feedRate:0.###} мм/об — проверьте, не пропущена ли десятичная точка (например, 0.12 вместо 12).");
        }
        else if (useFeed && feedRate > 0.35)
        {
            warnings.Add($"Повышенная подача F = {feedRate:0.###} мм/об для снятия фаски — высокий риск скола вершины пластины при врезании.");
        }

        bool hasWarning = warnings.Count > 0;
        string warningMessage = string.Join("\n", warnings);

        var inv = CultureInfo.InvariantCulture;
        string feedPart = useFeed ? string.Format(inv, " F{0:0.###}", feedRate) : string.Empty;

        string gcode = activeApproach
            ? string.Format(inv, "G00 X{0:0.###} Z{1:0.###}\nG01 X{2:0.###} Z{3:0.###}{4}", approachX, approachZ, endX, endZ, feedPart)
            : string.Format(inv, "G00 X{0:0.###} Z{1:0.###}\nG01 X{2:0.###} Z{3:0.###}{4}", startX, startZ, endX, endZ, feedPart);

        // Длина рабочего хода G01: если включён подвод — первый кадр G01 идёт до точки подвода,
        // а второй кадр G01 проходит путь от точки подвода до конца фаски
        double approachHypotenuse = activeApproach ? approachZOffset / Math.Cos(angleRad) : 0.0;
        double totalCuttingPathMm = hypotenuse + approachHypotenuse;

        int safeRpm = spindleRpm > 0 ? spindleRpm : 1200;
        double effectiveFeed = useFeed ? feedRate : 0.12;
        var (machiningSec, machiningText) = ComputeMachiningTime(totalCuttingPathMm, effectiveFeed, safeRpm);

        var result = new ChamferCalculationResult
        {
            IsValid = true,
            HasWarning = hasWarning,
            WarningMessage = warningMessage,
            IsDecreasingX = isDecreasingX,
            IsLeftToRight = isLeftToRight,
            IsHole = isHole,
            UseApproach = useApproach,
            AxialLeg = Math.Round(axialLeg, 4),
            RadialLeg = Math.Round(radialLeg, 4),
            DiameterDelta = Math.Round(diameterDelta, 4),
            AngleDegrees = Math.Round(angleDegrees, 2),
            ChamferHypotenuse = Math.Round(hypotenuse, 4),
            StartX = Math.Round(startX, 3),
            StartZ = Math.Round(startZ, 3),
            EndX = Math.Round(endX, 3),
            EndZ = Math.Round(endZ, 3),
            ApproachX = Math.Round(approachX, 3),
            ApproachZ = Math.Round(approachZ, 3),
            CncGCodeSnippet = gcode,
            ShowApproachLine = activeApproach,
            CuttingPathLengthMm = Math.Round(totalCuttingPathMm, 3),
            SpindleRpmUsed = safeRpm,
            MachiningTimeSeconds = machiningSec,
            MachiningTimeText = machiningText,
        };

        BuildChamferCanvasCoordinates(result, activeApproach ? approachZOffset / axialLeg : 0.0);
        return result;
    }


    private static void BuildChamferCanvasCoordinates(ChamferCalculationResult res, double approachRatio)
    {
        // Рабочая область Canvas: 320 x 200
        const double canvasW = 320.0;
        const double canvasH = 200.0;

        // Базовые размеры фаски на схеме
        double angleRad = res.AngleDegrees * DegToRad;
        double drawZ = 75.0;
        double drawR = Math.Clamp(drawZ * Math.Tan(angleRad), 24.0, 68.0);
        drawZ = Math.Clamp(drawR / Math.Tan(angleRad), 34.0, 95.0);

        // Коэффициент длины подвода на схеме (ограничиваем, чтобы не уходил за край)
        double appFactor = Math.Clamp(approachRatio, 0.0, 0.65);

        // Координаты строим сначала для базового направления "Справа налево" (деталь слева, подвод справа)
        // Верхний уровень и нижний уровень по Y на Canvas
        double startY, endY;
        if (!res.IsDecreasingX)
        {
            // На увеличение X: старт ниже (меньший диаметр), конец выше (больший диаметр)
            startY = 122.0;
            endY = startY - drawR;
        }
        else
        {
            // На уменьшение X: старт выше (больший диаметр), конец ниже (меньший диаметр)
            startY = 66.0;
            endY = startY + drawR;
        }

        double startX = 205.0;
        double endX = startX - drawZ;
        double appX = startX + drawZ * appFactor;
        double appY = startY - (endY - startY) * appFactor;

        // Границы тела детали (вал — металл снизу, отверстие — металл сверху)
        double leftWallX = 32.0;
        double rightWallX = startX;
        // Вычисляем масштаб по оси X (пикселей на 1 мм диаметра) и положение линии X = 0
        double deltaX = res.EndX - res.StartX;
        double scaleY = (endY - startY) / deltaX;
        double exactZeroY = startY + (0.0 - res.StartX) * scaleY;

        // Позиция красной линии X = 0 (ограничиваем границами Canvas, если X = 0 далеко)
        res.CenterLineY = Math.Clamp(exactZeroY, 14.0, 186.0);

        double bodyBoundY = res.IsHole
            ? 24.0
            : Math.Min(168.0, Math.Max(exactZeroY, Math.Max(startY, endY)));

        // Зеркалирование по горизонтали, если выбран режим "Слева направо"
        if (res.IsLeftToRight)
        {
            startX = canvasW - startX;
            endX = canvasW - endX;
            appX = canvasW - appX;
            leftWallX = canvasW - 32.0;
            rightWallX = startX;
        }

        res.CanvasStartX = startX;
        res.CanvasStartY = startY;

        // Масштаб по оси Z и положение вертикальной линии Z = 0
        double deltaZ = res.EndZ - res.StartZ; // равно -axialLeg
        double scaleX = (res.CanvasEndX - res.CanvasStartX) / deltaZ;
        double exactZeroX = res.CanvasStartX + (0.0 - res.StartZ) * scaleX;
        res.ZeroLineX = Math.Clamp(exactZeroX, 14.0, canvasW - 14.0);

        res.CanvasEndX = endX;
        res.CanvasEndY = endY;
        res.CanvasApproachX = appX;
        res.CanvasApproachY = appY;

        var inv = CultureInfo.InvariantCulture;

        // Полигон заливки тела детали (замкнутый контур: торец -> фаска -> цилиндр -> граница заготовки)
        res.PartBodyPoints = string.Format(inv,
            "{0:F1},{1:F1} {0:F1},{2:F1} {3:F1},{4:F1} {5:F1},{4:F1} {5:F1},{1:F1}",
            rightWallX, bodyBoundY, startY, endX, endY, leftWallX);

        // Контурная линия обработанной поверхности (торец + фаска + цилиндр)
        res.PartOutlinePoints = string.Format(inv,
            "{0:F1},{1:F1} {0:F1},{2:F1} {3:F1},{4:F1} {5:F1},{4:F1}",
            rightWallX, bodyBoundY, startY, endX, endY, leftWallX);

        // Формирование компактных строк координат для эскиза
        res.ApproachLabelText = string.Format(inv, "X{0:0.##} Z{1:0.##}", res.ApproachX, res.ApproachZ);
        res.StartLabelText = string.Format(inv, "X{0:0.##} Z{1:0.##}", res.StartX, res.StartZ);
        res.EndLabelText = string.Format(inv, "X{0:0.##} Z{1:0.##}", res.EndX, res.EndZ);

        // Смещаем подписи в свободную от детали сторону (для вала вверх, для отверстия вниз)
        double offsetY = res.IsHole ? 10.0 : -20.0;
        double sideShift = res.IsLeftToRight ? 6.0 : -64.0;

        res.LabelEndX = Math.Clamp(res.CanvasEndX + sideShift, 6.0, canvasW - 78.0);
        res.LabelEndY = Math.Clamp(res.CanvasEndY + offsetY, 4.0, canvasH - 22.0);

        res.LabelStartX = Math.Clamp(res.CanvasStartX + (res.IsLeftToRight ? -56.0 : 8.0), 6.0, canvasW - 78.0);
        res.LabelStartY = Math.Clamp(res.CanvasStartY + (res.IsHole ? 8.0 : -18.0), 4.0, canvasH - 22.0);

        res.LabelApproachX = Math.Clamp(res.CanvasApproachX + (res.IsLeftToRight ? -62.0 : 8.0), 6.0, canvasW - 82.0);
        res.LabelApproachY = Math.Clamp(res.CanvasApproachY + (res.IsHole ? -18.0 : 8.0), 4.0, canvasH - 22.0);

        // Вершина прямого угла (пересечение горизонтального и вертикального катетов)
        res.LegCornerX = res.CanvasEndX;
        res.LegCornerY = res.CanvasStartY;

        // Тексты длин катетов
        res.HorizontalLegText = string.Format(inv, "c={0:0.##}", res.AxialLeg);
        res.VerticalLegText = string.Format(inv, "ΔR={0:0.##}", res.RadialLeg);

        // Позиция подписи горизонтального катета (посередине отрезка, со смещением от линии)
        double midHorizX = (res.CanvasStartX + res.LegCornerX) / 2.0;
        bool horizAboveLine = res.CanvasStartY < res.CanvasEndY; // если линия сверху треугольника, подпись ставим выше
        res.LabelHorizLegX = midHorizX - 18.0;
        res.LabelHorizLegY = res.LegCornerY + (horizAboveLine ? -16.0 : 4.0);

        // Позиция подписи вертикального катета (посередине отрезка, с внешней стороны от гипотенузы)
        double midVertY = (res.LegCornerY + res.CanvasEndY) / 2.0;
        bool vertOnLeft = res.LegCornerX < res.CanvasStartX; // для режима "Справа налево" угол слева, выносим подпись левее
        res.LabelVertLegX = res.LegCornerX + (vertOnLeft ? -46.0 : 6.0);
        res.LabelVertLegY = midVertY - 8.0;

        // =====================================================================
        // ВЫНЕСЕННЫЙ ВЕКТОР НАПРАВЛЕНИЯ ПОДАЧИ G01 (ПАРАЛЛЕЛЬНО ФАСКЕ)
        // =====================================================================
        double dx = res.CanvasEndX - res.CanvasStartX;
        double dy = res.CanvasEndY - res.CanvasStartY;
        double len = Math.Sqrt(dx * dx + dy * dy);

        if (len > 1e-3)
        {
            // Единичный вектор направления движения резца (от Start к End)
            double ux = dx / len;
            double uy = dy / len;

            // Два возможных перпендикуляра (нормали) к линии фаски
            double n1x = -uy, n1y = ux;
            double n2x = uy, n2y = -ux;

            // Выбираем ту нормаль, которая ведёт в воздух (наружу от металла):
            // Для вала (IsHole = false) воздух сверху (n_y < 0), для отверстия (IsHole = true) воздух снизу (n_y > 0)
            double nx, ny;
            if (!res.IsHole)
            {
                if (n1y < -0.1) { nx = n1x; ny = n1y; }
                else { nx = n2x; ny = n2y; }
            }
            else
            {
                if (n1y > 0.1) { nx = n1x; ny = n1y; }
                else { nx = n2x; ny = n2y; }
            }

            // Центр отрезка фаски со сдвигом на 18 px в сторону воздуха
            const double offsetDist = 18.0;
            const double vectorLength = 42.0;
            double midX = (res.CanvasStartX + res.CanvasEndX) / 2.0 + nx * offsetDist;
            double midY = (res.CanvasStartY + res.CanvasEndY) / 2.0 + ny * offsetDist;

            double vStartX = midX - ux * (vectorLength / 2.0);
            double vStartY = midY - uy * (vectorLength / 2.0);
            double vEndX = midX + ux * (vectorLength / 2.0);
            double vEndY = midY + uy * (vectorLength / 2.0);

            res.FeedVectorStartX = vStartX;
            res.FeedVectorStartY = vStartY;
            res.FeedVectorEndX = vEndX;
            res.FeedVectorEndY = vEndY;

            // Треугольный наконечник стрелки на конце вектора (длина 8 px, полуширина 3.5 px)
            const double arrowLen = 8.0;
            const double arrowHalfW = 3.5;
            double baseX = vEndX - ux * arrowLen;
            double baseY = vEndY - uy * arrowLen;

            double p1X = baseX + nx * arrowHalfW;
            double p1Y = baseY + ny * arrowHalfW;
            double p2X = baseX - nx * arrowHalfW;
            double p2Y = baseY - ny * arrowHalfW;

            res.FeedArrowHeadPoints = string.Format(inv, "{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{5:F1}",
                vEndX, vEndY, p1X, p1Y, p2X, p2Y);

            // Позиция компактной подписи "G01" рядом со стрелкой
            res.LabelFeedX = Math.Clamp(midX + nx * 10.0 - 10.0, 6.0, canvasW - 32.0);
            res.LabelFeedY = Math.Clamp(midY + ny * 10.0 - 7.0, 4.0, canvasH - 18.0);
        }

    }

    private static ChamferCalculationResult ErrorChamfer(string msg) =>
        new() { IsValid = false, ErrorMessage = msg };

    private static BlindHoleCalculationResult ErrorHole(string msg) =>
          new() { IsValid = false, ErrorMessage = msg };

    public ArcInterpolationResult CalculateArcInterpolation(
                                    double radius,
                                    double inputX,
                                    double inputZ,
                                    ArcCoordinateInputMode inputMode,
                                    bool isConcaveFillet,
                                    bool isDecreasingX,
                                    bool isLeftToRight,
                                    bool isHole,
                                    bool isFrontTurret,
                                    bool useFeed = true,
                                    double feedRate = 0.12,
                                    int spindleRpm = 1200)
    {
        if (radius <= 0)
            return ErrorArc("Радиус скругления R должен быть больше 0.");
        if (inputX < 0)
            return ErrorArc("Введённый диаметр X не может быть отрицательным.");
        if (useFeed && feedRate <= 0)
            return ErrorArc("Рабочая подача F должна быть больше 0.");

        double signX = isDecreasingX ? -1.0 : 1.0;
        double deltaX = 2.0 * radius * signX;
        double deltaZ = -radius; // стандартный ноль детали справа, резание в -Z

        double startX, startZ, endX, endZ, cornerX, cornerZ;

        switch (inputMode)
        {
            case ArcCoordinateInputMode.ByCornerVertex:
                cornerX = inputX;
                cornerZ = inputZ;
                if (!isConcaveFillet)
                {
                    // Выпуклый радиус: старт на торце (смещён по X), конец на цилиндре (смещён по Z)
                    startX = cornerX - deltaX;
                    startZ = cornerZ;
                    endX = cornerX;
                    endZ = cornerZ + deltaZ;
                }
                else
                {
                    // Вогнутая галтель: старт на цилиндре (смещён по Z), конец на торце (смещён по X)
                    startX = cornerX;
                    startZ = cornerZ - deltaZ;
                    endX = cornerX + deltaX;
                    endZ = cornerZ;
                }
                break;

            case ArcCoordinateInputMode.ByStartPoint:
                startX = inputX;
                startZ = inputZ;
                endX = startX + deltaX;
                endZ = startZ + deltaZ;
                if (!isConcaveFillet)
                {
                    cornerX = endX;
                    cornerZ = startZ;
                }
                else
                {
                    cornerX = startX;
                    cornerZ = endZ;
                }
                break;

            case ArcCoordinateInputMode.ByEndPoint:
            default:
                endX = inputX;
                endZ = inputZ;
                startX = endX - deltaX;
                startZ = endZ - deltaZ;
                if (!isConcaveFillet)
                {
                    cornerX = endX;
                    cornerZ = startZ;
                }
                else
                {
                    cornerX = startX;
                    cornerZ = endZ;
                }
                break;
        }

        // Инкрементальные координаты центра окружности от точки старта дуги
        double centerOffsetI = !isConcaveFillet ? radius * signX : 0.0;
        double centerOffsetK = !isConcaveFillet ? 0.0 : -radius;

        // Определение команды G02 / G03 (при заднем резцедержателе: выпуклый +X или вогнутый -X -> G03)
        bool isCcwRear = (!isConcaveFillet && !isDecreasingX) || (isConcaveFillet && isDecreasingX);
        bool isG03 = isFrontTurret ? !isCcwRear : isCcwRear;
        string gCmd = isG03 ? "G03" : "G02";

        var warnings = new List<string>();
        if (startX < 0)
            warnings.Add($"Диаметр начала дуги X_нач ({startX:0.###} мм) уходит за ось симметрии (< 0).");
        if (endX < 0)
            warnings.Add($"Диаметр конца дуги X_кон ({endX:0.###} мм) уходит за ось симметрии (< 0).");

        if (useFeed && feedRate >= 0.8)
            warnings.Add($"Критически высокая подача F = {feedRate:0.###} мм/об — проверьте десятичную точку (например, 0.12 вместо 12).");
        else if (useFeed && feedRate > 0.35)
            warnings.Add($"Повышенная подача F = {feedRate:0.###} мм/об при круговой интерполяции — высокий риск дробления и ухудшения чистоты поверхности.");

        var inv = CultureInfo.InvariantCulture;
        double approachFeed = Math.Round(feedRate * 1.5, 3);
        string linearFeedPart = useFeed ? string.Format(inv, " F{0:0.###}", approachFeed) : string.Empty;
        string arcFeedPart = useFeed ? string.Format(inv, " F{0:0.###}", feedRate) : string.Empty;

        string gcodeR = string.Format(inv, "G01 X{0:0.###} Z{1:0.###}{6}\n{2} X{3:0.###} Z{4:0.###} R{5:0.###}{7}",
            startX, startZ, gCmd, endX, endZ, radius, linearFeedPart, arcFeedPart);

        string gcodeIK = string.Format(inv, "G01 X{0:0.###} Z{1:0.###}{7}\n{2} X{3:0.###} Z{4:0.###} I{5:0.###} K{6:0.###}{8}",
            startX, startZ, gCmd, endX, endZ, centerOffsetI, centerOffsetK, linearFeedPart, arcFeedPart);


        double arcLengthMm = (Math.PI * radius) / 2.0;
        int safeRpm = spindleRpm > 0 ? spindleRpm : 1200;
        double effectiveFeed = useFeed ? feedRate : 0.12;
        var (machiningSec, machiningText) = ComputeMachiningTime(arcLengthMm, effectiveFeed, safeRpm);

        var result = new ArcInterpolationResult
        {
            IsValid = true,
            HasWarning = warnings.Count > 0,
            WarningMessage = string.Join("\n", warnings),
            IsConcaveFillet = isConcaveFillet,
            IsDecreasingX = isDecreasingX,
            IsLeftToRight = isLeftToRight,
            IsHole = isHole,
            IsFrontTurret = isFrontTurret,
            InputMode = inputMode,
            Radius = Math.Round(radius, 3),
            GCodeCommand = gCmd,
            StartX = Math.Round(startX, 3),
            StartZ = Math.Round(startZ, 3),
            EndX = Math.Round(endX, 3),
            EndZ = Math.Round(endZ, 3),
            CornerX = Math.Round(cornerX, 3),
            CornerZ = Math.Round(cornerZ, 3),
            CenterOffsetI = Math.Round(centerOffsetI, 3),
            CenterOffsetK = Math.Round(centerOffsetK, 3),
            GCodeRadiusSnippet = gcodeR,
            GCodeIKSnippet = gcodeIK,
            ArcLengthMm = Math.Round(arcLengthMm, 3),
            SpindleRpmUsed = safeRpm,
            MachiningTimeSeconds = machiningSec,
            MachiningTimeText = machiningText
        };

        BuildArcCanvasCoordinates(result);
        return result;
    }

    private static void BuildArcCanvasCoordinates(ArcInterpolationResult res)
    {
        const double canvasW = 320.0;
        const double canvasH = 200.0;
        const double rPx = 56.0; // радиус дуги на схеме в пикселях

        double startY, endY, cornerY, centerY;
        if (!res.IsDecreasingX)
        {
            startY = 128.0;
            endY = startY - rPx;
        }
        else
        {
            startY = 68.0;
            endY = startY + rPx;
        }

        double startX = 205.0;
        double endX = startX - rPx;
        double cornerX, centerX;

        if (!res.IsConcaveFillet)
        {
            cornerX = startX;
            cornerY = endY;
            centerX = endX;
            centerY = startY;
        }
        else
        {
            cornerX = endX;
            cornerY = startY;
            centerX = startX;
            centerY = endY;
        }

        // Положение зелёной линии X = 0
        double deltaX = res.EndX - res.StartX;
        double scaleY = (endY - startY) / deltaX;
        double exactZeroY = startY + (0.0 - res.StartX) * scaleY;
        res.CenterLineY = Math.Clamp(exactZeroY, 14.0, 186.0);

        double leftWallX = 32.0;
        double rightWallX = Math.Max(startX, cornerX);
        double bodyBoundY = res.IsHole
            ? 24.0
            : Math.Min(168.0, Math.Max(exactZeroY, Math.Max(startY, endY)));

        // Зеркалирование по горизонтали при выборе "Слева направо"
        if (res.IsLeftToRight)
        {
            startX = canvasW - startX;
            endX = canvasW - endX;
            cornerX = canvasW - cornerX;
            centerX = canvasW - centerX;
            leftWallX = canvasW - 32.0;
            rightWallX = Math.Min(startX, cornerX);
        }

        res.CanvasStartX = startX;
        res.CanvasStartY = startY;
        res.CanvasEndX = endX;
        res.CanvasEndY = endY;
        res.CanvasCornerX = cornerX;
        res.CanvasCornerY = cornerY;
        res.CanvasCenterX = centerX;
        res.CanvasCenterY = centerY;

        // Положение вертикальной зелёной линии Z = 0
        double deltaZ = res.EndZ - res.StartZ;
        double scaleX = (res.CanvasEndX - res.CanvasStartX) / deltaZ;
        double exactZeroX = res.CanvasStartX + (0.0 - res.StartZ) * scaleX;
        res.ZeroLineX = Math.Clamp(exactZeroX, 14.0, canvasW - 14.0);

        // Направление изгиба дуги для SVG/WPF ArcSegment (SweepDirection: 1 = Clockwise, 0 = Counterclockwise)
        double crossProduct = (res.CanvasCenterX - res.CanvasStartX) * (res.CanvasEndY - res.CanvasStartY)
                            - (res.CanvasCenterY - res.CanvasStartY) * (res.CanvasEndX - res.CanvasStartX);
        int sweepFlag = crossProduct < 0 ? 1 : 0;

        var inv = CultureInfo.InvariantCulture;
        res.ArcPathData = string.Format(inv, "M {0:F1},{1:F1} A {2:F1},{2:F1} 0 0 {3} {4:F1},{5:F1}",
            res.CanvasStartX, res.CanvasStartY, rPx, sweepFlag, res.CanvasEndX, res.CanvasEndY);

        // Замкнутый контур тела детали с точной дугой (без пустой области между дугой и заливкой)
        res.PartBodyPathData = string.Format(inv,
            "M {0:F1},{1:F1} L {2:F1},{3:F1} A {4:F1},{4:F1} 0 0 {5} {6:F1},{7:F1} L {8:F1},{7:F1} L {8:F1},{1:F1} Z",
            rightWallX, bodyBoundY,
            res.CanvasStartX, res.CanvasStartY,
            rPx, sweepFlag,
            res.CanvasEndX, res.CanvasEndY,
            leftWallX);

        // Контурные линии торца и цилиндрической поверхности
        res.PartOutlinePathData = string.Format(inv,
            "M {0:F1},{1:F1} L {2:F1},{3:F1} M {4:F1},{5:F1} L {6:F1},{5:F1}",
            rightWallX, bodyBoundY,
            res.CanvasStartX, res.CanvasStartY,
            res.CanvasEndX, res.CanvasEndY,
            leftWallX);

        // Подписи координат начала, конца дуги и вершины угла
        res.StartLabelText = string.Format(inv, "X{0:0.##} Z{1:0.##}", res.StartX, res.StartZ);
        res.EndLabelText = string.Format(inv, "X{0:0.##} Z{1:0.##}", res.EndX, res.EndZ);
        res.CornerLabelText = string.Format(inv, "X{0:0.##} Z{1:0.##}", res.CornerX, res.CornerZ);

        double offsetY = res.IsHole ? 10.0 : -20.0;
        double sideShift = res.IsLeftToRight ? 6.0 : -68.0;

        res.LabelEndX = Math.Clamp(res.CanvasEndX + sideShift, 6.0, canvasW - 82.0);
        res.LabelEndY = Math.Clamp(res.CanvasEndY + offsetY, 4.0, canvasH - 22.0);

        res.LabelStartX = Math.Clamp(res.CanvasStartX + (res.IsLeftToRight ? -62.0 : 8.0), 6.0, canvasW - 82.0);
        res.LabelStartY = Math.Clamp(res.CanvasStartY + (res.IsHole ? 8.0 : -18.0), 4.0, canvasH - 22.0);

        // Смещаем подпись вершины угла наружу от центра дуги, чтобы она не перекрывала линии
        double cornerShiftX = res.CanvasCornerX >= res.CanvasCenterX ? 6.0 : -68.0;
        double cornerShiftY = res.CanvasCornerY >= res.CanvasCenterY ? 6.0 : -20.0;
        res.LabelCornerX = Math.Clamp(res.CanvasCornerX + cornerShiftX, 6.0, canvasW - 82.0);
        res.LabelCornerY = Math.Clamp(res.CanvasCornerY + cornerShiftY, 4.0, canvasH - 22.0);

        // Подписи координат начала и конца дуги
        res.StartLabelText = string.Format(inv, "X{0:0.##} Z{1:0.##}", res.StartX, res.StartZ);
        res.EndLabelText = string.Format(inv, "X{0:0.##} Z{1:0.##}", res.EndX, res.EndZ);

        res.LabelEndX = Math.Clamp(res.CanvasEndX + sideShift, 6.0, canvasW - 82.0);
        res.LabelEndY = Math.Clamp(res.CanvasEndY + offsetY, 4.0, canvasH - 22.0);

        res.LabelStartX = Math.Clamp(res.CanvasStartX + (res.IsLeftToRight ? -62.0 : 8.0), 6.0, canvasW - 82.0);
        res.LabelStartY = Math.Clamp(res.CanvasStartY + (res.IsHole ? 8.0 : -18.0), 4.0, canvasH - 22.0);

        // =====================================================================
        // ЭКВИДИСТАНТНАЯ ДУГА НАПРАВЛЕНИЯ ПОДАЧИ (G02 / G03)
        // =====================================================================
        // Для выпуклой дуги отступаем наружу (+16 px), для вогнутой галтели — внутрь к центру (-16 px)
        double rEq = res.IsConcaveFillet ? (rPx - 16.0) : (rPx + 16.0);

        double theta1 = Math.Atan2(res.CanvasStartY - res.CanvasCenterY, res.CanvasStartX - res.CanvasCenterX);
        double theta2 = Math.Atan2(res.CanvasEndY - res.CanvasCenterY, res.CanvasEndX - res.CanvasCenterX);

        double dTheta = theta2 - theta1;
        while (dTheta > Math.PI) dTheta -= 2.0 * Math.PI;
        while (dTheta < -Math.PI) dTheta += 2.0 * Math.PI;

        // Немного укорачиваем дугу по краям (на 12%), чтобы не задевать маркеры точек
        double aStart = theta1 + dTheta * 0.12;
        double aEnd = theta2 - dTheta * 0.12;

        double eqStartX = res.CanvasCenterX + rEq * Math.Cos(aStart);
        double eqStartY = res.CanvasCenterY + rEq * Math.Sin(aStart);
        double eqEndX = res.CanvasCenterX + rEq * Math.Cos(aEnd);
        double eqEndY = res.CanvasCenterY + rEq * Math.Sin(aEnd);

        res.EquidistantArcPathData = string.Format(inv,
            "M {0:F1},{1:F1} A {2:F1},{2:F1} 0 0 {3} {4:F1},{5:F1}",
            eqStartX, eqStartY, rEq, sweepFlag, eqEndX, eqEndY);

        // Единичный касательный вектор в конечной точке дуги (направление стрелки)
        double dirSign = dTheta >= 0 ? 1.0 : -1.0;
        double tx = -Math.Sin(aEnd) * dirSign;
        double ty = Math.Cos(aEnd) * dirSign;

        // Единичный радиальный вектор нормали
        double nx = Math.Cos(aEnd);
        double ny = Math.Sin(aEnd);

        const double arrowLen = 8.0;
        const double arrowHalfW = 3.5;
        double baseX = eqEndX - tx * arrowLen;
        double baseY = eqEndY - ty * arrowLen;

        double p1X = baseX + nx * arrowHalfW;
        double p1Y = baseY + ny * arrowHalfW;
        double p2X = baseX - nx * arrowHalfW;
        double p2Y = baseY - ny * arrowHalfW;

        res.EquidistantArrowHeadPoints = string.Format(inv,
            "{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{5:F1}",
            eqEndX, eqEndY, p1X, p1Y, p2X, p2Y);

        // Подпись "G02" или "G03" возле середины эквидистантной дуги
        double aMid = (aStart + aEnd) / 2.0;
        double rLabel = rEq + (res.IsConcaveFillet ? -13.0 : 13.0);
        double labelX = res.CanvasCenterX + rLabel * Math.Cos(aMid) - 10.0;
        double labelY = res.CanvasCenterY + rLabel * Math.Sin(aMid) - 7.0;

        res.LabelFeedX = Math.Clamp(labelX, 6.0, canvasW - 32.0);
        res.LabelFeedY = Math.Clamp(labelY, 4.0, canvasH - 18.0);

    }

    private static ArcInterpolationResult ErrorArc(string msg) =>
        new() { IsValid = false, ErrorMessage = msg };

    // =========================================================================
    // РАЗДЕЛ 5: ПРЯМОУГОЛЬНЫЕ КАНАВКИ И ОТРЕЗКА С ЦИКЛОМ G75 (КАРТОЧКА 5)
    // =========================================================================

    public GrooveCalculationResult CalculateGrooveOrPartOff(
    bool isPartOffMode,
    bool isLeftToRight,
    double stockDiameter,
    double grooveBottomDiameter,
    double startZ,
    double endZ,
    double approachOffsetX,
    double stepPMm,
    double retractR,
    double feedRate,
    bool considerToolWidth,
    double toolWidth,
    int macroVariableNumber,
    bool useAxisOvertravel,
    double axisOvertravel,
    bool reduceFeedNearCenter,
    int spindleRpm = 1000,
    bool includeFinishPass = false,
    bool finishPassWithAllowance = true,
    double finishAllowanceX = 0.2,
    double finishAllowanceZ = 0.1)
    {
        if (stockDiameter <= 0)
            return ErrorGroove("Диаметр заготовки D_заг должен быть больше 0.");
        if (!isPartOffMode && grooveBottomDiameter < 0)
            return ErrorGroove("Диаметр дна канавки D_дна не может быть отрицательным.");
        if (!isPartOffMode && grooveBottomDiameter >= stockDiameter)
            return ErrorGroove("Диаметр дна канавки должен быть меньше диаметра заготовки.");
        if (stepPMm <= 0)
            return ErrorGroove("Шаг врезания P должен быть больше 0.");
        if (retractR < 0)
            return ErrorGroove("Величина отскока R не может быть отрицательной.");
        if (feedRate <= 0)
            return ErrorGroove("Рабочая подача F должна быть больше 0.");
        if (considerToolWidth && toolWidth <= 0)
            return ErrorGroove("Толщина пластины резца B должна быть больше 0.");
        if (considerToolWidth && macroVariableNumber <= 0)
            return ErrorGroove("Номер переменной должен быть положительным целым числом (например, 1).");
        if (isPartOffMode && useAxisOvertravel && axisOvertravel < 0)
            return ErrorGroove("Величина перебега за ось не может быть отрицательной.");

        bool activeFinishPass = !isPartOffMode && includeFinishPass;
        bool activeFinishWithAllowance = activeFinishPass && finishPassWithAllowance;

        if (activeFinishWithAllowance)
        {
            if (finishAllowanceX < 0 || finishAllowanceZ < 0)
                return ErrorGroove("Чистовые припуски по X и Z не могут быть отрицательными.");

            double grooveWCheck = Math.Abs(endZ - startZ);
            if (grooveWCheck > 1e-6 && finishAllowanceZ * 2.0 >= grooveWCheck)
                return ErrorGroove("Суммарный припуск на стенки (2 × ΔZ) должен быть меньше ширины канавки.");

            if (grooveBottomDiameter + 2.0 * finishAllowanceX >= stockDiameter)
                return ErrorGroove("Черновой диаметр G75 с учётом припуска по X превышает диаметр заготовки.");
        }

        double approachX = stockDiameter + approachOffsetX;
        double targetX = isPartOffMode
            ? (useAxisOvertravel ? -axisOvertravel : 0.0)
            : grooveBottomDiameter;

        double radialDepth = isPartOffMode
            ? stockDiameter / 2.0
            : (stockDiameter - grooveBottomDiameter) / 2.0;

        int pMicrons = (int)Math.Round(stepPMm * 1000.0);
        double dirZ = isLeftToRight ? 1.0 : -1.0;

        var warnings = new List<string>();
        var infos = new List<string>();

        if (approachOffsetX <= 0)
            warnings.Add("Запас подвода по X ≤ 0: резец подойдёт на G00 вплотную к вращающейся заготовке.");

        double maxDepthFactor = isPartOffMode ? 7.0 : 4.0;
        double effectiveToolB = considerToolWidth ? toolWidth : 3.0;
        double minOverhang = Math.Round(radialDepth + 1.0, 2);
        double maxOverhang = Math.Round(effectiveToolB * maxDepthFactor, 2);
        string overhangText = considerToolWidth
            ? string.Format(CultureInfo.InvariantCulture, "{0:0.##} – {1:0.##} мм", minOverhang, maxOverhang)
            : string.Format(CultureInfo.InvariantCulture, "от {0:0.##} мм (укажите ширину B)", minOverhang);

        if (considerToolWidth && radialDepth > maxDepthFactor * toolWidth)
        {
            warnings.Add(isPartOffMode
                ? $"Глубина отрезки на сторону ({radialDepth:0.##} мм) превышает 7 ширины пластины ({toolWidth:0.##} мм) — убедитесь, что вылета лезвия хватит до центра детали."
                : $"Глубина канавки ({radialDepth:0.##} мм) превышает 4 ширины резца ({toolWidth:0.##} мм) — высокий риск поломки пластины.");
        }

        if (feedRate >= 0.5)
            warnings.Add($"Критически высокая подача F = {feedRate:0.###} мм/об — проверьте, не пропущена ли десятичная точка (например, 0.08 вместо 8).");
        else if (feedRate > 0.20)
            warnings.Add($"Повышенная подача F = {feedRate:0.###} мм/об — убедитесь, что она допустима для обрабатываемого металла и жёсткости резца.");

        if (stepPMm >= 10.0)
            warnings.Add($"Очень большой шаг врезания P = {stepPMm:0.###} мм — проверьте, не введены ли микроны вместо миллиметров (например, 1.5 вместо 1500).");
        else if (considerToolWidth && stepPMm > toolWidth * 1.05)
            warnings.Add($"Большой шаг врезания P = {stepPMm:0.###} мм (больше ширины резца B = {toolWidth:0.##} мм) — повышенная нагрузка на пластину и риск заклинивания стружки в пазу.");
        else if (!considerToolWidth && stepPMm > 3.5)
            warnings.Add($"Большой шаг врезания P = {stepPMm:0.###} мм — длинная стружка может заклинить в пазу и сломать пластину.");

        if (retractR > 1.5)
            warnings.Add($"Большая величина отскока R = {retractR:0.###} мм — увеличивается время цикла и ударная нагрузка на кромку (обычно достаточно 0.3–0.5 мм).");

        infos.Add(isLeftToRight
            ? "Направление слева направо: резец привязывается по ПРАВОЙ кромке."
            : "Направление справа налево: резец привязывается по ЛЕВОЙ кромке.");

        var inv = CultureInfo.InvariantCulture;
        double compStartZ = startZ;
        double compEndZ = isPartOffMode ? startZ : endZ;
        double roughTargetX = targetX;
        double stepZMm = 0.0;
        int qMicrons = 0;
        int passesCount = 1;

        string standardCode = string.Empty;
        string macroCode = string.Empty;

        double finishPathMm = 0.0;
        double finishFeed = Math.Round(feedRate * 0.6, 4);
        if (!isPartOffMode)
        {
            double grooveWidth = Math.Abs(endZ - startZ);
            if (grooveWidth < 1e-6)
                warnings.Add("Координаты Z_нач и Z_кон совпадают — будет выполнен один прямой проход без смещения Q.");

            if (considerToolWidth && grooveWidth > 1e-6 && grooveWidth < toolWidth - 1e-4)
                warnings.Add($"Ширина канавки ({grooveWidth:0.###} мм) меньше толщины резца ({toolWidth:0.###} мм) — проточка невозможна.");

            double finishWall1Z = considerToolWidth ? startZ + dirZ * toolWidth : startZ;
            double finishWall2Z = endZ;

            if (activeFinishWithAllowance)
            {
                roughTargetX = Math.Round(targetX + 2.0 * finishAllowanceX, 3);
                compStartZ = finishWall1Z + dirZ * finishAllowanceZ;
                compEndZ = finishWall2Z - dirZ * finishAllowanceZ;

                if ((dirZ < 0 && compEndZ > compStartZ) || (dirZ > 0 && compEndZ < compStartZ))
                {
                    double midZ = (finishWall1Z + finishWall2Z) / 2.0;
                    compStartZ = midZ;
                    compEndZ = midZ;
                }

                infos.Add(string.Format(inv,
                    "Чистовой проход с припуском: G75 оставляет ΔX={0:0.##} мм на сторону (черн. X{1:0.###}) и ΔZ={2:0.##} мм на стенки; далее встречная калибровка стенок и дна на F{3:0.####}.",
                    finishAllowanceX, roughTargetX, finishAllowanceZ, finishFeed));
            }
            else
            {
                compStartZ = finishWall1Z;
                compEndZ = finishWall2Z;

                if (activeFinishPass)
                {
                    infos.Add(string.Format(inv,
                        "Быстрая зачистка дна без припуска: G75 вырезает канавку сразу в чистовой размер X{0:0.###}, затем выполняется 1 проход вдоль дна (F{1:0.####}) для снятия гребешков.",
                        targetX, finishFeed));
                }
            }

            double travelZ = Math.Abs(compEndZ - compStartZ);
            if (considerToolWidth && travelZ > 1e-4)
            {
                double rawStep = Math.Max(0.1, Math.Round(toolWidth * 0.8, 1));
                passesCount = (int)Math.Ceiling(travelZ / rawStep) + 1;
                stepZMm = travelZ / (passesCount - 1);
                qMicrons = (int)Math.Round(stepZMm * 1000.0);
                infos.Add($"Проходов по ширине: {passesCount} (шаг смещения Q = {stepZMm:0.###} мм / Q{qMicrons}).");
            }
            else if (!considerToolWidth && travelZ > 1e-4)
            {
                stepZMm = travelZ;
                qMicrons = (int)Math.Round(stepZMm * 1000.0);
                passesCount = 2;
                infos.Add($"Прямой расчёт без учёта толщины резца: шаг Q{qMicrons} ({stepZMm:0.###} мм).");
            }

            string finishStdBlock = string.Empty;
            string finishMacroBlock = string.Empty;

            if (activeFinishPass)
            {
                if (activeFinishWithAllowance)
                {
                    double safeReliefX = Math.Round(targetX + 0.6, 3);
                    double midZ = Math.Round((finishWall1Z + finishWall2Z) / 2.0, 3);
                    double overlap = Math.Min(0.3, Math.Abs(finishWall2Z - finishWall1Z) * 0.25);
                    double pass1EndZ = Math.Round(midZ + dirZ * overlap, 3);

                    finishPathMm = 2.0 * radialDepth + Math.Abs(finishWall2Z - finishWall1Z) + overlap;

                    if (Math.Abs(finishWall2Z - finishWall1Z) > 1e-4)
                    {
                        finishStdBlock = string.Format(inv,
                            "\nG00 X{0:0.###}\nG00 Z{1:0.###}\nG01 X{2:0.###} F{3:0.####}\nG01 Z{4:0.###}\nG01 X{5:0.###}\nG00 X{0:0.###}\nG00 Z{6:0.###}\nG01 X{2:0.###}\nG01 Z{7:0.###}\nG01 X{5:0.###}\nG00 X{0:0.###}",
                            approachX, finishWall1Z, targetX, finishFeed, pass1EndZ, safeReliefX, finishWall2Z, midZ);

                        if (considerToolWidth)
                        {
                            string signStr = isLeftToRight ? "+" : "-";
                            string zWall1Expr = string.Format(inv, "[{0:0.###}{1}#{2}]", startZ, signStr, macroVariableNumber);
                            finishMacroBlock = string.Format(inv,
                                "\nG00 X{0:0.###}\nG00 Z{1}\nG01 X{2:0.###} F{3:0.####}\nG01 Z{4:0.###}\nG01 X{5:0.###}\nG00 X{0:0.###}\nG00 Z{6:0.###}\nG01 X{2:0.###}\nG01 Z{7:0.###}\nG01 X{5:0.###}\nG00 X{0:0.###}",
                                approachX, zWall1Expr, targetX, finishFeed, pass1EndZ, safeReliefX, finishWall2Z, midZ);
                        }
                    }
                    else
                    {
                        finishPathMm = radialDepth;
                        finishStdBlock = string.Format(inv,
                            "\nG00 X{0:0.###} Z{1:0.###}\nG01 X{2:0.###} F{3:0.####}\nG00 X{0:0.###}",
                            approachX, finishWall1Z, targetX, finishFeed);

                        if (considerToolWidth)
                        {
                            string signStr = isLeftToRight ? "+" : "-";
                            string zWall1Expr = string.Format(inv, "[{0:0.###}{1}#{2}]", startZ, signStr, macroVariableNumber);
                            finishMacroBlock = string.Format(inv,
                                "\nG00 X{0:0.###} Z{1}\nG01 X{2:0.###} F{3:0.####}\nG00 X{0:0.###}",
                                approachX, zWall1Expr, targetX, finishFeed);
                        }
                    }
                }
                else
                {
                    double safeReliefX = Math.Round(targetX + 0.6, 3);
                    double bottomWidthTravel = Math.Abs(finishWall2Z - finishWall1Z);
                    finishPathMm = radialDepth + bottomWidthTravel;

                    if (bottomWidthTravel > 1e-4)
                    {
                        finishStdBlock = string.Format(inv,
                            "\nG00 X{0:0.###} Z{1:0.###}\nG01 X{2:0.###} F{3:0.####}\nG01 Z{4:0.###}\nG01 X{5:0.###}\nG00 X{0:0.###}",
                            approachX, finishWall1Z, targetX, finishFeed, finishWall2Z, safeReliefX);

                        if (considerToolWidth)
                        {
                            string signStr = isLeftToRight ? "+" : "-";
                            string zWall1Expr = string.Format(inv, "[{0:0.###}{1}#{2}]", startZ, signStr, macroVariableNumber);
                            finishMacroBlock = string.Format(inv,
                                "\nG00 X{0:0.###} Z{1}\nG01 X{2:0.###} F{3:0.####}\nG01 Z{4:0.###}\nG01 X{5:0.###}\nG00 X{0:0.###}",
                                approachX, zWall1Expr, targetX, finishFeed, finishWall2Z, safeReliefX);
                        }
                    }
                    else
                    {
                        finishPathMm = radialDepth;
                        finishStdBlock = string.Format(inv,
                            "\nG00 X{0:0.###} Z{1:0.###}\nG01 X{2:0.###} F{3:0.####}\nG04 P300\nG00 X{0:0.###}",
                            approachX, finishWall1Z, targetX, finishFeed);

                        if (considerToolWidth)
                        {
                            string signStr = isLeftToRight ? "+" : "-";
                            string zWall1Expr = string.Format(inv, "[{0:0.###}{1}#{2}]", startZ, signStr, macroVariableNumber);
                            finishMacroBlock = string.Format(inv,
                                "\nG00 X{0:0.###} Z{1}\nG01 X{2:0.###} F{3:0.####}\nG04 P300\nG00 X{0:0.###}",
                                approachX, zWall1Expr, targetX, finishFeed);
                        }
                    }
                }
            }
            if (qMicrons > 0 && Math.Abs(compEndZ - compStartZ) > 1e-6)
            {
                standardCode = string.Format(inv,
                    "G00 X{0:0.###} Z{1:0.###}\nG75 R{2:0.###}\nG75 X{3:0.###} Z{4:0.###} P{5} Q{6} F{7:0.###}{8}",
                    approachX, compStartZ, retractR, roughTargetX, compEndZ, pMicrons, qMicrons, feedRate, finishStdBlock);
            }
            else
            {
                standardCode = string.Format(inv,
                    "G00 X{0:0.###} Z{1:0.###}\nG75 R{2:0.###}\nG75 X{3:0.###} P{4} F{5:0.###}{6}",
                    approachX, compStartZ, retractR, roughTargetX, pMicrons, feedRate, finishStdBlock);
            }

            if (considerToolWidth)
            {
                string signStr = isLeftToRight ? "+" : "-";
                string zStartExpr = activeFinishWithAllowance
                    ? string.Format(inv, "[{0:0.###}{1}#{2}{1}{3:0.###}]", startZ, signStr, macroVariableNumber, finishAllowanceZ)
                    : string.Format(inv, "[{0:0.###}{1}#{2}]", startZ, signStr, macroVariableNumber);

                if (qMicrons > 0 && Math.Abs(compEndZ - compStartZ) > 1e-6)
                {
                    macroCode = string.Format(inv,
                        "#{0}={1:0.###}\nG00 X{2:0.###} Z{3}\nG75 R{4:0.###}\nG75 X{5:0.###} Z{6:0.###} P{7} Q{8} F{9:0.###}{10}",
                        macroVariableNumber, toolWidth, approachX, zStartExpr, retractR, roughTargetX, compEndZ, pMicrons, qMicrons, feedRate, finishMacroBlock);
                }
                else
                {
                    macroCode = string.Format(inv,
                        "#{0}={1:0.###}\nG00 X{2:0.###} Z{3}\nG75 R{4:0.###}\nG75 X{5:0.###} P{6} F{7:0.###}{8}",
                        macroVariableNumber, toolWidth, approachX, zStartExpr, retractR, roughTargetX, pMicrons, feedRate, finishMacroBlock);
                }
            }
        }
        else
        {
            if (!useAxisOvertravel)
                infos.Add("Перебег за ось отключён: отрезка завершится ровно в X0 (возможен небольшой заусенец в центре).");

            double partOffShift = (considerToolWidth && isLeftToRight) ? -toolWidth : 0.0;
            compStartZ = startZ + partOffShift;
            compEndZ = compStartZ;

            double g75TargetX = targetX;
            string finishG01Line = string.Empty;

            if (reduceFeedNearCenter && stockDiameter > 6.5 && targetX < 6.0)
            {
                g75TargetX = 6.0;
                double halfFeed = Math.Round(feedRate / 2.0, 4);
                finishG01Line = string.Format(inv, "\nG01 X{0:0.###} F{1:0.####}", targetX, halfFeed);
                infos.Add($"Снижение подачи у оси: цикл G75 до Ø6 мм (где Vc резко падает), далее плавная дорезка G01 до X{targetX:0.###} на подаче F{halfFeed:0.####} без обламывания бобышки.");
            }

            standardCode = string.Format(inv,
                "G00 X{0:0.###} Z{1:0.###}\nG75 R{2:0.###}\nG75 X{3:0.###} P{4} F{5:0.###}{6}",
                approachX, compStartZ, retractR, g75TargetX, pMicrons, feedRate, finishG01Line);

            if (considerToolWidth)
            {
                string zPartOffExpr = isLeftToRight
                    ? string.Format(inv, "[{0:0.###}-#{1}]", startZ, macroVariableNumber)
                    : string.Format(inv, "{0:0.###}", startZ);

                macroCode = string.Format(inv,
                    "#{0}={1:0.###}\nG00 X{2:0.###} Z{3}\nG75 R{4:0.###}\nG75 X{5:0.###} P{6} F{7:0.###}{8}",
                    macroVariableNumber, toolWidth, approachX, zPartOffExpr, retractR, g75TargetX, pMicrons, feedRate, finishG01Line);
            }
        }

        double grooveWidthMm = !isPartOffMode
            ? Math.Abs(endZ - startZ)
            : (considerToolWidth ? toolWidth : 0.0);

        int safeRpm = spindleRpm > 0 ? spindleRpm : 1000;
        double totalPathMm;
        double totalSeconds;

        if (isPartOffMode && reduceFeedNearCenter && stockDiameter > 6.5 && targetX < 6.0)
        {
            double mainRadialDepth = (stockDiameter - 6.0) / 2.0;
            double finishRadialDepth = (6.0 - targetX) / 2.0;

            totalPathMm = mainRadialDepth + finishRadialDepth;
            var (secMain, _) = ComputeMachiningTime(mainRadialDepth, feedRate, safeRpm);
            var (secFinish, _) = ComputeMachiningTime(finishRadialDepth, feedRate / 2.0, safeRpm);
            totalSeconds = secMain + secFinish;
        }
        else if (!isPartOffMode && activeFinishPass)
        {
            double roughRadialDepth = (stockDiameter - roughTargetX) / 2.0;
            double roughPathMm = roughRadialDepth * passesCount;
            totalPathMm = roughPathMm + finishPathMm;

            var (secRough, _) = ComputeMachiningTime(roughPathMm, feedRate, safeRpm);
            var (secFinish, _) = ComputeMachiningTime(finishPathMm, finishFeed, safeRpm);
            totalSeconds = secRough + secFinish;
        }
        else
        {
            double effectiveRadialDepth = isPartOffMode && targetX < 0
                ? radialDepth + Math.Abs(targetX) / 2.0
                : radialDepth;

            totalPathMm = effectiveRadialDepth * passesCount;
            var (sec, _) = ComputeMachiningTime(totalPathMm, feedRate, safeRpm);
            totalSeconds = sec;
        }

        string machiningText = totalSeconds < 60.0
            ? string.Format(inv, "{0:0.##} сек", totalSeconds)
            : string.Format(inv, "{0} мин {1:0.#} сек", (int)(totalSeconds / 60.0), totalSeconds % 60.0);

        var result = new GrooveCalculationResult
        {
            IsValid = true,
            HasWarning = warnings.Count > 0,
            WarningMessage = string.Join("\n", warnings),
            HasInfo = infos.Count > 0,
            InfoMessage = string.Join("\n", infos),
            IsPartOffMode = isPartOffMode,
            IsLeftToRight = isLeftToRight,
            ConsiderToolWidth = considerToolWidth,
            IncludeFinishPass = activeFinishPass,
            IsFinishPassWithAllowance = activeFinishWithAllowance,
            ApproachDiameterX = Math.Round(approachX, 3),
            TargetDiameterX = Math.Round(targetX, 3),
            RoughTargetDiameterX = Math.Round(roughTargetX, 3),
            RadialDepth = Math.Round(radialDepth, 3),
            GrooveWidthMm = Math.Round(grooveWidthMm, 3),
            ComputedStartZ = Math.Round(compStartZ, 3),
            ComputedEndZ = Math.Round(compEndZ, 3),
            StepZMm = Math.Round(stepZMm, 3),
            StepQMicrons = qMicrons,
            StepPMicrons = pMicrons,
            PassesCount = passesCount,
            StandardGCodeSnippet = standardCode,
            MacroGCodeSnippet = macroCode,
            MinBladeOverhangMm = minOverhang,
            MaxBladeOverhangMm = maxOverhang,
            BladeOverhangRangeText = overhangText,
            TotalCuttingPathMm = Math.Round(totalPathMm, 2),
            SpindleRpmUsed = safeRpm,
            MachiningTimeSeconds = Math.Round(totalSeconds, 2),
            MachiningTimeText = machiningText
        };

        BuildGrooveCanvasCoordinates(result, startZ, endZ, considerToolWidth ? toolWidth : 3.0);
        return result;
    }

    private static void BuildGrooveCanvasCoordinates(
                                                    GrooveCalculationResult res,
                                                    double rawStartZ,
                                                    double rawEndZ,
                                                    double toolWidthMm)
    {

        var inv = CultureInfo.InvariantCulture;
        
        const double canvasW = 320.0;
        const double canvasH = 200.0;

        // Ось вращения X0 (в режиме отрезки приподнимаем её, чтобы было видно переход ниже оси при X < 0)
        double axisY = res.IsPartOffMode ? 158.0 : 172.0;
        const double stockTopY = 74.0;
        res.CenterLineY = axisY;

        // Вертикальная зелёная линия ноля детали Z0
        double zeroX = res.IsLeftToRight ? 54.0 : 266.0;
        res.ZeroLineX = zeroX;

        double leftWallX = 36.0;
        double rightWallX = 284.0;

        double toolPxW = Math.Clamp(toolWidthMm * 7.0, 14.0, 32.0);
        res.ToolWidthPx = toolPxW;
        res.ToolHeightPx = 38.0;

        double gLeftX, gRightX, bottomY;

        if (!res.IsPartOffMode)
        {
            // НАРУЖНАЯ КАНАВКА: ширина на эскизе пропорциональна ширине канавки
            double grooveWidthMm = Math.Max(toolWidthMm, Math.Abs(rawEndZ - rawStartZ));
            double groovePxW = Math.Clamp(grooveWidthMm * 7.0, toolPxW, 130.0);

            if (!res.IsLeftToRight)
            {
                // Справа налево: правая стенка ближе к Z0
                gRightX = 212.0;
                gLeftX = gRightX - groovePxW;
            }
            else
            {
                // Слева направо: левая стенка ближе к Z0
                gLeftX = 108.0;
                gRightX = gLeftX + groovePxW;
            }

            bottomY = 122.0;

            if (res.IsFinishPassWithAllowance && res.GrooveWidthMm > 1e-6 && res.RadialDepth > 1e-6)
            {
                res.ShowFinishAllowanceLayer = true;

                double allowXMm = Math.Max(0.0, (res.RoughTargetDiameterX - res.TargetDiameterX) / 2.0);
                double allowZMm = Math.Max(0.0, Math.Abs(res.ComputedStartZ - (res.IsLeftToRight ? rawStartZ + toolWidthMm : rawStartZ - toolWidthMm)));

                // Масштабируем толщину слоя припуска на схеме (минимум 4 px для наглядности, но не шире 30% канавки)
                double pxPerMmX = (bottomY - stockTopY) / res.RadialDepth;
                double pxPerMmZ = groovePxW / Math.Max(toolWidthMm, res.GrooveWidthMm);

                double allowPxY = allowXMm > 1e-5 ? Math.Clamp(allowXMm * pxPerMmX, 4.0, (bottomY - stockTopY) * 0.35) : 0.0;
                double allowPxZ = allowZMm > 1e-5 ? Math.Clamp(allowZMm * pxPerMmZ, 4.0, groovePxW * 0.28) : 0.0;

                double roughLeftX = gLeftX + allowPxZ;
                double roughRightX = gRightX - allowPxZ;
                double roughBottomY = bottomY - allowPxY;

                // Замкнутый U-образный полигон припуска между черновым и чистовым контурами
                res.FinishAllowancePolygonPoints = string.Format(inv,
                    "{0:F1},{1:F1} {0:F1},{2:F1} {3:F1},{2:F1} {3:F1},{1:F1} {4:F1},{1:F1} {4:F1},{5:F1} {6:F1},{5:F1} {6:F1},{1:F1}",
                    gLeftX, stockTopY, bottomY, gRightX, roughRightX, roughBottomY, roughLeftX);

                // Линия чернового дна и стенок после цикла G75
                res.RoughOutlinePoints = string.Format(inv,
                    "{0:F1},{1:F1} {0:F1},{2:F1} {3:F1},{2:F1} {3:F1},{1:F1}",
                    roughLeftX, stockTopY, roughBottomY, roughRightX);
            }
            else
            {
                res.ShowFinishAllowanceLayer = false;
            }
        }
        else
        {
            // ОТРЕЗКА ДЕТАЛИ: узкая прорезь до оси X0 или ниже неё (при перебеге X < 0)
            gLeftX = 160.0 - toolPxW / 2.0;
            gRightX = 160.0 + toolPxW / 2.0;

            double overtravelPx = 0.0;
            if (res.TargetDiameterX < 0 && res.ApproachDiameterX > 0)
            {
                double pxPerMm = (axisY - stockTopY) / (res.ApproachDiameterX / 2.0);
                overtravelPx = Math.Clamp(Math.Abs(res.TargetDiameterX) * pxPerMm, 4.0, 18.0);
            }

            bottomY = axisY + overtravelPx;
            res.ShowFinishAllowanceLayer = false;
        }

        // Контур заготовки с прямоугольной канавкой (или прорезью до оси)
        res.PartBodyPoints = string.Format(inv,
            "{0:F1},{1:F1} {0:F1},{2:F1} {3:F1},{2:F1} {3:F1},{4:F1} {5:F1},{4:F1} {5:F1},{2:F1} {6:F1},{2:F1} {6:F1},{1:F1}",
            rightWallX, axisY, stockTopY, gRightX, bottomY, gLeftX, leftWallX);

        res.PartOutlinePoints = string.Format(inv,
            "{0:F1},{1:F1} {0:F1},{2:F1} {3:F1},{2:F1} {3:F1},{4:F1} {5:F1},{4:F1} {5:F1},{2:F1} {6:F1},{2:F1}",
            rightWallX, axisY, stockTopY, gRightX, bottomY, gLeftX, leftWallX);

        // Положение пластины резца в точке первого врезания
        double toolLeftX = !res.IsLeftToRight ? (gRightX - toolPxW) : gLeftX;
        double toolTopY = 20.0;

        res.ToolLeftX = toolLeftX;
        res.ToolTopY = toolTopY;

        // Маркер привязанной кромки резца: левая вершина при движении справа налево, правая — слева направо
        double anchorX = !res.IsLeftToRight ? toolLeftX : (toolLeftX + toolPxW);
        double anchorY = toolTopY + res.ToolHeightPx;
        res.AnchorDotX = anchorX;
        res.AnchorDotY = anchorY;

        // Вертикальная стрелка врезания G75 вдоль привязанной кромки
        double plungeStartY = anchorY + 4.0;
        double plungeEndY = bottomY - 3.0;
        res.PlungeX = anchorX;
        res.PlungeStartY = plungeStartY;
        res.PlungeEndY = plungeEndY;

        res.PlungeArrowPoints = string.Format(inv,
            "{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{3:F1}",
            anchorX, plungeEndY, anchorX - 3.5, plungeEndY - 7.0, anchorX + 3.5, plungeEndY - 7.0);

        // Подписи координат Z_старт, Z_кон и X_кон
        res.ZStartLabelText = string.Format(inv, "Z{0:0.##}", res.ComputedStartZ);
        res.LabelZStartX = Math.Clamp(anchorX - 18.0, 6.0, canvasW - 64.0);
        res.LabelZStartY = 6.0;

        if (!res.IsPartOffMode)
        {
            res.ZWallStartLabelText = string.Format(inv, "Z{0:0.##}", rawStartZ);
            double startWallX = !res.IsLeftToRight ? gRightX : gLeftX;
            res.LabelZWallStartX = Math.Clamp(!res.IsLeftToRight ? (startWallX + 4.0) : (startWallX - 34.0), 6.0, canvasW - 64.0);
            res.LabelZWallStartY = stockTopY - 16.0;

            res.ZEndLabelText = string.Format(inv, "Z{0:0.##}", res.ComputedEndZ);
            double endEdgeX = !res.IsLeftToRight ? gLeftX : gRightX;
            res.LabelZEndX = Math.Clamp(endEdgeX - 18.0, 6.0, canvasW - 64.0);
            res.LabelZEndY = stockTopY - 16.0;
        }
        else
        {
            res.ZWallStartLabelText = string.Empty;
            res.ZEndLabelText = string.Empty;
        }

        res.BottomDiaLabelText = string.Format(inv, "X{0:0.##}", res.TargetDiameterX);
        res.LabelBottomDiaX = Math.Clamp((gLeftX + gRightX) / 2.0 - 18.0, 6.0, canvasW - 64.0);
        res.LabelBottomDiaY = Math.Min(canvasH - 16.0, bottomY + 4.0);

        if (!res.IsPartOffMode && res.GrooveWidthMm > 1e-6)
        {
            res.ShowWidthDimension = true;
            double dimY = bottomY + 22.0;
            res.WidthDimLeftX = gLeftX;
            res.WidthDimRightX = gRightX;
            res.WidthDimY = dimY;

            if (gRightX - gLeftX >= 22.0)
            {
                res.WidthLeftArrowPoints = string.Format(inv, "{0:F1},{1:F1} {2:F1},{3:F1} {2:F1},{4:F1}",
                    gLeftX, dimY, gLeftX + 6.0, dimY - 2.5, dimY + 2.5);
                res.WidthRightArrowPoints = string.Format(inv, "{0:F1},{1:F1} {2:F1},{3:F1} {2:F1},{4:F1}",
                    gRightX, dimY, gRightX - 6.0, dimY - 2.5, dimY + 2.5);
            }
            else
            {
                res.WidthLeftArrowPoints = string.Format(inv, "{0:F1},{1:F1} {2:F1},{3:F1} {2:F1},{4:F1}",
                    gLeftX, dimY, gLeftX - 6.0, dimY - 2.5, dimY + 2.5);
                res.WidthRightArrowPoints = string.Format(inv, "{0:F1},{1:F1} {2:F1},{3:F1} {2:F1},{4:F1}",
                    gRightX, dimY, gRightX + 6.0, dimY - 2.5, dimY + 2.5);
            }

            res.WidthLabelText = string.Format(inv, "W={0:0.###}", res.GrooveWidthMm);
            res.LabelWidthX = Math.Clamp((gLeftX + gRightX) / 2.0 - 16.0, 6.0, canvasW - 64.0);
            res.LabelWidthY = dimY - 13.0;

            // Размещаем подпись X... ниже размерной линии W, чтобы они не пересекались
            res.BottomDiaLabelText = string.Format(inv, "X{0:0.##}", res.TargetDiameterX);
            res.LabelBottomDiaX = Math.Clamp((gLeftX + gRightX) / 2.0 - 14.0, 6.0, canvasW - 64.0);
            res.LabelBottomDiaY = dimY + 4.0;

            // Вертикальная размерная линия глубины канавки t (на сторону)
            bool depthOnLeft = !res.IsLeftToRight;
            double dimX = depthOnLeft ? (gLeftX - 18.0) : (gRightX + 18.0);
            res.DepthDimX = dimX;
            res.DepthDimTopY = stockTopY;
            res.DepthDimBottomY = bottomY;
            res.DepthExtStartX = depthOnLeft ? gLeftX : gRightX;

            res.DepthTopArrowPoints = string.Format(inv, "{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{3:F1}",
                dimX, stockTopY, dimX - 2.5, stockTopY + 6.0, dimX + 2.5);
            res.DepthBottomArrowPoints = string.Format(inv, "{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{3:F1}",
                dimX, bottomY, dimX - 2.5, bottomY - 6.0, dimX + 2.5);

            res.DepthLabelText = string.Format(inv, "t={0:0.###}", res.RadialDepth);
            res.LabelDepthX = depthOnLeft
                ? Math.Max(4.0, dimX - 42.0)
                : Math.Min(canvasW - 52.0, dimX + 5.0);
            res.LabelDepthY = (stockTopY + bottomY) / 2.0 - 7.0;
        }
        else
        {
            res.ShowWidthDimension = false;
            res.BottomDiaLabelText = string.Format(inv, "X{0:0.##}", res.TargetDiameterX);
            res.LabelBottomDiaX = Math.Clamp((gLeftX + gRightX) / 2.0 - 18.0, 6.0, canvasW - 64.0);
            res.LabelBottomDiaY = Math.Min(canvasH - 16.0, bottomY + 4.0);
        }
    }

    private static GrooveCalculationResult ErrorGroove(string msg) =>
        new() { IsValid = false, ErrorMessage = msg };

    // =========================================================================
    // РАЗДЕЛ 6: СОПРЯЖЕНИЕ КОНУС — ЦИЛИНДР ДУГОЙ РАДИУСА R (КАРТОЧКА 6)
    // =========================================================================

    public ConeCylinderBlendResult CalculateConeCylinderBlend(
        double cylinderDiameter,
        double cornerZ,
        double coneAngleDeg,
        double blendRadius,
        bool isConcaveFillet,
        bool isLeftToRight,
        bool isHole,
        bool isFrontTurret,
        bool compensateToolNose = false,
        double toolNoseRadius = 0.4,
        bool useFeed = true,
        double feedRate = 0.12,
        int spindleRpm = 1200)
    {
        if (cylinderDiameter < 0)
            return ErrorConeBlend("Диаметр цилиндра D_цил не может быть отрицательным.");
        if (coneAngleDeg <= 0 || coneAngleDeg >= 89.9)
            return ErrorConeBlend("Угол уклона конуса α должен быть в пределах (0°; 90°).");
        if (blendRadius <= 0)
            return ErrorConeBlend("Радиус сопряжения R должен быть больше 0.");
        if (compensateToolNose && toolNoseRadius < 0)
            return ErrorConeBlend("Радиус при вершине пластины rε не может быть отрицательным.");
        if (useFeed && feedRate <= 0)
            return ErrorConeBlend("Рабочая подача F должна быть больше 0.");

        var warnings = new List<string>();
        double effRadius = blendRadius;

        if (compensateToolNose && toolNoseRadius > 0)
        {
            if (!isConcaveFillet)
            {
                // Наружное скругление: центр пластины описывает дугу большего радиуса (R + rε)
                effRadius = blendRadius + toolNoseRadius;
            }
            else
            {
                // Вогнутая галтель: центр пластины описывает дугу меньшего радиуса (R - rε)
                if (toolNoseRadius >= blendRadius)
                {
                    warnings.Add($"Радиус пластины rε ({toolNoseRadius:0.##} мм) больше или равен радиусу галтели R ({blendRadius:0.##} мм) — пластина не сможет сформировать заданную галтель.");
                    effRadius = 0.05;
                }
                else
                {
                    effRadius = blendRadius - toolNoseRadius;
                }
            }
        }

        double alphaRad = coneAngleDeg * DegToRad;
        double halfAlphaRad = alphaRad / 2.0;

        // Расстояние от теоретической вершины угла до точек касания по биссектрисе
        double tangentDist = effRadius * Math.Tan(halfAlphaRad);
        double deltaZ = tangentDist;
        double deltaRadial = tangentDist * Math.Sin(alphaRad);
        double deltaX = 2.0 * deltaRadial;

        // Направление подачи по оси Z (-1 справа налево, +1 слева направо)
        double dirZ = isLeftToRight ? 1.0 : -1.0;

        // Направление конуса по X (для вала: выпуклый конус растёт к цилиндру, галтель уходит вверх от цилиндра;
        // для отверстия — зеркально по радиусу)
        double signX = (!isHole)
            ? (isConcaveFillet ? 1.0 : -1.0)
            : (isConcaveFillet ? -1.0 : 1.0);

        double startX, startZ, endX, endZ;
        double centerOffsetI, centerOffsetK;

        if (!isConcaveFillet)
        {
            // ВЫПУКЛЫЙ ПЕРЕХОД: Конус → Цилиндр
            // Начало дуги — на образующей конуса (до вершины угла на tangentDist)
            startZ = cornerZ - dirZ * tangentDist * Math.Cos(alphaRad);
            startX = cylinderDiameter + signX * deltaX;

            // Конец дуги — на цилиндре D_цил (после вершины угла на tangentDist)
            endZ = cornerZ + dirZ * tangentDist;
            endX = cylinderDiameter;

            // Центр окружности лежит на нормали к конусу в точке Start
            // Нормаль внутрь металла: по радиусу к оси детали (-signX), по Z вперёд по подаче (+dirZ * sin(α))
            centerOffsetI = -signX * effRadius * Math.Cos(alphaRad);
            centerOffsetK = dirZ * effRadius * Math.Sin(alphaRad);
        }
        else
        {
            // ВОГНУТАЯ ГАЛТЕЛЬ: Цилиндр → Конус
            // Начало дуги — на цилиндре D_цил (до вершины угла на tangentDist)
            startZ = cornerZ - dirZ * tangentDist;
            startX = cylinderDiameter;

            // Конец дуги — на образующей конуса (после вершины угла на tangentDist)
            endZ = cornerZ + dirZ * tangentDist * Math.Cos(alphaRad);
            endX = cylinderDiameter + signX * deltaX;

            // Центр окружности от точки старта на цилиндре: перпендикулярно оси Z в сторону от металла (+signX * R)
            centerOffsetI = signX * effRadius;
            centerOffsetK = 0.0;
        }

        // Определение G02 / G03 с учётом расположения резцедержателя
        bool isCcwRear = (!isConcaveFillet && !isHole) || (isConcaveFillet && isHole);
        if (isLeftToRight) isCcwRear = !isCcwRear;
        bool isG03 = isFrontTurret ? !isCcwRear : isCcwRear;
        string gCmd = isG03 ? "G03" : "G02";

        if (startX < 0 || endX < 0)
            warnings.Add("Одна из расчётных точек сопряжения уходит за ось симметрии (X < 0).");

        if (useFeed && feedRate >= 0.8)
            warnings.Add($"Критически высокая подача F = {feedRate:0.###} мм/об — проверьте десятичную точку.");
        else if (useFeed && feedRate > 0.35)
            warnings.Add($"Повышенная подача F = {feedRate:0.###} мм/об на сопряжении конуса — возможна огранка дуги.");

        var inv = CultureInfo.InvariantCulture;
        double approachFeed = Math.Round(feedRate * 1.5, 3);
        string linearFeedPart = useFeed ? string.Format(inv, " F{0:0.###}", approachFeed) : string.Empty;
        string arcFeedPart = useFeed ? string.Format(inv, " F{0:0.###}", feedRate) : string.Empty;

        string gcodeR = string.Format(inv,
            "G01 X{0:0.###} Z{1:0.###}{6}\n{2} X{3:0.###} Z{4:0.###} R{5:0.###}{7}",
            startX, startZ, gCmd, endX, endZ, effRadius, linearFeedPart, arcFeedPart);

        string gcodeIK = string.Format(inv,
            "G01 X{0:0.###} Z{1:0.###}{7}\n{2} X{3:0.###} Z{4:0.###} I{5:0.###} K{6:0.###}{8}",
            startX, startZ, gCmd, endX, endZ, centerOffsetI, centerOffsetK, linearFeedPart, arcFeedPart);

        // Центральный угол дуги сопряжения равен углу уклона конуса α
        double arcLengthMm = effRadius * alphaRad;
        int safeRpm = spindleRpm > 0 ? spindleRpm : 1200;
        double effectiveFeed = useFeed ? feedRate : 0.12;
        var (machiningSec, machiningText) = ComputeMachiningTime(arcLengthMm, effectiveFeed, safeRpm);

        var result = new ConeCylinderBlendResult
        {
            IsValid = true,
            HasWarning = warnings.Count > 0,
            WarningMessage = string.Join("\n", warnings),
            IsConcaveFillet = isConcaveFillet,
            IsLeftToRight = isLeftToRight,
            IsHole = isHole,
            IsFrontTurret = isFrontTurret,
            CylinderDiameter = Math.Round(cylinderDiameter, 3),
            CornerZ = Math.Round(cornerZ, 3),
            ConeAngleDeg = Math.Round(coneAngleDeg, 3),
            BlendRadius = Math.Round(blendRadius, 3),
            EffectiveArcRadius = Math.Round(effRadius, 3),
            TangentOffsetZ = Math.Round(deltaZ, 3),
            TangentDeltaX = Math.Round(deltaX, 3),
            GCodeCommand = gCmd,
            StartX = Math.Round(startX, 3),
            StartZ = Math.Round(startZ, 3),
            EndX = Math.Round(endX, 3),
            EndZ = Math.Round(endZ, 3),
            CenterOffsetI = Math.Round(centerOffsetI, 3),
            CenterOffsetK = Math.Round(centerOffsetK, 3),
            ArcLengthMm = Math.Round(arcLengthMm, 3),
            SpindleRpmUsed = safeRpm,
            MachiningTimeSeconds = machiningSec,
            MachiningTimeText = machiningText,
            GCodeRadiusSnippet = gcodeR,
            GCodeIKSnippet = gcodeIK
        };

        BuildConeCylinderBlendCanvas(result);
        return result;
    }

    private static void BuildConeCylinderBlendCanvas(ConeCylinderBlendResult res)
    {
        const double canvasW = 320.0;
        const double canvasH = 200.0;

        // Строим базовую геометрию для "Справа налево" (вал, металл снизу)
        double cylY = res.IsHole ? 66.0 : 104.0;
        double cornerX = 165.0;
        double cornerY = cylY;

        double alphaRad = Math.Clamp(res.ConeAngleDeg, 10.0, 65.0) * DegToRad;
        double rPx = 46.0;
        double tPx = Math.Clamp(rPx * Math.Tan(alphaRad / 2.0), 14.0, 38.0);

        double startX, startY, endX, endY, coneEndX, coneEndY, cylEndX;

        if (!res.IsConcaveFillet)
        {
            // Конус справа -> дуга -> Цилиндр слева
            double dirY = res.IsHole ? -1.0 : 1.0; // для вала конус идёт снизу вверх к цилиндру
            startX = cornerX + tPx * Math.Cos(alphaRad);
            startY = cornerY + dirY * tPx * Math.Sin(alphaRad);

            endX = cornerX - tPx;
            endY = cornerY;

            coneEndX = cornerX + 85.0 * Math.Cos(alphaRad);
            coneEndY = cornerY + dirY * 85.0 * Math.Sin(alphaRad);
            cylEndX = 42.0;
        }
        else
        {
            // Цилиндр справа -> дуга (галтель) -> Конус слева вверх
            double dirY = res.IsHole ? 1.0 : -1.0; // для вала конус поднимается от цилиндра вверх
            startX = cornerX + tPx;
            startY = cornerY;

            endX = cornerX - tPx * Math.Cos(alphaRad);
            endY = cornerY + dirY * tPx * Math.Sin(alphaRad);

            cylEndX = 278.0;
            coneEndX = cornerX - 85.0 * Math.Cos(alphaRad);
            coneEndY = cornerY + dirY * 85.0 * Math.Sin(alphaRad);
        }

        if (res.IsLeftToRight)
        {
            startX = canvasW - startX;
            endX = canvasW - endX;
            cornerX = canvasW - cornerX;
            coneEndX = canvasW - coneEndX;
            cylEndX = canvasW - cylEndX;
        }

        res.CanvasStartX = startX;
        res.CanvasStartY = startY;
        res.CanvasEndX = endX;
        res.CanvasEndY = endY;
        res.CanvasCornerX = cornerX;
        res.CanvasCornerY = cornerY;
        res.CenterLineY = res.IsHole ? 176.0 : 176.0;
        res.ZeroLineX = cornerX;
        // Направление дуги SweepFlag для WPF Path
        double cross = (cornerX - startX) * (endY - startY) - (cornerY - startY) * (endX - startX);
        int sweepFlag = cross > 0 ? 1 : 0;
        var inv = CultureInfo.InvariantCulture;
        res.ArcPathData = string.Format(inv,
        "M {0:F1},{1:F1} A {2:F1},{2:F1} 0 0 {3} {4:F1},{5:F1}",
        startX, startY, rPx, sweepFlag, endX, endY);
        double bodyBaseY = res.IsHole ? 24.0 : 168.0;
        if (!res.IsConcaveFillet)
        {
            res.PartOutlinePathData = string.Format(inv,
            "M {0:F1},{1:F1} L {2:F1},{3:F1} M {4:F1},{5:F1} L {6:F1},{5:F1}",
            coneEndX, coneEndY, startX, startY, endX, endY, cylEndX);
            res.PartBodyPathData = string.Format(inv,
            "M {0:F1},{1:F1} L {0:F1},{2:F1} L {3:F1},{4:F1} A {5:F1},{5:F1} 0 0 {6} {7:F1},{8:F1} L {9:F1},{8:F1} L {9:F1},{1:F1} Z",
            coneEndX, bodyBaseY, coneEndY, startX, startY, rPx, sweepFlag, endX, endY, cylEndX);
        }
        else
        {
            res.PartOutlinePathData = string.Format(inv,
            "M {0:F1},{1:F1} L {2:F1},{1:F1} M {3:F1},{4:F1} L {5:F1},{6:F1}",
            cylEndX, startY, startX, endX, endY, coneEndX, coneEndY);
            res.PartBodyPathData = string.Format(inv,
            "M {0:F1},{1:F1} L {0:F1},{2:F1} L {3:F1},{2:F1} A {4:F1},{4:F1} 0 0 {5} {6:F1},{7:F1} L {8:F1},{9:F1} L {8:F1},{1:F1} Z",
            cylEndX, bodyBaseY, startY, startX, rPx, sweepFlag, endX, endY, coneEndX, coneEndY);
        }
        res.StartLabelText = string.Format(inv, "X{0:0.##} Z{1:0.##}", res.StartX, res.StartZ);
        res.EndLabelText = string.Format(inv, "X{0:0.##} Z{1:0.##}", res.EndX, res.EndZ);
        res.CornerLabelText = string.Format(inv, "Угол: X{0:0.##} Z{1:0.##}", res.CylinderDiameter, res.CornerZ);
        double labelOffsetY = res.IsHole ? 12.0 : -22.0;
        res.LabelStartX = Math.Clamp(startX + (res.IsLeftToRight ? -68.0 : 6.0), 6.0, canvasW - 84.0);
        res.LabelStartY = Math.Clamp(startY + labelOffsetY, 4.0, canvasH - 22.0);
        res.LabelEndX = Math.Clamp(endX + (res.IsLeftToRight ? 6.0 : -68.0), 6.0, canvasW - 84.0);
        res.LabelEndY = Math.Clamp(endY + labelOffsetY, 4.0, canvasH - 22.0);
        res.LabelCornerX = Math.Clamp(cornerX - 42.0, 6.0, canvasW - 96.0);
        res.LabelCornerY = Math.Clamp(cornerY + (res.IsHole ? -22.0 : 10.0), 4.0, canvasH - 22.0);
    }
    private static ConeCylinderBlendResult ErrorConeBlend(string msg) =>
    new() { IsValid = false, ErrorMessage = msg };

}
