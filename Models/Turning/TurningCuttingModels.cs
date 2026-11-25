using MechCore.Models.Materials;

namespace MechCore.Models.Turning
{
    public enum TurningOperationKind
    {
        ExternalTurning,    // Наружное продольное точение и торцевание
        InternalBoring,     // Внутреннее растачивание
        PartingAndGrooving, // Отрезка и прорезка канавок (для G75)
        Drilling,           // Сверление спиральным сверлом (для G81 / G83)
        Reaming,            // Зенкерование и развёртывание
        ThreadTurning       // Нарезание резьбы резцом (для G76)
    }

    public enum CuttingStageKind
    {
        Roughing,           // Черновая обработка
        SemiFinishing,      // Получистовая обработка
        Finishing           // Чистовая обработка
    }

    public enum ToolMaterialKind
    {
        CoatedCarbide,      // Твёрдый сплав с покрытием (CVD / PVD)
        UncoatedCarbide,    // Твёрдый сплав без покрытия
        Cermet,             // Кермет (чистовая обработка)
        CbnOrCeramic,       // CBN (эльбор) / Керамика (закалённые стали)
        Hss                 // Быстрорежущая сталь (HSS / Р6М5)
    }

    public class TurningOperationOption
    {
        public TurningOperationKind Kind { get; }
        public string Title { get; }
        public string ShortNote { get; }

        public TurningOperationOption(TurningOperationKind kind, string title, string shortNote)
        {
            Kind = kind;
            Title = title;
            ShortNote = shortNote;
        }
    }

    public class CuttingStageOption
    {
        public CuttingStageKind Kind { get; }
        public string Title { get; }
        public string ShortNote { get; }

        public CuttingStageOption(CuttingStageKind kind, string title, string shortNote)
        {
            Kind = kind;
            Title = title;
            ShortNote = shortNote;
        }
    }

    public class ToolMaterialOption
    {
        public ToolMaterialKind Kind { get; }
        public string Title { get; }
        public string ShortNote { get; }

        public ToolMaterialOption(ToolMaterialKind kind, string title, string shortNote)
        {
            Kind = kind;
            Title = title;
            ShortNote = shortNote;
        }
    }

    public class TurningCuttingRequest
    {
        public IsoMaterialGroupItem IsoGroup { get; set; } = null!;
        public MaterialGradeItem? SelectedGrade { get; set; }

        public TurningOperationKind Operation { get; set; } = TurningOperationKind.ExternalTurning;
        public CuttingStageKind Stage { get; set; } = CuttingStageKind.Roughing;
        public ToolMaterialKind ToolMaterial { get; set; } = ToolMaterialKind.CoatedCarbide;

        // Чистые числовые параметры (для прямого переиспользования в других модулях)
        public double Diameter { get; set; } = 50.0;        // Диаметр обработки или сверла D, мм
        public double OverhangRatio { get; set; } = 3.0;    // Относительный вылет L/D (жёсткость)
        public double GrooveWidth { get; set; } = 3.0;      // Ширина отрезного/канавочного резца B, мм
    }

    public class TurningCuttingResult
    {
        public bool IsValid { get; set; }

        // --- Числовые значения (для автоподбора на других страницах, напр. G75) ---
        public double VcMin { get; set; }
        public double VcMax { get; set; }
        public double RecommendedVc { get; set; }           // Рекомендуемая скорость резания, м/мин

        public int RecommendedRpm { get; set; }             // Расчётные обороты шпинделя, об/мин
        public int RecommendedG50 { get; set; }             // Рекомендуемое ограничение G50, об/мин

        public double FeedPerRevMin { get; set; }
        public double FeedPerRevMax { get; set; }
        public double RecommendedFeedPerRev { get; set; }   // Рекомендуемая оборотная подача fn, мм/об
        public double RecommendedFeedPerMin { get; set; }   // Минутная подача Vf, мм/мин

        public double DepthOfCutMin { get; set; }
        public double DepthOfCutMax { get; set; }
        public double RecommendedDepthOfCut { get; set; }   // Рекомендуемая глубина резания ap, мм

        // --- Отформатированные строки для интерфейса (UI) ---
        public string VcRangeText { get; set; } = "—";
        public string RecommendedVcText { get; set; } = "—";

        public string SpindleRpmText { get; set; } = "—";
        public string RecommendedG50Text { get; set; } = "—";

        public string FeedPerRevRangeText { get; set; } = "—";
        public string RecommendedFeedPerRevText { get; set; } = "—";
        public string FeedPerMinText { get; set; } = "—";

        public string DepthOfCutRangeText { get; set; } = "—";
        public string RecommendedDepthOfCutText { get; set; } = "—";

        // Инженерные рекомендации (режим Оператор)
        public string GeometryAdvice { get; set; } = "—";
        public string CoolantAdvice { get; set; } = "—";

        // Чистый фрагмент G-кода запуска режима (без комментариев)
        public string SampleGCode { get; set; } = "—";

        // Единая система сообщений проекта
        public bool HasWarning { get; set; }
        public string WarningMessage { get; set; } = string.Empty;

        public bool HasError { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public enum CuttingCalcInputMode
    {
        ByCuttingSpeedVc,   // Задана скорость резания Vc (м/мин) -> расчёт оборотов n
        BySpindleRpm        // Заданы обороты шпинделя n (об/мин) -> расчёт фактической Vc
    }

    public class SpindleSimulationRequest
    {
        public bool IsConstantSurfaceSpeed { get; set; } = true; // true = G96, false = G97
        public CuttingCalcInputMode InputMode { get; set; } = CuttingCalcInputMode.ByCuttingSpeedVc;

        public double InputVc { get; set; } = 180.0;             // Скорость резания Vc, м/мин
        public int InputRpm { get; set; } = 1200;                // Обороты n (при режиме G97 или расчёте Vc), об/мин
        public double FeedPerRev { get; set; } = 0.25;           // Оборотная подача fn, мм/об

        public double StartDiameter { get; set; } = 80.0;        // Начальный (наружный) диаметр D_нач, мм
        public double MinDiameter { get; set; } = 15.0;          // Минимальный (конечный) диаметр D_мин, мм
        public int MaxChuckRpmG50 { get; set; } = 2500;          // Ограничение патрона G50 S..., об/мин
                                                                 // Необязательный контроль соответствия выбранному материалу из справочника
        public string? MaterialContextLabel { get; set; }
        public double? ReferenceVcMin { get; set; }
        public double? ReferenceVcMax { get; set; }
    }

    public class SpindleSimulationResult
    {
        public bool IsValid { get; set; }

        // Основные пересчитанные величины
        public string StartRpmText { get; set; } = "—";          // Обороты на D_нач, об/мин
        public string MinDiaTheoreticalRpmText { get; set; } = "—"; // Расчётные обороты на D_мин без отсечки
        public string MinDiaActualRpmText { get; set; } = "—";   // Фактические обороты на D_мин с учётом G50

        public string StartVcText { get; set; } = "—";           // Фактическая скорость резания на D_нач, м/мин
        public string MinDiaActualVcText { get; set; } = "—";    // Фактическая скорость резания на D_мин, м/мин

        public string FeedPerMinRangeText { get; set; } = "—";   // Минутная подача Vf (на D_нач -> на D_мин), мм/мин
        public string CutoffDiameterText { get; set; } = "—";    // Диаметр отсечки G50 (где шпиндель упрётся в потолок)

        public bool IsG50Triggered { get; set; }                 // Сработало ли ограничение G50
        public string SampleGCode { get; set; } = "—";

        // Координаты для графика n(D) на Canvas 320x200
        public string RpmCurvePoints { get; set; } = string.Empty;      // Ломаная фактических оборотов n(D)
        public string TheoreticalCurvePoints { get; set; } = string.Empty; // Пунктир теоретического разгона без G50
        public double G50LineY { get; set; } = 40.0;                    // Горизонтальная линия предела G50
        public string G50LabelText { get; set; } = string.Empty;
        public string DStartLabelText { get; set; } = string.Empty;
        public string DMinLabelText { get; set; } = string.Empty;
        public bool ShowCutoffDot { get; set; }
        public double CutoffDotX { get; set; }
        public double CutoffDotY { get; set; }

        // Единая система сообщений проекта
        public bool HasWarning { get; set; }
        public string WarningMessage { get; set; } = string.Empty;

        public bool HasError { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public enum RoughnessCalcMode
    {
        ByTargetRoughness,  // Задан параметр чертежа (Ra или Rz) -> подбор оптимальной подачи fn
        ByFeedRate          // Задана подача fn -> расчёт теоретической шероховатости Ra / Rz
    }

    public enum RoughnessParameterKind
    {
        Ra,                 // Среднее арифметическое отклонение профиля Ra, мкм
        Rz                  // Высота неровностей профиля по 10 точкам Rz, мкм
    }

    public class RoughnessCalculationRequest
    {
        public RoughnessCalcMode Mode { get; set; } = RoughnessCalcMode.ByTargetRoughness;
        public RoughnessParameterKind ParameterKind { get; set; } = RoughnessParameterKind.Ra;

        public double NoseRadius { get; set; } = 0.4;           // Радиус при вершине пластины r_ε, мм
        public double TargetRoughnessValue { get; set; } = 1.6; // Требуемая шероховатость Ra или Rz, мкм
        public double InputFeedPerRev { get; set; } = 0.12;     // Подача fn (при прямом расчёте), мм/об

        // Необязательный контекст жёсткости из верхнего справочника (L/D)
        public double? OverhangRatioContext { get; set; }
    }

    public class RoughnessCalculationResult
    {
        public bool IsValid { get; set; }

        // Числовые значения (для передачи подачи fn в калькулятор режимов и G-код)
        public double CalculatedFeedPerRev { get; set; }        // Рекомендуемая / расчётная подача fn, мм/об
        public double TheoreticalRa { get; set; }               // Теоретическое Ra, мкм
        public double TheoreticalRz { get; set; }               // Теоретическое Rz, мкм
        public double PracticalRaMin { get; set; }              // Ожидаемое реальное Ra (с учётом вибраций/нароста), мкм
        public double PracticalRaMax { get; set; }

        // Отформатированные строки для интерфейса
        public string FeedPerRevText { get; set; } = "—";
        public string TheoreticalRaText { get; set; } = "—";
        public string TheoreticalRzText { get; set; } = "—";
        public string PracticalRaRangeText { get; set; } = "—";
        public string SurfaceQualityClassText { get; set; } = "—";
        public string SampleFeedGCode { get; set; } = "—";

        // Координаты векторного профиля гребешков для Canvas 320x200
        public string ProfilePathData { get; set; } = string.Empty;
        public double PeakLineY { get; set; } = 76.0;
        public double ValleyLineY { get; set; } = 128.0;
        public double PitchArrowLeftX { get; set; } = 120.0;
        public double PitchArrowRightX { get; set; } = 200.0;
        public string PitchLabelText { get; set; } = string.Empty;
        public string HeightLabelText { get; set; } = string.Empty;
        public string RadiusLabelText { get; set; } = string.Empty;

        // Единая система сообщений проекта
        public bool HasWarning { get; set; }
        public string WarningMessage { get; set; } = string.Empty;

        public bool HasError { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

}
