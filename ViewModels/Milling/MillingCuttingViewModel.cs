using MechCore.Core;
using MechCore.Models.Materials;
using MechCore.Models.Milling;
using MechCore.Services.Materials;
using MechCore.Services.Milling;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using MechCore.Models.Charts;

namespace MechCore.ViewModels.Milling
{
    public class MillingCuttingViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly IMaterialService _materialService;
        private readonly IMillingCuttingService _cuttingService;
        private readonly IMillingChartService _chartService;
        private readonly List<MaterialGradeItem> _allGrades;
        public List<MillingCutterGeometryOption> CutterGeometryOptions { get; }

        public MillingCuttingViewModel(MainViewModel mainViewModel)
            : this(mainViewModel, new MaterialStandardService(), new MillingCuttingService())
        {
        }

        public MillingCuttingViewModel(
            MainViewModel mainViewModel,
            IMaterialService materialService,
            IMillingCuttingService cuttingService)
        {
            _mainViewModel = mainViewModel;
            _materialService = materialService;
            _cuttingService = cuttingService;
            _chartService = new MillingChartService();

            _mainViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsOperatorMode) ||
                    e.PropertyName == nameof(MainViewModel.IsEngineerMode))
                {
                    OnPropertyChanged(nameof(IsOperatorMode));
                    OnPropertyChanged(nameof(IsEngineerMode));
                }
            };

            IsoGroups = _materialService.GetIsoGroups();
            _allGrades = _materialService.GetAllGrades();

            OperationOptions = new List<MillingOperationOption>
            {
                new(MillingOperationKind.ShoulderMilling,
                    "Фрезерование уступов и контуров",
                    "Боковое фрезерование наружных и внутренних контуров с умеренным радиальным шагом ae."),
                new(MillingOperationKind.FaceMilling,
                    "Торцевое фрезерование плоскостей",
                    "Обработка открытых плоскостей торцевой фрезой или многозубой головкой со СМП."),
                new(MillingOperationKind.SlotMilling,
                    "Прорезка пазов (на полную ширину)",
                    "Фрезерование закрытого паза с полным радиальным охватом ae = 100% D."),
                new(MillingOperationKind.PocketAndHsm,
                    "Выборка карманов / Скоростное (HSM)",
                    "Высокопроизводительное фрезерование на полную глубину ap при малом боковом шаге ae."),
                new(MillingOperationKind.Drilling,
                    "Сверление отверстий",
                    "Осевое сверление спиральным или твердосплавным сверлом на фрезерном центре."),
                new(MillingOperationKind.Chamfering,
                    "Снятие фасок и гравировка",
                    "Обработка кромок центровочной или фасочной фрезой.")
            };

            ToolMaterialOptions = new List<MillingToolMaterialOption>
            {
                new(MillingToolMaterialKind.SolidCarbide,
                    "Монолитный твёрдый сплав (VHM с покрытием)",
                    "Основной стандарт для концевых фрез по стали, нержавейке, чугуну и титану."),
                new(MillingToolMaterialKind.IndexableCarbide,
                    "Корпусная фреза со сменными пластинами (СМП)",
                    "Для торцевых фрез, кукуруз и черновых уступов большого диаметра."),
                new(MillingToolMaterialKind.UncoatedCarbide,
                    "Твёрдый сплав без покрытия (полированный)",
                    "Острая полированная кромка: идеален для алюминия и медных сплавов (ISO N)."),
                new(MillingToolMaterialKind.HssCo,
                    "Быстрорежущая сталь (HSS-Co / Р6М5К5)",
                    "Для свёрл, фасонных фрез и нежёсткого крепления на невысоких оборотах.")
            };

            SelectIsoGroupCommand = new RelayCommand(param =>
            {
                if (param is IsoMaterialGroupItem group)
                    SelectedIsoGroup = group;
            });

            SelectStageCommand = new RelayCommand(param =>
            {
                if (param is string stageStr && Enum.TryParse<MillingStageKind>(stageStr, out var stage))
                    SelectedStage = stage;
            });

            CutterGeometryOptions = new List<MillingCutterGeometryOption>
                                    {
                                        new(MillingCutterGeometryKind.FlatEndMill,
                                            "Концевая / торцевая фреза (90°)",
                                            "Обычное 2.5D-фрезерование плоскостей, уступов и пазов: эффективный диаметр Deff равен номинальному Dc."),
                                        new(MillingCutterGeometryKind.BallNose,
                                            "Сферическая фреза (Ball Nose)",
                                            "3D-обработка поверхностей: при глубине ap < Dc/2 автоматически вычисляется эффективный диаметр Deff и осевое утонение стружки."),
                                        new(MillingCutterGeometryKind.RoundInsert,
                                            "Фреза с круглыми пластинами (iC)",
                                            "Черновое/получистовое контурное фрезерование круглыми СМП: расчёт Deff и утонения стружки по диаметру пластины iC.")
                                    };

            ClearGradeCommand = new RelayCommand(_ => SelectedGrade = null);
            CopyGCodeCommand = new RelayCommand(_ => CopyGCode(), _ => CanCopyGCode);
            ApplyFromReferenceCommand = new RelayCommand(_ => ApplyFromReference(), _ => Result.IsValid);
            CopyFeedGCodeCommand = new RelayCommand(_ => CopyFeedGCode(), _ => CanCopyFeedGCode);

            _selectedIsoGroup = IsoGroups[0];
            _selectedOperation = OperationOptions[0];
            _selectedToolMaterial = ToolMaterialOptions[0];
            _selectedCutterGeometry = CutterGeometryOptions[0];

            UpdateFilteredGrades();
            Recalculate();
            RecalculateFeed();
            UpdateMillingChart();
        }

        public bool IsOperatorMode => _mainViewModel.IsOperatorMode;

        // --- Коллекции для UI ---
        public List<IsoMaterialGroupItem> IsoGroups { get; }
        public List<MillingOperationOption> OperationOptions { get; }
        public List<MillingToolMaterialOption> ToolMaterialOptions { get; }

        private List<MaterialGradeItem> _filteredGrades = new();
        public List<MaterialGradeItem> FilteredGrades
        {
            get => _filteredGrades;
            private set => SetProperty(ref _filteredGrades, value);
        }

        public ICommand SelectIsoGroupCommand { get; }
        public ICommand SelectStageCommand { get; }
        public ICommand ClearGradeCommand { get; }
        public ICommand CopyGCodeCommand { get; }
        public ICommand ApplyFromReferenceCommand { get; }
        public ICommand CopyFeedGCodeCommand { get; }

        // =====================================================================
        // БЛОК 1: СПРАВОЧНИК РЕЖИМОВ РЕЗАНИЯ (ПО ISO 513)
        // =====================================================================

        private IsoMaterialGroupItem _selectedIsoGroup;
        public IsoMaterialGroupItem SelectedIsoGroup
        {
            get => _selectedIsoGroup;
            set
            {
                if (value != null && SetProperty(ref _selectedIsoGroup, value))
                {
                    SelectedGrade = null;
                    UpdateFilteredGrades();
                    OnPropertyChanged(nameof(SelectedGradeHardnessInfo));
                    Recalculate();
                }
            }
        }

        private MaterialGradeItem? _selectedGrade;
        public MaterialGradeItem? SelectedGrade
        {
            get => _selectedGrade;
            set
            {
                if (SetProperty(ref _selectedGrade, value))
                {
                    OnPropertyChanged(nameof(SelectedGradeHardnessInfo));
                    OnPropertyChanged(nameof(HasSelectedGrade));
                    Recalculate();
                }
            }
        }

        public bool HasSelectedGrade => SelectedGrade != null;

        public string SelectedGradeHardnessInfo => SelectedGrade != null
            ? $"Марка: {SelectedGrade.GostGrade} ({SelectedGrade.DinGrade}) • Твёрдость: {SelectedGrade.TypicalHardness}"
            : $"Базовый расчёт по группе ISO {SelectedIsoGroup?.CodeLetter} (коэффициент Km = {SelectedIsoGroup?.MachinabilityFactor:0.00})";

        private MillingOperationOption _selectedOperation;
        public MillingOperationOption SelectedOperation
        {
            get => _selectedOperation;
            set
            {
                if (value != null && SetProperty(ref _selectedOperation, value))
                {
                    OnPropertyChanged(nameof(DiameterInputHint));
                    Recalculate();
                }
            }
        }

        public string DiameterInputHint => SelectedOperation.Kind == MillingOperationKind.Drilling
            ? "Диаметр сверла D (мм)"
            : "Диаметр фрезы D (мм)";

        private MillingStageKind _selectedStage = MillingStageKind.Roughing;
        public MillingStageKind SelectedStage
        {
            get => _selectedStage;
            set
            {
                if (SetProperty(ref _selectedStage, value))
                {
                    OnPropertyChanged(nameof(RoughingBg));
                    OnPropertyChanged(nameof(RoughingFg));
                    OnPropertyChanged(nameof(SemiFinishingBg));
                    OnPropertyChanged(nameof(SemiFinishingFg));
                    OnPropertyChanged(nameof(FinishingBg));
                    OnPropertyChanged(nameof(FinishingFg));
                    OnPropertyChanged(nameof(SelectedStageNote));
                    Recalculate();
                }
            }
        }

        public string RoughingBg => SelectedStage == MillingStageKind.Roughing ? "#1565C0" : "Transparent";
        public string RoughingFg => SelectedStage == MillingStageKind.Roughing ? "#FFFFFF" : "#263238";

        public string SemiFinishingBg => SelectedStage == MillingStageKind.SemiFinishing ? "#1565C0" : "Transparent";
        public string SemiFinishingFg => SelectedStage == MillingStageKind.SemiFinishing ? "#FFFFFF" : "#263238";

        public string FinishingBg => SelectedStage == MillingStageKind.Finishing ? "#1565C0" : "Transparent";
        public string FinishingFg => SelectedStage == MillingStageKind.Finishing ? "#FFFFFF" : "#263238";

        public string SelectedStageNote => SelectedStage switch
        {
        MillingStageKind.Roughing => "Черновая: максимальный съём припуска, повышенная подача на зуб fz и ширина ae.",
        MillingStageKind.SemiFinishing => "Получистовая: выравнивание припуска перед чистовым контурным проходом.",
            MillingStageKind.Finishing => "Чистовая: высокая скорость Vc, малый боковой шаг ae и умеренная fz для низкой шероховатости.",
            _ => string.Empty
        };
        private MillingToolMaterialOption _selectedToolMaterial;
        public MillingToolMaterialOption SelectedToolMaterial
        {
            get => _selectedToolMaterial;
            set
            {
                if (value != null && SetProperty(ref _selectedToolMaterial, value))
                    Recalculate();
            }
        }
        private string _cutterDiameterRaw = "12";
        public string CutterDiameterRaw
        {
            get => _cutterDiameterRaw;
            set { if (SetProperty(ref _cutterDiameterRaw, value)) Recalculate(); }
        }
        private string _fluteCountRaw = "4";
        public string FluteCountRaw
        {
            get => _fluteCountRaw;
            set { if (SetProperty(ref _fluteCountRaw, value)) Recalculate(); }
        }
        private string _overhangRatioRaw = "3";
        public string OverhangRatioRaw
        {
            get => _overhangRatioRaw;
            set { if (SetProperty(ref _overhangRatioRaw, value)) Recalculate(); }
        }
        private MillingCuttingResult _result = new();
        public MillingCuttingResult Result
        {
            get => _result;
            private set
            {
                if (SetProperty(ref _result, value))
                    OnPropertyChanged(nameof(CanCopyGCode));
            }
        }
        private bool _isCopySuccessVisible;
        public bool IsCopySuccessVisible
        {
            get => _isCopySuccessVisible;
            private set => SetProperty(ref _isCopySuccessVisible, value);
        }
        public bool CanCopyGCode => Result.IsValid && !string.IsNullOrWhiteSpace(Result.SampleGCode) && Result.SampleGCode != "—";
        private void UpdateFilteredGrades()
        {
            if (SelectedIsoGroup == null)
            {
                FilteredGrades = new List<MaterialGradeItem>();
                return;
            }

            FilteredGrades = _allGrades
                .Where(g => g.IsoGroup == SelectedIsoGroup.Code)
                .ToList();
        }

        private void Recalculate()
        {
            if (_cuttingService == null || SelectedIsoGroup == null) return;
            if (!TryParseDouble(CutterDiameterRaw, out double diameter))
            {
                Result = ErrorViewResult("Введите корректный диаметр фрезы D (мм).");
                return;
            }
            if (!int.TryParse(FluteCountRaw, out int flutes))
            {
                Result = ErrorViewResult("Введите корректное число зубьев z (целое число).");
                return;
            }
            if (!TryParseDouble(OverhangRatioRaw, out double overhangRatio))
            {
                Result = ErrorViewResult("Введите корректное значение относительного вылета L/D.");
                return;
            }
            var request = new MillingCuttingRequest
            {
                IsoGroup = SelectedIsoGroup,
                SelectedGrade = SelectedGrade,
                Operation = SelectedOperation.Kind,
                Stage = SelectedStage,
                ToolMaterial = SelectedToolMaterial.Kind,
                CutterDiameter = diameter,
                FluteCount = flutes,
                OverhangRatio = overhangRatio
            };
            Result = _cuttingService.Calculate(request);
            IsCopySuccessVisible = false;
            OnPropertyChanged(nameof(FeedMaterialContextNote));
            RecalculateFeed();
            UpdateMillingChart();
        }
        private static bool TryParseDouble(string raw, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string norm = raw.Trim().Replace(',', '.');
            return double.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }
        private static MillingCuttingResult ErrorViewResult(string msg) =>
        new() { IsValid = false, HasError = true, ErrorMessage = msg };
        private void CopyGCode()
        {
            if (!CanCopyGCode) return;
            Clipboard.SetText(Result.SampleGCode);
            IsCopySuccessVisible = true;
        }
        // =====================================================================
        // БЛОК 2: КАЛЬКУЛЯТОР ПОДАЧИ Vf И УТОНЕНИЯ СТРУЖКИ (hm)
        // =====================================================================
        private MillingCutterGeometryOption _selectedCutterGeometry;
        public MillingCutterGeometryOption SelectedCutterGeometry
        {
            get => _selectedCutterGeometry;
            set
            {
                if (value != null && SetProperty(ref _selectedCutterGeometry, value))
                {
                    OnPropertyChanged(nameof(IsRoundInsertGeometry));

                    if (value.Kind == MillingCutterGeometryKind.RoundInsert)
                    {
                        double ic = TryParseDouble(CalcInsertDiameterIcRaw, out double parsedIc) && parsedIc >= 3.0
                            ? parsedIc
                            : 10.0;

                        // Если текущая глубина ap больше допустимого предела iC / 2 (или не распознана),
                        // автоматически ставим безопасную рабочую глубину 0.2 * iC (например, 2 мм для пластины 10 мм)
                        if (!TryParseDouble(CalcApRaw, out double currentAp) || currentAp <= 0 || currentAp > ic * 0.5)
                        {
                            double safeAp = Math.Round(Math.Max(0.5, ic * 0.2), 2);
                            CalcApRaw = safeAp.ToString("0.##", CultureInfo.InvariantCulture);
                            return; // Сеттер CalcApRaw сам вызовет RecalculateFeed()
                        }
                    }

                    RecalculateFeed();
                }
            }
        }

        public bool IsRoundInsertGeometry => SelectedCutterGeometry?.Kind == MillingCutterGeometryKind.RoundInsert;

        private string _calcInsertDiameterIcRaw = "10";
        public string CalcInsertDiameterIcRaw
        {
            get => _calcInsertDiameterIcRaw;
            set { if (SetProperty(ref _calcInsertDiameterIcRaw, value)) RecalculateFeed(); }
        }

        private bool _feedCalcByCatalog = true; // true = Vc и fz -> S и F; false = S и F -> Vc и fz
        public bool FeedCalcByCatalog
        {
            get => _feedCalcByCatalog;
            set
            {
                if (SetProperty(ref _feedCalcByCatalog, value))
                {
                    OnPropertyChanged(nameof(FeedCalcByMachine));
                    OnPropertyChanged(nameof(FeedModeDescription));
                    RecalculateFeed();
                }
            }
        }
        public bool FeedCalcByMachine
        {
            get => !_feedCalcByCatalog;
            set => FeedCalcByCatalog = !value;
        }
        public string FeedModeDescription => FeedCalcByCatalog
        ? "Прямой расчёт: введите каталожную скорость Vc и подачу на зуб fz — получите кадры S и F с поправкой на утонение стружки."
        : "Обратный расчёт: введите обороты S и минутную подачу F со стойки — проверьте реальную нагрузку на зуб fz и толщину стружки hm.";
        public string FeedMaterialContextNote
        {
            get
            {
                if (SelectedIsoGroup == null) return string.Empty;
                string matTitle = SelectedGrade != null
                ? $"{SelectedGrade.GostGrade} (ISO {SelectedIsoGroup.CodeLetter})"
                : $"ISO {SelectedIsoGroup.CodeLetter} — {SelectedIsoGroup.Name}";
                return Result.IsValid
                ? $"Активный материал справочника: {matTitle} • Рекомендуемая Vc: {Result.VcRangeText}"
                : $"Активный материал справочника: {matTitle}";
            }
        }
        private string _calcDiameterRaw = "12";
        public string CalcDiameterRaw
        {
            get => _calcDiameterRaw;
            set { if (SetProperty(ref _calcDiameterRaw, value)) RecalculateFeed(); }
        }
        private string _calcFlutesRaw = "4";
        public string CalcFlutesRaw
        {
            get => _calcFlutesRaw;
            set { if (SetProperty(ref _calcFlutesRaw, value)) RecalculateFeed(); }
        }
        private string _calcVcRaw = "160";
        public string CalcVcRaw
        {
            get => _calcVcRaw;
            set { if (SetProperty(ref _calcVcRaw, value)) RecalculateFeed(); }
        }
        private string _calcFzRaw = "0.06";
        public string CalcFzRaw
        {
            get => _calcFzRaw;
            set { if (SetProperty(ref _calcFzRaw, value)) RecalculateFeed(); }
        }
        private string _calcRpmRaw = "4244";
        public string CalcRpmRaw
        {
            get => _calcRpmRaw;
            set { if (SetProperty(ref _calcRpmRaw, value)) RecalculateFeed(); }
        }
        private string _calcVfRaw = "1019";
        public string CalcVfRaw
        {
            get => _calcVfRaw;
            set { if (SetProperty(ref _calcVfRaw, value)) RecalculateFeed(); }
        }
        private string _calcApRaw = "6";
        public string CalcApRaw
        {
            get => _calcApRaw;
            set { if (SetProperty(ref _calcApRaw, value)) RecalculateFeed(); }
        }

        private string _calcMachinePowerRaw = "11";
        public string CalcMachinePowerRaw
        {
            get => _calcMachinePowerRaw;
            set { if (SetProperty(ref _calcMachinePowerRaw, value)) RecalculateFeed(); }
        }

        private string _calcMachineEfficiencyRaw = "0.85";
        public string CalcMachineEfficiencyRaw
        {
            get => _calcMachineEfficiencyRaw;
            set { if (SetProperty(ref _calcMachineEfficiencyRaw, value)) RecalculateFeed(); }
        }


        private string _calcAeRaw = "3";
        public string CalcAeRaw
        {
            get => _calcAeRaw;
            set { if (SetProperty(ref _calcAeRaw, value)) RecalculateFeed(); }
        }
        private bool _compensateChipThinning = true;
        public bool CompensateChipThinning
        {
            get => _compensateChipThinning;
            set { if (SetProperty(ref _compensateChipThinning, value)) RecalculateFeed(); }
        }
        private MillingFeedCalcResult _feedResult = new();
        public MillingFeedCalcResult FeedResult
        {
            get => _feedResult;
            private set
            {
                if (SetProperty(ref _feedResult, value))
                    OnPropertyChanged(nameof(CanCopyFeedGCode));
            }
        }
        private bool _isFeedCopySuccessVisible;
        public bool IsFeedCopySuccessVisible
        {
            get => _isFeedCopySuccessVisible;
            private set => SetProperty(ref _isFeedCopySuccessVisible, value);
        }
        public bool CanCopyFeedGCode =>
        FeedResult.IsValid && !string.IsNullOrWhiteSpace(FeedResult.SampleGCode) && FeedResult.SampleGCode != "—";
        private void ApplyFromReference()
        {
            if (!Result.IsValid) return;
            var inv = CultureInfo.InvariantCulture;
            CalcDiameterRaw = CutterDiameterRaw;
            CalcFlutesRaw = FluteCountRaw;
            CalcVcRaw = Result.RecommendedVc.ToString("0", inv);
            CalcFzRaw = Result.RecommendedFeedPerTooth.ToString("0.###", inv);
            CalcRpmRaw = Result.RecommendedRpm.ToString(inv);
            CalcVfRaw = Result.RecommendedFeedPerMin.ToString("0", inv);
            CalcApRaw = Result.RecommendedAp.ToString("0.##", inv);
            CalcAeRaw = Result.RecommendedAe.ToString("0.##", inv);
            RecalculateFeed();
        }
        private void RecalculateFeed()
        {
            if (!TryParseDouble(CalcMachinePowerRaw, out double machinePowerKw) || machinePowerKw <= 0)
            {
                FeedResult = ErrorFeedViewResult("Введите корректную паспортную мощность шпинделя станка (> 0 кВт).");
                return;
            }

            if (!TryParseDouble(CalcMachineEfficiencyRaw, out double machineEfficiency) || machineEfficiency < 0.5 || machineEfficiency > 1.0)
            {
                FeedResult = ErrorFeedViewResult("Введите корректный КПД привода шпинделя η (от 0,50 до 1,00).");
                return;
            }

            double insertIc = 10.0;
            if (IsRoundInsertGeometry && (!TryParseDouble(CalcInsertDiameterIcRaw, out insertIc) || insertIc <= 0))
            {
                FeedResult = ErrorFeedViewResult("Введите корректный диаметр круглой пластины iC (> 0 мм).");
                return;
            }

            if (_cuttingService == null) return;
            if (!TryParseDouble(CalcDiameterRaw, out double d) || d <= 0)
            {
                FeedResult = ErrorFeedViewResult("Введите корректный диаметр фрезы D (> 0 мм).");
                return;
            }
            if (!int.TryParse(CalcFlutesRaw, out int z) || z <= 0)
            {
                FeedResult = ErrorFeedViewResult("Введите корректное число зубьев z (> 0).");
                return;
            }
            if (!TryParseDouble(CalcApRaw, out double ap) || ap <= 0)
            {
                FeedResult = ErrorFeedViewResult("Введите корректную осевую глубину ap (> 0 мм).");
                return;
            }
            if (!TryParseDouble(CalcAeRaw, out double ae) || ae <= 0)
            {
                FeedResult = ErrorFeedViewResult("Введите корректную радиальную ширину фрезерования ae (> 0 мм).");
                return;
            }
            double vc = 0;
            double fz = 0;
            int rpm = 0;
            double vf = 0;
            if (FeedCalcByCatalog)
            {
                if (!TryParseDouble(CalcVcRaw, out vc) || vc <= 0)
                {
                    FeedResult = ErrorFeedViewResult("Введите корректную скорость резания Vc (> 0 м/мин).");
                    return;
                }
                if (!TryParseDouble(CalcFzRaw, out fz) || fz <= 0)
                {
                    FeedResult = ErrorFeedViewResult("Введите корректную подачу на зуб fz (> 0 мм/зуб).");
                    return;
                }
            }
            else
            {
                if (!int.TryParse(CalcRpmRaw, out rpm) || rpm <= 0)
                {
                    FeedResult = ErrorFeedViewResult("Введите корректные обороты шпинделя S (> 0 об/мин).");
                    return;
                }
                if (!TryParseDouble(CalcVfRaw, out vf) || vf <= 0)
                {
                    FeedResult = ErrorFeedViewResult("Введите корректную минутную подачу F (> 0 мм/мин).");
                    return;
                }
            }
            double kc = SelectedIsoGroup?.Code switch
            {
                IsoGroupCode.P => 2100.0,
                IsoGroupCode.M => 2450.0,
                IsoGroupCode.K => 1350.0,
                IsoGroupCode.N => 750.0,
                IsoGroupCode.S => 2800.0,
                IsoGroupCode.H => 3500.0,
                _ => 2100.0
            };
            var request = new MillingFeedCalcRequest
            {
                Mode = FeedCalcByCatalog ? MillingFeedCalcMode.ByCatalogVcAndFz : MillingFeedCalcMode.ByMachineRpmAndVf,
                CutterDiameter = d,
                FluteCount = z,
                InputVc = vc,
                InputFz = fz,
                InputRpm = rpm,
                InputVf = vf,
                AxialDepthAp = ap,
                RadialWidthAe = ae,
                CompensateChipThinning = CompensateChipThinning,
                MaterialContextLabel = SelectedGrade != null
            ? $"{SelectedGrade.GostGrade} (ISO {SelectedIsoGroup.CodeLetter})"
            : $"группы ISO {SelectedIsoGroup.CodeLetter}",
                ReferenceVcMin = Result.IsValid ? Result.VcMin : null,
                ReferenceVcMax = Result.IsValid ? Result.VcMax : null,
                SpecificCuttingForceKc = kc,
                CutterGeometry = SelectedCutterGeometry.Kind,
                InsertDiameterIc = insertIc,
                MachineSpindlePowerKw = machinePowerKw,
                MachineEfficiency = machineEfficiency,
            };
            
            FeedResult = _cuttingService.CalculateFeed(request);
            IsFeedCopySuccessVisible = false;
            UpdateMillingChart();

        }
        private static MillingFeedCalcResult ErrorFeedViewResult(string msg) =>
        new() { IsValid = false, HasError = true, ErrorMessage = msg };
        private void CopyFeedGCode()
        {
            if (!CanCopyFeedGCode) return;
            Clipboard.SetText(FeedResult.SampleGCode);
            IsFeedCopySuccessVisible = true;
        }

        // =====================================================================
        // БЛОК 3: ИНЖЕНЕРНЫЕ ГРАФИКИ И АНАЛИЗ РЕЖИМОВ ФРЕЗЕРОВАНИЯ
        // =====================================================================

        public bool IsEngineerMode => _mainViewModel.IsEngineerMode;

        private MillingChartMetric _selectedChartMetric = MillingChartMetric.ChipThinningCurve;
        public MillingChartMetric SelectedChartMetric
        {
            get => _selectedChartMetric;
            set
            {
                if (SetProperty(ref _selectedChartMetric, value))
                {
                    OnPropertyChanged(nameof(IsChipThinningChart));
                    OnPropertyChanged(nameof(IsSpindlePowerChart));
                    OnPropertyChanged(nameof(IsEffectiveDiameterChart));
                    OnPropertyChanged(nameof(IsToolLifeChart));
                    UpdateMillingChart();
                }
            }
        }

        public bool IsChipThinningChart
        {
            get => SelectedChartMetric == MillingChartMetric.ChipThinningCurve;
            set { if (value) SelectedChartMetric = MillingChartMetric.ChipThinningCurve; }
        }

        public bool IsSpindlePowerChart
        {
            get => SelectedChartMetric == MillingChartMetric.SpindlePowerLoad;
            set { if (value) SelectedChartMetric = MillingChartMetric.SpindlePowerLoad; }
        }

        public bool IsEffectiveDiameterChart
        {
            get => SelectedChartMetric == MillingChartMetric.EffectiveDiameterAp;
            set { if (value) SelectedChartMetric = MillingChartMetric.EffectiveDiameterAp; }
        }

        public bool IsToolLifeChart
        {
            get => SelectedChartMetric == MillingChartMetric.CutterToolLifeTaylor;
            set { if (value) SelectedChartMetric = MillingChartMetric.CutterToolLifeTaylor; }
        }

        private EngineeringChartModel? _millingChartModel;
        public EngineeringChartModel? MillingChartModel
        {
            get => _millingChartModel;
            private set => SetProperty(ref _millingChartModel, value);
        }

        private string _chartValueBadge = string.Empty;
        public string ChartValueBadge
        {
            get => _chartValueBadge;
            private set => SetProperty(ref _chartValueBadge, value);
        }

        private string _chartHeadline = string.Empty;
        public string ChartHeadline
        {
            get => _chartHeadline;
            private set => SetProperty(ref _chartHeadline, value);
        }

        private string _chartAdvice = string.Empty;
        public string ChartAdvice
        {
            get => _chartAdvice;
            private set => SetProperty(ref _chartAdvice, value);
        }

        private bool _isChartWarning;
        public bool IsChartWarning
        {
            get => _isChartWarning;
            private set => SetProperty(ref _isChartWarning, value);
        }

        private void UpdateMillingChart()
        {
            if (_chartService == null || SelectedIsoGroup == null) return;

            TryParseDouble(CalcDiameterRaw, out double d);
            TryParseDouble(CalcInsertDiameterIcRaw, out double ic);
            int.TryParse(CalcFlutesRaw, out int z);
            TryParseDouble(CalcApRaw, out double ap);
            TryParseDouble(CalcAeRaw, out double ae);
            TryParseDouble(CalcVcRaw, out double vcInput);
            TryParseDouble(CalcFzRaw, out double fzInput);
            TryParseDouble(CalcMachinePowerRaw, out double pMachine);
            TryParseDouble(CalcMachineEfficiencyRaw, out double eta);

            double kc = SelectedIsoGroup.Code switch
            {
                IsoGroupCode.P => 2100.0,
                IsoGroupCode.M => 2450.0,
                IsoGroupCode.K => 1350.0,
                IsoGroupCode.N => 750.0,
                IsoGroupCode.S => 2800.0,
                IsoGroupCode.H => 3500.0,
                _ => 2100.0
            };

            var context = new MillingChartContext
            {
                IsoGroup = SelectedIsoGroup.Code,
                MaterialLabel = SelectedGrade != null
                    ? $"{SelectedGrade.GostGrade} — ISO {SelectedIsoGroup.CodeLetter} ({SelectedIsoGroup.Name})"
                    : $"ISO {SelectedIsoGroup.CodeLetter} — {SelectedIsoGroup.Name}",
                ToolMaterial = SelectedToolMaterial.Kind,
                CutterGeometry = SelectedCutterGeometry.Kind,

                CutterDiameter = d > 0 ? d : 12.0,
                InsertDiameterIc = ic > 0 ? ic : 10.0,
                FluteCount = z > 0 ? z : 4,

                AxialDepthAp = ap > 0 ? ap : 6.0,
                RadialWidthAe = ae > 0 ? ae : 3.0,
                ActiveVc = FeedResult.IsValid && FeedResult.CalculatedVc > 0
                    ? FeedResult.CalculatedVc
                    : (vcInput > 0 ? vcInput : 160.0),
                ActiveVf = FeedResult.IsValid && FeedResult.CalculatedFeedPerMin > 0
                    ? FeedResult.CalculatedFeedPerMin
                    : 1000.0,
                BaseFz = fzInput > 0 ? fzInput : 0.06,
                AdjustedFz = FeedResult.IsValid && FeedResult.AdjustedFeedPerTooth > 0
                    ? FeedResult.AdjustedFeedPerTooth
                    : (fzInput > 0 ? fzInput : 0.06),

                SpecificCuttingForceKc = kc,
                MachineSpindlePowerKw = pMachine > 0 ? pMachine : 11.0,
                MachineEfficiency = eta >= 0.5 && eta <= 1.0 ? eta : 0.85
            };

            var summary = _chartService.BuildChart(SelectedChartMetric, context);

            MillingChartModel = summary.ChartModel;
            ChartValueBadge = summary.ValueBadge;
            ChartHeadline = summary.Headline;
            ChartAdvice = summary.Advice;
            IsChartWarning = summary.IsWarning;
        }

    }
}