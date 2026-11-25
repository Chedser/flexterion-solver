using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MechCore.Models.Common;

namespace MechCore.Controls
{
    public partial class NumberPickerButton : UserControl
    {
        public static readonly DependencyProperty SelectedValueProperty =
            DependencyProperty.Register(
                nameof(SelectedValue),
                typeof(string),
                typeof(NumberPickerButton),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string SelectedValue
        {
            get => (string)GetValue(SelectedValueProperty);
            set => SetValue(SelectedValueProperty, value);
        }

        private readonly List<NumberRangeGroup> _groups;

        public NumberPickerButton()
        {
            InitializeComponent();

            _groups = new List<NumberRangeGroup>
            {
                new("Мелкие детали и инструмент (1 – 65 мм)", 1, 65, 1),
                new("Основной диапазон (20 – 250 мм)", 20, 250, 1),
                new("Среднегабаритные детали (250 – 500 мм)", 250, 500, 1),
                new("Крупногабаритные детали (500 – 1500 мм)", 500, 1500, 5),
                new("Сверхтяжёлая обработка (1500 – 3150 мм)", 1500, 3150, 10)
            };

            GroupsComboBox.ItemsSource = _groups;
            GroupsComboBox.SelectedIndex = 1; // По умолчанию: "Основной диапазон (20 – 250 мм)"
        }

        private void OnOpenButtonClick(object sender, RoutedEventArgs e)
        {
            PickerPopup.IsOpen = !PickerPopup.IsOpen;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            PickerPopup.IsOpen = false;
        }

        private void OnGroupSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupsComboBox.SelectedItem is NumberRangeGroup selectedGroup)
            {
                ColumnsItemsControl.ItemsSource = selectedGroup.Columns;
            }
        }

        private void OnNumberCellClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is double val)
            {
                SelectedValue = val.ToString("0.####", CultureInfo.InvariantCulture);
                PickerPopup.IsOpen = false;
            }
        }
    }
}
