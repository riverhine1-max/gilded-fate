Shader "Gilded Fate/Chronicle Illustration VFX"
{
    Properties { _MainTex("Unused",2D)="white"{} _Accent("Light",Color)=(1,.7,.3,1) _Clock("Clock",Float)=0 _Mode("Mode",Float)=1 _Progress("Progress",Float)=1 _Intensity("Intensity",Float)=1 _Focus("Focus",Vector)=(.5,.5,0,0) }
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
            float4 _Accent,_Focus; float _Clock,_Mode,_Progress,_Intensity;
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) { float2 i=floor(p),f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y); }
            float glowLine(float d,float width) { return exp(-abs(d)*width); }
            float hand(float2 q,float theta,float reach,float width)
            {
                float2 direction=float2(cos(theta),sin(theta));
                return glowLine(length(q-direction*clamp(dot(q,direction),0,reach)),width);
            }
            float4 frag(v2f_img i):SV_Target
            {
                float2 uv=i.uv, q=(uv-_Focus.xy)*float2(1,1.5); float p=saturate(_Progress),r=length(q),angle=atan2(q.y,q.x);
                if(_Mode>6.5)
                {
                    q=uv-.5;r=length(q);angle=atan2(q.y,q.x);
                    if(_Mode<7.5)
                    {
                        // A lit crystal sphere with a moving liquid core and a soft energy corona.
                        float radius=.32,inside=1-smoothstep(radius-.007,radius+.003,r);
                        float z=sqrt(saturate(1-dot(q/radius,q/radius)));
                        float lighting=saturate(dot(float3(q/radius,z),normalize(float3(-.55,.65,.65))));
                        float flow=noise(q*17+float2(_Clock*.12,-_Clock*.08));
                        float veins=pow(saturate(sin(angle*3+r*34+flow*7-_Clock*.6)),12)*(.4+.6*z);
                        float highlight=exp(-length(q-float2(-.10,.13))*70);
                        float edge=pow(1-z,4);
                        float3 sphere=_Accent.rgb*(.11+lighting*.58+veins*.48)+float3(.95,1,.92)*highlight*.9;
                        sphere+=_Accent.rgb*edge*.5;
                        float corona=exp(-abs(r-radius)*36)*.22;
                        return float4(lerp(_Accent.rgb,sphere,inside),saturate(inside*.98+corona));
                    }
                    if(_Mode<8.5)
                    {
                        // Engraved watching eye: tapered lids, a shaded iris and a sharp vertical pupil.
                        float aperture=saturate(1-abs(q.x)/.43)*.18;
                        float lid=glowLine(abs(q.y)-aperture,650);
                        float mask=(1-smoothstep(aperture-.005,aperture+.005,abs(q.y)))*(1-smoothstep(.40,.43,abs(q.x)));
                        float iris=1-smoothstep(.128,.137,r);
                        float pupil=exp(-abs(q.x)*150)*(1-smoothstep(.09,.12,abs(q.y)));
                        float radial=(.55+.45*sin(angle*38+r*70+_Clock*.14));
                        float3 eye=float3(.015,.023,.02)+_Accent.rgb*(iris*radial*.63+lid*.65);
                        eye*=1-pupil*.98;eye+=float3(.85,1,.92)*exp(-length(q-float2(-.04,.05))*130)*iris;
                        return float4(eye,saturate(mask*.94+lid*.65));
                    }
                    if(_Mode<9.5)
                    {
                        // Antique celestial clock: brass rim, minute divisions, gears and independent hands.
                        float face=1-smoothstep(.395,.405,r);
                        float rings=glowLine(r-.4,230)+glowLine(r-.368,260)*.45+glowLine(r-.30,260)*.35;
                        float ticks=pow(saturate(cos(angle*60)),40)*(1-smoothstep(.012,.018,abs(r-.338)));
                        float hours=pow(saturate(cos(angle*12)),22)*(1-smoothstep(.022,.034,abs(r-.338)));
                        float gears=glowLine(length(q-float2(-.115,-.1))-.075,250)*.24+glowLine(length(q-float2(.105,-.1))-.063,250)*.24;
                        float hands=hand(q,1.57-_Clock*.025,.26,180)+hand(q,.2-_Clock*.003,.18,180);
                        float metal=.70+.40*saturate(q.y+q.x+.5);
                        float3 c=float3(.035,.044,.045)*face+_Accent.rgb*(rings+ticks*.5+hours*.75+gears+hands)*metal;
                        return float4(c,saturate(face*.90+rings*.5));
                    }
                    // A fine engraved class sigil with a luminous core, radial rune marks and a braided outer ring.
                    float rings=glowLine(r-.34,200)+glowLine(r-.29,230)*.4;
                    float diamond=glowLine(abs(q.x)+abs(q.y)-.25,200);
                    float marks=pow(saturate(cos(angle*16)),24)*(1-smoothstep(.011,.024,abs(r-.315)));
                    float glow=exp(-r*15)*(.30+.04*sin(_Clock*.6));
                    float intensity=rings*.55+diamond*.8+marks*.7;
                    return float4(lerp(_Accent.rgb,float3(1,.94,.74),saturate(intensity)*.45),saturate(intensity+glow));
                }
                float core=0,halo=0; float3 color=_Accent.rgb; float envelope=sin(p*3.14159265);
                if(_Mode<1.5)
                {
                    // Interwoven filaments, thinner white-gold cores, broad soft energy and travelling light nodes.
                    for(int strand=0;strand<8;strand++)
                    {
                        float seed=strand*1.173;
                        float y=.12+strand*.108+sin(uv.x*6+seed+_Clock*.12)*.055+sin(uv.x*14-seed)*.018;
                        float d=abs(uv.y-y),reveal=1-smoothstep(p-.04,p+.02,uv.x);
                        core+=glowLine(d,1000)*reveal; halo+=glowLine(d,85)*reveal*.30;
                        float node=frac(_Clock*.055+seed*.13);
                        halo+=exp(-length(float2((uv.x-node)*1.5,uv.y-y))*160)*reveal*.65;
                    }
                }
                else if(_Mode<2.5)
                {
                    // Branching cracks traverse the painting, exposing an emerald-gold light under its surface.
                    for(int crack=0;crack<6;crack++)
                    {
                        float seed=crack*2.31;
                        float x=.08+crack*.175+sin(uv.y*16+seed)*.016+sin(uv.y*41+seed)*.008;
                        float visible=smoothstep(1-p-.03,1-p+.02,uv.y);
                        float d=abs(uv.x-x);core+=glowLine(d,950)*visible;halo+=glowLine(d,110)*visible*.4;
                        float branch=abs(uv.y-(.19+crack*.10+(uv.x-x)*1.3));
                        float reach=(1-smoothstep(.015,.085,abs(uv.x-x)))*visible;
                        core+=glowLine(branch,900)*reach*.55;halo+=glowLine(branch,110)*reach*.15;
                    }
                    color=lerp(_Accent.rgb,float3(.35,.9,.76),.42);
                }
                else if(_Mode<3.5)
                {
                    float radius=.19+.085*p;
                    float wheel=glowLine(r-radius,1000)+glowLine(r-radius*.82,1000)*.6;
                    float spokes=pow(saturate(cos(angle*24)),22)*(1-smoothstep(.015,.030,abs(r-radius*.91)));
                    core=(wheel+spokes*.55)*p;halo=(glowLine(r-radius,58)*.38+exp(-r*8)*.17)*p;
                }
                else if(_Mode<4.5)
                {
                    float ray=pow(saturate(cos(angle*12+noise(float2(angle*3,0))*2)),18);
                    core=ray*exp(-r*8)*envelope*.45;halo=exp(-r*8)*envelope*.8;
                    core+=glowLine(r-p*.45,650)*envelope*.6;
                }
                else if(_Mode<5.5)
                {
                    core=glowLine(r-(.04+p*.47),1000)*(1-p)*.7;
                    halo=glowLine(r-(.04+p*.47),75)*(1-p)*.35;
                }
                else
                {
                    float curl=sin(angle*3-r*26+_Clock*.3)*.022;
                    core=glowLine(r-(.18+curl),750)*envelope*.5;
                    halo=glowLine(r-(.18+curl),70)*envelope*.35+exp(-r*7)*envelope*.2;
                }
                float fade=smoothstep(0,.035,uv.x)*smoothstep(0,.035,1-uv.x)*smoothstep(0,.025,uv.y)*smoothstep(0,.025,1-uv.y);
                float alpha=saturate((core*.68+halo)*_Intensity)*fade;
                return float4(lerp(color,float3(1,.96,.8),saturate(core)*.62),alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
