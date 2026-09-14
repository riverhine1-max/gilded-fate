using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private struct CombatHudTarget
        {
            public string key,title,detail;
            public Rect rect;
            public int group,command,index;
        }
        private readonly List<CombatHudTarget> combatHudTargets=new(96);
        private bool combatHudInspectActive;
        private string combatHudFocusKey;
        private void RegisterCombatHudTarget(string key,int group,Rect rect,string title,string detail,int command=0,int index=0)
        {
            if(screen!=ScreenMode.Combat||Event.current.type!=EventType.Repaint||routeInspectionOpen)return;
            combatHudTargets.Add(new CombatHudTarget{key=key,group=group,rect=rect,title=title,detail=detail,command=command,index=index});
        }
        private int CombatHudFocusIndex
        {
            get
            {
                var found=combatHudTargets.FindIndex(t=>t.key==combatHudFocusKey);
                if(found>=0)return found;
                if(combatHudTargets.Count>0){combatHudFocusKey=combatHudTargets[0].key;return 0;}
                return -1;
            }
        }
        private bool OpenCombatHudFocus(int group=1)
        {
            if(combatBusy||choicePresented||dragView!=null||controllerTargeting||combatPauseOpen||ShardDiscoveryOpen||inspectedCard!=null||combatHudTargets.Count==0)return false;
            combatHudInspectActive=true;controllerNavigation=true;selectedView=hoverView=null;
            var first=combatHudTargets.FindIndex(t=>t.group==group);
            combatHudFocusKey=combatHudTargets[Mathf.Max(0,first)].key;return true;
        }
        private void MoveCombatHudFocus(int delta,bool changeGroup)
        {
            var current=CombatHudFocusIndex;if(current<0)return;var group=combatHudTargets[current].group;
            if(changeGroup)
            {
                var groups=combatHudTargets.Select(t=>t.group).Distinct().OrderBy(g=>g).ToArray();
                var next=(System.Array.IndexOf(groups,group)+delta+groups.Length)%groups.Length;
                combatHudFocusKey=combatHudTargets.Find(t=>t.group==groups[next]).key;return;
            }
            var matches=combatHudTargets.Where(t=>t.group==group).ToArray();
            var selected=System.Array.FindIndex(matches,t=>t.key==combatHudFocusKey);
            combatHudFocusKey=matches[(selected+delta+matches.Length)%matches.Length].key;
        }
        private void ConfirmCombatHudFocus()
        {
            var i=CombatHudFocusIndex;if(i<0)return;var target=combatHudTargets[i];if(target.command==0)return;
            combatHudInspectActive=false;
            if(target.command==1)OpenRunDeck();
            else if(target.command==2)OpenRouteInspection();
            else if(target.command==3)ToggleRunSettings();
            else if(target.command==4&&target.index>=0&&target.index<run.shards.Count)
            {
                var owned=run.shards[target.index];
                var shard=Core.WorldContent.FateShards.FirstOrDefault(s=>s.id==owned.id);
                if(shard!=null&&owned.CanActivate&&string.IsNullOrEmpty(combat.activeShardId)&&CanAcceptCombatInput)QueueShard(shard,owned,target.index);
                else{combatHudInspectActive=true;ShowInputHint("This shard cannot be awakened now.");}
            }
        }
        private bool HandleCombatHudNavigation(MenuNavigation input)
        {
            if(input.page<0||input.hud)
            {
                if(combatHudInspectActive)combatHudInspectActive=false;else OpenCombatHudFocus();
                return true;
            }
            if(input.page>0&&!combatHudInspectActive)return OpenCombatHudFocus(8);
            if(!combatHudInspectActive)return false;
            if(input.back){combatHudInspectActive=false;return true;}
            if(input.x!=0)MoveCombatHudFocus(input.x,false);
            if(input.y!=0)MoveCombatHudFocus(input.y,true);
            if(input.accept)ConfirmCombatHudFocus();
            return true;
        }
        private void DrawCombatHudFocus(float w,float h)
        {
            if(!combatHudInspectActive||screen!=ScreenMode.Combat||routeInspectionOpen)return;
            var i=CombatHudFocusIndex;if(i<0){combatHudInspectActive=false;return;}
            var target=combatHudTargets[i];var r=target.rect;const float reach=7;
            foreach(var corner in new[]{new Vector2(r.x-4,r.y-4),new Vector2(r.xMax+4,r.y-4),new Vector2(r.xMax+4,r.yMax+4),new Vector2(r.x-4,r.yMax+4)})
            {
                DrawLine(corner,corner+new Vector2(corner.x<r.center.x?reach:-reach,0),Gold,2);
                DrawLine(corner,corner+new Vector2(0,corner.y<r.center.y?reach:-reach),Gold,2);
            }
            var detail=target.detail;
            if(captureMode&&CommandValue("-gfCapture")=="polish-tooltip")detail=string.Join("\n\n",RuleKeywords.Select(k=>k.title+"\n"+k.detail));
            SetCombatEffectTooltip(target.title,detail,r.center);
            DrawCombatEffectTooltip(w,h);
            GUI.Label(new Rect(22,h-27,540,20),menuUsesGamepad?"D-PAD / STICK · INSPECT    UP/DOWN · GROUP    A · CONFIRM    B · HAND":"ARROWS · INSPECT    UP/DOWN · GROUP    ENTER · CONFIRM    BACKSPACE · HAND",
                new GUIStyle(footerStyle){fontSize=10,alignment=TextAnchor.MiddleLeft,normal={textColor=Gold}});
        }
    }
}
