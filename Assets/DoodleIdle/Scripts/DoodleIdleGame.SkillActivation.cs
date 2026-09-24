using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        readonly Dictionary<string, float> equippedSkillClocks = new Dictionary<string, float>();
        readonly Dictionary<string, int> skillActivationCounts = new Dictionary<string, int>();
        readonly Dictionary<string, int> skillSplashCounts = new Dictionary<string, int>();
        public int SkillSplashCount(string ability) => skillSplashCounts.TryGetValue(ability, out var count) ? count : 0;
        readonly HashSet<string> equippedSkillAbilities = new HashSet<string>();
        bool castingEquippedSkill;
        float debugBananaRemaining;
        public bool CanTestSkill => Ready && !paused && player != null && enemies.Count > 0;
        public int SkillActivationCount(string ability) => skillActivationCounts.TryGetValue(ability, out var count) ? count : 0;

        bool SkillEquipped(string ability) => equippedSkillAbilities.Contains(ability);
        readonly List<UiItem> equippedSkillBuffer = new List<UiItem>(8);
        readonly List<string> removedSkillBuffer = new List<string>(8);
        void RefreshEquippedSkillAbilities()
        {
            equippedSkillAbilities.Clear();
            if (Ui) { Ui.FillEquippedItems("Skill", equippedSkillBuffer); foreach (var item in equippedSkillBuffer) equippedSkillAbilities.Add(item.ability); }
        }
        public float SkillInterval(string ability)
        {
            float ascension = AscensionInterval(ability); if (ascension > 0) return ascension;
            float variant = VariantInterval(ability); if (variant > 0) return variant;
            switch (ability)
            {
                case "Banana": return OrbitSkillCycle;
                case "Stone": return stoneInterval;
                case "Arrows": return arrowInterval;
                case "BouncyBall": return ballInterval;
                case "Fire": return fireInterval;
                case "Cannon": return cannonInterval;
                case "Shotgun": return shotgunInterval;
                case "Sound": return soundWaveInterval;
                case "TetherSnake": return tetherInterval;
                case "Cloud": return cloudInterval;
                case "Lightning": return cloudInterval;
                case "Molotov": return molotovInterval;
                case "Dragon": return dragonInterval;
                case "FireRing": return ringInterval;
                case "WaveSnakes": return snakeInterval;
                case "RedWave": return redWaveInterval;
                case "Tornado": return 10;
                case "DoubleClaw": return 5;
                case "Golem": return 13;
                case "Meteor": return 10;
                default: return 0;
            }
        }
        bool AutomaticSkillEnabled(string ability)
        {
            if (ability == "Banana" || ability == "Stone") return basicSkillsEnabled;
            if (ability == "Arrows" || ability == "BouncyBall" || ability == "Fire" || VariantInterval(ability) > 0) return extraSkillsEnabled;
            return summonSkillsEnabled;
        }
        void TickEquippedSkills(float dt)
        {
            RefreshEquippedSkillAbilities();
            var removed = removedSkillBuffer; removed.Clear();
            foreach (var pair in equippedSkillClocks) if (!SkillEquipped(pair.Key)) removed.Add(pair.Key);
            foreach (var ability in removed) equippedSkillClocks.Remove(ability);
            foreach (var ability in equippedSkillAbilities)
            {
                // Banana keeps its existing 8-second orbit / 4-second rest cycle below.
                if (ability == "Banana" || !AutomaticSkillEnabled(ability)) continue;
                if (!equippedSkillClocks.TryGetValue(ability, out var clock)) clock = .5f;
                clock -= dt;
                if (clock <= .0001f)
                {
                    castingEquippedSkill = true;
                    try { ActivateSkill(ability); }
                    finally { castingEquippedSkill = false; }
                    clock = Mathf.Max(.1f, SkillInterval(ability));
                }
                equippedSkillClocks[ability] = clock;
            }
        }

        // Explicit testing bypass: it neither equips/unlocks items nor changes saved progress/cooldowns.
        public bool DebugCastSkill(string itemId)
        {
            if (!CanTestSkill || !Ui) return false;
            var item = Ui.Items("Skill").Find(x => x.id == itemId);
            return item != null && ActivateSkill(item.ability);
        }
        bool ActivateSkill(string ability)
        {
            if (player == null || enemies.Count == 0 || SkillInterval(ability) <= 0) return false;
            switch (ability)
            {
                case "Banana": debugBananaRemaining = OrbitSkillLifetime; break;
                case "Stone": ThrowStones(); break;
                case "Arrows": CastExtraSkill(ExtraSkill.Arrows); break;
                case "BouncyBall": CastExtraSkill(ExtraSkill.BouncyBall); break;
                case "Fire": CastExtraSkill(ExtraSkill.Fire); break;
                case "Cannon": CastSummonSkill(SummonSkill.Cannon); break;
                case "Shotgun": CastSummonSkill(SummonSkill.Shotgun); break;
                case "Sound": CastSummonSkill(SummonSkill.SoundWave); break;
                case "TetherSnake": CastSummonSkill(SummonSkill.TetherSnake); break;
                case "Cloud": CastSummonSkill(SummonSkill.StormCloud); break;
                case "Lightning":
                    var targets = new List<Actor>(enemies);
                    targets.Sort((a, b) => (a.Position - player.Position).sqrMagnitude.CompareTo((b.Position - player.Position).sqrMagnitude));
                    for (int i = 0; i < Mathf.Min(3, targets.Count); i++)
                    {
                        var target = targets[i];
                        ShowLightningStrike(target.Position);
                        Impact(SummonSkill.StormCloud, target, 35, Vector2.down, "Lightning"); LightningStrikes++;
                    }
                    break;
                case "Molotov": CastSummonSkill(SummonSkill.Molotov); break;
                case "Dragon": CastSummonSkill(SummonSkill.Dragon); break;
                case "FireRing": CastSummonSkill(SummonSkill.FireRing); break;
                case "WaveSnakes": CastSummonSkill(SummonSkill.WaveSnakes); break;
                case "RedWave": CastSummonSkill(SummonSkill.RedWave); break;
                case "Tornado": CastPursuer(false); break;
                case "Golem": CastPursuer(true); break;
                case "DoubleClaw": CastDoubleClaw(); break;
                case "Meteor": CastMeteor(); break;
                default: if (AscensionInterval(ability) > 0) CastAscension(ability); else CastVariant(ability); break;
            }
            skillActivationCounts[ability] = SkillActivationCount(ability) + 1;
            return true;
        }
        void ResetSkillActivation()
        {
            equippedSkillClocks.Clear(); skillActivationCounts.Clear(); debugBananaRemaining = 0;
            castingEquippedSkill = false; RefreshEquippedSkillAbilities();
        }
    }
}
