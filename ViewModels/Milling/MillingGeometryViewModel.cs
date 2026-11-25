using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MechCore.Core;
using MechCore.Models.Materials;
using MechCore.Models.Milling;
using MechCore.Services.Materials;
using MechCore.Services.Milling;

namespace MechCore.ViewModels.Milling
{
    public class MillingGeometryViewModel : ObservableObject
    {
        private readonly IMillingGeometryService _millingGeometryService;
        private readonly IMaterialService _materialService;
        private readonly List<MaterialGradeItem> _allMaterialGrades;
        public IReadOnlyList<IsoMaterialGroupItem> IsoGroups { get; }
        public ICommand SelectHelicalIsoGroupCommand { get; }
        public ICommand ClearHelicalGradeCommand { get; }
        public ICommand SelectHelicalIntensityCommand { get; }
        public ICommand CopyHelicalGCodeCommand { get; }

        public MainViewModel Main { get; }
        public ICommand SelectThreadPresetCommand { get; }
        public ICommand SelectThreadIsoGroupCommand { get; }
        public ICommand ClearThreadGradeCommand { get; }
        public ICommand SelectThreadIntensityCommand { get; }
        public ICommand CopyMillingThreadGCodeCommand { get; }
        public ICommand SelectHoleCountPresetCommand { get; }
        public ICommand SelectStartAnglePresetCommand { get; }
        public ICommand CopyPatternGCodeCommand { get; }
        public ICommand SelectScallopTargetPresetCommand { get; }

        private static readonly Brush ActiveBtnBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));
        private static readonly Brush InactiveBtnBg = Brushes.Transparent;
        private static readonly Brush ActiveBtnFg = Brushes.White;
        private static readonly Brush InactiveBtnFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));

        public MillingGeometryViewModel(
                 MainViewModel main,
                 IMillingGeometryService? millingGeometryService = null,
                 IMaterialService? materialService = null)
        {
            Main = main;
            _millingGeometryService = millingGeometryService ?? new MillingGeometryService();
            _materialService = materialService ?? new MaterialStandardService();

            IsoGroups = _materialService.GetIsoGroups();
            _allMaterialGrades = _materialService.GetAllGrades();
            _selectedHelicalIsoGroup = IsoGroups.First();

            SelectHoleCountPresetCommand = new RelayCommand(param =>
            {
                if (param != null && int.TryParse(param.ToString(), out int cnt) && cnt > 0)
                {
                    _pcdHoleCount = cnt;
                    _pcdHoleCountText = cnt.ToString(CultureInfo.InvariantCulture);
                    OnPropertyChanged(nameof(PcdHoleCountText));
                    NotifyHoleCountButtons();
                    RecalculatePattern();
                }
            });

            SelectStartAnglePresetCommand = new RelayCommand(param =>
            {
                if (param != null && TryParseDecimal(param.ToString(), out double ang))
                {
                    _pcdStartAngle = ang;
                    _pcdStartAngleText = ang.ToString("0.##", CultureInfo.InvariantCulture);
                    OnPropertyChanged(nameof(PcdStartAngleText));
                    NotifyStartAngleButtons();
                    RecalculatePattern();
                }
            });

            CopyPatternGCodeCommand = new RelayCommand(_ =>
            {
                if (PatternResult != null && PatternResult.IsValid && !string.IsNullOrWhiteSpace(PatternResult.GCodeSnippet))
                    Clipboard.SetText(PatternResult.GCodeSnippet);
            });

            SelectHelicalIsoGroupCommand = new RelayCommand(param =>
            {
                if (param is IsoMaterialGroupItem group)
                    SelectedHelicalIsoGroup = group;
            });

            ClearHelicalGradeCommand = new RelayCommand(_ => SelectedHelicalMaterial = null);

            SelectHelicalIntensityCommand = new RelayCommand(param =>
            {
                if (param?.ToString() is string s && Enum.TryParse(s, out FeedIntensityKind kind))
                    HelicalIntensity = kind;
            });

            CopyHelicalGCodeCommand = new RelayCommand(_ =>
            {
                if (HelicalResult != null && HelicalResult.IsValid && !string.IsNullOrWhiteSpace(HelicalResult.GCodeSnippet))
                    Clipboard.SetText(HelicalResult.GCodeSnippet);
            });

            _selectedThreadIsoGroup = IsoGroups.First();

            SelectThreadPresetCommand = new RelayCommand(param =>
            {
                if (param?.ToString() is string s)
                {
                    var parts = s.Split('x');
                    if (parts.Length == 2 &&
                        TryParseDecimal(parts[0], out double d) &&
                        TryParseDecimal(parts[1], out double p))
                    {
                        _threadDiameter = d;
                        _threadDiameterText = d.ToString("0.##", CultureInfo.InvariantCulture);
                        _threadPitch = p;
                        _threadPitchText = p.ToString("0.##", CultureInfo.InvariantCulture);
                        OnPropertyChanged(nameof(ThreadDiameterText));
                        OnPropertyChanged(nameof(ThreadPitchText));
                        if (AutoCalculateThreadParams) ApplyAutoThreadParams();
                        else RecalculateMillingThread();
                    }
                }
            });

            SelectThreadIsoGroupCommand = new RelayCommand(param =>
            {
                if (param is IsoMaterialGroupItem group)
                    SelectedThreadIsoGroup = group;
            });

            ClearThreadGradeCommand = new RelayCommand(_ => SelectedThreadMaterial = null);

            SelectThreadIntensityCommand = new RelayCommand(param =>
            {
                if (param?.ToString() is string s && Enum.TryParse(s, out FeedIntensityKind kind))
                    ThreadIntensity = kind;
            });

            CopyMillingThreadGCodeCommand = new RelayCommand(_ =>
            {
                if (MillingThreadResult != null && MillingThreadResult.IsValid && !string.IsNullOrWhiteSpace(MillingThreadResult.GCodeSnippet))
                    Clipboard.SetText(MillingThreadResult.GCodeSnippet);
            });

            SelectScallopTargetPresetCommand = new RelayCommand(param =>
            {
                if (param != null && TryParseDecimal(param.ToString(), out double val) && val > 0)
                {
                    _scallopTargetMicrons = val;
                    _scallopTargetMicronsText = val.ToString("0.##", CultureInfo.InvariantCulture);
                    OnPropertyChanged(nameof(ScallopTargetMicronsText));
                    RecalculateScallop();
                }
            });

            RecalculateScallop();

            UpdateFilteredThreadGrades();
            RecalculateMillingThread();

            UpdateFilteredHelicalGrades();
            RecalculatePattern();
            RecalculateHelical();
        }


        // =====================================================================
        // КАРТОЧКА 1: КООРДИНАТЫ ОТВЕРСТИЙ (PCD И ПРЯМОУГОЛЬНАЯ СЕТКА)
        // =====================================================================

        private bool _isGridMode;
        /// <summary>false — Делительная окружность (PCD), true — Прямоугольная сетка (Grid).</summary>
        public bool IsGridMode
        {
            get => _isGridMode;
            set
            {
                if (SetProperty(ref _isGridMode, value))
                {
                    OnPropertyChanged(nameof(IsCirclePcdMode));
                    OnPropertyChanged(nameof(Card1BadgeText));
                    OnPropertyChanged(nameof(OriginXHint));
                    OnPropertyChanged(nameof(OriginYHint));
                    RecalculatePattern();
                }
            }
        }

        public bool IsCirclePcdMode => !_isGridMode;
        public string Card1BadgeText => IsGridMode ? "Режим: Прямоугольная сетка" : "Режим: Окружность PCD";
        public string OriginXHint => IsGridMode ? "Начальная точка X0 (мм)" : "Центр окружности X0 (мм)";
        public string OriginYHint => IsGridMode ? "Начальная точка Y0 (мм)" : "Центр окружности Y0 (мм)";

        private double _patternOriginX;
        private string _patternOriginXText = "0";
        public string PatternOriginXText
        {
            get => _patternOriginXText;
            set
            {
                if (SetProperty(ref _patternOriginXText, value) && TryParseDecimal(value, out double val))
                {
                    _patternOriginX = val;
                    RecalculatePattern();
                }
            }
        }

        private double _patternOriginY;
        private string _patternOriginYText = "0";
        public string PatternOriginYText
        {
            get => _patternOriginYText;
            set
            {
                if (SetProperty(ref _patternOriginYText, value) && TryParseDecimal(value, out double val))
                {
                    _patternOriginY = val;
                    RecalculatePattern();
                }
            }
        }

        // --- Параметры режима PCD ---
        private double _pcdDiameter = 100.0;
        private string _pcdDiameterText = "100";
        public string PcdDiameterText
        {
            get => _pcdDiameterText;
            set
            {
                if (SetProperty(ref _pcdDiameterText, value) && TryParseDecimal(value, out double val))
                {
                    _pcdDiameter = val;
                    RecalculatePattern();
                }
            }
        }

        private int _pcdHoleCount = 6;
        private string _pcdHoleCountText = "6";
        public string PcdHoleCountText
        {
            get => _pcdHoleCountText;
            set
            {
                if (SetProperty(ref _pcdHoleCountText, value) && int.TryParse(value, out int val))
                {
                    _pcdHoleCount = val;
                    NotifyHoleCountButtons();
                    RecalculatePattern();
                }
            }
        }

        public Brush Cnt3Bg => _pcdHoleCount == 3 ? ActiveBtnBg : InactiveBtnBg;
        public Brush Cnt3Fg => _pcdHoleCount == 3 ? ActiveBtnFg : InactiveBtnFg;
        public Brush Cnt4Bg => _pcdHoleCount == 4 ? ActiveBtnBg : InactiveBtnBg;
        public Brush Cnt4Fg => _pcdHoleCount == 4 ? ActiveBtnFg : InactiveBtnFg;
        public Brush Cnt6Bg => _pcdHoleCount == 6 ? ActiveBtnBg : InactiveBtnBg;
        public Brush Cnt6Fg => _pcdHoleCount == 6 ? ActiveBtnFg : InactiveBtnFg;
        public Brush Cnt8Bg => _pcdHoleCount == 8 ? ActiveBtnBg : InactiveBtnBg;
        public Brush Cnt8Fg => _pcdHoleCount == 8 ? ActiveBtnFg : InactiveBtnFg;
        public Brush Cnt12Bg => _pcdHoleCount == 12 ? ActiveBtnBg : InactiveBtnBg;
        public Brush Cnt12Fg => _pcdHoleCount == 12 ? ActiveBtnFg : InactiveBtnFg;

        private void NotifyHoleCountButtons()
        {
            OnPropertyChanged(nameof(Cnt3Bg)); OnPropertyChanged(nameof(Cnt3Fg));
            OnPropertyChanged(nameof(Cnt4Bg)); OnPropertyChanged(nameof(Cnt4Fg));
            OnPropertyChanged(nameof(Cnt6Bg)); OnPropertyChanged(nameof(Cnt6Fg));
            OnPropertyChanged(nameof(Cnt8Bg)); OnPropertyChanged(nameof(Cnt8Fg));
            OnPropertyChanged(nameof(Cnt12Bg)); OnPropertyChanged(nameof(Cnt12Fg));
        }

        private double _pcdStartAngle;
        private string _pcdStartAngleText = "0";
        public string PcdStartAngleText
        {
            get => _pcdStartAngleText;
            set
            {
                if (SetProperty(ref _pcdStartAngleText, value) && TryParseDecimal(value, out double val))
                {
                    _pcdStartAngle = val;
                    NotifyStartAngleButtons();
                    RecalculatePattern();
                }
            }
        }

        public Brush Ang0Bg => Math.Abs(_pcdStartAngle - 0.0) < 0.01 ? ActiveBtnBg : InactiveBtnBg;
        public Brush Ang0Fg => Math.Abs(_pcdStartAngle - 0.0) < 0.01 ? ActiveBtnFg : InactiveBtnFg;
        public Brush Ang30Bg => Math.Abs(_pcdStartAngle - 30.0) < 0.01 ? ActiveBtnBg : InactiveBtnBg;
        public Brush Ang30Fg => Math.Abs(_pcdStartAngle - 30.0) < 0.01 ? ActiveBtnFg : InactiveBtnFg;
        public Brush Ang45Bg => Math.Abs(_pcdStartAngle - 45.0) < 0.01 ? ActiveBtnBg : InactiveBtnBg;
        public Brush Ang45Fg => Math.Abs(_pcdStartAngle - 45.0) < 0.01 ? ActiveBtnFg : InactiveBtnFg;
        public Brush Ang90Bg => Math.Abs(_pcdStartAngle - 90.0) < 0.01 ? ActiveBtnBg : InactiveBtnBg;
        public Brush Ang90Fg => Math.Abs(_pcdStartAngle - 90.0) < 0.01 ? ActiveBtnFg : InactiveBtnFg;

        private void NotifyStartAngleButtons()
        {
            OnPropertyChanged(nameof(Ang0Bg)); OnPropertyChanged(nameof(Ang0Fg));
            OnPropertyChanged(nameof(Ang30Bg)); OnPropertyChanged(nameof(Ang30Fg));
            OnPropertyChanged(nameof(Ang45Bg)); OnPropertyChanged(nameof(Ang45Fg));
            OnPropertyChanged(nameof(Ang90Bg)); OnPropertyChanged(nameof(Ang90Fg));
        }

        private bool _useFullCircle = true;
        public bool UseFullCircle
        {
            get => _useFullCircle;
            set
            {
                if (SetProperty(ref _useFullCircle, value))
                {
                    OnPropertyChanged(nameof(IsCustomSectorAngle));
                    RecalculatePattern();
                }
            }
        }

        public bool IsCustomSectorAngle => !_useFullCircle;

        private double _pcdCustomStepAngle = 30.0;
        private string _pcdCustomStepAngleText = "30";
        public string PcdCustomStepAngleText
        {
            get => _pcdCustomStepAngleText;
            set
            {
                if (SetProperty(ref _pcdCustomStepAngleText, value) && TryParseDecimal(value, out double val))
                {
                    _pcdCustomStepAngle = val;
                    RecalculatePattern();
                }
            }
        }

        private bool _isPcdCounterClockwise = true;
        public bool IsPcdCounterClockwise
        {
            get => _isPcdCounterClockwise;
            set { if (SetProperty(ref _isPcdCounterClockwise, value)) RecalculatePattern(); }
        }
        // --- Параметры режима Rectangular Grid ---
        private int _gridCountX = 3;
        private string _gridCountXText = "3";
        public string GridCountXText
        {
            get => _gridCountXText;
            set
            {
                if (SetProperty(ref _gridCountXText, value) && int.TryParse(value, out int val))
                {
                    _gridCountX = val;
                    RecalculatePattern();
                }
            }
        }
        private int _gridCountY = 2;
        private string _gridCountYText = "2";
        public string GridCountYText
        {
            get => _gridCountYText;
            set
            {
                if (SetProperty(ref _gridCountYText, value) && int.TryParse(value, out int val))
                {
                    _gridCountY = val;
                    RecalculatePattern();
                }
            }
        }
        private double _gridStepX = 25.0;
        private string _gridStepXText = "25";
        public string GridStepXText
        {
            get => _gridStepXText;
            set
            {
                if (SetProperty(ref _gridStepXText, value) && TryParseDecimal(value, out double val))
                {
                    _gridStepX = val;
                    RecalculatePattern();
                }
            }
        }
        private double _gridStepY = 20.0;
        private string _gridStepYText = "20";
        public string GridStepYText
        {
            get => _gridStepYText;
            set
            {
                if (SetProperty(ref _gridStepYText, value) && TryParseDecimal(value, out double val))
                {
                    _gridStepY = val;
                    RecalculatePattern();
                }
            }
        }
        private double _gridRotationAngle;
        private string _gridRotationAngleText = "0";
        public string GridRotationAngleText
        {
            get => _gridRotationAngleText;
            set
            {
                if (SetProperty(ref _gridRotationAngleText, value) && TryParseDecimal(value, out double val))
                {
                    _gridRotationAngle = val;
                    RecalculatePattern();
                }
            }
        }
        private bool _useZigZagOrder = true;
        public bool UseZigZagOrder
        {
            get => _useZigZagOrder;
            set { if (SetProperty(ref _useZigZagOrder, value)) RecalculatePattern(); }
        }
        private bool _skipFirstPointInCycleCode;
        public bool SkipFirstPointInCycleCode
        {
            get => _skipFirstPointInCycleCode;
            set { if (SetProperty(ref _skipFirstPointInCycleCode, value)) RecalculatePattern(); }
        }
        private HolePatternCalculationResult _patternResult = new();
        public HolePatternCalculationResult PatternResult
        {
            get => _patternResult;
            private set => SetProperty(ref _patternResult, value);
        }
        private void RecalculatePattern()
        {
            PatternResult = _millingGeometryService.CalculateHolePattern(
            IsGridMode ? HolePatternMode.RectangularGrid : HolePatternMode.CirclePcd,
            _patternOriginX,
            _patternOriginY,
            _pcdDiameter,
            _pcdHoleCount,
            _pcdStartAngle,
            _pcdCustomStepAngle,
            UseFullCircle,
            IsPcdCounterClockwise,
            _gridCountX,
            _gridCountY,
            _gridStepX,
            _gridStepY,
            _gridRotationAngle,
            UseZigZagOrder,
            SkipFirstPointInCycleCode);
        }
        private static bool TryParseDecimal(string? text, out double result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string normalized = text.Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        // =====================================================================
        // КАРТОЧКА 2: ВИНТОВОЕ ВРЕЗАНИЕ И РАСФРЕЗЕРОВКА ОТВЕРСТИЙ (HELICAL)
        // =====================================================================

        private bool _isHelicalClimbMilling = true;
        /// <summary>true — Попутное (G03 внутри отверстия, по умолчанию), false — Встречное (G02).</summary>
        public bool IsHelicalClimbMilling
        {
            get => _isHelicalClimbMilling;
            set { if (SetProperty(ref _isHelicalClimbMilling, value)) RecalculateHelical(); }
        }

        private bool _includeHelicalBottomCleanup = true;
        public bool IncludeHelicalBottomCleanup
        {
            get => _includeHelicalBottomCleanup;
            set { if (SetProperty(ref _includeHelicalBottomCleanup, value)) RecalculateHelical(); }
        }

        private double _helicalHoleDiameter = 20.0;
        private string _helicalHoleDiameterText = "20";
        public string HelicalHoleDiameterText
        {
            get => _helicalHoleDiameterText;
            set
            {
                if (SetProperty(ref _helicalHoleDiameterText, value) && TryParseDecimal(value, out double val))
                {
                    _helicalHoleDiameter = val;
                    RecalculateHelical();
                }
            }
        }

        private double _helicalToolDiameter = 12.0;
        private string _helicalToolDiameterText = "12";
        public string HelicalToolDiameterText
        {
            get => _helicalToolDiameterText;
            set
            {
                if (SetProperty(ref _helicalToolDiameterText, value) && TryParseDecimal(value, out double val))
                {
                    _helicalToolDiameter = val;
                    if (AutoCalculateHelicalParams) ApplyAutoHelicalParams();
                    else RecalculateHelical();
                }
            }
        }

        private int _helicalFluteCount = 4;
        private string _helicalFluteCountText = "4";
        public string HelicalFluteCountText
        {
            get => _helicalFluteCountText;
            set
            {
                if (SetProperty(ref _helicalFluteCountText, value) && int.TryParse(value, out int val))
                {
                    _helicalFluteCount = val;
                    if (AutoCalculateHelicalParams) ApplyAutoHelicalParams();
                    else RecalculateHelical();
                }
            }
        }

        private double _helicalCenterX;
        private string _helicalCenterXText = "0";
        public string HelicalCenterXText
        {
            get => _helicalCenterXText;
            set
            {
                if (SetProperty(ref _helicalCenterXText, value) && TryParseDecimal(value, out double val))
                {
                    _helicalCenterX = val;
                    RecalculateHelical();
                }
            }
        }

        private double _helicalCenterY;
        private string _helicalCenterYText = "0";
        public string HelicalCenterYText
        {
            get => _helicalCenterYText;
            set
            {
                if (SetProperty(ref _helicalCenterYText, value) && TryParseDecimal(value, out double val))
                {
                    _helicalCenterY = val;
                    RecalculateHelical();
                }
            }
        }

        private double _helicalStartZ;
        private string _helicalStartZText = "0";
        public string HelicalStartZText
        {
            get => _helicalStartZText;
            set
            {
                if (SetProperty(ref _helicalStartZText, value) && TryParseDecimal(value, out double val))
                {
                    _helicalStartZ = val;
                    RecalculateHelical();
                }
            }
        }

        private double _helicalTargetZ = -15.0;
        private string _helicalTargetZText = "-15";
        public string HelicalTargetZText
        {
            get => _helicalTargetZText;
            set
            {
                if (SetProperty(ref _helicalTargetZText, value) && TryParseDecimal(value, out double val))
                {
                    _helicalTargetZ = val;
                    RecalculateHelical();
                }
            }
        }

        private double _helicalClearanceR = 2.0;
        private string _helicalClearanceRText = "2";
        public string HelicalClearanceRText
        {
            get => _helicalClearanceRText;
            set
            {
                if (SetProperty(ref _helicalClearanceRText, value) && TryParseDecimal(value, out double val))
                {
                    _helicalClearanceR = val;
                    RecalculateHelical();
                }
            }
        }

        private bool _autoCalculateHelicalParams;
        public bool AutoCalculateHelicalParams
        {
            get => _autoCalculateHelicalParams;
            set
            {
                if (SetProperty(ref _autoCalculateHelicalParams, value))
                {
                    OnPropertyChanged(nameof(HelicalAutoNote));
                    if (value) ApplyAutoHelicalParams();
                    else RecalculateHelical();
                }
            }
        }

        private IsoMaterialGroupItem _selectedHelicalIsoGroup;
        public IsoMaterialGroupItem SelectedHelicalIsoGroup
        {
            get => _selectedHelicalIsoGroup;
            set
            {
                if (value != null && SetProperty(ref _selectedHelicalIsoGroup, value))
                {
                    _selectedHelicalMaterial = null;
                    OnPropertyChanged(nameof(SelectedHelicalMaterial));
                    OnPropertyChanged(nameof(HasSelectedHelicalMaterial));
                    UpdateFilteredHelicalGrades();
                    OnPropertyChanged(nameof(HelicalAutoNote));
                    if (AutoCalculateHelicalParams) ApplyAutoHelicalParams();
                }
            }
        }

        private List<MaterialGradeItem> _filteredHelicalGrades = new();
        public List<MaterialGradeItem> FilteredHelicalGrades
        {
            get => _filteredHelicalGrades;
            private set => SetProperty(ref _filteredHelicalGrades, value);
        }

        private MaterialGradeItem? _selectedHelicalMaterial;
        public MaterialGradeItem? SelectedHelicalMaterial
        {
            get => _selectedHelicalMaterial;
            set
            {
                if (SetProperty(ref _selectedHelicalMaterial, value))
                {
                    OnPropertyChanged(nameof(HasSelectedHelicalMaterial));
                    OnPropertyChanged(nameof(HelicalAutoNote));
                    if (AutoCalculateHelicalParams) ApplyAutoHelicalParams();
                }
            }
        }

        public bool HasSelectedHelicalMaterial => SelectedHelicalMaterial != null;

        private void UpdateFilteredHelicalGrades()
        {
            if (SelectedHelicalIsoGroup == null)
            {
                FilteredHelicalGrades = new List<MaterialGradeItem>();
                return;
            }
            FilteredHelicalGrades = _allMaterialGrades
                .Where(g => g.IsoGroup == SelectedHelicalIsoGroup.Code)
                .ToList();
        }

        private FeedIntensityKind _helicalIntensity = FeedIntensityKind.Optimal;
        public FeedIntensityKind HelicalIntensity
        {
            get => _helicalIntensity;
            set
            {
                if (SetProperty(ref _helicalIntensity, value))
                {
                    OnPropertyChanged(nameof(HelicalGentleBg)); OnPropertyChanged(nameof(HelicalGentleFg));
                    OnPropertyChanged(nameof(HelicalOptimalBg)); OnPropertyChanged(nameof(HelicalOptimalFg));
                    OnPropertyChanged(nameof(HelicalAggressiveBg)); OnPropertyChanged(nameof(HelicalAggressiveFg));
                    OnPropertyChanged(nameof(HelicalIntensityNote));
                    if (AutoCalculateHelicalParams) ApplyAutoHelicalParams();
                }
            }
        }

        public Brush HelicalGentleBg => _helicalIntensity == FeedIntensityKind.Gentle ? ActiveBtnBg : InactiveBtnBg;
        public Brush HelicalGentleFg => _helicalIntensity == FeedIntensityKind.Gentle ? ActiveBtnFg : InactiveBtnFg;
        public Brush HelicalOptimalBg => _helicalIntensity == FeedIntensityKind.Optimal ? ActiveBtnBg : InactiveBtnBg;
        public Brush HelicalOptimalFg => _helicalIntensity == FeedIntensityKind.Optimal ? ActiveBtnFg : InactiveBtnFg;
        public Brush HelicalAggressiveBg => _helicalIntensity == FeedIntensityKind.Aggressive ? ActiveBtnBg : InactiveBtnBg;
        public Brush HelicalAggressiveFg => _helicalIntensity == FeedIntensityKind.Aggressive ? ActiveBtnFg : InactiveBtnFg;

        public string HelicalIntensityNote => _helicalIntensity switch
        {
            FeedIntensityKind.Gentle => "Щадящий режим: малый угол врезания α и сниженная подача для глубоких карманов и плохого вывода стружки.",
            FeedIntensityKind.Aggressive => "Форсированный режим: увеличенный угол спирали α и высокая подача для жёстких фрез с внутренним подводом СОЖ.",
            _ => "Оптимальный режим: безопасный угол врезания по каталогу и стабильное стружкодробление."
        };

        private double _helicalRampAngle = 3.0;
        private string _helicalRampAngleText = "3";
        public string HelicalRampAngleText
        {
            get => _helicalRampAngleText;
            set
            {
                if (SetProperty(ref _helicalRampAngleText, value) && TryParseDecimal(value, out double val))
                {
                    _helicalRampAngle = val;
                    RecalculateHelical();
                }
            }
        }
        private double _helicalEdgeFeed = 450.0;
        private string _helicalEdgeFeedText = "450";
        public string HelicalEdgeFeedText
        {
            get => _helicalEdgeFeedText;
            set
            {
                if (SetProperty(ref _helicalEdgeFeedText, value) && TryParseDecimal(value, out double val))
                {
                    _helicalEdgeFeed = val;
                    RecalculateHelical();
                }
            }
        }
        private int _helicalSpindleRpm = 3500;
        private string _helicalSpindleRpmText = "3500";
        public string HelicalSpindleRpmText
        {
            get => _helicalSpindleRpmText;
            set
            {
                if (SetProperty(ref _helicalSpindleRpmText, value) && int.TryParse(value, out int val))
                {
                    _helicalSpindleRpm = val;
                    RecalculateHelical();
                }
            }
        }
        public string HelicalAutoNote
        {
            get
            {
                if (!AutoCalculateHelicalParams || SelectedHelicalIsoGroup == null) return string.Empty;
                string matText = SelectedHelicalMaterial != null
                ? $"{SelectedHelicalMaterial.GostGrade} (ISO {SelectedHelicalIsoGroup.CodeLetter})"
                : $"группы ISO {SelectedHelicalIsoGroup.CodeLetter} ({SelectedHelicalIsoGroup.Name})";
                return $"Автоподбор α, S и Vf для {matText}: фреза Ø{_helicalToolDiameter:0.##} мм, z = {_helicalFluteCount}.";
            }
        }
        private void ApplyAutoHelicalParams()
        {
            if (SelectedHelicalIsoGroup == null) return;
            double dia = _helicalToolDiameter > 0.5 ? _helicalToolDiameter : 12.0;
            int z = Math.Clamp(_helicalFluteCount, 1, 24);
            // Базовые каталожные параметры для твёрдосплавной концевой фрезы по группам ISO 513
            var (baseVc, baseFz, baseAngle) = SelectedHelicalIsoGroup.Code switch
            {
                IsoGroupCode.P => (140.0, 0.045, 3.5), // Стали
                IsoGroupCode.M => (85.0, 0.032, 2.0), // Нержавейка
                IsoGroupCode.K => (160.0, 0.055, 4.5), // Чугун
                IsoGroupCode.N => (320.0, 0.075, 8.0), // Алюминий / цветные
                IsoGroupCode.S => (42.0, 0.022, 1.5), // Титан / жаропрочные
                IsoGroupCode.H => (55.0, 0.020, 1.2), // Закалённые стали
                _ => (120.0, 0.040, 3.0)
            };
            double km = SelectedHelicalIsoGroup.MachinabilityFactor;
            double intensityFactor = _helicalIntensity switch
            {
                FeedIntensityKind.Gentle => 0.75,
                FeedIntensityKind.Aggressive => 1.25,
                _ => 1.00
            };
            double vc = baseVc * Math.Sqrt(km) * intensityFactor;
            int rpm = (int)Math.Clamp(Math.Round((1000.0 * vc) / (Math.PI * dia)), 50, 24000);
            // Поправка fz на диаметр фрезы и интенсивность
            double fz = baseFz * Math.Clamp(dia / 12.0, 0.45, 1.6) * intensityFactor;
            double vf = Math.Round(rpm * z * fz, 0);
            double angle = Math.Round(baseAngle * intensityFactor, 1);
            _helicalRampAngle = angle;
            _helicalRampAngleText = angle.ToString("0.#", CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(HelicalRampAngleText));
            _helicalSpindleRpm = rpm;
            _helicalSpindleRpmText = rpm.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(HelicalSpindleRpmText));
            _helicalEdgeFeed = vf;
            _helicalEdgeFeedText = vf.ToString("0", CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(HelicalEdgeFeedText));
            OnPropertyChanged(nameof(HelicalAutoNote));
            RecalculateHelical();
        }
        private HelicalCalculationResult _helicalResult = new();
        public HelicalCalculationResult HelicalResult
        {
            get => _helicalResult;
            private set => SetProperty(ref _helicalResult, value);
        }
        private void RecalculateHelical()
        {
            HelicalResult = _millingGeometryService.CalculateHelicalInterpolation(
            _helicalHoleDiameter,
            _helicalToolDiameter,
            _helicalFluteCount,
            _helicalCenterX,
            _helicalCenterY,
            _helicalStartZ,
            _helicalTargetZ,
            _helicalClearanceR,
            _helicalRampAngle,
            _helicalEdgeFeed,
            _helicalSpindleRpm,
            IsHelicalClimbMilling,
            IncludeHelicalBottomCleanup);
        }

        // =====================================================================
        // КАРТОЧКА 3: РЕЗЬБОФРЕЗЕРОВАНИЕ И ЦИКЛ МЕТЧИКА G84
        // =====================================================================

        private bool _isThreadMillingMode = true;
        /// <summary>false — Метчик (M29 + G84), true — Резьбофрезерование.</summary>
        public bool IsThreadMillingMode
        {
            get => _isThreadMillingMode;
            set
            {
                if (SetProperty(ref _isThreadMillingMode, value))
                {
                    OnPropertyChanged(nameof(IsTappingMode));
                    OnPropertyChanged(nameof(Card3BadgeText));
                    if (AutoCalculateThreadParams) ApplyAutoThreadParams();
                    else RecalculateMillingThread();
                }
            }
        }

        public bool IsTappingMode => !_isThreadMillingMode;
        public string Card3BadgeText => IsThreadMillingMode ? "Режим: Резьбофреза (G02/G03)" : "Режим: Метчик (M29 + G84)";

        private bool _isExternalMillingThread;
        public bool IsExternalMillingThread
        {
            get => _isExternalMillingThread;
            set { if (SetProperty(ref _isExternalMillingThread, value)) RecalculateMillingThread(); }
        }

        private bool _isLeftHandMillingThread;
        public bool IsLeftHandMillingThread
        {
            get => _isLeftHandMillingThread;
            set { if (SetProperty(ref _isLeftHandMillingThread, value)) RecalculateMillingThread(); }
        }

        private double _threadDiameter = 12.0;
        private string _threadDiameterText = "12";
        public string ThreadDiameterText
        {
            get => _threadDiameterText;
            set
            {
                if (SetProperty(ref _threadDiameterText, value) && TryParseDecimal(value, out double val))
                {
                    _threadDiameter = val;
                    if (AutoCalculateThreadParams) ApplyAutoThreadParams();
                    else RecalculateMillingThread();
                }
            }
        }

        private double _threadPitch = 1.75;
        private string _threadPitchText = "1.75";
        public string ThreadPitchText
        {
            get => _threadPitchText;
            set
            {
                if (SetProperty(ref _threadPitchText, value) && TryParseDecimal(value, out double val))
                {
                    _threadPitch = val;
                    if (AutoCalculateThreadParams) ApplyAutoThreadParams();
                    else RecalculateMillingThread();
                }
            }
        }

        private double _threadMillDiameter = 8.0;
        private string _threadMillDiameterText = "8";
        public string ThreadMillDiameterText
        {
            get => _threadMillDiameterText;
            set
            {
                if (SetProperty(ref _threadMillDiameterText, value) && TryParseDecimal(value, out double val))
                {
                    _threadMillDiameter = val;
                    if (AutoCalculateThreadParams) ApplyAutoThreadParams();
                    else RecalculateMillingThread();
                }
            }
        }

        private int _threadMillFlutes = 3;
        private string _threadMillFlutesText = "3";
        public string ThreadMillFlutesText
        {
            get => _threadMillFlutesText;
            set
            {
                if (SetProperty(ref _threadMillFlutesText, value) && int.TryParse(value, out int val))
                {
                    _threadMillFlutes = val;
                    if (AutoCalculateThreadParams) ApplyAutoThreadParams();
                    else RecalculateMillingThread();
                }
            }
        }

        private double _threadCenterX;
        private string _threadCenterXText = "0";
        public string ThreadCenterXText
        {
            get => _threadCenterXText;
            set
            {
                if (SetProperty(ref _threadCenterXText, value) && TryParseDecimal(value, out double val))
                {
                    _threadCenterX = val;
                    RecalculateMillingThread();
                }
            }
        }

        private double _threadCenterY;
        private string _threadCenterYText = "0";
        public string ThreadCenterYText
        {
            get => _threadCenterYText;
            set
            {
                if (SetProperty(ref _threadCenterYText, value) && TryParseDecimal(value, out double val))
                {
                    _threadCenterY = val;
                    RecalculateMillingThread();
                }
            }
        }

        private double _threadStartZ;
        private string _threadStartZText = "0";
        public string ThreadStartZText
        {
            get => _threadStartZText;
            set
            {
                if (SetProperty(ref _threadStartZText, value) && TryParseDecimal(value, out double val))
                {
                    _threadStartZ = val;
                    RecalculateMillingThread();
                }
            }
        }

        private double _threadTargetZ = -18.0;
        private string _threadTargetZText = "-18";
        public string ThreadTargetZText
        {
            get => _threadTargetZText;
            set
            {
                if (SetProperty(ref _threadTargetZText, value) && TryParseDecimal(value, out double val))
                {
                    _threadTargetZ = val;
                    RecalculateMillingThread();
                }
            }
        }

        private double _threadClearanceR = 3.0;
        private string _threadClearanceRText = "3";
        public string ThreadClearanceRText
        {
            get => _threadClearanceRText;
            set
            {
                if (SetProperty(ref _threadClearanceRText, value) && TryParseDecimal(value, out double val))
                {
                    _threadClearanceR = val;
                    RecalculateMillingThread();
                }
            }
        }

        private bool _autoCalculateThreadParams;
        public bool AutoCalculateThreadParams
        {
            get => _autoCalculateThreadParams;
            set
            {
                if (SetProperty(ref _autoCalculateThreadParams, value))
                {
                    if (value) ApplyAutoThreadParams();
                    else RecalculateMillingThread();
                }
            }
        }

        private IsoMaterialGroupItem _selectedThreadIsoGroup;
        public IsoMaterialGroupItem SelectedThreadIsoGroup
        {
            get => _selectedThreadIsoGroup;
            set
            {
                if (value != null && SetProperty(ref _selectedThreadIsoGroup, value))
                {
                    _selectedThreadMaterial = null;
                    OnPropertyChanged(nameof(SelectedThreadMaterial));
                    OnPropertyChanged(nameof(HasSelectedThreadMaterial));
                    UpdateFilteredThreadGrades();
                    if (AutoCalculateThreadParams) ApplyAutoThreadParams();
                }
            }
        }

        private List<MaterialGradeItem> _filteredThreadGrades = new();
        public List<MaterialGradeItem> FilteredThreadGrades
        {
            get => _filteredThreadGrades;
            private set => SetProperty(ref _filteredThreadGrades, value);
        }

        private MaterialGradeItem? _selectedThreadMaterial;
        public MaterialGradeItem? SelectedThreadMaterial
        {
            get => _selectedThreadMaterial;
            set
            {
                if (SetProperty(ref _selectedThreadMaterial, value))
                {
                    OnPropertyChanged(nameof(HasSelectedThreadMaterial));
                    if (AutoCalculateThreadParams) ApplyAutoThreadParams();
                }
            }
        }

        public bool HasSelectedThreadMaterial => SelectedThreadMaterial != null;

        private void UpdateFilteredThreadGrades()
        {
            if (SelectedThreadIsoGroup == null)
            {
                FilteredThreadGrades = new List<MaterialGradeItem>();
                return;
            }
            FilteredThreadGrades = _allMaterialGrades
                .Where(g => g.IsoGroup == SelectedThreadIsoGroup.Code)
                .ToList();
        }

        private FeedIntensityKind _threadIntensity = FeedIntensityKind.Optimal;
        public FeedIntensityKind ThreadIntensity
        {
            get => _threadIntensity;
            set
            {
                if (SetProperty(ref _threadIntensity, value))
                {
                    OnPropertyChanged(nameof(ThreadGentleBg)); OnPropertyChanged(nameof(ThreadGentleFg));
                    OnPropertyChanged(nameof(ThreadOptimalBg)); OnPropertyChanged(nameof(ThreadOptimalFg));
                    OnPropertyChanged(nameof(ThreadAggressiveBg)); OnPropertyChanged(nameof(ThreadAggressiveFg));
                    OnPropertyChanged(nameof(ThreadIntensityNote));
                    if (AutoCalculateThreadParams) ApplyAutoThreadParams();
                }
            }
        }

        public Brush ThreadGentleBg => _threadIntensity == FeedIntensityKind.Gentle ? ActiveBtnBg : InactiveBtnBg;
        public Brush ThreadGentleFg => _threadIntensity == FeedIntensityKind.Gentle ? ActiveBtnFg : InactiveBtnFg;
        public Brush ThreadOptimalBg => _threadIntensity == FeedIntensityKind.Optimal ? ActiveBtnBg : InactiveBtnBg;
        public Brush ThreadOptimalFg => _threadIntensity == FeedIntensityKind.Optimal ? ActiveBtnFg : InactiveBtnFg;
        public Brush ThreadAggressiveBg => _threadIntensity == FeedIntensityKind.Aggressive ? ActiveBtnBg : InactiveBtnBg;
        public Brush ThreadAggressiveFg => _threadIntensity == FeedIntensityKind.Aggressive ? ActiveBtnFg : InactiveBtnFg;
        public string ThreadIntensityNote => _threadIntensity switch
        {
            FeedIntensityKind.Gentle => "Щадящий режим: сниженные обороты и подача для вязких сталей, глухих отверстий и тонких резьбофрез.",
            FeedIntensityKind.Aggressive => "Форсированный режим: максимальная производительность для жёстких фрез с внутренним подводом СОЖ.",
            _ => "Оптимальный режим: сбалансированная стойкость профиля резьбы и чистота витков."
        };
        private double _threadEdgeFeed = 320.0;
        private string _threadEdgeFeedText = "320";
        public string ThreadEdgeFeedText
        {
            get => _threadEdgeFeedText;
            set
            {
                if (SetProperty(ref _threadEdgeFeedText, value) && TryParseDecimal(value, out double val))
                {
                    _threadEdgeFeed = val;
                    RecalculateMillingThread();
                }
            }
        }
        private int _threadSpindleRpm = 3200;
        private string _threadSpindleRpmText = "3200";
        public string ThreadSpindleRpmText
        {
            get => _threadSpindleRpmText;
            set
            {
                if (SetProperty(ref _threadSpindleRpmText, value) && int.TryParse(value, out int val))
                {
                    _threadSpindleRpm = val;
                    RecalculateMillingThread();
                }
            }
        }
        private void ApplyAutoThreadParams()
        {
            if (SelectedThreadIsoGroup == null) return;
            double km = SelectedThreadIsoGroup.MachinabilityFactor;
            double intensityFactor = _threadIntensity switch
            {
                FeedIntensityKind.Gentle => 0.75,
                FeedIntensityKind.Aggressive => 1.25,
                _ => 1.00
            };
            if (!IsThreadMillingMode)
            {
                // Автоподбор скорости резания Vc (м/мин) для машинного метчика HSS-E
                double baseTapVc = SelectedThreadIsoGroup.Code switch
                {
                    IsoGroupCode.P => 14.0,
                    IsoGroupCode.M => 7.0,
                    IsoGroupCode.K => 16.0,
                    IsoGroupCode.N => 25.0,
                    IsoGroupCode.S => 4.5,
                    IsoGroupCode.H => 4.0,
                    _ => 12.0
                };
                double vc = baseTapVc * Math.Sqrt(km) * intensityFactor;
                double d = _threadDiameter > 0.5 ? _threadDiameter : 10.0;
                int rpm = (int)Math.Clamp(Math.Round((1000.0 * vc) / (Math.PI * d)), 30, 3000);
                _threadSpindleRpm = rpm;
                _threadSpindleRpmText = rpm.ToString(CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(ThreadSpindleRpmText));
            }
            else
            {
                // Автоподбор для твёрдосплавной резьбофрезы
                var (baseVc, baseFz) = SelectedThreadIsoGroup.Code switch
                {
                    IsoGroupCode.P => (110.0, 0.035),
                    IsoGroupCode.M => (65.0, 0.025),
                    IsoGroupCode.K => (125.0, 0.042),
                    IsoGroupCode.N => (240.0, 0.055),
                    IsoGroupCode.S => (35.0, 0.018),
                    IsoGroupCode.H => (45.0, 0.015),
                    _ => (95.0, 0.030)
                };
                double dia = _threadMillDiameter > 0.5 ? _threadMillDiameter : 8.0;
                int z = Math.Clamp(_threadMillFlutes, 1, 12);
                double vc = baseVc * Math.Sqrt(km) * intensityFactor;
                int rpm = (int)Math.Clamp(Math.Round((1000.0 * vc) / (Math.PI * dia)), 100, 24000);
                double fz = baseFz * Math.Clamp(dia / 8.0, 0.4, 1.5) * intensityFactor;
                double vf = Math.Round(rpm * z * fz, 0);
                _threadSpindleRpm = rpm;
                _threadSpindleRpmText = rpm.ToString(CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(ThreadSpindleRpmText));
                _threadEdgeFeed = vf;
                _threadEdgeFeedText = vf.ToString("0", CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(ThreadEdgeFeedText));
            }
            RecalculateMillingThread();
        }
        private MillingThreadCalculationResult _millingThreadResult = new();
        public MillingThreadCalculationResult MillingThreadResult
        {
            get => _millingThreadResult;
            private set => SetProperty(ref _millingThreadResult, value);
        }
        private void RecalculateMillingThread()
        {
            MillingThreadResult = _millingGeometryService.CalculateMillingThread(
            IsThreadMillingMode,
            IsExternalMillingThread,
            IsLeftHandMillingThread,
            _threadDiameter,
            _threadPitch,
            _threadMillDiameter,
            _threadMillFlutes,
            _threadCenterX,
            _threadCenterY,
            _threadStartZ,
            _threadTargetZ,
            _threadClearanceR,
            _threadEdgeFeed,
            _threadSpindleRpm);
        }

        // =====================================================================
        // КАРТОЧКА 4: ВЫСОТА ГРЕБЕШКА 3D-ФРЕЗЕРОВАНИЯ (SCALLOP HEIGHT)
        // =====================================================================

        private bool _isByStepoverInput;
        /// <summary>false — расчёт шага ae по целевой чистоте (h / Ra), true — проверочный расчёт h и Ra по шагу ae.</summary>
        public bool IsByStepoverInput
        {
            get => _isByStepoverInput;
            set
            {
                if (SetProperty(ref _isByStepoverInput, value))
                {
                    OnPropertyChanged(nameof(IsByTargetRoughnessInput));
                    OnPropertyChanged(nameof(Card4BadgeText));
                    RecalculateScallop();
                }
            }
        }

        public bool IsByTargetRoughnessInput => !_isByStepoverInput;
        public string Card4BadgeText => IsByStepoverInput ? "Режим: По шагу ae → чистота" : "Режим: По чистоте → шаг ae";

        private bool _isTargetRaMode;
        /// <summary>false — ввод высоты гребешка h (мкм), true — ввод шероховатости Ra (мкм).</summary>
        public bool IsTargetRaMode
        {
            get => _isTargetRaMode;
            set
            {
                if (SetProperty(ref _isTargetRaMode, value))
                {
                    OnPropertyChanged(nameof(ScallopTargetInputHint));
                    RecalculateScallop();
                }
            }
        }

        public string ScallopTargetInputHint => IsTargetRaMode
            ? "Требуемая шероховатость Ra (мкм)"
            : "Допустимая высота гребешка h (мкм)";

        private bool _isBullNoseMillingTool;
        /// <summary>false — Сферическая фреза (Ball Nose, r = D/2), true — Торовая фреза (Bull Nose, r < D/2).</summary>
        public bool IsBullNoseMillingTool
        {
            get => _isBullNoseMillingTool;
            set
            {
                if (SetProperty(ref _isBullNoseMillingTool, value))
                {
                    OnPropertyChanged(nameof(IsBallNoseMillingTool));
                    RecalculateScallop();
                }
            }
        }

        public bool IsBallNoseMillingTool => !_isBullNoseMillingTool;

        private double _scallopToolDiameter = 10.0;
        private string _scallopToolDiameterText = "10";
        public string ScallopToolDiameterText
        {
            get => _scallopToolDiameterText;
            set
            {
                if (SetProperty(ref _scallopToolDiameterText, value) && TryParseDecimal(value, out double val))
                {
                    _scallopToolDiameter = val;
                    RecalculateScallop();
                }
            }
        }

        private double _scallopCornerRadius = 1.5;
        private string _scallopCornerRadiusText = "1.5";
        public string ScallopCornerRadiusText
        {
            get => _scallopCornerRadiusText;
            set
            {
                if (SetProperty(ref _scallopCornerRadiusText, value) && TryParseDecimal(value, out double val))
                {
                    _scallopCornerRadius = val;
                    RecalculateScallop();
                }
            }
        }

        private double _scallopTargetMicrons = 5.0;
        private string _scallopTargetMicronsText = "5";
        public string ScallopTargetMicronsText
        {
            get => _scallopTargetMicronsText;
            set
            {
                if (SetProperty(ref _scallopTargetMicronsText, value) && TryParseDecimal(value, out double val))
                {
                    _scallopTargetMicrons = val;
                    RecalculateScallop();
                }
            }
        }

        private double _scallopInputStepoverAe = 0.35;
        private string _scallopInputStepoverAeText = "0.35";
        public string ScallopInputStepoverAeText
        {
            get => _scallopInputStepoverAeText;
            set
            {
                if (SetProperty(ref _scallopInputStepoverAeText, value) && TryParseDecimal(value, out double val))
                {
                    _scallopInputStepoverAe = val;
                    RecalculateScallop();
                }
            }
        }

        private double _scallopFeedPerMin = 1200.0;
        private string _scallopFeedPerMinText = "1200";
        public string ScallopFeedPerMinText
        {
            get => _scallopFeedPerMinText;
            set
            {
                if (SetProperty(ref _scallopFeedPerMinText, value) && TryParseDecimal(value, out double val))
                {
                    _scallopFeedPerMin = val;
                    RecalculateScallop();
                }
            }
        }

        private ScallopCalculationResult _scallopResult = new();
        public ScallopCalculationResult ScallopResult
        {
            get => _scallopResult;
            private set => SetProperty(ref _scallopResult, value);
        }

        private void RecalculateScallop()
        {
            ScallopResult = _millingGeometryService.CalculateScallopHeight(
                IsByTargetRoughnessInput,
                IsTargetRaMode,
                IsBallNoseMillingTool,
                _scallopToolDiameter,
                _scallopCornerRadius,
                _scallopTargetMicrons,
                _scallopInputStepoverAe,
                _scallopFeedPerMin);
        }

    }
}