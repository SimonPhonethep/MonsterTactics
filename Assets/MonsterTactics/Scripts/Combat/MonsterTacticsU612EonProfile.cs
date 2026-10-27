using UnityEngine;

namespace MonsterTactics
{
    [CreateAssetMenu(menuName="MonsterTactics/Combat/U6.11.4 Eon Duo Profile",
        fileName="MonsterTacticsU612EonProfile")]
    public sealed class MonsterTacticsU612EonProfile : ScriptableObject
    {
        public bool Enabled = true;
        [Range(0f, .5f)] public float BondDamageAmplification = .10f;
        [Range(0, 60)] public int PartnerManaPerCast = 20;
        [Range(0f, 1f)] public float SkyshardAttackSpeed1 = .30f;
        [Range(0f, 1f)] public float SkyshardAttackSpeed2 = .45f;
        [Range(0f, 1f)] public float SkyshardAttackSpeed3 = .70f;
        [Range(.2f, 15f)] public float SkyshardBuffSeconds = 5f;
        [Range(1, 8)] public int SkyshardAttacksPerBeam = 3;
        [Range(1, 10)] public int BeamMaximumRangeHexes = 5;
        [Range(.5f, 6f)] public float SkyweaveShieldSeconds = 3f;
        public static MonsterTacticsU612EonProfile Current =>
            Resources.Load<MonsterTacticsU612EonProfile>("MonsterTacticsU612EonProfile");
    }
}
