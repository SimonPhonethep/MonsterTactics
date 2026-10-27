#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    public static class MonsterTacticsU699bShadowValidator
    {
        [MenuItem("MonsterTactics/Tests/U6.9.9b Verify Shadow Thief Math")]
        private static void Verify()
        {
            Check("no effect", 1f, MonsterTacticsCombatRules.ShadowThiefDamageMultiplier(1f, 0f, 0f));
            Check("bonus", 1.08f, MonsterTacticsCombatRules.ShadowThiefDamageMultiplier(1f, .08f, 0f));
            Check("debuff", .92f, MonsterTacticsCombatRules.ShadowThiefDamageMultiplier(1f, 0f, .08f));
            Check("both", .9936f, MonsterTacticsCombatRules.ShadowThiefDamageMultiplier(1f, .08f, .08f));
            Check("already boosted", 1.38f, MonsterTacticsCombatRules.ShadowThiefDamageMultiplier(1.5f, 0f, .08f));
            Check("clamped", .80f, MonsterTacticsCombatRules.ShadowThiefDamageMultiplier(1f, 0f, .95f));
            Check("slow with shadow", .704f, MonsterTacticsCombatRules.AttackSpeedAfterSlow(.8f * 1.1f, .20f));
            Debug.Log("[U6.9.9b] PASS 7 Shadow Thief math checks. Play Mode: use the Debuff Icons Tester on opposing active fighters.");
            EditorUtility.DisplayDialog("MonsterTactics U6.9.9b", "PASS 7 pure math checks. Verify real-time buffs, debuffs, one-use limit and icons in Play Mode.", "OK");
        }
        private static void Check(string name, float expected, float actual)
        {
            if (Mathf.Abs(expected - actual) > .0001f)
                throw new InvalidOperationException(name + " expected " + expected + ", got " + actual);
        }
    }
}
#endif
