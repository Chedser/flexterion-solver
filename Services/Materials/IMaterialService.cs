using System.Collections.Generic;
using MechCore.Models.Materials;

namespace MechCore.Services.Materials
{
    public enum HardnessScale
    {
        HRC,
        HB,
        HV
    }

    public interface IMaterialService
    {
        List<IsoMaterialGroupItem> GetIsoGroups();
        List<MaterialGradeItem> GetAllGrades();
        HardnessConversionResult ConvertHardness(double value, HardnessScale sourceScale);
    }
}
