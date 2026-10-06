using System;
using System.Linq;

namespace GildedFate.Core
{
    // Act 3 · Theme 3: Gilded Throne. Identity: ROYAL ORDER (perfected authority, a disciplined palace army).
    // Royal Order is NOT one universal status. Each enemy defines its own relationship with its allies, Minions or
    // isolation. Nothing intercepts player attacks; protection is always visible Block, buffs and formation behavior.
    // Rules live in Combat/ThroneCombat.cs; this file is data only.
    public static class GildedThroneContent
    {
        public const string Theme=ActThemes.GildedThrone;
        public const string Vanguard="gilded_throne_royal_vanguard",Crownshield="gilded_throne_crownshield",Adjudicator="gilded_throne_royal_adjudicator",
            Strategist="gilded_throne_court_strategist",Commander="gilded_throne_gilded_commander",Guard="gilded_throne_royal_guard",
            Beast="gilded_throne_treasury_beast",Duelist="gilded_throne_crown_duelist",Thronebreaker="gilded_throne_thronebreaker",
            General="gilded_throne_royal_general",Warden="gilded_throne_treasury_warden",Duelmaster="gilded_throne_crown_duelmaster",
            Sovereign="gilded_throne_the_sovereign",Blade="gilded_throne_royal_blade",Shield="gilded_throne_royal_shield";

        private static EnemyDef N(string id,string name,int hp,int dmg,string text,bool elite=false,bool boss=false,bool minion=false,bool support=false)
            =>new(id,name,hp,dmg,text,elite,boss){theme=Theme,minion=minion,support=support};
        // HP is the solo/base value. Normals in a multi-enemy formation start at ~94%.
        public static readonly EnemyDef[] Enemies={
            N(Vanguard,"ROYAL VANGUARD",86,15,"The palace's frontline soldier. Advances, guards, cleaves and drills."),
            N(Crownshield,"CROWNSHIELD",96,11,"Extends its formation's durability with visible Block. It never redirects your attacks."),
            N(Adjudicator,"ROYAL ADJUDICATOR",80,16,"Declares you unworthy. Weak and Vulnerable, then the sentence."),
            N(Strategist,"COURT STRATEGIST",72,0,"A pure Support. Orders, Block and Strength for the royal formation. Never fights alone.",support:true),
            N(Commander,"GILDED COMMANDER",85,14,"Deploys Royal Guards, drills them and commands them."),
            N(Guard,"ROYAL GUARD",29,9,"A Commander's Minion. Spears, then shields its Owner.",minion:true),
            N(Beast,"TREASURY BEAST",93,13,"Fights on the palace's own Reserve. This is not your Gold."),
            N(Duelist,"CROWN DUELIST",88,16,"Answers how you played last turn. Its response is always shown."),
            N(Thronebreaker,"THRONEBREAKER",108,12,"A siege machine. Siege counts down in plain view to a huge Charge."),
            N(General,"THE ROYAL GENERAL",208,18,"Leads two Royal Guards. Formation, Command, one reinforcement and the Execution.",elite:true),
            N(Warden,"THE TREASURY WARDEN",224,14,"Builds a Royal Reserve and spends it on strikes, fortresses and barrages.",elite:true),
            N(Duelmaster,"THE CROWN DUELMASTER",216,20,"Three stances, two actions each: King's Edge, Royal Guard, Execution.",elite:true),
            N(Sovereign,"THE SOVEREIGN",430,17,"Perfected royal authority. Seated, then rising, then standing alone.",boss:true),
            N(Blade,"ROYAL BLADE",36,11,"The Sovereign's Minion. Withdraws when the Sovereign rises.",minion:true),
            N(Shield,"ROYAL SHIELD",40,7,"The Sovereign's Minion. Guards the throne. Withdraws when the Sovereign rises.",minion:true),
        };
        public static EnemyDef Find(string id)=>Array.Find(Enemies,e=>e.id==id);
        public static readonly string[] Elites={General,Warden,Duelmaster};
        public static readonly string[] Bosses={Sovereign};
        public static string[] StartingMinions(string id)=>id==General?new[]{Guard,Guard}:id==Sovereign?new[]{Blade,Shield}:Array.Empty<string>();

        // Standard / Advanced / Dangerous. Act 3 has no protected opening fights.
        public static readonly EncounterDef[] Formations={
            F("GT-S01",EncounterTier.Standard,Vanguard,Adjudicator),
            F("GT-S02",EncounterTier.Standard,Vanguard,Crownshield),
            F("GT-S03",EncounterTier.Standard,Beast),
            F("GT-S04",EncounterTier.Standard,Commander,Guard),
            F("GT-S05",EncounterTier.Standard,Duelist),
            F("GT-S06",EncounterTier.Standard,Thronebreaker),
            F("GT-S07",EncounterTier.Standard,Vanguard,Strategist),
            F("GT-S08",EncounterTier.Standard,Crownshield,Adjudicator),
            F("GT-S09",EncounterTier.Standard,Beast,Vanguard),
            F("GT-S10",EncounterTier.Standard,Adjudicator,Duelist),
            F("GT-A01",EncounterTier.Advanced,Vanguard,Crownshield,Strategist),
            F("GT-A02",EncounterTier.Advanced,Adjudicator,Duelist),
            F("GT-A03",EncounterTier.Advanced,Commander,Vanguard),
            F("GT-A04",EncounterTier.Advanced,Beast,Crownshield),
            F("GT-A05",EncounterTier.Advanced,Thronebreaker,Adjudicator),
            F("GT-A06",EncounterTier.Advanced,Duelist,Strategist),
            F("GT-A07",EncounterTier.Advanced,Commander,Adjudicator),
            F("GT-A08",EncounterTier.Advanced,Vanguard,Beast),
            F("GT-A09",EncounterTier.Advanced,Crownshield,Duelist),
            F("GT-A10",EncounterTier.Advanced,Thronebreaker,Strategist),
            F("GT-D01",EncounterTier.Dangerous,Vanguard,Crownshield,Strategist),
            F("GT-D02",EncounterTier.Dangerous,Commander,Duelist),
            F("GT-D03",EncounterTier.Dangerous,Beast,Adjudicator,Vanguard),
            F("GT-D04",EncounterTier.Dangerous,Thronebreaker,Crownshield),
            F("GT-D05",EncounterTier.Dangerous,Commander,Strategist,Vanguard),
            F("GT-D06",EncounterTier.Dangerous,Duelist,Adjudicator,Strategist),
            F("GT-D07",EncounterTier.Dangerous,Beast,Crownshield,Adjudicator),
            F("GT-D08",EncounterTier.Dangerous,Thronebreaker,Vanguard,Strategist),
            F("GT-D09",EncounterTier.Dangerous,Commander,Beast),
            F("GT-D10",EncounterTier.Dangerous,Duelist,Thronebreaker),
        };
        private static EncounterDef F(string id,EncounterTier tier,params string[] enemies)=>new(id,tier,3,3,enemies){theme=Theme};
    }
}
