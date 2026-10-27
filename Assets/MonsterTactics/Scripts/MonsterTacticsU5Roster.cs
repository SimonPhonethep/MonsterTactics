using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>Shared U5 family, evolution and shop-cost data. Always sells first-stage copies in the shop.</summary>
    public static class MonsterTacticsU5Roster
    {
        public sealed class Family
        {
            public readonly string[] Forms;
            public readonly int Cost;
            public readonly string[] Types;
            public readonly string Role;
            public readonly int BaseHP;
            public readonly int[] Bestiary;
            public Family(int cost, string role, int hp, string[] forms, string[] types, int[] dex)
            {
                Cost = cost; Role = role; BaseHP = hp; Forms = forms; Types = types; Bestiary = dex;
            }
            public string Form(int stage) => Forms[Mathf.Clamp(stage, 1, 3) - 1];
            public string Type(int stage) => Types[Mathf.Clamp(stage, 1, 3) - 1];
        }

        public static readonly Family[] All =
        {
            new Family(1, "Tank", 750, new [] { "Pebblin", "Craggon", "Granitus" },
                new [] { "Rock / Ground", "Rock / Ground", "Rock / Ground" }, new [] { 74, 75, 76 }),
            new Family(1, "Ranged", 430, new [] { "Flarbit", "Scorbolt", "Flarevolt" },
                new [] { "Fire", "Fire", "Fire" }, new [] { 813, 814, 815 }),
            new Family(1, "Mage", 370, new [] { "Mysprit", "Mysteryn", "Mystarion" },
                new [] { "Psychic", "Psychic", "Psychic" }, new [] { 63, 64, 65 }),
            new Family(2, "Ranged", 420, new [] { "Mosslet", "Leafwing", "Thornveil" },
                new [] { "Grass / Flying", "Grass / Flying", "Grass / Ghost" }, new [] { 722, 723, 724 }),
            new Family(2, "Mage", 550, new [] { "Emberlet", "Cindrake", "Inferdrake" },
                new [] { "Fire", "Fire", "Fire / Flying" }, new [] { 4, 5, 6 }),
            new Family(2, "Tank", 750, new [] { "Budhorn", "Briarthorn", "Verdantor" },
                new [] { "Grass / Poison", "Grass / Poison", "Grass / Poison" }, new [] { 1, 2, 3 }),
            new Family(3, "Fighter", 650, new [] { "Ripplet", "Rillcrest", "Tideroyal" },
                new [] { "Water", "Water", "Water / Steel" }, new [] { 393, 394, 395 }),
            new Family(3, "Fighter", 680, new [] { "Tusklet", "Ironfang", "Stormtusk" },
                new [] { "Dragon", "Dragon", "Dragon" }, new [] { 610, 611, 612 }),
            new Family(4, "Assassin", 550, new [] { "Dunebit", "Shardfang", "Dunebreaker" },
                new [] { "Dragon / Ground", "Dragon / Ground", "Dragon / Ground" }, new [] { 443, 444, 445 }),
            new Family(4, "Assassin", 500, new [] { "Mistrip", "Mistveil", "Nightstream" },
                new [] { "Water", "Water", "Water / Dark" }, new [] { 656, 657, 658 }),
            new Family(5, "Mage", 900, new [] { "Voltrix", "Voltrix", "Voltrix" },
                new [] { "Electric", "Electric", "Electric" }, new [] { 243, 243, 243 }),
            new Family(5, "Fighter", 1050, new [] { "Pyrothane", "Pyrothane", "Pyrothane" },
                new [] { "Fire", "Fire", "Fire" }, new [] { 244, 244, 244 }),
            new Family(5, "Tank", 1200, new [] { "Aqualith", "Aqualith", "Aqualith" },
                new [] { "Water", "Water", "Water" }, new [] { 245, 245, 245 })
        };

        /// <summary>Roster offered to shop / AI in the current match. Legacy All is never mutated.</summary>
        public static Family[] ActiveFamilies => MonsterTacticsContentRosterRuntime.ActiveFamilies;

        private static readonly Dictionary<string, Family> ByForm = BuildIndex();
        private static Dictionary<string, Family> BuildIndex()
        {
            var output = new Dictionary<string, Family>(StringComparer.OrdinalIgnoreCase);
            foreach (Family family in All)
                foreach (string form in family.Forms)
                    if (!output.ContainsKey(form)) output.Add(form, family);
            return output;
        }
        public static Family Find(string form)
        {
            Family catalogFamily;
            if (MonsterTacticsContentRosterRuntime.TryFind(form, out catalogFamily)) return catalogFamily;
            // Resolve legacy forms for old scene starters / existing test units.
            Family oldFamily;
            return !string.IsNullOrEmpty(form) && ByForm.TryGetValue(form, out oldFamily) ? oldFamily : null;
        }
        public static string BaseName(string form)
        {
            Family family = Find(form);
            return family == null ? form : family.Forms[0];
        }
        public static int StageOf(string form)
        {
            Family family = Find(form);
            if (family == null) return 1;
            for (int i = 0; i < 3; i++) if (family.Forms[i] == form) return i + 1;
            return 1;
        }
        public static int MaxHP(Family family, int stage) =>
            Mathf.RoundToInt(family.BaseHP * (stage == 1 ? 1f : stage == 2 ? 1.62f : 2.50f));

        public static bool HasDistinctEvolution(Family family)
        {
            return family != null && (!string.Equals(family.Forms[0], family.Forms[1], StringComparison.OrdinalIgnoreCase)
                || !string.Equals(family.Forms[1], family.Forms[2], StringComparison.OrdinalIgnoreCase));
        }

        public static MonsterTacticsCombatRules.Stats CombatStats(MonsterUnit unit)
        {
            if (unit == null) return MonsterTacticsCombatRules.ForMonster("Unknown");
            Family family = Find(unit.MonsterName);
            MonsterTacticsCombatRules.Stats stats = MonsterTacticsCombatRules.ForMonster(
                family == null ? unit.MonsterName : family.Forms[0]);
            // Only the numeric base stats migrate here. Existing ability handlers
            // still use MonsterTacticsCombatRules until the ability/VFX migration.
            MonsterTacticsContentRosterRuntime.TryApplyStats(unit.MonsterName, ref stats);
            int stage = unit.GetComponent<MonsterTacticsU5Progression>()?.Stage ?? StageOf(unit.MonsterName);
            if (stage > 1)
            {
                stats.Attack *= stage == 2 ? 1.40f : 1.95f;
                stats.AttackSpeed *= stage == 2 ? 1.08f : 1.16f;
                stats.Armor = Mathf.RoundToInt(stats.Armor * (stage == 2 ? 1.25f : 1.55f));
                stats.MagicResist = Mathf.RoundToInt(stats.MagicResist * (stage == 2 ? 1.20f : 1.48f));
                stats.AbilityPower = stage == 2 ? 115f : 135f;
            }
            return stats;
        }
        // U6.2.1 — Base TFT-inspired shop odds. Each row totals 100%.
        // Tier 5 is reserved for future roster additions.
        private static readonly int[,] ShopOdds =
        {
            { 100, 0, 0, 0, 0 }, { 100, 0, 0, 0, 0 },
            { 75, 25, 0, 0, 0 }, { 55, 30, 15, 0, 0 },
            { 45, 33, 20, 2, 0 }, { 30, 40, 25, 5, 0 },
            { 19, 30, 40, 10, 1 }, { 15, 25, 32, 25, 3 },
            { 10, 20, 25, 35, 10 }, { 5, 10, 20, 40, 25 }
        };

        public static int[] TierWeights(int level)
        {
            int row = Mathf.Clamp(level, 1, 10) - 1;
            int[] weights = new int[5];
            for (int tier = 1; tier <= 5; tier++)
            {
                bool available = false;
                foreach (Family family in ActiveFamilies)
                    if (family.Cost == tier) { available = true; break; }
                if (available) weights[tier - 1] = ShopOdds[row, tier - 1];
                else
                {
                    // Redirect odds for unavailable higher tiers to the highest
                    // tier actually present in the roster. Never roll an empty list.
                    int fallback = 0;
                    foreach (Family family in ActiveFamilies)
                        if (family.Cost <= tier) fallback = Mathf.Max(fallback, family.Cost);
                    if (fallback > 0) weights[fallback - 1] += ShopOdds[row, tier - 1];
                }
            }
            return weights;
        }

        public static Family Roll(int level)
        {
            int[] odds = TierWeights(level);
            int dice = UnityEngine.Random.Range(0, 100);
            int tier = 1;
            for (int i = 0; i < odds.Length; i++)
            {
                dice -= odds[i];
                if (dice < 0) { tier = i + 1; break; }
            }
            var options = new List<Family>();
            foreach (Family family in ActiveFamilies)
                if (family.Cost == tier) options.Add(family);
            // Robust fallback for incomplete custom rosters.
            return options.Count > 0 ? options[UnityEngine.Random.Range(0, options.Count)] :
                ActiveFamilies[UnityEngine.Random.Range(0, ActiveFamilies.Length)];
        }

    }
}
