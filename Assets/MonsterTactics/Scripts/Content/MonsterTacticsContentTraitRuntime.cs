using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.1f.2: opt-in, immutable-per-match trait authoring data.
    /// Battle logic continues to use MonsterTacticsTraits. This class only supplies
    /// the selected definitions and effect values; no second damage pipeline.
    /// </summary>
    public static class MonsterTacticsContentTraitRuntime
    {
        private const string ResourcePath = "Content/MonsterTacticsContentCatalog";

        private sealed class Tier
        {
            internal int Needed;
            internal readonly List<MonsterTacticsContentEffectRecord> Effects =
                new List<MonsterTacticsContentEffectRecord>();
        }
        private sealed class Entry
        {
            internal bool MatchOnly;
            internal readonly List<Tier> Tiers = new List<Tier>();
        }
        private sealed class Snapshot
        {
            internal bool Enabled;
            internal MonsterTacticsTraits.Definition[] Definitions = MonsterTacticsTraits.LegacyAll;
            internal readonly Dictionary<string, Entry> Traits =
                new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            internal readonly Dictionary<string, string[]> Origins =
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }

        private static Snapshot snapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnNewPlaySession() { snapshot = null; }

        private static Snapshot Current
        {
            get
            {
                // Re-read authoring edits outside Play Mode. During a running match,
                // use a deep-copy snapshot so Inspector edits cannot alter combat.
                if (!Application.isPlaying || snapshot == null) snapshot = Build();
                return snapshot;
            }
        }

        public static bool IsUsingCatalog { get { return Current.Enabled; } }
        public static MonsterTacticsTraits.Definition[] ActiveDefinitions
        {
            get { return Current.Definitions; }
        }

        public static bool IsEnabledTrait(string name)
        {
            Snapshot s = Current;
            return !s.Enabled || (!String.IsNullOrEmpty(name) && s.Traits.ContainsKey(name));
        }

        public static bool AppliesOnlyToMatching(string name)
        {
            Snapshot s = Current;
            Entry entry;
            return !s.Enabled || !s.Traits.TryGetValue(name ?? "", out entry) || entry.MatchOnly;
        }

        /// <summary>True if a catalog-owned family was found, including one with no origins.</summary>
        public static bool TryOrigins(string baseName, out string[] origins)
        {
            origins = null;
            Snapshot s = Current;
            return s.Enabled && !String.IsNullOrEmpty(baseName) &&
                s.Origins.TryGetValue(baseName, out origins);
        }

        public static int ActiveTier(string name, Dictionary<string, int> counts)
        {
            Snapshot s = Current;
            Entry entry;
            int count;
            if (!s.Enabled || !s.Traits.TryGetValue(name ?? "", out entry) ||
                counts == null || !counts.TryGetValue(name, out count)) return 0;
            int tier = 0;
            for (int i = 0; i < entry.Tiers.Count; i++)
                if (count >= entry.Tiers[i].Needed) tier = i + 1;
            return tier;
        }

        /// <summary>
        /// Single effective tier per trait, never cumulative lower tiers. Each
        /// active effect modifies the same MonsterTacticsTraits.Bonus used in combat.
        /// </summary>
        public static MonsterTacticsTraits.Bonus BonusForUnit(string[] ownedTraits,
            Dictionary<string, int> counts)
        {
            MonsterTacticsTraits.Bonus result = MonsterTacticsTraits.Bonus.Empty;
            Snapshot s = Current;
            if (!s.Enabled || counts == null) return result;
            var owned = new HashSet<string>(ownedTraits ?? new string[0], StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Entry> record in s.Traits)
            {
                if (record.Value.MatchOnly && !owned.Contains(record.Key)) continue;
                int tier = ActiveTier(record.Key, counts);
                if (tier <= 0) continue;
                foreach (MonsterTacticsContentEffectRecord effect in record.Value.Tiers[tier - 1].Effects)
                    Apply(ref result, effect);
            }
            return result;
        }

        /// <summary>Pure evaluator for validation and future editor preview panels.</summary>
        public static MonsterTacticsTraits.Bonus EvaluateEffects(
            IList<MonsterTacticsContentEffectRecord> effects)
        {
            MonsterTacticsTraits.Bonus bonus = MonsterTacticsTraits.Bonus.Empty;
            if (effects != null)
                foreach (MonsterTacticsContentEffectRecord effect in effects)
                    if (effect != null) Apply(ref bonus, effect);
            return bonus;
        }

        private static void Apply(ref MonsterTacticsTraits.Bonus b, MonsterTacticsContentEffectRecord e)
        {
            float multiplier = Mathf.Max(0f, 1f + e.value);
            switch (e.stat)
            {
                case MonsterTacticsContentEffectStat.AttackMultiplier:
                    b.AttackMultiplier *= multiplier; break;
                case MonsterTacticsContentEffectStat.AttackSpeedMultiplier:
                    b.AttackSpeedMultiplier *= multiplier; break;
                case MonsterTacticsContentEffectStat.AbilityPowerFlat:
                    b.AbilityPowerBonus += e.value; break;
                case MonsterTacticsContentEffectStat.MaxHpMultiplier:
                    b.HpMultiplier *= multiplier; break;
                case MonsterTacticsContentEffectStat.DamageMultiplier:
                    b.DamageMultiplier *= multiplier; break;
                case MonsterTacticsContentEffectStat.IncomingDamageMultiplier:
                    b.IncomingDamageMultiplier *= multiplier; break;
                case MonsterTacticsContentEffectStat.ManaGainMultiplier:
                    b.ManaGainMultiplier *= multiplier; break;
                case MonsterTacticsContentEffectStat.CritChanceFlat:
                    b.CritChanceBonus += e.value; break;
                case MonsterTacticsContentEffectStat.ArmorFlat:
                    b.ArmorBonus += Mathf.RoundToInt(e.value); break;
                case MonsterTacticsContentEffectStat.MagicResistFlat:
                    b.MagicResistBonus += Mathf.RoundToInt(e.value); break;
            }
        }

        private static string Describe(Tier tier, string legacyDescription)
        {
            if (tier.Effects.Count == 0) return legacyDescription ?? "No effect";
            var parts = new List<string>();
            foreach (MonsterTacticsContentEffectRecord e in tier.Effects)
            {
                string number = (e.value >= 0f ? "+" : "") + e.value.ToString("0.##");
                string percent = (e.value >= 0f ? "+" : "") + (e.value * 100f).ToString("0.#") + "%";
                switch (e.stat)
                {
                    case MonsterTacticsContentEffectStat.AttackMultiplier: parts.Add(percent + " AD"); break;
                    case MonsterTacticsContentEffectStat.AttackSpeedMultiplier: parts.Add(percent + " Attack Speed"); break;
                    case MonsterTacticsContentEffectStat.AbilityPowerFlat: parts.Add(number + " AP"); break;
                    case MonsterTacticsContentEffectStat.MaxHpMultiplier: parts.Add(percent + " Max HP"); break;
                    case MonsterTacticsContentEffectStat.DamageMultiplier: parts.Add(percent + " damage dealt"); break;
                    case MonsterTacticsContentEffectStat.IncomingDamageMultiplier: parts.Add(percent + " incoming damage"); break;
                    case MonsterTacticsContentEffectStat.ManaGainMultiplier: parts.Add(percent + " mana gained"); break;
                    case MonsterTacticsContentEffectStat.CritChanceFlat: parts.Add(percent + " Crit Chance"); break;
                    case MonsterTacticsContentEffectStat.ArmorFlat: parts.Add(number + " Armor"); break;
                    case MonsterTacticsContentEffectStat.MagicResistFlat: parts.Add(number + " MR"); break;
                }
            }
            return String.Join(", ", parts.ToArray());
        }

        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        private static bool IsMultiplier(MonsterTacticsContentEffectStat stat)
        {
            return stat == MonsterTacticsContentEffectStat.AttackMultiplier ||
                stat == MonsterTacticsContentEffectStat.AttackSpeedMultiplier ||
                stat == MonsterTacticsContentEffectStat.MaxHpMultiplier ||
                stat == MonsterTacticsContentEffectStat.DamageMultiplier ||
                stat == MonsterTacticsContentEffectStat.IncomingDamageMultiplier ||
                stat == MonsterTacticsContentEffectStat.ManaGainMultiplier;
        }
        private static Snapshot Invalid(string message)
        {
            Debug.LogError("[U6.9.1f.2] Live traits invalid. Falling back to legacy traits: " + message);
            return new Snapshot();
        }
        private static Snapshot Build()
        {
            var result = new Snapshot();
            var catalog = Resources.Load<MonsterTacticsContentCatalog>(ResourcePath);
            if (catalog == null || !catalog.enableLiveTraits) return result;
            if (catalog.traits == null || catalog.monster == null)
                return Invalid("Trait or Monster records missing");

            var definitions = new List<MonsterTacticsTraits.Definition>();
            foreach (MonsterTacticsTraitRecord trait in catalog.traits)
            {
                if (trait == null || !trait.enabled) continue;
                if (String.IsNullOrWhiteSpace(trait.displayName) ||
                    result.Traits.ContainsKey(trait.displayName) || trait.tiers == null)
                    return Invalid("Duplicate/incomplete trait: " + (trait == null ? "null" : trait.id));
                var entry = new Entry { MatchOnly = trait.matchingUnitsOnly };
                var breakpoints = new List<int>();
                var descriptions = new List<string>();
                int prior = 0;
                foreach (MonsterTacticsTraitTierRecord record in trait.tiers)
                {
                    if (record == null || record.uniqueFamiliesRequired <= prior ||
                        record.effects == null) return Invalid("Invalid breakpoints: " + trait.displayName);
                    var tier = new Tier { Needed = record.uniqueFamiliesRequired };
                    prior = record.uniqueFamiliesRequired;
                    foreach (MonsterTacticsContentEffectRecord effect in record.effects)
                    {
                        if (effect == null || !Finite(effect.value) ||
                            (IsMultiplier(effect.stat) && effect.value < -1f))
                            return Invalid("Invalid effect: " + trait.displayName);
                        // Deep copy each record to ensure no mid-match asset edits leak in.
                        tier.Effects.Add(new MonsterTacticsContentEffectRecord
                        {
                            stat = effect.stat, value = effect.value,
                            vfxPresetId = effect.vfxPresetId
                        });
                    }
                    entry.Tiers.Add(tier);
                    breakpoints.Add(tier.Needed);
                    descriptions.Add(Describe(tier, record.description));
                }
                result.Traits.Add(trait.displayName, entry);
                definitions.Add(new MonsterTacticsTraits.Definition(trait.displayName,
                    trait.isOrigin, breakpoints.ToArray(), descriptions.ToArray()));
            }
            if (definitions.Count == 0) return Invalid("No enabled traits");
            // Origins are authored on family records; this also supports new families.
            foreach (MonsterTacticsMonsterRecord monster in catalog.monster)
            {
                if (monster == null || monster.forms == null || monster.forms.Count < 1 ||
                    monster.forms[0] == null || String.IsNullOrWhiteSpace(monster.forms[0].name)) continue;
                var originNames = new List<string>();
                if (monster.origins != null)
                    foreach (string origin in monster.origins)
                    {
                        if (String.IsNullOrWhiteSpace(origin)) continue;
                        Entry entry;
                        if (!result.Traits.TryGetValue(origin, out entry)) continue;
                        if (!originNames.Contains(origin)) originNames.Add(origin);
                    }
                result.Origins[monster.forms[0].name] = originNames.ToArray();
            }
            result.Definitions = definitions.ToArray();
            result.Enabled = true;
            return result;
        }
    }
}
