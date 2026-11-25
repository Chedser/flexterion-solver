using System.Collections.Generic;

namespace MechCore.Models.Turning
{
    public enum CncSystemKind
    {
        FanucSystemA,
        FanucSystemBC,
        CustomManual
    }

    public class GCodeLineExplanation
    {
        public string Code { get; }
        public string Description { get; }

        public GCodeLineExplanation(string code, string description)
        {
            Code = code;
            Description = description;
        }
    }

    public class TurningHeaderRequest
    {
        public CncSystemKind SystemKind { get; set; } = CncSystemKind.FanucSystemA;

        // Идентификация программы
        public bool IncludeProgramNumber { get; set; } = true;
        public string ProgramNumberRaw { get; set; } = "0001";

        // Коды строки безопасности
        public bool UseG18 { get; set; } = true;
        public bool UseG21 { get; set; } = true;
        public bool UseG40 { get; set; } = true;
        public bool UseG80 { get; set; } = true;
        public bool UseG97Safety { get; set; } = true;
        public bool UseFeedPerRev { get; set; } = true;
        public bool UseG90 { get; set; } = false;

        // Рабочая система координат
        public string WorkOffset { get; set; } = "G54";

        // Безопасный выход в ноль станка
        public bool UseSafeHomeReturn { get; set; } = true;
        public bool UseSystemAHomeSyntax { get; set; } = true;

        // Ограничение максимальных оборотов
        public bool UseMaxRpmLimit { get; set; } = true;
        public string RpmLimitCode { get; set; } = "G50";
        public string MaxRpmRaw { get; set; } = "2500";

        // Вызов инструмента и корректора
        public bool IncludeToolCall { get; set; } = true;
        public string ToolNumber { get; set; } = "01";
        public string OffsetNumber { get; set; } = "01";

        // Запуск шпинделя и СОЖ
        public bool IncludeSpindleStart { get; set; } = true;
        public bool UseConstantSurfaceSpeed { get; set; } = true; // true = G96, false = G97
        public string SpeedValueRaw { get; set; } = "180";
        public bool IsSpindleClockwise { get; set; } = true; // true = M03, false = M04
        public bool UseCoolant { get; set; } = true; // M08
    }

    public class TurningHeaderResult
    {
        public bool IsValid { get; set; }
        public string GeneratedGCode { get; set; } = string.Empty;
        public List<GCodeLineExplanation> LineExplanations { get; set; } = new();

        public bool HasWarning { get; set; }
        public string WarningMessage { get; set; } = string.Empty;

        public bool HasError { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
