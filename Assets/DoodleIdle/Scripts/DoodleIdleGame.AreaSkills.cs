using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        sealed class Bottle { public SpriteRenderer art; public Vector2 start, end; public float age; public bool blue; }
        sealed class FireZone { public Vector2 center; public float age, emitClock, hitClock; public bool blue; }
        sealed class SoundPulse
        {
            public SpriteRenderer art;
            public Vector2 center, direction;
            public float age, radius;
            public readonly HashSet<Actor> victims = new HashSet<Actor>();
        }
        readonly List<Bottle> bottles = new List<Bottle>();
        readonly List<FireZone> fireZones = new List<FireZone>();
        readonly List<SoundPulse> soundWaves = new List<SoundPulse>();
        sealed class SoundVolley { public bool requiresEquipment; public Vector2 direction; public int remaining = 4; public float clock = SoundWaveShotGap; }
        readonly List<SoundVolley> soundVolleys = new List<SoundVolley>();
        public int SoundWavesLaunched { get; private set; }
        public event System.Action<float> SoundWaveLaunched;
        public const float SoundWaveShotGap = .18f, SoundWaveSpeed = 7, SoundWaveLifetime = 1.5f;
        const float SoundWaveDepthRatio = .55f;
        public int ActiveFireZones => fireZones.Count;
        public const float FireZoneLifetime = 4, FireZoneRadius = 2.3f;

        void ThrowMolotov(Vector2 origin, Vector2 target,bool blue=false)
        {
            bottles.Add(new Bottle { blue=blue,start = origin, end = target,
                art = Visual(blue?"Blue molotov airborne bottle":"Molotov airborne bottle", blue?DoodleVariantArt.Get("BlueMolotov"):summonArt["Molotov"], origin, Vector2.one * 1.1f, 540) });
        }
        void StartSoundVolley(Vector2 direction)
        {
            LaunchSoundWave(direction);
            soundVolleys.Add(new SoundVolley { direction = direction, requiresEquipment = castingEquippedSkill });
        }
        void LaunchSoundWave(Vector2 direction)
        {
            Vector2 origin = player.Position;
            var pulse = new SoundPulse { center = origin, direction = direction, radius = .35f,
                art = Visual("Traveling sound wave", summonArt["SoundWave"], origin, Vector2.one * .7f, 480) };
            UpdateSoundWaveShape(pulse);
            soundWaves.Add(pulse);
            SoundWavesLaunched++;
            SoundWaveLaunched?.Invoke(Time.fixedTime);
        }
        static void UpdateSoundWaveShape(SoundPulse pulse)
        {
            // The broad wavefront faces travel: short along travel, wide across it.
            pulse.art.transform.rotation = Aim(pulse.direction);
            var spriteSize = pulse.art.sprite.bounds.size;
            pulse.art.transform.localScale = new Vector3(
                pulse.radius * 2 * SoundWaveDepthRatio / spriteSize.x,
                pulse.radius * 2 / spriteSize.y, 1);
        }
        void EmitGroundFire(Vector2 center, int count,bool blue=false)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Direction(ParticleRandom(0, Mathf.PI * 2)) * Mathf.Sqrt(ParticleRandom(0, 1)) * FireZoneRadius;
                (blue?blueFireParticles:groundFireParticles).Emit(new ParticleSystem.EmitParams {
                    position = center + offset,
                    velocity = new Vector2(ParticleRandom(-.15f, .15f), ParticleRandom(.2f, .65f)),
                    startColor = Color.white, startSize = ParticleRandom(.5f, .9f),
                    startLifetime = ParticleRandom(.35f, .6f), rotation = ParticleRandom(-12, 12), randomSeed = ++particleSeed
                }, 1);
            }
        }
        void TickAreaSkills(float dt)
        {
            for (int i = soundVolleys.Count - 1; i >= 0; i--)
            {
                var volley = soundVolleys[i];
                if (volley.requiresEquipment && !SkillEquipped("Sound")) { soundVolleys.RemoveAt(i); continue; }
                volley.clock -= dt;
                if (volley.clock > .0001f) continue;
                LaunchSoundWave(volley.direction); volley.clock += SoundWaveShotGap;
                if (--volley.remaining == 0) soundVolleys.RemoveAt(i);
            }
            for (int i = bottles.Count - 1; i >= 0; i--)
            {
                var bottle = bottles[i]; bottle.age += dt;
                float t = Mathf.Clamp01(bottle.age / .8f);
                bottle.art.transform.position = Vector2.Lerp(bottle.start, bottle.end, t) + Vector2.up * (4 * 2.5f * t * (1 - t));
                bottle.art.transform.rotation = Quaternion.Euler(0, 0, -t * 300);
                if (t < 1) continue;
                EmitCannonExplosion(bottle.end);
                EmitGroundFire(bottle.end, 28,bottle.blue);
                fireZones.Add(new FireZone { center = bottle.end,blue=bottle.blue });
                for (int e = enemies.Count - 1; e >= 0; e--)
                    if (Vector2.Distance(enemies[e].Position, bottle.end) <= FireZoneRadius + .56f)
                        Impact(SummonSkill.Molotov, enemies[e], 18, Vector2.zero);
                Destroy(bottle.art.gameObject); bottles.RemoveAt(i);
            }
            for (int i = fireZones.Count - 1; i >= 0; i--)
            {
                var zone = fireZones[i]; zone.age += dt;
                if (zone.age >= FireZoneLifetime) { fireZones.RemoveAt(i); continue; }
                zone.emitClock -= dt; zone.hitClock -= dt;
                if (zone.emitClock <= 0) { zone.emitClock += .09f; EmitGroundFire(zone.center, 12,zone.blue); }
                if (zone.hitClock > 0) continue;
                zone.hitClock += .35f;
                for (int e = enemies.Count - 1; e >= 0; e--)
                    if (Vector2.Distance(enemies[e].Position, zone.center) <= FireZoneRadius + .56f)
                        Impact(SummonSkill.Molotov, enemies[e], 8, Vector2.zero);
            }
            for (int i = soundWaves.Count - 1; i >= 0; i--)
            {
                var pulse = soundWaves[i]; float previousRadius = pulse.radius;
                Vector2 previousCenter = pulse.center;
                pulse.age += dt; pulse.radius = .35f + pulse.age * .8f;
                pulse.center += pulse.direction * (SoundWaveSpeed * dt);
                pulse.art.transform.position = pulse.center;
                UpdateSoundWaveShape(pulse);
                pulse.art.color = new Color(1, 1, 1, Mathf.Clamp01((SoundWaveLifetime - pulse.age) / .4f) * .8f);
                // Sweep the moving ring between physics steps, excluding its empty inner region.
                for (int e = enemies.Count - 1; e >= 0; e--)
                {
                    var enemy = enemies[e];
                    // Expand BOTH ellipse axes by the enemy's world-space radius before normalizing.
                    // Scaling the enemy radius by the oval's aspect would incorrectly miss its outer edge.
                    Vector2 normal=new Vector2(-pulse.direction.y,pulse.direction.x);
                    Vector2 Relative(Vector2 p) { var d=p-previousCenter;return new Vector2(Vector2.Dot(d,pulse.direction)/(pulse.radius*SoundWaveDepthRatio+.56f),Vector2.Dot(d,normal)/(pulse.radius+.56f)); }
                    Vector2 point=Relative(enemy.Position),end=Relative(pulse.center);
                    float outerDistance = SegmentDistance(point, Vector2.zero, end);
                    float innerX=previousRadius*.77f*SoundWaveDepthRatio-.56f,innerY=previousRadius*.77f-.56f;
                    bool InsideHole(Vector2 center) { var d=enemy.Position-center;return innerX>0 && innerY>0 && Mathf.Pow(Vector2.Dot(d,pulse.direction)/innerX,2)+Mathf.Pow(Vector2.Dot(d,normal)/innerY,2)<1; }
                    if (outerDistance > 1 || (InsideHole(previousCenter)&&InsideHole(pulse.center)) || !pulse.victims.Add(enemy)) continue;
                    Impact(SummonSkill.SoundWave, enemy, 24, pulse.direction);
                }
                if (pulse.age >= SoundWaveLifetime) { Destroy(pulse.art.gameObject); soundWaves.RemoveAt(i); }
            }
        }
        void ClearAreaSkills()
        {
            foreach (var bottle in bottles) if (bottle.art) Destroy(bottle.art.gameObject);
            foreach (var pulse in soundWaves) if (pulse.art) Destroy(pulse.art.gameObject);
            bottles.Clear(); fireZones.Clear(); soundWaves.Clear(); soundVolleys.Clear();
            SoundWavesLaunched = 0;
        }
    }
}
