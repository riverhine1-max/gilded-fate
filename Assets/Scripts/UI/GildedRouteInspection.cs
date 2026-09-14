using UnityEngine;
using UnityEngine.InputSystem;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool routeInspectionOpen,routeSavedPause;
        private float routeSavedScroll;
        private int routeSavedFocus;
        private Rect RouteMapButton(float width)=>new Rect(width-185,7,46,44);
        private bool CanInspectRoute=>!run.closed&&run.nodes?.Count>0&&!acquisitionActive&&!ShardDiscoveryOpen&&pendingMapNode==null&&!runPauseOpen&&!combatPauseOpen&&!mapPauseOpen
            &&(screen!=ScreenMode.Combat||combat!=null&&!combatBusy&&!choicePresented&&dragView==null);
        private void OpenRouteInspection()
        {
            if(routeInspectionOpen||!CanInspectRoute)return;
            routeSavedScroll=mapScroll;routeSavedFocus=mapFocusFloor;routeSavedPause=mapPauseOpen;
            mapPauseOpen=false;mapFocusFloor=-1;routeInspectionOpen=true;
            GildedFate.Audio.GameAudio.SetCombatPaused(true);GildedFate.Audio.AdaptiveMusicDirector.SetPaused(true);
        }
        private void CloseRouteInspection()
        {
            if(!routeInspectionOpen)return;
            routeInspectionOpen=false;mapScroll=routeSavedScroll;mapFocusFloor=routeSavedFocus;mapPauseOpen=routeSavedPause;UpdateAudioPresentation();
        }
        private void UpdateRouteInspectionInput()
        {
            if(captureMode)return;
            HandleRouteInspectionNavigation(ReadMenuNavigation());
        }
        private void HandleRouteInspectionNavigation(MenuNavigation input)
        {
            if(input.back||input.map||input.accept){CloseRouteInspection();return;}
            if(input.Any)controllerNavigation=true;
            if(input.y!=0||input.page!=0){mapFocusFloor=-1;mapScroll=Mathf.Max(0,mapScroll+(input.y!=0?input.y:input.page*3)*140f);}
        }
        private void DrawRouteMapControl(float width,Vector2 pointer)
        {
            var r=RouteMapButton(width);var color=CanInspectRoute?Gold:new Color(.4f,.4f,.4f);
            // Freestanding folded map and a route, using the existing HUD palette.
            var a=new Vector2(r.x+6,r.y+10);var b=new Vector2(r.x+17,r.y+5);var c=new Vector2(r.x+29,r.y+11);var d=new Vector2(r.x+40,r.y+6);
            var down=new Vector2(0,27);
            DrawLine(a,b,color,2);DrawLine(b,c,color,2);DrawLine(c,d,color,2);
            DrawLine(a+down,b+down,color,2);DrawLine(b+down,c+down,color,2);DrawLine(c+down,d+down,color,2);
            foreach(var point in new[]{a,b,c,d})DrawLine(point,point+down,color,1.5f);
            DrawLine(a+new Vector2(4,19),b+new Vector2(5,10),color,2);DrawLine(b+new Vector2(5,10),d+new Vector2(-5,12),color,2);
            if(r.Contains(pointer))SetRunHudTooltip(r,routeInspectionOpen?"CLOSE MAP":"ACT MAP","Inspect your current route. Rooms cannot be entered from this view; return to exactly where you were.");
            var enabled=GUI.enabled;GUI.enabled=enabled&&(routeInspectionOpen||CanInspectRoute);
            if(GUI.Button(r,"",GUIStyle.none)){if(routeInspectionOpen)CloseRouteInspection();else OpenRouteInspection();}
            GUI.enabled=enabled;
        }
        private void DrawRouteInspection(float w,float h)
        {
            DrawFateMap(w,h,true);
            var back=new Rect(w*.5f-154,h-58,308,42);
            DrawButtonFrame(back,back.Contains(PointerPosition),false);
            if(GUI.Button(back,"RETURN · MAP PREVIEW ONLY",buttonStyle))CloseRouteInspection();
            DrawPersistentRunTooltip(w,h);
            if(controllerNavigation)GUI.Label(new Rect(210,112,w-420,28),menuUsesGamepad?"D-pad / Stick  Scroll    LB / RB  Page    A / B  Return":"↑ ↓  Scroll    Q / E  Page    Enter / Esc  Return",footerStyle);
        }
    }
}
