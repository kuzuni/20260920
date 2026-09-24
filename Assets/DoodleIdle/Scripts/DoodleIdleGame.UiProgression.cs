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
            // Only the highest successful unlocked tier applies; never multiply tiers together.
            // Untouched profiles consume no critical RNG.
            for (int tier = DoodleUi.CriticalStatIds.Length - 1; tier >= 0; tier--) {
                float chance = Ui.CriticalChance(tier);
                if (chance >= 100 || chance > 0 && Random.value < chance / 100f) {
                    multiplier = DoodleUi.CriticalMultiplierAt(tier); break;
                }
            }
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
            if (!Ui || Ui.EquippedAppearanceIcon == "Player") return walkingFrame;
            string key = Ui.EquippedAppearanceIcon;
            if (key.StartsWith("SkinAppearance_", System.StringComparison.Ordinal) && walkingFrame == playerWalkB)
                key = key.Substring(0, key.Length - 1) + "1";
            return WorldSkinSprite(key, false);
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
