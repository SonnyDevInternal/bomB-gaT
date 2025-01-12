Shader "Custom/OutlineShader"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        // First Pass: Render the outline
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On          // Allow writing to the depth buffer
            ZTest LEqual       // Test for depth to ensure outline is on top
            Cull Front         // Render the outline using back faces of the inflated geometry
            ColorMask RGB      // Render only color, no alpha blending
            Blend SrcAlpha OneMinusSrcAlpha  // Standard alpha blending

            // Outline color (black in this example)
            Color (0, 0, 0, 1) 

            // Inflate geometry for the outline effect
            Offset 15, 15

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return half4(0, 0, 0, 1); // Black outline color
            }

            ENDCG
        }

        // Second Pass: Render the object
        Pass
        {
            Name "OBJECT"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On             // Write to the depth buffer to preserve object depth
            ZTest LEqual          // Ensure the object is only drawn where it's visible
            ColorMask RGB         // Write only to the color buffer

            // Standard object rendering behavior
            Blend SrcAlpha OneMinusSrcAlpha  // Standard alpha blending

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return half4(1, 1, 1, 1); // Object color (white)
            }

            ENDCG
        }
    }
    Fallback "Diffuse"
}
