using System;
using UnityEngine;

namespace DoodleIdle
{
    [Serializable]
    public sealed class DoodlePvpLoadout
    {
        public int version=1, highestStage;
        public string playerName, collections, skins;
        public float attackBuff=1;
    }

    public sealed partial class DoodleUi
    {
        DoodlePvpLoadout pvpLoadout;

        public DoodlePvpLoadout CapturePvpLoadout()
        {
            InitCollections();InitSkins();
            var collection=new CollectionSave{version=3};
            foreach(var item in collectionItems) if(item.discovered)
                collection.items.Add(new ItemSave{id=item.id,level=item.level,count=0,slot=item.slot,equipped=item.equipped,discovered=true});
            foreach(var stat in statLevels)collection.stats.Add(new StatSave{id=stat.Key,level=stat.Value});
            var skinState=new SkinSave();
            foreach(var skin in skinCatalog) {
                if(skin.owned)skinState.owned.Add(skin.id);
                if(!skin.equipped)continue;
                if(skin.category=="Weapon")skinState.weapon=skin.id;else skinState.appearance=skin.id;
            }
            return new DoodlePvpLoadout{playerName=PlayerName,highestStage=HighestMainStage,attackBuff=AttackBuffMultiplier,
                collections=DoodleJson.ToJson(collection),skins=DoodleJson.ToJson(skinState)};
        }

        internal static DoodleUi CreatePvpCombatModel(DoodleIdleGame owner,DoodlePvpLoadout snapshot)
        {
            if(snapshot==null || snapshot.version!=1 || string.IsNullOrEmpty(snapshot.collections))
                throw new ArgumentException("지원하지 않는 PVP 전투 정보입니다.");
            var model=owner.gameObject.AddComponent<DoodleUi>();
            model.game=owner;model.suppressSaving=true;model.pvpLoadout=snapshot;
            model.PlayerName=snapshot.playerName;
            var tuning=Resources.Load<TextAsset>("DoodleIdle/UI/ServicesTuning");
            if(tuning)DoodleJson.FromJsonOverwrite(tuning.text,model.serviceTuning);
            model.services=new ServiceState{highestMainStage=Mathf.Max(0,snapshot.highestStage)};
            model.InitCollections();model.InitSkins();
            model.NormalizeEquipment("Skill");
            return model;
        }
    }
}
