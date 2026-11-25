using System;
using System.Globalization;
using System.Windows.Media;
using MechCore.Models.Charts;
using MechCore.Models.Materials;
using MechCore.Models.Milling;

namespace MechCore.Services.Milling
{
    public class MillingChartService : IMillingChartService
    {
        public MillingChartSummary BuildChart(MillingChartMetric metric, MillingChartContext context)
        {
            return metric switch
            {
                MillingChartMetric.ChipThinningCurve => BuildChipThinningChart(context),
                MillingChartMetric.SpindlePowerLoad => BuildSpindlePowerChart(context),
                MillingChartMetric.EffectiveDiameterAp => BuildEffectiveDiameterChart(context),
                MillingChartMetric.CutterToolLifeTaylor => BuildToolLifeChart(context),
                _ => BuildChipThinningChart(context)
            };
        }

        // =====================================================================
        // 1. УТОНЕНИЕ СТРУЖКИ k_hm(ae / Dc)
        // =====================================================================
        private static MillingChartSummary BuildChipThinningChart(MillingChartContext ctx)
        {
            var inv = CultureInfo.InvariantCulture;
            double dc = Math.Max(ctx.CutterDiameter, 0.5);
            double ae = Math.Clamp(ctx.RadialWidthAe, 0.05, dc);
            double currentEngagementPct = Math.Clamp((ae / dc) * 100.0, 2.0, 100.0);

            double CalcRadialFactor(double pct)
            {
                double ratio = Math.Clamp(pct / 100.0, 0.02, 1.0);
                if (ratio >= 0.5) return 1.0;
                double geom = 2.0 * Math.Sqrt(ratio * (1.0 - ratio));
                return geom > 0.1 ? Math.Clamp(1.0 / geom, 1.0, 2.5) : 2.5;
            }

            var model = new EngineeringChartModel
            {
                ChartTitle = "Коэффициент компенсации утонения стружки k_hm от радиального охвата ae / Dc",
                Description = "При ширине фрезерования ae < 50% D стружка утоняется; для сохранения толщины hm подачу на зуб fz умножают на коэффициент k_hm.",
                XAxis = new ChartAxisConfig
                {
                    Title = "Радиальный захват фрезы ae / Dc",
                    Unit = "%",
                    Min = 0,
                    Max = 100,
                    Step = 10,
                    NumberFormat = "F0"
                },
                YAxis = new ChartAxisConfig
                {
                    Title = "Множитель подачи k_hm",
                    Unit = "×",
                    Min = 0.8,
                    Max = 2.6,
                    Step = 0.2,
                    NumberFormat = "F1"
                },
                ActiveMaterialBadge = ctx.MaterialLabel
            };

            model.Zones.Add(new ChartZone
            {
                Label = "Зона HSM / трохоидального фрезерования (5–20% D)",
                Orientation = ChartZoneOrientation.Vertical,
                Start = 5,
                End = 20,
                FillBrush = new SolidColorBrush(Color.FromArgb(26, 76, 175, 80)),
                LabelBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50))
            });

            model.Thresholds.Add(new ChartThresholdLine
            {
                Label = "Порог утонения (ae = 50% Dc)",
                Orientation = ChartZoneOrientation.Vertical,
                Value = 50,
                StrokeBrush = new SolidColorBrush(Color.FromRgb(25, 118, 210))
            });

            var series = new ChartSeries
            {
                Name = "Радиальный коэффициент компенсации k_hm(ae/Dc)",
                StrokeBrush = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
                StrokeThickness = 2.8,
                IsHighlighted = true
            };

            for (int i = 2; i <= 100; i += 2)
            {
                series.Points.Add(new ChartPoint(i, CalcRadialFactor(i)));
            }
            model.Series.Add(series);

            double curK = CalcRadialFactor(currentEngagementPct);
            model.Markers.Add(new WorkingPointMarker
            {
                Title = string.Format(inv, "ae = {0:0.##} мм ({1:0.#}% D)", ae, currentEngagementPct),
                Subtitle = string.Format(inv, "Множитель ×{0:0.00} (fz={1:0.###})", curK, ctx.AdjustedFz),
                X = currentEngagementPct,
                Y = curK,
                FillBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                ShowGuideLines = true
            });

            bool isHsmZone = currentEngagementPct <= 25.0;
            return new MillingChartSummary
            {
                ChartModel = model,
                ValueBadge = string.Format(inv, "×{0:0.00} (ae = {1:0.#}% D)", curK, currentEngagementPct),
                Headline = isHsmZone
                    ? string.Format(inv, "Режим малого радиального охвата ({0:0.#}% D): компенсация подачи ×{1:0.00} предотвращает скобление кромки.", currentEngagementPct, curK)
                    : string.Format(inv, "Радиальный охват {0:0.#}% D: радиальное утонение стружки {1}.", currentEngagementPct, curK > 1.01 ? "компенсируется" : "отсутствует (k = 1.00)"),
                Advice = "При скоростной выборке карманов (HSM) с ae = 8–15% Dc обязательно включайте компенсацию утонения стружки, иначе фреза будет тереть металл вместо резания.",
                IsWarning = false
            };
        }

        // =====================================================================
        // 2. ЗАГРУЗКА ШПИНДЕЛЯ P_mot(ap) И ПАСПОРТНЫЙ ПРЕДЕЛ СТАНКА
        // =====================================================================
        private static MillingChartSummary BuildSpindlePowerChart(MillingChartContext ctx)
        {
            var inv = CultureInfo.InvariantCulture;
            double dc = Math.Max(ctx.CutterDiameter, 0.5);
            double ae = Math.Clamp(ctx.RadialWidthAe, 0.05, dc);
            double vf = Math.Max(ctx.ActiveVf, 10.0);
            double kc = ctx.SpecificCuttingForceKc > 100 ? ctx.SpecificCuttingForceKc : 2100.0;
            double eta = Math.Clamp(ctx.MachineEfficiency, 0.5, 1.0);
            double pMaxMachine = Math.Max(ctx.MachineSpindlePowerKw, 0.5);

            double maxApAxis = Math.Max(Math.Ceiling(Math.Max(dc * 1.5, ctx.AxialDepthAp * 1.4) / 2.0) * 2.0, 4.0);
            if (ctx.CutterGeometry == MillingCutterGeometryKind.RoundInsert)
            {
                maxApAxis = Math.Max(ctx.InsertDiameterIc * 0.6, ctx.AxialDepthAp * 1.3);
            }

            double CalcMotorKw(double ap) => ((ap * ae * vf * kc) / 60_000_000.0) / eta;

            double maxY = Math.Max(pMaxMachine * 1.35, CalcMotorKw(maxApAxis) * 1.15);
            maxY = Math.Ceiling(maxY / 2.0) * 2.0;

            var model = new EngineeringChartModel
            {
                ChartTitle = "Потребляемая мощность двигателя шпинделя P_mot(ap)",
                Description = string.Format(inv, "Нагрузка на привод при ae = {0:0.##} мм, Vf = {1:0} мм/мин, kc = {2:0} МПа и КПД η = {3:0.00}.", ae, vf, kc, eta),
                XAxis = new ChartAxisConfig
                {
                    Title = "Осевая глубина резания ap",
                    Unit = "мм",
                    Min = 0,
                    Max = maxApAxis,
                    Step = Math.Max(0.5, Math.Round(maxApAxis / 6.0 * 2.0) / 2.0),
                    NumberFormat = "F1"
                },
                YAxis = new ChartAxisConfig
                {
                    Title = "Мощность двигателя P_mot",
                    Unit = "кВт",
                    Min = 0,
                    Max = maxY,
                    Step = Math.Max(1.0, Math.Round(maxY / 5.0)),
                    NumberFormat = "F1"
                },
                ActiveMaterialBadge = ctx.MaterialLabel
            };

            model.Zones.Add(new ChartZone
            {
                Label = "Зона перегрузки привода (> 100% Pст)",
                Orientation = ChartZoneOrientation.Horizontal,
                Start = pMaxMachine,
                End = maxY,
                FillBrush = new SolidColorBrush(Color.FromArgb(26, 198, 40, 40)),
                LabelBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40))
            });

            model.Thresholds.Add(new ChartThresholdLine
            {
                Label = string.Format(inv, "Паспортная мощность станка Pст = {0:0.##} кВт (100%)", pMaxMachine),
                Orientation = ChartZoneOrientation.Horizontal,
                Value = pMaxMachine,
                StrokeBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40))
            });

            var series = new ChartSeries
            {
                Name = "Потребляемая мощность P_mot(ap)",
                StrokeBrush = new SolidColorBrush(Color.FromRgb(106, 27, 154)),
                StrokeThickness = 2.8,
                IsHighlighted = true
            };

            for (int i = 0; i <= 30; i++)
            {
                double ap = maxApAxis * i / 30.0;
                series.Points.Add(new ChartPoint(ap, Math.Min(CalcMotorKw(ap), maxY)));
            }
            model.Series.Add(series);

            double curAp = Math.Clamp(ctx.AxialDepthAp, 0.05, maxApAxis);
            double curKw = CalcMotorKw(curAp);
            double loadPct = (curKw / pMaxMachine) * 100.0;

            model.Markers.Add(new WorkingPointMarker
            {
                Title = string.Format(inv, "ap = {0:0.##} мм", curAp),
                Subtitle = string.Format(inv, "{0:0.##} кВт ({1:0}%)", curKw, loadPct),
                X = curAp,
                Y = Math.Min(curKw, maxY),
                FillBrush = new SolidColorBrush(loadPct > 100 ? Color.FromRgb(198, 40, 40) : Color.FromRgb(46, 125, 50)),
                ShowGuideLines = true
            });

            bool warn = loadPct >= 85.0;
            return new MillingChartSummary
            {
                ChartModel = model,
                ValueBadge = string.Format(inv, "{0:0.##} кВт ({1:0}%)", curKw, loadPct),
                Headline = loadPct > 100.0
? string.Format(inv, "ПЕРЕГРУЗКА ШПИНДЕЛЯ ({0:0}%): мощность резания превышает паспортный предел станка!", loadPct)
: string.Format(inv, "Загрузка главного привода составляет {0:0}% от паспортной мощности ({1:0.##} кВт).", loadPct, pMaxMachine),
                Advice = "При приближении загрузки к 85–100% снизьте осевую глубину ap на проход или уменьшите радиальный шаг ae.",
                IsWarning = warn
            };
        }
        // =====================================================================
        // 3. ЭФФЕКТИВНЫЙ ДИАМЕТР РЕЗАНИЯ Deff(ap) ДЛЯ СФЕРЫ / СМП / КОНЦЕВОЙ
        // =====================================================================
        private static MillingChartSummary BuildEffectiveDiameterChart(MillingChartContext ctx)
        {
            var inv = CultureInfo.InvariantCulture;
            double dc = Math.Max(ctx.CutterDiameter, 1.0);
            double ic = Math.Clamp(ctx.InsertDiameterIc, 3.0, dc);
            double maxAp = ctx.CutterGeometry switch
            {
                MillingCutterGeometryKind.BallNose => dc / 2.0,
                MillingCutterGeometryKind.RoundInsert => ic / 2.0,
                _ => Math.Max(dc * 0.5, ctx.AxialDepthAp * 1.2)
            };
            maxAp = Math.Max(maxAp, 1.0);
            double CalcDeff(double ap)
            {
                if (ctx.CutterGeometry == MillingCutterGeometryKind.BallNose)
                {
                    double r = dc / 2.0;
                    return ap >= r ? dc : Math.Max(0.2, 2.0 * Math.Sqrt(ap * (dc - ap)));
                }
                if (ctx.CutterGeometry == MillingCutterGeometryKind.RoundInsert)
                {
                    double r = ic / 2.0;
                    if (ap >= r) return dc;
                    double chord = 2.0 * Math.Sqrt(ap * (ic - ap));
                    return Math.Max(0.5, dc - ic + chord);
                }
                return dc;
            }
            var model = new EngineeringChartModel
            {
                ChartTitle = "Зависимость эффективного рабочего диаметра Deff от глубины ap",
                Description = "Для сферических фрез и круглых пластин при малых глубинах ap реальный диаметр контакта Deff меньше номинального Dc.",
                XAxis = new ChartAxisConfig
                {
                    Title = "Осевая глубина резания ap",
                    Unit = "мм",
                    Min = 0,
                    Max = maxAp,
                    Step = Math.Max(0.2, Math.Round(maxAp / 5.0 * 5.0) / 5.0),
                    NumberFormat = "F2"
                },
                YAxis = new ChartAxisConfig
                {
                    Title = "Эффективный диаметр Deff",
                    Unit = "мм",
                    Min = 0,
                    Max = Math.Ceiling(dc * 1.15),
                    Step = Math.Max(1.0, Math.Round(dc / 5.0)),
                    NumberFormat = "F1"
                },
                ActiveMaterialBadge = ctx.MaterialLabel
            };
            model.Thresholds.Add(new ChartThresholdLine
            {
                Label = string.Format(inv, "Номинальный диаметр фрезы Dc = {0:0.##} мм", dc),
                Orientation = ChartZoneOrientation.Horizontal,
                Value = dc,
                StrokeBrush = new SolidColorBrush(Color.FromRgb(25, 118, 210))
            });
            var series = new ChartSeries
            {
                Name = "Кривая эффективного диаметра Deff(ap)",
                StrokeBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50)),
                StrokeThickness = 2.8,
                IsHighlighted = true
            };
            for (int i = 0; i <= 40; i++)
            {
                double ap = maxAp * i / 40.0;
                series.Points.Add(new ChartPoint(ap, CalcDeff(ap)));
            }
            model.Series.Add(series);
            double curAp = Math.Clamp(ctx.AxialDepthAp, 0.02, maxAp);
            double curDeff = CalcDeff(curAp);
            model.Markers.Add(new WorkingPointMarker
            {
                Title = string.Format(inv, "ap = {0:0.##} мм", curAp),
                Subtitle = string.Format(inv, "Deff = {0:0.##} мм", curDeff),
                X = curAp,
                Y = curDeff,
                FillBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                ShowGuideLines = true
            });
            bool reduced = curDeff < dc * 0.95;
            return new MillingChartSummary
            {
                ChartModel = model,
                ValueBadge = string.Format(inv, "Deff = {0:0.##} мм", curDeff),
                Headline = reduced
            ? string.Format(inv, "Рабочий диаметр контакта Deff ({0:0.##} мм) составляет {1:0}% от номинала Dc ({2:0.##} мм).", curDeff, (curDeff / dc) * 100.0, dc)
            : string.Format(inv, "Фреза работает полным номинальным диаметром Deff = Dc = {0:0.##} мм.", dc),
                Advice = "При чистовой 3D-обработке сферической фрезой малыми строчками (ap = 0.1–0.5 мм) обязательно пересчитывайте обороты S по Deff, иначе реальная скорость Vc упадёт в 3–5 раз.",
                IsWarning = reduced
            };
        }
        // =====================================================================
        // 4. СТОЙКОСТЬ ЗУБЬЕВ ФРЕЗЫ ТЕЙЛОРА: T(Vc)
        // =====================================================================
        private static MillingChartSummary BuildToolLifeChart(MillingChartContext ctx)
        {
            var inv = CultureInfo.InvariantCulture;
            double m = ctx.ToolMaterial switch
            {
                MillingToolMaterialKind.HssCo => 0.15,
                MillingToolMaterialKind.UncoatedCarbide => 0.24,
                MillingToolMaterialKind.SolidCarbide => 0.29,
                MillingToolMaterialKind.IndexableCarbide => 0.31,
                _ => 0.27
            };
            double baseVc20 = ctx.IsoGroup switch
            {
                IsoGroupCode.P => 195,
                IsoGroupCode.M => 130,
                IsoGroupCode.K => 220,
                IsoGroupCode.N => 550,
                IsoGroupCode.S => 50,
                IsoGroupCode.H => 105,
                _ => 180
            };
            double toolMult = ctx.ToolMaterial switch
            {
                MillingToolMaterialKind.HssCo => 0.32,
                MillingToolMaterialKind.UncoatedCarbide => 0.80,
                MillingToolMaterialKind.SolidCarbide => 1.00,
                MillingToolMaterialKind.IndexableCarbide => 0.92,
                _ => 1.0
            };
            double vc20 = Math.Max(15.0, baseVc20 * toolMult);
            double minVc = Math.Max(10.0, Math.Floor(vc20 * 0.35 / 10.0) * 10.0);
            double maxVc = Math.Ceiling(vc20 * 1.65 / 50.0) * 50.0;
            double CalcLife(double vc)
            {
                double safeVc = Math.Max(vc, 5.0);
                double t = 20.0 * Math.Pow(vc20 / safeVc, 1.0 / m);
                return Math.Clamp(t, 1.0, 90.0);
            }
            var model = new EngineeringChartModel
            {
                ChartTitle = "Кривая стойкости фрезы Тейлора T(Vc)",
                Description = $"Ориентировочное время стойкости зубьев фрезы до переточки / поворота пластины ({ctx.MaterialLabel}).",
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
                    Title = "Стойкость фрезы T",
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
                Label = "Оптимальный ресурс фрезы (20–50 мин)",
                Orientation = ChartZoneOrientation.Horizontal,
                Start = 20,
                End = 50,
                FillBrush = new SolidColorBrush(Color.FromArgb(26, 76, 175, 80)),
                LabelBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50))
            });
            var curve = new ChartSeries
            {
                Name = $"Кривая стойкости ({ctx.MaterialLabel}",
                StrokeBrush = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
                StrokeThickness = 2.8,
                IsHighlighted = true
            };
            for (int i = 0; i <= 40; i++)
            {
                double vc = minVc + (maxVc - minVc) * i / 40.0;
                curve.Points.Add(new ChartPoint(vc, CalcLife(vc)));
            }
            model.Series.Add(curve);
            double activeVc = Math.Clamp(ctx.ActiveVc > 0 ? ctx.ActiveVc : vc20, minVc, maxVc);
            double life = CalcLife(activeVc);
            model.Markers.Add(new WorkingPointMarker
            {
                Title = string.Format(inv, "Vc = {0:0} м/мин", activeVc),
                Subtitle = string.Format(inv, "T ≈ {0:0} мин", life),
                X = activeVc,
                Y = life,
                FillBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                ShowGuideLines = true
            });
            bool warn = life < 12.0;
            return new MillingChartSummary
            {
                ChartModel = model,
                ValueBadge = string.Format(inv, "T ≈ {0:0} мин", life),
                Headline = warn
            ? "Повышенная скорость Vc сокращает ресурс фрезы — возможен скорый термический износ зубьев."
            : "Стойкость режущих зубьев находится в стабильном производственном диапазоне.",
                Advice = "При фрезеровании титана (ISO S) и нержавейки (ISO M) не превышайте каталожную Vc: тепло отводится плохо и мгновенно разрушает покрытие зубьев.",
                IsWarning = warn
            };
        }
    }
}