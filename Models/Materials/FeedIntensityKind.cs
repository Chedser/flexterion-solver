namespace MechCore.Models.Materials
{
    /// <summary>
    /// Уровень интенсивности режимов резания в пределах каталожного диапазона (Min / Avg / Max).
    /// </summary>
    public enum FeedIntensityKind
    {
        Gentle,     // Щадящий (нижняя граница диапазона Min)
        Optimal,    // Оптимальный (середина диапазона, по умолчанию)
        Aggressive  // Форсированный (верхняя граница диапазона Max)
    }
}
