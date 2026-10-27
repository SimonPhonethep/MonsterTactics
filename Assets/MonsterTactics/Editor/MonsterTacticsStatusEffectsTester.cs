using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>Temporary Editor-only controls to preview debuffs before items and traits exist.</summary>
    public sealed class MonsterTacticsStatusEffectsTester : EditorWindow
    {
        private Vector2 scrollPosition;
        private MonsterUnit target;
        private MonsterUnit shadowCaster;
        private MonsterUnit shadowVictim;
        private float duration = MonsterTacticsCombatRules.DefaultDebuffDuration;
        private float attackSlow = .20f;
        private int fortressArmor = 15;
        private int fortressMr = 15;
        private float fortressRatio = .60f;
        private float magmaFraction = .40f;
        private float magmaApCap = 80f;
        private MonsterUnit protectionCaster;
        private int forestDefense = 10;
        private float forestShield = .16f;
        private float protectionDuration = 4f;

        [MenuItem("MonsterTactics/Tests/Archive/Combat and Monster/U6.3.10 Debuff Icons Tester")]
        private static void OpenWindow()
        {
            GetWindow<MonsterTacticsStatusEffectsTester>("Combat Debuff Tester");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("U6.3.10 — Icons, Live Stats & 30% Debuffs", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Enter Play Mode, start a battle and select a Monster in the Hierarchy. Apply a debuff. Watch colored icons under its combat HP bar, then open its info panel. Effective defenses, healing received, and debuff timers update live. Effects clear on battle end.", MessageType.Info);
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode and begin combat first.", MessageType.Warning);
                return;
            }

            MonsterTacticsCombatDirector director = FindAnyObjectByType<MonsterTacticsCombatDirector>();
            if (director == null)
            {
                EditorGUILayout.HelpBox("Could not find the combat director in the scene.", MessageType.Warning);
                return;
            }
            if (director.IsBattleActive)
                EditorGUILayout.HelpBox("Battle phase ACTIVE. Choose living fighters below. Effects can be applied now.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Current phase: " + director.MatchPhase +
                    ". Test buttons are disabled until the Battle phase starts. Keep this window open and begin a combat round.",
                    MessageType.Warning);

            // This tester has many sections; keep controls reachable on smaller screens.
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (target == null && Selection.activeGameObject != null)
                target = Selection.activeGameObject.GetComponentInParent<MonsterUnit>();
            target = EditorGUILayout.ObjectField("Target Monster", target, typeof(MonsterUnit), true) as MonsterUnit;
            duration = EditorGUILayout.Slider("Duration (seconds)", duration, 1f, 15f);
            attackSlow = EditorGUILayout.Slider("Ice Slow fraction", attackSlow, .05f, .50f);
            using (new EditorGUI.DisabledScope(target == null || !director.IsBattleActive))
            {
                if (GUILayout.Button("Apply Sunder — 30% Armor reduction"))
                    Apply(director.ApplySunder(target, .30f, duration), "Sunder");
                if (GUILayout.Button("Apply Shred — 30% MR reduction"))
                    Apply(director.ApplyShred(target, .30f, duration), "Shred");
                if (GUILayout.Button("Apply Ice Slow — lower real attack rate"))
                    Apply(director.ApplyAttackSpeedSlow(target, attackSlow, duration), "Ice Slow");
                if (GUILayout.Button("Apply Wound — 33% less healing"))
                    Apply(director.ApplyWound(target, duration), "Wound");
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("U6.9.9a Iron Fortress (future Evodrel trait)", EditorStyles.boldLabel);
            fortressArmor = EditorGUILayout.IntSlider("Bonus Armor", fortressArmor, 0, 50);
            fortressMr = EditorGUILayout.IntSlider("Bonus MR", fortressMr, 0, 50);
            fortressRatio = EditorGUILayout.Slider("Armor -> Magic coefficient", fortressRatio, 0f, 2f);
            using (new EditorGUI.DisabledScope(target == null || !director.IsBattleActive))
            {
                if (GUILayout.Button("Enable Iron Fortress on selected fighter"))
                    Apply(director.EnableIronFortress(target, fortressArmor, fortressMr, 4,
                        fortressRatio), "Iron Fortress (only once per combat)");
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("U6.9.9c Magma Core (future Astramora)", EditorStyles.boldLabel);
            magmaFraction = EditorGUILayout.Slider("Armor/MR conversion", magmaFraction, 0f, 1f);
            magmaApCap = EditorGUILayout.Slider("AP conversion cap", magmaApCap, 0f, 160f);
            using (new EditorGUI.DisabledScope(target == null || !director.IsBattleActive))
            {
                if (GUILayout.Button("Enable Magma Core on selected fighter"))
                    Apply(director.EnableMagmaCore(target, magmaFraction, magmaApCap),
                        "Magma Core (only once per combat)");
                if (GUILayout.Button("Attempt Burn on selected fighter"))
                {
                    bool applied = director.ApplyBurn(target, .01f, 5f);
                    Debug.Log("[U6.9.9c] Burn applied? " + applied +
                        " (should be false if Magma Core is active).");
                }
            }
            EditorGUILayout.HelpBox("Magma Core converts bonus Armor/MR (not baseline defenses) " +
                "into AP, capped at the amount selected. Equip an Armor/MR item during combat " +
                "and watch live stats update. The Burn test should be rejected once active.", MessageType.None);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("U6.9.9d Temporary Protection (future Vexokel)", EditorStyles.boldLabel);
            protectionCaster = EditorGUILayout.ObjectField("Protection caster", protectionCaster,
                typeof(MonsterUnit), true) as MonsterUnit;
            protectionCaster = ChooseLivingCombatant("Pick live protection caster", protectionCaster, director);
            if (director.IsBattleActive && protectionCaster != null &&
                !IsLivingCombatant(director, protectionCaster))
                EditorGUILayout.HelpBox("The chosen caster isn't currently a living battlefield fighter. Use the picker above.",
                    MessageType.Warning);
            forestDefense = EditorGUILayout.IntSlider("Ally Armor / MR", forestDefense, 0, 100);
            forestShield = EditorGUILayout.Slider("Self Shield % Max HP", forestShield, .05f, .5f);
            protectionDuration = EditorGUILayout.Slider("Protection duration", protectionDuration, 1f, 10f);
            using (new EditorGUI.DisabledScope(target == null || !director.IsBattleActive))
            {
                if (GUILayout.Button("Apply Temporary Armor + MR on target"))
                    Apply(director.ApplyTemporaryDefenseBuff(target, forestDefense,
                        forestDefense, protectionDuration), "Forest Guard");
                if (GUILayout.Button("Apply Temporary Shield on target"))
                    Apply(director.ApplyTemporaryShield(target, forestShield,
                        protectionDuration), "Forest Shield");
            }
            using (new EditorGUI.DisabledScope(!IsLivingCombatant(director, protectionCaster)))
            {
                if (GUILayout.Button("Shield caster + buff 2 nearest allies"))
                    Apply(director.GrantNearestAllyProtection(protectionCaster,
                        forestShield, forestDefense, forestDefense, protectionDuration, 2),
                        "Nearest Ally Protection");
            }
            EditorGUILayout.HelpBox("Select a caster and living teammates. Repeated casts refresh " +
                "the strongest buffs without stacking. Test the timer, green shield icon, " +
                "shields that survive other shield expirations, and Magma Core AP conversion. " +
                "Vexokel is not included in this patch.", MessageType.None);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("U6.9.9b Shadow Thief (future Umbrelisk)", EditorStyles.boldLabel);
            if (shadowCaster == null && Selection.activeGameObject != null)
                shadowCaster = Selection.activeGameObject.GetComponentInParent<MonsterUnit>();
            shadowCaster = EditorGUILayout.ObjectField("Thief (caster)",
                shadowCaster, typeof(MonsterUnit), true) as MonsterUnit;
            shadowCaster = ChooseLivingCombatant("Pick live thief", shadowCaster, director);
            shadowVictim = EditorGUILayout.ObjectField("Victim (target)",
                shadowVictim, typeof(MonsterUnit), true) as MonsterUnit;
            if (IsLivingCombatant(director, shadowCaster) &&
                director.TryGetSnapshot(shadowCaster, out MonsterTacticsCombatDirector.CombatSnapshot thiefSnapshot))
                shadowVictim = ChooseLivingCombatant("Pick opposing victim", shadowVictim, director,
                    !thiefSnapshot.IsEnemy);
            else
                shadowVictim = ChooseLivingCombatant("Pick live victim", shadowVictim, director);
            bool validThief = IsLivingCombatant(director, shadowCaster);
            bool validVictim = IsLivingCombatant(director, shadowVictim);
            bool opposingSides = false;
            if (validThief && validVictim)
            {
                director.TryGetSnapshot(shadowCaster, out MonsterTacticsCombatDirector.CombatSnapshot thiefSide);
                director.TryGetSnapshot(shadowVictim, out MonsterTacticsCombatDirector.CombatSnapshot victimSide);
                opposingSides = thiefSide.IsEnemy != victimSide.IsEnemy;
            }
            if (director.IsBattleActive && (!validThief || !validVictim || !opposingSides))
                EditorGUILayout.HelpBox("Select two living battlefield Monster on OPPOSING teams. The two fields are independent now.",
                    MessageType.Warning);
            using (new EditorGUI.DisabledScope(!validThief || !validVictim || !opposingSides))
            {
                if (GUILayout.Button("Apply Shadow Thief: 12% AS, 8% damage, 5s"))
                    Apply(director.TryApplyShadowThief(shadowCaster, shadowVictim),
                        "Shadow Thief (once per combat; enemies only)");
            }
            EditorGUILayout.HelpBox("Choose opposing living combatants. This simulates the effect of a " +
                "successful future Spectral Ambush hit; it does NOT add Umbrelisk to the roster.", MessageType.None);
                        EditorGUILayout.HelpBox("Once active, let the target receive four normal basic " +
                "attacks (shielded hits count). Inspect the 0/4 -> 3/4 progress and " +
                "counterattack damage in Monster Info. Abilities and item bonus hits do not count.",
                MessageType.None);
            if (target != null && director.TryGetSnapshot(target, out MonsterTacticsCombatDirector.CombatSnapshot snapshot))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Current status (reapplications never stack):", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Armor (effective / base)",
                    snapshot.EffectiveArmor + " / " + snapshot.Stats.Armor);
                EditorGUILayout.LabelField("Magic Resist (effective / base)",
                    snapshot.EffectiveMagicResist + " / " + snapshot.Stats.MagicResist);
                EditorGUILayout.LabelField("Healing received", snapshot.Wounded ? "67%" : "100%");
                EditorGUILayout.LabelField("Actual Attack Speed", snapshot.Stats.AttackSpeed.ToString("0.00") + "/sec");
                EditorGUILayout.LabelField("Temporary Armor/MR", snapshot.TemporaryDefenseRemaining > 0f
                    ? "+" + snapshot.TemporaryArmorBonus + " / +" + snapshot.TemporaryMagicResistBonus +
                      " (" + snapshot.TemporaryDefenseRemaining.ToString("0.0") + "s)"
                    : "Inactive");
                EditorGUILayout.LabelField("Temporary Shield", snapshot.TemporaryShieldRemaining > 0f
                    ? snapshot.TemporaryShieldAmount + " remaining (" +
                      snapshot.TemporaryShieldRemaining.ToString("0.0") + "s)"
                    : "Inactive");
                EditorGUILayout.LabelField("Magma Core", snapshot.MagmaCoreActive
                    ? "Burn immune | -" + snapshot.MagmaCoreConvertedArmor + " Armor | -" +
                      snapshot.MagmaCoreConvertedMagicResist + " MR | +" +
                      snapshot.MagmaCoreGainedAbilityPower.ToString("0") + " AP"
                    : "Inactive");
                EditorGUILayout.LabelField("Iron Fortress", snapshot.IronFortressActive
                    ? snapshot.IronFortressBasicHits + "/" + snapshot.IronFortressHitsRequired +
                      " attacks; " + snapshot.IronFortressRetaliationRaw.ToString("0.#") + " raw magic damage"
                    : "Inactive");
                EditorGUILayout.LabelField("Ice Slow", snapshot.AttackSpeedSlowRemaining > 0f
                    ? (snapshot.AttackSpeedSlowFraction * 100f).ToString("0") + "% AS reduction — " +
                      snapshot.AttackSpeedSlowRemaining.ToString("0.0") + "s left"
                    : "Inactive");
                EditorGUILayout.LabelField("Sunder", snapshot.SunderRemaining > 0f
                    ? (snapshot.ArmorReduction * 100f).ToString("0") + "% — " + snapshot.SunderRemaining.ToString("0.0") + "s left"
                    : "Inactive");
                EditorGUILayout.LabelField("Shred", snapshot.ShredRemaining > 0f
                    ? (snapshot.MagicResistReduction * 100f).ToString("0") + "% — " + snapshot.ShredRemaining.ToString("0.0") + "s left"
                    : "Inactive");
                EditorGUILayout.LabelField("Wound", snapshot.Wounded
                    ? "33% healing reduction — " + snapshot.WoundRemaining.ToString("0.0") + "s left"
                    : "Inactive");
            }
            EditorGUILayout.EndScrollView();
            Repaint();
        }

        // Editor-only convenience picker: ObjectFields accept scene references, but
        // opponents may be hard to find in a crowded Hierarchy during combat.
        // Show only living fighters in the active Battle phase.
        private static MonsterUnit ChooseLivingCombatant(string label, MonsterUnit selected,
            MonsterTacticsCombatDirector director, bool? enemySide = null)
        {
            if (director == null || !director.IsBattleActive) return selected;
            var units = new List<MonsterUnit>();
            var labels = new List<string> { "— Select living combatant —" };
            foreach (MonsterUnit unit in Object.FindObjectsByType<MonsterUnit>())
            {
                if (!IsLivingCombatant(director, unit)) continue;
                if (!director.TryGetSnapshot(unit, out MonsterTacticsCombatDirector.CombatSnapshot info)) continue;
                if (enemySide.HasValue && info.IsEnemy != enemySide.Value) continue;
                units.Add(unit);
                labels.Add((info.IsEnemy ? "ENEMY · " : "ALLY · ") + unit.name +
                    " (" + info.Health + " HP)");
            }
            int selectedIndex = units.IndexOf(selected) + 1;
            int pickedIndex = EditorGUILayout.Popup(label, selectedIndex, labels.ToArray());
            if (pickedIndex != selectedIndex)
                return pickedIndex > 0 && pickedIndex <= units.Count ? units[pickedIndex - 1] : null;
            return selected;
        }

        private static bool IsLivingCombatant(MonsterTacticsCombatDirector director, MonsterUnit unit)
        {
            if (director == null || !director.IsBattleActive || unit == null) return false;
            return director.TryGetSnapshot(unit, out MonsterTacticsCombatDirector.CombatSnapshot info) &&
                info.InCombat && info.Health > 0;
        }

        private static void Apply(bool success, string effect)
        {
            if (!success) Debug.LogWarning("MonsterTactics: Could not apply " + effect + ". Target must be alive and actively in combat.");
        }
    }
}
