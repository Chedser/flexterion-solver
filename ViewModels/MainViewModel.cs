using MechCore.Core;
using MechCore.ViewModels.Contour;
using MechCore.ViewModels.Materials;
using MechCore.ViewModels.Milling;
using MechCore.ViewModels.Threads;
using MechCore.ViewModels.Tolerances;
using MechCore.ViewModels.Geometry;
using MechCore.ViewModels.Turning;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace MechCore.ViewModels
{
    public class CachedScreenItem : ObservableObject
    {
        public object Content { get; }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public CachedScreenItem(object content)
        {
            Content = content;
        }
    }

    public class MainViewModel : ObservableObject
    {
        private bool _isThreadsLoading;
        public bool IsThreadsLoading
        {
            get => _isThreadsLoading;
            set { if (SetProperty(ref _isThreadsLoading, value)) OnPropertyChanged(nameof(IsThreadsReady)); }
        }
        public bool IsThreadsReady => !_isThreadsLoading;

        private bool _isMaterialsLoading;
        public bool IsMaterialsLoading
        {
            get => _isMaterialsLoading;
            set { if (SetProperty(ref _isMaterialsLoading, value)) OnPropertyChanged(nameof(IsMaterialsReady)); }
        }
        public bool IsMaterialsReady => !_isMaterialsLoading;

        private bool _isGeometryLoading;
        public bool IsGeometryLoading
        {
            get => _isGeometryLoading;
            set { if (SetProperty(ref _isGeometryLoading, value)) OnPropertyChanged(nameof(IsGeometryReady)); }
        }
        public bool IsGeometryReady => !_isGeometryLoading;

        private bool _isGostLoading;
        public bool IsGostLoading
        {
            get => _isGostLoading;
            set { if (SetProperty(ref _isGostLoading, value)) OnPropertyChanged(nameof(IsGostReady)); }
        }
        public bool IsGostReady => !_isGostLoading;

        private bool _isTurningHeaderLoading;
        public bool IsTurningHeaderLoading
        {
            get => _isTurningHeaderLoading;
            set { if (SetProperty(ref _isTurningHeaderLoading, value)) OnPropertyChanged(nameof(IsTurningHeaderReady)); }
        }
        public bool IsTurningHeaderReady => !_isTurningHeaderLoading;

        private bool _isTurningCuttingLoading;
        public bool IsTurningCuttingLoading
        {
            get => _isTurningCuttingLoading;
            set { if (SetProperty(ref _isTurningCuttingLoading, value)) OnPropertyChanged(nameof(IsTurningCuttingReady)); }
        }
        public bool IsTurningCuttingReady => !_isTurningCuttingLoading;

        private bool _isMillingHeaderLoading;
        public bool IsMillingHeaderLoading
        {
            get => _isMillingHeaderLoading;
            set { if (SetProperty(ref _isMillingHeaderLoading, value)) OnPropertyChanged(nameof(IsMillingHeaderReady)); }
        }
        public bool IsMillingHeaderReady => !_isMillingHeaderLoading;

        private bool _isMillingCuttingLoading;
        public bool IsMillingCuttingLoading
        {
            get => _isMillingCuttingLoading;
            set { if (SetProperty(ref _isMillingCuttingLoading, value)) OnPropertyChanged(nameof(IsMillingCuttingReady)); }
        }
        public bool IsMillingCuttingReady => !_isMillingCuttingLoading;

        private bool _isMillingGeometryLoading;
        public bool IsMillingGeometryLoading
        {
            get => _isMillingGeometryLoading;
            set { if (SetProperty(ref _isMillingGeometryLoading, value)) OnPropertyChanged(nameof(IsMillingGeometryReady)); }
        }
        public bool IsMillingGeometryReady => !_isMillingGeometryLoading;

        public bool IsScreenCached(object? screenContent)
        {
            if (screenContent == null) return false;
            return CachedScreens.Any(s =>
                ReferenceEquals(s.Content, screenContent) ||
                (screenContent is UserControl && s.Content.GetType() == screenContent.GetType()));
        }

        public bool IsScreenTypeCached<T>() =>
            CachedScreens.Any(s => s.Content is T);

        private bool _isOperatorMode = true;
        private object? _currentViewModel;

        public ThreadsViewModel ThreadsVM { get; }
        public TolerancesViewModel TolerancesVM { get; }
        public MaterialsViewModel MaterialsVM { get; }
        public GeometryViewModel GeometryVM { get; }
        public TurningHeaderViewModel TurningHeaderVM { get; }
        public TurningCuttingViewModel TurningCuttingVM { get; }
        public ContourTurningViewModel ContourTurningVM { get; }
        public MillingHeaderViewModel MillingHeaderVM { get; }
        public MillingCuttingViewModel MillingCuttingVM { get; }
        public MillingGeometryViewModel MillingGeometryVM { get; }
        public ObservableCollection<CachedScreenItem> CachedScreens { get; } = new();
        public ICommand NavigateThreadsCommand { get; }

        public MainViewModel()
        {
            TolerancesVM = new TolerancesViewModel(this);
            ThreadsVM = new ThreadsViewModel(this);
            MaterialsVM = new MaterialsViewModel(this);
            GeometryVM = new GeometryViewModel(this);
            TurningHeaderVM = new TurningHeaderViewModel(this);
            TurningCuttingVM = new TurningCuttingViewModel(this);
            ContourTurningVM = new ContourTurningViewModel(this);
            MillingHeaderVM = new MillingHeaderViewModel(this);
            MillingCuttingVM = new MillingCuttingViewModel(this);
            MillingGeometryVM = new MillingGeometryViewModel(this);

            NavigateThreadsCommand = new RelayCommand(_ => ShowScreen(ThreadsVM));

            // Открываем стартовый экран и сразу помещаем его в кэш
            ShowScreen(TolerancesVM);
        }

        public void ShowScreen(object? screenContent)
        {
            if (screenContent == null) return;

            var existing = CachedScreens.FirstOrDefault(s =>
                ReferenceEquals(s.Content, screenContent) ||
                (screenContent is UserControl && s.Content.GetType() == screenContent.GetType()));

            if (existing == null)
            {
                existing = new CachedScreenItem(screenContent);
                CachedScreens.Add(existing);
            }

            foreach (var item in CachedScreens)
            {
                item.IsActive = ReferenceEquals(item, existing);
            }

            CurrentViewModel = existing.Content;
        }

        public bool IsOperatorMode
        {
            get => _isOperatorMode;
            set
            {
                if (SetProperty(ref _isOperatorMode, value))
                {
                    OnPropertyChanged(nameof(IsEngineerMode));
                }
            }
        }

        public bool IsEngineerMode
        {
            get => !_isOperatorMode;
            set => IsOperatorMode = !value;
        }

        public object? CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }
    }
}
