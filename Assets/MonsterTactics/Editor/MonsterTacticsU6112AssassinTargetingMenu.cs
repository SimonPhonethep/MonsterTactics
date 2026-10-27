#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    public static class MonsterTacticsU6112AssassinTargetingMenu
    {
        private const string Path = "Assets/MonsterTactics/Resources/MonsterTacticsU6112AssassinTargetingProfile.asset";

        [MenuItem("MonsterTactics/U6.11.2 Assassin AI/1. Enable Nearest Weakest (Recommended)")]
        public static void Enable()
        {
            if (!Ready()) return;
            var profile = AssetDatabase.LoadAssetAtPath<MonsterTacticsU6112AssassinTargetingProfile>(Path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MonsterTacticsU6112AssassinTargetingProfile>();
                AssetDatabase.CreateAsset(profile, Path);
            }
            Undo.RecordObject(profile, "Enable nearby lowest-HP Assassin AI");
            profile.Enabled = true;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Verify();
            Debug.Log("[U6.11.2] Enabled. Settings asset is editable. Restart Play Mode. " +
                "No units or scenes changed; only Assassin combat targeting is affected.");
        }

        [MenuItem("MonsterTactics/U6.11.2 Assassin AI/2. Restore Legacy Backline Targeting")]
        public static void Legacy()
        {
            if (!Ready()) return;
            var profile = AssetDatabase.LoadAssetAtPath<MonsterTacticsU6112AssassinTargetingProfile>(Path);
            if (profile == null)
            {
                Debug.Log("[U6.11.2] No profile installed. Already on U6.11.1 legacy targeting.");
                return;
            }
            Undo.RecordObject(profile, "Restore legacy Assassin targeting");
            profile.Enabled = false;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[U6.11.2] Legacy backline targeting restored for next combat.");
        }

        [MenuItem("MonsterTactics/U6.11.2 Assassin AI/3. Verify Setup + Run Targeting Tests")]
        public static void Verify()
        {
            var profile = AssetDatabase.LoadAssetAtPath<MonsterTacticsU6112AssassinTargetingProfile>(Path);
            if (profile == null)
            {
                Debug.LogWarning("[U6.11.2] Missing profile. Run menu option 1 to enable.");
                return;
            }
            int tests = RunRulesTests();
            Debug.Log("[U6.11.2] " + (profile.Enabled ? "ENABLED" : "LEGACY") +
                " | extra hexes=" + profile.ExtraSearchHexes +
                " | blocked retarget delay=" + profile.BlockedRetargetSeconds.ToString("0.00") +
                "s | pure targeting tests=" + tests + "/" + tests +
                ". Test actual unit movement in Play Mode separately.");
        }

        // Pure-rule assertions, runnable without a battlefield scene.
        private static int RunRulesTests()
        {
            int passes = 0;
            Action<bool, string> check = (condition, message) =>
            {
                if (!condition) throw new Exception("[U6.11.2] FAILED: " + message);
                passes++;
            };
            var c = new List<MonsterTacticsU6112AssassinTargetingRules.Candidate>
            {
                new MonsterTacticsU6112AssassinTargetingRules.Candidate(2, 800, 1000, true),
                new MonsterTacticsU6112AssassinTargetingRules.Candidate(3, 200, 1000, true),
                new MonsterTacticsU6112AssassinTargetingRules.Candidate(5, 1, 1000, true)
            };
            check(MonsterTacticsU6112AssassinTargetingRules.Select(c, 1) == 1,
                "nearby 20%-HP target wins over closer 80%-HP target");
            check(MonsterTacticsU6112AssassinTargetingRules.Select(c, 0) == 0,
                "0-extra-hex search stays on nearest enemy");
            c[1] = new MonsterTacticsU6112AssassinTargetingRules.Candidate(3, 50, 1000, false);
            check(MonsterTacticsU6112AssassinTargetingRules.Select(c, 1) == 0,
                "unreachable injured target is ignored");
            c[0] = new MonsterTacticsU6112AssassinTargetingRules.Candidate(2, 800, 1000, false);
            c[2] = new MonsterTacticsU6112AssassinTargetingRules.Candidate(5, 1, 1000, false);
            check(MonsterTacticsU6112AssassinTargetingRules.Select(c, 1) == 1,
                "when every candidate is blocked, pick nearby lowest HP");
            c.Clear();
            check(MonsterTacticsU6112AssassinTargetingRules.Select(c, 1) == -1,
                "empty enemy list");
            c.Add(new MonsterTacticsU6112AssassinTargetingRules.Candidate(2, 50, 100, true));
            c.Add(new MonsterTacticsU6112AssassinTargetingRules.Candidate(2, 300, 600, true));
            check(MonsterTacticsU6112AssassinTargetingRules.Select(c, 1) == 0,
                "identical HP fraction resolves stably by order/absolute HP");
            c.Clear();
            c.Add(new MonsterTacticsU6112AssassinTargetingRules.Candidate(1, 0, 100, true));
            c.Add(new MonsterTacticsU6112AssassinTargetingRules.Candidate(2, 90, 100, true));
            check(MonsterTacticsU6112AssassinTargetingRules.Select(c, 1) == 1,
                "dead units are excluded");
            return passes;
        }

        private static bool Ready()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
            Debug.LogWarning("[U6.11.2] Exit Play Mode before changing AI settings.");
            return false;
        }
    }
}
#endif
