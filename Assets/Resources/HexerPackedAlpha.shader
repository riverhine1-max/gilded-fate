Shader "Gilded Fate/Hexer Packed Alpha"
{
    Properties { _MainTex ("RGB left / Alpha right",2D)="black"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 frag(v2f_img i):SV_Target
            {
                float2 uv=i.uv;
                float3 rgb=tex2D(_MainTex,float2(uv.x*.5,uv.y)).rgb;
                float a=tex2D(_MainTex,float2(.5+uv.x*.5,uv.y)).r;
                #ifndef UNITY_COLORSPACE_GAMMA
                a=LinearToGammaSpace(a);
                #endif
                return float4(rgb,saturate(a));
            }
            ENDCG
        }
    }
}
