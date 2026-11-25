using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;

namespace MechCore.Views.Standards
{
    public partial class GostDirectoryView : UserControl
    {
        private string? _lastRightClickedGostText;
        public GostDirectoryView()
        {
            InitializeComponent();
            ValidateGostLinksDebug();
        }

        private void OnHyperlinkNavigate(object sender, RequestNavigateEventArgs e)
        {
            string target = e.Uri.OriginalString;

            // Извлекаем название ГОСТа из текста ссылки для заголовка резервного окна
            string gostTitle = "Стандарт ГОСТ";
            if (sender is Hyperlink link)
            {
                string linkText = new TextRange(link.ContentStart, link.ContentEnd).Text.Trim();
                if (!string.IsNullOrWhiteSpace(linkText))
                    gostTitle = linkText;
            }

            // Если это веб-ссылка — просто открываем в браузере
            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось открыть ссылку: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                e.Handled = true;
                return;
            }

            // Ищем локальный PDF-файл в папке сборки (и при отладке — в папке исходников)
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string fullPath = Path.GetFullPath(Path.Combine(baseDir, target));

            if (!File.Exists(fullPath))
            {
                string projectDirPath = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..", target));
                if (File.Exists(projectDirPath))
                {
                    fullPath = projectDirPath;
                }
            }

            if (!File.Exists(fullPath))
            {
                MessageBox.Show(
                    $"Локальный файл стандарта не найден:\n{fullPath}\n\nПроверьте наличие файла в папке Assets\\GOSTs.",
                    "Файл ГОСТа не найден",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                e.Handled = true;
                return;
            }

            // 1. Сначала пробуем открыть PDF в нашем встроенном нативном окне GostPdfViewerWindow
            try
            {
                var viewerWindow = new GostPdfViewerWindow(gostTitle, fullPath);
                viewerWindow.Show();
            }
            catch (Exception)
            {
                // 2. Если при открытии во встроенном окне произошла ошибка —
                // открываем документ в системной программе по умолчанию
                Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
            }

            e.Handled = true;
        }
    
        private void OnHyperlinkContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is Hyperlink link)
            {
                _lastRightClickedGostText = new TextRange(link.ContentStart, link.ContentEnd).Text.Trim();
            }
        }

        private void OnCopyGostNumberClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_lastRightClickedGostText))
            {
                Clipboard.SetText(_lastRightClickedGostText);
            }
        }

        [Conditional("DEBUG")]
        private void ValidateGostLinksDebug()
        {
            var errors = new List<string>();
            var hyperlinks = FindVisualChildren<Hyperlink>(this).ToList();

            foreach (var link in hyperlinks)
            {
                if (link.NavigateUri == null)
                {
                    errors.Add("Обнаружена ссылка без NavigateUri.");
                    continue;
                }

                string uriPath = link.NavigateUri.OriginalString;
                string linkText = new TextRange(link.ContentStart, link.ContentEnd).Text.Trim();

                // Пропускаем внешние веб-ссылки
                if (uriPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    uriPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // 1. Проверка соответствия названия ГОСТа и имени файла
                // "ГОСТ 2.307-2011" -> "GOST_2.307-2011.pdf"
                string expectedFileName = linkText
                    .Replace("ГОСТ", "GOST", StringComparison.OrdinalIgnoreCase)
                    .Replace(" ", "_") + ".pdf";

                string actualFileName = Path.GetFileName(uriPath);

                if (!string.Equals(expectedFileName, actualFileName, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Несоответствие имени: текст «{linkText}», но ссылка ведёт на «{actualFileName}» (ожидалось «{expectedFileName}»).");
                }

                // 2. Проверка существования файла (повторяет логику поиска из OnHyperlinkNavigate)
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string fullPath = Path.GetFullPath(Path.Combine(baseDir, uriPath));

                if (!File.Exists(fullPath))
                {
                    string projectDirPath = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..", uriPath));
                    if (File.Exists(projectDirPath))
                    {
                        fullPath = projectDirPath;
                    }
                }

                if (!File.Exists(fullPath))
                {
                    errors.Add($"Файл не найден для «{linkText}»: {uriPath}");
                }
            }

            if (errors.Count > 0)
            {
                string report = string.Join("\n\n• ", errors);
                MessageBox.Show(
                    $"Обнаружены проблемы со ссылками ГОСТов ({errors.Count} шт.):\n\n• {report}",
                    "Отладка: проверка справочника ГОСТов",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // Вспомогательный метод для поиска Hyperlink внутри TextBlock и других элементов
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    yield return typedChild;

                // Hyperlink не является Visual, поэтому для TextBlock отдельно проверяем Inlines
                if (child is TextBlock textBlock)
                {
                    foreach (var inline in textBlock.Inlines)
                    {
                        if (inline is T inlineTyped)
                            yield return inlineTyped;
                    }
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.Trim();
            int totalVisibleItems = 0;

            // Проходим по всем карточкам разделов внутри SectionsGrid
            foreach (var card in FindVisualChildren<MaterialDesignThemes.Wpf.Card>(SectionsGrid))
            {
                if (card.Content is not StackPanel sectionPanel)
                    continue;

                int visibleInCard = 0;

                // Первый элемент в sectionPanel (индекс 0) — это заголовок карточки с иконкой, пропускаем его
                for (int i = 1; i < sectionPanel.Children.Count; i++)
                {
                    if (sectionPanel.Children[i] is not StackPanel itemPanel)
                        continue;

                    if (string.IsNullOrEmpty(query))
                    {
                        itemPanel.Visibility = Visibility.Visible;
                        visibleInCard++;
                    }
                    else
                    {
                        // Собираем весь текст из блока ГОСТа (номер + описание)
                        string itemText = string.Join(" ",
                            itemPanel.Children.OfType<TextBlock>()
                                .Select(tb => new TextRange(tb.ContentStart, tb.ContentEnd).Text));

                        bool isMatch = itemText.Contains(query, StringComparison.OrdinalIgnoreCase);
                        itemPanel.Visibility = isMatch ? Visibility.Visible : Visibility.Collapsed;

                        if (isMatch)
                            visibleInCard++;
                    }
                }

                // Скрываем всю карточку раздела, если в ней не осталось ни одного подходящего ГОСТа
                card.Visibility = visibleInCard > 0 ? Visibility.Visible : Visibility.Collapsed;
                totalVisibleItems += visibleInCard;
            }

            // Обновляем счётчик найденного и заглушку пустого результата
            SearchStatsText.Text = string.IsNullOrEmpty(query)
                ? string.Empty
                : $"Найдено: {totalVisibleItems}";

            EmptySearchMessage.Visibility = (!string.IsNullOrEmpty(query) && totalVisibleItems == 0)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }


    }
}
