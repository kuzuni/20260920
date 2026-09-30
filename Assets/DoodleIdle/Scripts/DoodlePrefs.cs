using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using System.IO;
using CodeStage.AntiCheat.ObscuredTypes;

namespace DoodleIdle
{
    /// <summary>Account-scoped game preferences; legacy editor profiles remain untouched.</summary>
    public static class DoodlePrefs
    {
        [Serializable] public sealed class Entry { public string key, type; public ObscuredString value; }
        [Serializable] public sealed class Snapshot { public int version = 1; public bool dirty; public List<Entry> entries = new List<Entry>(); }
        static string prefix = "";
        static string FilePath => Path.Combine(Application.persistentDataPath, "saves", prefix + "json");
        static Snapshot state = new Snapshot();
        public static bool HasAccount => prefix.Length > 0;
        public static bool Dirty { get; private set; }
        public static long Revision { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { prefix = ""; state = new Snapshot(); Dirty = false; Revision = 0; }
        public static void UseAccount(string id)
        {
            prefix = string.IsNullOrEmpty(id) ? "" : "DoodleAccount." + DoodlePurchaseLedger.Hash(id) + ".";
            string raw = "";
            try
            {
                if (HasAccount && File.Exists(FilePath)) raw = DoodleProtectedJson.Decrypt(File.ReadAllText(FilePath), prefix);
                state = string.IsNullOrEmpty(raw) ? new Snapshot() : DoodleJson.FromJson<Snapshot>(raw);
                Validate(state);
            }
            catch (System.Security.Cryptography.CryptographicException) { DoodleSecurity.Detected(); throw; }
            catch (Newtonsoft.Json.JsonException) { DoodleSecurity.Detected(); throw; }
            catch (InvalidDataException) { DoodleSecurity.Detected(); throw; }
            Dirty = HasAccount && state.dirty;
            Revision = 0;
        }
        static Entry Find(string key)
        {
            foreach (var entry in state.entries) if (entry.key == key) return entry;
            return null;
        }
        public static bool HasKey(string key) => HasAccount ? Find(key) != null : PlayerPrefs.HasKey(key);
        public static string GetString(string key, string fallback = "") => HasAccount ? Find(key)?.value ?? fallback : PlayerPrefs.GetString(key, fallback);
        public static int GetInt(string key, int fallback = 0) => HasAccount ? int.TryParse(Find(key)?.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : fallback : PlayerPrefs.GetInt(key, fallback);
        public static float GetFloat(string key, float fallback = 0) => HasAccount ? float.TryParse(Find(key)?.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) ? n : fallback : PlayerPrefs.GetFloat(key, fallback);
        static void Set(string key, string value, string type)
        {
            var e = Find(key);
            if (e != null && e.value == value && e.type == type) return;
            if (e == null) { e = new Entry { key = key }; state.entries.Add(e); }
            e.value = value; e.type = type; Dirty = true; Revision++;
        }
        public static void SetString(string key, string value) { if (HasAccount) Set(key, value, "string"); else PlayerPrefs.SetString(key, value); }
        public static void SetInt(string key, int value) { if (HasAccount) Set(key, value.ToString(CultureInfo.InvariantCulture), "int"); else PlayerPrefs.SetInt(key, value); }
        public static void SetFloat(string key, float value) { if (HasAccount) Set(key, value.ToString("R", CultureInfo.InvariantCulture), "float"); else PlayerPrefs.SetFloat(key, value); }
        public static void DeleteKey(string key)
        {
            if (!HasAccount) { PlayerPrefs.DeleteKey(key); return; }
            if (state.entries.RemoveAll(e => e.key == key) > 0) { Dirty = true; Revision++; }
        }
        public static string Export() => DoodleJson.ToJson(state);
        public static void Import(string json)
        {
            var next = DoodleJson.FromJson<Snapshot>(json);
            Validate(next);
            state = next; Dirty = false; Revision++; Flush();
        }
        static void Validate(Snapshot next)
        {
            if (next == null || next.version != 1 || next.entries == null) throw new InvalidDataException("Invalid save.");
            var keys = new HashSet<string>();
            foreach (var e in next.entries)
                if (e == null || string.IsNullOrEmpty(e.key) || !e.key.StartsWith("Doodle", StringComparison.Ordinal) || !keys.Add(e.key) || (e.type != "string" && e.type != "int" && e.type != "float"))
                    throw new InvalidDataException("Invalid save entries.");
        }
        public static void MarkSynced(long revision) { if (Revision == revision) { Dirty = false; Flush(); } }
        public static void Save() { if (!HasAccount) PlayerPrefs.Save(); }
        public static void Flush()
        {
            if (DoodleSecurity.Compromised) return;
            if (!HasAccount) { PlayerPrefs.Save(); return; }
            state.dirty = Dirty;
            DoodleProtectedJson.WriteAtomic(FilePath, DoodleProtectedJson.Encrypt(Export(), prefix));
        }
        public static void DeleteAccountCache()
        {
            if (HasAccount && File.Exists(FilePath)) File.Delete(FilePath);
            UseAccount(null);
        }
    }
}
