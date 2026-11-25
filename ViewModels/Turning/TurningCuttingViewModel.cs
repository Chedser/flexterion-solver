using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MechCore.Core;
using MechCore.Models.Materials;
using MechCore.Models.Turning;
using MechCore.Services.Materials;
using MechCore.Services.Turning;
using MechCore.Models.Charts;

namespace MechCore.ViewModels.Turning
{
    public class TurningCuttingViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly IMaterialService _materialService;
        private readonly ITurningCuttingService _cuttingService;
        private readonly List<MaterialGradeItem> _allGrades;

        private readonly ITurningChartService _chartService;

        public TurningCuttingViewModel(MainViewModel mainViewModel)
            : this(mainViewModel, new MaterialStandardService(), new TurningCuttingService())
        {
        }

        public TurningCuttingViewModel(
            MainViewModel mainViewModel,
            IMaterialService materialService,
            ITurningCuttingService cuttingService)
        {
            _mainViewModel = mainViewModel;
            _materialService = materialService;
            _cuttingService = cuttingService;

            _mainViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsOperatorMode) ||
                    e.PropertyName == nameof(MainViewModel.IsEngineerMode))
                {
                    OnPropertyChanged(nameof(IsEngineerMode));
                    OnPropertyChanged(nameof(IsOperatorMode));
                }
            };

            IsoGroups = _materialService.GetIsoGroups();
            _allGrades = _materialService.GetAllGrades();

            OperationOptions = new List<TurningOperationOption>
            {
                new(TurningOperationKind.ExternalTurning,
                    "Наружное точение и торцевание",
                    "Продольная обточка наружных поверхностей и подрезка торцов."),
                new(TurningOperationKind.InternalBoring,
                    "Внутреннее растачивание",
                    "Обработка внутренних цилиндрических и ступенчатых отверстий расточным резцом."),
                new(TurningOperationKind.PartingAndGrooving,
                    "Отрезка и прорезка канавок",
                    "Радиальное врезание отрезным или канавочным резцом (в т.ч. для цикла G75)."),
                new(TurningOperationKind.Drilling,
                    "Сверление спиральным сверлом",
                    "Осевое сверление отверстий по центру вращения заготовки (режим G97)."),
                new(TurningOperationKind.Reaming,
                    "Зенкерование и развёртывание",
                    "Получистовая и чистовая калибровка отверстий мерным осевым инструментом."),
                new(TurningOperationKind.ThreadTurning,
                    "Нарезание резьбы резцом",
                    "Многопроходное нарезание наружной или внутренней резьбы (цикл G76 / G92).")
            };

            ToolMaterialOptions = new List<ToolMaterialOption>
            {
                new(ToolMaterialKind.CoatedCarbide,
                    "Твёрдый сплав с покрытием (CVD / PVD)",
                    "Основной промышленный стандарт для точения сталей, нержавейки и чугуна."),
                new(ToolMaterialKind.UncoatedCarbide,
                    "Твёрдый сплав без покрытия",
                    "Острая режущая кромка: отлично подходит для алюминия, цветных сплавов и титана."),
                new(ToolMaterialKind.Cermet,
                    "Кермет (металлокерамика)",
                    "Высокая износостойкость и качество поверхности на чистовых проходах без ударов."),
                new(ToolMaterialKind.CbnOrCeramic,
                    "CBN (эльбор) / Керамика",
                    "Для высокоскоростной обработки закалённых сталей (45–65 HRC) и серых чугунов."),
                new(ToolMaterialKind.Hss,
                    "Быстрорежущая сталь (HSS / Р6М5)",
                    "Для свёрл, развёрток, фасонных резцов и нежёстких станков на низких оборотах.")
            };

            SelectIsoGroupCommand = new RelayCommand(param =>
            {
                if (param is IsoMaterialGroupItem group)
                    SelectedIsoGroup = group;
            });

            SelectStageCommand = new RelayCommand(param =>
            {
                if (param is string stageStr && Enum.TryParse<CuttingStageKind>(stageStr, out var stage))
                    SelectedStage = stage;
            });

            SelectNoseRadiusCommand = new RelayCommand(param =>
            {
                if (param is string radStr)
                    RoughNoseRadiusRaw = radStr;
            });

            SelectTargetRoughnessCommand = new RelayCommand(param =>
            {
                if (param is string valStr)
                {
                    RoughByTargetMode = true;
                    RoughIsRzParameter = false;
                    RoughTargetValueRaw = valStr;
                }
            });

            ApplyFeedToSimulatorCommand = new RelayCommand(_ => ApplyFeedToSimulator(), _ => RoughResult.IsValid);
            PullFeedFromReferenceCommand = new RelayCommand(_ => PullFeedFromReference(), _ => Result.IsValid);


            ApplyFromReferenceCommand = new RelayCommand(_ => ApplyFromReference(), _ => Result.IsValid);
            CopySimGCodeCommand = new RelayCommand(_ => CopySimGCode(), _ => CanCopySimGCode);
            ClearGradeCommand = new RelayCommand(_ => SelectedGrade = null);
            CopyGCodeCommand = new RelayCommand(_ => CopyGCode(), _ => CanCopyGCode);

            _selectedIsoGroup = IsoGroups.First();
            _selectedOperation = OperationOptions[0];
            _selectedToolMaterial = ToolMaterialOptions[0];

            _chartService = new TurningChartService();
            // И в самом конце конструктора (после RecalculateRoughness()):
            UpdateTurningChart();

            UpdateFilteredGrades();
            Recalculate();
            RecalculateSimulation();
            RecalculateRoughness();

        }

        public bool IsOperatorMode => _mainViewModel.IsOperatorMode;

        // --- Коллекции для UI ---
        public List<IsoMaterialGroupItem> IsoGroups { get; }
        public List<TurningOperationOption> OperationOptions { get; }
        public List<ToolMaterialOption> ToolMaterialOptions { get; }

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

        // --- Выбор материала ---
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
        // --- Выбор операции, этапа обработки и материала инструмента ---
        private TurningOperationOption _selectedOperation;
        public TurningOperationOption SelectedOperation
        {
            get => _selectedOperation;
            set
            {
                if (value != null && SetProperty(ref _selectedOperation, value))
                {
                    OnPropertyChanged(nameof(IsGroovingOperation));
                    OnPropertyChanged(nameof(DiameterInputHint));
                    Recalculate();
                }
            }
        }

        public bool IsGroovingOperation => SelectedOperation.Kind == TurningOperationKind.PartingAndGrooving;

        public string DiameterInputHint => SelectedOperation.Kind switch
        {
            TurningOperationKind.Drilling => "Диаметр сверла D (мм)",
            TurningOperationKind.Reaming => "Диаметр отверстия D (мм)",
            TurningOperationKind.InternalBoring => "Диаметр растачивания D (мм)",
            _ => "Диаметр обработки D (мм)"
        };

        private CuttingStageKind _selectedStage = CuttingStageKind.Roughing;
        public CuttingStageKind SelectedStage
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

        // Подсветка активной кнопки типа обработки (в стиле GeometryView)
        public string RoughingBg => SelectedStage == CuttingStageKind.Roughing ? "#1565C0" : "Transparent";
        public string RoughingFg => SelectedStage == CuttingStageKind.Roughing ? "#FFFFFF" : "#263238";

        public string SemiFinishingBg => SelectedStage == CuttingStageKind.SemiFinishing ? "#1565C0" : "Transparent";
        public string SemiFinishingFg => SelectedStage == CuttingStageKind.SemiFinishing ? "#FFFFFF" : "#263238";

        public string FinishingBg => SelectedStage == CuttingStageKind.Finishing ? "#1565C0" : "Transparent";
        public string FinishingFg => SelectedStage == CuttingStageKind.Finishing ? "#FFFFFF" : "#263238";

        public string SelectedStageNote => SelectedStage switch
        {
            CuttingStageKind.Roughing => "Черновая: максимальный съём припуска, повышенная подача и глубина резания ap.",
            CuttingStageKind.SemiFinishing => "Получистовая: подготовка равномерного припуска перед чистовым проходом.",
            CuttingStageKind.Finishing => "Чистовая: высокая скорость Vc, малая глубина и подача для получения требуемой шероховатости Ra.",
            _ => string.Empty
        };

        private ToolMaterialOption _selectedToolMaterial;
        public ToolMaterialOption SelectedToolMaterial
        {
            get => _selectedToolMaterial;
            set
            {
                if (value != null && SetProperty(ref _selectedToolMaterial, value))
                    Recalculate();
            }
        }

        // --- Геометрические параметры ---
        private string _diameterRaw = "50";
        public string DiameterRaw
        {
            get => _diameterRaw;
            set { if (SetProperty(ref _diameterRaw, value)) Recalculate(); }
        }

        private string _overhangRatioRaw = "3";
        public string OverhangRatioRaw
        {
            get => _overhangRatioRaw;
            set { if (SetProperty(ref _overhangRatioRaw, value)) Recalculate(); }
        }

        private string _grooveWidthRaw = "3";
        public string GrooveWidthRaw
        {
            get => _grooveWidthRaw;
            set { if (SetProperty(ref _grooveWidthRaw, value)) Recalculate(); }
        }

        // --- Результат расчёта ---
        private TurningCuttingResult _result = new();
        public TurningCuttingResult Result
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

            if (!TryParseDouble(DiameterRaw, out double diameter))
            {
                Result = ErrorViewResult("Введите корректное числовое значение диаметра обработки D (мм).");
                return;
            }

            if (!TryParseDouble(OverhangRatioRaw, out double overhangRatio))
            {
                Result = ErrorViewResult("Введите корректное значение относительного вылета L/D.");
                return;
            }

            double grooveWidth = 3.0;
            if (IsGroovingOperation && !TryParseDouble(GrooveWidthRaw, out grooveWidth))
            {
                Result = ErrorViewResult("Введите корректную ширину отрезного/канавочного резца B (мм).");
                return;
            }

            var request = new TurningCuttingRequest
            {
                IsoGroup = SelectedIsoGroup,
                SelectedGrade = SelectedGrade,
                Operation = SelectedOperation.Kind,
                Stage = SelectedStage,
                ToolMaterial = SelectedToolMaterial.Kind,
                Diameter = diameter,
                OverhangRatio = overhangRatio,
                GrooveWidth = grooveWidth
            };

            Result = _cuttingService.Calculate(request);
            IsCopySuccessVisible = false;
            OnPropertyChanged(nameof(SimMaterialContextNote));
            RecalculateSimulation();
            UpdateTurningChart();
        }

        private static bool TryParseDouble(string raw, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string norm = raw.Trim().Replace(',', '.');
            return double.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private static TurningCuttingResult ErrorViewResult(string msg) =>
            new() { IsValid = false, HasError = true, ErrorMessage = msg };

        private void CopyGCode()
        {
            if (!CanCopyGCode) return;
            Clipboard.SetText(Result.SampleGCode);
            IsCopySuccessVisible = true;
        }

        // =====================================================================
        // КАЛЬКУЛЯТОР РЕЖИМОВ И МОДЕЛИРОВАНИЕ G96 / G97
        // =====================================================================

        public ICommand ApplyFromReferenceCommand { get; }
        public ICommand CopySimGCodeCommand { get; }

        private bool _simIsConstantSurfaceSpeed = true; // true = G96, false = G97
        public bool SimIsConstantSurfaceSpeed
        {
            get => _simIsConstantSurfaceSpeed;
            set
            {
                if (SetProperty(ref _simIsConstantSurfaceSpeed, value))
                {
                    if (!value && SimCalcByVc)
                        SimCalcByVc = false;
                    else if (value && !SimCalcByVc)
                        SimCalcByVc = true;

                    OnPropertyChanged(nameof(SimModeDescription));
                    RecalculateSimulation();
                }
            }
        }
        public string SimMaterialContextNote
        {
            get
            {
                if (SelectedIsoGroup == null) return string.Empty;

                string matTitle = SelectedGrade != null
                    ? $"{SelectedGrade.GostGrade} (ISO {SelectedIsoGroup.CodeLetter})"
                    : $"ISO {SelectedIsoGroup.CodeLetter} — {SelectedIsoGroup.Name}";

                return Result.IsValid
                    ? $"Активный материал справочника: {matTitle} • Рекомендуемый диапазон Vc: {Result.VcRangeText}"
                    : $"Активный материал справочника: {matTitle}";
            }
        }

        public string SimModeDescription => SimIsConstantSurfaceSpeed
            ? "G96 (CSS): шпиндель автоматически повышает обороты при уменьшении диаметра до порога G50."
            : "G97 (RPM): обороты шпинделя постоянны на всём проходе, скорость резания Vc падает к центру.";

        private bool _simCalcByVc = true; // true = задана Vc -> расчёт n; false = заданы обороты n -> расчёт Vc
        public bool SimCalcByVc
        {
            get => _simCalcByVc;
            set
            {
                if (SetProperty(ref _simCalcByVc, value))
                {
                    OnPropertyChanged(nameof(SimCalcByRpm));
                    RecalculateSimulation();
                }
            }
        }

        public bool SimCalcByRpm
        {
            get => !_simCalcByVc;
            set => SimCalcByVc = !value;
        }

        private string _simVcRaw = "180";
        public string SimVcRaw
        {
            get => _simVcRaw;
            set { if (SetProperty(ref _simVcRaw, value)) RecalculateSimulation(); }
        }

        private string _simRpmRaw = "1200";
        public string SimRpmRaw
        {
            get => _simRpmRaw;
            set { if (SetProperty(ref _simRpmRaw, value)) RecalculateSimulation(); }
        }

        private string _simFeedRaw = "0.25";
        public string SimFeedRaw
        {
            get => _simFeedRaw;
            set { if (SetProperty(ref _simFeedRaw, value)) RecalculateSimulation(); }
        }

        private string _simStartDiameterRaw = "80";
        public string SimStartDiameterRaw
        {
            get => _simStartDiameterRaw;
            set { if (SetProperty(ref _simStartDiameterRaw, value)) RecalculateSimulation(); }
        }

        private string _simMinDiameterRaw = "15";
        public string SimMinDiameterRaw
        {
            get => _simMinDiameterRaw;
            set { if (SetProperty(ref _simMinDiameterRaw, value)) RecalculateSimulation(); }
        }

        private string _simG50MaxRpmRaw = "2500";
        public string SimG50MaxRpmRaw
        {
            get => _simG50MaxRpmRaw;
            set { if (SetProperty(ref _simG50MaxRpmRaw, value)) RecalculateSimulation(); }
        }

        private SpindleSimulationResult _simResult = new();
        public SpindleSimulationResult SimResult
        {
            get => _simResult;
            private set
            {
                if (SetProperty(ref _simResult, value))
                    OnPropertyChanged(nameof(CanCopySimGCode));
            }
        }

        private bool _isSimCopySuccessVisible;
        public bool IsSimCopySuccessVisible
        {
            get => _isSimCopySuccessVisible;
            private set => SetProperty(ref _isSimCopySuccessVisible, value);
        }

        public bool CanCopySimGCode =>
            SimResult.IsValid && !string.IsNullOrWhiteSpace(SimResult.SampleGCode) && SimResult.SampleGCode != "—";

        private void ApplyFromReference()
        {
            if (!Result.IsValid) return;

            var inv = CultureInfo.InvariantCulture;
            SimVcRaw = Result.RecommendedVc.ToString("0", inv);
            SimRpmRaw = Result.RecommendedRpm.ToString(inv);
            SimG50MaxRpmRaw = Result.RecommendedG50.ToString(inv);

            if (Result.RecommendedFeedPerRev > 0)
                SimFeedRaw = Result.RecommendedFeedPerRev.ToString("0.###", inv);

            if (TryParseDouble(DiameterRaw, out double refDia) && refDia > 0)
                SimStartDiameterRaw = refDia.ToString("0.##", inv);

            RecalculateSimulation();
        }

        private void RecalculateSimulation()
        {
            if (_cuttingService == null) return;

            if (!TryParseDouble(SimStartDiameterRaw, out double dStart) || dStart <= 0)
            {
                SimResult = ErrorSimViewResult("Введите корректный начальный диаметр D_нач (> 0 мм).");
                return;
            }

            if (!TryParseDouble(SimMinDiameterRaw, out double dMin) || dMin < 0)
            {
                SimResult = ErrorSimViewResult("Минимальный диаметр D_мин не может быть отрицательным.");
                return;
            }

            if (!TryParseDouble(SimFeedRaw, out double feed) || feed <= 0)
            {
                SimResult = ErrorSimViewResult("Введите корректную оборотную подачу fn (> 0 мм/об).");
                return;
            }

            double vc = 0;
            int rpm = 0;

            if (SimCalcByVc)
            {
                if (!TryParseDouble(SimVcRaw, out vc) || vc <= 0)
                {
                    SimResult = ErrorSimViewResult("Введите корректную скорость резания Vc (> 0 м/мин).");
                    return;
                }
            }
            else
            {
                if (!int.TryParse(SimRpmRaw, out rpm) || rpm <= 0)
                {
                    SimResult = ErrorSimViewResult("Введите корректную частоту вращения шпинделя n (> 0 об/мин).");
                    return;
                }
            }

            if (!int.TryParse(SimG50MaxRpmRaw, out int g50) || g50 <= 0)
            {
                SimResult = ErrorSimViewResult("Введите корректное ограничение оборотов патрона G50 (> 0 об/мин).");
                return;
            }

            var request = new SpindleSimulationRequest
            {
                IsConstantSurfaceSpeed = SimIsConstantSurfaceSpeed,
                InputMode = SimCalcByVc ? CuttingCalcInputMode.ByCuttingSpeedVc : CuttingCalcInputMode.BySpindleRpm,
                InputVc = vc,
                InputRpm = rpm,
                FeedPerRev = feed,
                StartDiameter = dStart,
                MinDiameter = dMin,
                MaxChuckRpmG50 = g50,
                MaterialContextLabel = SelectedGrade != null
                    ? $"{SelectedGrade.GostGrade} (ISO {SelectedIsoGroup.CodeLetter})"
                    : $"группы ISO {SelectedIsoGroup.CodeLetter}",
                ReferenceVcMin = Result.IsValid ? Result.VcMin : null,
                ReferenceVcMax = Result.IsValid ? Result.VcMax : null
            };

            SimResult = _cuttingService.SimulateSpindle(request);
            IsSimCopySuccessVisible = false;

            UpdateTurningChart();
        }

        private static SpindleSimulationResult ErrorSimViewResult(string msg) =>
            new() { IsValid = false, HasError = true, ErrorMessage = msg };

        private void CopySimGCode()
        {
            if (!CanCopySimGCode) return;
            Clipboard.SetText(SimResult.SampleGCode);
            IsSimCopySuccessVisible = true;
        }

        // =====================================================================
        // КАЛЬКУЛЯТОР ТЕОРЕТИЧЕСКОЙ ШЕРОХОВАТОСТИ (Ra / Rz)
        // =====================================================================

        public ICommand SelectNoseRadiusCommand { get; }
        public ICommand SelectTargetRoughnessCommand { get; }
        public ICommand ApplyFeedToSimulatorCommand { get; }
        public ICommand PullFeedFromReferenceCommand { get; }

        private bool _roughByTargetMode = true; // true = задан Ra/Rz -> расчёт fn; false = задана fn -> расчёт Ra/Rz
        public bool RoughByTargetMode
        {
            get => _roughByTargetMode;
            set
            {
                if (SetProperty(ref _roughByTargetMode, value))
                {
                    OnPropertyChanged(nameof(RoughByFeedMode));
                    OnPropertyChanged(nameof(RoughModeDescription));
                    RecalculateRoughness();
                }
            }
        }

        public bool RoughByFeedMode
        {
            get => !_roughByTargetMode;
            set => RoughByTargetMode = !value;
        }

        public string RoughModeDescription => RoughByTargetMode
            ? "Подбор максимальной подачи fn под заданную по чертежу шероховатость Ra / Rz без потери производительности."
            : "Проверочный расчёт теоретической (Ra/Rz) и ожидаемой реальной шероховатости для заданной подачи fn.";

        private bool _roughIsRzParameter; // false = Ra (стандарт), true = Rz
        public bool RoughIsRzParameter
        {
            get => _roughIsRzParameter;
            set
            {
                if (SetProperty(ref _roughIsRzParameter, value))
                {
                    OnPropertyChanged(nameof(RoughTargetInputHint));
                    RecalculateRoughness();
                }
            }
        }

        public string RoughTargetInputHint => RoughIsRzParameter
            ? "Требуемая шероховатость Rz (мкм)"
            : "Требуемая шероховатость Ra (мкм)";

        private string _roughNoseRadiusRaw = "0.4";
        public string RoughNoseRadiusRaw
        {
            get => _roughNoseRadiusRaw;
            set
            {
                if (SetProperty(ref _roughNoseRadiusRaw, value))
                {
                    OnPropertyChanged(nameof(Rad02Bg)); OnPropertyChanged(nameof(Rad02Fg));
                    OnPropertyChanged(nameof(Rad04Bg)); OnPropertyChanged(nameof(Rad04Fg));
                    OnPropertyChanged(nameof(Rad08Bg)); OnPropertyChanged(nameof(Rad08Fg));
                    OnPropertyChanged(nameof(Rad12Bg)); OnPropertyChanged(nameof(Rad12Fg));
                    OnPropertyChanged(nameof(Rad16Bg)); OnPropertyChanged(nameof(Rad16Fg));
                    RecalculateRoughness();
                }
            }
        }

        // Подсветка кнопок стандартного ряда радиусов r_ε (ISO 1832)
        private bool IsRadiusSelected(string val) =>
            TryParseDouble(RoughNoseRadiusRaw, out double r) && System.Math.Abs(r - double.Parse(val, CultureInfo.InvariantCulture)) < 0.001;

        public string Rad02Bg => IsRadiusSelected("0.2") ? "#1565C0" : "Transparent";
        public string Rad02Fg => IsRadiusSelected("0.2") ? "#FFFFFF" : "#263238";

        public string Rad04Bg => IsRadiusSelected("0.4") ? "#1565C0" : "Transparent";
        public string Rad04Fg => IsRadiusSelected("0.4") ? "#FFFFFF" : "#263238";

        public string Rad08Bg => IsRadiusSelected("0.8") ? "#1565C0" : "Transparent";
        public string Rad08Fg => IsRadiusSelected("0.8") ? "#FFFFFF" : "#263238";

        public string Rad12Bg => IsRadiusSelected("1.2") ? "#1565C0" : "Transparent";
        public string Rad12Fg => IsRadiusSelected("1.2") ? "#FFFFFF" : "#263238";

        public string Rad16Bg => IsRadiusSelected("1.6") ? "#1565C0" : "Transparent";
        public string Rad16Fg => IsRadiusSelected("1.6") ? "#FFFFFF" : "#263238";

        private string _roughTargetValueRaw = "1.6";
        public string RoughTargetValueRaw
        {
            get => _roughTargetValueRaw;
            set { if (SetProperty(ref _roughTargetValueRaw, value)) RecalculateRoughness(); }
        }

        private string _roughInputFeedRaw = "0.12";
        public string RoughInputFeedRaw
        {
            get => _roughInputFeedRaw;
            set { if (SetProperty(ref _roughInputFeedRaw, value)) RecalculateRoughness(); }
        }

        private RoughnessCalculationResult _roughResult = new();
        public RoughnessCalculationResult RoughResult
        {
            get => _roughResult;
            private set => SetProperty(ref _roughResult, value);
        }

        private bool _isFeedAppliedNoticeVisible;
        public bool IsFeedAppliedNoticeVisible
        {
            get => _isFeedAppliedNoticeVisible;
            private set => SetProperty(ref _isFeedAppliedNoticeVisible, value);
        }

        private void RecalculateRoughness()
        {
            if (_cuttingService == null) return;

            if (!TryParseDouble(RoughNoseRadiusRaw, out double radius) || radius <= 0)
            {
                RoughResult = ErrorRoughViewResult("Введите корректный радиус при вершине пластины r_ε (> 0 мм).");
                return;
            }

            double targetVal = 1.6;
            double inputFeed = 0.12;

            if (RoughByTargetMode)
            {
                if (!TryParseDouble(RoughTargetValueRaw, out targetVal) || targetVal <= 0)
                {
                    RoughResult = ErrorRoughViewResult("Введите требуемую шероховатость (> 0 мкм).");
                    return;
                }
            }
            else
            {
                if (!TryParseDouble(RoughInputFeedRaw, out inputFeed) || inputFeed <= 0)
                {
                    RoughResult = ErrorRoughViewResult("Введите оборотную подачу fn (> 0 мм/об).");
                    return;
                }
            }

            double? ldContext = TryParseDouble(OverhangRatioRaw, out double ld) && ld > 0 ? ld : null;

            var request = new RoughnessCalculationRequest
            {
                Mode = RoughByTargetMode ? RoughnessCalcMode.ByTargetRoughness : RoughnessCalcMode.ByFeedRate,
                ParameterKind = RoughIsRzParameter ? RoughnessParameterKind.Rz : RoughnessParameterKind.Ra,
                NoseRadius = radius,
                TargetRoughnessValue = targetVal,
                InputFeedPerRev = inputFeed,
                OverhangRatioContext = ldContext
            };

            RoughResult = _cuttingService.CalculateRoughness(request);
            IsFeedAppliedNoticeVisible = false;

            UpdateTurningChart();
        }

        private static RoughnessCalculationResult ErrorRoughViewResult(string msg) =>
            new() { IsValid = false, HasError = true, ErrorMessage = msg };

        private void ApplyFeedToSimulator()
        {
            if (!RoughResult.IsValid || RoughResult.CalculatedFeedPerRev <= 0) return;

            SimFeedRaw = RoughResult.CalculatedFeedPerRev.ToString("0.###", CultureInfo.InvariantCulture);
            IsFeedAppliedNoticeVisible = true;
        }

        private void PullFeedFromReference()
        {
            if (!Result.IsValid || Result.RecommendedFeedPerRev <= 0) return;

            RoughByTargetMode = false;
            RoughInputFeedRaw = Result.RecommendedFeedPerRev.ToString("0.###", CultureInfo.InvariantCulture);
        }

        // =====================================================================
        // КАРТОЧКА 4: ИНЖЕНЕРНЫЕ ГРАФИКИ РЕЖИМОВ ТОЧЕНИЯ
        // =====================================================================

        public bool IsEngineerMode => _mainViewModel.IsEngineerMode;

        private TurningChartMetric _selectedChartMetric = TurningChartMetric.ToolLifeTaylor;
        public TurningChartMetric SelectedChartMetric
        {
            get => _selectedChartMetric;
            set
            {
                if (SetProperty(ref _selectedChartMetric, value))
                {
                    OnPropertyChanged(nameof(IsTaylorChart));
                    OnPropertyChanged(nameof(IsRoughnessChart));
                    OnPropertyChanged(nameof(IsPowerChart));
                    OnPropertyChanged(nameof(IsSpindleChart));
                    UpdateTurningChart();
                }
            }
        }

        public bool IsTaylorChart
        {
            get => SelectedChartMetric == TurningChartMetric.ToolLifeTaylor;
            set { if (value) SelectedChartMetric = TurningChartMetric.ToolLifeTaylor; }
        }

        public bool IsRoughnessChart
        {
            get => SelectedChartMetric == TurningChartMetric.SurfaceRoughnessRa;
            set { if (value) SelectedChartMetric = TurningChartMetric.SurfaceRoughnessRa; }
        }

        public bool IsPowerChart
        {
            get => SelectedChartMetric == TurningChartMetric.CuttingPowerAp;
            set { if (value) SelectedChartMetric = TurningChartMetric.CuttingPowerAp; }
        }

        public bool IsSpindleChart
        {
            get => SelectedChartMetric == TurningChartMetric.SpindleSpeedProfile;
            set { if (value) SelectedChartMetric = TurningChartMetric.SpindleSpeedProfile; }
        }

        private EngineeringChartModel? _turningChartModel;
        public EngineeringChartModel? TurningChartModel
        {
            get => _turningChartModel;
            private set => SetProperty(ref _turningChartModel, value);
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

        private void UpdateTurningChart()
        {
            if (_chartService == null || SelectedIsoGroup == null) return;

            TryParseDouble(SimVcRaw, out double simVc);
            int.TryParse(SimRpmRaw, out int simRpm);
            TryParseDouble(SimStartDiameterRaw, out double dStart);
            TryParseDouble(SimMinDiameterRaw, out double dMin);
            int.TryParse(SimG50MaxRpmRaw, out int g50);
            TryParseDouble(RoughNoseRadiusRaw, out double radius);
            TryParseDouble(RoughTargetValueRaw, out double targetVal);

            double targetRa = RoughByTargetMode
                ? (RoughIsRzParameter ? targetVal / 4.0 : targetVal)
                : 1.6;

            var context = new TurningChartContext
            {
                IsoGroup = SelectedIsoGroup.Code,
                MaterialLabel = SelectedGrade != null
                    ? $"{SelectedGrade.GostGrade} — ISO {SelectedIsoGroup.CodeLetter} ({SelectedIsoGroup.Name})"
                    : $"ISO {SelectedIsoGroup.CodeLetter} — {SelectedIsoGroup.Name}",
                ToolMaterial = SelectedToolMaterial.Kind,

                RecommendedVc = Result.IsValid && Result.RecommendedVc > 0 ? Result.RecommendedVc : 180.0,
                RecommendedFeed = Result.IsValid && Result.RecommendedFeedPerRev > 0 ? Result.RecommendedFeedPerRev : 0.25,
                RecommendedAp = Result.IsValid && Result.RecommendedDepthOfCut > 0 ? Result.RecommendedDepthOfCut : 2.0,

                IsG96Mode = SimIsConstantSurfaceSpeed,
                SimVc = simVc > 0 ? simVc : 180.0,
                SimRpm = simRpm > 0 ? simRpm : 1200,
                StartDiameter = dStart > 0 ? dStart : 80.0,
                MinDiameter = dMin >= 0 ? dMin : 15.0,
                G50MaxRpm = g50 > 0 ? g50 : 2500,

                NoseRadius = radius > 0 ? radius : 0.4,
                CurrentRoughnessFeed = RoughResult.IsValid && RoughResult.CalculatedFeedPerRev > 0
                    ? RoughResult.CalculatedFeedPerRev
                    : 0.12,
                TargetRa = targetRa > 0 ? targetRa : 1.6
            };

            var summary = _chartService.BuildChart(SelectedChartMetric, context);

            TurningChartModel = summary.ChartModel;
            ChartValueBadge = summary.ValueBadge;
            ChartHeadline = summary.Headline;
            ChartAdvice = summary.Advice;
            IsChartWarning = summary.IsWarning;
        }

    }
}
