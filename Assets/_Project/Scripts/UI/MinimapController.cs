using HueCity.Mission;
using UnityEngine;
using UnityEngine.UI;

namespace HueCity.UI
{
    /// <summary>
    /// Điều khiển Bản đồ con (Minimap Controller) góc dưới bên trái màn hình.
    /// Theo dõi vị trí và hướng nhìn của người chơi, đồng thời hiển thị la bàn GPS chỉ hướng tới mục tiêu nhiệm vụ.
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        [Header("Target Tracking")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private float cameraHeight = 65.0f;

        [Header("UI Icons & Markers")]
        [SerializeField] private RectTransform playerIcon;
        [SerializeField] private RectTransform missionMarkerIcon;
        [SerializeField] private Text distanceText;
        [SerializeField] private float minimapRadius = 80.0f;
        [SerializeField] private float worldScale = 2.0f; // Tỉ lệ mét thế giới ra pixel minimap

        public Transform PlayerTransform => playerTransform;
        public Camera MinimapCamera => minimapCamera;

        public void SetPlayerTarget(Transform player)
        {
            playerTransform = player;
        }

        public void SetMinimapCamera(Camera cam)
        {
            minimapCamera = cam;
        }

        private void LateUpdate()
        {
            if (playerTransform == null) return;

            Vector3 playerPos = playerTransform.position;

            // 1. Cập nhật vị trí Camera nhìn từ trên cao xuống
            if (minimapCamera != null)
            {
                minimapCamera.transform.position = new Vector3(playerPos.x, cameraHeight, playerPos.z);
                minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // 2. Cập nhật góc xoay biểu tượng người chơi trên minimap
            if (playerIcon != null)
            {
                float angle = -playerTransform.eulerAngles.y;
                playerIcon.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            // 3. Cập nhật GPS Marker chỉ hướng nhiệm vụ
            UpdateMissionMarker(playerPos);
        }

        private void UpdateMissionMarker(Vector3 playerPos)
        {
            if (missionMarkerIcon == null) return;

            bool hasMission = MissionManager.Instance != null && MissionManager.Instance.IsMissionActive;
            if (!hasMission)
            {
                missionMarkerIcon.gameObject.SetActive(false);
                if (distanceText != null) distanceText.gameObject.SetActive(false);
                return;
            }

            MissionData activeMis = MissionManager.Instance.ActiveMission;
            Vector3 targetPos = MissionManager.Instance.HasReachedPickup ? activeMis.TargetPosition : activeMis.StartPosition;

            Vector3 delta = targetPos - playerPos;
            delta.y = 0f;
            float distance = delta.magnitude;

            missionMarkerIcon.gameObject.SetActive(true);

            // Tọa độ tương đối trên mặt phẳng Minimap
            Vector2 screenOffset = new Vector2(delta.x, delta.z) * worldScale;

            // Giới hạn trong viền tròn của Minimap
            if (screenOffset.magnitude > minimapRadius)
            {
                screenOffset = screenOffset.normalized * minimapRadius;
            }

            missionMarkerIcon.anchoredPosition = screenOffset;

            // Hiển thị khoảng cách bằng chữ
            if (distanceText != null)
            {
                distanceText.gameObject.SetActive(true);
                distanceText.text = $"{distance:F0}m";
            }
        }
    }
}
