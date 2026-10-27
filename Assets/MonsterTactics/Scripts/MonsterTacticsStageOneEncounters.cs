using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.3.1 fixed opening encounters. Wild creatures are NOT shop families, so
    /// they never reserve copies from the player pool or appear on the bench.
    /// These stats are tuned for one / two / three unevolved cost-1 allies.
    /// </summary>
    public static class MonsterTacticsStageOneEncounters
    {
        public struct WildUnit
        {
            public readonly string Name;
            public readonly string Type;
            public readonly string Role;
            public readonly int HP;
            public readonly float Attack;
            public readonly float AttackSpeed;
            public readonly int Armor;
            public readonly int MagicResist;
            public readonly int Range;
            public readonly int ManaCost;
            public readonly string Ability;

            public WildUnit(string name, string type, string role, int hp, float attack,
                float attackSpeed, int armor, int magicResist, int range, int manaCost, string ability)
            {
                Name = name; Type = type; Role = role; HP = hp; Attack = attack;
                AttackSpeed = attackSpeed; Armor = armor; MagicResist = magicResist;
                Range = range; ManaCost = manaCost; Ability = ability;
            }

            public MonsterTacticsCombatRules.Stats Stats()
            {
                var stats = new MonsterTacticsCombatRules.Stats(Attack, AttackSpeed, Armor,
                    MagicResist, Range, ManaCost, .60f, Ability);
                stats.CriticalChance = 0f; // Opening wild rounds should be predictable.
                return stats;
            }
        }

        private static readonly WildUnit Bristlet = new WildUnit(
            "Bristlet", "Bug / Poison", "Fighter", 110, 12f, .53f, 8, 10, 1, 80, "Poison Sting");
        private static readonly WildUnit Winglit = new WildUnit(
            "Winglit", "Normal / Flying", "Ranged", 140, 14f, .55f, 8, 8, 3, 80, "Gust");
        private static readonly WildUnit Skitter = new WildUnit(
            "Skitter", "Normal", "Fighter", 150, 18f, .68f, 12, 8, 1, 80, "Quick Attack");
        private static readonly WildUnit Crawlet = new WildUnit(
            "Crawlet", "Bug", "Tank", 170, 10f, .48f, 20, 15, 1, 80, "String Shot");
        private static readonly WildUnit Ashhound = new WildUnit(
            "Ashhound", "Dark / Fire", "Mage", 260, 27f, .62f, 18, 16, 2, 85, "Ember");
        private static readonly WildUnit Nightfang = new WildUnit(
            "Nightfang", "Dark / Fire", "Boss", 900, 38f, .55f, 24, 24, 2, 70, "Flamethrower");

        public static WildUnit[] ForRound(int round)
        {
            switch (round)
            {
                case 1: return new [] { Bristlet, Winglit };
                case 2: return new [] { Winglit, Skitter, Crawlet };
                case 3: return new [] { Winglit, Skitter, Crawlet, Ashhound };
                case 4: return new [] { Nightfang };
                default: return new WildUnit[0];
            }
        }

        public static string Title(int round)
        {
            switch (round)
            {
                case 1: return "Wild: Bristlet + Winglit";
                case 2: return "Wild: Winglit, Skitter, Crawlet";
                case 3: return "Wild: Ashhound's pack";
                case 4: return "BOSS: Nightfang";
                default: return "Position your team";
            }
        }
    }
}
