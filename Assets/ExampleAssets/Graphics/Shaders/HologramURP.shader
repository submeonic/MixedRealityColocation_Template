Shader "Custom/Hologram"
{
    Properties
    {
        _Color ("Base Color", Color) = (0, 1, 1, 1)
        _FresnelPower ("Fresnel Power", Float) = 5
        _ScanSpeed ("Scan Speed", Float) = 1
        _ScanFrequency ("Scanline Frequency", Float) = 20
        _ScanStrength ("Scanline Strength", Float) = 0.5
        _GlowIntensity ("Glow Intensity", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "HologramPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D_X_FLOAT(_OculusDepthTexture);
            SAMPLER(sampler_OculusDepthTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 viewDirWS   : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 worldPos    : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _FresnelPower;
                float _ScanSpeed;
                float _ScanFrequency;
                float _ScanStrength;
                float _GlowIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(worldPos));

                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.normalWS = normalWS;
                OUT.viewDirWS = viewDirWS;
                OUT.worldPos = worldPos;
                return OUT;
            }

            float Linear01DepthFromOculus(float rawDepth)
            {
                return rawDepth; // already linear 0–1
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Get screen-space UV
                float2 screenUV = IN.positionHCS.xy / IN.positionHCS.w;
                screenUV = screenUV * 0.5 + 0.5;

                // Sample Oculus depth texture
                float oculusRawDepth = SAMPLE_TEXTURE2D_X(_OculusDepthTexture, sampler_OculusDepthTexture, screenUV);
                float oculusDepth = Linear01DepthFromOculus(oculusRawDepth);

                // Depth of our pixel
                float fragDepth = IN.positionHCS.z / IN.positionHCS.w;

                // Discard if behind real-world object
                if (fragDepth > oculusDepth)
                    discard;

                // Fresnel effect
                float fresnel = pow(1.0 - saturate(dot(IN.viewDirWS, IN.normalWS)), _FresnelPower);

                // Multi scanlines using sine banding
                float band = sin(IN.worldPos.y * _ScanFrequency + _Time.y * _ScanSpeed) * 0.5 + 0.5;
                band = pow(band, 4.0);
                float scanline = band * _ScanStrength;

                float glow = fresnel + scanline * _GlowIntensity;
                float3 finalColor = _Color.rgb * glow;

                return half4(finalColor, 0.75); // Transparent-ish
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
