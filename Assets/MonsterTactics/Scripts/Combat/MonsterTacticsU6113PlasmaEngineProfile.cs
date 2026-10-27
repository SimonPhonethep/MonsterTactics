using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.11.3 Stormlynx-only unique trait controls.
    /// Tuned in Resources, independent from Plasma Fists damage and chain targeting.
    /// Changes take effect in the next Play Mode battle.
    /// </summary>
    [CreateAssetMenu(menuName = "MonsterTactics/Combat/U6.11.3 Plasma Engine Profile",
        fileName = "MonsterTacticsU6113PlasmaEngineProfile")]
    public sealed class MonsterTacticsU6113PlasmaEngineProfile : ScriptableObject
    {
        public const string ResourceName = "MonsterTacticsU6113PlasmaEngineProfile";
        public bool Enabled = true;
        [Range(1, 8)] public int AttacksPerManaProc = 3;
        [Range(0, 30)] public int BonusManaPerProc = 10;
        [Range(0f, 1f)] public float AttackSpeedBonus = .25f;
        [Range(.1f, 12f)] public float AttackSpeedDuration = 4f;

        public static MonsterTacticsU6113PlasmaEngineProfile LoadProfile()
        {
            return Resources.Load<MonsterTacticsU6113PlasmaEngineProfile>(ResourceName);
        }
    }
}
