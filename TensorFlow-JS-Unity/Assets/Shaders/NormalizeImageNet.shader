Shader "Processing Shaders/NormalizeImageNet"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

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
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;

            // Set the pixel color values for the processed image
            float4 frag(v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                // Normalize the red color channel values
                col.r = (col.r - 0.4850) / 0.2290;
                // Normalize the green color channel values
                col.g = (col.g - 0.4560) / 0.2240;
                // Normalize the blue color channel values
                col.b = (col.b - 0.4060) / 0.2250;
                return col;
            }
            ENDCG
        }
    }
}
