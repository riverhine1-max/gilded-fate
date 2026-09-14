using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Combat;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private CombatState intentPreviewCombat;
        private List<EnemyIntentAction>[] intentPreviews;
        private sealed class IntentIconMotion {public string signature;public int oldIcon;public float changedAt;}
        private readonly Dictionary<string,IntentIconMotion> intentIconMotions=new();
        private readonly HashSet<EnemyActionType> warnedIntentTypes=new();
        private void InvalidateEnemyIntents(){intentPreviews=null;}
        private List<EnemyIntentAction> EnemyIntents(int owner)
        {
            if(intentPreviewCombat!=combat){intentPreviewCombat=combat;intentPreviews=null;intentIconMotions.Clear();}
            intentPreviews??=combat.PreviewEnemyIntents();
            return intentPreviews[Mathf.Clamp(owner,0,intentPreviews.Length-1)];
        }
        private string CompleteIntentDetail(int owner)=>string.Join("\n\n",EnemyIntents(owner).Select((a,i)=>(i+1)+". "+a.title+"\n"+a.detail));
        private Color IntentAccent(EnemyActionType type)=>type switch
        {
            EnemyActionType.Attack=>new Color(1,.43f,.34f),EnemyActionType.Block=>new Color(.37f,.77f,1),
            EnemyActionType.Strength or EnemyActionType.StealGold or EnemyActionType.SummonWeapon=>Gold,
            EnemyActionType.Heal=>new Color(.43f,.92f,.58f),_=>new Color(.83f,.61f,1)
        };
        private Rect IntentActionRect(int owner,int index)
        {
            var actions=EnemyIntents(owner);var portrait=GroupCombat?GroupPresentedPortrait(owner):EnemyPortraitRect;
            var width=GroupCombat?GroupCell(owner).width-20:Mathf.Max(340,portrait.width+70);
            var size=GroupCombat&&combat.EnemyCount==4?(profile.largeIntents?56f:52f):profile.largeIntents?64f:56f;
            var columns=EnemyIntentLayout.Columns(actions.Count,width,size);
            var rows=Mathf.CeilToInt(actions.Count/(float)columns);
            var destination=actions.Any(a=>a.destination!=IntentDestination.None);
            var rowHeight=EnemyIntentLayout.RowHeight(size,destination);
            var top=Mathf.Max(77,portrait.y-rows*rowHeight-10);
            return new Rect(EnemyIntentLayout.CellX(portrait.center.x,index,actions.Count,columns,size),
                top+index/columns*rowHeight,EnemyIntentLayout.CellWidth(size),rowHeight);
        }
        private void DrawEnemyIntentGroup(int owner)
        {
            var actions=EnemyIntents(owner);var atlas=LoadAuthoredArt("Art/UI/MasterPolish/EnemyIntents");
            for(var i=0;i<actions.Count;i++)
            {
                var action=actions[i];
                if(string.IsNullOrEmpty(action.title)&&(Debug.isDebugBuild||Application.isEditor)&&warnedIntentTypes.Add(action.type))Debug.LogWarning("Enemy action '"+action.type+"' has no Intent presentation.");
                var r=IntentActionRect(owner,i);var size=r.width-10;
                var icon=new Rect(r.center.x-size*.5f,r.y,size,size);var key=owner+":"+i;
                var signature=action.type+":"+action.ValueText+":"+action.Icon+":"+action.prevented;
                if(!intentIconMotions.TryGetValue(key,out var motion)){motion=new IntentIconMotion{signature=signature,oldIcon=action.Icon,changedAt=-10};intentIconMotions[key]=motion;}
                if(motion.signature!=signature){var previous=motion.signature.Split(':');motion.oldIcon=previous.Length>2&&int.TryParse(previous[2],out var old)?old:action.Icon;motion.signature=signature;motion.changedAt=Time.unscaledTime;}
                var age=Time.unscaledTime-motion.changedAt;var changing=age>=0&&age<.23f;
                var oldColor=GUI.color;var alpha=action.prevented||action.amount==0?.48f:1f;GUI.color=new Color(1,1,1,alpha);
                if(changing&&!profile.reduceMotion){var grow=Mathf.Sin(age/.23f*Mathf.PI)*3;icon=new Rect(icon.x-grow,icon.y-grow,icon.width+grow*2,icon.height+grow*2);}
                if(atlas)
                {
                    if(changing&&motion.oldIcon!=action.Icon&&!profile.reduceMotion&&!profile.reduceFlashing){GUI.color=new Color(1,1,1,alpha*(1-age/.23f));DrawAtlasIcon(atlas,motion.oldIcon,5,5,icon);GUI.color=new Color(1,1,1,alpha*age/.23f);}
                    DrawAtlasIcon(atlas,action.Icon,5,5,icon);GUI.color=new Color(1,1,1,alpha);
                }
                else if((Debug.isDebugBuild||Application.isEditor)&&warnedIntentTypes.Add(action.type))
                    Debug.LogWarning("Enemy action '"+action.type+"' has no Intent artwork.");
                var label=new Rect(r.x-4,r.y+size,r.width+8,23);
                ShadowLabel(label,action.prevented?"—":action.ValueText,new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=profile.largeIntents?20:18,fontStyle=FontStyle.Bold,normal={textColor=Color.white}});
                if(action.destination!=IntentDestination.None)
                {
                    var marker=(int)action.destination+14;var y=r.y+size+21;
                    if(atlas)DrawAtlasIcon(atlas,marker,5,5,new Rect(r.center.x-28,y,13,13));
                    GUI.Label(new Rect(r.center.x-13,y,48,13),action.destination.ToString().ToUpperInvariant(),new GUIStyle(footerStyle){fontSize=9,alignment=TextAnchor.MiddleLeft,normal={textColor=IntentAccent(action.type)}});
                }
                GUI.color=oldColor;
                RegisterCombatHudTarget("enemy:"+owner+":intent:"+i,2+owner,r,action.title,action.detail);
                if(CombatInspectionAllowed&&r.Contains(combatPointer))SetCombatEffectTooltip(action.title,action.detail,r.center);
            }
        }
    }
}
