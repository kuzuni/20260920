using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    // A shared corner marker evaluates live state without rebuilding the whole UI.
    public sealed class DoodleNotificationBadge : MonoBehaviour
    {
        Func<bool> available;
        Image marker;
        float next;
        public bool Visible => marker && marker.enabled;
        public static void Bind(Transform target, Func<bool> condition)
        {
            var badge=target.GetComponent<DoodleNotificationBadge>() ?? target.gameObject.AddComponent<DoodleNotificationBadge>();
            badge.available=condition;
            if(!badge.marker) {
                var rect=UiKit.Rect(target,"Red notification dot");
                rect.anchorMin=rect.anchorMax=Vector2.one;rect.anchoredPosition=new Vector2(-7,-7);rect.sizeDelta=Vector2.one*18;
                rect.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
                badge.marker=rect.gameObject.AddComponent<Image>();badge.marker.sprite=UiKit.NotificationDot;
                badge.marker.color=Color.white;badge.marker.raycastTarget=false;
            }
            badge.Refresh();
            target.GetComponent<DoodleUiSlotLayout>()?.Invalidate();
        }
        public void Refresh() { if(marker){marker.enabled=available!=null&&available();if(marker.transform.GetSiblingIndex()!=transform.childCount-1)marker.transform.SetAsLastSibling();} }
        void LateUpdate() { if(Time.unscaledTime<next)return;next=Time.unscaledTime+.2f;Refresh(); }
    }

    public sealed partial class DoodleUi
    {
        static void Notify(Transform target, Func<bool> condition) => DoodleNotificationBadge.Bind(target,condition);
        public bool CanUpgradeItem(UiItem item) => item!=null && item.discovered && item.level<ItemMaxLevel(item) && item.count>=CopiesNeeded(item);
        public bool CanSynthesize(UiItem item) { var target=SynthesisTarget(item);return target!=null&&item.discovered&&item.level>=100&&item.count>=5&&target.count<int.MaxValue; }
        readonly List<UiItem> notificationEquipped = new List<UiItem>(8);
        public bool CanImproveLoadout(UiItem item)
        {
            if(item==null||!item.discovered||item.equipped||EquipLimit(item.category)<=0)return false;
            if(IsEquipment(item)) {
                UiItem best=null;
                foreach(var candidate in collectionItems)
                    if(candidate.category == item.category && candidate.discovered&&(best==null||ItemEquipAmount(candidate)>ItemEquipAmount(best)))best=candidate;
                if(item!=best)return false;
            }
            var equipped=notificationEquipped; FillEquippedItems(item.category,equipped);
            if(equipped.Count<EquipLimit(item.category))return true;
            UiItem weakest=null;foreach(var current in equipped)if(weakest==null||CompareEquipPriority(current,weakest)<0)weakest=current;
            return weakest!=null&&CompareEquipPriority(item,weakest)>0;
        }
        bool CategoryCanUpgrade(string category) => Items(category).Exists(CanUpgradeItem);
        bool CategoryCanEquip(string category) => Items(category).Exists(CanImproveLoadout);
        bool ItemNeedsAttention(UiItem item) => CanUpgradeItem(item)||CanSynthesize(item)||CanImproveLoadout(item)||(item.category=="Skill"&&SkillRefundQuote(item)>0);
        bool EquipmentCategoryNeedsAttention(string category)
        {
            foreach (var item in collectionItems) if (item.category == category && ItemNeedsAttention(item)) return true;
            return false;
        }
        public bool CanClaimQuest(int tab,int index) => services!=null&&index>=0&&index<QuestCount(tab)&&!QuestClaimed(tab,index)&&QuestCounters(tab)[QuestMetrics[tab][index]]>=QuestGoal(tab,index)&&QuestReward(tab,index)>0&&(long)Diamonds+QuestReward(tab,index)<=int.MaxValue;
        public bool QuestTabHasReward(int tab) { for(int i=0;i<QuestCount(tab);i++)if(CanClaimQuest(tab,i))return true;return false; }
        public bool CanClaimAttendance => services!=null&&services.attendanceIndex<7&&services.attendanceDay!=services.day;
        public bool CanSpinRoulette => services!=null&&!rouletteSpinning&&services.spins<serviceTuning.dailySpins;
        public bool CanEnterDungeon(int index) => services!=null&&(index==0||index==2)&&services.activeDungeon<0&&services.dungeonUsed[index]<serviceTuning.dungeonAttempts;
        public bool NotificationForPage(string page)
        {
            if(services==null||collectionTuning==null)return false;
            switch(page) {
                case "Shop": return HasFreeShopReward;
                case "Quests": return QuestTabHasReward(0)||QuestTabHasReward(1)||QuestTabHasReward(2);
                case "Attendance": return CanClaimAttendance;
                case "Roulette": return CanSpinRoulette;
                case "Buffs": return GoldBuffSeconds==0||AttackBuffSeconds==0;
                case "Dungeons": return CanEnterDungeon(0)||CanEnterDungeon(2);
                case "Stats":
                    foreach(var stat in collectionTuning.stats){int count;GameNumber cost=StatUpgradeQuoteAmount(stat.id,1,out count);if(count>0&&GoldAmount>=cost)return true;}
                    return false;
                case "Equipment": return EquipmentCategoryNeedsAttention("Armor")||EquipmentCategoryNeedsAttention("Club")||EquipmentCategoryNeedsAttention("Necklace");
                case "Skins": return CanUnlockSkins("Weapon")||CanUnlockSkins("Appearance");
                case "Skills": return Items("Skill").Exists(ItemNeedsAttention);
                case "Companions": return Items("Companion").Exists(ItemNeedsAttention) || CollectionRefundQuote("Companion") > 0;
                case "Relics": return AllRelics.Exists(CanUpgradeItem);
                default:return false;
            }
        }
    }
}
