#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>Preview U6.10.1 family visuals without doing damage or changing combat stats.</summary>
    public sealed class MonsterTacticsU6101VfxPreview : EditorWindow
    {
        private static readonly string[] Abilities =
        {
            "Heavy Spark", "Avalanche Tackle", "Four-Armed Fury", "Thunder Rampage",
            "Magma Cannon", "Seismic Guard", "Heavy Slam", "Hammer Arm",
            "Spectral Ambush", "Volcanic Pressure", "Savage Bite", "Forest Shelter",
            "Shadow Ball", "Shadow Pulse", "Wild Charge", "Mystical Burst",
            "Flame Charge", "Dragon Darts: Barrage"
        };

        private MonsterUnit source, target;
        private int selected;
        private Vector2 scroll;

        [MenuItem("MonsterTactics/U6.10.1/Ability VFX Preview (Play Mode)")]
        public static void Open()
        {
            GetWindow<MonsterTacticsU6101VfxPreview>("U6.10.1 VFX Preview").Show();
        }

        [MenuItem("MonsterTactics/U6.10.1/Verify 18 VFX Patterns")]
        public static void Verify()
        {
            int found = 0;
            foreach (string ability in Abilities)
            {
                if (MonsterTacticsU610AbilityVfx.IsNewAbility(ability)) ++found;
                else Debug.LogError("[U6.10.1] Missing VFX: " + ability);
            }
            Debug.Log("[U6.10.1] Registered " + found + "/18 ability VFX patterns. " +
                "This checks the registry, not rendering or Unity combat behavior.");
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("U6.10.1 — Ability VFX Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Enter Play Mode. Assign two scene Monster; they need not be fighting. " +
                "This tool previews effects only; it never applies damage, buffs or debuffs.", MessageType.Info);
            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            source = (MonsterUnit)EditorGUILayout.ObjectField("Caster", source, typeof(MonsterUnit), true);
            target = (MonsterUnit)EditorGUILayout.ObjectField("Victim", target, typeof(MonsterUnit), true);
            if (GUILayout.Button("Use selected Monster as caster"))
            {
                if (Selection.activeGameObject != null)
                    source = Selection.activeGameObject.GetComponentInParent<MonsterUnit>();
            }
            if (GUILayout.Button("Use selected Monster as victim"))
            {
                if (Selection.activeGameObject != null)
                    target = Selection.activeGameObject.GetComponentInParent<MonsterUnit>();
            }
            selected = EditorGUILayout.Popup("Effect", selected, Abilities);
            EditorGUI.BeginDisabledGroup(source == null || target == null);
            if (GUILayout.Button("Preview cast + impact", GUILayout.Height(32)))
                Preview();
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Clear VFX"))
            {
                MonsterTacticsAbilityVfx vfx = FindVfx(false);
                if (vfx != null) vfx.ClearEffects();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("For Heavy Spark and Dragon Darts, the button previews their " +
                "special bonus-attack projectiles as well. All 18 families retain existing " +
                "damage and gameplay logic; previews are visual-only.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void Preview()
        {
            MonsterTacticsAbilityVfx vfx = FindVfx(true);
            if (vfx == null) return;
            string ability = Abilities[selected];
            if (ability == "Heavy Spark") vfx.PlayU610HeavySpark(source, target);
            else if (ability == "Dragon Darts: Barrage")
            {
                vfx.PlayCast(source, target, ability, .31f);
                vfx.PlayU610DragonDart(source, target, 0);
                vfx.PlayU610DragonDart(source, target, 1);
            }
            else
            {
                vfx.PlayCast(source, target, ability, .31f);
                if (ability != "Forest Shelter") vfx.PlayImpact(target, ability);
            }
            SceneView.RepaintAll();
            Debug.Log("[U6.10.1] Previewed " + ability + " on " + source.name +
                " → " + target.name + " (visuals only).");
        }

        private static MonsterTacticsAbilityVfx FindVfx(bool create)
        {
            MonsterTacticsAbilityVfx vfx = Object.FindAnyObjectByType<MonsterTacticsAbilityVfx>();
            if (vfx != null || !create) return vfx;
            MonsterTacticsCombatDirector director = Object.FindAnyObjectByType<MonsterTacticsCombatDirector>();
            if (director != null) return director.gameObject.AddComponent<MonsterTacticsAbilityVfx>();
            Debug.LogWarning("[U6.10.1] No Combat Director in scene. Open your game scene.");
            return null;
        }
    }
}
#endif
