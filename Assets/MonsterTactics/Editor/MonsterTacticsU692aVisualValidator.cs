#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    public static class MonsterTacticsU692aVisualValidator
    {
        [MenuItem("MonsterTactics/Tests/Archive/Augments/U6.9.2a Verify Augment Visuals")]
        private static void Verify()
        {
            int checks = 0, missingLogos = 0;
            var temp = ScriptableObject.CreateInstance<MonsterTacticsAugmentDefinition>();
            try
            {
                temp.id = "visual_sandbox";
                temp.displayName = "Visual Sandbox";
                temp.category = MonsterTacticsAugmentCategory.Economy;
                temp.useCustomAccent = true;
                temp.accentColor = Color.cyan;
                temp.showGlow = false;
                temp.artworkOpacity = .35f;
                var snapshot = new MonsterTacticsAugmentSnapshot(temp);
                Check(snapshot.Icon == null && snapshot.CategoryBadge == null &&
                    snapshot.CardArtwork == null, "Optional art stays optional", ref checks);
                Check(snapshot.UseCustomAccent && snapshot.AccentColor == Color.cyan &&
                    !snapshot.ShowGlow, "Visual settings are snapshotted", ref checks);
                Check(Mathf.Abs(snapshot.ArtworkOpacity - .35f) < .001f,
                    "Card artwork opacity is snapshotted", ref checks);
                Check(MonsterTacticsU692aIconTools.TierColor(MonsterTacticsAugmentTier.Rookie) !=
                    MonsterTacticsU692aIconTools.TierColor(MonsterTacticsAugmentTier.Ace) &&
                    MonsterTacticsU692aIconTools.TierColor(MonsterTacticsAugmentTier.Ace) !=
                    MonsterTacticsU692aIconTools.TierColor(MonsterTacticsAugmentTier.Champion),
                    "All three tier frame styles are distinct", ref checks);
                var cat = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(
                    "Assets/MonsterTactics/Resources/Augments/MonsterTacticsAugmentCatalog.asset");
                Check(cat != null, "Augment catalog found", ref checks);
                if (cat != null)
                {
                    foreach (var def in cat.definitions)
                    {
                        if (def == null) continue;
                        var snap = new MonsterTacticsAugmentSnapshot(def);
                        Check(snap.Icon == def.icon && snap.CardArtwork == def.cardArtwork &&
                            snap.CategoryBadge == def.categoryBadge, def.id + " art preserved", ref checks);
                        if (def.icon == null) missingLogos++;
                    }
                }
                Debug.Log("[U6.9.2a] PASS: " + checks + " visual data checks. " + missingLogos +
                    " augment(s) use fallback art (not an error). UI appearance still needs Play Mode review.");
                EditorUtility.DisplayDialog("U6.9.2a Visuals", "PASS: " + checks +
                    " checks. " + missingLogos + " logo(s) optional/fallback. Preview the live selection in Play Mode.", "OK");
            }
            finally { UnityEngine.Object.DestroyImmediate(temp); }
        }
        private static void Check(bool condition, string label, ref int passed)
        {
            if (!condition) throw new InvalidOperationException("[U6.9.2a] FAIL: " + label);
            passed++;
        }
    }
}
#endif
