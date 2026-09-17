using GildedFate.Audio;
using System.Collections.Generic;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private sealed class VitalBeat { public CombatEvent fact; public CardDef source; public float time; public CombatNumber number; }
        private readonly List<VitalBeat> vitalBeats=new();
        private int displayedPlayerHp,displayedEnemyHp,displayedPlayerBlock,displayedEnemyBlock;
        private float lastVitalScheduled=float.NegativeInfinity,lastVitalShownAt=float.NegativeInfinity;

        private void ResetVitalsPlayback()
        {
            vitalBeats.Clear();lastVitalScheduled=lastVitalShownAt=float.NegativeInfinity;ResetOpponentVisuals();SyncDisplayedVitals();
        }
        private void SyncDisplayedVitals()
        {
            SyncOpponentVisuals();displayedPlayerHp=combat?.player.hp??0;displayedEnemyHp=combat?.enemy.hp??0;
            displayedPlayerBlock=combat?.player.block??0;displayedEnemyBlock=combat?.enemy.block??0;
        }
        private void UpdateVitalsPlayback(float now)
        {
            while(vitalBeats.Count>0&&vitalBeats[0].time<=now)
            {
                var beat=vitalBeats[0];
                // A late frame must not compress sequential impacts into a single
                // visible update. Equal-time receipts (e.g. Block + HP for one hit)
                // still land together; intentionally compressed engines retain
                // their shorter authored gaps rather than gaining extra delays.
                var authoredGap=beat.time-lastVitalScheduled;
                if(authoredGap>.001f&&now-lastVitalShownAt<Mathf.Min(.08f,authoredGap))break;
                if(authoredGap>.001f||float.IsNegativeInfinity(lastVitalScheduled))lastVitalShownAt=now;
                lastVitalScheduled=beat.time;
                var fact=beat.fact;vitalBeats.RemoveAt(0);
                if(beat.number!=null)beat.number.start=now;
                PlayVitalSound(fact,beat.source);
                PlayHexerVital(fact);
                if(fact.hasVitals)
                {
                    if(fact.enemyIndex>=0&&fact.enemyIndex<opponentVisuals.Count){var v=opponentVisuals[fact.enemyIndex];v.hp=fact.enemyHp;v.block=fact.enemyBlock;if(!fact.playerSide&&fact.kind==CombatEventKind.Damage)v.hit=1;}
                    displayedPlayerHp=fact.playerHp;displayedEnemyHp=fact.enemyHp;
                    displayedPlayerBlock=fact.playerBlock;displayedEnemyBlock=fact.enemyBlock;
                }
                var damage=fact.kind==CombatEventKind.Damage;var block=fact.kind==CombatEventKind.Block;
                var effect=damage?7:block?1:5;
                if(fact.playerSide){playerVfxIndex=effect;playerVfxTime=.48f;}else{enemyVfxIndex=effect;enemyVfxTime=.48f;}
                if(damage)
                {
                    if(fact.playerSide)heroHit=1;else foeHit=1;
                    impactShake=Mathf.Max(impactShake,Mathf.Min(.65f,fact.amount/35f));
                }
                else{if(fact.playerSide)heroBuff=1;else foeBuff=1;}
            }
            // Rules remain atomic/save-safe. Once every receipt has been shown,
            // reconcile non-hit changes (e.g. Block clearing at turn start).
            if(vitalBeats.Count==0)SyncDisplayedVitals();
        }
    }
}
