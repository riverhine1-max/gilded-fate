using System.Collections.Generic;
using UnityEngine;

namespace GildedFate.UI
{
    // Combat vitals polish: gold filigree health frames, damage chip trail, heal shimmer,
    // crystal block emblem + shatter, and gold-dust enemy death dissolve.
    // Hooks: DrawActorHealthBar (DrawVitalsPolish) and DrawCombatStats (DrawVitalsPolishOverlay).
    public partial class GildedMainMenu
    {
        private sealed class VpVitals
        {public float chip=-1,dropAt=-10,healAt=-10;public int prevHp=int.MinValue,prevBlock;}
        private struct VpParticle
        {public Vector2 origin,velocity;public float start,life,size,spin;public bool shard;}
        private readonly Dictionary<int,VpVitals> vpVitals=new();
        private readonly List<VpParticle> vpParticles=new();
        private readonly List<(Rect rect,float start)> vpDeathFlashes=new();
        private readonly Dictionary<int,float> vpGroupDeathSeen=new();
        private object vpCombatRef;private float vpFoeDeathSeen;
        private static Texture2D vpSheenTex,vpGlowTex,vpGlintTex;

        private void VpEnsureTextures()
        {
            if(!vpSheenTex)
            {
                vpSheenTex=new Texture2D(1,32,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                for(var y=0;y<32;y++){var t=y/31f;/* t=0 bottom, 1 top */var a=t>.5f?Mathf.Pow((t-.5f)*2,1.6f)*.55f:(.5f-t)*2*.35f;vpSheenTex.SetPixel(0,y,t>.5f?new Color(1,1,1,a):new Color(0,0,0,a));}
                vpSheenTex.Apply(false,true);
            }
            if(!vpGlowTex)
            {
                vpGlowTex=new Texture2D(32,32,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                for(var y=0;y<32;y++)for(var x=0;x<32;x++){var d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(16,16))/16f;var a=Mathf.Clamp01(1-d);vpGlowTex.SetPixel(x,y,new Color(1,1,1,a*a));}
                vpGlowTex.Apply(false,true);
            }
            if(!vpGlintTex)
            {
                vpGlintTex=new Texture2D(16,1,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                for(var x=0;x<16;x++){var a=Mathf.Sin((x+.5f)/16f*Mathf.PI);vpGlintTex.SetPixel(x,0,new Color(1,1,1,a*a));}
                vpGlintTex.Apply(false,true);
            }
        }

        private void VpResetIfNewCombat()
        {
            if(ReferenceEquals(vpCombatRef,combat))return;
            vpCombatRef=combat;vpVitals.Clear();vpParticles.Clear();vpDeathFlashes.Clear();vpGroupDeathSeen.Clear();vpFoeDeathSeen=foeDeath;
            for(var i=0;i<opponentVisuals.Count;i++)if(opponentVisuals[i].death>=0)vpGroupDeathSeen[i]=opponentVisuals[i].death;
        }

        private void VpTex(Rect r,Texture2D tex,Color c)
        {
            if(r.width<=0||r.height<=0||!tex)return;var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,tex,ScaleMode.StretchToFill,true);GUI.color=old;
        }

        private void VpDiamond(Vector2 c,float rx,float ry,Color col,float width)
        {
            var t=new Vector2(c.x,c.y-ry);var rt=new Vector2(c.x+rx,c.y);var b=new Vector2(c.x,c.y+ry);var l=new Vector2(c.x-rx,c.y);
            DrawLine(t,rt,col,width);DrawLine(rt,b,col,width);DrawLine(b,l,col,width);DrawLine(l,t,col,width);
        }

        // key: -1 player, >=0 enemy index. Called right after the bar fill is drawn.
        private void DrawVitalsPolish(Rect r,int key,int hp,int block,int maxHp,float ratio)
        {
            if(combat==null)return;VpEnsureTextures();VpResetIfNewCombat();
            var now=Time.unscaledTime;var dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            if(!vpVitals.TryGetValue(key,out var s)){s=new VpVitals{chip=ratio,prevHp=hp,prevBlock=block};vpVitals[key]=s;}
            if(s.prevHp==int.MinValue){s.prevHp=hp;s.prevBlock=block;s.chip=ratio;}
            var hitFlag=key<0?heroHit>.05f:GroupCombat?(key<opponentVisuals.Count&&opponentVisuals[key].hit>.05f):foeHit>.05f;
            if(hp<s.prevHp)s.dropAt=now;
            if(hp>s.prevHp)s.healAt=now;
            // Block shatter: block broken to zero while taking a hit.
            var shieldCenter=new Vector2(r.x-(key<0&&!string.IsNullOrEmpty(combat.activeShardId)?80:38)+23,r.center.y);
            if(s.prevBlock>0&&block<=0&&(hp<s.prevHp||hitFlag))VpSpawnShatter(shieldCenter,now);
            s.prevHp=hp;s.prevBlock=block;

            // Damage chip trail: lighter segment holds briefly then drains (~0.5 s).
            if(profile.reduceMotion||ratio>=s.chip)s.chip=ratio;
            else if(now-s.dropAt>.1f){s.chip=Mathf.Lerp(s.chip,ratio,1-Mathf.Exp(-dt*7.5f));if(s.chip-ratio<.002f)s.chip=ratio;}
            var inner=new Rect(r.x+2,r.y+2,r.width-4,r.height-4);
            if(s.chip>ratio)
            {
                var x0=inner.x+inner.width*ratio;var x1=inner.x+inner.width*Mathf.Clamp01(s.chip);
                var fresh=Mathf.Clamp01(1-(now-s.dropAt)*1.6f);
                Fill(new Rect(x0,inner.y+1,x1-x0,inner.height-2),new Color(1f,.9f,.66f,.62f+fresh*.2f));
                Fill(new Rect(x0,inner.y+1,1,inner.height-2),new Color(1f,1f,.9f,.85f));
            }

            // Inner gradient + sheen on filled portion; slow glint when motion allowed.
            var filled=new Rect(inner.x,inner.y,inner.width*ratio,inner.height);
            VpTex(filled,vpSheenTex,new Color(1,1,1,.6f));
            if(!profile.reduceMotion&&filled.width>12)
            {
                var cycle=Mathf.Repeat(now*.24f+(key+1)*.37f,1f);if(cycle<.35f){var gx=filled.x+filled.width*(cycle/.35f)-10;VpTex(new Rect(Mathf.Max(filled.x,gx),filled.y,Mathf.Min(20,filled.xMax-Mathf.Max(filled.x,gx)),filled.height*.55f),vpGlintTex,new Color(1,.96f,.85f,.28f));}
            }

            // Heal shimmer (green-gold).
            var heal=Mathf.Clamp01(1-(now-s.healAt)/.65f);
            if(heal>0&&filled.width>0)
            {
                var a=profile.reduceFlashing?heal*.12f:heal*.3f;
                Fill(filled,new Color(.62f,1f,.45f,a*.6f));
                if(!profile.reduceMotion){var hx=filled.x+filled.width*(1-heal)-12;VpTex(new Rect(hx,filled.y,24,filled.height),vpGlintTex,new Color(1f,.92f,.45f,a*1.8f));}
                Fill(new Rect(filled.x,r.y-4,filled.width,1),new Color(.8f,1f,.55f,a*1.5f));
            }

            // Gold filigree frame: thin double border with diamond end caps.
            var gold=new Color(.96f,.77f,.36f,.92f);var deep=new Color(.55f,.38f,.14f,.9f);
            if(block>0){gold=Color.Lerp(gold,new Color(.62f,.88f,1f,.95f),.35f);}
            var o=new Rect(r.x-4,r.y-4,r.width+8,r.height+8);var m=new Rect(r.x-2,r.y-2,r.width+4,r.height+4);
            Fill(new Rect(o.x+4,o.y,o.width-8,1),gold);Fill(new Rect(o.x+4,o.yMax-1,o.width-8,1),gold);
            Fill(new Rect(m.x+3,m.y,m.width-6,1),deep);Fill(new Rect(m.x+3,m.yMax-1,m.width-6,1),deep);
            Fill(new Rect(o.x,o.y+4,1,o.height-8),gold);Fill(new Rect(o.xMax-1,o.y+4,1,o.height-8),gold);
            DrawLine(new Vector2(o.x+4,o.y),new Vector2(o.x,o.y+4),gold,1);DrawLine(new Vector2(o.xMax-4,o.y),new Vector2(o.xMax,o.y+4),gold,1);
            DrawLine(new Vector2(o.x,o.yMax-4),new Vector2(o.x+4,o.yMax),gold,1);DrawLine(new Vector2(o.xMax,o.yMax-4),new Vector2(o.xMax-4,o.yMax),gold,1);
            var cy=r.center.y;
            VpDiamond(new Vector2(o.x-4,cy),4,6,gold,1.4f);Fill(new Rect(o.x-5,cy-1,2,2),gold);
            VpDiamond(new Vector2(o.xMax+4,cy),4,6,gold,1.4f);Fill(new Rect(o.xMax+3,cy-1,2,2),gold);
            VpDiamond(new Vector2(r.center.x,o.y),3,2.5f,gold,1.2f);VpDiamond(new Vector2(r.center.x,o.yMax),3,2.5f,gold,1.2f);

            // Blue crystal block emblem behind the existing shield number.
            if(block>0)
            {
                var pulse=profile.reduceMotion?0:Mathf.Sin(now*2.4f+key)*.5f+.5f;var c=shieldCenter;
                VpTex(new Rect(c.x-40,c.y-40,80,80),vpGlowTex,new Color(.35f,.72f,1f,.38f+pulse*.12f));
                VpTex(new Rect(r.x-6,r.y-8,r.width+12,r.height+16),vpGlowTex,new Color(.3f,.65f,1f,.10f+pulse*.05f));
                var edge=new Color(.62f,.9f,1f,.9f);var facet=new Color(.55f,.85f,1f,.45f);
                VpDiamond(c,26,31,edge,1.6f);VpDiamond(c,17,21,facet,1f);
                DrawLine(new Vector2(c.x,c.y-31),new Vector2(c.x,c.y-21),facet,1);DrawLine(new Vector2(c.x,c.y+31),new Vector2(c.x,c.y+21),facet,1);
                DrawLine(new Vector2(c.x-26,c.y),new Vector2(c.x-17,c.y),facet,1);DrawLine(new Vector2(c.x+26,c.y),new Vector2(c.x+17,c.y),facet,1);
                if(!profile.reduceMotion&&!profile.reduceFlashing){var sp=Mathf.Repeat(now*.5f+key*.3f,1f);if(sp<.2f){var k=sp/.2f;var p=Vector2.Lerp(new Vector2(c.x-13,c.y-15),new Vector2(c.x+13,c.y+15),k);VpTex(new Rect(p.x-4,p.y-4,8,8),vpGlowTex,new Color(.9f,.98f,1f,Mathf.Sin(k*Mathf.PI)*.8f));}}
            }
        }

        private void VpSpawnShatter(Vector2 c,float now)
        {
            var count=profile.reducedVfx?6:10;
            for(var i=0;i<count;i++)
            {
                var ang=(i/(float)count)*Mathf.PI*2+Random.Range(-.25f,.25f);var dir=new Vector2(Mathf.Cos(ang),Mathf.Sin(ang));
                vpParticles.Add(new VpParticle{shard=true,origin=c+dir*8,velocity=dir*Random.Range(70f,130f)+new Vector2(0,-20),start=now,life=Random.Range(.5f,.75f),size=Random.Range(4f,7f),spin=Random.Range(-9f,9f)});
            }
        }

        private void VpSpawnDeath(Rect portrait,float now)
        {
            if(portrait.width<=0)return;var count=profile.reducedVfx?14:34;
            for(var i=0;i<count;i++)
            {
                var p=new Vector2(Random.Range(portrait.x+portrait.width*.12f,portrait.xMax-portrait.width*.12f),Random.Range(portrait.y+portrait.height*.2f,portrait.yMax-portrait.height*.05f));
                vpParticles.Add(new VpParticle{shard=false,origin=p,velocity=new Vector2(Random.Range(-14f,14f),-Random.Range(45f,95f)),start=now+Random.Range(0f,.12f),life=Random.Range(.6f,.8f),size=Random.Range(3f,7f),spin=Random.Range(0f,6.28f)});
            }
            vpDeathFlashes.Add((portrait,now));
        }

        // Called once per frame from DrawCombatStats (after actors are drawn).
        private void DrawVitalsPolishOverlay()
        {
            if(combat==null)return;VpEnsureTextures();VpResetIfNewCombat();var now=Time.unscaledTime;
            if(GroupCombat)
            {
                for(var i=0;i<opponentVisuals.Count&&i<combat.EnemyCount;i++)
                {var d=opponentVisuals[i].death;if(d>=0&&(!vpGroupDeathSeen.TryGetValue(i,out var seen)||seen!=d)){vpGroupDeathSeen[i]=d;VpSpawnDeath(GroupPresentedPortrait(i),now);}}
            }
            else if(foeDeath>0&&foeDeath!=vpFoeDeathSeen){vpFoeDeathSeen=foeDeath;VpSpawnDeath(EnemyPortraitRect,now);}
            else if(foeDeath<=0)vpFoeDeathSeen=0;

            for(var i=vpDeathFlashes.Count-1;i>=0;i--)
            {
                var (rect,start)=vpDeathFlashes[i];var t=now-start;if(t>.45f){vpDeathFlashes.RemoveAt(i);continue;}
                var a=(1-t/.45f)*(profile.reduceFlashing?.25f:.85f);var g=new Color(1f,.82f,.4f,a);var e=profile.reduceMotion?0:t*14;
                var fr=new Rect(rect.x-e,rect.y-e,rect.width+e*2,rect.height+e*2);
                Fill(new Rect(fr.x,fr.y,fr.width,2),g);Fill(new Rect(fr.x,fr.yMax-2,fr.width,2),g);Fill(new Rect(fr.x,fr.y,2,fr.height),g);Fill(new Rect(fr.xMax-2,fr.y,2,fr.height),g);
                if(!profile.reduceFlashing)VpTex(new Rect(rect.center.x-rect.width*.6f,rect.center.y-rect.height*.6f,rect.width*1.2f,rect.height*1.2f),vpGlowTex,new Color(1f,.78f,.35f,a*.35f));
            }

            for(var i=vpParticles.Count-1;i>=0;i--)
            {
                var p=vpParticles[i];var t=now-p.start;if(t>p.life){vpParticles.RemoveAt(i);continue;}if(t<0)continue;
                var k=t/p.life;var move=profile.reduceMotion?0:1;
                if(p.shard)
                {
                    var pos=p.origin+(p.velocity*t+new Vector2(0,120*t*t))*move;var a=1-k;var ang=p.spin*t*move+p.size;
                    var dir=new Vector2(Mathf.Cos(ang),Mathf.Sin(ang))*p.size;var perp=new Vector2(-dir.y,dir.x)*.4f;
                    var col=new Color(.6f,.88f,1f,a*.95f);
                    DrawLine(pos-dir,pos+perp,col,1.6f);DrawLine(pos+perp,pos+dir,col,1.6f);DrawLine(pos-dir,pos-perp,new Color(.85f,.97f,1f,a*.8f),1.2f);
                }
                else
                {
                    var sway=Mathf.Sin(p.spin+t*5)*6*move;var pos=p.origin+(p.velocity*t)*move+new Vector2(sway,0);
                    var a=Mathf.Sin(Mathf.Clamp01(k*1.15f)*Mathf.PI)*(1-k*.4f);var sz=p.size*(1-k*.5f);
                    VpTex(new Rect(pos.x-sz,pos.y-sz,sz*2,sz*2),vpGlowTex,new Color(1f,.8f,.36f,a*.9f));
                    if(!profile.reducedVfx)Fill(new Rect(pos.x-.75f,pos.y-.75f,1.5f,1.5f),new Color(1f,.95f,.7f,a));
                }
            }
        }
    }
}
