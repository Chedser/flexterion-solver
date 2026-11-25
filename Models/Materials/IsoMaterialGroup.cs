namespace MechCore.Models.Materials
{
    public enum IsoGroupCode
    {
        P, // Стали (синий)
        M, // Нержавеющие стали (жёлтый)
        K, // Чугуны (красный)
        N, // Цветные металлы и сплавы (зелёный)
        S, // Жаропрочные и титановые сплавы (оранжевый)
        H  // Закалённые стали и отбелённый чугун (серый)
    }

    public class IsoMaterialGroupItem
    {
        public IsoGroupCode Code { get; }
        public string CodeLetter => Code.ToString();
        public string Name { get; }
        public string BadgeColor { get; }          // Фирменный цвет ISO 513 (HEX)
        public string BadgeBackground { get; }     // Светлый фон карточки
        public double MachinabilityFactor { get; } // Коэффициент обрабатываемости Km (Сталь 45 = 1.0)
        public string RecommendedVcRange { get; }  // Ориентир скорости резания Vc (м/мин)
        public string CoolantAdvice { get; }       // Рекомендация по СОЖ
        public string ToolingTip { get; }          // Совет по геометрии и сплаву пластины

        public IsoMaterialGroupItem(
            IsoGroupCode code,
            string name,
            string badgeColor,
            string badgeBackground,
            double machinabilityFactor,
            string recommendedVcRange,
            string coolantAdvice,
            string toolingTip)
        {
            Code = code;
            Name = name;
            BadgeColor = badgeColor;
            BadgeBackground = badgeBackground;
            MachinabilityFactor = machinabilityFactor;
            RecommendedVcRange = recommendedVcRange;
            CoolantAdvice = coolantAdvice;
            ToolingTip = toolingTip;
        }
    }
}