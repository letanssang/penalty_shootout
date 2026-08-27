using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Eleven.Ball;
using Eleven.Keeper;
using Eleven.Shooter;
using Eleven.Match;
using Eleven.Presentation;
using Eleven.Presentation.Net;

namespace Eleven.UI
{
    /// <summary>
    /// Vòng lặp điều phối chính của trận đấu Penalty Shootout (Gameplay Orchestrator):
    /// - Tích hợp đầy đủ 4 kiểu sút (Cứa lòng má trong, Lá bàng Knuckle, Lốp bóng Panenka, Mu bàn chân)
    /// - Áp dụng lực bất ổn định thời gian thực từ KnuckleForce
    /// - Mô phỏng vật lý va chạm nảy Cột dọc / Xà ngang (Rebound Physics)
    /// - Phản xạ đẩy bóng sống động của thủ môn và Lưới Verlet 3D
    /// - Phát lại Replay Slow-motion tốc độ 0.35x
    /// </summary>
    public sealed class MatchGameLoop : MonoBehaviour
    {
        [Header("Tham chiếu Scene")]
        [SerializeField] private Transform ballTransform;
        [SerializeField] private TrailRenderer ballTrail;
        [SerializeField] private GoalNetView goalNet;
        [SerializeField] private GoalkeeperView goalkeeper;
        [SerializeField] private TouchSwipeReceiver swipeReceiver;
        [SerializeField] private ScoreboardUI scoreboard;
        [SerializeField] private UnityEngine.Camera mainCamera;

        private BallDriver ballDriver;
        private BallState ballState;
        private ShotIntent lastIntent;
        private bool isShotActive = false;
        private bool isShotResolved = false;
        private bool isReplayActive = false;
        private float flightTime = 0f;
        private float postShotTimer = 0f;

        private bool hitPost = false;
        private bool hitCrossbar = false;
        private bool isSaved = false;

        private ReplayPlayer currentReplayPlayer;
        private ReplayKickData lastKickData;

        private List<KickResult> homeKicks = new List<KickResult>();
        private List<KickResult> awayKicks = new List<KickResult>();
        private int currentKickIndex = 0;
        private uint currentSeed = 1001;

        // Vị trí camera
        private readonly Vector3 behindCamPos = new Vector3(0f, 1.80f, -4.5f);
        private readonly Vector3 behindCamRot = new Vector3(9.5f, 0f, 0f);
        private readonly Vector3 replayCamPos = new Vector3(-4.2f, 1.6f, 8.5f);

        private void Start()
        {
            if (ballTransform != null)
            {
                ballDriver = ballTransform.GetComponent<BallDriver>() ?? ballTransform.gameObject.AddComponent<BallDriver>();
            }

            ResetBall();

            if (swipeReceiver != null)
            {
                swipeReceiver.OnShotFired += HandleShotFired;
            }

            if (scoreboard != null)
            {
                scoreboard.OnReplayClicked += PlayReplay;
                scoreboard.OnNextKickClicked += PrepareNextKick;
                scoreboard.UpdateScores(homeKicks, awayKicks, currentKickIndex);
            }

            SetCameraBehindShooter();
        }

        private void OnDestroy()
        {
            if (swipeReceiver != null)
            {
                swipeReceiver.OnShotFired -= HandleShotFired;
            }
            if (scoreboard != null)
            {
                scoreboard.OnReplayClicked -= PlayReplay;
                scoreboard.OnNextKickClicked -= PrepareNextKick;
            }
        }

        private void ResetBall()
        {
            isReplayActive = false;
            isShotActive = false;
            isShotResolved = false;
            flightTime = 0f;
            postShotTimer = 0f;
            hitPost = false;
            hitCrossbar = false;
            isSaved = false;

            if (ballDriver != null)
            {
                ballDriver.ResetTo(new float3(0f, 0.11f, 0f));
            }
            else if (ballTransform != null)
            {
                ballTransform.position = new Vector3(0f, 0.11f, 0f);
                ballTransform.rotation = Quaternion.identity;
            }

            if (ballTrail != null)
            {
                ballTrail.Clear();
                ballTrail.emitting = false;
            }

            if (goalkeeper != null)
            {
                goalkeeper.ResetToHome();
            }

            if (swipeReceiver != null)
            {
                swipeReceiver.IsInputEnabled = true;
            }

            if (scoreboard != null)
            {
                scoreboard.HideBanner();
                scoreboard.HideShotBadge();
            }

            SetCameraBehindShooter();
        }

        private void HandleShotFired(ShotIntent intent, float3 launchVelocity)
        {
            currentSeed = (currentSeed * 1664525u + 1013904223u);
            lastIntent = intent;
            isShotActive = true;
            isShotResolved = false;
            flightTime = 0f;
            postShotTimer = 0f;
            hitPost = false;
            hitCrossbar = false;
            isSaved = false;

            ballState = new BallState(new float3(0f, 0.11f, 0f), launchVelocity, intent.spin);

            if (ballDriver != null)
            {
                ballDriver.Launch(ballState);
            }

            // Đổi màu vệt bóng theo phong cách của từng chiêu sút
            if (ballTrail != null)
            {
                ballTrail.Clear();
                ballTrail.emitting = true;

                switch (intent.type)
                {
                    case ShotType.InsideFoot: // Cứa lòng: Xanh dương uốn lượn
                        ballTrail.startColor = new Color(0.1f, 0.85f, 1.0f, 0.95f);
                        ballTrail.endColor = new Color(0f, 0.4f, 1.0f, 0f);
                        break;
                    case ShotType.Knuckle: // Lá bàng: Vàng điện giật
                        ballTrail.startColor = new Color(1.0f, 0.90f, 0.10f, 0.95f);
                        ballTrail.endColor = new Color(1.0f, 0.45f, 0.0f, 0f);
                        break;
                    case ShotType.Chip: // Panenka: Xanh ngọc bổng nhẹ
                        ballTrail.startColor = new Color(0.4f, 1.0f, 0.6f, 0.95f);
                        ballTrail.endColor = new Color(0.1f, 0.8f, 0.4f, 0f);
                        break;
                    default: // Mu bàn chân: Đỏ cam rực lửa
                        ballTrail.startColor = new Color(1.0f, 0.35f, 0.15f, 0.95f);
                        ballTrail.endColor = new Color(1.0f, 0.1f, 0.0f, 0f);
                        break;
                }
            }

            // Kích hoạt phản xạ thủ môn
            if (goalkeeper != null)
            {
                goalkeeper.ReactToShot(launchVelocity, intent.spin, currentSeed);
            }

            // Cập nhật thông số sút lên HUD
            if (scoreboard != null)
            {
                scoreboard.SetCurrentShotInfo(intent.type, intent.speed);
                scoreboard.HideBanner();
            }

            // Ghi dữ liệu Replay
            lastKickData = new ReplayKickData
            {
                seed = currentSeed,
                intent = intent,
                expectedOutcome = ShotOutcome.Goal,
                expectedCrossing = intent.aimPoint,
                expectedCell = GoalGeometry.CellOf(intent.aimPoint)
            };
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Xử lý Replay Player
            if (isReplayActive && currentReplayPlayer != null)
            {
                currentReplayPlayer.Tick(dt);
                if (ballTransform != null)
                {
                    ballTransform.position = (Vector3)(float3)currentReplayPlayer.CurrentBallState.position;
                }
                if (!currentReplayPlayer.IsPlaying || currentReplayPlayer.HasCompleted)
                {
                    isReplayActive = false;
                }
                return;
            }

            if (!isShotActive || ballDriver == null) return;

            flightTime += dt;
            ballState = ballDriver.State;

            // 1. Áp dụng gia tốc bất định KnuckleForce nếu là cú sút Knuckle
            if (lastIntent.unstable)
            {
                float3 knuckleAcc = KnuckleForce.Evaluate(in ballState, KnuckleConfig.Default, flightTime, currentSeed);
                if (math.lengthsq(knuckleAcc) > 0.001f)
                {
                    ballState.velocity += knuckleAcc * dt;
                    ballState.position += 0.5f * knuckleAcc * dt * dt;
                }
            }

            // 2. Mô phỏng va chạm và tương tác Lưới Verlet 3D
            if (goalNet != null)
            {
                goalNet.UpdateSimulation(dt, ballState.position, ballState.velocity, 0.11f);
            }

            // 3. Kiểm tra Thủ môn cản phá & Đẩy bóng (Parry/Deflect)
            if (goalkeeper != null && !isSaved)
            {
                float3 pos = ballState.position;
                float3 vel = ballState.velocity;
                if (goalkeeper.TryDeflectBall(ref pos, ref vel, 0.11f))
                {
                    isSaved = true;
                    ballState.position = pos;
                    ballState.velocity = vel;
                }
            }

            // 4. Vật lý va chạm nảy Cột dọc & Xà ngang (Rebound Physics)
            CheckGoalFrameCollision(ref ballState);

            // 5. Lực cản của lưới làm bóng cuộn lại khi vào trong gôn (Z >= 11.0m đến 12.8m)
            if (ballState.position.z >= 11.0f && ballState.position.z <= 12.8f && !isSaved && !hitPost && !hitCrossbar)
            {
                bool isInsideGoal = (Mathf.Abs(ballState.position.x) <= 3.60f && ballState.position.y >= 0.1f && ballState.position.y <= 2.40f);
                if (isInsideGoal)
                {
                    float3 dragVel = ballState.velocity * Mathf.Max(0f, 1f - dt * 7.5f);
                    dragVel.y -= 9.81f * dt * 0.5f;

                    if (ballState.position.y < 0.11f)
                    {
                        dragVel.y = 0f;
                        dragVel.x *= 0.4f;
                        dragVel.z *= 0.4f;
                    }

                    ballState.velocity = dragVel;
                    ballState.position += ballState.velocity * dt;
                    if (ballState.position.y < 0.11f)
                    {
                        var p = ballState.position;
                        p.y = 0.11f;
                        ballState.position = p;
                    }
                }
            }

            // Cập nhật vị trí hiển thị quả bóng
            if (ballTransform != null)
            {
                ballTransform.position = (Vector3)(float3)ballState.position;
            }

            // 6. Trì hoãn 1.2 giây sau khi bóng qua vạch vôi hoặc chạm đất để chiêm ngưỡng trọn vẹn
            if (ballState.position.z >= 11.0f || ballState.position.y < 0.08f || ballState.position.z > 13.5f || isSaved)
            {
                postShotTimer += dt;
                if (postShotTimer >= 1.2f && !isShotResolved)
                {
                    isShotResolved = true;
                    ballDriver.Freeze();
                    ResolveShotOutcome();
                }
            }
        }

        private void CheckGoalFrameCollision(ref BallState state)
        {
            if (state.position.z < 10.8f || state.position.z > 11.2f) return;

            float ballR = 0.11f;
            float postR = 0.06f;
            float effR = ballR + postR;

            float x = state.position.x;
            float y = state.position.y;

            // Cột trái (-3.66, y)
            float distLeft = Mathf.Sqrt((x - (-3.66f)) * (x - (-3.66f)));
            if (distLeft <= effR && y >= 0f && y <= 2.44f + postR)
            {
                hitPost = true;
                float normalX = (x >= -3.66f) ? 1.0f : -1.0f;
                state.velocity.x = Mathf.Abs(state.velocity.x) * normalX * 0.72f;
                state.velocity.z = -Mathf.Abs(state.velocity.z) * 0.55f;
                state.position.x = -3.66f + normalX * (effR + 0.01f);
                return;
            }

            // Cột phải (3.66, y)
            float distRight = Mathf.Sqrt((x - 3.66f) * (x - 3.66f));
            if (distRight <= effR && y >= 0f && y <= 2.44f + postR)
            {
                hitPost = true;
                float normalX = (x <= 3.66f) ? -1.0f : 1.0f;
                state.velocity.x = -Mathf.Abs(state.velocity.x) * normalX * 0.72f;
                state.velocity.z = -Mathf.Abs(state.velocity.z) * 0.55f;
                state.position.x = 3.66f + normalX * (effR + 0.01f);
                return;
            }

            // Xà ngang (x, 2.44)
            float distBar = Mathf.Abs(y - 2.44f);
            if (distBar <= effR && Mathf.Abs(x) <= 3.66f + postR)
            {
                hitCrossbar = true;
                float normalY = (y >= 2.44f) ? 1.0f : -1.0f;
                state.velocity.y = Mathf.Abs(state.velocity.y) * normalY * 0.65f;
                state.velocity.z = -Mathf.Abs(state.velocity.z) * 0.55f;
                state.position.y = 2.44f + normalY * (effR + 0.01f);
            }
        }

        private void ResolveShotOutcome()
        {
            float x = ballState.position.x;
            float y = ballState.position.y;
            bool isInsideGoal = (Mathf.Abs(x) <= 3.66f && y >= 0f && y <= 2.44f);

            string shotTypeName = lastIntent.type switch
            {
                ShotType.InsideFoot => "Cú Cứa Lòng Má Trong",
                ShotType.Knuckle => "Cú Sút Lá Bàng Knuckle",
                ShotType.Chip => "Cú Lốp Bóng Panenka",
                _ => "Cú Nã Đại Bác Mu Bàn Chân"
            };

            if (isSaved)
            {
                homeKicks.Add(KickResult.Missed);
                if (scoreboard != null)
                {
                    scoreboard.ShowBanner("🧤 BỊ CẢN PHÁ!", $"Thủ môn đã xuất sắc cản phá {shotTypeName}!", new Color(1f, 0.45f, 0.15f), replayAvailable: true);
                }
            }
            else if (hitCrossbar || hitPost)
            {
                homeKicks.Add(isInsideGoal ? KickResult.Scored : KickResult.Missed);
                string frameName = hitCrossbar ? "Xà ngang" : "Cột dọc";
                if (isInsideGoal)
                {
                    scoreboard?.ShowBanner("⚽ VÀO OOOO! (ĐẬP XÀ VÀO LƯỚI)", $"Bóng đập mép trong {frameName} găm thẳng vào lưới!", Color.green, replayAvailable: true);
                }
                else
                {
                    scoreboard?.ShowBanner("💥 ĐẬP KHUNG GỖ BẬT RA!", $"Cú sút đập trúng {frameName} bật ra ngoài đáng tiếc!", Color.red, replayAvailable: true);
                }
            }
            else if (isInsideGoal)
            {
                homeKicks.Add(KickResult.Scored);
                if (scoreboard != null)
                {
                    scoreboard.ShowBanner("⚽ VÀO OOOO!", $"{shotTypeName} tuyệt đỉnh găm thẳng vào góc lưới!", Color.green, replayAvailable: true);
                }
            }
            else
            {
                homeKicks.Add(KickResult.Missed);
                string reason = (Mathf.Abs(x) > 3.66f) ? "Bóng bay chệch cột dọc!" : "Bóng bay vọt xà ngang!";
                if (scoreboard != null)
                {
                    scoreboard.ShowBanner("❌ KHÔNG VÀO!", reason, Color.red, replayAvailable: true);
                }
            }

            if (scoreboard != null)
            {
                scoreboard.UpdateScores(homeKicks, awayKicks, currentKickIndex);
            }
        }

        private void PlayReplay()
        {
            if (ballTrail != null)
            {
                ballTrail.Clear();
                ballTrail.emitting = true;
            }

            currentReplayPlayer = new ReplayPlayer();
            currentReplayPlayer.Load(lastKickData);
            currentReplayPlayer.SetPlaybackSpeed(0.35f);
            currentReplayPlayer.Play();
            isReplayActive = true;

            SetCameraReplay();

            if (scoreboard != null)
            {
                scoreboard.ShowBanner("🎬 REPLAY SLOW-MOTION (0.35x)", "Xem lại quỹ đạo siêu phẩm", Color.cyan, replayAvailable: false);
            }
        }

        private void PrepareNextKick()
        {
            currentKickIndex++;
            if (currentKickIndex >= 5)
            {
                int goals = 0;
                for (int i = 0; i < homeKicks.Count; i++)
                {
                    if (homeKicks[i] == KickResult.Scored) goals++;
                }

                string finalMsg = (goals >= 3) ? "🎉 CHIẾN THẮNG CHUNG CUỘC!" : "😢 THUA CUỘC!";
                Color finalColor = (goals >= 3) ? Color.green : Color.red;

                if (scoreboard != null)
                {
                    scoreboard.ShowBanner(finalMsg, $"Bạn ghi được {goals}/5 bàn thắng.", finalColor, replayAvailable: true);
                }
                currentKickIndex = 0;
                homeKicks.Clear();
                awayKicks.Clear();
                return;
            }

            ResetBall();
        }

        private void SetCameraBehindShooter()
        {
            if (mainCamera != null)
            {
                mainCamera.transform.position = behindCamPos;
                mainCamera.transform.rotation = Quaternion.Euler(behindCamRot);
            }
        }

        private void SetCameraReplay()
        {
            if (mainCamera != null)
            {
                mainCamera.transform.position = replayCamPos;
                mainCamera.transform.LookAt(new Vector3(0f, 1.0f, 11.0f));
            }
        }
    }
}
