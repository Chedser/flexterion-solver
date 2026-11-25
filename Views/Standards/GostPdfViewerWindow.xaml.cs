using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MechCore.Views.Standards
{
    public partial class GostPdfViewerWindow : Window
    {
        private readonly string _filePath;
        private double _currentZoom = 1.0;

        public ObservableCollection<BitmapImage> RenderedPages { get; } = new();

        public GostPdfViewerWindow(string gostTitle, string fullFilePath)
        {
            InitializeComponent();
            _filePath = fullFilePath;

            Title = $"{gostTitle} — MechCore";
            HeaderTitleText.Text = gostTitle;
            PagesItemsControl.ItemsSource = RenderedPages;

            Loaded += OnWindowLoaded;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                StorageFile storageFile = await StorageFile.GetFileFromPathAsync(_filePath);
                PdfDocument pdfDoc = await PdfDocument.LoadFromFileAsync(storageFile);

                uint pageCount = pdfDoc.PageCount;
                PageCountText.Text = $"({pageCount} стр.)";

                LoadingPanel.Visibility = Visibility.Collapsed;
                PagesScrollViewer.Visibility = Visibility.Visible;

                // Поэтапно рендерим страницы в высоком качестве, чтобы окно открывалось сразу на 1-й странице
                for (uint i = 0; i < pageCount; i++)
                {
                    if (!IsLoaded) break;

                    using PdfPage page = pdfDoc.GetPage(i);
                    BitmapImage bitmap = await RenderPageToBitmapAsync(page, renderScale: 1.5);
                    RenderedPages.Add(bitmap);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Не удалось отобразить PDF во встроенном окне: {ex.Message}\n\nФайл будет открыт в системной программе.",
                    "Просмотр ГОСТа",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                OpenInExternalViewer();
                Close();
            }
        }

        private static async Task<BitmapImage> RenderPageToBitmapAsync(PdfPage page, double renderScale)
        {
            using var stream = new InMemoryRandomAccessStream();
            var options = new PdfPageRenderOptions
            {
                DestinationWidth = (uint)(page.Size.Width * renderScale),
                DestinationHeight = (uint)(page.Size.Height * renderScale)
            };

            await page.RenderToStreamAsync(stream, options);
            stream.Seek(0);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream.AsStreamForRead();
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }

        private void ApplyZoom(double newZoom)
        {
            _currentZoom = Math.Clamp(newZoom, 0.5, 2.5);
            PagesScaleTransform.ScaleX = _currentZoom;
            PagesScaleTransform.ScaleY = _currentZoom;
            ZoomLevelText.Text = $"{Math.Round(_currentZoom * 100)}%";
        }

        private void OnZoomInClicked(object sender, RoutedEventArgs e) => ApplyZoom(_currentZoom + 0.15);
        private void OnZoomOutClicked(object sender, RoutedEventArgs e) => ApplyZoom(_currentZoom - 0.15);
        private void OnZoomResetClicked(object sender, RoutedEventArgs e) => ApplyZoom(1.0);

        private void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                ApplyZoom(_currentZoom + (e.Delta > 0 ? 0.1 : -0.1));
                e.Handled = true;
            }
        }

        private void OnOpenExternalClicked(object sender, RoutedEventArgs e) => OpenInExternalViewer();

        private void OpenInExternalViewer()
        {
            try
            {
                Process.Start(new ProcessStartInfo(_filePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
