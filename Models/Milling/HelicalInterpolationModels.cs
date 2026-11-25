namespace MechCore.Models.Milling
{
    public class HelicalCalculationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public bool HasWarning { get; set; }
        public string WarningMessage { get; set; } = string.Empty;

        public double HoleDiameter { get; set; }
        public double ToolDiameter { get; set; }
        public double HelicalOrbitDiameter { get; set; }
        public double HelicalOrbitRadius { get; set; }

        public double RampAngleDeg { get; set; }
        public double NominalPitchPerRevMm { get; set; }
        public int FullRevolutionsCount { get; set; }
        public double ActualPitchPerRevMm { get; set; }
        public double TotalDepthMm { get; set; }

        public double EdgeFeedPerMin { get; set; }
        public double CenterFeedPerMin { get; set; }
        public int SpindleRpm { get; set; }

        public double TotalHelicalPathMm { get; set; }
        public double MachiningTimeSeconds { get; set; }
        public string MachiningTimeText { get; set; } = "—";

        public string GCodeSnippet { get; set; } = string.Empty;

        // Данные для векторного эскиза на Canvas (320 x 200)
        public double HoleCircleLeft { get; set; }
        public double HoleCircleTop { get; set; }
        public double HoleCircleDiaPx { get; set; }

        public double OrbitCircleLeft { get; set; }
        public double OrbitCircleTop { get; set; }
        public double OrbitCircleDiaPx { get; set; }

        public double ToolCircleLeft { get; set; }
        public double ToolCircleTop { get; set; }
        public double ToolCircleDiaPx { get; set; }

        public double OrbitCenterX { get; set; }
        public double OrbitCenterY { get; set; }
        public double ToolCenterX { get; set; }
        public double ToolCenterY { get; set; }

        public bool ShowCoreWarningZone { get; set; }
        public double CoreCircleLeft { get; set; }
        public double CoreCircleTop { get; set; }
        public double CoreCircleDiaPx { get; set; }

        public string HoleDiaLabelText { get; set; } = string.Empty;
        public string ToolDiaLabelText { get; set; } = string.Empty;
        public string OrbitRadLabelText { get; set; } = string.Empty;
    }
}
