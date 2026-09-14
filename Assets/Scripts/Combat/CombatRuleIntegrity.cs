using System;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    public sealed partial class CombatState
    {
        // Old saves placed installed Aspects in Exhaust. Move only identities
        // supported by their saved active effect; never reinstall or replay them.
        internal void RestoreLegacyAspectOwnership()
        {
            foreach(var card in exhaust.Where(c=>c.kind==CardKind.Power&&LegacyAspectValue(c.id)>0).ToArray())
            {exhaust.Remove(card);activeAspects.Add(card);}
            R.turnStartCards=Math.Max(0,cardsPlayed-Math.Max(0,RM.turnPlays));
        }
        private int LegacyAspectValue(string id)=>id switch
        {
            "battle_temper"=>memory.battleTemper,"iron_blood"=>memory.ironBlood,"living_armor"=>memory.livingArmor,
            "war_machine"=>memory.warMachine,"unbreakable_spirit"=>memory.powersInPlay,"patient_warrior"=>memory.patientWarrior,
            "retribution"=>memory.retributionPower,"hold_the_line"=>memory.holdLinePower,"relentless"=>memory.relentless,
            "onslaught"=>memory.onslaughtThreshold,"indomitable"=>memory.indomitable,"sigil_mastery"=>memory.sigilMastery,
            "grand_convergence"=>memory.grandConvergence,"ashes"=>memory.ashes,"deaths_gaze"=>memory.deathsGaze,
            "embrace_the_void"=>memory.embraceVoid,"damnation"=>memory.damnationStrength,"battle_rhythm"=>memory.battleRhythm,
            "reserve_energy"=>memory.reserveEnergy,"tactical_advantage"=>memory.tacticalAdvantage,"resourceful"=>memory.resourceful,
            "overflow"=>memory.overflowPower,"chain_reaction"=>memory.chainReaction,"against_all_odds"=>memory.againstAllOddsDraw,
            "perfect_form"=>memory.perfectForm,"deaths_embrace"=>memory.deathsEmbrace,"death_march"=>memory.deathMarchCadence,
            "gravekeeper"=>memory.gravekeeper,"endless_harvest"=>memory.endlessHarvest,"death_incarnate"=>memory.deathIncarnate,
            "grim_ascension"=>memory.grimAscensionBonus,"soulbound_tome"=>memory.soulboundTome,"eternal_souls"=>memory.eternalSouls,
            "reapers_calling"=>memory.reapersCalling,"gilded_warlord"=>memory.gildedWarlord,"unmovable"=>memory.unmovable,
            "relentless_conquest"=>memory.conquestCadence,"brand_of_ruin"=>memory.brandOfRuin,"beyond_the_veil_hexer"=>memory.extraSigilSlots,
            "soul_dominion"=>R.soulDominion,"golden_thread"=>R.goldenThread,"hidden_potential"=>R.hiddenPotential,_=>EffectValue(player,id)
        };
    }
}
