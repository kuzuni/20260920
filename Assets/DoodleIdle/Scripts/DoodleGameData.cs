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
            "DoodleUi.Commerce.Companion", "DoodleUi.Commerce.Relic", "DoodleUi.Commerce.DungeonRelic"
        };
        public static void ResetSavedProgress()
        {
            foreach (var key in SaveKeys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
        public static void ResetAndRestart(DoodleIdleGame game)
        {
            var previousScene = game ? game.gameObject.scene : default;
            // Stop old UI coroutines/autosaves before clearing data and loading the starter state.
            if (game) {
                if (game.Ui) game.Ui.StopSavingForReset();
                game.gameObject.SetActive(false);
            }
            ResetSavedProgress();
            if (!previousScene.IsValid() || string.IsNullOrEmpty(previousScene.path)) return;
            // Replace only the game's scene; keep editor tools or other loaded scenes alive.
            void Loaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != previousScene.path || scene.handle == previousScene.handle) return;
                SceneManager.sceneLoaded -= Loaded;
                SceneManager.SetActiveScene(scene);
                SceneManager.UnloadSceneAsync(previousScene);
            }
            SceneManager.sceneLoaded += Loaded;
            SceneManager.LoadSceneAsync(previousScene.path, LoadSceneMode.Additive);
        }
    }
}
