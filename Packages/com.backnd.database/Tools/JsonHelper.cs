using System;
using System.Globalization;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BACKND.Database
{
    /// <summary>
    /// JSON serialization helper for database operations
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// Deserialize object from JSON string to target type
        /// </summary>
        /// <typeparam name="T">Target type</typeparam>
        /// <param name="value">Input object (string or other)</param>
        /// <returns>Deserialized object of type T</returns>
        public static T DeserializeObject<T>(object value)
        {
            if (value == null)
                return default(T);

            // If the value is already the correct type, return it directly
            if (value is T directValue)
                return directValue;

            // Convert to string and deserialize
            string jsonString = value.ToString();

            try
            {
                return JsonConvert.DeserializeObject<T>(jsonString);
            }
            catch (JsonException)
            {
                // IL2CPP (iOS/WebGL) fallback: JToken.Parse → ToObject<T>()
                try
                {
                    var token = JToken.Parse(jsonString);
                    return token.ToObject<T>();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"Failed to deserialize JSON for type {typeof(T).Name}: {ex.Message}");
                    return default(T);
                }
            }
        }

        /// <summary>
        /// Serialize object to JSON string
        /// </summary>
        /// <param name="value">Object to serialize</param>
        /// <returns>JSON string</returns>
        public static string SerializeObject(object value)
        {
            if (value == null)
                return null;

            return JsonConvert.SerializeObject(value);
        }

        /// <summary>
        /// Convert object to string, preserving ISO 8601 format for DateTime
        /// </summary>
        /// <param name="value">Input object</param>
        /// <returns>String representation</returns>
        public static string ConvertToString(object value)
        {
            if (value == null)
                return null;

            if (value is string strValue)
                return strValue;

            if (value is DateTime dateValue)
                return dateValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

            return value.ToString();
        }
    }
}