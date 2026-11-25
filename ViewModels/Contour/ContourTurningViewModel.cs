using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using MechCore.Core;
using MechCore.Models.Contour;
using MechCore.Services.Contour;
using Microsoft.Win32;

namespace MechCore.ViewModels.Contour;

public class ContourTurningViewModel : ObservableObject
{
    private readonly SimpleDxfService _dxfService = new();
    private DxfContourLoadResult? _currentState;

    public const double CanvasWidth = 540.0;
    public const double CanvasHeight = 440.0;

    public MainViewModel Main { get; }
    public ObservableCollection<DxfSegmentItem> Segments { get; } = new();

    private string _partLengthText = "—";
    /// <summary>Общая длина детали по оси Z (мм).</summary>
    public string PartLengthText
    {
        get => _partLengthText;
        private set => SetProperty(ref _partLengthText, value);
    }

    private string _partMaxDiameterText = "—";
    /// <summary>Максимальный наружный диаметр детали (мм).</summary>
    public string PartMaxDiameterText
    {
        get => _partMaxDiameterText;
        private set => SetProperty(ref _partMaxDiameterText, value);
    }

    private string _partMinDiameterText = "—";
    /// <summary>Минимальный внутренний диаметр (для втулок, мм).</summary>
    public string PartMinDiameterText
    {
        get => _partMinDiameterText;
        private set => SetProperty(ref _partMinDiameterText, value);
    }

    private bool _isTubeStock;
    /// <summary>false — пруток (сплошной), true — труба (полая заготовка).</summary>
    public bool IsTubeStock
    {
        get => _isTubeStock;
        set
        {
            if (SetProperty(ref _isTubeStock, value))
            {
                OnPropertyChanged(nameof(IsBarStock));
                RecalculateStock();
            }
        }
    }

    public bool IsBarStock
    {
        get => !_isTubeStock;
        set => IsTubeStock = !value;
    }

    private bool _isAutoStock = true;
    /// <summary>Автоподбор стандартного проката по габаритам детали.</summary>
    public bool IsAutoStock
    {
        get => _isAutoStock;
        set
        {
            if (SetProperty(ref _isAutoStock, value))
                RecalculateStock();
        }
    }

    private string _stockOuterDiaText = "";
    public string StockOuterDiaText
    {
        get => _stockOuterDiaText;
        set
        {
            if (SetProperty(ref _stockOuterDiaText, value) && !_isSyncingStockFields)
            {
                _isAutoStock = false;
                OnPropertyChanged(nameof(IsAutoStock));
                ApplyManualStock();
            }
        }
    }

    private string _stockInnerDiaText = "0";
    public string StockInnerDiaText
    {
        get => _stockInnerDiaText;
        set
        {
            if (SetProperty(ref _stockInnerDiaText, value) && !_isSyncingStockFields)
            {
                _isAutoStock = false;
                OnPropertyChanged(nameof(IsAutoStock));
                ApplyManualStock();
            }
        }
    }

    private string _stockLengthText = "";
    public string StockLengthText
    {
        get => _stockLengthText;
        set
        {
            if (SetProperty(ref _stockLengthText, value) && !_isSyncingStockFields)
            {
                _isAutoStock = false;
                OnPropertyChanged(nameof(IsAutoStock));
                ApplyManualStock();
            }
        }
    }

    private string _faceAllowanceText = "2";
    /// <summary>Припуск на правый торец от нуля Z0 до детали (мм).</summary>
    public string FaceAllowanceText
    {
        get => _faceAllowanceText;
        set
        {
            if (SetProperty(ref _faceAllowanceText, value) && !_isSyncingStockFields)
            {
                if (IsAutoStock) RecalculateStock();
                else ApplyManualStock();
            }
        }
    }

    private string _stockWarningMessage = string.Empty;
    public string StockWarningMessage
    {
        get => _stockWarningMessage;
        private set
        {
            if (SetProperty(ref _stockWarningMessage, value))
                OnPropertyChanged(nameof(HasStockWarning));
        }
    }

    public bool HasStockWarning => !string.IsNullOrEmpty(StockWarningMessage);

    private StockParameters _stockRender = new();
    public StockParameters StockRender
    {
        get => _stockRender;
        private set => SetProperty(ref _stockRender, value);
    }

    private bool _isSyncingStockFields;


    private string _statusText = "Нажмите «Загрузить DXF», чтобы открыть замкнутый контур детали.";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    private bool _hasLoadedContour;
    public bool HasLoadedContour
    {
        get => _hasLoadedContour;
        set => SetProperty(ref _hasLoadedContour, value);
    }

    // Экранные координаты осей X0 и Z0 на Canvas
    private double _screenAxisY = 380.0;
    public double ScreenAxisY
    {
        get => _screenAxisY;
        set => SetProperty(ref _screenAxisY, value);
    }

    private double _screenZeroZ_X = 480.0;
    public double ScreenZeroZ_X
    {
        get => _screenZeroZ_X;
        set => SetProperty(ref _screenZeroZ_X, value);
    }

    private string _boreDiameterOffsetText = "0";
    /// <summary>Диаметр внутреннего отверстия (мм) для смещения оси X0 ниже нижней грани контура.</summary>
    public string BoreDiameterOffsetText
    {
        get => _boreDiameterOffsetText;
        set
        {
            if (SetProperty(ref _boreDiameterOffsetText, value) && _currentState != null && TryParse(value, out double dia) && dia >= 0)
            {
                double bottomY = _currentState.Segments.Min(s => System.Math.Min(s.RawY1, s.RawY2));
                _currentState.AxisY = bottomY - (dia / 2.0);
                RefreshCanvas();
            }
        }
    }

    private string _zeroZShiftText = "0";
    /// <summary>Смещение нуля Z0 вправо от правого торца (мм).</summary>
    public string ZeroZShiftText
    {
        get => _zeroZShiftText;
        set
        {
            if (SetProperty(ref _zeroZShiftText, value) && _currentState != null && TryParse(value, out double shift) && shift >= 0)
            {
                double maxRightX = _currentState.Segments.Max(s => System.Math.Max(s.RawX1, s.RawX2));
                _currentState.RightFaceX = maxRightX + shift;
                RefreshCanvas();
            }
        }
    }

    public ICommand OpenDxfCommand { get; }
    public ICommand ResetZeroCommand { get; }

    public ContourTurningViewModel(MainViewModel main)
    {
        Main = main;
        OpenDxfCommand = new RelayCommand(_ => OpenDxfFile());
        ResetZeroCommand = new RelayCommand(_ =>
        {
            BoreDiameterOffsetText = "0";
            ZeroZShiftText = "0";
        });
    }

    private void OpenDxfFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выберите файл контура DXF (КОМПАС-3D)",
            Filter = "Чертежи DXF (*.dxf)|*.dxf|Все файлы (*.*)|*.*"
        };

        if (dlg.ShowDialog() != true)
            return;

        var res = _dxfService.LoadFromFile(dlg.FileName, CanvasWidth, CanvasHeight);
        Segments.Clear();

        if (!res.Success)
        {
            _currentState = null;
            HasLoadedContour = false;
            HasError = true;
            StatusText = res.Message;
            return;
        }

        _currentState = res;
        HasError = false;
        HasLoadedContour = true;

        _boreDiameterOffsetText = res.DetectedBoreDiameter.ToString("0.###", CultureInfo.InvariantCulture);
        _zeroZShiftText = "0";
        OnPropertyChanged(nameof(BoreDiameterOffsetText));
        OnPropertyChanged(nameof(ZeroZShiftText));

        _isTubeStock = _currentState.Stock.Kind == StockKind.Tube;
        OnPropertyChanged(nameof(IsTubeStock));
        OnPropertyChanged(nameof(IsBarStock));

        _isAutoStock = true;
        OnPropertyChanged(nameof(IsAutoStock));

        SyncStockFieldsFromState();
        RefreshCanvas();
        StatusText = $"Файл: {Path.GetFileName(dlg.FileName)}. {res.Message}";
    }


    private void RefreshCanvas()
    {
        if (_currentState == null) return;

        _dxfService.UpdateZeroAndFit(_currentState, CanvasWidth, CanvasHeight);
        ScreenAxisY = _currentState.ScreenAxisY;
        ScreenZeroZ_X = _currentState.ScreenZeroZ_X;

        var list = _currentState.Segments.ToList();
        Segments.Clear();
        foreach (var s in list)
            Segments.Add(s);

        var inv = CultureInfo.InvariantCulture;
        PartLengthText = string.Format(inv, "{0:0.###} мм", _currentState.PartLength);
        PartMaxDiameterText = string.Format(inv, "Ø {0:0.###} мм", _currentState.MaxDiameter);
        PartMinDiameterText = _currentState.MinDiameter > 0.001
            ? string.Format(inv, "Ø {0:0.###} мм", _currentState.MinDiameter)
            : "Сплошной вал";
    }

    private void RecalculateStock()
    {
        if (_currentState == null) return;

        if (IsAutoStock)
        {
            RefreshCanvas();
        }
        else
        {
            ApplyManualStock();
        }
    }

    private void ApplyManualStock()
    {
        if (_currentState == null) return;

        _currentState.Stock.Kind = IsTubeStock ? StockKind.Tube : StockKind.Bar;

        if (TryParse(StockOuterDiaText, out double dOut) && dOut > 0)
            _currentState.Stock.OuterDiameter = dOut;

        if (IsTubeStock && TryParse(StockInnerDiaText, out double dIn) && dIn >= 0)
            _currentState.Stock.InnerDiameter = dIn;
        else if (!IsTubeStock)
            _currentState.Stock.InnerDiameter = 0;

        if (TryParse(StockLengthText, out double len) && len > 0)
            _currentState.Stock.Length = len;

        if (TryParse(FaceAllowanceText, out double fa) && fa >= 0)
            _currentState.Stock.RightFaceAllowance = fa;

        RefreshCanvas();
    }

    private void SyncStockFieldsFromState()
    {
        if (_currentState == null) return;
        _isSyncingStockFields = true;
        var inv = CultureInfo.InvariantCulture;
        StockOuterDiaText = _currentState.Stock.OuterDiameter.ToString("0.##", inv);
        StockInnerDiaText = _currentState.Stock.InnerDiameter.ToString("0.##", inv);
        StockLengthText = _currentState.Stock.Length.ToString("0.##", inv);
        FaceAllowanceText = _currentState.Stock.RightFaceAllowance.ToString("0.##", inv);
        _isSyncingStockFields = false;
    }

    private void ValidateStock()
    {
        if (_currentState == null)
        {
            StockWarningMessage = string.Empty;
            return;
        }

        var st = _currentState.Stock;
        if (st.OuterDiameter < _currentState.MaxDiameter)
            StockWarningMessage = $"Наружный Ø заготовки ({st.OuterDiameter:0.##} мм) меньше Ø детали ({_currentState.MaxDiameter:0.##} мм)!";
        else if (st.Kind == StockKind.Tube && st.InnerDiameter > _currentState.MinDiameter)
            StockWarningMessage = $"Отверстие трубы (Ø{st.InnerDiameter:0.##} мм) больше отверстия детали (Ø{_currentState.MinDiameter:0.##} мм)!";
        else if (st.Length < _currentState.PartLength + st.RightFaceAllowance)
            StockWarningMessage = "Длина заготовки меньше длины детали с учётом припуска на торец!";
        else
            StockWarningMessage = string.Empty;
    }

    private static bool TryParse(string text, out double value)
    {
        string norm = (text ?? string.Empty).Replace(',', '.');
        return double.TryParse(norm, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}