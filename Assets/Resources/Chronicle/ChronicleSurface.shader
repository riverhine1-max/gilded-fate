Shader "GildedFate/ChronicleSurface"
{
    Properties
    {
        _MainTex("Base color", 2D) = "white" {}
        _BumpMap("Normal", 2D) = "bump" {}
        _Mask("Metallic / AO / Smoothness", 2D) = "white" {}
        _EmissionMap("Emission", 2D) = "black" {}
        _EmissionColor("Emission color", Color) = (0,0,0,0)
        _PaperRelief("Paper relief", Range(0,1)) = 0
        _NormalStrength("Normal strength", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back ZWrite On ZTest LEqual
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex, _BumpMap, _Mask, _EmissionMap;
            float4 _EmissionColor;
            float _NormalStrength, _PaperRelief;
            float4 _MainTex_TexelSize;
            struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float4 tangent:TANGENT; float2 uv:TEXCOORD0; };
            struct Output { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; float3 normal:TEXCOORD2; float3 tangent:TEXCOORD3; float3 bitangent:TEXCOORD4; };
            Output vert(Input v)
            {
                Output o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.tangent=UnityObjectToWorldDir(v.tangent.xyz);
                o.bitangent=cross(o.normal,o.tangent)*v.tangent.w*unity_WorldTransformParams.w;
                return o;
            }
            float3 lit(float3 n,float3 v,float3 l,float3 base,float metal,float rough,float3 color)
            {
                float3 h=normalize(v+l);
                float nl=saturate(dot(n,l)),nv=max(.001,saturate(dot(n,v))),nh=saturate(dot(n,h)),vh=saturate(dot(v,h));
                float a=rough*rough,a2=a*a,den=nh*nh*(a2-1)+1;
                float d=a2/max(.0001,3.14159*den*den);
                float k=(rough+1)*(rough+1)/8;
                float g=(nl/(nl*(1-k)+k))*(nv/(nv*(1-k)+k));
                float3 f0=lerp(float3(.04,.04,.04),base,metal),f=f0+(1-f0)*pow(1-vh,5);
                float3 spec=d*g*f/max(.001,4*nl*nv);
                return ((1-f)*(1-metal)*base/3.14159+min(spec,float3(3,3,3)))*nl*color;
            }
            fixed4 frag(Output i):SV_Target
            {
                float3 base=tex2D(_MainTex,i.uv).rgb;
                float4 mask=tex2D(_Mask,i.uv);
                float3 t=UnpackNormal(tex2D(_BumpMap,i.uv));t.xy*=_NormalStrength;t=normalize(t);
                // The relief follows the actual wrinkle texture instead of adding a separate drifting pattern.
                // Restrict it to light vellum so foil ornaments retain their polished reflection.
                if(_PaperRelief>0)
                {
                    float2 stepUV=max(_MainTex_TexelSize.xy,float2(.001,.001));
                    float3 weights=float3(.299,.587,.114);
                    float dx=dot(tex2D(_MainTex,i.uv+float2(stepUV.x,0)).rgb-tex2D(_MainTex,i.uv-float2(stepUV.x,0)).rgb,weights);
                    float dy=dot(tex2D(_MainTex,i.uv+float2(0,stepUV.y)).rgb-tex2D(_MainTex,i.uv-float2(0,stepUV.y)).rgb,weights);
                    float low=min(base.r,min(base.g,base.b)),high=max(base.r,max(base.g,base.b));
                    float vellum=saturate((low-.25)*3)*(1-saturate((high-low)*2));
                    t.xy-=float2(dx,dy)*2.2*_PaperRelief*vellum;
                    t=normalize(t);
                }
                float3 n=normalize(i.tangent*t.x+i.bitangent*t.y+i.normal*t.z);
                float3 v=normalize(_WorldSpaceCameraPos-i.world);
                float rough=max(lerp(.10,.82,_PaperRelief),1-mask.a);
                // A self-contained warm studio. No global game lights or pipeline-specific light layers are changed.
                float3 color=base*(.28*(1-mask.r)+.24*mask.r)*mask.g;
                color+=lit(n,v,normalize(float3(-.40,.85,-.30)),base,mask.r,rough,float3(2.5,2.30,1.96));
                color+=lit(n,v,normalize(float3(.65,.45,.45)),base,mask.r,rough,float3(.82,.92,1.02));
                // Broad warm reflection and a cool rim make the foil read as metal without a global reflection probe.
                float fresnel=pow(1-saturate(dot(n,v)),4);
                color+=mask.r*(base*float3(.24,.19,.10)+float3(.15,.20,.21)*fresnel)*mask.g;
                color+=tex2D(_EmissionMap,i.uv).rgb*_EmissionColor.rgb;
                return fixed4(color/(1+color*.15),1);
            }
            ENDCG
        }
    }
}
