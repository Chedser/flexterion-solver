namespace MechCore.Services.Materials
{
    public interface IMaterialChartService
    {
        MaterialChartSummary BuildChart(
            MaterialChartMetric metric,
            string activeIsoCode,
            string materialTitle,
            double currentHb);
    }
}
