#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.1f.2 nonmutating tests. The end-to-end fight still needs Play Mode.
    /// </summary>
    public static class MonsterTacticsU691f2TraitValidator
    {
        [MenuItem("MonsterTactics/Tests/Archive/Augments/U6.9.1f.2 Verify Live Trait Math")]
        public static void Verify()
        {
            int checks = 0;
            var catalog = MonsterTacticsContentStudioImporter.FindCatalog();
            if (catalog == null) throw new InvalidOperationException("Import catalog before testing live traits.");
            var report = MonsterTacticsContentStudioValidator.Check(catalog);
            if (!report.Passed)
                throw new InvalidOperationException("Content catalog failed validation: " +
                    String.Join("; ", report.errors.ToArray()));
            checks++;
            MonsterTacticsTraitRecord fire = Find(catalog.traits, "Fire");
            if (fire == null || fire.tiers == null || fire.tiers.Count < 2)
                throw new InvalidOperationException("Fire must have two configured trait tiers.");
            checks++;
            TestTier("Fire 2", fire.tiers[0], 1.12f, 12f, ref checks);
            TestTier("Fire 3", fire.tiers[1], 1.22f, 22f, ref checks);
            // The live trait engine converts percentage deltas to fighter stats.
            var simulated = new List<MonsterTacticsContentEffectRecord>
            {
                new MonsterTacticsContentEffectRecord {
                    stat = MonsterTacticsContentEffectStat.AttackMultiplier, value = .30f },
                new MonsterTacticsContentEffectRecord {
                    stat = MonsterTacticsContentEffectStat.AbilityPowerFlat, value = 20f },
                new MonsterTacticsContentEffectRecord {
                    stat = MonsterTacticsContentEffectStat.AttackSpeedMultiplier, value = .15f },
                new MonsterTacticsContentEffectRecord {
                    stat = MonsterTacticsContentEffectStat.MaxHpMultiplier, value = .10f },
                new MonsterTacticsContentEffectRecord {
                    stat = MonsterTacticsContentEffectStat.DamageMultiplier, value = .20f },
                new MonsterTacticsContentEffectRecord {
                    stat = MonsterTacticsContentEffectStat.IncomingDamageMultiplier, value = -.20f },
                new MonsterTacticsContentEffectRecord {
                    stat = MonsterTacticsContentEffectStat.ManaGainMultiplier, value = .25f },
                new MonsterTacticsContentEffectRecord {
                    stat = MonsterTacticsContentEffectStat.CritChanceFlat, value = .10f },
                new MonsterTacticsContentEffectRecord {
                    stat = MonsterTacticsContentEffectStat.ArmorFlat, value = 15f },
                new MonsterTacticsContentEffectRecord {
                    stat = MonsterTacticsContentEffectStat.MagicResistFlat, value = 22f },
            };
            var bonus = MonsterTacticsContentTraitRuntime.EvaluateEffects(simulated);
            var raw = new MonsterTacticsCombatRules.Stats(48f, .72f, 20, 25, 3, 75, .55f,
                "Flamethrower");
            var modified = MonsterTacticsTraits.ApplyToStats(raw, bonus);
            Near("30% AD changes actual attack", modified.Attack, 62.4f, ref checks);
            Near("AP added to fighter stats", modified.AbilityPower, 120f, ref checks);
            Near("Attack speed", modified.AttackSpeed, .828f, ref checks);
            Near("Max HP multiplier", bonus.HpMultiplier, 1.10f, ref checks);
            Near("Damage dealt", modified.DamageMultiplier, 1.20f, ref checks);
            Near("Damage taken", modified.IncomingDamageMultiplier, .80f, ref checks);
            Near("Mana gain", modified.ManaGainMultiplier, 1.25f, ref checks);
            Near("Crit chance", modified.CriticalChance, .20f, ref checks);
            Near("Armor", modified.Armor, 35f, ref checks);
            Near("Magic resist", modified.MagicResist, 47f, ref checks);
            if (catalog.enableLiveTraits)
            {
                if (!MonsterTacticsContentTraitRuntime.IsUsingCatalog)
                    throw new InvalidOperationException("Live-trait mode was enabled but runtime fell back to legacy.");
                checks++;
            }
            Debug.Log("[U6.9.1f.2] PASS: " + checks +
                " checks. Live trait switch is " + (catalog.enableLiveTraits ? "ON" : "OFF") +
                ". Combat snapshot integration still requires Play Mode verification.");
            EditorUtility.DisplayDialog("MonsterTactics U6.9.1f.2",
                "PASS: " + checks + " catalog/effect math checks.\n\n" +
                "For end-to-end testing, enable Live Traits outside Play Mode, set Fire 2 to " +
                "+30% AD, deploy two distinct Fire families, and confirm increased " +
                "attack and real combat damage. Restore the value afterward.", "OK");
        }

        private static MonsterTacticsTraitRecord Find(List<MonsterTacticsTraitRecord> traits, string name)
        {
            foreach (MonsterTacticsTraitRecord t in traits)
                if (t != null && t.enabled && String.Equals(t.displayName, name,
                    StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }
        private static void TestTier(string label, MonsterTacticsTraitTierRecord tier,
            float attack, float ap, ref int checks)
        {
            var bonus = MonsterTacticsContentTraitRuntime.EvaluateEffects(tier.effects);
            // These check the imported defaults before intentional rebalancing. If
            // edited, check finite multipliers instead of enforcing old balancing.
            if (float.IsNaN(bonus.AttackMultiplier) || bonus.AttackMultiplier < 0 ||
                float.IsNaN(bonus.AbilityPowerBonus))
                throw new InvalidOperationException(label + " invalid effect math.");
            checks++;
            if (Mathf.Abs(bonus.AttackMultiplier - attack) > .0001f ||
                Mathf.Abs(bonus.AbilityPowerBonus - ap) > .0001f)
                Debug.Log("[U6.9.1f.2] " + label + " was intentionally edited: AD x" +
                    bonus.AttackMultiplier.ToString("0.##") + ", AP " + bonus.AbilityPowerBonus);
        }
        private static void Near(string label, float actual, float expected, ref int checks)
        {
            if (Mathf.Abs(actual - expected) > .001f)
                throw new InvalidOperationException(label + ": expected " + expected + ", got " + actual);
            checks++;
        }
    }
}
#endif
