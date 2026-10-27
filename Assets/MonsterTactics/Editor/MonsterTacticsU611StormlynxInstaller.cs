#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Opt-in, Stormlynx-only roster addition. Installs a single 5g family; does not
    /// touch any proposed U6.11 families or existing Monster/trait settings.
    /// </summary>
    public static class MonsterTacticsU611StormlynxInstaller
    {
        private const string CatalogPath = "Assets/MonsterTactics/Resources/Content/MonsterTacticsContentCatalog.asset";
        private const string SpritePath = "Assets/MonsterTactics/Art/MonsterU610/monster_807.png";
        private const string BackupPath = "Assets/MonsterTactics/Art/U611StormlynxBackup/MonsterTacticsContentCatalog.before-stormlynx.asset";
        private static UnityWebRequest pending;
        private static bool active;

        public static bool IsInstalled
        {
            get
            {
                var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
                return catalog != null && catalog.monster != null &&
                    catalog.monster.Exists(p => p != null && p.enabled && p.displayName == "Stormlynx");
            }
        }

        [MenuItem("MonsterTactics/U6.11 Stormlynx/1. Download Sprite + Install Stormlynx")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[U6.11] Exit Play Mode first."); return; }
            if (active) { Debug.LogWarning("[U6.11] Download already in progress."); return; }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if (catalog == null || catalog.readOnlyLegacyRuntime)
            { Debug.LogError("[U6.11] Requires the active U6.10.6 Content Catalog."); return; }

            if (File.Exists(SpritePath))
            {
                Apply(ImportSprite());
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
            active = true;
            pending = UnityWebRequest.Get(
                "https://raw.githubusercontent.com/MonsterAPI/sprites/master/sprites/monster/807.png");
            pending.timeout = 20;
            pending.SendWebRequest();
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            Debug.Log("[U6.11] Downloading Stormlynx sprite (#807). No other Monster are downloaded.");
        }

        private static void Poll()
        {
            if (!active || pending == null) { EditorApplication.update -= Poll; return; }
            if (!pending.isDone) return;
            try
            {
                if (pending.result != UnityWebRequest.Result.Success ||
                    pending.downloadHandler.data == null || pending.downloadHandler.data.Length < 100)
                {
                    Debug.LogError("[U6.11] Stormlynx download failed: " + pending.error +
                        ". Catalog unchanged; retry when online.");
                    return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
                File.WriteAllBytes(SpritePath, pending.downloadHandler.data);
                AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
                Apply(ImportSprite());
            }
            catch (Exception e) { Debug.LogError("[U6.11] Stormlynx import failed: " + e); }
            finally
            {
                pending.Dispose(); pending = null; active = false;
                EditorApplication.update -= Poll;
            }
        }

        private static Sprite ImportSprite()
        {
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null) { Debug.LogError("[U6.11] Missing texture importer: " + SpritePath); return null; }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 96;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        private static void Apply(Sprite sprite)
        {
            if (sprite == null) { Debug.LogError("[U6.11] Sprite missing. Catalog unchanged."); return; }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if (catalog == null || catalog.readOnlyLegacyRuntime)
            { Debug.LogError("[U6.11] Catalog not active. Nothing changed."); return; }
            if (!AssetDatabase.IsValidFolder("Assets/MonsterTactics/Art/U611StormlynxBackup"))
                AssetDatabase.CreateFolder("Assets/MonsterTactics/Art", "U611StormlynxBackup");
            if (AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(BackupPath) == null &&
                !AssetDatabase.CopyAsset(CatalogPath, BackupPath))
            { Debug.LogError("[U6.11] Couldn't back up catalog. Nothing changed."); return; }

            Undo.RecordObject(catalog, "Install Stormlynx only");
            var monster = catalog.monster.Find(p => p != null &&
                string.Equals(p.id, "family_stormlynx", StringComparison.OrdinalIgnoreCase));
            if (monster == null)
            {
                monster = new MonsterTacticsMonsterRecord();
                catalog.monster.Add(monster);
            }
            monster.id = "family_stormlynx";
            monster.displayName = "Stormlynx";
            monster.enabled = true;
            monster.shopCost = 5;
            monster.combatRole = "Assassin";
            monster.baseHp = 890;
            monster.forms.Clear();
            for (int stage = 0; stage < 3; stage++)
                monster.forms.Add(new MonsterTacticsMonsterFormRecord
                { name = "Stormlynx", types = "Electric", bestiaryNumber = 807, sprite = sprite });
            monster.origins.Clear(); // Electric is a type; no new origin trait.
            monster.stats = new MonsterTacticsMonsterStatRecord
            {
                attack = 98f, attackSpeed = 1.08f, abilityPower = 100f,
                criticalChance = .10f, criticalMultiplier = 1.5f,
                armor = 38, magicResist = 38,
                attackRange = 1, manaCost = 70, moveInterval = .42f
            };
            monster.abilityId = "u611_stormlynx";
            monster.designerNotes = "U6.11 standalone 5g Electric Assassin; Plasma Fists dashes, " +
                "physical primary and up to two distinct magic chains within 2 hexes. " +
                "Other U6.11 families intentionally NOT installed.";
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            MonsterTacticsU686SpriteBoundsBuilder.Build();
            Debug.Log("[U6.11] Stormlynx installed (ONE family). " +
                "Re-enter Play Mode; shop odds/pool use existing 5g rules. " +
                "For matching genuine art run MonsterTactics > U6.11.1 Stormlynx Fix > " +
                "1. Download Matching Stormlynx Animation.");
        }

        [MenuItem("MonsterTactics/U6.11 Stormlynx/2. Verify Sprite + Ability + Chain Rules")]
        public static void Verify()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            var unit = catalog != null && catalog.monster != null ?
                catalog.monster.Find(p => p != null && p.enabled && p.displayName == "Stormlynx") : null;
            MonsterTacticsCombatRules.AbilityProfile ability;
            bool hasAbility = MonsterTacticsU610Abilities.TryProfile("Stormlynx", 1,
                MonsterTacticsCombatRules.ForMonster("Stormlynx"), out ability);
            var center = new Vector2Int(0, 0);
            var candidates = new[] { new Vector2Int(1, 0), new Vector2Int(3, 0),
                new Vector2Int(0, 2), new Vector2Int(0, -1) };
            int[] picked = MonsterTacticsU611StormlynxRules.SelectTargets(center, candidates);
            bool ruleCheck = picked.Length == 2 && picked[0] == 0 && picked[1] == 3 &&
                MonsterTacticsU611StormlynxRules.SelectTargets(center, new Vector2Int[0]).Length == 0 &&
                MonsterTacticsU611StormlynxRules.SelectTargets(center,
                    new[] { new Vector2Int(1, 0) }).Length == 1 &&
                MonsterTacticsU611StormlynxRules.SelectTargets(center,
                    new[] { new Vector2Int(3, 0) }).Length == 0;
            bool artReady = unit != null && unit.forms != null && unit.forms.Count == 3 &&
                unit.forms[0].sprite != null && unit.shopCost == 5 && unit.combatRole == "Assassin";
            Debug.Log("[U6.11] " + (artReady && hasAbility && ruleCheck ? "PASS" : "FAIL") +
                " Stormlynx: art=" + artReady + ", profile=" + hasAbility +
                ", 2-hex closest-two rules=" + ruleCheck +
                ", one-enemy/no-chain=" + (MonsterTacticsU611StormlynxRules.SelectTargets(center,
                    new Vector2Int[0]).Length == 0));
        }

        [MenuItem("MonsterTactics/U6.11 Stormlynx/3. Disable Stormlynx (Keep Art)")]
        public static void Disable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[U6.11] Exit Play Mode first."); return; }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if (catalog == null || catalog.monster == null) return;
            var unit = catalog.monster.Find(p => p != null && p.id == "family_stormlynx");
            if (unit == null) return;
            Undo.RecordObject(catalog, "Disable Stormlynx");
            unit.enabled = false;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[U6.11] Stormlynx disabled for new matches. Artwork and backup retained.");
        }
    }
}
#endif
