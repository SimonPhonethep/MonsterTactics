using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.10.6 visual-only scaling profile. Values are absolute factors on the
    /// playable sprite's original transform, and additional encounter multipliers
    /// on top of U6.10.5's art catalog. Does not modify unit roots/hitboxes.
    /// </summary>
    public sealed class MonsterTacticsU6106ScaleProfile : ScriptableObject
    {
        [Serializable]
        public sealed class FamilyScaleOverride
        {
            public string Family;
            [Range(.65f, 1.3f)] public float RelativeScale = 1f;
        }

        public bool Enabled = true;
        [Header("Playable Monster — absolute sprite scale, NOT a second multiplier")]
        [Range(.65f, 1.2f)] public float OriginalPlayableScale = .90f;
        [Range(.65f, 1.2f)] public float NewPlayableScale = .90f;

        [Header("PvE — relative to the existing encounter sizes")]
        [Range(.7f, 1.2f)] public float WildScaleMultiplier = .90f;
        [Range(.7f, 1.2f)] public float NightfangScaleMultiplier = 1f;
        [Range(.7f, 1.2f)] public float OtherBossScaleMultiplier = 1f;
        [Range(1f, 1.8f)] public float SkyclawScaleMultiplier = 1.28f;
        [Range(1f, 1.8f)] public float DreadwingScaleMultiplier = 1.38f;

        [Tooltip("Optional extra per-family/per-species factors on top of the defaults; one entry per Monster family or PvE species. Values are relative, e.g. 0.95 means 5% smaller.")]
        public List<FamilyScaleOverride> SpeciesOverrides = new List<FamilyScaleOverride>();

        private static MonsterTacticsU6106ScaleProfile cached;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { cached = null; }

        public static MonsterTacticsU6106ScaleProfile Current
        {
            get
            {
                if (cached == null)
                    cached = Resources.Load<MonsterTacticsU6106ScaleProfile>("MonsterTacticsU6106ScaleProfile");
                return cached;
            }
        }

        public static bool IsActive => Current != null && Current.Enabled;

        private float RelativeFor(string name)
        {
            if (string.IsNullOrEmpty(name) || SpeciesOverrides == null) return 1f;
            for (int i = 0; i < SpeciesOverrides.Count; ++i)
            {
                FamilyScaleOverride entry = SpeciesOverrides[i];
                if (entry != null && string.Equals(entry.Family, name, StringComparison.OrdinalIgnoreCase))
                    return Mathf.Clamp(entry.RelativeScale, .65f, 1.3f);
            }
            return 1f;
        }

        public static float PlayableScale(string baseFamily, bool isNewFamily, MonsterTacticsU6104ArtSettings oldSettings)
        {
            var profile = Current;
            if (profile == null || !profile.Enabled)
            {
                return oldSettings == null ? 1f : isNewFamily ?
                    oldSettings.NewPlayableScale : oldSettings.OriginalPlayableScale;
            }
            float baseScale = isNewFamily ? profile.NewPlayableScale : profile.OriginalPlayableScale;
            return Mathf.Clamp(baseScale * profile.RelativeFor(baseFamily), .65f, 1.2f);
        }

        public static float EncounterMultiplier(string species, bool boss)
        {
            var profile = Current;
            if (profile == null || !profile.Enabled) return 1f;
            float factor;
            if (string.Equals(species, "Nightfang", StringComparison.OrdinalIgnoreCase))
                factor = profile.NightfangScaleMultiplier;
            else if (!boss)
                factor = profile.WildScaleMultiplier;
            else if (string.Equals(species, "Skyclaw", StringComparison.OrdinalIgnoreCase))
                factor = profile.OtherBossScaleMultiplier * profile.SkyclawScaleMultiplier;
            else if (string.Equals(species, "Dreadwing", StringComparison.OrdinalIgnoreCase))
                factor = profile.OtherBossScaleMultiplier * profile.DreadwingScaleMultiplier;
            else
                factor = profile.OtherBossScaleMultiplier;
            return Mathf.Clamp(factor * profile.RelativeFor(species), .65f, 2.1f);
        }
    }
}
