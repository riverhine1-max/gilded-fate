using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private Texture2D identityRooms,identityEnergy,identityHalo,identityEnergyLight,identityMapRing;
        private int identityLastEnergy=-1;
        private float identityEnergyChangedAt;
        private bool identityEnergyGain;

        private void DrawIdentityRoom(int index,Rect r)
        {
            if(!identityRooms)identityRooms=Resources.Load<Texture2D>("Art/Identity/RoomEmblems");
            if(identityRooms)GUI.DrawTextureWithTexCoords(r,identityRooms,new Rect((index%4)*.25f,index<4?.5f:0,.25f,.5f),true);
        }
        private static int IdentityEnergyIndex(CardOrigin origin)=>origin==CardOrigin.Arcane?1:origin==CardOrigin.Reaper?2:0;
        private void DrawIdentityEnergy(Rect r,int index)
        {
            if(!identityEnergy)identityEnergy=Resources.Load<Texture2D>("Art/Identity/EnergyVessels");
            if(identityEnergy)GUI.DrawTextureWithTexCoords(r,identityEnergy,new Rect(index/3f,0,1f/3f,1),true);
        }
        private void DrawIdentityEnergyReaction(Rect r,Color color,float reaction)
        {
            if(reaction<=0||profile.reduceMotion||profile.reduceFlashing||profile.reducedVfx)return;
            if(!identityEnergyLight)
            {
                identityEnergyLight=new Texture2D(96,96,TextureFormat.RGBA32,false){name="Contained Energy light",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color[96*96];
                for(var y=0;y<96;y++)for(var x=0;x<96;x++){var d=Vector2.Distance(new Vector2(x,y),new Vector2(47.5f,47.5f))/48;var alpha=Mathf.Exp(-Mathf.Pow((d-.60f)*7,2))*Mathf.Clamp01((1-d)*6);pixels[y*96+x]=new Color(1,1,1,alpha);}
                identityEnergyLight.SetPixels(pixels);identityEnergyLight.Apply(false,true);
            }
            var size=Mathf.Min(r.width,r.height)*Mathf.Lerp(.38f,.83f,identityEnergyGain?1-reaction:reaction);
            color.a=Mathf.Sin(reaction*Mathf.PI)*.35f;
            DrawCardUiShape(new Rect(r.center.x-size*.5f,r.center.y-size*.5f,size,size),identityEnergyLight,color);
        }
        private void EnsureIdentityHalo()
        {
            if(identityHalo)return;
            const int width=192,height=272;var pixels=new Color[width*height];
            identityHalo=new Texture2D(width,height,TextureFormat.RGBA32,false){name="Feathered card readiness",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)
            {
                // Rounded distance field: a soft light outside the actual silhouette,
                // never an extra opaque frame or a rectangle claiming input.
                var q=new Vector2(Mathf.Abs(x-width*.5f)-70,Mathf.Abs(y-height*.5f)-110);
                var distance=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-7;
                var alpha=distance<0?0:Mathf.Exp(-distance*distance/70f)*.64f;
                pixels[y*width+x]=new Color(1,1,1,alpha);
            }
            identityHalo.SetPixels(pixels);identityHalo.Apply(false,true);
        }
        private void DrawIdentityCardLight(Rect r,CardDef card,bool focused)
        {
            if(combat==null||!combat.hand.Contains(card)||card.instanceId==movingCard||!combat.CanPlay(card))return;
            EnsureIdentityHalo();var ready=CardPreview(card)?.conditionActive==true;
            var color=ready?new Color(.88f,.105f,.17f):new Color(1f,.98f,.89f);
            color.a=focused?.86f:.60f;
            DrawCardUiShape(new Rect(r.x-r.width*.12f,r.y-r.height*.075f,r.width*1.24f,r.height*1.15f),identityHalo,color);
        }
        private static Vector2 FateThreadPoint(Vector2 a,Vector2 b,float t)
        {
            // Fixed endpoints preserve graph ownership; the quiet bend is identical
            // for idle, traveled and currently weaving versions of an edge.
            var bend=Mathf.Clamp((b.x-a.x)*.08f,-12,12);
            return Vector2.Lerp(a,b,t)+new Vector2(Mathf.Sin(t*Mathf.PI)*bend,0);
        }
        private void DrawFateThread(Vector2 a,Vector2 b,Rect clip,bool braided,bool available,bool hot,float progress=1)
        {
            if(Mathf.Max(a.y,b.y)<clip.yMin||Mathf.Min(a.y,b.y)>clip.yMax)return;
            var color=braided?new Color(.77f,.60f,.29f,.80f):available?new Color(.98f,.80f,.43f,.86f):hot?new Color(.77f,.69f,.52f,.68f):new Color(.50f,.46f,.37f,.48f);
            var strands=braided?3:1;var steps=braided?60:10;
            var perpendicular=new Vector2(-(b-a).y,(b-a).x).normalized;
            for(var strand=0;strand<strands;strand++)
            {
                Vector2 At(float t)=>FateThreadPoint(a,b,t)+(braided?perpendicular*(Mathf.Sin(t*Mathf.PI*18+strand*Mathf.PI*2/3)*2.8f*Mathf.Sin(t*Mathf.PI)):Vector2.zero);
                var previous=At(0);
                for(var i=1;i<=steps;i++)
                {
                    var t=Mathf.Min(progress,i/(float)steps);var next=At(t);
                    // A small end overlap prevents sub-pixel gaps between segments.
                    var overlap=(next-previous).normalized*.45f;
                    DrawMapLink(previous-overlap,next+overlap,clip,color,braided?1.25f:available?1.9f:1.4f);previous=next;
                    if(t>=progress)break;
                }
            }
        }
        private void DrawIdentityMapRing(Rect r)
        {
            if(!identityMapRing)
            {
                const int size=192;var pixels=new Color[size*size];
                identityMapRing=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Current room gold ring",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                for(var y=0;y<size;y++)for(var x=0;x<size;x++)
                {
                    var d=Mathf.Abs(Vector2.Distance(new Vector2(x,y),new Vector2(95.5f,95.5f))-89);
                    pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(2.3f-d)*.9f+Mathf.Exp(-d*d/12)*.09f);
                }
                identityMapRing.SetPixels(pixels);identityMapRing.Apply(false,true);
            }
            var diameter=r.width*1.2f;
            DrawCardUiShape(new Rect(r.center.x-diameter*.5f,r.center.y-diameter*.5f,diameter,diameter),identityMapRing,new Color(1,.83f,.40f,.90f));
        }
    }
}
