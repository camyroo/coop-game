Shader "Custom/GridShader"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (1,1,1,0.3)
        _CellSize ("Cell Size", Float) = 2.0
        _LineWidth ("Line Width", Float) = 0.05
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };
            
            float4 _GridColor;
            float _CellSize;
            float _LineWidth;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float2 pos = i.worldPos.xz;
                float2 grid = abs(frac(pos / _CellSize - 0.5) - 0.5) / fwidth(pos / _CellSize);
                float lineValue = min(grid.x, grid.y);
                float gridMask = 1.0 - min(lineValue, 1.0);
                
                return float4(_GridColor.rgb, _GridColor.a * gridMask);
            }
            ENDCG
        }
    }
}