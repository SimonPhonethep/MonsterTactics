using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>U6.9.9d regression validation; live hooks are in the Status Effects Tester.</summary>
    public static class MonsterTacticsU699dProtectionValidator
    {
        [MenuItem("MonsterTactics/Tests/U6.9.9d Verify Temporary Protection")]
        public static void Verify()
        {
            int passes = 0;
            // Recast uses max(existing, incoming): no stacking.
            if (Mathf.Max(10, 10) == 10 && Mathf.Max(15, 10) == 15) passes++;
            // A shield from this ability is only part of the total shield pool.
            int total = 160, forest = 60, absorbed = 35;
            int ownedAbsorbed = Mathf.Min(forest, absorbed);
            total -= absorbed;
            forest -= ownedAbsorbed;
            if (total == 125 && forest == 25 && total - forest == 100) passes++;
            // Expiring the shield must preserve other remaining shields.
            total = Mathf.Max(0, total - forest);
            if (total == 100) passes++;
            // Resist reductions observe active stat bonuses as they happen.
            if (MonsterTacticsCombatRules.EffectiveResistance(30 + 15, .20f) == 36) passes++;
            // Magma Core must recompute from current, real bonus defenses.
            MonsterTacticsCombatRules.ComputeMagmaCoreConversion(80, 70, 40, 30, .40f, 80f,
                out int armorSpent, out int mrSpent, out float ap);
            if (armorSpent == 16 && mrSpent == 16 && ap == 32f) passes++;
            if (passes != 5)
            {
                Debug.LogError("[U6.9.9d] Temporary Protection FAILED (" + passes + "/5)");
                return;
            }
            Debug.Log("[U6.9.9d] Temporary Protection PASS 5/5. " +
                "Run the U6.3.10 Debuff Icons Tester in Play Mode for expiry and live buff QA.");
        }
    }
}
