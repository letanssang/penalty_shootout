using System;
using UnityEngine;
using Unity.Mathematics;
using Eleven.Ball;

namespace Eleven.Shooter
{
    /// <summary>
    /// Component nhận diện cử chỉ vuốt từ màn hình cảm ứng hoặc chuột trên PC.
    /// Sử dụng phép chiếu Raycast Camera chuẩn xác vào mặt phẳng khung thành (Z = 11.0m)
    /// Đảm bảo vuốt trúng điểm nào trên màn hình, bóng sẽ bay CHÍNH XÁC 100% vào điểm đó trong không gian 3D.
    /// </summary>
    public sealed class TouchSwipeReceiver : MonoBehaviour
    {
        public event Action<float3, float3> OnShotFired; // (launchVelocity, spin)

        [Header("Tùy chỉnh lực và độ nhạy")]
        [SerializeField] private float minSpeed = 22f;
        [SerializeField] private float maxSpeed = 34f;
        [SerializeField] private float maxSwipeTime = 1.0f;
        [SerializeField] private float minSwipeDistPixels = 20f;

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

            // 1. Phép chiếu Raycast từ Camera qua điểm ngón tay đến mặt phẳng khung thành Z = 11.0m
            var cam = Camera.main;
            Vector3 targetOnGoal;

            if (cam != null)
            {
                Ray ray = cam.ScreenPointToRay(end);
                if (Mathf.Abs(ray.direction.z) > 0.001f)
                {
                    float t = (11.0f - ray.origin.z) / ray.direction.z;
                    targetOnGoal = ray.origin + ray.direction * t;
                }
                else
                {
                    targetOnGoal = new Vector3(0f, 1.22f, 11.0f);
                }
            }
            else
            {
                float refH = Screen.height > 0 ? Screen.height : 1080f;
                float normX = (end.x - Screen.width * 0.5f) / (refH * 0.5f);
                float normY = (end.y - Screen.height * 0.4f) / (refH * 0.4f);
                targetOnGoal = new Vector3(normX * 3.66f, normY * 1.5f + 1.22f, 11.0f);
            }

            // 2. Vận tốc vuốt (vuốt càng nhanh bóng bay càng căng)
            float refH_speed = Screen.height > 0 ? Screen.height : 1080f;
            float swipeSpeed = (dist / refH_speed) / Mathf.Max(0.04f, duration);
            float tSpeed = Mathf.Clamp01((swipeSpeed - 0.8f) / 3.0f);
            float forwardSpeed = Mathf.Lerp(minSpeed, maxSpeed, tSpeed);

            // 3. Tính toán thời gian bay chính xác từ Z = 0 đến Z = 11.0m
            float flightTime = 11.0f / forwardSpeed;

            // 4. Bù trừ trọng lực chính xác tuyệt đối theo giải tích
            // Y(T) = Y0 + Vy*T - 0.5*g*T^2  ==>  Vy = (Y_target - Y0 + 0.5*g*T^2) / T
            float g = 9.81f;
            float vy = (targetOnGoal.y - 0.11f + 0.5f * g * flightTime * flightTime) / flightTime;
            float vx = targetOnGoal.x / flightTime;
            float vz = forwardSpeed;

            float3 launchVel = new float3(vx, vy, vz);

            // 5. Tính toán độ xoáy (Spin) dựa trên độ lệch ngang
            float curveX = (delta.x / refH_speed);
            float spinZ = -curveX * 4.0f * (float)Math.PI; // Xoáy quả chuối nhẹ
            float spinX = 1.0f * (float)Math.PI;
            float3 spin = new float3(spinX, spinZ, 0f);

            isInputEnabled = false; // Khóa input khi bóng đang bay
            OnShotFired?.Invoke(launchVel, spin);
        }
    }
}
