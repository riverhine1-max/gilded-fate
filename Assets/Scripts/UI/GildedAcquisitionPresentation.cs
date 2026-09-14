using System;
using GildedFate.Audio;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private enum AcquisitionKind { None, Card, Relic, Shard, Modification, Fateweave }
        private AcquisitionKind acquisitionKind;
        private CardDef acquisitionCard;
        private RelicDef acquisitionRelic;
        private FateShardDef acquisitionShard;
        private FateweaveDef acquisitionFateweave;
        private float acquisitionStarted,acquisitionDuration;
        private bool acquisitionActive,acquisitionPerfected;
        private Coroutine acquisitionSequence;

        private void BeginCardAcquisition(CardDef card,Action complete)
        {
            if(card==null||acquisitionActive)return;BeginAcquisition(AcquisitionKind.Card,card,null,complete);
        }
        private void BeginRelicAcquisition(RelicDef relic,Action complete)
        {
            if(relic==null||acquisitionActive)return;BeginAcquisition(AcquisitionKind.Relic,null,relic,complete);
        }
        private void BeginShardAcquisition(FateShardDef shard,Action complete)
        {
            if(shard==null||acquisitionActive)return;acquisitionShard=shard;BeginAcquisition(AcquisitionKind.Shard,null,null,complete);
        }
        private void BeginModificationAcquisition(CardDef card,Action complete,bool perfected=false)
        {
            if(card==null||(!card.IsModified&&!card.perfected)||acquisitionActive)return;
            acquisitionPerfected=perfected||!card.IsModified&&card.perfected;BeginAcquisition(AcquisitionKind.Modification,card,null,complete);
        }
        private void BeginFateweavePull(FateweaveDef fateweave,Action complete)
        {
            if(fateweave==null||acquisitionActive)return;acquisitionFateweave=fateweave;BeginAcquisition(AcquisitionKind.Fateweave,null,null,complete);
        }
        private HashSet<string> SnapshotCardCopies()=>run.cards.Select(card=>card.persistentId).ToHashSet();
        private HashSet<string> SnapshotRelics()=>run.relics.ToHashSet();
        private HashSet<string> SnapshotShards()=>run.shards.Select(shard=>shard.id).ToHashSet();
        private void PresentNewRunAcquisitions(HashSet<string> previousCards,HashSet<string> previousRelics,Action complete=null)
            =>PresentNewRunAcquisitions(previousCards,previousRelics,null,complete);
        private void PresentNewRunAcquisitions(HashSet<string> previousCards,HashSet<string> previousRelics,HashSet<string> previousShards,Action complete=null)
        {
            var cards=run.cards.Where(card=>previousCards==null||!previousCards.Contains(card.persistentId)).Select(card=>card.BuildDefinition()).Where(card=>card!=null).ToList();
            var relics=run.relics.Where(id=>previousRelics==null||!previousRelics.Contains(id)).Select(id=>GameContent.Relics.FirstOrDefault(relic=>relic.id==id)).Where(relic=>relic!=null).ToList();
            var shards=previousShards==null?new List<FateShardDef>():run.shards.Where(state=>!previousShards.Contains(state.id)).Select(state=>WorldContent.FateShards.FirstOrDefault(shard=>shard.id==state.id)).Where(shard=>shard!=null).ToList();
            PresentAcquisitionSequence(cards,shards,relics,complete);
        }
        private void PresentAcquisitionSequence(List<CardDef> cards,List<FateShardDef> shards,List<RelicDef> relics,Action complete)
        {
            if(cards.Count>0){var next=cards[0];cards.RemoveAt(0);BeginCardAcquisition(next,()=>PresentAcquisitionSequence(cards,shards,relics,complete));return;}
            if(shards.Count>0){var next=shards[0];shards.RemoveAt(0);BeginShardAcquisition(next,()=>PresentAcquisitionSequence(cards,shards,relics,complete));return;}
            if(relics.Count>0){var next=relics[0];relics.RemoveAt(0);BeginRelicAcquisition(next,()=>PresentAcquisitionSequence(cards,shards,relics,complete));return;}
            complete?.Invoke();
        }
        private void BeginAcquisition(AcquisitionKind kind,CardDef card,RelicDef relic,Action complete)
        {
            acquisitionKind=kind;acquisitionCard=card;acquisitionRelic=relic;acquisitionActive=true;acquisitionStarted=Time.unscaledTime;
            acquisitionDuration=profile.reduceMotion?.42f:profile.fastMode?.68f:kind is AcquisitionKind.Modification or AcquisitionKind.Fateweave?1.32f:kind==AcquisitionKind.Shard?1.18f:1.05f;
            Sfx(kind switch{AcquisitionKind.Relic=>SoundCue.RewardRelic,AcquisitionKind.Shard=>SoundCue.RewardShard,AcquisitionKind.Modification=>SoundCue.Binding,AcquisitionKind.Fateweave=>SoundCue.Fateweave,_=>card?.rarity==Rarity.Curse?SoundCue.Debuff:SoundCue.RewardCard});
            if(acquisitionSequence!=null)StopCoroutine(acquisitionSequence);acquisitionSequence=StartCoroutine(FinishAcquisition(complete));
        }
        private IEnumerator FinishAcquisition(Action complete)
        {
            var end=Time.unscaledTime+acquisitionDuration;while(Time.unscaledTime<end)yield return null;
            acquisitionActive=false;acquisitionKind=AcquisitionKind.None;acquisitionCard=null;acquisitionRelic=null;acquisitionShard=null;acquisitionFateweave=null;acquisitionSequence=null;complete?.Invoke();
        }

        private string CombatRewardTitle => currentNode?.kind==NodeKind.Boss?"THE VAULT YIELDS":currentNode?.kind==NodeKind.Elite?"A GUARDIAN FALLS":"VICTORY";

        private void DrawAcquisitionPresentation(float w,float h)
        {
            if(!acquisitionActive)return;var t=Mathf.Clamp01((Time.unscaledTime-acquisitionStarted)/Mathf.Max(.01f,acquisitionDuration));var reveal=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.25f));var depart=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.72f)/.28f));
            Fill(new Rect(0,0,w,h),new Color(0,0,0,.66f*reveal*(1-depart*.35f)));var center=new Vector2(w*.5f,h*.48f);var target=AcquisitionTarget(w);var position=Vector2.Lerp(center,target,depart);var scale=Mathf.Lerp(.72f,.13f,depart)*Mathf.Lerp(.78f,1f,reveal);
            if((acquisitionKind==AcquisitionKind.Card||acquisitionKind==AcquisitionKind.Modification)&&acquisitionCard!=null)
            {
                var modified=acquisitionKind==AcquisitionKind.Modification;var matrix=GUI.matrix;var cardScale=modified?scale*1.04f:scale;GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(position.x,position.y,0),Quaternion.identity,new Vector3(cardScale,cardScale,1));DrawCard(new Rect(-150,-205,300,410),acquisitionCard);GUI.matrix=matrix;
                if(modified)
                {
                    var fate=!acquisitionPerfected&&acquisitionCard.specialModificationKind==SpecialModificationKind.Fateweave;
                    var attachment=CardAttachments(acquisitionCard).FirstOrDefault(a=>acquisitionPerfected?a.title.StartsWith("PERFECTED"):a.binding);
                    var accent=acquisitionPerfected?new Color(1f,.84f,.40f):fate?new Color(1f,.63f,.24f):new Color(.32f,.87f,1f);
                    var iconSize=Mathf.Lerp(72,25,depart);var iconCenter=position+new Vector2(-128*cardScale,-72*cardScale);
                    var icon=new Rect(iconCenter.x-iconSize*.5f,iconCenter.y-iconSize*.5f,iconSize,iconSize);
                    if(attachment!=null)DrawAtlasIcon(attachment.binding?bindingFateIconAtlas:LoadAuthoredArt(attachment.resource),attachment.tile,attachment.columns,attachment.rows,icon);
                    if(!profile.reduceMotion&&!profile.reducedVfx)for(var i=0;i<12;i++){var a=i*Mathf.PI*2/12+shimmer;var radius=(42+65*(1-reveal))*(1-depart);var p=iconCenter+new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius);Fill(new Rect(p.x-2,p.y-2,4,4),new Color(accent.r,accent.g,accent.b,(1-depart)*.65f));}
                    if(depart<.4f)
                    {
                        GUI.Label(new Rect(w*.20f,h*.10f,w*.60f,46),acquisitionPerfected?"PERFECTED":fate?"FATEWOVEN":"BINDING ENGRAVED",new GUIStyle(titleStyle){fontSize=31,normal={textColor=accent}});
                        GUI.Label(new Rect(w*.26f,h*.74f,w*.48f,32),attachment?.title??"CARD MODIFIED",new GUIStyle(titleStyle){fontSize=22,normal={textColor=Color.Lerp(accent,Color.white,.25f)}});
                        GUI.Label(new Rect(w*.25f,h*.79f,w*.50f,h*.18f),attachment?.detail??"",new GUIStyle(footerStyle){fontSize=15,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.94f,.92f,.86f)}});
                    }
                }
                else if(depart<.4f){GUI.Label(new Rect(w*.24f,h*.13f,w*.52f,44),acquisitionCard.rarity==Rarity.Curse?"CURSE ADDED":"CARD ADDED",new GUIStyle(titleStyle){fontSize=31,normal={textColor=acquisitionCard.rarity==Rarity.Curse?new Color(1f,.35f,.48f):Gold}});GUI.Label(new Rect(w*.28f,h*.79f,w*.44f,38),"The card has entered your deck.",new GUIStyle(subtitleStyle){fontSize=13});}
            }
            else if(acquisitionKind==AcquisitionKind.Relic&&acquisitionRelic!=null)
            {
                var index=System.Array.FindIndex(GameContent.Relics,r=>r.id==acquisitionRelic.id);var size=220*scale/.72f;var art=new Rect(position.x-size*.5f,position.y-size*.5f,size,size);if(!profile.reduceMotion)art.y+=Mathf.Sin(shimmer*2.2f)*5*(1-depart);DrawRelicArt(art,index);
                if(depart<.4f){GUI.Label(new Rect(w*.22f,h*.14f,w*.56f,46),"RELIC ACQUIRED",new GUIStyle(titleStyle){fontSize=31,normal={textColor=Gold}});GUI.Label(new Rect(w*.25f,h*.68f,w*.5f,42),acquisitionRelic.name,new GUIStyle(titleStyle){fontSize=25,normal={textColor=new Color(1f,.87f,.55f)}});GUI.Label(new Rect(w*.29f,h*.74f,w*.42f,75),acquisitionRelic.text,new GUIStyle(footerStyle){fontSize=16,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.94f,.92f,.86f)}});}
            }
            else if(acquisitionKind==AcquisitionKind.Shard&&acquisitionShard!=null)
            {
                var size=220*scale/.72f;var art=new Rect(position.x-size*.5f,position.y-size*.5f,size,size);if(!profile.reduceMotion)art.y+=Mathf.Sin(shimmer*3.1f)*5*(1-depart);DrawFateShardArt(art,acquisitionShard);
                if(!profile.reduceMotion&&!profile.reducedVfx)for(var i=0;i<18;i++){var angle=i*Mathf.PI*2/18-shimmer*1.4f;var radius=(76+90*reveal)*(1-depart);var spark=center+new Vector2(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius*.72f);Fill(new Rect(spark.x-2,spark.y-2,4,4),new Color(i%3==0?.45f:1f,i%3==0?.82f:.72f,1f,(1-depart)*.82f));}
                if(depart<.4f){GUI.Label(new Rect(w*.20f,h*.13f,w*.60f,46),"FATE SHARD ACQUIRED",new GUIStyle(titleStyle){fontSize=31,normal={textColor=new Color(.65f,.88f,1f)}});GUI.Label(new Rect(w*.25f,h*.68f,w*.50f,42),acquisitionShard.name,new GUIStyle(titleStyle){fontSize=25,normal={textColor=new Color(1f,.87f,.55f)}});GUI.Label(new Rect(w*.27f,h*.74f,w*.46f,88),acquisitionShard.stableText+"\nThree uses · the final use becomes Fractured.",new GUIStyle(footerStyle){fontSize=15,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.94f,.92f,.86f)}});}
            }
            else if(acquisitionKind==AcquisitionKind.Fateweave&&acquisitionFateweave!=null)
            {
                var accent=new Color(1f,.70f,.27f);var snap=profile.reduceMotion?1:Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.18f)/.52f));var iconSize=Mathf.Lerp(126,184,reveal)*(1-depart*.55f);var iconCenter=new Vector2(center.x,center.y-15);DrawAtlasIcon(bindingFateIconAtlas,FateweaveIconIndex(acquisitionFateweave.id),4,4,new Rect(iconCenter.x-iconSize*.5f,iconCenter.y-iconSize*.5f,iconSize,iconSize));
                var top=new Vector2(center.x,0);var end=Vector2.Lerp(new Vector2(center.x,center.y-iconSize*.56f),center,snap);for(var segment=0;segment<14;segment++){var a=segment/14f;var b=(segment+1)/14f;var pa=Vector2.Lerp(top,end,a)+Vector2.right*Mathf.Sin(segment*.83f+shimmer*3)*8*(1-snap);var pb=Vector2.Lerp(top,end,b)+Vector2.right*Mathf.Sin((segment+1)*.83f+shimmer*3)*8*(1-snap);DrawLine(pa,pb,new Color(1f,.72f,.25f,.92f*(1-depart)),snap>.7f?1:4);}
                if(!profile.reduceMotion&&!profile.reducedVfx)for(var i=0;i<22;i++){var a=i*Mathf.PI*2/22+shimmer*.7f;var radius=50+snap*130;var p=center+new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius*.62f);Fill(new Rect(p.x-2,p.y-2,4,4),new Color(i%3==0?.62f:1f,i%3==0?.36f:.7f,i%3==0?1f:.25f,(1-depart)*.75f));}
                if(depart<.4f){GUI.Label(new Rect(w*.20f,h*.12f,w*.60f,46),"THE STRAND IS PULLED",new GUIStyle(titleStyle){fontSize=32,normal={textColor=accent}});GUI.Label(new Rect(w*.25f,h*.72f,w*.50f,34),acquisitionFateweave.name,new GUIStyle(titleStyle){fontSize=24,normal={textColor=new Color(1f,.88f,.54f)}});GUI.Label(new Rect(w*.27f,h*.77f,w*.46f,64),acquisitionFateweave.text,new GUIStyle(footerStyle){fontSize=15,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.94f,.92f,.86f)}});}
            }
            if(!profile.reduceMotion&&!profile.reducedVfx)for(var i=0;i<14;i++){var a=i*Mathf.PI*2/14+shimmer*.35f;var radius=(70+90*reveal)*(1-depart);Fill(new Rect(center.x+Mathf.Cos(a)*radius-2,center.y+Mathf.Sin(a)*radius-2,4,4),new Color(1f,.75f,.28f,(1-depart)*.75f));}
        }
    }
}
