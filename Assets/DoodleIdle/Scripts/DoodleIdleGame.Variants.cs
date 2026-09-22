using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        sealed class VariantVolley { public string ability; public bool requiresEquipment; public int remaining,index; public float clock; public Vector2 direction; }
        sealed class VariantShot
        {
            public SpriteRenderer art; public Vector2 direction,start,end; public float age,speed,life,damage,radius,spin;
            public bool arc,rolling,afterimage; public float size,trail; public readonly HashSet<Actor> victims=new HashSet<Actor>();
        }
        readonly Dictionary<string,float> variantClocks=new Dictionary<string,float>();
        readonly List<VariantVolley> variantVolleys=new List<VariantVolley>();
        readonly List<VariantShot> variantShots=new List<VariantShot>();
        public int VariantProjectilesLaunched { get; private set; }
        public static float VariantInterval(string ability)
        {
            switch(ability) {
                case "Eggplant":return 6; case "Durian":return 7; case "BrickVolley":return 5;
                case "Shuriken":return 6;case "IceSnakes":return 9;case "PurpleFireArrows":return 5;
                case "GiantWorm":return 8;case "BlueMolotov":return 8;case "RedCloud":return 10;
                case "Dumbbell":return 6;
                default:return 0;
            }
        }
        public void CastVariant(string ability)
        {
            if(player==null || enemies.Count==0 || VariantInterval(ability)<=0)return;
            var origin=player.Position;var target=Closest(origin);var direction=(target.Position-origin).normalized;
            if(ability=="Eggplant") {
                for(int i=0;i<2;i++) {
                    var shot=VariantProjectile("Cucumber",origin,Rotate(direction,(i-.5f)*30),4.6f,6,3.2f,42,1.3f);
                    shot.rolling=true;UpdateRollingVegetable(shot.art,shot.direction,0,shot.size);
                }
            }
            else if(ability=="IceSnakes") {
                for(int i=-1;i<=1;i++)SpawnSnake(SummonSkill.TetherSnake,Rotate(direction,i*50),true);
            }
            else if(ability=="GiantWorm") {
                CastExtraSkill(ExtraSkill.Worm);var worm=worms[worms.Count-1];worm.size=2;
                foreach(var part in worm.parts)part.transform.localScale*=2;
            }
            else if(ability=="BlueMolotov") {
                for(int i=0;i<2;i++)ThrowMolotov(origin,NearbyTarget(origin,i).Position,true);
            }
            else if(ability=="RedCloud")clouds.Add(new Cloud { red=true,art=Visual("Red storm cloud",DoodleVariantArt.Get("RedCloud"),origin+Vector2.up*2,Vector2.one*2.6f,650),target=target });
            else if(ability=="Shuriken") {
                for(int i=0;i<8;i++) {
                    var shot=VariantProjectile("Shuriken",origin,Rotate(direction,i*45),1.2f,SoundWaveSpeed,SoundWaveLifetime,30,.7f,720);
                    shot.afterimage=true;
                }
            }
            else {
                var volley=new VariantVolley { requiresEquipment=castingEquippedSkill,ability=ability,direction=direction,remaining=ability=="Dumbbell"?10:ability=="Durian"?3:ability=="PurpleFireArrows"?8:6 };
                FireVariantVolley(volley);if(volley.remaining>0)variantVolleys.Add(volley);
            }
        }
        static Vector2 Rotate(Vector2 direction,float degrees) => (Vector2)(Quaternion.Euler(0,0,degrees)*(Vector3)direction);
        void FireVariantVolley(VariantVolley volley)
        {
            var target=NearbyTarget(player.Position,volley.index);
            if(Alive(target)) {
                if(volley.ability=="Durian" || volley.ability=="PurpleFireArrows") {
                    Launch(volley.ability=="Durian"?ProjectileKind.Ball:ProjectileKind.Arrow,target,player.Position,volley.index);
                    var shot=extraShots[extraShots.Count-1];shot.size=2;shot.purple=volley.ability=="PurpleFireArrows";
                    if(shot.purple) {
                        shot.curveSide=volley.index%2==0?1:-1;
                        shot.waveDirection=Rotate(volley.direction,volley.index*45);
                        shot.curveNormal=new Vector2(-shot.waveDirection.y,shot.waveDirection.x);
                    }
                    SetSpriteArt(shot.art,DoodleVariantArt.Get(shot.purple?"PurpleFireArrow":"Durian"));shot.art.transform.localScale*=2;
                    shot.art.name=volley.ability+" projectile";VariantProjectilesLaunched++;
                } else if(volley.ability=="BrickVolley" || volley.ability=="Dumbbell") {
                    bool dumbbell=volley.ability=="Dumbbell";
                    var shot=VariantProjectile(dumbbell?"SkillDumbbell":"Brick",player.Position,volley.direction,1.29f,0,.8f,dumbbell?40:30,dumbbell?1:.65f,360);
                    shot.arc=true;shot.end=target.Position;
                }
            }
            volley.index++;volley.remaining--;volley.clock=volley.ability=="Durian"?.2f:volley.ability=="PurpleFireArrows"?.1f:.14f;
        }
        VariantShot VariantProjectile(string art,Vector2 origin,Vector2 direction,float size,float speed,float life,float damage,float radius,float spin=0)
        {
            var sprite=WorldIcon(art);
            var shot=new VariantShot { start=origin,direction=direction,speed=speed,life=life,damage=damage,radius=radius,spin=spin,size=size,
                art=Visual(art+" variant projectile",sprite,origin,Vector2.one*size,515) };
            shot.art.transform.rotation=Aim(direction);variantShots.Add(shot);VariantProjectilesLaunched++;return shot;
        }
        void TickVariants(float dt)
        {
            for(int i=variantVolleys.Count-1;i>=0;i--) {
                var volley=variantVolleys[i];
                if(volley.requiresEquipment && !SkillEquipped(volley.ability)){variantVolleys.RemoveAt(i);continue;}
                volley.clock-=dt;
                if(volley.clock<=.0001f){FireVariantVolley(volley);if(volley.remaining<=0)variantVolleys.RemoveAt(i);}
            }
            for(int i=variantShots.Count-1;i>=0;i--) {
                var shot=variantShots[i];shot.age+=dt;Vector2 old=shot.art.transform.position;
                float t=Mathf.Clamp01(shot.age/shot.life);
                Vector2 next=shot.arc?Vector2.Lerp(shot.start,shot.end,t)+Vector2.up*(10*t*(1-t)):old+shot.direction*(shot.speed*dt);
                shot.art.transform.position=next;
                if(shot.rolling)UpdateRollingVegetable(shot.art,shot.direction,shot.age,shot.size);
                else shot.art.transform.Rotate(0,0,shot.spin*dt);
                if(shot.afterimage) {
                    shot.trail-=dt;
                    if(shot.trail<=0) {
                        Echo("Shuriken afterimage",shot.art.sprite,old,shot.art.transform.localScale,shot.art.transform.rotation,.22f,.32f,490);
                        shot.trail=.05f;
                    }
                }
                if(!shot.arc || t>=1)for(int e=enemies.Count-1;e>=0;e--) {
                    var enemy=enemies[e];float distance=shot.arc?Vector2.Distance(enemy.Position,shot.end):SegmentDistance(enemy.Position,old,next);
                    if(distance>shot.radius+.56f || !shot.victims.Add(enemy))continue;
                    SkillDamage(enemy,shot.damage,shot.direction);
                }
                if(shot.age>=shot.life){Destroy(shot.art.gameObject);variantShots.RemoveAt(i);}
            }
        }
        void ClearVariants()
        {
            foreach(var shot in variantShots)if(shot.art)Destroy(shot.art.gameObject);
            variantShots.Clear();variantVolleys.Clear();variantClocks.Clear();VariantProjectilesLaunched=0;
        }
    }
}
