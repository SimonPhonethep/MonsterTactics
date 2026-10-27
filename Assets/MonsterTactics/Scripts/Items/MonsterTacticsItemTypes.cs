using System;
using System.Collections.Generic;
using UnityEngine;
namespace MonsterTactics
{
    public enum MonsterTacticsItemKind
    {
        AttackFang, MysticSpoon, SwiftFeather, IronPlate, VitalSeed, ManaCrystal, ResistCloak, LuckyCharm, TrainerCrest, TacticsPan, Craft00, Craft01, Craft02, Craft03, Craft04, Craft05, Craft06, Craft07, Craft08, Craft09, Craft10, Craft11, Craft12, Craft13, Craft14, Craft15, Craft16, Craft17, Craft18, Craft19, Craft20, Craft21, Craft22, Craft23, Craft24, Craft25, Craft26, Craft27, Craft28, Craft29, Craft30, Craft31, Craft32, Craft33, Craft34, Craft35, Special00, Special01, Special02, Special03, Special04, Special05, Special06, Special07, Special08, Special09, Special10, Special11, Special12, Special13, Special14, Special15, Special16, Special17, Special18, ItemRemover, Reforger
    }
    public struct MonsterTacticsItemBonus
    {
        public float Attack, AbilityPower, AttackSpeed, CriticalChance, Omnivamp, DamageAmplification, DamageReduction;
        public int Armor, MagicResist, MaximumHealth, StartingMana;
        public void Add(MonsterTacticsItemBonus other)
        {
            Attack += other.Attack; AbilityPower += other.AbilityPower; AttackSpeed += other.AttackSpeed;
            CriticalChance += other.CriticalChance; Omnivamp += other.Omnivamp;
            DamageAmplification += other.DamageAmplification; DamageReduction += other.DamageReduction;
            Armor += other.Armor; MagicResist += other.MagicResist; MaximumHealth += other.MaximumHealth; StartingMana += other.StartingMana;
        }
    }
    public static class MonsterTacticsItemTypes
    {
        public static readonly MonsterTacticsItemKind[] Components = { MonsterTacticsItemKind.AttackFang, MonsterTacticsItemKind.MysticSpoon, MonsterTacticsItemKind.SwiftFeather, MonsterTacticsItemKind.IronPlate, MonsterTacticsItemKind.VitalSeed, MonsterTacticsItemKind.ManaCrystal, MonsterTacticsItemKind.ResistCloak, MonsterTacticsItemKind.LuckyCharm };
        public static readonly MonsterTacticsItemKind[] CraftingComponents = Components; // Eight regular components only.
        public static readonly MonsterTacticsItemKind[] Completed = { MonsterTacticsItemKind.Craft00, MonsterTacticsItemKind.Craft01, MonsterTacticsItemKind.Craft02, MonsterTacticsItemKind.Craft03, MonsterTacticsItemKind.Craft04, MonsterTacticsItemKind.Craft05, MonsterTacticsItemKind.Craft06, MonsterTacticsItemKind.Craft07, MonsterTacticsItemKind.Craft08, MonsterTacticsItemKind.Craft09, MonsterTacticsItemKind.Craft10, MonsterTacticsItemKind.Craft11, MonsterTacticsItemKind.Craft12, MonsterTacticsItemKind.Craft13, MonsterTacticsItemKind.Craft14, MonsterTacticsItemKind.Craft15, MonsterTacticsItemKind.Craft16, MonsterTacticsItemKind.Craft17, MonsterTacticsItemKind.Craft18, MonsterTacticsItemKind.Craft19, MonsterTacticsItemKind.Craft20, MonsterTacticsItemKind.Craft21, MonsterTacticsItemKind.Craft22, MonsterTacticsItemKind.Craft23, MonsterTacticsItemKind.Craft24, MonsterTacticsItemKind.Craft25, MonsterTacticsItemKind.Craft26, MonsterTacticsItemKind.Craft27, MonsterTacticsItemKind.Craft28, MonsterTacticsItemKind.Craft29, MonsterTacticsItemKind.Craft30, MonsterTacticsItemKind.Craft31, MonsterTacticsItemKind.Craft32, MonsterTacticsItemKind.Craft33, MonsterTacticsItemKind.Craft34, MonsterTacticsItemKind.Craft35 };
        public static readonly MonsterTacticsItemKind[] SpecialCompleted = new MonsterTacticsItemKind[0]; // Reserved for a future update.
        private static readonly string[] Names = { "Attack Fang", "Mystic Spoon", "Swift Feather", "Iron Plate", "Vital Seed", "Mana Crystal", "Resist Cloak", "Lucky Charm", "Trainer Crest", "Tactics Pan", "Twin Fangs", "Spellblade", "Quick Claw", "Iron Fang", "Life Drinker", "Focused Strike", "Dragon Fang", "Precision Edge", "Wise Staff", "Mystic Tempo", "Arcane Guard", "Blight Staff", "Blue Spell", "Veiled Wand", "Critical Magic", "Windstorm", "Swift Guard", "Bloom Rush", "Feather Flow", "Zephyr Cloak", "Rapid Precision", "Stonewall", "Ironbark", "Defense Engine", "Balanced Shield", "Spiked Plate", "Giant Heart", "Life Font", "Protected Growth", "Lucky Bloom", "Quickcast", "Mind Shield", "Mana Roulette", "Mystic Barrier", "Reflex Ward", "Critical Strike", "Fire Emblem", "Water Emblem", "Grass Emblem", "Dragon Emblem", "Ground Emblem", "First Partners Emblem", "Playful Emblem", "Legendary Beasts Emblem", "Trainer Blade", "Trainer Wand", "Trainer Tempo", "Trainer Bulwark", "Trainer Vitality", "Trainer Focus", "Trainer Ward", "Trainer Fortune", "Origin Crown", "Tactics Crown", "Party Crown", "Item Remover", "Reforger" };
        public static string Name(MonsterTacticsItemKind kind) { int n=(int)kind; return n>=0 && n<Names.Length ? Names[n] : kind.ToString(); }
        public static string ShortName(MonsterTacticsItemKind kind)
        {
            if (IsTool(kind)) return kind == MonsterTacticsItemKind.ItemRemover ? "REMOVE" : "REFORGE";
            if (kind == MonsterTacticsItemKind.TrainerCrest) return "CREST";
            if (kind == MonsterTacticsItemKind.TacticsPan) return "PAN";
            if (IsComponent(kind)) return new [] {"FANG","AP","AS","ARM","HP","MANA","MR","CRIT"}[(int)kind];
            return Name(kind).Length <= 12 ? Name(kind).ToUpperInvariant() : Name(kind).Substring(0,12).ToUpperInvariant();
        }
        // Enum IDs 8,9 and 46..64 are intentionally RESERVED. Do not renumber them: Unity may have serialized existing IDs.
        public static bool IsAvailable(MonsterTacticsItemKind item) => IsComponent(item) || IsCompleted(item) || IsTool(item);
        public static bool IsComponent(MonsterTacticsItemKind item) => (int)item>=0 && (int)item<=7;
        public static bool IsTool(MonsterTacticsItemKind item) => item==MonsterTacticsItemKind.ItemRemover || item==MonsterTacticsItemKind.Reforger;
        public static bool IsCompleted(MonsterTacticsItemKind item) => (int)item>=10 && (int)item<=45;
        public static bool IsSpecial(MonsterTacticsItemKind item) => false; // Disabled for U6.7.3 standard-only.
        public static bool IsEmblem(MonsterTacticsItemKind item) => false; // Disabled for U6.7.3 standard-only.
        private static readonly string[] EmblemNames = { "Fire", "Water", "Grass", "Dragon", "Ground", "First Partners", "Playful", "Legendary Beasts" };
        public static string EmblemTrait(MonsterTacticsItemKind item) => IsEmblem(item) ? EmblemNames[(int)item-46] : null;
        public static bool TryRecipe(MonsterTacticsItemKind a, MonsterTacticsItemKind b, out MonsterTacticsItemKind recipe)
        {
            recipe=default(MonsterTacticsItemKind);
            if(!IsComponent(a)||!IsComponent(b)) return false;
            int x=Mathf.Min((int)a,(int)b), y=Mathf.Max((int)a,(int)b);
            if(x<8 && y<8)
            {
                int idx=x*8-(x*(x-1))/2 + y-x;
                recipe=(MonsterTacticsItemKind)(10+idx); return true;
            }
            return false;
        }
        public static string Description(MonsterTacticsItemKind item)
        {
            if(item==MonsterTacticsItemKind.ItemRemover) return "Consumable: unequip all held items; return them unchanged. Cannot target a deployed unit in combat.";
            if(item==MonsterTacticsItemKind.Reforger) return "Consumable: unequip and reroll all items into different ones of matching category. Cannot target a deployed unit in combat.";
            if(!IsAvailable(item)) return "Reserved for a future update (not obtainable or equippable).";
            if(IsCompleted(item)) return "Stats: " + BonusSummary(Bonus(item)) +
                ". Passive: " + MonsterTacticsItemPassiveCatalog.Effect(item) +
                " (Same-named passive is unique; duplicate item stats stack.)";
            return Name(item) + ": " + BonusSummary(Bonus(item));
        }
        public static string BonusSummary(MonsterTacticsItemBonus b)
        {
            var list = new List<string>();
            if (b.Attack > 0f) list.Add("+" + b.Attack.ToString("0") + " AD");
            if (b.AbilityPower > 0f) list.Add("+" + b.AbilityPower.ToString("0") + " AP");
            if (b.AttackSpeed > 0f) list.Add("+" + (b.AttackSpeed * 100f).ToString("0") + "% AS");
            if (b.Armor > 0) list.Add("+" + b.Armor + " Armor");
            if (b.MaximumHealth > 0) list.Add("+" + b.MaximumHealth + " HP");
            if (b.StartingMana > 0) list.Add("+" + b.StartingMana + " starting Mana");
            if (b.MagicResist > 0) list.Add("+" + b.MagicResist + " MR");
            if (b.CriticalChance > 0f) list.Add("+" + (b.CriticalChance * 100f).ToString("0") + "% Crit");
            if (b.Omnivamp > 0f) list.Add("+" + (b.Omnivamp * 100f).ToString("0") + "% Omnivamp");
            if (b.DamageAmplification > 0f) list.Add("+" + (b.DamageAmplification * 100f).ToString("0") + "% damage");
            if (b.DamageReduction > 0f) list.Add("-" + (b.DamageReduction * 100f).ToString("0") + "% damage taken");
            return list.Count > 0 ? string.Join(", ", list.ToArray()) : "No numerical bonus";
        }
        public static Color Color(MonsterTacticsItemKind kind)
        {
            Color[] colors={new Color(.92f,.47f,.39f),new Color(.65f,.48f,.95f),new Color(.53f,.86f,.82f),new Color(.65f,.77f,.85f),new Color(.50f,.83f,.49f),new Color(.40f,.65f,.98f),new Color(.80f,.66f,.95f),new Color(.97f,.77f,.41f)};
            int n=(int)kind;
            if(n>=0 && n<8)return colors[n];
            if(kind==MonsterTacticsItemKind.ItemRemover)return new Color(.57f,.83f,.97f);
            if(kind==MonsterTacticsItemKind.Reforger)return new Color(.95f,.52f,.88f);
            if(IsSpecial(kind)||n==8||n==9)return new Color(.94f,.68f,.31f);
            return new Color(.38f,.74f,.88f);
        }
        private static MonsterTacticsItemBonus ComponentBonus(int id)
        {
            MonsterTacticsItemBonus b=new MonsterTacticsItemBonus();
            switch(id)
            {
                case 0:b.Attack=9;break;
                case 1:b.AbilityPower=12;break;
                case 2:b.AttackSpeed=.12f;break;
                case 3:b.Armor=15;break;
                case 4:b.MaximumHealth=100;break;
                case 5:b.StartingMana=12;break;
                case 6:b.MagicResist=15;break;
                case 7:b.CriticalChance=.12f;break;
            }
            return b;
        }
        private static readonly int[] PairA = { 0,0,0,0,0,0,0,0,1,1,1,1,1,1,1,2,2,2,2,2,2,3,3,3,3,3,4,4,4,4,5,5,5,6,6,7 };
        private static readonly int[] PairB = { 0,1,2,3,4,5,6,7,1,2,3,4,5,6,7,2,3,4,5,6,7,3,4,5,6,7,4,5,6,7,5,6,7,6,7,7 };
        public static MonsterTacticsItemBonus Bonus(MonsterTacticsItemKind kind)
        {
            int id=(int)kind;
            MonsterTacticsItemBonus b=new MonsterTacticsItemBonus();
            if(id<8 && id>=0)return ComponentBonus(id);
            if(id>=10 && id<46)
            {
                int idx=id-10; b.Add(ComponentBonus(PairA[idx])); b.Add(ComponentBonus(PairB[idx]));
                // Consistent stat budget: every crafted item inherits exactly
                // its two component bonuses; its unique passive is separate.
                return b;
            }
            return b;
        }
        public static MonsterTacticsCombatRules.Stats ApplyStats(MonsterTacticsCombatRules.Stats stats, MonsterTacticsItemBonus b)
        {
            stats.Attack+=b.Attack; stats.AbilityPower+=b.AbilityPower;
            stats.AttackSpeed*=1f+b.AttackSpeed; stats.Armor+=b.Armor; stats.MagicResist+=b.MagicResist;
            stats.CriticalChance=Mathf.Clamp01(stats.CriticalChance+b.CriticalChance);
            stats.Omnivamp=Mathf.Clamp01(stats.Omnivamp+b.Omnivamp);
            stats.DamageMultiplier*=1f+b.DamageAmplification;
            stats.IncomingDamageMultiplier*=1f-Mathf.Clamp01(b.DamageReduction);
            return stats;
        }
        public static MonsterTacticsItemKind Reforge(MonsterTacticsItemKind original)
        {
            MonsterTacticsItemKind[] pool;
            int id = (int)original;
            if (id >= 0 && id <= 7) pool = Components;
            else if (id >= 10 && id <= 45) pool = Completed;
            else return original;
            int valid=0; foreach(var k in pool) if(k != original) valid++;
            if(valid<=0) return original;
            int pick=UnityEngine.Random.Range(0,valid);
            foreach(var k in pool){ if(k==original) continue; if(pick--==0) return k; }
            return original;
        }
    }
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsHeldItems : MonoBehaviour
    {
        public const int MaxSlots=3;
        [SerializeField] private List<MonsterTacticsItemKind> items=new List<MonsterTacticsItemKind>();
        public IReadOnlyList<MonsterTacticsItemKind> Items=>items;
        public bool IsFull=>items.Count>=MaxSlots;
        public MonsterTacticsItemBonus CombinedBonus
        {
            get{var b=new MonsterTacticsItemBonus();foreach(var item in items)b.Add(MonsterTacticsItemTypes.Bonus(item));return b;}
        }
        public bool TryAdd(MonsterTacticsItemKind item)
        {
            if(!MonsterTacticsItemTypes.IsAvailable(item) || MonsterTacticsItemTypes.IsTool(item))return false;
            if(MonsterTacticsItemTypes.IsComponent(item))
            {
                for(int i=0;i<items.Count;i++)
                {
                    if(MonsterTacticsItemTypes.TryRecipe(items[i],item,out MonsterTacticsItemKind crafted))
                    {
                        items[i]=crafted;return true;
                    }
                }
            }
            if(IsFull)return false;
            items.Add(item);return true;
        }
        public MonsterTacticsItemKind[] TakeAll(){var result=items.ToArray();items.Clear();return result;}
    }
}