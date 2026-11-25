using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MechCore.Core
{
    public static class NumericInputBehavior
    {
        public static readonly DependencyProperty IsDecimalProperty =
            DependencyProperty.RegisterAttached(
                "IsDecimal",
                typeof(bool),
                typeof(NumericInputBehavior),
                new PropertyMetadata(false, OnInputModeChanged));

        public static bool GetIsDecimal(DependencyObject obj) => (bool)obj.GetValue(IsDecimalProperty);
        public static void SetIsDecimal(DependencyObject obj, bool value) => obj.SetValue(IsDecimalProperty, value);

        public static readonly DependencyProperty IsIntegerProperty =
            DependencyProperty.RegisterAttached(
                "IsInteger",
                typeof(bool),
                typeof(NumericInputBehavior),
                new PropertyMetadata(false, OnInputModeChanged));

        public static bool GetIsInteger(DependencyObject obj) => (bool)obj.GetValue(IsIntegerProperty);
        public static void SetIsInteger(DependencyObject obj, bool value) => obj.SetValue(IsIntegerProperty, value);

        private static void OnInputModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox textBox) return;

            textBox.PreviewTextInput -= OnPreviewTextInput;
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            DataObject.RemovePastingHandler(textBox, OnPasting);

            if (GetIsDecimal(textBox) || GetIsInteger(textBox))
            {
                textBox.PreviewTextInput += OnPreviewTextInput;
                textBox.PreviewKeyDown += OnPreviewKeyDown;
                DataObject.AddPastingHandler(textBox, OnPasting);
            }
        }

        private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox) return;
            e.Handled = !IsValidInput(textBox, e.Text);
        }

        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Блокируем клавишу пробела (PreviewTextInput её не перехватывает)
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private static void OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string pastedText = (string)e.DataObject.GetData(typeof(string))!;
                if (!IsValidInput(textBox, pastedText))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private static bool IsValidInput(TextBox textBox, string newText)
        {
            // Формируем итоговую строку с учётом выделенного текста (если пользователь заменяет его)
            string currentText = textBox.Text;
            int selStart = textBox.SelectionStart;
            int selLength = textBox.SelectionLength;

            string resultingText = currentText.Remove(selStart, selLength).Insert(selStart, newText);
            bool integerOnly = GetIsInteger(textBox);

            int separatorCount = 0;
            for (int i = 0; i < resultingText.Length; i++)
            {
                char c = resultingText[i];
                if (!integerOnly && (c == '.' || c == ','))
                {
                    // Запрещаем начинать число с точки или ставить больше одного разделителя
                    if (i == 0 || ++separatorCount > 1)
                        return false;
                }
                else if (!char.IsDigit(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
