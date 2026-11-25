using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MechCore.ViewModels;
using MechCore.Views;

namespace MechCore
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private async Task NavigateWithFirstLoadSpinnerAsync(
             MainViewModel vm,
             object? screenContent,
             Func<bool> isLoadingGetter,
             Action<bool> isLoadingSetter)
        {
            if (screenContent == null || isLoadingGetter()) return;

            // Если страница уже в кэше — переключаем мгновенно без курсора ожидания
            if (vm.IsScreenCached(screenContent))
            {
                vm.ShowScreen(screenContent);
                return;
            }

            // Первый клик: ставим курсор загрузки и блокируем повторное нажатие
            isLoadingSetter(true);
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                vm.ShowScreen(screenContent);
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                isLoadingSetter(false);
            }
        }

        private void ClearOtherMenuSelections(ListBox activeList)
        {
            if (GeneralMenuListBox != null && GeneralMenuListBox != activeList)
                GeneralMenuListBox.SelectedIndex = -1;

            if (TurningMenuListBox != null && TurningMenuListBox != activeList)
                TurningMenuListBox.SelectedIndex = -1;

            if (MillingMenuListBox != null && MillingMenuListBox != activeList)
                MillingMenuListBox.SelectedIndex = -1;
        }

        private void OnTolerancesMenuSelected(object sender, RoutedEventArgs e)
        {
            ClearOtherMenuSelections(GeneralMenuListBox);
            if (DataContext is MainViewModel vm && vm.TolerancesVM != null)
                vm.ShowScreen(vm.TolerancesVM);
        }

        private async void OnThreadsMenuSelected(object sender, RoutedEventArgs e)
        {
            ClearOtherMenuSelections(GeneralMenuListBox);
            if (DataContext is MainViewModel vm)
                await NavigateWithFirstLoadSpinnerAsync(vm, vm.ThreadsVM, () => vm.IsThreadsLoading, v => vm.IsThreadsLoading = v);
        }

        private async void OnMaterialsMenuSelected(object sender, RoutedEventArgs e)
        {
            ClearOtherMenuSelections(GeneralMenuListBox);
            if (DataContext is MainViewModel vm)
                await NavigateWithFirstLoadSpinnerAsync(vm, vm.MaterialsVM, () => vm.IsMaterialsLoading, v => vm.IsMaterialsLoading = v);
        }

        private async void OnGeometryMenuSelected(object sender, RoutedEventArgs e)
        {
            ClearOtherMenuSelections(GeneralMenuListBox);
            if (DataContext is MainViewModel vm)
                await NavigateWithFirstLoadSpinnerAsync(vm, vm.GeometryVM, () => vm.IsGeometryLoading, v => vm.IsGeometryLoading = v);
        }

        private async void OnGostDirectoryMenuSelected(object sender, RoutedEventArgs e)
        {
            ClearOtherMenuSelections(GeneralMenuListBox);
            if (DataContext is not MainViewModel vm || vm.IsGostLoading) return;

            if (vm.IsScreenTypeCached<Views.Standards.GostDirectoryView>())
            {
                vm.ShowScreen(new Views.Standards.GostDirectoryView());
                return;
            }

            vm.IsGostLoading = true;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                vm.ShowScreen(new Views.Standards.GostDirectoryView());
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                vm.IsGostLoading = false;
            }
        }

        private void OnCalculatorMenuClicked(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process? existingCalc = null;
                foreach (var name in new[] { "CalculatorApp", "calc" })
                {
                    var processes = Process.GetProcessesByName(name);
                    foreach (var p in processes)
                    {
                        if (p.MainWindowHandle != IntPtr.Zero)
                        {
                            existingCalc = p;
                            break;
                        }
                    }
                    if (existingCalc != null) break;
                }

                if (existingCalc != null)
                {
                    ShowWindow(existingCalc.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(existingCalc.MainWindowHandle);
                }
                else
                {
                    Process.Start(new ProcessStartInfo("calc.exe") { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось запустить калькулятор: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            e.Handled = true;
        }

        private async void OnTurningHeaderMenuSelected(object sender, RoutedEventArgs e)
        {
            ClearOtherMenuSelections(TurningMenuListBox);
            if (DataContext is MainViewModel vm)
                await NavigateWithFirstLoadSpinnerAsync(vm, vm.TurningHeaderVM, () => vm.IsTurningHeaderLoading, v => vm.IsTurningHeaderLoading = v);
        }

        private async void OnTurningCuttingMenuSelected(object sender, RoutedEventArgs e)
        {
            ClearOtherMenuSelections(TurningMenuListBox);
            if (DataContext is MainViewModel vm)
                await NavigateWithFirstLoadSpinnerAsync(vm, vm.TurningCuttingVM, () => vm.IsTurningCuttingLoading, v => vm.IsTurningCuttingLoading = v);
        }

        private async void OnMillingHeaderMenuSelected(object sender, RoutedEventArgs e)
        {
            ClearOtherMenuSelections(MillingMenuListBox);
            if (DataContext is MainViewModel vm)
                await NavigateWithFirstLoadSpinnerAsync(vm, vm.MillingHeaderVM, () => vm.IsMillingHeaderLoading, v => vm.IsMillingHeaderLoading = v);
        }

        private async void OnMillingCuttingMenuSelected(object sender, RoutedEventArgs e)
        {
            ClearOtherMenuSelections(MillingMenuListBox);
            if (DataContext is MainViewModel vm)
                await NavigateWithFirstLoadSpinnerAsync(vm, vm.MillingCuttingVM, () => vm.IsMillingCuttingLoading, v => vm.IsMillingCuttingLoading = v);
        }

        private async void OnMillingGeometryMenuSelected(object sender, RoutedEventArgs e)
        {
            ClearOtherMenuSelections(MillingMenuListBox);
            if (DataContext is MainViewModel vm)
                await NavigateWithFirstLoadSpinnerAsync(vm, vm.MillingGeometryVM, () => vm.IsMillingGeometryLoading, v => vm.IsMillingGeometryLoading = v);
        }

        private void OnAboutClicked(object sender, MouseButtonEventArgs e)
        {
            var aboutWindow = new AboutWindow
            {
                Owner = this
            };
            aboutWindow.ShowDialog();

            e.Handled = true;
        }

    }
}
