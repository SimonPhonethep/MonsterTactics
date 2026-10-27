using System.Collections.Generic;

namespace MonsterTactics
{
    /// <summary>
    /// Deterministic and testable selection rules; no Unity scene state here.
    /// First prefer reachable enemies, then the weakest by HP percentage from
    /// the nearest reachable enemy's hex distance plus ExtraSearchHexes.
    /// No globally low-HP target may pull an Assassin across the battlefield.
    /// </summary>
    public static class MonsterTacticsU6112AssassinTargetingRules
    {
        public struct Candidate
        {
            public int Distance;
            public int Health;
            public int MaximumHealth;
            public bool Reachable;
            public Candidate(int distance, int hp, int maxHp, bool reachable)
            {
                Distance = distance;
                Health = hp;
                MaximumHealth = maxHp;
                Reachable = reachable;
            }
        }

        /// <summary>Returns -1 for empty candidates. Input order breaks exact ties.</summary>
        public static int Select(IList<Candidate> candidates, int extraSearchHexes)
        {
            if (candidates == null || candidates.Count == 0) return -1;
            bool hasReachable = false;
            int minDistance = int.MaxValue;
            foreach (Candidate candidate in candidates)
            {
                if (candidate.Health <= 0 || candidate.MaximumHealth <= 0 || !candidate.Reachable) continue;
                hasReachable = true;
                if (candidate.Distance < minDistance) minDistance = candidate.Distance;
            }
            // If the battlefield is temporarily blocked, keep a stable target
            // instead of idling or switching to arbitrary distant enemies.
            if (!hasReachable)
            {
                foreach (Candidate candidate in candidates)
                    if (candidate.Health > 0 && candidate.MaximumHealth > 0 && candidate.Distance < minDistance)
                        minDistance = candidate.Distance;
            }
            if (minDistance == int.MaxValue) return -1;
            int maxDistance = minDistance + (extraSearchHexes < 0 ? 0 : extraSearchHexes);
            int best = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate c = candidates[i];
                if (c.Health <= 0 || c.MaximumHealth <= 0 || c.Distance > maxDistance ||
                    (hasReachable && !c.Reachable)) continue;
                if (best == -1) { best = i; continue; }
                Candidate b = candidates[best];
                // Ratio comparison avoids floating-point ties and uses current,
                // not maximum or absolute health, for execute targeting.
                long aHealth = (long)c.Health * b.MaximumHealth;
                long bHealth = (long)b.Health * c.MaximumHealth;
                if (aHealth < bHealth ||
                    (aHealth == bHealth && c.Distance < b.Distance) ||
                    (aHealth == bHealth && c.Distance == b.Distance && c.Health < b.Health))
                    best = i;
            }
            return best;
        }
    }
}
