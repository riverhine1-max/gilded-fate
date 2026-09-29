using UnityEngine;

namespace GildedFate.UI
{
    // Storybook + run-end polish. Event: page-turn reveal sweep when an event opens,
    // faint drifting dust over the illustration, gold thread underline on hovered choices.
    // Run result: victory crown glow burst + falling gold motes; defeat title crack + glass shards.
    // Soft texture generated once (HideAndDontSave). Honors reduceMotion / reducedVfx / reduceFlashing.
    public sealed partial class GildedMainMenu
    {
        private static Texture2D storyEndSoftTex;
        private object storyEndEventKey;
        private float storyEndEventStart=-10f,storyEndEventLast=-10f,storyEndResultStart=-10f,storyEndResultLast=-10f;

        private static Texture2D StoryEndSoft
        {
            get
            {
                if(storyEndSoftTex!=null)return storyEndSoftTex;
                const int n=64;var t=new Texture2D(n,n,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                var px=new Color[n*n];
                for(var y=0;y<n;y++)for(var x=0;x<n;x++){var dx=(x+.5f)/n*2-1;var dy=(y+.5f)/n*2-1;var a=Mathf.Clamp01(1-Mathf.Sqrt(dx*dx+dy*dy));px[y*n+x]=new Color(1,1,1,a*a);}
                t.SetPixels(px);t.Apply(false,true);storyEndSoftTex=t;return t;
            }
        }

        private static float StoryEndHash(int i,int salt){var v=Mathf.Sin(i*127.1f+salt*311.7f)*43758.5453f;return v-Mathf.Floor(v);}

        private void StoryEndDot(Vector2 c,float r,Color col){var prev=GUI.color;GUI.color=col;GUI.DrawTexture(new Rect(c.x-r,c.y-r,r*2,r*2),StoryEndSoft);GUI.color=prev;}

        private void StoryEndLine(Vector2 a,Vector2 b,float thick,Color col)
        {
            var steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/Mathf.Max(1f,thick*.8f)));
            for(var i=0;i<=steps;i++){var p=Vector2.Lerp(a,b,i/(float)steps);Fill(new Rect(p.x-thick*.5f,p.y-thick*.5f,thick,thick),col);}
        }

        // Hook: end of Event story drawing (before choices).
        private void StoryEndEventPolish(Rect scene)
        {
            var now=Time.unscaledTime;
            if(!ReferenceEquals(storyEndEventKey,currentEvent)||now-storyEndEventLast>.5f){storyEndEventKey=currentEvent;storyEndEventStart=now;}
            storyEndEventLast=now;
            if(profile==null)return;
            var art=new Rect(scene.x,scene.y,scene.width,scene.height*.63f);
            if(!profile.reduceMotion)
            {
                var count=profile.reducedVfx?6:14;
                for(var i=0;i<count;i++)
                {
                    var sp=.012f+StoryEndHash(i,2)*.02f;var fy=1f-Mathf.Repeat(StoryEndHash(i,1)+now*sp,1f);
                    var fx=Mathf.Repeat(StoryEndHash(i,3)+Mathf.Sin(now*.35f+i)*.03f+now*.006f,1f);
                    var edge=Mathf.Clamp01(Mathf.Min(fy,1-fy)*6f);var tw=.55f+.45f*Mathf.Sin(now*(.8f+StoryEndHash(i,4))+i*2.1f);
                    StoryEndDot(new Vector2(art.x+fx*art.width,art.y+fy*art.height),1.6f+StoryEndHash(i,5)*2.4f,new Color(1f,.9f,.7f,.22f*edge*tw));
                }
                var k=Mathf.Clamp01((now-storyEndEventStart)/.6f);
                if(k<1f)
                {
                    var e=1f-(1f-k)*(1f-k);var ex=scene.x+scene.width*e;
                    Fill(new Rect(ex,scene.y,scene.xMax-ex,scene.height),new Color(.035f,.026f,.016f,.94f));
                    for(var b=0;b<10;b++){var bw=4f;var x=ex-(b+1)*bw;if(x<scene.x)break;Fill(new Rect(x,scene.y,bw,scene.height),new Color(1f,.93f,.76f,(profile.reduceFlashing?.16f:.32f)*(1-b/10f)*(1-k*.5f)));}
                    Fill(new Rect(ex,scene.y,2,scene.height),new Color(1f,.96f,.86f,profile.reduceFlashing?.45f:.8f));
                    for(var b=0;b<6;b++)Fill(new Rect(ex+2+b*4,scene.y,4,scene.height),new Color(0,0,0,.28f*(1-b/6f)));
                }
            }
        }

        // Hook: after each event choice is drawn.
        private void StoryEndChoiceThread(Rect rect,int index)
        {
            if(profile==null)return;
            var hot=rect.Contains(PointerPosition)||controllerNavigation&&screenControllerIndex==index;if(!hot)return;
            var y=rect.yMax-5;var x0=rect.x+16;var x1=rect.xMax-16;
            Fill(new Rect(x0,y,x1-x0,1),new Color(.86f,.64f,.28f,.85f));
            Fill(new Rect(x0,y+1,x1-x0,1),new Color(.35f,.24f,.08f,.5f));
            if(!profile.reduceMotion){var p=x0+(x1-x0)*Mathf.Repeat(Time.unscaledTime*.45f,1f);StoryEndDot(new Vector2(p,y+.5f),profile.reducedVfx?5:8,new Color(1f,.86f,.5f,profile.reduceFlashing?.35f:.65f));}
            Fill(new Rect(x0-3,y-1,3,3),new Color(1f,.82f,.42f,.9f));Fill(new Rect(x1,y-1,3,3),new Color(1f,.82f,.42f,.9f));
        }

        // Hook: after the Run Result heading is drawn.
        private void StoryEndRunResultPolish(float w,float h)
        {
            var now=Time.unscaledTime;if(now-storyEndResultLast>.5f)storyEndResultStart=now;storyEndResultLast=now;
            if(profile==null)return;
            var t=now-storyEndResultStart;var top=ShowsPersistentRunHud?68f:30f;var title=new Rect(w*.16f,top,w*.68f,59);var c=title.center;
            if(runResultVictory)
            {
                var burst=profile.reduceMotion?1f:Mathf.Clamp01(t/1.2f);var peak=profile.reduceFlashing?.18f:.42f;
                if(!profile.reduceMotion&&burst<1f)StoryEndDot(c,60+260*burst,new Color(1f,.8f,.36f,peak*(1-burst)));
                var pulse=profile.reduceMotion?.5f:.5f+.5f*Mathf.Sin(now*1.6f);
                StoryEndDot(c,150,new Color(1f,.78f,.32f,(.08f+.05f*pulse)*Mathf.Clamp01(burst*2f)));
                StoryEndDot(c+new Vector2(0,-38),38,new Color(1f,.9f,.55f,.22f+.08f*pulse));
                if(profile.reduceMotion)return;
                var count=profile.reducedVfx?10:26;
                for(var i=0;i<count;i++)
                {
                    var sp=.05f+StoryEndHash(i,7)*.06f;var fy=Mathf.Repeat(StoryEndHash(i,8)+t*sp,1f);var fx=StoryEndHash(i,9)+Mathf.Sin(t*.9f+i)*.012f;
                    var fade=Mathf.Clamp01(fy*8f)*Mathf.Clamp01((1-fy)*4f)*Mathf.Clamp01(t*1.5f);var tw=.6f+.4f*Mathf.Sin(t*3f+i*1.7f);
                    StoryEndDot(new Vector2(fx*w,fy*h),2f+StoryEndHash(i,10)*3f,new Color(1f,.8f,.34f,.7f*fade*tw));
                }
            }
            else
            {
                var grow=profile.reduceMotion?1f:Mathf.Clamp01((t-.15f)/.45f);
                if(grow<=0)return;
                var crackCol=new Color(.82f,.9f,1f,.8f);var dark=new Color(0,0,0,.55f);
                for(var s=0;s<5;s++)
                {
                    var prev=new Vector2(c.x+(StoryEndHash(s,11)-.5f)*title.width*.2f,title.y+4);var segs=5;
                    for(var j=1;j<=segs;j++)
                    {
                        var f=j/(float)segs;if(f>grow+.001f)break;
                        var p=new Vector2(prev.x+(StoryEndHash(s*9+j,12)-.5f)*46f+(s-2)*12f,title.y+4+f*(title.height-6));
                        StoryEndLine(prev+new Vector2(1,1),p+new Vector2(1,1),2f,dark);StoryEndLine(prev,p,1.4f,crackCol);prev=p;
                    }
                }
                if(!profile.reduceFlashing&&!profile.reduceMotion&&t>.15f&&t<.4f)StoryEndDot(c,120,new Color(.85f,.92f,1f,.3f*(1-(t-.15f)/.25f)));
                if(profile.reduceMotion)return;
                var shards=profile.reducedVfx?3:6;var saved=GUI.matrix;
                for(var i=0;i<shards;i++)
                {
                    var st=t-.3f-StoryEndHash(i,13)*.3f;if(st<0||st>2.4f)continue;
                    var p=new Vector2(c.x+(StoryEndHash(i,14)-.5f)*title.width*.5f+(StoryEndHash(i,15)-.5f)*60f*st,title.yMax-6+160f*st*st+20f*st);
                    var a=Mathf.Clamp01(1-st/2.4f);var sz=6f+StoryEndHash(i,16)*10f;
                    var shardAngle=StoryEndHash(i,17)*360f+st*(120f+StoryEndHash(i,18)*240f);GUI.matrix=saved*Matrix4x4.TRS(new Vector3(p.x,p.y,0),Quaternion.Euler(0,0,shardAngle),Vector3.one)*Matrix4x4.TRS(new Vector3(-p.x,-p.y,0),Quaternion.identity,Vector3.one);
                    Fill(new Rect(p.x-sz*.5f,p.y-sz*.2f,sz,sz*.4f),new Color(.78f,.88f,1f,.55f*a));
                    Fill(new Rect(p.x-sz*.5f,p.y-sz*.2f,sz,1),new Color(1f,1f,1f,.8f*a));
                }
                GUI.matrix=saved;
            }
        }
    }
}
