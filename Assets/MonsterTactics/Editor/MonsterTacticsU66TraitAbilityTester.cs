#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>Quick source/data verification; actual combat still needs a Play Mode test.</summary>
    public static class MonsterTacticsU66TraitAbilityTester
    {
        [MenuItem("MonsterTactics/Tests/Archive/Combat and Monster/U6.6 Trait & Ability Audit")]
        public static void Audit()
        {
            int errors = 0;
            foreach (MonsterTacticsU5Roster.Family family in MonsterTacticsU5Roster.All)
            {
                string name = family.Forms[0];
                var stats = MonsterTacticsCombatRules.ForMonster(name);
                var profile = MonsterTacticsCombatRules.AbilityFor(name, stats);
                if (string.IsNullOrEmpty(profile.Name)) { errors++; Debug.LogError("Missing ability: " + name); }
                if (profile.SplashRaw > 0f && profile.SplashRadius != 1)
                { errors++; Debug.LogError(name + " splash must use radius 1."); }
                if (profile.HitCount < 1) { errors++; Debug.LogError(name + " invalid hit count"); }
            }
            var pyrothane = MonsterTacticsCombatRules.AbilityFor("Pyrothane", MonsterTacticsCombatRules.ForMonster("Pyrothane"));
            var emberlet = MonsterTacticsCombatRules.AbilityFor("Emberlet", MonsterTacticsCombatRules.ForMonster("Emberlet"));
            if (Mathf.Abs(pyrothane.BurnDuration - 5f) > .001f ||
                Mathf.Abs(pyrothane.BurnMaxHpPerSecond - .01f) > .0001f ||
                Mathf.Abs(pyrothane.WoundDuration - 5f) > .001f)
            { errors++; Debug.LogError("Pyrothane Burn/Wound mismatch."); }
            if (emberlet.BurnDuration > 0f || emberlet.WoundDuration > 0f)
            { errors++; Debug.LogError("Emberlet must not Burn or Wound."); }
            if (MonsterTacticsTraits.ActiveTier("Legendary Beasts", new Dictionary<string,int>{{"Legendary Beasts",3}}) != 2 ||
                MonsterTacticsTraits.ActiveTier("First Partners", new Dictionary<string,int>{{"First Partners",6}}) != 3)
            { errors++; Debug.LogError("Trait breakpoints incorrect."); }
            string[] types = {"Fighting","Psychic","Poison","Dragon","Ghost","Dark","Ground","Fire",
                "Fairy","Water","Flying","Normal","Rock","Electric","Bug","Grass","Ice","Steel"};
            foreach (string type in types)
                if (Resources.Load<Texture2D>("TypeIcons/" + type) == null)
                { errors++; Debug.LogError("Missing type icon: " + type); }
            if (errors == 0)
                Debug.Log("MonsterTactics U6.6 trait and ability audit: PASS (13 families, 18 icons, status rules). " +
                    "Still verify combat damage, burn ticks, trait bonuses and the UI in Play Mode.");
            else
                Debug.LogError("MonsterTactics U6.6 audit found " + errors + " issues.");
        }
    }
}
#endif
