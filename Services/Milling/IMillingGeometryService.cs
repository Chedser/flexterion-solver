using MechCore.Models.Milling;

namespace MechCore.Services.Milling
{
    public interface IMillingGeometryService
    {
        HolePatternCalculationResult CalculateHolePattern(
                                    HolePatternMode mode,
                                    double centerX,
                                    double centerY,
                                    double pcdDiameter,
                                    int holeCount,
                                    double startAngleDeg,
                                    double customStepAngleDeg,
                                    bool useFullCircle,
                                    bool isCounterClockwise,
                                    int gridCountX,
                                    int gridCountY,
                                    double gridStepX,
                                    double gridStepY,
                                    double gridRotationAngleDeg,
                                    bool useZigZagOrder,
                                    bool skipFirstPointInCycleCode);

        HelicalCalculationResult CalculateHelicalInterpolation(
                                double holeDiameter,
                                double toolDiameter,
                                int fluteCount,
                                double centerX,
                                double centerY,
                                double startZ,
                                double targetZ,
                                double clearanceR,
                                double rampAngleDeg,
                                double edgeFeedPerMin,
                                int spindleRpm,
                                bool isClimbMilling,
                                bool includeBottomCleanupPass);

        MillingThreadCalculationResult CalculateMillingThread(
                                bool isThreadMillingMode,
                                bool isExternalThread,
                                bool isLeftHandThread,
                                double threadDiameter,
                                double threadPitch,
                                double toolDiameter,
                                int toolFlutes,
                                double centerX,
                                double centerY,
                                double startZ,
                                double targetZ,
                                double clearanceR,
                                double edgeFeedPerMin,
                                int spindleRpm);

        ScallopCalculationResult CalculateScallopHeight(
                                bool isByTargetRoughness,
                                bool isTargetRaMode,
                                bool isBallNose,
                                double toolDiameter,
                                double cornerRadius,
                                double targetValueMicrons,
                                double inputStepoverAe,
                                double feedPerMin);

    }

}
