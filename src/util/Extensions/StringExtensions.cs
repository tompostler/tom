using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Unlimitedinf.Utilities.Extensions
{
    /// <summary>
    /// Extensions for strings.
    /// </summary>
    public static class StringExtensions
    {
        private static readonly JsonSerializerOptions options;
        static StringExtensions()
        {
            options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
        }

        /// <summary>
        /// Using UTF8 encoding, convert a string to its (lowercase) hashed SHA256 value.
        /// </summary>
        public static string ComputeSHA256(this string value)
        {
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            return hash.ToLowercaseHash();
        }

        /// <summary>
        /// Convert a base64-encoded UTF8 json string back to an object.
        /// </summary>
        public static T FromBase64JsonString<T>(this string value) => JsonSerializer.Deserialize<T>(new ReadOnlySpan<byte>(Convert.FromBase64String(value)), options);

        /// <summary>
        /// Convert a json string back to an object.
        /// </summary>
        public static T FromJsonString<T>(this string value) => JsonSerializer.Deserialize<T>(value, options);

        /// <summary>
        /// Given an input string, split it into chunks.
        /// If the input string is not a multiple of <paramref name="chunkSize"/>, then the last chunk will be smaller.
        /// </summary>
        public static string[] Chunk(this string value, int chunkSize)
        {
            if (chunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be >0");
            }
            else if (string.IsNullOrEmpty(value))
            {
                return Array.Empty<string>();
            }
            else if (chunkSize >= value.Length)
            {
                return new[] { value };
            }
            else
            {
                // Init the array
                string[] result = new string[(int)Math.Ceiling((double)value.Length / chunkSize)];
                for (int chunk = 0; chunk * chunkSize < value.Length; chunk++)
                {
                    result[chunk] = value.Substring(chunk * chunkSize, Math.Min(chunkSize, value.Length - chunk * chunkSize));
                }
                return result;
            }
        }
    }
}
