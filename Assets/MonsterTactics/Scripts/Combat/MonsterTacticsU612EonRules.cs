using UnityEngine;
namespace MonsterTactics
{
    /// <summary>Pure hex geometry for single-direction, no-bounce psychic beams.</summary>
    public static class MonsterTacticsU612EonRules
    {
        private static Vector3 Cube(Vector2Int pos)
        {
            int q = pos.x - (pos.y - (pos.y & 1)) / 2;
            int r = pos.y;
            return new Vector3(q, -q-r, r);
        }
        // Allows a narrow hex-width corridor around the caster -> chosen target ray.
        // Distinct targets are selected by the calling combat handler.
        public static bool IsOnPiercingRay(Vector2Int from, Vector2Int toward,
            Vector2Int candidate, int maxRange)
        {
            if (maxRange < 1 || candidate == from) return false;
            Vector3 a = Cube(from), b = Cube(toward)-a, c = Cube(candidate)-a;
            float len2 = Vector3.Dot(b,b);
            if (len2 < .01f) return false;
            float progress = Vector3.Dot(c,b)/len2;
            if (progress < .35f) return false; // Never hit behind the caster.
            if (MonsterTacticsCombatRules.HexDistance(from,candidate)>maxRange) return false;
            Vector3 deviation = c - b*progress;
            // Exact ray or within a narrow half-hex corridor.
            return deviation.sqrMagnitude <= .40f;
        }
    }
}
