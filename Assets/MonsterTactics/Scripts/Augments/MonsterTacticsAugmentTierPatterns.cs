using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>One weighted three-choice rarity pattern shared by an entire match.</summary>
    [Serializable]
    public sealed class MonsterTacticsAugmentTierPattern
    {
        public MonsterTacticsAugmentTier first = MonsterTacticsAugmentTier.Rookie;
        public MonsterTacticsAugmentTier second = MonsterTacticsAugmentTier.Ace;
        public MonsterTacticsAugmentTier final = MonsterTacticsAugmentTier.Champion;
        [Min(0)] public int weight = 10;
    }

    /// <summary>Pure deterministic match pattern roller. Zero-weight entries are excluded.</summary>
    public static class MonsterTacticsAugmentTierPatterns
    {
        public static MonsterTacticsAugmentTier[] Roll(IList<MonsterTacticsAugmentTierPattern> patterns, int seed)
        {
            long sum = 0;
            if (patterns != null)
                foreach (var p in patterns) if (p != null) sum += Math.Max(0, p.weight);
            if (sum <= 0L)
                return new[] { MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentTier.Ace, MonsterTacticsAugmentTier.Champion };
            var rng = new System.Random(seed);
            long choice = (long)(rng.NextDouble() * sum);
            foreach (var p in patterns)
            {
                if (p == null || p.weight <= 0) continue;
                if (choice < p.weight)
                    return new[] { p.first, p.second, p.final };
                choice -= p.weight;
            }
            return new[] { MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentTier.Ace, MonsterTacticsAugmentTier.Champion };
        }
    }
}
