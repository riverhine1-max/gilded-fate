Shader "Hidden/GildedFate/RunStartVeil"
{
    Properties { _MainTex("Texture",2D)="white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float _Phase, _Hero, _Aspect, _Restraint;
            float hash21(float2 p){p=frac(p*float2(123.34,456.21));p+=dot(p,p+45.32);return frac(p.x*p.y);}
            float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash21(a),hash21(a+float2(1,0)),f.x),lerp(hash21(a+float2(0,1)),hash21(a+1),f.x),f.y);}
            float fbm(float2 p){float n=0,w=.5;for(int k=0;k<4;k++){n+=noise(p)*w;p=mul(float2x2(.8,-.6,.6,.8),p)*2.06+7.3;w*=.5;}return n;}
            float ease(float a,float b,float t){return smoothstep(a,b,t);}
            float segmentDistance(float2 p,float2 a,float2 b){float2 v=b-a;return length(p-a-v*saturate(dot(p-a,v)/max(dot(v,v),.00001)));}
            float ring(float r,float radius,float width){return exp(-abs(r-radius)/width);}
            float3 gold(){return float3(1,.63,.20);}
            float4 blade(float2 uv,float t)
            {
                float2 p=float2((uv.x-.5)*_Aspect,uv.y-.5);
                float2 axis=normalize(float2(_Aspect,-.58));float2 normal=float2(-axis.y,axis.x);
                float along=dot(p,axis),cross=dot(p,normal);
                float halfSpan=length(float2(_Aspect,.58))*.64;
                float head=lerp(-halfSpan,halfSpan,ease(.015,.32,t));
                float gate=1-smoothstep(head-.025,head+.02,along);
                float taper=pow(saturate(1-pow(along/(halfSpan*1.12),2)),.65);
                float opening=ease(.29,.81,t)*1.35;
                float split=opening*taper;
                float turbulence=fbm(float2(along*21,cross*6-t*3));
                float fracture=(turbulence-.48)*.02*ease(.20,.42,t);
                float edge=abs(cross+fracture)-split;
                float black=(1-smoothstep(-.005,.003,edge))*gate;
                float hot=exp(-abs(edge)/.0024)*gate;
                float crimson=exp(-abs(edge)/.024)*gate*(.4+turbulence);
                float haze=exp(-abs(edge)/.075)*gate*.3;
                float scar=exp(-abs(edge-.012-(noise(float2(along*85,t))-.5)*.008)/.0012)*gate;
                float decay=1-ease(.59,.84,t);
                // A short chased-metal highlight gives the cut a blade-like leading
                // edge, while the red heat falls away behind it rather than blooming.
                float chase=exp(-pow((along-(head-.14))/.19,2));
                float polish=.72+chase*.28;
                float3 light=(float3(.78,.012,.035)*crimson*.82+gold()*(hot*polish+scar*.22)+float3(.44,.008,.02)*haze*.65)*decay;
                // Tiny irregular gold splinters track the fracture, not generic confetti.
                float cell=floor((along+halfSpan)*73);float seed=hash21(float2(cell,4));
                float sparkAxis=frac((along+halfSpan)*73);
                float spark=exp(-abs(edge-(.02+seed*.075)*ease(.19,.57,t))/.0016);
                spark*=step(.78,seed)*smoothstep(.0,.20,sparkAxis)*(1-smoothstep(.36,.74,sparkAxis))*gate;
                light+=gold()*spark*decay*.52;
                float tip=exp(-length(float2((along-head)*.45,cross))/.018)*(1-ease(.29,.39,t));
                light+=float3(1,.84,.48)*tip*1.2;
                float alpha=max(black,saturate(max(light.r,max(light.g,light.b))));
                light*=lerp(1,.35,_Restraint);
                return float4(light/max(alpha,.001),alpha);
            }
            float4 ritual(float2 uv,float t)
            {
                float2 p=float2((uv.x-.5)*_Aspect,uv.y-.5);
                float2 center=float2(0,-.02);float converge=ease(.46,.71,t);
                float3 light=0;float opacity=0;
                for(int seal=0;seal<3;seal++)
                {
                    float enter=ease(.02+seal*.085,.15+seal*.085,t);
                    float2 origin=float2((seal-1)*_Aspect*.235,seal==1?-.16:.07);
                    float2 at=lerp(origin,center,converge);
                    float2 q=p-at;float r=length(q);float angle=atan2(q.y,q.x);
                    float radius=.185*lerp(.74,1,enter)*lerp(1,.15,converge);
                    float rotation=t*(seal==1?-.8:.65)*(1-_Restraint);
                    float theta=angle+rotation;
                    float segments=smoothstep(.06,.15,abs(sin(theta*6)));
                    float sweepAngle=frac(theta/6.2831853+1);
                    float inscription=1-smoothstep(enter,enter+.075,sweepAngle);
                    float lockIn=exp(-pow((enter-.88)/.13,2));
                    float outer=ring(r,radius,.0018)*segments*inscription;
                    float inner=ring(r,radius*.84,.0008)+ring(r,radius*1.10,.0009)*.35;
                    // Radial engraved glyphs: alternating hooks, stems and slanted cuts.
                    float glyphSector=frac(theta/6.2831853*24+.5);
                    float glyphBand=abs(r-radius*.96);
                    float glyph=exp(-abs(glyphSector-.30)/.035)*step(glyphBand,radius*.045);
                    glyph+=exp(-abs(glyphBand/radius-(glyphSector-.25)*.12)/.008)*step(.2,glyphSector)*step(glyphSector,.58)*step(glyphBand,radius*.05);
                    float triangles=0;
                    for(int corner=0;corner<3;corner++)
                    {
                        float a=corner*2.094395+rotation,b=a+2.094395;
                        triangles+=exp(-segmentDistance(q,float2(cos(a),sin(a))*radius*.75,float2(cos(b),sin(b))*radius*.75)/.001)*.3;
                    }
                    float activation=exp(-abs(r-radius*(.8+enter*.6))/.018)*(1-enter)*.6;
                    float halo=exp(-r*r/max(radius*radius,.001))* .20;
                    float3 color=seal==0?float3(1,.37,.08):seal==1?float3(.65,.24,1):float3(.48,.77,1);
                    float power=enter*(1-ease(.66,.83,t));
                    // Inscription travels once around each seal and settles into a
                    // quiet locked state; no additional particles or repeated flashes.
                    float runeRead=inscription*(.38+lockIn*.22);
                    light+=(color*(outer+inner*.85+triangles+activation+halo*.78)+gold()*glyph*runeRead)*power;
                    opacity=max(opacity,exp(-pow(r/max(radius*.9,.001),6))*.72*power);
                    // Braided Fate conduits pull each seal toward the common focus.
                    float2 d=center-at;float len=length(d);
                    float2 tangent=d/max(len,.001),perp=float2(-tangent.y,tangent.x);
                    float projected=dot(p-at,tangent)/max(len,.001);
                    float s=saturate(projected);
                    float bend=sin(s*3.14159)*.025*sin(s*10-t*12+seal);
                    float strand=abs(dot(p-at,perp)-bend);
                    float endpoint=smoothstep(0,.08,projected)*(1-smoothstep(.92,1,projected));
                    float traveling=exp(-pow((s-frac(t*2.8+seal*.21))/.13,2));
                    float conduction=exp(-strand/.0018)*endpoint*ease(.39,.50,t)*(1-converge);
                    light+=gold()*conduction*(.30+traveling*.35);
                }
                float burst=ease(.59,.66,t)*(1-ease(.68,.82,t));
                float r=length(p-center);
                light+=float3(.86,.69,1)*exp(-r/.095)*burst*1.25;
                light+=float3(.62,.40,1)*ring(r,(t-.59)*1.4,.009)*burst*.45;
                light*=lerp(1,.3,_Restraint);
                float alpha=max(opacity,saturate(max(light.r,max(light.g,light.b))));
                return float4(light/max(alpha,.001),alpha);
            }
            float4 souls(float2 uv,float t)
            {
                float progress=ease(.02,.80,t);
                float front=lerp(-.38,1.4,progress);
                float2 p=float2(uv.x*_Aspect,uv.y);
                float2 drift=float2(-t*.85,t*.22);
                float broad=fbm(p*3.6+drift);
                float fold=fbm(p*8+float2(broad*3,-t*.6));
                float thread=fbm(p*float2(16,8)+float2(fold*4,-t*.9));
                float boundary=front+sin(uv.y*5.5-t*2)*.075+(broad-.5)*.19;
                float d=uv.x-boundary;
                float consumed=1-smoothstep(-.025,.07+(fold*.035),d);
                float mist=exp(-pow((d-.015)/.115,2));
                float veins=pow(saturate(1-abs(thread-.50)*8),5);
                float ripples=pow(saturate(1-abs(fold-.49)*9),7);
                float billow=pow(saturate(fold*1.4),2)*mist;
                // Dark folds sit between softer near/far mist layers. Suppress the
                // bright contour field behind the front so the veil gains depth.
                float depth=smoothstep(-.10,.045,d);
                float underMist=exp(-pow((d+.045)/.16,2))*broad;
                float3 light=float3(.020,.24,.20)*(billow+underMist*.20)+float3(.09,.82,.60)*veins*mist*(.30+depth*.22);
                light+=float3(.48,1,.83)*ripples*mist*(.13+depth*.19);
                // Swept filament curls travel behind the leading veil.
                for(int soul=0;soul<3;soul++)
                {
                    float seed=hash21(float2(soul,19));
                    float y=.13+soul*.31+(seed-.5)*.10;
                    float xx=uv.x-front;
                    float path=y+sin(xx*(8+seed*4)+t*2.4+soul)*(.050+seed*.028)+sin(xx*17-soul)*.013;
                    float dist=abs(uv.y-path);
                    float tail=smoothstep(-.5,-.25,xx)*(1-smoothstep(-.06,.04,xx));
                    float wisp=pow(saturate(1-abs(xx+.19)/.32),.7);
                    light+=float3(.17,.77,.61)*exp(-dist/.0025)*tail*wisp*.38;
                    light+=float3(.04,.31,.23)*exp(-dist/.024)*tail*.25;
                }
                light*=1-ease(.72,.88,t);light*=lerp(1,.35,_Restraint);
                float alpha=max(consumed,saturate(mist*(fold*.7+.1)));
                return float4(light/max(alpha,.001),alpha);
            }
            fixed4 frag(v2f_img i):SV_Target
            {
                float2 uv=float2(i.uv.x,1-i.uv.y);
                float4 result=_Hero<.5?blade(uv,_Phase):_Hero<1.5?ritual(uv,_Phase):souls(uv,_Phase);
                return saturate(result);
            }
            ENDCG
        }
    }
}
