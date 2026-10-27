using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.1f.1: Opt-in immutable match snapshot of the authoring catalog.
    /// The asset itself is never modified at runtime. The old roster remains
    /// available for rollback and for scene starters from older demo scenes.
    /// Traits and ability execution deliberately remain legacy until f.2/f.3.
    /// </summary>
    public static class MonsterTacticsContentRosterRuntime
    {
        private sealed class Snapshot
        {
            internal bool Active;
            internal MonsterTacticsU5Roster.Family[] Families;
            internal Dictionary<string, MonsterTacticsU5Roster.Family> ByForm;
            internal Dictionary<string, MonsterTacticsMonsterStatRecord> Stats;
            internal Dictionary<string, Sprite> Sprites;
        }
        private static Snapshot cached;
        private const string ResourcePath = "Content/MonsterTacticsContentCatalog";

        // Domain reload disabled / Play Mode start still creates a fresh snapshot.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewPlaySession() { cached = null; }

        private static Snapshot Current
        {
            get
            {
                // Editor inspection should see current saved changes. Play Mode is
                // snapshotted so a mid-match edit cannot invalidate shop references.
                if (!Application.isPlaying || cached == null) cached = Build();
                return cached;
            }
        }

        public static bool IsUsingCatalog { get { return Current.Active; } }
        public static MonsterTacticsU5Roster.Family[] ActiveFamilies
        {
            get { return Current.Active ? Current.Families : MonsterTacticsU5Roster.All; }
        }
        public static bool TryFind(string form, out MonsterTacticsU5Roster.Family family)
        {
            family = null;
            if (String.IsNullOrEmpty(form)) return false;
            Snapshot s = Current;
            return s.Active && s.ByForm.TryGetValue(form, out family);
        }
        public static bool TryApplyStats(string form, ref MonsterTacticsCombatRules.Stats result)
        {
            if (String.IsNullOrEmpty(form)) return false;
            Snapshot s = Current;
            if (!s.Active) return false;
            MonsterTacticsU5Roster.Family family;
            if (!s.ByForm.TryGetValue(form, out family)) return false;
            MonsterTacticsMonsterStatRecord v;
            if (!s.Stats.TryGetValue(family.Forms[0], out v)) return false;
            result.Attack = v.attack;
            result.AttackSpeed = v.attackSpeed;
            result.AbilityPower = v.abilityPower;
            result.Omnivamp = v.omnivamp;
            result.CriticalChance = v.criticalChance;
            result.CriticalMultiplier = v.criticalMultiplier;
            result.Armor = v.armor;
            result.MagicResist = v.magicResist;
            result.Range = v.attackRange;
            result.ManaCost = v.manaCost;
            result.MoveInterval = v.moveInterval;
            return true;
        }
        public static Sprite FormSprite(string form)
        {
            if (String.IsNullOrEmpty(form)) return null;
            Snapshot s = Current;
            Sprite sprite;
            return s.Active && s.Sprites.TryGetValue(form, out sprite) ? sprite : null;
        }

        private static Snapshot Empty()
        {
            return new Snapshot
            {
                Active = false, Families = new MonsterTacticsU5Roster.Family[0],
                ByForm = new Dictionary<string, MonsterTacticsU5Roster.Family>(StringComparer.OrdinalIgnoreCase),
                Stats = new Dictionary<string, MonsterTacticsMonsterStatRecord>(StringComparer.OrdinalIgnoreCase),
                Sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase)
            };
        }
        private static Snapshot Build()
        {
            Snapshot s = Empty();
            MonsterTacticsContentCatalog catalog = Resources.Load<MonsterTacticsContentCatalog>(ResourcePath);
            if (catalog == null || catalog.readOnlyLegacyRuntime) return s;
            if (catalog.monster == null) return Invalid("Missing Monster catalog list");
            var list = new List<MonsterTacticsU5Roster.Family>();
            var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MonsterTacticsMonsterRecord p in catalog.monster)
            {
                if (p == null || !p.enabled) continue;
                if (p.forms == null || p.forms.Count != 3 || p.stats == null ||
                    String.IsNullOrWhiteSpace(p.id) || String.IsNullOrWhiteSpace(p.combatRole) ||
                    p.shopCost < 1 || p.shopCost > 5 || p.baseHp < 1 ||
                    p.stats.attack < 0f || !Finite(p.stats.attack) ||
                    !Finite(p.stats.attackSpeed) || p.stats.attackSpeed <= 0 ||
                    !Finite(p.stats.abilityPower) || p.stats.abilityPower < 0 ||
                    !Finite(p.stats.omnivamp) || p.stats.omnivamp < 0 || p.stats.omnivamp > 1 ||
                    !Finite(p.stats.criticalChance) || p.stats.criticalChance < 0 || p.stats.criticalChance > 1 ||
                    !Finite(p.stats.criticalMultiplier) || p.stats.criticalMultiplier < 1 ||
                    p.stats.armor < 0 || p.stats.magicResist < 0 ||
                    p.stats.attackRange < 1 || p.stats.manaCost < 1 ||
                    !Finite(p.stats.moveInterval) || p.stats.moveInterval <= 0)
                    return Invalid("Enabled Monster has invalid gameplay fields: " + (p == null ? "(null)" : p.id));
                string[] forms = new string[3], types = new string[3];
                int[] dex = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    MonsterTacticsMonsterFormRecord f = p.forms[i];
                    if (f == null || String.IsNullOrWhiteSpace(f.name) || String.IsNullOrWhiteSpace(f.types))
                        return Invalid("Incomplete form in " + p.id);
                    forms[i] = f.name.Trim(); types[i] = f.types.Trim(); dex[i] = f.bestiaryNumber;
                    if (types[i].StartsWith("/") || types[i].EndsWith("/"))
                        return Invalid("Invalid typing in " + p.id);
                    // Repeated names inside one family are legal for legendary species.
                    if (owners.Contains(forms[i]))
                    {
                        bool sameFamily = false;
                        for (int j = 0; j < i; j++)
                            if (String.Equals(forms[i], forms[j], StringComparison.OrdinalIgnoreCase))
                                sameFamily = true;
                        if (!sameFamily) return Invalid("Duplicate form across families: " + forms[i]);
                    }
                    owners.Add(forms[i]);
                    if (f.sprite != null) s.Sprites[forms[i]] = f.sprite;
                }
                bool legacyArt = false;
                foreach (MonsterTacticsU5Roster.Family old in MonsterTacticsU5Roster.All)
                    if (String.Equals(old.Forms[0], forms[0], StringComparison.OrdinalIgnoreCase))
                    { legacyArt = true; break; }
                // A completely new family must have art: no invisible shop cards.
                if (!legacyArt && p.forms[0].sprite == null)
                    return Invalid("New Monster " + forms[0] + " needs a base-form sprite override");
                MonsterTacticsU5Roster.Family family = new MonsterTacticsU5Roster.Family(
                    p.shopCost, p.combatRole, p.baseHp, forms, types, dex);
                list.Add(family);
                foreach (string form in forms) s.ByForm[form] = family;
                // Copy data so editing the ScriptableObject cannot change an active match.
                var original = p.stats;
                s.Stats[forms[0]] = new MonsterTacticsMonsterStatRecord
                {
                    attack = original.attack, attackSpeed = original.attackSpeed,
                    abilityPower = original.abilityPower, omnivamp = original.omnivamp,
                    criticalChance = original.criticalChance, criticalMultiplier = original.criticalMultiplier,
                    armor = original.armor, magicResist = original.magicResist,
                    attackRange = original.attackRange, manaCost = original.manaCost,
                    moveInterval = original.moveInterval
                };
            }
            if (list.Count == 0) return Invalid("Enable at least one Monster before catalog mode");
            bool hasCostOne = false;
            foreach (MonsterTacticsU5Roster.Family candidate in list)
                if (candidate.Cost == 1) hasCostOne = true;
            if (!hasCostOne) return Invalid("At least one enabled cost-1 Monster is required");
            s.Families = list.ToArray(); s.Active = true;
            return s;
        }
        private static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
        private static Snapshot Invalid(string reason)
        {
            Debug.LogError("[U6.9.1f.1] Catalog not activated; using legacy roster: " + reason);
            return Empty();
        }
    }
}
