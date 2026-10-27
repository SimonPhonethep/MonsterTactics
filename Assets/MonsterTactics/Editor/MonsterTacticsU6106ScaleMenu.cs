#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    public static class MonsterTacticsU6106ScaleMenu
    {
        private const string Path = "Assets/MonsterTactics/Resources/MonsterTacticsU6106ScaleProfile.asset";

        [MenuItem("MonsterTactics/U6.10.6/1. Apply Global Sprite Scale Normalization")]
        public static void Install()
        {
            if (!Ready()) return;
            var profile = AssetDatabase.LoadAssetAtPath<MonsterTacticsU6106ScaleProfile>(Path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MonsterTacticsU6106ScaleProfile>();
                AssetDatabase.CreateAsset(profile, Path);
                Debug.Log("[U6.10.6] Created visual scale profile with recommended defaults.");
            }
            else
                Debug.Log("[U6.10.6] Existing profile retained. Inspector tuning has NOT been overwritten.");
            Undo.RecordObject(profile, "Enable MonsterTactics scale normalization");
            profile.Enabled = true;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            MonsterTacticsU686SpriteBoundsBuilder.Build();
            Verify();
            Debug.Log("[U6.10.6] Scale normalization ready. Restart Play Mode. " +
                "All changes affect sprite visuals only. No re-downloads needed.");
        }

        [MenuItem("MonsterTactics/U6.10.6/2. Verify Global Scale Profile")]
        public static void Verify()
        {
            var profile = AssetDatabase.LoadAssetAtPath<MonsterTacticsU6106ScaleProfile>(Path);
            if (profile == null)
            {
                Debug.LogWarning("[U6.10.6] Not installed. Run menu option 1 first.");
                return;
            }
            int originalFamilies = MonsterTacticsU5Roster.All.Length;
            int bosses = MonsterTacticsBossEncounters.All.Length;
            var content = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(
                "Assets/MonsterTactics/Resources/Content/MonsterTacticsContentCatalog.asset");
            int active = content != null && content.monster != null ?
                content.monster.Count(x => x != null && x.enabled) : 0;
            bool pass = profile.Enabled && originalFamilies == 13 && bosses == 7 &&
                profile.SkyclawScaleMultiplier >= 1f && profile.DreadwingScaleMultiplier >= 1f;
            Debug.Log("[U6.10.6] " + (pass ? "PASS" : "CHECK") +
                " | enabled=" + profile.Enabled +
                " | legacy families=" + originalFamilies +
                " | catalog active families=" + active + " (expected 31 after U6.10)" +
                " | wild scale=" + profile.WildScaleMultiplier.ToString("0.00") +
                " | playable original/new=" + profile.OriginalPlayableScale.ToString("0.00") +
                "/" + profile.NewPlayableScale.ToString("0.00") +
                " | bosses=" + bosses +
                " | Skyclaw=" + profile.SkyclawScaleMultiplier.ToString("0.00") +
                " | Dreadwing=" + profile.DreadwingScaleMultiplier.ToString("0.00") +
                " | per-species overrides=" + (profile.SpeciesOverrides?.Count ?? 0) +
                ". Visual-only changes; Play Mode still needs verification.");
        }

        [MenuItem("MonsterTactics/U6.10.6/3. Disable Global Normalization (Restore Old Scales)")]
        public static void Disable()
        {
            if (!Ready()) return;
            var profile = AssetDatabase.LoadAssetAtPath<MonsterTacticsU6106ScaleProfile>(Path);
            if (profile == null) { Debug.Log("[U6.10.6] No scale profile installed."); return; }
            Undo.RecordObject(profile, "Disable MonsterTactics scale normalization");
            profile.Enabled = false;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[U6.10.6] Original U6.10.4/U6.10.5 scaling restored for next Play Mode.");
        }

        private static bool Ready()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
            Debug.LogWarning("[U6.10.6] Exit Play Mode before editing visual scales.");
            return false;
        }
    }
}
#endif
