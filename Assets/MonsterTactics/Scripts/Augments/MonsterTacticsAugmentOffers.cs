using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>Immutable per-match copy. Inspector changes do not alter an active match.</summary>
    public sealed class MonsterTacticsAugmentSnapshot
    {
        public readonly string Id, Name, Description, ExclusiveGroup;
        public readonly MonsterTacticsAugmentTier Tier;
        public readonly MonsterTacticsAugmentCategory Category;
        public readonly MonsterTacticsAugmentSlots Allowed;
        public readonly bool Enabled, AllowRepeat;
        public readonly int Weight, MinimumLevel, MinimumGold;
        public readonly string[] Conflicts, Prerequisites;
        public readonly MonsterTacticsAugmentRequirement[] Requirements;
        public readonly MonsterTacticsAugmentEffect[] Effects;
        public readonly MonsterTacticsAugmentStageVariant[] Variants;
        public readonly Sprite Icon, CardArtwork, CategoryBadge;
        public readonly bool UseCustomAccent, ShowGlow;
        public readonly Color AccentColor;
        public readonly float ArtworkOpacity;
        public readonly string PreferredTraitKey, PreferredFamilyKey;
        public readonly int PreferredBonusWeight, NextSelectionTierAdjustment;

        public MonsterTacticsAugmentSnapshot(MonsterTacticsAugmentDefinition d)
        {
            Id = (d.id ?? "").Trim(); Name = d.displayName ?? Id;
            Description = d.description ?? ""; Icon = d.icon;
            CardArtwork = d.cardArtwork; CategoryBadge = d.categoryBadge;
            UseCustomAccent = d.useCustomAccent; AccentColor = d.accentColor;
            ArtworkOpacity = Mathf.Clamp(d.artworkOpacity, 0f, 0.65f);
            ShowGlow = d.showGlow;
            Tier = d.tier; Category = d.category; Allowed = d.allowedSelections;
            Enabled = d.enabledInPool; Weight = d.offerWeight; MinimumLevel = d.minPlayerLevel;
            PreferredTraitKey = d.preferredTraitKey ?? "";
            PreferredFamilyKey = d.preferredFamilyKey ?? "";
            PreferredBonusWeight = Mathf.Max(0, d.preferredBonusWeight);
            NextSelectionTierAdjustment = Mathf.Clamp(d.nextSelectionTierAdjustment, -2, 2);
            MinimumGold = d.minGold; AllowRepeat = d.allowRepeatInMatch;
            ExclusiveGroup = (d.exclusiveGroup ?? "").Trim();
            Conflicts = d.conflictIds != null ? d.conflictIds.ToArray() : new string[0];
            Prerequisites = d.requiredAugmentIds != null ? d.requiredAugmentIds.ToArray() : new string[0];
            Requirements = CopyRequirements(d.requirements);
            Effects = CopyEffects(d.effects);
            Variants = CopyVariants(d.stageVariants);
        }
        private static MonsterTacticsAugmentRequirement[] CopyRequirements(List<MonsterTacticsAugmentRequirement> src)
        {
            if (src == null) return new MonsterTacticsAugmentRequirement[0];
            var a = new MonsterTacticsAugmentRequirement[src.Count];
            for (int i = 0; i < a.Length; i++)
                a[i] = src[i] == null ? null : new MonsterTacticsAugmentRequirement {
                    kind = src[i].kind, threshold = src[i].threshold, key = src[i].key
                };
            return a;
        }
        private static MonsterTacticsAugmentEffect[] CopyEffects(List<MonsterTacticsAugmentEffect> src)
        {
            if (src == null) return new MonsterTacticsAugmentEffect[0];
            var a = new MonsterTacticsAugmentEffect[src.Count];
            for (int i = 0; i < a.Length; i++)
                a[i] = src[i] == null ? null : new MonsterTacticsAugmentEffect {
                    kind = src[i].kind, trigger = src[i].trigger, rule = src[i].rule,
                    value = src[i].value, secondaryValue = src[i].secondaryValue,
                    tertiaryValue = src[i].tertiaryValue,
                    roundInterval = src[i].roundInterval,
                    intervalSeconds = src[i].intervalSeconds,
                    capPercent = src[i].capPercent,
                    item = src[i].item, delayRounds = src[i].delayRounds,
                    maxTriggers = src[i].maxTriggers, oncePerRound = src[i].oncePerRound
                };
            return a;
        }
        private static MonsterTacticsAugmentStageVariant[] CopyVariants(List<MonsterTacticsAugmentStageVariant> src)
        {
            if (src == null) return new MonsterTacticsAugmentStageVariant[0];
            var a = new MonsterTacticsAugmentStageVariant[src.Count];
            for (int i = 0; i < a.Length; i++)
                a[i] = src[i] == null ? null : new MonsterTacticsAugmentStageVariant {
                    slot = src[i].slot, descriptionOverride = src[i].descriptionOverride,
                    effectValueBonus = src[i].effectValueBonus
                };
            return a;
        }
        public MonsterTacticsAugmentStageVariant Variant(MonsterTacticsAugmentSlots slot)
        {
            foreach (var v in Variants) if (v != null && v.slot == slot) return v;
            return null;
        }
        public string DescriptionFor(MonsterTacticsAugmentSlots slot)
        {
            var v = Variant(slot);
            return v != null && !string.IsNullOrWhiteSpace(v.descriptionOverride) ? v.descriptionOverride : Description;
        }
        public bool EffectsSupported()
        {
            if (Effects.Length == 0) return false;
            foreach (var e in Effects)
                if (!MonsterTacticsAugmentEffectSupport.IsExecutable(e)) return false;
            return true;
        }
    }

    public sealed class MonsterTacticsAugmentOfferContext
    {
        public int PlayerLevel = 1, Gold;
        public readonly Dictionary<string, int> ActiveTraits =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> OwnedFamilies =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<MonsterTacticsAugmentSnapshot> Selected = new List<MonsterTacticsAugmentSnapshot>();
    }

    /// <summary>Pure offer eligibility and deterministic weighted sampling without replacement.</summary>
    public static class MonsterTacticsAugmentOffers
    {
        private static bool Contains(string[] items, string target)
        {
            foreach (string v in items)
                if (string.Equals((v ?? "").Trim(), target, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        public static bool Elidunebit(MonsterTacticsAugmentSnapshot d, MonsterTacticsAugmentSlots slot,
            MonsterTacticsAugmentTier tier, MonsterTacticsAugmentOfferContext ctx)
        {
            if (d == null || ctx == null || !d.Enabled || d.Weight <= 0 ||
                d.Tier != tier || !MonsterTacticsAugmentDefinition.HasSlot(d.Allowed, slot) ||
                !d.EffectsSupported() || ctx.PlayerLevel < d.MinimumLevel || ctx.Gold < d.MinimumGold)
                return false;
            foreach (var picked in ctx.Selected)
            {
                if (picked == null) continue;
                if (picked.Id == d.Id && !d.AllowRepeat) return false;
                if (Contains(d.Conflicts, picked.Id) || Contains(picked.Conflicts, d.Id)) return false;
                if (!string.IsNullOrEmpty(d.ExclusiveGroup) &&
                    string.Equals(d.ExclusiveGroup, picked.ExclusiveGroup, StringComparison.OrdinalIgnoreCase)) return false;
            }
            foreach (string required in d.Prerequisites)
            {
                bool present = false;
                foreach (var picked in ctx.Selected)
                    if (picked != null && string.Equals(picked.Id, (required ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                        present = true;
                if (!present) return false;
            }
            foreach (var r in d.Requirements)
            {
                if (r == null) return false;
                switch (r.kind)
                {
                    case MonsterTacticsAugmentRequirementKind.MinimumLevel:
                        if (ctx.PlayerLevel < r.threshold) return false; break;
                    case MonsterTacticsAugmentRequirementKind.MinimumGold:
                        if (ctx.Gold < r.threshold) return false; break;
                    case MonsterTacticsAugmentRequirementKind.ActiveTrait:
                        int count;
                        if (string.IsNullOrWhiteSpace(r.key) ||
                            !ctx.ActiveTraits.TryGetValue(r.key, out count) || count < r.threshold) return false;
                        break;
                    case MonsterTacticsAugmentRequirementKind.OwnedMonster:
                        if (string.IsNullOrWhiteSpace(r.key) || !ctx.OwnedFamilies.Contains(r.key)) return false;
                        break;
                    default: return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Deterministic weighted roll. Optional TFT-style diversity cap avoids three
        /// Economy/Traits/etc. cards together when alternatives exist. If the pool
        /// cannot satisfy the cap, it relaxes rather than rejecting an entire offer.
        /// Tailoring adds offer weight when a player's traits/families match, but does
        /// not turn a preferred trait into a hard eligibility requirement.
        /// </summary>
        public static List<MonsterTacticsAugmentSnapshot> Roll(IList<MonsterTacticsAugmentSnapshot> catalog,
            MonsterTacticsAugmentSlots slot, MonsterTacticsAugmentTier tier, MonsterTacticsAugmentOfferContext ctx,
            int count, System.Random rng, ISet<string> excluded = null,
            int maxSameCategory = 0, IList<MonsterTacticsAugmentSnapshot> otherVisible = null,
            bool allowTailoring = false)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var pool = new List<MonsterTacticsAugmentSnapshot>();
            if (catalog == null || count <= 0) return pool;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in catalog)
                if (d != null && seen.Add(d.Id) &&
                    (excluded == null || !excluded.Contains(d.Id)) && Elidunebit(d, slot, tier, ctx))
                    pool.Add(d);
            var output = new List<MonsterTacticsAugmentSnapshot>();
            var categoryCounts = new Dictionary<MonsterTacticsAugmentCategory, int>();
            if (otherVisible != null)
                foreach (var d in otherVisible)
                    if (d != null)
                    {
                        categoryCounts.TryGetValue(d.Category, out int prior);
                        categoryCounts[d.Category] = prior + 1;
                    }
            while (output.Count < count && pool.Count > 0)
            {
                var choices = new List<MonsterTacticsAugmentSnapshot>();
                foreach (var d in pool)
                {
                    categoryCounts.TryGetValue(d.Category, out int categoryCount);
                    if (maxSameCategory <= 0 || categoryCount < maxSameCategory)
                        choices.Add(d);
                }
                // No way to respect diversity: make a full offer instead of failing.
                if (choices.Count == 0) choices.AddRange(pool);
                long sum = 0;
                foreach (var d in choices) sum += EffectiveWeight(d, ctx, allowTailoring);
                if (sum <= 0) break;
                long ticket = (long)(rng.NextDouble() * sum);
                int choice = choices.Count - 1;
                for (int i = 0; i < choices.Count; i++)
                {
                    long weight = EffectiveWeight(choices[i], ctx, allowTailoring);
                    if (ticket < weight) { choice = i; break; }
                    ticket -= weight;
                }
                var chosen = choices[choice];
                output.Add(chosen);
                categoryCounts.TryGetValue(chosen.Category, out int existing);
                categoryCounts[chosen.Category] = existing + 1;
                pool.Remove(chosen);
            }
            return output;
        }

        /// <summary>
        /// Fail-soft offer selection. Keep every card elidunebit, enabled, supported
        /// and unique. Prefer the requested tier, then a complete alternative tier,
        /// then fill from other tiers. When fewer than three safe definitions exist,
        /// display only those that actually exist rather than skipping the round.
        /// </summary>
        public static List<MonsterTacticsAugmentSnapshot> RollWithFallback(
            IList<MonsterTacticsAugmentSnapshot> catalog, MonsterTacticsAugmentSlots slot,
            MonsterTacticsAugmentTier preferredTier, MonsterTacticsAugmentOfferContext ctx,
            int count, System.Random rng, int maxSameCategory = 0,
            bool allowTailoring = false, ISet<string> excluded = null,
            IList<MonsterTacticsAugmentSnapshot> otherVisible = null)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var preferred = Roll(catalog, slot, preferredTier, ctx, count, rng,
                excluded, maxSameCategory, otherVisible, allowTailoring);
            if (preferred.Count == count) return preferred;
            // Complete same-tier options take priority over mixed offerings.
            var tiers = new List<MonsterTacticsAugmentTier>();
            foreach (MonsterTacticsAugmentTier candidate in Enum.GetValues(typeof(MonsterTacticsAugmentTier)))
                if (candidate != preferredTier) tiers.Add(candidate);
            tiers.Sort((a,b) =>
                Math.Abs((int)a-(int)preferredTier).CompareTo(
                    Math.Abs((int)b-(int)preferredTier)));
            foreach (var tier in tiers)
            {
                var sameTier = Roll(catalog, slot, tier, ctx, count, rng,
                    excluded, maxSameCategory, otherVisible, allowTailoring);
                if (sameTier.Count == count) return sameTier;
            }
            // If no single tier has enough, preserve as many preferred-tier
            // cards as possible, then add DISTINCT elidunebit cards from others.
            var result = preferred;
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (excluded != null) foreach (string id in excluded) used.Add(id);
            foreach (var d in result) used.Add(d.Id);
            var visible = new List<MonsterTacticsAugmentSnapshot>();
            if (otherVisible != null) visible.AddRange(otherVisible);
            visible.AddRange(result);
            foreach (var tier in tiers)
            {
                if (result.Count >= count) break;
                var extras = Roll(catalog, slot, tier, ctx, count-result.Count,
                    rng, used, maxSameCategory, visible, allowTailoring);
                foreach (var d in extras)
                {
                    if (!used.Add(d.Id)) continue;
                    result.Add(d);
                    visible.Add(d);
                }
            }
            return result;
        }

        private static long EffectiveWeight(MonsterTacticsAugmentSnapshot d,
            MonsterTacticsAugmentOfferContext ctx, bool tailor)
        {
            long weight = Math.Max(0, d.Weight);
            if (!tailor || ctx == null || d.PreferredBonusWeight <= 0) return weight;
            int count;
            bool hasTrait = !string.IsNullOrEmpty(d.PreferredTraitKey) &&
                ctx.ActiveTraits.TryGetValue(d.PreferredTraitKey, out count) && count > 0;
            bool hasFamily = !string.IsNullOrEmpty(d.PreferredFamilyKey) &&
                ctx.OwnedFamilies.Contains(d.PreferredFamilyKey);
            return weight + (hasTrait || hasFamily ? d.PreferredBonusWeight : 0);
        }

    }
}
