using System.Linq;
using GildedFate.Core;
using GildedFate.Map;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private MapNode CurrentMapLocation=>run.nodes?.FirstOrDefault(n=>n.floor==run.activeNodeFloor&&n.lane==run.activeNodeLane)
            ??run.nodes?.Where(n=>n.complete).OrderByDescending(n=>n.floor).FirstOrDefault();
        private void DrawCurrentMapMarker(Rect r)
        {
            DrawIdentityMapRing(r);
        }
        private void DrawFateMap(float w,float h,bool inspectOnly=false)
        {
            Fill(new Rect(0,0,w,h),new Color(.003f,.004f,.007f,1));DrawPersistentRunBar(w,inspectOnly&&screen==ScreenMode.Combat);
            var viewport=MapViewport(w,h);var maxScroll=Mathf.Max(0,MapContentHeight-viewport.height);FocusMapToCurrentFloor(w,h);var pointer=PointerPosition;
            if(MapScrollZone(w,h).Contains(pointer)&&Event.current.type==EventType.ScrollWheel&&!mapPauseOpen){if(mapWheelFrame!=Time.frameCount){mapScroll=Mathf.Clamp(mapScroll+Event.current.delta.y*34,0,maxScroll);mapFocusFloor=run.floor;mapWheelFrame=Time.frameCount;}Event.current.Use();}
            var local=pointer-viewport.position+new Vector2(0,mapScroll);
            var hovered=viewport.Contains(pointer)&&!mapPauseOpen?run.nodes.FirstOrDefault(n=>Vector2.Distance(MapPosition(n,viewport.width),local)<46):null;
            var choices=run.nodes.Where(n=>n.floor==run.floor&&n.available).OrderBy(n=>n.lane).ToArray();
            if(controllerNavigation&&choices.Length>0)hovered=choices[Mathf.Clamp(mapControllerIndex,0,choices.Length-1)];
            foreach(var from in run.nodes.Where(n=>n.nextMask!=0))foreach(var target in run.nodes.Where(n=>n.floor==from.floor+1&&(from.nextMask&(1<<n.lane))!=0))
            {
                var a=viewport.position+MapPosition(from,viewport.width)-new Vector2(0,mapScroll);var b=viewport.position+MapPosition(target,viewport.width)-new Vector2(0,mapScroll);
                var completed=from.complete&&(target.complete||target==CurrentMapLocation);var current=from.complete&&target.available&&target.floor==run.floor;var hot=from==hovered||target==hovered;
                DrawFateThread(a,b,viewport,completed,current,hot);
            }
            MapNode clicked=null;GUI.BeginGroup(viewport);
            foreach(var node in run.nodes)
            {
                var current=node==CurrentMapLocation;var p=MapPosition(node,viewport.width)-new Vector2(0,mapScroll);var active=node.floor==run.floor&&node.available;var hot=node==hovered;var size=node.kind==NodeKind.Boss?108:current?84:hot?84:active?78:64;
                if(p.y<-size||p.y>viewport.height+size)continue;var r=new Rect(p.x-size*.5f,p.y-size*.5f,size,size);var old=GUI.color;
                GUI.color=current?Color.white:node.complete?new Color(.44f,.42f,.37f):active||hot?Color.white:new Color(.78f,.77f,.71f);
                DrawMapNodeIcon((int)node.kind,r);GUI.color=old;
                if(current)DrawCurrentMapMarker(r);
                var roomLabel=node.kind==NodeKind.Sanctuary?"REST SHRINE":node.kind.ToString().ToUpperInvariant();
                GUI.Label(new Rect(p.x-95,p.y+size*.5f+7,190,22),roomLabel+(current?" · HERE":node.complete?" · CLEARED":""),new GUIStyle(footerStyle){fontSize=11,normal={textColor=current||active?Gold:new Color(.78f,.77f,.70f)}});
                if(!inspectOnly&&active&&!mapPauseOpen&&pendingMapNode==null&&GUI.Button(r,"",GUIStyle.none))clicked=node;
            }
            GUI.EndGroup();
            var boss=run.nodes.FirstOrDefault(n=>n.kind==NodeKind.Boss);if(boss!=null)
            {
                var crown=new Rect(w-207,106,126,132);DrawBossMapPortrait(crown,boss);
                GUI.Label(new Rect(w-258,244,232,42),"ACT "+RomanAct(run.act)+" · SUMMIT KEEPER",new GUIStyle(footerStyle){fontSize=12,normal={textColor=Gold}});
                if(crown.Contains(pointer))SetRunHudTooltip(crown,EnemyForNode(boss).name,"The keeper waits above "+run.ActFloorCount+" layers of fate. Click to view the summit; enter only after reaching its connected path.");
                if(!mapPauseOpen&&GUI.Button(crown,"",GUIStyle.none)){if(!inspectOnly&&boss.floor==run.floor&&boss.available)BeginMapTravel(boss);else{mapScroll=0;mapFocusFloor=run.floor;}}
            }
            DrawMapLegend(w,h);
            var rail=new Rect(viewport.xMax-5,viewport.y+8,2,viewport.height-16);Fill(rail,new Color(.5f,.36f,.2f,.4f));var thumb=rail.height*viewport.height/MapContentHeight;Fill(new Rect(rail.x,rail.y+(rail.height-thumb)*mapScroll/Mathf.Max(1,maxScroll),2,thumb),Gold);
            GUI.Label(new Rect(viewport.x,viewport.yMax-25,viewport.width,22),"SCROLL TO EXPLORE · FOLLOW THE GILDED THREADS",new GUIStyle(footerStyle){fontSize=10,normal={textColor=new Color(.74f,.67f,.52f)}});
            if(hovered!=null&&!mapPauseOpen){var at=viewport.position+MapPosition(hovered,viewport.width)-new Vector2(0,mapScroll);SetRunHudTooltip(new Rect(at.x-35,at.y-35,70,70),hovered.kind==NodeKind.Sanctuary?"REST SHRINE":hovered.kind.ToString().ToUpperInvariant(),NodeDescription(hovered.kind));}
            if(!inspectOnly){if(clicked!=null)BeginMapTravel(clicked);DrawMapTravelTransition(w,h,viewport);if(mapPauseOpen)DrawMapPauseMenu(w,h);}
        }
    }
}
