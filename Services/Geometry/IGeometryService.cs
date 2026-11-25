using System.Collections.Generic;
using MechCore.Models.Geometry;

namespace MechCore.Services.Geometry;

public interface IGeometryService
{
    IReadOnlyList<DrillAngleItem> GetDrillAnglePresets();

    ChamferCalculationResult CalculateChamfer(
                        double axialLeg,
                        double angleDegrees,
                        double startX,
                        double startZ,
                        double approachZOffset,
                        bool useApproach,
                        bool isDecreasingX,
                        bool isLeftToRight,
                        bool isHole,
                        bool useFeed = true,
                        double feedRate = 0.12,
                        int spindleRpm = 1200);

    ArcInterpolationResult CalculateArcInterpolation(
                         double radius,
                         double inputX,
                         double inputZ,
                         ArcCoordinateInputMode inputMode,
                         bool isConcaveFillet,
                         bool isDecreasingX,
                         bool isLeftToRight,
                         bool isHole,
                         bool isFrontTurret,
                         bool useFeed = true,
                         double feedRate = 0.12,
                         int spindleRpm = 1200);

    TriangleCalculationResult CalculateCncCone(
                          ConeSolveKind solveKind,
                          double startDiameter,
                          double endDiameter,
                          double length,
                          double slopeAngleDeg,
                          double startZ,
                          bool isDecreasingX,
                          bool isLeftToRight,
                          bool isHole,
                          bool useFeed = true,
                          double feedRate = 0.15,
                          int spindleRpm = 1200);

    TriangleCalculationResult SolveTriangle(
                                TriangleSolveKind kind,
                                double val1,
                                double val2);


    BlindHoleCalculationResult CalculateBlindHole(
                              double diameter,
                              double tipAngleDegrees,
                              double inputDepth,
                              double startZ,
                              double retractR,
                              bool isTotalDepthInput,
                              bool isVertical,
                              bool roundSchemeValues = false,
                              bool useFeed = true,
                              double feedRate = 0.15,
                              int spindleRpm = 1000);

    GrooveCalculationResult CalculateGrooveOrPartOff(
                             bool isPartOffMode,
                             bool isLeftToRight,
                             double stockDiameter,
                             double grooveBottomDiameter,
                             double startZ,
                             double endZ,
                             double approachOffsetX,
                             double stepPMm,
                             double retractR,
                             double feedRate,
                             bool considerToolWidth,
                             double toolWidth,
                             int macroVariableNumber,
                             bool useAxisOvertravel,
                             double axisOvertravel,
                             bool reduceFeedNearCenter,
                             int spindleRpm = 1000,
                             bool includeFinishPass = false,
                             bool finishPassWithAllowance = true,
                             double finishAllowanceX = 0.2,
                             double finishAllowanceZ = 0.1);

    ConeCylinderBlendResult CalculateConeCylinderBlend(
                            double cylinderDiameter,
                            double cornerZ,
                            double coneAngleDeg,
                            double blendRadius,
                            bool isConcaveFillet,
                            bool isLeftToRight,
                            bool isHole,
                            bool isFrontTurret,
                            bool compensateToolNose = false,
                            double toolNoseRadius = 0.4,
                            bool useFeed = true,
                            double feedRate = 0.12,
                            int spindleRpm = 1200);

}
