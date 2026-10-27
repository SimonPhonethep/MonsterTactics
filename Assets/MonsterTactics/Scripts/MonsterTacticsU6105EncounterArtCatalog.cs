using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// Opt-in U6.10.5 PvE art library. Retains the old WildSprites resources in place,
    /// and falls back to them when this library is absent or disabled.
    /// Imported with the same MonsterAPI source and frame pipeline as U6.10.3/4.
    /// No changes to PvE roster, rounds, boss health, combat abilities, or hitboxes.
    /// </summary>
    public sealed class MonsterTacticsU6105EncounterArtCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Species;
            public Sprite StaticSprite;
            public Sprite[] AnimationFrames = new Sprite[0];
            public float[] FrameDurations = new float[0];
        }

        public bool UseUnifiedArt = true;
        [Range(0.7f, 1.4f)] public float WildScaleMultiplier = 1f;
        [Range(0.7f, 1.4f)] public float StageOneBossScaleMultiplier = 1f;
        [Range(0.7f, 1.4f)] public float SoloBossScaleMultiplier = 1f;
        public List<Entry> Entries = new List<Entry>();

        private static MonsterTacticsU6105EncounterArtCatalog cached;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { cached = null; }

        public static MonsterTacticsU6105EncounterArtCatalog Current
        {
            get
            {
                if (cached == null) cached = Resources.Load<MonsterTacticsU6105EncounterArtCatalog>("MonsterTacticsU6105EncounterArtCatalog");
                return cached;
            }
        }

        public static bool TryGet(string species, out Sprite[] frames, out float[] times)
        {
            frames = null;
            times = null;
            var library = Current;
            if (library == null || !library.UseUnifiedArt || library.Entries == null || string.IsNullOrEmpty(species))
                return false;
            foreach (Entry item in library.Entries)
            {
                if (item == null || !string.Equals(item.Species, species, StringComparison.OrdinalIgnoreCase)) continue;
                if (item.AnimationFrames != null && item.AnimationFrames.Length >= 2 &&
                    Array.TrueForAll(item.AnimationFrames, frame => frame != null) &&
                    item.FrameDurations != null && item.FrameDurations.Length == item.AnimationFrames.Length)
                {
                    frames = item.AnimationFrames;
                    times = item.FrameDurations;
                    return true;
                }
                if (item.StaticSprite == null) return false;
                frames = new[] { item.StaticSprite };
                times = new float[0];
                return true;
            }
            return false;
        }

        public static float ScaleFor(string species, bool boss)
        {
            var library = Current;
            if (library == null || !library.UseUnifiedArt || !TryGet(species, out _, out _)) return 1f;
            float value = boss ? library.SoloBossScaleMultiplier :
                string.Equals(species, "Nightfang", StringComparison.OrdinalIgnoreCase) ?
                library.StageOneBossScaleMultiplier : library.WildScaleMultiplier;
            return Mathf.Clamp(value, .7f, 1.4f);
        }

        public static bool IsUnified(string species) => TryGet(species, out _, out _);
    }
}
