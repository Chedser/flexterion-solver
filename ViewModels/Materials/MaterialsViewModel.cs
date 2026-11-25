using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using MechCore.Core;
using MechCore.Models.Materials;
using MechCore.Services.Materials;
using MechCore.Models.Charts;

namespace MechCore.ViewModels.Materials
{
    public class MaterialsViewModel : ObservableObject
    {
        private readonly IMaterialChartService _chartService;
        private MaterialGradeItem? _selectedGrade;
        private MaterialChartMetric _selectedChartMetric = MaterialChartMetric.CuttingSpeed;
        private EngineeringChartModel? _materialChartModel;
        private string _chartRecommendedTool = string.Empty;
        private string _chartOperatorAdvice = string.Empty;
        private string _chartValueBadge = string.Empty;
        private bool _isChartHardnessCritical;
        private double _activeChartHb = 190.0;
        private string _activeChartMaterialTitle = "Группа ISO P";

        private MaterialGradeItem? _inspectedApplicationGrade;
        public ICommand ShowApplicationCommand { get; }
        public ICommand CloseApplicationCommand { get; }

        private readonly IMaterialService _materialService;
        private readonly List<MaterialGradeItem> _allGrades;

        private IsoMaterialGroupItem _selectedIsoGroup;
        private string _searchQuery = string.Empty;
        private bool _filterBySelectedGroup;
        private List<MaterialGradeItem> _filteredGrades = new();

        private HardnessScale _selectedHardnessScale = HardnessScale.HRC;
        private string _hardnessInputText = "45";
        private HardnessConversionResult _hardnessResult;

        public MainViewModel Main { get; }
        public List<IsoMaterialGroupItem> IsoGroups { get; }
        public List<HardnessScale> HardnessScales { get; } = new() { HardnessScale.HRC, HardnessScale.HB, HardnessScale.HV };
        public ICommand SelectIsoGroupCommand { get; }

        public MaterialsViewModel(MainViewModel main)
        {
            Main = main;
            _materialService = new MaterialStandardService();

            _chartService = new MaterialChartService();

            IsoGroups = _materialService.GetIsoGroups();
            _allGrades = _materialService.GetAllGrades();

            _selectedIsoGroup = IsoGroups.First();
            _hardnessResult = _materialService.ConvertHardness(45.0, HardnessScale.HRC);

            SelectIsoGroupCommand = new RelayCommand(param =>
            {
                if (param is IsoMaterialGroupItem group)
                    SelectedIsoGroup = group;
            });

            ShowApplicationCommand = new RelayCommand(param =>
            {
                if (param is MaterialGradeItem grade)
                {
                    if (InspectedApplicationGrade == grade)
                    {
                        InspectedApplicationGrade = null;
                    }
                    else
                    {
                        SelectedGrade = grade;
                        InspectedApplicationGrade = grade;
                    }
                }
            });

            CloseApplicationCommand = new RelayCommand(_ => InspectedApplicationGrade = null);

            SyncChartFromSelection(useConverterValue: false);
            UpdateFilteredGrades();
        }

        public IsoMaterialGroupItem SelectedIsoGroup
        {
            get => _selectedIsoGroup;
            set
            {
                if (SetProperty(ref _selectedIsoGroup, value) && value != null)
                {
                    _selectedGrade = null;
                    OnPropertyChanged(nameof(SelectedGrade));

                    if (FilterBySelectedGroup)
                        UpdateFilteredGrades();

                    SyncChartFromSelection(useConverterValue: false);
                }
            }
        }


        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                    UpdateFilteredGrades();
            }
        }

        public bool FilterBySelectedGroup
        {
            get => _filterBySelectedGroup;
            set
            {
                if (SetProperty(ref _filterBySelectedGroup, value))
                    UpdateFilteredGrades();
            }
        }

        public List<MaterialGradeItem> FilteredGrades
        {
            get => _filteredGrades;
            private set => SetProperty(ref _filteredGrades, value);
        }

        public HardnessScale SelectedHardnessScale
        {
            get => _selectedHardnessScale;
            set
            {
                if (SetProperty(ref _selectedHardnessScale, value))
                    RecalculateHardness();
            }
        }

        public string HardnessInputText
        {
            get => _hardnessInputText;
            set
            {
                if (SetProperty(ref _hardnessInputText, value))
                    RecalculateHardness();
            }
        }

        public HardnessConversionResult HardnessResult
        {
            get => _hardnessResult;
            private set => SetProperty(ref _hardnessResult, value);
        }

        private void UpdateFilteredGrades()
        {
            IEnumerable<MaterialGradeItem> query = _allGrades;

            if (FilterBySelectedGroup && SelectedIsoGroup != null)
            {
                query = query.Where(x => x.IsoGroup == SelectedIsoGroup.Code);
            }

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string term = SearchQuery.Trim();
                query = query.Where(x =>
                    x.GostGrade.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    x.DinGrade.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    x.AisiGrade.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    x.GbGrade.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    x.ApplicationNote.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            FilteredGrades = query.ToList();
        }

        private void RecalculateHardness()
        {
            string norm = HardnessInputText.Replace(',', '.');
            if (double.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out double val) && val > 0)
            {
                HardnessResult = _materialService.ConvertHardness(val, SelectedHardnessScale);
                SyncChartFromSelection(useConverterValue: true);
            }
        }
        public MaterialGradeItem? SelectedGrade
        {
            get => _selectedGrade;
            set
            {
                if (SetProperty(ref _selectedGrade, value) && value != null)
                {
                    var matchingGroup = IsoGroups.FirstOrDefault(g => g.Code == value.IsoGroup);
                    if (matchingGroup != null && _selectedIsoGroup != matchingGroup)
                    {
                        _selectedIsoGroup = matchingGroup;
                        OnPropertyChanged(nameof(SelectedIsoGroup));
                    }
                    SyncChartFromSelection(useConverterValue: false);
                }
            }
        }

        public MaterialChartMetric SelectedChartMetric
        {
            get => _selectedChartMetric;
            set
            {
                if (SetProperty(ref _selectedChartMetric, value))
                {
                    OnPropertyChanged(nameof(IsCuttingSpeedChart));
                    OnPropertyChanged(nameof(IsCuttingForceChart));
                    OnPropertyChanged(nameof(IsFeedChart));
                    OnPropertyChanged(nameof(IsMachinabilityChart));
                    RebuildChart();
                }
            }
        }

        public bool IsCuttingSpeedChart
        {
            get => SelectedChartMetric == MaterialChartMetric.CuttingSpeed;
            set { if (value) SelectedChartMetric = MaterialChartMetric.CuttingSpeed; }
        }

        public bool IsCuttingForceChart
        {
            get => SelectedChartMetric == MaterialChartMetric.SpecificCuttingForce;
            set { if (value) SelectedChartMetric = MaterialChartMetric.SpecificCuttingForce; }
        }

        public bool IsFeedChart
        {
            get => SelectedChartMetric == MaterialChartMetric.RecommendedFeed;
            set { if (value) SelectedChartMetric = MaterialChartMetric.RecommendedFeed; }
        }

        public bool IsMachinabilityChart
        {
            get => SelectedChartMetric == MaterialChartMetric.MachinabilityIndex;
            set { if (value) SelectedChartMetric = MaterialChartMetric.MachinabilityIndex; }
        }

        public EngineeringChartModel? MaterialChartModel
        {
            get => _materialChartModel;
            private set => SetProperty(ref _materialChartModel, value);
        }

        public string ChartRecommendedTool
        {
            get => _chartRecommendedTool;
            private set => SetProperty(ref _chartRecommendedTool, value);
        }

        public string ChartOperatorAdvice
        {
            get => _chartOperatorAdvice;
            private set => SetProperty(ref _chartOperatorAdvice, value);
        }

        public string ChartValueBadge
        {
            get => _chartValueBadge;
            private set => SetProperty(ref _chartValueBadge, value);
        }

        public bool IsChartHardnessCritical
        {
            get => _isChartHardnessCritical;
            private set => SetProperty(ref _isChartHardnessCritical, value);
        }

        private void SyncChartFromSelection(bool useConverterValue)
        {
            string isoCode = SelectedIsoGroup?.CodeLetter ?? "P";

            if (useConverterValue)
            {
                string norm = HardnessInputText.Replace(',', '.');
                if (double.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out double val) && val > 0)
                {
                    _activeChartHb = ConvertAnyScaleToHb(val, SelectedHardnessScale);
                    _activeChartMaterialTitle = SelectedGrade != null
                        ? $"{SelectedGrade.GostGrade} ({val:0.#} {SelectedHardnessScale})"
                        : $"ISO {isoCode} ({val:0.#} {SelectedHardnessScale})";
                }
            }
            else if (SelectedGrade != null)
            {
                isoCode = SelectedGrade.IsoCode;
                _activeChartHb = ParseTypicalHardnessToHb(SelectedGrade.TypicalHardness);
                _activeChartMaterialTitle = $"{SelectedGrade.GostGrade} ({SelectedGrade.TypicalHardness})";
            }
            else
            {
                _activeChartHb = isoCode switch
                {
                    "P" => 190,
                    "M" => 175,
                    "K" => 200,
                    "N" => 100,
                    "S" => 300,
                    "H" => 615,
                    _ => 180
                };
                _activeChartMaterialTitle = $"Группа ISO {isoCode}";
            }

            RebuildChart();
        }

        private void RebuildChart()
        {
            string isoCode = SelectedGrade?.IsoCode ?? SelectedIsoGroup?.CodeLetter ?? "P";
            var summary = _chartService.BuildChart(
                SelectedChartMetric,
                isoCode,
                _activeChartMaterialTitle,
                _activeChartHb);

            MaterialChartModel = summary.ChartModel;
            ChartRecommendedTool = summary.RecommendedToolGrade;
            ChartOperatorAdvice = summary.OperatorAdvice;
            ChartValueBadge = summary.CalculatedValueBadge;
            IsChartHardnessCritical = summary.IsCriticalHardness;
        }

        private double ConvertAnyScaleToHb(double value, HardnessScale scale)
        {
            if (scale == HardnessScale.HB) return value;

            var conv = _materialService.ConvertHardness(value, scale);
            string digits = new string(conv.HbText.Where(c => char.IsDigit(c) || c == '.').ToArray());
            return double.TryParse(digits, NumberStyles.Any, CultureInfo.InvariantCulture, out double hb) && hb > 0
                ? hb
                : 180.0;
        }

        private double ParseTypicalHardnessToHb(string typicalHardness)
        {
            if (string.IsNullOrWhiteSpace(typicalHardness)) return 180.0;

            bool isHrc = typicalHardness.Contains("HRC", StringComparison.OrdinalIgnoreCase);
            var parts = typicalHardness
                .Replace("HRC", "", StringComparison.OrdinalIgnoreCase)
                .Replace("HB", "", StringComparison.OrdinalIgnoreCase)
                .Replace("–", "-")
                .Split('-', StringSplitOptions.RemoveEmptyEntries);

            var numbers = new List<double>();
            foreach (var part in parts)
            {
                if (double.TryParse(part.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
                    numbers.Add(num);
            }

            if (numbers.Count == 0) return 180.0;
            double avg = numbers.Average();
            return isHrc ? ConvertAnyScaleToHb(avg, HardnessScale.HRC) : avg;
        }

        public MaterialGradeItem? InspectedApplicationGrade
        {
            get => _inspectedApplicationGrade;
            private set
            {
                if (SetProperty(ref _inspectedApplicationGrade, value))
                    OnPropertyChanged(nameof(IsApplicationNoteVisible));
            }
        }

        public bool IsApplicationNoteVisible => InspectedApplicationGrade != null;

    }
}
