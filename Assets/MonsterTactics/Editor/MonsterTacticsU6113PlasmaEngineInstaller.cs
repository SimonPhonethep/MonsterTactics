#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>One-time, additive Stormlynx trait migration; preserves other roster records.</summary>
    public static class MonsterTacticsU6113PlasmaEngineInstaller
    {
        private const string CatalogPath = "Assets/MonsterTactics/Resources/Content/MonsterTacticsContentCatalog.asset";
        private const string BackupPath = "Assets/MonsterTactics/Resources/Content/MonsterTacticsContentCatalog.pre-plasma-engine.asset";
        private const string ProfilePath = "Assets/MonsterTactics/Resources/MonsterTacticsU6113PlasmaEngineProfile.asset";
        private const string TraitId = "trait_plasma_engine";
        private const string TraitName = "Plasma Engine";

        [MenuItem("MonsterTactics/U6.11.3 Stormlynx Trait/1. Install Plasma Engine (1)")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[U6.11.3] Exit Play Mode before applying the trait.");
                return;
            }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if (catalog == null || catalog.readOnlyLegacyRuntime)
            {
                Debug.LogError("[U6.11.3] Missing/legacy content catalog. Install U6.11 Stormlynx first.");
                return;
            }
            var stormlynx = catalog.monster != null ? catalog.monster.Find(p =>
                p != null && p.enabled && p.id == "family_stormlynx") : null;
            if (stormlynx == null)
            {
                Debug.LogError("[U6.11.3] Stormlynx not installed. Install U6.11 first.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(BackupPath) == null &&
                !AssetDatabase.CopyAsset(CatalogPath, BackupPath))
            {
                Debug.LogError("[U6.11.3] Failed to back up the catalog. Nothing changed.");
                return;
            }
            Undo.RecordObject(catalog, "Install Plasma Engine 1 origin");
            if (catalog.traits == null) catalog.traits = new List<MonsterTacticsTraitRecord>();
            var trait = catalog.traits.Find(t => t != null &&
                (t.id == TraitId || t.displayName == TraitName));
            if (trait == null)
            {
                trait = new MonsterTacticsTraitRecord();
                catalog.traits.Add(trait);
            }
            trait.id = TraitId;
            trait.displayName = TraitName;
            trait.enabled = true;
            trait.isOrigin = true;
            trait.matchingUnitsOnly = true;
            trait.tiers = new List<MonsterTacticsTraitTierRecord> {
                new MonsterTacticsTraitTierRecord {
                    uniqueFamiliesRequired = 1,
                    description = "Every 3 basic attacks: +10 Mana. After casting: +25% AS for 4s (refresh, no stacking).",
                    effects = new List<MonsterTacticsContentEffectRecord>() // proc handled by combat hook
                }
            };
            trait.designerNotes = "U6.11.3 unique 5g origin; proc values edited in the Plasma Engine Profile asset. " +
                "Runtime hook applies bonuses to Stormlynx only; no passive double application.";
            if (stormlynx.origins == null) stormlynx.origins = new List<string>();
            if (!stormlynx.origins.Contains(TraitName)) stormlynx.origins.Add(TraitName);
            catalog.enableLiveTraits = true;
            EditorUtility.SetDirty(catalog);

            var profile = AssetDatabase.LoadAssetAtPath<MonsterTacticsU6113PlasmaEngineProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MonsterTacticsU6113PlasmaEngineProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[U6.11.3] Plasma Engine (1) installed for Stormlynx only. " +
                "New Play Mode session required. Profile: " + ProfilePath);
        }

        [MenuItem("MonsterTactics/U6.11.3 Stormlynx Trait/2. Verify Unique Trait")]
        public static void Verify()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            var z = catalog == null || catalog.monster == null ? null :
                catalog.monster.Find(p => p != null && p.enabled && p.id == "family_stormlynx");
            var trait = catalog == null || catalog.traits == null ? null :
                catalog.traits.Find(t => t != null && t.id == TraitId && t.enabled);
            var profile = AssetDatabase.LoadAssetAtPath<MonsterTacticsU6113PlasmaEngineProfile>(ProfilePath);
            bool ok = catalog != null && catalog.enableLiveTraits && z != null &&
                z.origins != null && z.origins.Contains(TraitName) &&
                trait != null && trait.isOrigin && trait.matchingUnitsOnly &&
                trait.tiers != null && trait.tiers.Count == 1 &&
                trait.tiers[0].uniqueFamiliesRequired == 1 && profile != null;
            Debug.Log("[U6.11.3] " + (ok ? "PASS" : "FAIL") +
                " | Stormlynx=" + (z != null) + " | trait=" + (trait != null) +
                " | profile=" + (profile != null) + " | active=" +
                (profile != null && profile.Enabled) + ". Play Mode verification remains necessary.");
        }

        [MenuItem("MonsterTactics/U6.11.3 Stormlynx Trait/3. Disable Plasma Engine")]
        public static void Disable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if (catalog == null || catalog.traits == null) return;
            var trait = catalog.traits.Find(t => t != null && t.id == TraitId);
            if (trait == null) return;
            Undo.RecordObject(catalog, "Disable Stormlynx unique trait");
            trait.enabled = false;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[U6.11.3] Plasma Engine disabled in catalog; restart Play Mode.");
        }
    }
}
#endif
