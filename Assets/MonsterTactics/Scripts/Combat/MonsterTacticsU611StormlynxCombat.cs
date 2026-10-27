using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>U6.11 standalone Stormlynx gameplay; no other unit or trait is changed.</summary>
    public sealed partial class MonsterTacticsCombatDirector
    {
        // Unique to Stormlynx: other Assassins keep their normal backline priority.
        private static bool U611IsStormlynx(Fighter fighter)
        {
            return fighter != null && fighter.Unit != null &&
                MonsterTacticsU5Roster.BaseName(fighter.Name) == "Stormlynx";
        }

        /// <summary>
        /// Every Plasma Fists cast creates a visible short dash even when the
        /// target is already adjacent and there is no vacant destination hex.
        /// We animate the unit's root, then return to its occupied tile.
        /// No board reservation, target re-selection, or collision is changed.
        /// </summary>
        private void U611StartStormlynxLunge(Fighter source, Fighter target)
        {
            if (!U611IsStormlynx(source) || !IsValidTarget(source, target)) return;
            Vector3 from = source.Unit.transform.position;
            Vector3 to = target.Unit.transform.position;
            Vector3 planar = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float distance = planar.magnitude;
            if (distance < 0.01f) return;
            source.U611LungeFrom = from;
            // Travel visibly toward the enemy without stepping onto the enemy tile.
            source.U611LungeToward = from + planar.normalized * Mathf.Min(distance * .60f, .82f);
            source.U611LungeStart = elapsed;
            source.U611LungeEnd = elapsed + .36f;
            source.ActionReady = Mathf.Max(source.ActionReady, source.U611LungeEnd + .10f);
        }

        private void U611UpdateStormlynxLunges()
        {
            foreach (Fighter fighter in turnList)
            {
                if (!U611IsStormlynx(fighter) || fighter.U611LungeEnd <= 0f) continue;
                if (!fighter.Alive)
                {
                    fighter.U611LungeEnd = 0f;
                    continue;
                }
                float t = Mathf.Clamp01((elapsed - fighter.U611LungeStart) /
                    Mathf.Max(.001f, fighter.U611LungeEnd - fighter.U611LungeStart));
                // Quick outward sprint, momentary strike, smooth return.
                float outward = t < .60f ? Mathf.SmoothStep(0f, 1f, t / .60f) :
                    1f - Mathf.SmoothStep(0f, 1f, (t - .60f) / .40f);
                fighter.Unit.transform.position = Vector3.LerpUnclamped(
                    fighter.U611LungeFrom, fighter.U611LungeToward, outward) +
                    Vector3.up * (.10f * Mathf.Sin(t * Mathf.PI));
                if (t >= 1f)
                {
                    // Recover the authoritative hex's world-space transform.
                    fighter.Unit.transform.position = fighter.U611LungeFrom;
                    fighter.U611LungeEnd = 0f;
                }
            }
        }

        private void U611ResolveStormlynxLightning(Fighter source, Fighter primary)
        {
            if (source == null || source.Unit == null || primary == null ||
                primary.Tile == null) return;

            // Capture the original hit hex, including if Plasma Fists just
            // fainted the target. The primary target never takes chain damage.
            Vector2Int impact = MonsterTacticsCombatRules.Coord(primary.Tile);
            List<Fighter> enemies = source.Enemy ? players : opponents;
            var elidunebit = new List<Fighter>();
            var positions = new List<Vector2Int>();
            foreach (Fighter f in enemies)
            {
                if (f == null || f == primary || !f.Alive || f.Tile == null) continue;
                elidunebit.Add(f);
                positions.Add(MonsterTacticsCombatRules.Coord(f.Tile));
            }
            int[] selected = MonsterTacticsU611StormlynxRules.SelectTargets(impact, positions);
            if (selected.Length == 0) return; // one enemy left: full physical hit only.
            int star = source.Unit.GetComponent<MonsterTacticsU5Progression>()?.Stage ??
                MonsterTacticsU5Roster.StageOf(source.Name);
            float baseMagic = star >= 3 ? 450f : star == 2 ? 225f : 125f;
            float rawMagic = baseMagic + source.Stats.AbilityPower * .65f;
            foreach (int index in selected)
            {
                Fighter secondary = elidunebit[index];
                if (secondary == null || !secondary.Alive) continue;
                // Pure secondary spell damage; no second dash or bounce-back.
                DealDamage(secondary, rawMagic, true, source, "PLASMA CHAIN");
                if (abilityVfx != null && primary.Unit != null && secondary.Unit != null)
                    abilityVfx.PlayU611StormlynxChain(primary.Unit, secondary.Unit);
            }
        }
    }
}
