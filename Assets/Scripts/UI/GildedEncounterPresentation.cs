using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private sealed class OpponentVisual
        {public int hp,block,tracked,uid;public float trail,hold,hit,action,fill,healGlow,death=-1;}
        private readonly List<OpponentVisual> opponentVisuals=new();
        private readonly List<(int index,float time)> opponentActions=new();
        private int combatTargetIndex,groupRenderIndex=-1;
        private bool GroupCombat=>combat!=null&&combat.EnemyCount>1;
        // Bigger creatures get a wider lane, Minions a narrower one, so a boss is never squeezed to its
        // Minions' size and every hit region follows the body that is actually drawn.
        private float LaneWeight(int index)
        {
            var def=WorldContent.Enemies.FirstOrDefault(e=>e.id==combat.EnemyIdAt(index));
            return Mathf.Clamp(EnemyBodyScale(def),.75f,1.5f);
        }
        private Rect GroupCell(int index)
        {
            var count=combat.EnemyCount;var full=CombatWidth*.55f;
            var order=combat.BattleOrder();var weights=new float[count];var total=0f;
            for(var slot=0;slot<count;slot++){weights[slot]=LaneWeight(order[slot]);total+=weights[slot];}
            var mean=total/count;
            var unit=count>=4?full/count:Mathf.Min(count==2?244:232,full/count);
            var start=CombatWidth*.695f-unit*count*.5f;var mySlot=combat.BattleSlotOf(index);var x=start;
            for(var slot=0;slot<mySlot&&slot<count;slot++)x+=unit*weights[slot]/mean;
            return new Rect(x,155,unit*weights[Mathf.Clamp(mySlot,0,count-1)]/mean,373);
        }
        private Rect GroupPortrait(int index)
        {
            var cell=GroupCell(index);var def=WorldContent.Enemies.First(e=>e.id==combat.EnemyIdAt(index));
            var weight=EnemyBodyScale(def);
            var width=Mathf.Min(236*weight,cell.width-28);var height=Mathf.Min(226*weight,286);
            var texture=LoadAuthoredArt(GildedArtCatalog.EnemyResource(EnemyArtId(def.id)));
            if(texture){var fit=Mathf.Min(width/texture.width,height/texture.height);width=texture.width*fit;height=texture.height*fit;}
            // Reserve two status rows between group portraits and the resting hand.
            // The same portrait rect drives the health bar, hit region and effect anchor.
            var baseline=Mathf.Min(438,CombatHeight-414);
            return new Rect(cell.center.x-width*.5f,baseline-height,width,height);
        }
        private Rect GroupDropZone(int index)
        {
            var cell=GroupCell(index);var target=ComfortableEnemyTarget(GroupPortrait(index));
            target.xMin=Mathf.Max(target.xMin,cell.x+3);target.xMax=Mathf.Min(target.xMax,cell.xMax-3);return target;
        }
        private static Rect ComfortableEnemyTarget(Rect body)=>new(body.x-12,body.y-10,body.width+24,body.height+20);
        private static float EnemyBodyScale(EnemyDef enemy)=>enemy?.id switch
        {
            "vault_rat"=>.56f,"vault_spider"=>.62f,"golden_wisp"=>.55f,
            "ash_hound"=>.78f,"coin_mimic"=>.82f,
            "gilded_sentry" or "masked_acolyte" or "broken_knight" or "rune_mage"=>1f,
            "chained_brute"=>1.20f,"executioner"=>1.23f,"mirror_witch"=>1.12f,
            "golden_beast"=>1.30f,"collector"=>1.20f,
            "hollow_king"=>1.43f,"vault_mother"=>1.55f,"last_dealer"=>1.34f,
            _=>WildBodyScale(enemy)
        };
        private int TargetAt(Vector2 point)
        {
            if(!GroupCombat)return combat!=null&&combat.IsLivingTarget(0)&&EnemyDropZone.Contains(point)?0:-1;
            for(var i=0;i<combat.EnemyCount;i++)if(combat.IsLivingTarget(i)&&GroupDropZone(i).Contains(point))return i;
            return -1;
        }
        private void FocusLivingTarget(int direction)
        {
            // Left/right follows the battlefield (front of the formation first), not the internal index.
            var order=combat.BattleOrder();var slot=Array.IndexOf(order,combatTargetIndex);if(slot<0)slot=0;
            for(var n=0;n<order.Length;n++)
            {slot=(slot+direction+order.Length)%order.Length;combatTargetIndex=order[slot];if(combat.IsLivingTarget(combatTargetIndex))break;}
            combatPointer=GroupCombat?GroupDropZone(combatTargetIndex).center:EnemyDropZone.center;cardPreviewCache.Clear();
        }
        private void WithEnemyPresentation(int index,Action draw)
        {
            var old=currentEnemy;var oldIndex=groupRenderIndex;groupRenderIndex=index;
            try{combat.InspectEnemy(index,()=>{currentEnemy=WorldContent.Enemies.First(e=>e.id==combat.EnemyIdAt(index));draw();return 0;});}
            finally{currentEnemy=old;groupRenderIndex=oldIndex;}
        }
        private void ResetOpponentVisuals()
        {
            opponentVisuals.Clear();opponentActions.Clear();combatTargetIndex=0;groupRenderIndex=-1;
            if(combat==null)return;combatTargetIndex=combat.EnemyContextIndex;
            if(combat.wildCombat)foreach(var front in combat.BattleOrder())if(combat.IsLivingTarget(front)){combatTargetIndex=front;break;}
            for(var i=0;i<combat.EnemyCount;i++)opponentVisuals.Add(NewOpponentVisual(i,-100));
        }
        // Bodies that were already dead when the roster changed stay hidden instead of replaying their death.
        private OpponentVisual NewOpponentVisual(int i,float deadSince)
        {var f=combat.EnemyAt(i);return new OpponentVisual{hp=f.hp,block=f.block,tracked=f.hp,trail=f.hp,fill=f.hp,uid=combat.MindAt(i)?.uid??0,death=f.hp<=0?Time.unscaledTime+deadSince:-1};}
        private int seenRosterVersion=-1;
        private void SyncOpponentVisuals()
        {
            if(combat==null)return;
            if(opponentVisuals.Count!=combat.EnemyCount||seenRosterVersion!=combat.rosterVersion)
            {
                // Summons add a body or reuse a long-dead Minion's place; keep everyone else's visuals.
                seenRosterVersion=combat.rosterVersion;var target=combatTargetIndex;
                if(opponentVisuals.Count>combat.EnemyCount)ResetOpponentVisuals();
                for(var i=0;i<combat.EnemyCount;i++)
                {
                    var uid=combat.MindAt(i)?.uid??0;
                    if(i>=opponentVisuals.Count){opponentVisuals.Add(NewOpponentVisual(i,-100));OnEnemySlotChanged(i);}
                    else if(uid!=0&&opponentVisuals[i].uid!=uid){opponentVisuals[i]=NewOpponentVisual(i,-100);OnEnemySlotChanged(i);}
                }
                combatTargetIndex=Mathf.Clamp(target,0,combat.EnemyCount-1);
                if(!combat.IsLivingTarget(combatTargetIndex))FocusLivingTarget(1);
                InvalidateEnemyIntents();
            }
            for(var i=0;i<combat.EnemyCount;i++){opponentVisuals[i].hp=combat.EnemyAt(i).hp;opponentVisuals[i].block=combat.EnemyAt(i).block;}
        }
        private void UpdateOpponentVisuals(float dt)
        {
            if(!GroupCombat)return;
            for(var i=0;i<opponentVisuals.Count;i++)
            {
                var v=opponentVisuals[i];UpdateHealthTrail(v.hp,combat.EnemyAt(i).maxHp,ref v.tracked,ref v.trail,ref v.hold,dt);
                v.hit=Mathf.MoveTowards(v.hit,0,dt*3.5f);v.action=Mathf.MoveTowards(v.action,0,dt*2.4f);
                if(v.hp<=0&&v.death<0)v.death=Time.unscaledTime;
            }
            foreach(var action in opponentActions.Where(a=>a.time<=Time.unscaledTime).ToArray())
            {if(action.index<opponentVisuals.Count)opponentVisuals[action.index].action=1;opponentActions.Remove(action);}
        }
        private void DrawGroupActors()
        {
            for(var i=0;i<combat.EnemyCount;i++)
            {
                if(i>=opponentVisuals.Count)continue;var v=opponentVisuals[i];var r=GroupPresentedPortrait(i);
                var def=FinalEnemyDef(i);if(def==null||def.id!=combat.EnemyIdAt(i))def=WorldContent.Enemies.First(e=>e.id==combat.EnemyIdAt(i));
                // The sprite animates procedurally around its stable feet anchor
                // (GildedEnemyAnimation.cs); health and effects keep the presented rect.
                if(v.death>=0&&Time.unscaledTime-v.death>EnemyDeathDuration(EnemyAnimFor(i,def),true))continue;
                var old=GUI.color;
                DrawAnimatedEnemy(i,profile.reduceMotion?r:GroupPortrait(i),def,Color.Lerp(Color.white,new Color(1,.55f,.36f),v.hit*.6f),v.death);GUI.color=old;
                var hitAtlas=i<enemyAnims.Length?enemyAnims[i].hitAtlas:7;
                if(v.hit>0&&hitAtlas>=0)DrawCombatVfx(r.center,Mathf.Min(160,r.width),hitAtlas,v.hit*.3f);
            }
        }
        private Rect GroupEffectArea(int owner)
        {
            var portrait=GroupPresentedPortrait(owner);var cell=GroupCell(owner);
            return new Rect(portrait.center.x-(cell.width-24)*.5f,portrait.yMax+35,cell.width-24,98);
        }
        private void DrawGroupStats()
        {
            for(var i=0;i<combat.EnemyCount;i++)
            {
                if(i>=opponentVisuals.Count||opponentVisuals[i].hp<=0)continue;
                var index=i;WithEnemyPresentation(index,()=>
                {
                    var portrait=GroupPresentedPortrait(index);var health=new Rect(portrait.x,portrait.yMax+8,portrait.width,22);
                    DrawActorHealthBar(health,combat.enemy,false);
                    var cell=GroupCell(index);DrawEffectStrip(GroupEffectArea(index),EnemyEffectChips(index),false);
                    DrawEnemyIntentGroup(index);DrawMinionBadge(index,portrait);DrawWildCounter(index,portrait);DrawWildForecast(index,portrait);
                    if(CombatInspectionAllowed&&portrait.Contains(combatPointer))
                        SetCombatEffectTooltip(currentEnemy.name,ActorEffectSummary(EnemyEffectChips(index))+WildTooltipSuffix(index),portrait.center);
                });
            }
            DrawSeizedGoldHud();
        }
        private void RecordShardShatter()
        {
            for(var i=0;i<run.shards.Count;i++)
            {var state=run.shards[i];if(state.active&&state.uses>=3){shardFrameShatterAt=Time.unscaledTime;shardFlights.Add(new ShardFlight{from=ShardHealthOrigin,start=Time.unscaledTime,shatter=true,shard=WorldContent.FateShards.FirstOrDefault(s=>s.id==state.id)});}}
        }
    }
}
