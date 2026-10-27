#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.2 incremental authoring. Opt-in commands only: importing the patch
    /// cannot change or enable any user's existing Augment ScriptableObjects.
    /// </summary>
    public static class MonsterTacticsU692EconomyAuthoring
    {
        private const string Root = "Assets/MonsterTactics/Resources/Augments";
        private const string CatalogPath = Root + "/MonsterTacticsAugmentCatalog.asset";

        private static MonsterTacticsAugmentCatalog GetCatalog()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics Augments", "Exit Play Mode before editing augment assets.", "OK");
                return null;
            }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null)
                EditorUtility.DisplayDialog("MonsterTactics Augments",
                    "No augment catalog found. Run MonsterTactics > Augments > U6.9.0 Install Framework & Samples first.", "OK");
            return catalog;
        }

        private static string UniqueId(MonsterTacticsAugmentCatalog catalog, string prefix)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (catalog.definitions != null)
                foreach (var d in catalog.definitions)
                    if (d != null && !string.IsNullOrWhiteSpace(d.id)) ids.Add(d.id.Trim());
            int n = 1;
            while (ids.Contains(prefix + n.ToString("000")) ||
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Root + "/" + prefix + n.ToString("000") + ".asset") != null)
                n++;
            return prefix + n.ToString("000");
        }

        private static MonsterTacticsAugmentDefinition Create(
            MonsterTacticsAugmentCatalog catalog, string id, string title, string description,
            MonsterTacticsAugmentTier tier, MonsterTacticsAugmentCategory category,
            MonsterTacticsAugmentEffect effect)
        {
            if (catalog == null) return null;
            // Treat existing ID or path as authored user data: never overwrite it.
            foreach (var d in catalog.definitions)
                if (d != null && string.Equals(d.id, id, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("[U6.9.2] Kept existing augment " + id + " unchanged.");
                    return d;
                }
            string path = Root + "/" + id + ".asset";
            var existingAsset = AssetDatabase.LoadMainAssetAtPath(path);
            if (existingAsset != null)
            {
                var existing = existingAsset as MonsterTacticsAugmentDefinition;
                if (existing == null || !string.Equals(existing.id, id, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError("[U6.9.2] Existing asset at path is not this augment; refusing to overwrite: " + path);
                    return null;
                }
                Link(catalog, existing);
                return existing;
            }
            var def = ScriptableObject.CreateInstance<MonsterTacticsAugmentDefinition>();
            def.id = id;
            def.displayName = title;
            def.description = description;
            def.tier = tier;
            def.category = category;
            def.enabledInPool = false;
            def.offerWeight = 100;
            def.allowedSelections = MonsterTacticsAugmentSlots.All;
            def.effects = new List<MonsterTacticsAugmentEffect> { effect };
            AssetDatabase.CreateAsset(def, path);
            Link(catalog, def);
            return def;
        }

        private static void Link(MonsterTacticsAugmentCatalog catalog, MonsterTacticsAugmentDefinition def)
        {
            if (!catalog.definitions.Contains(def))
            {
                Undo.RecordObject(catalog, "Register augment definition");
                catalog.definitions.Add(def);
                EditorUtility.SetDirty(catalog);
            }
        }

        [MenuItem("MonsterTactics/Augments/U6.9.2 Create Blank Augment Draft")]
        private static void CreateBlankDraftMenu() { CreateBlankDraft(); }

        public static MonsterTacticsAugmentDefinition CreateBlankDraft()
        {
            var catalog = GetCatalog();
            if (catalog == null) return null;
            string id = UniqueId(catalog, "custom_augment_");
            var result = Create(catalog, id, "New Augment", "Describe its gameplay effect.",
                MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentCategory.Economy,
                new MonsterTacticsAugmentEffect {
                    kind = MonsterTacticsAugmentEffectKind.Gold, value = 1,
                    trigger = MonsterTacticsAugmentTrigger.OnChosen, maxTriggers = 1
                });
            AssetDatabase.SaveAssets();
            if (result != null) Selection.activeObject = result;
            Debug.Log("[U6.9.2] Created one disabled augment draft: " + id);
            return result;
        }

        [MenuItem("MonsterTactics/Augments/U6.9.2 Create First Two Economy Drafts (Disabled)")]
        public static void CreateFirstTwoDrafts()
        {
            var catalog = GetCatalog();
            if (catalog == null) return;
            if (catalog.suppressLegacySampleCreation)
            {
                Debug.LogWarning("[U6.9.2b] Old economy drafts are locked for the custom augment roster.");
                return;
            }
            Create(catalog, "u692_training_notes", "Training Notes",
                "Gain 4 XP immediately. Does not bypass the maximum player level.",
                MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentCategory.Economy,
                new MonsterTacticsAugmentEffect {
                    kind = MonsterTacticsAugmentEffectKind.Experience, value = 4,
                    trigger = MonsterTacticsAugmentTrigger.OnChosen, maxTriggers = 1
                });
            Create(catalog, "u692_steady_allowance", "Steady Allowance",
                "Gain 2 gold at the start of every future planning round.",
                MonsterTacticsAugmentTier.Ace, MonsterTacticsAugmentCategory.Economy,
                new MonsterTacticsAugmentEffect {
                    kind = MonsterTacticsAugmentEffectKind.Gold, value = 2,
                    trigger = MonsterTacticsAugmentTrigger.PlanningStarted,
                    maxTriggers = 0, oncePerRound = true
                });
            AssetDatabase.SaveAssets();
            Debug.Log("[U6.9.2] First two economy drafts are available. Existing records were not overwritten. " +
                "Both newly created definitions begin DISABLED; turn each on only after reviewing it.");
        }

        public static MonsterTacticsAugmentDefinition DuplicateAsDisabled(MonsterTacticsAugmentDefinition source)
        {
            var catalog = GetCatalog();
            if (catalog == null || source == null) return null;
            string id = UniqueId(catalog, "custom_augment_");
            var copy = ScriptableObject.CreateInstance<MonsterTacticsAugmentDefinition>();
            EditorUtility.CopySerialized(source, copy);
            copy.id = id;
            copy.displayName = (source.displayName ?? "Augment") + " Copy";
            copy.enabledInPool = false;
            copy.requiredAugmentIds = new List<string>(source.requiredAugmentIds ?? new List<string>());
            copy.conflictIds = new List<string>(source.conflictIds ?? new List<string>());
            string path = Root + "/" + id + ".asset";
            AssetDatabase.CreateAsset(copy, path);
            Link(catalog, copy);
            AssetDatabase.SaveAssets();
            Debug.Log("[U6.9.2] Duplicated " + source.id + " as disabled draft " + copy.id);
            return copy;
        }
    }
}
#endif
