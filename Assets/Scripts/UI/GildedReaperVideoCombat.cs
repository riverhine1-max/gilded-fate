using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;
using GildedFate.Combat;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private sealed class ReaperVideoBeat { public string clip; public float at,seconds; public int priority; }
        private readonly List<ReaperVideoBeat> reaperVideoBeats=new();
        private ReaperVideoSurface reaperVideos;
        private CombatState reaperVideoCombat;
        private float reaperBusyUntil,reaperLastReaction=-10;
        private int reaperPriority,reaperAttackVariant;
        private bool reaperDefeated,reaperVideoVerification,reaperVideoFrozen;
        private bool ReaperVideoEnabled=>run!=null&&run.hero==HeroId.Reaper&&profile!=null&&!profile.reduceMotion&&ReaperVideoCatalog.Available;

        private void UpdateReaperVideos()
        {
            if(screen!=ScreenMode.Combat||combat==null||!ReaperVideoEnabled){DisposeReaperVideos();return;}
            if(reaperVideoCombat!=combat){DisposeReaperVideos();reaperVideoCombat=combat;reaperDefeated=false;}
            reaperVideos??=new ReaperVideoSurface();
            var paused=IsRunInspectionPaused||reaperVideoFrozen;
            reaperVideos.Tick(paused,displayedPlayerHp*4<=combat.player.maxHp);
            if(reaperVideoVerification)return;
            if(paused){reaperBusyUntil+=Time.unscaledDeltaTime;foreach(var b in reaperVideoBeats)b.at+=Time.unscaledDeltaTime;return;}
            var now=Time.unscaledTime;
            reaperVideoBeats.RemoveAll(b=>now-b.at>1.5f);
            var next=reaperVideoBeats.Where(b=>b.at<=now).OrderByDescending(b=>b.priority).FirstOrDefault();
            if(next!=null&&(!reaperVideos.ActionPlaying||now>=reaperBusyUntil||next.priority>reaperPriority))
            {
                reaperVideoBeats.Remove(next);
                reaperVideoBeats.RemoveAll(b=>b.at<=now&&b.priority<=next.priority);
                if(!reaperDefeated)StartReaperVideo(next.clip,next.seconds,next.priority);
            }
        }
        private void StartReaperVideo(string clip,float seconds,int priority)
        {
            if(!ReaperVideoEnabled)return;
            reaperVideos??=new ReaperVideoSurface();reaperVideos.Play(clip,seconds);
            reaperPriority=priority;reaperBusyUntil=Time.unscaledTime+seconds+.12f;
            if(clip=="Defeat"){reaperDefeated=true;reaperVideoBeats.Clear();}
        }
        private void QueueReaperVideo(string clip,float seconds,int priority,float delay=0)
        {
            if(!ReaperVideoEnabled||reaperDefeated||ReaperVideoCatalog.Find(clip)==null)return;
            if((reaperVideos?.ActionPlaying??false)&&reaperVideos.ActionName==clip)return;
            if(reaperVideoBeats.Any(b=>b.clip==clip))return;
            if(reaperVideoBeats.Count>=8)reaperVideoBeats.RemoveAt(0);
            reaperVideoBeats.Add(new ReaperVideoBeat{clip=clip,seconds=seconds,priority=priority,at=Time.unscaledTime+delay});
        }
        private string ReaperCardAnimation(CardDef card)
        {
            if(card.kind==CardKind.Power)return "AspectActivation";
            if(card.id=="soul"||card.id=="devour_dead"||card.id=="soul_feast")return "SoulConsumption";
            if(card.id=="mark_of_the_grave"||card.id=="grave_execution")return "GraveMarkInteraction";
            if(card.id=="soul_call"||card.id=="army_of_the_dead"||card.id=="soul_conversion")return "SoulGeneration";
            if(card.kind==CardKind.Attack)
            {
                if(card.rarity==Rarity.Rare||card.value>=24)return "MajorFinisherAttack";
                if(card.value>=12)return "HeavyAttack";
                return reaperAttackVariant%2==0?"BasicScytheAttack":"AlternateScytheAttack";
            }
            if(card.effect==EffectKind.Block)return "DefensiveAnimation";
            if(card.effect==EffectKind.Exhaust)return "Dissipate";
            return "Buff";
        }
        private void PlayReaperCard(CardDef card,float travel)
        {
            if(!ReaperVideoEnabled||reaperDefeated)return;
            var clip=ReaperCardAnimation(card);
            StartReaperVideo(clip,AnimationSeconds(clip=="MajorFinisherAttack"||clip=="HeavyAttack"?1.05f:.8f),50);
            if(card.kind==CardKind.Attack)reaperAttackVariant++;
        }
        private void PlayReaperVital(CombatEvent fact)
        {
            if(!ReaperVideoEnabled||reaperDefeated)return;
            if(!fact.playerSide)return;
            if(fact.kind==CombatEventKind.Damage&&fact.amount>0&&(fact.playerHp<=0||Time.unscaledTime-reaperLastReaction>.18f))
            {
                StartReaperVideo(fact.playerHp<=0?"Defeat":fact.amount>=15?"HeavyHit":"LightHit",AnimationSeconds(fact.playerHp<=0?1.1f:.65f),100);
                reaperLastReaction=Time.unscaledTime;
            }
            else if(fact.kind==CombatEventKind.Block&&fact.label=="BLOCKED"&&fact.amount>0&&Time.unscaledTime-reaperLastReaction>.18f)
            {
                StartReaperVideo("DefensiveAnimation",AnimationSeconds(.6f),90);reaperLastReaction=Time.unscaledTime;
            }
            else if(fact.kind==CombatEventKind.Block&&fact.label!="BLOCKED"&&fact.amount>0&&!(reaperVideos?.ActionPlaying??false))
                QueueReaperVideo("DefensiveAnimation",AnimationSeconds(.65f),30);
        }
        private void PlayReaperStatus(CombatEvent fact)
        {
            if(!ReaperVideoEnabled||fact.amount<=0)return;
            if(!fact.playerSide)
            {
                if(fact.label=="GRAVEMARK"||fact.label=="MARK OF THE GRAVE")QueueReaperVideo("GraveMarkInteraction",AnimationSeconds(.65f),35);
                return;
            }
            if(fact.label.StartsWith("TRIGGER:",StringComparison.Ordinal)||fact.label=="ASPECT ACTIVE")return;
            if(fact.label=="SOUL"||fact.label=="SOUL REPLAY")return; // Already represented by the played Soul; no duplicate action.
            if(fact.label is "STRENGTH" or "FORTIFY" or "SOUL DAMAGE" or "SOULBOUND" or "AFTERLIFE" or "FEROCITY" or "PREPARATION" or "PHANTOM EDGE" or "RESOLVE" or "REVERBERATION" or "ADAPTATION" or "CHIMERA" or "FORESIGHT")
                QueueReaperVideo("Buff",AnimationSeconds(.6f),15);
        }
        private void ScheduleReaperVideoReceipts(CombatEvent[] facts,float[] offsets,float now)
        {
            if(!ReaperVideoEnabled)return;
            for(var i=0;i<facts.Length;i++)
            {
                var f=facts[i];var delay=Mathf.Max(0,now+offsets[i]-Time.unscaledTime);
                if(f.generatedCard&&f.card?.id=="soul")QueueReaperVideo("SoulGeneration",AnimationSeconds(.65f),30,delay);
                else if(f.kind==CombatEventKind.Exhaust)
                    QueueReaperVideo(f.card?.id=="soul"?"SoulConsumption":"Dissipate",AnimationSeconds(.6f),20,delay+.1f);
            }
        }
        private void ResetReaperVideos(){DisposeReaperVideos();reaperAttackVariant=0;reaperDefeated=false;}
        private bool DrawReaperVideo(Rect hero)=>ReaperVideoEnabled&&reaperVideos!=null&&reaperVideos.Draw(hero);
        private void DisposeReaperVideos()
        {
            reaperVideos?.Dispose();reaperVideos=null;reaperVideoCombat=null;reaperVideoBeats.Clear();
            reaperPriority=0;reaperBusyUntil=0;reaperLastReaction=-10;
        }
    }
}
