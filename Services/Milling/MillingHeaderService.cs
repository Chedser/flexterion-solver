using System;
using System.Collections.Generic;
using System.Globalization;
using MechCore.Models.Milling;
using MechCore.Models.Turning;

namespace MechCore.Services.Milling
{
    public class MillingHeaderService : IMillingHeaderService
    {
        public MillingHeaderResult GenerateHeader(MillingHeaderRequest request)
        {
            var result = new MillingHeaderResult { IsValid = true };
            var codeLines = new List<string>();
            var warnings = new List<string>();
            var errors = new List<string>();

            // 1. Номер управляющей программы
            if (request.IncludeProgramNumber)
            {
                if (!int.TryParse(request.ProgramNumberRaw, out int progNum) || progNum < 1 || progNum > 9999)
                {
                    result.IsValid = false;
                    errors.Add("Некорректный номер программы: введите целое число от 0001 до 9999.");
                }
                else
                {
                    string formattedProg = $"O{progNum:D4}";
                    codeLines.Add("%");
                    result.LineExplanations.Add(new GCodeLineExplanation("%", "Символ начала файла программы для передачи в стойку ЧПУ."));

                    codeLines.Add(formattedProg);
                    result.LineExplanations.Add(new GCodeLineExplanation(formattedProg, $"Номер управляющей программы ({progNum})."));
                }
            }

            // 2. Строка безопасности (сброс модальных режимов)
            var safetyCodes = new List<string> { "G00" };
            var safetyDesc = new List<string> { "G00 (ускоренный ход)" };

            if (!string.IsNullOrWhiteSpace(request.WorkPlaneCode) && request.WorkPlaneCode != "Не выводить")
            {
                safetyCodes.Add(request.WorkPlaneCode);
                string planeText = request.WorkPlaneCode switch
                {
                    "G17" => "G17 (плоскость XY — стандарт 3-осевого фрезерования)",
                    "G18" => "G18 (плоскость XZ)",
                    "G19" => "G19 (плоскость YZ)",
                    _ => request.WorkPlaneCode
                };
                safetyDesc.Add(planeText);

                if (request.WorkPlaneCode != "G17")
                {
                    warnings.Add($"Выбрана плоскость {request.WorkPlaneCode}: для обычного 3-осевого фрезерования и циклов сверления используется плоскость XY (G17).");
                }
            }

            if (request.UseMetricG21)
            {
                string metricCode = request.SystemKind == MillingCncSystemKind.SinumerikIso ? "G71" : "G21";
                safetyCodes.Add(metricCode);
                safetyDesc.Add($"{metricCode} (миллиметры)");
            }

            if (request.UseG40)
            {
                safetyCodes.Add("G40");
                safetyDesc.Add("G40 (отмена коррекции на радиус фрезы)");
            }

            if (request.UseG49 && request.SystemKind != MillingCncSystemKind.SinumerikIso)
            {
                safetyCodes.Add("G49");
                safetyDesc.Add("G49 (отмена коррекции длины инструмента)");
            }

            if (request.UseG80)
            {
                safetyCodes.Add("G80");
                safetyDesc.Add("G80 (отмена постоянных циклов)");
            }

            if (request.UseG90)
            {
                safetyCodes.Add("G90");
                safetyDesc.Add("G90 (абсолютные координаты)");
            }
            else
            {
                warnings.Add("Отключён код G90 (абсолютные координаты): убедитесь, что программа не останется в приращениях G91.");
            }

            if (request.UseG94)
            {
                safetyCodes.Add("G94");
                safetyDesc.Add("G94 (минутная подача мм/мин)");
            }

            if (safetyCodes.Count > 1)
            {
                string safetyLine = string.Join(" ", safetyCodes);
                codeLines.Add(safetyLine);
                result.LineExplanations.Add(new GCodeLineExplanation(
                    safetyLine,
                    "Строка безопасности: " + string.Join(", ", safetyDesc) + "."));
            }

            // 3. Безопасный отвод шпинделя вверх по оси Z перед сменой инструмента
            if (request.UseSafeZHome)
            {
                switch (request.HomeSyntax)
                {
                    case MillingHomeSyntaxKind.G91G28Z0:
                        codeLines.Add("G91 G28 Z0");
                        result.LineExplanations.Add(new GCodeLineExplanation(
                            "G91 G28 Z0",
                            "Подъём шпинделя в верхнюю точку смены инструмента по оси Z (ноль станка)."));

                        codeLines.Add("G90");
                        result.LineExplanations.Add(new GCodeLineExplanation(
                            "G90",
                            "Обязательный возврат в абсолютную систему координат после команды G91."));
                        break;

                    case MillingHomeSyntaxKind.G53Z0:
                        codeLines.Add("G00 G53 Z0");
                        result.LineExplanations.Add(new GCodeLineExplanation(
                            "G00 G53 Z0",
                            "Подъём шпинделя в верхнюю крайнюю точку по машинным координатам станка (G53 Z0)."));
                        break;

                    case MillingHomeSyntaxKind.SupaZ0D0:
                        codeLines.Add("SUPA G0 Z0 D0");
                        result.LineExplanations.Add(new GCodeLineExplanation(
                            "SUPA G0 Z0 D0",
                            "Безопасный подъём шпинделя Sinumerik в нулевую точку станка с временным подавлением всех смещений."));
                        break;
                }
            }

            // 4. Вызов и смена инструмента
            int toolNumInt = 1;
            int offsetNumInt = 1;
            int.TryParse(request.ToolNumber, out toolNumInt);
            int.TryParse(request.LengthOffsetNumber, out offsetNumInt);

            if (request.IncludeToolCall)
            {
                if (request.ToolChangeSyntax == MillingToolChangeSyntaxKind.TxD1M06)
                {
                    int dNum = Math.Max(1, offsetNumInt);
                    string toolLine = $"T{toolNumInt} D{dNum} M06";
                    codeLines.Add(toolLine);
                    result.LineExplanations.Add(new GCodeLineExplanation(
                        toolLine,
                        $"Установка инструмента №{toolNumInt} в шпиндель (M06) с активацией корректора Sinumerik D{dNum}."));
                }
                else
                {
                    string tStr = request.ToolNumber.PadLeft(2, '0');
                    string toolLine = $"T{tStr} M06";
                    codeLines.Add(toolLine);
                    result.LineExplanations.Add(new GCodeLineExplanation(
                        toolLine,
                        $"Вызов инструмента №{tStr} из магазина и автоматическая смена инструмента (M06)."));
                }
            }

            // 5. Рабочая система координат (РСК)
            if (!string.IsNullOrWhiteSpace(request.WorkOffset) && request.WorkOffset != "Не выводить")
            {
                codeLines.Add(request.WorkOffset);
                result.LineExplanations.Add(new GCodeLineExplanation(
                    request.WorkOffset,
                    $"Выбор рабочей системы координат {request.WorkOffset} (привязка нуля детали X0 Y0 Z0)."));
            }

            // 6. Запуск шпинделя
            if (request.IncludeSpindleStart)
            {
                if (!int.TryParse(request.SpindleRpmRaw, out int rpm) || rpm <= 0)
                {
                    result.IsValid = false;
                    errors.Add("Укажите корректную частоту вращения шпинделя S (об/мин).");
                }
                else
                {
                    if (rpm > 12000)
                    {
                        warnings.Add($"Высокие обороты шпинделя ({rpm} об/мин): проверьте балансировку оправки и паспортный предел фрезы.");
                    }

                    string dirCode = request.IsSpindleClockwise ? "M03" : "M04";
                    string dirText = request.IsSpindleClockwise ? "по часовой стрелке (правое вращение)" : "против часовой стрелки (левое вращение)";
                    string spindleLine = $"S{rpm} {dirCode}";

                    codeLines.Add(spindleLine);
                    result.LineExplanations.Add(new GCodeLineExplanation(
                        spindleLine,
                        $"Запуск шпинделя {dirText} на частоте {rpm} об/мин."));
                }
            }

            // 7. Компенсация длины инструмента G43 и подвод к безопасной высоте Z
            if (request.UseSafeZApproach)
            {
                string normZ = (request.SafeZHeightRaw ?? string.Empty).Trim().Replace(',', '.');
                if (!double.TryParse(normZ, NumberStyles.Any, CultureInfo.InvariantCulture, out double safeZ))
                {
                    result.IsValid = false;
                    errors.Add("Укажите корректную безопасную высоту подвода по оси Z (мм).");
                }
                else
                {
                    if (safeZ <= 0)
                    {
                        warnings.Add($"Опасно! Безопасная высота Z = {safeZ.ToString("0.###", CultureInfo.InvariantCulture)} мм ниже или на уровне нуля детали (Z0) — риск столкновения с заготовкой или прижимами!");
                    }
                    else if (safeZ < 10.0)
                    {
                        warnings.Add($"Малая безопасная высота подвода Z = {safeZ.ToString("0.###", CultureInfo.InvariantCulture)} мм: убедитесь, что фреза гарантированно выше прижимов и выступающих элементов.");
                    }

                    string zStr = safeZ.ToString("0.0##", CultureInfo.InvariantCulture);
                    if (request.ToolChangeSyntax == MillingToolChangeSyntaxKind.TxD1M06 ||
                    request.SystemKind == MillingCncSystemKind.SinumerikIso)
                    {
                        string approachLine = $"G00 Z{zStr}";
                        codeLines.Add(approachLine);
                        result.LineExplanations.Add(new GCodeLineExplanation(
                        approachLine,
                        $"Ускоренный подвод инструмента к безопасной плоскости Z = {zStr} мм над деталью."));
                    }
                    else
                    {
                        string hStr = request.LengthOffsetNumber.PadLeft(2, '0');
                        if (hStr == "00")
                        {
                            warnings.Add("Опасно! Выбран корректор длины H00 — длина инструмента не учитывается, высок риск врезания шпинделя в стол!");
                        }
                        else if (request.IncludeToolCall && toolNumInt != offsetNumInt)
                        {
                            warnings.Add($"Внимание! Номер инструмента (T{request.ToolNumber.PadLeft(2, '0')}) не совпадает с корректором длины (H{hStr}). На фрезерном станке это частая причина поломки фрезы.");
                        }
                        string approachLine = $"G43 H{hStr} Z{zStr}";
                        codeLines.Add(approachLine);
                        result.LineExplanations.Add(new GCodeLineExplanation(
                        approachLine,
                        $"Активация корректора длины инструмента H{hStr} и подвод к безопасной плоскости Z = {zStr} мм."));
                    }
                }
            }
            // 8. Подача СОЖ / обдув
            if (!string.IsNullOrWhiteSpace(request.CoolantCode) && request.CoolantCode != "Нет")
            {
                codeLines.Add(request.CoolantCode);
                string coolantDesc = request.CoolantCode switch
                {
                    "M08" => "Включение подачи смазочно-охлаждающей жидкости (СОЖ).",
                    "M07" => "Включение подачи масляного тумана / охлаждения воздухом (M07).",
                    _ => $"Включение охлаждения ({request.CoolantCode})."
                };
                result.LineExplanations.Add(new GCodeLineExplanation(request.CoolantCode, coolantDesc));
            }
            result.HasWarning = warnings.Count > 0;
            result.WarningMessage = string.Join(Environment.NewLine, warnings);
            result.HasError = errors.Count > 0;
            result.ErrorMessage = string.Join(Environment.NewLine, errors);
            result.GeneratedGCode = result.IsValid
            ? string.Join(Environment.NewLine, codeLines)
            : "—";
            return result;
        }
    }
}