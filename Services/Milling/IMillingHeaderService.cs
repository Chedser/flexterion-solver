using MechCore.Models.Milling;

namespace MechCore.Services.Milling
{
    public interface IMillingHeaderService
    {
        MillingHeaderResult GenerateHeader(MillingHeaderRequest request);
    }
}
