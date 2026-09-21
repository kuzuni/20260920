using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleIdle
{
    public static class DoodleGameData
    {
        public static readonly string[] SaveKeys = {
            "DoodleUi.Gold", "DoodleUi.Diamonds", "DoodleUi.CameraMode", "DoodleUi.SkipSummonAnimations",
            "DoodleUi.Collections.v1", "DoodleUi.Services.v1", "DoodleUi.Skins", "DoodleUi.SkillRefundRemainder",
            "DoodleUi.Commerce.Armor", "DoodleUi.Commerce.Club", "DoodleUi.Commerce.Skill",
            "DoodleUi.Commerce.Companion", "DoodleUi.Commerce.Relic"
        };
        public static void ResetSavedProgress()
        {
            foreach (var key in SaveKeys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
        public static void ResetAndRestart(DoodleIdleGame game)
        {
            string scene = game ? game.gameObject.scene.path : null;
            // Stop old UI coroutines/autosaves before clearing data and loading the starter state.
            if (game) game.gameObject.SetActive(false);
            ResetSavedProgress();
            if (!string.IsNullOrEmpty(scene)) SceneManager.LoadScene(scene);
        }
    }
}
