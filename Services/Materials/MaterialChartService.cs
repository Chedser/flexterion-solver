using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media;
using MechCore.Models.Charts;

namespace MechCore.Services.Materials
{
    public enum MaterialChartMetric
    {
        CuttingSpeed,         // Vc(HB), м/мин
        SpecificCuttingForce, // kc1.1(HB), Н/мм²
        RecommendedFeed,      // f(HB), мм/об
        MachinabilityIndex    // Индекс обрабатываемости Km, %
    }

    public sealed class MaterialChartSummary
    {
        public EngineeringChartModel ChartModel { get; init; } = new();
        public string RecommendedToolGrade { get; init; } = string.Empty;
        public string OperatorAdvice { get; init; } = string.Empty;
        public string CalculatedValueBadge { get; init; } = string.Empty;
        public bool IsCriticalHardness { get; init; }
    }

    public class MaterialChartService : IMaterialChartService
    {
        private sealed record IsoCurveProfile(
            string Code,
            string Name,
            Color LineColor,
            double HbMin,
            double HbMax,
            double VcRef180,
            double VcExponent,
            double KcRef180,
            double KcExponent,
            double FeedRef180,
            double MachinabilityBase);

        private static readonly List<IsoCurveProfile> Profiles = new()
        {
            new("P", "ISO P — Углеродистые и легированные стали", Color.FromRgb(25, 118, 210), 110, 420, 240, 0.42, 1800, 0.35, 0.28, 100),
            new("M", "ISO M — Нержавеющие стали (аустенитные/дуплекс)", Color.FromRgb(245, 124, 0), 130, 380, 155, 0.38, 2150, 0.32, 0.22, 65),
            new("K", "ISO K — Чугуны (серый, ковкий, ВЧ)", Color.FromRgb(198, 40, 40), 140, 360, 265, 0.45, 1350, 0.38, 0.32, 115),
            new("N", "ISO N — Цветные сплавы (Al, Cu, бронза)", Color.FromRgb(46, 125, 50), 30, 180, 620, 0.30, 720, 0.28, 0.35, 260),
            new("S", "ISO S — Жаропрочные и титановые сплавы", Color.FromRgb(123, 31, 162), 200, 440, 62, 0.40, 2600, 0.30, 0.16, 28),
            new("H", "ISO H — Закалённые стали (45–65 HRC)", Color.FromRgb(55, 71, 79), 420, 680, 95, 0.55, 3200, 0.36, 0.12, 35)
        };

        public MaterialChartSummary BuildChart(
            MaterialChartMetric metric,
            string activeIsoCode,
            string materialTitle,
            double currentHb)
        {
            string normalizedIso = string.IsNullOrWhiteSpace(activeIsoCode)
                ? "P"
                : activeIsoCode.Trim().ToUpperInvariant()[..1];

            var activeProfile = Profiles.Find(p => p.Code == normalizedIso) ?? Profiles[0];

            double clampedHb = Math.Clamp(currentHb > 0 ? currentHb : 180, 30, 680);

            var model = new EngineeringChartModel
            {
                XAxis = new ChartAxisConfig
                {
                    Title = "Твёрдость материала",
                    Unit = "HB",
                    Min = 40,
                    Max = 680,
                    Step = 80,
                    NumberFormat = "F0"
                },                
                ActiveMaterialBadge = string.IsNullOrWhiteSpace(materialTitle)
                                        ? activeProfile.Name
                                        : $"{materialTitle} ({activeProfile.Name})",
            };

            ConfigureAxesAndZones(model, metric, normalizedIso);

            foreach (var profile in Profiles)
            {
                bool isActive = profile.Code == normalizedIso;
                var series = new ChartSeries
                {
                    Name = profile.Name,
                    StrokeBrush = new SolidColorBrush(profile.LineColor),
                    StrokeThickness = isActive ? 2.8 : 1.6,
                    IsHighlighted = isActive,
                    IsEngineerOnly = !isActive,
                    IsDashed = profile.Code == "H" || profile.Code == "S"
                };

                int steps = 28;
                double stepHb = (profile.HbMax - profile.HbMin) / steps;
                for (int i = 0; i <= steps; i++)
                {
                    double hb = profile.HbMin + i * stepHb;
                    double val = EvaluateMetric(profile, metric, hb);
                    series.Points.Add(new ChartPoint(hb, val));
                }

                model.Series.Add(series);
            }

            double activeY = EvaluateMetric(activeProfile, metric, clampedHb);
            string unit = GetMetricUnit(metric);
            string formattedY = metric == MaterialChartMetric.RecommendedFeed
                ? activeY.ToString("F2", CultureInfo.InvariantCulture)
                : activeY.ToString("F0", CultureInfo.InvariantCulture);

            model.Markers.Add(new WorkingPointMarker
            {
                Title = string.IsNullOrWhiteSpace(materialTitle) ? $"Группа ISO {normalizedIso}" : materialTitle,
                Subtitle = $"{clampedHb:F0} HB → {formattedY} {unit}",
                X = clampedHb,
                Y = activeY,
                FillBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                ShowGuideLines = true
            });

            var (toolGrade, advice, isCritical) = BuildEngineeringGuidance(normalizedIso, clampedHb);

            return new MaterialChartSummary
            {
                ChartModel = model,
                RecommendedToolGrade = toolGrade,
                OperatorAdvice = advice,
                CalculatedValueBadge = $"{formattedY} {unit}",
                IsCriticalHardness = isCritical
            };
        }

        private static void ConfigureAxesAndZones(
            EngineeringChartModel model,
            MaterialChartMetric metric,
            string activeIso)
        {
            model.Zones.Add(new ChartZone
            {
                Label = "Зона лёгкой обработки",
                Orientation = ChartZoneOrientation.Vertical,
                Start = 110,
                End = 240,
                FillBrush = new SolidColorBrush(Color.FromArgb(26, 76, 175, 80)),
                LabelBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50))
            });

            model.Zones.Add(new ChartZone
            {
                Label = "Зона высокой твёрдости (CBN / керамика)",
                Orientation = ChartZoneOrientation.Vertical,
                Start = 430,
                End = 680,
                FillBrush = new SolidColorBrush(Color.FromArgb(24, 198, 40, 40)),
                LabelBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40))
            });

            model.Thresholds.Add(new ChartThresholdLine
            {
                Label = "Предел твёрдого сплава (~430 HB / 45 HRC)",
                Orientation = ChartZoneOrientation.Vertical,
                Value = 430,
                StrokeBrush = new SolidColorBrush(Color.FromRgb(230, 81, 0))
            });

            switch (metric)
            {
                case MaterialChartMetric.CuttingSpeed:
                    model.ChartTitle = "Зависимость базовой скорости резания Vc от твёрдости (HB)";
                    model.Description = "Влияние твёрдости заготовки на допустимую скорость резания для твёрдосплавного инструмента с покрытием (стойкость T = 15 мин).";
                    model.YAxis = new ChartAxisConfig
                    {
                        Title = "Скорость резания Vc",
                        Unit = "м/мин",
                        Min = 0,
                        Max = activeIso == "N" ? 900 : 400,
                        Step = activeIso == "N" ? 150 : 50,
                        NumberFormat = "F0"
                    };
                    break;

                case MaterialChartMetric.SpecificCuttingForce:
                    model.ChartTitle = "Зависимость удельной силы резания kc1.1 от твёрдости (HB)";
                    model.Description = "Удельное сопротивление срезанию слоя сечением 1×1 мм — определяет нагрузку на шпиндель, мощность и отжим инструмента.";
                    model.YAxis = new ChartAxisConfig
                    {
                        Title = "Удельная сила kc1.1",
                        Unit = "Н/мм²",
                        Min = 400,
                        Max = 4400,
                        Step = 500,
                        NumberFormat = "F0"
                    };
                    break;

                case MaterialChartMetric.RecommendedFeed:
                    model.ChartTitle = "Зависимость рекомендуемой подачи f от твёрдости (HB)";
                    model.Description = "Базовая подача на оборот при получастовом точении (радиус при вершине rε = 0.8 мм).";
                    model.YAxis = new ChartAxisConfig
                    {
                        Title = "Подача f",
                        Unit = "мм/об",
                        Min = 0.05,
                        Max = 0.45,
                        Step = 0.05,
                        NumberFormat = "F2"
                    };
                    break;

                case MaterialChartMetric.MachinabilityIndex:
                    model.ChartTitle = "Индекс обрабатываемости Km относительно стали 45 (180 HB = 100%)";
                    model.Description = "Относительная производительность съёма металла при равной стойкости режущей кромки.";
                    model.YAxis = new ChartAxisConfig
                    {
                        Title = "Индекс Km",
                        Unit = "%",
                        Min = 0,
                        Max = activeIso == "N" ? 350 : 180,
                        Step = activeIso == "N" ? 50 : 30,
                        NumberFormat = "F0"
                    };
                    model.Thresholds.Add(new ChartThresholdLine
                    {
                        Label = "Эталон: Сталь 45 (100%)",
                        Orientation = ChartZoneOrientation.Horizontal,
                        Value = 100,
                        StrokeBrush = new SolidColorBrush(Color.FromRgb(25, 118, 210))
                    });
                    break;
            }
        }

        private static double EvaluateMetric(IsoCurveProfile p, MaterialChartMetric metric, double hb)
        {
            double ratio = Math.Max(hb, 25.0) / 180.0;
            return metric switch
            {
                MaterialChartMetric.CuttingSpeed =>
                p.VcRef180 * Math.Pow(ratio, -p.VcExponent),
                MaterialChartMetric.SpecificCuttingForce =>
                p.KcRef180 * Math.Pow(ratio, p.KcExponent),
                MaterialChartMetric.RecommendedFeed =>
                Math.Clamp(p.FeedRef180 * Math.Pow(ratio, -0.32), 0.06, 0.42),
                MaterialChartMetric.MachinabilityIndex =>
                p.MachinabilityBase * Math.Pow(ratio, -p.VcExponent),
                _ => 0
            };
        }
        private static string GetMetricUnit(MaterialChartMetric metric) => metric switch
        {
            MaterialChartMetric.CuttingSpeed => "м/мин",
            MaterialChartMetric.SpecificCuttingForce => "Н/мм²",
            MaterialChartMetric.RecommendedFeed => "мм/об",
            MaterialChartMetric.MachinabilityIndex => "%",
            _ => string.Empty
        };
        private static (string ToolGrade, string Advice, bool IsCritical) BuildEngineeringGuidance(string iso, double hb)
        {
            if (hb >= 430 || iso == "H")
            {
                return (
                "Сплав: CBN (кубический нитрид бора) или смешанная керамика (CC650 / CB7015)",
                "Высокая твёрдость (> 45 HRC): работать без СОЖ (сухое резание) во избежание термоудара пластины; глубина ap ≤ 0.3 мм.",
                true
                );
            }
            return iso switch
            {
                "P" when hb < 150 => (
                "Сплав: P15–P25 с острым стружколомом и PVD/CVD покрытием TiAlN",
                "Низкая твёрдость (< 150 HB): высокий риск наростообразования и сливной стружки. Не занижайте Vc ниже 160 м/мин.",
                false
                ),
                "P" => (
                "Сплав: P20–P30 (CVD TiCN + Al₂O₃, аналог GC4325)",
                "Оптимальная зона обрабатываемости конструкционных сталей. Обильная подача эмульсии СОЖ.",
                false
                ),
                "M" => (
                "Сплав: M15–M25 с острой кромкой и PVD-покрытием (аналог GC1125 / GC2025)",
                "Нержавеющая сталь склонна к наклёпу: не допускайте трения без подачи (dwell) и используйте подачу выше 0.15 мм/об.",
                false
                ),
                "K" => (
                "Сплав: K15–K20 с толстым покрытием Al₂O₃ (аналог GC3210)",
                "Абразивная стружка надлома: допускается обработка всухую или с подачей воздуха для защиты от термотрещин.",
                false
                ),
                "N" => (
                "Сплав: H10 полированный твёрдый сплав без покрытия или PCD (алмаз)",
                "Алюминиевые и медные сплавы: максимальные обороты шпинделя, обильная СОЖ для предотвращения налипания на кромку.",
                false
                ),
                "S" => (
                "Сплав: S15–S25 мелкозернистый твёрдый сплав с PVD TiAlN (аналог GC1105)",
                "Низкая теплопроводность титана/жаропрочки: тепло уходит в пластину. Строго ограничивайте Vc и подавайте СОЖ под давлением.",
                true
                ),
                _ => ("Сплав: универсальный P25 / M25", "Поддерживайте стабильный отвод стружки.", false)
            };
        }
    }
}