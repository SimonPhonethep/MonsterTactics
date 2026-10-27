using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>U6.3.3: non-destructive validation of the three 5-cost kits.</summary>
    public static class MonsterTacticsLegendaryBalanceValidator
    {
        [MenuItem("MonsterTactics/Tests/Archive/Combat and Monster/U6.3.3 Verify Legendary Balance")]
        public static void Verify()
        {
            string[] names = { "Voltrix", "Pyrothane", "Aqualith" };
            string[] abilities = { "Thunderbolt", "Sacred Fire", "Aurora Beam" };
            int failed = 0;
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                MonsterTacticsU5Roster.Family family = MonsterTacticsU5Roster.Find(name);
                MonsterTacticsCombatRules.Stats stats = MonsterTacticsCombatRules.ForMonster(name);
                MonsterTacticsCombatRules.AbilityProfile ability = MonsterTacticsCombatRules.AbilityFor(name, stats);
                bool passes = family != null && family.Cost == 5 &&
                    ability.Name == abilities[i] && ability.Name != "Strike" &&
                    ability.PrimaryRaw > 0f && stats.Attack > 45f &&
                    (name != "Aqualith" || ability.ShieldRaw > 0f) &&
                    (name != "Pyrothane" || ability.DamageKind == "Physical") &&
                    (name != "Voltrix" || ability.SplashRaw > 0f);
                if (!passes) failed++;
                string details = name + " (" + (family == null ? "missing" : family.Cost + "g") + ")" +
                    " HP=" + (family == null ? 0 : family.BaseHP) +
                    " AD=" + stats.Attack + " AS=" + stats.AttackSpeed.ToString("0.00") +
                    " | " + ability.Name + " (" + ability.Formula + ")";
                if (passes) Debug.Log("[MonsterTactics U6.3.3] PASS " + details);
                else Debug.LogError("[MonsterTactics U6.3.3] FAIL " + details);
            }
            if (failed == 0)
                Debug.Log("[MonsterTactics U6.3.3] All 3 legendary combat kits configured. Test actual balance in Play Mode.");
            else
                Debug.LogError("[MonsterTactics U6.3.3] " + failed + " legendary kit(s) failed validation.");
        }
    }
}
