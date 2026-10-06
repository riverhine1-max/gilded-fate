using System;
using System.Linq;

namespace GildedFate.Core
{
    // Act 2 · Theme 2: Hollowwood. Identity: Growth (an enemy-specific counter, 0–3).
    // Rules live in Combat/HollowwoodCombat.cs; this file is data only.
    public static class HollowwoodContent
    {
        public const string Theme=ActThemes.Hollowwood;
        public const string Sproutling="hollowwood_sproutling",Stag="hollowwood_hollow_stag",Sporekeeper="hollowwood_sporekeeper",
            RootSnare="hollowwood_root_snare",BroodPod="hollowwood_brood_pod",Sporeling="hollowwood_sporeling",Bloomfang="hollowwood_bloomfang",
            Parasite="hollowwood_hollow_parasite",ElderHusk="hollowwood_elder_husk",
            GardenMother="hollowwood_garden_mother",Thornbud="hollowwood_thornbud",Bloombud="hollowwood_bloombud",Huskbud="hollowwood_huskbud",
            WalkingGrove="hollowwood_walking_grove",PaleGardener="hollowwood_pale_gardener",Heartroot="hollowwood_heartroot";

        private static EnemyDef N(string id,string name,int hp,int dmg,string text,bool elite=false,bool boss=false,bool minion=false,bool support=false)
            =>new(id,name,hp,dmg,text,elite,boss){theme=Theme,minion=minion,support=support};
        // HP is the solo/base value. Normals in a multi-enemy formation start at ~92% (Act 2).
        public static readonly EnemyDef[] Enemies={
            N(Sproutling,"SPROUTLING",48,8,"A hollow sapling that grows with every action, then bursts into bloom."),
            N(Stag,"HOLLOW STAG",69,11,"Starts with a head start on Growth. At full Growth its crown blooms."),
            N(Sporekeeper,"SPOREKEEPER",50,0,"A pure Support. It feeds its allies' Growth and wraps them in spores. It never attacks.",support:true),
            N(RootSnare,"ROOT SNARE",65,10,"Entangles with Weak, tightens its roots, then crushes."),
            N(BroodPod,"BROOD POD",58,8,"Incubates until it hatches a Sporeling. At the Minion limit it hardens instead."),
            N(Sporeling,"SPORELING",19,6,"A Brood Pod's Minion. It withers when its Pod dies.",minion:true),
            N(Bloomfang,"BLOOMFANG",63,9,"A closed bud that grows with each action. At 3 Growth it permanently blooms."),
            N(Parasite,"HOLLOW PARASITE",62,11,"Fights through a host. When the host breaks, the parasite is exposed and far more dangerous."),
            N(ElderHusk,"ELDER HUSK",78,10,"Cycles Rooted, Blooming and Withered."),
            N(GardenMother,"THE GARDEN MOTHER",158,14,"Tends a Thornbud and a Bloombud, commands them and grows toward Grand Bloom.",elite:true),
            N(Thornbud,"THORNBUD",26,8,"The Garden Mother's Minion. It jabs and sprays thorns.",minion:true),
            N(Bloombud,"BLOOMBUD",24,0,"The Garden Mother's Minion. It shields and feeds its Mother.",minion:true),
            N(Huskbud,"HUSKBUD",30,6,"The Garden Mother's one replacement Minion. It hardens its Mother.",minion:true),
            N(WalkingGrove,"THE WALKING GROVE",178,14,"One creature that wakes in three stages: Root, Branch, then Crown.",elite:true),
            N(PaleGardener,"THE PALE GARDENER",160,13,"Plants a visible Seed. The Seed resolves on its next action and cannot be cancelled.",elite:true),
            N(Heartroot,"THE HEARTROOT",320,14,"The living heart of the wood. Its Growth blooms in three phases.",boss:true),
        };
        public static EnemyDef Find(string id)=>Array.Find(Enemies,e=>e.id==id);
        public static readonly string[] Elites={GardenMother,WalkingGrove,PaleGardener};
        public static readonly string[] Bosses={Heartroot};
        public static string[] StartingMinions(string id)=>id switch{GardenMother=>new[]{Thornbud,Bloombud},_=>Array.Empty<string>()};

        // Enemies that use the shared Growth counter (0–3).
        public static readonly string[] GrowthUsers={Sproutling,Stag,BroodPod,Bloomfang,GardenMother,Heartroot};
        public static bool UsesGrowth(string id)=>GrowthUsers.Contains(id);
        public static int StartingGrowth(string id)=>id switch{Stag=>1,GardenMother=>1,_=>0};

        public static readonly EncounterDef[] Formations={
            F("HW-S01",EncounterTier.Standard,Sproutling,Stag),
            F("HW-S02",EncounterTier.Standard,Sproutling,Sproutling),
            F("HW-S03",EncounterTier.Standard,RootSnare,Sproutling),
            F("HW-S04",EncounterTier.Standard,BroodPod,Sporeling),
            F("HW-S05",EncounterTier.Standard,Bloomfang),
            F("HW-S06",EncounterTier.Standard,Parasite),
            F("HW-S07",EncounterTier.Standard,Stag,Sporekeeper),
            F("HW-S08",EncounterTier.Standard,RootSnare,Stag),
            F("HW-S09",EncounterTier.Standard,ElderHusk),
            F("HW-S10",EncounterTier.Standard,Bloomfang,Sproutling),
            F("HW-A01",EncounterTier.Advanced,Sproutling,Sproutling,Sporekeeper),
            F("HW-A02",EncounterTier.Advanced,Stag,RootSnare),
            F("HW-A03",EncounterTier.Advanced,BroodPod,Sproutling),
            F("HW-A04",EncounterTier.Advanced,Bloomfang,Sporekeeper),
            F("HW-A05",EncounterTier.Advanced,Parasite,RootSnare),
            F("HW-A06",EncounterTier.Advanced,ElderHusk,Sproutling),
            F("HW-A07",EncounterTier.Advanced,Stag,Bloomfang),
            F("HW-A08",EncounterTier.Advanced,BroodPod,RootSnare),
            F("HW-A09",EncounterTier.Advanced,Parasite,Sporekeeper),
            F("HW-A10",EncounterTier.Advanced,ElderHusk,Stag),
            F("HW-D01",EncounterTier.Dangerous,Stag,Sporekeeper,Sproutling),
            F("HW-D02",EncounterTier.Dangerous,Bloomfang,RootSnare,Sporekeeper),
            F("HW-D03",EncounterTier.Dangerous,BroodPod,Stag),
            F("HW-D04",EncounterTier.Dangerous,Parasite,Bloomfang),
            F("HW-D05",EncounterTier.Dangerous,ElderHusk,Sporekeeper,Sproutling),
            F("HW-D06",EncounterTier.Dangerous,BroodPod,Bloomfang),
            F("HW-D07",EncounterTier.Dangerous,RootSnare,Stag,Sproutling),
            F("HW-D08",EncounterTier.Dangerous,Parasite,ElderHusk),
            F("HW-D09",EncounterTier.Dangerous,Sproutling,Sproutling,Bloomfang,Sporekeeper),
            F("HW-D10",EncounterTier.Dangerous,ElderHusk,RootSnare,Sporekeeper),
        };
        private static EncounterDef F(string id,EncounterTier tier,params string[] enemies)=>new(id,tier,2,2,enemies){theme=Theme};
    }
}
