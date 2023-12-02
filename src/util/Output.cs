using System.Reflection;
using System.Text;
using Unlimitedinf.Utilities.Extensions;

namespace Unlimitedinf.Utilities
{
    /// <summary>
    /// Format output.
    /// </summary>
    public static class Output
    {

        /// <summary>
        /// Given an enumerable of objects and the expected property names, output those objects in a tabular format to the console.
        /// Will inspect the current <see cref="Console.BufferWidth"/> and try to wrap columns where necessary.
        /// </summary>
        public static void WriteTable<T>(this IEnumerable<T> @this, params string[] propertyNames)
            => Console.WriteLine(@this.WriteTable(Console.BufferWidth - 1, propertyNames));

        /// <summary>
        /// Given an enumerable of objects and the expected property names, output those objects in a tabular format to a string.
        /// Will use the <paramref name="bufferWidth"/> to try to wrap columns where necessary.
        /// </summary>
        public static string WriteTable<T>(this IEnumerable<T> @this, int bufferWidth, params string[] propertyNames)
        {
            @this ??= Enumerable.Empty<T>();
            PropertyInfo[] availableProperties = typeof(T).GetProperties();
            var propertyMap = availableProperties.ToDictionary(x => x.Name);

            // Validate
            foreach (string propertyName in propertyNames)
            {
                if (!propertyMap.ContainsKey(propertyName))
                {
                    throw new ArgumentException($"Property [{propertyName}] was not found in type {typeof(T).FullName}. Available options were {string.Join(", ", propertyMap)}");
                }
            }

            // Convert the data into a 2d array while keeping track of the max data length in each column.
            int[] columnWidths = propertyNames.Select(DetermineColumnWidthFromName).ToArray();
            string[,] outputData = new string[@this.Count(), propertyNames.Length];
            for (int i = 0; i < @this.Count(); i++)
            {
                T row = @this.ElementAt(i);
                for (int j = 0; j < propertyNames.Length; j++)
                {
                    outputData[i, j] = ToNiceString(propertyMap[propertyNames[j]].GetValue(row));

                    // But if it would push us over the buffer width, then don't increase the size.
                    // By evaluating here, we force the column widths to at least be the width of the property names.
                    // Add up the length of the individual columns, and the spaces between them.
                    int totalWidth = columnWidths.Sum() + columnWidths.Length;

                    if (totalWidth > bufferWidth)
                    {
                        // We're already too wide. Do nothing.
                    }
                    else if (columnWidths[j] < outputData[i, j].Length)
                    {
                        // The current column width is less than the new desired max width.

                        if (totalWidth - columnWidths[j] + outputData[i, j].Length <= bufferWidth)
                        {
                            // Adding the new column width will stay within the buffer, so it's fine.
                            columnWidths[j] = outputData[i, j].Length;
                        }
                        else
                        {
                            // Adding the new column width will exceed the buffer.
                            // Chop it off at the max, or preserve the current width.
                            columnWidths[j] = Math.Max(columnWidths[j], bufferWidth - totalWidth + columnWidths[j]);
                        }
                    }
                    else
                    {
                        // The current column width is equal to or larger than the new desired max width. Do nothing.
                    }
                }
            }

            StringBuilder sb = new();
            char columnDiv = ' ';

            // Output the header row.
            for (int columnIndex = 0; columnIndex < propertyNames.Length; columnIndex++)
            {
                _ = sb.Append(propertyNames[columnIndex].PadRight(columnWidths[columnIndex]));
                _ = sb.Append(columnDiv);
            }
            _ = sb.AppendLine();

            // Output the dashed line row.
            for (int columnIndex = 0; columnIndex < propertyNames.Length; columnIndex++)
            {
                _ = sb.Append(new string('-', columnWidths[columnIndex]));
                _ = sb.Append(columnDiv);
            }
            _ = sb.AppendLine();

            // Output each row of data.
            for (int rowIndex = 0; rowIndex < @this.Count(); rowIndex++)
            {
                List<StringBuilder> wrappedText = new();
                for (int columnIndex = 0; columnIndex < propertyNames.Length; columnIndex++)
                {
                    if (outputData[rowIndex, columnIndex].Length > columnWidths[columnIndex])
                    {
                        // Need to wrap the text across multiple lines.
                        string[] chunks = outputData[rowIndex, columnIndex].Chunk(columnWidths[columnIndex]);
                        _ = sb.Append(chunks[0].PadRight(columnWidths[columnIndex]));
                        _ = sb.Append(columnDiv);

                        // Append the extra into additional wrapped text lines.
                        for (int wrappedRowIndex = 1; wrappedRowIndex < chunks.Length; wrappedRowIndex++)
                        {
                            if (wrappedText.Count < wrappedRowIndex)
                            {
                                // We need to add a new stringbuilder and pad it to get to the current column.
                                wrappedText.Add(new(new string(' ', columnWidths.Take(columnIndex).Sum() + columnIndex)));
                            }

                            // Add the chunk to the stringbuilder, and trim the start to mark sure it looks nicer left-aligned.
                            _ = wrappedText[wrappedRowIndex - 1].Append(chunks[wrappedRowIndex].TrimStart().PadRight(columnWidths[columnIndex]));
                            _ = wrappedText[wrappedRowIndex - 1].Append(columnDiv);
                        }
                    }
                    else
                    {
                        // Whole text fits in the box.
                        // If it's purely numeric, PadLeft instead of PadRight.
                        _ = double.TryParse(outputData[rowIndex, columnIndex], out _)
                            ? sb.Append(outputData[rowIndex, columnIndex].PadLeft(columnWidths[columnIndex]))
                            : sb.Append(outputData[rowIndex, columnIndex].PadRight(columnWidths[columnIndex]));
                        _ = sb.Append(columnDiv);

                        // And if there's any stringbuilders, pad them.
                        foreach (StringBuilder wrappedTextBuilder in wrappedText)
                        {
                            _ = wrappedTextBuilder.Append(new string(' ', columnWidths[columnIndex]));
                            _ = wrappedTextBuilder.Append(columnDiv);
                        }
                    }

                }
                _ = sb.AppendLine();

                // And if there's any stringbuilders, append them too.
                foreach (StringBuilder wrappedTextBuilder in wrappedText)
                {
                    _ = sb.AppendLine(wrappedTextBuilder.ToString());
                }
            }

            return sb.ToString();
        }

        private static int DetermineColumnWidthFromName(string columnName)
        {
            // Special column names, and minimum column width is 4.
            int columnWidth = columnName.ToLowerInvariant() switch
            {
                "price" => 6,
                "total" => 8,
                _ => 4
            };

            // Then increase it, if necessary, to the column name.
            columnWidth = Math.Max(columnWidth, columnName.Length);

            return columnWidth;
        }

        private static string ToNiceString(object obj)
        {
            if (obj == null)
            {
                return string.Empty;
            }

            // Decimals default to 4 digits, so reduce that to the default 2 of a double.
            if (obj is decimal objDecimal)
            {
                return objDecimal.ToString("F2");
            }

            // Always ensure doubles have 2 digits.
            if (obj is double objDouble)
            {
                return objDouble.ToString("F2");
            }

            // If the date object represents a midnight, then only show the date.
            // Used to compare .Date back to the object, but found that was not always equal.
            if (obj is DateTimeOffset objDto)
            {
                return objDto.Hour == 0 && objDto.Minute == 0 && objDto.Second == 0
                    ? objDto.ToString("yyyy-MM-dd")
                    : objDto.ToString("u");
            }
            if (obj is DateTime objDt)
            {
                return objDt.Hour == 0 && objDt.Minute == 0 && objDt.Second == 0
                    ? objDt.ToString("yyyy-MM-dd")
                    : objDt.ToString("u");
            }
            if (obj is DateOnly objDo)
            {
                return objDo.ToString("o");
            }

            // Round time span to hundredths of seconds.
            // Leave off days or hours unless they have value as most time spans encountered are generally short.
            // And apparently you have to calculate the negative yourself.
            if (obj is TimeSpan objTs)
            {
                if (objTs.TotalDays >= 1)
                {
                    return objTs.ToString(@"dd\.hh\:mm\:ss\.ff");
                }
                if (objTs.TotalDays <= -1)
                {
                    return objTs.ToString(@"\-dd\.hh\:mm\:ss\.ff");
                }
                else if (objTs.TotalHours >= 1)
                {
                    return objTs.ToString(@"hh\:mm\:ss\.ff");
                }
                else if (objTs.TotalHours <= -1)
                {
                    return objTs.ToString(@"\-hh\:mm\:ss\.ff");
                }
                else
                {
                    return objTs.ToString((objTs < TimeSpan.Zero ? @"\-" : string.Empty) + @"mm\:ss\.ff");
                }
            }

            return obj?.ToString() ?? string.Empty;
        }
    }
}
