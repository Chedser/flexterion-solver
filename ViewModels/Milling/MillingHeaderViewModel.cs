using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MechCore.Core;
using MechCore.Models.Milling;
using MechCore.Models.Turning;
using MechCore.Services.Milling;

namespace MechCore.ViewModels.Milling
{
    public class MillingCncSystemOption
    {
        public MillingCncSystemKind Kind { get; }
        public string Title { get; }
        public string Subtitle { get; }

        public MillingCncSystemOption(MillingCncSystemKind kind, string title, string subtitle)
        {
            Kind = kind;
            Title = title;
            Subtitle = subtitle;
        }
    }

    public class MillingHeaderViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly IMillingHeaderService _headerService;
        private bool _isUpdatingInternal;

        public MillingHeaderViewModel(MainViewModel mainViewModel)
            : this(mainViewModel, new MillingHeaderService())
        {
        }

        public MillingHeaderViewModel(MainViewModel mainViewModel, IMillingHeaderService headerService)
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

            SystemOptions = new List<MillingCncSystemOption>
            {
                new(MillingCncSystemKind.FanucIso,
                    "Fanuc / ISO (стандарт 3-осевой)",
                    "Классический фрезерный формат: G17 G21 G40 G49 G80 G90 G94, выход по Z через G91 G28 Z0, смена T01 M06 и корректор длины G43 H01."),
                new(MillingCncSystemKind.Haas,
                    "Haas (NGC / Classic)",
                    "Фрезерный формат Haas: безопасный подъём шпинделя в машинный ноль G00 G53 Z0, вызов T01 M06 и компенсация длины G43 H01."),
                new(MillingCncSystemKind.SinumerikIso,
                    "Siemens Sinumerik (ISO-режим)",
                    "Синтаксис Sinumerik 840D/828D: метрика G71, отвод SUPA G0 Z0 D0, вызов инструмента и корректора T1 D1 M06 без отдельного G43."),
                new(MillingCncSystemKind.CustomManual,
                    "Другая стойка / Ручной режим",
                    "Полный ручной контроль всех G-кодов, формата отвода по оси Z и синтаксиса вызова инструмента.")
            };

            WorkPlanes = new List<string> { "G17", "G18", "G19", "Не выводить" };
            WorkOffsets = new List<string> { "Не выводить", "G54", "G55", "G56", "G57", "G58", "G59" };
            ToolNumbers = Enumerable.Range(1, 24).Select(i => $"{i:D2}").ToList();
            OffsetNumbers = Enumerable.Range(0, 25).Select(i => $"{i:D2}").ToList();
            HomeSyntaxOptions = new List<string>
            {
                "G91 G28 Z0 + G90 (Fanuc стандарт)",
                "G00 G53 Z0 (машинные координаты Haas)",
                "SUPA G0 Z0 D0 (Sinumerik)"
            };
            ToolChangeSyntaxOptions = new List<string>
            {
                "T01 M06 + G43 H01 (Fanuc / Haas)",
                "T1 D1 M06 (Sinumerik)"
            };
            SpindleDirections = new List<string> { "M03 (по часовой)", "M04 (против часовой)" };
            CoolantOptions = new List<string> { "M08 (СОЖ эмульсия)", "M07 (воздух / туман)", "Не выводить" };

            CopyGCodeCommand = new RelayCommand(_ => CopyGCode(), _ => CanCopyGCode);
            ResetDefaultsCommand = new RelayCommand(_ => ApplySystemPreset(SelectedSystem.Kind));

            _selectedSystem = SystemOptions[0];
            ApplySystemPreset(MillingCncSystemKind.FanucIso);
        }

        public bool IsOperatorMode => _mainViewModel.IsOperatorMode;

        // --- Коллекции для UI ---
        public List<MillingCncSystemOption> SystemOptions { get; }
        public List<string> WorkPlanes { get; }
        public List<string> WorkOffsets { get; }
        public List<string> ToolNumbers { get; }
        public List<string> OffsetNumbers { get; }
        public List<string> HomeSyntaxOptions { get; }
        public List<string> ToolChangeSyntaxOptions { get; }
        public List<string> SpindleDirections { get; }
        public List<string> CoolantOptions { get; }

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
        private MillingCncSystemOption _selectedSystem;
        public MillingCncSystemOption SelectedSystem
        {
            get => _selectedSystem;
            set
            {
                if (value != null && SetProperty(ref _selectedSystem, value))
                {
                    ApplySystemPreset(value.Kind);
                    OnPropertyChanged(nameof(IsManualSystem));
                    OnPropertyChanged(nameof(IsSinumerikSystem));
                    OnPropertyChanged(nameof(MetricCodeLabel));
                    OnPropertyChanged(nameof(OffsetComboHint));
                }
            }
        }

        public bool IsManualSystem => SelectedSystem.Kind == MillingCncSystemKind.CustomManual;
        public bool IsSinumerikSystem => SelectedSystem.Kind == MillingCncSystemKind.SinumerikIso;
        public string MetricCodeLabel => IsSinumerikSystem ? "G71" : "G21";
        public string OffsetComboHint => IsSinumerikSystem || SelectedToolChangeSyntax == ToolChangeSyntaxOptions[1]
            ? "Корректор D (Sinumerik)"
            : "Корректор длины H";

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
        private string _selectedWorkPlane = "G17";
        public string SelectedWorkPlane
        {
            get => _selectedWorkPlane;
            set { if (SetProperty(ref _selectedWorkPlane, value)) Recalculate(); }
        }

        private bool _useMetricG21 = true;
        public bool UseMetricG21 { get => _useMetricG21; set { if (SetProperty(ref _useMetricG21, value)) Recalculate(); } }

        private bool _useG40 = true;
        public bool UseG40 { get => _useG40; set { if (SetProperty(ref _useG40, value)) Recalculate(); } }

        private bool _useG49 = true;
        public bool UseG49 { get => _useG49; set { if (SetProperty(ref _useG49, value)) Recalculate(); } }

        private bool _useG80 = true;
        public bool UseG80 { get => _useG80; set { if (SetProperty(ref _useG80, value)) Recalculate(); } }

        private bool _useG90 = true;
        public bool UseG90 { get => _useG90; set { if (SetProperty(ref _useG90, value)) Recalculate(); } }

        private bool _useG94 = true;
        public bool UseG94 { get => _useG94; set { if (SetProperty(ref _useG94, value)) Recalculate(); } }

        // --- Безопасный отвод по Z и рабочая система координат ---
        private bool _useSafeZHome = true;
        public bool UseSafeZHome
        {
            get => _useSafeZHome;
            set { if (SetProperty(ref _useSafeZHome, value)) Recalculate(); }
        }

        private string _selectedHomeSyntax = "G91 G28 Z0 + G90 (Fanuc стандарт)";
        public string SelectedHomeSyntax
        {
            get => _selectedHomeSyntax;
            set { if (SetProperty(ref _selectedHomeSyntax, value)) Recalculate(); }
        }

        private string _selectedWorkOffset = "G54";
        public string SelectedWorkOffset
        {
            get => _selectedWorkOffset;
            set { if (SetProperty(ref _selectedWorkOffset, value)) Recalculate(); }
        }

        // --- Вызов и смена инструмента ---
        private bool _includeToolCall = true;
        public bool IncludeToolCall
        {
            get => _includeToolCall;
            set { if (SetProperty(ref _includeToolCall, value)) Recalculate(); }
        }

        private string _selectedToolChangeSyntax = "T01 M06 + G43 H01 (Fanuc / Haas)";
        public string SelectedToolChangeSyntax
        {
            get => _selectedToolChangeSyntax;
            set
            {
                if (SetProperty(ref _selectedToolChangeSyntax, value))
                {
                    OnPropertyChanged(nameof(OffsetComboHint));
                    Recalculate();
                }
            }
        }

        private string _selectedToolNumber = "01";
        public string SelectedToolNumber
        {
            get => _selectedToolNumber;
            set
            {
                if (SetProperty(ref _selectedToolNumber, value))
                {
                    if (!_isUpdatingInternal && !string.IsNullOrEmpty(value))
                    {
                        _isUpdatingInternal = true;
                        SelectedLengthOffsetNumber = value;
                        _isUpdatingInternal = false;
                    }
                    Recalculate();
                }
            }
        }
        private string _selectedLengthOffsetNumber = "01";
        public string SelectedLengthOffsetNumber
        {
            get => _selectedLengthOffsetNumber;
            set { if (SetProperty(ref _selectedLengthOffsetNumber, value)) Recalculate(); }
        }
        // --- Запуск шпинделя, безопасный подвод Z и СОЖ ---
        private bool _includeSpindleStart = true;
        public bool IncludeSpindleStart
        {
            get => _includeSpindleStart;
            set { if (SetProperty(ref _includeSpindleStart, value)) Recalculate(); }
        }
        private string _spindleRpmRaw = "3000";
        public string SpindleRpmRaw
        {
            get => _spindleRpmRaw;
            set { if (SetProperty(ref _spindleRpmRaw, value)) Recalculate(); }
        }
        private string _selectedSpindleDirection = "M03 (по часовой)";
        public string SelectedSpindleDirection
        {
            get => _selectedSpindleDirection;
            set { if (SetProperty(ref _selectedSpindleDirection, value)) Recalculate(); }
        }
        private bool _useSafeZApproach = true;
        public bool UseSafeZApproach
        {
            get => _useSafeZApproach;
            set { if (SetProperty(ref _useSafeZApproach, value)) Recalculate(); }
        }
        private string _safeZHeightRaw = "50";
        public string SafeZHeightRaw
        {
            get => _safeZHeightRaw;
            set { if (SetProperty(ref _safeZHeightRaw, value)) Recalculate(); }
        }
        private string _selectedCoolantOption = "M08 (СОЖ эмульсия)";
        public string SelectedCoolantOption
        {
            get => _selectedCoolantOption;
            set { if (SetProperty(ref _selectedCoolantOption, value)) Recalculate(); }
        }
        // --- Выходные свойства ---
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
        // --- Применение пресета при смене стойки ЧПУ ---
        private void ApplySystemPreset(MillingCncSystemKind kind)
        {
            _isUpdatingInternal = true;
            try
            {
                SelectedWorkPlane = "G17";
                UseMetricG21 = true;
                UseG40 = true;
                UseG80 = true;
                UseG90 = true;
                UseG94 = true;
                UseSafeZHome = true;
                IncludeToolCall = true;
                IncludeSpindleStart = true;
                UseSafeZApproach = true;
                switch (kind)
                {
                    case MillingCncSystemKind.FanucIso:
                        UseG49 = true;
                        SelectedHomeSyntax = HomeSyntaxOptions[0];
                        SelectedToolChangeSyntax = ToolChangeSyntaxOptions[0];
                        break;
                    case MillingCncSystemKind.Haas:
                        UseG49 = true;
                        SelectedHomeSyntax = HomeSyntaxOptions[1];
                        SelectedToolChangeSyntax = ToolChangeSyntaxOptions[0];
                        break;
                    case MillingCncSystemKind.SinumerikIso:
                        UseG49 = false;
                        SelectedHomeSyntax = HomeSyntaxOptions[2];
                        SelectedToolChangeSyntax = ToolChangeSyntaxOptions[1];
                        if (SelectedLengthOffsetNumber == "00")
                            SelectedLengthOffsetNumber = "01";
                        break;
                    case MillingCncSystemKind.CustomManual:
                        break;
                }
            }
            finally
            {
                _isUpdatingInternal = false;
            }
            OnPropertyChanged(nameof(OffsetComboHint));
            Recalculate();
        }
        // --- Пересчёт через сервис ---
        private void Recalculate()
        {
            if (_headerService == null) return;
            var homeKind = SelectedHomeSyntax switch
            {
                var s when s == HomeSyntaxOptions[1] => MillingHomeSyntaxKind.G53Z0,
                var s when s == HomeSyntaxOptions[2] => MillingHomeSyntaxKind.SupaZ0D0,
                _ => MillingHomeSyntaxKind.G91G28Z0
            };
            var toolSyntaxKind = SelectedToolChangeSyntax == ToolChangeSyntaxOptions[1]
            ? MillingToolChangeSyntaxKind.TxD1M06
            : MillingToolChangeSyntaxKind.TxxM06;
            string coolantCode = SelectedCoolantOption switch
            {
                var s when s.StartsWith("M08") => "M08",
                var s when s.StartsWith("M07") => "M07",
                _ => "Нет"
            };
            var request = new MillingHeaderRequest
            {
                SystemKind = SelectedSystem.Kind,
                IncludeProgramNumber = IncludeProgramNumber,
                ProgramNumberRaw = ProgramNumberRaw,
                WorkPlaneCode = SelectedWorkPlane,
                UseMetricG21 = UseMetricG21,
                UseG40 = UseG40,
                UseG49 = UseG49,
                UseG80 = UseG80,
                UseG90 = UseG90,
                UseG94 = UseG94,
                UseSafeZHome = UseSafeZHome,
                HomeSyntax = homeKind,
                WorkOffset = SelectedWorkOffset,
                IncludeToolCall = IncludeToolCall,
                ToolChangeSyntax = toolSyntaxKind,
                ToolNumber = SelectedToolNumber,
                LengthOffsetNumber = SelectedLengthOffsetNumber,
                IncludeSpindleStart = IncludeSpindleStart,
                SpindleRpmRaw = SpindleRpmRaw,
                IsSpindleClockwise = SelectedSpindleDirection.StartsWith("M03"),
                UseSafeZApproach = UseSafeZApproach,
                SafeZHeightRaw = SafeZHeightRaw,
                CoolantCode = coolantCode
            };
            MillingHeaderResult result = _headerService.GenerateHeader(request);
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