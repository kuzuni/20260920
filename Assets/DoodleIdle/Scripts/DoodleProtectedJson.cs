using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CodeStage.AntiCheat.Storage;
using UnityEngine;

namespace DoodleIdle
{
    /// <summary>JSON envelope containing authenticated, encrypted account JSON.</summary>
    public static class DoodleProtectedJson
    {
        [Serializable] sealed class Envelope { public int version = 2; public string iv, payload, mac; }
        static byte[] Key()
        {
            const string name = "Doodle.LocalEncryptionKey.v1";
            string encoded = ObscuredPrefs.GetString(name, "");
            if (string.IsNullOrEmpty(encoded))
            {
                var key = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(key);
                encoded = Convert.ToBase64String(key); ObscuredPrefs.SetString(name, encoded); ObscuredPrefs.Save();
            }
            return Convert.FromBase64String(encoded);
        }
        static byte[] Derive(byte[] master, string purpose)
        {
            using (var hmac = new HMACSHA256(master)) return hmac.ComputeHash(Encoding.UTF8.GetBytes("DoodleSave.v2/" + purpose));
        }
        public static string Encrypt(string json, string account)
        {
            var envelope = new Envelope(); byte[] key = Key();
            using (var aes = Aes.Create())
            {
                aes.Key = Derive(key, "encryption"); aes.GenerateIV(); envelope.iv = Convert.ToBase64String(aes.IV);
                using (var cipher = aes.CreateEncryptor())
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    envelope.payload = Convert.ToBase64String(cipher.TransformFinalBlock(bytes, 0, bytes.Length));
                }
            }
            envelope.mac = Mac(Derive(key, "authentication"), account, envelope);
            return JsonUtility.ToJson(envelope);
        }
        static string Mac(byte[] key, string account, Envelope envelope)
        {
            using (var hmac = new HMACSHA256(key))
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(envelope.version + "|" + account + "|" + envelope.iv + "|" + envelope.payload)));
        }
        public static string Decrypt(string json, string account)
        {
            try { return DecryptCore(json, account); }
            catch (FormatException error) { throw new CryptographicException("Malformed save envelope.", error); }
            catch (ArgumentException error) { throw new CryptographicException("Malformed save envelope.", error); }
        }
        static string DecryptCore(string json, string account)
        {
            var envelope = JsonUtility.FromJson<Envelope>(json);
            if (envelope == null || (envelope.version != 1 && envelope.version != 2) || string.IsNullOrEmpty(envelope.mac) || string.IsNullOrEmpty(envelope.iv) || string.IsNullOrEmpty(envelope.payload)) throw new CryptographicException("Invalid save envelope.");
            byte[] key = Key();
            byte[] expected = Convert.FromBase64String(Mac(envelope.version == 1 ? key : Derive(key, "authentication"), account, envelope)), actual = Convert.FromBase64String(envelope.mac);
            int difference = expected.Length ^ actual.Length;
            for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ (i < actual.Length ? actual[i] : 0);
            if (difference != 0) throw new CryptographicException("Save integrity check failed.");
            using (var aes = Aes.Create())
            {
                aes.Key = envelope.version == 1 ? key : Derive(key, "encryption"); aes.IV = Convert.FromBase64String(envelope.iv);
                using (var cipher = aes.CreateDecryptor())
                {
                    var bytes = Convert.FromBase64String(envelope.payload);
                    return Encoding.UTF8.GetString(cipher.TransformFinalBlock(bytes, 0, bytes.Length));
                }
            }
        }
        public static void WriteAtomic(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(json); stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
    }
}
