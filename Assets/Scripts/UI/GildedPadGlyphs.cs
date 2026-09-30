using UnityEngine;

namespace GildedFate.UI
{
    // Small Xbox-style button badges, shown only while a controller is the active
    // input: X beside the top bar, Y on the End Turn seal. Kept deliberately sparse.
    public sealed partial class GildedMainMenu
    {
        private Texture2D padGlyphDisc;
        private bool ShowPadGlyphs=>menuUsesGamepad&&UnityEngine.InputSystem.Gamepad.current!=null;
        private bool TopBarUsesX=>ShowsPersistentRunHud&&screen!=ScreenMode.Collection;
        private static Color PadGlyphColor(string button)=>button switch
        {
            "A"=>new Color(.42f,.78f,.30f),"B"=>new Color(.90f,.30f,.26f),
            "X"=>new Color(.40f,.68f,1f),"Y"=>new Color(.98f,.78f,.18f),_=>new Color(.7f,.7f,.72f)
        };
        private void DrawPadGlyph(Vector2 center,string button,bool enabled,float size=22)
        {
            if(!CardVfxRepaint)return;
            if(!padGlyphDisc)padGlyphDisc=BossPolishTexture("Pad glyph disc",64,d=>(1-d)*28f);
            var c=PadGlyphColor(button);var alpha=enabled?1f:.45f;var r=new Rect(center.x-size*.5f,center.y-size*.5f,size,size);
            var old=GUI.color;
            GUI.color=new Color(0,0,0,.6f*alpha);GUI.DrawTexture(new Rect(r.x-2,r.y-1,r.width+4,r.height+4),padGlyphDisc);
            GUI.color=new Color(.08f,.08f,.09f,alpha);GUI.DrawTexture(r,padGlyphDisc);
            GUI.color=new Color(c.r,c.g,c.b,.5f*alpha);GUI.DrawTexture(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),padGlyphDisc);
            GUI.color=old;
            GUI.Label(r,button,new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=Mathf.RoundToInt(size*.62f),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(c.r,c.g,c.b,alpha)}});
        }
    }
}
