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
            public Vector2 center, direction;
            public float age, radius;
            public readonly HashSet<Actor> victims = new HashSet<Actor>();
        }
        readonly List<Bottle> bottles = new List<Bottle>();
        readonly List<FireZone> fireZones = new List<FireZone>();
        readonly List<SoundPulse> soundWaves = new List<SoundPulse>();
        sealed class SoundVolley { public Vector2 direction; public int remaining = 4; public float clock = SoundWaveShotGap; }
        readonly List<SoundVolley> soundVolleys = new List<SoundVolley>();
        public int SoundWavesLaunched { get; private set; }
        public event System.Action<float> SoundWaveLaunched;
        public const float SoundWaveShotGap = .18f, SoundWaveSpeed = 7, SoundWaveLifetime = 1.5f;
        public int ActiveFireZones => fireZones.Count;
        public const float FireZoneLifetime = 4, FireZoneRadius = 2.3f;

        void ThrowMolotov(Vector2 origin, Vector2 target)
        {
            bottles.Add(new Bottle { start = origin, end = target,
                art = Visual("Molotov airborne bottle", summonArt["Molotov"], origin, Vector2.one * 1.1f, 540) });
        }
        void StartSoundVolley(Vector2 direction)
        {
            LaunchSoundWave(direction);
            soundVolleys.Add(new SoundVolley { direction = direction });
        }
        void LaunchSoundWave(Vector2 direction)
        {
            Vector2 origin = player.Position;
            soundWaves.Add(new SoundPulse { center = origin, direction = direction, radius = .35f,
                art = Visual("Traveling sound wave", summonArt["SoundWave"], origin, Vector2.one * .7f, 480) });
            SoundWavesLaunched++;
            SoundWaveLaunched?.Invoke(Time.fixedTime);
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
            for (int i = soundVolleys.Count - 1; i >= 0; i--)
            {
                var volley = soundVolleys[i]; volley.clock -= dt;
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
                var pulse = soundWaves[i]; float previousRadius = pulse.radius;
                Vector2 previousCenter = pulse.center;
                pulse.age += dt; pulse.radius = .35f + pulse.age * .8f;
                pulse.center += pulse.direction * (SoundWaveSpeed * dt);
                pulse.art.transform.position = pulse.center;
                var spriteSize = pulse.art.sprite.bounds.size;
                pulse.art.transform.localScale = new Vector3(pulse.radius * 2 / spriteSize.x, pulse.radius * 2 / spriteSize.y, 1);
                pulse.art.color = new Color(1, 1, 1, Mathf.Clamp01((SoundWaveLifetime - pulse.age) / .4f) * .8f);
                // Sweep the moving ring between physics steps, excluding its empty inner region.
                for (int e = enemies.Count - 1; e >= 0; e--)
                {
                    var enemy = enemies[e];
                    float outerDistance = SegmentDistance(enemy.Position, previousCenter, pulse.center);
                    float innerDistance = Mathf.Max(Vector2.Distance(enemy.Position, previousCenter), Vector2.Distance(enemy.Position, pulse.center));
                    if (outerDistance > pulse.radius + .56f || innerDistance < previousRadius * .77f - .56f || !pulse.victims.Add(enemy)) continue;
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
