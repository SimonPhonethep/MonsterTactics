#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Play Mode test of the real Sacred Fire damage/debuff pipeline.
    /// Does not manufacture fake debuff statuses or modify balance data.
    /// </summary>
    public sealed class MonsterTacticsU662PyrothaneAbilityTester : EditorWindow
    {
        private MonsterUnit pyrothane;
        private MonsterUnit enemy;

        [MenuItem("MonsterTactics/Tests/Archive/Combat and Monster/U6.6.2 Pyrothane Sacred Fire Tester")]
        public static void OpenWindow()
        {
            GetWindow<MonsterTacticsU662PyrothaneAbilityTester>("Sacred Fire Test");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Pyrothane — Sacred Fire Playtest", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Run a battle with your own Pyrothane deployed. Select Pyrothane and a living " +
                "enemy within 1 hex. 'Force Sacred Fire' executes its actual cast, " +
                "including physical damage, 1-hex splash, Burn (1% max HP/sec) " +
                "and 33% Wound for 5 seconds. No mana wait needed.", MessageType.Info);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Press Play and start a battle first.", MessageType.Warning);
                return;
            }

            MonsterTacticsCombatDirector combat = Object.FindAnyObjectByType<MonsterTacticsCombatDirector>();
            if (combat == null || !combat.IsBattleActive)
            {
                EditorGUILayout.HelpBox("Start an active battle to test the ability.", MessageType.Warning);
                return;
            }

            if (Selection.activeGameObject != null)
            {
                MonsterUnit selected = Selection.activeGameObject.GetComponentInParent<MonsterUnit>();
                if (selected != null && combat.TryGetSnapshot(selected, out var selectedStats) && selectedStats.InCombat)
                {
                    if (selectedStats.IsEnemy) enemy = selected;
                    else if (MonsterTacticsU5Roster.BaseName(selected.MonsterName) == "Pyrothane") pyrothane = selected;
                }
            }
            pyrothane = EditorGUILayout.ObjectField("Your Pyrothane", pyrothane, typeof(MonsterUnit), true) as MonsterUnit;
            enemy = EditorGUILayout.ObjectField("Enemy target", enemy, typeof(MonsterUnit), true) as MonsterUnit;

            bool valid = pyrothane != null && enemy != null;
            if (valid && combat.TryGetSnapshot(pyrothane, out var pyrothaneStats) &&
                combat.TryGetSnapshot(enemy, out var enemyStats))
            {
                valid = pyrothaneStats.InCombat && !pyrothaneStats.IsEnemy &&
                    MonsterTacticsU5Roster.BaseName(pyrothane.MonsterName) == "Pyrothane" &&
                    enemyStats.InCombat && enemyStats.IsEnemy;
            }
            else valid = false;
            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("Force Sacred Fire (real cast)", GUILayout.Height(32)))
                {
                    if (!combat.DebugCastPyrothaneSacredFire(pyrothane, enemy))
                        Debug.LogWarning("MonsterTactics Sacred Fire test: Pyrothane and enemy must both be " +
                            "alive, actively fighting on opposite sides and within 1 hex.");
                }
            }
            if (enemy != null && combat.TryGetSnapshot(enemy, out var target))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Enemy HP", target.Health + " / " + target.MaximumHealth);
                EditorGUILayout.LabelField("Burn", target.BurnRemaining > 0f
                    ? target.BurnDamagePerSecond.ToString("0.#") + " HP/sec, " +
                      target.BurnRemaining.ToString("0.0") + "s left" : "Inactive");
                EditorGUILayout.LabelField("Wound", target.WoundRemaining > 0f
                    ? "-33% received healing, " + target.WoundRemaining.ToString("0.0") + "s left"
                    : "Inactive");
                EditorGUILayout.LabelField("Burn expected tick", (target.MaximumHealth * 0.01f).ToString("0.#") + " HP before shields/reduction");
                EditorGUILayout.LabelField("Burn ticks completed", target.BurnTicksApplied.ToString());
                EditorGUILayout.LabelField("HP removed last Burn tick", target.LastBurnHpDamage.ToString());
                EditorGUILayout.LabelField("Total HP removed by Burn", target.TotalBurnHpDamage.ToString());
            }
            EditorGUILayout.HelpBox("For a 1,000-HP enemy, expect 10 HP of raw Burn per tick " +
                "at 1-second intervals, for up to 5 ticks. The tick counter measures real " +
                "Burn ticks only; the HP-removed field excludes shield absorption and other " +
                "attacks. Wound only changes healing from sources that actually heal. " +
                "Both debuffs clear at battle end.", MessageType.None);
            if (Event.current.type == EventType.Repaint) Repaint();
        }
    }
}
#endif
