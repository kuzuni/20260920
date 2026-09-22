using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    [Serializable]
    public sealed class UiSkin
    {
        public string id, name, category, icon, description, effect, acquisition;
        public int rarity, diamondCost, requiredStage = 100;
        public float ownedBonus;
        public Color tint = Color.white;
        public bool initiallyOwned;
        [NonSerialized] public bool owned, equipped;
    }

    public sealed partial class DoodleUi
    {
        [Serializable] sealed class SkinTuning { public UiSkin[] skins; }
        [Serializable] sealed class SkinSave { public List<string> owned = new List<string>(); public string weapon, appearance; }
        readonly List<UiSkin> skinCatalog = new List<UiSkin>();
        bool skinsInitialized;
        float skinTabPosition;
        string skinCategory = "Weapon", selectedWeaponSkin, selectedAppearanceSkin;

        void InitSkins()
        {
            if (skinsInitialized) return;
            skinsInitialized = true;
            var asset = Resources.Load<TextAsset>("DoodleIdle/UI/Skins");
            SkinTuning tuning = null;
            try { if (asset) tuning = JsonUtility.FromJson<SkinTuning>(asset.text); }
            catch (ArgumentException) { Debug.LogWarning("Invalid skin catalog; using basic appearances."); }
            var ids = new HashSet<string>();
            if (tuning != null && tuning.skins != null)
                foreach (var skin in tuning.skins)
                {
                    if (skin == null || string.IsNullOrEmpty(skin.id) || !ids.Add(skin.id)) continue;
                    if (skin.category != "Weapon" && skin.category != "Appearance") continue;
                    if (skin.acquisition != "Diamond" && skin.acquisition != "MainStage" && skin.acquisition != "HighestDungeonStage") continue;
                    skin.name = string.IsNullOrEmpty(skin.name) ? skin.id : skin.name;
                    skin.icon = skin.category == "Appearance" ? "Player" : string.IsNullOrEmpty(skin.icon) ? "Club" : skin.icon;
                    skin.rarity = Mathf.Clamp(skin.rarity, 0, 4);
                    skin.diamondCost = Mathf.Max(1, skin.diamondCost);
                    skin.requiredStage = Mathf.Max(1, skin.requiredStage);
                    skin.ownedBonus = Mathf.Clamp(skin.ownedBonus, 0, 10000);
                    if (skin.tint.a <= 0) skin.tint = Color.white;
                    skin.owned = skin.initiallyOwned;
                    skin.equipped = false;
                    skinCatalog.Add(skin);
                }
            foreach (string category in new[] { "Weapon", "Appearance" })
                if (!skinCatalog.Exists(x => x.category == category && x.initiallyOwned))
                    skinCatalog.Insert(0, new UiSkin { id = "basic_" + category, name = category == "Weapon" ? "기본 몽둥이" : "먼지고양이", category = category, icon = category == "Weapon" ? "Club" : "Player", initiallyOwned = true, owned = true, acquisition = "Diamond", tint = Color.white });
            SkinSave saved = null;
            try
            {
                string json = PlayerPrefs.GetString("DoodleUi.Skins", "");
                if (!string.IsNullOrEmpty(json)) saved = JsonUtility.FromJson<SkinSave>(json);
            }
            catch (ArgumentException) { Debug.LogWarning("Invalid saved skins; keeping basic appearances."); }
            if (saved != null && saved.owned != null)
                foreach (var skin in skinCatalog) skin.owned |= saved.owned.Contains(skin.id);
            foreach (string category in new[] { "Weapon", "Appearance" })
            {
                string equippedId = saved == null ? null : category == "Weapon" ? saved.weapon : saved.appearance;
                var equipped = skinCatalog.Find(x => x.category == category && x.owned && x.id == equippedId)
                    ?? skinCatalog.Find(x => x.category == category && x.initiallyOwned);
                equipped.equipped = true;
                if (category == "Weapon") selectedWeaponSkin = equipped.id; else selectedAppearanceSkin = equipped.id;
            }
        }

        void SaveSkins()
        {
            if (!skinsInitialized) return;
            var saved = new SkinSave();
            foreach (var skin in skinCatalog)
            {
                if (skin.owned) saved.owned.Add(skin.id);
                if (!skin.equipped) continue;
                if (skin.category == "Weapon") saved.weapon = skin.id; else saved.appearance = skin.id;
            }
            PlayerPrefs.SetString("DoodleUi.Skins", JsonUtility.ToJson(saved));
        }

        public IReadOnlyList<UiSkin> Skins(string category) { InitSkins(); return skinCatalog.FindAll(x => x.category == category); }
        public bool IsSkinOwned(string id) { InitSkins(); var skin = skinCatalog.Find(x => x.id == id); return skin != null && skin.owned; }
        UiSkin EquippedSkin(string category) { InitSkins(); return skinCatalog.Find(x => x.category == category && x.equipped); }
        public string EquippedSkinId(string category) { var skin = EquippedSkin(category); return skin == null ? null : skin.id; }
        public string EquippedAppearanceIcon => EquippedSkin("Appearance")?.icon ?? "Player";
        public string EquippedWeaponIcon => EquippedSkin("Weapon")?.icon ?? "Club";
        public Color EquippedAppearanceTint => EquippedSkin("Appearance")?.tint ?? Color.white;
        public Color EquippedWeaponTint => EquippedSkin("Weapon")?.tint ?? Color.white;

        public float SkinOwnedBonus(string effect)
        {
            InitSkins();
            float total = 0;
            foreach (var skin in skinCatalog) if (skin.owned && skin.effect == effect) total += skin.ownedBonus;
            return total;
        }

        public bool TryAcquireSkin(string id)
        {
            InitSkins();
            var skin = skinCatalog.Find(x => x.id == id);
            if (skin == null || skin.owned) return false;
            bool available = skin.acquisition == "Diamond" ? Diamonds >= skin.diamondCost
                : skin.acquisition == "MainStage" ? MainStage >= skin.requiredStage
                : skin.acquisition == "HighestDungeonStage" && HighestDungeonStage >= skin.requiredStage;
            if (!available) return false;
            long before = Power;
            if (skin.acquisition == "Diamond") Diamonds -= skin.diamondCost;
            skin.owned = true;
            Save();
            NotifyPowerChanged(before, skin.name + " 획득");
            RefreshPage();
            return true;
        }

        public bool EquipSkin(string id)
        {
            InitSkins();
            var skin = skinCatalog.Find(x => x.id == id);
            if (skin == null || !skin.owned || skin.equipped) return false;
            long before = Power;
            foreach (var other in skinCatalog) if (other.category == skin.category) other.equipped = other == skin;
            Save();
            NotifyPowerChanged(before, skin.name + " 외형 장착");
            RefreshPage();
            return true;
        }

        static string SkinEffectName(string effect)
        {
            switch (effect)
            {
                case "attack": return "공격력";
                case "health": return "체력";
                case "gold": return "골드 획득량";
                case "healthRegen": return "체력 회복";
                case "critDamage": return "치명타 피해";
                default: return "추가 효과 없음";
            }
        }

        string SkinAcquisitionText(UiSkin skin)
        {
            if (skin.owned) return "보유 중 · 장착 시 외형만 변경";
            if (skin.acquisition == "Diamond") return "다이아 " + UiNumber.Format(skin.diamondCost) + "개로 구매";
            int progress = skin.acquisition == "MainStage" ? MainStage : HighestDungeonStage;
            return (skin.acquisition == "MainStage" ? "메인" : "최고 던전") + " " + UiNumber.Format(skin.requiredStage) + "단계 완료 시 해방\n진행 " + UiNumber.Format(Mathf.Min(progress, skin.requiredStage)) + "/" + UiNumber.Format(skin.requiredStage);
        }

        void BuildSkins(RectTransform body)
        {
            InitSkins();
            body.GetComponent<VerticalLayoutGroup>().spacing = 8;
            var list = skinCatalog.FindAll(x => x.category == skinCategory);
            string selectedId = skinCategory == "Weapon" ? selectedWeaponSkin : selectedAppearanceSkin;
            var selected = list.Find(x => x.id == selectedId) ?? list[0];
            var frame = UiKit.Box(body, "Selected skin", UiKit.Paper, 226);
            var specification = UiKit.Row(frame, "Skin specification", 210, 12);
            UiKit.Stretch(specification, 8, 8, 8, 8);
            var preview = SkinSlot(specification, selected, () => { }, 128f * 4 / 3);
            preview.name = "Selected skin preview";
            var previewSize = preview.GetComponent<LayoutElement>();
            previewSize.minWidth = previewSize.preferredWidth = 128; previewSize.flexibleWidth = 0;
            var info = UiKit.Column(specification, "Skin information", 3, 0);
            UiKit.Text(info, selected.name, 29, TextAnchor.MiddleLeft, 34);
            UiKit.Text(info, selected.description ?? "기본 외형입니다.", 20, TextAnchor.MiddleLeft, 40);
            string effect = selected.ownedBonus > 0 ? SkinEffectName(selected.effect) + " +" + UiNumber.Format(selected.ownedBonus) + "%" : "추가 효과 없음";
            UiKit.Text(info, "보유 효과 · " + effect, 22, TextAnchor.MiddleLeft, 30);
            UiKit.Text(info, SkinAcquisitionText(selected), 19, TextAnchor.MiddleLeft, 42);
            if (selected.owned)
            {
                var equip = UiKit.Button(info, selected.equipped ? "장착 중" : "장착", () => EquipSkin(selected.id), UiKit.Green, 48);
                equip.name = "EquipSkin_" + selected.id;
                equip.interactable = !selected.equipped;
            }
            else if (selected.acquisition == "Diamond")
            {
                var buy = UiKit.Button(info, "구매", () => TryAcquireSkin(selected.id), UiKit.Blue, 48);
                buy.name = "BuySkin_" + selected.id;
                buy.GetComponentInChildren<Text>().gameObject.SetActive(false);
                var price = UiKit.Row(buy.transform, "Skin diamond price", 38, 5);
                UiKit.Stretch(price, 5, 5, 5, 5);
                UiKit.Text(price, "구매", 24, TextAnchor.MiddleRight, 36);
                UiKit.Icon(price, "Diamond", 26);
                UiKit.Text(price, UiNumber.Format(selected.diamondCost), 24, TextAnchor.MiddleLeft, 36);
                buy.interactable = Diamonds >= selected.diamondCost;
            }
            else
            {
                bool ready = (selected.acquisition == "MainStage" ? MainStage : HighestDungeonStage) >= selected.requiredStage;
                var unlock = UiKit.Button(info, ready ? "해방" : "조건 미달", () => TryAcquireSkin(selected.id), UiKit.Yellow, 48);
                unlock.name = "UnlockSkin_" + selected.id; unlock.interactable = ready;
            }
            var totals = new List<string>();
            foreach (string key in new[] { "attack", "health", "gold", "healthRegen", "critDamage" })
                if (SkinOwnedBonus(key) > 0) totals.Add(SkinEffectName(key) + " +" + UiNumber.Format(SkinOwnedBonus(key)) + "%");
            float totalsHeight = totals.Count > 2 ? 98 : 62;
            var effects = UiKit.Box(body, "Skin total ownership", new Color(.96f, .94f, .86f), totalsHeight);
            var totalText = UiKit.Text(effects, "스킨 총 보유 효과\n" + (totals.Count == 0 ? "추가 효과 없음" : string.Join(" · ", totals)), 21, TextAnchor.MiddleCenter, totalsHeight - 2);
            UiKit.Stretch(totalText.rectTransform, 5, 1, 5, 1);
            UiKit.Text(body, skinCategory == "Weapon" ? "무기 스킨 목록" : "외형 스킨 목록", 28, TextAnchor.MiddleLeft, 36);
            var grid = UiKit.Grid(body, "Skin inventory", 4, 150);
            UiKit.PortraitGrid(grid);
            foreach (var skin in list)
            {
                var current = skin;
                SkinSlot(grid, current, () =>
                {
                    if (skinCategory == "Weapon") selectedWeaponSkin = current.id; else selectedAppearanceSkin = current.id;
                    RefreshSkinDetails(body);
                }, 150);
            }
            var tabs = UiKit.Box(UiKit.Footer(body, "Skins footer", 56), "Skin tabs", new Color(.78f,.78f,.76f), 56);
            UiKit.SlidingTabs(tabs, "무기 스킨", "외형 스킨", skinCategory == "Weapon" ? 0 : 1, skinTabPosition,
                index => { skinCategory = index == 0 ? "Weapon" : "Appearance"; RefreshSkinDetails(body); }, value => skinTabPosition = value, 56);
        }

        void RefreshSkinDetails(RectTransform previousBody)
        {
            // RefreshPage preserves the outgoing scroll position; selecting a different
            // skin should preserve the detail area's top instead of the inventory offset.
            var scroll = previousBody.GetComponentInParent<ScrollRect>();
            if (scroll)
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1;
            }
            RefreshPage();
        }

        Button SkinSlot(Transform parent, UiSkin skin, Action click, float height)
        {
            var slot = UiKit.Slot(parent, skin.name, skin.icon, skin.rarity, skin.owned ? 1 : 0, 1, skin.equipped, !skin.owned, click, height);
            slot.name = "SkinSlot_" + skin.id;
            var icon = slot.transform.Find("Icon: " + skin.icon);
            if (icon && skin.owned) icon.GetComponent<Image>().color = skin.tint;
            var state = slot.transform.Find("Quantity gauge");
            if (state) state.GetComponentInChildren<Text>().text = skin.owned ? "보유" : "미획득";
            return slot;
        }
    }
}
