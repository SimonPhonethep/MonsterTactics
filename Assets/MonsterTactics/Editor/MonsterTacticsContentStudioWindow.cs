#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>U6.9.1f.6 non-destructive Content Studio authoring and runtime-status UI.</summary>
    public sealed class MonsterTacticsContentStudioWindow : EditorWindow
    {
        private enum Tab { Monster, Traits, Abilities }
        private Tab tab;
        private MonsterTacticsContentCatalog catalog;
        private Vector2 leftScroll, rightScroll;
        private string search = "";
        private int selected = -1;
        private bool enabledOnly;
        private bool showValidation;
        private MonsterTacticsContentStudioValidator.Report validation;
        private bool showRuntimeSettings;
        private bool identityOpen = true;
        private bool statsOpen;
        private bool formsOpen;
        private bool originsOpen;
        private bool tiersOpen = true;
        private bool effectOpen = true;
        private bool vfxOpen;
        private bool legacyOpen;
        private bool notesOpen;

        [MenuItem("MonsterTactics/Content Studio/U6.9.1f.0 Open Content Studio")]
        public static void Open()
        {
            var window = GetWindow<MonsterTacticsContentStudioWindow>("MonsterTactics Content Studio");
            window.minSize = new Vector2(900f, 550f);
            window.Show();
        }

        private void OnEnable()
        {
            catalog = MonsterTacticsContentStudioImporter.FindCatalog();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("MONSTERTACTICS CONTENT STUDIO  •  U6.9.1f.6", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Edit catalog data here; runtime source switches apply to NEXT Play Mode. " +
                "Monster and traits may use Content Catalog while individual abilities stay Legacy. " +
                "All legacy gameplay handlers remain installed.", MessageType.Info);
            EditorGUILayout.Space(3);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                if (GUILayout.Button("Import / Add Missing Legacy Data", GUILayout.Height(28)))
                {
                    int p, t, a;
                    catalog = MonsterTacticsContentStudioImporter.Import(out p, out t, out a);
                    selected = -1;
                    validation = null;
                    Debug.Log("[Content Studio] Added " + p + " Monster / " + t + " traits / " +
                        a + " abilities; existing edits untouched.");
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("Migration Audit", GUILayout.Width(115), GUILayout.Height(28)))
                    MonsterTacticsContentMigrationAudit.Open();
                if (GUILayout.Button("Ability Migration", GUILayout.Width(125), GUILayout.Height(28)))
                    MonsterTacticsU691f5AbilityMigrationWindow.Open();
                using (new EditorGUI.DisabledScope(catalog == null))
                {
                    if (GUILayout.Button("Validate", GUILayout.Width(95), GUILayout.Height(28)))
                    {
                        validation = MonsterTacticsContentStudioValidator.Check(catalog);
                        showValidation = true;
                    }
                    if (GUILayout.Button("Select Catalog", GUILayout.Width(125), GUILayout.Height(28)))
                        Selection.activeObject = catalog;
                }
            }
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("No catalog yet. Click Import / Add Missing Legacy Data.", MessageType.Warning);
                return;
            }
            EditorGUILayout.Space(5);
            DrawRuntimeSummary();
            showRuntimeSettings = EditorGUILayout.Foldout(showRuntimeSettings,
                "Runtime Sources & Rollback (next match)", true);
            if (showRuntimeSettings)
            {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool useCatalog = !catalog.readOnlyLegacyRuntime;
                EditorGUILayout.LabelField("Monster Runtime Source", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(useCatalog ? "CONTENT CATALOG (next Play Mode)" :
                    "LEGACY ROSTER (safe default)", EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    bool requested = EditorGUILayout.ToggleLeft("Use Content Catalog in new matches", useCatalog);
                    if (requested != useCatalog)
                    {
                        if (requested)
                        {
                            var check = MonsterTacticsContentStudioValidator.Check(catalog);
                            if (!check.Passed)
                            {
                                EditorUtility.DisplayDialog("Cannot enable live roster",
                                    check.Summary + "\nFix validation errors first.", "OK");
                            }
                            else if (EditorUtility.DisplayDialog("Enable live Monster roster?",
                                "The next Play Mode match will use enabled catalog Monster for shop/AI, " +
                                "costs, numeric combat stats, and evolutions. Traits have a SEPARATE " +
                                "opt-in switch below. You can revert here at any time.",
                                "Enable", "Cancel"))
                            {
                                Undo.RecordObject(catalog, "Enable catalog runtime");
                                catalog.readOnlyLegacyRuntime = false;
                                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
                            }
                        }
                        else
                        {
                            Undo.RecordObject(catalog, "Restore legacy runtime");
                            catalog.readOnlyLegacyRuntime = true;
                            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
                        }
                    }
                }
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Trait Runtime Source", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(catalog.enableLiveTraits ?
                    "CONTENT TRAITS (new matches: gameplay buffs active)" :
                    "LEGACY TRAITS (safe default)", EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    bool requested = EditorGUILayout.ToggleLeft(
                        "Use Content Traits in new matches", catalog.enableLiveTraits);
                    if (requested != catalog.enableLiveTraits)
                    {
                        if (requested)
                        {
                            var check = MonsterTacticsContentStudioValidator.Check(catalog);
                            if (!check.Passed)
                            {
                                EditorUtility.DisplayDialog("Cannot enable live traits",
                                    check.Summary + "\nFix validation errors first.", "OK");
                            }
                            else if (EditorUtility.DisplayDialog("Enable live traits?",
                                "NEW Play Mode matches will use trait definitions, origins, " +
                                "breakpoints and stat buffs from this Content Catalog. " +
                                "These change actual attack damage, HP, AP and other " +
                                "combat calculations. Ability casts are configured separately below. " +
                                "Exit Play Mode to edit safely.", "Enable", "Cancel"))
                            {
                                Undo.RecordObject(catalog, "Enable live trait runtime");
                                catalog.enableLiveTraits = true;
                                EditorUtility.SetDirty(catalog);
                                AssetDatabase.SaveAssets();
                            }
                        }
                        else
                        {
                            Undo.RecordObject(catalog, "Restore legacy trait runtime");
                            catalog.enableLiveTraits = false;
                            EditorUtility.SetDirty(catalog);
                            AssetDatabase.SaveAssets();
                        }
                    }
                }
                if (catalog.enableLiveTraits)
                    EditorGUILayout.LabelField(
                        "Each trait's Tiers > Effects applies to combat. 0.30 = +30% for multipliers.",
                        EditorStyles.wordWrappedMiniLabel);
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Ability Runtime Source", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(catalog.enableLiveAbilities ?
                    "CONTENT ABILITIES (only records with Use Effect Blocks enabled)" :
                    "LEGACY ABILITIES (safe default)", EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    bool requested = EditorGUILayout.ToggleLeft(
                        "Use Content Abilities in new matches", catalog.enableLiveAbilities);
                    if (requested != catalog.enableLiveAbilities)
                    {
                        if (requested)
                        {
                            var report = MonsterTacticsContentStudioValidator.Check(catalog);
                            if (!report.Passed)
                                EditorUtility.DisplayDialog("Cannot enable live abilities",
                                    report.Summary + "\nFix validation errors first.", "OK");
                            else if (EditorUtility.DisplayDialog("Enable authored ability effects?",
                                "Only abilities with Use Effect Blocks checked will run new combat " +
                                "formulas/VFX in the NEXT Play Mode session. All other abilities " +
                                "retain original code. Legacy movement abilities still use " +
                                "their original dash logic.", "Enable", "Cancel"))
                            {
                                Undo.RecordObject(catalog, "Enable authored ability runtime");
                                catalog.enableLiveAbilities = true;
                                EditorUtility.SetDirty(catalog);
                                AssetDatabase.SaveAssets();
                            }
                        }
                        else
                        {
                            Undo.RecordObject(catalog, "Restore legacy ability runtime");
                            catalog.enableLiveAbilities = false;
                            EditorUtility.SetDirty(catalog);
                            AssetDatabase.SaveAssets();
                        }
                    }
                }
                if (tab == Tab.Abilities)
                    EditorGUILayout.HelpBox("To customize a cast: select an Ability, check Use Effect Blocks, " +
                        "configure Primary/Splash/Self Heal/Self Shield coefficients (2 = 200% AP/AD), " +
                        "and choose a VFX Preset or optional Prefab. The linked Monster's Ability Id " +
                        "must point to that record. Imported abilities remain legacy until opted in.",
                        MessageType.None);
            }
            }
            else
                EditorGUILayout.LabelField("Expand to change Legacy / Content switches; existing choices are preserved.",
                    EditorStyles.miniLabel);
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("PLAY MODE: authoring is read-only. Exit Play Mode to edit saved content.", MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawTabButton(Tab.Monster, "Monster (" + Count(Tab.Monster) + ")");
                DrawTabButton(Tab.Traits, "Traits (" + Count(Tab.Traits) + ")");
                DrawTabButton(Tab.Abilities, "Abilities (" + Count(Tab.Abilities) + ")");
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Legacy Comparison", EditorStyles.toolbarButton))
                    MonsterTacticsContentStudioValidator.CompareLegacy();
            }
            if (showValidation && validation != null)
            {
                EditorGUILayout.HelpBox(validation.Summary + "\n" +
                    (validation.errors.Count > 0 ? String.Join("\n", validation.errors.ToArray()) :
                        "No blocking catalog errors."),
                    validation.Passed ? MessageType.Info : MessageType.Error);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Catalog warnings: " + validation.warnings.Count,
                        EditorStyles.miniLabel);
                    if (GUILayout.Button("Dismiss", GUILayout.Width(80))) showValidation = false;
                }
            }
            EditorGUILayout.Space(5);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                GUILayout.Space(8);
                DrawDetails();
            }
        }

        private void DrawTabButton(Tab which, string label)
        {
            if (GUILayout.Toggle(tab == which, label, EditorStyles.toolbarButton, GUILayout.Width(150)) && tab != which)
            {
                tab = which;
                selected = -1;
                validation = null;
                rightScroll = Vector2.zero;
                identityOpen = true;
            }
        }

        private int Count(Tab t)
        {
            if (t == Tab.Monster) return catalog.monster.Count;
            if (t == Tab.Traits) return catalog.traits.Count;
            return catalog.abilities.Count;
        }
        private string NameAt(int index)
        {
            if (tab == Tab.Monster) return catalog.monster[index] == null ? "(null)" : catalog.monster[index].displayName;
            if (tab == Tab.Traits) return catalog.traits[index] == null ? "(null)" : catalog.traits[index].displayName;
            return catalog.abilities[index] == null ? "(null)" : catalog.abilities[index].displayName;
        }
        private bool EnabledAt(int index)
        {
            if (tab == Tab.Monster) return catalog.monster[index] != null && catalog.monster[index].enabled;
            if (tab == Tab.Traits) return catalog.traits[index] != null && catalog.traits[index].enabled;
            return catalog.abilities[index] != null && catalog.abilities[index].enabled;
        }
        private void SetEnabled(int index, bool value)
        {
            Undo.RecordObject(catalog, "Enable or disable content record");
            if (tab == Tab.Monster) catalog.monster[index].enabled = value;
            else if (tab == Tab.Traits) catalog.traits[index].enabled = value;
            else catalog.abilities[index].enabled = value;
            EditorUtility.SetDirty(catalog);
            validation = null;
        }
        private void DrawRuntimeSummary()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("NEXT MATCH", EditorStyles.boldLabel, GUILayout.Width(100));
                GUILayout.Label("Monster: " + (catalog.readOnlyLegacyRuntime ? "LEGACY" : "CONTENT"),
                    EditorStyles.miniBoldLabel, GUILayout.MinWidth(120));
                GUILayout.Label("Traits: " + (catalog.enableLiveTraits ? "CONTENT" : "LEGACY"),
                    EditorStyles.miniBoldLabel, GUILayout.MinWidth(120));
                int authored = 0;
                for (int i = 0; i < catalog.abilities.Count; i++)
                    if (catalog.abilities[i] != null && catalog.abilities[i].enabled &&
                        catalog.abilities[i].useEffectBlocks) authored++;
                GUILayout.Label("Abilities: " + (catalog.enableLiveAbilities ?
                    (authored > 0 ? "HYBRID (" + authored + " authored)" : "LEGACY (0 authored)") :
                    "LEGACY (global off)"), EditorStyles.miniBoldLabel);
            }
        }

        private bool SearchMatches(int index)
        {
            if (enabledOnly && !EnabledAt(index)) return false;
            if (String.IsNullOrEmpty(search)) return true;
            string id = "";
            string extras = "";
            if (tab == Tab.Monster)
            {
                var p = catalog.monster[index];
                if (p != null) { id = p.id; extras = (p.combatRole ?? "") + " " + (p.abilityId ?? ""); }
            }
            else if (tab == Tab.Traits)
            {
                var t = catalog.traits[index];
                if (t != null) id = t.id;
            }
            else
            {
                var a = catalog.abilities[index];
                if (a != null) { id = a.id; extras = a.legacyHandlerMonster; }
            }
            string combined = (NameAt(index) ?? "") + " " + (id ?? "") + " " + (extras ?? "");
            return combined.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string ListSummary(int index)
        {
            if (tab == Tab.Monster)
            {
                var p = catalog.monster[index];
                return p == null ? "" : p.shopCost + "g  ·  " + (p.combatRole ?? "");
            }
            if (tab == Tab.Traits)
            {
                var t = catalog.traits[index];
                return t == null ? "" : (t.isOrigin ? "Origin" : "Type") + "  ·  " +
                    (t.tiers == null ? 0 : t.tiers.Count) + " tiers";
            }
            var a = catalog.abilities[index];
            return a == null ? "" : (catalog.enableLiveAbilities && a.useEffectBlocks ? "AUTHORED" : "LEGACY");
        }

        private void DrawList()
        {
            float width = Mathf.Clamp(position.width * .30f, 255f, 375f);
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
            {
                EditorGUILayout.LabelField("CONTENT LIBRARY", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    search = EditorGUILayout.TextField(search,
                        GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.textField);
                    enabledOnly = GUILayout.Toggle(enabledOnly, "Enabled only", GUILayout.Width(94));
                }
                int visible = 0;
                for (int i = 0; i < Count(tab); i++) if (SearchMatches(i)) visible++;
                EditorGUILayout.LabelField(visible + " visible / " + Count(tab) + " total", EditorStyles.miniLabel);
                leftScroll = EditorGUILayout.BeginScrollView(leftScroll, GUILayout.ExpandHeight(true));
                for (int i = 0; i < Count(tab); i++)
                {
                    if (!SearchMatches(i)) continue;
                    bool enabled = EnabledAt(i);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool next = GUILayout.Toggle(enabled, GUIContent.none, GUILayout.Width(19));
                        if (next != enabled) SetEnabled(i, next);
                        string label = (NameAt(i) ?? "(unnamed)") + "    " + ListSummary(i);
                        var style = i == selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                        if (GUILayout.Button(label, style, GUILayout.ExpandWidth(true)))
                        {
                            selected = i;
                            rightScroll = Vector2.zero;
                            GUI.FocusControl(null);
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ Add Disabled Draft", GUILayout.Height(28))) AddNew();
                    using (new EditorGUI.DisabledScope(selected < 0 || selected >= Count(tab)))
                        if (GUILayout.Button("Duplicate", GUILayout.Width(94), GUILayout.Height(28))) Duplicate();
                }
                EditorGUILayout.LabelField("Disable preserves the record. Duplicates begin disabled.",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static void DrawField(SerializedProperty root, string key, string label = null)
        {
            var property = root.FindPropertyRelative(key);
            if (property == null) return;
            if (label == null) EditorGUILayout.PropertyField(property, true);
            else EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }

        private static void DrawSection(SerializedProperty root, string key, string heading, ref bool expanded)
        {
            expanded = EditorGUILayout.Foldout(expanded, heading, true);
            if (!expanded) return;
            EditorGUI.indentLevel++;
            DrawField(root, key);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(3);
        }

        private void DrawAbilityDropdown(SerializedProperty record)
        {
            var abilityId = record.FindPropertyRelative("abilityId");
            if (abilityId == null) return;
            var options = new List<string> { "(none)" };
            for (int i = 0; i < catalog.abilities.Count; i++)
                if (catalog.abilities[i] != null && !String.IsNullOrEmpty(catalog.abilities[i].id))
                    options.Add(catalog.abilities[i].id);
            int chosen = options.IndexOf(abilityId.stringValue);
            if (chosen < 0) chosen = 0;
            int requested = EditorGUILayout.Popup("Assigned Ability", chosen, options.ToArray());
            if (requested != chosen) abilityId.stringValue = requested == 0 ? "" : options[requested];
            if (!String.IsNullOrEmpty(abilityId.stringValue) &&
                !options.Contains(abilityId.stringValue))
                EditorGUILayout.HelpBox("Assigned ability ID is not in this catalog: " +
                    abilityId.stringValue, MessageType.Warning);
            EditorGUILayout.PropertyField(abilityId, new GUIContent("Ability ID (manual)"));
        }

        private void DrawMonsterEditor(SerializedProperty record)
        {
            identityOpen = EditorGUILayout.Foldout(identityOpen, "Identity, Shop & Ability", true);
            if (identityOpen)
            {
                EditorGUI.indentLevel++;
                DrawField(record, "id", "Stable Family ID");
                DrawField(record, "enabled");
                DrawField(record, "displayName", "Family Name");
                DrawField(record, "shopCost", "Shop Cost (gold)");
                var role = record.FindPropertyRelative("combatRole");
                if (role != null)
                {
                    var supportedRoles = new[] { "Tank", "Fighter", "Mage", "Ranged", "Assassin" };
                    int current = Array.IndexOf(supportedRoles, role.stringValue);
                    if (current >= 0)
                    {
                        int requested = EditorGUILayout.Popup("Combat Role", current, supportedRoles);
                        if (requested != current) role.stringValue = supportedRoles[requested];
                    }
                    else EditorGUILayout.PropertyField(role, new GUIContent("Combat Role (custom)"));
                }
                DrawField(record, "baseHp", "Base HP (1 star)");
                DrawAbilityDropdown(record);
                EditorGUILayout.HelpBox("Shop cost, stats, forms and origins affect NEW matches. " +
                    "Keep the stable ID unchanged after other records reference it.", MessageType.None);
                EditorGUI.indentLevel--;
            }
            DrawSection(record, "stats", "Combat Stats (AD, AP, AS, Mana, Armor, MR)", ref statsOpen);
            DrawSection(record, "forms", "Evolution Forms, Types & Sprites", ref formsOpen);
            DrawSection(record, "origins", "Origin Traits (count toward synergies)", ref originsOpen);
            if (originsOpen) EditorGUILayout.LabelField("Elemental typing is configured per evolution form.",
                EditorStyles.wordWrappedMiniLabel);
            DrawSection(record, "designerNotes", "Designer Notes", ref notesOpen);
        }

        private void DrawTraitEditor(SerializedProperty record)
        {
            identityOpen = EditorGUILayout.Foldout(identityOpen, "Identity & Trait Membership", true);
            if (identityOpen)
            {
                EditorGUI.indentLevel++;
                DrawField(record, "id", "Stable Trait ID");
                DrawField(record, "enabled");
                DrawField(record, "displayName", "Trait Name");
                DrawField(record, "isOrigin", "Origin (not elemental)");
                DrawField(record, "matchingUnitsOnly", "Matching units only");
                EditorGUI.indentLevel--;
            }
            DrawSection(record, "tiers", "Breakpoints & Active Combat Buffs", ref tiersOpen);
            if (tiersOpen) EditorGUILayout.HelpBox("Effect multipliers are DELTAS: 0.30 = +30%. " +
                "New trait mechanics beyond the listed effect types require reusable C# handlers.", MessageType.None);
            DrawSection(record, "designerNotes", "Designer Notes", ref notesOpen);
        }

        private void DrawAbilityEditor(SerializedProperty record)
        {
            identityOpen = EditorGUILayout.Foldout(identityOpen, "Identity & Runtime Routing", true);
            if (identityOpen)
            {
                EditorGUI.indentLevel++;
                DrawField(record, "id", "Stable Ability ID");
                DrawField(record, "enabled");
                DrawField(record, "displayName");
                DrawField(record, "description");
                DrawField(record, "formula", "Formula Description");
                DrawField(record, "damageKind", "Damage Kind");
                DrawField(record, "legacyHandlerMonster", "Legacy Handler Family");
                DrawField(record, "useEffectBlocks", "Use Authored Effect Blocks");
                EditorGUI.indentLevel--;
            }
            var blocks = record.FindPropertyRelative("useEffectBlocks");
            bool authoring = catalog.enableLiveAbilities && blocks != null && blocks.boolValue &&
                EnabledAt(selected);
            EditorGUILayout.HelpBox(authoring ?
                "AUTHORED NEXT MATCH: this ability uses effect blocks and the configured VFX." :
                "LEGACY NEXT MATCH: original ability handler still runs. Authored values stay saved.",
                MessageType.Info);
            effectOpen = EditorGUILayout.Foldout(effectOpen, "Gameplay Effect Blocks", true);
            if (effectOpen)
            {
                EditorGUI.indentLevel++;
                DrawField(record, "primary", "Primary Damage (flat / AD / AP)");
                DrawField(record, "splash", "Splash Damage");
                DrawField(record, "selfHeal", "Self Heal");
                DrawField(record, "selfShield", "Self Shield");
                DrawField(record, "hitCount", "Hit Count");
                DrawField(record, "splashRadius", "Splash Radius (hexes)");
                DrawField(record, "maximumSecondaryTargets", "Maximum Splash Targets");
                DrawField(record, "burnDuration");
                DrawField(record, "burnMaxHpPerSecond");
                DrawField(record, "woundDuration");
                EditorGUI.indentLevel--;
                EditorGUILayout.LabelField("AP/AD coefficients: 2.0 = 200% of actual effective stat.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            vfxOpen = EditorGUILayout.Foldout(vfxOpen, "VFX Preset / Prefab", true);
            if (vfxOpen)
            {
                EditorGUI.indentLevel++;
                DrawField(record, "vfxPreset");
                DrawField(record, "vfxPrefab");
                DrawField(record, "vfxScale");
                DrawField(record, "vfxLifetime");
                EditorGUI.indentLevel--;
            }
            legacyOpen = EditorGUILayout.Foldout(legacyOpen, "Legacy Baseline (reference only)", true);
            if (legacyOpen)
            {
                EditorGUI.indentLevel++;
                DrawField(record, "legacyBasePrimaryDamage");
                DrawField(record, "legacyBaseSplashDamage");
                DrawField(record, "legacyBaseHealing");
                DrawField(record, "legacyBaseShield");
                EditorGUI.indentLevel--;
            }
            DrawSection(record, "designerNotes", "Designer Notes", ref notesOpen);
        }

        private void DrawDetails()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                if (selected < 0 || selected >= Count(tab))
                {
                    EditorGUILayout.HelpBox("Select a record on the left. Add or duplicate content to " +
                        "create a disabled draft. Use Validate before enabling it for gameplay.", MessageType.Info);
                    return;
                }
                EditorGUILayout.LabelField("EDIT  •  " + NameAt(selected), EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Saved catalog data  •  Changes used in NEXT Play Mode session",
                    EditorStyles.miniLabel);
                rightScroll = EditorGUILayout.BeginScrollView(rightScroll, GUILayout.ExpandHeight(true));
                var serialized = new SerializedObject(catalog);
                serialized.Update();
                string field = tab == Tab.Monster ? "monster" : tab == Tab.Traits ? "traits" : "abilities";
                SerializedProperty record = serialized.FindProperty(field).GetArrayElementAtIndex(selected);
                if (record != null)
                {
                    switch (tab)
                    {
                        case Tab.Monster: DrawMonsterEditor(record); break;
                        case Tab.Traits: DrawTraitEditor(record); break;
                        case Tab.Abilities: DrawAbilityEditor(record); break;
                    }
                }
                if (serialized.ApplyModifiedProperties()) validation = null;
                EditorGUILayout.EndScrollView();
            }
        }

        private void AddNew()
        {
            Undo.RecordObject(catalog, "Add content draft");
            if (tab == Tab.Monster)
            {
                var draft = new MonsterTacticsMonsterRecord { enabled = false,
                    id = UniqueId("family_new"), displayName = "New Monster", baseHp = 500,
                    designerNotes = "DRAFT: add unique form names, assign an ability and validate before use." };
                draft.forms.Add(new MonsterTacticsMonsterFormRecord { name = "New Form 1", types = "Normal" });
                draft.forms.Add(new MonsterTacticsMonsterFormRecord { name = "New Form 2", types = "Normal" });
                draft.forms.Add(new MonsterTacticsMonsterFormRecord { name = "New Form 3", types = "Normal" });
                catalog.monster.Add(draft);
            }
            else if (tab == Tab.Traits)
            {
                catalog.traits.Add(new MonsterTacticsTraitRecord {
                    id = UniqueId("trait_new"), enabled = false,
                    displayName = "New Trait", designerNotes = "DRAFT: configure tiers and supported stat effects. VFX presets wait for f.3." });
            }
            else
            {
                catalog.abilities.Add(new MonsterTacticsAbilityRecord {
                    id = UniqueId("ability_new"), enabled = false,
                    displayName = "New Ability", designerNotes = "DRAFT: enable Use Effect Blocks and configure scaling and visuals; assign this ID to a Monster." });
            }
            selected = Count(tab) - 1;
            search = "";
            enabledOnly = false;
            EditorUtility.SetDirty(catalog);
            validation = null;
            GUIUtility.ExitGUI();
        }
        private void Duplicate()
        {
            if (selected < 0 || selected >= Count(tab)) return;
            Undo.RecordObject(catalog, "Duplicate content record");
            if (tab == Tab.Monster)
            {
                var source = catalog.monster[selected];
                var clone = JsonUtility.FromJson<MonsterTacticsMonsterRecord>(JsonUtility.ToJson(source));
                clone.id = UniqueId(source.id + "_copy");
                clone.displayName += " Copy";
                clone.enabled = false;
                foreach (var form in clone.forms) form.name += " Copy";
                clone.designerNotes = "DUPLICATE DRAFT: rename forms, review ability reference, then validate.";
                catalog.monster.Add(clone);
            }
            else if (tab == Tab.Traits)
            {
                var clone = JsonUtility.FromJson<MonsterTacticsTraitRecord>(JsonUtility.ToJson(catalog.traits[selected]));
                clone.id = UniqueId(clone.id + "_copy"); clone.displayName += " Copy";
                clone.enabled = false;
                catalog.traits.Add(clone);
            }
            else
            {
                var clone = JsonUtility.FromJson<MonsterTacticsAbilityRecord>(JsonUtility.ToJson(catalog.abilities[selected]));
                clone.id = UniqueId(clone.id + "_copy"); clone.displayName += " Copy";
                clone.enabled = false;
                clone.legacyHandlerMonster = ""; // Never silently clone a live behavior binding.
                clone.useEffectBlocks = false; // Must explicitly opt into authored gameplay.
                catalog.abilities.Add(clone);
            }
            selected = Count(tab) - 1;
            search = "";
            enabledOnly = false;
            EditorUtility.SetDirty(catalog);
            validation = null;
            GUIUtility.ExitGUI();
        }
        private string UniqueId(string prefix)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in catalog.monster) if (p != null) existing.Add(p.id ?? "");
            foreach (var t in catalog.traits) if (t != null) existing.Add(t.id ?? "");
            foreach (var a in catalog.abilities) if (a != null) existing.Add(a.id ?? "");
            string id = prefix;
            int n = 2;
            while (existing.Contains(id)) id = prefix + "_" + n++;
            return id;
        }
    }
}
#endif
