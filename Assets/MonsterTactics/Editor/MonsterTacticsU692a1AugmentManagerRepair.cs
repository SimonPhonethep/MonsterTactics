#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// Non-destructive augment-manager diagnostics and scene wiring repair.
    /// Does not modify the augment catalog, definitions, runtime, or gameplay rules.
    /// </summary>
    public static class MonsterTacticsU692a1AugmentManagerRepair
    {
        private const string CatalogPath =
            "Assets/MonsterTactics/Resources/Augments/MonsterTacticsAugmentCatalog.asset";

        [MenuItem("MonsterTactics/Augments/U6.9.2a.1 Diagnose Augment Manager")]
        private static void Diagnose()
        {
            string report = BuildReport();
            Debug.Log("[U6.9.2a.1] Augment Manager Diagnostics:\n" + report);
            EditorUtility.DisplayDialog("MonsterTactics Augment Diagnostics", report, "OK");
        }

        [MenuItem("MonsterTactics/Augments/U6.9.2a.1 Repair Augment Manager (Edit Mode)")]
        private static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Repair unavailable",
                    "Exit Play Mode first. Runtime scene changes would not be saved reliably.", "OK");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null)
            {
                EditorUtility.DisplayDialog("Cannot repair",
                    "The saved augment catalog was not found at:\n" + CatalogPath +
                    "\n\nRestore its .asset file first. This tool will not generate or overwrite a catalog.", "OK");
                return;
            }

            var shop = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (shop == null)
            {
                EditorUtility.DisplayDialog("Cannot repair",
                    "No active MonsterTacticsU5ShopManager was found in the open scene.\n" +
                    "Open your playable MonsterTactics scene (not just a prefab) and try again.", "OK");
                return;
            }

            if (!shop.gameObject.scene.IsValid() || !shop.gameObject.scene.isLoaded)
            {
                EditorUtility.DisplayDialog("Cannot repair",
                    "The Shop Manager is not part of a loaded editable scene.", "OK");
                return;
            }

            var manager = FindManagerInLoadedScene(shop.gameObject.scene);
            bool created = false;
            if (manager == null)
            {
                manager = Undo.AddComponent<MonsterTacticsAugmentManager>(shop.gameObject);
                created = true;
            }

            Undo.RecordObject(manager, "Repair MonsterTactics Augment Manager references");
            var serialized = new SerializedObject(manager);
            var catProp = serialized.FindProperty("catalog");
            var shopProp = serialized.FindProperty("shop");
            var roundProp = serialized.FindProperty("roundManager");
            if (catProp == null || shopProp == null || roundProp == null)
            {
                if (created) Undo.DestroyObjectImmediate(manager);
                EditorUtility.DisplayDialog("Incompatible manager version",
                    "Expected catalog/shop/roundManager fields were not found. No catalog edits were made.\n" +
                    "Please share the current MonsterTacticsAugmentManager.cs for a version-specific fix.", "OK");
                return;
            }

            catProp.objectReferenceValue = catalog;
            shopProp.objectReferenceValue = shop;
            roundProp.objectReferenceValue =
                UnityEngine.Object.FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            serialized.ApplyModifiedProperties();
            if (!manager.enabled)
            {
                Undo.RecordObject(manager, "Enable MonsterTactics Augment Manager");
                manager.enabled = true;
            }
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);

            string detail = (created ? "Added" : "Re-linked") +
                " MonsterTacticsAugmentManager on '" + manager.gameObject.name + "'.\n" +
                "The existing catalog and all augment definitions were preserved.\n\n" +
                (catalog.masterEnabled
                    ? "Master Enabled is checked.\n"
                    : "Master Enabled is OFF: enable it in the Balance Editor before Play Mode.\n") +
                (!manager.gameObject.activeInHierarchy
                    ? "WARNING: The manager GameObject is inactive. Activate it in the Hierarchy.\n"
                    : "") +
                "\nSave the scene (Ctrl+S), exit and restart Play Mode, then rerun the preview.";
            Debug.Log("[U6.9.2a.1] " + detail, manager);
            EditorUtility.DisplayDialog("Augment Manager repaired", detail, "OK");
        }

        private static MonsterTacticsAugmentManager FindManagerInLoadedScene(UnityEngine.SceneManagement.Scene scene)
        {
            // Includes disabled components and inactive hierarchy objects, excluding project assets.
            var all = Resources.FindObjectsOfTypeAll<MonsterTacticsAugmentManager>();
            foreach (var manager in all)
                if (manager != null && manager.gameObject.scene == scene)
                    return manager;
            return null;
        }

        private static string BuildReport()
        {
            var sb = new StringBuilder();
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            var shop = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            var round = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            var manager = shop != null && shop.gameObject.scene.IsValid()
                ? FindManagerInLoadedScene(shop.gameObject.scene)
                : UnityEngine.Object.FindAnyObjectByType<MonsterTacticsAugmentManager>();

            sb.AppendLine("Mode: " + (EditorApplication.isPlaying ? "PLAY" : "EDIT"));
            sb.AppendLine("Saved catalog: " + (catalog != null ? "FOUND" : "MISSING"));
            if (catalog != null)
            {
                sb.AppendLine("Master Enabled (asset): " + (catalog.masterEnabled ? "ON" : "OFF"));
                sb.AppendLine("Augment definitions: " +
                    (catalog.definitions != null ? catalog.definitions.Count.ToString() : "null"));
            }
            sb.AppendLine("Active Shop Manager: " + (shop != null ? shop.name : "NOT FOUND"));
            sb.AppendLine("Active Round Manager: " + (round != null ? round.name : "NOT FOUND"));
            sb.AppendLine("Augment Manager component: " + (manager != null ? "FOUND" : "MISSING"));
            if (manager != null)
            {
                sb.AppendLine("Manager GameObject: " + manager.gameObject.name);
                sb.AppendLine("Manager enabled: " + manager.enabled);
                sb.AppendLine("Manager GameObject active: " + manager.gameObject.activeInHierarchy);
                var serialized = new SerializedObject(manager);
                var cat = serialized.FindProperty("catalog");
                var shopProp = serialized.FindProperty("shop");
                sb.AppendLine("Manager catalog assigned: " +
                    (cat != null && cat.objectReferenceValue != null));
                sb.AppendLine("Manager shop assigned: " +
                    (shopProp != null && shopProp.objectReferenceValue != null));
                if (EditorApplication.isPlaying)
                    sb.AppendLine("Manager IsActive in Play Mode: " + manager.IsActive);
            }
            sb.AppendLine();
            if (EditorApplication.isPlaying)
                sb.AppendLine("If inactive, exit Play Mode, run Repair, save the scene, and restart.");
            else
                sb.AppendLine("Run Repair in Edit Mode if the component or references are missing. Save scene afterward.");
            return sb.ToString();
        }
    }
}
#endif
