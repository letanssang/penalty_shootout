// Cỏ instanced (T29).
//
// ⚠️ Đây là shader rủi ro GPU cao nhất của dự án. Ba thứ giết hiệu năng cỏ trên di động,
// theo đúng thứ tự nặng dần:
//   1. OVERDRAW — mỗi lớp cỏ chồng lên nhau là một lần đọc-ghi tile trên kiến trúc TBDR.
//      Nhìn là là mặt sân thì mười lớp cỏ nằm sau nhau trên cùng một điểm ảnh là chuyện thường.
//   2. ALPHA CLIP — clip() giết early-Z của cả tile trên Mali và Adreno. Toàn bộ pixel phía
//      sau mất quyền bị loại sớm.
//   3. BĂNG THÔNG ĐỈNH — 24.000 túm × 8 đỉnh = 192.000 đỉnh, mỗi đỉnh đọc 32 byte instance.
//
// Vì thế ba công tắc dưới đây là KEYWORD chứ không phải nhánh if: phải đo được tám tổ hợp
// thật (bảng tám dòng của ô nghiệm thu), mà nhánh if thì vẫn trả tiền cho cả hai phía.
//
// Dùng multi_compile chứ KHÔNG dùng shader_feature: shader_feature bị lược khi build nếu
// không vật liệu nào bật keyword đó, và cả bốn biến thể ở đây được bật lúc chạy để đo —
// lược mất là lúc đo trong build sẽ ra số của biến thể khác.
//
// Công tắc đổ bóng KHÔNG phải keyword: nó là ShadowCastingMode trên lệnh vẽ phía C#.
// Pass ShadowCaster vẫn phải tồn tại thì mới bật lên đo được dòng "bóng+".
//
// Cách vẽ phía C#: Graphics.RenderMeshPrimitives với mesh là một túm chữ thập gồm hai tấm
// vuông cắt nhau (x, z ∈ [-0.5, 0.5], y ∈ [0, 1], gốc ở chân), instanceCount = GrassField.VisibleInstanceCount.
Shader "Eleven/Grass"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture túm cỏ", 2D) = "white" {}
        _Cutoff ("Ngưỡng cắt alpha", Range(0.0, 1.0)) = 0.35
        _ColorYoung ("Màu cỏ non (tuyến tính)", Color) = (0.24, 0.42, 0.14, 1)
        _ColorMature ("Màu cỏ già (tuyến tính)", Color) = (0.13, 0.28, 0.09, 1)
        _RootOcclusion ("Độ tối ở gốc túm", Range(0.0, 1.0)) = 0.55
        _WidthScale ("Bề ngang túm / chiều cao", Range(0.5, 3.0)) = 1.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Phải khớp từng trường với Eleven.Presentation.Grass.GrassInstanceGpu (32 byte).
        struct GrassInstanceGpu
        {
            float4 positionYaw;   // xyz = gốc túm (y = 0), w = yaw (radian)
            float4 shape;         // x = height (m), y = bend, z = windPhase [0,1), w = tint01
        };

        StructuredBuffer<GrassInstanceGpu> _GrassInstances;

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _ColorYoung;
            float4 _ColorMature;
            float  _Cutoff;
            float  _RootOcclusion;
            float  _WidthScale;
        CBUFFER_END

        // Do CPU đặt mỗi khung hình. Dùng đồng hồ riêng của GrassField chứ không đọc _Time:
        // replay (T27) phải phát lại đúng từng nhịp cỏ như lúc ghi.
        float  _GrassWindTime;        // = GrassField.WindTime
        float4 _GrassWindParams;      // xy = hướng gió (đã chuẩn hoá, mặt phẳng XZ), z = tần số không gian, w = tần số thời gian
        float  _GrassWindStrength;    // biên độ, tính theo tỉ lệ chiều cao túm

        // Đưa một đỉnh của mesh túm chữ thập ra không gian thế giới.
        // Quy ước mesh: x, z ∈ [-0.5, 0.5] · y ∈ [0, 1] · gốc toạ độ ở CHÂN túm.
        float3 GrassTransformVertex(GrassInstanceGpu inst, float3 positionOS)
        {
            float3 root   = inst.positionYaw.xyz;
            float  yaw    = inst.positionYaw.w;
            float  height = inst.shape.x;
            float  bend   = inst.shape.y;
            float  phase  = inst.shape.z;

            float s, c;
            sincos(yaw, s, c);

            // Xoay quanh Y, giữ nguyên chiều cao.
            float3 local = float3(positionOS.x * c + positionOS.z * s,
                                  positionOS.y,
                                  -positionOS.x * s + positionOS.z * c);

            float width = height * _WidthScale;
            float3 positionWS = root + float3(local.x * width, local.y * height, local.z * width);

            // Trọng số ngả: bậc hai theo độ cao. Gốc túm dính chặt mặt sân, ngọn ngả nhiều —
            // ngả tuyến tính làm cả túm trượt đi như bị kéo lê.
            float lean = positionOS.y * positionOS.y;

            float2 windDir = _GrassWindParams.xy;

            // Ngả sẵn: không túm nào trên sân thật đứng thẳng tuyệt đối.
            positionWS.xz += windDir * (bend * height * lean * 0.5);

        #ifdef _GRASS_WIND
            // Một sóng chạy qua sân (tần số không gian) cộng pha riêng của từng túm. Thiếu
            // thành phần không gian thì cả sân lượn cùng một nhịp và trông như tấm vải.
            float wave = sin(_GrassWindTime * _GrassWindParams.w
                             + phase * 6.2831853
                             + dot(root.xz, windDir) * _GrassWindParams.z);

            // Nhịp phụ lệch tần để chuyển động không tuần hoàn thấy rõ.
            float gust = sin(_GrassWindTime * _GrassWindParams.w * 0.37 + phase * 3.1415927) * 0.35;

            positionWS.xz += windDir * ((wave + gust) * _GrassWindStrength * height * lean);

            // Ngả thì phải thấp xuống: giữ nguyên độ cao khi ngả sẽ làm túm cỏ dài ra.
            float sway = abs(wave + gust) * _GrassWindStrength * lean;
            positionWS.y -= height * sway * sway * 0.5;
        #endif

            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "GrassForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Off            // tấm phẳng, nhìn từ mặt sau vẫn phải thấy
            Blend One Zero

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GrassVertex
            #pragma fragment GrassFragment

            #pragma multi_compile_local _ _GRASS_ALPHACLIP
            #pragma multi_compile_local_vertex _ _GRASS_WIND
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 color       : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float  occlusion   : TEXCOORD3;
            };

            Varyings GrassVertex(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;

                GrassInstanceGpu inst = _GrassInstances[instanceID];

                float3 positionWS = GrassTransformVertex(inst, IN.positionOS);

                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);

                OUT.color = lerp(_ColorYoung.rgb, _ColorMature.rgb, inst.shape.w);

                // Che khuất ở gốc túm: cỏ tự che nhau ở chân. Một phép nội suy theo độ cao,
                // rẻ hơn bất kỳ cách nào khác và là thứ duy nhất làm mặt sân có chiều sâu.
                OUT.occlusion = lerp(1.0 - _RootOcclusion, 1.0, IN.positionOS.y);

                return OUT;
            }

            half4 GrassFragment(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

            #ifdef _GRASS_ALPHACLIP
                // Cắt sớm, trước mọi phép tính sáng. Lưu ý clip() làm mất early-Z của tile —
                // đó chính là cái giá mà dòng "clip+/clip-" của bảng tám dòng đo ra.
                clip(albedo.a - _Cutoff);
            #endif

                // Pháp tuyến hướng lên: túm cỏ là tấm phẳng, dùng pháp tuyến thật của tấm sẽ
                // tạo một dải sáng chạy ngang sân mỗi khi camera quay. Cỏ ngắn 8cm thì hướng
                // lên là xấp xỉ đúng, và nó không đổi theo góc nhìn.
                float3 normalWS = float3(0.0, 1.0, 0.0);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half ndotl = saturate(dot(normalWS, mainLight.direction));

                // Hắt xuyên lá: cỏ mỏng, ánh sáng lọt qua. Một hằng số cộng thêm, không phải
                // tán xạ thật — đủ để cỏ không thành mảng đen khi mặt trời ở sau.
                half transmission = 0.25h * (1.0h - ndotl);

                half3 lighting = mainLight.color * (ndotl + transmission) * mainLight.shadowAttenuation
                               + SampleSH(normalWS);

                half3 color = albedo.rgb * IN.color * lighting * IN.occlusion;

                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Pass này tồn tại để bật lên ĐO được dòng "bóng+" của bảng tám dòng. Mặc định mọi bậc
        // đều vẽ cỏ với ShadowCastingMode.Off (xem GrassTierSettings): bóng của cỏ cao 8cm
        // không ai nhìn thấy, mà 24.000 túm ghi vào shadow map thì thấy ngay trên đồng hồ.
        Pass
        {
            Name "GrassShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GrassShadowVertex
            #pragma fragment GrassShadowFragment

            #pragma multi_compile_local _ _GRASS_ALPHACLIP
            #pragma multi_compile_local_vertex _ _GRASS_WIND
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            ShadowVaryings GrassShadowVertex(ShadowAttributes IN, uint instanceID : SV_InstanceID)
            {
                ShadowVaryings OUT;

                GrassInstanceGpu inst = _GrassInstances[instanceID];
                float3 positionWS = GrassTransformVertex(inst, IN.positionOS);

                float3 normalWS = float3(0.0, 1.0, 0.0);

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                OUT.positionCS.z = min(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                OUT.positionCS.z = max(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 GrassShadowFragment(ShadowVaryings IN) : SV_Target
            {
            #ifdef _GRASS_ALPHACLIP
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a;
                clip(alpha - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
