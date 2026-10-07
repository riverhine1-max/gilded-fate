using System;
using System.Linq;

namespace GildedFate.Core
{
    // Act 3 · Theme 1: Black Cathedral. Identity: JUDGMENT.
    // Some creatures read what you did on your previous turn and pick their next move from it. Every Judgment is
    // a visible rule shown in the tooltip, and it only ever chooses the enemy's NEXT intent on a normal enemy turn.
    // Rules live in Combat/CathedralCombat.cs; this file is data only.
    public static class BlackCathedralContent
    {
        public const string Theme=ActThemes.BlackCathedral;
        public const string Guard="black_cathedral_penitent_guard",Warden="black_cathedral_choir_warden",Confessor="black_cathedral_confessor",
            Censer="black_cathedral_censer_bearer",Saint="black_cathedral_reliquary_saint",Effigy="black_cathedral_chapel_effigy",
            Herald="black_cathedral_execution_herald",Priest="black_cathedral_oathbreaker_priest",Icon="black_cathedral_living_icon",
            HighConfessor="black_cathedral_high_confessor",ChoirEternal="black_cathedral_choir_eternal",
            VoiceBlade="black_cathedral_voice_of_blade",VoiceMercy="black_cathedral_voice_of_mercy",VoiceVigil="black_cathedral_voice_of_vigil",
            Bell="black_cathedral_bell_of_sentence",Bishop="black_cathedral_final_bishop";

        private static EnemyDef N(string id,string name,int hp,int dmg,string text,bool elite=false,bool boss=false,bool minion=false,bool support=false)
            =>new(id,name,hp,dmg,text,elite,boss){theme=Theme,minion=minion,support=support};
        // HP is the solo/base value. Normals in a multi-enemy formation start at ~94%.
        public static readonly EnemyDef[] Enemies={
            N(Guard,"PENITENT GUARD",82,14,"A reliable zealot in iron. Strikes, kneels behind steel, advances, and grows harder."),
            N(Warden,"CHOIR WARDEN",68,0,"A pure Support. Its hymns grant Strength, Block and mercy to its allies. It never attacks and never fights alone.",support:true),
            N(Confessor,"CONFESSOR",76,18,"Judges how many Attack cards you played last turn and answers accordingly."),
            N(Censer,"CENSER BEARER",74,14,"Fills the air with bitter incense, weakening you before its allies strike."),
            N(Saint,"RELIQUARY SAINT",80,13,"Consecrates Chapel Effigies, heals them and commands them. At most two."),
            N(Effigy,"CHAPEL EFFIGY",26,8,"The Reliquary Saint's Minion. It strikes, then guards its Owner.",minion:true),
            N(Herald,"EXECUTION HERALD",84,28,"Proclaims a Sentence you can count down. When it reaches the end, the Execution falls."),
            N(Priest,"OATHBREAKER PRIEST",78,14,"Judges whether you leaned on one card type last turn."),
            N(Icon,"LIVING ICON",92,16,"Cycles through Mercy, Judgment and Wrath: defend, control, burst."),
            N(HighConfessor,"THE HIGH CONFESSOR",178,22,"Judges your last turn in five ways. The verdict is shown before it acts.",elite:true),
            N(ChoirEternal,"THE CHOIR ETERNAL",158,15,"Conducts three Voices: Blade, Mercy and Vigil. It does not replace the dead.",elite:true),
            N(VoiceBlade,"VOICE OF BLADE",27,10,"The Choir Eternal's Minion. A piercing verse that leaves you Vulnerable.",minion:true),
            N(VoiceMercy,"VOICE OF MERCY",25,0,"A Minion that restores and shelters its Owner.",minion:true),
            N(VoiceVigil,"VOICE OF VIGIL",29,6,"A Minion that guards the other Voices.",minion:true),
            N(Bell,"THE BELL OF SENTENCE",210,32,"Tolls four times, then the Sentence falls. Toll X/4 is always shown.",elite:true),
            N(Bishop,"THE FINAL BISHOP",390,24,"The last authority of the Black Cathedral. Each phase strips away more of the ceremony.",boss:true),
        };
        public static EnemyDef Find(string id)=>Array.Find(Enemies,e=>e.id==id);
        public static readonly string[] Elites={HighConfessor,ChoirEternal,Bell};
        public static readonly string[] Bosses={Bishop};
        public static string[] StartingMinions(string id)=>id switch
        {
            ChoirEternal=>new[]{VoiceBlade,VoiceMercy,VoiceVigil},
            Bishop=>new[]{VoiceBlade,VoiceMercy},
            _=>Array.Empty<string>()
        };

        // Standard / Advanced / Dangerous. Act 3 has no protected opening fights.
        public static readonly EncounterDef[] Formations={
            F("BC-S01",EncounterTier.Standard,Guard,Confessor),
            F("BC-S02",EncounterTier.Standard,Guard,Censer),
            F("BC-S03",EncounterTier.Standard,Herald),
            F("BC-S04",EncounterTier.Standard,Saint,Effigy),
            F("BC-S05",EncounterTier.Standard,Icon),
            F("BC-S06",EncounterTier.Standard,Priest),
            F("BC-S07",EncounterTier.Standard,Guard,Warden),
            F("BC-S08",EncounterTier.Standard,Confessor,Censer),
            F("BC-S09",EncounterTier.Standard,Herald,Guard),
            F("BC-S10",EncounterTier.Standard,Icon,Guard),
            F("BC-A01",EncounterTier.Advanced,Guard,Warden,Confessor),
            F("BC-A02",EncounterTier.Advanced,Censer,Herald),
            F("BC-A03",EncounterTier.Advanced,Saint,Guard),
            F("BC-A04",EncounterTier.Advanced,Priest,Confessor),
            F("BC-A05",EncounterTier.Advanced,Icon,Warden),
            F("BC-A06",EncounterTier.Advanced,Herald,Censer,Guard),
            F("BC-A07",EncounterTier.Advanced,Saint,Censer),
            F("BC-A08",EncounterTier.Advanced,Priest,Warden),
            F("BC-A09",EncounterTier.Advanced,Icon,Confessor),
            F("BC-A10",EncounterTier.Advanced,Guard,Priest,Censer),
            F("BC-D01",EncounterTier.Dangerous,Herald,Warden,Confessor),
            F("BC-D02",EncounterTier.Dangerous,Saint,Priest),
            F("BC-D03",EncounterTier.Dangerous,Icon,Censer,Warden),
            F("BC-D04",EncounterTier.Dangerous,Confessor,Priest,Guard),
            F("BC-D05",EncounterTier.Dangerous,Herald,Saint),
            F("BC-D06",EncounterTier.Dangerous,Icon,Herald),
            F("BC-D07",EncounterTier.Dangerous,Saint,Warden,Censer),
            F("BC-D08",EncounterTier.Dangerous,Priest,Icon),
            F("BC-D09",EncounterTier.Dangerous,Guard,Warden,Herald),
            F("BC-D10",EncounterTier.Dangerous,Confessor,Censer,Icon),
        };
        private static EncounterDef F(string id,EncounterTier tier,params string[] enemies)=>new(id,tier,3,3,enemies){theme=Theme};
    }
}
