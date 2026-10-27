#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MonsterTactics
{
    public sealed class MonsterTacticsU690AugmentEditor : EditorWindow
    {
        private const string Root = "Assets/MonsterTactics/Resources/Augments";
        private const string CatalogPath = Root + "/MonsterTacticsAugmentCatalog.asset";
        private MonsterTacticsAugmentCatalog catalog;
        private MonsterTacticsAugmentDefinition selected;
        private string search = "";
        private Vector2 scroll;
        private MonsterTacticsAugmentTier tierFilter;
        private bool filterTier;
        private bool enabledOnly;
        private global::UnityEditor.Editor cachedEditor;

        [MenuItem("MonsterTactics/Augments/U6.9.0 Balance Editor")]
        private static void Open()
        {
            GetWindow<MonsterTacticsU690AugmentEditor>("Augment Balance").Show();
        }
        private void OnEnable() { catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath); }
        private void OnDisable() { if (cachedEditor != null) DestroyImmediate(cachedEditor); }
        private void OnGUI()
        {
            if (catalog == null) catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            EditorGUILayout.LabelField("MonsterTactics U6.9.0  |  Augment Balance", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Catalog changes affect NEW matches only. Actual selection UI is U6.9.1. Unsupported effects are excluded from offers.", MessageType.Info);
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("No catalog found. Click Install / Create Samples first.", MessageType.Warning);
                if (GUILayout.Button("Install Framework & Sample Augments")) Install();
                return;
            }
            var catalogSo = new SerializedObject(catalog);
            catalogSo.Update();
            EditorGUILayout.PropertyField(catalogSo.FindProperty("masterEnabled"), new GUIContent("Master Enabled (new matches)"));
            EditorGUILayout.PropertyField(catalogSo.FindProperty("seed"));
            EditorGUILayout.PropertyField(catalogSo.FindProperty("useFreshMatchSeed"),
                new GUIContent("Fresh Random Seed Each Match"));
            EditorGUILayout.HelpBox("Disable Fresh Random Seed to replay the same catalog seed " +
                "for testing. Live augment timeouts always auto-pick to prevent a stalled round.", MessageType.Info);
            EditorGUILayout.PropertyField(catalogSo.FindProperty("rerollsPerCard"), new GUIContent("Augment Rerolls Per Card"));
            EditorGUILayout.PropertyField(catalogSo.FindProperty("firstSelectionSeconds"), new GUIContent("First Selection Timer (sec)"));
            EditorGUILayout.PropertyField(catalogSo.FindProperty("laterSelectionSeconds"), new GUIContent("Later Selection Timer (sec)"));
            // Retained serialized legacy option. Gameplay always auto-picks on timeout.
            // Show its setting read-only to avoid offering an ineffective switch.
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(catalogSo.FindProperty("autoPickWhenTimeExpires"),
                    new GUIContent("Timeout Auto-Pick (Always On)"));
            EditorGUILayout.PropertyField(catalogSo.FindProperty("useMatchTierPatterns"), new GUIContent("Match-wide Tier Patterns"));
            EditorGUILayout.PropertyField(catalogSo.FindProperty("tierPatterns"), new GUIContent("Tier Pattern Weights"), true);
            EditorGUILayout.PropertyField(catalogSo.FindProperty("limitSameCategoryToTwo"), new GUIContent("Avoid 3 Same-Category Cards"));
            EditorGUILayout.PropertyField(catalogSo.FindProperty("enableTraitFamilyTailoring"), new GUIContent("Tailored Trait / Family Weight"));
            catalogSo.ApplyModifiedProperties();
            GUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(catalog.suppressLegacySampleCreation))
            {
                if (GUILayout.Button("Create / Refresh Samples")) { CreateSamples(); catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath); }
            }
            if (GUILayout.Button("Validate Catalog")) ValidateMenu();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New Disabled Augment"))
            {
                selected = MonsterTacticsU692EconomyAuthoring.CreateBlankDraft();
                if (selected != null) Selection.activeObject = selected;
            }
            using (new EditorGUI.DisabledScope(catalog.suppressLegacySampleCreation))
            if (GUILayout.Button("Create 2 Economy Drafts"))
            {
                MonsterTacticsU692EconomyAuthoring.CreateFirstTwoDrafts();
                catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Assign Starter Icons (Missing Only)"))
                MonsterTacticsU692aIconTools.AssignStarterIcons();
            if (selected != null && GUILayout.Button("Duplicate Selected (Disabled)"))
            {
                selected = MonsterTacticsU692EconomyAuthoring.DuplicateAsDisabled(selected);
                if (selected != null) Selection.activeObject = selected;
            }
            if (catalog.suppressLegacySampleCreation)
                EditorGUILayout.HelpBox("Custom augment roster is active. Legacy sample and economy-draft buttons are locked. " +
                    "Use U6.9.2b menu commands to create your custom augments.", MessageType.Info);
            else EditorGUILayout.HelpBox("U6.9.2: Create or duplicate augments ONE AT A TIME. New records start disabled; " +
                "enable only after editing and validation. The two economy examples are optional, never auto-added.", MessageType.Info);
            GUILayout.Space(7f);
            search = EditorGUILayout.TextField("Search name / ID", search);
            GUILayout.BeginHorizontal();
            filterTier = EditorGUILayout.ToggleLeft("Filter tier", filterTier, GUILayout.Width(85));
            if (filterTier) tierFilter = (MonsterTacticsAugmentTier)EditorGUILayout.EnumPopup(tierFilter);
            enabledOnly = EditorGUILayout.ToggleLeft("Enabled only", enabledOnly, GUILayout.Width(105));
            GUILayout.EndHorizontal();
            GUILayout.Label("Definitions: " + catalog.definitions.Count + " (disabled stay saved)", EditorStyles.miniLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Clamp(position.height * .40f, 140f, 340f)));
            foreach (var def in catalog.definitions)
            {
                if (def == null) continue;
                if (filterTier && def.tier != tierFilter) continue;
                if (enabledOnly && !def.enabledInPool) continue;
                if (!string.IsNullOrWhiteSpace(search) &&
                    (def.displayName == null || def.displayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) &&
                    (def.id == null || def.id.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                Rect iconRect = GUILayoutUtility.GetRect(27f, 27f, GUILayout.Width(27f));
                MonsterTacticsU692aIconTools.DrawEditorIcon(iconRect, def);
                bool next = EditorGUILayout.Toggle(def.enabledInPool, GUILayout.Width(18));
                if (next != def.enabledInPool) { Undo.RecordObject(def, "Toggle Augment"); def.enabledInPool = next; EditorUtility.SetDirty(def); }
                if (GUILayout.Button(def.displayName + " (" + def.id + ")", EditorStyles.linkLabel, GUILayout.MinWidth(180)))
                { selected = def; Selection.activeObject = def; }
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(def.tier.ToString(), GUILayout.Width(75));
                int weight = EditorGUILayout.IntField(def.offerWeight, GUILayout.Width(45));
                if (weight != def.offerWeight) { Undo.RecordObject(def, "Augment Weight"); def.offerWeight = Mathf.Clamp(weight, 0, 1000); EditorUtility.SetDirty(def); }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            if (selected != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Individual Configuration: " + selected.displayName, EditorStyles.boldLabel);
                if (cachedEditor == null || cachedEditor.target != selected)
                {
                    if (cachedEditor != null) DestroyImmediate(cachedEditor);
                    cachedEditor = UnityEditor.Editor.CreateEditor(selected);
                }
                if (cachedEditor != null) cachedEditor.OnInspectorGUI();
            }
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/MonsterTactics")) AssetDatabase.CreateFolder("Assets", "MonsterTactics");
            if (!AssetDatabase.IsValidFolder("Assets/MonsterTactics/Resources")) AssetDatabase.CreateFolder("Assets/MonsterTactics", "Resources");
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/MonsterTactics/Resources", "Augments");
        }
        private static MonsterTacticsAugmentCatalog GetOrCreateCatalog()
        {
            EnsureFolder();
            var asset = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (asset == null)
            {
                asset = CreateInstance<MonsterTacticsAugmentCatalog>();
                asset.masterEnabled = false;
                AssetDatabase.CreateAsset(asset, CatalogPath);
            }
            return asset;
        }
        [MenuItem("MonsterTactics/Augments/U6.9.0 Install Framework && Samples")]
        private static void Install()
        {
            var catalog = GetOrCreateCatalog();
            CreateSamples();
            var shop = FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (shop != null)
            {
                var manager = shop.GetComponent<MonsterTacticsAugmentManager>();
                if (manager == null) manager = Undo.AddComponent<MonsterTacticsAugmentManager>(shop.gameObject);
                var so = new SerializedObject(manager);
                so.FindProperty("catalog").objectReferenceValue = catalog;
                so.FindProperty("shop").objectReferenceValue = shop;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(manager);
                if (shop.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
                Debug.Log("[U6.9.0] Augment framework installed on " + shop.name + ". Save scene (Ctrl+S). Master switch is OFF.");
            }
            else Debug.LogWarning("[U6.9.0] Catalog ready, but no active Shop Manager found. Open SampleScene and run Install again.");
            AssetDatabase.SaveAssets();
            Open();
        }

        private static MonsterTacticsAugmentDefinition Add(MonsterTacticsAugmentCatalog catalog, string id, string title,
            MonsterTacticsAugmentTier tier, MonsterTacticsAugmentCategory category, MonsterTacticsAugmentSlots slots,
            MonsterTacticsAugmentEffectKind kind, int value,
            MonsterTacticsAugmentTrigger trigger = MonsterTacticsAugmentTrigger.OnChosen,
            MonsterTacticsRuleId rule = MonsterTacticsRuleId.RerollCost,
            MonsterTacticsItemKind item = MonsterTacticsItemKind.AttackFang)
        {
            string path = Root + "/" + id + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentDefinition>(path);
            if (def == null)
            {
                def = CreateInstance<MonsterTacticsAugmentDefinition>();
                def.id = id; def.displayName = title; def.tier = tier; def.category = category;
                def.allowedSelections = slots; def.enabledInPool = MonsterTacticsAugmentEffectSupport.IsExecutable(new MonsterTacticsAugmentEffect { kind = kind, value = value, trigger = trigger, item = item });
                def.effects.Add(new MonsterTacticsAugmentEffect { kind = kind, value = value,
                    trigger = trigger, rule = rule, item = item, maxTriggers =
                    trigger == MonsterTacticsAugmentTrigger.OnChosen ? 1 : 0 });
                def.description = "Sample: " + title + " (edit effect values and description in Inspector).";
                AssetDatabase.CreateAsset(def, path);
            }
            if (!catalog.definitions.Contains(def)) { Undo.RecordObject(catalog, "Add Augment"); catalog.definitions.Add(def); EditorUtility.SetDirty(catalog); }
            return def;
        }

        [MenuItem("MonsterTactics/Augments/U6.9.0 Create Sample Definitions")]
        private static void CreateSamples()
        {
            var catalog = GetOrCreateCatalog();
            if (catalog.suppressLegacySampleCreation)
            {
                Debug.LogWarning("[U6.9.2b] Legacy sample generation is locked for this custom catalog. " +
                    "Existing custom augments were not changed.");
                return;
            }
            var all = MonsterTacticsAugmentSlots.All;
            Add(catalog,"starter_allowance","Starter Allowance",MonsterTacticsAugmentTier.Rookie,MonsterTacticsAugmentCategory.Economy,all,MonsterTacticsAugmentEffectKind.Gold,5);
            Add(catalog,"pocket_reroll","Pocket Reroll",MonsterTacticsAugmentTier.Rookie,MonsterTacticsAugmentCategory.Shop,all,MonsterTacticsAugmentEffectKind.FreeRerolls,1);
            Add(catalog,"vital_supplies","Vital Supplies",MonsterTacticsAugmentTier.Rookie,MonsterTacticsAugmentCategory.Items,all,MonsterTacticsAugmentEffectKind.ItemReward,1,item:MonsterTacticsItemKind.VitalSeed);
            Add(catalog,"rich_trainer","Rich Trainer",MonsterTacticsAugmentTier.Ace,MonsterTacticsAugmentCategory.Economy,all,MonsterTacticsAugmentEffectKind.Gold,10);
            Add(catalog,"bargain_hunt","Bargain Hunt",MonsterTacticsAugmentTier.Ace,MonsterTacticsAugmentCategory.Shop,all,MonsterTacticsAugmentEffectKind.RuleAddition,-1,rule:MonsterTacticsRuleId.RerollCost);
            Add(catalog,"field_commander","Field Commander",MonsterTacticsAugmentTier.Ace,MonsterTacticsAugmentCategory.Combat,all,MonsterTacticsAugmentEffectKind.RuleAddition,1,rule:MonsterTacticsRuleId.TeamSizeBonus);
            Add(catalog,"champion_reserves","Champion Reserves",MonsterTacticsAugmentTier.Champion,MonsterTacticsAugmentCategory.Shop,all,MonsterTacticsAugmentEffectKind.FreeRerolls,3);
            Add(catalog,"grand_income","Grand Income",MonsterTacticsAugmentTier.Champion,MonsterTacticsAugmentCategory.Economy,all,MonsterTacticsAugmentEffectKind.RuleAddition,2,rule:MonsterTacticsRuleId.BaseRoundIncome);
            Add(catalog,"royal_treasure","Royal Treasure",MonsterTacticsAugmentTier.Champion,MonsterTacticsAugmentCategory.Items,all,MonsterTacticsAugmentEffectKind.ItemReward,1,item:MonsterTacticsItemKind.Craft00);
            // At least six pilot augments per tier for 3 offers + 3 independent rerolls.
            Add(catalog,"training_fund","Training Fund",MonsterTacticsAugmentTier.Rookie,MonsterTacticsAugmentCategory.Economy,all,MonsterTacticsAugmentEffectKind.Gold,4);
            Add(catalog,"mana_supplies","Mana Supplies",MonsterTacticsAugmentTier.Rookie,MonsterTacticsAugmentCategory.Items,all,MonsterTacticsAugmentEffectKind.ItemReward,1,item:MonsterTacticsItemKind.ManaCrystal);
            Add(catalog,"fang_supplies","Fang Supplies",MonsterTacticsAugmentTier.Rookie,MonsterTacticsAugmentCategory.Items,all,MonsterTacticsAugmentEffectKind.ItemReward,1,item:MonsterTacticsItemKind.AttackFang);
            Add(catalog,"extra_interest","Extra Interest",MonsterTacticsAugmentTier.Ace,MonsterTacticsAugmentCategory.Economy,all,MonsterTacticsAugmentEffectKind.RuleAddition,1,rule:MonsterTacticsRuleId.InterestCap);
            Add(catalog,"advance_allowance","Advance Allowance",MonsterTacticsAugmentTier.Ace,MonsterTacticsAugmentCategory.Economy,all,MonsterTacticsAugmentEffectKind.Gold,8);
            Add(catalog,"field_supplies","Field Supplies",MonsterTacticsAugmentTier.Ace,MonsterTacticsAugmentCategory.Items,all,MonsterTacticsAugmentEffectKind.ItemReward,1,item:MonsterTacticsItemKind.Craft01);
            Add(catalog,"grand_bounty","Grand Bounty",MonsterTacticsAugmentTier.Champion,MonsterTacticsAugmentCategory.Economy,all,MonsterTacticsAugmentEffectKind.Gold,20);
            Add(catalog,"deep_reserves","Deep Reserves",MonsterTacticsAugmentTier.Champion,MonsterTacticsAugmentCategory.Shop,all,MonsterTacticsAugmentEffectKind.FreeRerolls,5);
            Add(catalog,"champion_cache","Champion Cache",MonsterTacticsAugmentTier.Champion,MonsterTacticsAugmentCategory.Items,all,MonsterTacticsAugmentEffectKind.ItemReward,1,item:MonsterTacticsItemKind.Craft02);
            var future = Add(catalog,"ancient_relic","Ancient Relic (Future)",MonsterTacticsAugmentTier.Champion,MonsterTacticsAugmentCategory.Special,
                MonsterTacticsAugmentSlots.Second|MonsterTacticsAugmentSlots.Final,MonsterTacticsAugmentEffectKind.FutureArtifact,1);
            // Unsupported artifacts are never offered, even if enabled by mistake.
            future = Add(catalog,"elemental_crest","Elemental Crest (Future)",MonsterTacticsAugmentTier.Ace,MonsterTacticsAugmentCategory.Traits,
                MonsterTacticsAugmentSlots.Second|MonsterTacticsAugmentSlots.Final,MonsterTacticsAugmentEffectKind.FutureTrait,1);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[U6.9.0] Augment definitions ready; existing assets preserved. Master switch remains unchanged.");
        }

        [MenuItem("MonsterTactics/Augments/U6.9.0 Validate Catalog")]
        private static void ValidateMenu()
        {
            var c = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            var issues = MonsterTacticsU690Validator.CheckCatalog(c);
            if (issues.Count == 0) EditorUtility.DisplayDialog("U6.9.0", "Catalog validation passed.", "OK");
            else EditorUtility.DisplayDialog("U6.9.0 Catalog", issues.Count + " issue(s):\n\n" + string.Join("\n", issues.ToArray()), "OK");
        }
    }
}
#endif
