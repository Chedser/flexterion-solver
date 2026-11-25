using System;
using System.Collections.Generic;
using System.Linq;
using MechCore.Models.Materials;

namespace MechCore.Services.Materials
{
    public class MaterialStandardService : IMaterialService
    {
        private readonly List<IsoMaterialGroupItem> _isoGroups;
        private readonly List<MaterialGradeItem> _grades;

        // Опорная таблица перевода твёрдости (HRC, HB, HV, предел прочности σв в МПа) по ISO 18265 / ГОСТ 22761
        private static readonly (double Hrc, double Hb, double Hv, double SigmaB)[] HardnessTable =
        {
            (20.0, 226, 238, 765),
            (22.0, 237, 248, 800),
            (24.0, 247, 260, 835),
            (26.0, 258, 272, 875),
            (28.0, 271, 286, 920),
            (30.0, 286, 302, 970),
            (32.0, 301, 318, 1020),
            (34.0, 319, 336, 1080),
            (36.0, 336, 354, 1140),
            (38.0, 353, 372, 1200),
            (40.0, 371, 392, 1265),
            (42.0, 390, 412, 1335),
            (44.0, 409, 434, 1405),
            (46.0, 432, 458, 1485),
            (48.0, 455, 484, 1570),
            (50.0, 481, 513, 1660),
            (52.0, 512, 544, 1765),
            (54.0, 543, 577, 1875),
            (56.0, 577, 613, 1995),
            (58.0, 615, 653, 2120),
            (60.0, 654, 697, 2260),
            (62.0, 688, 746, 2380),
            (64.0, 722, 800, 2500),
            (65.0, 739, 832, 2560)
        };

        public MaterialStandardService()
        {
            _isoGroups = BuildIsoGroups();
            _grades = BuildGradesDatabase();
        }

        public List<IsoMaterialGroupItem> GetIsoGroups() => _isoGroups;

        public List<MaterialGradeItem> GetAllGrades() => _grades;

        public HardnessConversionResult ConvertHardness(double value, HardnessScale sourceScale)
        {
            // Для мягких сталей ниже шкалы HRC (HB от 80 до 225)
            if (sourceScale == HardnessScale.HB && value >= 80 && value < 226)
            {
                double hvLow = Math.Round(value * 1.05, 0);
                double sigmaLow = Math.Round(value * 3.4, 0);
                return new HardnessConversionResult(
                    "< 20 HRC (не нормируется)",
                    $"{value:0} HB",
                    $"{hvLow:0} HV",
                    $"~ {sigmaLow:0} МПа",
                    "Сырая / отожжённая сталь. Отличная обрабатываемость стандартным твёрдым сплавом.");
            }

            if (sourceScale == HardnessScale.HV && value >= 85 && value < 238)
            {
                double hbLow = Math.Round(value / 1.05, 0);
                double sigmaLow = Math.Round(hbLow * 3.4, 0);
                return new HardnessConversionResult(
                    "< 20 HRC (не нормируется)",
                    $"{hbLow:0} HB",
                    $"{value:0} HV",
                    $"~ {sigmaLow:0} МПа",
                    "Мягкое состояние материала. Рекомендуются позитивные пластины с острыми стружколомами.");
            }

            // Ограничиваем входное значение рабочим диапазоном таблицы ISO 18265
            double clamped = sourceScale switch
            {
                HardnessScale.HRC => Math.Clamp(value, 20.0, 65.0),
                HardnessScale.HB => Math.Clamp(value, 226.0, 739.0),
                HardnessScale.HV => Math.Clamp(value, 238.0, 832.0),
                _ => value
            };

            double hrc = 0, hb = 0, hv = 0, sigma = 0;

            for (int i = 0; i < HardnessTable.Length - 1; i++)
            {
                var p1 = HardnessTable[i];
                var p2 = HardnessTable[i + 1];

                double x1 = GetScaleVal(p1, sourceScale);
                double x2 = GetScaleVal(p2, sourceScale);

                if (clamped >= x1 && clamped <= x2)
                {
                    double t = (x2 - x1) > 0.0001 ? (clamped - x1) / (x2 - x1) : 0.0;
                    hrc = p1.Hrc + t * (p2.Hrc - p1.Hrc);
                    hb = p1.Hb + t * (p2.Hb - p1.Hb);
                    hv = p1.Hv + t * (p2.Hv - p1.Hv);
                    sigma = p1.SigmaB + t * (p2.SigmaB - p1.SigmaB);
                    break;
                }
            }

            string hrcText = $"{hrc:0.0} HRC";
            string hbText = hrc > 48.0 ? $"~ {hb:0} HB (шарик из тв. сплава)" : $"{hb:0} HB";
            string hvText = $"{hv:0} HV";
            string sigmaText = $"~ {sigma:0} МПа";

            string advice = hrc switch
            {
                < 32.0 => "Улучшенная сталь (средняя твёрдость). Стандартные пластины ISO P с износостойким покрытием.",
                < 45.0 => "Повышенная твёрдость. Снизьте скорость резания Vc на 25–35%, используйте жёсткую оправку.",
                < 55.0 => "Закалённая сталь (группа ISO H). Рекомендуются сплавы с мелкозернистой основой или CBN.",
                _ => "Высокая твёрдость (55–65 HRC). Точение только пластинами из кубического нитрида бора (CBN) или шлифование."
            };

            return new HardnessConversionResult(hrcText, hbText, hvText, sigmaText, advice);
        }

        private static double GetScaleVal((double Hrc, double Hb, double Hv, double SigmaB) row, HardnessScale scale) =>
            scale switch
            {
                HardnessScale.HRC => row.Hrc,
                HardnessScale.HB => row.Hb,
                HardnessScale.HV => row.Hv,
                _ => row.Hrc
            };

        private static List<IsoMaterialGroupItem> BuildIsoGroups()
        {
            return new List<IsoMaterialGroupItem>
            {
                new(IsoGroupCode.P, "Стали (углеродистые и легированные)", "#1565C0", "#E3F2FD", 1.00,
                    "140 – 280 м/мин", "Обильная эмульсия (СОЖ)",
                    "Самая широкая группа. Выбирайте пластины с хорошим стружколомом и покрытием TiCN / Al2O3."),

                new(IsoGroupCode.M, "Нержавеющие стали (аустенитные и др.)", "#F9A825", "#FFFDE7", 0.65,
                    "90 – 170 м/мин", "Обильная подача СОЖ под давлением",
                    "Склонны к наклёпу и наростообразованию. Нужны острые позитивные кромки и непрерывная подача."),

                new(IsoGroupCode.K, "Чугуны (серый, ковкий, ВЧ)", "#C62828", "#FFEBEE", 1.15,
                    "150 – 320 м/мин", "Без СОЖ (на сухую) или сжатый воздух",
                    "Стружка ломания (элементная). Избегайте термического удара от СОЖ при прерывистом резании."),

                new(IsoGroupCode.N, "Цветные сплавы (алюминий, латунь, медь)", "#2E7D32", "#E8F5E9", 2.50,
                    "300 – 900 м/мин", "Эмульсия или масляный туман",
                    "Используйте полированные пластины без покрытия с большим передним углом против налипания."),

                new(IsoGroupCode.S, "Жаропрочные сплавы и титан", "#EF6C00", "#FFF3E0", 0.35,
                    "30 – 75 м/мин", "Обязательна СОЖ высокого давления",
                    "Низкая теплопроводность — весь жар идёт в резец. Низкие скорости Vc и мелкозернистый твёрдый сплав."),

                new(IsoGroupCode.H, "Закалённые стали (45 – 65 HRC)", "#455A64", "#ECEFF1", 0.40,
                    "80 – 160 м/мин", "Без СОЖ (сухое точение для CBN)",
                    "Высокие силы резания. Применяйте вставки CBN с отрицательной фаской и жёсткий вылет резца.")
            };
        }

        private static List<MaterialGradeItem> BuildGradesDatabase()
        {
            return new List<MaterialGradeItem>
            {
                // Группа P — Стали
                new(IsoGroupCode.P, "#1565C0", "Ст3сп", "S235JR (1.0038)", "A283 Grade C", "Q235B", "130 – 160 HB", "Неответственные кронштейны, фланцы, сварные детали"),
                new(IsoGroupCode.P, "#1565C0", "Сталь 20", "C22 (1.0402)", "AISI 1020", "20", "130 – 156 HB", "Штуцеры, втулки, детали под цементацию"),
                new(IsoGroupCode.P, "#1565C0", "Сталь 45", "C45 (1.0503)", "AISI 1045", "45", "170 – 210 HB", "Эталонная конструкционная сталь: валы, оси, шестерни"),
                new(IsoGroupCode.P, "#1565C0", "09Г2С", "S355J2G3 (1.0570)", "A572 Grade 50", "Q345B", "140 – 180 HB", "Низколегированная сталь для конструкций под нагрузкой"),
                new(IsoGroupCode.P, "#1565C0", "40Х", "41Cr4 (1.7035)", "AISI 5140", "40Cr", "180 – 220 HB", "Ответственные валы, штоки, болты, улучшаемые детали"),
                new(IsoGroupCode.P, "#1565C0", "30ХГСА", "30CrMnSiA", "—", "30CrMnSiA", "190 – 230 HB", "Высокопрочные оси, фланцы и силовые узлы (хромансиль)"),
                new(IsoGroupCode.P, "#1565C0", "40ХН2МА", "36CrNiMo4 (1.6511)", "AISI 4340", "40CrNiMoA", "210 – 250 HB", "Тяжелонагруженные валы, роторы и зубчатые колёса"),
                new(IsoGroupCode.P, "#1565C0", "У8А", "C80U (1.1525)", "W1-8", "T8A", "187 – 217 HB", "Инструментальная углеродистая сталь (в отожжённом виде)"),

                // Группа M — Нержавеющие стали
                new(IsoGroupCode.M, "#F9A825", "12Х18Н10Т", "X6CrNiTi18-10 (1.4541)", "AISI 321", "06Cr18Ni11Ti", "150 – 190 HB", "Пищевая и химическая арматура, коррозионностойкие валы"),
                new(IsoGroupCode.M, "#F9A825", "08Х18Н10", "X5CrNi18-10 (1.4301)", "AISI 304", "06Cr19Ni10", "150 – 185 HB", "Самая массовая аустенитная нержавейка общего назначения"),
                new(IsoGroupCode.M, "#F9A825", "08Х17Н13М2", "X5CrNiMo17-12-2 (1.4401)", "AISI 316", "06Cr17Ni12Mo2", "160 – 200 HB", "Кислотостойкая нержавейка с молибденом (морская среда)"),
                new(IsoGroupCode.M, "#F9A825", "20Х13", "X20Cr13 (1.4021)", "AISI 420", "20Cr13", "170 – 220 HB", "Мартенситная нержавейка: лопатки, клапаны, штоки"),
                new(IsoGroupCode.M, "#F9A825", "40Х13", "X39Cr13 (1.4031)", "AISI 420", "40Cr13", "190 – 240 HB", "Подшипники, режущий и измерительный инструмент"),

                            // Группа K — Чугуны
            new(IsoGroupCode.K, "#C62828", "СЧ20", "EN-GJL-200 (0.6020)", "A48 Class 30B", "HT200", "170 – 220 HB", "Серый чугун: станины, корпуса редукторов, шкивы"),
            new(IsoGroupCode.K, "#C62828", "СЧ25", "EN-GJL-250 (0.6025)", "A48 Class 35B", "HT250", "180 – 240 HB", "Гильзы цилиндров, тормозные диски, блоки"),
            new(IsoGroupCode.K, "#C62828", "ВЧ50", "EN-GJS-500-7 (0.7050)", "A536 70-50-05", "QT500-7", "170 – 230 HB", "Высокопрочный чугун с шаровидным графитом: коленвалы, ступицы"),
            // Группа N — Цветные сплавы
            new(IsoGroupCode.N, "#2E7D32", "Д16Т", "AlCu4Mg1 (3.1355)", "AA 2024-T4", "2A12", "105 – 120 HB", "Дюралюминий: силовые авиационные и приборные детали"),
            new(IsoGroupCode.N, "#2E7D32", "АМг6", "AlMg6 (3.3565)", "AA 5056", "5A06", "65 – 80 HB", "Свариваемый коррозионностойкий алюминиевый сплав"),
            new(IsoGroupCode.N, "#2E7D32", "В95", "AlZn5.5MgCu (3.4365)", "AA 7075", "7A09", "140 – 160 HB", "Высокопрочный алюминиевый сплав для пресс-форм и оснастки"),
            new(IsoGroupCode.N, "#2E7D32", "ЛС59-1", "CuZn39Pb1 (2.0372)", "C37700", "HPb59-1", "80 – 110 HB", "Сыпучая автоматная латунь: штуцеры, гайки, втулки"),
            new(IsoGroupCode.N, "#2E7D32", "БрАЖ9-4", "CuAl10Fe3Mn2 (2.0936)", "C95400", "QAl9-4", "110 – 150 HB", "Алюминиевая бронза: втулки скольжения, червячные колёса"),
            // Группа S — Титан и жаропрочные сплавы
            new(IsoGroupCode.S, "#EF6C00", "ВТ1-0", "Ti Grade 2 (3.7035)", "Ti Grade 2", "TA2", "140 – 180 HB", "Технический титан: химическая и медицинская арматура"),
            new(IsoGroupCode.S, "#EF6C00", "ВТ6", "Ti6Al4V (3.7165)", "Ti Grade 5", "TC4", "290 – 340 HB", "Самый популярный титановый сплав: ответственный крепёж, лопатки"),
            new(IsoGroupCode.S, "#EF6C00", "ХН77ТЮР", "NiCr20TiAl (2.4952)", "Nimonic 80A", "GH4033", "260 – 320 HB", "Жаропрочный никелевый сплав для высоких температур"),
            // Группа H — Закалённые стали
            new(IsoGroupCode.H, "#455A64", "ШХ15 (закал.)", "100Cr6 (1.3505)", "AISI 52100", "GCr15", "60 – 64 HRC", "Подшипниковая сталь после термообработки"),
            new(IsoGroupCode.H, "#455A64", "Х12МФ (закал.)", "X153CrMoV12 (1.2379)", "AISI D2", "Cr12Mo1V1", "58 – 62 HRC", "Штамповая сталь высокой износостойкости")
            };
        }
    }
}