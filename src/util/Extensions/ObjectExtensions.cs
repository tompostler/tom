using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Unlimitedinf.Utilities.Extensions
{
    /// <summary>
    /// Extensions for the base object type.
    /// </summary>
    public static class ObjectExtensions
    {
        private static readonly JsonSerializerOptions indentedOptions;
        private static readonly JsonSerializerOptions options;

        static ObjectExtensions()
        {
            indentedOptions = new JsonSerializerOptions();
            indentedOptions.Converters.Add(new JsonStringEnumConverter());
            indentedOptions.WriteIndented = true;

            options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
        }

        /// <summary>
        /// Convert an object to a string of json and then base64 encode it as UTF8.
        /// </summary>
        public static string ToBase64JsonString(this object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value.ToJsonString()));

        /// <summary>
        /// Convert an object to a string of (optionally indented) json.
        /// </summary>
        public static string ToJsonString(this object value, bool indented = false) => JsonSerializer.Serialize(value, indented ? indentedOptions : options);
    }
}
