using MechCore.Core;

namespace MechCore.Models.Tolerances
{
    public class ToleranceLetterItem : ObservableObject
    {
        private bool _isEnabled = true;

        public string Letter { get; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public ToleranceLetterItem(string letter)
        {
            Letter = letter;
        }

        public override string ToString() => Letter;
    }
}

