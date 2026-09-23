using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        [Serializable] sealed class CommerceExtras
        {
            public string freeDiamondDay="";
            public int freeDiamondClaims,mileageCoupons;
            public List<string> fulfilledTransactions=new List<string>();
        }
        CommerceExtras commerceExtras=new CommerceExtras();
        const string CommerceExtrasKey="DoodleUi.CommerceExtras.v1";
        void InitCommerceExtras()
        {
            string json=PlayerPrefs.GetString(CommerceExtrasKey,"");
            if(!string.IsNullOrEmpty(json))try{JsonUtility.FromJsonOverwrite(json,commerceExtras);}catch(ArgumentException){commerceExtras=new CommerceExtras();}
            commerceExtras.freeDiamondClaims=Mathf.Clamp(commerceExtras.freeDiamondClaims,0,30);
            commerceExtras.mileageCoupons=Math.Max(0,commerceExtras.mileageCoupons);
            if(commerceExtras.fulfilledTransactions==null)commerceExtras.fulfilledTransactions=new List<string>();
        }
        public static string TicketIcon(string category) => category=="DungeonRelic"?"DungeonRelicTicket":"Ticket"+category;
        public int SummonTickets(string category) => category=="Relic"?RelicTickets:category=="DungeonRelic"?DungeonRelicTickets:summonStates.ContainsKey(category)?summonStates[category].tickets:0;
        public void GrantSummonTickets(string category,int amount)
        {
            if(amount<=0)return;
            if(category=="Relic"){GrantRelicTickets(amount);return;}
            if(category=="DungeonRelic"){GrantDungeonRelicTickets(amount);return;}
            if(!summonStates.ContainsKey(category))return;
            summonStates[category].tickets=(int)Math.Min(int.MaxValue,(long)summonStates[category].tickets+amount);Save();
        }
        public bool TrySummonTickets(string category,int count)
        {
            if(category=="DungeonRelic")return TrySummonDungeonRelicTickets(count);
            if((count!=1&&count!=10&&count!=50)||!summonStates.ContainsKey(category)||SummonTickets(category)<count)return false;
            var rewards=new List<UiItem>(count);for(int i=0;i<count;i++)rewards.Add(GrantItem(category,commerceRandom));
            if(category=="Relic")services.relicTickets-=count;else summonStates[category].tickets-=count;
            long before=Power;CompleteSummon(category,rewards);NotifyPowerChanged(before,"뽑기권 사용");return true;
        }
        void BuildTicketBalance(Transform parent,string category)
        {
            var row=UiKit.Row(parent,"Tickets: "+category,48,4);
            UiKit.Icon(row,TicketIcon(category),32);
            UiKit.Text(row,UiNumber.Format(SummonTickets(category))+"장 보유 · 뽑기권 우선 사용",20,TextAnchor.MiddleLeft,36);
        }
        public int FreeDiamondClaimsRemaining => commerceExtras.freeDiamondDay==CommerceDay()?30-commerceExtras.freeDiamondClaims:30;
        public bool CanClaimFreeDiamonds => FreeDiamondClaimsRemaining>0 && Diamonds<=int.MaxValue-1000;
        public bool HasFreeShopReward => CanClaimFreeDiamonds || Array.Exists(commerceCategories, category => category!="DungeonRelic" && CanFreeSummon(category));
        public int MileageCoupons => commerceExtras.mileageCoupons;
        public bool ClaimFreeDiamonds()
        {
            if(!CanClaimFreeDiamonds)return false;
            if(commerceExtras.freeDiamondDay!=CommerceDay()){commerceExtras.freeDiamondDay=CommerceDay();commerceExtras.freeDiamondClaims=0;}
            int amount=commerceRandom.Next(200,1001);commerceExtras.freeDiamondClaims++;
            GrantServiceDiamonds(amount,"무료 다이아 획득!");return true;
        }
        public bool ExchangeMileage(int count)
        {
            int amount=count==5?1500000:count==10?5000000:0;
            if(amount==0||MileageCoupons<count||Diamonds>int.MaxValue-amount)return false;
            commerceExtras.mileageCoupons-=count;GrantServiceDiamonds(amount,"마일리지 교환 완료!");return true;
        }
        // Fulfillment hook for a future verified payment adapter; the current shop never calls it.
        // The adapter must verify a receipt before supplying its unique transaction ID.
        public bool GrantConfirmedCurrencyProduct(int index,string transactionId)
        {
            if(index<0||index>=commerceTuning.products.Length||string.IsNullOrWhiteSpace(transactionId)||commerceExtras.fulfilledTransactions.Contains(transactionId))return false;
            var product=commerceTuning.products[index];
            if(Diamonds>int.MaxValue-product.amount||MileageCoupons>int.MaxValue-product.mileageCoupons)return false;
            commerceExtras.fulfilledTransactions.Add(transactionId);commerceExtras.mileageCoupons+=product.mileageCoupons;Diamonds+=product.amount;Save();return true;
        }
        void BuildMileageShop(RectTransform body)
        {
            var balance=UiKit.Row(body,"Mileage balance",72,10);UiKit.Icon(balance,"MileageCoupon",64);
            UiKit.Text(balance,"마일리지 쿠폰 "+MileageCoupons+"개",30,TextAnchor.MiddleLeft,64);
            foreach(int count in new[]{5,10}) {
                var card=CollectionBox(body,"Mileage exchange "+count,UiKit.Paper);
                var row=UiKit.Row(card,"Exchange reward",92,12);UiKit.Icon(row,"DiamondRoyalChest",82);
                UiKit.Text(row,(count==5?"150만":"500만")+" 다이아",32,TextAnchor.MiddleCenter,80);
                var button=UiKit.Button(card,"쿠폰 "+count+"개로 교환",()=>ExchangeMileage(count),UiKit.Yellow,64);
                button.interactable=MileageCoupons>=count;
            }
        }
        void BuildFreeDiamondCard(Transform grid)
        {
            var card=UiKit.Box(grid,"Free diamond card",new Color(1,.977f,.895f));
            var label=UiKit.Text(card,"무료 다이아\n200~1,000",30,TextAnchor.MiddleCenter,76).rectTransform;
            label.anchorMin=new Vector2(0,.74f);label.anchorMax=new Vector2(1,.98f);label.offsetMin=new Vector2(8,0);label.offsetMax=new Vector2(-8,0);
            var icon=UiKit.Icon(card,"DiamondPile",150).rectTransform;icon.anchorMin=new Vector2(.1f,.26f);icon.anchorMax=new Vector2(.9f,.73f);icon.offsetMin=icon.offsetMax=Vector2.zero;
            var button=UiKit.Button(card,"무료 받기 ("+FreeDiamondClaimsRemaining+"/30)",()=>ClaimFreeDiamonds(),UiKit.Yellow,64);CommerceButtonText(button,23);
            button.interactable=CanClaimFreeDiamonds;
            Notify(button.transform,()=>CanClaimFreeDiamonds);
            var rect=(RectTransform)button.transform;rect.anchorMin=new Vector2(.04f,.04f);rect.anchorMax=new Vector2(.96f,.23f);rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
    }
}
