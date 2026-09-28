using UnityEngine;

namespace DoodleIdle
{
    public static class DoodleLanguage
    {
        const string Key = "Doodle.Language";
        public static bool Korean => PlayerPrefs.GetString(Key, Application.systemLanguage == SystemLanguage.Korean ? "ko" : "en") == "ko";
        public static string Text(string korean, string english) => Korean ? korean : english;
        public static void Set(bool korean) { PlayerPrefs.SetString(Key, korean ? "ko" : "en"); PlayerPrefs.Save(); }
    }
}
