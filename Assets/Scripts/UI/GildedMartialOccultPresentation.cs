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
            _=>"ACTIVATE: Your next card repeats once. Gain 1 Resonance.\nNO PASSIVE EFFECT."
        };
        private void DrawSigilChoice(float w,float h)
        {
            combatEffectTooltipTitle=combatEffectTooltipDetail=null;
            var existing=combat.ChoiceKind==CardChoiceKind.SigilSlot;var options=combat.ChoiceOptions;
            var ritualTitle=combat.pendingPlay.card.id=="first_ritual"?"FIRST RITUAL: CHOOSE A SIGIL":"CHOOSE A SIGIL";
            GUI.Label(new Rect(w*.15f,91,w*.7f,54),existing?"CHOOSE A SIGIL TO ECHO":ritualTitle,new GUIStyle(titleStyle){fontSize=32,normal={textColor=new Color(1f,.88f,.58f)}});
            GUI.Label(new Rect(w*.2f,145,w*.6f,28),existing?"ACTIVATE ONE OF YOUR EXISTING SIGILS TWICE":"EMBER · HEX · ECHO",new GUIStyle(footerStyle){fontSize=13,fontStyle=FontStyle.Bold,normal={textColor=new Color(.86f,.82f,.76f)}});
            var columns=Mathf.Min(6,options.Count);var rows=Mathf.CeilToInt(options.Count/(float)columns);var width=Mathf.Min(190,(w-220-(columns-1)*24)/columns);var height=190f;var start=(w-columns*width-(columns-1)*24)*.5f;
            for(var i=0;i<options.Count;i++)
            {
                var kind=existing?combat.sigils[i]:(SigilKind)i;var color=kind==SigilKind.Ember?new Color(1,.55f,.27f):kind==SigilKind.Hex?new Color(.8f,.57f,1f):new Color(.51f,.81f,1f);
                var rect=new Rect(start+i%columns*(width+24),h*.30f+i/columns*(height+18),width,height);var hot=rect.Contains(combatPointer)||controllerNavigation&&choiceControllerIndex==i;
                var pulse=profile.reduceMotion?0:(Mathf.Sin(shimmer*2.2f+i*.8f)+1)*.5f;var glow=(hot ? .25f : .07f)+pulse*.025f;
                Fill(new Rect(rect.center.x-70,rect.y+7,140,140),new Color(color.r,color.g,color.b,glow));
                var size=hot?124f:112f;DrawRemainingSigilIcon(kind,new Rect(rect.center.x-size*.5f,rect.y+15-(profile.reduceMotion?0:pulse*3),size,size));
                if(hot){Outline(new Rect(rect.center.x-72,rect.y+5,144,144),new Color(color.r,color.g,color.b,.88f),2);DrawLine(new Vector2(rect.center.x-48,rect.y+155),new Vector2(rect.center.x+48,rect.y+155),color,2);}
                GUI.Label(new Rect(rect.x+8,rect.y+155,rect.width-16,30),options[i],new GUIStyle(buttonStyle){fontSize=17,normal={textColor=hot?Color.white:color}});
                if(rect.Contains(combatPointer))SetCombatEffectTooltip(kind.ToString().ToUpperInvariant()+" SIGIL",SigilDescription(kind),new Vector2(rect.xMax,rect.center.y));
                if(GUI.Button(rect,"",GUIStyle.none)&&choiceOptionSelected==null){choiceOptionSelected=options[i];Sfx(SoundCue.UiConfirm);}
            }
            Outline(new Rect(w*.12f,76,w*.76f,h*.65f),new Color(.72f,.55f,.28f,.35f),1);
            DrawCombatEffectTooltip(w,h);
        }
    }
}
