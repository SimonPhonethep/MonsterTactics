#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    // Pure calculations plus QA guidance for live combat-only hit dispatch.
    public static class MonsterTacticsU699aIronValidator
    {
        [MenuItem("MonsterTactics/Tests/U6.9.9a Verify Ice Slow + Iron Fortress")]
        private static void Verify()
        {
            int checks = 0;
            VerifyFloat("20% Slow", MonsterTacticsCombatRules.AttackSpeedAfterSlow(.8f, .2f), .64f, ref checks);
            VerifyFloat("Slow max cap", MonsterTacticsCombatRules.AttackSpeedAfterSlow(.8f, .7f), .4f, ref checks);
            VerifyFloat("Basic cooldown", MonsterTacticsCombatRules.RescaledBasicAttackDeadline(.5f, 1f, .8f, .64f), 1.125f, ref checks);
            VerifyInt("1st hit", MonsterTacticsCombatRules.NextIronFortressCounter(0,4), 1, ref checks);
            VerifyInt("2nd hit", MonsterTacticsCombatRules.NextIronFortressCounter(1,4), 2, ref checks);
            VerifyInt("3rd hit", MonsterTacticsCombatRules.NextIronFortressCounter(2,4), 3, ref checks);
            VerifyInt("4th resets counter", MonsterTacticsCombatRules.NextIronFortressCounter(3,4), 0, ref checks);
            VerifyFlag("4th triggers", MonsterTacticsCombatRules.IronFortressTriggers(3,4), true, ref checks);
            VerifyFlag("3rd not triggered", MonsterTacticsCombatRules.IronFortressTriggers(2,4), false, ref checks);
            VerifyFloat("150 armor deals 90 raw magic", MonsterTacticsCombatRules.IronFortressRawMagicDamage(150,.60f), 90f, ref checks);
            VerifyFloat("100 armor, 30% sunder deals 42", MonsterTacticsCombatRules.IronFortressRawMagicDamage(
                MonsterTacticsCombatRules.EffectiveResistance(100,.30f),.60f), 42f, ref checks);
            VerifyFloat("No negative retaliation", MonsterTacticsCombatRules.IronFortressRawMagicDamage(-30,.60f), 0f, ref checks);
            VerifyFloat("Shielded attacks don't change formula", MonsterTacticsCombatRules.IronFortressRawMagicDamage(75,.60f), 45f, ref checks);
            Debug.Log("[U6.9.9a] PASS " + checks + " Ice Slow and Iron Fortress math checks. " +
                "Next, Play Mode: Tests > Archive > Combat and Monster > Debuff Icons Tester.");
            EditorUtility.DisplayDialog("MonsterTactics U6.9.9a", "PASS " + checks +
                " arithmetic checks. Test actual counterattacks and icons in Play Mode.", "OK");
        }
        private static void VerifyFloat(string name,float actual,float expected,ref int checks)
        {
            if (Mathf.Abs(actual-expected)>.0001f)
                throw new InvalidOperationException(name+" expected "+expected+" got "+actual);
            checks++;
        }
        private static void VerifyInt(string name,int actual,int expected,ref int checks)
        {
            if(actual!=expected) throw new InvalidOperationException(name+" expected "+expected+" got "+actual);
            checks++;
        }
        private static void VerifyFlag(string name,bool actual,bool expected,ref int checks)
        {
            if(actual!=expected) throw new InvalidOperationException(name+" expected "+expected+" got "+actual);
            checks++;
        }
    }
}
#endif
