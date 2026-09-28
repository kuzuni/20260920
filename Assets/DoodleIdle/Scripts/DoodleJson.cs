using System;
using CodeStage.AntiCheat.ObscuredTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DoodleIdle
{
    // Each call owns its settings; never modify JsonConvert.DefaultSettings (Unity uses it too).
    public static class DoodleJson
    {
        static JsonSerializerSettings Settings() => new JsonSerializerSettings {
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 64,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Converters = { new ProtectedValues(), new ColorValues() }
        };
        public static string ToJson(object value, bool pretty = false) => JsonConvert.SerializeObject(value, pretty ? Formatting.Indented : Formatting.None, Settings());
        public static object FromJson(string value, Type type) => JsonConvert.DeserializeObject(value, type, Settings());
        public static T FromJson<T>(string value) => JsonConvert.DeserializeObject<T>(value, Settings());
        public static void FromJsonOverwrite(string json, object value) => JsonConvert.PopulateObject(json, value, Settings());
        sealed class ColorValues : JsonConverter
        {
            public override bool CanConvert(Type type) => type == typeof(Color);
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var c = (Color)value;
                new JObject { ["r"] = c.r, ["g"] = c.g, ["b"] = c.b, ["a"] = c.a }.WriteTo(writer);
            }
            public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
            {
                var c = JObject.Load(reader);
                return new Color((float?)c["r"] ?? 0, (float?)c["g"] ?? 0, (float?)c["b"] ?? 0, (float?)c["a"] ?? 1);
            }
        }
        sealed class ProtectedValues : JsonConverter
        {
            public override bool CanConvert(Type t) => t == typeof(ObscuredInt) || t == typeof(ObscuredLong) || t == typeof(ObscuredBool) || t == typeof(ObscuredFloat) || t == typeof(ObscuredDouble) || t == typeof(ObscuredString);
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value is ObscuredInt i) writer.WriteValue((int)i);
                else if (value is ObscuredLong l) writer.WriteValue((long)l);
                else if (value is ObscuredBool b) writer.WriteValue((bool)b);
                else if (value is ObscuredFloat f) writer.WriteValue((float)f);
                else if (value is ObscuredDouble d) writer.WriteValue((double)d);
                else if (value is ObscuredString s) writer.WriteValue((string)s);
                else writer.WriteNull();
            }
            public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
            {
                if (type == typeof(ObscuredInt)) return (ObscuredInt)Convert.ToInt32(reader.Value);
                if (type == typeof(ObscuredLong)) return (ObscuredLong)Convert.ToInt64(reader.Value);
                if (type == typeof(ObscuredBool)) return (ObscuredBool)Convert.ToBoolean(reader.Value);
                if (type == typeof(ObscuredFloat)) return (ObscuredFloat)Convert.ToSingle(reader.Value);
                if (type == typeof(ObscuredDouble)) return (ObscuredDouble)Convert.ToDouble(reader.Value);
                return (ObscuredString)(reader.Value == null ? null : Convert.ToString(reader.Value));
            }
        }
    }
}
