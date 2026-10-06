using System;
using System.Linq;

namespace GildedFate.Core
{
    // Act themes. Each act has up to three themes with their own roster, elites and boss.
    // A run rolls one implemented theme per act at the start (RunModel.actThemes).
    // "" / Vault is the original roster, used until a theme replaces it.
    public static class ActThemes
    {
        public const string Vault="";
        public const string GildedRuins="gilded_ruins",AshenWilds="ashen_wilds",DrownedQuarter="drowned_quarter",CrimsonFoundry="crimson_foundry",Hollowwood="hollowwood",ShatteredObservatory="shattered_observatory",BlackCathedral="black_cathedral";
        // Implemented themes per act. Gilded Ruins is Act 1's first theme; the original Vault roster
        // still serves Acts 2 and 3 until their themes exist. Act 2 rolls the Vault or one of three themes.
        public static string[] ForAct(int act)=>act switch
        {
            1=>new[]{GildedRuins,AshenWilds,DrownedQuarter},
            2=>new[]{Vault,CrimsonFoundry,Hollowwood,ShatteredObservatory},
            _=>new[]{Vault,BlackCathedral}
        };
        public static string Name(string theme)=>theme switch{GildedRuins=>"THE GILDED RUINS",AshenWilds=>"ASHEN WILDS",DrownedQuarter=>"THE DROWNED QUARTER",CrimsonFoundry=>"THE CRIMSON FOUNDRY",Hollowwood=>"HOLLOWWOOD",ShatteredObservatory=>"THE SHATTERED OBSERVATORY",BlackCathedral=>"THE BLACK CATHEDRAL",_=>"THE GILDED VAULT"};
        public static string Tagline(string theme)=>theme switch
        {
            GildedRuins=>"A dead kingdom whose systems still run · Seized Wealth",
            AshenWilds=>"A burned wilderness fighting to regrow · Pack Instinct",
            DrownedQuarter=>"A sinking city district · Delayed threats you can see coming",
            CrimsonFoundry=>"A war factory that never stopped · Heat",
            Hollowwood=>"A forest that grows through everything · Growth",
            ShatteredObservatory=>"A broken tower that reads the future · Prediction",
            BlackCathedral=>"A cathedral of sentence and ritual · Judgment",
            _=>"The vault's original keepers"
        };
        public static string Roll(int act,int seed)
        {
            var options=ForAct(act);var hash=unchecked((uint)seed*2246822519u^(uint)(act*374761393));hash^=hash>>15;hash*=2654435761u;hash^=hash>>13;
            return options[hash%(uint)options.Length];
        }
    }

    public static class AshenWildsContent
    {
        public const string Theme=ActThemes.AshenWilds;
        public const string Cinderfang="ashen_wilds_cinderfang",Grazer="ashen_wilds_barkhide_grazer",Emberwing="ashen_wilds_emberwing",
            Thornjaw="ashen_wilds_thornjaw",Stalker="ashen_wilds_ash_stalker",Rootcaller="ashen_wilds_rootcaller",Sapling="ashen_wilds_ash_sapling",
            Elk="ashen_wilds_mourning_elk",Maw="ashen_wilds_hollow_maw",Packmother="ashen_wilds_packmother",FangPup="ashen_wilds_fang_pup",
            AshbackCub="ashen_wilds_ashback_cub",Hart="ashen_wilds_burned_hart",Titan="ashen_wilds_root_titan",Alpha="ashen_wilds_cinder_alpha",
            Whelp="ashen_wilds_cinder_whelp",Runner="ashen_wilds_ember_runner";

        private static EnemyDef N(string id,string name,int hp,int dmg,string text,bool elite=false,bool boss=false,bool minion=false,bool support=false)
            =>new(id,name,hp,dmg,text,elite,boss){theme=Theme,minion=minion,support=support};
        // HP is the solo/base value. Normals in a multi-enemy formation start at ~90%.
        public static readonly EnemyDef[] Enemies={
            N(Cinderfang,"CINDERFANG",36,7,"A lean predator. It pounces harder when a packmate is wounded."),
            N(Grazer,"BARKHIDE GRAZER",58,9,"Bark-armored and stubborn. It shelters the whole herd."),
            N(Emberwing,"EMBERWING",37,0,"A support beast that never attacks. It keeps wounded pack members alive.",support:true),
            N(Thornjaw,"THORNJAW",47,8,"Coils its brambles, then releases them in a burst of thorns."),
            N(Stalker,"ASH STALKER",43,7,"An opportunist. It strikes hardest at weakened prey."),
            N(Rootcaller,"ROOTCALLER",49,7,"Grows Ash Saplings from the burned soil and directs them."),
            N(Sapling,"ASH SAPLING",15,5,"A Rootcaller's Minion. It withers when its Rootcaller dies.",minion:true),
            N(Elk,"MOURNING ELK",55,10,"When a packmate falls, its next action is a mournful cry."),
            N(Maw,"HOLLOW MAW",65,10,"Hungry, then fed, then burning. Then hungry again."),
            N(Packmother,"THE PACKMOTHER",106,14,"Leads two cubs. Kill her pack and she fights with bereaved fury.",elite:true),
            N(FangPup,"FANG PUP",20,6,"The Packmother's Minion. A quick, frenzied biter.",minion:true),
            N(AshbackCub,"ASHBACK CUB",24,5,"The Packmother's Minion. It shields its mother.",minion:true),
            N(Hart,"THE BURNED HART",118,13,"Its burning crown breaks apart as it is wounded, and it grows wilder.",elite:true),
            N(Titan,"THE ROOT TITAN",126,12,"Rooted, it is a wall. Uprooted, it is a landslide.",elite:true),
            N(Alpha,"THE CINDER ALPHA",225,13,"Leader of the burned pack. Its fire grows with every wound.",boss:true),
            N(Whelp,"CINDER WHELP",22,7,"The Cinder Alpha's Minion.",minion:true),
            N(Runner,"EMBER RUNNER",20,5,"The Cinder Alpha's Minion. Fast, and it weakens prey.",minion:true),
        };
        public static EnemyDef Find(string id)=>Array.Find(Enemies,e=>e.id==id);
        public static readonly string[] Elites={Packmother,Hart,Titan};
        public static readonly string[] Bosses={Alpha};
        // Minions an owner starts combat with (added automatically after the owner).
        public static string[] StartingMinions(string id)=>id switch
        {
            Packmother=>new[]{FangPup,AshbackCub},Alpha=>new[]{Whelp,Runner},_=>Array.Empty<string>()
        };
        public static bool IsMinion(string id)=>Find(id)?.minion==true;

        // Authored formations. Easy = the first two normal combats of the act (still
        // eligible later), Standard after that, Dangerous deeper into the act.
        public static readonly EncounterDef[] Formations={
            F("AW-E01",EncounterTier.Easy,Cinderfang),
            F("AW-E02",EncounterTier.Easy,Cinderfang,Cinderfang),
            F("AW-E03",EncounterTier.Easy,Grazer),
            F("AW-E04",EncounterTier.Easy,Thornjaw),
            F("AW-E05",EncounterTier.Easy,Stalker),
            F("AW-E06",EncounterTier.Easy,Elk),
            F("AW-S01",EncounterTier.Standard,Cinderfang,Grazer),
            F("AW-S02",EncounterTier.Standard,Cinderfang,Emberwing),
            F("AW-S03",EncounterTier.Standard,Stalker,Cinderfang),
            F("AW-S04",EncounterTier.Standard,Rootcaller,Sapling),
            F("AW-S05",EncounterTier.Standard,Thornjaw,Cinderfang),
            F("AW-S06",EncounterTier.Standard,Maw),
            F("AW-S07",EncounterTier.Standard,Elk,Cinderfang),
            F("AW-S08",EncounterTier.Standard,Grazer,Emberwing),
            F("AW-S09",EncounterTier.Standard,Stalker,Grazer),
            F("AW-S10",EncounterTier.Standard,Rootcaller),
            F("AW-D01",EncounterTier.Dangerous,Grazer,Emberwing,Cinderfang),
            F("AW-D02",EncounterTier.Dangerous,Stalker,Cinderfang,Cinderfang),
            F("AW-D03",EncounterTier.Dangerous,Rootcaller,Grazer),
            F("AW-D04",EncounterTier.Dangerous,Thornjaw,Stalker,Cinderfang),
            F("AW-D05",EncounterTier.Dangerous,Elk,Emberwing,Cinderfang),
            F("AW-D06",EncounterTier.Dangerous,Maw,Emberwing),
            F("AW-D07",EncounterTier.Dangerous,Stalker,Elk),
            F("AW-D08",EncounterTier.Dangerous,Rootcaller,Thornjaw),
            F("AW-D09",EncounterTier.Dangerous,Grazer,Elk,Cinderfang),
            F("AW-D10",EncounterTier.Dangerous,Thornjaw,Emberwing,Stalker),
        };
        private static EncounterDef F(string id,EncounterTier tier,params string[] enemies)=>new(id,tier,1,1,enemies){theme=Theme};
    }
}
