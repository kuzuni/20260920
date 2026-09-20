using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        readonly Dictionary<string, string> uiSavedStrings = new Dictionary<string, string>();
        bool uiHadDiamonds;
        int uiSavedDiamonds;
        bool uiHadCameraMode;
        int uiSavedCameraMode;
        readonly List<string> uiCaptureFailures = new List<string>();
        readonly Dictionary<string, Dictionary<string, Rect>> portraitPopupGeometry = new Dictionary<string, Dictionary<string, Rect>>();
        readonly Dictionary<string, Dictionary<string, Rect>> referenceUiGeometry = new Dictionary<string, Dictionary<string, Rect>>();

        [Test]
        public void UiNumbersUseAlphabeticThousandsWithRoundingAndInvariantDecimals()
        {
            Assert.That(UiNumber.Format(0), Is.EqualTo("0"));
            Assert.That(UiNumber.Format(999), Is.EqualTo("999"));
            Assert.That(UiNumber.Format(1000), Is.EqualTo("1a"));
            Assert.That(UiNumber.Format(1250), Is.EqualTo("1.3a"));
            Assert.That(UiNumber.Format(125480), Is.EqualTo("125.5a"));
            Assert.That(UiNumber.Format(999999), Is.EqualTo("1b"));
            Assert.That(UiNumber.Format(1e6), Is.EqualTo("1b"));
            Assert.That(UiNumber.Format(1e9), Is.EqualTo("1c"));
            Assert.That(UiNumber.Format(1e12), Is.EqualTo("1d"));
            Assert.That(UiNumber.Format(-1250), Is.EqualTo("-1.3a"));
            Assert.That(UiNumber.Format(1e78), Is.EqualTo("1z"));
            Assert.That(UiNumber.Format(1e81), Is.EqualTo("1aa"));
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
                Assert.That(UiNumber.Format(1250, 2), Is.EqualTo("1.25a"));
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
        }
        static readonly string[] UiProfileKeys = {
            "DoodleUi.Gold", "DoodleUi.Collections.v1", "DoodleUi.Services.v1", "DoodleUi.Skins",
            "DoodleUi.Commerce.Armor", "DoodleUi.Commerce.Club", "DoodleUi.Commerce.Skill",
            "DoodleUi.Commerce.Companion", "DoodleUi.Commerce.Relic"
        };

        // Called by the shared fixture before loading the scene and after unloading it.
        // Only this feature's keys are isolated; unrelated game/user preferences survive.
        void BeginUiTestProfile()
        {
            uiSavedStrings.Clear();
            foreach (string key in UiProfileKeys)
            {
                if (PlayerPrefs.HasKey(key)) uiSavedStrings.Add(key, PlayerPrefs.GetString(key));
                PlayerPrefs.DeleteKey(key);
            }
            uiHadDiamonds = PlayerPrefs.HasKey("DoodleUi.Diamonds");
            uiSavedDiamonds = PlayerPrefs.GetInt("DoodleUi.Diamonds");
            PlayerPrefs.DeleteKey("DoodleUi.Diamonds");
            uiHadCameraMode=PlayerPrefs.HasKey("DoodleUi.CameraMode");uiSavedCameraMode=PlayerPrefs.GetInt("DoodleUi.CameraMode");PlayerPrefs.DeleteKey("DoodleUi.CameraMode");
        }

        void EndUiTestProfile()
        {
            foreach (string key in UiProfileKeys)
            {
                if (uiSavedStrings.TryGetValue(key, out string value)) PlayerPrefs.SetString(key, value);
                else PlayerPrefs.DeleteKey(key);
            }
            if (uiHadDiamonds) PlayerPrefs.SetInt("DoodleUi.Diamonds", uiSavedDiamonds);
            else PlayerPrefs.DeleteKey("DoodleUi.Diamonds");
            if(uiHadCameraMode)PlayerPrefs.SetInt("DoodleUi.CameraMode",uiSavedCameraMode);else PlayerPrefs.DeleteKey("DoodleUi.CameraMode");
            PlayerPrefs.Save();
        }

        Transform UiRoot => game.Ui.Canvas.transform;
        Transform UiNode(string name, Transform scope = null)
        {
            var nodes = (scope ? scope : UiRoot).GetComponentsInChildren<Transform>().Where(t => t.name == name).ToArray();
            Assert.That(nodes.Length, Is.GreaterThan(0), "Missing active UI node: " + name);
            return nodes[nodes.Length - 1];
        }
        void UiClick(string name, Transform scope = null)
        {
            var buttons = (scope ? scope : UiRoot).GetComponentsInChildren<Button>().Where(b => b.name == name).ToArray();
            Assert.That(buttons.Length, Is.GreaterThan(0), "Missing UI button: " + name);
            var button = buttons[buttons.Length - 1];
            Assert.That(button.interactable, Is.True, "Button must be usable: " + name);
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            DoodlePopupMotion.CompleteAll(UiRoot);
            Canvas.ForceUpdateCanvases();
        }
        void UiOpen(string page)
        {
            game.Ui.ClosePage();
            if (!string.IsNullOrEmpty(page)) game.Ui.ShowPage(page);
            DoodlePopupMotion.CompleteAll(UiRoot);
            Canvas.ForceUpdateCanvases();
        }
        ScrollRect UiTopScroll()
        {
            return UiRoot.GetComponentsInChildren<ScrollRect>().Last();
        }
        void UiScrollBottom()
        {
            var scroll = UiTopScroll();
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator UiNavigationTabsLoadoutsAndStatPurchasesUseLiveState()
        {
            Assert.That(game.Ui, Is.Not.Null, "The delivered entry scene must automatically install the final UI.");
            UiClick("Stats", UiNode("Bottom navigation"));
            Assert.That(game.Ui.ActivePage, Is.EqualTo("Stats"));
            Assert.That(game.Ui.BlocksGameplay, Is.True);
            Assert.That(UiNode("Stat quantity").GetComponentsInChildren<Button>().Length, Is.EqualTo(4));
            Assert.That(UiRoot.GetComponentsInChildren<Button>().Any(b => b.name == "일괄강화"), Is.False);
            int attack = game.Ui.AttackStatLevel;
            long gold = game.Ui.Gold;
            UiClick("×10");
            var upgrade = UiNode("Stat attack").GetComponentsInChildren<Button>().Single();
            ExecuteEvents.Execute(upgrade.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            Assert.That(game.Ui.AttackStatLevel, Is.EqualTo(attack + 10));
            Assert.That(game.Ui.Gold, Is.LessThan(gold));
            UiClick("Stats", UiNode("Bottom navigation"));
            Assert.That(game.Ui.ActivePage, Is.Null);
            UiOpen("Equipment");
            var inventory = UiNode("Collection inventory");
            var armor = game.Ui.Items("Armor").First(x => !x.discovered);
            game.Ui.AddItem(armor, 8);
            UiClick("Slot: " + armor.name, inventory);
            UiClick("장착", UiNode("Selected item actions"));
            Assert.That(armor.equipped, Is.True);
            Assert.That(game.Ui.Items("Armor").Count(x => x.equipped), Is.EqualTo(1));
            Assert.That(UiNode("Selected equipment").GetComponentsInChildren<Text>().Any(t => t.text.Contains(armor.name)), Is.True);
            UiScrollBottom(); UiClick("몽둥이", UiNode("Equipment tabs"));
            Assert.That(UiNode("Collection inventory").GetComponentsInChildren<Button>().Any(b => b.name == "Slot: " + game.Ui.Items("Club")[0].name), Is.True);
            foreach (var item in game.Ui.Items("Skill")) game.Ui.AddItem(item, 1);
            UiOpen("Skills"); UiScrollBottom(); UiClick("자동장착");
            Assert.That(game.Ui.EquippedSkills.Count, Is.EqualTo(8));
            Assert.That(UiNode("Equipped Skill").GetComponentsInChildren<Button>().Length, Is.EqualTo(8));
            var selected = game.Ui.EquippedSkills[0];
            UiClick("Slot: " + selected.name, UiNode("Equipped Skill"));
            Assert.That(game.Ui.HasOverlay, Is.True);
            UiClick("장착 해제", UiNode("Detail actions"));
            Assert.That(selected.equipped, Is.False);
            Assert.That(game.Ui.EquippedSkills.Count, Is.EqualTo(7));
            foreach (var item in game.Ui.Items("Companion")) game.Ui.AddItem(item, 1);
            UiOpen("Companions"); UiScrollBottom(); UiClick("자동장착");
            Assert.That(game.Ui.EquippedCompanions.Count, Is.EqualTo(5));
            Assert.That(UiNode("Equipped Companion").GetComponentsInChildren<Button>().Length, Is.EqualTo(5));
            var masks = UiNode("Eight equipped cooldowns").GetComponentsInChildren<Image>().Where(i => i.name == "Clockwise cooldown mask").ToArray();
            Assert.That(masks.Length, Is.EqualTo(8));
            Assert.That(masks.All(i => i.fillClockwise && i.fillMethod == Image.FillMethod.Radial360), Is.True);
            Assert.That(UiNode("Eight equipped cooldowns").GetComponentsInChildren<Text>().Length, Is.Zero, "No seconds text belongs inside the eight HUD status slots.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator UiSummoningChargesOnceGrantsExactCountsAndRejectsInvalidPurchases()
        {
            UiOpen("Shop");
            var ui = game.Ui;
            int initial = ui.Items("Armor").Sum(x => x.count), wallet = ui.Diamonds;
            UiClick("무료 5회\n뽑기", UiNode("Summon_Armor"));
            Assert.That(ui.Items("Armor").Sum(x => x.count), Is.EqualTo(initial + 5));
            Assert.That(ui.Diamonds, Is.EqualTo(wallet));
            Assert.That(UiNode("SummonResultCards").childCount, Is.EqualTo(5));
            Assert.That(ui.CanFreeSummon("Armor"), Is.False);
            Assert.That(ui.CanFreeSummon("Club"), Is.True);
            Assert.That(ui.TrySummon("Armor", 5, true), Is.False);
            Assert.That(ui.Items("Armor").Sum(x => x.count), Is.EqualTo(initial + 5));
            ui.CloseFullscreen();
            UiClick("10회 뽑기", UiNode("Summon_Armor"));
            Assert.That(ui.Items("Armor").Sum(x => x.count), Is.EqualTo(initial + 15));
            Assert.That(ui.Diamonds, Is.EqualTo(wallet - 100));
            Assert.That(UiNode("SummonResultCards").childCount, Is.EqualTo(10));
            UiClick("50회 뽑기", UiNode("Fullscreen: 뽑기 결과"));
            Assert.That(ui.Items("Armor").Sum(x => x.count), Is.EqualTo(initial + 65));
            Assert.That(ui.Diamonds, Is.EqualTo(wallet - 550));
            Assert.That(UiNode("SummonResultCards").childCount, Is.EqualTo(50));
            Assert.That(UiNode("SummonResultCards").GetComponentsInChildren<Text>().Any(t => t.text.Contains("+1")), Is.False);
            ui.CloseFullscreen();
            Assert.That(ui.HasOverlay, Is.False, "Repeated summons replace the result screen; they must not stack obsolete screens.");
            int before = ui.Items("Armor").Sum(x => x.count);
            ui.Diamonds = 0;
            Assert.That(ui.TrySummon("Armor", 10, false), Is.False);
            Assert.That(ui.TrySummon("Armor", 1, false), Is.False);
            Assert.That(ui.Items("Armor").Sum(x => x.count), Is.EqualTo(before));
            foreach (string category in new[] { "Armor", "Club", "Skill", "Companion", "Relic" })
            {
                Assert.That(ui.Items(category).Sum(ui.ItemProbability), Is.EqualTo(100).Within(.000001));
                for (int rarity = 0; rarity < 5; rarity++)
                    Assert.That(ui.Items(category).Where(x => x.rarity == rarity).Sum(ui.ItemProbability), Is.EqualTo(ui.GradeProbability(category, rarity)).Within(.000001));
            }
            UiClick("재화", UiNode("ShopTabs"));
            string[] products = { "DiamondSingle", "DiamondPile", "DiamondBag", "DiamondChest", "DiamondRoyalChest" };
            var productSprites = products.Select(key => UiNode("Icon: " + key).GetComponent<Image>().sprite).ToArray();
            Assert.That(productSprites.All(sprite => sprite && sprite.texture.name == "CurrencyIcons"), Is.True, "All five products must use the reference-style diamond illustrations.");
            Assert.That(productSprites.Select(sprite => sprite.rect).Distinct().Count(), Is.EqualTo(5), "Currency tiers must not repeat the same single diamond.");
            UiClick("₩1,100");
            Assert.That(ui.Diamonds, Is.Zero, "An unconnected payment button cannot mint diamonds.");
            ui.Save();
            Assert.That(PlayerPrefs.GetInt("DoodleUi.Diamonds"), Is.Zero);
            Assert.That(PlayerPrefs.GetString("DoodleUi.Commerce.Armor"), Does.Contain("freeUsedDay"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator UiScrollEventsReachLowerShopRowsAndProbabilityLists()
        {
            UiOpen("Shop");
            yield return null;
            var scroll = UiTopScroll();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height), "Five summon rows need a scrolling body.");
            float before = scroll.verticalNormalizedPosition;
            ExecuteEvents.Execute(scroll.gameObject, new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -20) }, ExecuteEvents.scrollHandler);
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(before), "A genuine scroll event must move the shop.");
            scroll.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
            var bottomRow = (RectTransform)UiNode("Summon_Relic");
            Assert.That(scroll.viewport.rect.Overlaps(UiLocalBounds(scroll.viewport, bottomRow)), Is.True, "The final relic row must be reachable.");
            UiClick("i", bottomRow);
            Assert.That(game.Ui.HasOverlay, Is.True);
            var detail = UiNode("Detail dim: 뽑기 확률");
            foreach (var item in game.Ui.Items("Relic"))
                Assert.That(detail.GetComponentsInChildren<Transform>().Any(t => t.name == "Probability_" + item.id), Is.True, "Probability disclosure must include " + item.name);
            scroll = UiTopScroll();
            yield return null;
            // The five relics now fit in the unchanged portrait viewport at every aspect ratio.
            foreach (var item in game.Ui.Items("Relic"))
            {
                var row = (RectTransform)UiNode("Probability_" + item.id);
                var bounds = UiLocalBounds(scroll.viewport, row);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 1));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(scroll.viewport.rect.yMax + 1));
            }
            UiClick("Close 뽑기 확률", detail);
            scroll = UiTopScroll(); scroll.verticalNormalizedPosition = 1;
            Canvas.ForceUpdateCanvases();
            UiClick("i", UiNode("Summon_Armor"));
            detail = UiNode("Detail dim: 뽑기 확률");
            scroll = UiTopScroll();
            yield return null;
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height), "The larger armor catalog still needs scrolling.");
            ExecuteEvents.Execute(scroll.gameObject, new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -30) }, ExecuteEvents.scrollHandler);
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(1));
            scroll.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
            var lastProbability = (RectTransform)UiNode("Probability_" + game.Ui.Items("Armor").Last().id);
            Assert.That(scroll.viewport.rect.Overlaps(UiLocalBounds(scroll.viewport, lastProbability)), Is.True, "The final armor probability must be reachable.");
            UiClick("Close 뽑기 확률", detail);
            Assert.That(game.Ui.HasOverlay, Is.False);
            Assert.That(game.Ui.ActivePage, Is.EqualTo("Shop"), "Closing a nested probability dialog must preserve the shop.");
        }

        [UnityTest]
        public IEnumerator UiRewardDimConsumesReleaseWithoutStartingJoystick()
        {
            var background = InputSystem.settings.backgroundBehavior;
            var editor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                UiOpen(null);
                game.Ui.ShowRewards("획득 보상", new List<UiReward> { new UiReward { icon = "Diamond", amount = 100, rarity = 2 } });
                var reward = UiNode("Reward dim");
                Assert.That(reward.GetComponentsInChildren<Button>().Length, Is.EqualTo(1), "The reward has only its dismissible dim, no panel confirmation or X.");
                Assert.That(reward.GetComponentsInChildren<DoodleRewardRays>().Length, Is.EqualTo(1));
                Vector2 point = new Vector2(Screen.width * .5f, Screen.height * .74f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
                yield return null; yield return null;
                Assert.That(game.JoystickActive, Is.False);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return null;
                Assert.That(game.Ui.HasOverlay, Is.False, "A pointer release on the dim must dismiss the reward.");
                Assert.That(game.Ui.BlocksGameplay, Is.True, "The dismissing gesture remains consumed after the panel disappears.");
                Assert.That(game.JoystickActive, Is.False);
                yield return null; yield return null; yield return null;
                UiOpen("Shop");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
                yield return null; yield return null;
                Assert.That(game.JoystickActive, Is.False, "An open shop must block gameplay dragging.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point + Vector2.right * 100 });
                yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editor;
            }
        }

        [UnityTest]
        public IEnumerator UiExistingEntrySceneBootstrapsWithoutInspectorSetup()
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(originalScene);
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(testScene);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SampleScene.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            testScene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
#else
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("SampleScene", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            testScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("SampleScene");
#endif
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(testScene);
            yield return null;
            game = testScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DoodleIdleGame>()).Single();
            Assert.That(game.Ready, Is.True);
            Assert.That(game.Ui, Is.Not.Null);
            Assert.That(game.Ui.Canvas.isActiveAndEnabled, Is.True);
            Assert.That(testScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>()).Count(), Is.EqualTo(1), "The existing entry EventSystem must be reused.");
            UiOpen("Equipment");
            Assert.That(UiNode("Equipment tabs"), Is.Not.Null);
            var frame = CaptureFrame("ui-entry-SampleScene.png", 720, 1520);
            Object.Destroy(frame);
        }

        [UnityTest]
        public IEnumerator UiFinalStatesPreservePortraitPopupsAtAllFiveSupportedRatios()
        {
            uiCaptureFailures.Clear();
            portraitPopupGeometry.Clear();
            referenceUiGeometry.Clear();
            game.TogglePause();
            game.Ui.Diamonds = 100000;
            var sizes = new[] { new Vector2Int(720, 1520), new Vector2Int(720, 1280), new Vector2Int(900, 900), new Vector2Int(1440, 900), new Vector2Int(1520, 720) };
            foreach (var size in sizes)
            {
                UiOpen(null); UiCapture("01-main", size);
                UiOpen("Stats"); UiCapture("02-stats", size);
                UiOpen("Equipment"); UiClick("갑옷", UiNode("Equipment tabs")); UiCapture("03-armor", size);
                UiScrollBottom(); UiClick("몽둥이", UiNode("Equipment tabs")); UiCapture("04-club", size);
                UiOpen("Skills"); UiCapture("05-skills", size);
                UiOpen("Dungeons"); UiCapture("06-dungeons", size);
                UiOpen("Pvp"); UiCapture("07-pvp", size); UiCapture("07b-pvp-bottom", size, true);
                UiOpen("Shop"); UiClick("뽑기", UiNode("ShopTabs")); UiCapture("08-shop-top", size);
                UiClick("재화", UiNode("ShopTabs")); UiCapture("09-currency", size);
                UiClick("뽑기", UiNode("ShopTabs")); UiClick("10회 뽑기", UiNode("Summon_Club")); UiCapture("10-summon-results", size);
                UiOpen("Attendance"); UiCapture("11-attendance", size);
                UiOpen("Roulette"); UiCapture("12-roulette", size);
                UiOpen("Buffs"); UiCapture("13-buffs", size);
                UiOpen("Quests"); UiClick("일일"); UiCapture("14-daily-quests", size);
                UiClick("반복"); UiCapture("15-repeat-quests", size);
                UiClick("주간"); UiCapture("16-weekly-quests", size);
                UiOpen("Chat"); UiCapture("17-fullscreen-chat", size);
                UiOpen("Settings"); UiCapture("18-settings", size);
                UiOpen("Companions"); UiCapture("19-companions", size);
                UiOpen("Relics"); UiCapture("20-relics", size);
                UiOpen("Skills"); var skill = game.Ui.Items("Skill").First(x => x.discovered);
                UiClick("Slot: " + skill.name, UiNode("Collection inventory")); UiCapture("21-skill-detail", size);
                UiOpen("Shop"); UiClick("뽑기", UiNode("ShopTabs")); UiCapture("22-shop-bottom", size, true);
                UiClick("i", UiNode("Summon_Armor")); UiCapture("23-probabilities", size); UiCapture("23b-probabilities-bottom", size, true);
                UiOpen(null); game.Ui.ShowRewards("던전 클리어!", new List<UiReward> {
                    new UiReward { name = "골드", icon = "Gold", amount = 30000, rarity = 0 }
                }); UiCapture("24-dungeon-clear", size);
                UiOpen(null); game.Ui.ShowRewards("획득 보상", new List<UiReward> { new UiReward { icon = "Diamond", amount = 100, rarity = 0 } });
                UiCapture("25-rewards", size);
                UiOpen(null); game.Ui.ShowRewards("획득 보상", new List<UiReward> {
                    new UiReward { icon="Gold", amount=1000, rarity=0 },
                    new UiReward { icon="Diamond", amount=500, rarity=2 }
                }); UiCapture("25b-rewards-multiple",size);
                UiOpen("Skins"); UiClick("무기 스킨", UiNode("Skin tabs"));
                UiClick("SkinSlot_weapon_crystal"); UiCapture("26-skin-weapon-locked", size);
                UiClick("외형 스킨", UiNode("Skin tabs")); UiClick("SkinSlot_appearance_peach");
                UiCapture("27-skin-appearance-locked", size);
                if (!game.Ui.IsSkinOwned("weapon_vine")) Assert.That(game.Ui.TryAcquireSkin("weapon_vine"), Is.True);
                UiClick("무기 스킨", UiNode("Skin tabs")); UiClick("SkinSlot_weapon_vine");
                UiCapture("28-skin-owned", size);
                game.Ui.Gold=100000000;
                if(game.Ui.AttackStatLevel<15) Assert.That(game.Ui.UpgradeStat("attack",15-game.Ui.AttackStatLevel),Is.True);
                UiOpen(null); game.Ui.RefreshHud();
                yield return new WaitForSecondsRealtime(.2f); // Settle the actual Selectable enabled-color transition.
                UiCapture("29-mission-ready",size);
                game.Ui.CycleCameraMode();UiCapture("30-camera-mode-2",size);
                game.Ui.CycleCameraMode();UiCapture("31-camera-mode-3",size);
                game.Ui.CycleCameraMode();
                yield return null;
            }
            Assert.That(uiCaptureFailures, Is.Empty, string.Join("\n", uiCaptureFailures));
        }

        void UiCapture(string state, Vector2Int size, bool bottom = false)
        {
            game.Ui.Toast(""); UiNode("Power change toast").GetComponent<Text>().text="";
            string file = "ui-" + size.x + "x" + size.y + "-" + state + ".png";
            var rewardCards = new List<RectInt>();
            var rewardRays = new List<RectInt>();
            RectInt? popupPixels = null;
            var frame = CaptureFrame(file, size.x, size.y, true, () =>
            {
                if (bottom)
                {
                    // Reach the nested ranking viewport on short screens before scrolling its rows.
                    if (state == "07b-pvp-bottom")
                    {
                        var outer = UiRoot.GetComponentsInChildren<ScrollRect>().First();
                        outer.StopMovement(); outer.verticalNormalizedPosition = 0;
                        Canvas.ForceUpdateCanvases();
                    }
                    UiScrollBottom();
                }
                else if (UiRoot.GetComponentsInChildren<ScrollRect>().Length > 0)
                {
                    foreach (var scroll in UiRoot.GetComponentsInChildren<ScrollRect>())
                    {
                        scroll.StopMovement(); scroll.verticalNormalizedPosition = 1;
                    }
                    Canvas.ForceUpdateCanvases();
                }
                // Preserve every requested diagnostic image even when a layout assertion fails.
                // The test still fails after the complete five-ratio evidence set is exported.
                try
                {
                    if(state=="29-mission-ready")
                    {
                        Assert.That(game.Ui.CanClaimMainMission,Is.True);
                        Assert.That(UiNode("Claim main mission").GetComponent<Button>().interactable,Is.True);
                        Assert.That(UiNode("Mission").GetComponentsInChildren<Text>().Any(t=>t.text==game.Ui.MainMissionText),Is.True, file+" must show the refreshed mission objective");
                    }
                    if (state == "04-club")
                    {
                        UiClick("갑옷", UiNode("Equipment tabs"));
                        UiClick("몽둥이", UiNode("Equipment tabs"));
                        var selected = (RectTransform)UiNode("Selected equipment");
                        var viewport = selected.GetComponentInParent<ScrollRect>().viewport;
                        var bounds = UiLocalBounds(viewport, selected);
                        Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(viewport.rect.yMin - 2), file + " equipment spec clipped after tab switch");
                        Assert.That(bounds.yMax, Is.LessThanOrEqualTo(viewport.rect.yMax + 2), file + " equipment spec scrolled away after tab switch");
                    }
                    AssertUiGeometry(file);
                    AssertHeightMatchedUiGeometry(state, file, size);
                    AssertPortraitPopupGeometry(state, file, size.x == 720 && size.y == 1520);
                    if (size.x == 720 && size.y == 1520) AssertReferenceProportions(state, file);
                    if (size.y == 900 && (state == "03-armor" || state == "04-club" || state == "05-skills" || state == "19-companions"))
                    {
                        var inventory = UiNode("Collection inventory");
                        var card = (RectTransform)inventory.GetChild(0);
                        var outer = inventory.GetComponentsInParent<ScrollRect>().Last().viewport;
                        var bounds = UiLocalBounds(outer, card);
                        float visible = Mathf.Min(bounds.yMax, outer.rect.yMax) - Mathf.Max(bounds.yMin, outer.rect.yMin);
                        Assert.That(visible, Is.GreaterThanOrEqualTo(64), file + " first inventory row must be visible on entry");
                        var quantity = card.GetComponent<DoodleUiSlotLayout>().gauge;
                        foreach (var mask in quantity.GetComponentsInParent<RectMask2D>())
                        {
                            var viewport = (RectTransform)mask.transform;
                            var gaugeBounds = UiLocalBounds(viewport, quantity);
                            Assert.That(gaugeBounds.yMin, Is.GreaterThanOrEqualTo(viewport.rect.yMin - 2), file + " first row quantity must be visible without scrolling");
                            Assert.That(gaugeBounds.yMax, Is.LessThanOrEqualTo(viewport.rect.yMax + 2), file + " first row quantity clipped at top");
                        }
                    }
                    if (size.y == 900 && state == "23-probabilities")
                    {
                        var card = UiRoot.GetComponentsInChildren<RectTransform>().First(t => t.name.StartsWith("Probability_"));
                        var outer = card.GetComponentInParent<ScrollRect>().viewport;
                        var bounds = UiLocalBounds(outer, card);
                        float visible = Mathf.Min(bounds.yMax, outer.rect.yMax) - Mathf.Max(bounds.yMin, outer.rect.yMin);
                        Assert.That(visible, Is.GreaterThanOrEqualTo(32), file + " an individual probability must be visible on entry");
                    }
                    if (state == "07b-pvp-bottom")
                    {
                        var lastRank = (RectTransform)UiNode("Rank 100");
                        foreach (var mask in lastRank.GetComponentsInParent<RectMask2D>())
                        {
                            var viewport = (RectTransform)mask.transform;
                            var bounds = UiLocalBounds(viewport, lastRank);
                            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(viewport.rect.yMin - 3), file + " rank 100 below outer viewport");
                            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(viewport.rect.yMax + 3), file + " rank 100 above outer viewport");
                        }
                        foreach (var mask in lastRank.GetComponentsInParent<Mask>())
                        {
                            var viewport = (RectTransform)mask.transform;
                            var bounds = UiLocalBounds(viewport, lastRank);
                            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(viewport.rect.yMin - 3), file + " rank 100 below ranking viewport");
                            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(viewport.rect.yMax + 3), file + " rank 100 above ranking viewport");
                        }
                    }
                }
                catch (AssertionException error) { uiCaptureFailures.Add(error.Message); }
                foreach (var card in UiRoot.GetComponentsInChildren<RectTransform>().Where(t => t.name == "Reward frame"))
                    rewardCards.Add(UiPixelBounds(card, size));
                foreach (var rays in UiRoot.GetComponentsInChildren<DoodleRewardRays>())
                    rewardRays.Add(UiPixelBounds(rays.rectTransform, size));
                var topWindow = UiRoot.GetComponentsInChildren<DoodleUiWindow>().LastOrDefault();
                if (topWindow && !topWindow.full) popupPixels = UiPixelBounds((RectTransform)topWindow.transform, size);
            });
            var pixels = frame.GetPixels32();
            if (state == "24-dungeon-clear" || state == "25-rewards" || state == "25b-rewards-multiple")
            {
                // Each cave now grants one configured currency. Keep separate multi-card
                // coverage so the generic reward overlay still verifies every card and ray.
                int expected = state == "25b-rewards-multiple" ? 2 : 1;
                if (rewardCards.Count != expected || rewardRays.Count != expected)
                    uiCaptureFailures.Add(file + " must render each individual reward card and its rays.");
                for (int i = 0; i < rewardCards.Count; i++)
                {
                    var card = rewardCards[i]; int light = 0, ink = 0;
                    for (int y = card.yMin; y < card.yMax; y++) for (int x = card.xMin; x < card.xMax; x++)
                    {
                        var p = pixels[y * size.x + x];
                        if (p.r > 150 && p.g > 150 && p.b > 100) light++;
                        if (p.r < 100 && p.g < 100 && p.b < 100) ink++;
                    }
                    if (light < card.width * card.height * .4f || ink < card.width * card.height * .01f)
                        uiCaptureFailures.Add(file + " reward card " + i + " is missing its grade frame, art or amount.");
                    if (i >= rewardRays.Count) continue;
                    var rays = rewardRays[i]; int yellow = 0;
                    for (int y = rays.yMin; y < rays.yMax; y++) for (int x = rays.xMin; x < rays.xMax; x++)
                    {
                        if (card.Contains(new Vector2Int(x, y))) continue;
                        var p = pixels[y * size.x + x];
                        if (p.r > 190 && p.g > 145 && p.b < 120) yellow++;
                    }
                    if (yellow < rays.width * rays.height / 200)
                        uiCaptureFailures.Add(file + " reward card " + i + " has no visible yellow rays outside the frame.");
                }
            }
            else
            {
                if (popupPixels.HasValue)
                {
                    // A uniformly fitted detail popup deliberately occupies less screen area.
                    // Inspect its actual pixels rather than requiring it to fill the landscape screen.
                    var region = popupPixels.Value;
                    int cream = 0, ink = 0;
                    for (int y = region.yMin; y < region.yMax; y++) for (int x = region.xMin; x < region.xMax; x++)
                    {
                        var p = pixels[y * size.x + x];
                        if (p.r > 190 && p.g > 180 && p.b > 150) cream++;
                        if (p.r < 100 && p.g < 100 && p.b < 100) ink++;
                    }
                    if (cream <= region.width * region.height * .2f || ink <= region.width * region.height * .002f)
                        uiCaptureFailures.Add(file + " must render the popup frame and foreground inside its fitted bounds.");
                }
                else if (pixels.Count(p => p.r > 190 && p.g > 180 && p.b > 150) <= size.x * size.y / 30)
                    uiCaptureFailures.Add(file + " must contain rendered cream panels and visible artwork.");
            }
            Object.Destroy(frame);
        }

        RectInt UiPixelBounds(RectTransform item, Vector2Int size)
        {
            var canvas = (RectTransform)UiRoot; var bounds = UiLocalBounds(canvas, item); var area = canvas.rect;
            if (bounds.width <= 1 || bounds.height <= 1 || bounds.xMin < area.xMin - 1 || bounds.xMax > area.xMax + 1 || bounds.yMin < area.yMin - 1 || bounds.yMax > area.yMax + 1)
                uiCaptureFailures.Add(item.name + " pixel region must be nonempty and fully inside the rendered canvas.");
            int left = Mathf.Clamp(Mathf.FloorToInt((bounds.xMin - area.xMin) / area.width * size.x), 0, size.x);
            int bottom = Mathf.Clamp(Mathf.FloorToInt((bounds.yMin - area.yMin) / area.height * size.y), 0, size.y);
            int right = Mathf.Clamp(Mathf.CeilToInt((bounds.xMax - area.xMin) / area.width * size.x), 0, size.x);
            int top = Mathf.Clamp(Mathf.CeilToInt((bounds.yMax - area.yMin) / area.height * size.y), 0, size.y);
            return new RectInt(left, bottom, right - left, top - bottom);
        }

        void AssertHeightMatchedUiGeometry(string state, string context, Vector2Int size)
        {
            var canvas = game.Ui.Canvas;
            var scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null, context + " missing CanvasScaler");
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize), context);
            Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight), context);
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(720, 1520)), context);
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(1).Within(.0001f), context + " must match Height");
            Assert.That(canvas.scaleFactor, Is.EqualTo(size.y / 1520f).Within(.0001f), context + " capture must use the production scale");
            var safe = game.Ui.SafeRoot;
            Assert.That(safe.rect.width, Is.EqualTo(720).Within(.1f), context + " reference UI width");
            Assert.That(safe.rect.height, Is.EqualTo(1520).Within(.1f), context + " reference UI height");
            Assert.That(UiLocalBounds((RectTransform)UiRoot, safe).center.x, Is.EqualTo(0).Within(.1f), context + " reference UI must stay centered");

            var geometry = new Dictionary<string, Rect>();
            foreach (string name in new[] { "Profile and currencies", "Timed buffs", "Activities", "Mission", "Camera mode", "Stage progress", "Eight equipped cooldowns", "Bottom navigation" })
            {
                // Navigation is intentionally hidden in chat, but its reference layout still exists.
                var root = UiRoot.GetComponentsInChildren<RectTransform>(true).Single(node => node.name == name);
                CaptureReferenceTree(safe, root, "HUD/" + name, geometry);
            }
            foreach (var window in UiRoot.GetComponentsInChildren<DoodleUiWindow>().Where(window => window.full))
            {
                // Fullscreen backgrounds may widen. Their actual UI contents retain the reference width and layout.
                CaptureReferenceTree(safe, window.inner, "Fullscreen/" + window.name, geometry);
            }
            bool reference = size == new Vector2Int(720, 1520);
            if (reference) referenceUiGeometry[state] = geometry;
            else
            {
                Assert.That(referenceUiGeometry.ContainsKey(state), Is.True, context + " missing reference UI baseline");
                var baseline = referenceUiGeometry[state];
                CollectionAssert.AreEquivalent(baseline.Keys, geometry.Keys, context + " reference UI hierarchy changed");
                foreach (var pair in geometry)
                {
                    var expected = baseline[pair.Key];
                    Assert.That(Vector2.Distance(pair.Value.position, expected.position), Is.LessThan(.6f), context + " reference placement changed: " + pair.Key);
                    Assert.That(Vector2.Distance(pair.Value.size, expected.size), Is.LessThan(.6f), context + " reference size changed: " + pair.Key);
                }
            }
        }

        static void CaptureReferenceTree(RectTransform origin, RectTransform node, string path, Dictionary<string, Rect> geometry)
        {
            // Progress and randomly summoned items change between resolution passes.
            // Keep frames, icons and labels; omit data-dependent fill lengths and ownership decorations.
            if (node.name == "Fill" || node.name == "Scroll thumb" || node.name == "Equipped check" || node.name == "Locked padlock") return;
            geometry[path] = UiLocalBounds(origin, node);
            // These centered text/icon groups fit their current Lv/XP/price strings. The
            // values and font advance rounding can change across captures; their row bounds
            // and icon dimensions, rather than text-dependent offsets, must remain fixed.
            if (node.name == "Summon progress summary" || node.name == "DiamondCost")
            {
                int index = 0;
                foreach (var icon in node.GetComponentsInChildren<Image>().Where(image => image.name.StartsWith("Icon: ")))
                    geometry[path + "/icon/" + index++] = new Rect(Vector2.zero, icon.rectTransform.rect.size);
                return;
            }
            for (int i = 0; i < node.childCount; i++)
                if (node.GetChild(i) is RectTransform child)
                    CaptureReferenceTree(origin, child, path + "/" + i, geometry);
        }

        void AssertPortraitPopupGeometry(string state, string context, bool reference)
        {
            foreach (var window in UiRoot.GetComponentsInChildren<DoodleUiWindow>().Where(w => !w.full))
            {
                var panel = (RectTransform)window.transform;
                string key = state + "/" + panel.name;
                var geometry = new Dictionary<string, Rect> { { "panel", panel.rect } };
                foreach (var icon in panel.GetComponentsInChildren<Image>().Where(i => i.name.StartsWith("Icon: ")))
                {
                    string path = icon.name;
                    for (var parent = icon.transform.parent; parent != panel; parent = parent.parent) path = parent.name + "/" + path;
                    geometry[path] = UiLocalBounds(panel, icon.rectTransform);
                }
                geometry["viewport"] = UiLocalBounds(panel, window.viewport);
                geometry["title"] = UiLocalBounds(panel, window.titleText.rectTransform);
                geometry["close"] = UiLocalBounds(panel, window.closeButton);
                if (window.footer) geometry["footer"] = UiLocalBounds(panel, window.footer);
                if (reference) portraitPopupGeometry[key] = geometry;
                else
                {
                    Assert.That(portraitPopupGeometry.ContainsKey(key), Is.True, context + " missing 9:19 baseline");
                    var baseline = portraitPopupGeometry[key];
                    foreach (var pair in geometry.Where(p => baseline.ContainsKey(p.Key)))
                    {
                        var expected = baseline[pair.Key]; var actual = pair.Value;
                        Assert.That(actual.x, Is.EqualTo(expected.x).Within(.6f), context + " horizontal placement: " + pair.Key);
                        Assert.That(actual.y, Is.EqualTo(expected.y).Within(.6f), context + " vertical placement: " + pair.Key);
                        Assert.That(actual.width, Is.EqualTo(expected.width).Within(.6f), context + " width changed from 9:19: " + pair.Key);
                        Assert.That(actual.height, Is.EqualTo(expected.height).Within(.6f), context + " height changed from 9:19: " + pair.Key);
                    }
                    var screenBounds = UiLocalBounds((RectTransform)UiRoot, panel);
                    Assert.That(screenBounds.width / screenBounds.height, Is.EqualTo(baseline["panel"].width / baseline["panel"].height).Within(.001f), context + " stretched popup");
                }
                Assert.That(panel.lossyScale.x, Is.EqualTo(panel.lossyScale.y).Within(.001f), context + " nonuniform icon scaling");
                var bounds = UiLocalBounds(game.Ui.SafeRoot, panel);
                var safeBounds = game.Ui.SafeRoot.rect;
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(safeBounds.xMin), context + " popup outside safe left");
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(safeBounds.xMax), context + " popup outside safe right");
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(safeBounds.yMax - 107), context + " popup overlaps header");
                var navBounds = UiLocalBounds(game.Ui.SafeRoot, (RectTransform)UiNode("Bottom navigation"));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(navBounds.yMax), context + " popup overlaps navigation");
            }
            foreach (var rewards in UiRoot.GetComponentsInChildren<DoodleUiRewardLayout>())
            {
                var area = (RectTransform)rewards.transform;
                string key = state + "/floating rewards";
                var geometry = new Dictionary<string, Rect>();
                var nodes = area.GetComponentsInChildren<RectTransform>();
                for (int i = 0; i < nodes.Length; i++) geometry[i.ToString()] = UiLocalBounds(area, nodes[i]);
                if (reference) portraitPopupGeometry[key] = geometry;
                else
                {
                    Assert.That(portraitPopupGeometry.ContainsKey(key), Is.True, context + " missing reward baseline");
                    var baseline = portraitPopupGeometry[key];
                    Assert.That(geometry.Count, Is.EqualTo(baseline.Count), context + " reward hierarchy changed");
                    foreach (var pair in geometry)
                    {
                        var expected = baseline[pair.Key]; var actual = pair.Value;
                        Assert.That(Vector2.Distance(actual.position, expected.position), Is.LessThan(.6f), context + " reward placement changed");
                        Assert.That(Vector2.Distance(actual.size, expected.size), Is.LessThan(.6f), context + " reward size changed");
                    }
                }
                Assert.That(area.lossyScale.x, Is.EqualTo(area.lossyScale.y).Within(.001f), context + " stretched reward");
            }
        }

        void AssertReferenceProportions(string state, string context)
        {
            // Broad measured bands from FinalDesign, independent of runtime layout constants.
            // These guard against returning to tiny navigation, generic oversized panels and empty result pages.
            var canvas = (RectTransform)UiRoot;
            if (state == "01-main")
            {
                var nav = (RectTransform)UiNode("Bottom navigation");
                var bounds = UiLocalBounds(canvas, nav);
                Assert.That(bounds.height, Is.InRange(138f, 158f), context + " reference navigation height");
                var dock = (RectTransform)UiNode("Eight equipped cooldowns");
                Assert.That(dock.rect.height, Is.InRange(78f, 90f), context + " reference cooldown diameter");
                var mission = (RectTransform)UiNode("Mission");
                Assert.That(mission.rect.height, Is.InRange(174f, 194f), context + " mission card must also fit its explicit reward button");
            }
            if (state == "02-stats" || state == "04-club" || state == "05-skills" || state == "06-dungeons")
            {
                var window = UiRoot.GetComponentsInChildren<DoodleUiWindow>().Last();
                var bounds = UiLocalBounds(canvas, (RectTransform)window.transform);
                Assert.That(bounds.width / canvas.rect.width, Is.InRange(.75f, .83f), context + " reference panel width");
                bool fiveStatOrSkill=state=="02-stats"||state=="05-skills";
                Assert.That(bounds.height, Is.InRange(fiveStatOrSkill ? 970f : 790f, fiveStatOrSkill ? 1040f : 900f), context + " reference panel height with five growth stats");
                float center = (canvas.rect.yMax - bounds.center.y) / canvas.rect.height;
                Assert.That(center, Is.InRange(.41f, .54f), context + " reference panel placement");
            }
        }

        void AssertUiGeometry(string context)
        {
            var canvasRect = (RectTransform)UiRoot;
            var screen = canvasRect.rect;
            var cameraBounds=UiLocalBounds(canvasRect,(RectTransform)UiNode("Camera mode"));
            var stageBounds=UiLocalBounds(canvasRect,(RectTransform)UiNode("Stage progress"));
            foreach(var activity in UiNode("Activities").GetComponentsInChildren<Button>())
            {
                var bounds=UiLocalBounds(canvasRect,(RectTransform)activity.transform);
                Assert.That(bounds.Overlaps(cameraBounds),Is.False,context+" activity overlaps camera: "+activity.name);
                Assert.That(bounds.Overlaps(stageBounds),Is.False,context+" activity overlaps stage: "+activity.name);
            }
            foreach (var slot in UiRoot.GetComponentsInChildren<DoodleUiSlotLayout>())
            {
                var rect = ((RectTransform)slot.transform).rect;
                Assert.That(rect.width / rect.height, Is.EqualTo(.75f).Within(.005f), context + " slot must retain 3:4 aspect: " + slot.name);
            }
            foreach (var cost in UiRoot.GetComponentsInChildren<RectTransform>().Where(t => t.name == "DiamondCost"))
            {
                var button = cost.GetComponentInParent<Button>();
                Assert.That(button, Is.Not.Null, context + " diamond cost must be inside its summon button");
                var buttonRect = (RectTransform)button.transform;
                foreach (var part in cost.GetComponentsInChildren<RectTransform>())
                {
                    var bounds = UiLocalBounds(buttonRect, part);
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(buttonRect.rect.xMin - 1), context + " summon price left edge");
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(buttonRect.rect.xMax + 1), context + " summon price right edge");
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(buttonRect.rect.yMin - 1), context + " summon price bottom edge");
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(buttonRect.rect.yMax + 1), context + " summon price top edge");
                }
            }
            foreach (var panel in UiRoot.GetComponentsInChildren<RectTransform>().Where(t => t.name.StartsWith("Panel: ") || t.name == "Bottom navigation" || t.name == "Profile and currencies"))
            {
                var bounds = UiLocalBounds(canvasRect, panel);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(screen.xMin - 3), context + " left clipping: " + panel.name);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(screen.xMax + 3), context + " right clipping: " + panel.name);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(screen.yMin - 3), context + " bottom clipping: " + panel.name);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(screen.yMax + 3), context + " top clipping: " + panel.name);
            }
            foreach (var scroll in UiRoot.GetComponentsInChildren<ScrollRect>())
            {
                Assert.That(scroll.content.rect.width, Is.LessThanOrEqualTo(scroll.viewport.rect.width + 2), context + " scroll content width");
                Assert.That(scroll.viewport.rect.height, Is.GreaterThan(60), context + " needs a usable scrolling viewport");
            }
            foreach (var wheel in UiRoot.GetComponentsInChildren<DoodleRouletteGraphic>())
            {
                var viewport = wheel.GetComponentInParent<ScrollRect>().viewport;
                var bounds = UiLocalBounds(viewport, wheel.rectTransform);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(viewport.rect.yMin - 2), context + " roulette bottom clipping");
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(viewport.rect.yMax + 2), context + " roulette top clipping");
                Assert.That(Mathf.Abs(bounds.width - bounds.height), Is.LessThan(2), context + " roulette must remain circular");
            }
            foreach (var footer in UiRoot.GetComponentsInChildren<RectTransform>().Where(t => t.name.EndsWith(" footer")))
            {
                var panel = footer.GetComponentInParent<DoodleUiWindow>();
                var panelRect = (RectTransform)panel.transform;
                Assert.That(footer.GetComponentsInParent<ScrollRect>(), Is.Empty, context + " collection actions must remain outside scrolling content");
                foreach (var button in footer.GetComponentsInChildren<Button>())
                {
                    var bounds = UiLocalBounds(panelRect, (RectTransform)button.transform);
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(panelRect.rect.xMin), context + " clipped footer button: " + button.name);
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(panelRect.rect.xMax), context + " clipped footer button: " + button.name);
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(panelRect.rect.yMin), context + " clipped footer button: " + button.name);
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(panelRect.rect.yMax), context + " clipped footer button: " + button.name);
                }
            }
            var allText = UiRoot.GetComponentsInChildren<Text>();
            foreach (var label in allText)
            {
                if (string.IsNullOrWhiteSpace(label.text)) continue;
                var clipping = label.GetComponentsInParent<RectMask2D>();
                bool visible = true;
                foreach (var mask in clipping)
                {
                    var maskRect = (RectTransform)mask.transform;
                    if (!maskRect.rect.Overlaps(UiLocalBounds(maskRect, label.rectTransform))) { visible = false; break; }
                }
                if (!visible) continue; // Offscreen list entries are deliberately masked and scrollable.
                Assert.That(label.font, Is.Not.Null, context + " missing font: " + label.text);
                Assert.That(label.rectTransform.rect.width, Is.GreaterThan(1), context + " collapsed text: " + label.text);
                Assert.That(label.rectTransform.rect.height, Is.GreaterThan(1), context + " collapsed text: " + label.text);
                label.font.RequestCharactersInTexture(label.text, label.fontSize, label.fontStyle);
                foreach (char character in label.text.Where(c => c >= '\uac00' && c <= '\ud7a3').Distinct())
                    Assert.That(label.font.HasCharacter(character), Is.True, context + " Korean glyph missing: " + character);
                label.cachedTextGenerator.Populate(label.text, label.GetGenerationSettings(label.rectTransform.rect.size));
                Assert.That(label.cachedTextGenerator.characterCountVisible, Is.GreaterThan(0), context + " text is entirely truncated: " + label.text);
            }
            foreach (var row in UiRoot.GetComponentsInChildren<HorizontalLayoutGroup>())
            {
                var children = Enumerable.Range(0, row.transform.childCount).Select(i => row.transform.GetChild(i)).OfType<RectTransform>()
                    .Where(t => t.gameObject.activeInHierarchy && !(t.GetComponent<LayoutElement>() && t.GetComponent<LayoutElement>().ignoreLayout)).ToArray();
                for (int i = 1; i < children.Length; i++)
                {
                    var previous = UiLocalBounds((RectTransform)row.transform, children[i - 1]);
                    var current = UiLocalBounds((RectTransform)row.transform, children[i]);
                    Assert.That(current.xMin, Is.GreaterThanOrEqualTo(previous.xMax - 2), context + " overlapping row children in " + row.name);
                }
            }
        }

        static Rect UiLocalBounds(RectTransform relativeTo, RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var a = relativeTo.InverseTransformPoint(corners[0]); var b = relativeTo.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
    }
}
