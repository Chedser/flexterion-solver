using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MechCore.Core;
using MechCore.Models.Turning;
using MechCore.Services.Turning;

namespace MechCore.ViewModels.Turning
{
    public class CncSystemOption
    {
        public CncSystemKind Kind { get; }
        public string Title { get; }
        public string Subtitle { get; }

        public CncSystemOption(CncSystemKind kind, string title, string subtitle)
        {
            Kind = kind;
            Title = title;
            Subtitle = subtitle;
        }
    }

    public class TurningHeaderViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly ITurningHeaderService _headerService;
        private bool _isUpdatingInternal;

        public TurningHeaderViewModel(MainViewModel mainViewModel)
            : this(mainViewModel, new TurningHeaderService())
        {
        }

        public TurningHeaderViewModel(MainViewModel mainViewModel, ITurningHeaderService headerService)
        {
            _mainViewModel = mainViewModel;
            _headerService = headerService;

            _mainViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsOperatorMode) ||
                    e.PropertyName == nameof(MainViewModel.IsEngineerMode))
                {
                    OnPropertyChanged(nameof(IsOperatorMode));
                }
            };

            SystemOptions = new List<CncSystemOption>
            {
                new(CncSystemKind.FanucSystemA,
                    "Fanuc / ISO — System A (стандарт)",
                    "Самый распространённый токарный стандарт: оси X/Z (абсолют) и U/W (приращения), ограничение G50, подача G99."),
                new(CncSystemKind.FanucSystemBC,
                    "Fanuc / ISO — System B / C",
                    "Единая с фрезерной система координат: используются G90/G91, ограничение оборотов через G92, подача на оборот G95."),
                new(CncSystemKind.CustomManual,
                    "Другая стойка / Ручной режим",
                    "Полный ручной контроль всех G-кодов. Обязательно сверьте выбранные команды с руководством вашего станка.")
            };

            WorkOffsets = new List<string> { "Не выводить", "G54", "G55", "G56", "G57", "G58", "G59" };
            ToolNumbers = Enumerable.Range(1, 12).Select(i => $"{i:D2}").ToList();
            OffsetNumbers = Enumerable.Range(0, 13).Select(i => $"{i:D2}").ToList();
            RpmLimitCodeOptions = new List<string> { "G50", "G92" };
            HomeSyntaxOptions = new List<string> { "G28 U0 / G28 W0 (System A)", "G91 G28 X0 / Z0 + G90 (System B/C)" };
            SpindleDirections = new List<string> { "M03 (по часовой)", "M04 (против часовой)" };

            CopyGCodeCommand = new RelayCommand(_ => CopyGCode(), _ => CanCopyGCode);
            ResetDefaultsCommand = new RelayCommand(_ => ApplySystemPreset(SelectedSystem.Kind));

            _selectedSystem = SystemOptions[0];
            ApplySystemPreset(CncSystemKind.FanucSystemA);
        }

        public bool IsOperatorMode => _mainViewModel.IsOperatorMode;

        // --- Коллекции для ComboBox ---
        public List<CncSystemOption> SystemOptions { get; }
        public List<string> WorkOffsets { get; }
        public List<string> ToolNumbers { get; }
        public List<string> OffsetNumbers { get; }
        public List<string> RpmLimitCodeOptions { get; }
        public List<string> HomeSyntaxOptions { get; }
        public List<string> SpindleDirections { get; }

        public ObservableCollection<GCodeLineExplanation> LineExplanations { get; } = new();
        private bool _hasWarning;
        public bool HasWarning
        {
            get => _hasWarning;
            private set => SetProperty(ref _hasWarning, value);
        }

        private string _warningMessage = string.Empty;
        public string WarningMessage
        {
            get => _warningMessage;
            private set => SetProperty(ref _warningMessage, value);
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            private set => SetProperty(ref _hasError, value);
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }


        public ICommand CopyGCodeCommand { get; }
        public ICommand ResetDefaultsCommand { get; }

        // --- Выбор системы ЧПУ ---
        private CncSystemOption _selectedSystem;
        public CncSystemOption SelectedSystem
        {
            get => _selectedSystem;
            set
            {
                if (value != null && SetProperty(ref _selectedSystem, value))
                {
                    ApplySystemPreset(value.Kind);
                    OnPropertyChanged(nameof(IsSystemA));
                    OnPropertyChanged(nameof(IsSystemBC));
                    OnPropertyChanged(nameof(IsManualSystem));
                    OnPropertyChanged(nameof(IsG90Allowed));
                    OnPropertyChanged(nameof(FeedPerRevCodeLabel));
                    OnPropertyChanged(nameof(RpmLimitHeaderLabel));
                }
            }
        }

        public bool IsSystemA => SelectedSystem.Kind == CncSystemKind.FanucSystemA;
        public bool IsSystemBC => SelectedSystem.Kind == CncSystemKind.FanucSystemBC;
        public bool IsManualSystem => SelectedSystem.Kind == CncSystemKind.CustomManual;
        public bool IsG90Allowed => SelectedSystem.Kind != CncSystemKind.FanucSystemA;

        public string FeedPerRevCodeLabel => SelectedSystem.Kind == CncSystemKind.FanucSystemBC ? "G95" : "G99";
        public string RpmLimitHeaderLabel => $"Ограничение максимальных оборотов ({SelectedRpmLimitCode})";

        // --- Номер программы ---
        private bool _includeProgramNumber = true;
        public bool IncludeProgramNumber
        {
            get => _includeProgramNumber;
            set { if (SetProperty(ref _includeProgramNumber, value)) Recalculate(); }
        }

        private string _programNumberRaw = "0001";
        public string ProgramNumberRaw
        {
            get => _programNumberRaw;
            set { if (SetProperty(ref _programNumberRaw, value)) Recalculate(); }
        }

        // --- Коды строки безопасности ---
        private bool _useG18 = true;
        public bool UseG18 { get => _useG18; set { if (SetProperty(ref _useG18, value)) Recalculate(); } }

        private bool _useG21 = true;
        public bool UseG21 { get => _useG21; set { if (SetProperty(ref _useG21, value)) Recalculate(); } }

        private bool _useG40 = true;
        public bool UseG40 { get => _useG40; set { if (SetProperty(ref _useG40, value)) Recalculate(); } }

        private bool _useG80 = true;
        public bool UseG80 { get => _useG80; set { if (SetProperty(ref _useG80, value)) Recalculate(); } }

        private bool _useG97Safety = true;
        public bool UseG97Safety { get => _useG97Safety; set { if (SetProperty(ref _useG97Safety, value)) Recalculate(); } }

        private bool _useFeedPerRev = true;
        public bool UseFeedPerRev { get => _useFeedPerRev; set { if (SetProperty(ref _useFeedPerRev, value)) Recalculate(); } }

        private bool _useG90;
        public bool UseG90 { get => _useG90; set { if (SetProperty(ref _useG90, value)) Recalculate(); } }
        // --- Рабочая система координат (РСК) ---
        private string _selectedWorkOffset = "G54";
        public string SelectedWorkOffset
        {
            get => _selectedWorkOffset;
            set { if (SetProperty(ref _selectedWorkOffset, value)) Recalculate(); }
        }

        // --- Безопасный выход в ноль станка (G28) ---
        private bool _useSafeHomeReturn = true;
        public bool UseSafeHomeReturn
        {
            get => _useSafeHomeReturn;
            set { if (SetProperty(ref _useSafeHomeReturn, value)) Recalculate(); }
        }

        private string _selectedHomeSyntax = "G28 U0 / G28 W0 (System A)";
        public string SelectedHomeSyntax
        {
            get => _selectedHomeSyntax;
            set { if (SetProperty(ref _selectedHomeSyntax, value)) Recalculate(); }
        }

        // --- Ограничение максимальных оборотов (G50 / G92) ---
        private bool _useMaxRpmLimit = true;
        public bool UseMaxRpmLimit
        {
            get => _useMaxRpmLimit;
            set { if (SetProperty(ref _useMaxRpmLimit, value)) Recalculate(); }
        }

        private string _selectedRpmLimitCode = "G50";
        public string SelectedRpmLimitCode
        {
            get => _selectedRpmLimitCode;
            set
            {
                if (SetProperty(ref _selectedRpmLimitCode, value))
                {
                    OnPropertyChanged(nameof(RpmLimitHeaderLabel));
                    Recalculate();
                }
            }
        }

        private string _maxRpmRaw = "2500";
        public string MaxRpmRaw
        {
            get => _maxRpmRaw;
            set { if (SetProperty(ref _maxRpmRaw, value)) Recalculate(); }
        }

        // --- Вызов инструмента и корректора ---
        private bool _includeToolCall = true;
        public bool IncludeToolCall
        {
            get => _includeToolCall;
            set { if (SetProperty(ref _includeToolCall, value)) Recalculate(); }
        }

        private string _selectedToolNumber = "01";
        public string SelectedToolNumber
        {
            get => _selectedToolNumber;
            set
            {
                if (SetProperty(ref _selectedToolNumber, value))
                {
                    // Автоматически подставляем номер корректора, равный номеру инструмента
                    if (!_isUpdatingInternal && !string.IsNullOrEmpty(value))
                    {
                        _isUpdatingInternal = true;
                        SelectedOffsetNumber = value;
                        _isUpdatingInternal = false;
                    }
                    Recalculate();
                }
            }
        }

        private string _selectedOffsetNumber = "01";
        public string SelectedOffsetNumber
        {
            get => _selectedOffsetNumber;
            set { if (SetProperty(ref _selectedOffsetNumber, value)) Recalculate(); }
        }

        // --- Запуск шпинделя и СОЖ ---
        private bool _includeSpindleStart = true;
        public bool IncludeSpindleStart
        {
            get => _includeSpindleStart;
            set
            {
                if (SetProperty(ref _includeSpindleStart, value))
                {
                    if (value && UseConstantSurfaceSpeed && !UseMaxRpmLimit)
                        UseMaxRpmLimit = true;
                    Recalculate();
                }
            }
        }

        private bool _useConstantSurfaceSpeed = true; // true = G96, false = G97
        public bool UseConstantSurfaceSpeed
        {
            get => _useConstantSurfaceSpeed;
            set
            {
                if (SetProperty(ref _useConstantSurfaceSpeed, value))
                {
                    if (!_isUpdatingInternal)
                    {
                        _isUpdatingInternal = true;
                        if (value)
                        {
                            // При переходе на G96 автоматически включаем защиту G50/G92 и ставим типовое Vc
                            UseMaxRpmLimit = true;
                            if (SpeedValueRaw == "1200") SpeedValueRaw = "180";
                        }
                        else
                        {
                            // При переходе на G97 подставляем типовые обороты
                            if (SpeedValueRaw == "180") SpeedValueRaw = "1200";
                        }
                        _isUpdatingInternal = false;
                    }
                    OnPropertyChanged(nameof(SpeedInputHint));
                    Recalculate();
                }
            }
        }

        public string SpeedInputHint => UseConstantSurfaceSpeed
            ? "Скорость резания Vc (м/мин)"
            : "Частота вращения (об/мин)";

        private string _speedValueRaw = "180";
        public string SpeedValueRaw
        {
            get => _speedValueRaw;
            set { if (SetProperty(ref _speedValueRaw, value)) Recalculate(); }
        }

        private string _selectedSpindleDirection = "M03 (по часовой)";
        public string SelectedSpindleDirection
        {
            get => _selectedSpindleDirection;
            set { if (SetProperty(ref _selectedSpindleDirection, value)) Recalculate(); }
        }

        private bool _useCoolant = true;
        public bool UseCoolant
        {
            get => _useCoolant;
            set { if (SetProperty(ref _useCoolant, value)) Recalculate(); }
        }

        // --- Выходные свойства результата ---
        private string _generatedGCode = string.Empty;
        public string GeneratedGCode
        {
            get => _generatedGCode;
            private set
            {
                if (SetProperty(ref _generatedGCode, value))
                    OnPropertyChanged(nameof(CanCopyGCode));
            }
        }

        private bool _isCopySuccessVisible;
        public bool IsCopySuccessVisible
        {
            get => _isCopySuccessVisible;
            private set => SetProperty(ref _isCopySuccessVisible, value);
        }

        public bool CanCopyGCode => !string.IsNullOrWhiteSpace(GeneratedGCode) && GeneratedGCode != "—";

        // --- Применение пресета при смене системы ЧПУ ---
        private void ApplySystemPreset(CncSystemKind kind)
        {
            _isUpdatingInternal = true;
            try
            {
                UseG18 = true;
                UseG21 = true;
                UseG40 = true;
                UseG80 = true;
                UseG97Safety = true;
                UseFeedPerRev = true;

                switch (kind)
                {
                    case CncSystemKind.FanucSystemA:
                        UseG90 = false;
                        SelectedRpmLimitCode = "G50";
                        SelectedHomeSyntax = HomeSyntaxOptions[0];
                        break;

                    case CncSystemKind.FanucSystemBC:
                        UseG90 = true;
                        SelectedRpmLimitCode = "G92";
                        SelectedHomeSyntax = HomeSyntaxOptions[1];
                        break;

                    case CncSystemKind.CustomManual:
                        UseG90 = false;
                        break;
                }
            }
            finally
            {
                _isUpdatingInternal = false;
            }

            Recalculate();
        }

        // --- Пересчёт шапки через сервис ---
        private void Recalculate()
        {
            if (_headerService == null) return;

            var request = new TurningHeaderRequest
            {
                SystemKind = SelectedSystem.Kind,
                IncludeProgramNumber = IncludeProgramNumber,
                ProgramNumberRaw = ProgramNumberRaw,
                UseG18 = UseG18,
                UseG21 = UseG21,
                UseG40 = UseG40,
                UseG80 = UseG80,
                UseG97Safety = UseG97Safety,
                UseFeedPerRev = UseFeedPerRev,
                UseG90 = UseG90,
                WorkOffset = SelectedWorkOffset,
                UseSafeHomeReturn = UseSafeHomeReturn,
                UseSystemAHomeSyntax = SelectedHomeSyntax == HomeSyntaxOptions[0],
                UseMaxRpmLimit = UseMaxRpmLimit,
                RpmLimitCode = SelectedRpmLimitCode,
                MaxRpmRaw = MaxRpmRaw,
                IncludeToolCall = IncludeToolCall,
                ToolNumber = SelectedToolNumber,
                OffsetNumber = SelectedOffsetNumber,
                IncludeSpindleStart = IncludeSpindleStart,
                UseConstantSurfaceSpeed = UseConstantSurfaceSpeed,
                SpeedValueRaw = SpeedValueRaw,
                IsSpindleClockwise = SelectedSpindleDirection.StartsWith("M03"),
                UseCoolant = UseCoolant
            };

            TurningHeaderResult result = _headerService.GenerateHeader(request);

            GeneratedGCode = result.GeneratedGCode;

            LineExplanations.Clear();
            foreach (var item in result.LineExplanations)
                LineExplanations.Add(item);

            HasWarning = result.HasWarning;
            WarningMessage = result.WarningMessage;
            HasError = result.HasError;
            ErrorMessage = result.ErrorMessage;

            IsCopySuccessVisible = false;
        }

        private void CopyGCode()
        {
            if (!CanCopyGCode) return;
            Clipboard.SetText(GeneratedGCode);
            IsCopySuccessVisible = true;
        }
    }
}
