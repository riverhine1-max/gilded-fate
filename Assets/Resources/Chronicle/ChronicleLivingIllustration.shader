Shader "Gilded Fate/Chronicle Living Illustration"
{
    Properties { _MainTex("Art",2D)="white"{} _Accent("Light",Color)=(1,.7,.3,1) _Clock("Clock",Float)=0 _Mode("Mode",Float)=0 _Reveal("Ink reveal",Float)=1 }
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
            sampler2D _MainTex; float4 _MainTex_TexelSize, _Accent; float _Clock, _Mode, _Reveal;
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) { float2 i=floor(p),f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y); }
            float fog(float2 p) { return noise(p)*.58+noise(p*2.07)*.27+noise(p*4.13)*.15; }
            float4 frag(v2f_img i):SV_Target
            {
                float2 uv=i.uv;
                if(_Mode<.5)
                {
                    // Subpixel drift and layered atmosphere; geometry remains stable and the page clip is unchanged.
                    uv=(uv-.5)*.986+.5+float2(sin(_Clock*.13)*.003,cos(_Clock*.11)*.002);
                    float4 c=tex2D(_MainTex,saturate(uv));
                    float mist=fog(uv*float2(3,6)+float2(_Clock*.014,-_Clock*.007));
                    float floorMist=pow(saturate(1-uv.y),2)*smoothstep(.39,.75,mist);
                    c.rgb=lerp(c.rgb,_Accent.rgb*.36+.12,floorMist*.18);
                    float shafts=pow(saturate(sin((uv.x+uv.y*.38)*19+_Clock*.025)),18)*smoothstep(.18,.85,uv.y);
                    c.rgb+=_Accent.rgb*shafts*.045;
                    float vignette=smoothstep(.20,.76,length((uv-.5)*float2(1.05,.78)));
                    c.rgb*=1-vignette*.26;
                    float pigment=fog(uv*17);
                    c.a*=smoothstep(pigment-.11,pigment+.12,_Reveal*1.3);
                    return c;
                }
                // Breathing cloth motion is strongest at hems; faces, feet and weapons stay recognizable.
                uv.x+=sin(uv.y*12+_Clock*.7)*.0019*pow(1-uv.y,2);
                float4 c=tex2D(_MainTex,saturate(uv));
                float2 px=_MainTex_TexelSize.xy*1.5;
                float edge=saturate((tex2D(_MainTex,uv+float2(px.x,0)).a+tex2D(_MainTex,uv-float2(px.x,0)).a+tex2D(_MainTex,uv+float2(0,px.y)).a+tex2D(_MainTex,uv-float2(0,px.y)).a)*.25-c.a);
                float lum=dot(c.rgb,float3(.2126,.7152,.0722));
                float saturation=max(c.r,max(c.g,c.b))-min(c.r,min(c.g,c.b));
                float pulse=.64+.16*sin(_Clock*1.1+uv.y*6);
                c.rgb+=_Accent.rgb*(smoothstep(.47,.95,lum)*saturation*.22*pulse+edge*.18);
                if(_Mode>1.5) { c.rgb=lerp(float3(.018,.021,.035),_Accent.rgb*.10,lum); c.a*=.82; }
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
