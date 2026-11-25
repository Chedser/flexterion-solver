using MechCore.Models.Turning;

namespace MechCore.Services.Turning
{
    public interface ITurningHeaderService
    {
        TurningHeaderResult GenerateHeader(TurningHeaderRequest request);
    }
}
