#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>U6.11.8. Manual-only Monster Creature Orb integration tester.</summary>
    public sealed class MonsterTacticsU6118MonsterBallTester : EditorWindow
    {
        private bool specificFamily = true;
        private string familyName = "";
        private MonsterTacticsLootController.Tier tier = MonsterTacticsLootController.Tier.Silver;
        private string result = "Spawn a unit ball, then collect it with your trainer (or use the test button).";
        private MessageType resultType = MessageType.Info;
        private Vector2 scroll;
        private int previewStage = 2;

        [MenuItem("MonsterTactics/Tests/Monster Ball Reward Tester (Play Mode)", priority = 131)]
        public static void Open()
        {
            var window = GetWindow<MonsterTacticsU6118MonsterBallTester>("Monster Ball Test");
            window.minSize = new Vector2(415f, 355f);
            window.Show();
        }

        private void OnInspectorUpdate() { Repaint(); }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("U6.11.8  ·  POKÉ BALL UNIT REWARDS", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Test-only. This does NOT replace normal PvE item/gold rewards or change shop odds. " +
                "Balls contain one 1★ Monster and stay on the arena if the 9-slot bench is full.",
                MessageType.Info);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Press Play to test rewards.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            var loot = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsLootController>();
            var shop = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (loot == null || shop == null || !shop.IsShopReady)
            {
                EditorGUILayout.HelpBox("Waiting for arena loot controller and shop...", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.LabelField("Free Monster bench slots", shop.EmptyBenchSlots + " / 9");
            EditorGUILayout.LabelField("Balls waiting on arena", loot.PickupCount.ToString());
            EditorGUILayout.LabelField("Match phase", shop.PhaseForHud);
            EditorGUILayout.Space(6);
            specificFamily = EditorGUILayout.ToggleLeft("Pick specific Monster (instead of random)", specificFamily);

            if (specificFamily)
            {
                var families = MonsterTacticsU5Roster.ActiveFamilies
                    .Where(f => f != null && f.Forms != null && f.Forms.Length == 3 &&
                        shop.CanOfferLootMonster(f.Forms[0]))
                    .OrderBy(f => f.Cost)
                    .ThenBy(f => f.Forms[0], StringComparer.OrdinalIgnoreCase).ToArray();
                if (families.Length == 0)
                {
                    EditorGUILayout.HelpBox("No enabled uncompleted Monster families can be granted.", MessageType.Warning);
                    EditorGUILayout.EndScrollView();
                    return;
                }
                int index = Array.FindIndex(families, f =>
                    string.Equals(f.Forms[0], familyName, StringComparison.OrdinalIgnoreCase));
                if (index < 0) index = 0;
                index = EditorGUILayout.Popup("Monster", index, families.Select(f =>
                    f.Cost + "g  ·  " + f.Forms[0] + " → " + f.Forms[2]).ToArray());
                var selected = families[Mathf.Clamp(index, 0, families.Length - 1)];
                familyName = selected.Forms[0];
                // A 2/3/4g family may use either neighboring ball rarity.
                var validTiers = Enum.GetValues(typeof(MonsterTacticsLootController.Tier))
                    .Cast<MonsterTacticsLootController.Tier>()
                    .Where(t => MonsterTacticsLootController.MonsterCostMatchesTier(t, selected.Cost)).ToArray();
                int tierIndex = Array.IndexOf(validTiers, tier);
                if (tierIndex < 0) tierIndex = 0;
                tierIndex = EditorGUILayout.Popup("Creature Orb", tierIndex,
                    validTiers.Select(t => t.ToString()).ToArray());
                tier = validTiers[Mathf.Clamp(tierIndex, 0, validTiers.Length - 1)];
            }
            else
            {
                tier = (MonsterTacticsLootController.Tier)EditorGUILayout.EnumPopup("Creature Orb", tier);
                EditorGUILayout.HelpBox(
                    "Silver → 1–2g · Blue → 2–3g · Gold → 3–4g · Master → 4–5g. " +
                    "The family is picked when the ball spawns, NOT when collected.", MessageType.None);
            }

            if (GUILayout.Button("Spawn Monster Creature Orb", GUILayout.Height(34)))
            {
                bool ok = loot.DebugSpawnMonster(tier, specificFamily ? familyName : null, out result);
                resultType = ok ? MessageType.Info : MessageType.Warning;
            }
            if (GUILayout.Button("Collect All Waiting Balls (test only)", GUILayout.Height(27)))
            {
                loot.DebugCollectAll();
                result = "Attempted pickup. Full-bench unit balls will remain on the arena.";
                resultType = MessageType.Info;
            }
            EditorGUILayout.HelpBox(result, resultType);
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("U6.11.9  ·  OPTIONAL AUTOMATIC PvE POKÉMON DROPS",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "OFF by default. When enabled, PvE encounters can drop ONE EXTRA unit ball. " +
                "Existing guaranteed components, item capsules and gold are untouched. " +
                "All settings below affect only the current Play Mode session.", MessageType.Info);
            bool enabled = EditorGUILayout.Toggle("Enable extra PvE balls", loot.AutoPveMonsterBallsEnabled);
            float regularChance = EditorGUILayout.Slider("Regular PvE chance", loot.RegularPveMonsterChance, 0f, 1f);
            float bossChance = EditorGUILayout.Slider("Boss chance", loot.BossPveMonsterChance, 0f, 1f);
            bool stageOne = EditorGUILayout.Toggle("Allow in Stage 1", loot.IncludeStageOneMonsterBalls);
            bool master = EditorGUILayout.Toggle("Allow Master (Stage 7+)", loot.AllowLateMasterMonsterBalls);
            if (enabled != loot.AutoPveMonsterBallsEnabled ||
                !Mathf.Approximately(regularChance, loot.RegularPveMonsterChance) ||
                !Mathf.Approximately(bossChance, loot.BossPveMonsterChance) ||
                stageOne != loot.IncludeStageOneMonsterBalls ||
                master != loot.AllowLateMasterMonsterBalls)
            {
                loot.ConfigureAutoPveMonsterBalls(enabled, regularChance,
                    bossChance, stageOne, master);
            }
            EditorGUILayout.LabelField("Extra PvE balls this match", loot.AutoPveMonsterBallsSpawned.ToString());
            EditorGUILayout.HelpBox(
                "By stage: 1–2 Silver, 3 Blue, 4 Blue/Gold, 5–6 Gold, 7–8 Gold " +
                "(optional 10% Master). The drop chance is separate from the rarity roll.",
                MessageType.None);
            var roundManager = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            if (roundManager != null)
                EditorGUILayout.LabelField("Current match round", roundManager.StageRound);
            previewStage = EditorGUILayout.IntSlider("Stage to simulate", previewStage, 1, 8);
            if (GUILayout.Button("Spawn Stage-Based Monster Ball (manual test)", GUILayout.Height(29)))
            {
                bool ok = loot.DebugSpawnStageMonsterBall(previewStage, out result);
                resultType = ok ? MessageType.Info : MessageType.Warning;
            }
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Tip: test Spriggit and Tricklet using the specific Monster picker. " +
                "If the bench fills, deploy/sell a unit in Planning and collect again. " +
                "For 2★/3★ grants use the separate Unit Grant Tester.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
