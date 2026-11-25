using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media;
using MechCore.Models.Charts;
using MechCore.Models.Materials;
using MechCore.Models.Turning;

namespace MechCore.Services.Turning
{
    public class TurningChartService : ITurningChartService
    {
        public TurningChartSummary BuildChart(TurningChartMetric metric, TurningChartContext context)
        {
            return metric switch
            {
                TurningChartMetric.ToolLifeTaylor => BuildTaylorToolLifeChart(context),
                TurningChartMetric.SurfaceRoughnessRa => BuildRoughnessChart(context),
                TurningChartMetric.CuttingPowerAp => BuildCuttingPowerChart(context),
                TurningChartMetric.SpindleSpeedProfile => BuildSpindleProfileChart(context),
                _ => BuildTaylorToolLifeChart(context)
            };
        }

        // =====================================================================
        // 1. КРИВАЯ СТОЙКОСТИ ИНСТРУМЕНТА ТЕЙЛОРА: T(Vc)
        // =====================================================================
        private static TurningChartSummary BuildTaylorToolLifeChart(TurningChartContext ctx)
        {
            var inv = CultureInfo.InvariantCulture;

            // Показатель степени m и эталонная скорость V15 (стойкость 15 мин)
            double m = ctx.ToolMaterial switch
            {
                ToolMaterialKind.Hss => 0.14,
                ToolMaterialKind.UncoatedCarbide => 0.22,
                ToolMaterialKind.CoatedCarbide => 0.28,
                ToolMaterialKind.Cermet => 0.32,
                ToolMaterialKind.CbnOrCeramic => 0.36,
                _ => 0.26
            };

            double baseVc15 = ctx.IsoGroup switch
            {
                IsoGroupCode.P => 220,
                IsoGroupCode.M => 145,
                IsoGroupCode.K => 245,
                IsoGroupCode.N => 580,
                IsoGroupCode.S => 55,
                IsoGroupCode.H => 115,
                _ => 200
            };

            double toolFactor = ctx.ToolMaterial switch
            {
                ToolMaterialKind.Hss => 0.28,
                ToolMaterialKind.UncoatedCarbide => 0.78,
                ToolMaterialKind.CoatedCarbide => 1.00,
                ToolMaterialKind.Cermet => 1.18,
                ToolMaterialKind.CbnOrCeramic => 1.45,
                _ => 1.0
            };

            double vc15 = Math.Max(15.0, baseVc15 * toolFactor);
            double maxVc = Math.Ceiling(vc15 * 1.65 / 50.0) * 50.0;
            double minVc = Math.Max(10.0, Math.Floor(vc15 * 0.35 / 10.0) * 10.0);

            double CalcLifeMinutes(double vc)
            {
                double safeVc = Math.Max(vc, 5.0);
                double t = 15.0 * Math.Pow(vc15 / safeVc, 1.0 / m);
                return Math.Clamp(t, 1.0, 90.0);
            }

            var model = new EngineeringChartModel
            {
                ChartTitle = "Кривая стойкости режущей кромки Тейлора T(Vc)",
                Description = $"Зависимость времени непрерывного резания до критического износа от скорости Vc ({ctx.MaterialLabel}).",
                XAxis = new ChartAxisConfig
                {
                    Title = "Скорость резания Vc",
                    Unit = "м/мин",
                    Min = minVc,
                    Max = maxVc,
                    Step = Math.Max(10, Math.Round((maxVc - minVc) / 6.0 / 10.0) * 10.0),
                    NumberFormat = "F0"
                },
                YAxis = new ChartAxisConfig
                {
                    Title = "Стойкость кромки T",
                    Unit = "мин",
                    Min = 0,
                    Max = 60,
                    Step = 10,
                    NumberFormat = "F0"
                },
                ActiveMaterialBadge = ctx.MaterialLabel
            };

            model.Zones.Add(new ChartZone
            {
                Label = "Экономичный ресурс (15–45 мин)",
                Orientation = ChartZoneOrientation.Horizontal,
                Start = 15,
                End = 45,
                FillBrush = new SolidColorBrush(Color.FromArgb(26, 76, 175, 80)),
                LabelBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50))
            });

            model.Zones.Add(new ChartZone
            {
                Label = "Ускоренный износ (< 8 мин)",
                Orientation = ChartZoneOrientation.Horizontal,
                Start = 0,
                End = 8,
                FillBrush = new SolidColorBrush(Color.FromArgb(24, 198, 40, 40)),
                LabelBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40))
            });

            model.Thresholds.Add(new ChartThresholdLine
            {
                Label = "Каталожный стандарт (T = 15 мин)",
                Orientation = ChartZoneOrientation.Horizontal,
                Value = 15,
                StrokeBrush = new SolidColorBrush(Color.FromRgb(25, 118, 210))
            });

            var curve = new ChartSeries
            {
                Name = $"Кривая стойкости ({ctx.MaterialLabel})",
                StrokeBrush = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
                StrokeThickness = 2.6,
                IsHighlighted = true
            };

            const int steps = 40;
            for (int i = 0; i <= steps; i++)
            {
                double vc = minVc + (maxVc - minVc) * i / steps;
                curve.Points.Add(new ChartPoint(vc, CalcLifeMinutes(vc)));
            }
            model.Series.Add(curve);

            double recVc = Math.Clamp(ctx.RecommendedVc, minVc, maxVc);
            double recLife = CalcLifeMinutes(recVc);

            model.Markers.Add(new WorkingPointMarker
            {
                Title = "Справочник",
                Subtitle = string.Format(inv, "Vc={0:0} → T≈{1:0} мин", recVc, recLife),
                X = recVc,
                Y = recLife,
                FillBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50)),
                ShowGuideLines = true
            });

            if (Math.Abs(ctx.SimVc - ctx.RecommendedVc) > 3.0)
            {
                double simVcClamped = Math.Clamp(ctx.SimVc, minVc, maxVc);
                double simLife = CalcLifeMinutes(simVcClamped);
                model.Markers.Add(new WorkingPointMarker
                {
                    Title = "Калькулятор",
                    Subtitle = string.Format(inv, "Vc={0:0} → T≈{1:0} мин", simVcClamped, simLife),
                    X = simVcClamped,
                    Y = simLife,
                    FillBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                    ShowGuideLines = false
                });
            }

            double activeLife = CalcLifeMinutes(ctx.SimVc > 0 ? ctx.SimVc : ctx.RecommendedVc);
            bool warn = activeLife < 8.0;

            return new TurningChartSummary
            {
                ChartModel = model,
                ValueBadge = string.Format(inv, "T ≈ {0:0} мин", activeLife),
                Headline = warn
                    ? "Внимание: высокая скорость Vc резко сокращает ресурс режущей кромки!"
                    : "Стойкость инструмента находится в рабочем промышленном диапазоне.",
                Advice = warn
                    ? "Снизьте скорость резания на 15–20% или перейдите на пластину с более износостойким покрытием (CVD Al₂O₃ / CBN)."
                    : "Для повышения производительности без потери стойкости сначала увеличивайте подачу fn и глубину ap, и только в последнюю очередь — Vc.",
                IsWarning = warn
            };
        }

        // =====================================================================
        // 2. ЗАВИСИМОСТЬ ШЕРОХОВАТОСТИ Ra ОТ ПОДАЧИ fn ДЛЯ РАЗНЫХ РАДИУСОВ rε
        // =====================================================================
        private static TurningChartSummary BuildRoughnessChart(TurningChartContext ctx)
        {
            var inv = CultureInfo.InvariantCulture;
            double activeR = Math.Clamp(ctx.NoseRadius > 0 ? ctx.NoseRadius : 0.4, 0.1, 2.0);
            double activeFeed = Math.Clamp(ctx.CurrentRoughnessFeed > 0 ? ctx.CurrentRoughnessFeed : 0.12, 0.02, 0.50);

            double CalcRa(double fn, double r) => ((fn * fn) / (8.0 * r)) * 1000.0 / 4.0;

            var model = new EngineeringChartModel
            {
                ChartTitle = "Зависимость теоретической шероховатости Ra от подачи fn",
                Description = "Сравнение парабол чистоты поверхности для стандартных радиусов при вершине пластины rε (ISO 1832).",
                XAxis = new ChartAxisConfig
                {
                    Title = "Оборотная подача fn",
                    Unit = "мм/об",
                    Min = 0.02,
                    Max = 0.42,
                    Step = 0.05,
                    NumberFormat = "F2"
                },
                YAxis = new ChartAxisConfig
                {
                    Title = "Шероховатость Ra",
                    Unit = "мкм",
                    Min = 0,
                    Max = 10.0,
                    Step = 2.0,
                    NumberFormat = "F1"
                },
                ActiveMaterialBadge = ctx.MaterialLabel
            };

            model.Zones.Add(new ChartZone
            {
                Label = "Чистовое точение (Ra ≤ 1.6 мкм)",
                Orientation = ChartZoneOrientation.Horizontal,
                Start = 0,
                End = 1.6,
                FillBrush = new SolidColorBrush(Color.FromArgb(26, 76, 175, 80)),
                LabelBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50))
            });

            if (ctx.TargetRa > 0 && ctx.TargetRa <= 10.0)
            {
                model.Thresholds.Add(new ChartThresholdLine
                {
                    Label = string.Format(inv, "Допуск чертежа: Ra {0:0.##} мкм", ctx.TargetRa),
                    Orientation = ChartZoneOrientation.Horizontal,
                    Value = ctx.TargetRa,
                    StrokeBrush = new SolidColorBrush(Color.FromRgb(230, 81, 0))
                });
            }
            var radii = new (double R, string Title, Color Clr)[]
            {
                        (0.2, "Пластина rε = 0.2 мм (тонкая чистовая)", Color.FromRgb(123, 31, 162)),
                        (0.4, "Пластина rε = 0.4 мм (чистовая)", Color.FromRgb(25, 118, 210)),
                        (0.8, "Пластина rε = 0.8 мм (получистовая)", Color.FromRgb(46, 125, 50)),
                        (1.2, "Пластина rε = 1.2 мм (черновая)", Color.FromRgb(245, 124, 0))
            };
            bool matchedStandard = false;
            foreach (var item in radii)
            {
                bool isCurrent = Math.Abs(item.R - activeR) < 0.03;
                if (isCurrent) matchedStandard = true;
                var series = new ChartSeries
                {
                    Name = item.Title,
                    StrokeBrush = new SolidColorBrush(item.Clr),
                    StrokeThickness = isCurrent ? 2.8 : 1.5,
                    IsHighlighted = isCurrent,
                    IsEngineerOnly = !isCurrent
                };
                for (int i = 0; i <= 40; i++)
                {
                    double fn = 0.02 + (0.42 - 0.02) * i / 40.0;
                    double ra = Math.Min(CalcRa(fn, item.R), 10.0);
                    series.Points.Add(new ChartPoint(fn, ra));
                }
                model.Series.Add(series);
            }
            if (!matchedStandard)
            {
                var customSeries = new ChartSeries
                {
                    Name = string.Format(inv, "Выбранный радиус rε = {0:0.##} мм", activeR),
                    StrokeBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                    StrokeThickness = 2.8,
                    IsHighlighted = true
                };
                for (int i = 0; i <= 40; i++)
                {
                    double fn = 0.02 + (0.42 - 0.02) * i / 40.0;
                    customSeries.Points.Add(new ChartPoint(fn, Math.Min(CalcRa(fn, activeR), 10.0)));
                }
                model.Series.Add(customSeries);
            }
            double currentRa = CalcRa(activeFeed, activeR);
            model.Markers.Add(new WorkingPointMarker
            {
                Title = string.Format(inv, "rε={0:0.##} мм, fn={1:0.###}", activeR, activeFeed),
                Subtitle = string.Format(inv, "Ra теор. = {0:0.00} мкм", currentRa),
                X = Math.Clamp(activeFeed, 0.02, 0.42),
                Y = Math.Clamp(currentRa, 0, 10.0),
                FillBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                ShowGuideLines = true
            });
            bool warn = currentRa > ctx.TargetRa * 1.05;
            return new TurningChartSummary
            {
                ChartModel = model,
                ValueBadge = string.Format(inv, "Ra {0:0.00} мкм", currentRa),
                Headline = warn
            ? string.Format(inv, "Расчётная шероховатость Ra {0:0.00} мкм хуже целевого допуска ({1:0.##} мкм).", currentRa, ctx.TargetRa)
            : string.Format(inv, "Чистота поверхности укладывается в заданный ориентир Ra {0:0.##} мкм.", ctx.TargetRa),
                Advice = "Увеличение радиуса пластины rε вдвое позволяет поднять подачу fn в 1.4 раза при сохранении той же шероховатости (при достаточной жёсткости системы СПИД).",
                IsWarning = warn
            };
        }
        // =====================================================================
        // 3. МОЩНОСТЬ РЕЗАНИЯ НА ШПИНДЕЛЕ Pc(ap)
        // =====================================================================
        private static TurningChartSummary BuildCuttingPowerChart(TurningChartContext ctx)
        {
            var inv = CultureInfo.InvariantCulture;
            double kc11 = ctx.IsoGroup switch
            {
                IsoGroupCode.P => 1850,
                IsoGroupCode.M => 2150,
                IsoGroupCode.K => 1350,
                IsoGroupCode.N => 720,
                IsoGroupCode.S => 2650,
                IsoGroupCode.H => 3100,
                _ => 1850
            };
            double vc = Math.Max(ctx.RecommendedVc, 10.0);
            double fn = Math.Max(ctx.RecommendedFeed, 0.04);
            double curAp = Math.Clamp(ctx.RecommendedAp, 0.1, 6.0);
            // Pc (кВт) = (ap * fn * kc * Vc) / (60 * 1000 * КПД), где kc = kc1.1 * fn^(-0.25)
            double kcActual = kc11 * Math.Pow(fn, -0.25);
            const double efficiency = 0.82;
            double CalcPowerKw(double ap) => (ap * fn * kcActual * vc) / (60000.0 * efficiency);
            double maxPowerY = Math.Max(15.0, Math.Ceiling(CalcPowerKw(5.0) * 1.2 / 5.0) * 5.0);
            var model = new EngineeringChartModel
            {
                ChartTitle = "Потребляемая мощность резания на шпинделе Pc(ap)",
                Description = string.Format(inv, "Расчёт для {0} при Vc = {1:0} м/мин и подаче fn = {2:0.###} мм/об (КПД привода 82%).", ctx.MaterialLabel, vc, fn),
                XAxis = new ChartAxisConfig
                {
                    Title = "Глубина резания ap",
                    Unit = "мм",
                    Min = 0,
                    Max = 6.0,
                    Step = 1.0,
                    NumberFormat = "F1"
                },
                YAxis = new ChartAxisConfig
                {
                    Title = "Мощность Pc",
                    Unit = "кВт",
                    Min = 0,
                    Max = maxPowerY,
                    Step = Math.Max(2.0, Math.Round(maxPowerY / 5.0)),
                    NumberFormat = "F1"
                },
                ActiveMaterialBadge = ctx.MaterialLabel
            };
            model.Thresholds.Add(new ChartThresholdLine
            {
                Label = "Типовой предел токарного ЧПУ (~11 кВт)",
                Orientation = ChartZoneOrientation.Horizontal,
                Value = 11.0,
                StrokeBrush = new SolidColorBrush(Color.FromRgb(230, 81, 0))
            });
            var series = new ChartSeries
            {
                Name = string.Format(inv, "Мощность Pc(ap) для fn = {0:0.###} мм/об", fn),
                StrokeBrush = new SolidColorBrush(Color.FromRgb(106, 27, 154)),
                StrokeThickness = 2.6,
                IsHighlighted = true
            };
            for (int i = 0; i <= 30; i++)
            {
                double ap = 6.0 * i / 30.0;
                series.Points.Add(new ChartPoint(ap, Math.Min(CalcPowerKw(ap), maxPowerY)));
            }
            model.Series.Add(series);
            double curPower = CalcPowerKw(curAp);
            model.Markers.Add(new WorkingPointMarker
            {
                Title = string.Format(inv, "ap = {0:0.##} мм", curAp),
                Subtitle = string.Format(inv, "Pc = {0:0.0} кВт", curPower),
                X = curAp,
                Y = Math.Min(curPower, maxPowerY),
                FillBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                ShowGuideLines = true
            });
            bool warn = curPower > 11.0;
            return new TurningChartSummary
            {
                ChartModel = model,
                ValueBadge = string.Format(inv, "{0:0.0} кВт", curPower),
                Headline = warn
            ? string.Format(inv, "Высокая потребляемая мощность ({0:0.0} кВт) — проверьте нагрузку на главный привод и зажим в кулачках!", curPower)
            : string.Format(inv, "Потребляемая мощность резания составляет {0:0.0} кВт (в пределах стандартного привода).", curPower),
                Advice = "При нехватке мощности шпинделя или провороте заготовки уменьшайте глубину резания ap на проход, сохраняя подачу fn для нормального стружкодробления.",
                IsWarning = warn
            };
        }
        // =====================================================================
        // 4. ИНТЕРАКТИВНЫЙ ГРАФИК РАЗГОНА ШПИНДЕЛЯ n(D) В G96 / G97
        // =====================================================================
        private static TurningChartSummary BuildSpindleProfileChart(TurningChartContext ctx)
        {
            var inv = CultureInfo.InvariantCulture;
            double dStart = Math.Max(ctx.StartDiameter, 5.0);
            double dMin = Math.Clamp(ctx.MinDiameter, 1.0, dStart);
            double xMin = Math.Max(0, Math.Floor(dMin * 0.8 / 5.0) * 5.0);
            double xMax = Math.Ceiling(dStart * 1.1 / 10.0) * 10.0;
            if (xMax <= xMin + 5) xMax = xMin + 20;
            double maxY = ctx.IsG96Mode
            ? Math.Max(ctx.G50MaxRpm * 1.3, 1500.0)
            : Math.Max(ctx.SimRpm * 1.4, 1500.0);
            maxY = Math.Ceiling(maxY / 500.0) * 500.0;
            var model = new EngineeringChartModel
            {
                ChartTitle = ctx.IsG96Mode
            ? "Профиль разгона оборотов шпинделя n(D) в режиме G96 с отсечкой G50"
            : "Режим постоянных оборотов шпинделя n(D) в режиме G97",
                Description = "Наведите курсор на график, чтобы посмотреть точные обороты шпинделя на любом промежуточном диаметре детали.",
                XAxis = new ChartAxisConfig
                {
                    Title = "Текущий диаметр обработки D",
                    Unit = "мм",
                    Min = xMin,
                    Max = xMax,
                    Step = Math.Max(5, Math.Round((xMax - xMin) / 6.0 / 5.0) * 5.0),
                    NumberFormat = "F0"
                },
                YAxis = new ChartAxisConfig
                {
                    Title = "Обороты шпинделя n",
                    Unit = "об/мин",
                    Min = 0,
                    Max = maxY,
                    Step = Math.Max(250, Math.Round(maxY / 5.0 / 250.0) * 250.0),
                    NumberFormat = "F0"
                },
                ActiveMaterialBadge = ctx.MaterialLabel
            };
            if (ctx.IsG96Mode)
            {
                model.Thresholds.Add(new ChartThresholdLine
                {
                    Label = $"Ограничитель патрона G50 S{ctx.G50MaxRpm}",
                    Orientation = ChartZoneOrientation.Horizontal,
                    Value = ctx.G50MaxRpm,
                    StrokeBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40))
                });
            }
            var actualSeries = new ChartSeries
            {
                Name = ctx.IsG96Mode ? "Фактические обороты с учётом G50" : "Постоянные обороты G97",
                StrokeBrush = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
                StrokeThickness = 2.8,
                IsHighlighted = true
            };
            var theorSeries = new ChartSeries
            {
                Name = "Теоретическая гипербола без G50",
                StrokeBrush = new SolidColorBrush(Color.FromRgb(144, 202, 249)),
                StrokeThickness = 1.8,
                IsDashed = true,
                IsHighlighted = false,
                IsEngineerOnly = true
            };
            double scanMin = Math.Max(dMin, 1.0);
            const int steps = 45;
            for (int i = 0; i <= steps; i++)
            {
                double d = scanMin + (dStart - scanMin) * i / steps;
                double theorRpm = ctx.IsG96Mode
                ? (1000.0 * ctx.SimVc) / (Math.PI * d)
                : ctx.SimRpm;
                double actualRpm = ctx.IsG96Mode
                ? Math.Min(theorRpm, ctx.G50MaxRpm)
                : ctx.SimRpm;
                actualSeries.Points.Add(new ChartPoint(d, Math.Min(actualRpm, maxY)));
                if (ctx.IsG96Mode)
                    theorSeries.Points.Add(new ChartPoint(d, Math.Min(theorRpm, maxY)));
            }
            if (ctx.IsG96Mode) model.Series.Add(theorSeries);
            model.Series.Add(actualSeries);
            double startActualRpm = ctx.IsG96Mode
            ? Math.Min((1000.0 * ctx.SimVc) / (Math.PI * dStart), ctx.G50MaxRpm)
            : ctx.SimRpm;
            model.Markers.Add(new WorkingPointMarker
            {
                Title = string.Format(inv, "Старт Ø{0:0.#} мм", dStart),
                Subtitle = string.Format(inv, "{0:0} об/мин", startActualRpm),
                X = dStart,
                Y = Math.Min(startActualRpm, maxY),
                FillBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50)),
                ShowGuideLines = true
            });
            double endActualRpm = ctx.IsG96Mode
            ? Math.Min((1000.0 * ctx.SimVc) / (Math.PI * scanMin), ctx.G50MaxRpm)
            : ctx.SimRpm;
            if (Math.Abs(dStart - scanMin) > 1.0)
            {
                model.Markers.Add(new WorkingPointMarker
                {
                    Title = string.Format(inv, "Финиш Ø{0:0.#} мм", scanMin),
                    Subtitle = string.Format(inv, "{0:0} об/мин", endActualRpm),
                    X = scanMin,
                    Y = Math.Min(endActualRpm, maxY),
                    FillBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                    ShowGuideLines = false
                });
            }
            bool g50Hit = ctx.IsG96Mode && ((1000.0 * ctx.SimVc) / (Math.PI * scanMin)) > ctx.G50MaxRpm;
            return new TurningChartSummary
            {
                ChartModel = model,
                ValueBadge = string.Format(inv, "{0:0} → {1:0} об/мин", startActualRpm, endActualRpm),
                Headline = g50Hit
            ? string.Format(inv, "На малом диаметре шпиндель выходит на ограничение G50 S{0}.", ctx.G50MaxRpm)
            : "Шпиндель работает в штатном диапазоне оборотов на всём перепаде диаметров.",
                Advice = "При отрезке до центра детали (X0) или работе с длинным нежёстким прутком всегда проверяйте порог G50 перед запуском цикла.",
                IsWarning = g50Hit
            };
        }
    }
}