namespace MechCore.Models.Materials
{
    public class HardnessConversionResult
    {
        public string HrcText { get; }             // Твёрдость по Роквеллу (шкала C)
        public string HbText { get; }              // Твёрдость по Бринеллю
        public string HvText { get; }              // Твёрдость по Виккерсу
        public string TensileStrengthText { get; }// Ориентировочный предел прочности σв, МПа
        public string MachinabilityNote { get; }   // Оценка обрабатываемости и совет по инструменту

        public HardnessConversionResult(
            string hrcText,
            string hbText,
            string hvText,
            string tensileStrengthText,
            string machinabilityNote)
        {
            HrcText = hrcText;
            HbText = hbText;
            HvText = hvText;
            TensileStrengthText = tensileStrengthText;
            MachinabilityNote = machinabilityNote;
        }
    }
}
