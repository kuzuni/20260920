using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        sealed class AscensionVolley
        {
            public string ability;
            public Vector2 direction;
            public int index, remaining;
            public float clock;
            public bool requiresEquipment;
        }
        readonly List<AscensionVolley> ascensionVolleys = new List<AscensionVolley>();
        readonly Dictionary<string, int> ascensionLaunches = new Dictionary<string, int>();
        public int AscensionLaunchCount(string ability) => ascensionLaunches.TryGetValue(ability, out int count) ? count : 0;
        public int MissileRageExplosions { get; private set; }
        public event System.Action<string, float, Vector2> AscensionProjectileLaunched;
        public static float AscensionInterval(string ability)
        {
            switch (ability) {
                case "BladeRing": case "MissileRage": case "SolarVolley": return 7;
                case "FireGolem": return 13;
                case "CactusRage": case "RazorShuriken": return 6;
                case "FireTornado": case "GodHand": return 10;
                case "MightyDragon": return 11;
                case "SawSnakes": return 9;
                default: return 0;
            }
        }
        void RecordAscensionLaunch(string ability, Vector2 direction)
        {
            ascensionLaunches[ability] = AscensionLaunchCount(ability) + 1;
            AscensionProjectileLaunched?.Invoke(ability, Time.fixedTime, direction);
        }
        void CastAscension(string ability)
        {
            var direction = (Closest(player.Position).Position - player.Position).normalized;
            switch (ability) {
                case "FireGolem": CastPursuer(true, ability); break;
                case "FireTornado": CastPursuer(false, ability); break;
                case "GodHand": CastMeteor(true); break;
                case "MightyDragon": SpawnSnake(SummonSkill.Dragon, direction, false, ability, 1.7f, 5); break;
                case "SawSnakes":
                    for (int i = 0; i < 5; i++) SpawnSnake(SummonSkill.TetherSnake, Rotate(direction, (i - 2) * 36), true, ability, 1.1f, 8);
                    break;
                case "CactusRage": case "RazorShuriken":
                    int count = ability == "CactusRage" ? 4 : 18;
                    for (int i = 0; i < count; i++) {
                        bool cactus = ability == "CactusRage";
                        var aim = Rotate(direction, cactus ? (i - 1.5f) * 24 : i * 20);
                        var shot = VariantProjectile(cactus ? "Ascension_2" : "Ascension_4", player.Position, aim,
                            cactus ? 4.6f : 1.8f, cactus ? 6 : SoundWaveSpeed, cactus ? 3.2f : SoundWaveLifetime,
                            cactus ? 42 : 45, cactus ? 1.3f : .95f, cactus ? 0 : 900);
                        shot.ability = ability; shot.rolling = cactus; shot.afterimage = !cactus;
                        if (cactus) UpdateRollingVegetable(shot.art, shot.direction, 0, shot.size);
                        RecordAscensionLaunch(ability, aim);
                    }
                    break;
                default:
                    var volley = new AscensionVolley { ability = ability, direction = direction,
                        remaining = ability == "BladeRing" ? 22 : ability == "MissileRage" ? 13 : 3,
                        requiresEquipment = castingEquippedSkill };
                    FireAscensionVolley(volley); ascensionVolleys.Add(volley); break;
            }
        }
        void FireAscensionVolley(AscensionVolley volley)
        {
            if (volley.ability == "BladeRing") {
                var direction = Rotate(volley.direction, volley.index * (360f / 22));
                var shot = VariantProjectile("Ascension_0", player.Position, direction, 1.6f, 7, 2.2f, 30, .65f);
                shot.ability = volley.ability; shot.afterimage = true;
                RecordAscensionLaunch(volley.ability, direction);
            }
            else {
                var target = NearbyTarget(player.Position, volley.index);
                if (Alive(target)) {
                    bool solar = volley.ability == "SolarVolley";
                    Launch(solar ? ProjectileKind.Ball : ProjectileKind.Fire, target, player.Position, volley.index);
                    var shot = extraShots[extraShots.Count - 1]; shot.ability = volley.ability;
                    shot.size = solar ? 2.4f : 1.8f;
                    SetSpriteArt(shot.art, DoodleAscensionArt.Cell(solar ? 11 : 7));
                    shot.art.transform.localScale = Vector3.one * (solar ? 2.1f : 1.95f);
                    shot.art.name = volley.ability + " projectile";
                    RecordAscensionLaunch(volley.ability, (target.Position - player.Position).normalized);
                }
            }
            volley.index++; volley.remaining--; volley.clock += volley.ability == "SolarVolley" ? .2f : volley.ability == "BladeRing" ? .04f : .1f;
        }
        void TickAscension(float dt)
        {
            for (int i = ascensionVolleys.Count - 1; i >= 0; i--) {
                var volley = ascensionVolleys[i];
                if (volley.requiresEquipment && !SkillEquipped(volley.ability)) { ascensionVolleys.RemoveAt(i); continue; }
                volley.clock -= dt;
                if (volley.clock > .0001f) continue;
                FireAscensionVolley(volley);
                if (volley.remaining == 0) ascensionVolleys.RemoveAt(i);
            }
        }
        void ExplodeRageMissile(Vector2 position)
        {
            MissileRageExplosions++;
            EmitCannonExplosion(position);
            for (int i = enemies.Count - 1; i >= 0; i--) {
                var enemy = enemies[i];
                if (Vector2.Distance(enemy.Position, position) <= 2.4f)
                    SkillDamage(enemy, 55, (enemy.Position - position).normalized, "MissileRage");
            }
        }
        void ClearAscension()
        {
            ascensionVolleys.Clear(); ascensionLaunches.Clear(); MissileRageExplosions = 0;
        }
    }
}
