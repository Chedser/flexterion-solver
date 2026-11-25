using System;

namespace MechCore.Models.Threads
{
    public class ThreadEngineerResult
    {
        // Обозначение сопряжения (например, M16×2 - 6H/6g)
        public string FitDesignation { get; }
        public string ProfileAngleText { get; }

        // 1. Предельные размеры БОЛТА (наружная резьба)
        public string BoltMajorRange { get; }   // d (наружный)
        public string BoltPitchRange { get; }   // d2 (средний)
        public string BoltMinorRange { get; }   // d1 (внутренний)
        public string BoltAllowanceText { get; }

        // 2. Предельные размеры ГАЙКИ (внутренняя резьба)
        public string NutMajorRange { get; }    // D (наружный)
        public string NutPitchRange { get; }    // D2 (средний)
        public string NutMinorRange { get; }    // D1 (внутренний)
        public string NutAllowanceText { get; }

        // 3. Характеристики свинчиваемости и зазоры по среднему диаметру
        public double MinPitchClearance { get; } // Smin по d2/D2
        public double MaxPitchClearance { get; } // Smax по d2/D2
        public string EngagementLengthRange { get; } // Нормальная длина свинчивания N (ГОСТ 16093)

        // 4. Конструктивные элементы по ГОСТ 10549-80 (фаска, проточка, сбег)
        public string EntryChamferText { get; }     // Заходная фаска c × 45°
        public string GrooveWidthText { get; }      // Ширина нормальной проточки f
        public string GrooveDiameterText { get; }   // Диаметр проточки dk (вал) и Dk (отверстие)
        public string GrooveWallAngleText { get; }  // Угол стенки выхода (45° или 30°)
        public string RunoutAndUndercutText { get; }// Сбег x и недорез a без канавки

        // 5. Данные для графической схемы профиля резьбы
        public string ProfileGeometryPoints { get; } // Координаты ломаной профиля зуба
        public string PitchDimensionLabel { get; }   // Подпись шага P (мм)
        public string HeightDimensionLabel { get; }  // Подпись рабочей высоты H1 (мм)
        public string AngleBadgeText { get; }        // Угол при вершине (60°, 55°, 30°)
        public string MajorDiameterLabel { get; }    // Наружный диаметр d / D
        public string PitchDiameterLabel { get; }    // Средний диаметр d2 / D2
        public string MinorDiameterLabel { get; }    // Внутренний диаметр d1 / D1

        public ThreadEngineerResult(ThreadSpecItem spec, string boltClass, string nutClass)
        {
            double d = spec.NominalDiameter;
            double p = spec.Pitch;

            FitDesignation = $"{spec.SizeDesignation} × {p:0.###} — {nutClass} / {boltClass}";

            ProfileAngleText = spec.Type switch
            {
                ThreadType.PipeG or ThreadType.TaperedR => "55° (профиль Уитворта)",
                ThreadType.Trapezoidal => "30° (равнобочная трапеция)",
                ThreadType.Buttress => "30° / 3° (несимметричная упорная)",
                _ => "60° (треугольный профиль ISO / ГОСТ)"
            };

            // Базовые средние и внутренние диаметры
            double d2Base = spec.Type switch
            {
                ThreadType.PipeG or ThreadType.TaperedR => d - 0.640327 * p,
                ThreadType.TaperedNpt => d - 0.8 * p,
                ThreadType.Trapezoidal => d - 0.5 * p,
                ThreadType.Buttress => d - 0.75 * p,
                _ => d - 0.649519 * p
            };
            double d1Base = spec.MinorDiameter;

            // --- РАСЧЁТ ОТКЛОНЕНИЙ ПО ГОСТ 16093 / ISO 965 ---
            // Основное отклонение болта (es в мкм -> мм)
            double esBolt = boltClass switch
            {
                "6e" => Math.Round((50.0 + 11.0 * p) / 1000.0, 4),
                "6g" or "8g" => Math.Round((15.0 + 11.0 * p) / 1000.0, 4),
                _ => 0.0 // 4h, 6h (es = 0)
            };

            // Допуск среднего диаметра болта Td2
            double baseTd2 = (90.0 * Math.Pow(p, 0.4) * Math.Pow(d, 0.1)) / 1000.0;
            double td2Bolt = boltClass[0] switch
            {
                '4' => Math.Round(baseTd2 * 0.63, 4),
                '8' => Math.Round(baseTd2 * 1.60, 4),
                _ => Math.Round(baseTd2, 4) // 6-я степень точности
            };

            // Допуск наружного диаметра болта Td
            double baseTd = (180.0 * Math.Pow(p, 2.0 / 3.0) - 3.15 / Math.Sqrt(p)) / 1000.0;
            if (baseTd < 0.05) baseTd = 0.05;
            double tdBolt = boltClass[0] switch
            {
                '4' => Math.Round(baseTd * 0.63, 4),
                '8' => Math.Round(baseTd * 1.60, 4),
                _ => Math.Round(baseTd, 4)
            };

            // Итоговые размеры болта
            double boltDMax = Math.Round(d - esBolt, 4);
            double boltDMin = Math.Round(boltDMax - tdBolt, 4);
            double boltD2Max = Math.Round(d2Base - esBolt, 4);
            double boltD2Min = Math.Round(boltD2Max - td2Bolt, 4);
            double boltD1Max = Math.Round(d1Base - esBolt, 4);
            double boltD1Min = Math.Round(boltD1Max - td2Bolt * 1.15, 4);

            BoltMajorRange = $"{boltDMin:0.000} … {boltDMax:0.000} мм";
            BoltPitchRange = $"{boltD2Min:0.000} … {boltD2Max:0.000} мм";
            BoltMinorRange = $"{boltD1Min:0.000} … {boltD1Max:0.000} мм";
            BoltAllowanceText = esBolt > 0
                ? $"Основное отклонение es = −{esBolt:0.0000} мм (с гарантированным зазором)"
                : "Основное отклонение es = 0.0000 мм (без зазора на номинале)";

            // Основное отклонение гайки (EI в мкм -> мм)
            double eiNut = nutClass == "6G"
                ? Math.Round((15.0 + 11.0 * p) / 1000.0, 4)
                : 0.0; // 5H, 6H, 7H (EI = 0)

            // Допуск среднего диаметра гайки TD2 (в 1.32 раза больше Td2 болта)
            double baseTD2 = baseTd2 * 1.32;
            double td2Nut = nutClass[0] switch
            {
                '5' => Math.Round(baseTD2 * 0.80, 4),
                '7' => Math.Round(baseTD2 * 1.25, 4),
                _ => Math.Round(baseTD2, 4)
            };

            // Допуск внутреннего диаметра гайки TD1
            double baseTD1 = (0.2 * p + 0.05);
            double td1Nut = nutClass[0] switch
            {
                '5' => Math.Round(baseTD1 * 0.80, 4),
                '7' => Math.Round(baseTD1 * 1.25, 4),
                _ => Math.Round(baseTD1, 4)
            };

            // Итоговые размеры гайки
            double nutDMin = Math.Round(d + eiNut, 4);
            double nutD2Min = Math.Round(d2Base + eiNut, 4);
            double nutD2Max = Math.Round(nutD2Min + td2Nut, 4);
            double nutD1Min = Math.Round(d1Base + eiNut, 4);
            double nutD1Max = Math.Round(nutD1Min + td1Nut, 4);

            NutMajorRange = $"не менее {nutDMin:0.000} мм";
            NutPitchRange = $"{nutD2Min:0.000} … {nutD2Max:0.000} мм";
            NutMinorRange = $"{nutD1Min:0.000} … {nutD1Max:0.000} мм";
            NutAllowanceText = eiNut > 0
                ? $"Основное отклонение EI = +{eiNut:0.0000} мм (увеличенный зазор)"
                : "Основное отклонение EI = 0.0000 мм (основное отверстие H)";

            // Зазоры в сопряжении по среднему диаметру
            MinPitchClearance = Math.Round(nutD2Min - boltD2Max, 4);
            MaxPitchClearance = Math.Round(nutD2Max - boltD2Min, 4);

            // Нормальная длина свинчивания N по ГОСТ 16093: от 2.24 * P * d^0.2 до 6.7 * P * d^0.2
            double lMin = Math.Round(2.24 * p * Math.Pow(d, 0.2), 1);
            double lMax = Math.Round(6.7 * p * Math.Pow(d, 0.2), 1);
            EngagementLengthRange = $"св. {lMin:0.0} до {lMax:0.0} мм (группа N)";

            // --- КОНСТРУКТИВНЫЕ ЭЛЕМЕНТЫ ПО ГОСТ 10549-80 ---
            double chamferC = Math.Round(Math.Max(0.8 * p, 0.5), 1);
            EntryChamferText = $"{chamferC:0.0} × 45°";

            double grooveF = Math.Round(2.5 * p, 1);
            GrooveWidthText = $"{grooveF:0.0} мм (нормальная)";

            double dkBolt = Math.Round(d1Base - 0.3 * p, 2);
            double dkNut = Math.Round(d + 0.3 * p, 2);
            GrooveDiameterText = $"Вал dk = Ø {dkBolt:0.00} мм  |  Отверстие Dk = Ø {dkNut:0.00} мм";

            GrooveWallAngleText = (spec.Type == ThreadType.Trapezoidal || spec.Type == ThreadType.Buttress)
                ? "30° (по ГОСТ 10549-80 для Tr и S)"
                : "45° (по ГОСТ 10549-80 для M, G, R)";

            double runoutX = Math.Round(1.25 * p, 1);
            double undercutA = Math.Round(2.5 * p, 1);
            RunoutAndUndercutText = $"Сбег x = {runoutX:0.0} мм  |  Недорез a = {undercutA:0.0} мм";

            double h1Val = spec.Type switch
            {
                ThreadType.PipeG or ThreadType.TaperedR => 0.640327 * p,
                ThreadType.TaperedNpt => 0.8 * p,
                ThreadType.Trapezoidal => 0.5 * p + (p <= 5.0 ? 0.25 : 0.5),
                ThreadType.Buttress => 0.75 * p,
                _ => 0.541266 * p
            };

            PitchDimensionLabel = $"P = {p:0.###} мм";
            HeightDimensionLabel = $"H₁ = {h1Val:0.###} мм";
            MajorDiameterLabel = $"d (D) = {d:0.###} мм";
            PitchDiameterLabel = $"d₂ (D₂) = {d2Base:0.###} мм";
            MinorDiameterLabel = $"d₁ (D₁) = {d1Base:0.###} мм";

            AngleBadgeText = spec.Type switch
            {
                ThreadType.PipeG or ThreadType.TaperedR => "55°",
                ThreadType.Trapezoidal => "30°",
                ThreadType.Buttress => "30° / 3°",
                _ => "60°"
            };

            // Векторные координаты профиля (два полных шага по горизонтали X: 40..360, высота Y: 45..145)
            ProfileGeometryPoints = spec.Type switch
            {
                // Трапецеидальная резьба (плоские вершины и впадины)
                ThreadType.Trapezoidal =>
                    "20,145 55,145 85,45 135,45 165,145 215,145 245,45 295,45 325,145 380,145",

                // Упорная резьба (крутая рабочая грань 3° и пологая 30°)
                ThreadType.Buttress =>
                    "20,145 50,145 60,45 100,45 175,145 210,145 220,45 260,45 335,145 380,145",

                // Треугольный профиль со скруглённой/срезанной вершиной (M, G, R, NPT)
                _ =>
                    "20,145 45,145 105,45 125,45 185,145 215,145 275,45 295,45 355,145 380,145"
            };

        }
    }
}
