#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    public static class MonsterTacticsContentStudioValidator
    {
        public sealed class Report
        {
            public readonly List<string> errors = new List<string>();
            public readonly List<string> warnings = new List<string>();
            public int MonsterCount;
            public int TraitCount;
            public int AbilityCount;
            public bool Passed { get { return errors.Count == 0; } }
            public string Summary { get { return (Passed ? "PASS" : "FAIL") + ": " +
                MonsterCount + " families / " + TraitCount + " traits / " + AbilityCount +
                " abilities; " + errors.Count + " errors, " + warnings.Count + " warnings."; } }
        }

        public static Report Check(MonsterTacticsContentCatalog catalog)
        {
            var r = new Report();
            if (catalog == null)
            {
                r.errors.Add("No Content Catalog found. Run U6.9.1f.0 Import Legacy Snapshot first.");
                return r;
            }
            if (catalog.SchemaVersion != MonsterTacticsContentCatalog.CurrentSchema)
                r.errors.Add("Unsupported catalog schema: " + catalog.SchemaVersion);
            r.MonsterCount = catalog.monster == null ? 0 : catalog.monster.Count;
            r.TraitCount = catalog.traits == null ? 0 : catalog.traits.Count;
            r.AbilityCount = catalog.abilities == null ? 0 : catalog.abilities.Count;
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var abilityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var forms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var traitNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Referencing a disabled trait is valid: disable the effect without
            // forcing users to remove typing/origin memberships from Monster.
            var definedTraitNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in catalog.abilities)
            {
                if (a == null) { r.errors.Add("Null ability record"); continue; }
                ValidateId(a.id, ids, r.errors, "Ability");
                if (!a.enabled) { r.warnings.Add("Disabled ability draft: " + a.id); continue; }
                abilityIds.Add(a.id ?? "");
                if (String.IsNullOrWhiteSpace(a.displayName)) r.errors.Add("Ability " + a.id + ": missing name");
                if (a.hitCount < 1) r.errors.Add("Ability " + a.id + ": hit count must be positive");
                if (a.legacyBasePrimaryDamage < 0 || a.legacyBaseSplashDamage < 0 ||
                    a.legacyBaseHealing < 0 || a.legacyBaseShield < 0)
                    r.errors.Add("Ability " + a.id + ": negative damage/healing/shield snapshot");
                if (a.useEffectBlocks)
                {
                    string problem;
                    if (!MonsterTacticsContentAbilityRuntime.Valid(a, out problem))
                        r.errors.Add("Ability " + a.id + ": " + problem);
                }
                else if (String.IsNullOrEmpty(a.legacyHandlerMonster))
                    r.warnings.Add("Ability " + a.id + ": no handler; enable Use Effect Blocks to make playable");
            }
            foreach (var t in catalog.traits)
            {
                if (t == null) { r.errors.Add("Null trait record"); continue; }
                ValidateId(t.id, ids, r.errors, "Trait");
                if (!String.IsNullOrWhiteSpace(t.displayName))
                    definedTraitNames.Add(t.displayName);
                if (!t.enabled) { r.warnings.Add("Disabled trait draft: " + t.id); continue; }
                if (String.IsNullOrWhiteSpace(t.displayName)) r.errors.Add("Trait " + t.id + ": missing name");
                if (!traitNames.Add(t.displayName ?? "")) r.errors.Add("Duplicate trait name: " + t.displayName);
                int previous = 0;
                if (t.tiers == null) { r.errors.Add("Trait " + t.id + ": missing tier list"); continue; }
                foreach (var tier in t.tiers)
                {
                    if (tier == null) { r.errors.Add("Trait " + t.id + ": null breakpoint tier"); continue; }
                    if (tier.uniqueFamiliesRequired <= previous || tier.uniqueFamiliesRequired < 1)
                        r.errors.Add("Trait " + t.id + ": breakpoints must be positive and strictly increasing");
                    previous = tier.uniqueFamiliesRequired;
                    if (tier.effects == null) continue;
                    foreach (var e in tier.effects)
                    {
                        if (e == null) { r.errors.Add("Trait " + t.id + ": null effect"); continue; }
                        if (float.IsNaN(e.value) || float.IsInfinity(e.value))
                            r.errors.Add("Trait " + t.id + ": invalid stat value");
                        if (!Enum.IsDefined(typeof(MonsterTacticsContentEffectStat), e.stat))
                            r.errors.Add("Trait " + t.id + ": unknown effect stat " + e.stat);
                        if (IsMultiplier(e.stat) && e.value < -1f)
                            r.errors.Add("Trait " + t.id + ": multiplier may not reduce below zero");
                        if (e.stat == MonsterTacticsContentEffectStat.CritChanceFlat &&
                            (e.value < -1f || e.value > 1f))
                            r.errors.Add("Trait " + t.id + ": crit chance uses decimal fractions (-1 to 1)");
                        if (!String.IsNullOrWhiteSpace(e.vfxPresetId) && catalog.enableLiveTraits)
                            r.warnings.Add("Trait " + t.id + ": VFX preset '" + e.vfxPresetId +
                                "' is visual metadata only (trait aura triggers not supported yet)");
                    }
                }
            }
            foreach (var p in catalog.monster)
            {
                if (p == null) { r.errors.Add("Null Monster record"); continue; }
                ValidateId(p.id, ids, r.errors, "Monster");
                if (!p.enabled) { r.warnings.Add("Disabled Monster draft: " + p.id); continue; }
                string context = "Monster " + p.id + ": ";
                if (String.IsNullOrWhiteSpace(p.displayName)) r.errors.Add(context + "missing name");
                if (p.shopCost < 1 || p.shopCost > 5) r.errors.Add(context + "shop cost must be 1-5");
                if (p.baseHp < 1) r.errors.Add(context + "base HP must be positive");
                if (String.IsNullOrWhiteSpace(p.combatRole)) r.errors.Add(context + "missing combat role");
                if (p.forms == null || p.forms.Count != 3)
                    r.errors.Add(context + "expected exactly three form slots (same name allowed for legendary)");
                else
                {
                    foreach (var form in p.forms)
                    {
                        if (form == null || String.IsNullOrWhiteSpace(form.name))
                        { r.errors.Add(context + "empty form name"); continue; }
                        string prior;
                        if (forms.TryGetValue(form.name, out prior) && prior != p.id)
                            r.errors.Add(context + "form '" + form.name + "' also belongs to " + prior);
                        else forms[form.name] = p.id;
                        if (String.IsNullOrWhiteSpace(form.types))
                            r.errors.Add(context + "missing type for form " + form.name);
                        else foreach (var raw in form.types.Split('/'))
                        {
                            string type = raw.Trim();
                            if (type.Length == 0) r.errors.Add(context + "empty elemental type for " + form.name);
                            else if (!definedTraitNames.Contains(type))
                                r.errors.Add(context + "unknown elemental trait '" + type + "'");
                        }
                        // Legacy art is a valid fallback. Only new species need a base-form
                        // sprite before the opt-in runtime can safely create a template.
                    }
                }
                if (p.forms != null && p.forms.Count == 3 && p.forms[0] != null)
                {
                    bool legacySpecies = false;
                    foreach (MonsterTacticsU5Roster.Family f in MonsterTacticsU5Roster.All)
                        if (String.Equals(f.Forms[0], p.forms[0].name, StringComparison.OrdinalIgnoreCase))
                            legacySpecies = true;
                    if (!legacySpecies && p.forms[0].sprite == null)
                        r.errors.Add(context + "new species requires a base-form sprite override");
                }
                if (!abilityIds.Contains(p.abilityId ?? ""))
                    r.errors.Add(context + "ability ID not found: " + p.abilityId);
                if (p.origins != null) foreach (string origin in p.origins)
                {
                    if (!definedTraitNames.Contains(origin ?? ""))
                        r.errors.Add(context + "unknown origin trait '" + origin + "'");
                }
                var s = p.stats;
                if (s == null) { r.errors.Add(context + "missing combat stats"); continue; }
                if (s.attack < 0 || s.attackSpeed <= 0 || s.abilityPower < 0 ||
                    s.attackRange < 1 || s.manaCost < 1 || s.moveInterval <= 0 ||
                    s.criticalChance < 0 || s.criticalChance > 1 || s.criticalMultiplier < 1 ||
                    s.omnivamp < 0 || s.omnivamp > 1 || s.armor < 0 || s.magicResist < 0)
                    r.errors.Add(context + "invalid base combat stats");
            }
            bool hasOneCost = false;
            foreach (var candidate in catalog.monster)
                if (candidate != null && candidate.enabled && candidate.shopCost == 1) hasOneCost = true;
            if (!hasOneCost) r.errors.Add("Enable at least one cost-1 Monster for valid shop odds");
            if (r.MonsterCount == 0) r.warnings.Add("No Monster families in catalog");
            if (r.TraitCount == 0) r.warnings.Add("No traits in catalog");
            if (r.AbilityCount == 0) r.warnings.Add("No abilities in catalog");
            if (!catalog.readOnlyLegacyRuntime)
                r.warnings.Add("Monster catalog mode ON: shop/AI/stats/evolution active in new matches");
            if (catalog.enableLiveTraits)
                r.warnings.Add("Live trait stats ON: configured stat bonuses affect combat in new matches");
            if (catalog.enableLiveAbilities)
                r.warnings.Add("Live abilities ON: only explicitly opted-in Use Effect Blocks records override legacy casts");
            return r;
        }

        private static bool IsMultiplier(MonsterTacticsContentEffectStat s)
        {
            return s == MonsterTacticsContentEffectStat.AttackMultiplier ||
                   s == MonsterTacticsContentEffectStat.AttackSpeedMultiplier ||
                   s == MonsterTacticsContentEffectStat.MaxHpMultiplier ||
                   s == MonsterTacticsContentEffectStat.DamageMultiplier ||
                   s == MonsterTacticsContentEffectStat.IncomingDamageMultiplier ||
                   s == MonsterTacticsContentEffectStat.ManaGainMultiplier;
        }
        private static void ValidateId(string id, HashSet<string> allIds,
            List<string> errors, string category)
        {
            if (String.IsNullOrWhiteSpace(id)) errors.Add(category + " has a blank ID");
            else if (!allIds.Add(id)) errors.Add("Duplicate ID (across all content types): " + id);
        }

        [MenuItem("MonsterTactics/Tests/Archive/Content Studio/U6.9.1f.0 Validate Content Catalog")]
        public static void ValidateMenu()
        {
            Report report = Check(MonsterTacticsContentStudioImporter.FindCatalog());
            foreach (string e in report.errors) Debug.LogError("[U6.9.1f.0] " + e);
            // Collapse optional-art noise to avoid flooding the Console.
            int visible = Math.Min(12, report.warnings.Count);
            for (int i = 0; i < visible; i++) Debug.LogWarning("[U6.9.1f.0] " + report.warnings[i]);
            if (report.warnings.Count > visible)
                Debug.Log("[U6.9.1f.0] " + (report.warnings.Count - visible) +
                    " more warnings (mostly optional sprite overrides) suppressed.");
            Debug.Log("[U6.9.1f.0] " + report.Summary);
            EditorUtility.DisplayDialog("Content Catalog Validation",
                report.Summary + "\nSee Console for errors/warnings.\n\n" +
                "Validation does not change the running match.", "OK");
        }

        [MenuItem("MonsterTactics/Tests/Archive/Content Studio/U6.9.1f.0 Compare Legacy Snapshot")]
        public static void CompareLegacy()
        {
            MonsterTacticsContentCatalog c = MonsterTacticsContentStudioImporter.FindCatalog();
            if (c == null) { Debug.LogError("[U6.9.1f.0] Import a catalog first."); return; }
            int compared = 0, differences = 0;
            foreach (var f in MonsterTacticsU5Roster.All)
            {
                MonsterTacticsMonsterRecord found = null;
                string id = "family_" + MonsterTacticsContentStudioImporter.Slug(f.Forms[0]);
                foreach (var p in c.monster)
                    if (p != null && p.id == id) { found = p; break; }
                if (found == null)
                {
                    differences++;
                    Debug.LogWarning("[U6.9.1f.0] Missing legacy family " + f.Forms[0]);
                    continue;
                }
                compared++;
                var s = MonsterTacticsCombatRules.ForMonster(f.Forms[0]);
                bool mismatch = found.shopCost != f.Cost || found.baseHp != f.BaseHP ||
                    found.combatRole != f.Role || found.forms == null || found.forms.Count != 3 ||
                    found.stats == null;
                if (!mismatch)
                {
                    var b = found.stats;
                    mismatch = Mathf.Abs(b.attack - s.Attack) > .001f ||
                        Mathf.Abs(b.attackSpeed - s.AttackSpeed) > .001f ||
                        Mathf.Abs(b.abilityPower - s.AbilityPower) > .001f ||
                        Mathf.Abs(b.omnivamp - s.Omnivamp) > .001f ||
                        Mathf.Abs(b.criticalChance - s.CriticalChance) > .001f ||
                        Mathf.Abs(b.criticalMultiplier - s.CriticalMultiplier) > .001f ||
                        b.armor != s.Armor || b.magicResist != s.MagicResist ||
                        b.attackRange != s.Range || b.manaCost != s.ManaCost ||
                        Mathf.Abs(b.moveInterval - s.MoveInterval) > .001f;
                    for (int i = 0; i < 3; i++)
                    {
                        var form = found.forms[i];
                        if (form == null || form.name != f.Forms[i] ||
                            form.types != f.Types[i] || form.bestiaryNumber != f.Bestiary[i])
                            mismatch = true;
                    }
                    var profile = MonsterTacticsCombatRules.AbilityFor(f.Forms[0], s);
                    string abilityId = "ability_" + MonsterTacticsContentStudioImporter.Slug(f.Forms[0]) +
                        "_" + MonsterTacticsContentStudioImporter.Slug(profile.Name);
                    if (found.abilityId != abilityId) mismatch = true;
                    bool abilityFound = false;
                    foreach (var a in c.abilities)
                        if (a != null && a.id == abilityId)
                        {
                            abilityFound = true;
                            if (a.displayName != profile.Name || a.formula != profile.Formula ||
                                a.damageKind != profile.DamageKind ||
                                Mathf.Abs(a.legacyBasePrimaryDamage - profile.PrimaryRaw) > .001f ||
                                Mathf.Abs(a.legacyBaseSplashDamage - profile.SplashRaw) > .001f)
                                mismatch = true;
                            break;
                        }
                    if (!abilityFound) mismatch = true;
                }
                if (mismatch)
                {
                    differences++;
                    Debug.LogWarning("[U6.9.1f.0] Edited/different snapshot for " + f.Forms[0] +
                        ". No live gameplay change.");
                }
            }
            int checkedTraits = 0;
            foreach (var def in MonsterTacticsTraits.LegacyAll)
            {
                string id = "trait_" + MonsterTacticsContentStudioImporter.Slug(def.Name);
                MonsterTacticsTraitRecord found = null;
                foreach (var t in c.traits) if (t != null && t.id == id) { found = t; break; }
                if (found == null) { differences++; continue; }
                checkedTraits++;
                bool mismatch = found.displayName != def.Name || found.isOrigin != def.Origin ||
                    found.tiers == null || found.tiers.Count != def.Breakpoints.Length;
                if (!mismatch)
                    for (int i = 0; i < def.Breakpoints.Length; i++)
                        if (found.tiers[i] == null ||
                            found.tiers[i].uniqueFamiliesRequired != def.Breakpoints[i] ||
                            found.tiers[i].description != (i < def.Descriptions.Length ?
                                def.Descriptions[i] : "")) mismatch = true;
                if (mismatch)
                {
                    differences++;
                    Debug.LogWarning("[U6.9.1f.0] Edited/different trait: " + def.Name);
                }
            }
            Debug.Log("[U6.9.1f.0] Compared " + compared + " legacy families and " +
                checkedTraits + " traits; " + differences +
                " missing/edited. No runtime migration was performed.");
            EditorUtility.DisplayDialog("Legacy Snapshot Comparison",
                "Checked " + compared + " families and " + checkedTraits +
                " traits.\n" + differences +
                " missing/edited.\n\nDifferences are normal after intentional editing. Live gameplay is still unchanged.", "OK");
        }
    }
}
#endif
