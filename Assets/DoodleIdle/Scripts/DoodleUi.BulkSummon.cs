using System;
using System.Collections.Generic;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        int summonResultMultiplier = 1;
        static readonly int[] summonMultipliers = { 1, 10, 100, 1000 };
        static bool ValidSummonCount(int count, bool ticketOnly = false)
        {
            foreach (int multiplier in summonMultipliers)
                if (count == 10 * multiplier || count == 50 * multiplier || ticketOnly && count == multiplier) return true;
            return false;
        }

        void AdvanceSummonExperience(SummonState state, int count)
        {
            if (IsRelicSummon(state.category) || state.level >= MaxSummonLevel) { state.experience = 0; return; }
            state.experience += count;
            while (state.level < MaxSummonLevel && state.experience >= CommerceExperienceNeeded(state)) {
                state.experience -= CommerceExperienceNeeded(state); state.level++;
            }
            if (state.level == MaxSummonLevel) state.experience = 0;
        }

        // Preview progression locally, then pay/grant/save once. Every roll uses its own level.
        List<UiItem> RollSummonRewards(string category, int count, System.Random rng)
        {
            var current = summonStates[category];
            var cursor = new SummonState { category = category, level = current.level, experience = current.experience };
            var items = Items(category);
            var pools = new List<UiItem>[GradeNames.Length];
            for (int grade = 0; grade < pools.Length; grade++) pools[grade] = items.FindAll(x => x.rarity == grade);
            var rewards = new List<UiItem>(count);
            int weightLevel = -1; int[] weights = null;
            for (int i = 0; i < count; i++) {
                UiItem item;
                if (IsRelicSummon(category)) item = items[rng.Next(items.Count)];
                else {
                    if (weightLevel != cursor.level) { weights = SummonWeights(category, cursor.level); weightLevel = cursor.level; }
                    int roll = rng.Next(SummonWeightTotal), grade = 0;
                    while (grade < weights.Length - 1 && roll >= weights[grade]) { roll -= weights[grade]; grade++; }
                    var choices = pools[grade];
                    if (choices.Count == 0) throw new InvalidOperationException("Missing summon grade for " + category);
                    int tierRoll = rng.Next(SummonTierWeightTotal(choices.Count)), choice = 0;
                    while (choice < choices.Count - 1 && tierRoll >= SummonTierWeight(choice, choices.Count)) { tierRoll -= SummonTierWeight(choice, choices.Count); choice++; }
                    item = choices[choice];
                }
                rewards.Add(item);
                AdvanceSummonExperience(cursor, 1);
            }
            return rewards;
        }

        string SummonGradeLabel(UiItem item)
        {
            int tier = item.tier > 0 ? item.tier : Items(item.category).FindAll(x => x.rarity == item.rarity).IndexOf(item) + 1;
            return (item.category == "Relic" ? "유물" : GradeNames[item.rarity]) + " " + tier;
        }
    }
}
