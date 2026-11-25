using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
using MechCore.Models.Threads;

namespace MechCore.Services.Threads
{
    public class GostThreadService : IThreadService
    {
        private readonly List<ThreadTypeItem> _types;
        private readonly List<ThreadSpecItem> _allSpecs;

        public GostThreadService()
        {
            _types = new List<ThreadTypeItem>
            {
                new(ThreadType.Metric, "M", "Метрическая (M)", "ГОСТ 8724-2002 / 24705-2004"),
                new(ThreadType.PipeG, "G", "Трубная цил. (G)", "ГОСТ 6357-81"),
                new(ThreadType.TaperedR, "R / Rc", "Трубная кон. (R)", "ГОСТ 6211-81"),
                new(ThreadType.TaperedNpt, "NPT", "Коническая (NPT)", "ANSI/ASME B1.20.1"),
                new(ThreadType.Trapezoidal, "Tr", "Трапецеидальная (Tr)", "ГОСТ 24737-81 / 9484-81"),
                new(ThreadType.Buttress, "S", "Упорная (S)", "ГОСТ 10177-82")
            };

            _allSpecs = BuildStandardDatabase();
        }

        public List<ThreadTypeItem> GetThreadTypes() => _types;

        public List<ThreadSpecItem> GetSpecsByType(ThreadType type) =>
            _allSpecs.Where(x => x.Type == type).ToList();

        private static List<ThreadSpecItem> BuildStandardDatabase()
        {
            var list = new List<ThreadSpecItem>();

            // =========================================================================
            // 1. МЕТРИЧЕСКАЯ РЕЗЬБА (M) — ГОСТ 8724-2002 (Таблица 1, ряды 1–3)
            // =========================================================================
            AddMetric(list, 2, 0.4, 0.25);
            AddMetric(list, 2.5, 0.45, 0.35);
            AddMetric(list, 3, 0.5, 0.35);
            AddMetric(list, 3.5, 0.6, 0.35);
            AddMetric(list, 4, 0.7, 0.5);
            AddMetric(list, 5, 0.8, 0.5);
            AddMetric(list, 6, 1.0, 0.75, 0.5);
            AddMetric(list, 7, 1.0, 0.75, 0.5);
            AddMetric(list, 8, 1.25, 1.0, 0.75, 0.5);
            AddMetric(list, 9, 1.25, 1.0, 0.75, 0.5);
            AddMetric(list, 10, 1.5, 1.25, 1.0, 0.75, 0.5);
            AddMetric(list, 11, 1.5, 1.0, 0.75, 0.5);
            AddMetric(list, 12, 1.75, 1.5, 1.25, 1.0, 0.75, 0.5);
            AddMetric(list, 14, 2.0, 1.5, 1.25, 1.0, 0.75, 0.5);
            AddMetric(list, 15, null, 1.5, 1.0);
            AddMetric(list, 16, 2.0, 1.5, 1.0, 0.75, 0.5);
            AddMetric(list, 17, null, 1.5, 1.0);
            AddMetric(list, 18, 2.5, 2.0, 1.5, 1.0, 0.75, 0.5);
            AddMetric(list, 20, 2.5, 2.0, 1.5, 1.0, 0.75, 0.5);
            AddMetric(list, 22, 2.5, 2.0, 1.5, 1.0, 0.75, 0.5);
            AddMetric(list, 24, 3.0, 2.0, 1.5, 1.0, 0.75);
            AddMetric(list, 25, null, 2.0, 1.5, 1.0);
            AddMetric(list, 27, 3.0, 2.0, 1.5, 1.0, 0.75);
            AddMetric(list, 28, null, 2.0, 1.5, 1.0);
            AddMetric(list, 30, 3.5, 3.0, 2.0, 1.5, 1.0, 0.75);
            AddMetric(list, 32, null, 2.0, 1.5);
            AddMetric(list, 33, 3.5, 3.0, 2.0, 1.5, 1.0, 0.75);
            AddMetric(list, 35, null, 1.5);
            AddMetric(list, 36, 4.0, 3.0, 2.0, 1.5, 1.0);
            AddMetric(list, 38, null, 1.5);
            AddMetric(list, 39, 4.0, 3.0, 2.0, 1.5, 1.0);
            AddMetric(list, 40, null, 3.0, 2.0, 1.5);
            AddMetric(list, 42, 4.5, 4.0, 3.0, 2.0);
            AddMetric(list, 45, 4.5, 4.0, 3.0, 2.0);
            AddMetric(list, 48, 5.0, 4.0, 3.0, 2.0);
            AddMetric(list, 50, null, 3.0, 2.0);
            AddMetric(list, 52, 5.0, 4.0, 3.0, 2.0);
            AddMetric(list, 55, null, 4.0, 3.0, 2.0);
            AddMetric(list, 56, 5.5, 4.0, 3.0, 2.0);
            AddMetric(list, 60, 5.5, 4.0, 3.0, 2.0);
            AddMetric(list, 64, 6.0, 4.0, 3.0, 2.0);
            AddMetric(list, 68, 6.0, 4.0, 3.0, 2.0);
            AddMetric(list, 72, null, 6.0, 4.0, 3.0, 2.0);
            AddMetric(list, 76, null, 6.0, 4.0, 3.0, 2.0);
            AddMetric(list, 80, null, 6.0, 4.0, 3.0, 2.0);

            // =========================================================================
            // 2. ТРУБНАЯ ЦИЛИНДРИЧЕСКАЯ (G) — ГОСТ 6357-81 (Таблица 1)
            // =========================================================================
            AddInch(list, ThreadType.PipeG, "G 1/16\"", 7.723, 28);
            AddInch(list, ThreadType.PipeG, "G 1/8\"", 9.728, 28);
            AddInch(list, ThreadType.PipeG, "G 1/4\"", 13.157, 19);
            AddInch(list, ThreadType.PipeG, "G 3/8\"", 16.662, 19);
            AddInch(list, ThreadType.PipeG, "G 1/2\"", 20.955, 14);
            AddInch(list, ThreadType.PipeG, "G 5/8\"", 22.911, 14);
            AddInch(list, ThreadType.PipeG, "G 3/4\"", 26.441, 14);
            AddInch(list, ThreadType.PipeG, "G 7/8\"", 30.201, 14);
            AddInch(list, ThreadType.PipeG, "G 1\"", 33.249, 11);
            AddInch(list, ThreadType.PipeG, "G 1 1/8\"", 37.897, 11);
            AddInch(list, ThreadType.PipeG, "G 1 1/4\"", 41.910, 11);
            AddInch(list, ThreadType.PipeG, "G 1 1/2\"", 47.803, 11);
            AddInch(list, ThreadType.PipeG, "G 1 3/4\"", 53.746, 11);
            AddInch(list, ThreadType.PipeG, "G 2\"", 59.614, 11);
            AddInch(list, ThreadType.PipeG, "G 2 1/2\"", 75.184, 11);
            AddInch(list, ThreadType.PipeG, "G 3\"", 87.884, 11);
            AddInch(list, ThreadType.PipeG, "G 4\"", 113.030, 11);

            // =========================================================================
            // 3. ТРУБНАЯ КОНИЧЕСКАЯ (R / Rc) — ГОСТ 6211-81 (Таблица 1, основная плоскость)
            // =========================================================================
            AddInch(list, ThreadType.TaperedR, "R 1/16\"", 7.723, 28);
            AddInch(list, ThreadType.TaperedR, "R 1/8\"", 9.728, 28);
            AddInch(list, ThreadType.TaperedR, "R 1/4\"", 13.157, 19);
            AddInch(list, ThreadType.TaperedR, "R 3/8\"", 16.662, 19);
            AddInch(list, ThreadType.TaperedR, "R 1/2\"", 20.955, 14);
            AddInch(list, ThreadType.TaperedR, "R 3/4\"", 26.441, 14);
            AddInch(list, ThreadType.TaperedR, "R 1\"", 33.249, 11);
            AddInch(list, ThreadType.TaperedR, "R 1 1/4\"", 41.910, 11);
            AddInch(list, ThreadType.TaperedR, "R 1 1/2\"", 47.803, 11);
            AddInch(list, ThreadType.TaperedR, "R 2\"", 59.614, 11);
            AddInch(list, ThreadType.TaperedR, "R 2 1/2\"", 75.184, 11);
            AddInch(list, ThreadType.TaperedR, "R 3\"", 87.884, 11);
            AddInch(list, ThreadType.TaperedR, "R 4\"", 113.030, 11);

            // =========================================================================
            // 4. ДЮЙМОВАЯ КОНИЧЕСКАЯ (NPT) — ANSI/ASME B1.20.1 (основная плоскость)
            // =========================================================================
            AddInch(list, ThreadType.TaperedNpt, "1/16\" NPT", 7.938, 27);
            AddInch(list, ThreadType.TaperedNpt, "1/8\" NPT", 10.287, 27);
            AddInch(list, ThreadType.TaperedNpt, "1/4\" NPT", 13.716, 18);
            AddInch(list, ThreadType.TaperedNpt, "3/8\" NPT", 17.145, 18);
            AddInch(list, ThreadType.TaperedNpt, "1/2\" NPT", 21.336, 14);
            AddInch(list, ThreadType.TaperedNpt, "3/4\" NPT", 26.670, 14);
            AddInch(list, ThreadType.TaperedNpt, "1\" NPT", 33.401, 11.5);
            AddInch(list, ThreadType.TaperedNpt, "1 1/4\" NPT", 42.164, 11.5);
            AddInch(list, ThreadType.TaperedNpt, "1 1/2\" NPT", 48.260, 11.5);
            AddInch(list, ThreadType.TaperedNpt, "2\" NPT", 60.325, 11.5);
            AddInch(list, ThreadType.TaperedNpt, "2 1/2\" NPT", 73.025, 8);
            AddInch(list, ThreadType.TaperedNpt, "3\" NPT", 88.900, 8);
            AddInch(list, ThreadType.TaperedNpt, "4\" NPT", 114.300, 8);

            // =========================================================================
            // 5. ТРАПЕЦЕИДАЛЬНАЯ (Tr) — ГОСТ 24737-81 (основной, мелкий и крупный ряды)
            // =========================================================================
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 8, mainPitch: 1.5, finePitch: null, largePitch: null);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 10, mainPitch: 2.0, finePitch: 1.5, largePitch: null);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 12, mainPitch: 3.0, finePitch: 2.0, largePitch: null);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 14, mainPitch: 3.0, finePitch: 2.0, largePitch: null);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 16, mainPitch: 4.0, finePitch: 2.0, largePitch: null);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 18, mainPitch: 4.0, finePitch: 2.0, largePitch: null);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 20, mainPitch: 4.0, finePitch: 2.0, largePitch: null);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 22, mainPitch: 5.0, finePitch: 3.0, largePitch: 8.0);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 24, mainPitch: 5.0, finePitch: 3.0, largePitch: 8.0);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 26, mainPitch: 5.0, finePitch: 3.0, largePitch: 8.0);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 28, mainPitch: 5.0, finePitch: 3.0, largePitch: 8.0);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 32, mainPitch: 6.0, finePitch: 3.0, largePitch: 10.0);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 36, mainPitch: 6.0, finePitch: 3.0, largePitch: 10.0);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 40, mainPitch: 7.0, finePitch: 3.0, largePitch: 10.0);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 44, mainPitch: 7.0, finePitch: 3.0, largePitch: 12.0);

            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 48, mainPitch: 8.0, finePitch: 3.0, largePitch: 12.0);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 52, mainPitch: 8.0, finePitch: 3.0, largePitch: 12.0);
            AddMultiSeries(list, ThreadType.Trapezoidal, "Tr", 60, mainPitch: 9.0, finePitch: 3.0, largePitch: 14.0);
            // =========================================================================
            // 6. УПОРНАЯ (S) — ГОСТ 10177-82 (основной, мелкий и крупный ряды)
            // =========================================================================
            AddMultiSeries(list, ThreadType.Buttress, "S", 10, mainPitch: 2.0, finePitch: null, largePitch: null);
            AddMultiSeries(list, ThreadType.Buttress, "S", 12, mainPitch: 3.0, finePitch: 2.0, largePitch: null);
            AddMultiSeries(list, ThreadType.Buttress, "S", 14, mainPitch: 3.0, finePitch: 2.0, largePitch: null);
            AddMultiSeries(list, ThreadType.Buttress, "S", 16, mainPitch: 4.0, finePitch: 2.0, largePitch: null);
            AddMultiSeries(list, ThreadType.Buttress, "S", 18, mainPitch: 4.0, finePitch: 2.0, largePitch: null);
            AddMultiSeries(list, ThreadType.Buttress, "S", 20, mainPitch: 4.0, finePitch: 2.0, largePitch: null);
            AddMultiSeries(list, ThreadType.Buttress, "S", 22, mainPitch: 5.0, finePitch: 3.0, largePitch: 8.0);
            AddMultiSeries(list, ThreadType.Buttress, "S", 24, mainPitch: 5.0, finePitch: 3.0, largePitch: 8.0);
            AddMultiSeries(list, ThreadType.Buttress, "S", 28, mainPitch: 5.0, finePitch: 3.0, largePitch: 8.0);
            AddMultiSeries(list, ThreadType.Buttress, "S", 32, mainPitch: 6.0, finePitch: 3.0, largePitch: 10.0);
            AddMultiSeries(list, ThreadType.Buttress, "S", 36, mainPitch: 6.0, finePitch: 3.0, largePitch: 10.0);
            AddMultiSeries(list, ThreadType.Buttress, "S", 40, mainPitch: 7.0, finePitch: 3.0, largePitch: 10.0);
            AddMultiSeries(list, ThreadType.Buttress, "S", 44, mainPitch: 7.0, finePitch: 3.0, largePitch: 12.0);
            AddMultiSeries(list, ThreadType.Buttress, "S", 48, mainPitch: 8.0, finePitch: 3.0, largePitch: 12.0);
            AddMultiSeries(list, ThreadType.Buttress, "S", 52, mainPitch: 8.0, finePitch: 3.0, largePitch: 12.0);
            AddMultiSeries(list, ThreadType.Buttress, "S", 60, mainPitch: 9.0, finePitch: 3.0, largePitch: 14.0);
            return list;
        }
        private static void AddMetric(List<ThreadSpecItem> list, double d, double? coarsePitch, params double[] finePitches)
        {
            if (coarsePitch.HasValue)
            {
                list.Add(new ThreadSpecItem(ThreadType.Metric, $"M{d:0.##}", d, coarsePitch.Value, "основной"));
            }

            foreach (double fine in finePitches)
            {
                list.Add(new ThreadSpecItem(ThreadType.Metric, $"M{d:0.##}", d, fine, "мелкий"));
            }
        }

        private static void AddMultiSeries(
            List<ThreadSpecItem> list,
            ThreadType type,
            string prefix,
            double d,
            double mainPitch,
            double? finePitch,
            double? largePitch)
        {
            list.Add(new ThreadSpecItem(type, $"{prefix} {d:0.##}", d, mainPitch, "основной"));

            if (finePitch.HasValue)
                list.Add(new ThreadSpecItem(type, $"{prefix} {d:0.##}", d, finePitch.Value, "мелкий"));

            if (largePitch.HasValue)
                list.Add(new ThreadSpecItem(type, $"{prefix} {d:0.##}", d, largePitch.Value, "крупный"));
        }

        private static void AddInch(List<ThreadSpecItem> list, ThreadType type, string designation, double dMm, double tpi)
        {
            double pitchMm = 25.4 / tpi;
            list.Add(new ThreadSpecItem(type, designation, dMm, pitchMm, string.Empty, tpi));
        }
    }
}