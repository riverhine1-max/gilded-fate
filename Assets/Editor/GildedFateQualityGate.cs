using System.Collections.Generic;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Map;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    [InitializeOnLoad]
    public static class GildedFateQualityGate
    {
        public static bool LastPassed { get; private set; }
        private static readonly string[] RequiredArt =
        {
            "Assets/Resources/Art/GildedVault_Menu.png",
            "Assets/Resources/Art/GildedFate_Logo.png",
            "Assets/Resources/Art/Heroes_Select.png",
            "Assets/Resources/Art/CombatArena_LowerVault.png",
            "Assets/Resources/Art/BossArenaAtlas_3.png",
            "Assets/Resources/Art/RunMap_LowerVault.png",
            "Assets/Resources/Art/MapNodeAtlas_7.png",
            "Assets/Resources/Art/CombatIconAtlas_10.png",
            "Assets/Resources/Art/CombatVfxAtlas_8.png",
            "Assets/Resources/Art/CollectionArchive.png",
            "Assets/Resources/Art/RewardReliquary.png",
            "Assets/Resources/Art/LocationAtlas_4.png",
            "Assets/Resources/Art/EnemyAtlas_17.png",
            "Assets/Resources/Art/VanguardCardAtlas_25.png",
            "Assets/Resources/Art/HexerCardAtlas_25.png",
            "Assets/Resources/Art/NeutralCurseCardAtlas_12.png",
            "Assets/Resources/Art/RelicAtlas_24.png",
            "Assets/Resources/Art/RelicAtlas_Bonus_12.png",
            "Assets/Resources/Art/ConsumableAtlas_8.png",
            "Assets/Resources/Art/ReaperCharacter.png",
            "Assets/Resources/Art/DeathsKeepsake.png",
            "Assets/Resources/Art/ReaperCardAtlas_A25.png",
            "Assets/Resources/Art/ReaperCardAtlas_B25.png",
            "Assets/Resources/Art/ReaperCardAtlas_C4.png"
        };

        static GildedFateQualityGate() => EditorApplication.delayCall += Validate;

        [MenuItem("Gilded Fate/Run Production Quality Gate")]
        public static void Validate()
        {
            var failures = new List<string>();
            Expect("cards", GameContent.Cards.Length, 282, failures);
            Expect("relics", GameContent.Relics.Length, 90, failures);
            Expect("enemies", WorldContent.Enemies.Length, 17, failures);
            Expect("Fate Shards", WorldContent.FateShards.Length, 30, failures);
            Expect("Bindings", WorldContent.Bindings.Length, 11, failures);
            Expect("Fateweaves", WorldContent.Fateweaves.Length, 18, failures);
            Expect("events", WorldContent.Events.Length, 50, failures);
            Expect("normal enemies", System.Array.FindAll(WorldContent.Enemies,x=>!x.elite&&!x.boss).Length, 10, failures);
            Expect("elite enemies", System.Array.FindAll(WorldContent.Enemies,x=>x.elite).Length, 4, failures);
            Expect("boss enemies", System.Array.FindAll(WorldContent.Enemies,x=>x.boss).Length, 3, failures);
            Unique("card", System.Array.ConvertAll(GameContent.Cards, x => x.id), failures);
            Unique("relic", System.Array.ConvertAll(GameContent.Relics, x => x.id), failures);
            Unique("enemy", System.Array.ConvertAll(WorldContent.Enemies, x => x.id), failures);
            Unique("Fate Shard", System.Array.ConvertAll(WorldContent.FateShards, x => x.id), failures);
            Unique("Binding", System.Array.ConvertAll(WorldContent.Bindings, x => x.id), failures);
            Unique("Fateweave", System.Array.ConvertAll(WorldContent.Fateweaves, x => x.id), failures);
            Unique("event", System.Array.ConvertAll(WorldContent.Events, x => x.id), failures);
            var vanguard = new RunModel(); vanguard.NewRun(HeroId.Vanguard, 12345);
            var hexer = new RunModel(); hexer.NewRun(HeroId.Hexer, 12345);
            var reaper = new RunModel(); reaper.NewRun(HeroId.Reaper, 12345);
            Expect("Vanguard starter cards", vanguard.deck.Count, 10, failures);
            Expect("Hexer starter cards", hexer.deck.Count, 10, failures);
            Expect("Reaper starter cards", reaper.deck.Count, 10, failures);
            if(!vanguard.HasValidMap())failures.Add("Generated map failed route, boss, or reachability validation");
            if (vanguard.nodes.FindAll(n => n.kind == NodeKind.Boss).Count != 1||!vanguard.nodes.Exists(n=>n.floor==RunModel.FloorCount-1&&n.lane==RunModel.LaneCount/2&&n.kind==NodeKind.Boss)) failures.Add("Final floor must contain one centered boss");
            foreach(var node in vanguard.nodes)if(node.floor<RunModel.FloorCount-1&&(node.nextMask==0||(node.nextMask&~((1<<RunModel.LaneCount)-1))!=0))failures.Add($"Invalid route links at floor {node.floor}, lane {node.lane}");
            for(var floor=0;floor<RunModel.FloorCount;floor++){var available=vanguard.nodes.Find(n=>n.floor==floor&&n.available);if(available==null){failures.Add("Generated route became unreachable at floor "+floor);break;}vanguard.AdvanceFrom(available);}
            var stageTest=new RunModel();stageTest.NewRun(HeroId.Vanguard,20260902);var stageNode=stageTest.nodes.Find(n=>n.floor==0&&n.available);stageTest.stage=RunStage.Merchant;stageTest.activeNodeFloor=stageNode.floor;stageTest.activeNodeLane=stageNode.lane;stageTest.activeEnemyId="vault_rat";stageTest.activeEventId="masked_beggar";stageTest.merchantRemoved=stageTest.merchantHealed=true;stageTest.merchantSold.Add("strike");stageTest.AdvanceFrom(stageNode);if(stageTest.stage!=RunStage.Map||stageTest.activeNodeFloor!=-1||stageTest.activeNodeLane!=-1||stageTest.activeEnemyId!=""||stageTest.activeEventId!=""||stageTest.merchantRemoved||stageTest.merchantHealed||stageTest.merchantSold.Count!=0)failures.Add("Completed node did not clear resumable stage state");
            foreach(var card in GameContent.Cards)if(card.rarity is not (Rarity.Curse or Rarity.Status or Rarity.Special)){var upgraded=GameContent.Upgrade(card);if(upgraded==null||!upgraded.upgraded||!upgraded.name.EndsWith("+")||(upgraded.cost==card.cost&&upgraded.value==card.value&&upgraded.secondary==card.secondary&&upgraded.hits==card.hits&&upgraded.exhaust==card.exhaust&&upgraded.text==card.text))failures.Add("Card has no meaningful authored upgrade: "+card.id);}
            ValidateCombat(failures);
            ValidateArtwork(failures);
            foreach(var name in new[]{"SourceSans3-Regular","SourceSans3-Semibold","SourceSerif4Display-Semibold"})
                if(!AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/"+name+".ttf"))failures.Add("Missing bundled font: "+name);
            foreach (var path in RequiredArt)
            {
                var art=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(!art)failures.Add("Missing art: " + path);else if(art.width<1024||art.height<512)failures.Add($"Production art is below minimum resolution: {path} ({art.width}×{art.height})");
            }

            LastPassed=failures.Count==0;
            if (LastPassed)
                Debug.Log("[Gilded Fate Quality Gate] PASS · 282 uniquely illustrated cards · 90 relics · 3 heroes · 17 enemies · transparent actor art · 50 events");
            else
                Debug.LogError("[Gilded Fate Quality Gate] FAIL\n" + string.Join("\n", failures));
        }

        private static void Expect(string label, int actual, int expected, List<string> failures)
        {
            if (actual != expected) failures.Add($"Expected {expected} {label}; found {actual}");
        }

        private static void ValidateArtwork(List<string> failures)
        {
            ValidateCutout("Art/Powers/MartialOccult",failures);
            ValidateCutout("Art/Powers/MajorVanguard",failures);
            ValidateCutout("Art/Powers/MajorRelicEffects",failures);
            for(var atlas=1;atlas<=3;atlas++)ValidateCutout("Art/Relics/Expansion/major_relics_"+atlas,failures);
            var mapped=new HashSet<string>();
            foreach(var sheet in GildedArtCatalog.Sheets)
            {
                if(sheet.ids.Length>sheet.columns*sheet.rows)failures.Add("Card sheet overflow: "+sheet.resource);
                var texture=Resources.Load<Texture2D>(sheet.resource);
                if(!texture)failures.Add("Missing card sheet: "+sheet.resource);
                else if(texture.width/sheet.columns<128||texture.height/sheet.rows<128)failures.Add("Card sheet cells too small: "+sheet.resource);
                foreach(var id in sheet.ids)
                {
                    if(!mapped.Add(id))failures.Add("Duplicate card art identity: "+id);
                    if(GameContent.Find(id)==null)failures.Add("Unknown card art identity: "+id);
                }
            }
            foreach(var card in GameContent.Cards)if(!mapped.Contains(card.id))failures.Add("Unillustrated card: "+card.id);
            foreach(HeroId hero in System.Enum.GetValues(typeof(HeroId)))ValidateCutout(GildedArtCatalog.HeroResource(hero),failures);
            foreach(var enemy in WorldContent.Enemies)ValidateCutout(GildedArtCatalog.EnemyResource(enemy.id),failures);
        }

        private static void ValidateCutout(string resource,List<string> failures)
        {
            var path="Assets/Resources/"+resource+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null||!AssetDatabase.LoadAssetAtPath<Texture2D>(path))failures.Add("Missing combat cutout: "+resource);
            else if(!importer.DoesSourceTextureHaveAlpha())failures.Add("Combat image lacks transparent alpha: "+resource);
        }

        private static void Unique(string label, string[] ids, List<string> failures)
        {
            var seen = new HashSet<string>();
            foreach (var id in ids) if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) failures.Add($"Duplicate or empty {label} ID: {id}");
        }

        private static void ValidateCombat(List<string> failures)
        {
            var strike=System.Array.Find(GameContent.Cards,c=>c.id=="strike");var strikes=new List<CardDef>();for(var i=0;i<10;i++)strikes.Add(strike);var attackTest=new CombatState();attackTest.Begin(HeroId.Vanguard,strikes,60);var played=attackTest.Play(attackTest.hand[0]);if(!played||attackTest.energy!=2||attackTest.enemy.hp!=54)failures.Add("Basic attack/energy flow failed");
            var defend=System.Array.Find(GameContent.Cards,c=>c.id=="defend");var guards=new List<CardDef>();for(var i=0;i<10;i++)guards.Add(defend);var blockTest=new CombatState();blockTest.Begin(HeroId.Vanguard,guards,60,0,80,80,null,"vault_rat",7);blockTest.Play(blockTest.hand[0]);blockTest.EndTurn();if(blockTest.player.hp!=78)failures.Add("Block absorption/enemy turn flow failed");
            var buckleTest=new CombatState();buckleTest.Begin(HeroId.Vanguard,guards,60,0,80,80,new[]{"gilded_buckle"});buckleTest.Play(buckleTest.hand[0]);if(buckleTest.player.block!=10)failures.Add("Gilded Buckle first-Block rule failed");
            var upgraded=GameContent.Upgrade(strike);if(upgraded==null||upgraded.value<=strike.value||!upgraded.upgraded)failures.Add("Card upgrade combat values failed");
            var battleCry=System.Array.Find(GameContent.Cards,c=>c.id=="battle_cry");var temporaryStrength=new CombatState();temporaryStrength.Begin(HeroId.Vanguard,guards,60,0,80,80,null,"gilded_sentry",9);temporaryStrength.PlayFree(battleCry);temporaryStrength.EndTurn();if(temporaryStrength.player.strength!=0)failures.Add("Battle Cry temporary Strength did not expire");
            var ember=System.Array.Find(GameContent.Cards,c=>c.id=="ember_ritual");var invocation=System.Array.Find(GameContent.Cards,c=>c.id=="invocation");var ritual=new CombatState();ritual.Begin(HeroId.Hexer,guards,60,0,68,68,new[]{"cracked_prism"});ritual.PlayFree(ember);ritual.PlayFree(invocation);if(ritual.sigils.Count!=1||ritual.enemy.burn!=2||ritual.resonance!=3)failures.Add("Sigil activation, Burn, or Resonance flow failed");
            var retaliate=new CombatState();retaliate.Begin(HeroId.Vanguard,guards,60,0,80,80,null,"vault_rat",7);retaliate.retaliation=5;retaliate.player.block=0;retaliate.EndTurn();if(retaliate.enemy.hp!=55||retaliate.retaliation!=0)failures.Add("Retaliate must consume once per Attack, even without Block");
            var hollow=new CombatState();hollow.Begin(HeroId.Vanguard,guards,180,2,200,200,null,"hollow_king",21);hollow.enemy.hp=119;hollow.RefreshEnemyState();if(hollow.bossPhase!=2||hollow.spectralWeapons!=2||hollow.enemy.block!=14)failures.Add("Hollow King phase mechanics failed");
            var mother=new CombatState();mother.Begin(HeroId.Vanguard,guards,205,2,200,200,null,"vault_mother",18);mother.enemy.hp=130;mother.RefreshEnemyState();if(mother.bossPhase!=2||mother.vaultWards!=0||mother.enemy.block!=18)failures.Add("Vault Mother visible-Block phase mechanics failed");
            var dealer=new CombatState();dealer.Begin(HeroId.Hexer,guards,170,2,200,200,null,"last_dealer",20);dealer.enemy.hp=55;dealer.RefreshEnemyState();if(dealer.bossPhase!=3||dealer.cursePressure!=5)failures.Add("Last Dealer phase mechanics failed");
        }
    }
}
