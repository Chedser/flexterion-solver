namespace MechCore.Models.Milling
{
    public class MillingThreadCalculationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public bool HasWarning { get; set; }
        public string WarningMessage { get; set; } = string.Empty;

        public bool IsThreadMillingMode { get; set; }
        public bool IsExternalThread { get; set; }
        public bool IsLeftHandThread { get; set; }

        public double ThreadDiameter { get; set; }
        public double ThreadPitch { get; set; }
        public double RecommendedPreHoleDiameter { get; set; }
        public double ThreadDepthMm { get; set; }

        public double ToolDiameter { get; set; }
        public double OrbitRadius { get; set; }
        public double EdgeFeedPerMin { get; set; }
        public double ProgrammedFeedPerMin { get; set; }
        public int SpindleRpm { get; set; }

        public string DirectionSummaryText { get; set; } = "—";
        public double MachiningTimeSeconds { get; set; }
        public string MachiningTimeText { get; set; } = "—";

        public string GCodeSnippet { get; set; } = string.Empty;

        // Данные для векторного эскиза на Canvas (320 x 200)
        public double ThreadCircleLeft { get; set; }
        public double ThreadCircleTop { get; set; }
        public double ThreadCircleDiaPx { get; set; }

        public double CoreCircleLeft { get; set; }
        public double CoreCircleTop { get; set; }
        public double CoreCircleDiaPx { get; set; }

        public double ToolCircleLeft { get; set; }
        public double ToolCircleTop { get; set; }
        public double ToolCircleDiaPx { get; set; }

        public string ThreadLabelText { get; set; } = string.Empty;
        public string ToolLabelText { get; set; } = string.Empty;
        public string PreHoleLabelText { get; set; } = string.Empty;
        public string CenterLabelText { get; set; } = string.Empty;

    }
}