// Khán giả impostor (T30).
//
// MỘT atlas, MỘT draw call cho toàn bộ khán đài. Dữ liệu từng người nằm trong một
// StructuredBuffer duy nhất, đọc bằng SV_InstanceID — không dùng UNITY_INSTANCING_BUFFER
// (giới hạn ~500 instance mỗi batch trên nhiều thiết bị di động, tức là khán đài 2000 người
// sẽ tự động tách thành 4+ draw call mà không báo gì).
//
// Cách vẽ phía C#: Graphics.RenderPrimitives / RenderMeshPrimitives với mesh là một tấm vuông
// đơn vị (x ∈ [-0.5, 0.5], y ∈ [0, 1], gốc ở chân) và instanceCount = tổng số ghế.
//
// YÊU CẦU NỀN TẢNG: StructuredBuffer đọc trong vertex shader cần shader model 4.5 —
// Vulkan, Metal và GLES 3.1 đều có; GLES 3.0 thì KHÔNG. Dự án build Android bằng Vulkan
// (tools/build.sh) nên điều kiện này thoả. Nếu có ngày phải hạ xuống GLES 3.0, đường lui là
// nhét dữ liệu instance vào một texture float và tra bằng vertex texture fetch.
Shader "Eleven/CrowdImpostor"
{
    Properties
    {
        [MainTexture] _BaseMap ("Atlas khán giả (8 cột × 4 hàng)", 2D) = "white" {}
        _Cutoff ("Ngưỡng cắt alpha", Range(0.0, 1.0)) = 0.5
        _AmbientBoost ("Bù sáng môi trường", Range(0.0, 2.0)) = 1.0
        _ShadeStrength ("Độ đậm bóng theo hàng ghế", Range(0.0, 1.0)) = 0.35
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

        Pass
        {
            Name "CrowdForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Off            // tấm phẳng, nhìn từ sau vẫn phải thấy
            Blend One Zero

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex CrowdVertex
            #pragma fragment CrowdFragment

            // Khán đài KHÔNG nhận bóng đổ, KHÔNG dùng đèn phụ, KHÔNG lightmap: mỗi keyword
            // ở đây là một biến thể shader phải biên dịch và một nhánh phải chạy cho vài
            // nghìn instance. Ánh sáng của họ là một hằng số cộng với hắt trời — ở khoảng
            // cách 15m không ai phân biệt được, mà giá thì rẻ hơn hàng chục lần.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Phải khớp từng trường với Eleven.Presentation.Crowd.CrowdInstanceGpu (48 byte).
            struct CrowdInstanceGpu
            {
                float4 positionScale;   // xyz = chân, w = chiều cao (m)
                float4 phaseSpeed;      // x = pha [0,1), y = nhịp, zw = trống
                float4 tint;            // rgb = màu áo tuyến tính
            };

            StructuredBuffer<CrowdInstanceGpu> _CrowdInstances;

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float  _Cutoff;
                float  _AmbientBoost;
                float  _ShadeStrength;
            CBUFFER_END

            // Do CPU đặt mỗi khung hình — cùng nguồn với CrowdDirector, không đọc _Time để
            // animation trong replay khớp tuyệt đối với lúc ghi.
            float  _CrowdTime;          // = CrowdDirector.AnimationTime
            float  _CrowdFps;           // = CrowdTierSettings.animationFps (0 = đứng yên)
            float  _CrowdMoodRow;       // = (int)CrowdMood
            float  _CrowdAnimated;      // 1 = có animation, 0 = bậc C tĩnh
            float4 _CrowdAtlasLayout;   // (cột, hàng, padding, chưa dùng)
            float  _CrowdQuadAspect;    // = CrowdBillboard.QuadAspect

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 tint       : TEXCOORD1;
                float  shade      : TEXCOORD2;
            };

            // Bản sao đúng công thức của Eleven.Presentation.Crowd.CrowdBillboard.
            // Chỉ xoay quanh Y: cross(up, viewDir) sẽ đổi dấu khi camera đi ngang qua đỉnh
            // đầu và lật ngược cả khán đài trong một khung hình.
            float CrowdYaw(float3 instancePos, float3 cameraPos)
            {
                float dx = cameraPos.x - instancePos.x;
                float dz = cameraPos.z - instancePos.z;

                // Camera thẳng đỉnh đầu: giữ hướng mặc định (nhìn về -Z, phía chấm 11m)
                // thay vì để atan2(0,0) trả giá trị tuỳ nền tảng.
                if (dx * dx + dz * dz < 1e-6)
                {
                    return 0.0;
                }
                return atan2(dx, dz);
            }

            int CrowdFrame(float phase01, float speedScale)
            {
                uint frameCount = (uint)_CrowdAtlasLayout.x;

                if (_CrowdAnimated < 0.5)
                {
                    return 0;   // bậc C: đứng yên ở khung 0
                }

                float frames = _CrowdTime * _CrowdFps * speedScale + phase01 * frameCount;

                // Chia dư trên uint chứ không trên int: trình biên dịch cảnh báo "integer modulus
                // may be much slower" — phép này chạy MỖI ĐỈNH cho vài nghìn instance.
                // Đầu vào không bao giờ âm (_CrowdTime >= 0, phase01 >= 0, speedScale > 0) nên
                // kẹp về 0 là đủ an toàn, và kết quả khớp từng khung với CrowdDirector.GetFrame.
                uint frame = ((uint)max(0.0, floor(frames))) % frameCount;
                return (int)frame;
            }

            float4 CrowdCellUv(int moodRow, int frame)
            {
                float columns = _CrowdAtlasLayout.x;
                float rows    = _CrowdAtlasLayout.y;
                float padding = _CrowdAtlasLayout.z;

                float cellU = 1.0 / columns;
                float cellV = 1.0 / rows;

                return float4(frame * cellU + padding,
                              moodRow * cellV + padding,
                              cellU - 2.0 * padding,
                              cellV - 2.0 * padding);
            }

            Varyings CrowdVertex(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;

                CrowdInstanceGpu inst = _CrowdInstances[instanceID];

                float3 footPosition = inst.positionScale.xyz;
                float  height       = inst.positionScale.w;
                float  phase01      = inst.phaseSpeed.x;
                float  speedScale   = inst.phaseSpeed.y;

                float yaw = CrowdYaw(footPosition, GetCameraPositionWS());

                float s, c;
                sincos(yaw, s, c);
                float3 right = float3(c, 0.0, -s);
                float3 up    = float3(0.0, 1.0, 0.0);

                float3 positionWS = footPosition
                                  + right * (IN.positionOS.x * height * _CrowdQuadAspect)
                                  + up    * (IN.positionOS.y * height);

                OUT.positionCS = TransformWorldToHClip(positionWS);

                int frame = CrowdFrame(phase01, speedScale);
                float4 cell = CrowdCellUv((int)_CrowdMoodRow, frame);
                OUT.uv = cell.xy + IN.uv * cell.zw;

                OUT.tint = inst.tint.rgb;

                // Hàng ghế càng thấp càng bị khán đài phía trước che → tối dần. Một phép nội
                // suy tuyến tính theo độ cao, đủ để đám đông có chiều sâu mà không tốn gì.
                float rowDarkening = saturate(footPosition.y / 6.0);
                OUT.shade = lerp(1.0 - _ShadeStrength, 1.0, rowDarkening);

                return OUT;
            }

            half4 CrowdFragment(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                // Cắt sớm: pixel trong suốt của impostor chiếm quá nửa diện tích tấm bảng,
                // bỏ chúng trước khi tính sáng là phần tiết kiệm lớn nhất của shader này.
                clip(albedo.a - _Cutoff);

                Light mainLight = GetMainLight();

                // Ánh sáng phẳng có chủ ý: khán đài không có pháp tuyến thật (là tấm phẳng),
                // đánh bóng theo pháp tuyến tấm bảng sẽ ra một dải sáng chạy ngang khán đài
                // khi camera quay. Dùng cường độ đèn chính + hắt trời, không dùng hướng.
                half3 ambient = SampleSH(half3(0.0, 1.0, 0.0)) * _AmbientBoost;
                half3 lighting = mainLight.color * 0.35h + ambient;

                half3 color = albedo.rgb * IN.tint * lighting * IN.shade;

                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    // Không có pass ShadowCaster: khán đài không đổ bóng. Vài nghìn tấm phẳng ghi vào
    // shadow map là chi phí thuần lãng phí — bóng của họ đổ vào khán đài phía sau, chỗ
    // camera không bao giờ nhìn tới.
    Fallback Off
}
