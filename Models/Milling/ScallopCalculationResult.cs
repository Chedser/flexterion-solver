namespace MechCore.Models.Milling
{
    public class ScallopCalculationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public bool HasWarning { get; set; }
        public string WarningMessage { get; set; } = string.Empty;

        public bool IsBallNose { get; set; }
        public double ToolDiameter { get; set; }
        public double CornerRadius { get; set; }

        public double StepoverAeMm { get; set; }
        public double StepoverPercent { get; set; }

        public double ScallopHeightMicrons { get; set; }
        public double EstimatedRaMicrons { get; set; }
        public string SurfaceQualityClassText { get; set; } = "—";

        public double PathLengthPer100x100Meters { get; set; }
        public double MachiningTimePer100x100Seconds { get; set; }
        public string MachiningTimePer100x100Text { get; set; } = "—";

        // Данные для векторного эскиза на Canvas (320 x 200)
        public string MetalProfilePathData { get; set; } = string.Empty;
        public double LeftToolCenterX { get; set; }
        public double RightToolCenterX { get; set; }
        public double ToolCenterY { get; set; }
        public double ToolArcRadiusPx { get; set; }
        public double PeakLineY { get; set; }
        public double ValleyLineY { get; set; }

        public string StepoverLabelText { get; set; } = string.Empty;
        public string ScallopLabelText { get; set; } = string.Empty;
        public string RadiusLabelText { get; set; } = string.Empty;
    }
}