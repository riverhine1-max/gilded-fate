using System;
using System.Linq;

namespace GildedFate.Core
{
    // Act 2 · Theme 1: The Crimson Foundry. Identity: Heat. Rules live in
    // Combat/FoundryCombat.cs; this file is data only.
    public static class CrimsonFoundryContent
    {
        public const string Theme=ActThemes.CrimsonFoundry;
        public const string Forgehand="crimson_foundry_forgehand",Hound="crimson_foundry_furnace_hound",RivetPriest="crimson_foundry_rivet_priest",
            ChainWarden="crimson_foundry_chain_warden",AssemblyMaster="crimson_foundry_assembly_master",ScrapDrone="crimson_foundry_scrap_drone",
            Knight="crimson_foundry_crucible_knight",Carrier="crimson_foundry_molten_carrier",Automaton="crimson_foundry_redline_automaton",
            Forgemaster="crimson_foundry_forgemaster",HammerDrone="crimson_foundry_hammer_drone",ShieldDrone="crimson_foundry_shield_drone",
            Smelter="crimson_foundry_smelter",Prototype="crimson_foundry_prototype_zero",Saint="crimson_foundry_iron_saint",
            Servitor="crimson_foundry_saint_servitor";

        private static EnemyDef N(string id,string name,int hp,int dmg,string text,bool elite=false,bool boss=false,bool minion=false,bool support=false)
            =>new(id,name,hp,dmg,text,elite,boss){theme=Theme,minion=minion,support=support};
        // HP is the solo/base value. Normals in a multi-enemy formation start at ~92% (Act 2).
        public static readonly EnemyDef[] Enemies={
            N(Forgehand,"FORGEHAND",54,10,"A foundry worker that heats up as it fights, then vents."),
            N(Hound,"FURNACE HOUND",50,9,"Runs hot fast. At full Heat its Redline Pounce hits hardest."),
            N(RivetPriest,"RIVET PRIEST",48,0,"A pure Support. It rivets armor onto allies and stokes their furnaces. It never attacks.",support:true),
            N(ChainWarden,"CHAIN WARDEN",61,10,"Binds and drags its prey with chains: Weak, then Vulnerable."),
            N(AssemblyMaster,"ASSEMBLY MASTER",59,9,"Deploys and repairs Scrap Drones, then directs them."),
            N(ScrapDrone,"SCRAP DRONE",20,6,"An Assembly Master's Minion. It shuts down when its master dies.",minion:true),
            N(Knight,"CRUCIBLE KNIGHT",72,10,"Defensive while cold. Hot, it turns aggressive; at full Heat it breaks you."),
            N(Carrier,"MOLTEN CARRIER",64,11,"Loads its crucible, then spills it all at once."),
            N(Automaton,"REDLINE AUTOMATON",70,10,"Its Overdrive Assault grows with Heat. At full Heat it strikes twice, then must vent."),
            N(Forgemaster,"THE FORGEMASTER",148,14,"Commands a Hammer Drone and a Shield Drone. It can rebuild one of them once.",elite:true),
            N(HammerDrone,"HAMMER DRONE",27,9,"The Forgemaster's Minion. Heavy hammer strikes.",minion:true),
            N(ShieldDrone,"SHIELD DRONE",30,6,"The Forgemaster's Minion. It shields its master.",minion:true),
            N(Smelter,"THE SMELTER",162,13,"Burns away its own armor layer by layer. The core beneath is far deadlier.",elite:true),
            N(Prototype,"PROTOTYPE ZERO",154,17,"Cycles Assault, Defense and Overdrive modes, two actions each.",elite:true),
            N(Saint,"THE IRON SAINT",300,14,"The Foundry's sacred war machine. Each phase breaks it further loose.",boss:true),
            N(Servitor,"SAINT SERVITOR",32,8,"The Iron Saint's Minion. It serves and shields the Saint.",minion:true),
        };
        public static EnemyDef Find(string id)=>Array.Find(Enemies,e=>e.id==id);
        public static readonly string[] Elites={Forgemaster,Smelter,Prototype};
        public static readonly string[] Bosses={Saint};
        public static string[] StartingMinions(string id)=>id switch
        {
            Forgemaster=>new[]{HammerDrone,ShieldDrone},Saint=>new[]{Servitor},_=>Array.Empty<string>()
        };
        // Enemies that use the shared Heat counter (0–4).
        public static readonly string[] HeatUsers={Forgehand,Hound,Knight,Automaton,Forgemaster,Saint};
        public static bool UsesHeat(string id)=>HeatUsers.Contains(id);
        // Heat a creature starts combat with.
        public static int StartingHeat(string id)=>id switch{Hound=>1,Automaton=>1,Forgemaster=>1,_=>0};

        public static readonly EncounterDef[] Formations={
            F("CF-S01",EncounterTier.Standard,Forgehand,Hound),
            F("CF-S02",EncounterTier.Standard,Forgehand,ChainWarden),
            F("CF-S03",EncounterTier.Standard,Knight),
            F("CF-S04",EncounterTier.Standard,AssemblyMaster,ScrapDrone),
            F("CF-S05",EncounterTier.Standard,Carrier,Forgehand),
            F("CF-S06",EncounterTier.Standard,Automaton),
            F("CF-S07",EncounterTier.Standard,Hound,ChainWarden),
            F("CF-S08",EncounterTier.Standard,Forgehand,RivetPriest),
            F("CF-S09",EncounterTier.Standard,Knight,Forgehand),
            F("CF-S10",EncounterTier.Standard,Carrier),
            F("CF-A01",EncounterTier.Advanced,Forgehand,Hound,RivetPriest),
            F("CF-A02",EncounterTier.Advanced,Knight,ChainWarden),
            F("CF-A03",EncounterTier.Advanced,AssemblyMaster,Forgehand),
            F("CF-A04",EncounterTier.Advanced,Automaton,RivetPriest),
            F("CF-A05",EncounterTier.Advanced,Carrier,ChainWarden),
            F("CF-A06",EncounterTier.Advanced,Hound,Hound,Forgehand),
            F("CF-A07",EncounterTier.Advanced,Knight,RivetPriest),
            F("CF-A08",EncounterTier.Advanced,AssemblyMaster,Hound),
            F("CF-A09",EncounterTier.Advanced,Automaton,ChainWarden),
            F("CF-A10",EncounterTier.Advanced,Carrier,Hound),
            F("CF-D01",EncounterTier.Dangerous,Knight,Hound,RivetPriest),
            F("CF-D02",EncounterTier.Dangerous,Automaton,ChainWarden,Forgehand),
            F("CF-D03",EncounterTier.Dangerous,AssemblyMaster,Knight),
            F("CF-D04",EncounterTier.Dangerous,Carrier,RivetPriest,Hound),
            F("CF-D05",EncounterTier.Dangerous,Automaton,RivetPriest,ChainWarden),
            F("CF-D06",EncounterTier.Dangerous,Knight,Carrier),
            F("CF-D07",EncounterTier.Dangerous,AssemblyMaster,ChainWarden,Forgehand),
            F("CF-D08",EncounterTier.Dangerous,Forgehand,Forgehand,RivetPriest,Hound),
            F("CF-D09",EncounterTier.Dangerous,Automaton,Knight),
            F("CF-D10",EncounterTier.Dangerous,Carrier,Automaton),
        };
        private static EncounterDef F(string id,EncounterTier tier,params string[] enemies)=>new(id,tier,2,2,enemies){theme=Theme};
    }
}
