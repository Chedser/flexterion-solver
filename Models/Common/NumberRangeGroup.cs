using System.Collections.Generic;
using System.Linq;

namespace MechCore.Models.Common
{
    public class NumberCellItem
    {
        public double Value { get; }
        public string DisplayText => Value.ToString("0.####");
        public bool IsColumnHeader { get; } // Первый элемент в каждом столбце из 10 штук

        public NumberCellItem(double value, bool isColumnHeader)
        {
            Value = value;
            IsColumnHeader = isColumnHeader;
        }
    }

    public class NumberRangeGroup
    {
        public string Title { get; }
        public List<List<NumberCellItem>> Columns { get; }

        public NumberRangeGroup(string title, double start, double end, double step)
        {
            Title = title;
            var allItems = new List<NumberCellItem>();

            int index = 0;
            for (double val = start; val <= end + 0.0001; val += step)
            {
                bool isFirstInColumn = (index % 10 == 0);
                allItems.Add(new NumberCellItem(val, isFirstInColumn));
                index++;
            }

            // Разбиваем общий список на вертикальные столбцы по 10 элементов в каждом
            Columns = allItems
                .Select((item, idx) => new { item, idx })
                .GroupBy(x => x.idx / 10)
                .Select(g => g.Select(x => x.item).ToList())
                .ToList();
        }
    }
}
