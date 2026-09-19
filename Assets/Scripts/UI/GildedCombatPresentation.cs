using GildedFate.Audio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Map;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private sealed class HandView
        {
            public CardDef card;
            public Vector2 position;
            public float angle,scale=1,readyAt;
            public bool readinessKnown,playable;
            public float readinessPulse;
        }
        private sealed class CardMotion
        {
            public CardDef card;
            public Vector2 from,to;
            public float start,duration,fromScale,toScale,fromAngle,toAngle;
            public bool exhaust,back,absorb;
        }
        private sealed class CombatEffectChip
        {
            public string code,title,detail,counter;
            public int value;
            public Color color;
            public bool power;
            public CombatEffectChip(string code,string title,string detail,int value,Color color,bool power=false,string counter=null){this.code=code;this.title=title;this.detail=detail;this.value=value;this.color=color;this.power=power;this.counter=counter;}
        }
        private sealed class CombatNumber
        {
            public string text;
            public Vector2 origin;
            public Color color;
            public float start;
        }
        private sealed class RuleKeyword
        {
            public readonly string term,title,detail,hex;
            public RuleKeyword(string term,string title,string detail,string hex){this.term=term;this.title=title;this.detail=detail;this.hex=hex;}
        }
        private static readonly RuleKeyword[] RuleKeywords=
        {
            new("Hidden Potential","HIDDEN POTENTIAL","Attack: +5 damage. Skill: +5 Block, or -1 cost for non-Block Skills. Power: first Power costs 1 less. Lasts this turn.","E1BE68"),
            new("Ruin","RUIN SIGIL","Activation: deal 5 damage to all enemies.","FF8B72"),
            new("Wither","WITHER SIGIL","Activation: apply 1 Weak to all enemies.","AED178"),
            new("Grave Sigil","GRAVE SIGIL","Activation: Exhaust your leftmost Curse or Status; if successful, draw 1.","70E6DA"),
            new("Mirror Sigil","MIRROR SIGIL","Copies the activation effect of the Sigil on its left. Cannot copy another Mirror.","F2F5EE"),
            new("Death's Echo","DEATH'S ECHO","Each card drawn on your turn deals this much damage to a random living enemy.","70E6DA"),
            new("Reaped","REAPED","Souls deal this percentage of additional damage to this enemy for the combat.","70E6DA"),
            new("Gravemark","GRAVEMARK","Grave Execution consumes every stack for separate damage hits.","70E6DA"),
            new("Soulbound","SOULBOUND","Combat-only enchantment. Each Soul played increases the selected card's damage or Block. Does not replace a Binding.","70E6DA"),
            new("Soulbind","SOULBIND","Attach Soulbound to the chosen card for this combat. Each Soul played increases that card's damage or Block. Does not replace a Binding.","70E6DA"),
            new("Discard","DISCARD","Move the card from your hand to the discard pile. It can return when the draw pile reshuffles.","B9C4D5"),
            new("Unplayable","UNPLAYABLE","This card cannot be played. Its written drawn, held or end-of-turn effects still apply.","C5C7CE"),
            new("Retain","RETAIN","Keep this card in hand instead of discarding it at the end of your turn.","A9DCC6"),
            new("Energy","ENERGY","Pays card costs. Your normal Energy is replenished at the start of your turn; unused Energy normally does not carry over.","F6CC61"),
            new("Vulnerable","VULNERABLE","Takes 50% more attack damage while this status is active.","FF806F"),
            new("Resonance","RESONANCE","Hexer's combat resource. Sigil activations and specific effects generate it; cards state how much they spend. No stack cap. Unspent Resonance persists between turns, but resets each combat.","F6CC61"),
            new("Strength","STRENGTH","Adds its value to attack damage. Multi-hit attacks benefit on every hit.","FF9B58"),
            new("Fortify","FORTIFY","Adds its value to numerical Gain Block effects, including attacks and triggered effects, unless a rule explicitly excludes Fortify.","7CD7FF"),
            new("Retaliate","RETALIATE","The next enemy attack takes this much damage back, then consumes all Retaliate. Persists between turns; multi-hit attacks trigger once, even through Block.","FFD072"),
            new("Heavy","HEAVY","Receives its listed bonus if no other Attack has been played this turn.","F0B66A"),
            new("Revenge","REVENGE","Receives its listed bonus if an enemy attacked you during the previous enemy turn.","E89172"),
            new("Marked","MARKED","Occult setup stacks that Arcane cards can consume for stronger effects.","D987FF"),
            new("Sigil","SIGIL","Runes persist in combat slots. Each activation also grants 1 Resonance. Hover a rune for its specific effect.","B99AFF"),
            new("Exhaust","DISSIPATE","Moves the card to the Dissipate pile for this combat. Playing an Aspect activates it separately and does not count as Dissipating.","C8A0FF"),
            new("Ethereal","ETHEREAL","If this remains in hand at end of turn, it Exhausts.","D8C3FF"),
            new("Binding","BINDING","A permanent special modification on this individual card copy.","FFE28A"),
            new("Fateweave","FATEWEAVE MODIFICATION","A permanent strand-of-fate change. It occupies the card's one special-modification slot.","FFE28A"),
            new("Fate Shard","FATE SHARD","A selectable three-use combat power. Only one can be active in a combat.","FFD35A"),
            new("Stable","STABLE","The first two activations use the Shard's stable effect.","8FD6FF"),
            new("Fractured","FRACTURED","The third and final activation is stronger; the Shard shatters after combat.","FF806F"),
            new("Block","BLOCK","Prevents incoming damage before HP is lost. It normally clears on the next turn.","7CD7FF"),
            new("Burn","BURN","On an enemy, deals its stacks after that enemy acts, then loses 1 stack.","FF8A45"),
            new("Weak","WEAK","Deals 25% less attack damage while this status is active.","A9D87B"),
            new("Draw","DRAW","Moves cards from the Draw pile into your hand. Discard reshuffles when Draw is empty.","8FCBFF"),
            new("Soul","SOUL","A temporary 0-cost Skill: deal 3 damage (5 upgraded), draw 1, then Exhaust. Souls vanish after combat.","65E6D2"),
            new("Replay","REPLAY","Resolves the Soul's effect one additional time before that Soul Exhausts.","8FFFF0"),
            new("On Kill","ON KILL","This exact attack must deal the killing blow for its reward to trigger.","F1C86B"),
            new("Temporary","TEMPORARY","Created only for the current combat and never added to the permanent deck.","91C9BE"),
            new("Transform","TRANSFORM","Replaces cards for this combat without triggering their normal Exhaust effects.","70D7C7")
        };
        private readonly Dictionary<int,HandView> handViews=new();
        private readonly Dictionary<int,CombatCardPreview> cardPreviewCache=new();
        private readonly Dictionary<string,int> effectValueCache=new();
        private readonly Dictionary<string,float> effectPulseUntil=new();
        private readonly List<CardMotion> cardMotions=new();
        private readonly List<CombatNumber> combatNumbers=new();
        private readonly List<string> combatHistory=new();
        private HandView hoverView,dragView,selectedView;
        private Vector2 combatPointer,pressPoint,grabOffset;
        private bool cardDragging,combatBusy,combatTestInput,combatHistoryOpen,controllerTargeting;
        private int movingCard=-1,pileOpen=-1,pilePage,controllerHandIndex=-1,controllerShardIndex;
        private float pileScroll;
        private int trackedPlayerHp,trackedEnemyHp;
        private float handReadyAt,turnBannerUntil,displayResonance,resonancePulse,drawPulse,discardPulse,exhaustPulse,powerBadgePulse,energyPulse,targetLockPulse;
        private float playerHealthTrail,enemyHealthTrail,playerHealthTrailHold,enemyHealthTrailHold;
        private float playerAction,enemyAction,heroHit,foeHit,heroBuff,foeBuff,heroDeath,foeDeath,heroVictory,impactShake;
        private EffectKind playerActionKind;
        private string turnBanner="",inputHint="";
        private string combatEffectTooltipTitle,combatEffectTooltipDetail;
        private Vector2 combatEffectTooltipAnchor;
        private float hintUntil;
        private Coroutine combatSequence;
        private static Color Gold=>new Color(1f,.84f,.47f);
        private float CombatScale=>Mathf.Max(.35f,Mathf.Min(Screen.width/1440f,Screen.height/810f));
        private float CombatWidth=>Screen.width/CombatScale;
        private float CombatHeight=>Screen.height/CombatScale;
        private float AnimationSeconds(float seconds)=>seconds/Mathf.Clamp(profile.cardAnimationSpeed,.5f,2f)/(profile.fastMode?1.55f:1f);
        private float EnemyVisualCenterX=>GroupCombat?GroupPortrait(Mathf.Clamp(groupRenderIndex>=0?groupRenderIndex:combatTargetIndex,0,combat.EnemyCount-1)).center.x:CombatWidth*.695f;
        private float EnemyPortraitScale=>EnemyBodyScale(currentEnemy);
        private Rect HeroPortraitRect=>HasHero3D?new Rect(CombatWidth*.22f-125,174,250,276):run.hero==HeroId.Hexer?new Rect(CombatWidth*.22f-93,196,186,254):new Rect(CombatWidth*.22f-93,174,186,276);
        private Rect EnemyPortraitRect
        {
            get
            {
                if(GroupCombat)return GroupPortrait(Mathf.Clamp(groupRenderIndex>=0?groupRenderIndex:combatTargetIndex,0,combat.EnemyCount-1));
                var scale=EnemyPortraitScale;var width=236*scale;var height=226*scale;
                var texture=currentEnemy==null?null:LoadAuthoredArt(GildedArtCatalog.EnemyResource(currentEnemy.id));
                if(texture){var fit=Mathf.Min(width/texture.width,height/texture.height);width=texture.width*fit;height=texture.height*fit;}
                if(height>258){width*=258/height;height=258;}
                return new Rect(EnemyVisualCenterX-width*.5f,438-height,width,height);
            }
        }
        // Target the visible actor with a small forgiving margin, excluding intent and status UI.
        private Rect EnemyDropZone{get{if(GroupCombat)return GroupDropZone(Mathf.Clamp(combatTargetIndex,0,combat.EnemyCount-1));return ComfortableEnemyTarget(EnemyPortraitRect);}}
        // Targetless cards should resolve after a natural upward pull, anywhere across
        // the battlefield. The previous narrow center rectangle made Skills and Powers
        // look ready while rejecting releases that were still just above the hand.
        private Rect SkillDropZone=>new Rect(18,74,CombatWidth-36,Mathf.Max(1,CombatHeight-244));
        private Rect PileRect(int pile)=>pile==0?new Rect(18,CombatHeight-104,72,82):pile==1?
            new Rect(CombatWidth-86,CombatHeight-104,68,78):new Rect(CombatWidth-86,CombatHeight-190,68,78);
        private Rect EnergyMeterRect=>new Rect(103,CombatHeight-112,96,96);
        private Rect EndTurnRect=>new Rect(CombatWidth-286,CombatHeight-155,182,54);
        private Vector2 PilePoint(int pile)=>PileRect(pile).center;
        private Vector2 CardImpactPoint(CardDef card)=>combat!=null&&card!=null&&combat.RequiresEnemyTarget(card)?
            new Vector2(EnemyVisualCenterX,CombatHeight*.34f):card?.kind==CardKind.Power?
            HeroPortraitRect.center+Vector2.right*72:new Vector2(CombatWidth*.43f,CombatHeight*.38f);
        private bool CanAcceptCombatInput=>!combatHudInspectActive&&!ShardDiscoveryOpen&&inspectedCard==null&&!combatPauseOpen&&!choicePresented&&!combatBusy&&Time.unscaledTime>=handReadyAt&&bossIntroTime<=0&&combat!=null&&combat.pendingPlay==null&&!combat.IsOver&&combat.phase==CombatPhase.Player;
        private bool CombatInspectionAllowed=>!controllerNavigation&&!ShardDiscoveryOpen&&inspectedCard==null&&dragView==null&&selectedView==null&&!controllerTargeting&&!cardDragging&&!combatBusy;

        private void ResetCombatPresentation()
        {
            ResetHexerVideos();ResetVanguardVideos();ResetReaperVideos();
            GameAudio.ClearCombat();
            ResetPlayerStatusPlayback();
            if(combatSequence!=null)StopCoroutine(combatSequence);
            handViews.Clear();cardMotions.Clear();combatNumbers.Clear();hoverView=dragView=selectedView=null;
            cardPreviewCache.Clear();effectValueCache.Clear();effectPulseUntil.Clear();combatHistory.Clear();combatHistoryOpen=false;
            combatBusy=cardDragging=controllerTargeting=false;movingCard=pileOpen=controllerHandIndex=controllerFocusedCardId=-1;pilePage=controllerShardIndex=0;pileScroll=0;
            combatHudInspectActive=false;combatNavigationInput=default;
            choicePresented=combatPauseOpen=false;choiceSelected=choicePressed=null;
            heroDeath=foeDeath=heroVictory=heroHit=foeHit=playerAction=enemyAction=0;
            displayResonance=0;resonancePulse=powerBadgePulse=0;inputHint="";
            energyPulse=targetLockPulse=0;identityLastEnergy=-1;
            inspectedCard=null;ResetVitalsPlayback();
            trackedPlayerHp=combat?.player.hp??0;trackedEnemyHp=combat?.enemy.hp??0;playerHealthTrail=trackedPlayerHp;enemyHealthTrail=trackedEnemyHp;playerHealthTrailHold=enemyHealthTrailHold=0;
            handReadyAt=Time.unscaledTime+ConsumeCombatEvents(combat.TakeEvents());
        }
        private void OnApplicationFocus(bool focused)
        {
            if(focused)return;
            dragView=selectedView=hoverView=null;cardDragging=controllerTargeting=false;
        }

        private void UpdateCombatPresentation()
        {
            if(screen!=ScreenMode.Combat||combat==null)return;
            var dt=Mathf.Min(.05f,Time.unscaledDeltaTime);var now=Time.unscaledTime;
            UpdateVitalsPlayback(now);
            UpdateMasterPolishPlayback(now);
            UpdateHealthTrails(dt);
            UpdateHealingPresentation(dt);
            if(!combatTestInput&&!menuInputConsumed&&!ShardDiscoveryOpen&&!acquisitionActive)
            {
                var mouse=Mouse.current;
                if(mouse!=null&&!controllerNavigation)
                {
                    if(combatHudInspectActive&&(mouse.leftButton.wasPressedThisFrame||mouse.delta.ReadValue().sqrMagnitude>16))combatHudInspectActive=false;
                    var physical=mouse.position.ReadValue();
                    var point=guiPointerReady?guiPointerPosition:CanvasPointFromPhysical(physical,Screen.height,CombatScale);
                    HandleCombatPointer(point,mouse.leftButton.wasPressedThisFrame,mouse.leftButton.isPressed,mouse.leftButton.wasReleasedThisFrame);
                    if(mouse.rightButton.wasPressedThisFrame){if(inspectedCard!=null){PrepareCardInspection(inspectedCard);SelectInspectionVersion(true);}else if(dragView!=null||selectedView!=null){dragView=selectedView=null;cardDragging=false;}else if(pileOpen<0&&hoverView!=null)InspectUpgrade(hoverView.card);}
                }
                if(Keyboard.current?.spaceKey.wasPressedThisFrame==true&&CanAcceptCombatInput&&pileOpen<0)QueueEndTurn();
                HandleCombatController();
            }
            ReconcileCombatHandFocus();
            var visible=combat.hand.Where(c=>c.instanceId!=movingCard).ToArray();
            for(var i=0;i<visible.Length;i++)
            {
                if(!handViews.TryGetValue(visible[i].instanceId,out var view))continue;
                if(now<view.readyAt)continue;
                var canPlay=combat.CanPlay(view.card);
                if(!combatBusy){if(view.readinessKnown&&!view.playable&&canPlay)view.readinessPulse=.32f;view.playable=canPlay;view.readinessKnown=true;}
                view.readinessPulse=Mathf.Max(0,view.readinessPulse-dt);
                var slot=HandLayout.Slot(i,visible.Length,CombatWidth,CombatHeight);
                var target=new Vector2(slot.x,slot.y);var angle=slot.angle;var scale=1f;
                if(view==dragView&&cardDragging&&!combat.RequiresEnemyTarget(view.card))
                {
                    // Track the pointer directly; smoothing here introduces noticeable input lag.
                    view.position=combatPointer-grabOffset;view.angle=Mathf.Lerp(view.angle,0,1-Mathf.Exp(-28*dt));view.scale=Mathf.Lerp(view.scale,1.1f,1-Mathf.Exp(-24*dt));continue;
                }
                if(view==dragView&&cardDragging&&combat.RequiresEnemyTarget(view.card))
                {
                    // Targeted cards have weight: they snap forward from their exact
                    // hand slot instead of sticking to the cursor pixel-for-pixel.
                    var pull=Mathf.Clamp01((pressPoint.y-combatPointer.y-24)/105f);var forward=new Vector2(Mathf.Lerp(slot.x,CombatWidth*.49f,.38f),CombatHeight*.57f);
                    target=Vector2.Lerp(new Vector2(slot.x,slot.y),forward,Mathf.SmoothStep(0,1,pull));angle=Mathf.Lerp(slot.angle,0,pull);scale=Mathf.Lerp(1f,1.16f,pull);
                    if(pull>.34f&&TargetAt(combatPointer)>=0)targetLockPulse=1;
                }
                if(view==hoverView||view==selectedView){target.y-=profile.reduceMotion?104:144;angle=0;scale=1.18f;target.x=Mathf.Clamp(target.x,HandLayout.CardWidth*scale*.5f+24,CombatWidth-HandLayout.CardWidth*scale*.5f-24);}
                else if(!profile.reduceMotion)
                {
                    var raised=hoverView??selectedView;
                    if(raised!=null&&dragView==null){var distance=slot.x-raised.position.x;target.x+=Mathf.Sign(distance)*Mathf.Max(0,1-Mathf.Abs(distance)/210)*18;}
                }
                var focused=view==hoverView||view==selectedView;var smooth=1-Mathf.Exp(-(profile.reduceMotion?28:focused?25:18)*dt*Mathf.Clamp(profile.cardAnimationSpeed,.5f,2f));
                view.position=Vector2.Lerp(view.position,target,smooth);view.angle=Mathf.LerpAngle(view.angle,angle,smooth);view.scale=Mathf.Lerp(view.scale,scale,smooth);
            }
            cardMotions.RemoveAll(m=>now>m.start+m.duration);
            powerArrivals.RemoveAll(p=>now>p.landsAt+.18f);
            combatNumbers.RemoveAll(n=>now>n.start+1.1f);
            // The exact receipt count is displayed; pulse it instead of counting
            // through thousands of interpolated intermediate values.
            displayResonance=resonanceTarget;
            resonancePulse=Mathf.Max(0,resonancePulse-dt);powerBadgePulse=Mathf.Max(0,powerBadgePulse-dt*1.7f);drawPulse=Mathf.Max(0,drawPulse-dt*2);discardPulse=Mathf.Max(0,discardPulse-dt*2);exhaustPulse=Mathf.Max(0,exhaustPulse-dt*2);
            playerAction=Mathf.Max(0,playerAction-dt*2.3f);enemyAction=Mathf.Max(0,enemyAction-dt*1.8f);heroHit=Mathf.Max(0,heroHit-dt*3);foeHit=Mathf.Max(0,foeHit-dt*3);heroBuff=Mathf.Max(0,heroBuff-dt*1.5f);foeBuff=Mathf.Max(0,foeBuff-dt*1.5f);impactShake=Mathf.Max(0,impactShake-dt*3.5f);energyPulse=Mathf.Max(0,energyPulse-dt*2.8f);targetLockPulse=Mathf.Max(0,targetLockPulse-dt*4.5f);
        }

        private void HandleCombatController()
        {
            HandleCombatNavigation(combatNavigationInput,Gamepad.current?.startButton.wasPressedThisFrame==true,Keyboard.current?.escapeKey.wasPressedThisFrame==true);
        }
        private void HandleCombatNavigation(MenuNavigation input,bool pause=false,bool escape=false)
        {
            var left=input.x<0;var right=input.x>0;var accept=input.accept;var cancel=input.back;
            if(input.Any){if(!controllerNavigation){inputHint="";hintUntil=0;}controllerNavigation=true;if(dragView!=null){dragView=null;cardDragging=false;}ReconcileCombatHandFocus();}
            if(inspectedCard!=null){PrepareCardInspection(inspectedCard);if(cancel)inspectedCard=inspectionSource=null;else if(left||right||accept)SelectInspectionVersion(!inspectionShowUpgrade);return;}
            if(pause){controllerTargeting=false;selectedView=null;combatHudInspectActive=false;combatPauseOpen=!combatPauseOpen;return;}
            if(combatPauseOpen){if(cancel||accept)combatPauseOpen=false;return;}
            if(combatHistoryOpen){if(cancel||accept)combatHistoryOpen=false;return;}
            if(choicePresented)
            {
                if(escape){combatPauseOpen=true;return;}
                var count=combat.ChoiceOptions.Count>0?combat.ChoiceOptions.Count:combat.ChoiceCards.Count;
                if(count<=0)return;
                var step=input.x+input.y*(combat.ChoiceOptions.Count>0?1:6);
                if(step!=0)choiceControllerIndex=Mathf.Clamp(choiceControllerIndex+step,0,count-1);
                if(accept)
                {
                    if(combat.ChoiceOptions.Count>0){choiceControllerIndex=Mathf.Clamp(choiceControllerIndex,0,combat.ChoiceOptions.Count-1);choiceOptionSelected=combat.ChoiceOptions[choiceControllerIndex];Sfx(SoundCue.UiConfirm);}
                    else{choiceControllerIndex=Mathf.Clamp(choiceControllerIndex,0,combat.ChoiceCards.Count-1);SubmitCombatChoice(combat.ChoiceCards[choiceControllerIndex]);}
                }
                return;
            }
            if(controllerTargeting)
            {
                if(cancel){controllerTargeting=false;selectedView=null;ReconcileCombatHandFocus();ShowInputHint("Back to hand · choose a card");}
                else if(left||right)FocusLivingTarget(right?1:-1);
                else if(accept&&selectedView!=null){controllerTargeting=false;QueueCardPlay(selectedView);}return;
            }
            if(HandleCombatHudNavigation(input))return;
            if(cancel){if(pileOpen>=0)pileOpen=-1;else if(escape)combatPauseOpen=true;else{controllerFocusedCardId=controllerHandIndex=-1;hoverView=null;}dragView=selectedView=null;cardDragging=false;return;}
            if(!CanAcceptCombatInput||pileOpen>=0||combatHistoryOpen)return;
            if(input.category&&menuUsesGamepad){QueueEndTurn();return;}
            var cards=combat.hand.Where(c=>c.instanceId!=movingCard).ToArray();if(cards.Length==0)return;
            if(input.inspect){FocusCombatHand(cards,Mathf.Clamp(controllerHandIndex,0,cards.Length-1));InspectUpgrade(cards[controllerHandIndex]);selectedView=hoverView=null;return;}
            if(left||right)
            {
                FocusCombatHand(cards,controllerHandIndex<0?0:Mathf.Clamp(controllerHandIndex+(right?1:-1),0,cards.Length-1));
                Sfx(SoundCue.UiHover);
            }
            if(accept)
            {
                FocusCombatHand(cards,Mathf.Clamp(controllerHandIndex,0,cards.Length-1));
                if(handViews.TryGetValue(cards[controllerHandIndex].instanceId,out var focus))
                {
                    if(combat.CanPlay(focus.card)&&combat.RequiresEnemyTarget(focus.card)){selectedView=hoverView=focus;controllerTargeting=true;if(!combat.IsLivingTarget(combatTargetIndex))FocusLivingTarget(1);combatPointer=GroupCombat?GroupDropZone(combatTargetIndex).center:EnemyDropZone.center;cardPreviewCache.Clear();ShowInputHint(menuUsesGamepad?"Choose target · A confirm · B back to hand":"Choose target · Enter confirm · Backspace cancel");targetLockPulse=1;Sfx(SoundCue.UiHover);}
                    else if(combat.CanPlay(focus.card))QueueCardPlay(focus);
                    else{hoverView=focus;ShowInputHint(focus.card.unplayable?"This card cannot be played.":"Not enough Energy.");Sfx(SoundCue.UiDenied);}
                }
            }
        }

        private int controllerFocusedCardId=-1;
        private void FocusCombatHand(CardDef[] cards,int index)
        {
            controllerHandIndex=index;controllerFocusedCardId=cards[index].instanceId;
            if(handViews.TryGetValue(controllerFocusedCardId,out var view))hoverView=view;
        }
        private void ReconcileCombatHandFocus()
        {
            if(!controllerNavigation||controllerFocusedCardId<0||combat==null||combatBusy||choicePresented||combatPauseOpen||combatHudInspectActive||inspectedCard!=null||pileOpen>=0||dragView!=null)return;
            var cards=combat.hand.Where(c=>c.instanceId!=movingCard).ToArray();
            if(cards.Length==0){controllerFocusedCardId=controllerHandIndex=-1;selectedView=hoverView=null;controllerTargeting=false;return;}
            var index=System.Array.FindIndex(cards,c=>c.instanceId==controllerFocusedCardId);
            if(index<0){controllerTargeting=false;selectedView=null;index=Mathf.Clamp(controllerHandIndex,0,cards.Length-1);}
            FocusCombatHand(cards,index);
        }
        private void UseCombatPointerMode()
        {
            if(controllerNavigation&&screen==ScreenMode.Combat){selectedView=hoverView=null;controllerTargeting=false;combatHudInspectActive=false;inputHint="";hintUntil=0;}
            controllerNavigation=false;hudNavigationIndex=-1;
        }

        private void UpdateHealthTrails(float dt)
        {
            UpdateHealthTrail(displayedPlayerHp,combat.player.maxHp,ref trackedPlayerHp,ref playerHealthTrail,ref playerHealthTrailHold,dt);
            if(GroupCombat)UpdateOpponentVisuals(dt);else UpdateHealthTrail(displayedEnemyHp,combat.enemy.maxHp,ref trackedEnemyHp,ref enemyHealthTrail,ref enemyHealthTrailHold,dt);
        }

        private static void UpdateHealthTrail(int hp,int maxHp,ref int trackedHp,ref float trail,ref float hold,float dt)
        {
            if(hp<trackedHp){trail=trackedHp;hold=.10f;}
            else if(hp>trackedHp){trail=hp;hold=0;}
            trackedHp=hp;trail=Mathf.Clamp(trail,0,maxHp);
            if(hold>0)hold=Mathf.Max(0,hold-dt);else trail=Mathf.MoveTowards(trail,hp,Mathf.Max(24,maxHp*1.65f)*dt);
        }

        // The real mouse and the isolated QA player call this same path. QA never sends OS input.
        private void HandleCombatPointer(Vector2 point,bool down,bool held,bool up)
        {
            if(controllerNavigation&&(down||held||up||(point-combatPointer).sqrMagnitude>4))UseCombatPointerMode();
            if(controllerNavigation)return;
            if(combatHudInspectActive)return;
            if(controllerTargeting&&!down&&!held&&!up)return;if(controllerTargeting){controllerTargeting=false;selectedView=hoverView=null;}
            combatPointer=point;
            var hoveredEnemy=TargetAt(point);if(hoveredEnemy>=0&&combatTargetIndex!=hoveredEnemy){combatTargetIndex=hoveredEnemy;cardPreviewCache.Clear();}
            if(choicePresented){HandleChoicePointer(point,down,up);return;}
            if(combatHistoryOpen){hoverView=null;return;}
            if(!CanAcceptCombatInput||pileOpen>=0){hoverView=null;return;}
            if(dragView==null)
            {
                var next=PickHandCard(point);
                if(next!=hoverView&&next!=null)Sfx(SoundCue.UiHover);
                hoverView=next;
            }
            if(down)
            {
                selectedView=null;
                // Resolve the pressed card from the current pointer instead of requiring
                // a hover result from the previous frame. Fast move-and-click gestures
                // and low editor frame rates can otherwise fail to begin a drag.
                var pressed=PickHandCard(point);
                hoverView=pressed;
                if(pressed!=null)
                {
                    if(!combat.CanPlay(pressed.card)){ShowInputHint(pressed.card.kind==CardKind.Curse?"This Curse cannot be played.":"Not enough Energy.");Sfx(SoundCue.UiDenied);return;}
                    dragView=pressed;pressPoint=point;grabOffset=point-dragView.position;cardDragging=false;Sfx(SoundCue.CardPickup);
                }
            }
            if(dragView!=null&&held&&Vector2.Distance(pressPoint,point)>8)cardDragging=true;
            if(dragView!=null&&up)
            {
                var released=dragView;var wasDrag=cardDragging;dragView=null;cardDragging=false;
                if(wasDrag)
                {
                    if(IsValidCardDrop(released.card,point))QueueCardPlay(released);
                    else{Sfx(SoundCue.CardReturn);hoverView=null;selectedView=null;ShowInputHint("Card returned · no Energy spent");}
                }
                else {selectedView=null;hoverView=released;ShowInputHint(combat.RequiresEnemyTarget(released.card)?"Drag this card onto the enemy":"Drag this card into the battlefield");}
            }
        }

        private HandView PickHandCard(Vector2 point)
        {
            var cards=combat.hand.Where(c=>c.instanceId!=movingCard).ToArray();
            // A raised card is visually on top, so it must win hit testing before
            // the overlapping resting fan underneath it. This removes hover dropouts.
            if(hoverView!=null&&HandCardContains(point,hoverView.card,hoverView.position.x,hoverView.position.y,hoverView.angle,hoverView.scale))return hoverView;
            if(selectedView!=null&&HandCardContains(point,selectedView.card,selectedView.position.x,selectedView.position.y,selectedView.angle,selectedView.scale))return selectedView;
            // Stable resting slots make every overlapping card reachable without hover jitter.
            for(var i=cards.Length-1;i>=0;i--)
            {
                var slot=HandLayout.Slot(i,cards.Length,CombatWidth,CombatHeight);
                if(HandCardContains(point,cards[i],slot.x,slot.y,slot.angle)&&handViews.TryGetValue(cards[i].instanceId,out var hit))return hit;
            }
            return null;
        }

        private bool IsValidCardDrop(CardDef card,Vector2 point)=>combat.CanPlay(card)&&(combat.RequiresEnemyTarget(card)?TargetAt(point)>=0:SkillDropZone.Contains(point));
        private void ShowInputHint(string message){inputHint=message;hintUntil=Time.unscaledTime+2.4f;}
        private void QueueCardPlay(HandView view)
        {
            if(!CanAcceptCombatInput||view==null||!combat.CanPlay(view.card))return;
            if(combat.RequiresEnemyTarget(view.card)){var target=TargetAt(combatPointer);if(target<0)return;combatTargetIndex=target;}
            combatBusy=true;movingCard=view.card.instanceId;hoverView=dragView=selectedView=null;cardDragging=false;
            combatSequence=StartCoroutine(AnimateCardPlay(view));
        }
        private IEnumerator AnimateCardPlay(HandView view)
        {
            var card=view.card;var gilded=combat.WillGild(card);
            var hadPowerIcon=card.kind==CardKind.Power&&PlayerEffectChips().Any(c=>PowerIconCatalog.Title(c.title)==PowerIconCatalog.Title(card.name));
            var power=card.kind==CardKind.Power;var important=gilded||card.rarity==Rarity.Rare||power;
            var travel=AnimationSeconds(profile.reduceMotion?.09f:important?.32f:.23f);
            var impact=CardImpactPoint(card);
            MoveCard(card,view.position,impact,travel,view.scale,power?.85f:.91f,view.angle,0);
            PlayHexerCard(card,travel);PlayVanguardCard(card,travel);PlayReaperCard(card,travel);
            playerActionKind=card.effect;playerAction=1;Sfx(combat.RequiresEnemyTarget(card)?AttackSound(card):SoundCue.CardPickup,combatSound:true);
            yield return new WaitForSecondsRealtime(travel);
            while(combatPauseOpen)yield return null;
            var energyBefore=combat.energy;var resolved=combat.Play(card,combatTargetIndex);if(combat.energy!=energyBefore)energyPulse=1;
            if(resolved)SaveCombatCheckpoint();
            if(resolved)
            {
                handViews.Remove(card.instanceId);
                var facts=combat.TakeEvents();
                if(!power)MoveCard(card,impact,impact,important?.16f:.08f,.91f,.91f);
                else{powerBadgePulse=1;heroBuff=1;playerVfxIndex=4;playerVfxTime=.48f;}
                var settling=ConsumeCombatEvents(facts,card,important?.16f:.08f);
                if(power)settling=Mathf.Max(settling,SchedulePowerArrival(card,impact,hadPowerIcon));
                var vfx=VfxFor(card.effect);
                if(combat.RequiresEnemyTarget(card)){enemyVfxIndex=vfx;enemyVfxTime=.48f;}else{playerVfxIndex=vfx;playerVfxTime=.48f;}
                if(gilded){gildedFlash=.65f;resonancePulse=1;Sfx(SoundCue.Resonance);}
                UpdateBossPhaseVisual();
                yield return new WaitForSecondsRealtime(Mathf.Max(settling,AnimationSeconds(important?.52f:.33f)));
                yield return ResolvePendingCardChoices(card);
            }
            movingCard=-1;
            yield return FinishCombatIfNeeded();
            combatBusy=false;combatSequence=null;
        }
        private void QueueEndTurn()
        {
            if(!CanAcceptCombatInput||pileOpen>=0)return;
            combatBusy=true;hoverView=dragView=selectedView=null;cardDragging=false;Sfx(SoundCue.EndTurn,combatSound:true);
            combatSequence=StartCoroutine(AnimateEnemyTurn());
        }
        private IEnumerator AnimateEnemyTurn()
        {
            if(combat.phase==CombatPhase.Player){combat.EndPlayerTurn();SaveCombatCheckpoint();}
            var discardTime=ConsumeCombatEvents(combat.TakeEvents());
            yield return new WaitForSecondsRealtime(Mathf.Max(discardTime,AnimationSeconds(.28f)));
            if(!combat.IsOver&&combat.phase==CombatPhase.Enemy)
            {
                enemyAction=1;var anticipate=currentEnemy?.boss==true?.65f:combat.intent==IntentKind.Special?.48f:.30f;
                if(combat.IntentDealsDamage)Sfx(SoundCue.EnemyAttack,pan:.25f,combatSound:true);
                yield return new WaitForSecondsRealtime(AnimationSeconds(profile.reduceMotion?.14f:anticipate));
                while(combatPauseOpen)yield return null;
                combat.ResolveEnemyTurn();SaveCombatCheckpoint();UpdateBossPhaseVisual();
                var effectTime=ConsumeCombatEvents(combat.TakeEvents());
                yield return new WaitForSecondsRealtime(Mathf.Max(effectTime,AnimationSeconds(.44f)));
            }
            if(!combat.IsOver&&combat.phase==CombatPhase.EnemyResolved)
            {
                while(combatPauseOpen)yield return null;
                combat.NextTurn();SaveCombatCheckpoint();var drawTime=ConsumeCombatEvents(combat.TakeEvents());
                handReadyAt=Time.unscaledTime+drawTime;
                yield return new WaitForSecondsRealtime(drawTime);
            }
            yield return FinishCombatIfNeeded();combatBusy=false;combatSequence=null;
        }
        private IEnumerator FinishCombatIfNeeded()
        {
            while(vitalBeats.Count>0)yield return null;
            if(!combat.IsOver)yield break;
            RecordShardShatter();if(combat.player.hp<=0)heroDeath=Time.unscaledTime;else{foeDeath=Time.unscaledTime;heroVictory=Time.unscaledTime;}
            Sfx(combat.player.hp<=0?SoundCue.Defeat:SoundCue.Victory);
            turnBanner=combat.player.hp<=0?"FATE SHATTERED":"VICTORY";turnBannerUntil=Time.unscaledTime+1.2f;
            yield return new WaitForSecondsRealtime(AnimationSeconds(profile.reduceMotion?.25f:.85f));
            CheckCombat();
        }
        private void MoveCard(CardDef card,Vector2 from,Vector2 to,float duration,float fromScale=1,float toScale=.24f,float fromAngle=0,float toAngle=0,float delay=0,bool exhaust=false,bool back=false,bool absorb=false)
        {
            cardMotions.Add(new CardMotion{card=card,from=from,to=to,start=Time.unscaledTime+delay,duration=duration,fromScale=fromScale,toScale=toScale,fromAngle=fromAngle,toAngle=toAngle,exhaust=exhaust,back=back,absorb=absorb});
        }
        private float ConsumeCombatEvents(CombatEvent[] facts,CardDef played=null,float destinationDelay=0)
        {
            cardPreviewCache.Clear();InvalidateEnemyIntents();
            var arrivalTimes=new Dictionary<int,float>();var arrivalPoints=new Dictionary<int,Vector2>();
            var arrivalGap=AnimationSeconds(CombatHitTiming.CardStagger(facts.Count(f=>f.card!=null&&(f.generatedCard||f.kind==CombatEventKind.Draw))));
            var exitGap=AnimationSeconds(CombatHitTiming.CardStagger(facts.Count(f=>f.kind is CombatEventKind.Discard or CombatEventKind.Exhaust),.025f));
            var finish=0f;var startupCardsReady=0f;var drawDelay=0f;var discardDelay=0f;var numberDelay=0f;var now=Time.unscaledTime;var playerNumbers=0;var enemyNumbers=0;
            var hitGap=Mathf.Max(.09f,AnimationSeconds(CombatHitTiming.DefaultHitGap));var hitOffsets=CombatHitTiming.PresentationOffsets(facts,hitGap);var factIndex=0;
            ScheduleRetaliateReturns(facts,hitOffsets,now);
            ScheduleHexerVideoReceipts(facts,hitOffsets,now);
            ScheduleReaperVideoReceipts(facts,hitOffsets,now);
            foreach(var fact in facts)
            {
                var hitTime=hitOffsets[factIndex++];
                ScheduleMasterPolishReceipt(fact,now+hitTime);
                if(fact.playerStatuses!=null)playerStatusBeats.Add((now+hitTime,fact.playerStatuses,fact.playerRetaliation,fact.spiritDirectionsUsed));
                if(fact.enemyStatuses!=null)enemyStatusBeats.Add((now+hitTime,fact.enemyIndex,fact.enemyStatuses));
                AppendCombatHistory(fact,played);
                if(CombatHitTiming.IsRelicWrapper(fact))continue; // Its dedicated relic receipt owns the single pulse/cue.
                if(fact.kind==CombatEventKind.Status&&fact.label=="RETALIATE"&&fact.amount<0)continue; // Consumption is shown by the returning stack, not a debuff burst.
                if(fact.kind==CombatEventKind.RelicTrigger){relicPulseBeats.Add((fact.label,now+hitTime));Sfx(SoundCue.RewardRelic,intensity:.32f,combatSound:true,delay:hitTime);finish=Mathf.Max(finish,hitTime+.26f);continue;}
                if(fact.kind==CombatEventKind.ShardTrigger){run.EnsureShardSlots();var slot=run.shards.FirstOrDefault(s=>s.active&&s.id==fact.label)?.slot??0;var from=ShardHealthOrigin;var to=!fact.playerSide?(GroupCombat?GroupPortrait(fact.enemyIndex).center:EnemyPortraitRect.center):fact.card!=null&&handViews.TryGetValue(fact.card.instanceId,out var targetCard)?targetCard.position:HeroPortraitRect.center;shardFlights.Add(new ShardFlight{from=from,to=to,start=now+hitTime});finish=Mathf.Max(finish,hitTime+.48f);continue;}
                if(fact.kind==CombatEventKind.EnemyAction){opponentActions.Add((fact.enemyIndex,now+hitTime));continue;}
                if(fact.card!=null&&(fact.generatedCard||fact.kind==CombatEventKind.Draw))
                {
                    drawDelay=Mathf.Max(drawDelay,hitTime);
                    var arrival=PresentCardArrival(fact,drawDelay,fact.generatedCard);
                    if(fact.label=="OPENING STATUS")startupCardsReady=Mathf.Max(startupCardsReady,arrival);
                    arrivalTimes[fact.card.instanceId]=arrival;arrivalPoints[fact.card.instanceId]=ReceiptCardTarget(fact);
                    drawDelay+=arrivalGap;finish=Mathf.Max(finish,arrival);continue;
                }
                if(fact.kind==CombatEventKind.Shuffle&&fact.card!=null)
                {
                    var duration=AnimationSeconds(.32f);var source=arrivalPoints.TryGetValue(fact.card.instanceId,out var point)?point:PilePoint(1);
                    var delay=Mathf.Max(hitTime,arrivalTimes.TryGetValue(fact.card.instanceId,out var arrived)?arrived:0);
                    MoveCard(fact.card,source,ReceiptCardTarget(fact),duration,.48f,.21f,delay:delay);
                    finish=Mathf.Max(finish,delay+duration);drawPulse=1;continue;
                }
                if(fact.kind==CombatEventKind.Shuffle)
                {
                    var opening=fact.label=="OPENING";drawDelay=Mathf.Max(drawDelay,Mathf.Max(hitTime,opening?startupCardsReady:0));var duration=AnimationSeconds(.30f);var offset=drawDelay;
                    Sfx(SoundCue.CardShuffle,pan:-.25f,combatSound:true,delay:offset);
                    for(var i=0;i<Mathf.Min(5,fact.amount);i++)MoveCard(null,opening?PilePoint(0)+new Vector2(48,-35):PilePoint(1),PilePoint(0),duration,.22f,.22f,i*7,0,offset+i*.035f,back:true);
                    drawDelay+=duration+.14f;drawPulse=1;if(!opening)discardPulse=1;ShowInputHint(opening?"New cards shuffled into the opening deck":"Discard reshuffled into Draw");finish=Mathf.Max(finish,drawDelay);
                }
                else if(fact.kind==CombatEventKind.Status&&fact.label=="ASPECT ACTIVE"&&fact.card!=null)
                {
                    // The existing identity-to-HUD flight owns the arrival. Installing
                    // an Aspect must not pulse or animate the Dissipate pile.
                    handViews.Remove(fact.card.instanceId);powerBadgePulse=1;
                    finish=Mathf.Max(finish,destinationDelay+AnimationSeconds(.18f));
                }
                else if((fact.kind==CombatEventKind.Discard||fact.kind==CombatEventKind.Exhaust)&&fact.card!=null)
                {
                    var isExhaust=fact.kind==CombatEventKind.Exhaust;var origin=played==fact.card?CardImpactPoint(fact.card):handViews.TryGetValue(fact.card.instanceId,out var view)?view.position:arrivalPoints.TryGetValue(fact.card.instanceId,out var prior)?prior:PilePoint(1);
                    var delay=Mathf.Max(hitTime,played==fact.card?destinationDelay:discardDelay);
                    if(arrivalTimes.TryGetValue(fact.card.instanceId,out var arrived))delay=Mathf.Max(delay,arrived);
                    var duration=AnimationSeconds(isExhaust?.56f:.27f);
                    Sfx(isExhaust?SoundCue.CardExhaust:SoundCue.CardDiscard,pan:.25f,combatSound:true,delay:delay);
                    MoveCard(fact.card,origin,PilePoint(isExhaust?2:1),duration,played==fact.card?.91f:1,.19f,0,isExhaust?35:-12,delay,isExhaust);
                    handViews.Remove(fact.card.instanceId);discardDelay+=exitGap;finish=Mathf.Max(finish,delay+duration);if(isExhaust)exhaustPulse=1;else discardPulse=1;
                }
                else if(fact.kind==CombatEventKind.Damage||fact.kind==CombatEventKind.Block||fact.kind==CombatEventKind.Heal||fact.kind==CombatEventKind.Status)
                {
                    var damage=fact.kind==CombatEventKind.Damage;var block=fact.kind==CombatEventKind.Block;var heal=fact.kind==CombatEventKind.Heal;
                    var trigger=fact.kind==CombatEventKind.Status&&fact.label?.StartsWith("TRIGGER:")==true;var triggerTitle=trigger?PowerIconCatalog.Title(fact.label.Substring(8)):"";
                    var text=damage?"−"+fact.amount:block?(fact.label=="BLOCKED"?"BLOCKED ":"+")+fact.amount+(fact.label=="BLOCKED"?"":" BLOCK"):heal?"+"+fact.amount+" HP":trigger?triggerTitle+"!":fact.label+(fact.amount>0?" +"+fact.amount:"");
                    var color=damage?(fact.playerSide?new Color(1,.4f,.32f):Gold):block?new Color(.48f,.86f,1f):heal?new Color(.52f,1,.68f):new Color(.85f,.67f,1f);
                    var lane=fact.playerSide?playerNumbers++:enemyNumbers++;
                    var health=CombatHitTiming.IsHealthFact(fact);var delay=hitTime;
                    var sourceOnly=trigger||fact.sigilSlot>=0&&fact.label.EndsWith(" ACTIVATE");
                    CombatNumber number=null;
                    if(!sourceOnly)
                    {
                        number=new CombatNumber{text=GameplayTerms.Display(text),origin=new Vector2(fact.playerSide?CombatWidth*.27f:GroupCombat?GroupPortrait(fact.enemyIndex).center.x:EnemyVisualCenterX,235+(lane%4)*35),color=color,start=health?float.PositiveInfinity:now+delay};
                        combatNumbers.Add(number);
                        numberDelay=Mathf.Max(numberDelay,delay+(health?hitGap:.11f));
                    }
                    else finish=Mathf.Max(finish,hitTime+.26f);
                    if(health)vitalBeats.Add(new VitalBeat{fact=fact,source=played,time=now+delay,number=number});
                    else{PlayStatusSound(fact,sourceOnly?hitTime:delay);if(!sourceOnly)statusVisualBeats.Add((now+delay,fact));}
                    if(trigger)
                    {
                        var triggerKey=(fact.playerSide?"P:":"E:"+(GroupCombat?fact.enemyIndex:-1)+":")+triggerTitle;
                        powerPulseBeats.Add((triggerKey,now+hitTime));
                        if(fact.playerSide)expansionPulseStarts[triggerTitle]=now+hitTime;
                    }
                    finish=Mathf.Max(finish,numberDelay+.18f);
                }
                else if(fact.kind==CombatEventKind.Resonance)resonancePulse=1;
                else if(fact.kind==CombatEventKind.Energy){energyPulse=1;Sfx(SoundCue.Energy,intensity:.7f,combatSound:true,delay:hitTime);}
                else if(fact.kind==CombatEventKind.PlayerTurn||fact.kind==CombatEventKind.EnemyTurn)
                {if(fact.kind==CombatEventKind.PlayerTurn)Sfx(SoundCue.TurnStart,intensity:.7f,combatSound:true);turnBanner=fact.kind==CombatEventKind.PlayerTurn?"YOUR TURN":"ENEMY TURN";turnBannerUntil=now+AnimationSeconds(.9f);}
            }
            return finish;
        }

        private CombatCardPreview CardPreview(CardDef card)
        {
            if(card==null)return new CombatCardPreview();
            if(!cardPreviewCache.TryGetValue(card.instanceId,out var preview)){preview=GroupCombat?combat.InspectEnemy(combatTargetIndex,()=>combat.PreviewCard(card)):combat.PreviewCard(card);cardPreviewCache[card.instanceId]=preview;}
            return preview;
        }
        private void AppendCombatHistory(CombatEvent fact,CardDef played)
        {
            if(fact==null)return;string line=null;
            if(fact.kind==CombatEventKind.CardResolved&&fact.card!=null)line=fact.card.name+" resolved.";
            else if(fact.kind==CombatEventKind.Damage)line=(!string.IsNullOrEmpty(fact.label)?fact.label+" · ":"")+(fact.playerSide?run.hero.ToString():currentEnemy?.name??"Enemy")+" lost "+fact.amount+" HP.";
            else if(fact.kind==CombatEventKind.Block)line=fact.label=="BLOCKED"?fact.amount+" damage was absorbed by Block.":(fact.playerSide?run.hero.ToString():currentEnemy?.name??"Enemy")+" gained "+fact.amount+" Block.";
            else if(fact.kind==CombatEventKind.Status&&fact.card?.kind==CardKind.Power)line=fact.card.name+" became a persistent Power.";
            else if(fact.kind==CombatEventKind.Status&&fact.label?.StartsWith("TRIGGER:")==true)line=fact.label.Substring(8)+" triggered.";
            else if(fact.kind==CombatEventKind.Status&&!string.IsNullOrEmpty(fact.label))line=fact.label+" changed by "+(fact.amount>=0?"+":"")+fact.amount+".";
            else if(fact.kind==CombatEventKind.Resonance)line="Resonance is now "+fact.amount+".";
            else if(fact.kind==CombatEventKind.Energy)line="Energy is now "+fact.amount+".";
            else if(fact.kind==CombatEventKind.PlayerTurn||fact.kind==CombatEventKind.EnemyTurn)line=fact.kind==CombatEventKind.PlayerTurn?"Player turn began.":"Enemy turn began.";
            else if(fact.kind==CombatEventKind.Shuffle)line="Discard reshuffled into Draw.";
            if(string.IsNullOrEmpty(line))return;
            combatHistory.Add(line);if(combatHistory.Count>40)combatHistory.RemoveAt(0);
        }

        private void DrawCombat(float w,float h)
        {
            if(combatPauseOpen){DrawCombatPause(w,h);DrawCombatRunBar(w);return;}
            if(choicePresented){DrawCombatChoice(w,h);DrawCombatRunBar(w);return;}
            if(pileOpen>=0){DrawPileInspector(w,h);DrawCombatRunBar(w);return;}
            if(currentEnemy?.boss==true)DrawAtlasIcon(bossArenaAtlas,Mathf.Max(0,System.Array.IndexOf(WorldContent.Enemies,currentEnemy)-14),3,1,new Rect(0,0,w,h));
            else if(combatBackground)GUI.DrawTexture(new Rect(0,0,w,h),combatBackground,ScaleMode.ScaleAndCrop);
            Fill(new Rect(0,0,w,h),new Color(.005f,.008f,.015f,.24f));
            DrawCombat3DStage(w,h);
            DrawCombatRunBar(w);
            if(bossIntroTime>0){DrawBossIntro(w,h);return;}
            DrawCombatAtmosphere(w,h);
            var matrix=GUI.matrix;
            if(profile.screenShake&&!profile.reduceMotion&&impactShake>0)GUI.matrix=matrix*Matrix4x4.Translate(new Vector3(Mathf.Sin(shimmer*61)*impactShake*3,Mathf.Cos(shimmer*47)*impactShake*2,0));
            DrawCombatActors(w,h);GUI.matrix=matrix;
            combatEffectTooltipTitle=combatEffectTooltipDetail=null;DrawCombatStats(w,h);
            DrawTargetGuide();
            Fill(new Rect(0,h-138,w,138),new Color(.005f,.009f,.017f,.18f));
            var hint=Time.unscaledTime<hintUntil?inputHint:combatBusy?"":controllerNavigation?(menuUsesGamepad?"STICK / D-PAD · A PLAY · B BACK · X INSPECT · LB EFFECTS · Y END TURN":"ARROWS  SELECT CARD · ENTER  PLAY · BACKSPACE  CANCEL"):"DRAG A CARD TO PLAY · RIGHT-CLICK TO CANCEL";
            GUI.Label(new Rect(w*.5f-330,h-292,660,26),hint,new GUIStyle(footerStyle){fontSize=14,normal={textColor=new Color(.88f,.83f,.7f)}});
            DrawCombatHand();DrawCombatMotions();DrawRetaliateReturns();DrawCombatControls();DrawCombatNativeHitTargets();DrawCombatNumbers();DrawCardKeywordHelp(w,h);DrawCombatEffectTooltip(w,h);DrawPersistentRunTooltip(w,h);
            if(!profile.reduceFlashing&&gildedFlash>0)Fill(new Rect(0,0,w,h),new Color(1,.7f,.25f,gildedFlash*.12f));
            if(Time.unscaledTime<turnBannerUntil)
            {
                var alpha=Mathf.Clamp01((turnBannerUntil-Time.unscaledTime)*4);
                Fill(new Rect(w*.34f,132,w*.32f,44),new Color(.015f,.02f,.035f,alpha*.92f));
                GUI.Label(new Rect(w*.34f,132,w*.32f,44),turnBanner,new GUIStyle(buttonStyle){fontSize=22,normal={textColor=new Color(1,.88f,.57f,alpha)}});
            }
            if(bossPhaseTime>0)DrawBossPhaseTransition(w,h);
        }

        private void DrawCombatHand()
        {
            foreach(var card in combat.hand)
                if(card.instanceId!=movingCard&&handViews.TryGetValue(card.instanceId,out var view)&&view!=hoverView&&view!=selectedView&&view!=dragView)DrawHandView(view);
            if(selectedView!=null&&selectedView!=dragView)DrawHandView(selectedView);
            if(hoverView!=null&&hoverView!=selectedView&&hoverView!=dragView)DrawHandView(hoverView);
            if(dragView!=null)DrawHandView(dragView);
        }
        private void DrawCombatNativeHitTargets()
        {
            if(!CanAcceptCombatInput||pileOpen>=0||combatHistoryOpen)return;
            // A dragged targetless card follows the pointer. Leaving a GUI.Button on
            // that moving card lets IMGUI claim MouseUp and cancel the manual drag
            // before it resolves. Once any drag begins, pointer routing owns release.
            if(dragView!=null)return;
            // These coordinate-safe hit targets only focus cards. Mouse play is
            // deliberately drag-and-drop so a stray click can never spend Energy.
            var ordered=new List<HandView>();
            void Add(HandView view){if(view!=null&&!ordered.Contains(view))ordered.Add(view);}
            Add(selectedView);Add(hoverView);
            var cards=combat.hand.Where(card=>card.instanceId!=movingCard).ToArray();
            for(var i=cards.Length-1;i>=0;i--)if(handViews.TryGetValue(cards[i].instanceId,out var view))Add(view);
            foreach(var view in ordered)
            {
                if(Time.unscaledTime<view.readyAt)continue;
                var matrix=GUI.matrix;GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(view.position.x,view.position.y,0),Quaternion.Euler(0,0,view.angle),new Vector3(view.scale,view.scale,1));
                var clicked=GUI.Button(new Rect(-HandLayout.CardWidth*.5f,-HandLayout.CardHeight*.5f,HandLayout.CardWidth,HandLayout.CardHeight),"",GUIStyle.none);GUI.matrix=matrix;
                if(!clicked)continue;
                if(!combat.CanPlay(view.card)){ShowInputHint(view.card.kind==CardKind.Curse?"This Curse cannot be played.":"Not enough Energy.");Sfx(SoundCue.UiDenied);return;}
                selectedView=null;hoverView=view;dragView=null;cardDragging=false;
                ShowInputHint(combat.RequiresEnemyTarget(view.card)?"Drag this card onto the enemy":"Drag this card into the battlefield");Sfx(SoundCue.UiHover);
                return;
            }
        }
        private void DrawHandView(HandView view)
        {
            if(Time.unscaledTime<view.readyAt)return;
            var focus=view==hoverView||view==selectedView||view==dragView;
            drawingDisabledHandCard=view.readinessKnown&&!view.playable;
            try{DrawMovingCard(view.card,view.position,view.angle,view.scale,focus,1);}
            finally{drawingDisabledHandCard=false;}
        }
        private void DrawMovingCard(CardDef card,Vector2 center,float angle,float scale,bool focus,float alpha,bool back=false)
        {
            var matrix=GUI.matrix;var old=GUI.color;
            GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(center.x,center.y,0),Quaternion.Euler(0,0,angle),new Vector3(scale,scale,1));GUI.color=new Color(1,1,1,alpha);
            var r=new Rect(-HandLayout.CardWidth*.5f,-HandLayout.CardHeight*.5f,HandLayout.CardWidth,HandLayout.CardHeight);
            if(back||card==null)
            {
                Fill(r,new Color(.027f,.033f,.06f));Outline(r,Gold,3);Outline(new Rect(r.x+12,r.y+12,r.width-24,r.height-24),new Color(.48f,.38f,.2f),1);
                var oldMatrix=GUI.matrix;GUIUtility.RotateAroundPivot(45,Vector2.zero);Outline(new Rect(-43,-43,86,86),Gold,3);GUI.matrix=oldMatrix;
                GUI.Label(new Rect(r.x,-24,r.width,48),"FATE",new GUIStyle(buttonStyle){fontSize=25});
            }
            else
            {
                DrawIdentityCardLight(r,card,focus);
                DrawCard(r,card);
            }
            GUI.color=old;GUI.matrix=matrix;
        }
        private static string FormatCardRules(string text,CardDef card=null)
        {
            if(string.IsNullOrEmpty(text))return text;
            // Match once, longest mechanic first, outside existing color spans. Dynamic
            // green/red values and upgrade-diff markup must never be recolored.
            var output=new System.Text.StringBuilder();var colored=0;
            foreach(var chunk in System.Text.RegularExpressions.Regex.Split(text,@"(<[^>]+>)"))
            {
                if(chunk.StartsWith("<")){if(chunk.StartsWith("<color",StringComparison.OrdinalIgnoreCase))colored++;else if(chunk.StartsWith("</color",StringComparison.OrdinalIgnoreCase))colored=Math.Max(0,colored-1);output.Append(chunk);continue;}
                output.Append(colored>0?chunk:RuleMechanicPattern.Replace(chunk,match=>
                {
                    var term=match.Groups["term"].Value;
                    var keyword=RuleKeywords.First(k=>string.Equals(k.term,CanonicalKeywordTerm(term),StringComparison.OrdinalIgnoreCase));
                    return "<b><color=#"+CardKeywordHex(keyword,card)+">"+match.Value+"</color></b>";
                }));
            }
            return GameplayTerms.Display(output.ToString());
        }
        private static readonly System.Text.RegularExpressions.Regex RuleMechanicPattern=new(
            @"(?<![\w])(?:[+\-]?\d+(?:\.\d+)?%?[ \t]+)?(?<term>"+
            string.Join("|",RuleKeywords.OrderByDescending(k=>k.term.Length).Select(k=>System.Text.RegularExpressions.Regex.Escape(k.term)))+"|Dissipat(?:e[sd]?|ing)"+
            @")(?:(?<=Exhaust)(?:s|ed|ing)|(?<=Discard)(?:s|ed|ing)|(?<=Retain)(?:s|ed|ing)|s)?\b(?:[ \t]+[+\-]?\d+(?:\.\d+)?%?)?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.Compiled);
        private static string CardKeywordHex(RuleKeyword keyword,CardDef card)
        {
            if(card==null||CardUsesDarkRules(card))return keyword.hex;
            return keyword.term switch
            {
                "Vulnerable"=>"A52B25","Resonance"=>"765000","Strength"=>"AA4218","Fortify"=>"006784","Retaliate"=>"765000",
                "Heavy"=>"7C4E0C","Revenge"=>"8A3829","Marked"=>"71258F","Sigil"=>"58368B","Exhaust"=>"5D367C",
                "Ethereal"=>"594075","Binding"=>"6C4C08","Fateweave"=>"6C4C08","Fate Shard"=>"6C4C08","Stable"=>"165B7A",
                "Fractured"=>"A52B25","Block"=>"006784","Burn"=>"A93D0E","Weak"=>"486A1B","Draw"=>"1A587F","Soul"=>"147C72","Replay"=>"147C72","On Kill"=>"7A4E08","Temporary"=>"476F68","Transform"=>"147C72",_=>keyword.hex
            };
        }
        private static string CanonicalKeywordTerm(string term)=>term.StartsWith("Dissipat",StringComparison.OrdinalIgnoreCase)?"Exhaust":term;
        private static bool HasRuleKeyword(string text,RuleKeyword keyword)=>!string.IsNullOrEmpty(text)&&
            RuleMechanicPattern.Matches(StripRichTags.Replace(text,"")).Cast<System.Text.RegularExpressions.Match>().Any(m=>string.Equals(CanonicalKeywordTerm(m.Groups["term"].Value),keyword.term,StringComparison.OrdinalIgnoreCase));
        private void RegisterCardKeywordHelp(Rect globalRect,CardDef card,bool focused=false)
        {
            RegisterInspectionInput(globalRect,card);
            if(card==null||profile?.tooltips!=true||acquisitionActive||!(focused||!controllerNavigation&&CardHelpContains(globalRect,card,PointerPosition)))return;
            if(CardAttachments(card).Count==0&&!RuleKeywords.Any(keyword=>HasRuleKeyword(card.text,keyword)))return;
            hoveredCardHelp=card;hoveredCardHelpAnchor=globalRect;
        }
        private void DrawScreenCardKeywordHelp(float w,float h)
        {
            if(hoveredCardHelp==null||profile?.tooltips!=true||acquisitionActive)return;
            if(screen==ScreenMode.Combat&&(hoverView!=null||selectedView!=null||dragView!=null))return;
            var title=hoveredCardHelp.name;var detail=CardGlossaryDetail(hoveredCardHelp);
            if(CardAttachmentHelp(hoveredCardHelpAnchor,hoveredCardHelp,PointerPosition,out var attachmentTitle,out var attachmentDetail)){title=attachmentTitle;detail=attachmentDetail;}
            if(string.IsNullOrEmpty(detail))return;
            const float width=330;
            var x=hoveredCardHelpAnchor.center.x<w*.55f?hoveredCardHelpAnchor.xMax+16:hoveredCardHelpAnchor.x-width-16;
            DrawTooltip(new Rect(x,hoveredCardHelpAnchor.y+12,width,116),title,detail,hoveredCardHelpAnchor);
        }
        private void DrawCardKeywordHelp(float w,float h)
        {
            var active=hoverView??selectedView??dragView;if(active?.card==null||profile?.tooltips!=true||cardDragging&&dragView!=null)return;
            var title=active.card.name;var detail=CardGlossaryDetail(active.card,CardPreview(active.card));
            var local=HandCardLocalPoint(combatPointer,active.position.x,active.position.y,active.angle,active.scale);
            if(!controllerNavigation&&CardAttachmentHelp(new Rect(-HandLayout.CardWidth*.5f,-HandLayout.CardHeight*.5f,HandLayout.CardWidth,HandLayout.CardHeight),active.card,local,out var attachmentTitle,out var attachmentDetail)){title=attachmentTitle;detail=attachmentDetail;}
            if(string.IsNullOrEmpty(detail))return;
            const float width=330;var half=HandLayout.CardWidth*active.scale*.5f;
            var x=active.position.x<w*.56f?active.position.x+half+20:active.position.x-half-width-20;
            DrawTooltip(new Rect(x,active.position.y-HandLayout.CardHeight*active.scale*.5f,width,116),title,detail);
        }
        private void DrawCombatMotions()
        {
            if(combat.pendingPlay!=null&&!choicePresented)DrawMovingCard(combat.pendingPlay.card,CardImpactPoint(combat.pendingPlay.card),0,.8f,false,1);
            foreach(var motion in cardMotions)
            {
                var t=(Time.unscaledTime-motion.start)/motion.duration;if(t<0||t>1)continue;
                var eased=t*t*(3-2*t);var destination=motion.absorb&&motion.card!=null?PowerHudTarget(motion.card).center:motion.to;var p=Vector2.Lerp(motion.from,destination,eased);
                if(!profile.reduceMotion)p.y-=Mathf.Sin(t*Mathf.PI)*(motion.back?65:38);
                if(motion.exhaust)p=motion.from+new Vector2(0,profile.reduceMotion?0:-Mathf.SmoothStep(.22f,1f,t)*18);
                // A readable resolve beat precedes the calm purple essence fade.
                var alpha=motion.exhaust?1-Mathf.SmoothStep(.22f,1f,t):motion.absorb?1-Mathf.SmoothStep(.12f,.72f,t):1;
                DrawMovingCard(motion.card,p,motion.exhaust?motion.fromAngle:Mathf.Lerp(motion.fromAngle,motion.toAngle,eased),motion.exhaust?motion.fromScale*(1-.08f*eased):Mathf.Lerp(motion.fromScale,motion.toScale,eased),false,alpha,motion.back);
                if(motion.exhaust&&!profile.reduceMotion&&!profile.reducedVfx&&t>.22f)for(var i=0;i<6;i++){var drift=(t-.22f)/.78f;var x=(i-2.5f)*9+Mathf.Sin(i+drift*2)*4;Fill(new Rect(p.x+x,p.y-drift*(22+i*4),2,3),new Color(.80f,.59f,.94f,Mathf.Sin(drift*Mathf.PI)*.6f));}
                if(motion.absorb)
                {
                    var accent=motion.card?.hero==HeroId.Reaper?new Color(.30f,.92f,.82f):motion.card?.hero==HeroId.Hexer?new Color(.73f,.49f,1):Gold;
                    var flare=Mathf.Sin(Mathf.Clamp01((t-.25f)/.75f)*Mathf.PI);
                    if(motion.card!=null&&t>.16f)
                    {
                        var iconSize=Mathf.Lerp(62,38,Mathf.Clamp01((t-.16f)/.84f));
                        var old=GUI.color;GUI.color=new Color(1,1,1,Mathf.Clamp01((t-.16f)/.24f));
                        DrawPowerTravelIcon(new Rect(p.x-iconSize*.5f,p.y-iconSize*.5f,iconSize,iconSize),motion.card);
                        GUI.color=old;
                    }
                    if(!profile.reduceMotion&&!profile.reduceFlashing&&!profile.reducedVfx)
                        for(var i=0;i<5;i++){var angle=i*Mathf.PI*.4f+shimmer;var radius=(1-t)*26+5;Fill(new Rect(p.x+Mathf.Cos(angle)*radius-1,p.y+Mathf.Sin(angle)*radius-1,2,2),new Color(accent.r,accent.g,accent.b,flare*.6f));}
                }
            }
        }

        private void DrawTargetGuide()
        {
            var active=dragView??selectedView;if(active==null)return;
            var enemy=combat.RequiresEnemyTarget(active.card);var target=enemy?EnemyDropZone:SkillDropZone;var valid=enemy?TargetAt(combatPointer)>=0:target.Contains(combatPointer);
            var pull=dragView==null||!cardDragging?1f:Mathf.Clamp01((pressPoint.y-combatPointer.y-24)/105f);
            if(enemy&&pull<.34f)
            {
                GUI.Label(new Rect(active.position.x-150,active.position.y-HandLayout.CardHeight*.72f,300,28),"PULL UP TO TARGET",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=13,normal={textColor=new Color(.92f,.82f,.58f)}});return;
            }
            var color=valid?Gold:enemy?new Color(1,.42f,.3f):new Color(.46f,.81f,1);
            if(enemy)
            {
                // Recede everything except the valid target without placing an
                // opaque panel over the enemy itself.
                Fill(new Rect(0,66,target.x,CombatHeight-66),new Color(0,0,0,.20f));Fill(new Rect(target.xMax,66,CombatWidth-target.xMax,CombatHeight-66),new Color(0,0,0,.20f));Fill(new Rect(target.x,66,target.width,Mathf.Max(0,target.y-66)),new Color(0,0,0,.13f));Fill(new Rect(target.x,target.yMax,target.width,CombatHeight-target.yMax),new Color(0,0,0,.13f));
            }
            // Brackets identify the target without covering the illustration in a solid box.
            foreach(var corner in new[]{new Vector2(target.x,target.y),new Vector2(target.xMax,target.y),new Vector2(target.x,target.yMax),new Vector2(target.xMax,target.yMax)})
            {var dx=corner.x==target.x?1:-1;var dy=corner.y==target.y?1:-1;DrawLine(corner,corner+new Vector2(24*dx,0),color,valid?4:2);DrawLine(corner,corner+new Vector2(0,24*dy),color,valid?4:2);}
            GUI.Label(new Rect(target.x,target.y-28,target.width,25),valid?"RELEASE TO PLAY":enemy?"TARGET ENEMY":"PLAY IN THE BATTLEFIELD",new GUIStyle(footerStyle){fontSize=16,normal={textColor=color}});
            if(enemy)
            {
                // Slay-the-Spire-style targeting: the attack remains anchored in the
                // hand and the arrowhead follows the cursor. The previous version drew
                // this backwards, which made valid targets feel badly offset.
                var start=new Vector2(active.position.x,active.position.y-HandLayout.CardHeight*active.scale*.43f);
                var end=combatPointer;var previous=start;
                if(Vector2.Distance(start,end)>8)
                {
                    for(var i=1;i<=22;i++){var t=i/22f;var next=Vector2.Lerp(start,end,t)-Vector2.up*Mathf.Sin(t*Mathf.PI)*42;DrawLine(previous,next,new Color(color.r,color.g,color.b,.85f),3);previous=next;}
                    var direction=(end-previous).normalized;var perpendicular=new Vector2(-direction.y,direction.x);
                    DrawLine(end,end-direction*18+perpendicular*9,color,3);DrawLine(end,end-direction*18-perpendicular*9,color,3);
                }
                var preview=CardPreview(active.card);var shownEnemy=combat.EnemyAt(Mathf.Clamp(combatTargetIndex,0,combat.EnemyCount-1));var damage=preview.DamageExpression;if(preview.triggeredDamage>0)damage+=" + "+preview.triggeredDamage+" TRIGGERED";var absorbed=Mathf.Min(shownEnemy.block,preview.blockedByTarget);var lost=preview.hpDamage;var result=new Rect(target.center.x-160,target.yMax+8,320,42);
                Fill(result,new Color(.008f,.012f,.019f,.96f));Outline(result,color,2);GUI.Label(result,$"{damage} DAMAGE   ·   BLOCK {shownEnemy.block}→{Mathf.Max(0,shownEnemy.block-absorbed)}   ·   HP {shownEnemy.hp}→{Mathf.Max(0,shownEnemy.hp-lost)}",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,alignment=TextAnchor.MiddleCenter,normal={textColor=color}});
                if(valid&&targetLockPulse>0)Outline(new Rect(target.x-5,target.y-5,target.width+10,target.height+10),new Color(1f,.88f,.52f,targetLockPulse),4);
            }
        }

        private void DrawCombatRunBar(float w)
        {
            DrawPersistentRunBar(w,true);
        }

        private void DrawCombatHistory(float w,float h)
        {
            Fill(new Rect(0,66,w,h-66),new Color(0,0,0,.38f));var panel=new Rect(w-430,82,400,h-172);Fill(panel,new Color(.006f,.011f,.019f,.985f));Outline(panel,new Color(.76f,.61f,.34f),2);
            GUI.Label(new Rect(panel.x+24,panel.y+18,panel.width-48,34),"COMBAT HISTORY",new GUIStyle(titleStyle){fontSize=23,alignment=TextAnchor.MiddleLeft,normal={textColor=Gold}});
            GUI.Label(new Rect(panel.x+24,panel.y+52,panel.width-48,22),"MOST RECENT EVENTS · WHY NUMBERS CHANGED",new GUIStyle(footerStyle){fontSize=10,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.69f,.68f,.65f)}});
            var shown=combatHistory.Skip(Mathf.Max(0,combatHistory.Count-13)).Reverse().ToArray();for(var i=0;i<shown.Length;i++){var row=new Rect(panel.x+22,panel.y+86+i*36,panel.width-44,31);if(i%2==0)Fill(row,new Color(1f,1f,1f,.025f));GUI.Label(new Rect(row.x+9,row.y,row.width-18,row.height),shown[i],new GUIStyle(footerStyle){fontSize=12,alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip,normal={textColor=i==0?new Color(1f,.9f,.65f):new Color(.88f,.86f,.8f)}});}
            if(shown.Length==0)GUI.Label(new Rect(panel.x+25,panel.y+104,panel.width-50,40),"Actions will appear here as combat resolves.",new GUIStyle(footerStyle){fontSize=13,alignment=TextAnchor.MiddleLeft});
            var close=new Rect(panel.x+95,panel.yMax-55,panel.width-190,36);DrawButtonFrame(close,close.Contains(combatPointer),false);if(GUI.Button(close,"CLOSE · B / ESC",new GUIStyle(buttonStyle){fontSize=12}))combatHistoryOpen=false;
        }

        private void DrawCombatControls()
        {
            var names=new[]{"DECK","DISCARD","DISSIPATE"};var counts=new[]{combat.draw.Count,combat.discard.Count,combat.exhaust.Count};var icons=new[]{12,13,14};
            for(var i=0;i<3;i++)
            {
                var r=PileRect(i);var hovered=r.Contains(combatPointer);var icon=new Rect(r.center.x-27,r.y+19,54,54);
                if(hovered&&!profile.reduceMotion){var old=GUI.color;GUI.color=new Color(1f,.78f,.32f,.25f);DrawAtlasIcon(combatReadabilityAtlas,icons[i],8,8,new Rect(icon.x-5,icon.y-5,icon.width+10,icon.height+10));GUI.color=old;}
                DrawAtlasIcon(combatReadabilityAtlas,icons[i],8,8,icon);
                GUI.Label(new Rect(r.x-8,r.y,r.width+16,17),names[i],new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=9,fontStyle=FontStyle.Bold,normal={textColor=hovered?Color.white:Gold}});
                GUI.Label(new Rect(icon.xMax-15,icon.yMax-25,34,26),counts[i].ToString(),new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=18,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}});
                if(hovered)SetCombatEffectTooltip(names[i]+" · "+counts[i],i==0?"Cards waiting to be drawn. Click to inspect them alphabetically without revealing draw order.":i==1?"Played and discarded cards wait here. When the Draw pile empties, this pile is shuffled back in.":"Cards removed for the rest of this combat. Click to inspect them.",r.center);
                if(GUI.Button(r,"",GUIStyle.none)&&!combatBusy){pileOpen=i;pilePage=0;pileScroll=0;selectedView=dragView=hoverView=null;Sfx(SoundCue.UiHover);}
            }
            DrawEnergyMeter(EnergyMeterRect);
            var end=EndTurnRect;var enabled=CanAcceptCombatInput&&pileOpen<0;
            DrawButtonFrame(end,!controllerNavigation&&end.Contains(combatPointer)&&enabled,!enabled);GUI.enabled=enabled;
            if(GUI.Button(end,combatBusy?"RESOLVING…":"END TURN",buttonStyle))QueueEndTurn();GUI.enabled=true;
            GUI.Label(new Rect(end.x,end.y-26,end.width,22),"HAND  "+combat.hand.Count+(controllerNavigation&&menuUsesGamepad?"     ·     Y":"     ·     SPACE"),footerStyle);
        }
        private void DrawEnergyMeter(Rect r)
        {
            var baseEnergy=3+(run.relics.Contains("gilded_heart")?1:0);var index=run.hero==HeroId.Hexer?1:run.hero==HeroId.Reaper?2:0;
            var color=index==1?new Color(.69f,.39f,.90f):index==2?new Color(.28f,.83f,.73f):new Color(.96f,.64f,.26f);
            if(identityLastEnergy!=combat.energy){identityEnergyGain=combat.energy>identityLastEnergy;identityEnergyChangedAt=identityLastEnergy<0?-10:Time.unscaledTime;identityLastEnergy=combat.energy;}
            var seal=new Rect(r.x,r.y,r.width,r.height-12);var old=GUI.color;
            var reaction=profile.reduceFlashing?0:1-Mathf.Clamp01((Time.unscaledTime-identityEnergyChangedAt)/.36f);
            GUI.color=Color.Lerp(combat.energy>0?Color.white:new Color(.56f,.56f,.60f),identityEnergyGain?new Color(1.16f,1.12f,1.08f):new Color(.66f,.66f,.70f),reaction*.65f);
            DrawIdentityEnergy(seal,index);GUI.color=old;
            DrawIdentityEnergyReaction(seal,color,reaction);
            GUI.Label(seal,$"{combat.energy}<size=15>/{baseEnergy}</size>",new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=31,richText=true,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}});
            GUI.Label(new Rect(r.x-4,r.yMax-14,r.width+8,16),"ENERGY",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=8,normal={textColor=Color.Lerp(color,Color.white,.28f)}});
            if(r.Contains(combatPointer))SetCombatEffectTooltip("ENERGY",$"{combat.energy} available now. Your normal turn begins with {baseEnergy}. Cards spend the value shown in their Energy vessel.",r.center);
        }

        private void DrawPileInspector(float w,float h)
        {
            var modal=inspectedCard!=null;
            Fill(new Rect(0,0,w,h),new Color(.004f,.008f,.016f,1f));
            var pile=pileOpen==0?combat.draw:pileOpen==1?combat.discard:combat.exhaust;
            // Draw inspection intentionally does not reveal the random upcoming order.
            var cards=pileOpen==0?pile.OrderBy(c=>c.name).ThenBy(c=>c.instanceId).ToArray():pile.ToArray();
            Heading(w,(pileOpen==0?"DRAW PILE":pileOpen==1?"DISCARD PILE":"DISSIPATE PILE")+"  ·  "+cards.Length,pileOpen==0?"CONTENTS SHOWN ALPHABETICALLY · DRAW ORDER HIDDEN":pileOpen==2?"REMOVED FOR THE REST OF THIS COMBAT":"RETURNS TO DRAW WHEN THE DECK IS EMPTY");
            const int columns=6;const float gap=12f;var viewport=new Rect(34,150,w-68,h-228);var cardW=Mathf.Min(186f,(viewport.width-34-gap*(columns-1))/columns);var cardH=cardW*1.48f;var rowStep=cardH+17;var totalW=columns*cardW+(columns-1)*gap;var start=(viewport.width-totalW)*.5f;var rows=Mathf.CeilToInt(cards.Length/(float)columns);var contentHeight=Mathf.Max(viewport.height,rows*rowStep+8);if(!modal)UpdateScrollArea(viewport,ref pileScroll,contentHeight);
            GUI.BeginGroup(viewport);for(var index=0;index<cards.Length;index++){var row=index/columns;var col=index%columns;var r=new Rect(start+col*(cardW+gap),row*rowStep+4-pileScroll,cardW,cardH);if(r.yMax<0||r.y>viewport.height)continue;DrawCard(r,cards[index]);if(!modal){RegisterCardKeywordHelp(new Rect(viewport.x+r.x,viewport.y+r.y,r.width,r.height),cards[index]);if(GUI.Button(r,"",GUIStyle.none)){inspectedCard=cards[index];Sfx(SoundCue.UiConfirm);}}}GUI.EndGroup();DrawScrollRail(viewport,pileScroll,contentHeight,cards.Length+" CARDS  ·  SCROLL");
            if(cards.Length==0)GUI.Label(new Rect(0,300,w,60),"This pile is empty.",subtitleStyle);
            if(modal)return;
            var close=new Rect(w*.5f-90,h-62,180,40);DrawButtonFrame(close,close.Contains(combatPointer),false);if(GUI.Button(close,"CLOSE",buttonStyle)){pileOpen=-1;Sfx(SoundCue.UiHover);}
        }

        private void DrawCombatActors(float w,float h)
        {
            var now=Time.unscaledTime;var motion=profile.reduceMotion?0:1;
            var hero=HeroPortraitRect;var foe=EnemyPortraitRect;
            var videoHero=DrawHexerVideo(hero)||DrawVanguardVideo(hero)||DrawReaperVideo(hero);
            var breathe=1+Mathf.Sin(shimmer*1.65f)*.008f*motion;
            var attack=playerActionKind==EffectKind.Damage?Mathf.Sin((1-playerAction)*Mathf.PI)*playerAction:0;
            hero.x+=attack*38*motion+Mathf.Sin(shimmer*58)*heroHit*5*motion;
            if(heroVictory>0)hero.y-=Mathf.Sin(Mathf.Clamp01((now-heroVictory)*2)*Mathf.PI)*12*motion;
            if(heroDeath>0)hero.y+=Mathf.Clamp01(now-heroDeath)*35;
            var old=GUI.color;GUI.color=heroDeath>0?new Color(1,.6f,.6f,1-Mathf.Clamp01(now-heroDeath)*.8f):Color.Lerp(Color.white,new Color(1,.6f,.48f),heroHit*.5f);
            var matrix=GUI.matrix;if(!HasHero3D&&!videoHero){GUIUtility.ScaleAroundPivot(new Vector2(1,breathe),new Vector2(hero.center.x,hero.yMax));DrawHeroPortrait(hero,run.hero);}GUI.matrix=matrix;GUI.color=old;
            if(GroupCombat){DrawGroupActors();DrawCombatVfx(hero.center,215,playerVfxIndex,playerVfxTime);return;}
            var anticipation=enemyAction>.55f?-(enemyAction-.55f)*16:Mathf.Sin(enemyAction/.55f*Mathf.PI)*-37;
            foe.x+=(anticipation+Mathf.Sin(shimmer*63)*foeHit*7)*motion;
            if(foeDeath>0)foe.y+=Mathf.Clamp01((now-foeDeath)*1.4f)*50;
            GUI.color=foeDeath>0?new Color(1,.65f,.5f,1-Mathf.Clamp01(now-foeDeath)*.9f):Color.Lerp(Color.white,new Color(1,.6f,.43f),foeHit*.5f);
            if(!HasEnemy3D)DrawEnemySigil(foe);GUI.color=old;
            DrawCombatVfx(new Vector2(foe.center.x,foe.center.y),230,enemyVfxIndex,enemyVfxTime);
            DrawCombatVfx(new Vector2(hero.center.x,hero.center.y),215,playerVfxIndex,playerVfxTime);
        }

        private void DrawCombatStats(float w,float h)
        {
            var hero=HeroPortraitRect;var foe=EnemyPortraitRect;var playerHealth=new Rect(hero.x,hero.yMax+8,hero.width,22);var enemyHealth=new Rect(foe.x,foe.yMax+8,foe.width,22);
            var playerChips=PlayerEffectChips();
            DrawActorHealthBar(playerHealth,combat.player,true);DrawEffectStrip(PlayerEffectArea(playerChips.Count),playerChips,true);
            if(GroupCombat){DrawGroupStats();if(run.hero==HeroId.Hexer){DrawSigilSlots(HexerSigilArea);DrawHexerResonance();}return;}
            DrawActorHealthBar(enemyHealth,combat.enemy,false);if(currentEnemy?.boss==true)DrawBossPhaseMarkers(enemyHealth);DrawEffectStrip(new Rect(foe.center.x-Mathf.Max(foe.width,282)*.5f,enemyHealth.yMax+6,Mathf.Max(foe.width,282),98),EnemyEffectChips(),false);
            if(run.hero==HeroId.Hexer){DrawSigilSlots(HexerSigilArea);DrawHexerResonance();}
            DrawEnemyIntentAndEffects();
        }

        private void DrawActorHealthBar(Rect r,FighterState fighter,bool playerSide)
        {
            var group=!playerSide&&GroupCombat&&groupRenderIndex>=0?opponentVisuals[groupRenderIndex]:null;
            var shownHp=playerSide?displayedPlayerHp:group?.hp??displayedEnemyHp;var shownBlock=playerSide?displayedPlayerBlock:group?.block??displayedEnemyBlock;
            RegisterCombatHudTarget(playerSide?"hero:health":"enemy:"+groupRenderIndex+":health",playerSide?1:2+Mathf.Max(0,groupRenderIndex),r,playerSide?"YOUR HEALTH":"ENEMY HEALTH",$"{shownHp}/{fighter.maxHp} HP. {shownBlock} Block. Black is missing health; blue indicates Block.");
            var guarded=shownBlock>0;var pulse=playerSide?heroBuff:foeBuff;var healthColor=guarded?new Color(.12f,.56f,.94f,.99f):new Color(.91f,.075f,.11f,.99f);var shownFill=playerSide?playerHealthFill:group?.fill??enemyHealthFill;
            var ratio=Mathf.Clamp01(shownFill/Mathf.Max(1,fighter.maxHp));var trail=playerSide?playerHealthTrail:group?.trail??enemyHealthTrail;
            // The orange damage slice exists only during the short impact animation;
            // the resting lost-health region remains pure black.
            FillBeveledHealthBar(r,ratio,shownFill<shownHp?ratio:Mathf.Clamp01(trail/Mathf.Max(1,fighter.maxHp)),healthColor,guarded?new Color(.58f,.88f,1f):new Color(.86f,.61f,.34f));
            var healGlow=playerSide?playerHealGlow:group?.healGlow??enemyHealGlow;
            if(healGlow>0&&!profile.reduceFlashing)DrawLine(new Vector2(r.x+6,r.y+2),new Vector2(r.x+Mathf.Max(6,(r.width-6)*ratio),r.y+2),new Color(.5f,1f,.67f,healGlow),2);
            if(playerSide)DrawFateHealthTreatment(r);
            GUI.Label(r,$"{shownHp}/{fighter.maxHp}",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=13,fontStyle=FontStyle.Bold,normal={textColor=Color.white}});
            if(guarded)
            {
                var shield=new Rect(r.x-(playerSide&&!string.IsNullOrEmpty(combat.activeShardId)?80:38),r.center.y-23,46,46);DrawSimpleShieldIcon(shield,shownBlock,pulse);
                if(CombatInspectionAllowed&&(r.Contains(combatPointer)||shield.Contains(combatPointer)))SetCombatEffectTooltip("BLOCK · "+fighter.block,$"The blue guard absorbs {fighter.block} damage before this health bar is harmed.",r.center);
            }
            else if(CombatInspectionAllowed&&r.Contains(combatPointer))SetCombatEffectTooltip(playerSide?"YOUR HEALTH":"ENEMY HEALTH",$"{fighter.hp} of {fighter.maxHp} health remains. The black portion is health already lost.",r.center);
        }

        private void DrawSimpleShieldIcon(Rect r,int amount,float pulse)
        {
            var c=r.center;var top=new Vector2(c.x,r.y+4);var left=new Vector2(r.x+7,r.y+11);var right=new Vector2(r.xMax-7,r.y+11);var lowLeft=new Vector2(r.x+12,r.yMax-12);var lowRight=new Vector2(r.xMax-12,r.yMax-12);var bottom=new Vector2(c.x,r.yMax-3);
            Fill(new Rect(r.x+10,r.y+10,r.width-20,r.height-20),new Color(.015f,.09f,.16f,.94f));var edge=new Color(.36f,.78f,1f,.96f);var glow=new Color(.56f,.9f,1f,.55f+pulse*.35f);
            DrawLine(top,left,edge,3);DrawLine(left,lowLeft,edge,3);DrawLine(lowLeft,bottom,edge,3);DrawLine(bottom,lowRight,edge,3);DrawLine(lowRight,right,edge,3);DrawLine(right,top,edge,3);
            GUI.Label(r,amount.ToString(),new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=17,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}});
        }

        private void FillBeveledHealthBar(Rect r,float healthRatio,float trailRatio,Color healthColor,Color edgeColor)
        {
            var rows=Mathf.Max(1,Mathf.CeilToInt(r.height));var bevel=Mathf.Clamp(Mathf.RoundToInt(r.height*.28f),3,6);var black=new Color(.004f,.005f,.007f,.99f);var orange=new Color(1f,.48f,.09f,.99f);
            for(var row=0;row<rows;row++)
            {
                var inset=row<bevel?bevel-row:row>=rows-bevel?row-(rows-bevel-1):0;var x=r.x+inset;var width=Mathf.Max(0,r.width-inset*2);var y=r.y+row;
                Fill(new Rect(x,y,width,1.15f),black);
                var trailWidth=width*Mathf.Clamp01(trailRatio);var healthWidth=width*Mathf.Clamp01(healthRatio);
                if(trailWidth>healthWidth)Fill(new Rect(x+healthWidth,y,trailWidth-healthWidth,1.15f),orange);
                if(healthWidth>0)Fill(new Rect(x,y,healthWidth,1.15f),healthColor);
                if(row==2&&healthWidth>0)Fill(new Rect(x,y,healthWidth,1),new Color(1f,1f,1f,.22f));
            }
            var p0=new Vector2(r.x+bevel,r.y);var p1=new Vector2(r.xMax-bevel,r.y);var p2=new Vector2(r.xMax,r.y+bevel);var p3=new Vector2(r.xMax,r.yMax-bevel);var p4=new Vector2(r.xMax-bevel,r.yMax);var p5=new Vector2(r.x+bevel,r.yMax);var p6=new Vector2(r.x,r.yMax-bevel);var p7=new Vector2(r.x,r.y+bevel);
            DrawLine(p0,p1,edgeColor,1.5f);DrawLine(p1,p2,edgeColor,1.5f);DrawLine(p2,p3,edgeColor,1.5f);DrawLine(p3,p4,edgeColor,1.5f);DrawLine(p4,p5,edgeColor,1.5f);DrawLine(p5,p6,edgeColor,1.5f);DrawLine(p6,p7,edgeColor,1.5f);DrawLine(p7,p0,edgeColor,1.5f);
        }

        private List<CombatEffectChip> PlayerEffectChips()
        {
            var chips=new List<CombatEffectChip>();var positive=new Color(.35f,.78f,1f);var power=new Color(1f,.72f,.25f);var harmful=new Color(.90f,.32f,.45f);
            AddHiddenPotentialChip(chips,power);
            AddPowerChip(chips,"gilded_warlord",combat.memory.gildedWarlord,power,combat.WarlordReady?"READY":"REST");
            AddPowerChip(chips,"unmovable",combat.memory.unmovable,power);
            AddPowerChip(chips,"relentless_conquest",combat.memory.conquestCadence,power,$"{combat.memory.conquestAttacksThisTurn%Mathf.Max(1,combat.memory.conquestCadence)}/{combat.memory.conquestCadence}");
            AddPowerChip(chips,"brand_of_ruin",combat.memory.brandOfRuin,power);
            AddPowerChip(chips,"beyond_the_veil_hexer",combat.memory.extraSigilSlots,power);
            AddFighterStatusChips(chips,PresentedPlayerStatuses(),true,harmful,positive);
            if(combat.memory.temporaryFortify>0)chips.Add(new("FOR","TEMPORARY FORTIFY",$"{combat.memory.temporaryFortify} of your Fortify expires at the end of this turn. Iron Will and other persistent Fortify remain.",combat.memory.temporaryFortify,positive));
            if(combat.memory.nextHeavyCostReduction>0)chips.Add(new("BRACE","BRACE",$"Your next Heavy Attack costs {combat.memory.nextHeavyCostReduction} less. Other cards do not consume this discount.",combat.memory.nextHeavyCostReduction,positive));
            if(VisibleRetaliation>0)chips.Add(new("RET","RETALIATE",$"The next enemy attack takes {VisibleRetaliation} damage back, then consumes all Retaliate. Persists between turns. A multi-hit attack triggers once; Block is not required.",VisibleRetaliation,positive));
            AddPowerChip(chips,"battle_temper",combat.memory.battleTemper,power);AddPowerChip(chips,"iron_blood",combat.memory.ironBlood,power);AddPowerChip(chips,"living_armor",combat.memory.livingArmor,power);AddPowerChip(chips,"war_machine",combat.memory.warMachine,power,$"{Mathf.Min(combat.memory.warMachineUses,combat.memory.warMachine)}/{combat.memory.warMachine}");
            if(combat.activeAspects.Any(c=>c.id=="unbreakable_spirit"))
            {
                var strengthReady=(presentedSpiritDirections&1)==0;var fortifyReady=(presentedSpiritDirections&2)==0;
                chips.Add(new("PWR","UNBREAKABLE SPIRIT","Once per turn when you gain Strength, gain that much Fortify. Once per turn when you gain Fortify, gain that much Strength.\n\nStrength → Fortify: "+(strengthReady?"READY":"USED")+"\nFortify → Strength: "+(fortifyReady?"READY":"USED")+"\nEach direction resets next turn; neither can repeat in the same turn.",1,power,true,$"{(strengthReady?1:0)+(fortifyReady?1:0)}/2"));
            }
            AddPowerChip(chips,"patient_warrior",combat.memory.patientWarrior,power);AddPowerChip(chips,"retribution",combat.memory.retributionPower,power);AddPowerChip(chips,"hold_the_line",combat.memory.holdLinePower,power);AddPowerChip(chips,"relentless",combat.memory.relentless,power,$"{combat.memory.attacksThisTurn%3}/3");AddPowerChip(chips,"onslaught",combat.memory.onslaughtThreshold,power,$"{(combat.memory.onslaughtUsed?combat.memory.onslaughtThreshold:Mathf.Min(combat.memory.attacksThisTurn,combat.memory.onslaughtThreshold))}/{combat.memory.onslaughtThreshold}");AddPowerChip(chips,"indomitable",combat.memory.indomitable,power);
            AddPowerChip(chips,"sigil_mastery",combat.memory.sigilMastery,power,$"{(combat.memory.sigilMasteryUsed?1:0)}/1");AddPowerChip(chips,"grand_convergence",combat.memory.grandConvergence,power);AddPowerChip(chips,"ashes",combat.memory.ashes,power);AddPowerChip(chips,"deaths_gaze",combat.memory.deathsGaze,power);AddPowerChip(chips,"embrace_the_void",combat.memory.embraceVoid,power);AddPowerChip(chips,"damnation",combat.memory.damnationStrength,power);
            AddPowerChip(chips,"battle_rhythm",combat.memory.battleRhythm,power,$"{combat.cardsPlayed%3}/3");AddPowerChip(chips,"reserve_energy",combat.memory.reserveEnergy,power);AddPowerChip(chips,"tactical_advantage",combat.memory.tacticalAdvantage,power);AddPowerChip(chips,"resourceful",combat.memory.resourceful,power);AddPowerChip(chips,"overflow",combat.memory.overflowPower,power);AddPowerChip(chips,"chain_reaction",combat.memory.chainReaction,power);AddPowerChip(chips,"against_all_odds",combat.memory.againstAllOddsDraw,power);AddPowerChip(chips,"perfect_form",combat.memory.perfectForm,power,$"{(combat.memory.firstAttackPlayed?1:0)+(combat.memory.firstSkillPlayed?1:0)}/2");
            AddPowerChip(chips,"deaths_embrace",combat.memory.deathsEmbrace,power,$"{Mathf.Min(1,combat.memory.soulsPlayedThisTurn)}/1");
            AddPowerChip(chips,"death_march",combat.memory.deathMarchCadence,power,$"{combat.memory.soulsPlayedCombat%Mathf.Max(1,combat.memory.deathMarchCadence)}/{combat.memory.deathMarchCadence}");
            AddPowerChip(chips,"gravekeeper",combat.memory.gravekeeper,power,$"{(combat.memory.gravekeeperUsed?1:0)}/1");
            AddPowerChip(chips,"endless_harvest",combat.memory.endlessHarvest,power,$"{Mathf.Min(combat.memory.soulsPlayedThisTurn,2)}/2");
            AddPowerChip(chips,"death_incarnate",combat.memory.deathIncarnate,power);AddPowerChip(chips,"grim_ascension",combat.memory.grimAscensionBonus,power);
            AddPowerChip(chips,"soulbound_tome",combat.memory.soulboundTome,power);AddPowerChip(chips,"eternal_souls",combat.memory.eternalSouls,power);AddPowerChip(chips,"reapers_calling",combat.memory.reapersCalling,power,$"{Mathf.Min(3,combat.memory.cardsDrawnThisTurn)}/3");
            return chips;
        }
        private void AddPowerChip(List<CombatEffectChip> chips,string id,int value,Color color,string counter=null)
        {
            if(value<=0)return;var card=combat.activeAspects.LastOrDefault(c=>c.id==id)??GameContent.Find(id);if(card==null)return;chips.Add(new("PWR",card.name.TrimEnd('+'),GameplayTerms.Display(card.text)+"\n\nSource: "+card.name+" · Aspect\nDuration: this combat."+(PowerIsDormant(card.name)?"\nDormant — its limited trigger has been used.":"")+(string.IsNullOrEmpty(counter)?"":"\nProgress: "+counter),value,color,true,counter));
        }
        private void DrawSigilSlots(Rect row)
        {
            if(sigilLayoutCombat!=combat){sigilLayoutCombat=combat;visibleSigilSlots=combat.SigilCapacity;}
            var expansionWaiting=expansionPulseStarts.TryGetValue("BEYOND THE VEIL",out var began)&&Time.unscaledTime<began+.20f;
            if(!expansionWaiting&&Event.current.type==EventType.Repaint)
                visibleSigilSlots=profile.reduceMotion?combat.SigilCapacity:Mathf.MoveTowards(visibleSigilSlots,combat.SigilCapacity,Time.unscaledDeltaTime*9);
            for(var i=0;i<combat.SigilCapacity;i++)
            {
                var reveal=Mathf.Clamp01(visibleSigilSlots-i);if(reveal<=0)continue;
                var filled=i<presentedSigils.Length;var kind=filled?presentedSigils[i]:SigilKind.Echo;
                var r=SigilSlotRect(i);var accent=filled?SigilAccent(kind):new Color(.47f,.44f,.51f);
                var pulse=0f;foreach(var beat in sigilPulseBeats)if(beat.slot==i&&Time.unscaledTime>=beat.time)
                    pulse=Mathf.Max(pulse,1-(Time.unscaledTime-beat.time)/.26f);
                var old=GUI.color;GUI.color=new Color(1,1,1,reveal);
                var c=r.center;var inset=filled?5:11;var lineAlpha=filled?.48f:.22f;
                DrawLine(new Vector2(r.x+inset,r.yMax-4),new Vector2(r.xMax-inset,r.yMax-4),new Color(accent.r,accent.g,accent.b,lineAlpha),1);
                if(filled)
                {
                    var scale=profile.reduceMotion?1:1+Mathf.Max(0,pulse)*.11f;
                    var size=r.width*scale;
                    DrawRemainingSigilIcon(kind,new Rect(c.x-size*.5f,c.y-size*.5f,size,size));
                    if(pulse>0&&!profile.reduceFlashing)
                        DrawLine(new Vector2(c.x,r.yMax-4),new Vector2(c.x,HeroPortraitRect.y-16),new Color(accent.r,accent.g,accent.b,pulse*.65f),2);
                }
                else
                {
                    var d=9f;
                    DrawLine(c+Vector2.up*d,c+Vector2.right*d,new Color(.55f,.50f,.61f,.35f),1);
                    DrawLine(c+Vector2.right*d,c+Vector2.down*d,new Color(.55f,.50f,.61f,.35f),1);
                    DrawLine(c+Vector2.down*d,c+Vector2.left*d,new Color(.55f,.50f,.61f,.35f),1);
                    DrawLine(c+Vector2.left*d,c+Vector2.up*d,new Color(.55f,.50f,.61f,.35f),1);
                }
                GUI.Label(new Rect(r.x-5,r.yMax+1,r.width+10,14),filled?kind.ToString().ToUpperInvariant():(i+1).ToString(),
                    new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=10,normal={textColor=accent}});
                GUI.color=old;
                RegisterCombatHudTarget("sigil:"+i,7,r,filled?kind.ToString().ToUpperInvariant()+" SIGIL":"EMPTY SIGIL SLOT "+(i+1),filled?SigilDescription(kind):"Create a Sigil here. Slots resolve from left to right.");
                if(CombatInspectionAllowed&&r.Contains(combatPointer))
                    SetCombatEffectTooltip(filled?kind.ToString().ToUpperInvariant()+" SIGIL":"EMPTY SIGIL SLOT "+(i+1),
                        filled?SigilDescription(kind):"Create a Sigil here. Slots resolve from left to right.",r.center);
            }
        }
        private List<CombatEffectChip> EnemyEffectChips(int owner=-1)
        {
            var chips=new List<CombatEffectChip>();AddFighterStatusChips(chips,PresentedEnemyStatuses(owner<0?combat.EnemyContextIndex:owner),false,new Color(.90f,.32f,.45f),new Color(.35f,.78f,1f));
            // Passive encounter prose remains available on enemy inspection, not in the active-effect strip.
            return chips;
        }
        private static void AddFighterStatusChips(List<CombatEffectChip> chips,FighterState fighter,bool playerSide,Color harmful,Color positive)
        {
            if(fighter.strength>0)chips.Add(new("STR","STRENGTH",$"Attacks deal {fighter.strength} additional damage.",fighter.strength,new Color(1f,.56f,.26f)));
            if(fighter.fortify>0)chips.Add(new("FOR","FORTIFY",$"Block cards gain {fighter.fortify} additional Block.",fighter.fortify,positive));
            if(fighter.burn>0)chips.Add(new("BRN","BURN",$"Takes {fighter.burn} damage after acting, then loses 1 stack.",fighter.burn,new Color(1f,.38f,.16f)));
            if(fighter.marked>0)chips.Add(new("MRK","MARKED","Hexer cards can consume or amplify these stacks.",fighter.marked,new Color(.72f,.35f,1f)));
            if(fighter.weak>0)chips.Add(new("WEK","WEAK","Deals 25% less attack damage.",fighter.weak,harmful));
            if(fighter.vulnerable>0)chips.Add(new("VUL","VULNERABLE","Takes 50% more attack damage.",fighter.vulnerable,harmful));
            if(fighter.frail>0)chips.Add(new("FRL","FRAIL","Gains less Block from cards.",fighter.frail,harmful));
            foreach(var effect in fighter.effects??new())if(effect.value>0)chips.Add(new("EFT",effect.id.Replace('_',' ').ToUpperInvariant(),MajorEffectDetail(effect),effect.value,playerSide?positive:harmful));
        }
        private void DrawEnemyIntentAndEffects()
        {
            DrawEnemyIntentGroup(0);
            var foe=EnemyPortraitRect;
            if(CombatInspectionAllowed&&foe.Contains(combatPointer))
                SetCombatEffectTooltip(currentEnemy?.name??"ENEMY",CompleteIntentDetail(0)+"\n\n"+combat.mechanicTitle+"\n"+combat.mechanicText,foe.center);
        }
        private void DrawEffectStrip(Rect r,List<CombatEffectChip> chips,bool playerSide)
        {
            if(chips.Count==0)return;
            var size=profile.largeEffectIcons?52f:44f;const float gap=5;var columns=EffectStripLayout.Columns(r.width,size,gap);var rows=EffectStripLayout.Rows(r.height,size,gap);var capacity=columns*rows;var shown=Mathf.Min(chips.Count,capacity);
            for(var i=0;i<shown;i++)
            {
                var hidden=i==shown-1&&chips.Count>shown;var chip=chips[i];var col=i%columns;var row=i/columns;var rect=new Rect(EffectStripLayout.CellX(r.center.x,i,shown,columns,size,gap),r.y+row*(size+gap),size,size);
                RegisterCombatHudTarget((playerSide?"hero:":"enemy:"+groupRenderIndex+":")+chip.title,playerSide?1:2+Mathf.Max(0,groupRenderIndex),rect,hidden?"MORE ACTIVE EFFECTS":chip.title,hidden?string.Join("\n\n",chips.Skip(i).Select(c=>c.title+" — "+c.detail)):chip.detail);
                var key=(playerSide?"P:":"E:"+groupRenderIndex+":")+chip.title;var displayState=chip.value*397+(string.IsNullOrEmpty(chip.counter)?0:chip.counter.GetHashCode());if(chip.title!="UNBREAKABLE SPIRIT"&&effectValueCache.TryGetValue(key,out var oldValue)&&oldValue!=displayState)effectPulseUntil[key]=Time.unscaledTime+.42f;effectValueCache[key]=displayState;
                var expansion=playerSide&&chip.power&&ExpansionPowerIndex(chip.title)>=0;
                var pulse=Mathf.Max(ScheduledPowerPulse(key),effectPulseUntil.TryGetValue(key,out var until)?Mathf.Clamp01((until-Time.unscaledTime)*3):0);
                // Readiness is expressed by the active icon, not an endless idle pulse.
                var scale=profile.reduceMotion?1:1+pulse*.16f;var iconRect=new Rect(rect.center.x-size*.44f*scale,rect.center.y-size*.44f*scale,size*.88f*scale,size*.88f*scale);
                var iconTint=GUI.color;var arrival=playerSide?PowerArrivalOpacity(chip.title):1;var dormant=playerSide&&chip.power&&PowerIsDormant(chip.title)&&pulse<=.01f;
                GUI.color=iconTint*(dormant?new Color(.53f,.57f,.62f,arrival*.86f):new Color(1,1,1,arrival));
                if(pulse>0&&!expansion&&!profile.reduceFlashing)Fill(new Rect(rect.x+3,rect.y+3,rect.width-6,rect.height-6),new Color(chip.color.r,chip.color.g,chip.color.b,pulse*.12f));
                if(hidden)GUI.Label(rect,"+"+(chips.Count-shown+1),new GUIStyle(titleStyle){fontSize=21,normal={textColor=Gold}});else if(!DrawMajorEffectIcon(iconRect,chip.title)&&(!expansion||!DrawExpansionPowerIcon(iconRect,chip.title,chip.title=="GILDED WARLORD"&&!combat.WarlordReady&&pulse<=.01f)))DrawAtlasIcon(combatReadabilityAtlas,CombatReadabilityIcon(chip),8,8,iconRect);
                if(!hidden&&chip.value>0){var label=string.IsNullOrEmpty(chip.counter)?chip.value.ToString():chip.counter;var width=string.IsNullOrEmpty(chip.counter)?21f:36f;var number=new Rect(rect.xMax-width+3,rect.yMax-19,width,21);Fill(number,new Color(.005f,.008f,.012f,.96f));Outline(number,chip.color,1);GUI.Label(number,label,new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=string.IsNullOrEmpty(chip.counter)?12:9,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}});}
                if(profile.colorblindStatus&&!hidden)GUI.Label(new Rect(rect.x-3,rect.yMax-10,rect.width+6,14),chip.code,new GUIStyle(footerStyle){fontSize=8,alignment=TextAnchor.MiddleCenter,normal={textColor=chip.color}});
                GUI.color=iconTint;
                if(CombatInspectionAllowed&&rect.Contains(combatPointer))SetCombatEffectTooltip(hidden?"MORE ACTIVE EFFECTS":chip.title,hidden?string.Join("\n\n",chips.Skip(i).Select(c=>c.title+" — "+c.detail)):chip.detail,rect.center);
            }
        }
        private static int CombatReadabilityIcon(CombatEffectChip chip)
        {
            return chip.title switch
            {
                "BLOCK"=>1,"STRENGTH"=>2,"TEMPORARY FORTIFY"=>3,"BRACE"=>42,"FORTIFY"=>3,"RETALIATE"=>4,"BURN"=>5,"MARKED"=>6,"WEAK"=>7,"VULNERABLE"=>8,"FRAIL"=>9,
                "BATTLE TEMPER"=>16,"IRON BLOOD"=>17,"LIVING ARMOR"=>18,"WAR MACHINE"=>19,"UNBREAKABLE SPIRIT"=>20,"PATIENT WARRIOR"=>21,"RETRIBUTION"=>22,"HOLD THE LINE"=>23,
                "RELENTLESS"=>24,"ONSLAUGHT"=>25,"INDOMITABLE"=>26,"SIGIL MASTERY"=>27,"GRAND CONVERGENCE"=>28,"ASHES"=>29,"DEATH'S GAZE"=>30,"EMBRACE THE VOID"=>31,
                "DAMNATION"=>32,"BATTLE RHYTHM"=>33,"RESERVE ENERGY"=>34,"TACTICAL ADVANTAGE"=>35,"RESOURCEFUL"=>36,"OVERFLOW"=>37,"CHAIN REACTION"=>38,"AGAINST ALL ODDS"=>39,"PERFECT FORM"=>40,
                _=>chip.code=="SHD"?10:61
            };
        }
        private static int PowerIconForCard(CardDef card)=>CombatReadabilityIcon(new CombatEffectChip("PWR",card?.name??"POWER","",1,Color.white,true));
        private void SetCombatEffectTooltip(string title,string detail,Vector2 anchor){combatEffectTooltipTitle=title;combatEffectTooltipDetail=detail;combatEffectTooltipAnchor=anchor;}
        private void DrawCombatEffectTooltip(float w,float h)
        {
            if(string.IsNullOrEmpty(combatEffectTooltipTitle))return;
            const float width=340;var x=combatEffectTooltipAnchor.x+28;if(x+width>w-18)x=combatEffectTooltipAnchor.x-width-28;if(x<18)x=18;var y=Mathf.Clamp(combatEffectTooltipAnchor.y-54,118,h-210);
            DrawTooltip(new Rect(x,y,width,116),combatEffectTooltipTitle,combatEffectTooltipDetail);
        }
        private void DrawCombatRelics()
        {
            var shown=Mathf.Min(6,run.relics.Count);for(var i=0;i<shown;i++)
            {
                var id=run.relics[i];var index=System.Array.FindIndex(GameContent.Relics,relic=>relic.id==id);var r=new Rect(520+i*48,9,42,42);
                DrawRelicArt(r,index>=0?index:0);if(powerBadgePulse>0)Outline(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),new Color(1f,.78f,.3f,powerBadgePulse),2);
                if(r.Contains(combatPointer)){var relic=index>=0?GameContent.Relics[index]:null;DrawTooltip(new Rect(Mathf.Clamp(r.x-80,18,CombatWidth-318),72,300,70),relic?.name??"UNKNOWN RELIC",relic?.text??"This legacy relic is no longer part of the replacement catalog.");}
            }
            if(run.relics.Count>shown)GUI.Label(new Rect(520+shown*48,11,40,38),"+"+(run.relics.Count-shown),new GUIStyle(footerStyle){fontSize=12,normal={textColor=Gold}});
        }
        private void DrawCombatAtmosphere(float w,float h)
        {
            foreach(var p in motes)
            {
                var alpha=.11f+Mathf.Sin(shimmer+p.phase)*.07f;Fill(new Rect(p.position.x*w,p.position.y*h,p.size,p.size),new Color(1,.75f,.36f,alpha));
            }
        }
        private void DrawCombatNumbers()
        {
            if(!profile.damageNumbers)return;
            foreach(var number in combatNumbers)
            {
                var t=Time.unscaledTime-number.start;if(t<0)continue;var alpha=Mathf.Clamp01((1.1f-t)*2.5f);var p=number.origin-new Vector2(0,profile.reduceMotion?0:t*44);
                var baseSize=number.text.Length>9?23:31;var style=new GUIStyle(titleStyle){font=labelFont,fontSize=profile.largeDamageNumbers?baseSize+8:baseSize,normal={textColor=new Color(number.color.r,number.color.g,number.color.b,alpha)}};
                ShadowLabel(new Rect(p.x-170,p.y-30,340,54),number.text,style);
            }
        }

        private void QueueShard(FateShardDef shard,FateShardState saved,int index)
        {
            if(!CanAcceptCombatInput||index<0||index>=run.shards.Count||saved==null||!saved.CanActivate||!string.IsNullOrEmpty(combat.activeShardId) )return;
            combatBusy=true;hoverView=dragView=selectedView=null;
            combatSequence=StartCoroutine(AnimateShard(shard,saved,index));
        }
        private IEnumerator AnimateShard(FateShardDef shard,FateShardState saved,int inventoryIndex)
        {
            playerAction=1;playerActionKind=EffectKind.Power;Sfx(SoundCue.Resonance);yield return new WaitForSecondsRealtime(AnimationSeconds(.18f));
            while(combatPauseOpen)yield return null;
            if(inventoryIndex>=run.shards.Count||run.shards[inventoryIndex]!=saved){combatBusy=false;combatSequence=null;yield break;}
            var fractured=saved.Fractured;if(!combat.ActivateShard(shard,fractured)){combatBusy=false;combatSequence=null;yield break;}saved.active=true;saved.activeFractured=fractured;saved.uses++;
            SaveCombatCheckpoint();
            var settle=ConsumeCombatEvents(combat.TakeEvents());
            playerVfxIndex=VfxFor(EffectKind.Power);playerVfxTime=.48f;
            UpdateBossPhaseVisual();yield return new WaitForSecondsRealtime(Mathf.Max(settle,.25f));if(combat.pendingPlay!=null)yield return ResolvePendingCardChoices(combat.pendingPlay.card);yield return FinishCombatIfNeeded();combatBusy=false;combatSequence=null;
        }
    }
}
