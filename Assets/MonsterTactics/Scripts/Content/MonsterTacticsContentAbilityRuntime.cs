using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.1f.3 optional ability engine. A snapshot is built once per Play Mode
    /// session. Only explicitly opted-in abilities are routed through authored
    /// formulas. All older abilities and all enemy PvE specials keep legacy behavior.
    /// </summary>
    public static class MonsterTacticsContentAbilityRuntime
    {
        private const string CatalogPath = "Content/MonsterTacticsContentCatalog";

        public struct Visual
        {
            public MonsterTacticsAbilityVfxPreset Preset;
            public GameObject Prefab;
            public float Scale;
            public float Lifetime;
        }

        private sealed class Entry
        {
            public string DisplayName, Description, Formula, DamageKind;
            public MonsterTacticsAbilityScaling Primary, Splash, Heal, Shield;
            public int Hits, Radius, MaxSecondary;
            public float BurnSeconds, BurnFraction, WoundSeconds;
            public Visual Appearance;
        }
        private sealed class Snapshot
        {
            internal bool Enabled;
            internal readonly Dictionary<string, Entry> ByForm =
                new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            internal readonly Dictionary<string, Visual> ByAbilityName =
                new Dictionary<string, Visual>(StringComparer.OrdinalIgnoreCase);
        }
        private static Snapshot snapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewPlaySession() { snapshot = null; }
        private static Snapshot Current
        {
            get
            {
                if (!Application.isPlaying || snapshot == null) snapshot = Build();
                return snapshot;
            }
        }
        public static bool IsActive { get { return Current.Enabled; } }
        public static bool TryGetVisual(string abilityName, out Visual visual)
        {
            visual = default(Visual);
            return Current.Enabled && Current.ByAbilityName.TryGetValue(abilityName ?? "", out visual);
        }

        public static bool TryGetProfile(string monsterForm, MonsterTacticsCombatRules.Stats stats,
            out MonsterTacticsCombatRules.AbilityProfile result)
        {
            result = default(MonsterTacticsCombatRules.AbilityProfile);
            Entry e;
            if (!Current.Enabled || !Current.ByForm.TryGetValue(monsterForm ?? "", out e)) return false;
            result = Evaluate(e, stats);
            return true;
        }

        private static MonsterTacticsCombatRules.AbilityProfile Evaluate(Entry e,
            MonsterTacticsCombatRules.Stats stats)
        {
            float primary = e.Primary.Evaluate(stats.Attack, stats.AbilityPower);
            float splash = e.Splash.Evaluate(stats.Attack, stats.AbilityPower);
            float heal = e.Heal.Evaluate(stats.Attack, stats.AbilityPower);
            float shield = e.Shield.Evaluate(stats.Attack, stats.AbilityPower);
            var profile = new MonsterTacticsCombatRules.AbilityProfile(e.DisplayName, e.Description,
                e.Formula, e.DamageKind, primary, splash, e.Hits, heal, shield);
            profile.SplashRadius = splash > 0f ? e.Radius : 0;
            profile.MaximumSecondaryTargets = e.MaxSecondary;
            profile.BurnDuration = e.BurnSeconds;
            profile.BurnMaxHpPerSecond = e.BurnFraction;
            profile.WoundDuration = e.WoundSeconds;
            return profile;
        }

        /// <summary>Pure calculator used by the Editor regression suite, never mutates assets.</summary>
        public static MonsterTacticsCombatRules.AbilityProfile EvaluateRecord(
            MonsterTacticsAbilityRecord record, MonsterTacticsCombatRules.Stats stats)
        {
            string message;
            if (!Valid(record, out message) || !record.useEffectBlocks)
                throw new ArgumentException("Invalid authored ability: " + message);
            return Evaluate(new Entry
            {
                DisplayName = record.displayName, Description = record.description,
                Formula = record.formula, DamageKind = record.damageKind,
                Primary = Clone(record.primary), Splash = Clone(record.splash),
                Heal = Clone(record.selfHeal), Shield = Clone(record.selfShield),
                Hits = record.hitCount, Radius = record.splashRadius,
                MaxSecondary = record.maximumSecondaryTargets,
                BurnSeconds = record.burnDuration,
                BurnFraction = record.burnMaxHpPerSecond,
                WoundSeconds = record.woundDuration
            }, stats);
        }

        private static string DescribeFormula(MonsterTacticsAbilityRecord a)
        {
            var parts = new List<string>();
            if (HasAmount(a.primary)) parts.Add("Hit: " + Describe(a.primary));
            if (HasAmount(a.splash)) parts.Add("Splash: " + Describe(a.splash));
            if (HasAmount(a.selfHeal)) parts.Add("Heal: " + Describe(a.selfHeal));
            if (HasAmount(a.selfShield)) parts.Add("Shield: " + Describe(a.selfShield));
            if (a.burnDuration > 0f && a.burnMaxHpPerSecond > 0f)
                parts.Add("Burn " + (100f * a.burnMaxHpPerSecond).ToString("0.#") +
                    "% max HP/s for " + a.burnDuration.ToString("0.#") + "s");
            if (a.woundDuration > 0f) parts.Add("Wound " + a.woundDuration.ToString("0.#") + "s");
            return String.Join("; ", parts.ToArray());
        }
        private static bool HasAmount(MonsterTacticsAbilityScaling s)
        {
            return s != null && (s.flat > 0 || s.attack > 0 || s.abilityPower > 0);
        }
        private static string Describe(MonsterTacticsAbilityScaling s)
        {
            var pieces = new List<string>();
            if (s.flat > 0) pieces.Add(s.flat.ToString("0.##"));
            if (s.attack > 0) pieces.Add((s.attack * 100f).ToString("0.#") + "% AD");
            if (s.abilityPower > 0) pieces.Add((s.abilityPower * 100f).ToString("0.#") + "% AP");
            return String.Join(" + ", pieces.ToArray());
        }

        private static MonsterTacticsAbilityScaling Clone(MonsterTacticsAbilityScaling scale)
        {
            return new MonsterTacticsAbilityScaling
            {
                flat = scale.flat, attack = scale.attack, abilityPower = scale.abilityPower
            };
        }
        public static bool ValidScale(MonsterTacticsAbilityScaling scale)
        {
            return scale != null && Finite(scale.flat) && Finite(scale.attack) &&
                Finite(scale.abilityPower) && scale.flat >= 0f && scale.attack >= 0f &&
                scale.abilityPower >= 0f;
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        public static bool Valid(MonsterTacticsAbilityRecord ability, out string problem)
        {
            problem = "";
            if (ability == null) { problem = "null ability"; return false; }
            if (!ability.useEffectBlocks) return true;
            if (String.IsNullOrWhiteSpace(ability.displayName)) { problem = "missing ability name"; return false; }
            if (ability.damageKind != "Magic" && ability.damageKind != "Physical" &&
                ability.damageKind != "Shield")
            { problem = "DamageKind must be Magic, Physical or Shield"; return false; }
            if (!ValidScale(ability.primary) || !ValidScale(ability.splash) ||
                !ValidScale(ability.selfHeal) || !ValidScale(ability.selfShield))
            { problem = "invalid/negative scaling coefficient"; return false; }
            if (ability.hitCount < 1 || ability.hitCount > 12 || ability.splashRadius < 0 ||
                ability.splashRadius > 6 || ability.maximumSecondaryTargets < 0 ||
                ability.burnDuration < 0f || ability.woundDuration < 0f ||
                ability.burnMaxHpPerSecond < 0f || ability.burnMaxHpPerSecond > 1f ||
                !Finite(ability.burnDuration) || !Finite(ability.woundDuration) ||
                !Finite(ability.burnMaxHpPerSecond) ||
                !Finite(ability.vfxScale) || ability.vfxScale <= 0f ||
                !Finite(ability.vfxLifetime) || ability.vfxLifetime <= 0f)
            { problem = "invalid hits/radius/status/VFX values"; return false; }
            // A strictly 0-damage ability that only heals won't resolve if the
            // enemy dies first; require damage or shielding at this stage.
            bool damage = ability.primary.flat > 0 || ability.primary.attack > 0 ||
                ability.primary.abilityPower > 0 || ability.splash.flat > 0 ||
                ability.splash.attack > 0 || ability.splash.abilityPower > 0;
            bool shield = ability.selfShield.flat > 0 || ability.selfShield.attack > 0 ||
                ability.selfShield.abilityPower > 0;
            if (!damage && !shield)
            { problem = "requires damage or self-shield (heal-only not supported yet)"; return false; }
            bool splash = ability.splash.flat > 0 || ability.splash.attack > 0 ||
                ability.splash.abilityPower > 0;
            if (splash && (ability.splashRadius == 0 || ability.maximumSecondaryTargets == 0))
            { problem = "splash needs a radius > 0 and max secondary targets > 0"; return false; }
            if (damage && ability.damageKind == "Shield")
            { problem = "damaging abilities must use Physical or Magic damage kind"; return false; }
            return true;
        }
        private static Snapshot Build()
        {
            var state = new Snapshot();
            var catalog = Resources.Load<MonsterTacticsContentCatalog>(CatalogPath);
            if (catalog == null || !catalog.enableLiveAbilities) return state;
            if (catalog.abilities == null || catalog.monster == null)
            {
                Debug.LogError("[U6.9.1f.3] Live ability catalog incomplete; retaining legacy casts.");
                return state;
            }
            var byId = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            foreach (MonsterTacticsAbilityRecord a in catalog.abilities)
            {
                if (a == null || !a.enabled || !a.useEffectBlocks) continue;
                string problem;
                if (!Valid(a, out problem))
                {
                    Debug.LogError("[U6.9.1f.3] Rejected ability '" + a.id + "': " + problem);
                    continue;
                }
                if (byId.ContainsKey(a.id ?? "")) continue;
                var entry = new Entry
                {
                    DisplayName = a.displayName,
                    Description = a.description ?? "",
                    Formula = DescribeFormula(a),
                    DamageKind = a.damageKind,
                    Primary = Clone(a.primary), Splash = Clone(a.splash),
                    Heal = Clone(a.selfHeal), Shield = Clone(a.selfShield),
                    Hits = a.hitCount, Radius = a.splashRadius,
                    MaxSecondary = a.maximumSecondaryTargets,
                    BurnSeconds = a.burnDuration,
                    BurnFraction = a.burnMaxHpPerSecond,
                    WoundSeconds = a.woundDuration,
                    Appearance = new Visual
                    {
                        Preset = a.vfxPreset, Prefab = a.vfxPrefab,
                        Scale = a.vfxScale, Lifetime = a.vfxLifetime
                    }
                };
                byId.Add(a.id, entry);
            }
            foreach (MonsterTacticsMonsterRecord p in catalog.monster)
            {
                if (p == null || !p.enabled || p.forms == null) continue;
                Entry entry;
                if (!byId.TryGetValue(p.abilityId ?? "", out entry)) continue;
                foreach (MonsterTacticsMonsterFormRecord form in p.forms)
                {
                    if (form == null || String.IsNullOrWhiteSpace(form.name)) continue;
                    state.ByForm[form.name] = entry;
                }
                state.ByAbilityName[entry.DisplayName] = entry.Appearance;
            }
            state.Enabled = true;
            return state;
        }
    }
}
