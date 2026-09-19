using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        sealed class Bottle { public SpriteRenderer art; public Vector2 start, end; public float age; }
        sealed class FireZone { public Vector2 center; public float age, emitClock, hitClock; }
        sealed class SoundPulse
        {
            public SpriteRenderer art;
            public Vector2 center;
            public float age, radius;
            public readonly HashSet<Actor> victims = new HashSet<Actor>();
        }
        readonly List<Bottle> bottles = new List<Bottle>();
        readonly List<FireZone> fireZones = new List<FireZone>();
        readonly List<SoundPulse> soundWaves = new List<SoundPulse>();
        public int ActiveFireZones => fireZones.Count;
        public const float FireZoneLifetime = 4, FireZoneRadius = 2.3f;

        void ThrowMolotov(Vector2 origin, Vector2 target)
        {
            bottles.Add(new Bottle { start = origin, end = target,
                art = Visual("Molotov airborne bottle", summonArt["Molotov"], origin, Vector2.one * 1.1f, 540) });
        }
        void SpawnSoundWave(Vector2 origin)
        {
            soundWaves.Add(new SoundPulse { center = origin, radius = .3f,
                art = Visual("Expanding sound wave", summonArt["SoundWave"], origin, Vector2.one * .6f, 480) });
        }
        void EmitGroundFire(Vector2 center, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Direction(ParticleRandom(0, Mathf.PI * 2)) * Mathf.Sqrt(ParticleRandom(0, 1)) * FireZoneRadius;
                groundFireParticles.Emit(new ParticleSystem.EmitParams {
                    position = center + offset,
                    velocity = new Vector2(ParticleRandom(-.15f, .15f), ParticleRandom(.2f, .65f)),
                    startColor = Color.white, startSize = ParticleRandom(.5f, .9f),
                    startLifetime = ParticleRandom(.35f, .6f), rotation = ParticleRandom(-12, 12), randomSeed = ++particleSeed
                }, 1);
            }
        }
        void TickAreaSkills(float dt)
        {
            for (int i = bottles.Count - 1; i >= 0; i--)
            {
                var bottle = bottles[i]; bottle.age += dt;
                float t = Mathf.Clamp01(bottle.age / .8f);
                bottle.art.transform.position = Vector2.Lerp(bottle.start, bottle.end, t) + Vector2.up * (4 * 2.5f * t * (1 - t));
                bottle.art.transform.rotation = Quaternion.Euler(0, 0, -t * 300);
                if (t < 1) continue;
                EmitCannonExplosion(bottle.end);
                EmitGroundFire(bottle.end, 28);
                fireZones.Add(new FireZone { center = bottle.end });
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
                if (zone.emitClock <= 0) { zone.emitClock += .09f; EmitGroundFire(zone.center, 12); }
                if (zone.hitClock > 0) continue;
                zone.hitClock += .35f;
                for (int e = enemies.Count - 1; e >= 0; e--)
                    if (Vector2.Distance(enemies[e].Position, zone.center) <= FireZoneRadius + .56f)
                        Impact(SummonSkill.Molotov, enemies[e], 8, Vector2.zero);
            }
            for (int i = soundWaves.Count - 1; i >= 0; i--)
            {
                var pulse = soundWaves[i]; float previous = pulse.radius;
                pulse.age += dt; pulse.radius = .3f + pulse.age * 5;
                var spriteSize = pulse.art.sprite.bounds.size;
                pulse.art.transform.localScale = new Vector3(pulse.radius * 2 / spriteSize.x, pulse.radius * 2 / spriteSize.y, 1);
                pulse.art.color = new Color(1, 1, 1, Mathf.Clamp01((1.5f - pulse.age) / .4f) * .8f);
                // Swept annulus: only the expanding ring hits, never the empty center.
                for (int e = enemies.Count - 1; e >= 0; e--)
                {
                    var enemy = enemies[e]; float distance = Vector2.Distance(enemy.Position, pulse.center);
                    if (distance < previous * .77f - .56f || distance > pulse.radius + .56f || !pulse.victims.Add(enemy)) continue;
                    Impact(SummonSkill.SoundWave, enemy, 24, (enemy.Position - pulse.center).normalized);
                }
                if (pulse.age >= 1.5f) { Destroy(pulse.art.gameObject); soundWaves.RemoveAt(i); }
            }
        }
        void ClearAreaSkills()
        {
            foreach (var bottle in bottles) if (bottle.art) Destroy(bottle.art.gameObject);
            foreach (var pulse in soundWaves) if (pulse.art) Destroy(pulse.art.gameObject);
            bottles.Clear(); fireZones.Clear(); soundWaves.Clear();
        }
    }
}
