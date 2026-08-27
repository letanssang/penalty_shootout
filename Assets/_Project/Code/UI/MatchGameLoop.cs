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
    /// Vòng lặp điều phối chính của toàn bộ trận đấu Penalty Shootout (Gameplay Orchestrator).
    /// Kết nối trực tiếp: BallSolver + KeeperBrain + NetSimulator + CameraDirector + ScoreboardUI.
    /// </summary>
    public sealed class MatchGameLoop : MonoBehaviour
    {
        [Header("Tham chiếu các đối tượng Scene")]
        [SerializeField] private Transform ballTransform;
        [SerializeField] private TrailRenderer ballTrail;
        [SerializeField] private GoalNetView goalNet;
        [SerializeField] private GoalkeeperView goalkeeper;
        [SerializeField] private TouchSwipeReceiver swipeReceiver;
        [SerializeField] private ScoreboardUI scoreboard;
        [SerializeField] private UnityEngine.Camera mainCamera;

        private BallDriver ballDriver;
        private BallState ballState;
        private bool isReplayActive = false;
        private ReplayPlayer currentReplayPlayer;
        private ReplayKickData lastKickData;

        private List<KickResult> homeKicks = new List<KickResult>();
        private List<KickResult> awayKicks = new List<KickResult>();
        private int currentKickIndex = 0;
        private uint currentSeed = 1001;

        // Vị trí camera
        private Vector3 behindShooterCamPos = new Vector3(0f, 1.8f, -4.5f);
        private Vector3 behindShooterCamRot = new Vector3(10f, 0f, 0f);
        private Vector3 broadcastCamPos = new Vector3(8.5f, 4.0f, 6.0f);
        private Vector3 replayCamPos = new Vector3(-4.0f, 1.5f, 9.0f);

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
                scoreboard.ShowBanner("HÃY VUỐT ĐỂ SÚT!", "Vuốt nhanh về phía khung thành để sút bóng", Color.yellow, replayAvailable: false);
            }

            SetCameraBehindShooter();
        }

        private void HandleShotFired(float3 launchVelocity, float3 spin)
        {
            currentSeed += 7u;

            ballState = new BallState(new float3(0f, 0.11f, 0f), launchVelocity, spin);

            if (ballDriver != null)
            {
                ballDriver.Launch(ballState);
            }

            if (ballTrail != null)
            {
                ballTrail.Clear();
                ballTrail.emitting = true;
            }

            // Kích hoạt thủ môn phản xạ
            if (goalkeeper != null)
            {
                goalkeeper.ReactToShot(launchVelocity, spin, currentSeed);
            }

            // Lưu dữ liệu cho Replay
            float speed = math.length(launchVelocity);
            float3 aim = new float3(launchVelocity.x * (11.0f / math.max(1f, launchVelocity.z)), launchVelocity.y * (11.0f / math.max(1f, launchVelocity.z)), 11.0f);

            lastKickData = new ReplayKickData
            {
                seed = currentSeed,
                intent = new ShotIntent
                {
                    aimPoint = aim,
                    spin = spin,
                    speed = speed,
                    type = ShotType.Instep,
                    quality = 0.95f,
                    unstable = false,
                    scatterRadius = 0.05f
                },
                expectedOutcome = ShotOutcome.Goal,
                expectedCrossing = aim,
                expectedCell = 4
            };

            if (scoreboard != null)
            {
                scoreboard.HideBanner();
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

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

            if (ballDriver == null || !ballDriver.IsLive) return;

            ballState = ballDriver.State;

            // Cập nhật tương tác Lưới Verlet
            if (goalNet != null)
            {
                goalNet.UpdateSimulation(dt, ballState.position, ballState.velocity, 0.11f);
            }

            // Kiểm tra khi bóng bay đến vạch vôi khung thành (Z >= 11.0m)
            if (ballState.position.z >= 11.0f || ballState.position.y < 0f || ballState.position.z > 14.0f)
            {
                ballDriver.Freeze();
                ResolveShotOutcome();
            }
        }

        private void ResolveShotOutcome()
        {
            // Kiểm tra phân loại bàn thắng
            float x = ballState.position.x;
            float y = ballState.position.y;

            bool isInsideGoal = (Mathf.Abs(x) <= 3.66f && y >= 0f && y <= 2.44f);
            bool isSaved = false;

            // Kiểm tra khoảng cách tới thủ môn
            if (goalkeeper != null && ballTransform != null)
            {
                float distToKeeper = Vector3.Distance(ballTransform.position, goalkeeper.CurrentPosition);
                if (distToKeeper <= 0.85f)
                {
                    isSaved = true;
                }
            }

            if (isInsideGoal && !isSaved)
            {
                // BÀN THẮNG!
                homeKicks.Add(KickResult.Scored);
                if (scoreboard != null)
                {
                    scoreboard.ShowBanner("⚽ VÀO OOOO!", "Cú sút tuyệt đỉnh găm thẳng vào lưới!", Color.green, replayAvailable: true);
                }
                SetCameraBroadcast();
            }
            else if (isSaved)
            {
                // THỦ MÔN CẢN PHÁ!
                homeKicks.Add(KickResult.Missed);
                if (scoreboard != null)
                {
                    scoreboard.ShowBanner("🧤 BỊ CẢN PHÁ!", "Thủ môn đã bay người xuất thần cứu thua!", new Color(1f, 0.4f, 0.2f), replayAvailable: true);
                }
            }
            else
            {
                // BÓNG BAY RA NGOÀI HOẶC TRÚNG CỘT
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
                scoreboard.ShowBanner("🎬 REPLAY SLOW-MOTION (0.35x)", "Xem lại pha bóng quỹ đạo chuẩn", Color.cyan, replayAvailable: false);
            }
        }

        private void PrepareNextKick()
        {
            currentKickIndex++;
            if (currentKickIndex >= 5)
            {
                // Tổng kết trận đấu
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
                mainCamera.transform.position = behindShooterCamPos;
                mainCamera.transform.rotation = Quaternion.Euler(behindShooterCamRot);
            }
        }

        private void SetCameraBroadcast()
        {
            if (mainCamera != null)
            {
                mainCamera.transform.position = broadcastCamPos;
                mainCamera.transform.LookAt(new Vector3(0f, 1.2f, 11.0f));
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
