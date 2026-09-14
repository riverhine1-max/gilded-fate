using System.Collections.Generic;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private sealed class RetaliateReturn
        {
            public int amount,owner;
            public float holdAt,start,landAt;
        }
        private readonly List<RetaliateReturn> retaliateReturns=new();
        private int VisibleRetaliation
        {
            get
            {
                var amount=presentedRetaliation;var now=Time.unscaledTime;
                foreach(var beat in retaliateReturns)if(now>=beat.holdAt&&now<beat.landAt)amount+=beat.amount;
                return amount;
            }
        }
        private void ScheduleRetaliateReturns(CombatEvent[] facts,float[] offsets,float now)
        {
            for(var i=0;i<facts.Length;i++)
            {
                var spent=facts[i];if(spent.kind!=CombatEventKind.Status||spent.label!="RETALIATE"||spent.amount>=0)continue;
                for(var j=i+1;j<facts.Length;j++)
                {
                    if(facts[j].label!="TRIGGER:RETALIATE"||facts[j].enemyIndex!=spent.enemyIndex)continue;
                    // Fatal attacks do not return damage. Full enemy Block still does.
                    if(j+1<facts.Length&&CombatHitTiming.IsHealthFact(facts[j+1])&&!facts[j+1].playerSide)
                        retaliateReturns.Add(new RetaliateReturn{amount=-spent.amount,owner=spent.enemyIndex,holdAt=now+offsets[i],start=now+offsets[j],landAt=now+offsets[j+1]});
                    break;
                }
            }
        }
        private void DrawRetaliateReturns()
        {
            var now=Time.unscaledTime;
            foreach(var beat in retaliateReturns)
            {
                if(now<beat.start||now>=beat.landAt)continue;
                var chips=PlayerEffectChips();var index=chips.FindIndex(c=>c.title=="RETALIATE");
                var from=EffectCell(PlayerEffectArea(chips.Count),index,chips.Count).center;
                var to=(GroupCombat?GroupPortrait(beat.owner):EnemyPortraitRect).center;
                var t=Mathf.Clamp01((now-beat.start)/Mathf.Max(.01f,beat.landAt-beat.start));
                if(profile.reduceMotion||profile.reducedVfx)
                {
                    DrawLine(from,from+Vector2.right*18,new Color(1,.8f,.35f,1-t),2);
                    continue;
                }
                Vector2 At(float p)=>Vector2.Lerp(from,to,p)+Vector2.up*Mathf.Sin(p*Mathf.PI)*72;
                var a=At(Mathf.Max(0,t-.16f));var b=At(t);
                DrawLine(a,b,new Color(1,.75f,.27f,.85f),3);
                DrawAtlasIcon(combatReadabilityAtlas,4,8,8,new Rect(b.x-13,b.y-13,26,26));
            }
        }
        private string rewardPresentationKey;
        private float importantRewardStarted;
        private bool FateweavePullActive=>acquisitionActive&&acquisitionKind==AcquisitionKind.Fateweave;
        private float FateweaveChoiceOpacity=>!FateweavePullActive?1:1-Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-acquisitionStarted)/.36f));
        private float RewardChoiceOpacity=>!acquisitionActive||acquisitionKind!=AcquisitionKind.Card?1:1-Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-acquisitionStarted)/.24f));

        // One entrance per actual encounter, not per visit to Deck or Settings.
        private bool BeginRewardPresentation()
        {
            var key=run.runId+"/"+run.RoomReceipt;
            if(rewardPresentationKey==key)return false;
            rewardPresentationKey=key;importantRewardStarted=Time.unscaledTime;return true;
        }
        private void DrawImportantRewardAtmosphere(float w,float h)
        {
            var boss=currentNode?.kind==NodeKind.Boss;var elite=currentNode?.kind==NodeKind.Elite;
            if(!boss&&!elite)return;
            var elapsed=Time.unscaledTime-importantRewardStarted;
            var entrance=profile.reduceMotion?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.6f));
            var strength=boss?1f:.58f;var glow=profile.reducedVfx?.025f:.065f+.035f*(1-Mathf.Clamp01(elapsed/1.2f));
            var previous=GUI.color;GUI.color=new Color(1,.76f,.32f,glow*entrance*strength);
            DrawAtlasIcon(combatVfxAtlas,4,4,2,new Rect(w*.5f-310,128,620,520));GUI.color=previous;
            var span=Mathf.Min(350,w*.3f)*entrance;var y=h*.245f;
            DrawLine(new Vector2(w*.5f-span,y),new Vector2(w*.5f-78,y),new Color(.9f,.69f,.34f,.5f*entrance*strength),1);
            DrawLine(new Vector2(w*.5f+78,y),new Vector2(w*.5f+span,y),new Color(.9f,.69f,.34f,.5f*entrance*strength),1);
            GUI.Label(new Rect(w*.5f-96,y-11,192,23),boss?"ACT SEAL BROKEN":"ELITE DEFEATED",new GUIStyle(footerStyle){fontSize=12,fontStyle=FontStyle.Bold,normal={textColor=new Color(1,.83f,.5f,entrance)}});
        }
        private void DrawFateweaveVoid(float w,float h)
        {
            Fill(new Rect(0,0,w,h),new Color(.003f,.004f,.009f));
            // No room art, horizon or floor: the strands exist outside the Vault.
            if(!profile.reducedVfx)
            {
                var previous=GUI.color;GUI.color=new Color(.55f,.28f,.74f,.09f);
                DrawAtlasIcon(combatVfxAtlas,4,4,2,new Rect(w*.5f-470,h*.5f-370,940,740));GUI.color=previous;
            }
            for(var i=0;i<20;i++)
            {
                var x=Mathf.Repeat(i*137.71f+57,w);var sway=profile.reduceMotion?0:Mathf.Sin(shimmer*.12f+i)*5;
                DrawLine(new Vector2(x+sway,0),new Vector2(x+Mathf.Sin(i)*35,h),new Color(.87f,.62f,.26f,.025f+i%4*.008f),1);
            }
            DrawRunHud(w);
        }
    }
}
