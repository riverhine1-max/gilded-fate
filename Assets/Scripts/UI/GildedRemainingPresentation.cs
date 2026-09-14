using System;
using System.Linq;
using GildedFate.Core;
using GildedFate.Combat;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private void AddHiddenPotentialChip(System.Collections.Generic.List<CombatEffectChip> chips,Color color)
        {
            var r=combat.memory.remaining;if(r==null||r.hiddenPotential<=0)return;
            var types=new System.Collections.Generic.List<string>();if((r.hiddenTypes&1)!=0)types.Add("ATTACK +5");if((r.hiddenTypes&2)!=0)types.Add("SKILL");if((r.hiddenTypes&4)!=0)types.Add("ASPECT");
            var description="At the start of each turn, reveal the bottom "+r.hiddenPotential+" card(s).\nAttack: Attacks deal +5 damage.\nSkill: Block Skills gain +5 Block; other Skills cost 1 less.\nPower: the first Power costs 1 less.\nBonuses last this turn; duplicate types do not stack.\n\nRevealed: "+(string.IsNullOrEmpty(r.hiddenRevealedNames)?"Waiting for your next turn.":r.hiddenRevealedNames);
            chips.Add(new CombatEffectChip("PWR","HIDDEN POTENTIAL",description,r.hiddenPotential,color,true,types.Count==0?"READY":"ACTIVE"));
            for(var i=0;i<3;i++)if((r.hiddenTypes&(1<<i))!=0){var type=((CardKind)i).ToString().ToUpperInvariant();var value=i==0?"+5":i==1?"+5 / -1":r.hiddenPowerUsed?"USED":"-1";chips.Add(new CombatEffectChip("PWR","HIDDEN POTENTIAL "+type,description,1,color,true,value));}
        }
        private void DrawRemainingSigilIcon(SigilKind kind,Rect r)
        {
            if((int)kind<3){DrawAtlasIcon(combatReadabilityAtlas,54+(int)kind,8,8,r);return;}
            var art=LoadAuthoredArt("Art/Powers/RemainingExpansion");if(art)DrawAtlasIcon(art,(int)kind-3,4,3,r);
        }
        private bool DrawRemainingEffectIcon(Rect r,string title)
        {
            var index=title switch{"DEATHS ECHO"=>4,"DEATH'S ECHO"=>4,"REAPED"=>5,"GRAVEMARK"=>6,"MARK OF THE GRAVE"=>6,"GOLDEN THREAD"=>7,"HIDDEN POTENTIAL"=>9,"SOULBOUND"=>10,"PASSING FORTUNE"=>11,"SOUL DOMINION"=>4,_=>title.StartsWith("IMPRINT ")?8:-1};
            if(index<0)return false;var art=LoadAuthoredArt("Art/Powers/RemainingExpansion");if(!art)return false;DrawAtlasIcon(art,index,4,3,r);return true;
        }
        private void DrawRemainingOptions(float w,float h)
        {
            var card=combat.pendingPlay.card;var options=combat.ChoiceOptions;Heading(w,card.name,"CHOOSE AN OUTCOME");
            GUI.Label(new Rect(w*.18f,178,w*.64f,46),card.text,new GUIStyle(subtitleStyle){fontSize=17,wordWrap=true});
            var columns=Mathf.Min(4,options.Count);var width=Mathf.Min(255,(w-100-(columns-1)*18)/Mathf.Max(1,columns));var start=(w-columns*width-(columns-1)*18)/2;
            for(var i=0;i<options.Count;i++)
            {
                var option=options[i];var r=new Rect(start+i%columns*(width+18),h*.3f+i/columns*205,width,188);var hot=r.Contains(combatPointer)||controllerNavigation&&choiceControllerIndex==i;
                Fill(r,new Color(.018f,.022f,.035f));Outline(r,hot?Gold:new Color(.32f,.34f,.42f),hot?3:1);
                var sigilName=option.Contains(" · ")?option.Substring(option.IndexOf(" · ",StringComparison.Ordinal)+3):option;sigilName=sigilName.Replace(" SIGIL","");
                var isSigil=option.EndsWith(" SIGIL")&&Enum.TryParse<SigilKind>(sigilName,true,out _);
                var detail=card.text;
                if(isSigil){var kind=(SigilKind)Enum.Parse(typeof(SigilKind),sigilName,true);DrawRemainingSigilIcon(kind,new Rect(r.center.x-28,r.y+12,56,56));detail=SigilDescription(kind);}
                else GUI.Label(new Rect(r.x+10,r.y+18,r.width-20,36),option=="FINISH"?"✓":"◆",titleStyle);
                GUI.Label(new Rect(r.x+12,r.y+74,r.width-24,42),option,new GUIStyle(buttonStyle){fontSize=15,wordWrap=true});
                GUI.Label(new Rect(r.x+14,r.y+120,r.width-28,58),isSigil?detail:option=="FINISH"?"Keep your remaining cards and finish this effect.":"Select this outcome.",new GUIStyle(subtitleStyle){fontSize=12,wordWrap=true});
                if(hot)SetCombatEffectTooltip(option,detail,new Vector2(r.xMax,r.center.y));
                if(GUI.Button(r,"",GUIStyle.none)&&choiceOptionSelected==null)choiceOptionSelected=option;
            }
            DrawCombatEffectTooltip(w,h);
        }
    }
}
