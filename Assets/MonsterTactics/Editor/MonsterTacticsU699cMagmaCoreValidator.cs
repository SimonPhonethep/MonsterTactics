#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    public static class MonsterTacticsU699cMagmaCoreValidator
    {
        [MenuItem("MonsterTactics/Tests/U6.9.9c Verify Magma Core Math")]
        private static void Verify()
        {
            Check("no bonuses", 50, 50, 50, 50, .40f, 80, 0, 0, 0);
            Check("typical 50 + 50", 100, 100, 50, 50, .40f, 80, 20, 20, 40);
            Check("armor only", 150, 30, 50, 30, .40f, 80, 40, 0, 40);
            Check("only negative bonuses", 30, 50, 50, 50, .40f, 80, 0, 0, 0);
            Check("cap split equally", 170, 170, 50, 50, .40f, 80, 40, 40, 80);
            Check("cap uneven", 210, 90, 50, 50, .40f, 80, 64, 16, 80);
            Check("no conversion", 100, 100, 50, 50, 0f, 80, 0, 0, 0);
            Check("zero cap", 100, 100, 50, 50, .40f, 0, 0, 0, 0);
            Debug.Log("[U6.9.9c] PASS 8 Magma Core pure-math tests. Play Mode: manually activate on a combatant, equip an armor/MR item, and verify Burn immunity and live info.");
            EditorUtility.DisplayDialog("MonsterTactics U6.9.9c", "PASS 8 pure-math checks. Runtime conversion, item updates and Burn immunity require Play Mode verification.", "OK");
        }

        private static void Check(string label, int armor, int mr, int baselineArmor,
            int baselineMR, float fraction, float cap, int expectedArmor,
            int expectedMR, int expectedAP)
        {
            MonsterTacticsCombatRules.ComputeMagmaCoreConversion(armor, mr, baselineArmor,
                baselineMR, fraction, cap, out int a, out int m, out float ap);
            if (a != expectedArmor || m != expectedMR || Math.Abs(ap - expectedAP) > .001f)
                throw new InvalidOperationException(label + ": expected " + expectedArmor +
                    "/" + expectedMR + "/" + expectedAP + " got " + a + "/" + m + "/" + ap);
        }
    }
}
#endif
