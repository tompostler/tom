using System;
using System.Text;
using System.Text.Encodings.Web;
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
            indentedOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };
            indentedOptions.Converters.Add(new JsonStringEnumConverter());

            options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            options.Converters.Add(new JsonStringEnumConverter());
        }

        /// <summary>
        /// Convert an object to a string of json and then base64 encode it as UTF8.
        /// </summary>
        public static string ToBase64JsonString(this object value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value.ToJsonString()));

        /// <summary>
        /// Convert an object to a binary array of (optionally indented) UTF8-encoded json.
        /// </summary>
        public static byte[] ToJsonBytes(this object value, bool indented = false) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, indented ? indentedOptions : options));

        /// <summary>
        /// Convert an object to a string of (optionally indented) json.
        /// </summary>
        public static string ToJsonString(this object value, bool indented = false) => JsonSerializer.Serialize(value, indented ? indentedOptions : options);
    }
}
