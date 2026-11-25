using System.Collections.Generic;

namespace MechCore.Models.Milling
{
    public enum HolePatternMode
    {
        CirclePcd,       // Делительная окружность (PCD)
        RectangularGrid  // Прямоугольная сетка (Grid)
    }

    public class HolePointItem
    {
        public int Index { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double AngleDeg { get; set; }
        public string AngleText { get; set; } = "—";
        public string GCodeLine { get; set; } = string.Empty;
    }

    public class HoleCanvasDot
    {
        public double CanvasX { get; set; }
        public double CanvasY { get; set; }
        public double LabelX { get; set; }
        public double LabelY { get; set; }
        public string Label { get; set; } = string.Empty;
        public bool IsFirst { get; set; }
    }

    public class HolePatternCalculationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public bool HasWarning { get; set; }
        public string WarningMessage { get; set; } = string.Empty;

        public bool IsCircleMode { get; set; }
        public int TotalHolesCount { get; set; }
        public double StepAngleDeg { get; set; }
        public double MinPitchMm { get; set; }
        public double TotalRapidTravelMm { get; set; }

        public List<HolePointItem> Points { get; set; } = new();
        public string GCodeSnippet { get; set; } = string.Empty;

        // Данные для векторного эскиза на Canvas (320 x 200)
        public double CanvasCenterX { get; set; }
        public double CanvasCenterY { get; set; }
        public double CanvasCircleLeft { get; set; }
        public double CanvasCircleTop { get; set; }
        public double CanvasCircleDiameter { get; set; }
        public string PathPolylinePoints { get; set; } = string.Empty;
        public List<HoleCanvasDot> CanvasDots { get; set; } = new();
        public string CenterLabelText { get; set; } = string.Empty;
        public double CenterLabelX { get; set; }
        public double CenterLabelY { get; set; }
    }
}
