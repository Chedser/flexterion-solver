using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using MechCore.Core;
using MechCore.Models.Threads;
using MechCore.Services.Threads;
using MechCore.Models.Materials;
using MechCore.Services.Materials;

namespace MechCore.ViewModels.Threads
{
    public class ThreadsViewModel : ObservableObject
    {
        private readonly IThreadService _threadService;
        private List<ThreadSpecItem> _currentTypeSpecs = new();

        private ThreadTypeItem _selectedThreadType;
        private List<string> _availableSizes = new();
        private string _selectedSize = string.Empty;
        private List<ThreadSpecItem> _availablePitches = new();
        private ThreadSpecItem? _selectedSpec;

        private readonly IMaterialService _materialService;
        private readonly List<MaterialGradeItem> _allMaterialGrades;

        public IReadOnlyList<IsoMaterialGroupItem> IsoGroups { get; }
        public ICommand SelectThreadIsoGroupCommand { get; }
        public ICommand ClearThreadGradeCommand { get; }

        private bool _considerThreadMaterial;
        /// <summary>Учитывать материал заготовки при подборе сверла, раскатника и проходов G76.</summary>
        public bool ConsiderThreadMaterial
        {
            get => _considerThreadMaterial;
            set
            {
                if (SetProperty(ref _considerThreadMaterial, value))
                {
                    OnPropertyChanged(nameof(OperatorResult));
                    OnPropertyChanged(nameof(ControlAndG76Result));
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
                    if (ConsiderThreadMaterial)
                    {
                        OnPropertyChanged(nameof(OperatorResult));
                        OnPropertyChanged(nameof(ControlAndG76Result));
                    }
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
                    if (ConsiderThreadMaterial)
                    {
                        OnPropertyChanged(nameof(OperatorResult));
                        OnPropertyChanged(nameof(ControlAndG76Result));
                    }
                }
            }
        }

        public bool HasSelectedThreadMaterial => SelectedThreadMaterial != null;

        private bool _isRollFormingThread;
        /// <summary>false — Метчик / резец (резать), true — Раскатник (накатывание без стружки).</summary>
        public bool IsRollFormingThread
        {
            get => _isRollFormingThread;
            set
            {
                if (SetProperty(ref _isRollFormingThread, value))
                {
                    OnPropertyChanged(nameof(OperatorResult));
                    OnPropertyChanged(nameof(ControlAndG76Result));
                }
            }
        }

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


        private bool _isInternalThread = false;
        private string _threadLengthText = "25";
        private double _threadLength = 25.0;
        private bool _hasReliefGroove = false;

        private string _threadStartZText = "0";
        private double _threadStartZ = 0.0;

        private string _leadInText = "2";
        private double _leadIn = 2.0;

        private string _overtravelText = "0";
        private double _overtravel = 0.0;

        private string _selectedBoltClass = "6g";
        private string _selectedNutClass = "6H";

        public ICommand CopyG76Command { get; }
        public string ThreadLengthText
        {
            get => _threadLengthText;
            set
            {
                if (SetProperty(ref _threadLengthText, value))
                {
                    string norm = value.Replace(',', '.');
                    if (double.TryParse(norm, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double parsed) && parsed > 0 && parsed <= 2000)
                    {
                        _threadLength = parsed;
                        OnPropertyChanged(nameof(ControlAndG76Result));
                    }
                }
            }
        }

        public bool HasReliefGroove
        {
            get => _hasReliefGroove;
            set
            {
                if (SetProperty(ref _hasReliefGroove, value))
                {
                    OnPropertyChanged(nameof(ControlAndG76Result));
                }
            }
        }

        public string ThreadStartZText
        {
            get => _threadStartZText;
            set
            {
                if (SetProperty(ref _threadStartZText, value))
                {
                    string norm = value.Replace(',', '.');
                    if (double.TryParse(norm, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double parsed) && parsed >= -5000 && parsed <= 5000)
                    {
                        _threadStartZ = parsed;
                        OnPropertyChanged(nameof(ControlAndG76Result));
                    }
                }
            }
        }

        public string LeadInText
        {
            get => _leadInText;
            set
            {
                if (SetProperty(ref _leadInText, value))
                {
                    string norm = value.Replace(',', '.');
                    if (double.TryParse(norm, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double parsed) && parsed >= 0 && parsed <= 500)
                    {
                        _leadIn = parsed;
                        OnPropertyChanged(nameof(ControlAndG76Result));
                    }
                }
            }
        }

        public string OvertravelText
        {
            get => _overtravelText;
            set
            {
                if (SetProperty(ref _overtravelText, value))
                {
                    string norm = value.Replace(',', '.');
                    if (double.TryParse(norm, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double parsed) && parsed >= 0 && parsed <= 500)
                    {
                        _overtravel = parsed;
                        OnPropertyChanged(nameof(ControlAndG76Result));
                    }
                }
            }
        }

        public List<string> BoltToleranceClasses { get; } = new() { "6g", "6e", "8g", "6h", "4h" };
        public List<string> NutToleranceClasses { get; } = new() { "6H", "5H", "7H", "6G" };
        public string SelectedBoltClass
        {
            get => _selectedBoltClass;
            set
            {
                if (SetProperty(ref _selectedBoltClass, value))
                {
                    OnPropertyChanged(nameof(EngineerResult));
                }
            }
        }

        public string SelectedNutClass
        {
            get => _selectedNutClass;
            set
            {
                if (SetProperty(ref _selectedNutClass, value))
                {
                    OnPropertyChanged(nameof(EngineerResult));
                }
            }
        }

        public List<ThreadFitPresetItem> ThreadFitPresets { get; } = new()
        {
            new("6H", "6g", "средний класс (базовая)"),
            new("6H", "6e", "с увеличенным зазором"),
            new("6G", "6e", "под толстое покрытие"),
            new("7H", "8g", "грубый класс"),
            new("5H", "4h", "точный класс")
        };

        public ICommand ApplyThreadFitPresetCommand { get; }
        public ThreadEngineerResult? EngineerResult => SelectedSpec != null
            ? new ThreadEngineerResult(SelectedSpec, SelectedBoltClass, SelectedNutClass)
            : null;

        public ThreadControlAndG76Result? ControlAndG76Result =>
                                            SelectedSpec != null && OperatorResult != null
                                                ? new ThreadControlAndG76Result(
                                                    SelectedSpec,
                                                    OperatorResult,
                                                    IsInternalThread,
                                                    _threadStartZ,
                                                    _leadIn,
                                                    _overtravel,
                                                    _threadLength,
                                                    HasReliefGroove)
                                                : null;

        public MainViewModel Main { get; }
        public List<ThreadTypeItem> ThreadTypes { get; }
        public ICommand SelectThreadTypeCommand { get; }

        public ThreadOperatorResult? OperatorResult => SelectedSpec != null
                                                        ? new ThreadOperatorResult(
                                                            SelectedSpec,
                                                            IsInternalThread,
                                                            IsRollFormingThread,
                                                            ConsiderThreadMaterial,
                                                            SelectedThreadIsoGroup,
                                                            SelectedThreadMaterial)
                                                        : null;

        public ThreadsViewModel(
     MainViewModel main,
     IThreadService? threadService = null,
     IMaterialService? materialService = null)
        {
            Main = main;
            _threadService = threadService ?? new GostThreadService();
            _materialService = materialService ?? new MaterialStandardService();

            IsoGroups = _materialService.GetIsoGroups();
            _allMaterialGrades = _materialService.GetAllGrades();
            _selectedThreadIsoGroup = IsoGroups.First();

            SelectThreadIsoGroupCommand = new RelayCommand(param =>
            {
                if (param is IsoMaterialGroupItem group)
                    SelectedThreadIsoGroup = group;
            });

            ClearThreadGradeCommand = new RelayCommand(_ => SelectedThreadMaterial = null);

            UpdateFilteredThreadGrades();

            ThreadTypes = _threadService.GetThreadTypes();
            _selectedThreadType = ThreadTypes.First();

            SelectThreadTypeCommand = new RelayCommand(param =>
            {
                if (param is ThreadTypeItem item)
                    SelectedThreadType = item;
            });

            CopyG76Command = new RelayCommand(_ =>
            {
                if (ControlAndG76Result != null)
                    System.Windows.Clipboard.SetText(ControlAndG76Result.G76CodeBlock);
            });

            ApplyThreadFitPresetCommand = new RelayCommand(param =>
            {
                if (param is ThreadFitPresetItem preset)
                {
                    SelectedNutClass = preset.NutClass;
                    SelectedBoltClass = preset.BoltClass;
                }
            });

            LoadSpecsForCurrentType(preferredSize: "M16");
        }

        public ThreadTypeItem SelectedThreadType
        {
            get => _selectedThreadType;
            set
            {
                if (SetProperty(ref _selectedThreadType, value) && value != null)
                {
                    LoadSpecsForCurrentType();
                }
            }
        }

        public List<string> AvailableSizes
        {
            get => _availableSizes;
            private set => SetProperty(ref _availableSizes, value);
        }

        public string SelectedSize
        {
            get => _selectedSize;
            set
            {
                if (SetProperty(ref _selectedSize, value) && !string.IsNullOrEmpty(value))
                {
                    LoadPitchesForSelectedSize();
                }
            }
        }

        public List<ThreadSpecItem> AvailablePitches
        {
            get => _availablePitches;
            private set => SetProperty(ref _availablePitches, value);
        }

        public ThreadSpecItem? SelectedSpec
        {
            get => _selectedSpec;
            set
            {
                if (SetProperty(ref _selectedSpec, value))
                {
                    OnPropertyChanged(nameof(OperatorResult));
                    OnPropertyChanged(nameof(ControlAndG76Result));
                    OnPropertyChanged(nameof(EngineerResult));
                }
            }
        }

        public bool IsInternalThread
        {
            get => _isInternalThread;
            set
            {
                if (SetProperty(ref _isInternalThread, value))
                {
                    OnPropertyChanged(nameof(ThreadLocationTitle));
                    OnPropertyChanged(nameof(OperatorResult));
                    OnPropertyChanged(nameof(ControlAndG76Result));
                }
            }
        }

        public string ThreadLocationTitle => IsInternalThread
            ? "Внутренняя резьба (гайка / отверстие)"
            : "Наружная резьба (болт / вал)";

        private void LoadSpecsForCurrentType(string? preferredSize = null)
        {
            _currentTypeSpecs = _threadService.GetSpecsByType(SelectedThreadType.Type);

            AvailableSizes = _currentTypeSpecs
                .Select(x => x.SizeDesignation)
                .Distinct()
                .ToList();

            if (preferredSize != null && AvailableSizes.Contains(preferredSize))
            {
                SelectedSize = preferredSize;
            }
            else
            {
                SelectedSize = AvailableSizes.FirstOrDefault() ?? string.Empty;
            }
        }

        private void LoadPitchesForSelectedSize()
        {
            AvailablePitches = _currentTypeSpecs
                .Where(x => x.SizeDesignation == SelectedSize)
                .ToList();

            SelectedSpec = AvailablePitches.FirstOrDefault(x => x.IsCoarse)
                           ?? AvailablePitches.FirstOrDefault();
        }
    }

    public class ThreadFitPresetItem
    {
        public string NutClass { get; }
        public string BoltClass { get; }
        public string Label { get; }
        public string Description { get; }

        public ThreadFitPresetItem(string nutClass, string boltClass, string description)
        {
            NutClass = nutClass;
            BoltClass = boltClass;
            Label = $"{nutClass} / {boltClass}";
            Description = description;
        }
    }


}
