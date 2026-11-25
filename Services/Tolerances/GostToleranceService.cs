using System;
using System.Linq;
using System.Text.RegularExpressions;
using MechCore.Models.Tolerances;

namespace MechCore.Services.Tolerances
{
    public class GostToleranceService : IToleranceService
    {
        public double CalculateStandardTolerance(double size, string itGrade)
        {
            int sizeIndex = FindSizeIndex(Gost25347Data.ToleranceTable, size);
            int gradeIndex = Array.IndexOf(Gost25347Data.ItGrades, itGrade);

            if (gradeIndex == -1)
                throw new ArgumentException("Квалитет должен быть в диапазоне от IT1 до IT18.", nameof(itGrade));

            double tolerance = Gost25347Data.ToleranceTable[sizeIndex].ElementAt(gradeIndex + 2);

            // Квалитеты IT1–IT11 указаны в мкм (делим на 1000), IT12–IT18 сразу в мм
            return gradeIndex <= 10 ? tolerance / 1000.0 : tolerance;
        }

        public ToleranceZoneResult CalculateElement(double nominalSize, string toleranceClass)
        {
            if (string.IsNullOrWhiteSpace(toleranceClass))
                throw new ArgumentException("Укажите поле допуска (например, H7 или k6).", nameof(toleranceClass));

            var match = Regex.Match(toleranceClass.Trim(), @"^(?<letters>[A-Za-z]+)(?<num>\d+)$");
            if (!match.Success)
                throw new ArgumentException($"Неверный формат поля допуска: {toleranceClass}", nameof(toleranceClass));

            string letters = match.Groups["letters"].Value;
            int itNum = int.Parse(match.Groups["num"].Value);
            string itGrade = $"IT{itNum}";

            double tolerance = CalculateStandardTolerance(nominalSize, itGrade);
            double lower = 0;
            double upper = 0;

            bool isShaft = letters == letters.ToLowerInvariant();

            if (isShaft)
            {
                int codeIndex = Array.IndexOf(Gost25347Data.ShaftCodes, letters);
                if (codeIndex >= 0)
                {
                    int sizeIndex = FindSizeIndex(Gost25347Data.ShaftDeviations, nominalSize);
                    double baseDeviation = Gost25347Data.ShaftDeviations[sizeIndex].ElementAt(codeIndex + 2) / 1000.0;

                    if (codeIndex <= 10)
                    {
                        upper = baseDeviation;
                        lower = baseDeviation - tolerance;
                    }
                    else
                    {
                        lower = baseDeviation;
                        upper = baseDeviation + tolerance;
                    }
                }
                else if (letters == "js")
                {
                    upper = tolerance / 2.0;
                    lower = -tolerance / 2.0;
                }
                else if (letters == "j")
                {
                    int sizeIndex = FindSizeIndex(Gost25347Data.ShaftDeviationsJ, nominalSize);
                    if (itNum == 5 || itNum == 6)
                    {
                        lower = Gost25347Data.ShaftDeviationsJ[sizeIndex].ElementAt(2) / 1000.0;
                        upper = lower + tolerance;
                    }
                    else if (itNum == 7)
                    {
                        lower = Gost25347Data.ShaftDeviationsJ[sizeIndex].ElementAt(3) / 1000.0;
                        upper = lower + tolerance;
                    }
                    else
                    {
                        throw new ArgumentException($"Для вала j поддерживаются только квалитеты 5, 6 и 7.");
                    }
                }
                else if (letters == "k")
                {
                    if (nominalSize <= 500 && itNum >= 4 && itNum <= 7)
                    {
                        int sizeIndex = FindSizeIndex(Gost25347Data.ShaftDeviationsK, nominalSize);
                        lower = Gost25347Data.ShaftDeviationsK[sizeIndex].ElementAt(2) / 1000.0;
                        upper = lower + tolerance;
                    }
                    else
                    {
                        lower = 0;
                        upper = lower + tolerance;
                    }
                }
                else
                {
                    throw new ArgumentException($"Неизвестное обозначение вала: {letters}");
                }
            }
            else
            {
                int codeIndex = Array.IndexOf(Gost25347Data.HoleCodes, letters);
                if (codeIndex >= 0)
                {
                    int sizeIndex = FindSizeIndex(Gost25347Data.HoleDeviations, nominalSize);
                    double baseDeviation = Gost25347Data.HoleDeviations[sizeIndex].ElementAt(codeIndex + 2) / 1000.0;
                    lower = baseDeviation;
                    upper = baseDeviation + tolerance;
                }
                else if (letters == "JS" || letters == "Js")
                {
                    upper = tolerance / 2.0;
                    lower = -tolerance / 2.0;
                }
                else if (letters == "J")
                {
                    int sizeIndex = FindSizeIndex(Gost25347Data.HoleDeviationsJ, nominalSize);
                    if (itNum >= 6 && itNum <= 8)
                    {
                        upper = Gost25347Data.HoleDeviationsJ[sizeIndex].ElementAt(itNum - 4) / 1000.0;
                        lower = upper - tolerance;
                    }
                    else
                    {
                        throw new ArgumentException($"Для отверстия J поддерживаются только квалитеты 6, 7 и 8.");
                    }
                }
                else if (letters == "K" || letters == "M" || letters == "N")
                {
                    int sizeIndex = FindSizeIndex(Gost25347Data.HoleDeviationsKMN, nominalSize);
                    int kmnIndex = Array.IndexOf(Gost25347Data.HoleCodesKMN, letters) + 2;

                    if (nominalSize > 500)
                    {
                        upper = Gost25347Data.HoleDeviationsKMN[sizeIndex].ElementAt(kmnIndex) / 1000.0;
                        lower = upper - tolerance;
                    }
                    else if (itNum >= 3 && itNum <= 8)
                    {
                        upper = Gost25347Data.HoleDeviationsKMN[sizeIndex].ElementAt(kmnIndex) / 1000.0;
                        upper += Gost25347Data.Deltas[sizeIndex].ElementAt(itNum - 1) / 1000.0;
                        lower = upper - tolerance;
                    }
                    else if (letters == "K")
                    {
                        upper = 0;
                        lower = -tolerance;
                    }
                    else if (letters == "M")
                    {
                        upper = Gost25347Data.HoleDeviationsKMN[sizeIndex].ElementAt(kmnIndex) / 1000.0;
                        lower = upper - tolerance;
                    }
                    else if (letters == "N")
                    {
                        upper = sizeIndex == 0 ? -4.0 / 1000.0 : 0;
                        lower = upper - tolerance;
                    }
                }
                else
                {
                    codeIndex = Array.IndexOf(Gost25347Data.HoleCodesPZC, letters);
                    if (codeIndex >= 0)
                    {
                        int sizeIndex = FindSizeIndex(Gost25347Data.HoleDeviationsPZC, nominalSize);
                        upper = Gost25347Data.HoleDeviationsPZC[sizeIndex].ElementAt(codeIndex + 2) / 1000.0;

                        if (itNum >= 3 && itNum <= 7 && nominalSize <= 500)
                        {
                            int deltaIndex = FindSizeIndex(Gost25347Data.Deltas, nominalSize);
                            upper += Gost25347Data.Deltas[deltaIndex].ElementAt(itNum - 1) / 1000.0;
                        }

                        lower = upper - tolerance;
                    }
                    else
                    {
                        throw new ArgumentException($"Неизвестное обозначение отверстия: {letters}");
                    }
                }
            }

            return new ToleranceZoneResult
            {
                Kind = isShaft ? ElementKind.Shaft : ElementKind.Hole,
                NominalSize = nominalSize,
                ToleranceClass = toleranceClass.Trim(),
                ToleranceValue = Math.Round(tolerance, 4),
                LowerDeviation = Math.Round(lower, 4),
                UpperDeviation = Math.Round(upper, 4)
            };
        }


        public FitCalculationResult CalculateFit(double nominalSize, string holeClass, string shaftClass)
        {
            var hole = CalculateElement(nominalSize, holeClass);
            var shaft = CalculateElement(nominalSize, shaftClass);

            if (hole.Kind != ElementKind.Hole)
                throw new ArgumentException("Первый параметр посадки должен быть отверстием (заглавная буква).", nameof(holeClass));

            if (shaft.Kind != ElementKind.Shaft)
                throw new ArgumentException("Второй параметр посадки должен быть валом (строчная буква).", nameof(shaftClass));

            FitType fitType;
            if (hole.LowerDeviation >= shaft.UpperDeviation)
            {
                fitType = FitType.Clearance;
            }
            else if (hole.UpperDeviation <= shaft.LowerDeviation)
            {
                fitType = FitType.Interference;
            }
            else
            {
                fitType = FitType.Transition;
            }

            return new FitCalculationResult
            {
                NominalSize = nominalSize,
                Hole = hole,
                Shaft = shaft,
                FitType = fitType
            };
        }

        private static int FindSizeIndex(double[][] table, double size)
        {
            int index = Array.FindIndex(table, r => size > r.First() && size <= r.ElementAt(1));
            if (index == -1)
                throw new ArgumentOutOfRangeException(nameof(size), $"Размер {size} мм выходит за пределы таблицы отклонений.");
            return index;
        }
    }
}
