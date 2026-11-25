using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using MechCore.Core;
using MechCore.Models.Tolerances;
using MechCore.Services.Tolerances;

namespace MechCore.ViewModels.Tolerances
{
    public class TolerancesViewModel : ObservableObject
    {
        private readonly IToleranceService _toleranceService;
        private bool _isHoleSystem = true;

        public SetupChartData? SetupChart => ActiveSetupResult != null
                                             ? new SetupChartData(ActiveSetupResult, MeasuredSize)
                                            : null;

        private static readonly HashSet<string> AllowedLargeLetters = new(StringComparer.OrdinalIgnoreCase)
        {
            "D", "E", "F", "G", "H", "JS", "K", "M", "N", "P", "R", "S", "T", "U"
        };

        private double _nominalSize = 40.0;
        private string _nominalSizeText = "40";
        private ToleranceLetterItem _selectedHoleLetter;
        private int _selectedHoleGrade = 7;
        private ToleranceLetterItem _selectedShaftLetter;
        private int _selectedShaftGrade = 6;

        private bool _isSetupShaft = true;
        private double? _measuredSize;
        private string _measuredSizeText = string.Empty;
        private string _errorMessage = string.Empty;
        private string _largeSizeWarning = string.Empty;

        private ToleranceZoneResult? _holeResult;
        private ToleranceZoneResult? _shaftResult;
        private FitCalculationResult? _fitResult;

        public MainViewModel Main { get; }

        public List<ToleranceLetterItem> HoleLetters { get; } = new()
        {
            new("A"), new("B"), new("C"), new("CD"), new("D"), new("E"), new("EF"), new("F"), new("FG"), new("G"), new("H"),
            new("JS"), new("J"), new("K"), new("M"), new("N"),
            new("P"), new("R"), new("S"), new("T"), new("U"), new("V"), new("X"), new("Y"), new("Z"), new("ZA"), new("ZB"), new("ZC")
        };

        public List<ToleranceLetterItem> ShaftLetters { get; } = new()
        {
            new("a"), new("b"), new("c"), new("cd"), new("d"), new("e"), new("ef"), new("f"), new("fg"), new("g"), new("h"),
            new("js"), new("j"), new("k"),
            new("m"), new("n"), new("p"), new("r"), new("s"), new("t"), new("u"), new("v"), new("x"), new("y"), new("z"), new("za"), new("zb"), new("zc")
        };

        public List<int> Grades { get; } = Enumerable.Range(1, 18).ToList();
        public List<string> QuickPresets { get; } = new() { "H7", "H8", "H9", "h6", "h7", "g6", "f7", "js6", "k6", "m6", "p6" };

        private readonly List<FitPresetItem> _holeSystemClearanceFits = new()
        {
            new("H7/h6", "скользящая"),
            new("H7/g6", "движения"),
            new("H7/f7", "ходовая"),
            new("H8/e8", "легкоходовая"),
            new("H9/d9", "широкоходовая"),
            new("H11/d11", "грубая")
        };

        private readonly List<FitPresetItem> _holeSystemTransitionFits = new()
        {
            new("H7/js6", "плотная"),
            new("H7/k6", "напряжённая"),
            new("H7/m6", "тугая"),
            new("H7/n6", "глухая")
        };

        private readonly List<FitPresetItem> _holeSystemInterferenceFits = new()
        {
            new("H7/p6", "легкопрессовая"),
            new("H7/s6", "прессовая средняя"),
            new("H8/u8", "горячепрессовая")
        };

        // Предпочтительные посадки в СИСТЕМЕ ВАЛА (ГОСТ 25347)
        private readonly List<FitPresetItem> _shaftSystemClearanceFits = new()
        {
            new("G7/h6", "движения"),
            new("F8/h7", "ходовая"),
            new("E9/h8", "легкоходовая"),
            new("D9/h9", "широкоходовая"),
            new("D11/h11", "грубая")
        };

        private readonly List<FitPresetItem> _shaftSystemTransitionFits = new()
        {
            new("JS7/h6", "плотная"),
            new("K7/h6", "напряжённая"),
            new("M7/h6", "тугая"),
            new("N7/h6", "глухая")
        };

        private readonly List<FitPresetItem> _shaftSystemInterferenceFits = new()
        {
            new("P7/h6", "легкопрессовая"),
            new("R7/h6", "прессовая средняя"),
            new("S7/h6", "прессовая сильная"),
            new("U8/h8", "горячепрессовая")
        };

        public List<FitPresetItem> ClearanceFits => IsHoleSystem ? _holeSystemClearanceFits : _shaftSystemClearanceFits;
        public List<FitPresetItem> TransitionFits => IsHoleSystem ? _holeSystemTransitionFits : _shaftSystemTransitionFits;
        public List<FitPresetItem> InterferenceFits => IsHoleSystem ? _holeSystemInterferenceFits : _shaftSystemInterferenceFits;

        public bool IsHoleSystem
        {
            get => _isHoleSystem;
            set
            {
                if (SetProperty(ref _isHoleSystem, value))
                {
                    OnPropertyChanged(nameof(ClearanceFits));
                    OnPropertyChanged(nameof(TransitionFits));
                    OnPropertyChanged(nameof(InterferenceFits));
                    OnPropertyChanged(nameof(IsShaftSystem));

                    // При переключении системы сразу подставляем базовую посадку
                    ApplyFitPreset(value ? "H7/k6" : "K7/h6");
                }
            }
        }

        public bool IsShaftSystem
        {
            get => !IsHoleSystem;
            set => IsHoleSystem = !value;
        }

        private bool _showExtraFitInfo;

        public bool ShowExtraFitInfo
        {
            get => _showExtraFitInfo;
            set => SetProperty(ref _showExtraFitInfo, value);
        }

        private bool _showNominalSizesOnChart;

        public bool ShowNominalSizesOnChart
        {
            get => _showNominalSizesOnChart;
            set
            {
                if (SetProperty(ref _showNominalSizesOnChart, value))
                {
                    OnPropertyChanged(nameof(FitChart));
                }
            }
        }

        public ICommand ApplyPresetCommand { get; }
        public ICommand ApplyFitPresetCommand { get; }


        public TolerancesViewModel(MainViewModel main)
        {
            Main = main;
            _toleranceService = new GostToleranceService();

            _selectedHoleLetter = HoleLetters.First(x => x.Letter == "H");
            _selectedShaftLetter = ShaftLetters.First(x => x.Letter == "h");

            ApplyFitPresetCommand = new RelayCommand(param =>
            {
                if (param is string fitPreset)
                    ApplyFitPreset(fitPreset);
            });

            ApplyPresetCommand = new RelayCommand(param =>
            {
                if (param is string preset)
                    ApplyPreset(preset);
            });

            UpdateLettersAvailability(_nominalSize);
            Recalculate();
        }

        public string NominalSizeText
        {
            get => _nominalSizeText;
            set
            {
                if (SetProperty(ref _nominalSizeText, value))
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        ErrorMessage = "Введите номинальный диаметр свыше 0 до 3150 мм.";
                        UpdateLettersAvailability(null);
                        ClearResults();
                        return;
                    }

                    string normalized = value.Replace(',', '.');
                    if (double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                    {
                        if (parsed > 3150)
                        {
                            ErrorMessage = "Превышен максимальный размер! По ГОСТ 25347 допустимый диапазон: свыше 0 до 3150 мм.";
                            UpdateLettersAvailability(parsed);
                            ClearResults();
                            return;
                        }
                        if (parsed <= 0)
                        {
                            ErrorMessage = "Размер должен быть больше 0 мм (до 3150 мм).";
                            UpdateLettersAvailability(parsed);
                            ClearResults();
                            return;
                        }

                        _nominalSize = parsed;
                        OnPropertyChanged(nameof(NominalSize));
                        UpdateLettersAvailability(parsed);
                        Recalculate();
                    }
                    else
                    {
                        ErrorMessage = "Некорректный формат числа. Используйте цифры и точку или запятую.";
                        UpdateLettersAvailability(null);
                        ClearResults();
                    }
                }
            }
        }

        public double NominalSize => _nominalSize;

        public ToleranceLetterItem SelectedHoleLetter
        {
            get => _selectedHoleLetter;
            set
            {
                if (SetProperty(ref _selectedHoleLetter, value))
                    Recalculate();
            }
        }

        public int SelectedHoleGrade
        {
            get => _selectedHoleGrade;
            set
            {
                if (SetProperty(ref _selectedHoleGrade, value))
                {
                    OnPropertyChanged(nameof(GradeDifferenceWarning));
                    Recalculate();
                }
            }
        }

        public ToleranceLetterItem SelectedShaftLetter
        {
            get => _selectedShaftLetter;
            set
            {
                if (SetProperty(ref _selectedShaftLetter, value))
                    Recalculate();
            }
        }

        public int SelectedShaftGrade
        {
            get => _selectedShaftGrade;
            set
            {
                if (SetProperty(ref _selectedShaftGrade, value))
                {
                    OnPropertyChanged(nameof(GradeDifferenceWarning));
                    Recalculate();
                }
            }
        }

        // Текстовое отображение текущего типа элемента в режиме "Наладка"
        public string SetupElementTypeText => IsSetupShaft ? "Вал" : "Отверстие";

        public bool IsSetupShaft
        {
            get => _isSetupShaft;
            set
            {
                if (SetProperty(ref _isSetupShaft, value))
                {
                    OnPropertyChanged(nameof(SetupElementTypeText));
                    OnPropertyChanged(nameof(ActiveSetupResult));
                    OnPropertyChanged(nameof(MeasurementStatusTitle));
                    OnPropertyChanged(nameof(MeasurementDetails));
                    OnPropertyChanged(nameof(IsMeasurementScrap));
                    OnPropertyChanged(nameof(SetupChart));
                }
            }
        }

        public ToleranceZoneResult? ActiveSetupResult => IsSetupShaft ? ShaftResult : HoleResult;

        public string MeasuredSizeText
        {
            get => _measuredSizeText;
            set
            {
                if (SetProperty(ref _measuredSizeText, value))
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        MeasuredSize = null;
                        return;
                    }

                    string normalized = value.Replace(',', '.');
                    if (double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                    {
                        MeasuredSize = parsed;
                    }
                }
            }
        }

        public double? MeasuredSize
        {
            get => _measuredSize;
            set
            {
                if (SetProperty(ref _measuredSize, value))
                    OnPropertyChanged(nameof(MeasurementStatusTitle));
                    OnPropertyChanged(nameof(MeasurementDetails));
                    OnPropertyChanged(nameof(IsMeasurementScrap));
                    OnPropertyChanged(nameof(SetupChart));
            }
        }

        // Флаг окончательного брака (для подсветки текста красным цветом)
        public bool IsMeasurementScrap
        {
            get
            {
                if (MeasuredSize == null || ActiveSetupResult == null)
                    return false;

                double val = MeasuredSize.Value;
                // Для вала брак — только меньше MinSize; для отверстия — больше MaxSize
                return IsSetupShaft
                    ? val < ActiveSetupResult.MinSize
                    : val > ActiveSetupResult.MaxSize;
            }
        }

        // Крупный заголовок результата замера
        public string MeasurementStatusTitle
        {
            get
            {
                if (MeasuredSize == null || ActiveSetupResult == null)
                    return "Ожидание замера";

                double val = MeasuredSize.Value;
                double min = ActiveSetupResult.MinSize;
                double max = ActiveSetupResult.MaxSize;

                if (val >= min && val <= max)
                    return "ГОДЕН (размер в пределах допуска)";

                if (IsSetupShaft)
                {
                    return val > max
                        ? "НЕ В ДОПУСКЕ — ПРИПУСК (можно доточить)"
                        : "ОКОНЧАТЕЛЬНЫЙ БРАК (вал провален)";
                }

                return val < min
                    ? "НЕ В ДОПУСКЕ — НЕДОБОР (можно расточить)"
                    : "ОКОНЧАТЕЛЬНЫЙ БРАК (отверстие прослаблено)";
            }
        }

        // Развёрнутая инструкция для наладчика ЧПУ
        public string MeasurementDetails
        {
            get
            {
                if (MeasuredSize == null || ActiveSetupResult == null)
                    return "Введите показание микрометра или нутромера в поле слева, чтобы рассчитать сдвиг корректора по оси X.";

                double val = MeasuredSize.Value;
                double min = ActiveSetupResult.MinSize;
                double max = ActiveSetupResult.MaxSize;
                double mean = ActiveSetupResult.MeanSize;
                double correction = Math.Round(mean - val, 4);
                string sign = correction >= 0 ? "+" : "";

                // 1. Деталь в допуске
                if (val >= min && val <= max)
                {
                    if (Math.Abs(correction) < 0.0001)
                        return $"Идеальное попадание в середину поля допуска ({mean:0.0000} мм). Корректировка инструмента не требуется.";

                    string action = correction > 0 ? "увеличить диаметр на" : "уменьшить диаметр на";
                    return $"Деталь соответствует чертежу. Чтобы вывести размер точно в середину допуска ({mean:0.0000} мм), нужно {action} {Math.Abs(correction):0.0000} мм — введите {sign}{correction:0.0000} в корректор износа по оси X (на диаметр).";
                }

                // 2. Логика для ВАЛА
                if (IsSetupShaft)
                {
                    if (val > max)
                    {
                        double allowance = Math.Round(val - max, 4);
                        return $"Измеренный диаметр ({val:0.0000} мм) больше верхнего предела ({max:0.0000} мм) на +{allowance:0.0000} мм. Это исправимый припуск. Чтобы снять лишний металл и попасть в середину допуска ({mean:0.0000} мм), введите {correction:0.0000} в корректор износа по оси X (на диаметр) и повторите чистовой проход.";
                    }

                    double undercut = Math.Round(min - val, 4);
                    return $"Измеренный диаметр ({val:0.0000} мм) меньше нижнего предела ({min:0.0000} мм) на -{undercut:0.0000} мм — снято слишком много металла. Для следующей детали отодвиньте резец: введите +{correction:0.0000} в корректор износа по оси X (на диаметр).";
                }

                // 3. Логика для ОТВЕРСТИЯ
                if (val < min)
                {
                    double allowance = Math.Round(min - val, 4);
                    return $"Измеренный диаметр ({val:0.0000} мм) меньше нижнего предела ({min:0.0000} мм) на -{allowance:0.0000} мм. Остался припуск под расточку. Чтобы вывести отверстие в середину допуска ({mean:0.0000} мм), введите +{correction:0.0000} в корректор износа по оси X (на диаметр) и повторите проход.";
                }

                double oversize = Math.Round(val - max, 4);
                return $"Измеренный диаметр ({val:0.0000} мм) больше верхнего предела ({max:0.0000} мм) на +{oversize:0.0000} мм — отверстие прослаблено. Для следующей детали уменьшите вылет резца: введите {correction:0.0000} в корректор износа по оси X (на диаметр).";
            }
        }

        public ToleranceZoneResult? HoleResult
        {
            get => _holeResult;
            private set => SetProperty(ref _holeResult, value);
        }

        public ToleranceZoneResult? ShaftResult
        {
            get => _shaftResult;
            private set => SetProperty(ref _shaftResult, value);
        }

        public FitChartData? FitChart => FitResult != null
                                        ? new FitChartData(FitResult, ShowNominalSizesOnChart)
                                        : null;

        public FitCalculationResult? FitResult
        {
            get => _fitResult;
            private set
            {
                if (SetProperty(ref _fitResult, value))
                {
                    OnPropertyChanged(nameof(FitChart));
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public string LargeSizeWarning
        {
            get => _largeSizeWarning;
            private set => SetProperty(ref _largeSizeWarning, value);
        }

        private void UpdateLettersAvailability(double? size)
        {
            if (size == null || size <= 0 || size > 3150)
            {
                foreach (var item in HoleLetters) item.IsEnabled = false;
                foreach (var item in ShaftLetters) item.IsEnabled = false;
                LargeSizeWarning = string.Empty;
                return;
            }

            if (size > 500)
            {
                foreach (var item in HoleLetters)
                    item.IsEnabled = AllowedLargeLetters.Contains(item.Letter);

                foreach (var item in ShaftLetters)
                    item.IsEnabled = AllowedLargeLetters.Contains(item.Letter);

                LargeSizeWarning = "Для размеров свыше 500 мм по ГОСТ 25347 доступны только поля D–U (d–u), JS (js), K (k), M (m), N (n).";
            }
            else
            {
                foreach (var item in HoleLetters) item.IsEnabled = true;
                foreach (var item in ShaftLetters) item.IsEnabled = true;
                LargeSizeWarning = string.Empty;
            }
        }

        private void ApplyPreset(string preset)
        {
            bool isShaft = char.IsLower(preset.First());
            string letter = new string(preset.TakeWhile(char.IsLetter).ToArray());
            int grade = int.Parse(new string(preset.SkipWhile(char.IsLetter).ToArray()));

            if (isShaft)
            {
                IsSetupShaft = true;
                var found = ShaftLetters.FirstOrDefault(x => x.Letter == letter);
                if (found != null) SelectedShaftLetter = found;
                SelectedShaftGrade = grade;
            }
            else
            {
                IsSetupShaft = false;
                var found = HoleLetters.FirstOrDefault(x => x.Letter == letter);
                if (found != null) SelectedHoleLetter = found;
                SelectedHoleGrade = grade;
            }
        }

        private void ApplyFitPreset(string fitPreset)
        {
            var parts = fitPreset.Split('/');
            if (parts.Length != 2) return;

            string holePart = parts.First();
            string shaftPart = parts.ElementAt(1);

            string holeLetter = new string(holePart.TakeWhile(char.IsLetter).ToArray());
            int holeGrade = int.Parse(new string(holePart.SkipWhile(char.IsLetter).ToArray()));

            string shaftLetter = new string(shaftPart.TakeWhile(char.IsLetter).ToArray());
            int shaftGrade = int.Parse(new string(shaftPart.SkipWhile(char.IsLetter).ToArray()));

            var foundHole = HoleLetters.FirstOrDefault(x => x.Letter == holeLetter);
            if (foundHole != null) SelectedHoleLetter = foundHole;
            SelectedHoleGrade = holeGrade;

            var foundShaft = ShaftLetters.FirstOrDefault(x => x.Letter == shaftLetter);
            if (foundShaft != null) SelectedShaftLetter = foundShaft;
            SelectedShaftGrade = shaftGrade;
        }

        public string GradeDifferenceWarning
        {
            get
            {
                int diff = Math.Abs(SelectedHoleGrade - SelectedShaftGrade);
                if (diff > 2)
                {
                    return $"Рекомендация ГОСТ: разница между квалитетами отверстия (IT{SelectedHoleGrade}) и вала (IT{SelectedShaftGrade}) составляет {diff}.\nОбычно назначают разницу не более 1–2 квалитетов.\nУбедитесь в целесообразности технологии";
                }
                return string.Empty;
            }
        }

        private void Recalculate()
        {
            try
            {
                ErrorMessage = string.Empty;

                if (NominalSize > 500 && (!SelectedHoleLetter.IsEnabled || !SelectedShaftLetter.IsEnabled))
                {
                    ErrorMessage = "Выбранное поле допуска не применяется для размеров свыше 500 мм. Выберите активную букву из списка.";
                    ClearResults();
                    return;
                }

                string holeClass = $"{SelectedHoleLetter.Letter}{SelectedHoleGrade}";
                string shaftClass = $"{SelectedShaftLetter.Letter}{SelectedShaftGrade}";

                HoleResult = _toleranceService.CalculateElement(NominalSize, holeClass);
                ShaftResult = _toleranceService.CalculateElement(NominalSize, shaftClass);
                FitResult = _toleranceService.CalculateFit(NominalSize, holeClass, shaftClass);

                OnPropertyChanged(nameof(ActiveSetupResult));
                OnPropertyChanged(nameof(SetupChart));
                OnPropertyChanged(nameof(IsMeasurementScrap));
                OnPropertyChanged(nameof(MeasurementStatusTitle));
                OnPropertyChanged(nameof(MeasurementDetails));
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                ClearResults();
            }
        }

        private void ClearResults()
        {
            HoleResult = null;
            ShaftResult = null;
            FitResult = null;
            OnPropertyChanged(nameof(ActiveSetupResult));
            OnPropertyChanged(nameof(SetupChart));
            OnPropertyChanged(nameof(IsMeasurementScrap));
            OnPropertyChanged(nameof(MeasurementStatusTitle));
            OnPropertyChanged(nameof(MeasurementDetails));
        }
    }
}
