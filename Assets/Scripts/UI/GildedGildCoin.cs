using UnityEngine;

namespace GildedFate.UI
{
    // Procedural minted-coin art for the GILD medallion. Built once in code, never
    // saved to disk; the draw helpers compose GUI.matrix themselves because
    // GUIUtility.RotateAroundPivot misplaces pivots under the scaled combat matrix.
    public sealed partial class GildedMainMenu
    {
        private static Texture2D gildCoinCrown,gildCoinDouble,gildCoinGlow;
        private const int GildCoinTextureSize=128;

        private static Texture2D GildCoinFace(bool doubled)
        {
            if(doubled){if(!gildCoinDouble)gildCoinDouble=BuildGildCoinTexture(true);return gildCoinDouble;}
            if(!gildCoinCrown)gildCoinCrown=BuildGildCoinTexture(false);
            return gildCoinCrown;
        }

        private static Texture2D GildCoinGlow
        {
            get
            {
                if(gildCoinGlow)return gildCoinGlow;
                const int n=64;var pixels=new Color[n*n];
                for(var y=0;y<n;y++)for(var x=0;x<n;x++)
                {
                    var u=(x+.5f)/n*2-1;var v=(y+.5f)/n*2-1;var a=Mathf.Clamp01(1f-Mathf.Sqrt(u*u+v*v));
                    pixels[y*n+x]=new Color(1f,1f,1f,a*a*(3-2*a)*a);
                }
                gildCoinGlow=NewGildTexture("Gild coin glow",n,pixels);
                return gildCoinGlow;
            }
        }

        // Coin faces keep a mip chain: the same art is drawn from 86 px down to a 12 px glyph.
        private static Texture2D NewGildTexture(string name,int n,Color[] pixels,bool mips=false)
        {
            var texture=new Texture2D(n,n,TextureFormat.RGBA32,mips){name=name,hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels(pixels);texture.Apply(mips,true);
            return texture;
        }

        // Height-field emboss: double rim, beaded groove, hammered field and a raised stamp,
        // lit from the upper left with a radial gold gradient.
        private static Texture2D BuildGildCoinTexture(bool doubled)
        {
            const int n=GildCoinTextureSize;var pixels=new Color[n*n];const float e=1.6f/n;
            var light=new Vector3(-.45f,.55f,.70f).normalized;var half=(light+Vector3.forward).normalized;
            var deep=new Color(.40f,.20f,.035f);var mid=new Color(.90f,.62f,.20f);var pale=new Color(1f,.91f,.60f);
            for(var y=0;y<n;y++)for(var x=0;x<n;x++)
            {
                // Texture rows run bottom-up, so v is up: the crown stands upright on screen.
                var u=(x+.5f)/n*2-1;var v=(y+.5f)/n*2-1;var r=Mathf.Sqrt(u*u+v*v);
                var alpha=Mathf.Clamp01((1f-r)*n*.5f);
                if(alpha<=0){pixels[y*n+x]=new Color(deep.r,deep.g,deep.b,0);continue;}
                var h=GildCoinHeight(u,v,doubled);
                var dx=(GildCoinHeight(u+e,v,doubled)-GildCoinHeight(u-e,v,doubled))/(2*e);
                var dy=(GildCoinHeight(u,v+e,doubled)-GildCoinHeight(u,v-e,doubled))/(2*e);
                var normal=new Vector3(-dx*.085f,-dy*.085f,1f).normalized;
                var diffuse=Mathf.Clamp01(Vector3.Dot(normal,light));
                var spec=Mathf.Pow(Mathf.Clamp01(Vector3.Dot(normal,half)),28f);
                var sweep=Mathf.Clamp01(Vector2.Distance(new Vector2(u,v),new Vector2(-.40f,.44f))/1.75f);
                var baseColor=sweep<.5f?Color.Lerp(pale,mid,sweep*2):Color.Lerp(mid,deep,(sweep-.5f)*2);
                // Recesses (groove, field) sit in soft shadow; the rims and the stamp catch light.
                var occlusion=Mathf.Lerp(.70f,1f,Mathf.Clamp01(h));
                var c=baseColor*(.36f+.84f*diffuse)*occlusion+new Color(1f,.95f,.80f)*(spec*.55f);
                // Darker lip on the outermost pixels reads as the coin's milled edge.
                c=Color.Lerp(c,new Color(.34f,.17f,.03f),Mathf.Clamp01((r-.955f)/.045f)*.7f);
                c.r=Mathf.Clamp01(c.r);c.g=Mathf.Clamp01(c.g);c.b=Mathf.Clamp01(c.b);c.a=alpha;
                pixels[y*n+x]=c;
            }
            return NewGildTexture(doubled?"Gild coin double face":"Gild coin crown face",n,pixels,true);
        }

        private static float GildCoinHeight(float u,float v,bool doubled)
        {
            var r=Mathf.Sqrt(u*u+v*v);
            var outer=GildBand(r,.875f,.985f,.022f);var inner=GildBand(r,.770f,.825f,.016f)*.82f;
            // Beads minted into the groove between the two rims.
            const int beads=36;var step=Mathf.PI*2f/beads;var angle=Mathf.Round(Mathf.Atan2(v,u)/step)*step;
            var bead=1f-GildSmooth(.016f,.028f,Vector2.Distance(new Vector2(u,v),new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.85f));
            var h=Mathf.Max(outer,Mathf.Max(inner,bead*.62f));
            var field=1f-GildSmooth(.72f,.76f,r);
            if(field<=0)return h;
            var hammered=(Mathf.PerlinNoise(u*7.3f+3.1f,v*7.3f+1.7f)-.5f)*.075f+(Mathf.PerlinNoise(u*19f+9.2f,v*19f+4.6f)-.5f)*.03f;
            var stamp=doubled?GildDoubleMask(u,v):GildCrownMask(u,v);
            return Mathf.Max(h,(.32f+.06f*(1-r*r)+hammered+stamp*.52f)*field);
        }

        // Hermite edge like shader smoothstep(e0,e1,x). Mathf.SmoothStep interpolates between its
        // first two arguments instead, which flattened every edge of the minted relief.
        private static float GildSmooth(float e0,float e1,float x){var t=Mathf.Clamp01((x-e0)/(e1-e0));return t*t*(3f-2f*t);}
        private static float GildBand(float r,float a,float b,float w)=>GildSmooth(a-w,a+w,r)*(1f-GildSmooth(b-w,b+w,r));
        private static float GildCover(float distance,float w=.022f)=>1f-GildSmooth(-w,w,distance);

        private static float GildBox(float u,float v,float hx,float hy)
        {
            var qx=Mathf.Abs(u)-hx;var qy=Mathf.Abs(v)-hy;var ox=Mathf.Max(qx,0);var oy=Mathf.Max(qy,0);
            return Mathf.Sqrt(ox*ox+oy*oy)+Mathf.Min(Mathf.Max(qx,qy),0);
        }

        private static float GildSegment(float u,float v,Vector2 a,Vector2 b)
        {
            var p=new Vector2(u,v);var ab=b-a;var t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(1e-6f,ab.sqrMagnitude));
            return Vector2.Distance(p,a+ab*t);
        }

        // Three-pointed crown: a jewelled band, a zigzag body and a ball on each point.
        private static float GildCrownMask(float u,float v)
        {
            var band=GildBox(u,v+.25f,.40f,.075f);
            var s=Mathf.Min(2f,Mathf.Abs(u)/.2f);var top=.02f+Mathf.Abs(s-1f)*.26f+(s<1f?(1f-s)*.06f:0f);
            var body=Mathf.Max(Mathf.Abs(u)-.40f,Mathf.Max(-.19f-v,(v-top)*.6f));
            var balls=Mathf.Min(Vector2.Distance(new Vector2(u,v),new Vector2(0f,.395f))-.058f,
                Mathf.Min(Vector2.Distance(new Vector2(u,v),new Vector2(-.40f,.325f)),Vector2.Distance(new Vector2(u,v),new Vector2(.40f,.325f)))-.05f);
            var crown=GildCover(Mathf.Min(band,Mathf.Min(body,balls)));
            // Gems are struck into the band, and a fine line separates band from body.
            var gems=Mathf.Min(Vector2.Distance(new Vector2(u,v),new Vector2(0,-.25f))-.042f,
                Mathf.Min(Vector2.Distance(new Vector2(u,v),new Vector2(-.23f,-.25f)),Vector2.Distance(new Vector2(u,v),new Vector2(.23f,-.25f)))-.032f);
            var seam=GildCover(Mathf.Max(Mathf.Abs(v+.175f)-.008f,Mathf.Abs(u)-.40f),.012f);
            return Mathf.Clamp01(crown-GildCover(gems,.014f)*.55f-seam*.35f);
        }

        // "×2" struck in the field: two capsules for the ×, a polyline arc and tail for the 2.
        private static float GildDoubleMask(float u,float v)
        {
            var c=new Vector2(-.21f,-.01f);const float arm=.125f;
            var cross=Mathf.Min(GildSegment(u,v,c+new Vector2(-arm,-arm),c+new Vector2(arm,arm)),GildSegment(u,v,c+new Vector2(-arm,arm),c+new Vector2(arm,-arm)))-.048f;
            var arcCenter=new Vector2(.225f,.105f);const float radius=.148f;var two=float.MaxValue;
            var previous=arcCenter+new Vector2(Mathf.Cos(150f*Mathf.Deg2Rad),Mathf.Sin(150f*Mathf.Deg2Rad))*radius;
            for(var i=1;i<=10;i++)
            {
                var a=Mathf.Lerp(150f,-38f,i/10f)*Mathf.Deg2Rad;var point=arcCenter+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                two=Mathf.Min(two,GildSegment(u,v,previous,point));previous=point;
            }
            var foot=new Vector2(.06f,-.275f);
            two=Mathf.Min(two,Mathf.Min(GildSegment(u,v,previous,foot),GildSegment(u,v,foot,new Vector2(.405f,-.275f))))-.052f;
            return GildCover(Mathf.Min(cross,two));
        }

        private void DrawGildSprite(Texture2D texture,Vector2 center,float width,float height,Color tint,float angle=0f)
        {
            if(!texture||width<.05f||height<.05f||tint.a<=.003f)return;
            var matrix=GUI.matrix;var color=GUI.color;
            GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(center.x,center.y,0),Quaternion.Euler(0,0,angle),Vector3.one);
            GUI.color=tint;GUI.DrawTexture(new Rect(-width*.5f,-height*.5f,width,height),texture);
            GUI.color=color;GUI.matrix=matrix;
        }

        // A coin turned about its vertical axis: turn is the visible width fraction (cos of
        // the spin angle); a darker copy slid sideways shows the milled edge while turned.
        private void DrawGildCoin(Vector2 center,float diameter,float turn,bool doubled,Color tint)
        {
            var face=GildCoinFace(doubled);var sx=Mathf.Max(.035f,Mathf.Abs(turn));
            if(sx<.985f)
            {
                var edge=new Color(tint.r*.55f,tint.g*.38f,tint.b*.22f,tint.a);var depth=(1f-sx)*diameter*.07f*(turn<0?-1:1);
                DrawGildSprite(face,center+new Vector2(depth,0),Mathf.Max(diameter*sx,diameter*.07f),diameter,edge);
                DrawGildSprite(face,center+new Vector2(depth*.5f,0),Mathf.Max(diameter*sx,diameter*.07f),diameter,edge);
            }
            DrawGildSprite(face,center,diameter*sx,diameter,tint);
        }
    }
}
