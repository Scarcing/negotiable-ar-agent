Shader "Hidden/NegotiableAR/TaskForeground"
{
    Properties { _MainTex ("Camera", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _CropRect;
            float _UseTaskDepth, _TaskRange, _TaskEdgeTolerance;
            float3 _RgbOrigin, _RgbRay00, _RgbRay10, _RgbRay01, _RgbRay11;
            UNITY_DECLARE_TEX2DARRAY(_TaskDepthTexture);
            float4x4 _TaskDepthProjection;
            float4 _TaskDepthParameters;

            float LinearTaskDepth(float ndc)
            {
                float denominator = ndc + _TaskDepthParameters.y;
                if (abs(denominator) < 0.000001) return 10000;
                return _TaskDepthParameters.x / denominator;
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 uv = _CropRect.xy + input.uv * _CropRect.zw;
                fixed4 color = tex2D(_MainTex, uv);
                if (_UseTaskDepth < 0.5) return color;

                // Pinhole rays use the RGB camera's timestamped pose/intrinsics.
                // Project a range boundary into LEFT environment depth, rather than
                // sampling RGB UVs in the differently positioned depth camera.
                float3 ray = normalize(lerp(lerp(_RgbRay00, _RgbRay10, uv.x),
                                           lerp(_RgbRay01, _RgbRay11, uv.x), uv.y));
                float3 boundary = _RgbOrigin + ray * _TaskRange;
                float4 projected = mul(_TaskDepthProjection, float4(boundary, 1));
                if (projected.w <= 0.000001) return color;
                float2 depthUv = (projected.xy / projected.w + 1) * 0.5;
                if (any(depthUv < 0) || any(depthUv > 1)) return color;

                float raw = UNITY_SAMPLE_TEX2DARRAY(_TaskDepthTexture, float3(depthUv, 0)).r;
                // Missing depth and areas outside coverage fall back to the crop.
                if (raw <= 0.000001 || raw >= 0.999999) return color;
                float surfaceDepth = LinearTaskDepth(raw * 2 - 1);
                float boundaryDepth = LinearTaskDepth(projected.z / projected.w);
                if (!(surfaceDepth > 0 && surfaceDepth < 9999 && boundaryDepth > 0 && boundaryDepth < 9999)) return color;

                float keep = 1 - smoothstep(boundaryDepth - _TaskEdgeTolerance,
                                           boundaryDepth + _TaskEdgeTolerance, surfaceDepth);
                // Neutral fill is visually separate from the retained task pixels.
                return fixed4(lerp(float3(0.18, 0.18, 0.18), color.rgb, keep), 1);
            }
            ENDCG
        }
    }
}
