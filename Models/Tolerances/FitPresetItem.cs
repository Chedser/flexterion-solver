namespace MechCore.Models.Tolerances
{
    public class FitPresetItem
    {
        public string Fit { get; }
        public string Description { get; }
        public string DisplayText => $"{Fit} ({Description})";

        public FitPresetItem(string fit, string description)
        {
            Fit = fit;
            Description = description;
        }
    }
}
