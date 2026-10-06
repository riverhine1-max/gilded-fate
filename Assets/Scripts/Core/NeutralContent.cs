using System;
using System.Linq;

namespace GildedFate.Core
{
    // Prompt 11: act-specific shared enemies ("neutral" is INTERNAL terminology and is never shown to the player).
    // Each neutral is shared only between the three themes of ITS OWN act. One gameplay definition per enemy;
    // the picture depends on the active theme (see VariantArtId). Rules live in Combat/NeutralCombat.cs.
    public static class NeutralContent
    {
        public const string Theme=EncounterContent.Neutral;
        public const string UnboundBlade="act1_neutral_unbound_blade",Fateworn="act1_neutral_fateworn",StrayIdol="act1_neutral_stray_idol",Wayfarer="act1_neutral_elite_wayfarer",
            RaggedVanguard="act2_neutral_ragged_vanguard",ShiftingHusk="act2_neutral_shifting_husk",CrookedOracle="act2_neutral_crooked_oracle",Deepcrawler="act2_neutral_elite_deepcrawler",
            IronWanderer="act3_neutral_iron_wanderer",PaleChimera="act3_neutral_pale_chimera",NamelessSeer="act3_neutral_nameless_seer",Worldbreaker="act3_neutral_elite_worldbreaker";

        private static EnemyDef N(string id,string name,int hp,int dmg,string text,bool elite=false)=>new(id,name,hp,dmg,text,elite,false){theme=Theme};
        public static readonly EnemyDef[] Enemies={
            N(UnboundBlade,"UNBOUND BLADE",40,8,"A readable attacker: strike, gather nerve, drive."),
            N(Fateworn,"FATEWORN",46,9,"Guards, cuts, then turns the thread."),
            N(StrayIdol,"STRAY IDOL",54,8,"Gathers fortune, then releases it. The release costs it Strength."),
            N(Wayfarer,"THE WAYFARER",116,12,"Rotates through three visible stances, two actions each.",true),
            N(RaggedVanguard,"RAGGED VANGUARD",68,12,"A frontline hybrid that builds momentum."),
            N(ShiftingHusk,"SHIFTING HUSK",80,11,"Armored until it is badly hurt, then exposed."),
            N(CrookedOracle,"CROOKED ORACLE",70,13,"Reacts to the Energy you left unspent last turn."),
            N(Deepcrawler,"THE DEEPCRAWLER",174,19,"A huge armored worm. It burrows, and the eruption is always shown a turn ahead.",true),
            N(IronWanderer,"IRON WANDERER",96,17,"A heavy bruiser without tricks."),
            N(PaleChimera,"PALE CHIMERA",100,20,"Alternates two actions of attack with two of defense."),
            N(NamelessSeer,"NAMELESS SEER",94,16,"Readied Fate always prepares the Sentence that follows."),
            N(Worldbreaker,"THE WORLDBREAKER",252,20,"Charges World Break over two warning turns.",true),
        };
        public static EnemyDef Find(string id)=>Array.Find(Enemies,e=>e.id==id);
        public static bool IsNeutral(string id)=>Find(id)!=null;
        public static int ActOf(string id)=>id==null?0:id.StartsWith("act1_")?1:id.StartsWith("act2_")?2:id.StartsWith("act3_")?3:0;
        public static readonly string[] Elites={Wayfarer,Deepcrawler,Worldbreaker};
        public static string EliteForAct(int act)=>act==1?Wayfarer:act==2?Deepcrawler:act==3?Worldbreaker:"";
        public static int ActOfTheme(string theme)=>theme switch
        {
            ActThemes.GildedRuins or ActThemes.AshenWilds or ActThemes.DrownedQuarter=>1,
            ActThemes.CrimsonFoundry or ActThemes.Hollowwood or ActThemes.ShatteredObservatory=>2,
            ActThemes.BlackCathedral or ActThemes.FracturedRealm or ActThemes.GildedThrone=>3,
            _=>0
        };
        // The picture for the current theme. The base id is the fallback when a variant is not present.
        public static string VariantArtId(string id,string theme)=>IsNeutral(id)&&!string.IsNullOrEmpty(theme)?id+"_"+theme:id;

        // Neutral-theme formations join every theme of their own act (and no other act). Mixed formations
        // belong to one theme and pair a neutral with that theme's own enemies.
        public static readonly EncounterDef[] Formations={
            F("A1N-E01",EncounterTier.Easy,EncounterContent.Neutral,1,NeutralContent.UnboundBlade),
            F("A1N-E02",EncounterTier.Easy,EncounterContent.Neutral,1,NeutralContent.Fateworn),
            F("A1N-E03",EncounterTier.Easy,EncounterContent.Neutral,1,NeutralContent.StrayIdol),
            F("GR-N01",EncounterTier.Standard,ActThemes.GildedRuins,1,NeutralContent.UnboundBlade,GildedRuinsContent.Scavenger),
            F("GR-N02",EncounterTier.Standard,ActThemes.GildedRuins,1,NeutralContent.Fateworn,GildedRuinsContent.Bastion),
            F("GR-N03",EncounterTier.Standard,ActThemes.GildedRuins,1,NeutralContent.StrayIdol,GildedRuinsContent.Appraiser),
            F("GR-N04",EncounterTier.Dangerous,ActThemes.GildedRuins,1,NeutralContent.UnboundBlade,GildedRuinsContent.Chorister,GildedRuinsContent.Scavenger),
            F("GR-N05",EncounterTier.Standard,ActThemes.GildedRuins,1,NeutralContent.Fateworn,GildedRuinsContent.Herald),
            F("GR-N06",EncounterTier.Standard,ActThemes.GildedRuins,1,NeutralContent.StrayIdol,GildedRuinsContent.Duelist),
            F("AW-N01",EncounterTier.Standard,ActThemes.AshenWilds,1,NeutralContent.UnboundBlade,AshenWildsContent.Cinderfang),
            F("AW-N02",EncounterTier.Standard,ActThemes.AshenWilds,1,NeutralContent.Fateworn,AshenWildsContent.Grazer),
            F("AW-N03",EncounterTier.Standard,ActThemes.AshenWilds,1,NeutralContent.StrayIdol,AshenWildsContent.Stalker),
            F("AW-N04",EncounterTier.Dangerous,ActThemes.AshenWilds,1,NeutralContent.UnboundBlade,AshenWildsContent.Emberwing,AshenWildsContent.Cinderfang),
            F("AW-N05",EncounterTier.Standard,ActThemes.AshenWilds,1,NeutralContent.Fateworn,AshenWildsContent.Elk),
            F("AW-N06",EncounterTier.Standard,ActThemes.AshenWilds,1,NeutralContent.StrayIdol,AshenWildsContent.Thornjaw),
            F("DQ-N01",EncounterTier.Standard,ActThemes.DrownedQuarter,1,NeutralContent.UnboundBlade,DrownedQuarterContent.Rustwalker),
            F("DQ-N02",EncounterTier.Standard,ActThemes.DrownedQuarter,1,NeutralContent.Fateworn,DrownedQuarterContent.BellDiver),
            F("DQ-N03",EncounterTier.Standard,ActThemes.DrownedQuarter,1,NeutralContent.StrayIdol,DrownedQuarterContent.CanalStalker),
            F("DQ-N04",EncounterTier.Dangerous,ActThemes.DrownedQuarter,1,NeutralContent.UnboundBlade,DrownedQuarterContent.RustPriest,DrownedQuarterContent.Rustwalker),
            F("DQ-N05",EncounterTier.Standard,ActThemes.DrownedQuarter,1,NeutralContent.Fateworn,DrownedQuarterContent.Lurker),
            F("DQ-N06",EncounterTier.Standard,ActThemes.DrownedQuarter,1,NeutralContent.StrayIdol,DrownedQuarterContent.Marionette),
            F("CF-N01",EncounterTier.Standard,ActThemes.CrimsonFoundry,2,NeutralContent.RaggedVanguard,CrimsonFoundryContent.Forgehand),
            F("CF-N02",EncounterTier.Standard,ActThemes.CrimsonFoundry,2,NeutralContent.ShiftingHusk,CrimsonFoundryContent.ChainWarden),
            F("CF-N03",EncounterTier.Standard,ActThemes.CrimsonFoundry,2,NeutralContent.CrookedOracle,CrimsonFoundryContent.Hound),
            F("CF-N04",EncounterTier.Advanced,ActThemes.CrimsonFoundry,2,NeutralContent.RaggedVanguard,CrimsonFoundryContent.RivetPriest,CrimsonFoundryContent.Forgehand),
            F("CF-N05",EncounterTier.Advanced,ActThemes.CrimsonFoundry,2,NeutralContent.ShiftingHusk,CrimsonFoundryContent.Knight),
            F("CF-N06",EncounterTier.Advanced,ActThemes.CrimsonFoundry,2,NeutralContent.CrookedOracle,CrimsonFoundryContent.Automaton),
            F("HW-N01",EncounterTier.Standard,ActThemes.Hollowwood,2,NeutralContent.RaggedVanguard,HollowwoodContent.Sproutling),
            F("HW-N02",EncounterTier.Standard,ActThemes.Hollowwood,2,NeutralContent.ShiftingHusk,HollowwoodContent.RootSnare),
            F("HW-N03",EncounterTier.Standard,ActThemes.Hollowwood,2,NeutralContent.CrookedOracle,HollowwoodContent.Stag),
            F("HW-N04",EncounterTier.Advanced,ActThemes.Hollowwood,2,NeutralContent.RaggedVanguard,HollowwoodContent.Sporekeeper,HollowwoodContent.Sproutling),
            F("HW-N05",EncounterTier.Advanced,ActThemes.Hollowwood,2,NeutralContent.ShiftingHusk,HollowwoodContent.Bloomfang),
            F("HW-N06",EncounterTier.Advanced,ActThemes.Hollowwood,2,NeutralContent.CrookedOracle,HollowwoodContent.ElderHusk),
            F("SO-N01",EncounterTier.Standard,ActThemes.ShatteredObservatory,2,NeutralContent.RaggedVanguard,ShatteredObservatoryContent.Scribe),
            F("SO-N02",EncounterTier.Standard,ActThemes.ShatteredObservatory,2,NeutralContent.ShiftingHusk,ShatteredObservatoryContent.Sentinel),
            F("SO-N03",EncounterTier.Standard,ActThemes.ShatteredObservatory,2,NeutralContent.CrookedOracle,ShatteredObservatoryContent.Monk),
            F("SO-N04",EncounterTier.Advanced,ActThemes.ShatteredObservatory,2,NeutralContent.RaggedVanguard,ShatteredObservatoryContent.Attendant,ShatteredObservatoryContent.Scribe),
            F("SO-N05",EncounterTier.Advanced,ActThemes.ShatteredObservatory,2,NeutralContent.ShiftingHusk,ShatteredObservatoryContent.Chronoglyph),
            F("SO-N06",EncounterTier.Advanced,ActThemes.ShatteredObservatory,2,NeutralContent.CrookedOracle,ShatteredObservatoryContent.Astrologer),
            F("BC-N01",EncounterTier.Standard,ActThemes.BlackCathedral,3,NeutralContent.IronWanderer,BlackCathedralContent.Guard),
            F("BC-N02",EncounterTier.Standard,ActThemes.BlackCathedral,3,NeutralContent.PaleChimera,BlackCathedralContent.Confessor),
            F("BC-N03",EncounterTier.Standard,ActThemes.BlackCathedral,3,NeutralContent.NamelessSeer,BlackCathedralContent.Censer),
            F("BC-N04",EncounterTier.Advanced,ActThemes.BlackCathedral,3,NeutralContent.IronWanderer,BlackCathedralContent.Warden,BlackCathedralContent.Guard),
            F("BC-N05",EncounterTier.Advanced,ActThemes.BlackCathedral,3,NeutralContent.PaleChimera,BlackCathedralContent.Herald),
            F("BC-N06",EncounterTier.Advanced,ActThemes.BlackCathedral,3,NeutralContent.NamelessSeer,BlackCathedralContent.Priest),
            F("FR-N01",EncounterTier.Standard,ActThemes.FracturedRealm,3,NeutralContent.IronWanderer,FracturedRealmContent.Soldier),
            F("FR-N02",EncounterTier.Standard,ActThemes.FracturedRealm,3,NeutralContent.PaleChimera,FracturedRealmContent.Beast),
            F("FR-N03",EncounterTier.Standard,ActThemes.FracturedRealm,3,NeutralContent.NamelessSeer,FracturedRealmContent.Shear),
            F("FR-N04",EncounterTier.Advanced,ActThemes.FracturedRealm,3,NeutralContent.IronWanderer,FracturedRealmContent.Binder,FracturedRealmContent.Soldier),
            F("FR-N05",EncounterTier.Advanced,ActThemes.FracturedRealm,3,NeutralContent.PaleChimera,FracturedRealmContent.Husk),
            F("FR-N06",EncounterTier.Advanced,ActThemes.FracturedRealm,3,NeutralContent.NamelessSeer,FracturedRealmContent.Oracle),
            F("GT-N01",EncounterTier.Standard,ActThemes.GildedThrone,3,NeutralContent.IronWanderer,GildedThroneContent.Vanguard),
            F("GT-N02",EncounterTier.Standard,ActThemes.GildedThrone,3,NeutralContent.PaleChimera,GildedThroneContent.Crownshield),
            F("GT-N03",EncounterTier.Standard,ActThemes.GildedThrone,3,NeutralContent.NamelessSeer,GildedThroneContent.Adjudicator),
            F("GT-N04",EncounterTier.Advanced,ActThemes.GildedThrone,3,NeutralContent.IronWanderer,GildedThroneContent.Strategist,GildedThroneContent.Vanguard),
            F("GT-N05",EncounterTier.Advanced,ActThemes.GildedThrone,3,NeutralContent.PaleChimera,GildedThroneContent.Duelist),
            F("GT-N06",EncounterTier.Advanced,ActThemes.GildedThrone,3,NeutralContent.NamelessSeer,GildedThroneContent.Thronebreaker),
            F("A1-NO1",EncounterTier.Standard,EncounterContent.Neutral,1,NeutralContent.UnboundBlade,NeutralContent.Fateworn),
            F("A1-NO2",EncounterTier.Standard,EncounterContent.Neutral,1,NeutralContent.Fateworn,NeutralContent.StrayIdol),
            F("A1-NO3",EncounterTier.Dangerous,EncounterContent.Neutral,1,NeutralContent.UnboundBlade,NeutralContent.Fateworn,NeutralContent.StrayIdol),
            F("A2-NO1",EncounterTier.Standard,EncounterContent.Neutral,2,NeutralContent.RaggedVanguard,NeutralContent.CrookedOracle),
            F("A2-NO2",EncounterTier.Advanced,EncounterContent.Neutral,2,NeutralContent.ShiftingHusk,NeutralContent.RaggedVanguard),
            F("A2-NO3",EncounterTier.Dangerous,EncounterContent.Neutral,2,NeutralContent.RaggedVanguard,NeutralContent.ShiftingHusk,NeutralContent.CrookedOracle),
            F("A3-NO1",EncounterTier.Standard,EncounterContent.Neutral,3,NeutralContent.IronWanderer,NeutralContent.NamelessSeer),
            F("A3-NO2",EncounterTier.Advanced,EncounterContent.Neutral,3,NeutralContent.PaleChimera,NeutralContent.IronWanderer),
            F("A3-NO3",EncounterTier.Dangerous,EncounterContent.Neutral,3,NeutralContent.IronWanderer,NeutralContent.PaleChimera,NeutralContent.NamelessSeer),
        };
        private static EncounterDef F(string id,EncounterTier tier,string theme,int act,params string[] enemies)=>new(id,tier,act,act,enemies){theme=theme};
    }
}
