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
        private sealed class HexerVideoBeat { public string clip; public float at,seconds; public int priority; }
        private readonly List<HexerVideoBeat> hexerVideoBeats=new();
        private HexerVideoSurface hexerVideos;
        private CombatState hexerVideoCombat;
        private float hexerVideoBusyUntil,hexerLastReaction=-10;
        private int hexerVideoPriority,hexerLastHp,hexerAttackVariant,hexerKnownSigils;
        private bool hexerVideoFrozen,hexerDefeated;
        private bool HexerVideoEnabled=>run!=null&&run.hero==HeroId.Hexer&&profile!=null&&!profile.reduceMotion&&HexerVideoCatalog.Available;

        private void UpdateHexerVideos()
        {
            var active=screen==ScreenMode.Combat&&combat!=null&&HexerVideoEnabled;
            if(!active){DisposeHexerVideos();return;}
            if(hexerVideoCombat!=combat){DisposeHexerVideos();hexerVideoCombat=combat;hexerLastHp=combat.player.hp;hexerKnownSigils=combat.sigils.Count;}
            hexerVideos??=new HexerVideoSurface();
            var paused=hexerVideoFrozen||IsRunInspectionPaused;
            hexerVideos.Tick(paused,displayedPlayerHp*4<=combat.player.maxHp);
            if(hexerVideoVerification)return;
            if(paused)
            {
                var dt=Time.unscaledDeltaTime;hexerVideoBusyUntil+=dt;
                foreach(var beat in hexerVideoBeats)beat.at+=dt;
                return;
            }
            var now=Time.unscaledTime;
            hexerVideoBeats.RemoveAll(b=>now-b.at>1.4f);
            var next=hexerVideoBeats.Where(b=>b.at<=now).OrderByDescending(b=>b.priority).FirstOrDefault();
            if(next!=null&&(!hexerVideos.ActionPlaying||now>=hexerVideoBusyUntil||next.priority>hexerVideoPriority))
            {
                hexerVideoBeats.Remove(next);
                // Same-time minor receipts are coalesced, never a backlog of stale animations.
                hexerVideoBeats.RemoveAll(b=>b.at<=now&&b.priority<=next.priority);
                if(!hexerDefeated||next.clip=="ReviveRecovery")StartHexerVideo(next.clip,next.seconds,next.priority);
            }
        }
        private void StartHexerVideo(string clip,float seconds,int priority)
        {
            if(!HexerVideoEnabled)return;
            hexerVideos??=new HexerVideoSurface();
            hexerVideos.Play(clip,seconds);
            hexerVideoPriority=priority;hexerVideoBusyUntil=Time.unscaledTime+seconds+.12f;
            if(clip=="Defeat"){hexerDefeated=true;hexerVideoBeats.Clear();}
            if(clip=="ReviveRecovery")hexerDefeated=false;
        }
        private void QueueHexerVideo(string clip,float at,float seconds,int priority)
        {
            if(!HexerVideoEnabled||hexerDefeated||HexerVideoCatalog.Find(clip)==null)return;
            if(hexerVideoBeats.Any(b=>b.clip==clip&&Mathf.Abs(b.at-at)<.4f))return;
            if(hexerVideoBeats.Count>=8)hexerVideoBeats.RemoveAt(0);
            hexerVideoBeats.Add(new HexerVideoBeat{clip=clip,at=at,seconds=seconds,priority=priority});
        }
        private void ResetHexerVideos()
        {
            DisposeHexerVideos();hexerLastHp=combat?.player.hp??0;
            hexerKnownSigils=combat?.sigils.Count??0;hexerAttackVariant=0;hexerDefeated=false;
        }
        private string HexerCardAnimation(CardDef card)
        {
            if(card.id=="first_ritual"||card.id=="blasphemous_ritual"||card.id=="sigil_of_malice")return "FirstRitual";
            if(card.kind==CardKind.Power)return "AspectActivation";
            if(card.kind==CardKind.Attack)
                return card.rarity==Rarity.Rare?"FinisherAttack":card.value>=11?"PowerfulSigilAttack":hexerAttackVariant%2==0?"BasicMagicalAttack":"AlternateMagicalAttack";
            if(card.effect==EffectKind.Block)return card.value>=12?"MajorDefensiveSkill":"BasicDefensiveSkill";
            if(card.effect==EffectKind.Draw)return "DrawKnowledgeResponse";
            if(card.effect==EffectKind.Exhaust)return "Dissipate";
            if(card.effect==EffectKind.Sigil)return "NormalSigilTrigger";
            return "BuffPositiveEffect";
        }
        private void PlayHexerCard(CardDef card,float travel)
        {
            if(!HexerVideoEnabled)return;
            var clip=HexerCardAnimation(card);
            StartHexerVideo(clip,AnimationSeconds(clip=="FirstRitual"?1.25f:clip=="FinisherAttack"?1f:.8f),50);
            if(card.kind==CardKind.Attack)hexerAttackVariant++;
            if(combat.memory.echoArmed>0)QueueHexerVideo("EchoTrigger",Time.unscaledTime+travel+.1f,AnimationSeconds(.7f),65);
        }
        private void ScheduleHexerVideoReceipts(CombatEvent[] facts,float[] offsets,float now)
        {
            if(!HexerVideoEnabled)return;
            var activationCounts=facts.Where(f=>f.sigilSlot>=0&&f.label.EndsWith(" ACTIVATE",StringComparison.Ordinal))
                .GroupBy(f=>f.sigilSlot).ToDictionary(g=>g.Key,g=>g.Count());
            var sigilScheduled=false;
            for(var i=0;i<facts.Length;i++)
            {
                var f=facts[i];var at=now+offsets[i];
                var grew=f.sigils!=null&&f.sigils.Length>hexerKnownSigils;
                if(f.sigils!=null)hexerKnownSigils=f.sigils.Length;
                if(grew)QueueHexerVideo("SigilSelected",at,AnimationSeconds(.85f),70);
                else if(f.label=="ECHO SIGIL")QueueHexerVideo("EchoTrigger",at,AnimationSeconds(.7f),65);
                if(f.sigilSlot>=0&&f.label.EndsWith(" ACTIVATE",StringComparison.Ordinal)&&!sigilScheduled)
                {
                    var echo=f.label.StartsWith("ECHO",StringComparison.Ordinal);
                    var repeat=activationCounts.Values.Any(n=>n>1);
                    QueueHexerVideo(echo?"EchoTrigger":repeat?"OverflowTrigger":"NormalSigilTrigger",at,AnimationSeconds(repeat?.8f:.55f),60);
                    sigilScheduled=true;
                }
                if(f.kind==CombatEventKind.Exhaust)QueueHexerVideo("Dissipate",at+.18f,AnimationSeconds(.65f),20);
                if(f.kind==CombatEventKind.Draw&&!f.generatedCard&&combat.phase==CombatPhase.Player)
                    QueueHexerVideo("DrawKnowledgeResponse",at,AnimationSeconds(.6f),10);
            }
        }
        private void PlayHexerVital(CombatEvent fact)
        {
            if(!HexerVideoEnabled||hexerVideos==null||!fact.playerSide)return;
            if(fact.hasVitals&&hexerLastHp<=0&&fact.playerHp>0)
                StartHexerVideo("ReviveRecovery",AnimationSeconds(1.25f),110);
            else if(fact.kind==CombatEventKind.Damage&&fact.amount>0&&(fact.playerHp<=0||Time.unscaledTime-hexerLastReaction>.18f))
            {
                StartHexerVideo(fact.playerHp<=0?"Defeat":fact.amount>=15?"HeavyHitStagger":"LightHitReaction",AnimationSeconds(fact.playerHp<=0?.8f:.65f),100);
                hexerLastReaction=Time.unscaledTime;
            }
            else if(fact.kind==CombatEventKind.Block&&fact.label=="BLOCKED"&&Time.unscaledTime-hexerLastReaction>.18f)
            {
                StartHexerVideo("BarrierImpact",AnimationSeconds(.65f),90);hexerLastReaction=Time.unscaledTime;
            }
            else if(fact.kind==CombatEventKind.Block&&fact.label!="BLOCKED"&&!hexerVideos.ActionPlaying)
                QueueHexerVideo(fact.amount>=12?"MajorDefensiveSkill":"BasicDefensiveSkill",Time.unscaledTime,AnimationSeconds(.65f),30);
            if(fact.hasVitals)hexerLastHp=fact.playerHp;
        }
        private void PlayHexerStatus(CombatEvent fact)
        {
            if(!HexerVideoEnabled||!fact.playerSide||fact.amount<=0||fact.label.StartsWith("TRIGGER:",StringComparison.Ordinal)||fact.label.Contains("SIGIL")||fact.label.EndsWith(" ACTIVATE")||fact.label=="ASPECT ACTIVE")return;
            QueueHexerVideo("BuffPositiveEffect",Time.unscaledTime,AnimationSeconds(.6f),15);
        }
        private bool DrawHexerVideo(Rect hero)=>HexerVideoEnabled&&hexerVideos!=null&&hexerVideos.Draw(hero);
        private void DisposeHexerVideos()
        {
            hexerVideos?.Dispose();hexerVideos=null;hexerVideoCombat=null;hexerVideoBeats.Clear();
            hexerVideoPriority=0;hexerVideoBusyUntil=0;hexerLastReaction=-10;
        }
    }
}
