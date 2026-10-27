#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>Imports a lossless *reference snapshot* of currently hardcoded shop data.
    /// Re-import is additive only: designer edits to existing records are preserved.</summary>
    public static class MonsterTacticsContentStudioImporter
    {
        public const string AssetPath = "Assets/MonsterTactics/Resources/Content/MonsterTacticsContentCatalog.asset";
        private const string Folder = "Assets/MonsterTactics/Resources/Content";

        public static MonsterTacticsContentCatalog FindCatalog()
        {
            return AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(AssetPath);
        }

        [MenuItem("MonsterTactics/Content Studio/U6.9.1f.0 Import Legacy Snapshot")]
        public static void ImportMenu()
        {
            int addedMonster, addedTraits, addedAbilities;
            MonsterTacticsContentCatalog c = Import(out addedMonster, out addedTraits, out addedAbilities);
            EditorUtility.DisplayDialog("MonsterTactics Content Studio",
                "Content staging catalog ready.\n\nAdded " + addedMonster + " Monster families, " +
                addedTraits + " traits, " + addedAbilities + " abilities.\n" +
                "Re-import never overwrites existing edits.\n\n" +
                "IMPORTANT: Existing gameplay still uses the original hardcoded systems.", "OK");
            Selection.activeObject = c;
        }

        public static MonsterTacticsContentCatalog Import(out int addedMonster,
            out int addedTraits, out int addedAbilities)
        {
            addedMonster = addedTraits = addedAbilities = 0;
            if (!AssetDatabase.IsValidFolder("Assets/MonsterTactics"))
                throw new InvalidOperationException("Cannot locate Assets/MonsterTactics. Install inside the existing project.");
            if (!AssetDatabase.IsValidFolder("Assets/MonsterTactics/Resources"))
                AssetDatabase.CreateFolder("Assets/MonsterTactics", "Resources");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/MonsterTactics/Resources", "Content");
            MonsterTacticsContentCatalog c = FindCatalog();
            if (c == null)
            {
                c = ScriptableObject.CreateInstance<MonsterTacticsContentCatalog>();
                AssetDatabase.CreateAsset(c, AssetPath);
            }
            Undo.RecordObject(c, "Import legacy MonsterTactics content");
            var monsterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var traitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var abilityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in c.monster) if (r != null) monsterIds.Add(r.id ?? "");
            foreach (var r in c.traits) if (r != null) traitIds.Add(r.id ?? "");
            foreach (var r in c.abilities) if (r != null) abilityIds.Add(r.id ?? "");

            foreach (MonsterTacticsU5Roster.Family family in MonsterTacticsU5Roster.All)
            {
                string baseName = family.Forms[0];
                string id = "family_" + Slug(baseName);
                var stats = MonsterTacticsCombatRules.ForMonster(baseName);
                var profile = MonsterTacticsCombatRules.AbilityFor(baseName, stats);
                string abilityId = "ability_" + Slug(baseName) + "_" + Slug(profile.Name);
                if (monsterIds.Add(id))
                {
                    var p = new MonsterTacticsMonsterRecord
                    {
                        id = id, displayName = baseName, shopCost = family.Cost,
                        combatRole = family.Role, baseHp = family.BaseHP, abilityId = abilityId,
                        stats = new MonsterTacticsMonsterStatRecord
                        {
                            attack = stats.Attack, attackSpeed = stats.AttackSpeed,
                            abilityPower = stats.AbilityPower, omnivamp = stats.Omnivamp,
                            criticalChance = stats.CriticalChance, criticalMultiplier = stats.CriticalMultiplier,
                            armor = stats.Armor, magicResist = stats.MagicResist,
                            attackRange = stats.Range, manaCost = stats.ManaCost,
                            moveInterval = stats.MoveInterval
                        }
                    };
                    for (int i = 0; i < family.Forms.Length; i++)
                        p.forms.Add(new MonsterTacticsMonsterFormRecord
                        {
                            name = family.Forms[i], types = family.Types[i],
                            bestiaryNumber = family.Bestiary[i]
                        });
                    if (In(baseName, "Budhorn", "Emberlet", "Ripplet", "Mosslet", "Flarbit", "Mistrip"))
                        p.origins.Add("First Partners");
                    if (In(baseName, "Flarbit", "Dunebit")) p.origins.Add("Playful");
                    if (In(baseName, "Voltrix", "Pyrothane", "Aqualith")) p.origins.Add("Legendary Beasts");
                    c.monster.Add(p);
                    addedMonster++;
                }
                if (abilityIds.Add(abilityId))
                {
                    c.abilities.Add(new MonsterTacticsAbilityRecord
                    {
                        id = abilityId, displayName = profile.Name,
                        description = profile.Description, formula = profile.Formula,
                        damageKind = profile.DamageKind,
                        legacyBasePrimaryDamage = profile.PrimaryRaw,
                        legacyBaseSplashDamage = profile.SplashRaw,
                        legacyBaseHealing = profile.HealRaw,
                        legacyBaseShield = profile.ShieldRaw,
                        hitCount = profile.HitCount,
                        splashRadius = profile.SplashRadius,
                        maximumSecondaryTargets = profile.MaximumSecondaryTargets,
                        burnDuration = profile.BurnDuration,
                        burnMaxHpPerSecond = profile.BurnMaxHpPerSecond,
                        woundDuration = profile.WoundDuration,
                        legacyHandlerMonster = baseName,
                        designerNotes = "Imported from MonsterTacticsCombatRules.AbilityFor; runtime still uses legacy handler."
                    });
                    addedAbilities++;
                }
            }
            foreach (MonsterTacticsTraits.Definition def in MonsterTacticsTraits.All)
            {
                string id = "trait_" + Slug(def.Name);
                if (!traitIds.Add(id)) continue;
                var t = new MonsterTacticsTraitRecord
                {
                    id = id, displayName = def.Name, isOrigin = def.Origin
                };
                for (int i = 0; i < def.Breakpoints.Length; i++)
                {
                    var tier = new MonsterTacticsTraitTierRecord
                    {
                        uniqueFamiliesRequired = def.Breakpoints[i],
                        description = i < def.Descriptions.Length ? def.Descriptions[i] : ""
                    };
                    AddLegacyTraitEffects(def.Name, i, tier.effects);
                    t.tiers.Add(tier);
                }
                c.traits.Add(t);
                addedTraits++;
            }
            // U6.9.1f.1: re-import must preserve the user-selected runtime mode.
            // A newly created catalog already defaults to legacy mode.
            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
            return c;
        }

        private static bool In(string value, params string[] names)
        {
            foreach (string name in names) if (value == name) return true;
            return false;
        }
        public static string Slug(string text)
        {
            if (String.IsNullOrEmpty(text)) return "unnamed";
            var parts = new System.Text.StringBuilder();
            bool underscore = false;
            foreach (char c in text.ToLowerInvariant())
            {
                if (Char.IsLetterOrDigit(c)) { parts.Append(c); underscore = false; }
                else if (!underscore && parts.Length > 0) { parts.Append('_'); underscore = true; }
            }
            return parts.ToString().Trim('_');
        }
        private static void Effect(List<MonsterTacticsContentEffectRecord> list,
            MonsterTacticsContentEffectStat stat, float value)
        {
            list.Add(new MonsterTacticsContentEffectRecord { stat = stat, value = value });
        }
        private static void AddLegacyTraitEffects(string name, int tier,
            List<MonsterTacticsContentEffectRecord> effects)
        {
            var e = effects;
            switch (name)
            {
                case "Fire":
                    Effect(e, MonsterTacticsContentEffectStat.AttackMultiplier, tier >= 1 ? .22f : .12f);
                    Effect(e, MonsterTacticsContentEffectStat.AbilityPowerFlat, tier >= 1 ? 22f : 12f);
                    break;
                case "Water": Effect(e, MonsterTacticsContentEffectStat.ManaGainMultiplier, tier >= 1 ? .25f : .15f); break;
                case "Grass": Effect(e, MonsterTacticsContentEffectStat.MaxHpMultiplier, .12f); break;
                case "Dragon": Effect(e, MonsterTacticsContentEffectStat.DamageMultiplier, .15f); break;
                case "Ground":
                    Effect(e, MonsterTacticsContentEffectStat.ArmorFlat, 15);
                    Effect(e, MonsterTacticsContentEffectStat.MagicResistFlat, 15);
                    break;
                case "First Partners":
                    Effect(e, MonsterTacticsContentEffectStat.AttackSpeedMultiplier,
                        tier >= 2 ? .35f : tier == 1 ? .20f : .10f);
                    break;
                case "Playful":
                    Effect(e, MonsterTacticsContentEffectStat.AttackSpeedMultiplier, .15f);
                    Effect(e, MonsterTacticsContentEffectStat.CritChanceFlat, .10f);
                    break;
                case "Legendary Beasts":
                    Effect(e, MonsterTacticsContentEffectStat.DamageMultiplier, .10f);
                    Effect(e, MonsterTacticsContentEffectStat.MaxHpMultiplier, .10f);
                    if (tier >= 1) Effect(e, MonsterTacticsContentEffectStat.IncomingDamageMultiplier, -.20f);
                    break;
            }
        }
    }
}
#endif
