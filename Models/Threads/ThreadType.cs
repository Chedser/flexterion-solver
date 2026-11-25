namespace MechCore.Models.Threads
{
    public enum ThreadType
    {
        Metric,         // Метрическая (M) - ГОСТ 24705 / 8724
        PipeG,          // Трубная цилиндрическая (G) - ГОСТ 6357
        TaperedR,       // Трубная коническая (R / Rc) - ГОСТ 6211
        TaperedNpt,     // Дюймовая коническая (NPT) - ANSI/ASME B1.20.1
        Trapezoidal,    // Трапецеидальная (Tr) - ГОСТ 9484
        Buttress        // Упорная (S) - ГОСТ 10177
    }

    public class ThreadTypeItem
    {
        public ThreadType Type { get; }
        public string ShortCode { get; }
        public string Name { get; }
        public string Standard { get; }

        public ThreadTypeItem(ThreadType type, string shortCode, string name, string standard)
        {
            Type = type;
            ShortCode = shortCode;
            Name = name;
            Standard = standard;
        }
    }
}
