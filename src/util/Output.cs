using System.Reflection;
using System.Text;
using Unlimitedinf.Utilities.Extensions;

namespace Unlimitedinf.Utilities
{
    /// <summary>
    /// Send output to the Console.
    /// </summary>
    public static class Output
    {
        /// <summary>
        /// Given an enumerable of objects and the expected property names, output those objects in a tabular format.
        /// Will inspect the current <see cref="Console.BufferWidth"/> and try to wrap columns where necessary.
        /// </summary>
        public static void WriteTable<T>(this IEnumerable<T> @this, params string[] propertyNames)
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

            // Convert the data into a 2d array while keeping track of the max data length in each column
            int[] columnWidths = propertyNames.Select(x => x.Length).ToArray();
            string[,] outputData = new string[@this.Count(), propertyNames.Length];
            for (int i = 0; i < @this.Count(); i++)
            {
                T row = @this.ElementAt(i);
                for (int j = 0; j < propertyNames.Length; j++)
                {
                    outputData[i, j] = ToNiceString(propertyMap[propertyNames[j]].GetValue(row));

                    // But if it would push us over the buffer width, then don't increase the size
                    // By evaluating here, we for the column widths to at least be the width of the property names
                    // Add up the length of the individual columns, and the spaces between them
                    int totalWidth = columnWidths.Sum() + columnWidths.Length;

                    if (totalWidth > Console.BufferWidth)
                    {
                        // We're already too wide. Do nothing
                    }
                    else if (columnWidths[j] < outputData[i, j].Length)
                    {
                        // The current column width is less than the new desired max width

                        if (totalWidth - columnWidths[j] + outputData[i, j].Length <= Console.BufferWidth)
                        {
                            // Adding the new column width will stay within the buffer, so it's fine
                            columnWidths[j] = outputData[i, j].Length;
                        }
                        else
                        {
                            // Adding the new column width will exceed the buffer
                            // Chop it off at the max, or preserve the current width
                            columnWidths[j] = Math.Max(columnWidths[j], Console.BufferWidth - totalWidth + columnWidths[j]);
                        }
                    }
                    else
                    {
                        // The current column width is equal to or larger than the new desired max width. Do nothing
                    }
                }
            }

            StringBuilder sb = new();
            char columnDiv = ' ';

            // Output the header row
            for (int columnIndex = 0; columnIndex < propertyNames.Length; columnIndex++)
            {
                _ = sb.Append(propertyNames[columnIndex].PadRight(columnWidths[columnIndex]));
                _ = sb.Append(columnDiv);
            }
            _ = sb.AppendLine();

            // Output the dashed line row
            for (int columnIndex = 0; columnIndex < propertyNames.Length; columnIndex++)
            {
                _ = sb.Append(new string('-', columnWidths[columnIndex]));
                _ = sb.Append(columnDiv);
            }
            _ = sb.AppendLine();

            // Output each row of data
            for (int rowIndex = 0; rowIndex < @this.Count(); rowIndex++)
            {
                List<StringBuilder> wrappedText = new();
                for (int columnIndex = 0; columnIndex < propertyNames.Length; columnIndex++)
                {
                    if (outputData[rowIndex, columnIndex].Length > columnWidths[columnIndex])
                    {
                        // Need to wrap the text across multiple lines
                        string[] chunks = outputData[rowIndex, columnIndex].Chunk(columnWidths[columnIndex]);
                        _ = sb.Append(chunks[0].PadRight(columnWidths[columnIndex]));
                        _ = sb.Append(columnDiv);

                        // Append the extra into additional wrapped text lines
                        for (int wrappedRowIndex = 1; wrappedRowIndex < chunks.Length; wrappedRowIndex++)
                        {
                            if (wrappedText.Count < wrappedRowIndex)
                            {
                                // We need to add a new stringbuilder and pad it to get to the current column
                                wrappedText.Add(new(new string(' ', columnWidths.Take(columnIndex).Sum() + columnIndex)));
                            }

                            // Add the chunk to the stringbuilder, and trim the start to mark sure it looks nicer left-aligned
                            _ = wrappedText[wrappedRowIndex - 1].Append(chunks[wrappedRowIndex].TrimStart().PadRight(columnWidths[columnIndex]));
                            _ = wrappedText[wrappedRowIndex - 1].Append(columnDiv);
                        }
                    }
                    else
                    {
                        // Whole text fits in the box
                        _ = sb.Append(outputData[rowIndex, columnIndex].PadRight(columnWidths[columnIndex]));
                        _ = sb.Append(columnDiv);

                        // And if there's any stringbuilders, pad them
                        foreach (StringBuilder wrappedTextBuilder in wrappedText)
                        {
                            _ = wrappedTextBuilder.Append(new string(' ', columnWidths[columnIndex]));
                            _ = wrappedTextBuilder.Append(columnDiv);
                        }
                    }

                }
                _ = sb.AppendLine();

                // And if there's any stringbuilders, append them too
                foreach (StringBuilder wrappedTextBuilder in wrappedText)
                {
                    _ = sb.AppendLine(wrappedTextBuilder.ToString());
                }
            }

            Console.WriteLine(sb.ToString());
        }

        private static string ToNiceString(object obj)
        {
            if (obj == null)
            {
                return string.Empty;
            }

            if (obj is DateTimeOffset objDto)
            {
                return objDto.ToString("u");
            }
            if (obj is DateTime objDt)
            {
                return objDt.ToString("u");
            }

            return obj?.ToString() ?? string.Empty;
        }
    }
}
