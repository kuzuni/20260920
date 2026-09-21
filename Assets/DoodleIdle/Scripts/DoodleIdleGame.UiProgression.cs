using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        readonly Dictionary<string,Sprite> worldSkinSprites = new Dictionary<string,Sprite>();
        public void SetUiCameraSize(float size) { if(gameCamera)gameCamera.orthographicSize=Mathf.Max(1,size); }
        public float RollUiCriticalMultiplier()
        {
            if(!Ui)return 1;
            float multiplier=1;
            // A 4x result takes priority; the 2x roll applies only when it did not occur.
            // Do not consume the combat RNG at all for an untouched 0%-critical profile.
            if(Ui.Critical4Chance>=100 || (Ui.Critical4Chance>0 && Random.value < Ui.Critical4Chance/100f)) multiplier=4;
            else if(Ui.Critical2Chance>=100 || (Ui.Critical2Chance>0 && Random.value < Ui.Critical2Chance/100f)) multiplier=2;
            return multiplier>1?multiplier*(1+Ui.CriticalDamageBonus/100f):1;
        }
        Sprite WorldSkinSprite(string key,bool grip)
        {
            string cacheKey=(grip?"weapon:":"appearance:")+key;
            if(worldSkinSprites.TryGetValue(cacheKey,out var cached))return cached;
            var source=UiKit.Art(key);
            if(!source)return grip?sprites[4]:sprites[0];
            var result=Sprite.Create(source.texture,source.rect,grip?new Vector2(.15f,.12f):Vector2.one*.5f,Mathf.Max(source.rect.width,source.rect.height));
            result.name="Skin "+key;worldSkinSprites[cacheKey]=result;return result;
        }
        Sprite PlayerSkinFrame(Sprite walkingFrame)
        {
            return !Ui || Ui.EquippedAppearanceIcon=="Player" ? walkingFrame : WorldSkinSprite(Ui.EquippedAppearanceIcon,false);
        }
        void ApplyWeaponSkin(SpriteRenderer art)
        {
            string key=Ui?Ui.EquippedWeaponIcon:"Club";
            var sprite=key=="Club"?sprites[4]:WorldSkinSprite(key,true);
            if(art.sprite!=sprite)SetSpriteArt(art,sprite);
            art.color=Ui?Ui.EquippedWeaponTint:Color.white;
        }
        void DisposeWorldSkins()
        {
            foreach(var sprite in worldSkinSprites.Values)if(sprite)Destroy(sprite);
            worldSkinSprites.Clear();
        }
    }
}
