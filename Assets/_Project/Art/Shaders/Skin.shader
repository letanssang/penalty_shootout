// Shader da tán xạ dưới bề mặt (T31).
//
// KỸ THUẬT: pre-integrated SSS (Penner). Tích phân trước kết quả tán xạ cho mọi cặp
// (góc chiếu, độ cong) vào một LUT 128×32, lúc chạy chỉ còn ĐÚNG MỘT lần lấy mẫu texture.
// Không render target riêng, không pass blur, không copy màn hình — đó là lý do duy nhất khiến
// kỹ thuật này chạy được trên GPU di động trong ngân sách 0.5ms cho cả hai nhân vật.
//
// BA THỨ QUYẾT ĐỊNH SỐNG CHẾT CỦA SHADER NÀY TRÊN MOBILE
//
//   1. SỐ BIẾN THỂ. Mỗi biến thể là một lần biên dịch lúc chạy, và lần biên dịch đầu tiên chặn
//      luôn khung hình. URP/Lit khai hơn ba mươi multi_compile → hàng chục nghìn biến thể.
//      Shader này khai bảy, ra 2×3×2×2×2×2×2 = 192 biến thể cho pass dựng hình, cộng 2 cho
//      ShadowCaster và 1 cho DepthOnly = 195. Xem SkinBudget.MaxForwardVariants.
//      Mỗi lần thêm một #pragma multi_compile là NHÂN ĐÔI con số đó — hãy đếm trước khi thêm.
//
//   2. KEYWORD BỊ LƯỢC NHẦM. Hai keyword riêng của T31 khai bằng multi_compile chứ KHÔNG phải
//      shader_feature: shader_feature bị lược khi build nếu không vật liệu nào trong build bật
//      keyword đó, mà ở đây keyword được bật/tắt LÚC CHẠY theo bậc thiết bị. Bị lược thì nhân
//      vật hoặc thành màu hồng, hoặc tệ hơn: trông vẫn bình thường nhưng chạy nhánh sai.
//
//   3. ĐỘ CONG. Độ cong ước lượng bằng đạo hàm màn hình của pháp tuyến (fwidth). Nó RẺ nhưng
//      nhiễu ở rìa hình học và ở khoảng cách xa. Đường nâng cấp khi cần: nướng sẵn một
//      curvature map vào kênh của texture da — không đổi shader, chỉ thay _CurvatureScale bằng
//      một lần fetch. Chưa làm vì chưa đo thấy cần.
//
// TƯƠNG THÍCH URP FORWARD+: keyword _CLUSTER_LIGHT_LOOP (URP 6.1 đổi tên từ _FORWARD_PLUS) và
// cặp macro LIGHT_LOOP_BEGIN / LIGHT_LOOP_END. Viết vòng lặp đèn phụ bằng tay theo
// GetAdditionalLightsCount() sẽ chạy sai trong Forward+: ở chế độ phân cụm hàm đó trả về 0 và
// danh sách đèn nằm trong bit list của cụm.
Shader "Eleven/Skin"
{
    Properties
    {
        [MainTexture] _BaseMap ("Màu da", 2D) = "white" {}
        [MainColor]   _BaseColor ("Sắc da", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Pháp tuyến", 2D) = "bump" {}
        _NormalScale ("Độ mạnh pháp tuyến", Range(0.0, 2.0)) = 1.0

        [NoScaleOffset] _SssLut ("LUT tán xạ (128×32, nướng bởi SkinSssLut)", 2D) = "white" {}
        _SssStrength ("Cường độ tán xạ", Range(0.0, 1.0)) = 1.0

        _CurvatureScale ("Hệ số độ cong", Range(0.0, 8.0)) = 1.0
        _CurvatureBias ("Bù độ cong (1/mm)", Range(0.0, 0.1)) = 0.0

        _Smoothness ("Độ bóng", Range(0.0, 1.0)) = 0.45
        _SpecularTint ("Màu phản chiếu", Color) = (0.25, 0.22, 0.20, 1)

        [NoScaleOffset] _ThicknessMap ("Độ dày (cho ánh sáng xuyên)", 2D) = "white" {}
        _TransmissionColor ("Màu ánh sáng xuyên", Color) = (0.62, 0.16, 0.10, 1)
        _TransmissionPower ("Độ tụ ánh sáng xuyên", Range(1.0, 16.0)) = 6.0
        _TransmissionStrength ("Cường độ ánh sáng xuyên", Range(0.0, 4.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _NormalScale;
            half   _SssStrength;
            half   _CurvatureScale;
            half   _CurvatureBias;
            half   _Smoothness;
            half4  _SpecularTint;
            half4  _TransmissionColor;
            half   _TransmissionPower;
            half   _TransmissionStrength;
        CBUFFER_END

        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);      SAMPLER(sampler_BumpMap);
        TEXTURE2D(_SssLut);       SAMPLER(sampler_SssLut);
        TEXTURE2D(_ThicknessMap); SAMPLER(sampler_ThicknessMap);

        // PHẢI khớp từng số với Eleven.Presentation.Skin.SkinSssLut.
        // Đơn vị độ cong là 1/MILIMÉT, còn thế giới tính bằng MÉT — chỗ đổi đơn vị nằm trong
        // SkinCurvature() bên dưới. Lệch một lần 1000 ở đây là toàn bộ sắc da lệch mà không có
        // lỗi nào báo ra.
        #define SKIN_MIN_CURVATURE 0.005     // = 1 / 200 mm
        #define SKIN_MAX_CURVATURE 0.1666667 // = 1 / 6 mm

        /// Bản sao đúng công thức của SkinSssLut.Uv.
        float2 SkinSssUv(float ndotl, float curvature)
        {
            float u = saturate(ndotl * 0.5 + 0.5);
            float v = saturate((curvature - SKIN_MIN_CURVATURE) / (SKIN_MAX_CURVATURE - SKIN_MIN_CURVATURE));
            return float2(u, v);
        }

        /// Độ cong cục bộ (1/mm), ước lượng từ đạo hàm màn hình.
        float SkinCurvature(float3 normalWS, float3 positionWS)
        {
            float deltaN = length(fwidth(normalWS));
            float deltaP = length(fwidth(positionWS));

            // deltaN/deltaP có đơn vị 1/mét; LUT dùng 1/milimét → chia 1000.
            float curvaturePerMm = (deltaN / max(deltaP, 1e-5)) * _CurvatureScale * 0.001;

            return clamp(curvaturePerMm + _CurvatureBias, SKIN_MIN_CURVATURE, SKIN_MAX_CURVATURE);
        }

        /// Phần khuếch tán của một nguồn sáng.
        /// Không có _SKIN_SSS_ON thì đây đúng là Lambert — tức là đường Lit thường của bậc C.
        half3 SkinDiffuse(float ndotl, float curvature)
        {
            half3 lambert = (half3)saturate(ndotl).xxx;

        #ifdef _SKIN_SSS_ON
            float2 uv = SkinSssUv(ndotl, curvature);
            half3 scattered = SAMPLE_TEXTURE2D(_SssLut, sampler_SssLut, uv).rgb;
            return lerp(lambert, scattered, _SssStrength);
        #else
            return lambert;
        #endif
        }

        /// GGX một thuỳ, dạng rút gọn của URP cho mobile. Da người thật có hai thuỳ phản chiếu
        /// (lớp dầu + lớp sừng); ở khoảng cách camera của trò chơi này, thuỳ thứ hai không đáng
        /// gấp đôi chi phí.
        half3 SkinSpecular(half3 normalWS, half3 viewDirWS, half3 lightDirWS)
        {
            half3 halfDir = SafeNormalize(lightDirWS + viewDirWS);
            half nh = saturate(dot(normalWS, halfDir));
            half lh = saturate(dot(lightDirWS, halfDir));

            half roughness = max(1.0h - _Smoothness, 0.02h);
            half r2 = roughness * roughness;
            half d = nh * nh * (r2 * r2 - 1.0h) + 1.00001h;

            half normalization = roughness * 4.0h + 2.0h;
            half specTerm = (r2 * r2) / ((d * d) * max(0.1h, lh * lh) * normalization);

            return specTerm * _SpecularTint.rgb;
        }
        ENDHLSL

        Pass
        {
            Name "SkinForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SkinVertex
            #pragma fragment SkinFragment

            // ── Bảy multi_compile, 192 biến thể. Đếm trước khi thêm dòng thứ tám. ──
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP                                 // 2
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE     // 3
            #pragma multi_compile _ _ADDITIONAL_LIGHTS                                  // 2
            #pragma multi_compile_fragment _ _SHADOWS_SOFT                              // 2
            #pragma multi_compile _ _SKIN_SSS_ON                                        // 2
            #pragma multi_compile_fragment _ _SKIN_TRANSMISSION_ON                      // 2
            #pragma multi_compile_fragment _ DEBUG_DISPLAY                              // 2

            // KHÔNG khai: LIGHTMAP_ON (nhân vật động, không lightmap), _SCREEN_SPACE_OCCLUSION
            // (T32 cấm SSAO ở mọi bậc), _ADDITIONAL_LIGHT_SHADOWS (đèn pha sân không đổ bóng),
            // _REFLECTION_PROBE_* (da gần như không phản chiếu môi trường), _LIGHT_COOKIES,
            // LOD_FADE_CROSSFADE, multi_compile_instancing (đúng hai nhân vật).
            // Mỗi dòng bỏ đi ở đây là một nửa thời gian biên dịch màn hình đầu tiên.

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half4  tangentWS  : TEXCOORD3;   // w = dấu bitangent
            };

            Varyings SkinVertex(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS);
                VertexNormalInputs normals = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS = half3(normals.normalWS);
                OUT.tangentWS = half4(normals.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);

                return OUT;
            }

            half4 SkinFragment(Varyings IN) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = baseSample.rgb * _BaseColor.rgb;

                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv), _NormalScale);

                half3 bitangentWS = IN.tangentWS.w * cross(IN.normalWS, IN.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(IN.tangentWS.xyz, bitangentWS, IN.normalWS);
                half3 normalWS = NormalizeNormalPerPixel(mul(normalTS, tangentToWorld));

                half3 viewDirWS = half3(GetWorldSpaceNormalizeViewDir(IN.positionWS));

                // Độ cong tính từ pháp tuyến HÌNH HỌC, không phải pháp tuyến đã map: chi tiết
                // lỗ chân lông trong normal map không phải là độ cong của cái đầu, và đưa nó vào
                // đây sẽ làm LUT nhảy loạn giữa các điểm ảnh cạnh nhau.
                float curvature = SkinCurvature(normalize(IN.normalWS), IN.positionWS);

                // Biến này PHẢI tên là inputData: macro LIGHT_LOOP_BEGIN của URP đọc thẳng
                // inputData.normalizedScreenSpaceUV và inputData.positionWS.
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);

                half3 color = half3(0.0h, 0.0h, 0.0h);

                // ── Đèn chính ────────────────────────────────────────────────────────────────
                Light mainLight = GetMainLight(inputData.shadowCoord);
                half mainAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                float mainNdotL = dot(normalWS, mainLight.direction);
                half3 mainRadiance = mainLight.color * mainAtten;

                color += albedo * SkinDiffuse(mainNdotL, curvature) * mainRadiance;
                color += SkinSpecular(normalWS, viewDirWS, half3(mainLight.direction)) * mainRadiance
                         * saturate(mainNdotL);

            #ifdef _SKIN_TRANSMISSION_ON
                // Ánh sáng xuyên qua chỗ mỏng: vành tai, cánh mũi, kẽ ngón tay khi ngược sáng.
                // Chỉ tính cho đèn chính — đây là hiệu ứng của MẶT TRỜI hoặc đèn pha chính,
                // đèn phụ không đủ mạnh để xuyên qua thịt.
                half thickness = SAMPLE_TEXTURE2D(_ThicknessMap, sampler_ThicknessMap, IN.uv).r;
                half backLit = pow(saturate(dot(viewDirWS, -mainLight.direction)), _TransmissionPower);
                color += _TransmissionColor.rgb * _TransmissionStrength
                         * backLit * (1.0h - thickness) * mainLight.color * mainLight.distanceAttenuation;
            #endif

                // ── Đèn phụ ─────────────────────────────────────────────────────────────────
            #ifdef _ADDITIONAL_LIGHTS
                uint additionalLightCount = GetAdditionalLightsCount();

                LIGHT_LOOP_BEGIN(additionalLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS);

                    half atten = light.shadowAttenuation * light.distanceAttenuation;
                    float ndotl = dot(normalWS, light.direction);
                    half3 radiance = light.color * atten;

                    color += albedo * SkinDiffuse(ndotl, curvature) * radiance;
                    color += SkinSpecular(normalWS, viewDirWS, half3(light.direction)) * radiance
                             * saturate(ndotl);
                LIGHT_LOOP_END
            #endif

                // ── Hắt trời ────────────────────────────────────────────────────────────────
                color += albedo * SampleSH(normalWS);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SkinShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Back
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SkinShadowVertex
            #pragma fragment SkinShadowFragment

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW   // 2

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings SkinShadowVertex(ShadowAttributes IN)
            {
                ShadowVaryings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 SkinShadowFragment(ShadowVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "SkinDepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SkinDepthVertex
            #pragma fragment SkinDepthFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes { float3 positionOS : POSITION; };
            struct DepthVaryings  { float4 positionCS : SV_POSITION; };

            DepthVaryings SkinDepthVertex(DepthAttributes IN)
            {
                DepthVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                return OUT;
            }

            half4 SkinDepthFragment(DepthVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // KHÔNG có pass DepthNormals: nó chỉ cần cho SSAO và decal màn hình, mà T32 đã cấm SSAO
        // ở mọi bậc và dự án không dùng decal. Thêm pass đó là thêm biến thể phải biên dịch ở
        // màn hình đầu tiên để đổi lấy đúng không gì.
    }

    Fallback Off
}
