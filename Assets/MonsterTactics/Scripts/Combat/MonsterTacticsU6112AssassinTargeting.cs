using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    public sealed partial class MonsterTacticsCombatDirector
    {
        private static MonsterTacticsU6112AssassinTargetingProfile u6112CachedProfile;
        private static MonsterTacticsU6112AssassinTargetingProfile U6112Profile
        {
            get
            {
                if (u6112CachedProfile == null)
                    u6112CachedProfile = Resources.Load<MonsterTacticsU6112AssassinTargetingProfile>(
                        "MonsterTacticsU6112AssassinTargetingProfile");
                return u6112CachedProfile;
            }
        }

        private static bool U6112EnabledFor(Fighter actor)
        {
            if (!IsAssassin(actor)) return false;
            var profile = U6112Profile;
            return profile != null && profile.Enabled;
        }

        private static float U6112BlockedDelay
        {
            get
            {
                var profile = U6112Profile;
                return profile != null ? Mathf.Clamp(profile.BlockedRetargetSeconds, .4f, 3f) : 1.1f;
            }
        }

        /// <summary>
        /// Excludes dead and allied units, prefers currently reachable enemies,
        /// then lowest current HP percentage within +1 hex of the nearest.
        /// On re-acquire only (death or true prolonged path blockage); never
        /// continuously pulls an Assassin off its current target.
        /// </summary>
        private Fighter U6112PickNearestWeakest(Fighter self, Fighter exclude, bool onlyReachable)
        {
            if (self == null || self.Tile == null) return null;
            List<Fighter> enemies = self.Enemy ? players : opponents;
            var fighters = new List<Fighter>();
            var candidates = new List<MonsterTacticsU6112AssassinTargetingRules.Candidate>();
            HashSet<Vector2Int> occupied = OccupiedExcept(self);
            Vector2Int position = MonsterTacticsCombatRules.Coord(self.Tile);
            foreach (Fighter enemy in enemies)
            {
                if (!IsValidTarget(self, enemy) || enemy == exclude) continue;
                int distance = MonsterTacticsCombatRules.HexDistance(
                    position, MonsterTacticsCombatRules.Coord(enemy.Tile));
                bool reachable = distance <= self.Stats.Range ||
                    MonsterTacticsCombatRules.NextStep(self.Tile, enemy.Tile,
                        self.Stats.Range, board, occupied, out _);
                if (onlyReachable && !reachable) continue;
                fighters.Add(enemy);
                candidates.Add(new MonsterTacticsU6112AssassinTargetingRules.Candidate(
                    distance, enemy.HP, enemy.MaxHP, reachable));
            }
            var profile = U6112Profile;
            int slack = profile != null ? Mathf.Clamp(profile.ExtraSearchHexes, 0, 3) : 1;
            int index = MonsterTacticsU6112AssassinTargetingRules.Select(candidates, slack);
            return index >= 0 && index < fighters.Count ? fighters[index] : null;
        }
    }
}
