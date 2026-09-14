using System.Collections;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private CardDef[] bindingCaptureCards;
        private static CardDef BindingCheckCard(string id,string binding,SpecialModificationKind kind=SpecialModificationKind.Binding)
        {
            var card=GameContent.Find(id).Copy();card.specialModificationKind=kind;card.specialModification=binding;return card;
        }
        private bool ConfigureBindingCapture(string mode)
        {
            if(mode=="binding-cards"||mode=="binding-inspection")
            {
                run.NewRun(HeroId.Vanguard,20260906);screen=ScreenMode.Collection;
                bindingCaptureCards=new[]{BindingCheckCard("strike","serrated"),BindingCheckCard("defend","reinforced"),BindingCheckCard("hex","focused"),BindingCheckCard("battle_rush","recurring"),BindingCheckCard("soul_call","first_light",SpecialModificationKind.Fateweave),GameContent.Find("strike").Copy()};
                if(mode=="binding-inspection")inspectedCard=bindingCaptureCards[0];
                return true;
            }
            if(mode!="combat-binding"&&mode!="combat-binding-hover")return false;
            combatTestInput=true;PrepareCombatCheck("strike",5);
            combat.hand[0].specialModificationKind=SpecialModificationKind.Binding;combat.hand[0].specialModification="serrated";
            combat.hand[2].specialModificationKind=SpecialModificationKind.Binding;combat.hand[2].specialModification="chained";
            combat.hand[4].specialModificationKind=SpecialModificationKind.Fateweave;combat.hand[4].specialModification="golden_echo";
            if(mode=="combat-binding-hover")StartCoroutine(PrepareHoveredCapture(mode));
            return true;
        }
        private static Vector2 BindingWorldPoint(Vector2 local,Vector2 center,float angle,float scale)
        {
            var a=angle*Mathf.Deg2Rad;local*=scale;
            return center+new Vector2(local.x*Mathf.Cos(a)-local.y*Mathf.Sin(a),local.x*Mathf.Sin(a)+local.y*Mathf.Cos(a));
        }
        private void CheckBindingClaspGeometry()
        {
            var card=BindingCheckCard("strike","serrated");var plain=GameContent.Find("strike").Copy();
            foreach(var size in new[]{new Vector2(176,260),new Vector2(194,264),new Vector2(220,310),new Vector2(413,590)})
            {
                var r=new Rect(120,80,size.x,size.y);var badge=CardBindingClaspRect(r);
                CombatCheck(badge.x<r.x&&badge.xMax>r.x&&!badge.Overlaps(CardNameArea(r))&&!badge.Overlaps(CardRuleArea(r))&&!badge.Overlaps(CardArtArea(r)),"Binding clasp attaches left without covering art or text at "+size);
                var point=new Vector2(badge.x+badge.width*.24f,badge.center.y);
                CombatCheck(CardHelpContains(r,card,point)&&!CardHelpContains(r,plain,point),"Only a bound physical copy has a hoverable clasp at "+size);
            }
            var local=CardBindingClaspRect(new Rect(-97,-132,194,264));var pick=new Vector2(local.x+local.width*.24f,local.center.y);
            foreach(var angle in new[]{-10f,0f,10f})foreach(var scale in new[]{.85f,1f,1.18f})
            {
                var center=new Vector2(540,660);var point=BindingWorldPoint(pick,center,angle,scale);
                CombatCheck(HandCardContains(point,card,center.x,center.y,angle,scale)&&!HandCardContains(point,plain,center.x,center.y,angle,scale),"Clasp follows the visible card at angle "+angle+" / scale "+scale);
                var outside=BindingWorldPoint(new Vector2(local.x-5,local.center.y),center,angle,scale);
                CombatCheck(!HandCardContains(outside,card,center.x,center.y,angle,scale),"Clasp does not leave a phantom hover box at angle "+angle+" / scale "+scale);
            }
            CardModificationInfo(card,out var title,out var detail);
            CombatCheck(title=="SERRATED"&&detail.Contains("+2")&&ModificationIconIndex(card)==0,"Clasp identity resolves the card's actual binding and effect");
            PrepareCardInspection(card);
            CombatCheck(inspectionBase.IsModified&&inspectionUpgrade.IsModified&&ModificationIconIndex(inspectionBase)==ModificationIconIndex(inspectionUpgrade),"Normal and upgraded inspection retain the same physical binding");
        }
        private IEnumerator CheckBindingClaspDrag()
        {
            PrepareCombatCheck("strike",5);yield return WaitForCombatQueue();
            var card=combat.hand[0];card.specialModificationKind=SpecialModificationKind.Binding;card.specialModification="serrated";
            var slot=HandLayout.Slot(0,5,CombatWidth,CombatHeight);var r=CardBindingClaspRect(new Rect(-97,-132,194,264));
            var point=BindingWorldPoint(new Vector2(r.x+r.width*.24f,r.center.y),new Vector2(slot.x,slot.y),slot.angle,1);
            var energy=combat.energy;var hp=combat.enemy.hp;
            HandleCombatPointer(point,false,false,false);
            CombatCheck(hoverView?.card==card,"Hovering the attached clasp selects the correct card for its effect tooltip");
            HandleCombatPointer(point,true,true,false);HandleCombatPointer(point,false,false,true);
            CombatCheck(combat.hand.Contains(card)&&combat.energy==energy&&!combatBusy,"Clicking a clasp alone does not accidentally play its card");
            HandleCombatPointer(point,true,true,false);HandleCombatPointer(EnemyDropZone.center,false,true,false);HandleCombatPointer(EnemyDropZone.center,false,false,true);
            yield return WaitForCombatQueue();
            CombatCheck(combat.cardsPlayed==1&&combat.energy==energy-combat.CostFor(card)&&combat.enemy.hp==hp-8,"Drag from the clasp plays the bound Strike exactly once at the unchanged target");
        }
    }
}
