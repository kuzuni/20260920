using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        sealed class Pursuer
        {
            public SpriteRenderer art, shadow;
            public Actor target;
            public float age, attackClock, punch;
            public bool golem, slamPending;
            public string ability;
            public readonly Dictionary<Actor, float> nextHit = new Dictionary<Actor, float>();
        }
        sealed class ClawStrike { public Actor target; public Vector2 position; public float clock = .24f; }
        sealed class MeteorFall { public SpriteRenderer art; public Vector2 start, end; public float age, trail, echo; public bool hand; }
        sealed class StoneVolley { public int remaining = 2, index = 1; public float clock = .18f; public bool requiresEquipment; }
        sealed class MeteorVolley { public int remaining = 2; public float clock = 1; public bool requiresEquipment, hand; }
        readonly List<MeteorVolley> meteorVolleys = new List<MeteorVolley>();
        sealed class LightningFlash { public SpriteRenderer art; public float age; }
        readonly List<LightningFlash> lightningFlashes = new List<LightningFlash>();
        readonly List<Pursuer> pursuers = new List<Pursuer>();
        readonly List<ClawStrike> clawStrikes = new List<ClawStrike>();
        readonly List<MeteorFall> meteors = new List<MeteorFall>();
        readonly List<StoneVolley> stoneVolleys = new List<StoneVolley>();
        public int TornadoHits { get; private set; }
        public int ClawHits { get; private set; }
        public int GolemHits { get; private set; }
        public int MeteorsLanded { get; private set; }
        public int MeteorsLaunched { get; private set; }
        public event System.Action<float> MeteorProjectileLaunched;
        public int MeteorHits { get; private set; }
        public int GolemsSummoned { get; private set; }

        void CastPursuer(bool golem, string ability = null)
        {
            int count = golem ? (ability == "FireGolem" ? 10 : 5) : 1;
            for (int i = 0; i < count; i++)
            {
                Vector2 position = player.Position + (golem ? Direction(i * Mathf.PI * 2 / count) * 1.5f : Vector2.zero);
                var sprite = ability != null ? (golem ? DoodleAscensionArt.FireGolem(0) : DoodleAscensionArt.Cell(3)) : DoodleExpansionArt.Get(golem ? "SkillGolem" : "SkillTornado");
                var art = Visual(ability ?? (golem ? "Summoned golem" : "Homing tornado"), sprite, position, Vector2.one * (golem ? 1.6f : 2.5f), 510);
                SpriteRenderer shadow = null;
                if (!golem) {
                    shadow = Visual("Tornado ground shadow", disc, position + Vector2.down * 1.1f, new Vector2(1.05f, .3f), -900);
                    shadow.color = new Color(.08f, .07f, .06f, .32f);
                }
                pursuers.Add(new Pursuer { art = art, shadow = shadow, target = NearbyTarget(position, i), golem = golem, ability = ability ?? (golem ? "Golem" : "Tornado") });
                if (golem) GolemsSummoned++;
            }
        }
        void CastDoubleClaw()
        {
            var targets = new List<Actor>(enemies);
            targets.Sort((a, b) => (a.Position - player.Position).sqrMagnitude.CompareTo((b.Position - player.Position).sqrMagnitude));
            for (int i = 0; i < Mathf.Min(5, targets.Count); i++)
            {
                var strike = new ClawStrike { target = targets[i], position = targets[i].Position };
                ClawHit(strike, 0); clawStrikes.Add(strike);
            }
        }
        void ClawHit(ClawStrike strike, int frame)
        {
            if (Alive(strike.target)) strike.position = strike.target.Position;
            Echo("Double claw strike " + frame, DoodleExpansionArt.Get("SkillDoubleClaw", frame), strike.position, Vector2.one * 2, Quaternion.identity, .22f, 1, 600);
            if (Alive(strike.target)) { SkillImpact(strike.target, 38, Vector2.zero, "DoubleClaw"); ClawHits++; }
        }
        void CastMeteor(bool hand = false)
        {
            LaunchMeteor(hand);
            meteorVolleys.Add(new MeteorVolley { requiresEquipment = castingEquippedSkill, hand = hand });
        }
        void LaunchMeteor(bool hand = false)
        {
            var target = Closest(player.Position);
            if (!Alive(target)) return;
            Vector2 end = target.Position;
            var start = end + new Vector2(hand ? 0 : 4, 10);
            var art = Visual(hand ? "Falling divine palm" : "Falling red meteor", hand ? DoodleAscensionArt.Cell(6) : DoodleExpansionArt.Get("SkillMeteorRock"), start, Vector2.one * (hand ? 3.8f : 2.3f), 650);
            meteors.Add(new MeteorFall { art = art, start = start, end = end, hand = hand });
            MeteorsLaunched++;
            MeteorProjectileLaunched?.Invoke(Time.fixedTime);
        }
        void TickExpansionSkills(float dt)
        {
            for (int i = meteorVolleys.Count - 1; i >= 0; i--) {
                var volley = meteorVolleys[i];
                if (volley.requiresEquipment && !SkillEquipped(volley.hand ? "GodHand" : "Meteor")) { meteorVolleys.RemoveAt(i); continue; }
                volley.clock -= dt;
                if (volley.clock > .0001f) continue;
                LaunchMeteor(volley.hand); volley.clock += 1;
                if (--volley.remaining == 0) meteorVolleys.RemoveAt(i);
            }
            for (int i = lightningFlashes.Count - 1; i >= 0; i--) {
                var flash = lightningFlashes[i]; flash.age += dt;
                if (flash.age >= .3f) { Destroy(flash.art.gameObject); lightningFlashes.RemoveAt(i); continue; }
                SetSpriteArt(flash.art, DoodleExpansionArt.Get("SkillLightning", flash.age < .15f ? 0 : 1));
            }
            for (int i = stoneVolleys.Count - 1; i >= 0; i--)
            {
                var volley = stoneVolleys[i];
                if (volley.requiresEquipment && !SkillEquipped("Stone")) { stoneVolleys.RemoveAt(i); continue; }
                volley.clock -= dt;
                if (volley.clock > .0001f) continue;
                LaunchStone(volley.index++); volley.clock += .18f;
                if (--volley.remaining == 0) stoneVolleys.RemoveAt(i);
            }
            for (int i = clawStrikes.Count - 1; i >= 0; i--)
            {
                var strike = clawStrikes[i]; strike.clock -= dt;
                if (strike.clock > 0) continue;
                ClawHit(strike, 1); clawStrikes.RemoveAt(i);
            }
            for (int i = pursuers.Count - 1; i >= 0; i--)
            {
                var unit = pursuers[i]; unit.age += dt; unit.attackClock -= dt; unit.punch -= dt;
                if (unit.age >= (unit.golem ? 10 : 7)) {
                    Destroy(unit.art.gameObject); if (unit.shadow) Destroy(unit.shadow.gameObject); pursuers.RemoveAt(i); continue;
                }
                Vector2 position = unit.art.transform.position;
                if (!Alive(unit.target)) unit.target = Closest(position);
                Vector2 direction = Alive(unit.target) ? unit.target.Position - position : Vector2.zero;
                if (unit.golem)
                {
                    if (Mathf.Abs(direction.x) > .01f) unit.art.flipX = direction.x < 0;
                    if (unit.punch <= 0 && direction.magnitude > 1.1f)
                        position = Vector2.MoveTowards(position, unit.target.Position, dt * 4.5f);
                    if (Alive(unit.target) && direction.magnitude <= 1.3f && unit.attackClock <= 0)
                    {
                        unit.punch = .38f; unit.attackClock = .65f; unit.slamPending = true;
                    }
                    if (unit.slamPending && unit.punch <= .18f) {
                        unit.slamPending = false;
                        if (unit.ability == "FireGolem") EmitCannonExplosion(position + direction.normalized * .65f);
                        EmitBurst(golemSlamParticles, position + direction.normalized * .65f + Vector2.down * .35f,
                            new Color(.72f, .64f, .5f, .85f), 22, .25f, .6f, 4.2f, .25f, .5f);
                        if (Alive(unit.target) && direction.magnitude <= 1.6f) {
                            SkillImpact(unit.target, 32, direction.normalized, unit.ability); GolemHits++;
                        }
                    }
                    int frame = unit.punch > .18f ? 2 : unit.punch > 0 ? 3 : (int)(unit.age * 7) % 2;
                    SetSpriteArt(unit.art, unit.ability == "FireGolem" ? DoodleAscensionArt.FireGolem(frame) : DoodleExpansionArt.Get("SkillGolem", frame));
                    unit.art.sortingOrder = Order(position) + 1;
                }
                else
                {
                    if (Alive(unit.target)) position = Vector2.MoveTowards(position, unit.target.Position, dt * 12);
                    if (unit.ability == "FireTornado") {
                        SetSpriteArt(unit.art, DoodleAscensionArt.FireTornado((int)(unit.age * 10) % 2));
                    } else SetSpriteArt(unit.art, DoodleExpansionArt.Get("SkillTornado", (int)(unit.age * 10) % 2));
                    for (int e = enemies.Count - 1; e >= 0; e--)
                    {
                        var enemy = enemies[e];
                        if ((enemy.Position - position).sqrMagnitude > 1.8f * 1.8f) continue;
                        if (unit.nextHit.TryGetValue(enemy, out float next) && next > unit.age) continue;
                        unit.nextHit[enemy] = unit.age + .18f;
                        SkillDamage(enemy, 18, Vector2.zero, unit.ability); TornadoHits++;
                    }
                }
                unit.art.transform.position = position;
                if (unit.shadow) unit.shadow.transform.position = position + Vector2.down * 1.1f;
            }
            for (int i = meteors.Count - 1; i >= 0; i--)
            {
                var meteor = meteors[i]; meteor.age += dt; meteor.echo -= dt;
                float t = Mathf.Clamp01(meteor.age / .85f);
                Vector2 position = Vector2.Lerp(meteor.start, meteor.end, t * t);
                Vector2 previous = meteor.art.transform.position;
                Vector2 direction = (meteor.end - meteor.start).normalized;
                meteor.art.transform.position = position;
                meteor.art.transform.rotation = Quaternion.Euler(0, 0, meteor.hand ? 0 : meteor.age * 720);
                // Emit by travelled distance, so acceleration leaves an unbroken world-space trail.
                if (!meteor.hand) {
                    float distance = Vector2.Distance(previous, position);
                    for (float step = .15f - meteor.trail; step <= distance; step += .15f)
                        EmitMeteorFlame(previous + direction * step, direction);
                    meteor.trail = (meteor.trail + distance) % .15f;
                }
                if (meteor.echo <= 0)
                {
                    Echo(meteor.hand ? "Divine palm afterimage" : "Meteor rock afterimage", meteor.hand ? DoodleExpansionArt.Get("SkillSkyPalm") : meteor.art.sprite, position, Vector2.one * (meteor.hand ? 3.8f : 2.3f), meteor.art.transform.rotation, meteor.hand ? .35f : .22f, meteor.hand ? .5f : .28f, 638);
                    meteor.echo += .05f;
                }
                if (t < 1) continue;
                if (meteor.hand) {
                    Echo("Divine palm impact echo", DoodleExpansionArt.Get("SkillSkyPalm"), meteor.end, Vector2.one * 4.5f, Quaternion.identity, .45f, .6f, 638);
                    Echo("Divine palm ground imprint", DoodleExpansionArt.Get("SkillPalmCrater"), meteor.end, new Vector2(4.5f, 3.5f), Quaternion.identity, 7, 1, -890);
                } else {
                    EmitBurst(meteorExplosionParticles, meteor.end, Color.white, 1, 5, 5, 0, .4f, .4f);
                    EmitBurst(meteorExplosionParticles, meteor.end, new Color(1, .65f, .35f), 20, .45f, 1, 5, .3f, .7f);
                    EmitBurst(dustParticles, meteor.end, new Color(.65f, .5f, .38f), 16, .5f, 1.1f, 3, .5f, .9f);
                    Echo("Meteor impact crater", DoodleExpansionArt.Get("SkillMeteorCrater"), meteor.end, Vector2.one * 4.5f, Quaternion.identity, 7, .9f, -890);
                }
                for (int e = enemies.Count - 1; e >= 0; e--)
                    if ((enemies[e].Position - meteor.end).sqrMagnitude <= (meteor.hand ? 4.2f * 4.2f : 3.2f * 3.2f))
                    { var enemy = enemies[e]; SkillDamage(enemy, 150, (enemy.Position - meteor.end).normalized, meteor.hand ? "GodHand" : "Meteor"); MeteorHits++; }
                MeteorsLanded++; Destroy(meteor.art.gameObject); meteors.RemoveAt(i);
            }
        }
        void LaunchStone(int index)
        {
            var target = NearbyTarget(player.Position, index);
            if (!Alive(target)) return;
            var sprite = Visual("Parabolic stone", sprites[6], player.Position, Vector2.one * .645f, 550);
            sprite.gameObject.AddComponent<CircleCollider2D>().isTrigger = true;
            shots.Add(new Shot { visual = sprite.transform, start = player.Position, end = target.Position, target = target, duration = .65f, stone = true });
            StonesLaunched++;
        }
        void ClearExpansionSkills()
        {
            foreach (var unit in pursuers) {
                if (unit.art) Destroy(unit.art.gameObject);
                if (unit.shadow) Destroy(unit.shadow.gameObject);
            }
            foreach (var flash in lightningFlashes) if (flash.art) Destroy(flash.art.gameObject);
            lightningFlashes.Clear();
            skillSplashCounts.Clear();
            foreach (var meteor in meteors) if (meteor.art) Destroy(meteor.art.gameObject);
            pursuers.Clear(); meteors.Clear(); meteorVolleys.Clear(); clawStrikes.Clear(); stoneVolleys.Clear();
            TornadoHits = ClawHits = GolemHits = GolemsSummoned = MeteorsLanded = MeteorsLaunched = MeteorHits = 0;
        }
        void ShowLightningStrike(Vector2 position)
        {
            lightningFlashes.Add(new LightningFlash {
                art = Visual("Direct lightning strike", DoodleExpansionArt.Get("SkillLightning"), position + Vector2.up * 1.5f, Vector2.one * 3, 580)
            });
        }
    }
}
