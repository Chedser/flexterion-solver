using MechCore.Models.Milling;

namespace MechCore.Services.Milling
{
    public interface IMillingCuttingService
    {
        MillingCuttingResult Calculate(MillingCuttingRequest request);
        MillingFeedCalcResult CalculateFeed(MillingFeedCalcRequest request);
    }
}
