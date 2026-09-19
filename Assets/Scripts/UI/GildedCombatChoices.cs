using GildedFate.Audio;
using System.Collections;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool choicePresented,combatPauseOpen,verificationSavesAllowed;
        private int choicePage,choiceControllerIndex;
        private float choiceScroll;
        private CardDef choicePressed,choiceSelected;private string choiceOptionSelected;

        private bool SaveCombatCheckpoint()
        {
            if(combat==null||captureMode&&!verificationSavesAllowed)return true;
            run.stage=RunStage.Combat;run.activeEnemyId=combat.EnemyIdAt(0);run.hero=combat.hero;
            run.activeNodeFloor=currentNode?.floor??run.activeNodeFloor;run.activeNodeLane=currentNode?.lane??run.activeNodeLane;
            run.hp=combat.player.hp;run.maxHp=combat.player.maxHp;run.immortalThreadUsed=combat.ImmortalThreadUsed;
            run.SyncPerfectedGrowth(combat);run.combatCheckpoint=combat.CaptureCheckpoint();run.hasCombatCheckpoint=true;
            var saved=SaveService.Save(run);if(!saved)ShowInputHint("SAVE FAILED · your previous save is intact");
            return saved;
        }
        private void OnApplicationQuit()
        {
            if(captureMode)return;
            if(screen==ScreenMode.Combat)SaveCombatCheckpoint();
            ProfileService.Save(profile);
        }
        private void OnDestroy(){DisposeHexerVideos();DisposeVanguardVideos();DisposeReaperVideos();DisposeRunStartSurface();DisposeCombat3D();}
        private bool SaveCombatAndReturnToMenu()
        {
            if(!SaveCombatCheckpoint())return false;
            if(combatSequence!=null)StopCoroutine(combatSequence);
            combatSequence=null;combatBusy=false;combatPauseOpen=choicePresented=false;
            dragView=hoverView=selectedView=null;choicePressed=choiceSelected=null;choiceOptionSelected=null;
            screen=ScreenMode.Menu;banner="COMBAT SAVED · CONTINUE RESUMES THIS EXACT STATE";
            return true;
        }

        private void RestoreCombatPresentation()
        {
            ResetCombatPresentation();
            handViews.Clear();cardMotions.Clear();combatNumbers.Clear();
            for(var i=0;i<combat.hand.Count;i++)
            {
                var slot=HandLayout.Slot(i,combat.hand.Count,CombatWidth,CombatHeight);var card=combat.hand[i];
                handViews[card.instanceId]=new HandView{card=card,position=new Vector2(slot.x,slot.y),angle=slot.angle,scale=1};
            }
            displayResonance=combat.resonance;handReadyAt=0;bossIntroTime=0;lastBossPhase=combat.bossPhase;
            turnBanner="COMBAT RESUMED";turnBannerUntil=Time.unscaledTime+1.1f;
            if(combat.pendingPlay!=null||combat.phase!=CombatPhase.Player)
            {combatBusy=true;combatSequence=StartCoroutine(ResumeSavedCombat());}
        }
        private IEnumerator ResumeSavedCombat()
        {
            if(combat.pendingPlay!=null)
            {
                var card=combat.pendingPlay.card;movingCard=card.instanceId;
                yield return ResolvePendingCardChoices(card);movingCard=-1;
            }
            if(combat.IsOver){yield return FinishCombatIfNeeded();combatBusy=false;combatSequence=null;yield break;}
            else if(combat.phase!=CombatPhase.Player){yield return AnimateEnemyTurn();yield break;}
            SaveCombatCheckpoint();combatBusy=false;combatSequence=null;
        }
        private IEnumerator ResolvePendingCardChoices(CardDef playedCard)
        {
            while(combat.AwaitingChoice)
            {
                choiceSelected=choicePressed=null;choiceOptionSelected=null;choicePage=choiceControllerIndex=0;choiceScroll=0;choicePresented=true;
                if(combat.ChoiceOptions.Count>0){while(choiceOptionSelected==null)yield return null;var option=choiceOptionSelected;choiceOptionSelected=null;choicePresented=false;if(!combat.ChooseOption(option))continue;}
                else{while(choiceSelected==null)yield return null;var selected=choiceSelected;choiceSelected=null;choicePresented=false;if(!combat.ChooseCard(selected))continue;}
                SaveCombatCheckpoint();Sfx(SoundCue.UiConfirm);
                var settle=ConsumeCombatEvents(combat.TakeEvents(),playedCard);
                yield return new WaitForSecondsRealtime(Mathf.Max(settle,AnimationSeconds(.20f)));
            }
            choicePresented=false;
        }
        private bool SubmitCombatChoice(CardDef card)
        {
            if(!choicePresented||combatPauseOpen||choiceSelected!=null||card==null||!combat.ChoiceCards.Contains(card))return false;
            choiceSelected=card;return true;
        }
        private Rect ChoiceViewportRect=>new Rect(34,150,CombatWidth-68,CombatHeight-255);
        private Rect ChoiceCardRect(int absoluteIndex)
        {
            const int columns=6;const float gap=12f;var viewport=ChoiceViewportRect;var cardW=Mathf.Min(184f,(viewport.width-34-gap*(columns-1))/columns);var cardH=cardW*1.34f;var totalW=columns*cardW+(columns-1)*gap;var start=viewport.x+(viewport.width-totalW)*.5f;return new Rect(start+(absoluteIndex%columns)*(cardW+gap),viewport.y+(absoluteIndex/columns)*(cardH+17)+4-choiceScroll,cardW,cardH);
        }
        private void HandleChoicePointer(Vector2 point,bool down,bool up)
        {
            if(!choicePresented||combatPauseOpen)return;
            var cards=combat.ChoiceCards;CardDef hit=null;
            for(var i=0;i<cards.Count;i++)if(ChoiceViewportRect.Contains(point)&&ChoiceCardRect(i).Contains(point)){hit=cards[i];break;}
            if(down)choicePressed=hit;
            if(up){if(hit!=null&&hit==choicePressed)SubmitCombatChoice(hit);choicePressed=null;}
        }
        private void DrawCombatChoice(float w,float h)
        {
            Fill(new Rect(0,0,w,h),new Color(.008f,.012f,.022f,1));
            if(combat.ChoiceOptions.Count>0){DrawCombatOptionChoice(w,h);return;}
            var discard=combat.ChoiceKind==CardChoiceKind.DiscardFromHand;var exhaust=combat.ChoiceKind is CardChoiceKind.ExhaustFromHand or CardChoiceKind.ExhaustCurseFromHand or CardChoiceKind.ExhaustSoulFromHand;var fromExhaust=combat.ChoiceKind is CardChoiceKind.ReturnAttackFromExhaust or CardChoiceKind.ReturnSkillFromExhaust;
            Heading(w,combat.ChoiceKind==CardChoiceKind.ExpansionCard?"CHOOSE A CARD":discard?"CHOOSE A CARD TO DISCARD":exhaust?"CHOOSE A CARD TO DISSIPATE":fromExhaust?"RETURN FROM DISSIPATE":"RETURN A DISCARDED CARD",(combat.pendingPlay?.card.name??"CARD EFFECT")+" · CHOOSE ONE EXACT COPY");
            var cards=combat.ChoiceCards;const int columns=6;const float gap=12f;var viewport=ChoiceViewportRect;var cardW=Mathf.Min(184f,(viewport.width-34-gap*(columns-1))/columns);var cardH=cardW*1.34f;var rowStep=cardH+17;var totalW=columns*cardW+(columns-1)*gap;var start=(viewport.width-totalW)*.5f;var rows=Mathf.CeilToInt(cards.Count/(float)columns);var contentHeight=Mathf.Max(viewport.height,rows*rowStep+8);if(controllerNavigation)KeepGridSelectionVisible(ref choiceScroll,choiceControllerIndex,columns,rowStep,viewport.height,contentHeight);UpdateScrollArea(viewport,ref choiceScroll,contentHeight);
            GUI.BeginGroup(viewport);for(var i=0;i<cards.Count;i++)
            {
                var card=cards[i];var r=new Rect(start+(i%columns)*(cardW+gap),(i/columns)*rowStep+4-choiceScroll,cardW,cardH);if(r.yMax<0||r.y>viewport.height)continue;var global=new Rect(viewport.x+r.x,viewport.y+r.y,r.width,r.height);var hot=global.Contains(combatPointer)||choiceControllerIndex==i;
                DrawCard(r,card);RegisterCardKeywordHelp(global,card);if(hot)Outline(new Rect(r.x-4,r.y-4,r.width+8,r.height+8),Gold,3);
                GUI.Label(new Rect(r.x,r.yMax-22,r.width,18),hot?(discard?"DISCARD":exhaust?"DISSIPATE":"RETURN"):"SELECT",new GUIStyle(footerStyle){fontSize=10,normal={textColor=hot?Gold:new Color(.65f,.69f,.74f)}});
            }
            GUI.EndGroup();DrawScrollRail(viewport,choiceScroll,contentHeight,cards.Count+" ELIGIBLE CARDS  ·  SCROLL");
            GUI.Label(new Rect(w*.2f,h-59,w*.6f,28),"The card is already paid for. Finish this choice, or save and resume it later.",footerStyle);
        }
        private void DrawCombatOptionChoice(float w,float h)
        {
            if(combat.ChoiceKind==CardChoiceKind.ExpansionOption){DrawRemainingOptions(w,h);return;}
            if(combat.ChoiceKind is CardChoiceKind.SigilMode or CardChoiceKind.SigilSlot){DrawSigilChoice(w,h);return;}
            var title=combat.ChoiceKind==CardChoiceKind.AdaptMode?"ADAPT":combat.ChoiceKind==CardChoiceKind.BuffToGain?"RALLY THE FALLEN":"LIMIT BREAK";
            Heading(w,title,(combat.pendingPlay?.card.name??"CARD EFFECT")+" · CHOOSE ONE OUTCOME");var options=combat.ChoiceOptions;var width=250f;var gap=34f;var start=(w-(options.Count*width+(options.Count-1)*gap))*.5f;
            for(var index=0;index<options.Count;index++)
            {
                var option=options[index];var r=new Rect(start+index*(width+gap),h*.36f,width,190);var hot=r.Contains(combatPointer)||choiceControllerIndex==index;Fill(r,new Color(.02f,.025f,.04f,.98f));Outline(r,hot?Gold:new Color(.48f,.42f,.30f),hot?4:2);
                var buffChoice=combat.ChoiceKind is CardChoiceKind.BuffToGain or CardChoiceKind.BuffToDouble;
                if(buffChoice)DrawAtlasIcon(combatReadabilityAtlas,option=="STRENGTH"?2:option=="FORTIFY"?3:4,8,8,new Rect(r.center.x-32,r.y+24,64,64));
                else GUI.Label(new Rect(r.x,r.y+24,r.width,62),option=="BLOCK"?"◆":"✦",new GUIStyle(titleStyle){fontSize=42,normal={textColor=hot?Gold:new Color(.82f,.78f,.68f)}});
                GUI.Label(new Rect(r.x+12,r.y+100,r.width-24,40),option,new GUIStyle(buttonStyle){fontSize=18});GUI.Label(new Rect(r.x+18,r.y+143,r.width-36,30),"SELECT",footerStyle);
                if(GUI.Button(r,"",GUIStyle.none)&&choiceOptionSelected==null){choiceOptionSelected=option;Sfx(SoundCue.UiConfirm);}
            }
            GUI.Label(new Rect(w*.2f,h*.69f,w*.6f,48),combat.ChoiceKind==CardChoiceKind.BuffToGain?$"Gain {combat.pendingPlay.choiceFollowupValue} stacks of one buff you already have.":combat.ChoiceKind==CardChoiceKind.BuffToDouble?"Only active, stackable combat buffs are eligible.":"The same card can answer offense or defense.",new GUIStyle(subtitleStyle){fontSize=18,wordWrap=true});
            var pause=new Rect(w-212,26,180,42);DrawButtonFrame(pause,pause.Contains(combatPointer),false);if(GUI.Button(pause,"PAUSE · ESC",buttonStyle))combatPauseOpen=true;
        }
        private void DrawCombatPause(float w,float h)=>DrawUnifiedPauseMenu(w,h,true);
    }
}
