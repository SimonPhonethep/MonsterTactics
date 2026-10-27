namespace MonsterTactics
{
    /// <summary>
    /// U6.3.2: fixed SOLO PvE bosses. Separate from the shared shop pool.
    /// Stats are first-pass tuning targets, independent of the cost roster.
    /// </summary>
    public static class MonsterTacticsBossEncounters
    {
        public struct Entry
        {
            public readonly int Stage;
            public readonly string Species;
            public readonly MonsterTacticsStageOneEncounters.WildUnit Unit;
            public readonly float VisualScale;

            public Entry(int stage, string species, string type, int hp, float ad, float attackSpeed,
                int armor, int magicResist, int range, int mana, string ability, float visualScale)
            {
                Stage = stage;
                Species = species;
                Unit = new MonsterTacticsStageOneEncounters.WildUnit(species, type, "Boss", hp, ad,
                    attackSpeed, armor, magicResist, range, mana, ability);
                VisualScale = visualScale;
            }
        }

        public static readonly Entry[] All =
        {
            new Entry(2, "Skyclaw", "Rock / Flying", 2000, 48f, .62f, 28, 25, 1, 80, "Rock Slide", 1.48f),
            new Entry(3, "Huggevodrel", "Normal / Fighting", 3400, 70f, .60f, 42, 32, 1, 80, "Bear Hug", 1.52f),
            new Entry(4, "MonolithKing", "Normal", 5300, 88f, .54f, 58, 42, 1, 85, "Titan Slam", 1.67f),
            new Entry(5, "Gloomborne", "Dark", 7200, 103f, .71f, 48, 62, 3, 80, "Dark Pulse", 1.53f),
            new Entry(6, "Starshifter", "Psychic", 9000, 121f, .77f, 56, 62, 3, 80, "Psycho Boost", 1.55f),
            new Entry(7, "Dreadwing", "Dark / Flying", 11500, 142f, .76f, 65, 70, 4, 85, "Oblivion Wing", 1.70f),
            new Entry(8, "VortexPrime", "Psychic", 14500, 162f, .82f, 72, 80, 4, 85, "Psystrike", 1.64f),
        };

        public static bool TryGet(int stage, int round, out Entry entry)
        {
            foreach (Entry candidate in All)
                if (candidate.Stage == stage && round == (stage == 8 ? 1 : 7))
                {
                    entry = candidate;
                    return true;
                }
            entry = default(Entry);
            return false;
        }

        public static string Title(int stage, int round)
        {
            return TryGet(stage, round, out Entry entry) ? "SOLO BOSS: " + entry.Species : "Position your team";
        }

        public static bool SpriteReady(int stage, int round)
        {
            return TryGet(stage, round, out Entry entry) &&
                MonsterTacticsWildSpriteLibrary.GetFrames(entry.Species).Length > 0;
        }
    }
}
