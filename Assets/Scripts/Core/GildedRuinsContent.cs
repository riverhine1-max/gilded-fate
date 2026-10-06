using System;
using System.Linq;

namespace GildedFate.Core
{
    // Act 1 · Theme 1: Gilded Ruins. Identity: SEIZED WEALTH + FORMATION PRIORITY.
    // A dead kingdom whose machines, guards, servants and laws still run. Some creatures Seize your Gold
    // (they hold it until they die), some guard formations, some summon or react to what you do.
    // Rules live in Combat/RuinsCombat.cs; this file is data only.
    public static class GildedRuinsContent
    {
        public const string Theme=ActThemes.GildedRuins;
        public const string Scavenger="gilded_ruins_giltblade_scavenger",Bastion="gilded_ruins_oathbound_bastion",Chorister="gilded_ruins_gilded_chorister",
            Appraiser="gilded_ruins_tarnished_appraiser",Keeper="gilded_ruins_reliquary_keeper",Servitor="gilded_ruins_gilded_servitor",
            Mimic="gilded_ruins_coin_mimic",Herald="gilded_ruins_bellbound_herald",Duelist="gilded_ruins_crownless_duelist",
            Collector="gilded_ruins_crown_collector",Guard="gilded_ruins_coinbound_guard",Auctioneer="gilded_ruins_royal_auctioneer",
            Treasury="gilded_ruins_living_treasury",Procession="gilded_ruins_last_procession";

        private static EnemyDef N(string id,string name,int hp,int dmg,string text,bool elite=false,bool boss=false,bool minion=false,bool support=false)
            =>new(id,name,hp,dmg,text,elite,boss){theme=Theme,minion=minion,support=support};
        // HP is the solo/base value. Normals in a multi-enemy formation start at ~90%.
        public static readonly EnemyDef[] Enemies={
            N(Scavenger,"GILTBLADE SCAVENGER",34,7,"Slashes, pockets your Gold, then cuts desperately. It holds your Gold until it dies."),
            N(Bastion,"OATHBOUND BASTION",52,7,"Sworn to hold the line. It shields the weakest of its allies, or itself when alone."),
            N(Chorister,"GILDED CHORISTER",38,0,"A pure Support. It sings Strength and Block into its allies and never attacks.",support:true),
            N(Appraiser,"TARNISHED APPRAISER",44,6,"Weighs your Gold. If you are rich it seizes some; if you are poor it strikes.") ,
            N(Keeper,"RELIQUARY KEEPER",50,8,"Awakens Gilded Servitors, mends them and commands them. At most two."),
            N(Servitor,"GILDED SERVITOR",16,5,"The Reliquary Keeper's Minion. It jabs, then braces.",minion:true),
            N(Mimic,"COIN MIMIC",48,12,"Guards a hoard of Bonus Gold. What it has not consumed is yours when it dies."),
            N(Herald,"BELLBOUND HERALD",46,5,"Tolls three times: Rally, Dissonance, Grand Bellstrike."),
            N(Duelist,"CROWNLESS DUELIST",58,10,"Reads the cards you played last turn and answers in kind."),
            N(Collector,"THE CROWN COLLECTOR",98,10,"Collects what is due with two Coinbound Guards. Its Foreclosure grows with the Gold it holds.",elite:true),
            N(Guard,"COINBOUND GUARD",23,7,"The Crown Collector's Minion. It cuts, then guards its Collector.",minion:true),
            N(Auctioneer,"THE ROYAL AUCTIONEER",94,12,"Announces an upcoming Lot. You can see it, and you cannot outbid it.",elite:true),
            N(Treasury,"THE LIVING TREASURY",108,14,"Every Attack that wounds it fills its Reserve. A full Reserve is released all at once.",elite:true),
            N(Procession,"THE LAST PROCESSION",210,11,"A royal ceremony that never ended. Each phase tears away more of the pageantry.",boss:true),
        };
        public static EnemyDef Find(string id)=>Array.Find(Enemies,e=>e.id==id);
        public static readonly string[] Elites={Collector,Auctioneer,Treasury};
        public static readonly string[] Bosses={Procession};
        public static string[] StartingMinions(string id)=>id==Collector?new[]{Guard,Guard}:Array.Empty<string>();

        public static readonly EncounterDef[] Formations={
            F("GR-E01",EncounterTier.Easy,Scavenger),
            F("GR-E02",EncounterTier.Easy,Scavenger,Scavenger),
            F("GR-E03",EncounterTier.Easy,Bastion),
            F("GR-E04",EncounterTier.Easy,Mimic),
            F("GR-E05",EncounterTier.Easy,Appraiser),
            F("GR-E06",EncounterTier.Easy,Herald),
            F("GR-S01",EncounterTier.Standard,Scavenger,Bastion),
            F("GR-S02",EncounterTier.Standard,Scavenger,Chorister),
            F("GR-S03",EncounterTier.Standard,Appraiser,Scavenger),
            F("GR-S04",EncounterTier.Standard,Keeper,Servitor),
            F("GR-S05",EncounterTier.Standard,Mimic,Scavenger),
            F("GR-S06",EncounterTier.Standard,Duelist),
            F("GR-S07",EncounterTier.Standard,Herald,Scavenger),
            F("GR-S08",EncounterTier.Standard,Bastion,Herald),
            F("GR-S09",EncounterTier.Standard,Appraiser,Bastion),
            F("GR-S10",EncounterTier.Standard,Keeper),
            F("GR-D01",EncounterTier.Dangerous,Bastion,Chorister,Scavenger),
            F("GR-D02",EncounterTier.Dangerous,Appraiser,Scavenger,Scavenger),
            F("GR-D03",EncounterTier.Dangerous,Keeper,Bastion),
            F("GR-D04",EncounterTier.Dangerous,Mimic,Appraiser,Scavenger),
            F("GR-D05",EncounterTier.Dangerous,Duelist,Chorister),
            F("GR-D06",EncounterTier.Dangerous,Herald,Bastion,Scavenger),
            F("GR-D07",EncounterTier.Dangerous,Appraiser,Chorister,Duelist),
            F("GR-D08",EncounterTier.Dangerous,Keeper,Herald),
            F("GR-D09",EncounterTier.Dangerous,Bastion,Scavenger,Scavenger),
            F("GR-D10",EncounterTier.Dangerous,Mimic,Herald,Appraiser),
        };
        private static EncounterDef F(string id,EncounterTier tier,params string[] enemies)=>new(id,tier,1,1,enemies){theme=Theme};
    }
}
