using System;
using GildedFate.Audio;
using System.Collections.Generic;
using GildedFate.Core;
using GildedFate.Combat;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private readonly Dictionary<string,float> expansionPulseStarts=new();
        private Texture2D dormantExpansionIcons;
        private CombatState sigilLayoutCombat;
        private float visibleSigilSlots=3;
        private FighterState presentedPlayerStatuses;
        private int presentedRetaliation,presentedSpiritDirections;
        private readonly List<(float time,FighterState fighter,int retaliation,int spiritDirections)> playerStatusBeats=new();
        private readonly List<(float time,int owner,FighterState fighter)> enemyStatusBeats=new();
        private readonly Dictionary<int,FighterState> presentedEnemyStatuses=new();
        private readonly List<(string key,float start)> powerPulseBeats=new();
        private void ResetPlayerStatusPlayback()
        {
            ResetMasterPolishPlayback();powerArrivals.Clear();
            playerStatusBeats.Clear();enemyStatusBeats.Clear();presentedEnemyStatuses.Clear();powerPulseBeats.Clear();expansionPulseStarts.Clear();
            presentedPlayerStatuses=combat?.player.Copy();presentedRetaliation=combat?.retaliation??0;
            presentedSpiritDirections=combat==null?0:(combat.memory.spiritStrengthTriggered?1:0)|(combat.memory.spiritFortifyTriggered?2:0);
            if(combat!=null)for(var i=0;i<combat.EnemyCount;i++)presentedEnemyStatuses[i]=combat.EnemyAt(i).Copy();
        }
        private FighterState PresentedPlayerStatuses()
        {
            while(playerStatusBeats.Count>0&&playerStatusBeats[0].time<=Time.unscaledTime){var beat=playerStatusBeats[0];presentedPlayerStatuses=beat.fighter;presentedRetaliation=beat.retaliation;presentedSpiritDirections=beat.spiritDirections;playerStatusBeats.RemoveAt(0);}
            // Idle/turn-boundary HUD reads must not keep a stale zero receipt.
            if(playerStatusBeats.Count==0&&!combatBusy){presentedPlayerStatuses=combat.player.Copy();presentedRetaliation=combat.retaliation;}
            return presentedPlayerStatuses??combat.player;
        }
        private FighterState PresentedEnemyStatuses(int owner)
        {
            while(enemyStatusBeats.Count>0&&enemyStatusBeats[0].time<=Time.unscaledTime){var beat=enemyStatusBeats[0];presentedEnemyStatuses[beat.owner]=beat.fighter;enemyStatusBeats.RemoveAt(0);}
            if(enemyStatusBeats.Count==0&&!combatBusy)presentedEnemyStatuses[owner]=combat.EnemyAt(owner).Copy();
            return presentedEnemyStatuses.TryGetValue(owner,out var fighter)?fighter:combat.EnemyAt(owner);
        }
        private float ScheduledPowerPulse(string key)
        {
            const float duration=.18f;var now=Time.unscaledTime;powerPulseBeats.RemoveAll(p=>now>=p.start+duration);
            var pulse=0f;foreach(var beat in powerPulseBeats)if(beat.key==key&&now>=beat.start)pulse=Mathf.Max(pulse,1-(now-beat.start)/duration);
            return pulse;
        }
        private static int ExpansionPowerIndex(string title)=>title?.TrimEnd('+') switch
        {"GILDED WARLORD"=>0,"UNMOVABLE"=>1,"RELENTLESS CONQUEST"=>2,"BRAND OF RUIN"=>3,"BEYOND THE VEIL"=>4,_=>-1};
        private bool DrawExpansionPowerIcon(Rect rect,string title,bool dormant=false)
        {
            var index=ExpansionPowerIndex(title);if(index<0)return false;
            var texture=LoadAuthoredArt("Art/Powers/MartialOccult");if(!texture)return false;
            if(dormant)
            {
                if(!dormantExpansionIcons)
                {
                    // Runtime display state only: original colored artwork is untouched.
                    var pixels=texture.GetPixels32();for(var i=0;i<pixels.Length;i++){var p=pixels[i];var grey=(byte)((p.r*54+p.g*183+p.b*19)/256);pixels[i]=new Color32(grey,grey,grey,p.a);}
                    dormantExpansionIcons=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};dormantExpansionIcons.SetPixels32(pixels);dormantExpansionIcons.Apply(false,true);
                }
                texture=dormantExpansionIcons;
            }
            DrawAtlasIcon(texture,index,3,2,rect);return true;
        }
        private void DrawPowerTravelIcon(Rect rect,CardDef card)
        {if(!DrawPowerHudIcon(rect,card?.name))DrawAtlasIcon(combatReadabilityAtlas,PowerIconForCard(card),8,8,rect);}
        private static string SigilDescription(SigilKind kind)=>kind switch
        {
            SigilKind.Ruin=>"ACTIVATE: Deal 5 damage to ALL enemies. Gain 1 Resonance.",
            SigilKind.Wither=>"ACTIVATE: Apply 1 Weak to ALL enemies. Gain 1 Resonance.",
            SigilKind.Grave=>"ACTIVATE: Exhaust the leftmost Curse or Status in hand. If one was Exhausted, draw 1. Gain 1 Resonance.",
            SigilKind.Mirror=>"ACTIVATE: Repeat the activation effect of the Sigil immediately to the left. Never repeats another Mirror. Gain 1 Resonance.",
            SigilKind.Ember=>"ACTIVATE: Apply 2 Burn. Gain 1 Resonance.\nPASSIVE: Activates at the end of your turn.",
            SigilKind.Hex=>"ACTIVATE: Apply 1 Marked. Gain 1 Resonance.\nPASSIVE: Your first Attack each turn applies 1 Marked and gains 1 Resonance.",
            _=>"ACTIVATE: Your next card repeats. Gain 1 Resonance.\nPASSIVE: Every fourth card repeats and gains 1 Resonance."
        };
        private void DrawSigilChoice(float w,float h)
        {
            combatEffectTooltipTitle=combatEffectTooltipDetail=null;
            var existing=combat.ChoiceKind==CardChoiceKind.SigilSlot;var options=combat.ChoiceOptions;
            Heading(w,existing?"CHOOSE A SIGIL TO ECHO":"CHOOSE A SIGIL",combat.pendingPlay.card.name+" · "+(existing?"ACTIVATE YOUR CHOSEN SIGIL TWICE":"CREATE IN THE NEXT OPEN SLOT"));
            var columns=Mathf.Min(6,options.Count);var rows=Mathf.CeilToInt(options.Count/(float)columns);var width=Mathf.Min(260,(w-220-(columns-1)*18)/columns);var height=Mathf.Min(274,(h-220)/Mathf.Max(1,rows));var start=Mathf.Max(140,(w-columns*width-(columns-1)*18)*.5f);
            for(var i=0;i<options.Count;i++)
            {
                var kind=existing?combat.sigils[i]:(SigilKind)i;var color=kind==SigilKind.Ember?new Color(1,.55f,.27f):kind==SigilKind.Hex?new Color(.8f,.57f,1f):new Color(.51f,.81f,1f);
                var rect=new Rect(start+i%columns*(width+18),h*.26f+i/columns*(height+12),width,height);var hot=rect.Contains(combatPointer)||controllerNavigation&&choiceControllerIndex==i;
                Fill(rect,new Color(.018f,.019f,.03f,.97f));Outline(rect,hot?color:new Color(.32f,.30f,.38f),hot?3:1);
                DrawRemainingSigilIcon(kind,new Rect(rect.center.x-42,rect.y+12,84,84));
                GUI.Label(new Rect(rect.x+8,rect.y+98,rect.width-16,28),options[i],new GUIStyle(buttonStyle){fontSize=16,normal={textColor=color}});
                GUI.Label(new Rect(rect.x+16,rect.y+132,rect.width-32,rect.height-146),SigilDescription(kind),new GUIStyle(subtitleStyle){fontSize=13,alignment=TextAnchor.UpperLeft,wordWrap=true});
                if(rect.Contains(combatPointer))SetCombatEffectTooltip(kind.ToString().ToUpperInvariant()+" SIGIL",SigilDescription(kind),new Vector2(rect.xMax,rect.center.y));
                if(GUI.Button(rect,"",GUIStyle.none)&&choiceOptionSelected==null){choiceOptionSelected=options[i];Sfx(SoundCue.UiConfirm);}
            }
            DrawCombatEffectTooltip(w,h);
        }
    }
}
