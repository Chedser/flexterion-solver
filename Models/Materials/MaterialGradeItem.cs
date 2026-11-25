namespace MechCore.Models.Materials
{
    public class MaterialGradeItem
    {
        public IsoGroupCode IsoGroup { get; }
        public string IsoCode => IsoGroup.ToString();
        public string IsoBadgeColor { get; }

        public string GostGrade { get; }     // Марка по ГОСТ (например: "40Х", "12Х18Н10Т")
        public string DinGrade { get; }      // Аналог DIN / EN (например: "41Cr4 (1.7035)")
        public string AisiGrade { get; }     // Аналог AISI / ASTM (например: "5140", "AISI 304")
        public string GbGrade { get; }       // Аналог GB (Китай, например: "40Cr", "06Cr19Ni10")
        public string TypicalHardness { get; }// Типичная твёрдость в состоянии поставки
        public string ApplicationNote { get; }// Типовое применение в машиностроении

        public MaterialGradeItem(
            IsoGroupCode isoGroup,
            string isoBadgeColor,
            string gostGrade,
            string dinGrade,
            string aisiGrade,
            string gbGrade,
            string typicalHardness,
            string applicationNote)
        {
            IsoGroup = isoGroup;
            IsoBadgeColor = isoBadgeColor;
            GostGrade = gostGrade;
            DinGrade = dinGrade;
            AisiGrade = aisiGrade;
            GbGrade = gbGrade;
            TypicalHardness = typicalHardness;
            ApplicationNote = applicationNote;
        }
    }
}
