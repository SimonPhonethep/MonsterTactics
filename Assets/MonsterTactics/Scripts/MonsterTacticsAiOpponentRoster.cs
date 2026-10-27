using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.5: Single-player training opponents. Each regular round has an independently
    /// generated trainer and roster, NEVER a copy of the player's team. This is not
    /// yet the persistent, 8-player/shared-pool simulation planned for U7.
    /// Fixed seeds make test rounds repeatable and do not touch Unity's RNG state.
    /// </summary>
    public static class MonsterTacticsAiOpponentRoster
    {
        public struct UnitSpec
        {
            public MonsterTacticsU5Roster.Family Family;
            public int Stars;
            public Vector2Int Hex;
        }

        public sealed class Team
        {
            public string Trainer;
            public string Strategy;
            public int Level;
            public UnitSpec[] Units;
        }

        private static readonly string[] Names =
        {
            "Scout Rowan", "Trainer Nova", "Ace Kai", "Trainer Iris",
            "Veteran Ashen", "Ace Mira", "Trainer Ellis", "Rival Sage"
        };

        private static readonly string[] Styles =
        {
            "Balanced", "Bulky", "Casters", "Rushdown", "Backline", "Ambush"
        };

        private static readonly int[] CenterColumns = { 3, 4, 2, 5, 1, 6, 0, 7 };

        public static string TrainerName(int stage, int round) =>
            Names[(Mathf.Clamp(stage, 2, 7) * 7 + Mathf.Clamp(round, 1, 6) * 3) % Names.Length];

        public static bool IsRegularOpponentRound(int stage, int round) =>
            stage >= 2 && stage <= 7 && round >= 1 && round <= 6;

        public static Team Build(int stage, int round)
        {
            stage = Mathf.Clamp(stage, 2, 7);
            round = Mathf.Clamp(round, 1, 6);
            int level = Mathf.Clamp(stage + 1 + (round >= 4 ? 1 : 0), 3, 10);
            // Every regular opponent has a personality; changing rounds changes
            // the squad even when the player's shop and board don't change.
            int seed = unchecked(71417 + stage * 4079 + round * 1013);
            var rng = new System.Random(seed);
            int style = (stage * 3 + round * 5) % Styles.Length;
            var slots = new List<UnitSpec>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int front = 0;
            int back = 0;
            int middle = 0;
            int defenders = Mathf.Clamp(Mathf.CeilToInt(level * .32f), 1, 3);

            for (int i = 0; i < level; i++)
            {
                string desired = ChooseRole(i, defenders, style);
                MonsterTacticsU5Roster.Family family = ChooseFamily(rng, level, desired, seen);
                if (family == null) continue;
                seen.Add(family.Forms[0]);
                int stars = PickStars(rng, stage, round, family);
                bool frontline = i < defenders;
                bool assassin = family.Role == "Assassin";
                // Independent role-aware team placement. Player half = rows 0–3.
                // Enemy melee starts on row 5; ranged behind them on row 7.
                int row, col;
                if (frontline) { row = 5; col = CenterColumns[(front++) % 8]; }
                else if (assassin) { row = 6; col = CenterColumns[(middle++) % 8]; }
                else { row = 7; col = CenterColumns[(back++) % 8]; }
                slots.Add(new UnitSpec { Family = family, Stars = stars, Hex = new Vector2Int(col, row) });
            }
            return new Team
            {
                Trainer = TrainerName(stage, round),
                Strategy = Styles[style],
                Level = level,
                Units = slots.ToArray()
            };
        }

        private static string ChooseRole(int i, int defenders, int style)
        {
            if (i < defenders) return style == 3 ? "Fighter" : "Tank";
            switch (style)
            {
                case 1: return i % 3 == 0 ? "Tank" : "Ranged";
                case 2: return i % 3 == 0 ? "Ranged" : "Mage";
                case 3: return i % 2 == 0 ? "Assassin" : "Fighter";
                case 4: return i % 3 == 0 ? "Mage" : "Ranged";
                case 5: return i % 2 == 0 ? "Assassin" : "Mage";
                default: return i % 3 == 0 ? "Mage" : i % 3 == 1 ? "Ranged" : "Fighter";
            }
        }

        private static MonsterTacticsU5Roster.Family ChooseFamily(System.Random rng, int level,
            string desired, HashSet<string> seen)
        {
            int[] odds = MonsterTacticsU5Roster.TierWeights(level);
            int roll = rng.Next(100);
            int cost = 1;
            for (int c = 0; c < odds.Length; c++)
            {
                roll -= odds[c];
                if (roll < 0) { cost = c + 1; break; }
            }
            var choices = new List<MonsterTacticsU5Roster.Family>();
            foreach (var family in MonsterTacticsU5Roster.ActiveFamilies)
                if (family.Cost == cost) choices.Add(family);
            // A fresh species of the desired role wins if the tier supports it;
            // otherwise permit another role rather than invent inaccessible costs.
            MonsterTacticsU5Roster.Family best = null;
            double top = double.MinValue;
            foreach (var family in choices)
            {
                double score = rng.NextDouble();
                if (family.Role == desired) score += 4;
                if (desired == "Tank" && family.Role == "Fighter") score += 2.5;
                if (desired == "Fighter" && family.Role == "Tank") score += 2;
                if (desired == "Ranged" && family.Role == "Mage") score += 1;
                if (!seen.Contains(family.Forms[0])) score += 2.5;
                if (score > top) { best = family; top = score; }
            }
            return best;
        }

        private static int PickStars(System.Random rng, int stage, int round,
            MonsterTacticsU5Roster.Family family)
        {
            // No AI upgrades at Stage 2. Upgrades become more common in later stages,
            // but 3-star units remain rare, limited to low-cost families late-game.
            if (stage <= 2) return 1;
            if (stage >= 7 && round >= 4 && family.Cost <= 2 && rng.NextDouble() < .12)
                return 3;
            float chance = Mathf.Clamp01((stage - 2) * .125f + (round >= 4 ? .06f : 0f));
            if (family.Cost >= 5) chance *= .5f;
            return rng.NextDouble() < chance ? 2 : 1;
        }
    }
}
