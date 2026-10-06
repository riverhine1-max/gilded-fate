using System;
using System.Linq;

namespace GildedFate.Core
{
    // Act 3 · Theme 2: Fractured Realm. Identity: FRACTURE (reality repeating, splitting, copying and delaying).
    // Every enemy uses its own version of the distortion; none of it is hidden. Rules live in Combat/FractureCombat.cs;
    // this file is data only.
    public static class FracturedRealmContent
    {
        public const string Theme=ActThemes.FracturedRealm;
        public const string Soldier="fractured_realm_echo_soldier",Splitling="fractured_realm_splitling",SplitEcho="fractured_realm_split_echo",
            Husk="fractured_realm_mirror_husk",Binder="fractured_realm_rift_binder",Shear="fractured_realm_time_shear",
            Beast="fractured_realm_phase_beast",Oracle="fractured_realm_broken_oracle",Colossus="fractured_realm_rift_colossus",
            Duplicate="fractured_realm_duplicate",Sovereign="fractured_realm_split_sovereign",BladeFragment="fractured_realm_blade_fragment",
            WardFragment="fractured_realm_ward_fragment",Loopkeeper="fractured_realm_loopkeeper",Unmade="fractured_realm_the_unmade";

        private static EnemyDef N(string id,string name,int hp,int dmg,string text,bool elite=false,bool boss=false,bool minion=false,bool support=false)
            =>new(id,name,hp,dmg,text,elite,boss){theme=Theme,minion=minion,support=support};
        // HP is the solo/base value. Normals in a multi-enemy formation start at ~94%.
        public static readonly EnemyDef[] Enemies={
            N(Soldier,"ECHO SOLDIER",84,15,"Slashes, guards, then repeats its previous action exactly. The repeat is always shown."),
            N(Splitling,"SPLITLING",74,13,"At half health it splits into two Split Echoes. It does not die, heal or grow stronger."),
            N(SplitEcho,"SPLIT ECHO",24,8,"The Splitling's Minion. A thin copy that claws and flickers.",minion:true),
            N(Husk,"MIRROR HUSK",81,14,"After your turn it copies up to 2 stacks of Strength or Fortify you gained."),
            N(Binder,"RIFT BINDER",70,0,"A pure Support. It duplicates its allies' Strength and Block, and never attacks or fights alone.",support:true),
            N(Shear,"TIME-SHEAR",79,12,"Some of its attacks leave an Echo that lands on its next action. The Echo is always shown."),
            N(Beast,"PHASE BEAST",90,16,"Cycles Solid, Flicker and Rift: offense, control, burst."),
            N(Oracle,"BROKEN ORACLE",82,17,"Always shows exactly two possible actions. One is chosen when it acts."),
            N(Colossus,"RIFT COLOSSUS",104,15,"Whole, then Cracked, then Fragmented as it is hurt. Each stage hits differently."),
            N(Duplicate,"THE DUPLICATE",205,18,"Every second action repeats the one before it at 125%.",elite:true),
            N(Sovereign,"THE SPLIT SOVEREIGN",190,17,"Splits off a Blade Fragment and a Ward Fragment as it is hurt.",elite:true),
            N(BladeFragment,"BLADE FRAGMENT",32,11,"The Split Sovereign's Minion. It cuts, then strikes twice.",minion:true),
            N(WardFragment,"WARD FRAGMENT",36,7,"The Split Sovereign's Minion. It shields the Sovereign.",minion:true),
            N(Loopkeeper,"THE LOOPKEEPER",212,16,"Performs a three-action sequence, then replays it exactly once. The replay is shown.",elite:true),
            N(Unmade,"THE UNMADE",410,18,"Reality collapsing into a living thing. Memory, Echo and two-way outcomes escalate across three forms.",boss:true),
        };
        public static EnemyDef Find(string id)=>Array.Find(Enemies,e=>e.id==id);
        public static readonly string[] Elites={Duplicate,Sovereign,Loopkeeper};
        public static readonly string[] Bosses={Unmade};
        public static string[] StartingMinions(string id)=>Array.Empty<string>();

        // Standard / Advanced / Dangerous. Act 3 has no protected opening fights.
        public static readonly EncounterDef[] Formations={
            F("FR-S01",EncounterTier.Standard,Soldier,Husk),
            F("FR-S02",EncounterTier.Standard,Splitling,Soldier),
            F("FR-S03",EncounterTier.Standard,Shear),
            F("FR-S04",EncounterTier.Standard,Beast),
            F("FR-S05",EncounterTier.Standard,Oracle),
            F("FR-S06",EncounterTier.Standard,Colossus),
            F("FR-S07",EncounterTier.Standard,Soldier,Binder),
            F("FR-S08",EncounterTier.Standard,Husk,Shear),
            F("FR-S09",EncounterTier.Standard,Splitling,Husk),
            F("FR-S10",EncounterTier.Standard,Beast,Soldier),
            F("FR-A01",EncounterTier.Advanced,Soldier,Binder,Shear),
            F("FR-A02",EncounterTier.Advanced,Splitling,Beast),
            F("FR-A03",EncounterTier.Advanced,Husk,Binder),
            F("FR-A04",EncounterTier.Advanced,Oracle,Soldier),
            F("FR-A05",EncounterTier.Advanced,Colossus,Binder),
            F("FR-A06",EncounterTier.Advanced,Shear,Beast),
            F("FR-A07",EncounterTier.Advanced,Splitling,Husk,Soldier),
            F("FR-A08",EncounterTier.Advanced,Oracle,Shear),
            F("FR-A09",EncounterTier.Advanced,Colossus,Soldier),
            F("FR-A10",EncounterTier.Advanced,Beast,Binder),
            F("FR-D01",EncounterTier.Dangerous,Oracle,Shear,Binder),
            F("FR-D02",EncounterTier.Dangerous,Colossus,Husk),
            F("FR-D03",EncounterTier.Dangerous,Splitling,Beast,Soldier),
            F("FR-D04",EncounterTier.Dangerous,Oracle,Husk),
            F("FR-D05",EncounterTier.Dangerous,Shear,Colossus),
            F("FR-D06",EncounterTier.Dangerous,Splitling,Binder,Husk),
            F("FR-D07",EncounterTier.Dangerous,Beast,Oracle),
            F("FR-D08",EncounterTier.Dangerous,Colossus,Binder),
            F("FR-D09",EncounterTier.Dangerous,Soldier,Shear,Oracle),
            F("FR-D10",EncounterTier.Dangerous,Splitling,Colossus),
        };
        private static EncounterDef F(string id,EncounterTier tier,params string[] enemies)=>new(id,tier,3,3,enemies){theme=Theme};
    }
}
