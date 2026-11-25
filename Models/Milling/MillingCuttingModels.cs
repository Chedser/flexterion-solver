using MechCore.Models.Materials;

namespace MechCore.Models.Milling
{
    public enum MillingOperationKind
    {
        FaceMilling,        // Торцевое фрезерование плоскостей
        ShoulderMilling,    // Фрезерование уступов и контуров (боковое)
        SlotMilling,        // Прорезка пазов (полная ширина ae = D)
        PocketAndHsm,       // Выборка карманов / скоростное фрезерование (малое ae)
        Drilling,           // Сверление спиральным / корпусным сверлом
        Chamfering          // Снятие фасок / гравировка
    }

    public enum MillingStageKind
    {
        Roughing,           // Черновая обработка
        SemiFinishing,      // Получистовая обработка
        Finishing           // Чистовая обработка
    }

    public enum MillingToolMaterialKind
    {
        SolidCarbide,       // Монолитный твёрдый сплав (VHM) с покрытием (TiAlN / AlTiN)
        IndexableCarbide,   // Корпусная фреза со сменными пластинами (СМП)
        UncoatedCarbide,    // Твёрдый сплав без покрытия (полированный — для алюминия)
        HssCo               // Быстрорежущая сталь (HSS-Co / Р6М5К5)
    }

    public enum MillingCutterGeometryKind
    {
        FlatEndMill,        // Концевая / торцевая фреза (90° или малый радиус при вершине)
        BallNose,           // Сферическая фреза (Ball Nose, R = Dc / 2)
        RoundInsert         // Фреза с круглыми пластинами (диаметр пластины iC)
    }

    public class MillingOperationOption
    {
        public MillingOperationKind Kind { get; }
        public string Title { get; }
        public string ShortNote { get; }

        public MillingOperationOption(MillingOperationKind kind, string title, string shortNote)
        {
            Kind = kind;
            Title = title;
            ShortNote = shortNote;
        }
    }

    public class MillingToolMaterialOption
    {
        public MillingToolMaterialKind Kind { get; }
        public string Title { get; }
        public string ShortNote { get; }

        public MillingToolMaterialOption(MillingToolMaterialKind kind, string title, string shortNote)
        {
            Kind = kind;
            Title = title;
            ShortNote = shortNote;
        }
    }

    public class MillingCutterGeometryOption
    {
        public MillingCutterGeometryKind Kind { get; }
        public string Title { get; }
        public string ShortNote { get; }

        public MillingCutterGeometryOption(MillingCutterGeometryKind kind, string title, string shortNote)
        {
            Kind = kind;
            Title = title;
            ShortNote = shortNote;
        }
    }

    // =========================================================================
    // КАРТОЧКА 1: СПРАВОЧНИК РЕЖИМОВ ФРЕЗЕРОВАНИЯ (ПО ISO 513)
    // =========================================================================

    public class MillingCuttingRequest
    {
        public IsoMaterialGroupItem IsoGroup { get; set; } = null!;
        public MaterialGradeItem? SelectedGrade { get; set; }

        public MillingOperationKind Operation { get; set; } = MillingOperationKind.ShoulderMilling;
        public MillingStageKind Stage { get; set; } = MillingStageKind.Roughing;
        public MillingToolMaterialKind ToolMaterial { get; set; } = MillingToolMaterialKind.SolidCarbide;

        public double CutterDiameter { get; set; } = 12.0;  // Диаметр фрезы или сверла D, мм
        public int FluteCount { get; set; } = 4;            // Число зубьев z
        public double OverhangRatio { get; set; } = 3.0;    // Относительный вылет L/D
    }

    public class MillingCuttingResult
    {
        public bool IsValid { get; set; }

        // Числовые значения (для передачи в калькулятор подачи)
        public double VcMin { get; set; }
        public double VcMax { get; set; }
        public double RecommendedVc { get; set; }           // Скорость резания Vc, м/мин

        public int RecommendedRpm { get; set; }             // Обороты шпинделя n, об/мин

        public double FeedPerToothMin { get; set; }
        public double FeedPerToothMax { get; set; }
        public double RecommendedFeedPerTooth { get; set; } // Подача на зуб fz, мм/зуб
        public double RecommendedFeedPerRev { get; set; }   // Подача на оборот fn = fz * z, мм/об
        public double RecommendedFeedPerMin { get; set; }   // Минутная подача Vf = fn * n, мм/мин

        public double RecommendedAp { get; set; }           // Осевая глубина резания ap, мм
        public double RecommendedAe { get; set; }           // Радиальная ширина фрезерования ae, мм

        // Отформатированные строки для плиток UI
        public string VcRangeText { get; set; } = "—";
        public string RecommendedVcText { get; set; } = "—";

        public string SpindleRpmText { get; set; } = "—";
        public string FeedPerRevText { get; set; } = "—";

        public string FeedPerToothRangeText { get; set; } = "—";
        public string RecommendedFeedPerToothText { get; set; } = "—";
        public string FeedPerMinText { get; set; } = "—";

        public string DepthApAeText { get; set; } = "—";
        public string DepthShareNoteText { get; set; } = "—";

        // Рекомендации для режима «Оператор»
        public string StrategyAdvice { get; set; } = "—";
        public string CoolantAdvice { get; set; } = "—";

        // Чистый фрагмент G-кода
        public string SampleGCode { get; set; } = "—";

        public bool HasWarning { get; set; }
        public string WarningMessage { get; set; } = string.Empty;

        public bool HasError { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    // =========================================================================
    // КАРТОЧКА 2: КАЛЬКУЛЯТОР ПОДАЧИ Vf, УТОНЕНИЯ СТРУЖКИ (hm) И Deff
    // =========================================================================

    public enum MillingFeedCalcMode
    {
        ByCatalogVcAndFz,   // Заданы Vc и fz (из каталога) -> расчёт оборотов n и минутной подачи Vf
        ByMachineRpmAndVf   // Заданы обороты S и подача F со стойки -> проверка фактических Vc и fz
    }

    public class MillingFeedCalcRequest
    {
        public MillingFeedCalcMode Mode { get; set; } = MillingFeedCalcMode.ByCatalogVcAndFz;
        public MillingCutterGeometryKind CutterGeometry { get; set; } = MillingCutterGeometryKind.FlatEndMill;

        public double CutterDiameter { get; set; } = 12.0;  // Номинальный диаметр фрезы Dc, мм
        public double InsertDiameterIc { get; set; } = 10.0;// Диаметр круглой пластины iC, мм (для RoundInsert)
        public int FluteCount { get; set; } = 4;            // Число зубьев z

        public double InputVc { get; set; } = 160.0;        // Скорость резания Vc, м/мин
        public double InputFz { get; set; } = 0.06;         // Подача на зуб fz, мм/зуб

        public int InputRpm { get; set; } = 4200;           // Обороты шпинделя n, об/мин
        public double InputVf { get; set; } = 1000.0;       // Минутная подача Vf, мм/мин

        public double AxialDepthAp { get; set; } = 6.0;     // Осевая глубина резания ap, мм
        public double RadialWidthAe { get; set; } = 3.0;    // Радиальная ширина фрезерования ae, мм
        public bool CompensateChipThinning { get; set; } = true; // Учитывать утонение стружки (ae и Deff)

        // Необязательный контекст из верхнего справочника ISO 513
        public string? MaterialContextLabel { get; set; }
        public double? ReferenceVcMin { get; set; }
        public double? ReferenceVcMax { get; set; }
        public double SpecificCuttingForceKc { get; set; } = 2100.0; // Удельная сила резания kc1.1 (Н/мм²)
        public double MachineSpindlePowerKw { get; set; } = 11.0; // Паспортная мощность шпинделя станка Pст, кВт
        public double MachineEfficiency { get; set; } = 0.85;     // КПД привода шпинделя η (0.70 – 0.98)

    }

    public class MillingFeedCalcResult
    {
        public bool IsValid { get; set; }

        public int CalculatedRpm { get; set; }
        public double CalculatedVc { get; set; }
        public double EffectiveDiameterDeff { get; set; }   // Эффективный диаметр резания Deff, мм
        public double BaseFeedPerTooth { get; set; }        // Базовая fz без поправки, мм/зуб
        public double AdjustedFeedPerTooth { get; set; }    // Скорректированная fz (с учётом утонения), мм/зуб
        public double AverageChipThicknessHm { get; set; }  // Средняя толщина стружки hm, мм
        public double ChipThinningFactor { get; set; } = 1.0; // Суммарный коэффициент повышения подачи k_hm
        public double CalculatedFeedPerMin { get; set; }    // Итоговая минутная подача Vf, мм/мин

        public double MetalRemovalRateQ { get; set; }       // Объёмный съём металла Q, см³/мин
        public double EstimatedPowerKw { get; set; }        // Ориентировочная мощность резания Pc, кВт

        public double SpindleTorqueNm { get; set; }         // Крутящий момент на шпинделе Mкр, Н·м
        public double SpindleLoadPercent { get; set; }      // Загрузка шпинделя от паспортной мощности, %
        public bool IsSpindleOverloaded { get; set; }       // Превышение паспортной мощности (> 100%)

        // Отформатированные строки для UI
        public string SpindleRpmText { get; set; } = "—";
        public string CuttingSpeedVcText { get; set; } = "—";
        public string EffectiveDiameterText { get; set; } = "—";
        public string FeedPerMinText { get; set; } = "—";
        public string BaseFeedNoteText { get; set; } = "—";
        public string FeedPerToothText { get; set; } = "—";
        public string ChipThicknessText { get; set; } = "—";
        public string RemovalAndPowerText { get; set; } = "—";
        public string SpindleLoadAndTorqueText { get; set; } = "—";
        public string EngagementPercentText { get; set; } = "—";
        public string SampleGCode { get; set; } = "—";

        public bool HasWarning { get; set; }
        public string WarningMessage { get; set; } = string.Empty;

        public bool HasError { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
