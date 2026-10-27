#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.1f.4: Authoring-only, read-only routing/cleanup audit.
    /// It does not replace legacy handlers or modify running gameplay.
    /// </summary>
    public sealed class MonsterTacticsContentMigrationAudit : EditorWindow
    {
        public sealed class AuditReport
        {
            public string CatalogPath;
            public string MonsterPath, TraitPath, AbilityPath;
            public int EnabledMonster, DisabledMonster, EnabledTraits, DisabledTraits;
            public int EnabledAbilities, DisabledAbilities;
            public int LinkedAuthored, LinkedLegacy, UnresolvedLinks, UnlinkedAbilities;
            public int EffectBlockRecords, LegacyHandlerRecords;
            public int Errors;
            public readonly List<string> Findings = new List<string>();
            public readonly List<string> CleanupQueue = new List<string>();
            public string Text;
        }

        private MonsterTacticsContentCatalog catalog;
        private Vector2 scroll;
        private AuditReport report;

        [MenuItem("MonsterTactics/Content Studio/U6.9.1f.4 Migration Audit")]
        public static void Open()
        {
            var w = GetWindow<MonsterTacticsContentMigrationAudit>("Content Migration Audit");
            w.minSize = new Vector2(770f, 560f);
            w.Show();
        }

        [MenuItem("MonsterTactics/Tests/Archive/Content Studio/U6.9.1f.4 Verify Safe Cleanup")]
        public static void Verify()
        {
            var c = MonsterTacticsContentStudioImporter.FindCatalog();
            if (c == null)
            {
                Debug.LogError("[U6.9.1f.4] Catalog missing. Run legacy import first.");
                EditorUtility.DisplayDialog("U6.9.1f.4", "FAIL: Content Catalog missing.", "OK");
                return;
            }
            bool roster = c.readOnlyLegacyRuntime;
            bool traits = c.enableLiveTraits;
            bool abilities = c.enableLiveAbilities;
            int p = c.monster == null ? -1 : c.monster.Count;
            int t = c.traits == null ? -1 : c.traits.Count;
            int a = c.abilities == null ? -1 : c.abilities.Count;
            AuditReport r = Inspect(c);
            var validation = MonsterTacticsContentStudioValidator.Check(c);
            bool routingTestsPass = VerifySimulatedRoutes();
            // Regression: inspect must not mutate editor settings or serialized lists.
            bool unchanged = roster == c.readOnlyLegacyRuntime && traits == c.enableLiveTraits &&
                abilities == c.enableLiveAbilities && p == (c.monster == null ? -1 : c.monster.Count) &&
                t == (c.traits == null ? -1 : c.traits.Count) &&
                a == (c.abilities == null ? -1 : c.abilities.Count);
            bool pass = unchanged && routingTestsPass && validation.Passed && r.Errors == 0 &&
                r.EnabledMonster > 0 && r.EnabledTraits > 0 && r.EnabledAbilities > 0 &&
                r.LinkedAuthored + r.LinkedLegacy + r.UnresolvedLinks == r.EnabledMonster;
            string summary = (pass ? "PASS" : "FAIL") + ": non-destructive migration audit. " +
                r.EnabledMonster + " active Monster; " + r.EnabledTraits + " active traits; " +
                r.EnabledAbilities + " active abilities; " + r.UnresolvedLinks + " ability-routing warnings; " +
                validation.errors.Count + " catalog validation errors. " +
                "Simulated authored/legacy/unresolved routing " + (routingTestsPass ? "PASS" : "FAIL") + ".\n" +
                "Does not test Play Mode damage, visual effects, or shop odds.";
            if (pass) Debug.Log("[U6.9.1f.4] " + summary);
            else Debug.LogError("[U6.9.1f.4] " + summary + "\n" + r.Text);
            EditorUtility.DisplayDialog("MonsterTactics U6.9.1f.4", summary, "OK");
        }

        // Test the routing decision matrix without mutating the user's .asset.
        private static bool VerifySimulatedRoutes()
        {
            var draft = ScriptableObject.CreateInstance<MonsterTacticsContentCatalog>();
            try
            {
                var p = new MonsterTacticsMonsterRecord { id = "test_family", enabled = true,
                    displayName = "Test", abilityId = "test_cast" };
                var a = new MonsterTacticsAbilityRecord { id = "test_cast", enabled = true,
                    displayName = "Test Cast", useEffectBlocks = true, damageKind = "Magic",
                    primary = new MonsterTacticsAbilityScaling { abilityPower = 2f },
                    legacyHandlerMonster = "Test", hitCount = 1 };
                draft.monster.Add(p);
                draft.abilities.Add(a);
                draft.traits.Add(new MonsterTacticsTraitRecord { id = "trait_test", enabled = true,
                    displayName = "Test Trait" });
                draft.enableLiveAbilities = true;
                var authored = Inspect(draft);
                if (authored.LinkedAuthored != 1 || authored.LinkedLegacy != 0 ||
                    authored.UnresolvedLinks != 0) return false;
                draft.enableLiveAbilities = false;
                var legacy = Inspect(draft);
                if (legacy.LinkedAuthored != 0 || legacy.LinkedLegacy != 1 ||
                    legacy.UnresolvedLinks != 0) return false;
                a.legacyHandlerMonster = "";
                var missing = Inspect(draft);
                return missing.LinkedAuthored == 0 && missing.LinkedLegacy == 0 &&
                    missing.UnresolvedLinks == 1;
            }
            finally { UnityEngine.Object.DestroyImmediate(draft); }
        }

        private void OnEnable() { catalog = MonsterTacticsContentStudioImporter.FindCatalog(); }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("U6.9.1f.4  •  SAFE CONTENT MIGRATION AUDIT", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("The best architecture is catalog data + reusable C# handlers + a retained legacy fallback. " +
                "This audit never deletes code or changes match data. Runtime switches are applied on the next Play Mode session.",
                MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Audit", GUILayout.Height(29)))
                {
                    catalog = MonsterTacticsContentStudioImporter.FindCatalog();
                    report = Inspect(catalog);
                }
                using (new EditorGUI.DisabledScope(catalog == null))
                {
                    if (GUILayout.Button("Validate Catalog", GUILayout.Height(29)))
                    {
                        var check = MonsterTacticsContentStudioValidator.Check(catalog);
                        if (check.Passed) Debug.Log("[U6.9.1f.4] " + check.Summary);
                        else Debug.LogError("[U6.9.1f.4] " + check.Summary + "\n" +
                            String.Join("\n", check.errors.ToArray()));
                        EditorUtility.DisplayDialog("Content Catalog", check.Summary, "OK");
                    }
                    if (GUILayout.Button("Export Report...", GUILayout.Height(29))) Export();
                }
            }
            catalog = catalog == null ? MonsterTacticsContentStudioImporter.FindCatalog() : catalog;
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("No catalog found at the expected Resources/Content path. Import first.", MessageType.Warning);
                return;
            }
            if (report == null) report = Inspect(catalog);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(8);
            Heading("LIVE SOURCE ROUTING — NEXT PLAY MODE");
            Route("Monster Roster", report.MonsterPath,
                "Shop/AI availability, costs, base stats, evolution, form sprite overrides");
            Route("Trait Bonuses", report.TraitPath,
                "Synergy counts, breakpoints, matching-unit buffs; existing combat math executes them");
            Route("Abilities", report.AbilityPath,
                "Only records with Use Effect Blocks are routed to authored handlers");
            EditorGUILayout.Space(8);
            Heading("CATALOG COVERAGE");
            EditorGUILayout.LabelField("Monster: " + report.EnabledMonster + " enabled / " + report.DisabledMonster + " disabled", EditorStyles.label);
            EditorGUILayout.LabelField("Traits: " + report.EnabledTraits + " enabled / " + report.DisabledTraits + " disabled", EditorStyles.label);
            EditorGUILayout.LabelField("Abilities: " + report.EnabledAbilities + " enabled / " + report.DisabledAbilities + " disabled", EditorStyles.label);
            EditorGUILayout.LabelField("Enabled families using authored ability blocks (when globally enabled): " + report.LinkedAuthored, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Enabled families using legacy ability handlers: " + report.LinkedLegacy, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Ability routing issues: " + report.UnresolvedLinks, EditorStyles.label);
            EditorGUILayout.LabelField("Unlinked enabled ability definitions (valid reusable library entries): " + report.UnlinkedAbilities, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(8);
            Heading("REVIEW BEFORE DELETING ANY LEGACY CODE");
            foreach (string s in report.CleanupQueue)
                EditorGUILayout.HelpBox(s, MessageType.None);
            if (report.Findings.Count > 0)
            {
                Heading("WARNINGS / MANUAL CHECKS");
                foreach (string finding in report.Findings)
                    EditorGUILayout.HelpBox(finding, MessageType.Warning);
            }
            else EditorGUILayout.HelpBox("No new audit warnings. This is NOT proof that old handlers are safe to delete.", MessageType.Info);
            EditorGUILayout.Space(12);
            Heading("ROLLBACK — KEEP ALL CONTENT EDITS");
            EditorGUILayout.HelpBox("Restore all three runtime switches to legacy for the NEXT Play Mode session. " +
                "Individual ability effect blocks, Monster records, traits, art references, and edited values stay saved. " +
                "To return, re-enable your switches in Content Studio. Use version control for code rollback.",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying ||
                (catalog.readOnlyLegacyRuntime && !catalog.enableLiveTraits && !catalog.enableLiveAbilities)))
            {
                if (GUILayout.Button("Restore All Legacy Runtime Sources (Next Match)", GUILayout.Height(30)))
                    RestoreLegacy();
            }
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("Exit Play Mode to change runtime source settings.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
        }

        private static void Heading(string value)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
        }
        private static void Route(string title, string mode, string details)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title + ":  " + mode, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(details, EditorStyles.wordWrappedMiniLabel);
            }
        }
        private void RestoreLegacy()
        {
            if (Application.isPlaying || catalog == null) return;
            if (!EditorUtility.DisplayDialog("Restore all-legacy runtime?",
                "Switch Monster roster, traits, and abilities back to legacy for NEXT Play Mode. " +
                "All catalog records/edits and per-ability effect-block settings are preserved.\n\n" +
                "This does not uninstall the Content Studio or delete old/new code.",
                "Restore Legacy", "Cancel")) return;
            Undo.RecordObject(catalog, "Restore all legacy content sources");
            catalog.readOnlyLegacyRuntime = true;
            catalog.enableLiveTraits = false;
            catalog.enableLiveAbilities = false;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            report = Inspect(catalog);
            Debug.Log("[U6.9.1f.4] Next match uses all legacy sources. Catalog edits were preserved.");
        }
        private void Export()
        {
            if (catalog == null) return;
            // Only the user-chosen filesystem location is written; no project assets touched.
            string file = EditorUtility.SaveFilePanel("Export MonsterTactics Migration Audit", "",
                "MonsterTactics_Content_Migration_Audit", "txt");
            if (String.IsNullOrEmpty(file)) return;
            report = Inspect(catalog);
            try
            {
                File.WriteAllText(file, report.Text, new UTF8Encoding(false));
                Debug.Log("[U6.9.1f.4] Migration audit exported to " + file);
            }
            catch (Exception e) { Debug.LogError("[U6.9.1f.4] Could not export audit: " + e.Message); }
        }

        public static AuditReport Inspect(MonsterTacticsContentCatalog c)
        {
            var r = new AuditReport();
            r.CatalogPath = MonsterTacticsContentStudioImporter.AssetPath;
            r.CleanupQueue.Add("KEEP: MonsterTacticsCombatRules, combat director, movement/hex targeting, status calculation, and VFX playback. These are reusable execution logic, not obsolete balance data.");
            r.CleanupQueue.Add("KEEP: MonsterTacticsU5Roster.All, MonsterTacticsTraits.LegacyAll and specialized legacy ability branches until every dependent scene, PvE AI special, and edge case passes regression testing.");
            r.CleanupQueue.Add("CATALOG AUTHORING: Monster costs/stats/types/origins, trait tiers, and supported ability formulas/VFX should live in the ScriptableObject; avoid adding new character-specific switch/case branches.");
            r.CleanupQueue.Add("LATER: retire duplicated value tables only after a Play Mode parity test, scripted scene/asset reference search, backup commit, and successful all-legacy rollback test.");
            if (c == null)
            {
                r.Errors++;
                r.Findings.Add("Catalog is missing. Install/import U6.9.1f.0 before auditing.");
                r.MonsterPath = r.TraitPath = r.AbilityPath = "MISSING CATALOG";
                r.Text = Format(r);
                return r;
            }
            r.MonsterPath = c.readOnlyLegacyRuntime ? "LEGACY" : "CONTENT CATALOG";
            r.TraitPath = c.enableLiveTraits ? "CONTENT CATALOG" : "LEGACY";
            r.AbilityPath = c.enableLiveAbilities ? "HYBRID: opted-in effect blocks + legacy" : "LEGACY";
            if (c.monster == null || c.traits == null || c.abilities == null)
            {
                r.Errors++;
                r.Findings.Add("One or more content lists are missing; run catalog validation.");
                r.Text = Format(r);
                return r;
            }
            var activeAbilities = new Dictionary<string, MonsterTacticsAbilityRecord>(StringComparer.OrdinalIgnoreCase);
            var abilityRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MonsterTacticsAbilityRecord a in c.abilities)
            {
                if (a == null) { r.Errors++; continue; }
                if (!a.enabled) { r.DisabledAbilities++; continue; }
                r.EnabledAbilities++;
                if (!String.IsNullOrEmpty(a.id) && !activeAbilities.ContainsKey(a.id))
                    activeAbilities.Add(a.id, a);
                if (a.useEffectBlocks) r.EffectBlockRecords++;
                else if (!String.IsNullOrWhiteSpace(a.legacyHandlerMonster)) r.LegacyHandlerRecords++;
            }
            foreach (MonsterTacticsTraitRecord t in c.traits)
            {
                if (t == null) { r.Errors++; continue; }
                if (t.enabled) r.EnabledTraits++;
                else r.DisabledTraits++;
                if (t.enabled && t.tiers != null)
                    foreach (MonsterTacticsTraitTierRecord tier in t.tiers)
                        if (tier != null && tier.effects != null)
                            foreach (MonsterTacticsContentEffectRecord e in tier.effects)
                                if (e != null && !String.IsNullOrWhiteSpace(e.vfxPresetId) && c.enableLiveTraits)
                                    r.Findings.Add("Trait aura VFX '" + e.vfxPresetId + "' on " + t.displayName +
                                        " is metadata only; no runtime aura handler yet.");
            }
            foreach (MonsterTacticsMonsterRecord p in c.monster)
            {
                if (p == null) { r.Errors++; continue; }
                if (!p.enabled) { r.DisabledMonster++; continue; }
                r.EnabledMonster++;
                MonsterTacticsAbilityRecord a;
                if (!String.IsNullOrEmpty(p.abilityId)) abilityRefs.Add(p.abilityId);
                if (String.IsNullOrEmpty(p.abilityId) || !activeAbilities.TryGetValue(p.abilityId, out a))
                {
                    r.UnresolvedLinks++;
                    r.Findings.Add("Enabled Monster " + (p.displayName ?? p.id) +
                        " references a missing/disabled ability: " + (p.abilityId ?? "(none)"));
                    continue;
                }
                if (c.enableLiveAbilities && a.useEffectBlocks)
                {
                    string issue;
                    if (!MonsterTacticsContentAbilityRuntime.Valid(a, out issue))
                    {
                        r.UnresolvedLinks++;
                        r.Findings.Add("Authored ability invalid for " + p.displayName + ": " + issue);
                    }
                    else r.LinkedAuthored++;
                }
                else
                {
                    r.LinkedLegacy++;
                    bool hasLegacy = !String.IsNullOrWhiteSpace(a.legacyHandlerMonster);
                    if (!hasLegacy)
                    {
                        r.UnresolvedLinks++;
                        r.LinkedLegacy--;
                        r.Findings.Add("Enabled Monster " + p.displayName + " has no executable authored " +
                            "effect blocks AND no known legacy handler. Turn on Live Abilities + Use Effect Blocks, " +
                            "or link an implemented legacy special.");
                    }
                }
            }
            foreach (MonsterTacticsAbilityRecord a in c.abilities)
                if (a != null && a.enabled && !String.IsNullOrEmpty(a.id) && !abilityRefs.Contains(a.id))
                    r.UnlinkedAbilities++;
            if (!c.readOnlyLegacyRuntime && !c.enableLiveAbilities)
                r.Findings.Add("Live Monster roster is on but Live Abilities is off. Any NEW Monster without a " +
                    "dedicated legacy cast handler may use a fallback instead of its authored ability.");
            if (c.enableLiveAbilities && r.LinkedAuthored == 0)
                r.Findings.Add("Live Abilities is enabled, but no enabled Monster is using authored effect blocks.");
            if (c.readOnlyLegacyRuntime && (c.enableLiveTraits || c.enableLiveAbilities))
                r.Findings.Add("Mixed-mode setup: legacy shop roster with catalog traits/abilities. This can be " +
                    "intentional; verify disabled catalog families against the active roster.");
            r.Text = Format(r);
            return r;
        }

        private static string Format(AuditReport r)
        {
            var b = new StringBuilder();
            b.AppendLine("MonsterTactics U6.9.1f.4 — Migration Audit (read-only)");
            b.AppendLine("Catalog: " + r.CatalogPath);
            b.AppendLine("Roster: " + r.MonsterPath);
            b.AppendLine("Traits: " + r.TraitPath);
            b.AppendLine("Abilities: " + r.AbilityPath);
            b.AppendLine("Enabled/Disabled Monster: " + r.EnabledMonster + "/" + r.DisabledMonster);
            b.AppendLine("Enabled/Disabled Traits: " + r.EnabledTraits + "/" + r.DisabledTraits);
            b.AppendLine("Enabled/Disabled Abilities: " + r.EnabledAbilities + "/" + r.DisabledAbilities);
            b.AppendLine("Enabled Monster linked to active authored casts: " + r.LinkedAuthored);
            b.AppendLine("Enabled Monster linked to legacy casts: " + r.LinkedLegacy);
            b.AppendLine("Unresolved ability links: " + r.UnresolvedLinks);
            b.AppendLine("Enabled abilities not referenced by enabled catalog Monster: " + r.UnlinkedAbilities);
            b.AppendLine("\nCleanup rules:");
            foreach (string s in r.CleanupQueue) b.AppendLine(" - " + s);
            b.AppendLine("\nWarnings / manual checks:");
            foreach (string s in r.Findings) b.AppendLine(" - " + s);
            if (r.Findings.Count == 0) b.AppendLine(" - None in the static catalog audit.");
            b.AppendLine("\nNOTE: Passing this static audit does not authorize deleting legacy branches.");
            return b.ToString();
        }
    }
}
#endif
