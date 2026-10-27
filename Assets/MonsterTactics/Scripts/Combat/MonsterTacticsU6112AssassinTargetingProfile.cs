using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.11.2 opt-in Assassin AI. Stored in Resources so existing scenes do not
    /// require a director component or serialized scene/prefab modifications.
    /// Only affects Monster whose combat role is Assassin.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterTacticsU6112AssassinTargetingProfile",
        menuName = "MonsterTactics/U6.11.2 Assassin Targeting Profile")]
    public sealed class MonsterTacticsU6112AssassinTargetingProfile : ScriptableObject
    {
        [Tooltip("When false, keep U6.11.1 behavior (backline priority for legacy Assassins; closest for Stormlynx).")]
        public bool Enabled = true;

        [Range(0, 3)]
        [Tooltip("Allow candidates this many hexes beyond the nearest reachable enemy. 1 is recommended.")]
        public int ExtraSearchHexes = 1;

        [Range(0.4f, 3f)]
        [Tooltip("Switch targets only after a locked enemy cannot be reached for this many combat seconds.")]
        public float BlockedRetargetSeconds = 1.1f;
    }
}
