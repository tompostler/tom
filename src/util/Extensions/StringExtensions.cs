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
    }
}
