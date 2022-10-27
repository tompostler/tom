using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Unlimitedinf.Tom
{
    internal static class Extensions
    {
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

        /// <summary>
        /// Using the width and height of an image, return its megapixel count;
        /// </summary>
#pragma warning disable CA1416 // Validate platform compatibility
        public static double GetMegapixels(this Image image) => image.Width * image.Height / 1e6;
#pragma warning restore CA1416 // Validate platform compatibility
    }
}
