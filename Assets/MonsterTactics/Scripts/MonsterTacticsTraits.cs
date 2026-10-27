using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>U6.6: data shared by trait panel and battle bonuses. Combat roles are not traits.
    /// Only distinct, deployed Monster families contribute. Evolution updates elemental typing.</summary>
    public static class MonsterTacticsTraits
    {
        public sealed class Definition
        {
            public string Name;
            public int[] Breakpoints;
            public string[] Descriptions;
            public bool Origin;
            public Definition(string name, bool origin, int[] breakpoints, params string[] descriptions)
            { Name = name; Origin = origin; Breakpoints = breakpoints; Descriptions = descriptions; }
        }

        public static readonly Definition[] LegacyAll =
        {
            new Definition("Fire", false, new[]{2,3}, "+12% AD and +12 AP", "+22% AD and +22 AP"),
            new Definition("Water", false, new[]{2,3}, "+15% mana gained", "+25% mana gained"),
            new Definition("Grass", false, new[]{2}, "+12% maximum HP"),
            new Definition("Dragon", false, new[]{2}, "+15% damage dealt"),
            new Definition("Ground", false, new[]{2}, "+15 Armor and Magic Resist"),
            new Definition("First Partners", true, new[]{2,4,6}, "+10% Attack Speed", "+20% Attack Speed", "+35% Attack Speed"),
            new Definition("Playful", true, new[]{2}, "+15% Attack Speed, +10 percentage points Crit Chance"),
            new Definition("Legendary Beasts", true, new[]{2,3}, "+10% damage dealt and +10% maximum HP", "Retain previous bonuses; also reduce incoming damage by 20%"),
            // Show types actually deployed even if there are currently too few
            // shop families to activate a bonus. Empty breakpoints = no effect.
            new Definition("Normal", false, new int[0]),
            new Definition("Fighting", false, new int[0]),
            new Definition("Psychic", false, new int[0]),
            new Definition("Poison", false, new int[0]),
            new Definition("Ghost", false, new int[0]),
            new Definition("Dark", false, new int[0]),
            new Definition("Fairy", false, new int[0]),
            new Definition("Flying", false, new int[0]),
            new Definition("Rock", false, new int[0]),
            new Definition("Electric", false, new int[0]),
            new Definition("Bug", false, new int[0]),
            new Definition("Ice", false, new int[0]),
            new Definition("Steel", false, new int[0])
        };

        /// <summary>Live trait definitions or unchanged U6.6 definitions (safe fallback).</summary>
        public static Definition[] All => MonsterTacticsContentTraitRuntime.ActiveDefinitions;

        public struct Bonus
        {
            public float AttackMultiplier;
            public float AttackSpeedMultiplier;
            public float AbilityPowerBonus;
            public float HpMultiplier;
            public float DamageMultiplier;
            public float IncomingDamageMultiplier;
            public float ManaGainMultiplier;
            public float CritChanceBonus;
            public int ArmorBonus;
            public int MagicResistBonus;

            public static Bonus Empty => new Bonus { AttackMultiplier = 1f, AttackSpeedMultiplier = 1f,
                HpMultiplier = 1f, DamageMultiplier = 1f, IncomingDamageMultiplier = 1f,
                ManaGainMultiplier = 1f };
        }

        public static string[] ForUnit(MonsterUnit unit)
        {
            if (unit == null) return new string[0];
            var list = new List<string>();
            var family = MonsterTacticsU5Roster.Find(unit.MonsterName);
            if (family == null) return list.ToArray(); // Non-shop PvE units have no shop traits.
            int star = unit.GetComponent<MonsterTacticsU5Progression>()?.Stage ?? MonsterTacticsU5Roster.StageOf(unit.MonsterName);
            string types = family.Type(star);
            foreach (string type in types.Split('/'))
            {
                string value = type.Trim();
                if (value.Length > 0 && !list.Contains(value) &&
                    MonsterTacticsContentTraitRuntime.IsEnabledTrait(value)) list.Add(value);
            }
            string baseName = family.Forms[0];
            string[] catalogOrigins;
            if (MonsterTacticsContentTraitRuntime.TryOrigins(baseName, out catalogOrigins))
            {
                // The catalog is authoritative here: removing an origin in the
                // editor must remove it from the next match, not fall back to legacy.
                foreach (string origin in catalogOrigins)
                    if (!list.Contains(origin) && MonsterTacticsContentTraitRuntime.IsEnabledTrait(origin))
                        list.Add(origin);
            }
            else if (!MonsterTacticsContentTraitRuntime.IsUsingCatalog)
            {
                if (baseName == "Budhorn" || baseName == "Emberlet" || baseName == "Ripplet" ||
                    baseName == "Mosslet" || baseName == "Flarbit" || baseName == "Mistrip") list.Add("First Partners");
                if (baseName == "Flarbit" || baseName == "Dunebit" || baseName == "Tricklet") list.Add("Playful");
                if (baseName == "Voltrix" || baseName == "Pyrothane" || baseName == "Aqualith") list.Add("Legendary Beasts");
            }
            MonsterTacticsHeldItems held = unit.GetComponent<MonsterTacticsHeldItems>();
            if (held != null)
            {
                foreach (MonsterTacticsItemKind item in held.Items)
                {
                    string emblem = MonsterTacticsItemTypes.EmblemTrait(item);
                    if (!string.IsNullOrEmpty(emblem) && !list.Contains(emblem) &&
                        MonsterTacticsContentTraitRuntime.IsEnabledTrait(emblem)) list.Add(emblem);
                }
            }
            return list.ToArray();
        }

        public static Dictionary<string, int> CountDeployed(IEnumerable<MonsterUnit> units)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (units == null) return counts;
            foreach (MonsterUnit unit in units)
            {
                if (unit == null || unit.OccupiedTile == null || !unit.gameObject.activeInHierarchy) continue;
                string familyName = MonsterTacticsU5Roster.BaseName(unit.MonsterName);
                if (!seen.Add(familyName)) continue;
                foreach (string trait in ForUnit(unit))
                {
                    counts.TryGetValue(trait, out int current);
                    counts[trait] = current + 1;
                }
            }
            return counts;
        }

        public static int ActiveTier(string name, Dictionary<string, int> counts)
        {
            if (MonsterTacticsContentTraitRuntime.IsUsingCatalog)
                return MonsterTacticsContentTraitRuntime.ActiveTier(name, counts);
            if (counts == null || !counts.TryGetValue(name, out int count)) return 0;
            foreach (Definition def in All)
            {
                if (def.Name != name) continue;
                int level = 0;
                for (int i = 0; i < def.Breakpoints.Length; i++)
                    if (count >= def.Breakpoints[i]) level = i + 1;
                return level;
            }
            return 0;
        }

        /// <summary>
        /// One shared stat modifier path for live planning inspection and combat.
        /// Never writes into the roster or the MonsterUnit's base stats, so repeated
        /// deployment, benching, and inspection cannot stack the bonus.
        /// </summary>
        public static MonsterTacticsCombatRules.Stats ApplyToStats(
            MonsterTacticsCombatRules.Stats stats, Bonus bonus)
        {
            stats.Attack *= bonus.AttackMultiplier;
            stats.AttackSpeed *= bonus.AttackSpeedMultiplier;
            stats.AbilityPower += bonus.AbilityPowerBonus;
            stats.Armor += bonus.ArmorBonus;
            stats.MagicResist += bonus.MagicResistBonus;
            stats.CriticalChance = Mathf.Clamp01(stats.CriticalChance + bonus.CritChanceBonus);
            stats.DamageMultiplier *= bonus.DamageMultiplier;
            stats.IncomingDamageMultiplier *= bonus.IncomingDamageMultiplier;
            stats.ManaGainMultiplier *= bonus.ManaGainMultiplier;
            return stats;
        }

        public static Bonus ForUnit(MonsterUnit unit, Dictionary<string, int> counts)
        {
            Bonus b = Bonus.Empty;
            if (unit == null || counts == null) return b;
            string[] names = ForUnit(unit);
            if (MonsterTacticsContentTraitRuntime.IsUsingCatalog)
                return MonsterTacticsContentTraitRuntime.BonusForUnit(names, counts);
            foreach (string name in names)
            {
                int tier = ActiveTier(name, counts);
                if (tier == 0) continue;
                switch (name)
                {
                    case "Fire":
                        b.AttackMultiplier *= tier >= 2 ? 1.22f : 1.12f;
                        b.AbilityPowerBonus += tier >= 2 ? 22f : 12f;
                        break;
                    case "Water": b.ManaGainMultiplier *= tier >= 2 ? 1.25f : 1.15f; break;
                    case "Grass": b.HpMultiplier *= 1.12f; break;
                    case "Dragon": b.DamageMultiplier *= 1.15f; break;
                    case "Ground": b.ArmorBonus += 15; b.MagicResistBonus += 15; break;
                    case "First Partners":
                        b.AttackSpeedMultiplier *= tier >= 3 ? 1.35f : tier == 2 ? 1.20f : 1.10f;
                        break;
                    case "Playful": b.AttackSpeedMultiplier *= 1.15f; b.CritChanceBonus += .10f; break;
                    case "Legendary Beasts":
                        b.HpMultiplier *= 1.10f;
                        b.DamageMultiplier *= 1.10f;
                        if (tier >= 2) b.IncomingDamageMultiplier *= .80f;
                        break;
                }
            }
            return b;
        }
    }
}
