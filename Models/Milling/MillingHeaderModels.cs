using System.Collections.Generic;
using MechCore.Models.Turning;

namespace MechCore.Models.Milling
{
    public enum MillingCncSystemKind
    {
        FanucIso,       // Fanuc / ISO (стандарт)
        Haas,           // Haas NGC / Classic
        SinumerikIso,   // Siemens Sinumerik (ISO-режим)
        CustomManual    // Другая стойка / Ручной режим
    }

    public enum MillingHomeSyntaxKind
    {
        G91G28Z0,       // G91 G28 Z0 + G90 (Fanuc стандарт)
        G53Z0,          // G00 G53 Z0 (машинные координаты — Haas / современные стойки)
        SupaZ0D0        // SUPA G0 Z0 D0 (Sinumerik безопасный отвод без смещений)
    }

    public enum MillingToolChangeSyntaxKind
    {
        TxxM06,         // T01 M06 (стандарт Fanuc / Haas)
        TxD1M06         // T1 D1 M06 (стандарт Sinumerik)
    }

    public class MillingHeaderRequest
    {
        public MillingCncSystemKind SystemKind { get; set; } = MillingCncSystemKind.FanucIso;

        // 1. Идентификация программы
        public bool IncludeProgramNumber { get; set; } = true;
        public string ProgramNumberRaw { get; set; } = "0001";

        // 2. Коды строки безопасности
        public string WorkPlaneCode { get; set; } = "G17";      // G17 (XY), G18 (XZ), G19 (YZ)
        public bool UseMetricG21 { get; set; } = true;          // G21 (мм) / G71 для Sinumerik
        public bool UseG40 { get; set; } = true;                // Отмена коррекции на радиус фрезы
        public bool UseG49 { get; set; } = true;                // Отмена коррекции на длину инструмента
        public bool UseG80 { get; set; } = true;                // Отмена постоянных циклов сверления/растачивания
        public bool UseG90 { get; set; } = true;                // Абсолютная система координат
        public bool UseG94 { get; set; } = true;                // Минутная подача (мм/мин)

        // 3. Безопасный отвод по Z и рабочая система координат (РСК)
        public bool UseSafeZHome { get; set; } = true;
        public MillingHomeSyntaxKind HomeSyntax { get; set; } = MillingHomeSyntaxKind.G91G28Z0;
        public string WorkOffset { get; set; } = "G54";

        // 4. Вызов и смена инструмента
        public bool IncludeToolCall { get; set; } = true;
        public MillingToolChangeSyntaxKind ToolChangeSyntax { get; set; } = MillingToolChangeSyntaxKind.TxxM06;
        public string ToolNumber { get; set; } = "01";
        public string LengthOffsetNumber { get; set; } = "01";  // Корректор длины H (или D для Sinumerik)

        // 5. Запуск шпинделя, подвод по Z и СОЖ
        public bool IncludeSpindleStart { get; set; } = true;
        public string SpindleRpmRaw { get; set; } = "3000";
        public bool IsSpindleClockwise { get; set; } = true;    // true = M03, false = M04

        public bool UseSafeZApproach { get; set; } = true;      // Подвод к безопасной плоскости (G43 H01 Z50.0)
        public string SafeZHeightRaw { get; set; } = "50";

        public string CoolantCode { get; set; } = "M08";        // M08 (СОЖ), M07 (туман/воздух), "Нет"
    }

    public class MillingHeaderResult
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
