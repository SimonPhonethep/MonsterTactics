using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics.Editor
{
    /// <summary>Non-destructive U4.1 installer. Does not regenerate any scene content.</summary>
    public static class MonsterTacticsAbilityMenu
    {
        [MenuItem("MonsterTactics/Combat/Install U4.1 VFX + Monster Inspector", priority = 98)]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics U4.1", "Stop Play mode before installing.", "OK");
                return;
            }

            GameObject directorRoot = GameObject.Find("MonsterTactics U4 Combat Director");
            MonsterTacticsCombatDirector combat = directorRoot != null ?
                directorRoot.GetComponent<MonsterTacticsCombatDirector>() : null;
            if (combat == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U4.1",
                    "Can't find the U4 Combat Director. In this scene, install U4 Auto-Combat first. " +
                    "Do not regenerate your arena or Monster.", "OK");
                return;
            }

            MonsterTacticsAbilityVfx vfx = directorRoot.GetComponent<MonsterTacticsAbilityVfx>();
            if (vfx == null) vfx = Undo.AddComponent<MonsterTacticsAbilityVfx>(directorRoot);

            MonsterTacticsMonsterInspector inspector = directorRoot.GetComponent<MonsterTacticsMonsterInspector>();
            if (inspector == null)
                inspector = Undo.AddComponent<MonsterTacticsMonsterInspector>(directorRoot);

            SerializedObject directorSerialized = new SerializedObject(combat);
            SerializedProperty vfxProperty = directorSerialized.FindProperty("abilityVfx");
            if (vfxProperty == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U4.1",
                    "The U4.1 Combat Director source isn't installed yet. Copy the ZIP's Scripts/Combat files first.", "OK");
                return;
            }
            vfxProperty.objectReferenceValue = vfx;
            directorSerialized.ApplyModifiedProperties();

            SerializedObject inspectorSerialized = new SerializedObject(inspector);
            inspectorSerialized.FindProperty("combat").objectReferenceValue = combat;
            Camera sceneCamera = Camera.main;
            if (sceneCamera == null) sceneCamera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            inspectorSerialized.FindProperty("boardCamera").objectReferenceValue = sceneCamera;
            inspectorSerialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(combat);
            EditorUtility.SetDirty(inspector);
            EditorUtility.SetDirty(vfx);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = directorRoot;
            Debug.Log("MonsterTactics U4.1 installed. Save the scene; in Play mode click a Monster for the " +
                "live stats / ability / mana panel. START BATTLE to see projectile VFX.");
        }

        [MenuItem("MonsterTactics/Combat/Run U4.1 Ability Scaling Self-Check", priority = 99)]
        public static void Verify()
        {
            MonsterTacticsCombatRules.Stats p = MonsterTacticsCombatRules.ForMonster("Ripplet");
            MonsterTacticsCombatRules.AbilityProfile aqua = MonsterTacticsCombatRules.AbilityFor("Ripplet", p);
            Assert(Mathf.Approximately(aqua.PrimaryRaw, 20f + 1.65f * p.Attack), "Aqua Jet AD scaling");
            Assert(aqua.DamageKind == "Physical" && p.ManaCost == 65, "Ripplet damage / mana");

            MonsterTacticsCombatRules.Stats c = MonsterTacticsCombatRules.ForMonster("Emberlet");
            MonsterTacticsCombatRules.AbilityProfile flame = MonsterTacticsCombatRules.AbilityFor("Emberlet", c);
            Assert(Mathf.Approximately(flame.PrimaryRaw, 200f) &&
                   Mathf.Approximately(flame.SplashRaw, 65f), "Flamethrower AP + splash");

            MonsterTacticsCombatRules.Stats b = MonsterTacticsCombatRules.ForMonster("Budhorn");
            Assert(Mathf.Approximately(MonsterTacticsCombatRules.AbilityFor("Budhorn", b).HealRaw, 75f),
                "Leech Seed healing");

            MonsterTacticsCombatRules.Stats s = MonsterTacticsCombatRules.ForMonster("Flarbit");
            Assert(Mathf.Approximately(MonsterTacticsCombatRules.AbilityFor("Flarbit", s).SplashRaw, 80f),
                "Pyro Ball splash");

            MonsterTacticsCombatRules.Stats f = MonsterTacticsCombatRules.ForMonster("Mistrip");
            Assert(MonsterTacticsCombatRules.AbilityFor("Mistrip", f).HitCount == 3,
                "Water Shuriken triple hit");
            Assert(Mathf.Approximately(MonsterTacticsCombatRules.Mitigate(200f, 100), 100f),
                "Armor/MR mitigation");
            Debug.Log("MonsterTactics U4.1 PASS — ability scaling, mana costs, splash, healing, " +
                "shuriken hit count, and mitigation.");
        }

        private static void Assert(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("U4.1 self-check failed: " + description);
        }
    }
}
