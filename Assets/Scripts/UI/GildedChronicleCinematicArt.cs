using GildedFate.Chronicle;
using GildedFate.ChronicleBook;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        ChronicleLivingArt chronicleLivingArt;
        ChronicleLivingArt LivingArt => chronicleLivingArt ??= new ChronicleLivingArt();
        bool ChronicleArtReducedMotion => profile != null && profile.reduceMotion;
        float ChronicleArtClock => ChronicleArtReducedMotion ? 0f : Time.realtimeSinceStartup;

        internal static string ChronicleEnvironmentArt(string backdrop)
        {
            switch (backdrop)
            {
                case "kingdom": case "gate": case "field": case "migration": case "plane": case "dawn": case "fading": case "battle": return "Kingdom";
                case "campfire": case "restored": return "Campfire";
                case "chamber": case "sanctuary": case "passage": case "observatory": case "page": case "journal": return "Sanctum";
                default: return "FracturedWeave";
            }
        }
        static bool ChroniclePaletteMatches(ChronicleColor a, ChronicleColor b) => Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)<.015f;
        internal static string ChronicleCharacterArt(ChronicleActor actor)
        {
            if (ChroniclePaletteMatches(actor.color, ChronicleColor.Vanguard)) return "Art/Characters/vanguard";
            if (ChroniclePaletteMatches(actor.color, ChronicleColor.Hexer)) return "Art/Characters/hexer";
            if (ChroniclePaletteMatches(actor.color, ChronicleColor.Reaper)) return "Art/Characters/reaper";
            return "Chronicle/Illustrations/IlluminatedTraveler";
        }
        Color ChronicleArtAccent(ChronicleActor actor) => ChroniclePaletteMatches(actor.color,ChronicleColor.Vanguard) ? new Color(1,.58f,.20f)
            : ChroniclePaletteMatches(actor.color,ChronicleColor.Hexer) ? new Color(.68f,.38f,1)
            : ChroniclePaletteMatches(actor.color,ChronicleColor.Reaper) ? new Color(.18f,.88f,.78f) : new Color(.9f,.74f,.38f);

        void ChronicleTexture(Rect rect, Texture texture, Color color, ScaleMode scale = ScaleMode.StretchToFill)
        {
            if (!texture || Event.current.type != EventType.Repaint) return;
            var saved=GUI.color;
            try { GUI.color=color; GUI.DrawTexture(rect,texture,scale,true); }
            finally { GUI.color=saved; }
        }
        bool DrawChronicleCinematicBackdrop(Rect r, ChronicleIllustrationState ill, float alpha)
        {
            LivingArt.BeginIllustration();
            var texture=LivingArt.Load("Chronicle/Illustrations/Cinematic/"+ChronicleEnvironmentArt(ill.backdrop));
            if (!texture) return false;
            var accent=ill.backdrop=="campfire"||ill.backdrop=="restored" ? new Color(.65f,.47f,.28f) : new Color(.44f,.67f,.59f);
            var image=LivingArt.Render(texture,ChronicleArtClock,accent,0,Mathf.Clamp01(ill.alpha));
            ChronicleTexture(r,image,new Color(1,1,1,alpha),ScaleMode.ScaleAndCrop);
            if(ill.backdrop=="battle"||ill.backdrop=="collapse") Fill(r,new Color(.26f,.035f,.018f,alpha*.16f));
            if(ill.backdrop=="fading") Fill(r,new Color(.09f,.12f,.18f,alpha*.24f));
            return true;
        }

        bool DrawChronicleCinematicFigure(Rect r, Rect original, Color color, ChronicleActor actor, bool shadow=false)
        {
            if(Event.current.type!=EventType.Repaint) return true;
            var path=shadow ? "Chronicle/Illustrations/IlluminatedTraveler" : ChronicleCharacterArt(actor);
            var source=LivingArt.Load(path);if(!source) return false;
            var hero=path.StartsWith("Art/Characters/");
            var accent=ChronicleArtAccent(actor);
            var height=original.height*(hero ? 1.50f : 1.12f)*Mathf.Max(.1f,actor.scale);
            var width=height*source.width/source.height;
            // Actor movement and foot placement remain authoritative. Enlarging the painted figure reveals its costume details.
            var box=new Rect(original.center.x-width*.5f,original.yMax-height,width,height);
            var time=ChronicleArtClock;
            if(!ChronicleArtReducedMotion) box.y+=Mathf.Sin(time*.8f+actor.x*8)*r.height*.0012f;
            ShardSoft(new Rect(box.center.x-width*.27f,box.yMax-r.height*.006f,width*.54f,r.height*.026f),new Color(.01f,.016f,.022f,color.a*.70f));
            if(hero)
            {
                var halo=new Rect(box.x-width*.16f,box.y+height*.22f,width*1.32f,height*.73f);
                ShardSoft(halo,new Color(accent.r,accent.g,accent.b,color.a*.105f));
                // Small drifting motes carry the class palette without hiding the painted face or costume.
                for(var i=0;i<10;i++)
                {
                    var phase=Mathf.Repeat(i*.137f+time*.025f,1);
                    var x=box.center.x+Mathf.Sin(i*2.31f+time*.12f)*width*.47f;
                    var y=box.yMax-phase*height*.80f;
                    var opacity=Mathf.Sin(phase*Mathf.PI)*color.a*.42f;
                    ShardSoft(new Vector2(x,y),Mathf.Max(4,r.width*.011f),new Color(accent.r,accent.g,accent.b,opacity));
                }
            }
            var art=LivingArt.Render(source,time,accent,shadow?2:1);
            var tint=shadow ? new Color(.64f,.66f,.82f,color.a) : hero ? new Color(1,1,1,color.a) : new Color(.87f,.86f,.77f,color.a);
            ChronicleTexture(box,art,tint);
            return true;
        }

        bool DrawChronicleCinematicProp(Rect r, Rect box, ChronicleActor actor, float alpha)
        {
            if(actor.shape==ChronicleShape.Castle)
            {
                var source=LivingArt.Load("Chronicle/Illustrations/IlluminatedCastle");if(!source)return false;
                var height=Mathf.Min(box.height,box.width*source.height/source.width);
                var grounded=new Rect(box.center.x-height*source.width/source.height*.5f,r.height*.83f-height,height*source.width/source.height,height);
                ShardSoft(new Rect(grounded.x-grounded.width*.10f,grounded.yMax-height*.03f,grounded.width*1.2f,height*.08f),new Color(.035f,.05f,.045f,alpha*.65f));
                DrawChronicleCastle(grounded,alpha,actor.rotation);
                ShardSoft(new Rect(grounded.x-height*.12f,grounded.yMax-height*.055f,grounded.width+height*.24f,height*.10f),new Color(.40f,.49f,.43f,alpha*.14f));
                return true;
            }
            if(actor.shape==ChronicleShape.Page)
            {
                var paper=LivingArt.Load("Chronicle/Textures/Parchment/CHR_Weathered_BaseColor");
                if(!paper) return false;
                ShardSoft(new Rect(box.x+box.width*.04f,box.y+box.height*.04f,box.width,box.height),new Color(0,0,0,alpha*.5f));
                if(Event.current.type==EventType.Repaint)
                {
                    // The book texture contains both pages. A portrait story prop uses one page, with no spine under the ending.
                    var saved=GUI.color;
                    try{GUI.color=new Color(.86f,.82f,.70f,alpha);GUI.DrawTextureWithTexCoords(box,paper,new Rect(0,0,.5f,1),true);}
                    finally{GUI.color=saved;}
                }
                Outline(box,new Color(.65f,.46f,.22f,alpha*.85f),2);
                var inset=new Rect(box.x+box.width*.075f,box.y+box.height*.07f,box.width*.85f,box.height*.86f);
                Outline(inset,new Color(.34f,.21f,.11f,alpha*.48f),1);
                for(var i=0;i<13;i++)
                {
                    var length=box.width*(.42f+((i*37)%7)*.045f);
                    var y=box.y+box.height*(.18f+i*.050f);
                    ChronicleArtLine(new Vector2(box.x+box.width*.14f,y),new Vector2(box.x+box.width*.14f+length,y+box.height*.003f),new Color(.22f,.15f,.085f,alpha*.33f),1);
                }
                return true;
            }
            if(actor.shape==ChronicleShape.Shadow)
            {
                if(actor.h<.10f)
                {
                    ShardSoft(new Rect(box.x,box.y,box.width,box.height),new Color(.01f,.016f,.024f,alpha*.80f));return true;
                }
                return DrawChronicleCinematicFigure(r,box,new Color(1,1,1,alpha),actor,true);
            }
            if(actor.shape==ChronicleShape.Circle && actor.id=="fire")
            {
                // A layered candle-like flame and heat halo replace the static orange disc.
                var center=box.center;var clock=ChronicleArtClock;
                ShardSoft(center,box.width*3.1f,new Color(1,.35f,.05f,alpha*.30f));
                for(var flame=0;flame<7;flame++)
                {
                    var height=box.height*(1.2f+Mathf.Sin(clock*1.8f+flame*2.3f)*.13f);
                    var x=center.x+Mathf.Sin(flame*2.1f+clock*.7f)*box.width*.27f;
                    var color=Color.Lerp(new Color(1,.18f,.03f),new Color(1,.89f,.39f),flame/6f);color.a=alpha*.55f;
                    ShardSoft(new Rect(x-box.width*.16f,center.y-height,box.width*.32f,height*1.2f),color);
                }
                return true;
            }
            if(actor.shape==ChronicleShape.Circle || actor.shape==ChronicleShape.Mote || actor.shape==ChronicleShape.Clock || actor.shape==ChronicleShape.Eye || actor.shape==ChronicleShape.Glyph)
            {
                if(actor.shape==ChronicleShape.Glyph&&!string.IsNullOrEmpty(actor.label))
                {
                    var style=new GUIStyle(ChronicleStyle(Mathf.RoundToInt(Mathf.Clamp(r.width*.056f,18,32)),true)){alignment=TextAnchor.MiddleCenter};
                    SetChronicleTextInk(style,ChronCol(actor.color,alpha));
                    GUI.Label(new Rect(box.center.x-r.width*.28f,box.center.y-r.height*.035f,r.width*.56f,r.height*.075f),actor.label,style);return true;
                }
                var mode=actor.shape==ChronicleShape.Eye?8:actor.shape==ChronicleShape.Clock?9:actor.shape==ChronicleShape.Glyph?10:7;
                var color=ChronCol(actor.color,1);
                if(mode==9)color=Color.Lerp(color,new Color(.95f,.73f,.34f),.55f);
                var image=LivingArt.RenderArtifact(mode,ChronicleArtClock,color);
                if(!image)return Event.current.type!=EventType.Repaint;
                // Story actor coordinates are normalized to a portrait page. Round props must use equal pixel dimensions.
                var size=Mathf.Sqrt(box.width*box.height)*(mode==9?1.25f:1.5f);
                var dimensions=mode==8?new Vector2(box.width*1.35f,box.height*2.4f):new Vector2(size,size);
                var detail=new Rect(box.center.x-dimensions.x*.5f,box.center.y-dimensions.y*.5f,dimensions.x,dimensions.y);
                ChronicleTexture(detail,image,new Color(1,1,1,alpha));
                return true;
            }
            if(actor.shape==ChronicleShape.Rect && actor.color.g>actor.color.r*1.8f)
            {
                ShardSoft(new Rect(box.x-box.width*2,box.y,box.width*5,box.height),new Color(.15f,.72f,.48f,alpha*.20f));
                Fill(box,new Color(.45f,.91f,.74f,alpha*.60f));return true;
            }
            return false;
        }

        void DrawChronicleCinematicAtmosphere(Rect r,ChronicleIllustrationState ill,float alpha,bool foreground)
        {
            if(Event.current.type!=EventType.Repaint) return;
            var time=ChronicleArtClock;var camp=ill.backdrop=="campfire"||ill.backdrop=="restored";
            var violent=ill.backdrop=="battle"||ill.backdrop=="collapse";
            var count=foreground?18:24;
            for(var i=0;i<count;i++)
            {
                var phase=Mathf.Repeat(i*.1379f+time*(foreground?.013f:.005f),1);
                var x=Mathf.Repeat(i*.38197f+Mathf.Sin(time*.07f+i)*.023f,1)*r.width;
                var y=(1-phase)*r.height;
                if(camp||violent){x=r.width*(.5f+Mathf.Sin(i*2.18f+time*.13f)*(.08f+phase*.25f));y=r.height*(.79f-phase*.48f);}
                var size=r.width*(foreground?.010f:.006f)*(i%4==0?1.6f:1);
                var c=camp||violent?new Color(1,.58f,.15f):new Color(.83f,.89f,.72f);
                c.a=alpha*Mathf.Sin(phase*Mathf.PI)*(foreground?.29f:.16f);
                ShardSoft(new Vector2(x,y),size,c);
            }
            if(foreground)
            {
                // An atmospheric veil integrates the feet with the landscape and separates the foreground from distant architecture.
                var fog=camp?new Color(.20f,.26f,.43f,alpha*.065f):new Color(.36f,.50f,.45f,alpha*.075f);
                for(var layer=0;layer<3;layer++)
                {
                    var x=r.width*(.15f+layer*.36f+Mathf.Sin(time*.05f+layer)*.03f);
                    ShardSoft(new Rect(x-r.width*.32f,r.height*(.70f+layer*.075f),r.width*.67f,r.height*.15f),fog);
                }
            }
        }

        bool DrawChronicleCinematicEffect(Rect r,ChronicleIllustrationState ill,ChronicleAnimation animation,float alpha)
        {
            var mode=0;
            switch(animation.name)
            {
                case "threads":case "branch":mode=1;break;
                case "fracture":case "shatter_world":case "crack_pass":case "split_screen":mode=2;break;
                case "sigil_glow":mode=3;break;
                case "flash":case "clash":case "magic_burst":mode=4;break;
                case "pulse":case "expand_circle":case "echo_ring":mode=5;break;
                case "overlap":mode=6;break;
                case "breathe":case "shake":return true;
                default:return false;
            }
            var actor=ill.Find(animation.target);
            var focus=actor==null?new Vector2(.5f,.5f):new Vector2(actor.x,actor.y);
            var accent=actor==null?new Color(.95f,.74f,.32f):ChronicleArtAccent(actor);
            var intensity=Mathf.Clamp(animation.intensity,.1f,1.3f);
            if(profile!=null&&profile.reduceFlashing&&(mode==4||mode==2))intensity*=.30f;
            var image=LivingArt.RenderEffect(mode,animation.Progress,ChronicleArtClock,intensity,focus,accent);
            if(!image) return Event.current.type!=EventType.Repaint;
            ChronicleTexture(r,image,new Color(1,1,1,alpha));return true;
        }
    }
}
