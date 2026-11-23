using System;
using HueCity.Mission;
using UnityEngine;

namespace HueCity.Core
{
    /// <summary>
    /// Event Bus tĩnh để giao tiếp giữa các hệ thống (Player, Vehicle, Camera, Mission, UI)
    /// Tránh phụ thuộc chéo (decoupled architecture).
    /// </summary>
    public static class GameEvents
    {
        // 1. Sự kiện Phương tiện & Người chơi
        public static event Action<GameObject, GameObject> OnPlayerEnteredVehicle; // (player, vehicle)
        public static event Action<GameObject, GameObject> OnPlayerExitedVehicle;  // (player, vehicle)

        public static void TriggerPlayerEnteredVehicle(GameObject player, GameObject vehicle)
        {
            OnPlayerEnteredVehicle?.Invoke(player, vehicle);
        }

        public static void TriggerPlayerExitedVehicle(GameObject player, GameObject vehicle)
        {
            OnPlayerExitedVehicle?.Invoke(player, vehicle);
        }

        // 2. Sự kiện Tiền tệ & Kinh tế
        public static event Action<int, int> OnMoneyChanged; // (totalMoney, deltaMoney)

        public static void TriggerMoneyChanged(int totalMoney, int deltaMoney)
        {
            OnMoneyChanged?.Invoke(totalMoney, deltaMoney);
        }

        // 3. Sự kiện Hệ thống Nhiệm vụ
        public static event Action<MissionData> OnMissionStarted;
        public static event Action<MissionData, float, string> OnMissionProgress; // (mission, progress 0-1, statusText)
        public static event Action<MissionData, int> OnMissionCompleted;          // (mission, rewardMoney)
        public static event Action<MissionData, string> OnMissionFailed;          // (mission, failReason)

        public static void TriggerMissionStarted(MissionData mission)
        {
            OnMissionStarted?.Invoke(mission);
        }

        public static void TriggerMissionProgress(MissionData mission, float progress, string status)
        {
            OnMissionProgress?.Invoke(mission, progress, status);
        }

        public static void TriggerMissionCompleted(MissionData mission, int rewardMoney)
        {
            OnMissionCompleted?.Invoke(mission, rewardMoney);
        }

        public static void TriggerMissionFailed(MissionData mission, string reason)
        {
            OnMissionFailed?.Invoke(mission, reason);
        }

        // 4. Sự kiện Thông báo Banner HUD
        public static event Action<string, float, Color> OnNotificationRequested; // (message, duration, color)

        public static void TriggerNotification(string message, float duration = 3.0f, Color? color = null)
        {
            Color bannerColor = color ?? Color.white;
            OnNotificationRequested?.Invoke(message, duration, bannerColor);
        }

        // 5. Sự kiện Thời tiết & Môi trường
        public static event Action<bool> OnWeatherRainChanged; // isRaining

        public static void TriggerWeatherRainChanged(bool isRaining)
        {
            OnWeatherRainChanged?.Invoke(isRaining);
        }
    }
}
