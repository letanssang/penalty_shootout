using System;
using UnityEngine;
using Unity.Mathematics;
using Eleven.Ball;

namespace Eleven.Shooter
{
    /// <summary>
    /// Component nhận diện cử chỉ vuốt từ màn hình cảm ứng hoặc chuột trên PC.
    /// Chuyển đường vuốt thành vector vận tốc phóng và độ xoáy cho quả bóng.
    /// </summary>
    public sealed class TouchSwipeReceiver : MonoBehaviour
    {
        public event Action<float3, float3> OnShotFired; // (launchVelocity, spin)

        [Header("Tùy chỉnh lực và độ nhạy")]
        [SerializeField] private float minSpeed = 18f;
        [SerializeField] private float maxSpeed = 32f;
        [SerializeField] private float maxSwipeTime = 1.0f;
        [SerializeField] private float minSwipeDistPixels = 30f;

        private Vector2 startPos;
        private float startTime;
        private bool isSwiping;
        private bool isInputEnabled = true;

        public bool IsInputEnabled
        {
            get => isInputEnabled;
            set => isInputEnabled = value;
        }

        private void Update()
        {
            if (!isInputEnabled) return;

            // Xử lý Touch trên Mobile hoặc Chuột trên PC
            if (Input.GetMouseButtonDown(0))
            {
                startPos = Input.mousePosition;
                startTime = Time.time;
                isSwiping = true;
            }
            else if (Input.GetMouseButtonUp(0) && isSwiping)
            {
                isSwiping = false;
                Vector2 endPos = Input.mousePosition;
                float duration = Time.time - startTime;

                if (duration <= maxSwipeTime)
                {
                    ProcessSwipe(startPos, endPos, duration);
                }
            }
        }

        private void ProcessSwipe(Vector2 start, Vector2 end, float duration)
        {
            Vector2 delta = end - start;
            float dist = delta.magnitude;

            if (dist < minSwipeDistPixels) return; // Vuốt quá ngắn, bỏ qua

            // Chuẩn hóa theo kích thước màn hình
            float normX = delta.x / Screen.width;  // [-0.5 .. 0.5]
            float normY = delta.y / Screen.height; // [0.1 .. 1.0]

            // Tính toán hướng bay về phía khung thành (Z = 11m)
            // Khung thành rộng 7.32m (-3.66m đến +3.66m), cao 2.44m (0m đến 2.44m)
            float targetX = Mathf.Clamp(normX * 12.0f, -4.5f, 4.5f);
            float targetY = Mathf.Clamp(normY * 4.5f, 0.3f, 3.2f);

            // Vận tốc vuốt (vuốt càng nhanh bóng bay càng căng)
            float swipeSpeed = dist / (duration * Screen.dpi > 0 ? Screen.dpi : 160f); // inch/sec
            float tSpeed = Mathf.Clamp01((swipeSpeed - 5f) / 25f);
            float forwardSpeed = Mathf.Lerp(minSpeed, maxSpeed, tSpeed);

            // Vector vận tốc ban đầu (Bóng xuất phát tại (0, 0.11, 0) bay tới Z=11.0)
            float3 launchVel = new float3(
                targetX * (forwardSpeed / 11.0f),
                targetY * (forwardSpeed / 11.0f) + 1.2f, // bù trừ trọng lực
                forwardSpeed
            );

            // Tính toán độ xoáy (Spin) dựa trên độ lệch ngang và độ cong
            float spinZ = -normX * 8.0f * (float)Math.PI; // Xoáy ngang (Magnus effect)
            float spinX = (normY < 0.3f ? -4.0f : 2.0f) * (float)Math.PI; // Xoáy dọc
            float3 spin = new float3(spinX, spinZ, 0f);

            // Nếu vuốt rất nhanh và thẳng -> Knuckleball (xoáy ~ 0)
            if (Mathf.Abs(normX) < 0.03f && tSpeed > 0.75f)
            {
                spin = float3.zero;
            }

            isInputEnabled = false; // Khóa input khi bóng đang bay
            OnShotFired?.Invoke(launchVel, spin);
        }
    }
}
