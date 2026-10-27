using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U4 combat balance defaults. Kept independent of scene and trainer code,
    /// so later we can replace them with the full MonsterTactics roster database.
    /// </summary>
    public static class MonsterTacticsCombatRules
    {
        public struct Stats
        {
            public float Attack;
            public float AttackSpeed;
            public float AbilityPower;
            // Heal for a percentage of actual post-mitigation HP damage dealt.
            // Stored as a fraction: .10f = 10%. Future items, traits, and augments can add bonuses.
            public float Omnivamp;
            public float DamageMultiplier;
            public float IncomingDamageMultiplier;
            public float ManaGainMultiplier;
            public float CriticalChance;
            public float CriticalMultiplier;
            public int Armor;
            public int MagicResist;
            public int Range;
            public int ManaCost;
            public float MoveInterval;
            public string Ability;

            public Stats(float attack, float attackSpeed, int armor, int magicResist,
                int range, int manaCost, float moveInterval, string ability)
            {
                Attack = attack;
                AttackSpeed = attackSpeed;
                AbilityPower = 100f; // U4 default AP; later items/traits can modify this.
                Omnivamp = 0f; // Default for every shop unit, wild Monster, and boss.
                DamageMultiplier = 1f;
                IncomingDamageMultiplier = 1f;
                ManaGainMultiplier = 1f;
                CriticalChance = .10f;
                CriticalMultiplier = 1.5f;
                Armor = armor;
                MagicResist = magicResist;
                Range = range;
                ManaCost = manaCost;
                MoveInterval = moveInterval;
                Ability = ability;
            }
        }

        /// <summary>
        /// Magma Core (1): spend a fixed fraction of positive BONUSES above
        /// baseline defenses, never touching base Armor/MR. AP is equal to the
        /// number of points spent, capped globally across both defenses.
        /// Exact integer allocation avoids hidden rounding-created AP.
        /// Pure math method, safe to test without entering Play Mode.
        /// </summary>
        public static void ComputeMagmaCoreConversion(int armor, int magicResist,
            int baseArmor, int baseMR, float fraction, float maximumAP,
            out int armorSpent, out int mrSpent, out float apGained)
        {
            int bonusArmor = Mathf.Max(0, armor - Mathf.Max(0, baseArmor));
            int bonusMR = Mathf.Max(0, magicResist - Mathf.Max(0, baseMR));
            float ratio = Mathf.Clamp01(fraction);
            int wantedArmor = Mathf.FloorToInt(bonusArmor * ratio + .00001f);
            int wantedMR = Mathf.FloorToInt(bonusMR * ratio + .00001f);
            int cap = Mathf.Max(0, Mathf.FloorToInt(maximumAP));
            int wanted = wantedArmor + wantedMR;
            if (wanted <= cap)
            {
                armorSpent = wantedArmor;
                mrSpent = wantedMR;
            }
            else if (wanted == 0 || cap == 0)
            {
                armorSpent = 0;
                mrSpent = 0;
            }
            else
            {
                // Allocate the AP cap proportionally and deterministically.
                armorSpent = Mathf.Min(wantedArmor,
                    Mathf.FloorToInt((float)cap * wantedArmor / wanted));
                mrSpent = Mathf.Min(wantedMR, cap - armorSpent);
                armorSpent += Mathf.Min(wantedArmor - armorSpent,
                    cap - armorSpent - mrSpent);
            }
            apGained = armorSpent + mrSpent;
        }

        // U6.9.9b: Shadow Thief's outgoing damage bonus and reduction. These
        // remain separate from Armor/MR, incoming damage reduction and shields.
        public static float ShadowThiefDamageMultiplier(float baseDamageMultiplier,
            float gainedFraction, float lostFraction)
        {
            return Mathf.Max(0f, baseDamageMultiplier) *
                (1f + Mathf.Clamp(gainedFraction, 0f, .20f)) *
                (1f - Mathf.Clamp(lostFraction, 0f, .20f));
        }

        public static Stats ForMonster(string name)
        {
            switch (name)
            {
                case "Ripplet": return new Stats(55f, .80f, 35, 35, 1, 65, .48f, "Aqua Jet");
                case "Emberlet": return new Stats(48f, .72f, 20, 25, 3, 75, .55f, "Flamethrower");
                case "Budhorn": return new Stats(43f, .70f, 45, 40, 1, 70, .58f, "Leech Seed");
                case "Flarbit": return new Stats(55f, .90f, 22, 25, 3, 70, .46f, "Pyro Ball");
                case "Mistrip": return new Stats(60f, .95f, 23, 30, 2, 65, .40f, "Water Shuriken");
                // U6.3.4 first-pass Pebblin tank tuning. Other original roster defaults are unchanged.
                case "Pebblin": return new Stats(35f, .50f, 55, 40, 1, 70, .62f, "Rock Guard");
                case "Mosslet": return new Stats(70f, .85f, 25, 30, 4, 60, .52f, "Leaf Arrow");
                case "Mysprit": return new Stats(68f, .70f, 18, 35, 3, 75, .54f, "Psybeam");
                case "Tusklet": return new Stats(68f, .75f, 35, 35, 1, 70, .50f, "Dragon Claw");
                case "Dunebit":
                    // Dunebit/Shardfang/Dunebreaker: only Monster family with innate omnivamp.
                    Stats dunebit = new Stats(85f, 1.00f, 30, 30, 1, 75, .43f, "Dragon Rush");
                    dunebit.Omnivamp = .10f;
                    return dunebit;
                case "Voltrix": return new Stats(70f, .85f, 35, 45, 4, 80, .47f, "Thunderbolt");
                case "Pyrothane": return new Stats(92f, .78f, 55, 42, 1, 75, .43f, "Sacred Fire");
                case "Aqualith": return new Stats(65f, .68f, 70, 65, 1, 80, .53f, "Aurora Beam");
                default: return new Stats(45f, .75f, 25, 25, 1, 75, .55f, "Strike");
            }
        }


        /// <summary>Single source of truth for U4 ability scaling and the inspector.</summary>
        public struct AbilityProfile
        {
            public string Name;
            public string Description;
            public string Formula;
            public string DamageKind;
            public float PrimaryRaw;
            public float SplashRaw;
            public int HitCount;
            public float HealRaw;
            public float ShieldRaw;
            public int SplashRadius;
            public int MaximumSecondaryTargets;
            public float BurnDuration;
            public float BurnMaxHpPerSecond;
            public float WoundDuration;

            public AbilityProfile(string name, string description, string formula,
                string damageKind, float primary, float splash = 0f, int hits = 1,
                float heal = 0f, float shield = 0f)
            {
                Name = name;
                Description = description;
                Formula = formula;
                DamageKind = damageKind;
                PrimaryRaw = primary;
                SplashRaw = splash;
                HitCount = hits;
                HealRaw = heal;
                ShieldRaw = shield;
                SplashRadius = splash > 0f ? 1 : 0;
                MaximumSecondaryTargets = name == "Psybeam" ? 2 : int.MaxValue;
                BurnDuration = 0f;
                BurnMaxHpPerSecond = 0f;
                WoundDuration = 0f;
            }
        }

        public static AbilityProfile AbilityFor(string monster, Stats stats)
        {
            // U6.10 custom profiles; the battle director passes actual star level.
            AbilityProfile u610;
            if (MonsterTacticsU610Abilities.TryProfile(MonsterTacticsU5Roster.BaseName(monster), 1, stats, out u610))
                return u610;
            // U6.9.1f.3: opt-in authored profiles use the SAME battle damage,
            // status, healing and shielding code. Non-opted-in profiles stay legacy.
            AbilityProfile authored;
            if (MonsterTacticsContentAbilityRuntime.TryGetProfile(monster, stats, out authored))
                return authored;
            return LegacyAbilityFor(monster, stats);
        }

        /// <summary>
        /// Pure legacy-reference profile for edit-time migration comparisons.
        /// Do not delete: PvE specials, opt-out abilities and regression tests
        /// require the original formula independent of the catalog switch.
        /// </summary>
        public static AbilityProfile LegacyAbilityFor(string monster, Stats stats)
        {
            float ap = stats.AbilityPower / 100f;
            switch (monster)
            {
                case "Bristlet": return new AbilityProfile("Poison Sting", "A light poisonous jab.", "24 magic", "Magic", 24f);
                case "Winglit": return new AbilityProfile("Gust", "A weak gust of wind.", "26 magic", "Magic", 26f);
                case "Skitter": return new AbilityProfile("Quick Attack", "A quick bite.", "35 physical", "Physical", 35f);
                case "Crawlet": return new AbilityProfile("String Shot", "A small sticky strike.", "20 magic", "Magic", 20f);
                case "Ashhound": return new AbilityProfile("Ember", "A small flame.", "48 magic", "Magic", 48f);
                case "Nightfang": return new AbilityProfile("Flamethrower", "Opening boss: controlled flame with light splash.", "70 magic + 25 splash", "Magic", 70f, 25f);
                // U6.3.2 solo PvE bosses; deliberately avoid hard CC and one-shot attacks.
                case "Skyclaw": return new AbilityProfile("Rock Slide", "Falling rocks with light splash.", "95 physical + 35 splash", "Physical", 95f, 35f);
                case "Huggevodrel": return new AbilityProfile("Bear Hug", "A heavy single-target strike.", "150 physical", "Physical", 150f);
                case "MonolithKing": return new AbilityProfile("Titan Slam", "Strong strike with a nearby shockwave.", "205 physical + 65 splash", "Physical", 205f, 65f);
                case "Gloomborne": return new AbilityProfile("Dark Pulse", "A shadow burst with splash damage.", "235 magic + 75 splash", "Magic", 235f, 75f);
                case "Starshifter": return new AbilityProfile("Psycho Boost", "A concentrated psychic burst.", "305 magic + 95 splash", "Magic", 305f, 95f);
                case "Dreadwing": return new AbilityProfile("Oblivion Wing", "Drains power to recover health.", "340 magic + 120 heal", "Magic", 340f, heal: 120f);
                case "VortexPrime": return new AbilityProfile("Psystrike", "A devastating psychic wave.", "415 magic + 145 splash", "Magic", 415f, 145f);
                // U6.3.3: true 5-cost kits. All numbers use the same star-scaled
                // Stats instance as the combat inspector and other shop units.
                case "Voltrix":
                    return new AbilityProfile("Thunderbolt", "Deals magic damage and splashes enemies within 1 hex of the target.",
                        "230% AP magic + 85% AP splash", "Magic", 230f * ap, 85f * ap);
                case "Pyrothane":
                {
                    AbilityProfile sacredFire = new AbilityProfile("Sacred Fire",
                        "Deals physical damage to the target and all enemies within 1 hex. Applies Burn for 5s (1% maximum HP true damage per second, 5 ticks) and Wound (-33% healing) for 5s; effects refresh, never stack.",
                        "45 + 175% ATK physical; 65% ATK splash within 1 hex", "Physical",
                        45f + 1.75f * stats.Attack, .65f * stats.Attack);
                    sacredFire.BurnDuration = 5f;
                    sacredFire.BurnMaxHpPerSecond = .01f;
                    sacredFire.WoundDuration = 5f;
                    return sacredFire;
                }
                case "Aqualith":
                    return new AbilityProfile("Aurora Beam", "Deals magic damage and refreshes its own shield (maximum shield: 35% of maximum HP).",
                        "140% AP magic + (160 + 120% AP) self shield", "Magic",
                        140f * ap, shield: 160f + 1.20f * stats.AbilityPower);
                case "Ripplet":
                    return new AbilityProfile("Aqua Jet", "Dashes adjacent to its target and deals physical damage. Cast range follows normal attack targeting.",
                        "20 + 165% ATK", "Physical", 20f + 1.65f * stats.Attack);
                case "Emberlet":
                    return new AbilityProfile("Flamethrower", "Deals magic damage to its target and splashes enemies within 1 hex. Does not apply Burn.",
                        "200% AP + 65% AP splash", "Magic", 200f * ap, 65f * ap);
                case "Budhorn":
                    return new AbilityProfile("Leech Seed", "Damages an enemy and heals the caster.",
                        "140% AP + 75% AP healing", "Magic", 140f * ap, 0f, 1, 75f * ap);
                case "Flarbit":
                    return new AbilityProfile("Pyro Ball", "Deals magic damage to the target and splashes enemies within 1 hex.",
                        "200% AP + 80% AP splash", "Magic", 200f * ap, 80f * ap);
                case "Mistrip":
                    return new AbilityProfile("Water Shuriken", "Strikes the target three times, each hit 0.13 seconds apart; physical damage per hit.",
                        "3 hits × (22 + 60% ATK)", "Physical", 22f + .60f * stats.Attack, 0f, 3);
                case "Pebblin":
                    return new AbilityProfile("Rock Guard", "Raises a rocky barrier that absorbs incoming damage.",
                        "90 + 70% AP shield", "Shield", 0f, shield: 90f + .70f * stats.AbilityPower);
                case "Mosslet":
                    return new AbilityProfile("Leaf Arrow", "Fires a precise physical leaf arrow at its target.",
                        "25 + 170% ATK", "Physical", 25f + 1.70f * stats.Attack);
                case "Mysprit":
                    return new AbilityProfile("Psybeam", "Deals magic damage to the target and up to two other enemies within 1 hex.",
                        "185% AP to up to 3 foes", "Magic", 185f * ap, 185f * ap);
                case "Tusklet":
                    return new AbilityProfile("Dragon Claw", "Slashes a single enemy with its claws.",
                        "30 + 180% ATK", "Physical", 30f + 1.80f * stats.Attack);
                case "Dunebit":
                    return new AbilityProfile("Dragon Rush",
                        "Dashes beside its selected combat target and deals physical damage. Under legacy Assassin AI, casts may retarget the farthest backline enemy. Passive: 10% Omnivamp from actual HP damage dealt.",
                        "30 + 105% ATK", "Physical", 30f + 1.05f * stats.Attack);
                default:
                    return new AbilityProfile("Strike", "A focused attack.",
                        "100% AP", "Magic", 100f * ap);
            }
        }

        /// <summary>
        /// A self-shield refreshes to the stronger of its current and newly cast values.
        /// It never accumulates infinitely; any active shield is capped at 35% of maximum HP.
        /// Applies to Rock Guard, Aurora Beam and future self-shield abilities.
        /// </summary>
        public static int RefreshSelfShield(int currentShield, int newShield, int maxHP)
        {
            int maxShield = Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, maxHP) * .35f));
            return Mathf.Min(maxShield, Mathf.Max(Mathf.Max(0, currentShield), Mathf.Max(0, newShield)));
        }

        /// <summary>
        /// Healing from real health damage actually dealt (after defense, shields,
        /// and overkill). No vamp from breaking shields or dealing damage to corpses.
        /// A future stat bonus can be applied as: stats.Omnivamp =
        /// Mathf.Clamp01(stats.Omnivamp + bonusFromItems + bonusFromTraits + bonusFromAugments).
        /// </summary>
        public static int OmnivampHealing(int actualHealthDamage, float omnivamp)
        {
            if (actualHealthDamage <= 0 || omnivamp <= 0f) return 0;
            return Mathf.RoundToInt(actualHealthDamage * Mathf.Clamp01(omnivamp));
        }

        // U6.3.8 — shared debuff rules for future items, traits and augments.
        // Sunder = Armor reduction; Shred = Magic Resist reduction.
        // Each has a HARD 30% cap and cannot stack with the same debuff.
        public const float MaxResistanceReduction = .30f;
        public const float WoundHealReduction = .33f;
        public const float DefaultDebuffDuration = 5f;

        // U6.9.9: Ice/attack-speed slow. All values are fractions: .20f = 20%.
        // Unlike Sunder/Shred, the slow changes attack timing, never move speed
        // or ability windup. Repeated slows do not add together.
        public const float MaxAttackSpeedSlow = .50f;
        public const float MinimumAttackSpeed = .10f;

        public static float CappedAttackSpeedSlow(float fraction)
        {
            return Mathf.Clamp(fraction, 0f, MaxAttackSpeedSlow);
        }

        public static float AttackSpeedAfterSlow(float attackSpeed, float fraction)
        {
            return Mathf.Max(MinimumAttackSpeed,
                Mathf.Max(0f, attackSpeed) * (1f - CappedAttackSpeedSlow(fraction)));
        }

        // Preserve the progress of a basic-attack cooldown when a slow starts
        // or expires. Example: halfway through a 1s recovery, a 20% slow
        // increases only the remaining half to 0.625s (not a full new attack).
        public static float RescaledBasicAttackDeadline(float now, float deadline,
            float oldAttackSpeed, float newAttackSpeed)
        {
            if (deadline <= now) return deadline;
            return now + (deadline - now) *
                Mathf.Max(MinimumAttackSpeed, oldAttackSpeed) /
                Mathf.Max(MinimumAttackSpeed, newAttackSpeed);
        }


        // U6.9.9a Iron Fortress: one retaliatory hit per N direct basic attacks.
        // Stored per Fighter, not as an item passive (abilities / bonus item hits
        // never count). "0" after reaching the threshold indicates it fired.
        public static int NextIronFortressCounter(int current, int attacksRequired)
        {
            return (Mathf.Max(0, current) + 1) % Mathf.Max(1, attacksRequired);
        }

        public static bool IronFortressTriggers(int current, int attacksRequired)
        {
            return Mathf.Max(0, current) + 1 >= Mathf.Max(1, attacksRequired);
        }

        public static float IronFortressRawMagicDamage(int effectiveArmor, float armorRatio)
        {
            return Mathf.Max(0, effectiveArmor) * Mathf.Clamp(armorRatio, 0f, 3f);
        }

        public static float CappedResistanceReduction(float fraction)
        {
            return Mathf.Clamp(fraction, 0f, MaxResistanceReduction);
        }

        public static int EffectiveResistance(int original, float reduction)
        {
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, original) *
                (1f - CappedResistanceReduction(reduction))));
        }

        // A Wounded unit receives 67% of attempted healing. Applies to all
        // healing, including Omnivamp and ability healing, and never stacks.
        public static int HealingAfterWound(int requestedHealing, bool wounded)
        {
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, requestedHealing) *
                (wounded ? 1f - WoundHealReduction : 1f)));
        }

        public static float Mitigate(float raw, int resistance)
        {
            // TFT-inspired nonnegative defense scale.
            return Mathf.Max(0f, raw) * 100f / (100f + Mathf.Max(0, resistance));
        }

        /// <summary>Neighbor coordinates for odd-row shifted, pointy-top hexes (U1 layout).</summary>
        public static IEnumerable<Vector2Int> Neighbors(Vector2Int position)
        {
            int x = position.x;
            int y = position.y;
            int side = (y & 1) == 0 ? -1 : 1;
            yield return new Vector2Int(x - 1, y);
            yield return new Vector2Int(x + 1, y);
            yield return new Vector2Int(x, y - 1);
            yield return new Vector2Int(x + side, y - 1);
            yield return new Vector2Int(x, y + 1);
            yield return new Vector2Int(x + side, y + 1);
        }

        public static int HexDistance(Vector2Int a, Vector2Int b)
        {
            // Odd-r coordinates -> cube coordinates.
            int ax = a.x - (a.y - (a.y & 1)) / 2;
            int az = a.y;
            int ay = -ax - az;
            int bx = b.x - (b.y - (b.y & 1)) / 2;
            int bz = b.y;
            int by = -bx - bz;
            return (Mathf.Abs(ax - bx) + Mathf.Abs(ay - by) + Mathf.Abs(az - bz)) / 2;
        }

        public static Vector2Int Coord(HexTile tile)
        {
            return new Vector2Int(tile.Column, tile.Row);
        }

        /// <summary>
        /// If occupied tiles temporarily prevent any complete route into attack
        /// range, move one hex along the shortest walkable path to the nearest
        /// approachable tile. Never steps through occupied or off-board hexes.
        /// This is a fallback only; NextStep is preferred whenever possible.
        /// </summary>
        public static bool NextApproachStep(
            HexTile start, HexTile target,
            Dictionary<Vector2Int, HexTile> board,
            HashSet<Vector2Int> blocked, out HexTile next)
        {
            next = null;
            if (start == null || target == null) return false;
            Vector2Int origin = Coord(start);
            Vector2Int goal = Coord(target);
            var queue = new Queue<Vector2Int>();
            var parents = new Dictionary<Vector2Int, Vector2Int>();
            var depths = new Dictionary<Vector2Int, int>();
            queue.Enqueue(origin);
            parents[origin] = origin;
            depths[origin] = 0;
            int bestDistance = HexDistance(origin, goal);
            int bestDepth = 0; // equal-distance tiles must not cause idle wandering
            Vector2Int best = origin;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int neighbor in Neighbors(current))
                {
                    if (parents.ContainsKey(neighbor) || !board.ContainsKey(neighbor) ||
                        (blocked != null && blocked.Contains(neighbor))) continue;
                    parents.Add(neighbor, current);
                    depths.Add(neighbor, depths[current] + 1);
                    queue.Enqueue(neighbor);
                    int distance = HexDistance(neighbor, goal);
                    if (distance < bestDistance ||
                        (distance == bestDistance && depths[neighbor] < bestDepth))
                    {
                        bestDistance = distance;
                        bestDepth = depths[neighbor];
                        best = neighbor;
                    }
                }
            }
            if (best == origin) return false;
            while (parents[best] != origin) best = parents[best];
            return board.TryGetValue(best, out next);
        }

        /// <summary>
        /// Breadth-first path to the nearest FREE tile from which target is in range.
        /// Returns just the next step, so moving actors replan around each other.
        /// </summary>
        public static bool NextStep(
            HexTile start, HexTile target, int attackRange,
            Dictionary<Vector2Int, HexTile> board,
            HashSet<Vector2Int> blocked, out HexTile next)
        {
            next = null;
            if (start == null || target == null) return false;
            Vector2Int origin = Coord(start);
            Vector2Int goal = Coord(target);
            if (HexDistance(origin, goal) <= attackRange) return false;

            var queue = new Queue<Vector2Int>();
            var parents = new Dictionary<Vector2Int, Vector2Int>();
            queue.Enqueue(origin);
            parents[origin] = origin;
            Vector2Int found = origin;
            bool reached = false;

            while (queue.Count != 0)
            {
                Vector2Int current = queue.Dequeue();
                if (current != origin && HexDistance(current, goal) <= attackRange)
                {
                    found = current;
                    reached = true;
                    break;
                }
                foreach (Vector2Int neighbor in Neighbors(current))
                {
                    if (parents.ContainsKey(neighbor) || !board.ContainsKey(neighbor) ||
                        (blocked != null && blocked.Contains(neighbor))) continue;
                    parents.Add(neighbor, current);
                    queue.Enqueue(neighbor);
                }
            }
            if (!reached) return false;
            while (parents[found] != origin) found = parents[found];
            return board.TryGetValue(found, out next);
        }
    }
}
