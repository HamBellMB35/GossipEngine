Shader "TownsPeople/TMP_SandDissolve"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
        _FaceColor ("Text Color", Color) = (1,1,1,1)
        _NoiseTex ("Dissolve Noise", 2D) = "gray" {}
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _EdgeWidth ("Edge Width", Range(0.001, 0.5)) = 0.08
        _EdgeColor ("Edge Color", Color) = (1, 0.75, 0.35, 1)
        _WindDirection ("Wind Direction", Vector) = (1, 0.35, 0, 0)
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.15

        // Standard Unity UI stencil block — required so this renders correctly when nested
        // inside a masked container (DialogueOptionButton lives inside OptionsScrollView's
        // Mask). Without these, dissolving text could bleed outside the scroll view's bounds.
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            float4 _FaceColor;
            float _DissolveAmount;
            float _EdgeWidth;
            float4 _EdgeColor;
            float4 _WindDirection;
            float _WindStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color; // Already carries the parent CanvasGroup's alpha, baked in
                                    // by Unity's UI system before this shader ever runs — this
                                    // is what makes the existing fade "just work" underneath
                                    // the dissolve, with no extra wiring needed.
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Simplified TMP SDF read (font atlas alpha = signed distance, 0.5 = glyph
                // edge). Deliberately skips outline/bold support to keep this legible — meant
                // for plain option-button text, not the full TMP feature set.
                float sdf = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                float textMask = smoothstep(0.5 - fwidth(sdf), 0.5 + fwidth(sdf), sdf);

                // Offset the noise sample by a growing amount in _WindDirection as the dissolve
                // progresses — this is what makes the disintegrating edge appear to drift/blow
                // sideways instead of just eating away symmetrically in place.
                float2 windUV = i.uv + _WindDirection.xy * _DissolveAmount * _WindStrength;
                float noiseVal = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, windUV * 3.0).r;

                // A pixel survives as long as its noise value is still above the current
                // dissolve threshold; everything below has "blown away".
                float dissolveMask = step(_DissolveAmount, noiseVal);

                // Thin glowing rim right at the cutoff edge (the "hot sand" look), fading back
                // to the normal text color further into the still-solid region.
                float rimBand = smoothstep(_DissolveAmount, _DissolveAmount + _EdgeWidth, noiseVal);
                float rim = (1.0 - rimBand) * dissolveMask;

                float3 finalColor = lerp(_FaceColor.rgb, _EdgeColor.rgb, rim);
                float finalAlpha = textMask * dissolveMask * i.color.a * _FaceColor.a;

                return float4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}