using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator ArtCachesRecoverDestroyedSpritesAndResetForFastPlayMode()
        {
            game.TogglePause();
            // Fast enter-play-mode keeps managed dictionaries after native sprites are destroyed.
            for (int round = 0; round < 2; round++) {
                var slot = UiKit.Art("SkillThumb_21");
                var companion = DoodleCollectionArt.CompanionFrame(12, 1);
                var variant = DoodleVariantArt.Get("Eggplant");
                var expansion = DoodleExpansionArt.Get("SkillGolem", 3);
                var plus = UiKit.Art("AddSlot");
                Object.Destroy(slot); Object.Destroy(companion); Object.Destroy(variant); Object.Destroy(expansion); Object.Destroy(plus);
                yield return null;
                foreach (var sprite in new[] { UiKit.Art("SkillThumb_21"), DoodleCollectionArt.CompanionFrame(12, 1), DoodleVariantArt.Get("Eggplant"), DoodleExpansionArt.Get("SkillGolem", 3), UiKit.Art("AddSlot") }) {
                    Assert.That(sprite, Is.Not.Null); Assert.That((bool)sprite, Is.True); Assert.That((bool)sprite.texture, Is.True);
                }
                foreach (var type in new[] { typeof(UiKit), typeof(DoodleCollectionArt), typeof(DoodleVariantArt), typeof(DoodleExpansionArt) }) {
                    var reset = type.GetMethod("ResetCache", BindingFlags.Static | BindingFlags.NonPublic);
                    var hook = reset.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                    Assert.That(hook, Is.Not.Null);
                    Assert.That(hook.loadType, Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration));
                    reset.Invoke(null, null);
                }
                // Initialize restores this font before building the first widgets.
                UiKit.Font = Resources.Load<Font>("DoodleIdle/UI/DisplayFont");
                foreach (var item in game.Ui.Items("Skill")) Assert.That((bool)UiKit.Art(item.icon), Is.True);
                foreach (var item in game.Ui.Items("Companion")) {
                    Assert.That((bool)UiKit.Art(item.icon), Is.True);
                    Assert.That((bool)UiKit.Art(item.projectile), Is.True);
                }
                Assert.That((bool)UiKit.Frame && (bool)UiKit.Circle, Is.True);
                UiOpen("Skills");
                Object.Destroy(CaptureFrame("fast-play-art-restored-" + round + ".png", 720, 1520));
                game.Ui.ClosePage();
            }
        }
    }
}
