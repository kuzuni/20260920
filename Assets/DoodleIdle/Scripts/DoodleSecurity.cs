using CodeStage.AntiCheat.Detectors;
using CodeStage.AntiCheat.Storage;
using UnityEngine;

namespace DoodleIdle
{
    public sealed class DoodleSecurity : MonoBehaviour
    {
        public static bool Compromised { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Compromised = false; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var guard = new GameObject("Integrity guard").AddComponent<DoodleSecurity>(); DontDestroyOnLoad(guard.gameObject);
#if !UNITY_EDITOR
            ObscuredCheatingDetector.StartDetection(Detected);
            SpeedHackDetector.StartDetection(Detected, 1f, 5, 30);
            ObscuredPrefs.NotGenuineDataDetected += Detected;
#endif
        }
        public static void Detected()
        {
            if (Compromised) return;
            Compromised = true;
            var ui = FindFirstObjectByType<DoodleUi>(); if (ui) ui.StopSavingForReset();
            Time.timeScale = 0;
#if UNITY_EDITOR
            Debug.LogError("Game data integrity check failed. Saving is disabled.");
#else
            Application.Quit();
#endif
        }
    }
}
