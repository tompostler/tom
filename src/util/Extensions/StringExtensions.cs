using System;
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
        /// Convert a base64-encoded json string back to an object.
        /// </summary>
        public static T FromBase64JsonString<T>(this string value) => JsonSerializer.Deserialize<T>(new ReadOnlySpan<byte>(Convert.FromBase64String(value)), options);

        /// <summary>
        /// Convert a json string back to an object.
        /// </summary>
        public static T FromJsonString<T>(this string value) => JsonSerializer.Deserialize<T>(value, options);
    }
}
