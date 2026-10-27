using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>Pure, deterministic two-hex chain target selection for Stormlynx.
    /// This helper is kept separate from the combat state so it can be verified in the Editor.</summary>
    public static class MonsterTacticsU611StormlynxRules
    {
        public const int ChainHexRange = 2;
        public const int MaximumSecondaryTargets = 2;

        /// <summary>Positions must contain only living enemies, excluding the primary.
        /// Returns distinct indices nearest to primary; ties use input order.</summary>
        public static int[] SelectTargets(Vector2Int primary, IList<Vector2Int> elidunebit)
        {
            if (elidunebit == null || elidunebit.Count == 0) return new int[0];
            var chosen = new List<int>(MaximumSecondaryTargets);
            for (int step = 0; step < MaximumSecondaryTargets; ++step)
            {
                int selected = -1, closest = int.MaxValue;
                for (int i = 0; i < elidunebit.Count; ++i)
                {
                    if (chosen.Contains(i)) continue;
                    int distance = MonsterTacticsCombatRules.HexDistance(primary, elidunebit[i]);
                    if (distance > ChainHexRange || distance >= closest) continue;
                    closest = distance;
                    selected = i;
                }
                if (selected < 0) break;
                chosen.Add(selected);
            }
            return chosen.ToArray();
        }
    }
}
