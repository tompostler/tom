using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Unlimitedinf.Utilities
{
    /// <summary>
    /// Send output to the Console.
    /// </summary>
    public static class Output
    {
        /// <summary>
        /// Given an enumerable of objects and the expected property names, output those objects in a tabular format.
        /// </summary>
        public static void WriteTable<T>(this IEnumerable<T> @this, params string[] propertyNames)
        {
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
            int[] columnLengths = propertyNames.Select(x => x.Length).ToArray();
            string[,] outputData = new string[@this.Count(), propertyNames.Length];
            for (int i = 0; i < @this.Count(); i++)
            {
                T row = @this.ElementAt(i);
                for (int j = 0; j < propertyNames.Length; j++)
                {
                    outputData[i, j] = ToNiceString(propertyMap[propertyNames[j]].GetValue(row));
                    columnLengths[j] = Math.Max(columnLengths[j], outputData[i, j].Length);
                }
            }

            StringBuilder sb = new();

            // Output the header row
            for (int i = 0; i < propertyNames.Length; i++)
            {
                _ = sb.Append(propertyNames[i].PadRight(columnLengths[i]));
                _ = sb.Append(' ');
            }
            _ = sb.AppendLine();

            // Output the dashed line row
            for (int i = 0; i < propertyNames.Length; i++)
            {
                _ = sb.Append(new string('-', columnLengths[i]));
                _ = sb.Append(' ');
            }
            _ = sb.AppendLine();

            // Output each row of data
            for (int i = 0; i < @this.Count(); i++)
            {
                for (int j = 0; j < propertyNames.Length; j++)
                {
                    _ = sb.Append(outputData[i, j].PadRight(columnLengths[j]));
                    _ = sb.Append(' ');
                }
                _ = sb.AppendLine();
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
