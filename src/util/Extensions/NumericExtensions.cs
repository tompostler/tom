using System.Globalization;
using System.Text;

namespace Unlimitedinf.Utilities.Extensions
{
    /// <summary>
    /// Extensions for primitive numeric types.
    /// </summary>
    public static class NumericExtensions
    {
        /// <summary>
        /// Convert a number of bytes to a base2 representation
        /// </summary>
        public static string AsBytesToFriendlyString(this long bytes)
        {
            if (bytes < 1_048_576)
            {
                return $"{bytes / 1_024d:0.00}KiB";
            }
            else if (bytes >= 1_048_576 && bytes < 1_073_741_824)
            {
                return $"{bytes / 1_048_576d:0.00}MiB";
            }
            else if (bytes >= 1_073_741_824 && bytes < 1_099_511_627_776)
            {
                return $"{bytes / 1_073_741_824d:0.00}GiB";
            }
            else
            {
                return $"{bytes / 1_099_511_627_776d:0.00}TiB";
            }
        }

        /// <summary>
        /// Convert a number of bytes to a base10 numbers of bits
        /// </summary>
        public static string AsBytesToFriendlyBitString(this long bytes)
        {
            decimal bits = bytes * 8;
            if (bits < 1_000_000)
            {
                return $"{bits / 1_000:0.00}Kb";
            }
            else if (bits >= 1_000_000 && bits < 1_000_000_000)
            {
                return $"{bits / 1_000_000:0.00}Mb";
            }
            else if (bits >= 1_000_000_000 && bits < 1_000_000_000_000)
            {
                return $"{bits / 1_000_000_000:0.00}Gb";
            }
            else
            {
                return $"{bits / 1_000_000_000_000:0.00}Tb";
            }
        }

        /// <summary>
        /// Given a source number, convert it to a baseX representation of that number.
        /// Primary goal is shortened counting values that are still "double-click safe" in browsers.
        /// Implementation details:<br/>
        ///     - Base2-Base63 conversions are custom implementation, but stable.<br/>
        ///     - Base2-Base10 is numeric.<br/>
        ///     - Base11-Base36 is uppercase alphabet.<br/>
        ///     - Base37-Base62 is lowercase alphabet.<br/>
        ///     - Base63 adds underscore (to maintain double-click to copy behavior).<br/>
        /// </summary>
        /// <param name="this">Source value to convert</param>
        /// <param name="base">The base-X notation to use. Max of 63.</param>
        public static string ToBaseX(this long @this, byte @base)
        {
            bool wasNeg = false;
            if (@this < 0)
            {
                wasNeg = true;
                @this *= -1;
            }
            string positiveResult = ToBaseX((ulong)@this, @base);

            return wasNeg ? '-' + positiveResult : positiveResult;
        }

        /// <summary>
        /// Given a source number, convert it to a baseX representation of that number.
        /// Primary goal is shortened counting values that are still "double-click safe" in browsers.
        /// Implementation details:<br/>
        ///     - Base2-Base63 conversions are custom implementation, but stable.<br/>
        ///     - Base2-Base10 is numeric.<br/>
        ///     - Base11-Base36 is uppercase alphabet.<br/>
        ///     - Base37-Base62 is lowercase alphabet.<br/>
        ///     - Base63 adds underscore (to maintain double-click to copy behavior).<br/>
        /// </summary>
        /// <param name="this">Source value to convert</param>
        /// <param name="base">The base-X notation to use. Max of 63.</param>
        public static string ToBaseX(this ulong @this, byte @base)
        {
            if (@base < 2 || @base > 63)
            {
                throw new ArgumentOutOfRangeException(nameof(@base), "Maximum of Base63 and minimum of Base2");
            }
            else if (@this == 0)
            {
                return "0";
            }
            else if (@base == 10)
            {
                return @this.ToString(CultureInfo.InvariantCulture);
            }

            const string sourceChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_";
            string chars = sourceChars.Substring(0, @base);
            StringBuilder sb = new();

            while (@this > 0)
            {
                _ = sb.Insert(0, chars[(int)(@this % (ulong)chars.Length)]);
                @this /= (ulong)chars.Length;
            }

            return sb.ToString();
        }
    }
}
