using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unlimitedinf.Utilities.Extensions
{
    /// <summary>
    /// Extensions for various enumerables.
    /// </summary>
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Convert an array of UTF8-encoded json back to a specified type
        /// </summary>
        public static T FromJsonBytes<T>(this byte[] @this, int length) => Encoding.UTF8.GetString(@this, 0, length).FromJsonString<T>();

        /// <summary>
        /// Calculate the medain of an enumerable. Attempts to convert to doubles.
        /// </summary>
        public static double Median<T>(this IEnumerable<T> source)
        {
            int count = source.Count();
            if (count == 0)
            {
                throw new ArgumentException("No elements to median-ize", nameof(source));
            }

            source = source.OrderBy(n => n);

            int midpoint = count / 2;
            if (count % 2 == 0)
            {
                return (Convert.ToDouble(source.ElementAt(midpoint - 1)) + Convert.ToDouble(source.ElementAt(midpoint))) / 2.0;
            }
            else
            {
                return Convert.ToDouble(source.ElementAt(midpoint));
            }
        }
    }
}
