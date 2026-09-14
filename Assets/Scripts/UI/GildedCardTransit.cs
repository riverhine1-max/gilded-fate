using GildedFate.Audio;
using GildedFate.Combat;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private Vector2 ReceiptCardTarget(CombatEvent fact)
        {
            if(fact.destination==CombatCardDestination.Hand)
            {
                var slot=HandLayout.Slot(Mathf.Max(0,fact.handIndex<0?combat.hand.IndexOf(fact.card):fact.handIndex),Mathf.Max(1,fact.handCount>0?fact.handCount:combat.hand.Count),CombatWidth,CombatHeight);
                return new Vector2(slot.x,slot.y);
            }
            return PilePoint(fact.destination==CombatCardDestination.Exhaust?2:fact.destination==CombatCardDestination.Draw?0:1);
        }
        private static SoundCue CardArrivalSound(CombatEvent fact,bool generated)=>!generated?SoundCue.CardDraw:fact.label=="SOUL"?SoundCue.AttackReaper:fact.label=="CURSE"?SoundCue.EventWhisper:fact.label is "STATUS" or "OPENING STATUS"?SoundCue.Debuff:SoundCue.CardDraw;

        private float PresentCardArrival(CombatEvent fact,float delay,bool generated)
        {
            var enemySource=generated&&(combat.phase is CombatPhase.Enemy or CombatPhase.EnemyResolved);
            var source=generated?(enemySource?(GroupCombat?GroupPortrait(fact.enemyIndex).center:EnemyPortraitRect.center):HeroPortraitRect.center+Vector2.right*112):PilePoint(fact.label=="FROM EXHAUST"?2:fact.label is "DISCARD" or "RETURN" or "LINGERING"?1:0);
            var target=ReceiptCardTarget(fact);var reveal=generated?AnimationSeconds(profile.reduceMotion?.06f:fact.label=="OPENING STATUS"?.28f:.16f):0;
            var duration=AnimationSeconds(profile.reduceMotion?.16f:.34f);var hand=fact.destination==CombatCardDestination.Hand;
            if(generated)MoveCard(fact.card,source,source,reveal,.54f,.62f,delay:delay);
            MoveCard(fact.card,source,target,duration,generated?.62f:.24f,hand?1:.21f,0,0,delay+reveal);
            var arrival=delay+reveal+duration;
            if(hand&&combat.hand.Contains(fact.card))handViews[fact.card.instanceId]=new HandView{card=fact.card,position=target,scale=1,readyAt=Time.unscaledTime+arrival};
            Sfx(CardArrivalSound(fact,generated),intensity:generated?.45f:1f,combatSound:true,delay:delay);
            if(fact.destination==CombatCardDestination.Draw)drawPulse=1;
            else if(fact.destination==CombatCardDestination.Discard)discardPulse=1;
            else if(fact.destination==CombatCardDestination.Exhaust)exhaustPulse=1;
            return arrival;
        }
    }
}
