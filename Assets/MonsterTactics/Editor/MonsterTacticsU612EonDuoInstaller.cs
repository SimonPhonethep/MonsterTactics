#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Debug=UnityEngine.Debug;

namespace MonsterTactics.Editor
{
    public static class MonsterTacticsU612EonDuoInstaller
    {
        private const string CatalogPath="Assets/MonsterTactics/Resources/Content/MonsterTacticsContentCatalog.asset";
        private const string BackupPath="Assets/MonsterTactics/Resources/Content/MonsterTacticsContentCatalog.before-eon-duo.asset";
        private const string ArtRoot="Assets/MonsterTactics/Art/MonsterU610";
        private const string ProfilePath="Assets/MonsterTactics/Resources/MonsterTacticsU612EonProfile.asset";
        private const string AnimationScript="Assets/MonsterTactics/Tools/DownloadU610AnimatedSprites.ps1";
        private static readonly int[] Dex={380,381};
        private static readonly string[] Names={"Skyweave","Skyshard"};
        private static readonly Dictionary<int,Sprite> sprites=new Dictionary<int,Sprite>();
        private static UnityWebRequest pending;
        private static int next;
        private static bool downloading;
        private static Process process;

        public static bool IsInstalled(string name)
        {
            var catalog=AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            return catalog!=null && catalog.monster!=null && catalog.monster.Exists(p=>
                p!=null && p.enabled && p.displayName==name);
        }
        [MenuItem("MonsterTactics/U6.11.4 Eon Duo/1. Install Skyweave + Skyshard (Matching Sprites)")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || downloading) return;
            var catalog=AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if(catalog==null || catalog.readOnlyLegacyRuntime)
            {Debug.LogError("[U6.11.4] Active content catalog required.");return;}
            if(!Directory.Exists(ArtRoot)) Directory.CreateDirectory(ArtRoot);
            next=0; sprites.Clear(); downloading=true;
            EditorApplication.update-=PollSprites;
            EditorApplication.update+=PollSprites;
            Debug.Log("[U6.11.4] Checking both Eon Duo sprites (#380/#381). Both are required before catalog changes.");
        }
        private static string SpritePath(int dex) => ArtRoot+"/monster_"+dex+".png";
        private static Sprite Import(int dex)
        {
            string path=SpritePath(dex);
            if(!File.Exists(path)) return null;
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)return null;
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=96f;
            importer.filterMode=FilterMode.Point;
            importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.wrapMode=TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static void PollSprites()
        {
            if(!downloading){EditorApplication.update-=PollSprites;return;}
            if(pending!=null)
            {
                if(!pending.isDone)return;
                int dex=Dex[next];
                bool ok=pending.result==UnityWebRequest.Result.Success &&
                    pending.downloadHandler.data!=null && pending.downloadHandler.data.Length>100;
                if(ok)
                {
                    try {File.WriteAllBytes(SpritePath(dex),pending.downloadHandler.data);}
                    catch(Exception e){Debug.LogError("[U6.11.4] Save failed: "+e.Message);ok=false;}
                }
                if(!ok)Debug.LogError("[U6.11.4] Failed downloading #"+dex+". No catalog changes.");
                pending.Dispose();pending=null;
                if(!ok){StopDownload();return;}
                sprites[dex]=Import(dex);
                if(sprites[dex]==null){StopDownload();Debug.LogError("[U6.11.4] Sprite import failed.");return;}
                next++;
            }
            if(next==Dex.Length)
            {
                StopDownload();
                ApplyDuo();
                return;
            }
            int id=Dex[next];
            if(File.Exists(SpritePath(id)))
            {
                sprites[id]=Import(id);
                if(sprites[id]==null){StopDownload();Debug.LogError("[U6.11.4] Existing art failed import.");return;}
                next++;
                return;
            }
            pending=UnityWebRequest.Get("https://raw.githubusercontent.com/MonsterAPI/sprites/master/sprites/monster/"+id+".png");
            pending.timeout=25;
            pending.SendWebRequest();
        }
        private static void StopDownload()
        {
            downloading=false;
            EditorApplication.update-=PollSprites;
            if(pending!=null){pending.Dispose();pending=null;}
        }
        private static void EnsureFamily(MonsterTacticsContentCatalog catalog,string name,int dex,
            string role,int hp,float attack,float speed,int range,int mana)
        {
            var p=catalog.monster.Find(x=>x!=null && x.id=="family_"+name.ToLowerInvariant());
            if(p==null){p=new MonsterTacticsMonsterRecord();catalog.monster.Add(p);}
            p.id="family_"+name.ToLowerInvariant();p.displayName=name;p.enabled=true;
            p.shopCost=5;p.combatRole=role;p.baseHp=hp;
            if(p.forms==null)p.forms=new List<MonsterTacticsMonsterFormRecord>();
            p.forms.Clear();
            for(int stage=0;stage<3;stage++)p.forms.Add(new MonsterTacticsMonsterFormRecord{
                name=name,types="Dragon / Psychic",bestiaryNumber=dex,sprite=sprites[dex]});
            if(p.origins==null)p.origins=new List<string>();
            p.origins.Clear();p.origins.Add("Eon Bond");
            p.stats=new MonsterTacticsMonsterStatRecord{attack=attack,attackSpeed=speed,
                abilityPower=100f,criticalChance=.10f,criticalMultiplier=1.5f,
                armor=role=="Mage"?38:32,magicResist=role=="Mage"?42:35,
                attackRange=range,manaCost=mana,moveInterval=.55f};
            p.abilityId="u6114_"+name.ToLowerInvariant();
            p.designerNotes="U6.11.4 Eon Duo standalone 5g; matching frame-based art with fallback. " +
                (name=="Skyshard"?"Luster Purge: AS buff, every 3rd attack shoots piercing magic.":
                "Mist Ball: targeted magic spell, shields lowest HP% ally.");
        }
        private static void ApplyDuo()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if(catalog==null || catalog.readOnlyLegacyRuntime || sprites.Count!=2)return;
            if(AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(BackupPath)==null &&
                !AssetDatabase.CopyAsset(CatalogPath,BackupPath))
            {Debug.LogError("[U6.11.4] Backup failed; installation cancelled.");return;}
            Undo.RecordObject(catalog,"Install U6.11.4 Eon Duo");
            EnsureFamily(catalog,"Skyweave",380,"Mage",900,60f,.82f,3,80);
            EnsureFamily(catalog,"Skyshard",381,"Ranged",800,74f,.95f,4,90);
            if(catalog.traits==null)catalog.traits=new List<MonsterTacticsTraitRecord>();
            var bond=catalog.traits.Find(t=>t!=null && t.id=="trait_eon_bond");
            if(bond==null){bond=new MonsterTacticsTraitRecord();catalog.traits.Add(bond);}
            bond.id="trait_eon_bond";bond.displayName="Eon Bond";
            bond.enabled=true;bond.isOrigin=true;bond.matchingUnitsOnly=true;
            bond.tiers=new List<MonsterTacticsTraitTierRecord>{new MonsterTacticsTraitTierRecord{
                uniqueFamiliesRequired=2,
                description="Skyweave + Skyshard alive: +10% damage dealt. Casting grants partner 20 Mana.",
                effects=new List<MonsterTacticsContentEffectRecord>()}};
            bond.designerNotes="Runtime checks living pair at damage and cast time. " +
                "No passive bonus while one Eon sibling is fainted. Configured via profile.";
            catalog.enableLiveTraits=true;
            EditorUtility.SetDirty(catalog);
            if(AssetDatabase.LoadAssetAtPath<MonsterTacticsU612EonProfile>(ProfilePath)==null)
            {
                var profile=ScriptableObject.CreateInstance<MonsterTacticsU612EonProfile>();
                AssetDatabase.CreateAsset(profile,ProfilePath);
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            MonsterTacticsU686SpriteBoundsBuilder.Build();
            Debug.Log("[U6.11.4] Skyweave+Skyshard added to 5-cost pool with Eon Bond (2). " +
                "Next: run menu 2 to fetch matching genuine animations, then menu 4 to verify.");
        }

        [MenuItem("MonsterTactics/U6.11.4 Eon Duo/2. Download Matching Animated Frames")]
        public static void DownloadAnimations()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode ||
                !IsInstalled("Skyweave") || !IsInstalled("Skyshard"))
            {Debug.LogError("[U6.11.4] Install both first; exit Play Mode.");return;}
            if(Application.platform!=RuntimePlatform.WindowsEditor)
            {Debug.LogWarning("[U6.11.4] Use local frame PNGs in MonsterU610Animated/skyweave & /skyshard " +
                "and select menu 3 on non-Windows systems.");return;}
            if(process!=null && !process.HasExited)return;
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            string script=Path.Combine(root,AnimationScript.Replace('/',Path.DirectorySeparatorChar));
            if(!File.Exists(script)){Debug.LogError("[U6.11.4] Animation script missing.");return;}
            string manifest=Path.Combine(root,"Library","MonsterTacticsU6114EonAnimation.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(manifest));
            File.WriteAllText(manifest,"name,id"+Environment.NewLine+
                "skyweave,380"+Environment.NewLine+"skyshard,381"+Environment.NewLine);
            try{
                process=Process.Start(new ProcessStartInfo{
                    FileName="powershell.exe",
                    Arguments="-NoProfile -ExecutionPolicy Bypass -File \"" + script +
                        "\" -ProjectRoot \"" + root + "\" -ManifestPath \"" + manifest + "\"",
                    WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true});
                if(process==null)throw new InvalidOperationException("PowerShell did not start.");
                EditorApplication.update-=PollAnimations;EditorApplication.update+=PollAnimations;
                Debug.Log("[U6.11.4] Importing animated Skyweave and Skyshard frames with shared 96x96 pipeline.");
            }catch(Exception e){Debug.LogError(e.Message);if(process!=null)process.Dispose();process=null;}
        }
        private static void PollAnimations()
        {
            if(process==null){EditorApplication.update-=PollAnimations;return;}
            if(!process.HasExited)return;
            int code=process.ExitCode;process.Dispose();process=null;
            EditorApplication.update-=PollAnimations;
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            MonsterTacticsU6103AnimatedSpritesMenu.Rebuild();
            Debug.Log("[U6.11.4] Animation import finished (exit="+code+"). Run Verify.");
        }
        [MenuItem("MonsterTactics/U6.11.4 Eon Duo/3. Rebuild Animation Catalog From Local Frames")]
        public static void RebuildAnimations()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            MonsterTacticsU6103AnimatedSpritesMenu.Rebuild();
        }
        [MenuItem("MonsterTactics/U6.11.4 Eon Duo/4. Verify Duo + Art + Abilities")]
        public static void Verify()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            var trait=catalog!=null && catalog.traits!=null ?
                catalog.traits.Find(t=>t!=null && t.enabled && t.id=="trait_eon_bond"):null;
            var profile=AssetDatabase.LoadAssetAtPath<MonsterTacticsU612EonProfile>(ProfilePath);
            var anim=AssetDatabase.LoadAssetAtPath<MonsterTacticsU610AnimatedArtCatalog>(
                "Assets/MonsterTactics/Resources/MonsterTacticsU610AnimatedArtCatalog.asset");
            bool beamRules =
                MonsterTacticsU612EonRules.IsOnPiercingRay(new Vector2Int(0,0),
                    new Vector2Int(1,0), new Vector2Int(1,0),5) &&
                MonsterTacticsU612EonRules.IsOnPiercingRay(new Vector2Int(0,0),
                    new Vector2Int(1,0), new Vector2Int(2,0),5) &&
                !MonsterTacticsU612EonRules.IsOnPiercingRay(new Vector2Int(0,0),
                    new Vector2Int(1,0), new Vector2Int(-1,0),5);
            bool ok=trait!=null && trait.isOrigin && trait.tiers!=null &&
                trait.tiers.Count==1 && trait.tiers[0].uniqueFamiliesRequired==2 &&
                catalog!=null && catalog.enableLiveTraits && profile!=null && beamRules;
            foreach(string name in Names)
            {
                var p=catalog!=null && catalog.monster!=null?catalog.monster.Find(x=>
                    x!=null&&x.enabled&&x.displayName==name):null;
                MonsterTacticsCombatRules.AbilityProfile ability;
                bool valid=MonsterTacticsU610Abilities.TryProfile(name,1,
                    MonsterTacticsCombatRules.ForMonster(name),out ability);
                bool sprite=p!=null&&p.shopCost==5&&p.forms!=null&&p.forms.Count==3&&
                    p.forms[0].sprite!=null&&p.origins.Contains("Eon Bond");
                bool animated=false;
                if(sprite && anim!=null && anim.Forms!=null)
                    animated=anim.Forms.Exists(x=>x!=null && x.Form==name &&
                        x.StaticSource==p.forms[0].sprite && x.Frames!=null && x.Frames.Length>=2);
                ok &= valid&&sprite;
                Debug.Log("[U6.11.4] "+name+": family="+(p!=null)+", sprite="+sprite+
                    ", ability="+valid+", genuineFrames="+animated+
                    (animated?" (ready)":" (static+idle fallback only; run menu 2)"));
            }
            Debug.Log("[U6.11.4] "+(ok?"PASS":"FAIL")+
                " content setup (beam geometry="+beamRules+"). Eon Bond activation, partner Mana, live target choice, beam hit VFX, " +
                "frame playback must still be checked in Unity Play Mode.");
        }
        [MenuItem("MonsterTactics/U6.11.4 Eon Duo/5. Disable Duo (Keep Art)")]
        public static void Disable()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var catalog=AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if(catalog==null||catalog.monster==null)return;
            Undo.RecordObject(catalog,"Disable Eon Duo");
            foreach(var p in catalog.monster)
                if(p!=null&&(p.id=="family_skyweave"||p.id=="family_skyshard"))p.enabled=false;
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            Debug.Log("[U6.11.4] Skyweave & Skyshard disabled; backup and art retained.");
        }
    }
}
#endif
