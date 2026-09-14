using System;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private static int MajorEffectIcon(string title)=>title switch
        {"CHALLENGE THEM ALL"=>0,"RALLYING GUARD"=>1,"STEEL THROUGH PAIN"=>2,"BRACE FOR IMPACT"=>3,"UNRELENTING ASSAULT"=>4,_=>-1};
        private bool DrawMajorEffectIcon(Rect r,string title)
        {
            if(DrawPowerHudIcon(r,title))return true;if(DrawRelicEffectIcon(r,title))return true;if(DrawRemainingEffectIcon(r,title))return true;var tile=MajorEffectIcon(title);if(tile<0)return false;var art=LoadAuthoredArt("Art/Powers/MajorVanguard");
            if(!art)return false;DrawAtlasIcon(art,tile,3,2,r);return true;
        }
        private static string MajorEffectDetail(CombatEffectState effect)
        {
            var value=effect.value;
            var description=RelicEffectDetail(effect)??(effect.id switch
            {
                "challenge_them_all"=>$"The next time Retaliate triggers, gain {value} Block.",
                "rallying_guard"=>$"The next buff you gain gets {value} additional stack(s).",
                "steel_through_pain"=>$"The next enemy attack that completely breaks your Block grants {value} Retaliate for a later attack.",
                "brace_for_impact"=>$"Gain {value} Retaliate BEFORE each enemy attack. Multi-hit attacks count once.",
                "unrelenting_assault"=>$"Your next Attack gains {value} damage on each hit against each enemy.",
                "deaths_echo"=>$"Each card drawn this turn deals {value} damage to a random living enemy.",
                "reaped"=>$"Souls deal {value}% more damage to this enemy.",
                "gravemark"=>$"Grave Execution consumes all {value} stacks: one separate hit per stack.",
                "mark_of_the_grave"=>$"Each card drawn this turn applies {value} Gravemark to this enemy.",
                "unstable_hex"=>$"Next loss of Marked applies {value} Weak to all enemies.",
                "passing_fortune"=>$"Next applicable card this turn gains +{value} damage or Block.",
                "golden_thread"=>"First card each turn that did not start in the deck draws 1. Upgraded: also +3 damage or Block.",
                "hidden_potential"=>"Revealed types this turn: Attack = +5 Attack damage. Skill = +5 Block on Block Skills; other Skills cost 1 less. Power = first Power costs 1 less. Duplicate types do not stack.",
                "soul_dominion"=>value>1?"Souls that draw non-Souls add a Soul to the draw pile.":"Souls that draw non-Souls add a Soul to discard.",
                _=>effect.id.StartsWith("imprint_")?$"IMPRINTED COST {effect.id.Substring(8)}: playing this printed Energy cost deals {value} damage to a random living enemy.":effect.id
            });
            var source=GameContent.Find(effect.source)?.name??System.Array.Find(GameContent.Relics,r=>r.id==effect.source)?.name??effect.source;
            var duration=effect.duration==CombatEffectDuration.TurnEnd?"Until the end of this turn or consumed.":effect.duration==CombatEffectDuration.NextTurn?"Until your next turn or consumed.":"For this combat, until consumed.";
            return description+"\nSource: "+source+"\n"+duration;
        }
        private Rect GroupPresentedPortrait(int index)
        {
            var r=GroupPortrait(index);if(profile.reduceMotion||index>=opponentVisuals.Count)return r;
            var visual=opponentVisuals[index];r.x-=Mathf.Sin((1-visual.action)*Mathf.PI)*visual.action*26;
            r.x+=Mathf.Sin(shimmer*58)*visual.hit*5;return r;
        }
    }
}
