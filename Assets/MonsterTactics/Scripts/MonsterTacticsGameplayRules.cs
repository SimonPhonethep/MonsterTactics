using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    // Stable rule identifiers; augments will reference IDs rather than changing UI code.
    public enum MonsterTacticsRuleId
    {
        RerollCost, XPBuyCost, XPPerPurchase, BaseRoundIncome,
        InterestCap, InterestGoldStep, PvpWinGold, TeamSizeBonus,
        ShopTier1Weight, ShopTier2Weight, ShopTier3Weight,
        ShopTier4Weight, ShopTier5Weight
    }

    /// <summary>
    /// A per-match rule evaluator. Each source contributes one additive modifier per
    /// rule. Replacing/removing a source is idempotent; the baseline is never mutated.
    /// U6.9 ScriptableObject effects can register their own stable source IDs here.
    /// No augment is implemented/enabled in U6.8.7.
    /// </summary>
    public sealed class MonsterTacticsRuleBook
    {
        private readonly Dictionary<MonsterTacticsRuleId, Dictionary<string, int>> additions =
            new Dictionary<MonsterTacticsRuleId, Dictionary<string, int>>();
        public event Action Changed;

        public static int Default(MonsterTacticsRuleId id)
        {
            switch (id)
            {
                case MonsterTacticsRuleId.RerollCost: return 2;
                case MonsterTacticsRuleId.XPBuyCost: return 4;
                case MonsterTacticsRuleId.XPPerPurchase: return 4;
                case MonsterTacticsRuleId.BaseRoundIncome: return 5;
                case MonsterTacticsRuleId.InterestCap: return 5;
                case MonsterTacticsRuleId.InterestGoldStep: return 10;
                case MonsterTacticsRuleId.PvpWinGold: return 1;
                case MonsterTacticsRuleId.TeamSizeBonus: return 0;
                default: return 0;
            }
        }

        public int Get(MonsterTacticsRuleId id)
        {
            long result = Default(id);
            Dictionary<string, int> bySource;
            if (additions.TryGetValue(id, out bySource))
                foreach (int value in bySource.Values) result += value;
            switch (id)
            {
                case MonsterTacticsRuleId.RerollCost:
                case MonsterTacticsRuleId.XPBuyCost:
                case MonsterTacticsRuleId.BaseRoundIncome:
                case MonsterTacticsRuleId.PvpWinGold:
                case MonsterTacticsRuleId.InterestCap:
                case MonsterTacticsRuleId.TeamSizeBonus:
                    return (int)Math.Max(0L, Math.Min(999L, result));
                case MonsterTacticsRuleId.InterestGoldStep:
                case MonsterTacticsRuleId.XPPerPurchase:
                    return (int)Math.Max(1L, Math.Min(999L, result));
                default:
                    return (int)Math.Max(-100L, Math.Min(100L, result));
            }
        }

        public void SetAddition(string sourceId, MonsterTacticsRuleId id, int amount)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("Stable source ID required", nameof(sourceId));
            Dictionary<string, int> bySource;
            if (!additions.TryGetValue(id, out bySource))
                additions[id] = bySource = new Dictionary<string, int>(StringComparer.Ordinal);
            if (amount == 0)
            {
                if (bySource.Remove(sourceId)) Changed?.Invoke();
                return;
            }
            int previous;
            if (bySource.TryGetValue(sourceId, out previous) && previous == amount) return;
            bySource[sourceId] = amount;
            Changed?.Invoke();
        }

        public void RemoveSource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            bool any = false;
            foreach (var entry in additions) any |= entry.Value.Remove(sourceId);
            if (any) Changed?.Invoke();
        }
        public void Clear()
        {
            additions.Clear();
            Changed?.Invoke();
        }

        public int InterestForGold(int gold)
        {
            return Math.Min(Get(MonsterTacticsRuleId.InterestCap),
                Math.Max(0, gold) / Get(MonsterTacticsRuleId.InterestGoldStep));
        }

        public int GoldToNextInterest(int gold)
        {
            if (InterestForGold(gold) >= Get(MonsterTacticsRuleId.InterestCap)) return 0;
            int step = Get(MonsterTacticsRuleId.InterestGoldStep);
            return step - Math.Max(0, gold) % step;
        }

        // A zero-modifier call returns the existing TFT-inspired roster percentages
        // exactly. Nonzero tier modifiers change relative weights, then normalize
        // deterministically to a 100% distribution for both gameplay and HUD.
        public int[] ShopOdds(int[] baseline)
        {
            if (baseline == null || baseline.Length != 5) throw new ArgumentException("Five-tier odds required");
            var values = new int[5];
            int total = 0;
            for (int i = 0; i < 5; i++)
            {
                values[i] = Math.Max(0, baseline[i] + Get((MonsterTacticsRuleId)((int)MonsterTacticsRuleId.ShopTier1Weight + i)));
                total += values[i];
            }
            if (total <= 0) return (int[])baseline.Clone();
            if (total == 100) return values;
            var output = new int[5];
            int placed = 0;
            for (int i = 0; i < 5; i++)
            {
                output[i] = (int)((long)values[i] * 100 / total);
                placed += output[i];
            }
            // Largest remainder, stable by tier for ties.
            while (placed < 100)
            {
                int best = 0;
                long bestRem = -1;
                for (int i = 0; i < 5; i++)
                {
                    long rem = (long)values[i] * 100 - (long)output[i] * total;
                    if (rem > bestRem) { bestRem = rem; best = i; }
                }
                output[best]++;
                placed++;
            }
            return output;
        }
    }

    public enum MonsterTacticsGameplayEventKind
    {
        PhaseChanged, PlanningStarted, CombatStarted, RoundResolved,
        GoldChanged, ShopRerolled, XPGranted, UnitPurchased, UnitSold,
        UnitEvolved, ItemGranted, ItemCrafted, ItemEquipped, ItemToolUsed,
        FreeRerollsGranted, AbilityCast, UnitFainted,
        // Added at tail to preserve existing Unity enum serialization.
        PlayerDamaged, PlayerLeveledUp
    }

    public struct MonsterTacticsGameplayEvent
    {
        public MonsterTacticsGameplayEventKind Kind;
        public string Source;
        public string UnitName;
        public string StageRound;
        public int Amount;
        public MonsterTacticsGameplayEvent(MonsterTacticsGameplayEventKind kind, string source,
            int amount, string unitName, string stageRound)
        {
            Kind = kind; Source = source; Amount = amount;
            UnitName = unitName; StageRound = stageRound;
        }
    }

    /// <summary>Scoped to the owner's match; no global/static subscriptions.</summary>
    public sealed class MonsterTacticsGameplayEvents
    {
        public event Action<MonsterTacticsGameplayEvent> Raised;
        public void Publish(MonsterTacticsGameplayEvent evt) => Raised?.Invoke(evt);
    }
}
