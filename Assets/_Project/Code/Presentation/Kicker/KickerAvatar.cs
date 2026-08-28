// KickerAvatar.cs
// Nhân vật người sút dạng greybox — dựng hoàn toàn bằng primitive Unity,
// không dùng Animator, không dùng model hay asset ngoài.
// Assembly: Eleven.Presentation (tham chiếu Unity.Mathematics).

using Eleven.Match;
using Eleven.Presentation.Kicker;
using Eleven.Shooter;
using UnityEngine;
using Unity.Mathematics;

namespace Eleven.Presentation
{
    [DisallowMultipleComponent]
    public sealed class KickerAvatar : MonoBehaviour, IKickerAnimator
    {
        // ─── Hằng số vị trí chạy đà ───────────────────────────────────────────
        // Người sút xuất phát từ phía sau-trái bóng, kết thúc cạnh bóng bên trái
        static readonly float3 k_StartPos  = new float3(-0.9f,  0f, -2.6f);
        static readonly float3 k_PlantPos  = new float3(-0.35f, 0f, -0.15f);

        // ─── Offset từng khớp (tính từ cha trực tiếp) ─────────────────────────
        const float k_HipsY       = 0.92f;   // hông cách mặt đất
        const float k_SpineLen    = 0.42f;   // thân trên
        const float k_HeadRadius  = 0.11f;   // bán kính đầu
        const float k_ThighLen    = 0.40f;   // chiều dài đùi
        const float k_ShinLen     = 0.38f;   // chiều dài cẳng chân
        const float k_FootLen     = 0.24f;   // chiều dài bàn chân

        // ─── Properties API (hợp đồng bất biến) ───────────────────────────────
        public Transform Root      { get; private set; }
        public Transform Hips      { get; private set; }
        public Transform PlantFoot { get; private set; }
        public Transform KickFoot  { get; private set; }

        // ─── Cache toàn bộ transform khớp để tránh GetComponent trong Tick ────
        Transform _spine;
        Transform _head;

        Transform _lUpperArm;
        Transform _lForeArm;
        Transform _rUpperArm;
        Transform _rForeArm;

        // Chân TRÁI = chân trụ (PlantFoot), Chân PHẢI = chân sút (KickFoot)
        Transform _lThigh;
        Transform _lShin;
        Transform _lFoot;   // = PlantFoot
        Transform _rThigh;
        Transform _rShin;
        Transform _rFoot;   // = KickFoot

        // ─── Materials cache ───────────────────────────────────────────────────
        Material _matBody;   // áo xanh dương đậm
        Material _matPants;  // quần trắng
        Material _matSkin;   // da mặt/tay
        Material _matShoe;   // giày đen

        // ─── Trạng thái nội bộ ────────────────────────────────────────────────
        bool _built;

        // ══════════════════════════════════════════════════════════════════════
        // BUILD
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Dựng các khối primitive tạo thành nhân vật greybox.
        /// Idempotent: gọi 2 lần không nhân đôi khối.
        /// </summary>
        public void BuildGreybox()
        {
            // Nếu hierarchy đã tồn tại thì chỉ nối lại tham chiếu
            Transform existingHips = transform.Find("Hips");
            if (existingHips != null)
            {
                Root = transform;
                _ReconnectRefs();
                _built = true;
                return;
            }

            Root = transform;

            // Khởi tạo vật liệu dùng chung — tránh cấp phát lại
            _CreateMaterials();

            // ── Hips (gốc toàn bộ skeleton) ───────────────────────────────────
            Hips = _NewNode("Hips", transform,
                new Vector3(0f, k_HipsY, 0f));

            // ── Thân trên (Capsule) ────────────────────────────────────────────
            // Capsule Unity cao 2 đơn vị mặc định → scale y để khớp chiều dài
            _spine = _NewPrimitive("Spine", PrimitiveType.Capsule, Hips,
                Vector3.zero,
                new Vector3(0.26f, k_SpineLen * 0.5f, 0.16f),
                _matBody);

            // ── Đầu (Sphere) ───────────────────────────────────────────────────
            _head = _NewPrimitive("Head", PrimitiveType.Sphere, _spine,
                new Vector3(0f, k_SpineLen + k_HeadRadius, 0f),
                Vector3.one * (k_HeadRadius * 2f),
                _matSkin);

            // ── Cánh tay TRÁI ──────────────────────────────────────────────────
            _lUpperArm = _NewPrimitive("L_UpperArm", PrimitiveType.Capsule, _spine,
                new Vector3(-0.30f, k_SpineLen * 0.7f, 0f),
                new Vector3(0.09f, 0.18f, 0.09f),
                _matSkin);
            _lForeArm = _NewPrimitive("L_ForeArm", PrimitiveType.Capsule, _lUpperArm,
                new Vector3(0f, -0.36f, 0f),
                new Vector3(0.08f, 0.16f, 0.08f),
                _matSkin);

            // ── Cánh tay PHẢI ─────────────────────────────────────────────────
            _rUpperArm = _NewPrimitive("R_UpperArm", PrimitiveType.Capsule, _spine,
                new Vector3(0.30f, k_SpineLen * 0.7f, 0f),
                new Vector3(0.09f, 0.18f, 0.09f),
                _matSkin);
            _rForeArm = _NewPrimitive("R_ForeArm", PrimitiveType.Capsule, _rUpperArm,
                new Vector3(0f, -0.36f, 0f),
                new Vector3(0.08f, 0.16f, 0.08f),
                _matSkin);

            // ── Chân TRÁI (trụ) — hierarchy: Hips → LThigh → LShin → LFoot ───
            _lThigh = _NewPrimitive("L_Thigh", PrimitiveType.Capsule, Hips,
                new Vector3(-0.12f, -k_ThighLen * 0.5f, 0f),
                new Vector3(0.14f, k_ThighLen * 0.5f, 0.14f),
                _matPants);
            _lShin = _NewPrimitive("L_Shin", PrimitiveType.Capsule, _lThigh,
                new Vector3(0f, -k_ThighLen, 0f),
                new Vector3(0.11f, k_ShinLen * 0.5f, 0.11f),
                _matPants);
            _lFoot = _NewPrimitive("L_Foot", PrimitiveType.Cube, _lShin,
                new Vector3(0f, -k_ShinLen - 0.04f, 0.05f),
                new Vector3(0.12f, 0.06f, k_FootLen),
                _matShoe);
            PlantFoot = _lFoot;

            // ── Chân PHẢI (sút) — hierarchy: Hips → RThigh → RShin → RFoot ───
            _rThigh = _NewPrimitive("R_Thigh", PrimitiveType.Capsule, Hips,
                new Vector3(0.12f, -k_ThighLen * 0.5f, 0f),
                new Vector3(0.14f, k_ThighLen * 0.5f, 0.14f),
                _matPants);
            _rShin = _NewPrimitive("R_Shin", PrimitiveType.Capsule, _rThigh,
                new Vector3(0f, -k_ThighLen, 0f),
                new Vector3(0.11f, k_ShinLen * 0.5f, 0.11f),
                _matPants);
            _rFoot = _NewPrimitive("R_Foot", PrimitiveType.Cube, _rShin,
                new Vector3(0f, -k_ShinLen - 0.04f, 0.05f),
                new Vector3(0.12f, 0.06f, k_FootLen),
                _matShoe);
            KickFoot = _rFoot;

            _built = true;
        }

        // ══════════════════════════════════════════════════════════════════════
        // API CHUYỂN ĐỘNG
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Đặt lại về điểm bắt đầu chạy đà, tư thế đứng thẳng.
        /// ballPosition dùng để tham khảo hướng nhìn (luôn +Z nên không cần tính).
        /// </summary>
        public void ResetToStart(float3 ballPosition)
        {
            if (!_built) BuildGreybox();

            // Đặt vị trí gốc người sút
            transform.position = new Vector3(k_StartPos.x, k_StartPos.y, k_StartPos.z);
            transform.rotation = Quaternion.identity; // luôn nhìn +Z

            // Trả toàn bộ khớp về trung lập
            _ResetJoints();
        }

        /// <summary>
        /// Xoay hông/vai theo hướng ngắm khi người chơi kéo để chọn góc sút.
        /// yawDeg bị kẹp trong [-35, 35] để trông hợp lý.
        /// </summary>
        public void SetAimYawDegrees(float yawDeg)
        {
            if (!_built) return;

            // Kẹp yaw để nhân vật không xoay quá mức
            float clamped = math.clamp(yawDeg, -35f, 35f);

            // Hips xoay theo hướng ngắm
            Hips.localRotation = Quaternion.Euler(0f, clamped, 0f);

            // Vai xoay ngược nhẹ để người trông tự nhiên hơn (counter-rotation)
            if (_spine != null)
                _spine.localRotation = Quaternion.Euler(0f, -clamped * 0.35f, 0f);
        }

        /// <summary>
        /// Idle: dao động thở nhẹ ~0.6 Hz để nhân vật không đứng cứng như tượng.
        /// Không dùng Random — chỉ dùng sin theo Time.time.
        /// </summary>
        public void TickIdle(float dt)
        {
            if (!_built) return;

            // Thở: thân nhấp nhô biên độ rất nhỏ
            float breath = math.sin(Time.time * (2f * math.PI * 0.6f)) * 0.008f;
            if (_spine != null)
                _spine.localRotation = Quaternion.Euler(breath * 30f, 0f, 0f);

            // Hông dập nhẹ theo nhịp thở
            if (Hips != null)
                Hips.localPosition = new Vector3(0f, k_HipsY + breath, 0f);

            // Tay đung đưa rất nhẹ ngược pha nhau
            float armSwing = math.sin(Time.time * (2f * math.PI * 0.6f)) * 5f;
            if (_lUpperArm != null)
                _lUpperArm.localRotation = Quaternion.Euler(armSwing, 0f, -30f);
            if (_rUpperArm != null)
                _rUpperArm.localRotation = Quaternion.Euler(-armSwing, 0f, 30f);
        }

        /// <summary>
        /// Chạy đà: t01 từ 0 (xuất phát) đến 1 (cạnh bóng).
        /// Ease-in bằng t*t, chân sải bước luân phiên bằng sin.
        /// Ở t01 = 1, chân trụ (trái) nằm ~0.35m bên trái bóng.
        /// </summary>
        public void TickRunUp(float t01)
        {
            if (!_built) return;

            // Ease-in: tăng tốc khi bắt đầu, đều hơn khi gần bóng
            float tEase = t01 * t01;

            // Nội suy vị trí người sút từ điểm xuất phát đến cạnh bóng
            float3 pos = math.lerp(k_StartPos, k_PlantPos, tEase);
            transform.position = new Vector3(pos.x, pos.y, pos.z);

            // Tốc độ sải bộ: 3 chu kỳ trong toàn bộ chạy đà
            float stridePhase = t01 * 3f * 2f * math.PI;

            // ── Chân TRÁI (trụ): sải bộ thường ─────────────────────────────
            float lThighAngle = math.sin(stridePhase) * 35f;            // ±35° đùi
            float lShinBend   = math.max(0f, -math.sin(stridePhase)) * 40f; // gập gối khi ra sau
            if (_lThigh != null)
                _lThigh.localRotation = Quaternion.Euler(lThighAngle, 0f, 0f);
            if (_lShin != null)
                _lShin.localRotation  = Quaternion.Euler(-lShinBend, 0f, 0f);

            // ── Chân PHẢI (sút): ở nửa cuối vung ngày càng ra sau ───────────
            // Nửa đầu: sải bộ bình thường (sin đối pha với chân trái)
            // Nửa cuối: vung ra sau chuẩn bị đá
            float rPhase = stridePhase + math.PI; // đối pha
            float rNormal = math.sin(rPhase) * 35f;

            // Hệ số vung ra sau: tăng mạnh ở t01 > 0.5
            float windupFactor = math.max(0f, (t01 - 0.5f) * 2f); // 0→1 ở nửa sau
            float windupAngle  = windupFactor * 55f;               // vung tối đa 55°

            // Kết hợp sải bộ thường và vung ra sau
            float rThighAngle = math.lerp(rNormal, windupAngle, windupFactor);
            float rShinBend   = windupFactor * 30f; // gập gối khi vung ra sau

            if (_rThigh != null)
                _rThigh.localRotation = Quaternion.Euler(rThighAngle, 0f, 0f);
            if (_rShin != null)
                _rShin.localRotation  = Quaternion.Euler(-rShinBend, 0f, 0f);

            // ── Thân hơi nghiêng về trước khi chạy ──────────────────────────
            float leanAngle = math.lerp(5f, 15f, tEase); // nghiêng 5→15° khi tăng tốc
            if (_spine != null)
                _spine.localRotation = Quaternion.Euler(-leanAngle, 0f, 0f);

            // ── Tay vung đối pha với chân để cân bằng ────────────────────────
            float armPhase = math.sin(stridePhase) * 40f;
            if (_lUpperArm != null)
                _lUpperArm.localRotation = Quaternion.Euler(-armPhase, 0f, -20f);
            if (_rUpperArm != null)
                _rUpperArm.localRotation = Quaternion.Euler(armPhase, 0f, 20f);
            if (_lForeArm != null)
                _lForeArm.localRotation  = Quaternion.Euler(math.max(0f, -armPhase) * 0.6f, 0f, 0f);
            if (_rForeArm != null)
                _rForeArm.localRotation  = Quaternion.Euler(math.max(0f, armPhase) * 0.6f, 0f, 0f);
        }

        /// <summary>
        /// Pha đá: chân sút quét từ sau (+55°) ra trước (-35°).
        /// Điểm tiếp xúc bóng xảy ra ở t01 ≈ 0.35.
        /// Sau đó là theo đà (follow-through sơ bộ).
        /// </summary>
        public void TickStrike(float t01)
        {
            if (!_built) return;

            // Góc đùi chân sút: từ +55° (vung sau) → -35° (theo đà trước)
            // Dùng lerp tuyến tính để cảm giác quét mạnh và nhất quán
            float thighAngle = math.lerp(55f, -35f, t01);

            // Cẳng chân: gập mạnh ở đầu pha rồi duỗi ra khi tiếp xúc (t01~0.35)
            // Hàm parabola: đỉnh gập ở t=0, duỗi hoàn toàn sau t=0.35
            float shinUnfold = math.saturate(t01 / 0.35f); // 0→1 trong khoảng tiếp xúc
            float shinBend   = math.lerp(45f, 0f, shinUnfold);

            if (_rThigh != null)
                _rThigh.localRotation = Quaternion.Euler(thighAngle, 0f, 0f);
            if (_rShin != null)
                _rShin.localRotation  = Quaternion.Euler(-shinBend, 0f, 0f);

            // Chân trụ (trái): giữ vững, hơi gập gối để hấp thụ lực
            if (_lThigh != null)
                _lThigh.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            if (_lShin != null)
                _lShin.localRotation  = Quaternion.Euler(-15f, 0f, 0f);

            // Thân xoay nhẹ theo chiều đá (xoay quanh Y cùng chiều chân phải)
            float bodyTwist = math.lerp(0f, 12f, t01);
            if (_spine != null)
                _spine.localRotation = Quaternion.Euler(-10f, -bodyTwist, 0f);

            // Tay trái vươn ra để giữ thăng bằng khi đá mạnh
            if (_lUpperArm != null)
                _lUpperArm.localRotation = Quaternion.Euler(-30f, 0f, -60f);
            // Tay phải về sau theo quán tính
            if (_rUpperArm != null)
                _rUpperArm.localRotation = Quaternion.Euler(40f, 0f, 20f);
        }

        /// <summary>
        /// Sau khi sút: giữ tư thế cuối, từ từ hạ chân về trung lập.
        /// dt dùng để nội suy về neutral theo thời gian thực.
        /// </summary>
        public void TickFollowThrough(float dt)
        {
            if (!_built) return;

            // Tốc độ về tư thế trung lập — đủ chậm để thấy được follow-through
            const float returnSpeed = 1.2f;
            float t = dt * returnSpeed;

            // Lerp từng khớp về neutral (Quaternion.identity = không xoay)
            if (_rThigh != null)
                _rThigh.localRotation = Quaternion.Slerp(
                    _rThigh.localRotation, Quaternion.Euler(-20f, 0f, 0f), t);
            if (_rShin != null)
                _rShin.localRotation = Quaternion.Slerp(
                    _rShin.localRotation, Quaternion.identity, t);
            if (_lThigh != null)
                _lThigh.localRotation = Quaternion.Slerp(
                    _lThigh.localRotation, Quaternion.identity, t);
            if (_lShin != null)
                _lShin.localRotation = Quaternion.Slerp(
                    _lShin.localRotation, Quaternion.identity, t);
            if (_spine != null)
                _spine.localRotation = Quaternion.Slerp(
                    _spine.localRotation, Quaternion.Euler(-5f, 0f, 0f), t);
            if (_lUpperArm != null)
                _lUpperArm.localRotation = Quaternion.Slerp(
                    _lUpperArm.localRotation, Quaternion.Euler(0f, 0f, -20f), t);
            if (_rUpperArm != null)
                _rUpperArm.localRotation = Quaternion.Slerp(
                    _rUpperArm.localRotation, Quaternion.Euler(0f, 0f, 20f), t);
        }

        // ══════════════════════════════════════════════════════════════════════
        // INTERNAL HELPERS — không gọi trong Tick (không cấp phát runtime)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Tạo vật liệu dùng URP Unlit nếu có, fallback "Unlit/Color", "Sprites/Default".
        /// Set cả _BaseColor lẫn _Color để tương thích nhiều shader version.
        /// </summary>
        void _CreateMaterials()
        {
            _matBody  = _MakeMat(new Color(0.10f, 0.28f, 0.75f)); // áo xanh dương đậm
            _matPants = _MakeMat(Color.white);                     // quần trắng
            _matSkin  = _MakeMat(new Color(0.86f, 0.68f, 0.55f)); // màu da
            _matShoe  = _MakeMat(Color.black);                     // giày đen
        }

        /// <summary>
        /// Tạo một Material với màu chỉ định.
        /// Thử URP Unlit trước vì dự án dùng URP; fallback để không bị magenta.
        /// </summary>
        static Material _MakeMat(Color color)
        {
            // Ưu tiên URP Unlit
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null)
                sh = Shader.Find("Unlit/Color");
            if (sh == null)
                sh = Shader.Find("Sprites/Default");

            var mat = new Material(sh);

            // Set cả 2 property để tương thích URP lẫn built-in
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            return mat;
        }

        /// <summary>
        /// Tạo GameObject trống làm node khớp (không có MeshRenderer).
        /// </summary>
        static Transform _NewNode(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        /// <summary>
        /// Tạo primitive, xoá Collider, gán Material và scale.
        /// Phải xoá Collider ngay sau CreatePrimitive vì game tự xử lý va chạm bằng toán học.
        /// </summary>
        static Transform _NewPrimitive(
            string name, PrimitiveType type, Transform parent,
            Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;

            // Xoá Collider — dùng DestroyImmediate trong Editor, Destroy trong Play mode
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(col);
                else
                    Object.DestroyImmediate(col);
            }

            // Gán vật liệu
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sharedMaterial = mat;

            return go.transform;
        }

        /// <summary>
        /// Nối lại tham chiếu sau khi phát hiện hierarchy đã tồn tại (idempotent).
        /// Dùng Find() một lần duy nhất khi build, không gọi trong Tick.
        /// </summary>
        void _ReconnectRefs()
        {
            Hips       = transform.Find("Hips");
            _spine     = Hips?.Find("Spine");
            _head      = _spine?.Find("Head");

            _lUpperArm = _spine?.Find("L_UpperArm");
            _lForeArm  = _lUpperArm?.Find("L_ForeArm");
            _rUpperArm = _spine?.Find("R_UpperArm");
            _rForeArm  = _rUpperArm?.Find("R_ForeArm");

            _lThigh    = Hips?.Find("L_Thigh");
            _lShin     = _lThigh?.Find("L_Shin");
            _lFoot     = _lShin?.Find("L_Foot");
            PlantFoot  = _lFoot;

            _rThigh    = Hips?.Find("R_Thigh");
            _rShin     = _rThigh?.Find("R_Shin");
            _rFoot     = _rShin?.Find("R_Foot");
            KickFoot   = _rFoot;
        }

        /// <summary>
        /// Đặt toàn bộ khớp về góc trung lập — dùng trong ResetToStart.
        /// </summary>
        void _ResetJoints()
        {
            if (Hips != null)
                Hips.localPosition = new Vector3(0f, k_HipsY, 0f);

            // Đặt từng khớp về identity để tư thế đứng thẳng
            var joints = new Transform[]
            {
                Hips, _spine, _head,
                _lUpperArm, _lForeArm,
                _rUpperArm, _rForeArm,
                _lThigh, _lShin, _lFoot,
                _rThigh, _rShin, _rFoot
            };
            foreach (var j in joints)
            {
                if (j != null)
                    j.localRotation = Quaternion.identity;
            }

            // Tư thế đứng tự nhiên: tay dọc thân hơi dang ra
            if (_lUpperArm != null)
                _lUpperArm.localRotation = Quaternion.Euler(0f, 0f, -20f);
            if (_rUpperArm != null)
                _rUpperArm.localRotation = Quaternion.Euler(0f, 0f, 20f);
        }

        // ══════════════════════════════════════════════════════════════════════
        // IKickerAnimator — bộ chuyển tiếp (T35)
        //
        // Greybox và model thật đi CHUNG một interface thay vì để MatchGameLoop rẽ nhánh
        // "nếu có model thì…". Nhánh đó là chỗ hai đường đi lặng lẽ phân kỳ: sửa nhịp ở một
        // bên, quên bên kia, rồi tháng sau không ai nhớ bên nào mới đúng.
        //
        // Chọn clip do KickerClipSelector lo — cùng một hàm, cùng bộ kiểm thử với
        // MecanimKickerAnimator. Ở đây chỉ dịch clip đã chọn ra các Tick* thủ công sẵn có.
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Cú đá greybox quét trong 0.45 s và chạm bóng ở t01 ≈ 0.35 — xem
        /// <see cref="TickStrike"/>. Hai số này định ra thời điểm bật clip sút.</summary>
        const float k_StrikeSeconds = 0.45f;
        const float k_StrikeContactNorm = 0.35f;

        KickerClipSelector _selector;
        KickPhase _phase = KickPhase.Complete;
        KickerClip _clip = KickerClip.Idle;
        float _strikeElapsed;

        /// <summary>Quả cầu "Head" của greybox — máy quay cận mặt ở pha phản ứng nhắm vào đây.</summary>
        Transform IKickerAnimator.Head => _head;

        KickerClip IKickerAnimator.CurrentClip => _clip;

        float IKickerAnimator.NormalizedTime => math.saturate(_strikeElapsed / k_StrikeSeconds);

        float IKickerAnimator.ContactNormalizedTime
            => _clip >= KickerClip.StrikeInstep && _clip <= KickerClip.StrikeKnuckle
                ? k_StrikeContactNorm : 0f;

        void IKickerAnimator.PrepareFor(ShotType type) => _selector.PrepareFor(type);

        void IKickerAnimator.SetOutcome(KickResult result) => _selector.SetOutcome(result);

        void IKickerAnimator.OnPhaseChanged(KickPhase oldPhase, KickPhase newPhase)
        {
            if (newPhase == KickPhase.Placing)
            {
                _selector.Reset();
                _strikeElapsed = 0f;
            }
            _phase = newPhase;
        }

        void IKickerAnimator.Tick(float dt, float phaseProgress01)
        {
            float remaining = _phase == KickPhase.RunUp
                ? math.max(0f, (1f - math.saturate(phaseProgress01)) * _runUpDuration)
                : 0f;

            var next = _selector.Resolve(_phase, remaining, k_StrikeSeconds * k_StrikeContactNorm);
            bool isStrike = next >= KickerClip.StrikeInstep && next <= KickerClip.StrikeKnuckle;

            if (next != _clip)
            {
                _clip = next;
                if (isStrike)
                {
                    _strikeElapsed = 0f;
                    _selector.LockStrike();
                }
            }

            switch (next)
            {
                case KickerClip.Idle:
                    TickIdle(dt);
                    break;
                case KickerClip.RunUp:
                    TickRunUp(math.saturate(phaseProgress01));
                    break;
                default:
                    if (isStrike)
                    {
                        _strikeElapsed += dt;
                        if (_strikeElapsed <= k_StrikeSeconds)
                        {
                            TickStrike(math.saturate(_strikeElapsed / k_StrikeSeconds));
                            break;
                        }
                    }
                    // Ăn mừng/cúi đầu chưa có tư thế riêng cho greybox; về trung lập là
                    // trung thực hơn đứng đơ giữa cú vung.
                    TickFollowThrough(dt);
                    break;
            }
        }

        float _runUpDuration = 0.90f;

        /// <summary>Đồng bộ với nhịp trận đang chạy. Không có nó thì clip sút bật sai lúc
        /// mỗi khi ai đó chỉnh <c>KickPhaseDurations.runUp</c>.</summary>
        public void SetRunUpDuration(float seconds) => _runUpDuration = math.max(0.01f, seconds);

    }
}
