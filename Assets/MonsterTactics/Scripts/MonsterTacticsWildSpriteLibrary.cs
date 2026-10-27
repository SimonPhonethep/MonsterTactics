using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>Real animated sprite frames imported from MonsterAPI. No generated placeholders.</summary>
    public static class MonsterTacticsWildSpriteLibrary
    {
        public static readonly string[] Species =
            { "Bristlet", "Winglit", "Skitter", "Crawlet", "Ashhound", "Nightfang" };
        private static readonly Dictionary<string, Sprite[]> Cache =
            new Dictionary<string, Sprite[]>(StringComparer.OrdinalIgnoreCase);

        public static Sprite[] GetFrames(string monsterName)
        {
            if (string.IsNullOrEmpty(monsterName)) return new Sprite[0];
            string key = monsterName.Trim().ToLowerInvariant();
            // U6.10.5 overrides legacy PvE visuals only after an opt-in catalog is installed.
            // This lookup comes before the legacy cache to support switching back safely.
            if (MonsterTacticsU6105EncounterArtCatalog.TryGet(key, out Sprite[] unified, out _))
                return unified;
            if (Cache.TryGetValue(key, out Sprite[] cached)) return cached;
            Sprite[] frames = Resources.LoadAll<Sprite>("WildSprites/" + key);
            Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (frames.Length == 0)
            {
                // A real single-frame sprite is acceptable when the GIF source is unavailable.
                Sprite staticArt = Resources.Load<Sprite>("WildSprites/" + key);
                if (staticArt != null) frames = new[] { staticArt };
            }
            if (frames.Length != 0) Cache[key] = frames;
            return frames;
        }

        public static float[] GetTimings(string monsterName)
        {
            if (string.IsNullOrEmpty(monsterName)) return new float[0];
            if (MonsterTacticsU6105EncounterArtCatalog.TryGet(monsterName, out _, out float[] unifiedDurations))
                return unifiedDurations;
            TextAsset text = Resources.Load<TextAsset>("WildSprites/" + monsterName.ToLowerInvariant() + "/timings");
            if (text == null) return new float[0];
            string[] lines = text.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            float[] durations = new float[lines.Length];
            for (int i = 0; i < lines.Length; i++)
                if (!float.TryParse(lines[i], NumberStyles.Float, CultureInfo.InvariantCulture, out durations[i]))
                    durations[i] = .12f;
            return durations;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { Cache.Clear(); }

        public static bool StageOneArtReady
        {
            get
            {
                foreach (string name in Species)
                    if (GetFrames(name).Length == 0) return false;
                return true;
            }
        }
    }
}
