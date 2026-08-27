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
using Eleven.Presentation.Camera;

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
        private BallParams ballParams;
        private BallState ballState;
        private bool isBallInFlight = false;
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
            isBallInFlight = false;
            isReplayActive = false;

            if (ballTransform != null)
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
            isBallInFlight = true;

            ballParams = new BallParams
            {
                radius = 0.11f,
                mass = 0.43f,
                dragCrisis = true
            };

            ballState = new BallState
            {
                position = new float3(0f, 0.11f, 0f),
                velocity = launchVelocity,
                spin = spin
            };

            ballDriver = new BallDriver(ballState, ballParams);

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
            lastKickData = new ReplayKickData(
                seed: currentSeed,
                shooterId: 0,
                keeperId: 1,
                strikeType: 0,
                launchPosition: ballState.position,
                launchVelocity: launchVelocity,
                spin: spin,
                flightDuration: 0.55f,
                result: 1
            );

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
                    ballTransform.position = (Vector3)(float3)currentReplayPlayer.CurrentPosition;
                }
                if (!currentReplayPlayer.IsPlaying)
                {
                    isReplayActive = false;
                }
                return;
            }

            if (!isBallInFlight || ballDriver == null) return;

            // Bước tích phân vật lý bóng bay RK4 120Hz
            ballDriver.Step(dt);
            ballState = ballDriver.State;

            if (ballTransform != null)
            {
                ballTransform.position = (Vector3)(float3)ballState.position;
            }

            // Cập nhật tương tác Lưới Verlet
            if (goalNet != null)
            {
                goalNet.UpdateSimulation(dt, ballState.position, ballState.velocity, 0.11f);
            }

            // Kiểm tra khi bóng bay đến vạch vôi khung thành (Z >= 11.0m)
            if (ballState.position.z >= 11.0f || ballState.position.y < 0f || ballState.position.z > 14.0f)
            {
                ResolveShotOutcome();
            }
        }

        private void ResolveShotOutcome()
        {
            isBallInFlight = false;

            // Kiểm tra phân loại bàn thắng
            float x = ballState.position.x;
            float y = ballState.position.y;

            bool isInsideGoal = (Mathf.Abs(x) <= 3.66f && y >= 0f && y <= 2.44f);
            bool isSaved = false;

            // Kiểm tra khoảng cách tới thủ môn
            if (goalkeeper != null)
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

            currentReplayPlayer = new ReplayPlayer(lastKickData, playbackSpeed: 0.35f);
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
