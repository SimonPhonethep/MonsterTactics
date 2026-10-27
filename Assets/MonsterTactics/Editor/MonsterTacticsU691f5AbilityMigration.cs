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
    /// U6.9.1f.5: Explicit, non-destructive legacy -> authored ability templates.
    /// The legacy evaluator stays available for parity checking and rollback.
    /// No migration occurs merely by opening the window.
    /// </summary>
    public static class MonsterTacticsU691f5AbilityMigration
    {
        public static readonly string[] LegacyFamilies = {
            "Ripplet", "Emberlet", "Budhorn", "Flarbit", "Mistrip",
            "Pebblin", "Mosslet", "Mysprit", "Tusklet", "Dunebit", "Voltrix", "Pyrothane", "Aqualith"
        };

        public static bool IsSpecialized(string family)
        {
            // Movement/backline locking stays in MonsterTacticsCombatDirector. Not effect blocks.
            return family == "Ripplet" || family == "Dunebit";
        }

        public static bool TryTemplate(string family, out MonsterTacticsAbilityRecord record, out string note)
        {
            record = null;
            note = "";
            if (String.IsNullOrEmpty(family)) return false;
            var stats = MonsterTacticsCombatRules.ForMonster(family);
            var legacy = MonsterTacticsCombatRules.LegacyAbilityFor(family, stats);
            var candidate = new MonsterTacticsAbilityRecord {
                id = "template_" + family.ToLowerInvariant(),
                enabled = true,
                displayName = legacy.Name,
                description = legacy.Description,
                formula = legacy.Formula,
                damageKind = legacy.DamageKind,
                useEffectBlocks = true,
                primary = new MonsterTacticsAbilityScaling(),
                splash = new MonsterTacticsAbilityScaling(),
                selfHeal = new MonsterTacticsAbilityScaling(),
                selfShield = new MonsterTacticsAbilityScaling(),
                hitCount = legacy.HitCount,
                splashRadius = legacy.SplashRadius,
                maximumSecondaryTargets = legacy.MaximumSecondaryTargets,
                burnDuration = legacy.BurnDuration,
                burnMaxHpPerSecond = legacy.BurnMaxHpPerSecond,
                woundDuration = legacy.WoundDuration,
                vfxScale = 1f,
                vfxLifetime = 1.2f,
                vfxPreset = MonsterTacticsAbilityVfxPreset.Water
            };
            switch (family)
            {
                case "Ripplet":
                    candidate.primary.flat = 20f; candidate.primary.attack = 1.65f;
                    candidate.vfxPreset = MonsterTacticsAbilityVfxPreset.Water;
                    note = "SPECIALIZED: Aqua Jet's dash stays in the combat director; compare visual movement manually.";
                    break;
                case "Emberlet":
                    candidate.primary.abilityPower = 2f; candidate.splash.abilityPower = .65f;
                    candidate.vfxPreset = MonsterTacticsAbilityVfxPreset.Fire;
                    break;
                case "Budhorn":
                    candidate.primary.abilityPower = 1.4f; candidate.selfHeal.abilityPower = .75f;
                    candidate.vfxPreset = MonsterTacticsAbilityVfxPreset.Grass;
                    break;
                case "Flarbit":
                    candidate.primary.abilityPower = 2f; candidate.splash.abilityPower = .80f;
                    candidate.vfxPreset = MonsterTacticsAbilityVfxPreset.Fire;
                    break;
                case "Mistrip":
                    candidate.primary.flat = 22f; candidate.primary.attack = .60f;
                    candidate.vfxPreset = MonsterTacticsAbilityVfxPreset.Water;
                    break;
                case "Pebblin":
                    candidate.selfShield.flat = 90f; candidate.selfShield.abilityPower = .70f;
                    candidate.vfxPreset = MonsterTacticsAbilityVfxPreset.Rock;
                    break;
                case "Mosslet":
                    candidate.primary.flat = 25f; candidate.primary.attack = 1.70f;
                    candidate.vfxPreset = MonsterTacticsAbilityVfxPreset.Grass;
                    break;
                case "Mysprit":
                    candidate.primary.abilityPower = 1.85f; candidate.splash.abilityPower = 1.85f;
                    candidate.vfxPreset = MonsterTacticsAbilityVfxPreset.Psychic;
                    break;
                case "Tusklet":
                    candidate.primary.flat = 30f; candidate.primary.attack = 1.80f;
                    candidate.vfxPreset = MonsterTacticsAbilityVfxPreset.Dragon;
                    break;
                case "Dunebit":
                    candidate.primary.flat = 30f; candidate.primary.attack = 1.05f;
                    candidate.vfxPreset = MonsterTacticsAbilityVfxPreset.Dragon;
                    note = "SPECIALIZED: Dragon Rush backline retarget and dash stay in the combat director.";
                    break;
                case "Voltrix":
                    candidate.primary.abilityPower = 2.30f; candidate.splash.abilityPower = .85f;
                    candidate.vfxPreset = MonsterTacticsAbilityVfxPreset.Electric;
                    break;
                case "Pyrothane":
                    candidate.primary.flat = 45f; candidate.primary.attack = 1.75f;
                    candidate.splash.attack = .65f;
                    candidate.vfxPreset = MonsterTacticsAbilityVfxPreset.Fire;
                    break;
                case "Aqualith":
                    candidate.primary.abilityPower = 1.40f;
                    candidate.selfShield.flat = 160f; candidate.selfShield.abilityPower = 1.20f;
                    candidate.vfxPreset = MonsterTacticsAbilityVfxPreset.Ice;
                    break;
                default: return false;
            }
            string issue;
            if (!MonsterTacticsContentAbilityRuntime.Valid(candidate, out issue))
            {
                note = "Template rejected: " + issue;
                return false;
            }
            record = candidate;
            return true;
        }

        public sealed class Parity
        {
            public int Checks;
            public float LargestDifference;
            public readonly List<string> Differences = new List<string>();
            public bool Passed { get { return Differences.Count == 0; } }
            public string Summary {
                get {
                    return (Passed ? "PASS" : "DIFF") + " (" + Checks + " comparisons; max numeric delta " +
                        LargestDifference.ToString("0.###") + ")";
                }
            }
        }

        public static Parity Compare(string family, MonsterTacticsAbilityRecord record)
        {
            var report = new Parity();
            if (record == null)
            {
                report.Differences.Add("No ability record found.");
                return report;
            }
            // Compare an in-memory copy so migration previews work even when the
            // real ability is opted OUT. No serialized state is changed.
            var staged = JsonUtility.FromJson<MonsterTacticsAbilityRecord>(JsonUtility.ToJson(record));
            staged.useEffectBlocks = true;
            string error;
            if (!MonsterTacticsContentAbilityRuntime.Valid(staged, out error))
            {
                report.Differences.Add("Invalid authored profile: " + error);
                return report;
            }
            var baseline = MonsterTacticsCombatRules.ForMonster(family);
            foreach (float attackScale in new[] { .75f, 1f, 1.5f, 2f })
            {
                foreach (float ap in new[] { 70f, 100f, 175f })
                {
                    var stats = baseline;
                    stats.Attack = baseline.Attack * attackScale;
                    stats.AbilityPower = ap;
                    var oldProfile = MonsterTacticsCombatRules.LegacyAbilityFor(family, stats);
                    var newProfile = MonsterTacticsContentAbilityRuntime.EvaluateRecord(staged, stats);
                    string context = "ADx" + attackScale.ToString("0.##") + " AP " + ap.ToString("0");
                    Difference(report, context + " primary", oldProfile.PrimaryRaw, newProfile.PrimaryRaw);
                    Difference(report, context + " splash", oldProfile.SplashRaw, newProfile.SplashRaw);
                    Difference(report, context + " heal", oldProfile.HealRaw, newProfile.HealRaw);
                    Difference(report, context + " shield", oldProfile.ShieldRaw, newProfile.ShieldRaw);
                    Difference(report, context + " burn/sec", oldProfile.BurnMaxHpPerSecond, newProfile.BurnMaxHpPerSecond);
                    Difference(report, context + " burn duration", oldProfile.BurnDuration, newProfile.BurnDuration);
                    Difference(report, context + " wound duration", oldProfile.WoundDuration, newProfile.WoundDuration);
                    Same(report, context + " hit count", oldProfile.HitCount, newProfile.HitCount);
                    Same(report, context + " radius", oldProfile.SplashRadius, newProfile.SplashRadius);
                    Same(report, context + " target cap", oldProfile.MaximumSecondaryTargets,
                        newProfile.MaximumSecondaryTargets);
                    if (oldProfile.DamageKind != newProfile.DamageKind)
                        Add(report, context + ": damage type " + oldProfile.DamageKind + " → " + newProfile.DamageKind);
                    report.Checks++;
                }
            }
            return report;
        }
        private static void Add(Parity report, string message)
        {
            // Limit printed details without hiding the fact of a mismatch.
            if (report.Differences.Count < 18) report.Differences.Add(message);
        }
        private static void Same(Parity report, string label, int want, int got)
        {
            if (want != got) Add(report, label + ": " + want + " → " + got);
            report.Checks++;
        }
        private static void Difference(Parity report, string label, float want, float got)
        {
            float delta = Mathf.Abs(got - want);
            report.LargestDifference = Mathf.Max(report.LargestDifference, delta);
            if (delta > .005f) Add(report, label + ": " + want.ToString("0.###") + " → " + got.ToString("0.###"));
            report.Checks++;
        }

        public static MonsterTacticsAbilityRecord FindAbility(MonsterTacticsContentCatalog catalog, string family)
        {
            if (catalog == null || catalog.monster == null || catalog.abilities == null) return null;
            foreach (var monster in catalog.monster)
            {
                if (monster == null || monster.forms == null || monster.forms.Count == 0 ||
                    monster.forms[0] == null || !String.Equals(monster.forms[0].name, family,
                    StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var ability in catalog.abilities)
                    if (ability != null && String.Equals(ability.id, monster.abilityId,
                        StringComparison.OrdinalIgnoreCase)) return ability;
            }
            return null;
        }

        public static void CopyTemplate(MonsterTacticsAbilityRecord destination, MonsterTacticsAbilityRecord template)
        {
            // Deliberately preserve id, displayName, description, notes and prefab.
            // Copying a legacy template must not silently erase artist/designer work.
            destination.damageKind = template.damageKind;
            destination.primary = Clone(template.primary);
            destination.splash = Clone(template.splash);
            destination.selfHeal = Clone(template.selfHeal);
            destination.selfShield = Clone(template.selfShield);
            destination.hitCount = template.hitCount;
            destination.splashRadius = template.splashRadius;
            destination.maximumSecondaryTargets = template.maximumSecondaryTargets;
            destination.burnDuration = template.burnDuration;
            destination.burnMaxHpPerSecond = template.burnMaxHpPerSecond;
            destination.woundDuration = template.woundDuration;
            destination.vfxPreset = template.vfxPreset;
            // Activation is a separate, explicit step.
            destination.useEffectBlocks = false;
        }
        private static MonsterTacticsAbilityScaling Clone(MonsterTacticsAbilityScaling value)
        {
            return new MonsterTacticsAbilityScaling { flat = value.flat, attack = value.attack,
                abilityPower = value.abilityPower };
        }

        [MenuItem("MonsterTactics/Tests/Archive/Content Studio/U6.9.1f.5 Verify Ability Migration Templates")]
        public static void Verify()
        {
            int checks = 0, special = 0;
            var catalog = MonsterTacticsContentStudioImporter.FindCatalog();
            if (catalog == null) throw new InvalidOperationException("Content Catalog missing. Import f.0 first.");
            foreach (string family in LegacyFamilies)
            {
                MonsterTacticsAbilityRecord template;
                string note;
                if (!TryTemplate(family, out template, out note))
                    throw new InvalidOperationException("No valid migration template for " + family + ": " + note);
                var parity = Compare(family, template);
                if (!parity.Passed)
                    throw new InvalidOperationException(family + ": parity failed: " +
                        String.Join(" | ", parity.Differences.ToArray()));
                if (FindAbility(catalog, family) == null)
                    throw new InvalidOperationException("Missing linked ability for " + family);
                checks += parity.Checks;
                if (IsSpecialized(family)) special++;
            }
            if (special != 2) throw new InvalidOperationException("Specialized classification unexpectedly changed.");
            // Detect deliberate formula changes, without treating them as incorrect in normal authoring.
            MonsterTacticsAbilityRecord changed; string info;
            TryTemplate("Emberlet", out changed, out info);
            changed.primary.abilityPower += 1f;
            if (Compare("Emberlet", changed).Passed)
                throw new InvalidOperationException("Regression: changed damage formula was not detected.");
            // Rollback is a flag only, retaining authored fields.
            changed.useEffectBlocks = false;
            if (changed.primary.abilityPower != 3f)
                throw new InvalidOperationException("Rollback unexpectedly changed authored settings.");
            Debug.Log("[U6.9.1f.5] PASS: " + LegacyFamilies.Length +
                " migration templates; " + checks + " legacy/authored profile comparisons; " +
                special + " specialized movement abilities flagged. No catalog changes.");
            EditorUtility.DisplayDialog("MonsterTactics U6.9.1f.5",
                "PASS: " + LegacyFamilies.Length + " templates; " + checks +
                " profile comparisons. Two specialized movement abilities remain C# backed.\n\n" +
                "This does NOT prove live combat damage or VFX parity. Test in Play Mode.", "OK");
        }
    }

    public sealed class MonsterTacticsU691f5AbilityMigrationWindow : EditorWindow
    {
        private MonsterTacticsContentCatalog catalog;
        private int selected = 1; // Emberlet first
        private Vector2 scroll, rightScroll;
        private string search = "";
        private MonsterTacticsU691f5AbilityMigration.Parity result;

        [MenuItem("MonsterTactics/Content Studio/U6.9.1f.5 Ability Migration & Testing")]
        public static void Open()
        {
            var window = GetWindow<MonsterTacticsU691f5AbilityMigrationWindow>("Ability Migration");
            window.minSize = new Vector2(840f, 560f);
            window.Show();
        }
        private void OnEnable() { catalog = MonsterTacticsContentStudioImporter.FindCatalog(); }
        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("U6.9.1f.5  •  ABILITY MIGRATION & TESTING", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("All original combat handlers remain intact. Importing only copies reusable effect values; " +
                "it does NOT enable them or delete old formulas. Enable one ability at a time, then restart Play Mode.",
                MessageType.Info);
            catalog = catalog == null ? MonsterTacticsContentStudioImporter.FindCatalog() : catalog;
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("No Content Catalog found. Run U6.9.1f.0 Import Legacy Snapshot.", MessageType.Error);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run 13-Ability Template Tests", GUILayout.Height(27)))
                    MonsterTacticsU691f5AbilityMigration.Verify();
                if (GUILayout.Button("Open Content Studio", GUILayout.Width(150), GUILayout.Height(27)))
                    MonsterTacticsContentStudioWindow.Open();
                if (GUILayout.Button("Export Status", GUILayout.Width(110), GUILayout.Height(27))) Export();
            }
            EditorGUILayout.HelpBox("Live Abilities global switch: " +
                (catalog.enableLiveAbilities ? "ON — authored records run next match" :
                    "OFF — all abilities run legacy until you enable it in Content Studio") +
                ". Per-ability toggle still controls which casts opt in.",
                catalog.enableLiveAbilities ? MessageType.Info : MessageType.Warning);
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(230f)))
                {
                    search = EditorGUILayout.TextField("Search", search);
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    for (int i = 0; i < MonsterTacticsU691f5AbilityMigration.LegacyFamilies.Length; i++)
                    {
                        string family = MonsterTacticsU691f5AbilityMigration.LegacyFamilies[i];
                        if (!String.IsNullOrEmpty(search) && family.IndexOf(search,
                            StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var ability = MonsterTacticsU691f5AbilityMigration.FindAbility(catalog, family);
                        string label = (ability == null ? "? " : ability.useEffectBlocks ? "● " : "○ ") + family;
                        if (GUILayout.Toggle(selected == i, label, EditorStyles.toolbarButton))
                        {
                            if (selected != i) { selected = i; result = null; }
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
                    DrawSelected();
                    EditorGUILayout.EndScrollView();
                }
            }
        }
        private void DrawSelected()
        {
            string family = MonsterTacticsU691f5AbilityMigration.LegacyFamilies[selected];
            var record = MonsterTacticsU691f5AbilityMigration.FindAbility(catalog, family);
            if (record == null)
            {
                EditorGUILayout.HelpBox("No linked ability record found for " + family +
                    ". Restore the link in Content Studio before migrating.", MessageType.Error);
                return;
            }
            var legacyStats = MonsterTacticsCombatRules.ForMonster(family);
            var legacy = MonsterTacticsCombatRules.LegacyAbilityFor(family, legacyStats);
            MonsterTacticsAbilityRecord template; string note;
            bool available = MonsterTacticsU691f5AbilityMigration.TryTemplate(family, out template, out note);
            EditorGUILayout.LabelField(family + " — " + legacy.Name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Ability Id: " + record.id, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Legacy: " + legacy.Formula, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(record.useEffectBlocks ?
                "Current: AUTHORED effect blocks (if global switch is ON)" :
                "Current: LEGACY combat handler", EditorStyles.boldLabel);
            if (MonsterTacticsU691f5AbilityMigration.IsSpecialized(family))
                EditorGUILayout.HelpBox(note + " Damage can be authored, but movement/targeting and original " +
                    "specialized visuals require a Play Mode comparison. Do not delete old handlers.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(Application.isPlaying || !available))
            {
                if (GUILayout.Button("Copy Legacy Values to Effect Blocks (Stay Legacy)", GUILayout.Height(29)))
                    Copy(family, record, template);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Enable Authored for NEXT Match", GUILayout.Height(27)))
                        Enable(family, record);
                    using (new EditorGUI.DisabledScope(!record.useEffectBlocks))
                    {
                        if (GUILayout.Button("Rollback This Ability to Legacy", GUILayout.Height(27)))
                            Rollback(record);
                    }
                }
            }
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("Exit Play Mode to edit ability assets. In-game testing uses the " +
                    "snapshot from the start of the current Play Mode session.", MessageType.Info);
            EditorGUILayout.Space(5);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Compare Current Formula to Legacy", GUILayout.Height(26)))
                    result = MonsterTacticsU691f5AbilityMigration.Compare(family, record);
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("Preview VFX (No Damage)", GUILayout.Height(26)))
                        PreviewVfx(record.displayName);
                }
            }
            if (result != null)
            {
                EditorGUILayout.HelpBox(result.Summary +
                    (result.Passed ? " — numeric profile parity across 12 effective-stat samples." :
                        " — differences may be intentional edits; review before enabling."),
                    result.Passed ? MessageType.Info : MessageType.Warning);
                foreach (string line in result.Differences)
                    EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Live combat verification", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. Exit Play Mode. Copy formula, compare, and enable one ability.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("2. Enable Live Abilities globally in Content Studio; restart Play Mode.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("3. Cast on a real enemy. Compare damage, splash/targets, healing/shields/status, " +
                "and VFX against a legacy-mode run. Item/trait/crit randomness affects final HP.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("4. If incorrect, exit Play Mode and use per-ability Rollback. " +
                "Your authored coefficients stay saved.", EditorStyles.wordWrappedLabel);
        }
        private void Copy(string family, MonsterTacticsAbilityRecord dest, MonsterTacticsAbilityRecord template)
        {
            if (!EditorUtility.DisplayDialog("Copy legacy formula?",
                "Copy " + family + "'s supported legacy coefficients, status fields and preset " +
                "into its Content Catalog ability record?\n\n" +
                "This OVERWRITES existing authored coefficients but retains name, description, " +
                "prefab, notes and ID. Use Undo or a version-controlled backup if needed. " +
                "The original C# handler remains active.", "Copy Values", "Cancel")) return;
            Undo.RecordObject(catalog, "Import legacy ability coefficients");
            MonsterTacticsU691f5AbilityMigration.CopyTemplate(dest, template);
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            result = null;
            Debug.Log("[U6.9.1f.5] Copied " + family + " coefficients; legacy execution retained.");
        }
        private void Enable(string family, MonsterTacticsAbilityRecord record)
        {
            var test = MonsterTacticsU691f5AbilityMigration.Compare(family, record);
            string message = (test.Passed ?
                "Formula matches legacy numeric profiles in all test samples. " :
                "WARNING: Current coefficients DIFFER from legacy. " + test.Summary +
                " (this might be an intentional balance edit). ") +
                "\n\nEnable the authored profile for the NEXT Play Mode match?" +
                (MonsterTacticsU691f5AbilityMigration.IsSpecialized(family) ?
                    "\n\nSPECIALIZED: existing dash/backline logic is still hardcoded. Compare movement in Play Mode." : "") +
                (catalog.enableLiveAbilities ? "" :
                    "\n\nGlobal Live Abilities is OFF; enable it separately in Content Studio.");
            string reason;
            var staged = JsonUtility.FromJson<MonsterTacticsAbilityRecord>(JsonUtility.ToJson(record));
            staged.useEffectBlocks = true;
            if (!MonsterTacticsContentAbilityRuntime.Valid(staged, out reason))
            {
                EditorUtility.DisplayDialog("Invalid authored ability", reason, "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Opt into authored gameplay?", message,
                "Enable Next Match", "Cancel")) return;
            Undo.RecordObject(catalog, "Enable authored ability");
            record.useEffectBlocks = true;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            result = MonsterTacticsU691f5AbilityMigration.Compare(family, record);
        }
        private void Rollback(MonsterTacticsAbilityRecord record)
        {
            if (!EditorUtility.DisplayDialog("Revert this cast to legacy?",
                "Next match uses this ability's original C# damage and VFX. " +
                "Authored coefficients and prefab references remain saved.", "Use Legacy", "Cancel")) return;
            Undo.RecordObject(catalog, "Rollback ability to legacy");
            record.useEffectBlocks = false;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            result = null;
        }
        private void PreviewVfx(string abilityName)
        {
            var vfx = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsAbilityVfx>();
            var units = UnityEngine.Object.FindObjectsByType<MonsterUnit>();
            if (vfx == null || units == null || units.Length < 2)
            {
                EditorUtility.DisplayDialog("VFX Preview", "Enter Play Mode and ensure two " +
                    "Monster are present and the ability VFX component exists.", "OK");
                return;
            }
            vfx.PlayCast(units[0], units[1], abilityName, .4f);
            vfx.PlayImpact(units[1], abilityName);
            Debug.Log("[U6.9.1f.5] Visual-only preview of '" + abilityName +
                "'. No mana, cooldown, HP or combat status changed. Preview uses current Play Mode snapshot.");
        }
        private void Export()
        {
            string path = EditorUtility.SaveFilePanel("Export Ability Migration Status", "",
                "MonsterTactics_Ability_Migration_Status", "txt");
            if (String.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("MonsterTactics U6.9.1f.5 — Ability migration snapshot");
            sb.AppendLine("Global Live Abilities: " + catalog.enableLiveAbilities);
            foreach (string family in MonsterTacticsU691f5AbilityMigration.LegacyFamilies)
            {
                var ability = MonsterTacticsU691f5AbilityMigration.FindAbility(catalog, family);
                sb.Append(family).Append(": ").Append(ability == null ? "MISSING" :
                    ability.useEffectBlocks ? "AUTHORED" : "LEGACY");
                if (ability != null && ability.useEffectBlocks)
                    sb.Append(" — ").Append(MonsterTacticsU691f5AbilityMigration.Compare(family, ability).Summary);
                if (MonsterTacticsU691f5AbilityMigration.IsSpecialized(family))
                    sb.Append(" [specialized dash/targeting still in C#]");
                sb.AppendLine();
            }
            try { File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false)); }
            catch (Exception exception) { Debug.LogError("Export failed: " + exception.Message); }
        }
    }
}
#endif
