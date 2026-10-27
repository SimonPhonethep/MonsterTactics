#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// U6.6.3: Non-destructive editor audit of ability geometry and tooltip data.
    /// Predicts targets from real hex coordinates; never fabricates a combat cast.
    /// </summary>
    public sealed class MonsterTacticsU663AbilityGeometryTester : EditorWindow
    {
        private MonsterUnit caster;
        private MonsterUnit primary;
        private Vector2 scroll;

        [MenuItem("MonsterTactics/Tests/Archive/Combat and Monster/U6.6.3 Ability Range Audit")]
        public static void Open() => GetWindow<MonsterTacticsU663AbilityGeometryTester>("Ability Geometry");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Ability & Hex Range Audit", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "In Play Mode, choose a Monster and a target to check range and which " +
                "additional enemies are within its ability splash radius. This does not " +
                "cast an ability or change combat.", MessageType.Info);
            caster = EditorGUILayout.ObjectField("Caster", caster, typeof(MonsterUnit), true) as MonsterUnit;
            primary = EditorGUILayout.ObjectField("Primary target", primary, typeof(MonsterUnit), true) as MonsterUnit;
            if (GUILayout.Button("Check all 13 shop ability profiles"))
                CheckDefinitions();
            if (caster == null) return;
            MonsterTacticsCombatDirector combat = Object.FindAnyObjectByType<MonsterTacticsCombatDirector>();
            MonsterTacticsCombatRules.Stats stats = MonsterTacticsU5Roster.CombatStats(caster);
            if (combat != null && combat.TryGetSnapshot(caster, out var current))
                stats = current.Stats;
            string baseName = MonsterTacticsU5Roster.BaseName(caster.MonsterName);
            var profile = MonsterTacticsCombatRules.AbilityFor(baseName, stats);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(baseName + " — " + profile.Name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Normal attack / cast range", stats.Range + " hex(es)");
            EditorGUILayout.LabelField("Damage kind", profile.DamageKind);
            EditorGUILayout.LabelField("Primary raw damage", profile.PrimaryRaw.ToString("0.##"));
            EditorGUILayout.LabelField("Splash raw damage", profile.SplashRaw.ToString("0.##"));
            EditorGUILayout.LabelField("Splash radius", profile.SplashRadius + " hex(es) from primary");
            EditorGUILayout.LabelField("Secondary limit", profile.MaximumSecondaryTargets == int.MaxValue
                ? "All nearby enemies" : profile.MaximumSecondaryTargets.ToString());
            EditorGUILayout.LabelField("Hit count", profile.HitCount.ToString());
            EditorGUILayout.LabelField("Burn", profile.BurnDuration > 0f
                ? (profile.BurnMaxHpPerSecond * 100f).ToString("0.#") + "% max HP/s for " +
                  profile.BurnDuration.ToString("0.#") + "s"
                : "None");
            EditorGUILayout.LabelField("Wound", profile.WoundDuration > 0f
                ? "33% healing reduction for " + profile.WoundDuration.ToString("0.#") + "s"
                : "None");
            if (caster.OccupiedTile == null || primary == null || primary.OccupiedTile == null)
            {
                EditorGUILayout.HelpBox("Caster and target must occupy board hexes to check distance.", MessageType.None);
                return;
            }
            int dist = MonsterTacticsCombatRules.HexDistance(
                MonsterTacticsCombatRules.Coord(caster.OccupiedTile),
                MonsterTacticsCombatRules.Coord(primary.OccupiedTile));
            EditorGUILayout.LabelField("Target distance", dist + " hex(es)");
            EditorGUILayout.HelpBox(dist <= stats.Range
                ? "Target is inside the caster's normal cast range."
                : "Target is out of range; caster must move closer before a normal cast.",
                dist <= stats.Range ? MessageType.Info : MessageType.Warning);
            if (profile.SplashRaw <= 0f) return;
            EditorGUILayout.LabelField("Nearby enemies affected by splash:", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(120));
            int shown = 0;
            MonsterUnit[] units = Object.FindObjectsByType<MonsterUnit>();
            foreach (MonsterUnit unit in units)
            {
                if (unit == null || unit == primary || unit == caster ||
                    unit.OccupiedTile == null || !unit.gameObject.activeInHierarchy) continue;
                if (combat != null && combat.IsBattleActive &&
                    combat.TryGetSnapshot(primary, out var targetSnapshot) &&
                    combat.TryGetSnapshot(unit, out var possibleSnapshot) &&
                    targetSnapshot.InCombat && possibleSnapshot.InCombat &&
                    targetSnapshot.IsEnemy != possibleSnapshot.IsEnemy) continue;
                int radius = MonsterTacticsCombatRules.HexDistance(
                    MonsterTacticsCombatRules.Coord(primary.OccupiedTile),
                    MonsterTacticsCombatRules.Coord(unit.OccupiedTile));
                if (radius > profile.SplashRadius) continue;
                shown++;
                EditorGUILayout.LabelField(unit.MonsterName + " — " + radius + " hex(es) away");
            }
            if (shown == 0) EditorGUILayout.LabelField("No secondary units within radius");
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox("This is a geometry preview. Real combat also checks living " +
                "targets, enemy teams, timing, shields and maximum secondary target count.", MessageType.None);
        }

        private static void CheckDefinitions()
        {
            string[] shop = {"Pebblin", "Flarbit", "Mysprit", "Mosslet", "Emberlet",
                "Budhorn", "Ripplet", "Tusklet", "Dunebit", "Mistrip", "Voltrix", "Pyrothane", "Aqualith"};
            var errors = new List<string>();
            foreach (string name in shop)
            {
                var stats = MonsterTacticsCombatRules.ForMonster(name);
                var ability = MonsterTacticsCombatRules.AbilityFor(name, stats);
                if (ability.Name != stats.Ability)
                    errors.Add(name + " ability name differs between combat stats and profile");
                if (stats.Range < 1 || ability.HitCount < 1)
                    errors.Add(name + " invalid range or hit count");
                if (ability.SplashRaw > 0f && ability.SplashRadius < 1)
                    errors.Add(name + " splash damage has no target radius");
                if (name == "Emberlet" && (ability.BurnDuration > 0 || ability.WoundDuration > 0))
                    errors.Add("Emberlet should not apply Burn/Wound");
                if (name == "Pyrothane" && (ability.BurnDuration != 5f || ability.WoundDuration != 5f ||
                    Mathf.Abs(ability.BurnMaxHpPerSecond - .01f) > .0001f || ability.SplashRadius != 1))
                    errors.Add("Pyrothane Burn/Wound/radius mismatch");
                if (name == "Mistrip" && ability.HitCount != 3)
                    errors.Add("Mistrip should strike three times");
                if (name == "Mysprit" && ability.MaximumSecondaryTargets != 2)
                    errors.Add("Mysprit should splash to at most 2 secondary enemies");
            }
            if (errors.Count == 0)
                Debug.Log("MonsterTactics U6.6.3 static ability profile audit: PASS (13 families). " +
                    "This does not replace Unity Play Mode combat testing.");
            else
                Debug.LogError("MonsterTactics U6.6.3 static ability profile audit FAILED:\n" +
                    string.Join("\n", errors));
        }
    }
}
#endif
