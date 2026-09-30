using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator PopupPortraitsUseIdleForStatsAndStillWeaponFreeSkins()
        {
            game.TogglePause();
            foreach (var entry in DoodleCharacterCatalog.Current.entries.Where(e => e.group == "Player"))
            {
                Assert.That(entry.appearancePortrait, Is.Not.Null, entry.id);
            }
            UiOpen("Stats"); yield return null;
            var idle = Object.FindObjectsByType<DoodleIdlePortrait>(FindObjectsSortMode.None).Single(p=>p.view==DoodleIdlePortrait.View.Stats);
            Assert.That(idle, Is.Not.Null);
            var initial = idle.PreviewRig.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Object.Destroy(CaptureFrame("popup-stats-idle.png", 720, 1520));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(idle.PreviewRig.animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(initial), "Stats keeps playing the authored Idle while the world is paused");
            game.Ui.ClosePage(); UiOpen("Skins"); yield return null;
            UiClick("외형 스킨", UiNode("Skin tabs")); yield return null;
            var portraits = Object.FindObjectsByType<DoodleSkinPortrait>(FindObjectsSortMode.None);
            Assert.That(portraits.Length, Is.GreaterThan(1));
            var textures = portraits.Select(p => p.mainTexture).ToArray();
            Assert.That(textures.All(t => t.name.EndsWith("_appearance")), Is.True);
            var equipped = Object.FindObjectsByType<DoodleUiSlotLayout>(FindObjectsSortMode.None)
                .Where(s => s.name.StartsWith("SkinSlot_") || s.name == "Selected skin preview");
            foreach (var slot in equipped)
            {
                Assert.That(slot.equippedLabelAtTop, Is.False);
                var label = slot.transform.Find("Equipped label") as RectTransform;
                if (label) Assert.That(label.anchoredPosition.y, Is.EqualTo(0).Within(.01f));
            }
            Object.Destroy(CaptureFrame("popup-skins-still.png", 720, 1520));
            yield return new WaitForSecondsRealtime(.3f);
            for (int i = 0; i < portraits.Length; i++) Assert.That(portraits[i].mainTexture, Is.SameAs(textures[i]));
            game.Ui.ClosePage(); UiOpen("Companions"); yield return null;
            Object.Destroy(CaptureFrame("popup-companions-right-gaze.png", 720, 1520));
        }
    }
}
