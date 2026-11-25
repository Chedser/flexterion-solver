using System;
using MechCore.Models.Materials;
using MechCore.Models.Turning;

namespace MechCore.Models.Threads
{
    public class ThreadOperatorResult
    {
        public bool IsInternal { get; }
        public bool IsRollForming { get; }
        public string PrepTitle { get; }           // Заголовок карточки подготовки (сверло / раскатник / проточка)
        public string PrepDiameterText { get; }    // Например: "Ø 14.0 мм" или "Ø 15.10 мм"
        public string PrepNote { get; }            // Технологическое пояснение для наладчика

        public bool HasWarning { get; }
        public string WarningMessage { get; } = string.Empty;

        public double Pitch { get; }               // Шаг P (мм)
        public double ThreadHeightH1 { get; }      // Рабочая высота профиля H1 на сторону (мм)
        public double PitchDiameter { get; }       // Средний диаметр d2 / D2 (мм)
        public int RecommendedPasses { get; }      // Рекомендуемое число проходов для G76
        public double FirstPassDepth { get; }      // Глубина первого прохода (мм)

        public ThreadOperatorResult(
            ThreadSpecItem spec,
            bool isInternal,
            bool isRollForming = false,
            bool considerMaterial = false,
            IsoMaterialGroupItem? isoGroup = null,
            MaterialGradeItem? grade = null)
        {
            IsInternal = isInternal;
            IsRollForming = isInternal && isRollForming;
            Pitch = spec.Pitch;

            // 1. Расчёт рабочей высоты профиля H1 (на сторону) и среднего диаметра d2/D2
            double h1;
            double d2;

            switch (spec.Type)
            {
                case ThreadType.Metric:
                    h1 = 0.541266 * spec.Pitch;
                    d2 = spec.NominalDiameter - 0.649519 * spec.Pitch;
                    break;

                case ThreadType.PipeG:
                case ThreadType.TaperedR:
                    h1 = 0.640327 * spec.Pitch;
                    d2 = spec.NominalDiameter - 0.640327 * spec.Pitch;
                    break;

                case ThreadType.TaperedNpt:
                    h1 = 0.8 * spec.Pitch;
                    d2 = spec.NominalDiameter - 0.8 * spec.Pitch;
                    break;

                case ThreadType.Trapezoidal:
                    double ac = spec.Pitch <= 5.0 ? 0.25 : 0.5;
                    h1 = 0.5 * spec.Pitch + ac;
                    d2 = spec.NominalDiameter - 0.5 * spec.Pitch;
                    break;

                case ThreadType.Buttress:
                    h1 = 0.75 * spec.Pitch;
                    d2 = spec.NominalDiameter - 0.75 * spec.Pitch;
                    break;

                default:
                    h1 = 0.541266 * spec.Pitch;
                    d2 = spec.NominalDiameter - 0.649519 * spec.Pitch;
                    break;
            }

            ThreadHeightH1 = Math.Round(h1, 3);
            PitchDiameter = Math.Round(d2, 3);

            bool activeMat = considerMaterial && isoGroup != null;
            string matLabel = activeMat
                ? (grade != null ? $"{grade.GostGrade} (ISO {isoGroup!.CodeLetter})" : $"группы ISO {isoGroup!.CodeLetter}")
                : string.Empty;

            // 2. Расчёт диаметра подготовки (сверло под метчик, раскатник или проточка вала)
            if (isInternal)
            {
                if (IsRollForming)
                {
                    PrepTitle = "Диаметр отверстия под раскатник (H8–H9)";
                    // При бесстружечном накатывании металл выдавливается во впадины: D_раск ≈ D - 0.45 * P
                    double rollDrill = spec.NominalDiameter - 0.45 * spec.Pitch;

                    // Корректировка по пластичности материала
                    if (activeMat)
                    {
                        if (isoGroup!.Code == IsoGroupCode.M || isoGroup.Code == IsoGroupCode.S)
                            rollDrill += 0.04 * spec.Pitch; // Для вязких сплавов чуть больше, чтобы снизить момент
                        else if (isoGroup.Code == IsoGroupCode.N || grade?.GostGrade is "Ст3сп" or "Сталь 20")
                            rollDrill -= 0.02 * spec.Pitch; // Мягкие металлы легко заполняют профиль
                    }

                    PrepDiameterText = $"Ø {rollDrill:0.00} мм";
                    PrepNote = activeMat
                        ? $"Расчёт для {matLabel}: при накатывании профиль формируется пластической деформацией без стружки (допуск отверстия H8–H9, обильная смазка)."
                        : $"Базовый диаметр D = {spec.NominalDiameter:0.###} мм. При накатывании раскатником отверстие сверлится/развёртывается по квалитету H8–H9.";

                    // Проверка совместимости раскатника с материалом и профилем
                    if (spec.Type == ThreadType.Trapezoidal || spec.Type == ThreadType.Buttress)
                    {
                        HasWarning = true;
                        WarningMessage = "Внимание: трапецеидальные (Tr) и упорные (S) резьбы не изготавливаются бесстружечным раскатником — используйте резец или метчик.";
                    }
                    else if (activeMat && (isoGroup!.Code == IsoGroupCode.K || isoGroup.Code == IsoGroupCode.H))
                    {
                        HasWarning = true;
                        WarningMessage = $"Недопустимый режим: {matLabel} не обладает пластичностью — накатывание раскатником приведёт к выкрашиванию металла и поломке инструмента. Используйте метчик или резец.";
                    }
                    else if (activeMat && isoGroup!.Code == IsoGroupCode.S)
                    {
                        HasWarning = true;
                        WarningMessage = $"Внимание: для {matLabel} накатывание раскатником требует высокой жёсткости и специальной противозадирной СОЖ из-за высокого крутящего момента.";
                    }
                }
                else
                {
                    PrepTitle = "Рекомендуемый диаметр сверла (допуск H11)";
                    double baseDrill = spec.Type == ThreadType.Metric
                        ? spec.NominalDiameter - spec.Pitch
                        : spec.MinorDiameter + 0.05 * spec.Pitch;

                    double viscosityShift = 0.0;
                    string matComment = "Для вязких сталей и нержавейки допускается увеличить сверло на +0.1…0.2 мм.";

                    if (activeMat)
                    {
                        switch (isoGroup!.Code)
                        {
                            case IsoGroupCode.M: // Нержавейка — вязкая, склонна к налипанию и закусыванию
                            case IsoGroupCode.S: // Титан и жаропрочные сплавы
                                viscosityShift = Math.Clamp(0.08 * spec.Pitch, 0.05, 0.25);
                                matComment = $"Учтена высокая вязкость {matLabel}: диаметр сверла увеличен на +{viscosityShift:0.##} мм (в пределах допуска D₁) для снижения крутящего момента на метчике.";
                                break;

                            case IsoGroupCode.H: // Закалённые стали
                                viscosityShift = Math.Clamp(0.10 * spec.Pitch, 0.05, 0.30);
                                matComment = $"Для твёрдого материала ({matLabel}) сверло увеличено на +{viscosityShift:0.##} мм для предотвращения скола зубьев метчика.";
                                break;

                            case IsoGroupCode.P: // Конструкционные стали
                                if (grade?.GostGrade is "40Х" or "30ХГСА" or "40ХН2МА")
                                {
                                    viscosityShift = Math.Clamp(0.05 * spec.Pitch, 0.05, 0.15);
                                    matComment = $"Для легированной стали ({matLabel}) добавлена поправка +{viscosityShift:0.##} мм для облегчения работы метчика.";
                                }
                                else
                                {
                                    matComment = $"Стандартный расчёт для {matLabel} (процент перекрытия профиля витка ~75%, сверление по H11).";
                                }
                                break;

                            case IsoGroupCode.K: // Чугуны — сыпучая стружка
                                matComment = $"Для чугуна ({matLabel}) стружка сыпучая, заклинивания нет — берётся базовый диаметр сверла по H11.";
                                break;

                            case IsoGroupCode.N: // Цветные сплавы
                                matComment = $"Для цветного сплава ({matLabel}) берётся стандартный диаметр сверла по H11 (рекомендуется СОЖ/эмульсия от налипания).";
                                break;
                        }
                    }

                    double drill = Math.Round(baseDrill + viscosityShift, 1);
                    PrepDiameterText = $"Ø {drill:0.0} мм";
                    PrepNote = $"Базовый внутренний диаметр D₁ = {spec.MinorDiameter:0.###} мм. {matComment}";
                }
            }
            else
            {
                PrepTitle = "Диаметр проточки заготовки (вал, допуск h11)";
                double allowance = Math.Clamp(0.05 * spec.Pitch, 0.04, 0.18);

                if (activeMat && (isoGroup!.Code == IsoGroupCode.M || isoGroup.Code == IsoGroupCode.N || grade?.GostGrade is "Ст3сп" or "Сталь 20"))
                {
                    allowance *= 1.25; // Вязкие и пластичные металлы сильнее выдавливаются на вершину витка
                }

                double turnMin = Math.Round(spec.NominalDiameter - allowance * 1.5, 2);
                double turnMax = Math.Round(spec.NominalDiameter - allowance * 0.5, 2);

                PrepDiameterText = $"Ø {turnMin:0.00} – {turnMax:0.00} мм";
                PrepNote = activeMat
                                    ? $"Номинал d = {spec.NominalDiameter:0.###} мм (квалитет h11). Занижение рассчитано для {matLabel}, чтобы исключить наволакивание металла на вершину пластины."
                                    : $"Номинал d = {spec.NominalDiameter:0.###} мм (квалитет h11). Небольшое занижение предотвращает наволакивание металла на вершину резьбовой пластины.";
            }
            // 3. Рекомендуемое количество проходов для цикла ЧПУ G76 (по шагу P и материалу)
            int basePasses = spec.Pitch switch
            {
                <= 0.5 => 4,
                <= 0.8 => 5,
                <= 1.0 => 6,
                <= 1.25 => 7,
                <= 1.5 => 8,
                <= 1.75 => 9,
                <= 2.0 => 10,
                <= 2.5 => 12,
                <= 3.0 => 14,
                <= 4.0 => 16,
                <= 5.0 => 18,
                _ => 22
            };
            double passFactor = 1.0;
            if (activeMat)
            {
                passFactor = isoGroup!.Code switch
                {
                    IsoGroupCode.N => 0.75, // Алюминий / латунь — меньше проходов
                    IsoGroupCode.K => 0.90, // Чугун
                    IsoGroupCode.P => 1.00, // Стали (эталон)
                    IsoGroupCode.M => 1.25, // Нержавейка — больше проходов против наклёпа
                    IsoGroupCode.H => 1.35, // Закалённые стали — щадящий съём
                    IsoGroupCode.S => 1.45, // Титан / жаропрочные сплавы
                    _ => 1.00
                };
            }
            RecommendedPasses = Math.Max(3, (int)Math.Round(basePasses * passFactor));
            FirstPassDepth = Math.Round(ThreadHeightH1 / Math.Sqrt(RecommendedPasses), 3);
        }
    }
}