using System;
using System.Linq;

namespace GildedFate.Core
{
    // Act 1 · Theme 3: The Drowned Quarter. Identity: delayed threats the player can see coming.
    // Rules live in Combat/DrownedCombat.cs; this file is data only.
    public static class DrownedQuarterContent
    {
        public const string Theme=ActThemes.DrownedQuarter;
        public const string Rustwalker="drowned_quarter_rustwalker",Lurker="drowned_quarter_drowned_lurker",BellDiver="drowned_quarter_bell_diver",
            RustPriest="drowned_quarter_rust_priest",CanalStalker="drowned_quarter_canal_stalker",Tidecaller="drowned_quarter_tidecaller",
            Hand="drowned_quarter_drowned_hand",Hulk="drowned_quarter_barnacle_hulk",Marionette="drowned_quarter_flooded_marionette",
            Ferryman="drowned_quarter_ferryman",Bellkeeper="drowned_quarter_bellkeeper",TollThrall="drowned_quarter_toll_thrall",
            SinkerThrall="drowned_quarter_sinker_thrall",Engine="drowned_quarter_sunken_engine",Magistrate="drowned_quarter_drowned_magistrate",
            Bailiff="drowned_quarter_bailiff_echo";

        private static EnemyDef N(string id,string name,int hp,int dmg,string text,bool elite=false,bool boss=false,bool minion=false,bool support=false)
            =>new(id,name,hp,dmg,text,elite,boss){theme=Theme,minion=minion,support=support};
        // HP is the solo/base value. Normals in a multi-enemy formation start at ~90%.
        public static readonly EnemyDef[] Enemies={
            N(Rustwalker,"RUSTWALKER",38,8,"A flooded construct that still marches. Cleave, brace, advance."),
            N(Lurker,"DROWNED LURKER",42,5,"Drags you under, sinks, then surfaces for a heavy strike."),
            N(BellDiver,"BELL DIVER",45,8,"Its tolling bell leaves you Weak and Vulnerable."),
            N(RustPriest,"RUST PRIEST",39,0,"A pure Support. It patches, shields and blesses its allies and never attacks.",support:true),
            N(CanalStalker,"CANAL STALKER",43,7,"Watches the water, then ambushes. It punishes turns you end without Block."),
            N(Tidecaller,"TIDECALLER",50,7,"Calls Drowned Hands up from below and directs them."),
            N(Hand,"DROWNED HAND",15,5,"A Tidecaller's Minion. It sinks when its Tidecaller dies.",minion:true),
            N(Hulk,"BARNACLE HULK",62,8,"Shelled, it is a wall. Below 30 health its shell breaks and it turns savage."),
            N(Marionette,"FLOODED MARIONETTE",54,7,"Loses a String every action. The fewer Strings, the wilder it fights."),
            N(Ferryman,"THE FERRYMAN",112,11,"Drags its anchor, raises it, then brings it crashing down.",elite:true),
            N(Bellkeeper,"THE BELLKEEPER",96,10,"Rings its bell over two thralls. Alone, it rings the Final Ring.",elite:true),
            N(TollThrall,"TOLL THRALL",21,6,"The Bellkeeper's Minion. It rings weakness into you.",minion:true),
            N(SinkerThrall,"SINKER THRALL",25,7,"The Bellkeeper's Minion. It shields its keeper.",minion:true),
            N(Engine,"THE SUNKEN ENGINE",124,10,"Builds Pressure you can watch rise, then releases it all at once.",elite:true),
            N(Magistrate,"THE DROWNED MAGISTRATE",220,12,"Fused to its flooded court. Each phase tears it further free.",boss:true),
            N(Bailiff,"BAILIFF ECHO",22,7,"The Drowned Magistrate's Minion. It holds court for its master.",minion:true),
        };
        public static EnemyDef Find(string id)=>Array.Find(Enemies,e=>e.id==id);
        public static readonly string[] Elites={Ferryman,Bellkeeper,Engine};
        public static readonly string[] Bosses={Magistrate};
        public static string[] StartingMinions(string id)=>id==Bellkeeper?new[]{TollThrall,SinkerThrall}:Array.Empty<string>();
        // Allies the Rust Priest's Patchwork Blessing can mend: constructs and armored creatures.
        public static readonly string[] Armored={Rustwalker,BellDiver,Hulk,Marionette,Engine};

        public static readonly EncounterDef[] Formations={
            F("DQ-E01",EncounterTier.Easy,Rustwalker),
            F("DQ-E02",EncounterTier.Easy,Rustwalker,Rustwalker),
            F("DQ-E03",EncounterTier.Easy,Lurker),
            F("DQ-E04",EncounterTier.Easy,BellDiver),
            F("DQ-E05",EncounterTier.Easy,CanalStalker),
            F("DQ-E06",EncounterTier.Easy,Marionette),
            F("DQ-S01",EncounterTier.Standard,Rustwalker,Lurker),
            F("DQ-S02",EncounterTier.Standard,Rustwalker,RustPriest),
            F("DQ-S03",EncounterTier.Standard,BellDiver,Rustwalker),
            F("DQ-S04",EncounterTier.Standard,Tidecaller,Hand),
            F("DQ-S05",EncounterTier.Standard,CanalStalker,Lurker),
            F("DQ-S06",EncounterTier.Standard,Hulk),
            F("DQ-S07",EncounterTier.Standard,Marionette,Rustwalker),
            F("DQ-S08",EncounterTier.Standard,BellDiver,CanalStalker),
            F("DQ-S09",EncounterTier.Standard,Rustwalker,Hulk),
            F("DQ-S10",EncounterTier.Standard,Tidecaller),
            F("DQ-D01",EncounterTier.Dangerous,Hulk,RustPriest,Rustwalker),
            F("DQ-D02",EncounterTier.Dangerous,BellDiver,CanalStalker,Rustwalker),
            F("DQ-D03",EncounterTier.Dangerous,Tidecaller,Hulk),
            F("DQ-D04",EncounterTier.Dangerous,Lurker,BellDiver,CanalStalker),
            F("DQ-D05",EncounterTier.Dangerous,Marionette,RustPriest,Rustwalker),
            F("DQ-D06",EncounterTier.Dangerous,Lurker,Lurker,Rustwalker),
            F("DQ-D07",EncounterTier.Dangerous,Hulk,BellDiver),
            F("DQ-D08",EncounterTier.Dangerous,Tidecaller,CanalStalker),
            F("DQ-D09",EncounterTier.Dangerous,Marionette,Lurker,BellDiver),
            F("DQ-D10",EncounterTier.Dangerous,Hulk,CanalStalker,RustPriest),
        };
        private static EncounterDef F(string id,EncounterTier tier,params string[] enemies)=>new(id,tier,1,1,enemies){theme=Theme};
    }

    // One place that knows every themed roster. Combat, routing and the Playground ask this
    // instead of a specific theme, so new themes (and Act 1 neutrals) only register here.
    public static class ThemeRosters
    {
        public static EnemyDef Find(string id)=>AshenWildsContent.Find(id)??DrownedQuarterContent.Find(id)??CrimsonFoundryContent.Find(id)??HollowwoodContent.Find(id)??ShatteredObservatoryContent.Find(id)??GildedRuinsContent.Find(id)??BlackCathedralContent.Find(id)??FracturedRealmContent.Find(id)??GildedThroneContent.Find(id);
        public static EnemyDef[] AllEnemies=>AshenWildsContent.Enemies.Concat(DrownedQuarterContent.Enemies).Concat(CrimsonFoundryContent.Enemies).Concat(HollowwoodContent.Enemies).Concat(ShatteredObservatoryContent.Enemies).Concat(GildedRuinsContent.Enemies).Concat(BlackCathedralContent.Enemies).Concat(FracturedRealmContent.Enemies).Concat(GildedThroneContent.Enemies).ToArray();
        public static EncounterDef[] Formations=>AshenWildsContent.Formations.Concat(DrownedQuarterContent.Formations).Concat(CrimsonFoundryContent.Formations).Concat(HollowwoodContent.Formations).Concat(ShatteredObservatoryContent.Formations).Concat(GildedRuinsContent.Formations).Concat(BlackCathedralContent.Formations).Concat(FracturedRealmContent.Formations).Concat(GildedThroneContent.Formations).ToArray();
        public static string[] Elites(string theme)=>theme switch
        {
            ActThemes.AshenWilds=>AshenWildsContent.Elites,ActThemes.DrownedQuarter=>DrownedQuarterContent.Elites,ActThemes.CrimsonFoundry=>CrimsonFoundryContent.Elites,ActThemes.Hollowwood=>HollowwoodContent.Elites,ActThemes.ShatteredObservatory=>ShatteredObservatoryContent.Elites,ActThemes.BlackCathedral=>BlackCathedralContent.Elites,ActThemes.FracturedRealm=>FracturedRealmContent.Elites,ActThemes.GildedThrone=>GildedThroneContent.Elites,ActThemes.GildedRuins=>GildedRuinsContent.Elites,_=>Array.Empty<string>()
        };
        public static string Boss(string theme)=>theme switch
        {
            ActThemes.AshenWilds=>AshenWildsContent.Alpha,ActThemes.DrownedQuarter=>DrownedQuarterContent.Magistrate,ActThemes.CrimsonFoundry=>CrimsonFoundryContent.Saint,ActThemes.Hollowwood=>HollowwoodContent.Heartroot,ActThemes.ShatteredObservatory=>ShatteredObservatoryContent.Curator,ActThemes.BlackCathedral=>BlackCathedralContent.Bishop,ActThemes.FracturedRealm=>FracturedRealmContent.Unmade,ActThemes.GildedThrone=>GildedThroneContent.Sovereign,ActThemes.GildedRuins=>GildedRuinsContent.Procession,_=>""
        };
        public static string[] StartingMinions(string id)
        {
            var a=AshenWildsContent.StartingMinions(id);if(a.Length>0)return a;
            a=DrownedQuarterContent.StartingMinions(id);if(a.Length>0)return a;
            a=CrimsonFoundryContent.StartingMinions(id);if(a.Length>0)return a;
            a=HollowwoodContent.StartingMinions(id);if(a.Length>0)return a;
            a=ShatteredObservatoryContent.StartingMinions(id);if(a.Length>0)return a;
            a=GildedRuinsContent.StartingMinions(id);if(a.Length>0)return a;
            a=BlackCathedralContent.StartingMinions(id);if(a.Length>0)return a;
            a=FracturedRealmContent.StartingMinions(id);return a.Length>0?a:GildedThroneContent.StartingMinions(id);
        }
        // The Minion type an owner can Summon mid-fight ("" = it never summons).
        public static string SummonType(string ownerId)=>ownerId switch
        {
            AshenWildsContent.Rootcaller=>AshenWildsContent.Sapling,
            DrownedQuarterContent.Tidecaller=>DrownedQuarterContent.Hand,
            DrownedQuarterContent.Magistrate=>DrownedQuarterContent.Bailiff,
            CrimsonFoundryContent.AssemblyMaster=>CrimsonFoundryContent.ScrapDrone,
            CrimsonFoundryContent.Saint=>CrimsonFoundryContent.Servitor,
            HollowwoodContent.BroodPod=>HollowwoodContent.Sporeling,
            HollowwoodContent.GardenMother=>HollowwoodContent.Huskbud,
            ShatteredObservatoryContent.Weaver=>ShatteredObservatoryContent.StarFragment,
            GildedRuinsContent.Keeper=>GildedRuinsContent.Servitor,
            GildedRuinsContent.Collector=>GildedRuinsContent.Guard,
            BlackCathedralContent.Saint=>BlackCathedralContent.Effigy,
            FracturedRealmContent.Splitling=>FracturedRealmContent.SplitEcho,
            FracturedRealmContent.Sovereign=>FracturedRealmContent.BladeFragment,
            GildedThroneContent.Commander or GildedThroneContent.General=>GildedThroneContent.Guard,
            _=>""
        };
        public static int MinionCap(string ownerId)=>ownerId is DrownedQuarterContent.Magistrate or CrimsonFoundryContent.Saint?1:2;
        // Group HP for normal enemies that start beside another non-Minion: ~90% in Act 1, ~92% in Act 2.
        public static int GroupHpPercent(EnemyDef def)=>def?.theme is ActThemes.CrimsonFoundry or ActThemes.Hollowwood or ActThemes.ShatteredObservatory?92:def?.theme is ActThemes.BlackCathedral or ActThemes.FracturedRealm or ActThemes.GildedThrone?94:90;
        public static bool IsMinion(string id)=>Find(id)?.minion==true;
        // True when this owner may start a fight with (or later summon) this Minion.
        public static bool CanOwn(string ownerId,string minionId)=>SummonType(ownerId)==minionId||StartingMinions(ownerId).Contains(minionId);
    }
}
