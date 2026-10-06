using System;
using System.Linq;

namespace GildedFate.Core
{
    // Act 2 · Theme 3: Shattered Observatory. Identity: PREDICTION (future intents, queued effects,
    // two-possibility pairs and reactions to the Energy you spent last turn).
    // Rules live in Combat/ObservatoryCombat.cs; this file is data only.
    public static class ShatteredObservatoryContent
    {
        public const string Theme=ActThemes.ShatteredObservatory;
        public const string Scribe="shattered_observatory_starbound_scribe",Sentinel="shattered_observatory_orbiting_sentinel",
            Attendant="shattered_observatory_astral_attendant",Chronoglyph="shattered_observatory_chronoglyph",
            Lenskeeper="shattered_observatory_lenskeeper",Weaver="shattered_observatory_constellation_weaver",
            StarFragment="shattered_observatory_star_fragment",Monk="shattered_observatory_gravity_monk",
            Astrologer="shattered_observatory_fallen_astrologer",OrreryKeeper="shattered_observatory_orrery_keeper",
            SunFragment="shattered_observatory_sun_fragment",MoonFragment="shattered_observatory_moon_fragment",
            BlindSeer="shattered_observatory_blind_seer",Comet="shattered_observatory_fallen_comet",
            Curator="shattered_observatory_astral_curator";

        private static EnemyDef N(string id,string name,int hp,int dmg,string text,bool elite=false,bool boss=false,bool minion=false,bool support=false)
            =>new(id,name,hp,dmg,text,elite,boss){theme=Theme,minion=minion,support=support};
        // HP is the solo/base value. Normals in a multi-enemy formation start at ~92% (Act 2).
        public static readonly EnemyDef[] Enemies={
            N(Scribe,"STARBOUND SCRIBE",52,10,"Charts the stars, then calls one down. Its Falling Star is queued where you can see it."),
            N(Sentinel,"ORBITING SENTINEL",68,10,"Three orbiting Plates guard it. Launched Plates hurt, and every missing Plate sharpens its strike."),
            N(Attendant,"ASTRAL ATTENDANT",49,0,"A pure Support. It wards, guides and heals its allies. It never attacks.",support:true),
            N(Chronoglyph,"CHRONOGLYPH",57,11,"Always shows its Current action and its Future action. The Future becomes Current when it acts."),
            N(Lenskeeper,"LENSKEEPER",60,11,"Changes its next action based on Energy spent during your previous turn."),
            N(Weaver,"CONSTELLATION WEAVER",61,9,"Weaves Star Fragments, mends them and commands them. At most two at a time."),
            N(StarFragment,"STAR FRAGMENT",20,6,"The Weaver's Minion. It pulses, then guards.",minion:true),
            N(Monk,"GRAVITY MONK",66,10,"Weighs you down, then compresses its own mass into a Collapse Point."),
            N(Astrologer,"FALLEN ASTROLOGER",70,16,"Shows two possible next actions. Only one is chosen, when it acts."),
            N(OrreryKeeper,"THE ORRERY KEEPER",150,14,"Keeps a Sun Fragment and a Moon Fragment in orbit and aligns them for a Grand Alignment.",elite:true),
            N(SunFragment,"SUN FRAGMENT",28,10,"The Orrery Keeper's Minion. It flares, then surges.",minion:true),
            N(MoonFragment,"MOON FRAGMENT",30,8,"The Orrery Keeper's Minion. It guards the Keeper, then strikes.",minion:true),
            N(BlindSeer,"THE BLIND SEER",156,15,"Previews three actions ahead: Current, Next and Following.",elite:true),
            N(Comet,"THE FALLEN COMET",168,12,"Builds Momentum toward a single devastating Impact, then cools.",elite:true),
            N(Curator,"THE ASTRAL CURATOR",310,14,"Keeper of every future. Shows a Future Intent, then fractures into Twin Fate.",boss:true),
        };
        public static EnemyDef Find(string id)=>Array.Find(Enemies,e=>e.id==id);
        public static readonly string[] Elites={OrreryKeeper,BlindSeer,Comet};
        public static readonly string[] Bosses={Curator};
        public static string[] StartingMinions(string id)=>id==OrreryKeeper?new[]{SunFragment,MoonFragment}:Array.Empty<string>();

        public static readonly EncounterDef[] Formations={
            F("SO-S01",EncounterTier.Standard,Scribe,Sentinel),
            F("SO-S02",EncounterTier.Standard,Chronoglyph,Scribe),
            F("SO-S03",EncounterTier.Standard,Lenskeeper),
            F("SO-S04",EncounterTier.Standard,Weaver,StarFragment),
            F("SO-S05",EncounterTier.Standard,Monk),
            F("SO-S06",EncounterTier.Standard,Astrologer),
            F("SO-S07",EncounterTier.Standard,Sentinel,Attendant),
            F("SO-S08",EncounterTier.Standard,Scribe,Lenskeeper),
            F("SO-S09",EncounterTier.Standard,Chronoglyph,Monk),
            F("SO-S10",EncounterTier.Standard,Sentinel),
            F("SO-A01",EncounterTier.Advanced,Scribe,Attendant,Monk),
            F("SO-A02",EncounterTier.Advanced,Chronoglyph,Sentinel),
            F("SO-A03",EncounterTier.Advanced,Weaver,Scribe),
            F("SO-A04",EncounterTier.Advanced,Lenskeeper,Monk),
            F("SO-A05",EncounterTier.Advanced,Astrologer,Scribe),
            F("SO-A06",EncounterTier.Advanced,Sentinel,Attendant,Lenskeeper),
            F("SO-A07",EncounterTier.Advanced,Chronoglyph,Astrologer),
            F("SO-A08",EncounterTier.Advanced,Weaver,Monk),
            F("SO-A09",EncounterTier.Advanced,Lenskeeper,Sentinel),
            F("SO-A10",EncounterTier.Advanced,Monk,Attendant),
            F("SO-D01",EncounterTier.Dangerous,Chronoglyph,Monk,Attendant),
            F("SO-D02",EncounterTier.Dangerous,Astrologer,Sentinel),
            F("SO-D03",EncounterTier.Dangerous,Weaver,Lenskeeper),
            F("SO-D04",EncounterTier.Dangerous,Monk,Lenskeeper,Scribe),
            F("SO-D05",EncounterTier.Dangerous,Astrologer,Chronoglyph),
            F("SO-D06",EncounterTier.Dangerous,Sentinel,Monk,Attendant),
            F("SO-D07",EncounterTier.Dangerous,Weaver,Astrologer),
            F("SO-D08",EncounterTier.Dangerous,Chronoglyph,Lenskeeper,Scribe),
            F("SO-D09",EncounterTier.Dangerous,Astrologer,Monk),
            F("SO-D10",EncounterTier.Dangerous,Weaver,Sentinel,Attendant),
        };
        private static EncounterDef F(string id,EncounterTier tier,params string[] enemies)=>new(id,tier,2,2,enemies){theme=Theme};
    }
}
