#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace MonsterTactics
{
    /// <summary>U6.10 roster editor installer. Does not modify original project until invoked.</summary>
    public static class MonsterTacticsU610RosterInstaller
    {
        private const string CatalogPath = "Assets/MonsterTactics/Resources/Content/MonsterTacticsContentCatalog.asset";
        private const string SpriteDir = "Assets/MonsterTactics/Art/MonsterU610";
        // base|cost|role|hp|forms|type-per-stage|dex-per-stage|origins|AD|AS|Armor|MR|Range|Mana
        private static readonly string[] Entries = {
            "Yaroris|2|Ranged|480|Yaroris,Astravar,Glimmon|Electric,Electric,Electric|172,25,26|Swifty|55|0.83|25|28|4|9999",
            "Quillobor|2|Tank|760|Quillobor,Bellokel,Umbramora|Ice / Ground,Ice / Ground,Ice / Ground|220,221,473|Bruiser|44|0.55|48|35|1|75",
            "Quillorixa|3|Fighter|740|Quillorixa,Pentamon,Orbirix|Fighting,Fighting,Fighting|66,67,68|Grit|69|0.68|42|36|1|75",
            "Astraset|4|Fighter|790|Astraset,Yarodrel,Zenodon|Electric,Electric,Electric|239,125,466|Grit,Overdrive|78|0.74|38|36|1|80",
            "Rovavian|4|Mage|580|Rovavian,Sylvociel,Tarodrin|Fire,Fire,Fire|240,126,467|Overdrive|64|0.70|25|35|3|80",
            "Jaspvar|4|Tank|1020|Jaspvar,Evotar,Wiloset|Rock / Ground,Rock / Ground,Rock / Dark|246,247,248|Grit|70|0.60|62|48|1|85",
            "Evodrel|3|Tank|890|Evodrel,Ionathorn,Caroris|Steel / Rock,Steel / Rock,Steel / Rock|304,305,306|Iron Fortress|48|0.57|65|52|1|75",
            "Sylvodon|1|Fighter|650|Sylvodon,Umbraset,Neromon|Fighting,Fighting,Fighting|532,533,534|Grit|51|0.68|34|28|1|65",
            "Umbrelisk|5|Assassin|920|Umbrelisk,Umbrelisk,Umbrelisk|Ghost / Fighting,Ghost / Fighting,Ghost / Fighting|802,802,802|Shadow Thief|105|1.05|38|40|1|80",
            "Astramora|5|Tank|1320|Astramora,Astramora,Astramora|Fire / Steel,Fire / Steel,Fire / Steel|485,485,485|Magma Core|72|0.65|72|67|1|90",
            "Lumomora|1|Fighter|610|Lumomora,Glimdrel,Heliodon|Ground / Dark,Ground / Dark,Ground / Dark|551,552,553|Bruiser|57|0.73|30|28|1|70",
            "Vexokel|3|Tank|880|Vexokel,Sylvovar,Tarothorn|Grass,Grass,Grass / Ground|387,388,389|Bruiser,First Partners|48|0.60|55|46|1|80",
            "Kaironet|3|Mage|540|Kaironet,Yaromon,Lumolyn|Ghost / Poison,Ghost / Poison,Ghost / Poison|92,93,94|Hexcraft|62|0.73|22|35|3|70",
            "Xenobor|1|Tank|670|Xenobor,Vexovek,Wiloris|Ghost,Ghost,Ghost|355,356,477|Hexcraft|43|0.60|48|49|1|70",
            "Orbivek|3|Fighter|700|Orbivek,Morarius,Nerotar|Electric,Electric,Electric|403,404,405|Overdrive|72|0.77|34|32|1|75",
            "Vexorius|2|Mage|450|Vexorius,Xenorix,Wilotar|Psychic,Psychic,Psychic / Fairy|856,857,858|Hexcraft|54|0.65|23|36|3|70",
            "Evomon|1|Ranged|470|Evomon,Draxirix,Rovadrel|Normal / Flying,Fire / Flying,Fire / Flying|661,662,663|Swifty|47|0.84|24|25|4|65",
            "Umbralyn|4|Ranged|560|Umbralyn,Tevodrelet,Sylvomora|Dragon / Ghost,Dragon / Ghost,Dragon / Ghost|885,886,887|Swifty|81|0.91|27|31|4|80"
        };
        // U6.10.3 shared source of truth for all 50 distinct sprite forms.
        public static IEnumerable<KeyValuePair<string, int>> EnumerateFormDex()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string row in Entries)
            {
                string[] columns = row.Split('|');
                string[] forms = columns[4].Split(',');
                string[] numbers = columns[6].Split(',');
                for (int i = 0; i < forms.Length && i < numbers.Length; i++)
                    if (seen.Add(forms[i]))
                        yield return new KeyValuePair<string, int>(forms[i], int.Parse(numbers[i]));
            }
        }

        private static readonly Queue<int> DownloadDex = new Queue<int>();
        private static readonly Dictionary<int, Sprite> Loaded = new Dictionary<int, Sprite>();
        private static UnityWebRequest pending;
        private static int currentDex;
        private static int failed;
        private static int processed;
        private static bool started;

        [MenuItem("MonsterTactics/U6.10/1. Download Evolution Sprites and Install Roster")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[U6.10] Exit Play Mode before installing."); return; }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if (catalog == null) { Debug.LogError("[U6.10] Missing Content Catalog: " + CatalogPath); return; }
            if (!Directory.Exists(SpriteDir)) Directory.CreateDirectory(SpriteDir);
            DownloadDex.Clear(); Loaded.Clear(); failed = processed = 0;
            var unique = new HashSet<int>();
            foreach (var row in Entries)
            {
                var parts = row.Split('|');
                foreach (var val in parts[6].Split(','))
                {
                    int dex = int.Parse(val);
                    if (unique.Add(dex)) DownloadDex.Enqueue(dex);
                }
            }
            started = true;
            EditorApplication.update -= Process;
            EditorApplication.update += Process;
            Debug.Log("[U6.10] Downloading " + DownloadDex.Count + " Monster sprites. Keep Unity Editor open; roster activates only after downloads finish.");
        }
        private static void Process()
        {
            if (!started) return;
            if (pending != null)
            {
                if (!pending.isDone) return;
                byte[] data = pending.result == UnityWebRequest.Result.Success ? pending.downloadHandler.data : null;
                if (data == null || data.Length < 100)
                { failed++; Debug.LogWarning("[U6.10] Sprite download failed for National Dex " + currentDex + ": " + pending.error); }
                else
                {
                    string path = SpriteDir + "/monster_" + currentDex + ".png";
                    File.WriteAllBytes(path, data);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.spritePixelsPerUnit = 96;
                        importer.filterMode = FilterMode.Point;
                        importer.mipmapEnabled = false;
                        importer.SaveAndReimport();
                    }
                    Loaded[currentDex] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (Loaded[currentDex] == null) failed++;
                }
                pending.Dispose(); pending = null; processed++;
            }
            if (DownloadDex.Count > 0)
            {
                currentDex = DownloadDex.Dequeue();
                string local = SpriteDir + "/monster_" + currentDex + ".png";
                if (File.Exists(local))
                {
                    Loaded[currentDex] = AssetDatabase.LoadAssetAtPath<Sprite>(local);
                    if (Loaded[currentDex] == null) failed++;
                    processed++; return;
                }
                pending = UnityWebRequest.Get("https://raw.githubusercontent.com/MonsterAPI/sprites/master/sprites/monster/" + currentDex + ".png");
                pending.timeout = 15;
                pending.SendWebRequest();
                return;
            }
            started = false;
            EditorApplication.update -= Process;
            if (failed > 0)
            {
                Debug.LogError("[U6.10] " + failed + " sprite downloads failed. Roster NOT activated. Check internet and run the installer again.");
                return;
            }
            ApplyCatalog();
        }
        private static void ApplyCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            Undo.RecordObject(catalog, "Install U6.10 Monster Roster");
            foreach (string row in Entries)
            {
                string[] p = row.Split('|');
                string[] forms = p[4].Split(','); string[] types = p[5].Split(','); string[] dex = p[6].Split(',');
                var existing = catalog.monster.Find(x => x != null && string.Equals(x.id, "family_" + p[0].ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
                if (existing == null) { existing = new MonsterTacticsMonsterRecord(); catalog.monster.Add(existing); }
                existing.id = "family_" + p[0].ToLowerInvariant(); existing.displayName = p[0]; existing.enabled = true;
                existing.shopCost = int.Parse(p[1]); existing.combatRole = p[2]; existing.baseHp = int.Parse(p[3]);
                existing.forms.Clear(); existing.origins.Clear();
                for (int i=0; i<3; i++)
                {
                    int d = int.Parse(dex[i]);
                    existing.forms.Add(new MonsterTacticsMonsterFormRecord { name = forms[i], types = types[i], bestiaryNumber = d,
                        sprite = Loaded[d] });
                }
                foreach (var origin in p[7].Split(',')) existing.origins.Add(origin);
                existing.stats = new MonsterTacticsMonsterStatRecord {attack=float.Parse(p[8], System.Globalization.CultureInfo.InvariantCulture),
                    attackSpeed=float.Parse(p[9], System.Globalization.CultureInfo.InvariantCulture), abilityPower=100,
                    criticalChance=.10f, criticalMultiplier=1.5f, armor=int.Parse(p[10]), magicResist=int.Parse(p[11]),
                    attackRange=int.Parse(p[12]), manaCost=int.Parse(p[13]), moveInterval=.52f};
                existing.abilityId = "u610_" + p[0].ToLowerInvariant();
                existing.designerNotes = "U6.10 playable family. Battle ability is implemented in MonsterTacticsU610Abilities.";
            }
            AddOrigin(catalog, "Mosslet", "Swifty");
            AddOrigin(catalog, "Budhorn", "Bruiser");
            AddTrait(catalog, "Swifty", "(2) +10% Attack Speed, +3% per hex to current target (max 5 hexes)", 2, null);
            AddTrait(catalog, "Bruiser", "(2) +12% max HP to Bruisers and +75 HP to all allies", 2,
                "(4) +24% max HP to Bruisers and +150 HP to all allies", 4);
            AddTrait(catalog, "Grit", "(2) Below 50% HP once: +125 HP, +12% AD, +10% omnivamp", 2,
                "(4) Below 50% HP once: +250 HP, +20% AD, +15% omnivamp", 4);
            AddTrait(catalog, "Overdrive", "(2) After cast +12% Damage Amp for 8s", 2,
                "(3) After cast +20% Damage Amp and +15% AS for 8s", 3);
            AddTrait(catalog, "Hexcraft", "(2) Ability hits apply 20% Shred for 4s", 2,
                "(3) Same Shred and +10% damage to shredded enemies", 3);
            AddTrait(catalog, "Iron Fortress", "(1) +15 Armor/MR; magic counter every fourth basic received", 1);
            AddTrait(catalog, "Shadow Thief", "(1) Once per fight steal AS and damage on first ability hit", 1);
            AddTrait(catalog, "Magma Core", "(1) Bonus Armor/MR converts to AP; immune to Burn", 1);
            catalog.readOnlyLegacyRuntime = false;
            catalog.enableLiveTraits = true;
            // New abilities use the dedicated combat handler. Leave legacy effect blocks disabled.
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            // U6.10.2: freshly downloaded sprites must be added to visible-alpha
            // bounds metadata or their HP bars anchor to the full PNG canvas.
            MonsterTactics.Editor.MonsterTacticsU686SpriteBoundsBuilder.Build();
            Debug.Log("[U6.10] Installed 18 new families + two trait updates. Total family records: " + catalog.monster.Count + ". Restart Play Mode to load the new roster.");
        }
        private static void AddOrigin(MonsterTacticsContentCatalog cat, string family, string origin)
        {
            var p = cat.monster.Find(x=>x!=null&&x.displayName==family);
            if (p != null && !p.origins.Contains(origin)) p.origins.Add(origin);
        }
        private static void AddTrait(MonsterTacticsContentCatalog cat,string name,string text,int threshold,string next=null,int nextThreshold=0)
        {
            var entry = cat.traits.Find(x=>x!=null&&x.displayName==name);
            if(entry==null){ entry=new MonsterTacticsTraitRecord(); cat.traits.Add(entry); }
            entry.id="trait_u610_"+name.Replace(" ","").ToLowerInvariant(); entry.displayName=name;
            entry.enabled=true; entry.isOrigin=true; entry.matchingUnitsOnly=true; entry.tiers.Clear();
            entry.tiers.Add(new MonsterTacticsTraitTierRecord{uniqueFamiliesRequired=threshold,description=text});
            if(nextThreshold>threshold) entry.tiers.Add(new MonsterTacticsTraitTierRecord{uniqueFamiliesRequired=nextThreshold,description=next});
            // All special-case bonuses handled in combat, so no double application here.
        }
        [MenuItem("MonsterTactics/U6.10/2. Verify Roster Installation")]
        public static void Verify()
        {
            var c=AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            int count=0, missing=0;
            foreach(string row in Entries){ string p=row.Split('|')[0]; var r=c.monster.Find(x=>x!=null&&x.displayName==p&&x.enabled);
                if(r==null){Debug.LogError("[U6.10] Missing family "+p);continue;} count++;
                foreach(var f in r.forms) if(f.sprite==null){Debug.LogError("[U6.10] Missing sprite: "+f.name);missing++;}}
            int profiles=0;
            foreach(string row in Entries)
            {
                string family=row.Split('|')[0];
                MonsterTacticsCombatRules.AbilityProfile p;
                if(MonsterTacticsU610Abilities.TryProfile(family,1,
                    MonsterTacticsCombatRules.ForMonster(family),out p)) profiles++;
            }
            Debug.Log("[U6.10] Enabled new families="+count+"/18, ability profiles="+profiles+
                "/18, missing sprites="+missing+", catalog="+(!c.readOnlyLegacyRuntime)+". " +
                (count==18 && missing==0 && profiles==18 && !c.readOnlyLegacyRuntime?"PASS":"FAIL"));
        }
    }
}
#endif
