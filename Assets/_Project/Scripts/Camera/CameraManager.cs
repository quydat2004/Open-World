using Cinemachine;
using HueCity.Core;
using UnityEngine;

namespace HueCity.Camera
{
    /// <summary>
    /// Điều phối chuyển đổi Camera Cinemachine mượt mà giữa trạng thái Đi bộ và Lái xe.
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        [Header("Cinemachine Cameras")]
        [SerializeField] private CinemachineVirtualCameraBase playerCamera;
        [SerializeField] private CinemachineVirtualCameraBase vehicleCamera;

        [Header("Priority Settings")]
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 10;

        private void OnEnable()
        {
            GameEvents.OnPlayerEnteredVehicle += HandlePlayerEnteredVehicle;
            GameEvents.OnPlayerExitedVehicle += HandlePlayerExitedVehicle;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerEnteredVehicle -= HandlePlayerEnteredVehicle;
            GameEvents.OnPlayerExitedVehicle -= HandlePlayerExitedVehicle;
        }

        private void Start()
        {
            // Mặc định lúc bắt đầu game là camera người đi bộ
            SetPlayerCameraActive();
        }

        private void HandlePlayerEnteredVehicle(GameObject player, GameObject vehicle)
        {
            if (vehicleCamera != null)
            {
                // Tự động gán Follow & LookAt vào xe nếu chưa có
                vehicleCamera.Follow = vehicle.transform;
                vehicleCamera.LookAt = vehicle.transform;
                vehicleCamera.Priority = activePriority;
            }

            if (playerCamera != null)
            {
                playerCamera.Priority = inactivePriority;
            }
        }

        private void HandlePlayerExitedVehicle(GameObject player, GameObject vehicle)
        {
            SetPlayerCameraActive();
        }

        private void SetPlayerCameraActive()
        {
            if (playerCamera != null)
            {
                playerCamera.Priority = activePriority;
            }

            if (vehicleCamera != null)
            {
                vehicleCamera.Priority = inactivePriority;
            }
        }
    }
}
