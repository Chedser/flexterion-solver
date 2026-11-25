using System.Windows.Media;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using MechCore.Core;
using MechCore.Models.Geometry;
using MechCore.Services.Geometry;
using System.Linq;
using MechCore.Models.Materials;
using MechCore.Models.Turning;
using MechCore.Services.Materials;
using MechCore.Services.Turning;


namespace MechCore.ViewModels.Geometry;

public class GeometryViewModel : ObservableObject
{
    private readonly IMaterialService _materialService;
    private readonly ITurningCuttingService _cuttingService;
    private readonly List<MaterialGradeItem> _allMaterialGrades;

    public IReadOnlyList<IsoMaterialGroupItem> IsoGroups { get; }
    public ICommand SelectChamferIsoGroupCommand { get; }
    public ICommand SelectChamferIntensityCommand { get; }

    public ICommand ClearChamferGradeCommand { get; }

    private readonly IGeometryService _geometryService;

    public MainViewModel Main { get; }

    public IReadOnlyList<double> ChamferAnglePresets { get; }
  //  public IReadOnlyList<DrillAngleItem> DrillAnglePresets { get; }

    public ICommand SelectChamferAngleCommand { get; }
    public ICommand CopyChamferGCodeCommand { get; }
  //  public ICommand CopyCncDrillDepthCommand { get; }
    public ICommand SelectDrillAngleCommand { get; }
    public ICommand CopyHoleG81Command { get; }
    public ICommand CopyHoleG83Command { get; }
    public ICommand SelectHoleIsoGroupCommand { get; }
    public ICommand SelectHoleIntensityCommand { get; }

    public ICommand ClearHoleGradeCommand { get; }

    public ICommand SelectConeSolveKindCommand { get; }
    public ICommand SelectConicityPresetCommand { get; }
    public ICommand CopyConeGCodeCommand { get; }
    public ICommand SelectConeIsoGroupCommand { get; }
    public ICommand SelectConeIntensityCommand { get; }

    public ICommand ClearConeGradeCommand { get; }

    public ICommand SelectTriangleKindCommand { get; }
    public ICommand CopyGrooveStandardGCodeCommand { get; }
    public ICommand CopyGrooveMacroGCodeCommand { get; }
    public ICommand SelectGrooveIsoGroupCommand { get; }
    public ICommand SelectGrooveToolWidthCommand { get; }
    public ICommand SelectGrooveIntensityCommand { get; }

    public ICommand CopyBlendGCodeRadiusCommand { get; }
    public ICommand CopyBlendGCodeIKCommand { get; }
    public ICommand SelectBlendIsoGroupCommand { get; }
    public ICommand SelectBlendIntensityCommand { get; }
    public ICommand ClearBlendGradeCommand { get; }
    public ICommand SelectBlendRadiusCommand { get; }
    public ICommand ClearGrooveGradeCommand { get; }

    private static readonly Brush ActiveAngleBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));
    private static readonly Brush InactiveAngleBg = Brushes.Transparent;
    private static readonly Brush ActiveAngleFg = Brushes.White;
    private static readonly Brush InactiveAngleFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));

    private double _selectedChamferAngle = 45.0;
    public double SelectedChamferAngle
    {
        get => _selectedChamferAngle;
        set
        {
            if (SetProperty(ref _selectedChamferAngle, value))
            {
                OnPropertyChanged(nameof(Angle15Bg)); OnPropertyChanged(nameof(Angle15Fg));
                OnPropertyChanged(nameof(Angle20Bg)); OnPropertyChanged(nameof(Angle20Fg));
                OnPropertyChanged(nameof(Angle30Bg)); OnPropertyChanged(nameof(Angle30Fg));
                OnPropertyChanged(nameof(Angle45Bg)); OnPropertyChanged(nameof(Angle45Fg));
                OnPropertyChanged(nameof(Angle60Bg)); OnPropertyChanged(nameof(Angle60Fg));
                RecalculateChamfer();
            }
        }
    }
    public ICommand SelectArcRadiusCommand { get; }
    public ICommand SelectArcInputModeCommand { get; }
    public ICommand CopyArcGCodeRadiusCommand { get; }
    public ICommand CopyArcGCodeIKCommand { get; }
    public ICommand SelectArcIsoGroupCommand { get; }
    public ICommand SelectArcIntensityCommand { get; }

    public ICommand ClearArcGradeCommand { get; }

    public Brush Angle15Bg => System.Math.Abs(_selectedChamferAngle - 15.0) < 0.01 ? ActiveAngleBg : InactiveAngleBg;
    public Brush Angle15Fg => System.Math.Abs(_selectedChamferAngle - 15.0) < 0.01 ? ActiveAngleFg : InactiveAngleFg;

    public Brush Angle20Bg => System.Math.Abs(_selectedChamferAngle - 20.0) < 0.01 ? ActiveAngleBg : InactiveAngleBg;
    public Brush Angle20Fg => System.Math.Abs(_selectedChamferAngle - 20.0) < 0.01 ? ActiveAngleFg : InactiveAngleFg;

    public Brush Angle30Bg => System.Math.Abs(_selectedChamferAngle - 30.0) < 0.01 ? ActiveAngleBg : InactiveAngleBg;
    public Brush Angle30Fg => System.Math.Abs(_selectedChamferAngle - 30.0) < 0.01 ? ActiveAngleFg : InactiveAngleFg;

    public Brush Angle45Bg => System.Math.Abs(_selectedChamferAngle - 45.0) < 0.01 ? ActiveAngleBg : InactiveAngleBg;
    public Brush Angle45Fg => System.Math.Abs(_selectedChamferAngle - 45.0) < 0.01 ? ActiveAngleFg : InactiveAngleFg;

    public Brush Angle60Bg => System.Math.Abs(_selectedChamferAngle - 60.0) < 0.01 ? ActiveAngleBg : InactiveAngleBg;
    public Brush Angle60Fg => System.Math.Abs(_selectedChamferAngle - 60.0) < 0.01 ? ActiveAngleFg : InactiveAngleFg;

    public GeometryViewModel(
        MainViewModel main,
        IGeometryService? geometryService = null,
        IMaterialService? materialService = null,
        ITurningCuttingService? cuttingService = null)
    {
        Main = main;
        _geometryService = geometryService ?? new GeometrySolverService();
        _materialService = materialService ?? new MaterialStandardService();
        _cuttingService = cuttingService ?? new TurningCuttingService();

         IsoGroups = _materialService.GetIsoGroups();
        _allMaterialGrades = _materialService.GetAllGrades();
        _selectedChamferIsoGroup = IsoGroups.First();

        SelectChamferIsoGroupCommand = new RelayCommand(param =>
        {
            if (param is IsoMaterialGroupItem group)
                SelectedChamferIsoGroup = group;
        });

        SelectChamferIntensityCommand = new RelayCommand(param =>
        {
            if (param?.ToString() is string s && System.Enum.TryParse(s, out FeedIntensityKind kind))
                ChamferIntensity = kind;
        });

        ClearChamferGradeCommand = new RelayCommand(_ => SelectedChamferMaterial = null);

        UpdateFilteredChamferGrades();

        _selectedArcIsoGroup = IsoGroups.First();

        SelectArcIsoGroupCommand = new RelayCommand(param =>
        {
            if (param is IsoMaterialGroupItem group)
                SelectedArcIsoGroup = group;
        });

        SelectArcIntensityCommand = new RelayCommand(param =>
        {
            if (param?.ToString() is string s && System.Enum.TryParse(s, out FeedIntensityKind kind))
                ArcIntensity = kind;
        });

        ClearArcGradeCommand = new RelayCommand(_ => SelectedArcMaterial = null);

        UpdateFilteredArcGrades();

        _selectedHoleIsoGroup = IsoGroups.First();

        SelectHoleIsoGroupCommand = new RelayCommand(param =>
        {
            if (param is IsoMaterialGroupItem group)
                SelectedHoleIsoGroup = group;
        });

        SelectHoleIntensityCommand = new RelayCommand(param =>
        {
            if (param?.ToString() is string s && System.Enum.TryParse(s, out FeedIntensityKind kind))
                HoleIntensity = kind;
        });

        ClearHoleGradeCommand = new RelayCommand(_ => SelectedHoleMaterial = null);

        UpdateFilteredHoleGrades();

        _selectedConeIsoGroup = IsoGroups.First();

        SelectConeIsoGroupCommand = new RelayCommand(param =>
        {
            if (param is IsoMaterialGroupItem group)
                SelectedConeIsoGroup = group;
        });
        
        SelectConeIntensityCommand = new RelayCommand(param =>
        {
            if (param?.ToString() is string s && System.Enum.TryParse(s, out FeedIntensityKind kind))
                ConeIntensity = kind;
        });

        ClearConeGradeCommand = new RelayCommand(_ => SelectedConeMaterial = null);

        UpdateFilteredConeGrades();

        _selectedBlendIsoGroup = IsoGroups.First();

        SelectBlendIsoGroupCommand = new RelayCommand(param =>
        {
            if (param is IsoMaterialGroupItem group)
                SelectedBlendIsoGroup = group;
        });

        SelectBlendIntensityCommand = new RelayCommand(param =>
        {
            if (param?.ToString() is string s && System.Enum.TryParse(s, out FeedIntensityKind kind))
                BlendIntensity = kind;
        });

        ClearBlendGradeCommand = new RelayCommand(_ => SelectedBlendMaterial = null);

        SelectBlendRadiusCommand = new RelayCommand(param =>
        {
            if (param != null && TryParseDecimal(param.ToString(), out double r))
            {
                _blendRadius = r;
                _blendRadiusText = r.ToString("0.###", CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(BlendRadiusText));
                if (IsBlendMaterialSelectorActive) ApplyAutoBlendFeed();
                else RecalculateConeCylinderBlend();
            }
        });

        CopyBlendGCodeRadiusCommand = new RelayCommand(_ =>
        {
            if (BlendResult != null && BlendResult.IsValid && !string.IsNullOrEmpty(BlendResult.GCodeRadiusSnippet))
                Clipboard.SetText(BlendResult.GCodeRadiusSnippet);
        });

        CopyBlendGCodeIKCommand = new RelayCommand(_ =>
        {
            if (BlendResult != null && BlendResult.IsValid && !string.IsNullOrEmpty(BlendResult.GCodeIKSnippet))
                Clipboard.SetText(BlendResult.GCodeIKSnippet);
        });

        UpdateFilteredBlendGrades();

        _selectedGrooveIsoGroup = IsoGroups.First();

        SelectGrooveIsoGroupCommand = new RelayCommand(param =>
        {
            if (param is IsoMaterialGroupItem group)
                SelectedGrooveIsoGroup = group;
        });

        SelectGrooveToolWidthCommand = new RelayCommand(param =>
        {
            if (param != null && TryParseDecimal(param.ToString(), out double w))
            {
                _grooveToolWidth = w;
                _grooveToolWidthText = w.ToString("0.###", CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(GrooveToolWidthText));
                NotifyGrooveToolWidthButtons();
                if (AutoCalculateGrooveFeed) ApplyAutoGrooveFeed();
                else RecalculateGroove();
            }
        });

        SelectGrooveIntensityCommand = new RelayCommand(param =>
        {
            if (param?.ToString() is string s && System.Enum.TryParse(s, out FeedIntensityKind kind))
                GrooveIntensity = kind;
        });
        ClearGrooveGradeCommand = new RelayCommand(_ => SelectedGrooveMaterial = null);

        UpdateFilteredGrooveGrades();

        ChamferAnglePresets = new List<double> { 15.0, 20.0, 30.0, 45.0, 60.0 };
        //  DrillAnglePresets = _geometryService.GetDrillAnglePresets();

        SelectChamferAngleCommand = new RelayCommand(param =>
        {
            if (param is double angle)
            {
                SelectedChamferAngle = angle;
            }
            else if (param != null && TryParseDecimal(param.ToString(), out double parsed))
            {
                SelectedChamferAngle = parsed;
            }
        });

        CopyChamferGCodeCommand = new RelayCommand(_ =>
        {
            if (ChamferResult != null && ChamferResult.IsValid && !string.IsNullOrEmpty(ChamferResult.CncGCodeSnippet))
            {
                Clipboard.SetText(ChamferResult.CncGCodeSnippet);
            }
        });


        SelectArcRadiusCommand = new RelayCommand(param =>
        {
            if (param != null && TryParseDecimal(param.ToString(), out double r))
            {
                _arcRadius = r;
                _arcRadiusText = r.ToString("0.###", CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(ArcRadiusText));
                if (IsArcMaterialSelectorActive) ApplyAutoArcFeed();
                else RecalculateArc();

            }
        });

        SelectArcInputModeCommand = new RelayCommand(param =>
        {
            if (param?.ToString() is string modeStr && System.Enum.TryParse(modeStr, out ArcCoordinateInputMode mode))
            {
                ArcInputMode = mode;
            }
        });

        CopyArcGCodeRadiusCommand = new RelayCommand(_ =>
        {
            if (ArcResult != null && ArcResult.IsValid && !string.IsNullOrEmpty(ArcResult.GCodeRadiusSnippet))
                Clipboard.SetText(ArcResult.GCodeRadiusSnippet);
        });

        CopyArcGCodeIKCommand = new RelayCommand(_ =>
        {
            if (ArcResult != null && ArcResult.IsValid && !string.IsNullOrEmpty(ArcResult.GCodeIKSnippet))
                Clipboard.SetText(ArcResult.GCodeIKSnippet);
        });

        SelectDrillAngleCommand = new RelayCommand(param =>
        {
            string? tag = param?.ToString();
            if (tag == "Custom")
            {
                IsCustomDrillAngle = true;
            }
            else if (TryParseDecimal(tag, out double angle))
            {
                IsCustomDrillAngle = false;
                _drillTipAngle = angle;
                _customDrillAngleText = angle.ToString("0.##", CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(CustomDrillAngleText));
            }
            NotifyDrillAngleButtons();
            if (IsHoleMaterialSelectorActive) ApplyAutoHoleFeed();
            else RecalculateBlindHole();

        });

        CopyHoleG81Command = new RelayCommand(_ =>
        {
            if (HoleResult != null && HoleResult.IsValid && !string.IsNullOrEmpty(HoleResult.G81Snippet))
                Clipboard.SetText(HoleResult.G81Snippet);
        });

        CopyHoleG83Command = new RelayCommand(_ =>
        {
            if (HoleResult != null && HoleResult.IsValid && !string.IsNullOrEmpty(HoleResult.G83Snippet))
                Clipboard.SetText(HoleResult.G83Snippet);
        });

        SelectConeSolveKindCommand = new RelayCommand(param =>
        {
            if (param?.ToString() is string s && System.Enum.TryParse(s, out ConeSolveKind kind))
                ConeSolveKind = kind;
        });

        SelectConicityPresetCommand = new RelayCommand(param =>
        {
            if (param != null && TryParseDecimal(param.ToString(), out double ratioN) && ratioN > 0)
            {
                // Конусность 1:N -> tg(α) = 1 / (2 * N)
                double alphaDeg = System.Math.Atan(1.0 / (2.0 * ratioN)) * (180.0 / System.Math.PI);
                _coneSlopeAngle = alphaDeg;
                _coneSlopeAngleText = alphaDeg.ToString("0.####", CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(ConeSlopeAngleText));

                if (ConeSolveKind == ConeSolveKind.ByTwoDiametersAndLength)
                    ConeSolveKind = ConeSolveKind.ByStartDiameterLengthAndAngle;
                else if (IsConeMaterialSelectorActive)
                    ApplyAutoConeFeed();
                else
                    RecalculateConeOrTriangle();
            }
        });

        CopyConeGCodeCommand = new RelayCommand(_ =>
        {
            if (TriangleResult != null && TriangleResult.IsValid && !TriangleResult.IsTriangleMode && !string.IsNullOrEmpty(TriangleResult.ConeGCodeSnippet))
                Clipboard.SetText(TriangleResult.ConeGCodeSnippet);
        });

        SelectTriangleKindCommand = new RelayCommand(param =>
        {
            if (param?.ToString() is string s && System.Enum.TryParse(s, out TriangleSolveKind kind))
                SelectedTriangleKind = kind;
        });

        CopyGrooveStandardGCodeCommand = new RelayCommand(_ =>
        {
            if (GrooveResult != null && GrooveResult.IsValid && !string.IsNullOrEmpty(GrooveResult.StandardGCodeSnippet))
                Clipboard.SetText(GrooveResult.StandardGCodeSnippet);
        });

        CopyGrooveMacroGCodeCommand = new RelayCommand(_ =>
        {
            if (GrooveResult != null && GrooveResult.IsValid && !string.IsNullOrEmpty(GrooveResult.MacroGCodeSnippet))
                Clipboard.SetText(GrooveResult.MacroGCodeSnippet);
        });

        RecalculateGroove();
        RecalculateConeOrTriangle();
        RecalculateArc();
        RecalculateChamfer();
        RecalculateBlindHole();
        RecalculateConeCylinderBlend();
    }

    // =========================================================================
    // РАЗДЕЛ 1: РАСЧЁТ ТОКАРНЫХ ФАСОК (КАРТОЧКА 1)
    // =========================================================================

    private FeedIntensityKind _chamferIntensity = FeedIntensityKind.Optimal;
    public FeedIntensityKind ChamferIntensity
    {
        get => _chamferIntensity;
        set
        {
            if (SetProperty(ref _chamferIntensity, value))
            {
                OnPropertyChanged(nameof(ChamferGentleBg)); OnPropertyChanged(nameof(ChamferGentleFg));
                OnPropertyChanged(nameof(ChamferOptimalBg)); OnPropertyChanged(nameof(ChamferOptimalFg));
                OnPropertyChanged(nameof(ChamferAggressiveBg)); OnPropertyChanged(nameof(ChamferAggressiveFg));
                OnPropertyChanged(nameof(ChamferIntensityNote));
                if (IsChamferMaterialSelectorActive)
                    ApplyAutoChamferFeed();
            }
        }
    }

    public Brush ChamferGentleBg => _chamferIntensity == FeedIntensityKind.Gentle ? ActiveAngleBg : InactiveAngleBg;
    public Brush ChamferGentleFg => _chamferIntensity == FeedIntensityKind.Gentle ? ActiveAngleFg : InactiveAngleFg;

    public Brush ChamferOptimalBg => _chamferIntensity == FeedIntensityKind.Optimal ? ActiveAngleBg : InactiveAngleBg;
    public Brush ChamferOptimalFg => _chamferIntensity == FeedIntensityKind.Optimal ? ActiveAngleFg : InactiveAngleFg;

    public Brush ChamferAggressiveBg => _chamferIntensity == FeedIntensityKind.Aggressive ? ActiveAngleBg : InactiveAngleBg;
    public Brush ChamferAggressiveFg => _chamferIntensity == FeedIntensityKind.Aggressive ? ActiveAngleFg : InactiveAngleFg;
    public string ChamferIntensityNote => _chamferIntensity switch
    {
        FeedIntensityKind.Gentle => "Щадящий режим (нижняя граница каталога): для нежёстких деталей, тонких стенок и максимальной стойкости пластины.",
        FeedIntensityKind.Aggressive => "Форсированный режим (верхняя граница каталога): для жёсткого крепления, мощных станков и сокращения времени цикла.",
        _ => "Оптимальный режим (середина диапазона): сбалансированное соотношение стойкости кромки, чистоты и времени."
    };

    private int _chamferSpindleRpm = 1200;
    private string _chamferSpindleRpmText = "1200";
    public string ChamferSpindleRpmText
    {
        get => _chamferSpindleRpmText;
        set
        {
            if (SetProperty(ref _chamferSpindleRpmText, value) && int.TryParse(value, out int val) && val > 0)
            {
                _chamferSpindleRpm = val;
                RecalculateChamfer();
            }
        }
    }

    private bool _isChamferDecreasingX;
    /// <summary>false — На увеличение X (по умолчанию), true — На уменьшение X.</summary>
    public bool IsChamferDecreasingX
    {
        get => _isChamferDecreasingX;
        set { if (SetProperty(ref _isChamferDecreasingX, value)) RecalculateChamfer(); }
    }

    private bool _isChamferLeftToRight;
    /// <summary>false — Справа налево (по умолчанию), true — Слева направо.</summary>
    public bool IsChamferLeftToRight
    {
        get => _isChamferLeftToRight;
        set { if (SetProperty(ref _isChamferLeftToRight, value)) RecalculateChamfer(); }
    }

    private bool _isChamferHole;
    /// <summary>false — Вал (по умолчанию), true — Отверстие.</summary>
    public bool IsChamferHole
    {
        get => _isChamferHole;
        set
        {
            if (SetProperty(ref _isChamferHole, value))
            {
                if (IsChamferMaterialSelectorActive) ApplyAutoChamferFeed();
                else RecalculateChamfer();
            }
        }
    }

    private double _chamferAxialLeg = 2.0;
    private string _chamferAxialLegText = "2";
    public string ChamferAxialLegText
    {
        get => _chamferAxialLegText;
        set
        {
            if (SetProperty(ref _chamferAxialLegText, value) && TryParseDecimal(value, out double val))
            {
                _chamferAxialLeg = val;
                if (IsChamferMaterialSelectorActive) ApplyAutoChamferFeed();
                else RecalculateChamfer();
            }
        }
    }

    private double _chamferStartX = 0.0;
    private string _chamferStartXText = "0";
    public string ChamferStartXText
    {
        get => _chamferStartXText;
        set
        {
            if (SetProperty(ref _chamferStartXText, value) && TryParseDecimal(value, out double val))
            {
                _chamferStartX = val;
                if (IsChamferMaterialSelectorActive) ApplyAutoChamferFeed();
                else RecalculateChamfer();
            }
        }
    }

    private double _chamferStartZ = 0.0;
    private string _chamferStartZText = "0";
    public string ChamferStartZText
    {
        get => _chamferStartZText;
        set
        {
            if (SetProperty(ref _chamferStartZText, value) && TryParseDecimal(value, out double val))
            {
                _chamferStartZ = val;
                RecalculateChamfer();
            }
        }
    }

    private bool _useChamferApproach = true;
    public bool UseChamferApproach
    {
        get => _useChamferApproach;
        set { if (SetProperty(ref _useChamferApproach, value)) RecalculateChamfer(); }
    }

    private double _chamferApproachZ = 0.0;
    private string _chamferApproachZText = "0";
    public string ChamferApproachZText
    {
        get => _chamferApproachZText;
        set
        {
            if (SetProperty(ref _chamferApproachZText, value) && TryParseDecimal(value, out double val))
            {
                _chamferApproachZ = val;
                RecalculateChamfer();
            }
        }
    }

    private ChamferCalculationResult _chamferResult = new();
    public ChamferCalculationResult ChamferResult
    {
        get => _chamferResult;
        private set => SetProperty(ref _chamferResult, value);
    }

    private bool _useChamferFeed = true;
    /// <summary>Учитывать рабочую подачу F в G-коде.</summary>
    public bool UseChamferFeed
    {
        get => _useChamferFeed;
        set
        {
            if (SetProperty(ref _useChamferFeed, value))
            {
                OnPropertyChanged(nameof(IsChamferFeedManualEditable));
                OnPropertyChanged(nameof(IsChamferMaterialSelectorActive));
                if (value && AutoCalculateChamferFeed)
                    ApplyAutoChamferFeed();
                else
                    RecalculateChamfer();
            }
        }
    }

    private bool _autoCalculateChamferFeed;
    /// <summary>Автоматически подбирать подачу F по выбранному материалу.</summary>
    public bool AutoCalculateChamferFeed
    {
        get => _autoCalculateChamferFeed;
        set
        {
            if (SetProperty(ref _autoCalculateChamferFeed, value))
            {
                OnPropertyChanged(nameof(IsChamferFeedManualEditable));
                OnPropertyChanged(nameof(IsChamferMaterialSelectorActive));
                OnPropertyChanged(nameof(ChamferFeedAutoNote));
                if (value)
                    ApplyAutoChamferFeed();
                else
                    RecalculateChamfer();
            }
        }
    }

    public bool IsChamferFeedManualEditable => UseChamferFeed && !AutoCalculateChamferFeed;
    public bool IsChamferMaterialSelectorActive => UseChamferFeed && AutoCalculateChamferFeed;
      
    private IsoMaterialGroupItem _selectedChamferIsoGroup;
    public IsoMaterialGroupItem SelectedChamferIsoGroup
    {
        get => _selectedChamferIsoGroup;
        set
        {
            if (value != null && SetProperty(ref _selectedChamferIsoGroup, value))
            {
                _selectedChamferMaterial = null;
                OnPropertyChanged(nameof(SelectedChamferMaterial));
                OnPropertyChanged(nameof(HasSelectedChamferMaterial));
                UpdateFilteredChamferGrades();
                OnPropertyChanged(nameof(ChamferFeedAutoNote));
                if (IsChamferMaterialSelectorActive)
                    ApplyAutoChamferFeed();
            }
        }
    }

    private List<MaterialGradeItem> _filteredChamferGrades = new();
    public List<MaterialGradeItem> FilteredChamferGrades
    {
        get => _filteredChamferGrades;
        private set => SetProperty(ref _filteredChamferGrades, value);
    }

    private MaterialGradeItem? _selectedChamferMaterial;
    public MaterialGradeItem? SelectedChamferMaterial
    {
        get => _selectedChamferMaterial;
        set
        {
            if (SetProperty(ref _selectedChamferMaterial, value))
            {
                OnPropertyChanged(nameof(HasSelectedChamferMaterial));
                OnPropertyChanged(nameof(ChamferFeedAutoNote));
                if (IsChamferMaterialSelectorActive)
                    ApplyAutoChamferFeed();
            }
        }
    }

    public bool HasSelectedChamferMaterial => SelectedChamferMaterial != null;

    private void UpdateFilteredChamferGrades()
    {
        if (SelectedChamferIsoGroup == null)
        {
            FilteredChamferGrades = new List<MaterialGradeItem>();
            return;
        }

        FilteredChamferGrades = _allMaterialGrades
            .Where(g => g.IsoGroup == SelectedChamferIsoGroup.Code)
            .ToList();
    }

    private double _chamferFeedRate = 0.12;
    private string _chamferFeedRateText = "0.12";
    public string ChamferFeedRateText
    {
        get => _chamferFeedRateText;
        set
        {
            if (SetProperty(ref _chamferFeedRateText, value) && TryParseDecimal(value, out double val))
            {
                _chamferFeedRate = val;
                RecalculateChamfer();
            }
        }
    }

    public string ChamferFeedAutoNote
    {
        get
        {
            if (!AutoCalculateChamferFeed || SelectedChamferIsoGroup == null)
                return string.Empty;

            string matText = SelectedChamferMaterial != null
                ? $"{SelectedChamferMaterial.GostGrade} (ISO {SelectedChamferIsoGroup.CodeLetter})"
                : $"группы ISO {SelectedChamferIsoGroup.CodeLetter} ({SelectedChamferIsoGroup.Name})";

            string opText = IsChamferHole ? "внутренней фаски (растачивание)" : "наружной фаски";
            string stageText = _chamferAxialLeg <= 2.0 ? "чистовой режим (c ≤ 2 мм)" : "получистовой режим (c > 2 мм)";
            return $"Автоподбор для {matText}: {opText}, {stageText}.";
        }
    }

    private void ApplyAutoChamferFeed()
    {
        if (SelectedChamferIsoGroup == null) return;

        var operation = IsChamferHole
            ? TurningOperationKind.InternalBoring
            : TurningOperationKind.ExternalTurning;

        var stage = _chamferAxialLeg <= 2.0
            ? CuttingStageKind.Finishing
            : CuttingStageKind.SemiFinishing;

        double refDia = _chamferStartX > 1.0 ? _chamferStartX : 40.0;

        var rec = _cuttingService.QuickRecommend(
            SelectedChamferIsoGroup,
            SelectedChamferMaterial,
            operation,
            stage,
            refDia);

        if (rec.IsValid && rec.RecommendedFeedPerRev > 0)
        {
            double selectedFeed = _chamferIntensity switch
            {
                FeedIntensityKind.Gentle => rec.FeedPerRevMin,
                FeedIntensityKind.Aggressive => rec.FeedPerRevMax,
                _ => rec.RecommendedFeedPerRev
            };

            double selectedVc = _chamferIntensity switch
            {
                FeedIntensityKind.Gentle => rec.VcMin,
                FeedIntensityKind.Aggressive => rec.VcMax,
                _ => rec.RecommendedVc
            };

            int calculatedRpm = (int)System.Math.Clamp(
                System.Math.Round((1000.0 * selectedVc) / (System.Math.PI * refDia)),
                20, 12000);

            _chamferSpindleRpm = calculatedRpm;
            _chamferSpindleRpmText = calculatedRpm.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(ChamferSpindleRpmText));

            _chamferFeedRate = selectedFeed;
            _chamferFeedRateText = selectedFeed.ToString("0.###", CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(ChamferFeedRateText));
            OnPropertyChanged(nameof(ChamferFeedAutoNote));
        }

        RecalculateChamfer();
    }

    // =========================================================================
    // РАЗДЕЛ 2: КРУГОВАЯ ИНТЕРПОЛЯЦИЯ G02 / G03 (КАРТОЧКА 2)
    // =========================================================================
    private FeedIntensityKind _arcIntensity = FeedIntensityKind.Optimal;
    public FeedIntensityKind ArcIntensity
    {
        get => _arcIntensity;
        set
        {
            if (SetProperty(ref _arcIntensity, value))
            {
                OnPropertyChanged(nameof(ArcGentleBg)); OnPropertyChanged(nameof(ArcGentleFg));
                OnPropertyChanged(nameof(ArcOptimalBg)); OnPropertyChanged(nameof(ArcOptimalFg));
                OnPropertyChanged(nameof(ArcAggressiveBg)); OnPropertyChanged(nameof(ArcAggressiveFg));
                OnPropertyChanged(nameof(ArcIntensityNote));
                if (IsArcMaterialSelectorActive)
                    ApplyAutoArcFeed();
            }
        }
    }

    public Brush ArcGentleBg => _arcIntensity == FeedIntensityKind.Gentle ? ActiveAngleBg : InactiveAngleBg;
    public Brush ArcGentleFg => _arcIntensity == FeedIntensityKind.Gentle ? ActiveAngleFg : InactiveAngleFg;
    public Brush ArcOptimalBg => _arcIntensity == FeedIntensityKind.Optimal ? ActiveAngleBg : InactiveAngleBg;
    public Brush ArcOptimalFg => _arcIntensity == FeedIntensityKind.Optimal ? ActiveAngleFg : InactiveAngleFg;
    public Brush ArcAggressiveBg => _arcIntensity == FeedIntensityKind.Aggressive ? ActiveAngleBg : InactiveAngleBg;
    public Brush ArcAggressiveFg => _arcIntensity == FeedIntensityKind.Aggressive ? ActiveAngleFg : InactiveAngleFg;
    public string ArcIntensityNote => _arcIntensity switch
    {
        FeedIntensityKind.Gentle => "Щадящий режим (нижняя граница каталога): для плавного прохода дуги без дробления и высокой чистоты радиуса.",
        FeedIntensityKind.Aggressive => "Форсированный режим (верхняя граница каталога): для жёстких заготовок и сокращения времени прохода дуги.",
        _ => "Оптимальный режим (середина диапазона): сбалансированное соотношение чистоты профиля, стойкости кромки и времени."
    };

    private int _arcSpindleRpm = 1200;
    private string _arcSpindleRpmText = "1200";
    public string ArcSpindleRpmText
    {
        get => _arcSpindleRpmText;
        set
        {
            if (SetProperty(ref _arcSpindleRpmText, value) && int.TryParse(value, out int val) && val > 0)
            {
                _arcSpindleRpm = val;
                RecalculateArc();
            }
        }
    }

    private bool _isArcConcaveFillet;
    /// <summary>false — Выпуклый радиус (по умолчанию), true — Вогнутая галтель.</summary>
    public bool IsArcConcaveFillet
    {
        get => _isArcConcaveFillet;
        set { if (SetProperty(ref _isArcConcaveFillet, value)) RecalculateArc(); }
    }

    private bool _isArcDecreasingX;
    /// <summary>false — На увеличение X (по умолчанию), true — На уменьшение X.</summary>
    public bool IsArcDecreasingX
    {
        get => _isArcDecreasingX;
        set { if (SetProperty(ref _isArcDecreasingX, value)) RecalculateArc(); }
    }

    private bool _isArcLeftToRight;
    /// <summary>false — Справа налево (по умолчанию), true — Слева направо.</summary>
    public bool IsArcLeftToRight
    {
        get => _isArcLeftToRight;
        set { if (SetProperty(ref _isArcLeftToRight, value)) RecalculateArc(); }
    }

    private bool _isArcHole;
    /// <summary>false — Вал (по умолчанию), true — Отверстие.</summary>
    public bool IsArcHole
    {
        get => _isArcHole;
        set
        {
            if (SetProperty(ref _isArcHole, value))
            {
                if (IsArcMaterialSelectorActive) ApplyAutoArcFeed();
                else RecalculateArc();
            }
        }
    }


    private bool _isArcFrontTurret;
    /// <summary>false — Задний резцедержатель (по умолчанию), true — Передний резцедержатель.</summary>
    public bool IsArcFrontTurret
    {
        get => _isArcFrontTurret;
        set { if (SetProperty(ref _isArcFrontTurret, value)) RecalculateArc(); }
    }

    private ArcCoordinateInputMode _arcInputMode = ArcCoordinateInputMode.ByCornerVertex;
    public ArcCoordinateInputMode ArcInputMode
    {
        get => _arcInputMode;
        set
        {
            if (SetProperty(ref _arcInputMode, value))
            {
                OnPropertyChanged(nameof(ArcInputXLabel));
                OnPropertyChanged(nameof(ArcInputZLabel));
                OnPropertyChanged(nameof(ModeCornerBg)); OnPropertyChanged(nameof(ModeCornerFg));
                OnPropertyChanged(nameof(ModeStartBg)); OnPropertyChanged(nameof(ModeStartFg));
                OnPropertyChanged(nameof(ModeEndBg)); OnPropertyChanged(nameof(ModeEndFg));
                RecalculateArc();
            }
        }
    }

    public Brush ModeCornerBg => _arcInputMode == ArcCoordinateInputMode.ByCornerVertex ? ActiveAngleBg : InactiveAngleBg;
    public Brush ModeCornerFg => _arcInputMode == ArcCoordinateInputMode.ByCornerVertex ? ActiveAngleFg : InactiveAngleFg;

    public Brush ModeStartBg => _arcInputMode == ArcCoordinateInputMode.ByStartPoint ? ActiveAngleBg : InactiveAngleBg;
    public Brush ModeStartFg => _arcInputMode == ArcCoordinateInputMode.ByStartPoint ? ActiveAngleFg : InactiveAngleFg;

    public Brush ModeEndBg => _arcInputMode == ArcCoordinateInputMode.ByEndPoint ? ActiveAngleBg : InactiveAngleBg;
    public Brush ModeEndFg => _arcInputMode == ArcCoordinateInputMode.ByEndPoint ? ActiveAngleFg : InactiveAngleFg;

    public string ArcInputXLabel => _arcInputMode switch
    {
        ArcCoordinateInputMode.ByStartPoint => "Диаметр начала X_нач (мм)",
        ArcCoordinateInputMode.ByEndPoint => "Диаметр конца X_кон (мм)",
        _ => "Диаметр вершины X_угла (мм)"
    };

    public string ArcInputZLabel => _arcInputMode switch
    {
        ArcCoordinateInputMode.ByStartPoint => "Координата Z_нач (мм)",
        ArcCoordinateInputMode.ByEndPoint => "Координата Z_кон (мм)",
        _ => "Координата Z_угла (мм)"
    };

    private double _arcRadius = 2.0;
    private string _arcRadiusText = "2";
    public string ArcRadiusText
    {
        get => _arcRadiusText;
        set
        {
            if (SetProperty(ref _arcRadiusText, value) && TryParseDecimal(value, out double val))
            {
                _arcRadius = val;
                if (IsArcMaterialSelectorActive) ApplyAutoArcFeed();
                else RecalculateArc();
            }
        }
    }

    private double _arcInputX = 40.0;
    private string _arcInputXText = "40";
    public string ArcInputXText
    {
        get => _arcInputXText;
        set
        {
            if (SetProperty(ref _arcInputXText, value) && TryParseDecimal(value, out double val))
            {
                _arcInputX = val;
                if (IsArcMaterialSelectorActive) ApplyAutoArcFeed();
                else RecalculateArc();
            }
        }
    }

    private double _arcInputZ = 0.0;
    private string _arcInputZText = "0";
    public string ArcInputZText
    {
        get => _arcInputZText;
        set
        {
            if (SetProperty(ref _arcInputZText, value) && TryParseDecimal(value, out double val))
            {
                _arcInputZ = val;
                RecalculateArc();
            }
        }
    }

    private ArcInterpolationResult _arcResult = new();
    public ArcInterpolationResult ArcResult
    {
        get => _arcResult;
        private set => SetProperty(ref _arcResult, value);
    }

    private bool _useArcFeed = true;
    /// <summary>Учитывать рабочую подачу F в кадрах G01 и G02/G03.</summary>
    public bool UseArcFeed
    {
        get => _useArcFeed;
        set
        {
            if (SetProperty(ref _useArcFeed, value))
            {
                OnPropertyChanged(nameof(IsArcFeedManualEditable));
                OnPropertyChanged(nameof(IsArcMaterialSelectorActive));
                if (value && AutoCalculateArcFeed)
                    ApplyAutoArcFeed();
                else
                    RecalculateArc();
            }
        }
    }

    private bool _autoCalculateArcFeed;
    /// <summary>Автоматически подбирать подачу F по выбранному материалу.</summary>
    public bool AutoCalculateArcFeed
    {
        get => _autoCalculateArcFeed;
        set
        {
            if (SetProperty(ref _autoCalculateArcFeed, value))
            {
                OnPropertyChanged(nameof(IsArcFeedManualEditable));
                OnPropertyChanged(nameof(IsArcMaterialSelectorActive));
                OnPropertyChanged(nameof(ArcFeedAutoNote));
                if (value)
                    ApplyAutoArcFeed();
                else
                    RecalculateArc();
            }
        }
    }

    public bool IsArcFeedManualEditable => UseArcFeed && !AutoCalculateArcFeed;
    public bool IsArcMaterialSelectorActive => UseArcFeed && AutoCalculateArcFeed;

    private IsoMaterialGroupItem _selectedArcIsoGroup;
    public IsoMaterialGroupItem SelectedArcIsoGroup
    {
        get => _selectedArcIsoGroup;
        set
        {
            if (value != null && SetProperty(ref _selectedArcIsoGroup, value))
            {
                _selectedArcMaterial = null;
                OnPropertyChanged(nameof(SelectedArcMaterial));
                OnPropertyChanged(nameof(HasSelectedArcMaterial));
                UpdateFilteredArcGrades();
                OnPropertyChanged(nameof(ArcFeedAutoNote));
                if (IsArcMaterialSelectorActive)
                    ApplyAutoArcFeed();
            }
        }
    }

    private List<MaterialGradeItem> _filteredArcGrades = new();
    public List<MaterialGradeItem> FilteredArcGrades
    {
        get => _filteredArcGrades;
        private set => SetProperty(ref _filteredArcGrades, value);
    }

    private MaterialGradeItem? _selectedArcMaterial;
    public MaterialGradeItem? SelectedArcMaterial
    {
        get => _selectedArcMaterial;
        set
        {
            if (SetProperty(ref _selectedArcMaterial, value))
            {
                OnPropertyChanged(nameof(HasSelectedArcMaterial));
                OnPropertyChanged(nameof(ArcFeedAutoNote));
                if (IsArcMaterialSelectorActive)
                    ApplyAutoArcFeed();
            }
        }
    }

    public bool HasSelectedArcMaterial => SelectedArcMaterial != null;

    private void UpdateFilteredArcGrades()
    {
        if (SelectedArcIsoGroup == null)
        {
            FilteredArcGrades = new List<MaterialGradeItem>();
            return;
        }

        FilteredArcGrades = _allMaterialGrades
            .Where(g => g.IsoGroup == SelectedArcIsoGroup.Code)
            .ToList();
    }

    private double _arcFeedRate = 0.12;
    private string _arcFeedRateText = "0.12";
    public string ArcFeedRateText
    {
        get => _arcFeedRateText;
        set
        {
            if (SetProperty(ref _arcFeedRateText, value) && TryParseDecimal(value, out double val))
            {
                _arcFeedRate = val;
                RecalculateArc();
            }
        }
    }
    public string ArcFeedAutoNote
    {
        get
        {
            if (!AutoCalculateArcFeed || SelectedArcIsoGroup == null)
                return string.Empty;

            string matText = SelectedArcMaterial != null
                ? $"{SelectedArcMaterial.GostGrade} (ISO {SelectedArcIsoGroup.CodeLetter})"
                : $"группы ISO {SelectedArcIsoGroup.CodeLetter} ({SelectedArcIsoGroup.Name})";

            string opText = IsArcHole ? "внутренней дуги (растачивание)" : "наружной дуги";
            string stageText = _arcRadius <= 2.0 ? "чистовой режим (R ≤ 2 мм)" : "получистовой режим (R > 2 мм)";
            return $"Автоподбор для {matText}: {opText}, {stageText}.";
        }
    }

    private void ApplyAutoArcFeed()
    {
        if (SelectedArcIsoGroup == null) return;

        var operation = IsArcHole
            ? TurningOperationKind.InternalBoring
            : TurningOperationKind.ExternalTurning;

        var stage = _arcRadius <= 2.0
            ? CuttingStageKind.Finishing
            : CuttingStageKind.SemiFinishing;

        double refDia = _arcInputX > 1.0 ? _arcInputX : 40.0;

        var rec = _cuttingService.QuickRecommend(
            SelectedArcIsoGroup,
            SelectedArcMaterial,
            operation,
            stage,
            refDia);

        if (rec.IsValid && rec.RecommendedFeedPerRev > 0)
        {
            double selectedFeed = _arcIntensity switch
            {
                FeedIntensityKind.Gentle => rec.FeedPerRevMin,
                FeedIntensityKind.Aggressive => rec.FeedPerRevMax,
                _ => rec.RecommendedFeedPerRev
            };

            double selectedVc = _arcIntensity switch
            {
                FeedIntensityKind.Gentle => rec.VcMin,
                FeedIntensityKind.Aggressive => rec.VcMax,
                _ => rec.RecommendedVc
            };

            int calculatedRpm = (int)System.Math.Clamp(
                System.Math.Round((1000.0 * selectedVc) / (System.Math.PI * refDia)),
                20, 12000);

            _arcSpindleRpm = calculatedRpm;
            _arcSpindleRpmText = calculatedRpm.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(ArcSpindleRpmText));

            _arcFeedRate = selectedFeed;
            _arcFeedRateText = selectedFeed.ToString("0.###", CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(ArcFeedRateText));
            OnPropertyChanged(nameof(ArcFeedAutoNote));
        }

        RecalculateArc();
    }
    private void RecalculateArc()
    {
        ArcResult = _geometryService.CalculateArcInterpolation(
            _arcRadius,
            _arcInputX,
            _arcInputZ,
            ArcInputMode,
            IsArcConcaveFillet,
            IsArcDecreasingX,
            IsArcLeftToRight,
            IsArcHole,
            IsArcFrontTurret,
            UseArcFeed,
            _arcFeedRate,
            _arcSpindleRpm);
    }

    // =========================================================================
    // РАЗДЕЛ 3: ГЛУХИЕ ОТВЕРСТИЯ, КОНУС СВЕРЛА И ЦИКЛЫ G81 / G83 (КАРТОЧКА 3)
    // =========================================================================
    private FeedIntensityKind _holeIntensity = FeedIntensityKind.Optimal;
    public FeedIntensityKind HoleIntensity
    {
        get => _holeIntensity;
        set
        {
            if (SetProperty(ref _holeIntensity, value))
            {
                OnPropertyChanged(nameof(HoleGentleBg)); OnPropertyChanged(nameof(HoleGentleFg));
                OnPropertyChanged(nameof(HoleOptimalBg)); OnPropertyChanged(nameof(HoleOptimalFg));
                OnPropertyChanged(nameof(HoleAggressiveBg)); OnPropertyChanged(nameof(HoleAggressiveFg));
                OnPropertyChanged(nameof(HoleIntensityNote));
                if (IsHoleMaterialSelectorActive)
                    ApplyAutoHoleFeed();
            }
        }
    }
    public Brush HoleGentleBg => _holeIntensity == FeedIntensityKind.Gentle ? ActiveAngleBg : InactiveAngleBg;
    public Brush HoleGentleFg => _holeIntensity == FeedIntensityKind.Gentle ? ActiveAngleFg : InactiveAngleFg;

    public Brush HoleOptimalBg => _holeIntensity == FeedIntensityKind.Optimal ? ActiveAngleBg : InactiveAngleBg;
    public Brush HoleOptimalFg => _holeIntensity == FeedIntensityKind.Optimal ? ActiveAngleFg : InactiveAngleFg;

    public Brush HoleAggressiveBg => _holeIntensity == FeedIntensityKind.Aggressive ? ActiveAngleBg : InactiveAngleBg;
    public Brush HoleAggressiveFg => _holeIntensity == FeedIntensityKind.Aggressive ? ActiveAngleFg : InactiveAngleFg;

    public string HoleIntensityNote => _holeIntensity switch
    {
        FeedIntensityKind.Gentle => "Щадящий режим (нижняя граница каталога): для глубоких отверстий, тонких свёрл, нежёсткого крепления и защиты от увода оси.",
        FeedIntensityKind.Aggressive => "Форсированный режим (верхняя граница каталога): для жёстких свёрл с внутренним подводом СОЖ и максимальной производительности.",
        _ => "Оптимальный режим (середина диапазона): сбалансированное дробление стружки, точность отверстия и ресурс сверла."
    };

    private int _holeSpindleRpm = 1000;
    private string _holeSpindleRpmText = "1000";
    public string HoleSpindleRpmText
    {
        get => _holeSpindleRpmText;
        set
        {
            if (SetProperty(ref _holeSpindleRpmText, value) && int.TryParse(value, out int val) && val > 0)
            {
                _holeSpindleRpm = val;
                RecalculateBlindHole();
            }
        }
    }

    private bool _isTotalDepthInput;
    /// <summary>false — ввод глубины по цилиндру L_цил, true — ввод полной глубины по кончику L_полн.</summary>
    public bool IsTotalDepthInput
    {
        get => _isTotalDepthInput;
        set
        {
            if (SetProperty(ref _isTotalDepthInput, value))
            {
                OnPropertyChanged(nameof(HoleDepthInputLabel));
                if (IsHoleMaterialSelectorActive) ApplyAutoHoleFeed();
                else RecalculateBlindHole();
            }
        }
    }

    public string HoleDepthInputLabel => IsTotalDepthInput
        ? "Полная глубина по кончику L_полн (мм)"
        : "Глубина цилиндрической части L_цил (мм)";

    private bool _isHoleVertical;
    /// <summary>false — Горизонтально (токарный ЧПУ), true — Вертикально (фрезерный ЧПУ).</summary>
    public bool IsHoleVertical
    {
        get => _isHoleVertical;
        set { if (SetProperty(ref _isHoleVertical, value)) RecalculateBlindHole(); }
    }

    private double _drillTipAngle = 118.0;
    private bool _isCustomDrillAngle;
    public bool IsCustomDrillAngle
    {
        get => _isCustomDrillAngle;
        private set => SetProperty(ref _isCustomDrillAngle, value);
    }

    private string _customDrillAngleText = "118";
    public string CustomDrillAngleText
    {
        get => _customDrillAngleText;
        set
        {
            if (SetProperty(ref _customDrillAngleText, value) && TryParseDecimal(value, out double val))
            {
                _drillTipAngle = val;
                if (IsHoleMaterialSelectorActive) ApplyAutoHoleFeed();
                else RecalculateBlindHole();
            }
        }
    }

    public Brush Drill90Bg => !_isCustomDrillAngle && System.Math.Abs(_drillTipAngle - 90.0) < 0.01 ? ActiveAngleBg : InactiveAngleBg;
    public Brush Drill90Fg => !_isCustomDrillAngle && System.Math.Abs(_drillTipAngle - 90.0) < 0.01 ? ActiveAngleFg : InactiveAngleFg;

    public Brush Drill118Bg => !_isCustomDrillAngle && System.Math.Abs(_drillTipAngle - 118.0) < 0.01 ? ActiveAngleBg : InactiveAngleBg;
    public Brush Drill118Fg => !_isCustomDrillAngle && System.Math.Abs(_drillTipAngle - 118.0) < 0.01 ? ActiveAngleFg : InactiveAngleFg;

    public Brush Drill135Bg => !_isCustomDrillAngle && System.Math.Abs(_drillTipAngle - 135.0) < 0.01 ? ActiveAngleBg : InactiveAngleBg;
    public Brush Drill135Fg => !_isCustomDrillAngle && System.Math.Abs(_drillTipAngle - 135.0) < 0.01 ? ActiveAngleFg : InactiveAngleFg;

    public Brush Drill140Bg => !_isCustomDrillAngle && System.Math.Abs(_drillTipAngle - 140.0) < 0.01 ? ActiveAngleBg : InactiveAngleBg;
    public Brush Drill140Fg => !_isCustomDrillAngle && System.Math.Abs(_drillTipAngle - 140.0) < 0.01 ? ActiveAngleFg : InactiveAngleFg;

    public Brush DrillCustomBg => _isCustomDrillAngle ? ActiveAngleBg : InactiveAngleBg;
    public Brush DrillCustomFg => _isCustomDrillAngle ? ActiveAngleFg : InactiveAngleFg;

    private void NotifyDrillAngleButtons()
    {
        OnPropertyChanged(nameof(Drill90Bg)); OnPropertyChanged(nameof(Drill90Fg));
        OnPropertyChanged(nameof(Drill118Bg)); OnPropertyChanged(nameof(Drill118Fg));
        OnPropertyChanged(nameof(Drill135Bg)); OnPropertyChanged(nameof(Drill135Fg));
        OnPropertyChanged(nameof(Drill140Bg)); OnPropertyChanged(nameof(Drill140Fg));
        OnPropertyChanged(nameof(DrillCustomBg)); OnPropertyChanged(nameof(DrillCustomFg));
    }

    private double _drillDiameter = 10.0;
    private string _drillDiameterText = "10";
    public string DrillDiameterText
    {
        get => _drillDiameterText;
        set
        {
            if (SetProperty(ref _drillDiameterText, value) && TryParseDecimal(value, out double val))
            {
                _drillDiameter = val;
                if (IsHoleMaterialSelectorActive) ApplyAutoHoleFeed();
                else RecalculateBlindHole();
            }
        }
    }

    private double _holeInputDepth = 20.0;
    private string _holeInputDepthText = "20";
    public string HoleInputDepthText
    {
        get => _holeInputDepthText;
        set
        {
            if (SetProperty(ref _holeInputDepthText, value) && TryParseDecimal(value, out double val))
            {
                _holeInputDepth = val;
                if (IsHoleMaterialSelectorActive) ApplyAutoHoleFeed();
                else RecalculateBlindHole();
            }
        }
    }

    private double _holeStartZ = 0.0;
    private string _holeStartZText = "0";
    public string HoleStartZText
    {
        get => _holeStartZText;
        set
        {
            if (SetProperty(ref _holeStartZText, value) && TryParseDecimal(value, out double val))
            {
                _holeStartZ = val;
                RecalculateBlindHole();
            }
        }
    }

    private double _holeRetractR = 2.0;
    private string _holeRetractRText = "2";
    public string HoleRetractRText
    {
        get => _holeRetractRText;
        set
        {
            if (SetProperty(ref _holeRetractRText, value) && TryParseDecimal(value, out double val))
            {
                _holeRetractR = val;
                RecalculateBlindHole();
            }
        }
    }

    private bool _roundHoleSchemeValues;
    /// <summary>false — показывать на эскизе точные значения (0.###), true — округлять на схеме (0.#).</summary>
    public bool RoundHoleSchemeValues
    {
        get => _roundHoleSchemeValues;
        set { if (SetProperty(ref _roundHoleSchemeValues, value)) RecalculateBlindHole(); }
    }

    private BlindHoleCalculationResult _holeResult = new();
    public BlindHoleCalculationResult HoleResult
    {
        get => _holeResult;
        private set => SetProperty(ref _holeResult, value);
    }


    // =========================================================================
    // РАЗДЕЛ 4: ТОКАРНЫЙ КОНУС ЧПУ / ПРЯМОУГОЛЬНЫЙ ТРЕУГОЛЬНИК (КАРТОЧКА 4)
    // =========================================================================
    private FeedIntensityKind _coneIntensity = FeedIntensityKind.Optimal;
    public FeedIntensityKind ConeIntensity
    {
        get => _coneIntensity;
        set
        {
            if (SetProperty(ref _coneIntensity, value))
            {
                OnPropertyChanged(nameof(ConeGentleBg)); OnPropertyChanged(nameof(ConeGentleFg));
                OnPropertyChanged(nameof(ConeOptimalBg)); OnPropertyChanged(nameof(ConeOptimalFg));
                OnPropertyChanged(nameof(ConeAggressiveBg)); OnPropertyChanged(nameof(ConeAggressiveFg));
                OnPropertyChanged(nameof(ConeIntensityNote));
                if (IsConeMaterialSelectorActive)
                    ApplyAutoConeFeed();
            }
        }
    }

    public Brush ConeGentleBg => _coneIntensity == FeedIntensityKind.Gentle ? ActiveAngleBg : InactiveAngleBg;
    public Brush ConeGentleFg => _coneIntensity == FeedIntensityKind.Gentle ? ActiveAngleFg : InactiveAngleFg;

    public Brush ConeOptimalBg => _coneIntensity == FeedIntensityKind.Optimal ? ActiveAngleBg : InactiveAngleBg;
    public Brush ConeOptimalFg => _coneIntensity == FeedIntensityKind.Optimal ? ActiveAngleFg : InactiveAngleFg;

    public Brush ConeAggressiveBg => _coneIntensity == FeedIntensityKind.Aggressive ? ActiveAngleBg : InactiveAngleBg;
    public Brush ConeAggressiveFg => _coneIntensity == FeedIntensityKind.Aggressive ? ActiveAngleFg : InactiveAngleFg;

    public string ConeIntensityNote => _coneIntensity switch
    {
        FeedIntensityKind.Gentle => "Щадящий режим (нижняя граница каталога): для точных конусов (Морзе, 7:24), нежёстких валов и высокой чистоты образующей.",
        FeedIntensityKind.Aggressive => "Форсированный режим (верхняя граница каталога): для жёстких заготовок с равномерным припуском и сокращения времени прохода.",
        _ => "Оптимальный режим (середина диапазона): сбалансированное соотношение точности конусности, чистоты и стойкости пластины."
    };

    private int _coneSpindleRpm = 1200;
    private string _coneSpindleRpmText = "1200";
    public string ConeSpindleRpmText
    {
        get => _coneSpindleRpmText;
        set
        {
            if (SetProperty(ref _coneSpindleRpmText, value) && int.TryParse(value, out int val) && val > 0)
            {
                _coneSpindleRpm = val;
                RecalculateConeOrTriangle();
            }
        }
    }


    private bool _isTriangleMode;
    /// <summary>false — режим "Токарный конус ЧПУ", true — режим "Прямоугольный треугольник".</summary>
    public bool IsTriangleMode
    {
        get => _isTriangleMode;
        set
        {
            if (SetProperty(ref _isTriangleMode, value))
            {
                OnPropertyChanged(nameof(IsConeMode));
                OnPropertyChanged(nameof(Card4BadgeText));
                RecalculateConeOrTriangle();
            }
        }
    }

    public bool IsConeMode => !_isTriangleMode;
    public string Card4BadgeText => IsTriangleMode ? "Режим: Прямоугольный треугольник" : "Режим: Токарный конус (G01)";

    private ConeSolveKind _coneSolveKind = ConeSolveKind.ByTwoDiametersAndLength;
    public ConeSolveKind ConeSolveKind
    {
        get => _coneSolveKind;
        set
        {
            if (SetProperty(ref _coneSolveKind, value))
            {
                OnPropertyChanged(nameof(IsConeEndDiameterEnabled));
                OnPropertyChanged(nameof(IsConeLengthEnabled));
                OnPropertyChanged(nameof(IsConeAngleEnabled));
                OnPropertyChanged(nameof(IsConeXDirectionEnabled));
                OnPropertyChanged(nameof(ConeKind1Bg)); OnPropertyChanged(nameof(ConeKind1Fg));
                OnPropertyChanged(nameof(ConeKind2Bg)); OnPropertyChanged(nameof(ConeKind2Fg));
                OnPropertyChanged(nameof(ConeKind3Bg)); OnPropertyChanged(nameof(ConeKind3Fg));
                RecalculateConeOrTriangle();
            }
        }
    }

    public bool IsConeXDirectionEnabled => ConeSolveKind == ConeSolveKind.ByStartDiameterLengthAndAngle;

    public bool IsConeEndDiameterEnabled => ConeSolveKind != ConeSolveKind.ByStartDiameterLengthAndAngle;
    public bool IsConeLengthEnabled => ConeSolveKind != ConeSolveKind.ByTwoDiametersAndAngle;
    public bool IsConeAngleEnabled => ConeSolveKind != ConeSolveKind.ByTwoDiametersAndLength;

    public Brush ConeKind1Bg => ConeSolveKind == ConeSolveKind.ByTwoDiametersAndLength ? ActiveAngleBg : InactiveAngleBg;
    public Brush ConeKind1Fg => ConeSolveKind == ConeSolveKind.ByTwoDiametersAndLength ? ActiveAngleFg : InactiveAngleFg;

    public Brush ConeKind2Bg => ConeSolveKind == ConeSolveKind.ByStartDiameterLengthAndAngle ? ActiveAngleBg : InactiveAngleBg;
    public Brush ConeKind2Fg => ConeSolveKind == ConeSolveKind.ByStartDiameterLengthAndAngle ? ActiveAngleFg : InactiveAngleFg;

    public Brush ConeKind3Bg => ConeSolveKind == ConeSolveKind.ByTwoDiametersAndAngle ? ActiveAngleBg : InactiveAngleBg;
    public Brush ConeKind3Fg => ConeSolveKind == ConeSolveKind.ByTwoDiametersAndAngle ? ActiveAngleFg : InactiveAngleFg;

    private bool _isConeDecreasingX;
    public bool IsConeDecreasingX
    {
        get => _isConeDecreasingX;
        set { if (SetProperty(ref _isConeDecreasingX, value)) RecalculateConeOrTriangle(); }
    }

    private bool _isConeLeftToRight;
    public bool IsConeLeftToRight
    {
        get => _isConeLeftToRight;
        set { if (SetProperty(ref _isConeLeftToRight, value)) RecalculateConeOrTriangle(); }
    }

    private bool _isConeHole;
    public bool IsConeHole
    {
        get => _isConeHole;
        set { if (SetProperty(ref _isConeHole, value)) if (IsConeMaterialSelectorActive) ApplyAutoConeFeed();
                else RecalculateConeOrTriangle();
        }
    }

    private double _coneStartDiameter = 30.0;
    private string _coneStartDiameterText = "30";
    public string ConeStartDiameterText
    {
        get => _coneStartDiameterText;
        set
        {
            if (SetProperty(ref _coneStartDiameterText, value) && TryParseDecimal(value, out double val))
            {
                _coneStartDiameter = val;
                if (IsConeMaterialSelectorActive) ApplyAutoConeFeed();
                else RecalculateConeOrTriangle();
            }
        }
    }

    private double _coneEndDiameter = 40.0;
    private string _coneEndDiameterText = "40";
    public string ConeEndDiameterText
    {
        get => _coneEndDiameterText;
        set
        {
            if (SetProperty(ref _coneEndDiameterText, value) && TryParseDecimal(value, out double val))
            {
                _coneEndDiameter = val;
                if (IsConeMaterialSelectorActive) ApplyAutoConeFeed();
                else RecalculateConeOrTriangle();
            }
        }
    }

    private double _coneLength = 25.0;
    private string _coneLengthText = "25";
    public string ConeLengthText
    {
        get => _coneLengthText;
        set
        {
            if (SetProperty(ref _coneLengthText, value) && TryParseDecimal(value, out double val))
            {
                _coneLength = val;
                if (IsConeMaterialSelectorActive) ApplyAutoConeFeed();
                else RecalculateConeOrTriangle();
            }
        }
    }

    private double _coneSlopeAngle = 11.31;
    private string _coneSlopeAngleText = "11.31";
    public string ConeSlopeAngleText
    {
        get => _coneSlopeAngleText;
        set
        {
            if (SetProperty(ref _coneSlopeAngleText, value) && TryParseDecimal(value, out double val))
            {
                _coneSlopeAngle = val;
                if (IsConeMaterialSelectorActive) ApplyAutoConeFeed();
                else RecalculateConeOrTriangle();
            }
        }
    }

    private double _coneStartZ = 0.0;
    private string _coneStartZText = "0";
    public string ConeStartZText
    {
        get => _coneStartZText;
        set
        {
            if (SetProperty(ref _coneStartZText, value) && TryParseDecimal(value, out double val))
            {
                _coneStartZ = val;
                RecalculateConeOrTriangle();
            }
        }
    }

    private bool _useConeFeed = true;
    /// <summary>Учитывать рабочую подачу F в кадрах конуса G01.</summary>
    public bool UseConeFeed
    {
        get => _useConeFeed;
        set
        {
            if (SetProperty(ref _useConeFeed, value))
            {
                OnPropertyChanged(nameof(IsConeFeedManualEditable));
                OnPropertyChanged(nameof(IsConeMaterialSelectorActive));
                if (value && AutoCalculateConeFeed)
                    ApplyAutoConeFeed();
                else
                    RecalculateConeOrTriangle();
            }
        }
    }

    private bool _autoCalculateConeFeed;
    /// <summary>Автоматически подбирать подачу F при точении конуса по материалу и радиальному припуску.</summary>
    public bool AutoCalculateConeFeed
    {
        get => _autoCalculateConeFeed;
        set
        {
            if (SetProperty(ref _autoCalculateConeFeed, value))
            {
                OnPropertyChanged(nameof(IsConeFeedManualEditable));
                OnPropertyChanged(nameof(IsConeMaterialSelectorActive));
                OnPropertyChanged(nameof(ConeFeedAutoNote));
                if (value)
                    ApplyAutoConeFeed();
                else
                    RecalculateConeOrTriangle();
            }
        }
    }

    public bool IsConeFeedManualEditable => UseConeFeed && !AutoCalculateConeFeed;
    public bool IsConeMaterialSelectorActive => UseConeFeed && AutoCalculateConeFeed;

    private IsoMaterialGroupItem _selectedConeIsoGroup;
    public IsoMaterialGroupItem SelectedConeIsoGroup
    {
        get => _selectedConeIsoGroup;
        set
        {
            if (value != null && SetProperty(ref _selectedConeIsoGroup, value))
            {
                _selectedConeMaterial = null;
                OnPropertyChanged(nameof(SelectedConeMaterial));
                OnPropertyChanged(nameof(HasSelectedConeMaterial));
                UpdateFilteredConeGrades();
                OnPropertyChanged(nameof(ConeFeedAutoNote));
                if (IsConeMaterialSelectorActive)
                    ApplyAutoConeFeed();
            }
        }
    }

    private List<MaterialGradeItem> _filteredConeGrades = new();
    public List<MaterialGradeItem> FilteredConeGrades
    {
        get => _filteredConeGrades;
        private set => SetProperty(ref _filteredConeGrades, value);
    }

    private MaterialGradeItem? _selectedConeMaterial;
    public MaterialGradeItem? SelectedConeMaterial
    {
        get => _selectedConeMaterial;
        set
        {
            if (SetProperty(ref _selectedConeMaterial, value))
            {
                OnPropertyChanged(nameof(HasSelectedConeMaterial));
                OnPropertyChanged(nameof(ConeFeedAutoNote));
                if (IsConeMaterialSelectorActive)
                    ApplyAutoConeFeed();
            }
        }
    }

    public bool HasSelectedConeMaterial => SelectedConeMaterial != null;

    private void UpdateFilteredConeGrades()
    {
        if (SelectedConeIsoGroup == null)
        {
            FilteredConeGrades = new List<MaterialGradeItem>();
            return;
        }

        FilteredConeGrades = _allMaterialGrades
            .Where(g => g.IsoGroup == SelectedConeIsoGroup.Code)
            .ToList();
    }

    private double _coneFeedRate = 0.15;
    private string _coneFeedRateText = "0.15";
    public string ConeFeedRateText
    {
        get => _coneFeedRateText;
        set
        {
            if (SetProperty(ref _coneFeedRateText, value) && TryParseDecimal(value, out double val))
            {
                _coneFeedRate = val;
                RecalculateConeOrTriangle();
            }
        }
    }

    private double EstimateConeRadialAllowance()
    {
        return ConeSolveKind switch
        {
            ConeSolveKind.ByStartDiameterLengthAndAngle =>
                _coneLength * System.Math.Tan(_coneSlopeAngle * (System.Math.PI / 180.0)),
            _ => System.Math.Abs(_coneEndDiameter - _coneStartDiameter) / 2.0
        };
    }

    public string ConeFeedAutoNote
    {
        get
        {
            if (!AutoCalculateConeFeed || SelectedConeIsoGroup == null)
                return string.Empty;

            string matText = SelectedConeMaterial != null
                ? $"{SelectedConeMaterial.GostGrade} (ISO {SelectedConeIsoGroup.CodeLetter})"
                : $"группы ISO {SelectedConeIsoGroup.CodeLetter} ({SelectedConeIsoGroup.Name})";

            string opText = IsConeHole ? "внутреннего конуса (растачивание)" : "наружного конуса";
            double allowance = EstimateConeRadialAllowance();
            string stageText = allowance <= 1.5
                ? $"чистовой режим (ΔR ≈ {allowance:0.##} ≤ 1.5 мм)"
                : $"получистовой режим (ΔR ≈ {allowance:0.##} > 1.5 мм)";

            return $"Автоподбор для {matText}: {opText}, {stageText}.";
        }
    }

    private void ApplyAutoConeFeed()
    {
        if (SelectedConeIsoGroup == null) return;

        var operation = IsConeHole
            ? TurningOperationKind.InternalBoring
            : TurningOperationKind.ExternalTurning;

        double allowance = EstimateConeRadialAllowance();
        var stage = allowance <= 1.5
            ? CuttingStageKind.Finishing
            : CuttingStageKind.SemiFinishing;

        double refDia = System.Math.Max(_coneStartDiameter, _coneEndDiameter);
        if (refDia < 1.0) refDia = 40.0;

        var rec = _cuttingService.QuickRecommend(
            SelectedConeIsoGroup,
            SelectedConeMaterial,
            operation,
            stage,
            refDia);

        if (rec.IsValid && rec.RecommendedFeedPerRev > 0)
        {
            double selectedFeed = _coneIntensity switch
            {
                FeedIntensityKind.Gentle => rec.FeedPerRevMin,
                FeedIntensityKind.Aggressive => rec.FeedPerRevMax,
                _ => rec.RecommendedFeedPerRev
            };

            double selectedVc = _coneIntensity switch
            {
                FeedIntensityKind.Gentle => rec.VcMin,
                FeedIntensityKind.Aggressive => rec.VcMax,
                _ => rec.RecommendedVc
            };

            int calculatedRpm = (int)System.Math.Clamp(
                System.Math.Round((1000.0 * selectedVc) / (System.Math.PI * refDia)),
                20, 12000);

            _coneSpindleRpm = calculatedRpm;
            _coneSpindleRpmText = calculatedRpm.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(ConeSpindleRpmText));

            _coneFeedRate = selectedFeed;
            _coneFeedRateText = selectedFeed.ToString("0.###", CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(ConeFeedRateText));
            OnPropertyChanged(nameof(ConeFeedAutoNote));
        }

        RecalculateConeOrTriangle();
    }

    // =========================================================================
    // РАЗДЕЛ 5: ПРЯМОУГОЛЬНЫЕ КАНАВКИ И ОТРЕЗКА С ЦИКЛОМ G75 (КАРТОЧКА 5)
    // =========================================================================

    private bool _includeGrooveFinishPass;
    /// <summary>Добавлять чистовой калибрующий проход по стенкам и дну канавки (G01).</summary>
    public bool IncludeGrooveFinishPass
    {
        get => _includeGrooveFinishPass;
        set
        {
            if (SetProperty(ref _includeGrooveFinishPass, value))
                RecalculateGroove();
        }
    }

    private bool _isGrooveFinishWithAllowance = true;
    /// <summary>true — чистовой проход с припуском (стенки и дно), false — быстрая зачистка дна без припуска.</summary>
    public bool IsGrooveFinishWithAllowance
    {
        get => _isGrooveFinishWithAllowance;
        set
        {
            if (SetProperty(ref _isGrooveFinishWithAllowance, value))
                RecalculateGroove();
        }
    }

    private double _grooveFinishAllowanceX = 0.2;
    private string _grooveFinishAllowanceXText = "0.2";
    public string GrooveFinishAllowanceXText
    {
        get => _grooveFinishAllowanceXText;
        set
        {
            if (SetProperty(ref _grooveFinishAllowanceXText, value) && TryParseDecimal(value, out double val))
            {
                _grooveFinishAllowanceX = val;
                RecalculateGroove();
            }
        }
    }

    private double _grooveFinishAllowanceZ = 0.1;
    private string _grooveFinishAllowanceZText = "0.1";
    public string GrooveFinishAllowanceZText
    {
        get => _grooveFinishAllowanceZText;
        set
        {
            if (SetProperty(ref _grooveFinishAllowanceZText, value) && TryParseDecimal(value, out double val))
            {
                _grooveFinishAllowanceZ = val;
                RecalculateGroove();
            }
        }
    }
    private FeedIntensityKind _grooveIntensity = FeedIntensityKind.Optimal;
    public FeedIntensityKind GrooveIntensity
    {
        get => _grooveIntensity;
        set
        {
            if (SetProperty(ref _grooveIntensity, value))
            {
                OnPropertyChanged(nameof(GrooveGentleBg)); OnPropertyChanged(nameof(GrooveGentleFg));
                OnPropertyChanged(nameof(GrooveOptimalBg)); OnPropertyChanged(nameof(GrooveOptimalFg));
                OnPropertyChanged(nameof(GrooveAggressiveBg)); OnPropertyChanged(nameof(GrooveAggressiveFg));
                OnPropertyChanged(nameof(GrooveIntensityNote));
                if (AutoCalculateGrooveFeed)
                    ApplyAutoGrooveFeed();
            }
        }
    }

    public Brush GrooveGentleBg => _grooveIntensity == FeedIntensityKind.Gentle ? ActiveAngleBg : InactiveAngleBg;
    public Brush GrooveGentleFg => _grooveIntensity == FeedIntensityKind.Gentle ? ActiveAngleFg : InactiveAngleFg;

    public Brush GrooveOptimalBg => _grooveIntensity == FeedIntensityKind.Optimal ? ActiveAngleBg : InactiveAngleBg;
    public Brush GrooveOptimalFg => _grooveIntensity == FeedIntensityKind.Optimal ? ActiveAngleFg : InactiveAngleFg;

    public Brush GrooveAggressiveBg => _grooveIntensity == FeedIntensityKind.Aggressive ? ActiveAngleBg : InactiveAngleBg;
    public Brush GrooveAggressiveFg => _grooveIntensity == FeedIntensityKind.Aggressive ? ActiveAngleFg : InactiveAngleFg;

    public string GrooveIntensityNote => _grooveIntensity switch
    {
        FeedIntensityKind.Gentle => "Щадящий режим (нижняя граница + частый отскок P): для глубоких пазов, узких отрезных резцов и вязких сплавов.",
        FeedIntensityKind.Aggressive => "Форсированный режим (верхняя граница каталога): для неглубоких канавок на жёстких державках и сокращения времени цикла.",
        _ => "Оптимальный режим (середина диапазона): надёжное завивание стружки, безопасная нагрузка на пластину и стабильная стойкость."
    };

    private int _grooveSpindleRpm = 1000;
    private string _grooveSpindleRpmText = "1000";
    public string GrooveSpindleRpmText
    {
        get => _grooveSpindleRpmText;
        set
        {
            if (SetProperty(ref _grooveSpindleRpmText, value) && int.TryParse(value, out int val) && val > 0)
            {
                _grooveSpindleRpm = val;
                RecalculateGroove();
            }
        }
    }

    private bool _isGroovePartOffMode;
    /// <summary>false — наружная канавка (G75), true — отрезка детали (G75).</summary>
    public bool IsGroovePartOffMode
    {
        get => _isGroovePartOffMode;
        set
        {
            if (SetProperty(ref _isGroovePartOffMode, value))
            {
                OnPropertyChanged(nameof(IsGrooveChannelMode));
                OnPropertyChanged(nameof(Card5BadgeText));
                OnPropertyChanged(nameof(GrooveStartZHint));
                if (AutoCalculateGrooveFeed) ApplyAutoGrooveFeed();
                else RecalculateGroove();
            }
        }
    }

    public bool IsGrooveChannelMode => !_isGroovePartOffMode;
    public string Card5BadgeText => IsGroovePartOffMode ? "Режим: Отрезка детали (G75)" : "Режим: Наружная канавка (G75)";
    public string GrooveStartZHint => IsGroovePartOffMode ? "Координата отрезки Z (мм)" : "Начальная стенка Z_нач (мм)";

    private bool _isGrooveLeftToRight;
    /// <summary>false — справа налево (Z-), true — слева направо (Z+).</summary>
    public bool IsGrooveLeftToRight
    {
        get => _isGrooveLeftToRight;
        set
        {
            if (SetProperty(ref _isGrooveLeftToRight, value))
                RecalculateGroove();
        }
    }

    private bool _considerGrooveToolWidth = true;
    /// <summary>Учитывать толщину резца B (при true выводятся 2 варианта кода: числовой и с переменной #N).</summary>
    public bool ConsiderGrooveToolWidth
    {
        get => _considerGrooveToolWidth;
        set
        {
            if (SetProperty(ref _considerGrooveToolWidth, value))
                if (AutoCalculateGrooveFeed) ApplyAutoGrooveFeed();
                else RecalculateGroove();
        }
    }

    private bool _useGrooveAxisOvertravel = true;
    /// <summary>Перебег за ось симметрии при отрезке (переход через X0).</summary>
    public bool UseGrooveAxisOvertravel
    {
        get => _useGrooveAxisOvertravel;
        set
        {
            if (SetProperty(ref _useGrooveAxisOvertravel, value))
                RecalculateGroove();
        }
    }

    private bool _reduceGrooveFeedNearCenter = true;
    /// <summary>Снижать подачу в 2 раза к центру (дорезка G01 с Ø6 мм при отрезке).</summary>
    public bool ReduceGrooveFeedNearCenter
    {
        get => _reduceGrooveFeedNearCenter;
        set
        {
            if (SetProperty(ref _reduceGrooveFeedNearCenter, value))
                RecalculateGroove();
        }
    }

    private double _grooveStockDiameter = 40.0;
    private string _grooveStockDiameterText = "40";
    public string GrooveStockDiameterText
    {
        get => _grooveStockDiameterText;
        set
        {
            if (SetProperty(ref _grooveStockDiameterText, value) && TryParseDecimal(value, out double val))
            {
                _grooveStockDiameter = val;
                if (AutoCalculateGrooveFeed) ApplyAutoGrooveFeed();
                else RecalculateGroove();
            }
        }
    }

    private double _grooveBottomDiameter = 30.0;
    private string _grooveBottomDiameterText = "30";
    public string GrooveBottomDiameterText
    {
        get => _grooveBottomDiameterText;
        set
        {
            if (SetProperty(ref _grooveBottomDiameterText, value) && TryParseDecimal(value, out double val))
            {
                _grooveBottomDiameter = val;
                if (AutoCalculateGrooveFeed) ApplyAutoGrooveFeed();
                else RecalculateGroove();
            }
        }
    }

    private double _grooveStartZ = -20.0;
    private string _grooveStartZText = "-20";
    public string GrooveStartZText
    {
        get => _grooveStartZText;
        set
        {
            if (SetProperty(ref _grooveStartZText, value) && TryParseDecimal(value, out double val))
            {
                _grooveStartZ = val;
                RecalculateGroove();
            }
        }
    }

    private double _grooveEndZ = -30.0;
    private string _grooveEndZText = "-30";
    public string GrooveEndZText
    {
        get => _grooveEndZText;
        set
        {
            if (SetProperty(ref _grooveEndZText, value) && TryParseDecimal(value, out double val))
            {
                _grooveEndZ = val;
                RecalculateGroove();
            }
        }
    }

    private double _grooveApproachOffsetX = 2.0;
    private string _grooveApproachOffsetXText = "2";
    public string GrooveApproachOffsetXText
    {
        get => _grooveApproachOffsetXText;
        set
        {
            if (SetProperty(ref _grooveApproachOffsetXText, value) && TryParseDecimal(value, out double val))
            {
                _grooveApproachOffsetX = val;
                RecalculateGroove();
            }
        }
    }

    private double _grooveStepPMm = 1.5;
    private string _grooveStepPMmText = "1.5";
    public string GrooveStepPMmText
    {
        get => _grooveStepPMmText;
        set
        {
            if (SetProperty(ref _grooveStepPMmText, value) && TryParseDecimal(value, out double val))
            {
                _grooveStepPMm = val;
                RecalculateGroove();
            }
        }
    }

    private double _grooveRetractR = 0.5;
    private string _grooveRetractRText = "0.5";
    public string GrooveRetractRText
    {
        get => _grooveRetractRText;
        set
        {
            if (SetProperty(ref _grooveRetractRText, value) && TryParseDecimal(value, out double val))
            {
                _grooveRetractR = val;
                RecalculateGroove();
            }
        }
    }

    private double _grooveFeedRate = 0.08;
    private string _grooveFeedRateText = "0.08";
    public string GrooveFeedRateText
    {
        get => _grooveFeedRateText;
        set
        {
            if (SetProperty(ref _grooveFeedRateText, value) && TryParseDecimal(value, out double val))
            {
                _grooveFeedRate = val;
                RecalculateGroove();
            }
        }
    }

    private bool _autoCalculateGrooveFeed;
    /// <summary>Автоматически подбирать подачу F для цикла G75 по материалу и ширине резца B.</summary>
    public bool AutoCalculateGrooveFeed
    {
        get => _autoCalculateGrooveFeed;
        set
        {
            if (SetProperty(ref _autoCalculateGrooveFeed, value))
            {
                OnPropertyChanged(nameof(GrooveFeedAutoNote));
                if (value)
                    ApplyAutoGrooveFeed();
                else
                    RecalculateGroove();
            }
        }
    }

    private IsoMaterialGroupItem _selectedGrooveIsoGroup;
    public IsoMaterialGroupItem SelectedGrooveIsoGroup
    {
        get => _selectedGrooveIsoGroup;
        set
        {
            if (value != null && SetProperty(ref _selectedGrooveIsoGroup, value))
            {
                _selectedGrooveMaterial = null;
                OnPropertyChanged(nameof(SelectedGrooveMaterial));
                OnPropertyChanged(nameof(HasSelectedGrooveMaterial));
                UpdateFilteredGrooveGrades();
                OnPropertyChanged(nameof(GrooveFeedAutoNote));
                if (AutoCalculateGrooveFeed)
                    ApplyAutoGrooveFeed();
            }
        }
    }

    private List<MaterialGradeItem> _filteredGrooveGrades = new();
    public List<MaterialGradeItem> FilteredGrooveGrades
    {
        get => _filteredGrooveGrades;
        private set => SetProperty(ref _filteredGrooveGrades, value);
    }

    private MaterialGradeItem? _selectedGrooveMaterial;
    public MaterialGradeItem? SelectedGrooveMaterial
    {
        get => _selectedGrooveMaterial;
        set
        {
            if (SetProperty(ref _selectedGrooveMaterial, value))
            {
                OnPropertyChanged(nameof(HasSelectedGrooveMaterial));
                OnPropertyChanged(nameof(GrooveFeedAutoNote));
                if (AutoCalculateGrooveFeed)
                    ApplyAutoGrooveFeed();
            }
        }
    }

    public bool HasSelectedGrooveMaterial => SelectedGrooveMaterial != null;

    private void UpdateFilteredGrooveGrades()
    {
        if (SelectedGrooveIsoGroup == null)
        {
            FilteredGrooveGrades = new List<MaterialGradeItem>();
            return;
        }

        FilteredGrooveGrades = _allMaterialGrades
            .Where(g => g.IsoGroup == SelectedGrooveIsoGroup.Code)
            .ToList();
    }

    public string GrooveFeedAutoNote
    {
        get
        {
            if (!AutoCalculateGrooveFeed || SelectedGrooveIsoGroup == null)
                return string.Empty;

            string matText = SelectedGrooveMaterial != null
                ? $"{SelectedGrooveMaterial.GostGrade} (ISO {SelectedGrooveIsoGroup.CodeLetter})"
                : $"группы ISO {SelectedGrooveIsoGroup.CodeLetter} ({SelectedGrooveIsoGroup.Name})";

            double effectiveB = ConsiderGrooveToolWidth && _grooveToolWidth > 0 ? _grooveToolWidth : 3.0;
            string opText = IsGroovePartOffMode ? "отрезки" : "проточки канавки";
            return $"Автоподбор F и P для {matText}: режим {opText}, ширина резца B = {effectiveB:0.##} мм.";
        }
    }
    private void ApplyAutoGrooveFeed()
    {
        if (SelectedGrooveIsoGroup == null) return;

        double effectiveB = ConsiderGrooveToolWidth && _grooveToolWidth > 0 ? _grooveToolWidth : 3.0;
        double refDia = _grooveStockDiameter > 1.0 ? _grooveStockDiameter : 40.0;

        // 1. Подбор подачи F и скорости Vc по материалу, ширине пластины B и интенсивности
        var stage = IsGroovePartOffMode ? CuttingStageKind.SemiFinishing : CuttingStageKind.Roughing;

        var rec = _cuttingService.QuickRecommend(
            SelectedGrooveIsoGroup,
            SelectedGrooveMaterial,
            TurningOperationKind.PartingAndGrooving,
            stage,
            refDia,
            toolOrGrooveWidth: effectiveB);

        if (rec.IsValid && rec.RecommendedFeedPerRev > 0)
        {
            double selectedFeed = _grooveIntensity switch
            {
                FeedIntensityKind.Gentle => rec.FeedPerRevMin,
                FeedIntensityKind.Aggressive => rec.FeedPerRevMax,
                _ => rec.RecommendedFeedPerRev
            };

            double selectedVc = _grooveIntensity switch
            {
                FeedIntensityKind.Gentle => rec.VcMin,
                FeedIntensityKind.Aggressive => rec.VcMax,
                _ => rec.RecommendedVc
            };

            int calculatedRpm = (int)System.Math.Clamp(
                System.Math.Round((1000.0 * selectedVc) / (System.Math.PI * refDia)),
                20, 12000);

            _grooveSpindleRpm = calculatedRpm;
            _grooveSpindleRpmText = calculatedRpm.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(GrooveSpindleRpmText));

            _grooveFeedRate = selectedFeed;
            _grooveFeedRateText = selectedFeed.ToString("0.###", CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(GrooveFeedRateText));
        }

        // 2. Подбор шага врезания P (мм) по материалу (стружкодробление), ширине B и интенсивности
        double peckFactor = SelectedGrooveIsoGroup.Code switch
        {
            IsoGroupCode.S => 0.45, // Титан / жаропрочные — частый отскок
            IsoGroupCode.M => 0.55, // Нержавейка — вязкая сливная стружка
            IsoGroupCode.P => 0.75, // Углеродистые и легированные стали
            IsoGroupCode.N => 0.85, // Алюминий / цветные сплавы
            IsoGroupCode.H => 0.90, // Закалённые стали
            IsoGroupCode.K => 1.00, // Чугуны — полный шаг равный ширине резца B
            _ => 0.75
        };

        if (SelectedGrooveMaterial?.GostGrade is "Ст3сп" or "Сталь 20")
            peckFactor *= 0.85;
        else if (SelectedGrooveMaterial?.GostGrade == "ЛС59-1")
            peckFactor = 1.00;

        if (IsGroovePartOffMode)
            peckFactor *= 0.85;

        // Коррекция шага P по выбранной интенсивности
        peckFactor *= _grooveIntensity switch
        {
            FeedIntensityKind.Gentle => 0.80,
            FeedIntensityKind.Aggressive => 1.15,
            _ => 1.00
        };

        double rawStepP = effectiveB * peckFactor;

        double targetD = IsGroovePartOffMode ? 0.0 : _grooveBottomDiameter;
        double radialDepth = System.Math.Max(0.3, (_grooveStockDiameter - targetD) / 2.0);
        double clampedStepP = System.Math.Clamp(rawStepP, 0.2, radialDepth);

        _grooveStepPMm = System.Math.Round(clampedStepP, 2);
        _grooveStepPMmText = _grooveStepPMm.ToString("0.##", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(GrooveStepPMmText));
        OnPropertyChanged(nameof(GrooveFeedAutoNote));

        RecalculateGroove();
    }

    public Brush ToolW2Bg => System.Math.Abs(_grooveToolWidth - 2.0) < 0.01 ? ActiveAngleBg : InactiveAngleBg;
    public Brush ToolW2Fg => System.Math.Abs(_grooveToolWidth - 2.0) < 0.01 ? ActiveAngleFg : InactiveAngleFg;

    public Brush ToolW3Bg => System.Math.Abs(_grooveToolWidth - 3.0) < 0.01 ? ActiveAngleBg : InactiveAngleBg;
    public Brush ToolW3Fg => System.Math.Abs(_grooveToolWidth - 3.0) < 0.01 ? ActiveAngleFg : InactiveAngleFg;

    public Brush ToolW4Bg => System.Math.Abs(_grooveToolWidth - 4.0) < 0.01 ? ActiveAngleBg : InactiveAngleBg;
    public Brush ToolW4Fg => System.Math.Abs(_grooveToolWidth - 4.0) < 0.01 ? ActiveAngleFg : InactiveAngleFg;

    public Brush ToolW5Bg => System.Math.Abs(_grooveToolWidth - 5.0) < 0.01 ? ActiveAngleBg : InactiveAngleBg;
    public Brush ToolW5Fg => System.Math.Abs(_grooveToolWidth - 5.0) < 0.01 ? ActiveAngleFg : InactiveAngleFg;

    private void NotifyGrooveToolWidthButtons()
    {
        OnPropertyChanged(nameof(ToolW2Bg)); OnPropertyChanged(nameof(ToolW2Fg));
        OnPropertyChanged(nameof(ToolW3Bg)); OnPropertyChanged(nameof(ToolW3Fg));
        OnPropertyChanged(nameof(ToolW4Bg)); OnPropertyChanged(nameof(ToolW4Fg));
        OnPropertyChanged(nameof(ToolW5Bg)); OnPropertyChanged(nameof(ToolW5Fg));
    }

    private double _grooveToolWidth = 3.0;
    private string _grooveToolWidthText = "3";
    public string GrooveToolWidthText
    {
        get => _grooveToolWidthText;
        set
        {
            if (SetProperty(ref _grooveToolWidthText, value) && TryParseDecimal(value, out double val))
            {
                _grooveToolWidth = val;
                NotifyGrooveToolWidthButtons();
                if (AutoCalculateGrooveFeed) ApplyAutoGrooveFeed();
                else RecalculateGroove();
            }
        }
    }

    private int _grooveMacroVarNumber = 1;
    private string _grooveMacroVarNumberText = "1";
    public string GrooveMacroVarNumberText
    {
        get => _grooveMacroVarNumberText;
        set
        {
            if (SetProperty(ref _grooveMacroVarNumberText, value) && int.TryParse(value, out int val))
            {
                _grooveMacroVarNumber = val;
                RecalculateGroove();
            }
        }
    }

    private double _grooveAxisOvertravel = 1.0;
    private string _grooveAxisOvertravelText = "1";
    public string GrooveAxisOvertravelText
    {
        get => _grooveAxisOvertravelText;
        set
        {
            if (SetProperty(ref _grooveAxisOvertravelText, value) && TryParseDecimal(value, out double val))
            {
                _grooveAxisOvertravel = val;
                RecalculateGroove();
            }
        }
    }

    private GrooveCalculationResult _grooveResult = new();
    public GrooveCalculationResult GrooveResult
    {
        get => _grooveResult;
        private set => SetProperty(ref _grooveResult, value);
    }

    private void RecalculateGroove()
    {
        GrooveResult = _geometryService.CalculateGrooveOrPartOff(
            IsGroovePartOffMode,
            IsGrooveLeftToRight,
            _grooveStockDiameter,
            _grooveBottomDiameter,
            _grooveStartZ,
            _grooveEndZ,
            _grooveApproachOffsetX,
            _grooveStepPMm,
            _grooveRetractR,
            _grooveFeedRate,
            ConsiderGrooveToolWidth,
            _grooveToolWidth,
            _grooveMacroVarNumber,
            UseGrooveAxisOvertravel,
            _grooveAxisOvertravel,
            ReduceGrooveFeedNearCenter,
            _grooveSpindleRpm,
            IncludeGrooveFinishPass,
            IsGrooveFinishWithAllowance,
            _grooveFinishAllowanceX,
            _grooveFinishAllowanceZ);
    }


    // =========================================================================
    // МЕТОДЫ РАСЧЁТА И ПАРСИНГА
    // =========================================================================

    private void RecalculateChamfer()
    {
        ChamferResult = _geometryService.CalculateChamfer(
            _chamferAxialLeg,
            SelectedChamferAngle,
            _chamferStartX,
            _chamferStartZ,
            _chamferApproachZ,
            UseChamferApproach,
            IsChamferDecreasingX,
            IsChamferLeftToRight,
            IsChamferHole,
            UseChamferFeed,
            _chamferFeedRate,
            _chamferSpindleRpm);
    }

    private bool _useHoleFeed = true;
    /// <summary>Учитывать рабочую подачу F в циклах G81 / G83.</summary>
    public bool UseHoleFeed
    {
        get => _useHoleFeed;
        set
        {
            if (SetProperty(ref _useHoleFeed, value))
            {
                OnPropertyChanged(nameof(IsHoleFeedManualEditable));
                OnPropertyChanged(nameof(IsHoleMaterialSelectorActive));
                if (value && AutoCalculateHoleFeed)
                    ApplyAutoHoleFeed();
                else
                    RecalculateBlindHole();
            }
        }
    }

    private bool _autoCalculateHoleFeed;
    /// <summary>Автоматически подбирать подачу F при сверлении по материалу, диаметру D и глубине.</summary>
    public bool AutoCalculateHoleFeed
    {
        get => _autoCalculateHoleFeed;
        set
        {
            if (SetProperty(ref _autoCalculateHoleFeed, value))
            {
                OnPropertyChanged(nameof(IsHoleFeedManualEditable));
                OnPropertyChanged(nameof(IsHoleMaterialSelectorActive));
                OnPropertyChanged(nameof(HoleFeedAutoNote));
                if (value)
                    ApplyAutoHoleFeed();
                else
                    RecalculateBlindHole();
            }
        }
    }

    public bool IsHoleFeedManualEditable => UseHoleFeed && !AutoCalculateHoleFeed;
    public bool IsHoleMaterialSelectorActive => UseHoleFeed && AutoCalculateHoleFeed;

    private IsoMaterialGroupItem _selectedHoleIsoGroup;
    public IsoMaterialGroupItem SelectedHoleIsoGroup
    {
        get => _selectedHoleIsoGroup;
        set
        {
            if (value != null && SetProperty(ref _selectedHoleIsoGroup, value))
            {
                _selectedHoleMaterial = null;
                OnPropertyChanged(nameof(SelectedHoleMaterial));
                OnPropertyChanged(nameof(HasSelectedHoleMaterial));
                UpdateFilteredHoleGrades();
                OnPropertyChanged(nameof(HoleFeedAutoNote));
                if (IsHoleMaterialSelectorActive)
                    ApplyAutoHoleFeed();
            }
        }
    }

    private List<MaterialGradeItem> _filteredHoleGrades = new();
    public List<MaterialGradeItem> FilteredHoleGrades
    {
        get => _filteredHoleGrades;
        private set => SetProperty(ref _filteredHoleGrades, value);
    }
    private MaterialGradeItem? _selectedHoleMaterial;
    public MaterialGradeItem? SelectedHoleMaterial
    {
        get => _selectedHoleMaterial;
        set
        {
            if (SetProperty(ref _selectedHoleMaterial, value))
            {
                OnPropertyChanged(nameof(HasSelectedHoleMaterial));
                OnPropertyChanged(nameof(HoleFeedAutoNote));
                if (IsHoleMaterialSelectorActive)
                    ApplyAutoHoleFeed();
            }
        }
    }

    public bool HasSelectedHoleMaterial => SelectedHoleMaterial != null;

    private void UpdateFilteredHoleGrades()
    {
        if (SelectedHoleIsoGroup == null)
        {
            FilteredHoleGrades = new List<MaterialGradeItem>();
            return;
        }

        FilteredHoleGrades = _allMaterialGrades
            .Where(g => g.IsoGroup == SelectedHoleIsoGroup.Code)
            .ToList();
    }

    private double _holeFeedRate = 0.15;
    private string _holeFeedRateText = "0.15";
    public string HoleFeedRateText
    {
        get => _holeFeedRateText;
        set
        {
            if (SetProperty(ref _holeFeedRateText, value) && TryParseDecimal(value, out double val))
            {
                _holeFeedRate = val;
                RecalculateBlindHole();
            }
        }
    }

    public string HoleFeedAutoNote
    {
        get
        {
            if (!AutoCalculateHoleFeed || SelectedHoleIsoGroup == null)
                return string.Empty;

            string matText = SelectedHoleMaterial != null
                ? $"{SelectedHoleMaterial.GostGrade} (ISO {SelectedHoleIsoGroup.CodeLetter})"
                : $"группы ISO {SelectedHoleIsoGroup.CodeLetter} ({SelectedHoleIsoGroup.Name})";

            string toolText = !_isCustomDrillAngle && System.Math.Abs(_drillTipAngle - 140.0) < 0.1
                ? "твёрдый сплав (140°)"
                : "быстрорез HSS";

            double ld = _drillDiameter > 0 ? _holeInputDepth / _drillDiameter : 3.0;
            return $"Автоподбор для {matText}: Ø{_drillDiameter:0.##} мм, {toolText}, L/D ≈ {ld:0.#}.";
        }
    }

    private void ApplyAutoHoleFeed()
    {
        if (SelectedHoleIsoGroup == null) return;

        var toolMaterial = !_isCustomDrillAngle && System.Math.Abs(_drillTipAngle - 140.0) < 0.1
            ? ToolMaterialKind.CoatedCarbide
            : ToolMaterialKind.Hss;

        double dia = _drillDiameter > 0.5 ? _drillDiameter : 10.0;
        double overhangRatio = System.Math.Max(2.0, _holeInputDepth / dia);

        var rec = _cuttingService.QuickRecommend(
            SelectedHoleIsoGroup,
            SelectedHoleMaterial,
            TurningOperationKind.Drilling,
            CuttingStageKind.Roughing,
            dia,
            overhangRatio: overhangRatio,
            toolMaterial: toolMaterial);

        if (rec.IsValid && rec.RecommendedFeedPerRev > 0)
        {
            double selectedFeed = _holeIntensity switch
            {
                FeedIntensityKind.Gentle => rec.FeedPerRevMin,
                FeedIntensityKind.Aggressive => rec.FeedPerRevMax,
                _ => rec.RecommendedFeedPerRev
            };

            double selectedVc = _holeIntensity switch
            {
                FeedIntensityKind.Gentle => rec.VcMin,
                FeedIntensityKind.Aggressive => rec.VcMax,
                _ => rec.RecommendedVc
            };

            int calculatedRpm = (int)System.Math.Clamp(
                System.Math.Round((1000.0 * selectedVc) / (System.Math.PI * dia)),
                20, 12000);

            _holeSpindleRpm = calculatedRpm;
            _holeSpindleRpmText = calculatedRpm.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(HoleSpindleRpmText));

            _holeFeedRate = selectedFeed;
            _holeFeedRateText = selectedFeed.ToString("0.###", CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(HoleFeedRateText));
            OnPropertyChanged(nameof(HoleFeedAutoNote));
        }

        RecalculateBlindHole();
    }

    private void RecalculateBlindHole()
    {
        HoleResult = _geometryService.CalculateBlindHole(
            _drillDiameter,
            _drillTipAngle,
            _holeInputDepth,
            _holeStartZ,
            _holeRetractR,
            IsTotalDepthInput,
            IsHoleVertical,
            RoundHoleSchemeValues,
            UseHoleFeed,
            _holeFeedRate,
            _holeSpindleRpm);
    }


    // --- Подраздел 4Б: Прямоугольный треугольник ---

    private TriangleSolveKind _selectedTriangleKind = TriangleSolveKind.TwoLegs;
    public TriangleSolveKind SelectedTriangleKind
    {
        get => _selectedTriangleKind;
        set
        {
            if (SetProperty(ref _selectedTriangleKind, value))
            {
                OnPropertyChanged(nameof(TriangleParam1Hint));
                OnPropertyChanged(nameof(TriangleParam2Hint));
                OnPropertyChanged(nameof(TriKind1Bg)); OnPropertyChanged(nameof(TriKind1Fg));
                OnPropertyChanged(nameof(TriKind2Bg)); OnPropertyChanged(nameof(TriKind2Fg));
                OnPropertyChanged(nameof(TriKind3Bg)); OnPropertyChanged(nameof(TriKind3Fg));
                OnPropertyChanged(nameof(TriKind4Bg)); OnPropertyChanged(nameof(TriKind4Fg));
                OnPropertyChanged(nameof(TriKind5Bg)); OnPropertyChanged(nameof(TriKind5Fg));
                RecalculateConeOrTriangle();
            }
        }
    }

    public string TriangleParam1Hint => SelectedTriangleKind switch
    {
        TriangleSolveKind.HypotenuseAndAngle => "Гипотенуза c (мм)",
        _ => "Вертикальный катет a (мм)"
    };

    public string TriangleParam2Hint => SelectedTriangleKind switch
    {
        TriangleSolveKind.TwoLegs => "Горизонтальный катет b (мм)",
        TriangleSolveKind.LegAndHypotenuse => "Гипотенуза c (мм)",
        TriangleSolveKind.LegAndOppositeAngle => "Противолежащий угол α (°)",
        TriangleSolveKind.LegAndAdjacentAngle => "Прилежащий угол β (°)",
        TriangleSolveKind.HypotenuseAndAngle => "Острый угол α (°)",
        _ => "Параметр 2"
    };

    public Brush TriKind1Bg => SelectedTriangleKind == TriangleSolveKind.TwoLegs ? ActiveAngleBg : InactiveAngleBg;
    public Brush TriKind1Fg => SelectedTriangleKind == TriangleSolveKind.TwoLegs ? ActiveAngleFg : InactiveAngleFg;

    public Brush TriKind2Bg => SelectedTriangleKind == TriangleSolveKind.LegAndHypotenuse ? ActiveAngleBg : InactiveAngleBg;
    public Brush TriKind2Fg => SelectedTriangleKind == TriangleSolveKind.LegAndHypotenuse ? ActiveAngleFg : InactiveAngleFg;

    public Brush TriKind3Bg => SelectedTriangleKind == TriangleSolveKind.LegAndOppositeAngle ? ActiveAngleBg : InactiveAngleBg;
    public Brush TriKind3Fg => SelectedTriangleKind == TriangleSolveKind.LegAndOppositeAngle ? ActiveAngleFg : InactiveAngleFg;

    public Brush TriKind4Bg => SelectedTriangleKind == TriangleSolveKind.LegAndAdjacentAngle ? ActiveAngleBg : InactiveAngleBg;
    public Brush TriKind4Fg => SelectedTriangleKind == TriangleSolveKind.LegAndAdjacentAngle ? ActiveAngleFg : InactiveAngleFg;

    public Brush TriKind5Bg => SelectedTriangleKind == TriangleSolveKind.HypotenuseAndAngle ? ActiveAngleBg : InactiveAngleBg;
    public Brush TriKind5Fg => SelectedTriangleKind == TriangleSolveKind.HypotenuseAndAngle ? ActiveAngleFg : InactiveAngleFg;

    private double _triangleParam1 = 30.0;
    private string _triangleParam1Text = "30";
    public string TriangleParam1Text
    {
        get => _triangleParam1Text;
        set
        {
            if (SetProperty(ref _triangleParam1Text, value) && TryParseDecimal(value, out double val))
            {
                _triangleParam1 = val;
                RecalculateConeOrTriangle();
            }
        }
    }

    private double _triangleParam2 = 40.0;
    private string _triangleParam2Text = "40";
    public string TriangleParam2Text
    {
        get => _triangleParam2Text;
        set
        {
            if (SetProperty(ref _triangleParam2Text, value) && TryParseDecimal(value, out double val))
            {
                _triangleParam2 = val;
                RecalculateConeOrTriangle();
            }
        }
    }

    private TriangleCalculationResult _triangleResult = new();
    public TriangleCalculationResult TriangleResult
    {
        get => _triangleResult;
        private set => SetProperty(ref _triangleResult, value);
    }

    private void RecalculateConeOrTriangle()
    {
        if (!IsTriangleMode)
        {
            if (ConeSolveKind != ConeSolveKind.ByStartDiameterLengthAndAngle
                && System.Math.Abs(_coneEndDiameter - _coneStartDiameter) > 1e-6)
            {
                _isConeDecreasingX = _coneEndDiameter < _coneStartDiameter;
                OnPropertyChanged(nameof(IsConeDecreasingX));
            }

            TriangleResult = _geometryService.CalculateCncCone(
                ConeSolveKind,
                _coneStartDiameter,
                _coneEndDiameter,
                _coneLength,
                _coneSlopeAngle,
                _coneStartZ,
                IsConeDecreasingX,
                IsConeLeftToRight,
                IsConeHole,
                UseConeFeed,
                _coneFeedRate,
                _coneSpindleRpm);
        }
        else
        {
            TriangleResult = _geometryService.SolveTriangle(
                SelectedTriangleKind,
                _triangleParam1,
                _triangleParam2);
        }
    }

    private static bool TryParseDecimal(string? text, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string normalized = text.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    // =========================================================================
    // РАЗДЕЛ 6: СОПРЯЖЕНИЕ КОНУС — ЦИЛИНДР ДУГОЙ РАДИУСА R (КАРТОЧКА 6)
    // =========================================================================

    private bool _isBlendConcaveFillet;
    /// <summary>false — Конус → Цилиндр (выпуклый), true — Цилиндр → Конус (вогнутая галтель).</summary>
    public bool IsBlendConcaveFillet
    {
        get => _isBlendConcaveFillet;
        set
        {
            if (SetProperty(ref _isBlendConcaveFillet, value))
            {
                OnPropertyChanged(nameof(Card6BadgeText));
                RecalculateConeCylinderBlend();
            }
        }
    }

    public string Card6BadgeText => IsBlendConcaveFillet
        ? "Режим: Цилиндр → Конус (галтель)"
        : "Режим: Конус → Цилиндр (скругление)";

    private bool _isBlendLeftToRight;
    public bool IsBlendLeftToRight
    {
        get => _isBlendLeftToRight;
        set { if (SetProperty(ref _isBlendLeftToRight, value)) RecalculateConeCylinderBlend(); }
    }

    private bool _isBlendHole;
    public bool IsBlendHole
    {
        get => _isBlendHole;
        set
        {
            if (SetProperty(ref _isBlendHole, value))
            {
                if (IsBlendMaterialSelectorActive) ApplyAutoBlendFeed();
                else RecalculateConeCylinderBlend();
            }
        }
    }

    private bool _isBlendFrontTurret;
    public bool IsBlendFrontTurret
    {
        get => _isBlendFrontTurret;
        set { if (SetProperty(ref _isBlendFrontTurret, value)) RecalculateConeCylinderBlend(); }
    }

    private double _blendCylinderDiameter = 40.0;
    private string _blendCylinderDiameterText = "40";
    public string BlendCylinderDiameterText
    {
        get => _blendCylinderDiameterText;
        set
        {
            if (SetProperty(ref _blendCylinderDiameterText, value) && TryParseDecimal(value, out double val))
            {
                _blendCylinderDiameter = val;
                if (IsBlendMaterialSelectorActive) ApplyAutoBlendFeed();
                else RecalculateConeCylinderBlend();
            }
        }
    }

    private double _blendCornerZ = -15.0;
    private string _blendCornerZText = "-15";
    public string BlendCornerZText
    {
        get => _blendCornerZText;
        set
        {
            if (SetProperty(ref _blendCornerZText, value) && TryParseDecimal(value, out double val))
            {
                _blendCornerZ = val;
                RecalculateConeCylinderBlend();
            }
        }
    }

    private double _blendConeAngle = 15.0;
    private string _blendConeAngleText = "15";
    public string BlendConeAngleText
    {
        get => _blendConeAngleText;
        set
        {
            if (SetProperty(ref _blendConeAngleText, value) && TryParseDecimal(value, out double val))
            {
                _blendConeAngle = val;
                RecalculateConeCylinderBlend();
            }
        }
    }

    private double _blendRadius = 3.0;
    private string _blendRadiusText = "3";
    public string BlendRadiusText
    {
        get => _blendRadiusText;
        set
        {
            if (SetProperty(ref _blendRadiusText, value) && TryParseDecimal(value, out double val))
            {
                _blendRadius = val;
                if (IsBlendMaterialSelectorActive) ApplyAutoBlendFeed();
                else RecalculateConeCylinderBlend();
            }
        }
    }

    private bool _compensateBlendToolNose;
    public bool CompensateBlendToolNose
    {
        get => _compensateBlendToolNose;
        set { if (SetProperty(ref _compensateBlendToolNose, value)) RecalculateConeCylinderBlend(); }
    }

    private double _blendToolNoseRadius = 0.4;
    private string _blendToolNoseRadiusText = "0.4";
    public string BlendToolNoseRadiusText
    {
        get => _blendToolNoseRadiusText;
        set
        {
            if (SetProperty(ref _blendToolNoseRadiusText, value) && TryParseDecimal(value, out double val))
            {
                _blendToolNoseRadius = val;
                RecalculateConeCylinderBlend();
            }
        }
    }

    private bool _useBlendFeed = true;
    public bool UseBlendFeed
    {
        get => _useBlendFeed;
        set
        {
            if (SetProperty(ref _useBlendFeed, value))
            {
                OnPropertyChanged(nameof(IsBlendMaterialSelectorActive));
                if (value && AutoCalculateBlendFeed) ApplyAutoBlendFeed();
                else RecalculateConeCylinderBlend();
            }
        }
    }

    private bool _autoCalculateBlendFeed;
    public bool AutoCalculateBlendFeed
    {
        get => _autoCalculateBlendFeed;
        set
        {
            if (SetProperty(ref _autoCalculateBlendFeed, value))
            {
                OnPropertyChanged(nameof(IsBlendMaterialSelectorActive));
                OnPropertyChanged(nameof(BlendFeedAutoNote));
                if (value) ApplyAutoBlendFeed();
                else RecalculateConeCylinderBlend();
            }
        }
    }

    public bool IsBlendMaterialSelectorActive => UseBlendFeed && AutoCalculateBlendFeed;

    private FeedIntensityKind _blendIntensity = FeedIntensityKind.Optimal;
    public FeedIntensityKind BlendIntensity
    {
        get => _blendIntensity;
        set
        {
            if (SetProperty(ref _blendIntensity, value))
            {
                OnPropertyChanged(nameof(BlendGentleBg)); OnPropertyChanged(nameof(BlendGentleFg));
                OnPropertyChanged(nameof(BlendOptimalBg)); OnPropertyChanged(nameof(BlendOptimalFg));
                OnPropertyChanged(nameof(BlendAggressiveBg)); OnPropertyChanged(nameof(BlendAggressiveFg));
                if (IsBlendMaterialSelectorActive) ApplyAutoBlendFeed();
            }
        }
    }

    public Brush BlendGentleBg => _blendIntensity == FeedIntensityKind.Gentle ? ActiveAngleBg : InactiveAngleBg;
    public Brush BlendGentleFg => _blendIntensity == FeedIntensityKind.Gentle ? ActiveAngleFg : InactiveAngleFg;
    public Brush BlendOptimalBg => _blendIntensity == FeedIntensityKind.Optimal ? ActiveAngleBg : InactiveAngleBg;
    public Brush BlendOptimalFg => _blendIntensity == FeedIntensityKind.Optimal ? ActiveAngleFg : InactiveAngleFg;
    public Brush BlendAggressiveBg => _blendIntensity == FeedIntensityKind.Aggressive ? ActiveAngleBg : InactiveAngleBg;
    public Brush BlendAggressiveFg => _blendIntensity == FeedIntensityKind.Aggressive ? ActiveAngleFg : InactiveAngleFg;

    private IsoMaterialGroupItem _selectedBlendIsoGroup;
    public IsoMaterialGroupItem SelectedBlendIsoGroup
    {
        get => _selectedBlendIsoGroup;
        set
        {
            if (value != null && SetProperty(ref _selectedBlendIsoGroup, value))
            {
                _selectedBlendMaterial = null;
                OnPropertyChanged(nameof(SelectedBlendMaterial));
                OnPropertyChanged(nameof(HasSelectedBlendMaterial));
                UpdateFilteredBlendGrades();
                OnPropertyChanged(nameof(BlendFeedAutoNote));
                if (IsBlendMaterialSelectorActive) ApplyAutoBlendFeed();
            }
        }
    }

    private List<MaterialGradeItem> _filteredBlendGrades = new();
    public List<MaterialGradeItem> FilteredBlendGrades
    {
        get => _filteredBlendGrades;
        private set => SetProperty(ref _filteredBlendGrades, value);
    }

    private MaterialGradeItem? _selectedBlendMaterial;
    public MaterialGradeItem? SelectedBlendMaterial
    {
        get => _selectedBlendMaterial;
        set
        {
            if (SetProperty(ref _selectedBlendMaterial, value))
            {
                OnPropertyChanged(nameof(HasSelectedBlendMaterial));
                OnPropertyChanged(nameof(BlendFeedAutoNote));
                if (IsBlendMaterialSelectorActive) ApplyAutoBlendFeed();
            }
        }
    }

    public bool HasSelectedBlendMaterial => SelectedBlendMaterial != null;

    private void UpdateFilteredBlendGrades()
    {
        if (SelectedBlendIsoGroup == null)
        {
            FilteredBlendGrades = new List<MaterialGradeItem>();
            return;
        }
        FilteredBlendGrades = _allMaterialGrades
            .Where(g => g.IsoGroup == SelectedBlendIsoGroup.Code)
            .ToList();
    }

    public string BlendFeedAutoNote
    {
        get
        {
            if (!AutoCalculateBlendFeed || SelectedBlendIsoGroup == null) return string.Empty;
            string matText = SelectedBlendMaterial != null
                ? $"{SelectedBlendMaterial.GostGrade} (ISO {SelectedBlendIsoGroup.CodeLetter})"
                : $"группы ISO {SelectedBlendIsoGroup.CodeLetter}";
            return $"Автоподбор для {matText}: чистовой проход сопряжения дугой R = {_blendRadius:0.##} мм.";
        }
    }

    private double _blendFeedRate = 0.12;
    private string _blendFeedRateText = "0.12";
    public string BlendFeedRateText
    {
        get => _blendFeedRateText;
        set
        {
            if (SetProperty(ref _blendFeedRateText, value) && TryParseDecimal(value, out double val))
            {
                _blendFeedRate = val;
                RecalculateConeCylinderBlend();
            }
        }
    }

    private int _blendSpindleRpm = 1200;
    private string _blendSpindleRpmText = "1200";
    public string BlendSpindleRpmText
    {
        get => _blendSpindleRpmText;
        set
        {
            if (SetProperty(ref _blendSpindleRpmText, value) && int.TryParse(value, out int val) && val > 0)
            {
                _blendSpindleRpm = val;
                RecalculateConeCylinderBlend();
            }
        }
    }

    private ConeCylinderBlendResult _blendResult = new();
    public ConeCylinderBlendResult BlendResult
    {
        get => _blendResult;
        private set => SetProperty(ref _blendResult, value);
    }

    private void ApplyAutoBlendFeed()
    {
        if (SelectedBlendIsoGroup == null) return;
        var op = IsBlendHole ? TurningOperationKind.InternalBoring : TurningOperationKind.ExternalTurning;
        double refDia = _blendCylinderDiameter > 1.0 ? _blendCylinderDiameter : 40.0;
        var rec = _cuttingService.QuickRecommend(
        SelectedBlendIsoGroup,
        SelectedBlendMaterial,
        op,
        CuttingStageKind.Finishing,
        refDia);
        if (rec.IsValid && rec.RecommendedFeedPerRev > 0)
        {
            double selectedFeed = _blendIntensity switch
            {
                FeedIntensityKind.Gentle => rec.FeedPerRevMin,
                FeedIntensityKind.Aggressive => rec.FeedPerRevMax,
                _ => rec.RecommendedFeedPerRev
            };
            double selectedVc = _blendIntensity switch
            {
                FeedIntensityKind.Gentle => rec.VcMin,
                FeedIntensityKind.Aggressive => rec.VcMax,
                _ => rec.RecommendedVc
            };
            int rpm = (int)System.Math.Clamp(System.Math.Round((1000.0 * selectedVc) / (System.Math.PI * refDia)), 20, 12000);
            _blendSpindleRpm = rpm;
            _blendSpindleRpmText = rpm.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(BlendSpindleRpmText));
            _blendFeedRate = selectedFeed;
            _blendFeedRateText = selectedFeed.ToString("0.###", CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(BlendFeedRateText));
            OnPropertyChanged(nameof(BlendFeedAutoNote));
        }
        RecalculateConeCylinderBlend();
    }
    private void RecalculateConeCylinderBlend()
    {
        BlendResult = _geometryService.CalculateConeCylinderBlend(
        _blendCylinderDiameter,
        _blendCornerZ,
        _blendConeAngle,
        _blendRadius,
        IsBlendConcaveFillet,
        IsBlendLeftToRight,
        IsBlendHole,
        IsBlendFrontTurret,
        CompensateBlendToolNose,
        _blendToolNoseRadius,
        UseBlendFeed,
        _blendFeedRate,
        _blendSpindleRpm);
    }

}
