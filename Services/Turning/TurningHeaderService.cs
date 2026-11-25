using System;
using System.Collections.Generic;
using MechCore.Models.Turning;

namespace MechCore.Services.Turning
{
    public class TurningHeaderService : ITurningHeaderService
    {
        public TurningHeaderResult GenerateHeader(TurningHeaderRequest request)
        {
            var result = new TurningHeaderResult { IsValid = true };
            var codeLines = new List<string>();
            var warnings = new List<string>();
            var errors = new List<string>();

            // 1. Номер программы
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

            // 2. Проверка конфликта G90 в System A
            if (request.UseG90 && request.SystemKind == CncSystemKind.FanucSystemA)
            {
                warnings.Add("Внимание! В Fanuc System A код G90 запускает цикл продольного точения, а не абсолютные координаты. Отключите G90 или переключите систему на System B/C.");
            }
            else if (request.UseG90 && request.SystemKind == CncSystemKind.CustomManual)
            {
                warnings.Add("Включён код G90: убедитесь, что ваша стойка использует System B/C (в System A G90 — это цикл точения).");
            }

            // 3. Строка безопасности
            var safetyCodes = new List<string> { "G00" };
            var safetyDesc = new List<string> { "G00 (ускоренный ход)" };

            if (request.UseG18)
            {
                safetyCodes.Add("G18");
                safetyDesc.Add("G18 (плоскость XZ)");
            }
            if (request.UseG21)
            {
                safetyCodes.Add("G21");
                safetyDesc.Add("G21 (миллиметры)");
            }
            if (request.UseG40)
            {
                safetyCodes.Add("G40");
                safetyDesc.Add("G40 (отмена коррекции радиуса)");
            }
            if (request.UseG80)
            {
                safetyCodes.Add("G80");
                safetyDesc.Add("G80 (отмена циклов)");
            }
            if (request.UseG90)
            {
                safetyCodes.Add("G90");
                safetyDesc.Add("G90 (абсолютные координаты)");
            }
            if (request.UseG97Safety)
            {
                safetyCodes.Add("G97");
                safetyDesc.Add("G97 (постоянные обороты, сброс CSS)");
            }
            if (request.UseFeedPerRev)
            {
                string feedCode = request.SystemKind == CncSystemKind.FanucSystemBC ? "G95" : "G99";
                safetyCodes.Add(feedCode);
                safetyDesc.Add($"{feedCode} (подача мм/об)");
            }

            if (safetyCodes.Count > 1)
            {
                string safetyLine = string.Join(" ", safetyCodes);
                codeLines.Add(safetyLine);
                result.LineExplanations.Add(new GCodeLineExplanation(
                    safetyLine,
                    "Строка безопасности: " + string.Join(", ", safetyDesc) + "."));
            }

            // 4. Рабочая система координат (РСК)
            if (!string.IsNullOrWhiteSpace(request.WorkOffset) && request.WorkOffset != "Не выводить")
            {
                codeLines.Add(request.WorkOffset);
                result.LineExplanations.Add(new GCodeLineExplanation(
                    request.WorkOffset,
                    $"Выбор рабочей системы координат {request.WorkOffset} (ноль детали Z0 на правом торце)."));
            }

            // 5. Безопасный выход в ноль станка (раздельно по осям)
            if (request.UseSafeHomeReturn)
            {
                if (request.UseSystemAHomeSyntax)
                {
                    codeLines.Add("G28 U0");
                    result.LineExplanations.Add(new GCodeLineExplanation(
                        "G28 U0",
                        "Безопасный отвод револьверной головки вверх по оси X в ноль станка (нулевое приращение U0)."));

                    codeLines.Add("G28 W0");
                    result.LineExplanations.Add(new GCodeLineExplanation(
                        "G28 W0",
                        "Безопасный отвод револьверной головки вправо от патрона по оси Z в ноль станка (нулевое приращение W0)."));
                }
                else
                {
                    codeLines.Add("G91 G28 X0");
                    result.LineExplanations.Add(new GCodeLineExplanation(
                        "G91 G28 X0",
                        "Переход в приращения (G91) и безопасный отвод вверх по оси X в ноль станка."));

                    codeLines.Add("G91 G28 Z0");
                    result.LineExplanations.Add(new GCodeLineExplanation(
                        "G91 G28 Z0",
                        "Безопасный отвод вправо от патрона по оси Z в ноль станка."));

                    codeLines.Add("G90");
                    result.LineExplanations.Add(new GCodeLineExplanation(
                        "G90",
                        "Обязательный возврат в абсолютную систему координат после команды G91."));
                }
            }

            // 6. Ограничение максимальных оборотов шпинделя (G50 / G92)
            string limitCode = string.IsNullOrWhiteSpace(request.RpmLimitCode) ? "G50" : request.RpmLimitCode;

            if (request.UseMaxRpmLimit)
            {
                if (!int.TryParse(request.MaxRpmRaw, out int maxRpm) || maxRpm <= 0)
                {
                    result.IsValid = false;
                    errors.Add($"Укажите корректное максимальное число оборотов для команды {limitCode} S...");
                }
                else
                {
                    if (maxRpm > 4500)
                    {
                        warnings.Add($"Высокое ограничение оборотов ({maxRpm} об/мин): проверьте паспортный предел токарного патрона и вылет заготовки.");
                    }

                    string limitLine = $"{limitCode} S{maxRpm}";
                    codeLines.Add(limitLine);
                    result.LineExplanations.Add(new GCodeLineExplanation(
                        limitLine,
                        $"Ограничение максимальной частоты вращения шпинделя до {maxRpm} об/мин (защита от разгона в режиме G96)."));
                }
            }
            else if (request.IncludeSpindleStart && request.UseConstantSurfaceSpeed)
            {
                warnings.Add($"Опасно! Выбран режим постоянной скорости резания G96, но отключено ограничение оборотов {limitCode}. При подходе резца к центру (X0) шпиндель разгонится до максимума!");
            }

            // 7. Вызов инструмента и корректора
            if (request.IncludeToolCall)
            {
                string tNum = request.ToolNumber.PadLeft(2, '0');
                string oNum = request.OffsetNumber.PadLeft(2, '0');
                string toolLine = $"T{tNum}{oNum}";

                if (oNum == "00")
                {
                    warnings.Add("Внимание! Выбран корректор 00 — вылет резца не учитывается, возможен зарез или столкновение.");
                }
                else if (tNum != oNum)
                {
                    warnings.Add($"Номер инструмента ({tNum}) не совпадает с номером корректора ({oNum}). Убедитесь, что это сделано намеренно.");
                }

                codeLines.Add(toolLine);
                result.LineExplanations.Add(new GCodeLineExplanation(
                    toolLine,
                    $"Выбор позиции револьверной головки №{tNum} и активация корректора геометрии резца №{oNum}."));
            }

            // 8. Запуск шпинделя и СОЖ
            if (request.IncludeSpindleStart)
            {
                if (!int.TryParse(request.SpeedValueRaw, out int speedVal) || speedVal <= 0)
                {
                    result.IsValid = false;
                    errors.Add(request.UseConstantSurfaceSpeed
                        ? "Укажите скорость резания Vc (м/мин) для команды G96 S..."
                        : "Укажите частоту вращения шпинделя (об/мин) для команды G97 S...");
                }
                else
                {
                    string modeCode = request.UseConstantSurfaceSpeed ? "G96" : "G97";
                    string dirCode = request.IsSpindleClockwise ? "M03" : "M04";
                    string dirText = request.IsSpindleClockwise ? "по часовой стрелке" : "против часовой стрелки";
                    string modeText = request.UseConstantSurfaceSpeed
                        ? $"постоянная скорость резания {speedVal} м/мин"
                        : $"постоянные обороты {speedVal} об/мин";

                    string spindleLine = $"{modeCode} S{speedVal} {dirCode}";
                    codeLines.Add(spindleLine);
                    result.LineExplanations.Add(new GCodeLineExplanation(
                        spindleLine,
                        $"Включение шпинделя ({dirText}): {modeText}."));
                }

                if (request.UseCoolant)
                {
                    codeLines.Add("M08");
                    result.LineExplanations.Add(new GCodeLineExplanation(
                        "M08",
                        "Включение подачи смазочно-охлаждающей жидкости (СОЖ)."));
                }
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