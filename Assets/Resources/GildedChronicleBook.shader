Shader "Hidden/GildedFate/ChronicleBook"
{
    // Flat-shaded, depth-correct unlit surface for the Chronicle's placeholder book. One directional term is baked into the
    // colour so the covers, page block and turning sheet read as solid objects without needing scene lights, which keeps it
    // identical under the project's URP asset and in WebGL builds. _Emission adds the magical glow on gold trim and the emblem.
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _Emission("Emission", Color) = (0,0,0,0)
        _Light("Light direction", Vector) = (-0.35,0.9,-0.25,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back ZWrite On ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            fixed4 _Color; fixed4 _Emission; float4 _Light;
            struct v2f { float4 pos : SV_POSITION; float shade : TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                float l = saturate(dot(n, normalize(_Light.xyz)));
                o.shade = 0.42 + 0.58 * l;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = _Color;
                c.rgb = c.rgb * i.shade + _Emission.rgb;
                c.a = 1;
                return c;
            }
            ENDCG
        }
    }
}
