using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        public bool companionsEnabled=true;
        sealed class CompanionActor { public UiItem item; public SpriteRenderer art; public float clock,attackAge,shotClock,swing,aim; public int pending,shotIndex; public Vector2 attackStart,attackEnd; }
        readonly Dictionary<string,CompanionActor> companions=new Dictionary<string,CompanionActor>();
        readonly Dictionary<string,Sprite> companionSprites=new Dictionary<string,Sprite>();
        public int ActiveCompanions => companions.Count;
        public int CompanionAttacks { get; private set; }
        Sprite WorldIcon(string key)
        {
            var variant=DoodleVariantArt.Get(key);if(variant)return variant;
            if(companionSprites.TryGetValue(key,out var cached))return cached;
            var source=UiKit.Art(key);var rect=source.rect;
            var sprite=Sprite.Create(source.texture,rect,Vector2.one*.5f,Mathf.Max(rect.width,rect.height));sprite.name=key;
            companionSprites[key]=sprite;return sprite;
        }
        void TickCompanions(float dt)
        {
            var equipped=Ui.EquippedCompanions;
            var removed=new List<string>();
            foreach(var pair in companions)if(!companionsEnabled || !equipped.Exists(x=>x.id==pair.Key))removed.Add(pair.Key);
            foreach(var id in removed){Destroy(companions[id].art.gameObject);companions.Remove(id);}
            if(!companionsEnabled)return;
            for(int i=0;i<equipped.Count;i++) {
                var item=equipped[i];
                if(!companions.TryGetValue(item.id,out var companion)) {
                    companion=new CompanionActor {item=item,clock=.5f+i*.15f,art=Visual("Companion: "+item.id,WorldIcon(item.icon),player.Position,Vector2.one*.95f,450)};
                    companions[item.id]=companion;
                }
                var role=item.ability;
                float angle=i*Mathf.PI*2/Mathf.Max(1,equipped.Count)+Mathf.PI*.5f+(role=="OrbitGun"?Elapsed*1.65f:0);
                Vector2 home=player.Position+Direction(angle)*(role=="OrbitGun"?2.5f:1.75f)+Vector2.up*Mathf.Sin(Elapsed*4+i)*.1f;
                companion.clock-=dt;
                if(companion.attackAge>0) {
                    companion.attackAge=Mathf.Max(0,companion.attackAge-dt);
                    float t=1-companion.attackAge/.5f;
                    home=t<.5f?Vector2.Lerp(companion.attackStart,companion.attackEnd,t*2):Vector2.Lerp(companion.attackEnd,home,(t-.5f)*2);
                }
                Vector2 old=companion.art.transform.position;
                companion.art.transform.position=Vector2.Lerp(old,home,1-Mathf.Exp(-dt*14));
                if(Mathf.Abs(home.x-old.x)>.01f)companion.art.flipX=home.x<old.x;
                companion.art.sortingOrder=Order(companion.art.transform.position)+2;
                if(role=="Guardian") {
                    companion.swing=Mathf.Max(0,companion.swing-dt);float progress=1-companion.swing/.34f;
                    float sweep=progress<.65f?Mathf.Lerp(-70,85,progress/.65f):Mathf.Lerp(85,0,(progress-.65f)/.35f);
                    companion.art.transform.rotation=Quaternion.Euler(0,0,companion.aim+sweep);companion.art.flipX=false;
                }
                if(role=="Drone")SetSpriteArt(companion.art,(int)(Elapsed*8)%2==0?skillArt[3]:droneFrameB);
                Vector2 origin=companion.art.transform.position;
                if(role=="Drone" && companion.pending>0) {
                    companion.shotClock-=dt;
                    if(companion.shotClock<=.0001f) {
                        Launch(ProjectileKind.Missile,NearbyTarget(origin,companion.shotIndex++),origin,companion.shotIndex);
                        companion.pending--;companion.shotClock+=.06f;
                    }
                }
                var target=InRange(origin,role=="Guardian"||role=="Claw"||role=="Sting"?5:10);
                if(target==null || companion.clock>0)continue;
                CompanionAttacks++;var direction=(target.Position-origin).normalized;
                companion.art.flipX=direction.x<0;
                companion.clock=role=="OrbitGun"?.09f:role=="Guardian"?guardianInterval:role=="Drone"?droneInterval:1.8f;
                if(role=="Drone") {
                    // Preserve the original twenty-shot salvo, emitted from the moving companion.
                    companion.pending=20;companion.shotIndex=0;companion.shotClock=0;
                }
                else if(role=="Guardian" || role=="Claw" || role=="Sting") {
                    if(role=="Sting"){companion.attackAge=.5f;companion.attackStart=origin;companion.attackEnd=target.Position;}
                    summonCasts[(int)SummonSkill.GuardianSword]++;
                    AddMoving(SummonSkill.GuardianSword,slash,origin,direction,1.4f,12,.5f,23,1);
                    if(role=="Guardian"){companion.swing=.34f;companion.aim=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;companion.art.flipX=false;}
                }
                else if(role=="OrbitGun") {
                    summonCasts[(int)SummonSkill.OrbitGun]++;
                    companion.art.flipX=false;companion.art.flipY=direction.x<0;companion.art.transform.rotation=Aim(direction);
                    Vector2 muzzle=origin+direction*.45f;
                    AddMoving(SummonSkill.OrbitGun,summonArt["OrbitBullet"],muzzle,direction,.38f,20,.7f,8,.65f);
                    Echo("Companion muzzle flash",summonArt["MuzzleFlash"],muzzle,Vector2.one*.5f,Aim(direction),.065f,1,470);
                    OrbitBulletsLaunched++;OrbitBulletLaunched?.Invoke(Time.fixedTime,muzzle,direction);
                }
                else if(role=="Lightning") {
                    var delta=target.Position-origin;
                    Echo("Companion lightning",summonArt["Lightning"],origin+delta*.5f,new Vector2(delta.magnitude,.7f),Aim(delta),.16f,1,580);
                    Damage(target,28,direction);
                }
                else {
                    string art=role=="Spore"?"SandPuff":role=="Frost"?"IceSnakeSegment":role=="Brick"?"Brick":role=="Shuriken"?"Shuriken":role=="Poison"?"PurpleSnakeHead":role=="Stone"?"Stone":role=="Star"?"Shuriken":role=="Wind"?"SoundWave":"Fireball";
                    int count=role=="Stone"||role=="Poison"?1:3;
                    for(int n=0;n<count;n++)VariantProjectile(art,origin,Rotate(direction,(n-(count-1)*.5f)*18),role=="Brick"?1.29f:.65f,9,1.7f,18,.45f,role=="Shuriken"?720:0);
                }
            }
        }
        void ClearCompanions()
        {
            foreach(var companion in companions.Values)if(companion.art)Destroy(companion.art.gameObject);
            companions.Clear();CompanionAttacks=0;
        }
        void DisposeCompanionArt(){foreach(var sprite in companionSprites.Values)if(sprite)Destroy(sprite);companionSprites.Clear();}
    }
}
