using UnityEngine;

namespace GildedFate.UI
{
    // Merchant hanging threads + coin burst on purchase; Sanctuary procedural flame + heal motes.
    public sealed partial class GildedMainMenu
    {
        private static Texture2D shopRestSoftTexture;
        private int shopRestMerchantGold=int.MinValue;private float shopRestCoinStart=-10;private Vector2 shopRestCoinTarget;
        private int shopRestSanctuaryHp=int.MinValue;private float shopRestMoteStart=-10;

        private static Texture2D ShopRestSoftTexture
        {
            get
            {
                if(shopRestSoftTexture!=null)return shopRestSoftTexture;
                const int size=64;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                var pixels=new Color[size*size];
                for(var y=0;y<size;y++)for(var x=0;x<size;x++){var dx=(x+.5f)/size*2-1;var dy=(y+.5f)/size*2-1;var a=Mathf.Clamp01(1-Mathf.Sqrt(dx*dx+dy*dy));pixels[y*size+x]=new Color(1,1,1,a*a*(3-2*a));}
                tex.SetPixels(pixels);tex.Apply(false,true);shopRestSoftTexture=tex;return tex;
            }
        }

        private void ShopRestSoft(Vector2 center,Vector2 size,Color color)
        {
            if(Event.current.type!=EventType.Repaint)return;
            var old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(center.x-size.x*.5f,center.y-size.y*.5f,size.x,size.y),ShopRestSoftTexture,ScaleMode.StretchToFill,true);GUI.color=old;
        }

        // Hook: DrawPhysicalMerchant, right after Heading.
        private void DrawShopRestMerchantPolish(float w,float h)
        {
            var now=Time.unscaledTime;var motion=!profile.reduceMotion;
            if(shopRestMerchantGold==int.MinValue)shopRestMerchantGold=run.gold;
            if(run.gold<shopRestMerchantGold&&!profile.reducedVfx){shopRestCoinStart=now;shopRestCoinTarget=PointerPosition;}
            shopRestMerchantGold=run.gold;
            if(Event.current.type!=EventType.Repaint)return;
            for(var i=0;i<9;i++)
            {
                var r=i<7?ShopCardRect(i):ShopRelicRect(i-7);
                var sway=motion?Mathf.Sin(now*1.3f+i*.9f)*3.5f:0;
                var top=new Vector2(r.center.x+sway*.3f,0);var bottom=new Vector2(r.center.x+sway,r.y);var mid=Vector2.Lerp(top,bottom,.5f)+new Vector2(sway*.6f,0);
                var c=new Color(1f,.82f,.42f,.32f);DrawLine(top,mid,c,1);DrawLine(mid,bottom,c,1);
            }
            var duration=motion?.85f:.35f;var t=(now-shopRestCoinStart)/duration;
            if(t<0||t>=1)return;
            var from=new Vector2(242,28);var count=profile.reduceMotion?4:10;
            for(var i=0;i<count;i++)
            {
                var k=Mathf.Clamp01(t*1.15f-i*.012f);var e=1-(1-k)*(1-k);
                var spread=new Vector2(Mathf.Sin(i*2.39f)*38,-Mathf.Abs(Mathf.Cos(i*1.7f))*46);
                var p=Vector2.Lerp(from,shopRestCoinTarget,e)+spread*Mathf.Sin(k*Mathf.PI);
                var a=1-Mathf.Clamp01((t-.7f)/.3f);var s=motion?6+Mathf.Abs(Mathf.Sin(now*9+i))*3:7;
                Fill(new Rect(p.x-s*.5f,p.y-3.5f,s,7),new Color(1f,.8f,.3f,a*.95f));Fill(new Rect(p.x-1,p.y-2.5f,2,2),new Color(1f,.97f,.8f,a));
            }
        }

        // Hook: Sanctuary screen, right after Heading.
        private void DrawShopRestSanctuaryPolish(float w,float h)
        {
            var now=Time.unscaledTime;var motion=!profile.reduceMotion;
            if(shopRestSanctuaryHp==int.MinValue)shopRestSanctuaryHp=run.hp;
            if(run.hp>shopRestSanctuaryHp&&!profile.reducedVfx)shopRestMoteStart=now;
            shopRestSanctuaryHp=run.hp;
            if(Event.current.type!=EventType.Repaint)return;
            var baseP=new Vector2(w*.5f,h*.34f+320);
            var pulse=profile.reduceFlashing?.85f:.8f+.2f*Mathf.Sin(now*(motion?2.1f:.6f));
            ShopRestSoft(baseP+Vector2.down*40,new Vector2(260,220)*pulse,new Color(1f,.55f,.18f,.22f));
            for(var i=0;i<4;i++)
            {
                var f=motion?Mathf.PerlinNoise(now*2.4f,i*3.1f):.5f;var fh=(96-i*18)*(.85f+.3f*f);var fw=(46-i*9)*(.9f+.15f*f);
                var lean=motion?(Mathf.PerlinNoise(i*1.7f,now*1.6f)-.5f)*14:0;
                var col=i==0?new Color(1f,.38f,.1f,.55f):i==1?new Color(1f,.6f,.2f,.62f):i==2?new Color(1f,.82f,.42f,.7f):new Color(1f,.96f,.8f,.8f);
                for(var s=0;s<5;s++){var k=s/4f;var c=baseP+new Vector2(lean*k*k,-fh*k*.8f);var sz=new Vector2(fw*(1-k*.75f),fh*.45f*(1-k*.4f));ShopRestSoft(c,sz,col);}
            }
            if(!profile.reducedVfx)
            {
                var embers=motion?12:4;
                for(var i=0;i<embers;i++)
                {
                    var life=Mathf.Repeat((motion?now*.45f:0)+i*.137f,1);var x=baseP.x+Mathf.Sin(i*2.7f+life*4)*(18+life*30);var y=baseP.y-30-life*170;
                    Fill(new Rect(x-1.5f,y-1.5f,3,3),new Color(1f,.7f,.3f,(1-life)*.8f));
                }
                var t=(now-shopRestMoteStart)/(motion?1.6f:.6f);
                if(t>=0&&t<1)for(var i=0;i<18;i++)
                {
                    var x=w*.2f+Mathf.Repeat(i*.618f,1)*w*.6f;var rise=motion?(t*(120+i%5*20)):20;var y=h*.34f+230-rise;
                    ShopRestSoft(new Vector2(x+Mathf.Sin(t*6+i)*6,y),new Vector2(14,14),new Color(.72f,.95f,.45f,(1-t)*.7f));
                }
            }
        }
    }
}
