Shader "WES/NightDarknessOverlay"
{
    Properties
    {
        // Unity UI Image가 머티리얼에 메인 텍스처를 항상 세팅하므로 _MainTex가 필수.
        // 실제 샘플링은 하지 않지만(_Color 기반 단색 오버레이) 프로퍼티가 없으면 매 프레임 경고.
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Darkness Color", Color) = (0, 0, 0, 0.85)
        _CircleCount ("Active Circle Count", Int) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGBA

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            // 최대 16개 원형 컷아웃 지원
            #define MAX_CIRCLES 16

            struct appdata_t
            {
                float4 vertex    : POSITION;
                float4 color     : COLOR;
                float2 texcoord  : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                fixed4 color     : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;
            int _CircleCount;

            // 각 원: xy = 스크린 UV 좌표 (0~1), z = 반경(UV 단위), w = 미사용
            float4 _Circles[MAX_CIRCLES];

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 현재 프래그먼트의 스크린 UV (0~1)
                float2 uv = IN.texcoord;

                // ClipRect로 마스킹
                half4 color = IN.color;
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                clip(color.a - 0.001);

                // 원형 컷아웃: 원 내부 픽셀의 알파를 0으로
                for (int i = 0; i < _CircleCount && i < MAX_CIRCLES; i++)
                {
                    float2 center = _Circles[i].xy;
                    float radius  = _Circles[i].z;
                    float aspect  = _Circles[i].w;

                    // aspect 보정: UV X가 늘어나지 않도록
                    float2 diff = uv - center;
                    diff.x *= aspect;
                    float dist = length(diff);

                    if (dist < radius * aspect)
                    {
                        // 경계를 부드럽게 처리 (feathering)
                        float edgeRadius = radius * aspect;
                        float feather = edgeRadius * 0.1;
                        float alpha = smoothstep(edgeRadius - feather, edgeRadius, dist);
                        color.a *= alpha;
                    }
                }

                return color;
            }
            ENDCG
        }
    }
}
