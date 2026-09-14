using System;
using System.Linq;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using GildedFate.Audio;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ScreenChoiceHot(Rect r,int index)=>controllerNavigation?screenControllerIndex==index:r.Contains(PointerPosition);
        private string healthServiceMessage;
        private float healthServiceUntil;
        private void ShowHealthServiceGain(int before)
        {healthServiceMessage="+"+(run.hp-before)+" HP";healthServiceUntil=Time.unscaledTime+1.1f;Sfx(SoundCue.Heal);}
        private void RestAtShrine()
        {
            if(acquisitionActive||runPauseOpen)return;
            var before=run.hp;run.hp=Mathf.Min(run.maxHp,run.hp+Mathf.RoundToInt(run.maxHp*.3f));
            ShowHealthServiceGain(before);Advance();
        }
        private void DrawHealthServiceFeedback()
        {
            if(Time.unscaledTime>=healthServiceUntil||string.IsNullOrEmpty(healthServiceMessage))return;
            var t=Mathf.Clamp01((healthServiceUntil-Time.unscaledTime)/1.1f);
            GUI.Label(new Rect(36,60-(profile.reduceMotion?0:(1-t)*8),180,30),healthServiceMessage,
                new GUIStyle(footerStyle){fontSize=20,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.61f,.96f,.72f,Mathf.Min(1,t*4))}});
        }
        private bool CancelBindingScreen()
        {
            if(acquisitionActive||screen is not (ScreenMode.BindingSelect or ScreenMode.BindingCard))return false;
            // Cleared offers mean the permanent action was already committed.
            if(run.bindingOffers.Count==0)return false;
            run.pendingBindingId="";run.pendingChoicesNeeded=0;run.pendingSelectedCardIds.Clear();cardChoiceScroll=0;screenControllerIndex=0;
            if(screen==ScreenMode.BindingCard){run.stage=RunStage.BindingSelect;screen=ScreenMode.BindingSelect;}
            else{run.stage=RunStage.Sanctuary;screen=ScreenMode.Sanctuary;}
            SaveService.Save(run);return true;
        }
        private void DrawBindingBack(float h)
        {
            var r=new Rect(34,h-65,220,40);DrawButtonFrame(r,!controllerNavigation&&r.Contains(PointerPosition),acquisitionActive);
            if(!acquisitionActive&&GUI.Button(r,screen==ScreenMode.BindingCard?"BACK TO BINDINGS":"CANCEL",buttonStyle))CancelBindingScreen();
        }
        private string BindingCardReason(RunCard card)
        {
            if(card.specialModificationKind!=SpecialModificationKind.None)return "Already modified. Each physical card has one permanent modification slot.";
            var binding=WorldContent.Bindings.FirstOrDefault(b=>b.id==run.pendingBindingId);
            return binding!=null&&BindingMatches(binding,card.BuildDefinition())?"Eligible — confirm this card to attach "+binding.name+".":"This card does not match this Binding's allowed card type or effect.";
        }
        private static string EventDecisionSentence(EventChoiceDef choice)
        {
            string Phrase(string text)
            {
                if(string.IsNullOrWhiteSpace(text))return "No cost";
                var result=GameplayTerms.Display(text).Trim().TrimEnd('.').ToLowerInvariant();
                foreach(var word in new[]{"HP","Gold","Attack","Skill","Aspect","Binding","Relic","Curse","Status","Fate Shard","Block","Strength","Fortify","Draw","Dissipate"})
                    result=System.Text.RegularExpressions.Regex.Replace(result,@"\b"+word+@"\b",word,System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                return char.ToUpperInvariant(result[0])+result.Substring(1);
            }
            return Phrase(choice.costText)+" → "+Phrase(choice.rewardText)+".";
        }
        private static Rect EventDecisionArea(Rect r)=>new(r.x+58,r.y+55,r.width-80,r.height<176?r.height-69:68);
        private void DrawClearEventChoice(Rect rect,EventChoiceDef choice,int index)
        {
            var availability=EventSystem.Availability(run,choice);var hot=controllerNavigation?screenControllerIndex==index:rect.Contains(PointerPosition);
            var r=rect;if(hot&&!profile.reduceMotion)r.x+=4;
            Fill(r,hot?new Color(.045f,.040f,.028f,.98f):new Color(.006f,.011f,.018f,.93f));
            Fill(new Rect(r.x,r.y,hot?3:1,r.height),availability.available?Gold:new Color(.40f,.39f,.36f));
            GUI.Label(new Rect(r.x+13,r.y+17,34,28),(index+1).ToString("00"),new GUIStyle(titleStyle){fontSize=18});
            var titleArea=new Rect(r.x+58,r.y+13,r.width-80,36);var title=FittedEventStyle(choice.title,titleArea,titleStyle,22,17);title.normal.textColor=availability.available?new Color(.96f,.89f,.72f):Color.gray;GUI.Label(titleArea,choice.title,title);
            var area=EventDecisionArea(r);var sentence=FormatCardRules(EventDecisionSentence(choice));
            var style=FittedEventStyle(sentence,area,footerStyle,18,16);style.richText=true;style.normal.textColor=availability.available?new Color(.99f,.96f,.87f):new Color(.65f,.65f,.62f);GUI.Label(area,sentence,style);
            if(r.height>=176){var flavor=availability.available?EventChoiceAction(choice):availability.reason;GUI.Label(new Rect(r.x+58,r.y+133,r.width-80,r.height-146),flavor,new GUIStyle(footerStyle){fontSize=14,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=availability.available?new Color(.68f,.71f,.71f):new Color(1f,.64f,.48f)}});}
            if(hot)SetRunHudTooltip(rect,choice.title,EventDecisionSentence(choice)+(availability.available?"":"\n\n"+availability.reason));
            var enabled=GUI.enabled;GUI.enabled=enabled&&availability.available&&!acquisitionActive;
            if(GUI.Button(rect,"",GUIStyle.none))BeginEventChoice(choice);GUI.enabled=enabled;
        }
        private static Rect FittedServiceIcon(Rect r,float size)=>new(r.center.x-size*.5f,r.y+8,size,size);
        private void DrawRemovalServiceIcon(Rect r)
        {
            var icon=FittedServiceIcon(r,76);var center=icon.center;
            // Two scissor handles and blades: explicitly a service, not a card.
            foreach(var sign in new[]{-1,1})
            {
                var c=center+new Vector2(-24,sign*19);var previous=c+Vector2.right*9;
                for(var i=1;i<=16;i++){var next=c+new Vector2(Mathf.Cos(i*Mathf.PI/8),Mathf.Sin(i*Mathf.PI/8))*9;DrawLine(previous,next,new Color(.84f,.72f,.45f),2);previous=next;}
                DrawLine(c+new Vector2(7,-sign*5),center+new Vector2(31,-sign*25),new Color(.86f,.88f,.90f),3);
            }
            Fill(new Rect(center.x-2,center.y-2,4,4),Gold);
        }
    }
}
