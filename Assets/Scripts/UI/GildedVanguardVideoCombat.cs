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
        private sealed class VanguardVideoBeat { public string clip; public float at,seconds; public int priority; }
        private readonly List<VanguardVideoBeat> vanguardVideoBeats=new();
        private VanguardVideoSurface vanguardVideos;
        private CombatState vanguardVideoCombat;
        private float vanguardBusyUntil,vanguardLastReaction=-10;
        private int vanguardPriority,vanguardAttackVariant;
        private bool vanguardDefeated,vanguardVideoVerification,vanguardVideoFrozen;
        private bool VanguardVideoEnabled=>run!=null&&run.hero==HeroId.Vanguard&&profile!=null&&!profile.reduceMotion&&VanguardVideoCatalog.Available;

        private void UpdateVanguardVideos()
        {
            if(screen!=ScreenMode.Combat||combat==null||!VanguardVideoEnabled){DisposeVanguardVideos();return;}
            if(vanguardVideoCombat!=combat){DisposeVanguardVideos();vanguardVideoCombat=combat;vanguardDefeated=false;}
            vanguardVideos??=new VanguardVideoSurface();
            var paused=IsRunInspectionPaused||vanguardVideoFrozen;
            vanguardVideos.Tick(paused,displayedPlayerHp*4<=combat.player.maxHp);
            if(vanguardVideoVerification)return;
            if(paused){vanguardBusyUntil+=Time.unscaledDeltaTime;foreach(var b in vanguardVideoBeats)b.at+=Time.unscaledDeltaTime;return;}
            var now=Time.unscaledTime;
            vanguardVideoBeats.RemoveAll(b=>now-b.at>1.5f);
            var next=vanguardVideoBeats.Where(b=>b.at<=now).OrderByDescending(b=>b.priority).FirstOrDefault();
            if(next!=null&&(!vanguardVideos.ActionPlaying||now>=vanguardBusyUntil||next.priority>vanguardPriority))
            {
                vanguardVideoBeats.Remove(next);
                vanguardVideoBeats.RemoveAll(b=>b.at<=now&&b.priority<=next.priority);
                if(!vanguardDefeated)StartVanguardVideo(next.clip,next.seconds,next.priority);
            }
        }
        private void StartVanguardVideo(string clip,float seconds,int priority)
        {
            if(!VanguardVideoEnabled)return;
            vanguardVideos??=new VanguardVideoSurface();vanguardVideos.Play(clip,seconds);
            vanguardPriority=priority;vanguardBusyUntil=Time.unscaledTime+seconds+.12f;
            if(clip=="Defeat"){vanguardDefeated=true;vanguardVideoBeats.Clear();}
        }
        private void QueueVanguardVideo(string clip,float seconds,int priority,float delay=0)
        {
            if(!VanguardVideoEnabled||vanguardDefeated||VanguardVideoCatalog.Find(clip)==null)return;
            if(vanguardVideoBeats.Any(b=>b.clip==clip))return;
            if(vanguardVideoBeats.Count>=8)vanguardVideoBeats.RemoveAt(0);
            vanguardVideoBeats.Add(new VanguardVideoBeat{clip=clip,seconds=seconds,priority=priority,at=Time.unscaledTime+delay});
        }
        private string VanguardCardAnimation(CardDef card)
        {
            if(card.kind==CardKind.Power)return "AspectActivation";
            if(card.kind==CardKind.Attack)
            {
                if(card.rarity==Rarity.Rare||card.value>=28)return "MajorFinisherAttack";
                if(card.keywords.Contains("Heavy")&&combat.memory.attacksThisTurn==0)return "HeavyAttack";
                return vanguardAttackVariant%2==0?"BasicAttack":"AlternateAttack";
            }
            if(card.effect==EffectKind.Block)return card.value>=12?"MajorDefense":"Block";
            if(card.effect==EffectKind.Strength)return "BuffStrength";
            return "Block";
        }
        private void PlayVanguardCard(CardDef card,float travel)
        {
            if(!VanguardVideoEnabled||vanguardDefeated)return;
            var clip=VanguardCardAnimation(card);
            StartVanguardVideo(clip,AnimationSeconds(clip=="MajorFinisherAttack"||clip=="HeavyAttack"?1.05f:.8f),50);
            if(card.kind==CardKind.Attack)vanguardAttackVariant++;
        }
        private void PlayVanguardVital(CombatEvent fact)
        {
            if(!VanguardVideoEnabled||vanguardDefeated)return;
            if(!fact.playerSide)
            {
                if(fact.kind==CombatEventKind.Damage&&fact.label=="RETALIATE"&&fact.amount>0)
                    QueueVanguardVideo("RetaliateAttack",AnimationSeconds(.75f),95,.12f);
                return;
            }
            if(fact.kind==CombatEventKind.Damage&&fact.amount>0&&(fact.playerHp<=0||Time.unscaledTime-vanguardLastReaction>.18f))
            {
                StartVanguardVideo(fact.playerHp<=0?"Defeat":fact.amount>=15?"HeavyHitStagger":"LightHit",AnimationSeconds(fact.playerHp<=0?1.1f:.65f),100);
                vanguardLastReaction=Time.unscaledTime;
            }
            else if(fact.kind==CombatEventKind.Block&&fact.label=="BLOCKED"&&fact.amount>0&&Time.unscaledTime-vanguardLastReaction>.18f)
            {
                StartVanguardVideo("BlockImpact",AnimationSeconds(.6f),90);vanguardLastReaction=Time.unscaledTime;
            }
            else if(fact.kind==CombatEventKind.Block&&fact.label!="BLOCKED"&&fact.amount>0&&!(vanguardVideos?.ActionPlaying??false))
                QueueVanguardVideo(fact.amount>=12?"MajorDefense":"Block",AnimationSeconds(.65f),30);
        }
        private void PlayVanguardStatus(CombatEvent fact)
        {
            if(!VanguardVideoEnabled||!fact.playerSide||fact.amount<=0||fact.label.StartsWith("TRIGGER:",StringComparison.Ordinal))return;
            var clip=fact.label=="STRENGTH"?"BuffStrength":fact.label=="FORTIFY"?"Fortify":fact.label=="RETALIATE"?"RetaliateReady":null;
            if(clip!=null)QueueVanguardVideo(clip,AnimationSeconds(.7f),25);
        }
        private void ResetVanguardVideos(){DisposeVanguardVideos();vanguardAttackVariant=0;vanguardDefeated=false;}
        private bool DrawVanguardVideo(Rect hero)=>VanguardVideoEnabled&&vanguardVideos!=null&&vanguardVideos.Draw(hero);
        private void DisposeVanguardVideos()
        {
            vanguardVideos?.Dispose();vanguardVideos=null;vanguardVideoCombat=null;vanguardVideoBeats.Clear();
            vanguardPriority=0;vanguardBusyUntil=0;vanguardLastReaction=-10;
        }
    }
}
