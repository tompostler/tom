using System.Text.Json;
using System.Text.Json.Serialization;

namespace Unlimitedinf.Utilities.Extensions
{
    /// <summary>
    /// Extensions for the base object type.
    /// </summary>
    public static class ObjectExtensions
    {
        private static readonly JsonSerializerOptions options;
        static ObjectExtensions()
        {
            options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            options.WriteIndented = true;
        }

        /// <summary>
        /// Convert an object to a string of indented json.
        /// </summary>
        public static string ToJsonString(this object value) => JsonSerializer.Serialize(value, options);
    }
}
