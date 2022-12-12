﻿namespace Unlimitedinf.Utilities.Extensions
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
    }
}
