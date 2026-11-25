using MechCore.Models.Contour;
using netDxf;
using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MechCore.Services.Contour;

public class DxfContourLoadResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<DxfSegmentItem> Segments { get; set; } = new();

    // Положение рабочего нуля в исходных координатах DXF
    public double AxisY { get; set; }
    public double RightFaceX { get; set; }

    // Диаметр базового отверстия (0 — сплошной вал)
    public double DetectedBoreDiameter { get; set; }

    // Габариты детали в системе координат станка (мм)
    public double PartLength { get; set; }
    public double MaxDiameter { get; set; }
    public double MinDiameter { get; set; }

    // Экранные координаты осей X0 и Z0 для Canvas
    public double ScreenAxisY { get; set; }
    public double ScreenZeroZ_X { get; set; }
    public StockParameters Stock { get; set; } = new();

}

public class SimpleDxfService
{
    public DxfContourLoadResult LoadFromFile(string filePath, double canvasWidth, double canvasHeight)
    {
        if (!File.Exists(filePath))
            return new DxfContourLoadResult { Success = false, Message = "Файл не найден." };

        DxfDocument doc;
        try
        {
            doc = DxfDocument.Load(filePath);
        }
        catch (Exception ex)
        {
            return new DxfContourLoadResult { Success = false, Message = $"Ошибка чтения DXF: {ex.Message}" };
        }

        if (doc == null)
            return new DxfContourLoadResult { Success = false, Message = "Не удалось распознать структуру DXF." };

        var items = new List<DxfSegmentItem>();
        int id = 1;

        // 1. Отрезки (LINE)
        foreach (var line in doc.Entities.Lines)
        {
            if (IsDimensionOrFrame(line)) continue;
            if (Dist(line.StartPoint.X, line.StartPoint.Y, line.EndPoint.X, line.EndPoint.Y) < 1e-4) continue;

            items.Add(new DxfSegmentItem
            {
                Id = id++,
                Kind = DxfPrimitiveKind.Line,
                RawX1 = line.StartPoint.X,
                RawY1 = line.StartPoint.Y,
                RawX2 = line.EndPoint.X,
                RawY2 = line.EndPoint.Y
            });
        }

        // 2. Дуги (ARC)
        foreach (var arc in doc.Entities.Arcs)
        {
            if (IsDimensionOrFrame(arc) || arc.Radius < 1e-4) continue;

            double a1 = arc.StartAngle * Math.PI / 180.0;
            double a2 = arc.EndAngle * Math.PI / 180.0;

            items.Add(new DxfSegmentItem
            {
                Id = id++,
                Kind = DxfPrimitiveKind.Arc,
                Radius = arc.Radius,
                RawX1 = arc.Center.X + arc.Radius * Math.Cos(a1),
                RawY1 = arc.Center.Y + arc.Radius * Math.Sin(a1),
                RawX2 = arc.Center.X + arc.Radius * Math.Cos(a2),
                RawY2 = arc.Center.Y + arc.Radius * Math.Sin(a2),
                IsCcw = true
            });
        }

        // 3. Полилинии (LWPOLYLINE из КОМПАС-3D)
        foreach (var poly in doc.Entities.Polylines2D)
        {
            if (IsDimensionOrFrame(poly) || poly.Vertexes.Count < 2) continue;

            int count = poly.IsClosed ? poly.Vertexes.Count : poly.Vertexes.Count - 1;
            for (int i = 0; i < count; i++)
            {
                var v1 = poly.Vertexes[i];
                var v2 = poly.Vertexes[(i + 1) % poly.Vertexes.Count];
                if (Dist(v1.Position.X, v1.Position.Y, v2.Position.X, v2.Position.Y) < 1e-4) continue;

                if (Math.Abs(v1.Bulge) < 1e-5)
                {
                    items.Add(new DxfSegmentItem
                    {
                        Id = id++,
                        Kind = DxfPrimitiveKind.Line,
                        RawX1 = v1.Position.X,
                        RawY1 = v1.Position.Y,
                        RawX2 = v2.Position.X,
                        RawY2 = v2.Position.Y
                    });
                }
                else
                {
                    items.Add(ArcFromBulge(id++, v1.Position.X, v1.Position.Y, v2.Position.X, v2.Position.Y, v1.Bulge));
                }
            }
        }

        if (items.Count == 0)
            return new DxfContourLoadResult { Success = false, Message = "В файле не найдено геометрических элементов." };

        // 4. Проверка замкнутости контура
        var openEndpoints = FindOpenEndpoints(items, tolerance: 0.02);
        if (openEndpoints.Count > 0)
        {
            string pts = string.Join(", ", openEndpoints.Take(4).Select(p => $"({p.X:0.##}; {p.Y:0.##})"));
            return new DxfContourLoadResult
            {
                Success = false,
                Message = $"Загрузка отклонена: контур детали незамкнут (разрыв в точках: {pts}). Замкните контур в КОМПАС-3D и повторите импорт."
            };
        }

        // 5. Определение оси вращения X0 по геометрии контура (без осевой линии)
        double minY = items.Min(s => Math.Min(s.RawY1, s.RawY2));
        double maxY = items.Max(s => Math.Max(s.RawY1, s.RawY2));
        double axisY;
        string note;

        // Если контур выгружен полным симметричным сечением (верх + низ) — ось проходит ровно посередине
        if (IsVerticallySymmetric(items, minY, maxY))
        {
            axisY = (minY + maxY) / 2.0;
            var upper = items.Where(s => s.RawY1 >= axisY - 0.05 && s.RawY2 >= axisY - 0.05).ToList();
            if (upper.Count > 0)
                items = upper;

            note = "Ось X0 определена по симметрии полного сечения.";
        }
        else
        {
            // Выгружена только верхняя половина контура — по умолчанию ставим ось на нижнюю линию (сплошной вал)
            axisY = minY;
            note = "Ось X0 установлена по нижней границе контура (для втулки укажите Ø отверстия слева).";
        }

        for (int i = 0; i < items.Count; i++)
            items[i].Id = i + 1;

        double upperMinY = items.Min(s => Math.Min(s.RawY1, s.RawY2));
        double detectedBore = Math.Max(0.0, Math.Round(2.0 * (upperMinY - axisY), 3));
        double rightFaceX = items.Max(s => Math.Max(s.RawX1, s.RawX2));

        var result = new DxfContourLoadResult
        {
            Success = true,
            Message = $"Элементов: {items.Count}. {note}",
            Segments = items,
            AxisY = axisY,
            RightFaceX = rightFaceX,
            DetectedBoreDiameter = detectedBore
        };

        // Предварительный расчёт габаритов для автоподбора заготовки
        double minX = result.Segments.Min(s => Math.Min(s.RawX1, s.RawX2));
        double initMaxDia = Math.Round(result.Segments.Max(s => Math.Max(2.0 * (s.RawY1 - result.AxisY), 2.0 * (s.RawY2 - result.AxisY))), 3);
        double initMinDia = Math.Round(result.Segments.Min(s => Math.Min(2.0 * (s.RawY1 - result.AxisY), 2.0 * (s.RawY2 - result.AxisY))), 3);
        double initLength = Math.Round(result.RightFaceX - minX, 3);

        result.PartLength = initLength;
        result.MaxDiameter = initMaxDia;
        result.MinDiameter = initMinDia;

        ApplyAutoStock(result, StockKind.Bar, 2.0);

        UpdateZeroAndFit(result, canvasWidth, canvasHeight);
        return result;
    }

    // Стандартный ряд диаметров проката (ГОСТ 2590 / сортамент, мм)
    private static readonly double[] StandardBarDiameters =
                                        {
                                        6, 8, 10, 12, 14, 15, 16, 18, 20, 22, 24, 25, 26, 28, 30, 32, 34, 35, 36, 38, 40,
                                        42, 45, 48, 50, 52, 55, 56, 60, 63, 65, 68, 70, 75, 80, 85, 90, 95, 100, 105, 110,
                                        115, 120, 125, 130, 140, 150, 160, 170, 180, 190, 200, 210, 220, 230, 240, 250,
                                        260, 280, 300, 320, 340, 360, 380, 400, 420, 450, 480, 500
                                    };

    public void ApplyAutoStock(DxfContourLoadResult state, StockKind kind, double faceAllowanceMm)
    {
        double targetOuter = state.MaxDiameter + 4.0; // минимум +2 мм припуска на сторону
        double chosenOuter = StandardBarDiameters.FirstOrDefault(d => d >= targetOuter);
        if (chosenOuter <= 0)
            chosenOuter = Math.Ceiling(targetOuter / 5.0) * 5.0;

        double chosenInner = 0;
        if (kind == StockKind.Tube && state.MinDiameter > 4.0)
        {
            double targetInner = Math.Max(0, state.MinDiameter - 4.0);
            chosenInner = Math.Floor(targetInner / 2.0) * 2.0;
        }

        double grip = 15.0;
        double cutoffAndSafe = 5.0;
        double totalLength = Math.Ceiling(state.PartLength + faceAllowanceMm + grip + cutoffAndSafe);

        state.Stock.Kind = kind;
        state.Stock.OuterDiameter = chosenOuter;
        state.Stock.InnerDiameter = chosenInner;
        state.Stock.RightFaceAllowance = faceAllowanceMm;
        state.Stock.ChuckGripLength = grip;
        state.Stock.Length = totalLength;
    }

    public void UpdateZeroAndFit(DxfContourLoadResult state, double canvasWidth, double canvasHeight)
    {
        if (state.Segments.Count == 0) return;

        double faceShift = state.Stock.RightFaceAllowance;

        // 1. Пересчёт координат станка (X — диаметр, Z — ось с нулём справа с учётом припуска торца)
        foreach (var s in state.Segments)
        {
            s.StartX = Math.Round(2.0 * (s.RawY1 - state.AxisY), 3);
            s.StartZ = Math.Round((s.RawX1 - state.RightFaceX) - faceShift, 3);
            s.EndX = Math.Round(2.0 * (s.RawY2 - state.AxisY), 3);
            s.EndZ = Math.Round((s.RawX2 - state.RightFaceX) - faceShift, 3);
        }

        // 1.1. Расчёт габаритов детали (длина L по Z, максимальный и минимальный диаметры по X)
        double minZ = state.Segments.Min(s => Math.Min(s.StartZ, s.EndZ));
        double maxZ = state.Segments.Max(s => Math.Max(s.StartZ, s.EndZ));
        state.PartLength = Math.Round(maxZ - minZ, 3);

        state.MaxDiameter = Math.Round(state.Segments.Max(s => Math.Max(s.StartX, s.EndX)), 3);
        state.MinDiameter = Math.Round(state.Segments.Min(s => Math.Min(s.StartX, s.EndX)), 3);

        // 2. Границы заготовки и кулачков в исходных координатах DXF
        double stockRightRawX = state.RightFaceX;
        double stockLeftRawX = stockRightRawX - state.Stock.Length;
        double chuckVisualWidthMm = Math.Min(state.Stock.ChuckGripLength, 25.0);
        double chuckLeftRawX = stockLeftRawX - 8.0;

        double stockTopRawY = state.AxisY + state.Stock.OuterDiameter / 2.0;
        double stockBottomRawY = state.Stock.Kind == StockKind.Tube
            ? state.AxisY + state.Stock.InnerDiameter / 2.0
            : state.AxisY;

        // 3. Вписываем в Canvas вместе с осями X0, Z0, заготовкой и кулачками
        double partMinX = state.Segments.Min(s => Math.Min(s.RawX1, s.RawX2)) - faceShift;
        double partMaxX = state.Segments.Max(s => Math.Max(s.RawX1, s.RawX2)) - faceShift;

        double minX = Math.Min(partMinX, Math.Min(state.RightFaceX, state.Stock.Length > 0 ? chuckLeftRawX : partMinX));
        double maxX = Math.Max(partMaxX, state.RightFaceX);
        double minY = Math.Min(state.Segments.Min(s => Math.Min(s.RawY1, s.RawY2)), state.AxisY);
        double maxY = Math.Max(state.Segments.Max(s => Math.Max(s.RawY1, s.RawY2)),
            state.Stock.OuterDiameter > 0 ? stockTopRawY : state.AxisY);

        double rawW = Math.Max(maxX - minX, 1e-3);
        double rawH = Math.Max(maxY - minY, 1e-3);

        const double pad = 42.0;
        double scale = Math.Min((canvasWidth - 2 * pad) / rawW, (canvasHeight - 2 * pad) / rawH);

        double offsetX = (canvasWidth - rawW * scale) / 2.0;
        double offsetY = (canvasHeight - rawH * scale) / 2.0;
        var inv = CultureInfo.InvariantCulture;

        state.ScreenAxisY = canvasHeight - (offsetY + (state.AxisY - minY) * scale);
        state.ScreenZeroZ_X = offsetX + (state.RightFaceX - minX) * scale;

        // 4. Экранные координаты заготовки и верхнего кулачка патрона
        state.Stock.ScreenLeft = offsetX + (stockLeftRawX - minX) * scale;
        state.Stock.ScreenTop = canvasHeight - (offsetY + (stockTopRawY - minY) * scale);
        state.Stock.ScreenWidth = Math.Max((stockRightRawX - stockLeftRawX) * scale, 2.0);
        state.Stock.ScreenHeight = Math.Max((stockTopRawY - stockBottomRawY) * scale, 2.0);

        double chuckRightRawX = stockLeftRawX + chuckVisualWidthMm;
        double chuckTopRawY = stockTopRawY + 12.0 / scale;

        state.Stock.ChuckLeft = offsetX + (chuckLeftRawX - minX) * scale;
        state.Stock.ChuckTop = canvasHeight - (offsetY + (chuckTopRawY - minY) * scale);
        state.Stock.ChuckWidth = Math.Max((chuckRightRawX - chuckLeftRawX) * scale, 10.0);
        state.Stock.ChuckHeight = Math.Max((chuckTopRawY - stockTopRawY) * scale, 10.0);

        state.Stock.OuterDiaLabel = string.Format(inv, "Заготовка: Ø{0:0.##} × {1:0.##} мм",
            state.Stock.OuterDiameter, state.Stock.Length);
        state.Stock.LengthLabel = string.Format(inv, "Lзаг = {0:0.##} мм", state.Stock.Length);

        // 5. Экранные координаты сегментов детали (с учётом сдвига на припуск торца)
        foreach (var s in state.Segments)
        {
            s.ScreenX1 = offsetX + ((s.RawX1 - faceShift) - minX) * scale;
            s.ScreenY1 = canvasHeight - (offsetY + (s.RawY1 - minY) * scale);

            s.ScreenX2 = offsetX + ((s.RawX2 - faceShift) - minX) * scale;
            s.ScreenY2 = canvasHeight - (offsetY + (s.RawY2 - minY) * scale);

            if (s.Kind == DxfPrimitiveKind.Arc)
            {
                double rPx = s.Radius * scale;
                int sweepFlag = s.IsCcw ? 0 : 1;
                s.ArcPathData = string.Format(inv, "M {0:F2},{1:F2} A {2:F2},{2:F2} 0 0 {3} {4:F2},{5:F2}",
                    s.ScreenX1, s.ScreenY1, rPx, sweepFlag, s.ScreenX2, s.ScreenY2);
            }
        }
    }

    private static bool IsVerticallySymmetric(IList<DxfSegmentItem> items, double minY, double maxY)
    {
        if (maxY - minY < 1.0) return false;
        double midY = (minY + maxY) / 2.0;

        // Проверяем, что для каждой вершины выше середины есть зеркальная вершина ниже середины
        var vertices = new List<(double X, double Y)>();
        foreach (var s in items)
        {
            vertices.Add((s.RawX1, s.RawY1));
            vertices.Add((s.RawX2, s.RawY2));
        }

        int upperCount = vertices.Count(v => v.Y > midY + 0.1);
        int lowerCount = vertices.Count(v => v.Y < midY - 0.1);
        if (upperCount == 0 || upperCount != lowerCount) return false;

        foreach (var v in vertices.Where(v => v.Y > midY + 0.1))
        {
            double mirrorY = 2.0 * midY - v.Y;
            bool hasMirror = vertices.Any(m => Math.Abs(m.X - v.X) <= 0.05 && Math.Abs(m.Y - mirrorY) <= 0.05);
            if (!hasMirror) return false;
        }

        return true;
    }

    private static List<(double X, double Y)> FindOpenEndpoints(IList<DxfSegmentItem> segments, double tolerance)
    {
        var endpoints = new List<(double X, double Y)>();
        foreach (var s in segments)
        {
            endpoints.Add((s.RawX1, s.RawY1));
            endpoints.Add((s.RawX2, s.RawY2));
        }
        var openPoints = new List<(double X, double Y)>();
        for (int i = 0; i < endpoints.Count; i++)
        {
            int connections = 0;
            for (int j = 0; j < endpoints.Count; j++)
            {
                if (Dist(endpoints[i].X, endpoints[i].Y, endpoints[j].X, endpoints[j].Y) <= tolerance)
                    connections++;
            }
            if (connections < 2)
            {
                bool alreadyAdded = openPoints.Any(p => Dist(p.X, p.Y, endpoints[i].X, endpoints[i].Y) <= tolerance);
                if (!alreadyAdded)
                    openPoints.Add(endpoints[i]);
            }
        }
        return openPoints;
    }
    private static bool IsDimensionOrFrame(EntityObject e)
    {
        string layer = e.Layer?.Name ?? string.Empty;
        return layer.Contains("DIM", StringComparison.OrdinalIgnoreCase) ||
        layer.Contains("РАЗМЕР", StringComparison.OrdinalIgnoreCase) ||
        layer.Contains("РАМК", StringComparison.OrdinalIgnoreCase);
    }
    private static DxfSegmentItem ArcFromBulge(int id, double x1, double y1, double x2, double y2, double bulge)
    {
        double theta = 4.0 * Math.Atan(Math.Abs(bulge));
        double chord = Dist(x1, y1, x2, y2);
        double r = chord / (2.0 * Math.Sin(theta / 2.0));
        return new DxfSegmentItem
        {
            Id = id,
            Kind = DxfPrimitiveKind.Arc,
            RawX1 = x1,
            RawY1 = y1,
            RawX2 = x2,
            RawY2 = y2,
            Radius = r,
            IsCcw = bulge > 0
        };
    }
    private static double Dist(double x1, double y1, double x2, double y2) =>
    Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
}