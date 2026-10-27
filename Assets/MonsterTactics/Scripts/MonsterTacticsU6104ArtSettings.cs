using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// Opt-in U6.10.4 art upgrade. No saved asset means the legacy player art stays
    /// exactly as it was before the installer was run.
    /// </summary>
    public sealed class MonsterTacticsU6104ArtSettings : ScriptableObject
    {
        public bool UnifiedLegacyArt = true;
        [Range(0.65f, 1.2f)] public float OriginalPlayableScale = 1f;
        [Range(0.65f, 1.2f)] public float NewPlayableScale = 0.90f;

        private static MonsterTacticsU6104ArtSettings cached;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearCache() { cached = null; }

        public static MonsterTacticsU6104ArtSettings Current
        {
            get
            {
                if (cached == null) cached = Resources.Load<MonsterTacticsU6104ArtSettings>("MonsterTacticsU6104ArtSettings");
                return cached;
            }
        }
    }
}
