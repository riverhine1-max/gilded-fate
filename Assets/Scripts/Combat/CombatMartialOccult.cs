using System;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    public sealed partial class CombatState
    {
        public int SigilCapacity=>3+Math.Max(0,memory.extraSigilSlots);
        public bool WarlordReady=>memory.gildedWarlord>0&&turn>=memory.warlordReadyTurn;
        private void ExpansionPowerPulse(string id)=>Emit(CombatEventKind.Status,0,true,GameContent.Find(id),"TRIGGER:"+GameContent.Find(id).name);
        private int DoubleWarlordBuff(int amount)
        {
            if(amount<=0||!WarlordReady)return amount;
            // Latch before any chained buff, and survive the whole following turn.
            memory.warlordReadyTurn=turn+2;ExpansionPowerPulse("gilded_warlord");return amount*2;
        }
        private void GainRetaliate(int amount)
        {
            if(amount<=0)return;amount=DoubleWarlordBuff(amount+RallyBuffBonus());retaliation+=amount;Emit(CombatEventKind.Status,amount,true,null,"RETALIATE");RelicBuff("Retaliate");
        }
        private bool InstallMartialOccultPower(CardDef card,int value)
        {
            switch(card.id)
            {
                case "gilded_warlord":memory.gildedWarlord=1;return true;
                case "unmovable":memory.unmovable=1;return true;
                case "relentless_conquest":memory.conquestCadence=memory.conquestCadence==0?value:Math.Min(memory.conquestCadence,value);return true;
                case "brand_of_ruin":memory.brandOfRuin+=value;return true;
                case "beyond_the_veil_hexer":ExpansionPowerPulse(card.id);memory.extraSigilSlots+=value;return true;
                default:return false;
            }
        }
        private void ActivateAllSigils(int times)
        {
            // No Curse-based activation cap. ActivateSigil's existing gates protect
            // chained powers; this loop is finite and snapshots its requested count.
            for(var pass=0;pass<times;pass++)for(var slot=0;slot<sigils.Count;slot++)ActivateSigil(slot);
        }
        private bool ResolveMartialOccultCard(CardDef card,int value,int secondary,bool heavy)
        {
            switch(card.id)
            {
                case "break_the_line":DamageEnemy(value,card);if(heavy){energy++;Emit(CombatEventKind.Energy,energy,true);}return true;
                case "iron_momentum":var previous=memory.attacksThisTurn;DamageEnemy(value,card);if(previous>0)GainBlock(previous*secondary,true,card);return true;
                case "crushing_advance":DamageEnemy(value,card);if(heavy)GainFortify(secondary);return true;
                case "stand_your_ground":GainBlock(value,true,card);if(retaliation>0)GainFortify(secondary);return true;
                case "gilded_fury":GainStrength(memory.attacksThisTurn/Math.Max(1,value));return true;
                case "breaking_momentum":var third=memory.attacksThisTurn==2;DamageEnemy(value,card);if(third){GainStrength(secondary);GainFortify(1);}return true;
                case "forbidden_convergence":ActivateAllSigils(1+hand.Count(c=>c.origin==CardOrigin.Curse));return true;
                case "arcane_overload":DamageEnemy(value,card);ActivateAllSigils(2);return true;
                case "sigil_of_malice":
                    if(sigils.Count<SigilCapacity){pendingPlay.choice=CardChoiceKind.SigilMode;pendingPlay.choiceFollowupValue=EnemyDebuffCount()>0?1:0;}return true;
                case "unstable_ritual":
                    if(sigils.Count>0){var slot=sigils.Count-1;for(var i=0;i<value;i++)ActivateSigil(slot);RemoveRemainingSigil(slot);Emit(CombatEventKind.Status,1,true,card,"SIGIL SHATTER");}return true;
                case "dark_resonance":
                    if(!card.upgraded)AddRandomCurse(false);ActivateAllSigils(1);if(card.upgraded){GainResonance(sigils.Count);AddRandomCurse(false);}return true;
                case "arcane_detonation":
                    var spent=Math.Min(3,resonance);resonance-=spent;Emit(CombatEventKind.Resonance,resonance,true);DamageEnemy(value+secondary*spent,card);return true;
                case "hexed_reverberation":
                    if(ConsumeMarked(1)>0&&sigils.Count>0){if(card.upgraded){pendingPlay.choice=CardChoiceKind.SigilSlot;pendingPlay.choiceFollowupValue=value;}else for(var i=0;i<value;i++)ActivateSigil(0);}return true;
                default:return false;
            }
        }
    }
}
