using System.Collections.Generic;
using HueCity.Core;
using UnityEngine;

namespace HueCity.Mission
{
    /// <summary>
    /// Trình quản lý Hệ thống Nhiệm vụ cốt lõi (Mission Manager).
    /// Quản lý vòng đời nhiệm vụ: Bắt đầu, Kiểm tra mục tiêu, Hoàn thành, Thất bại và Phát sự kiện C#.
    /// Tuân thủ AGENTS.md: Giao tiếp decoupled qua C# Action/Events.
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        [Header("Available Missions Catalog")]
        [SerializeField] private List<MissionData> missionsCatalog = new List<MissionData>();

        [Header("Runtime Active Mission")]
        [SerializeField] private MissionData activeMission;
        [SerializeField] private float remainingTimer = 0f;
        [SerializeField] private bool hasReachedPickup = false;
        [SerializeField] private bool isMissionActive = false;

        [Header("In-World Objective Triggers")]
        [SerializeField] private MissionTrigger pickupMarker;
        [SerializeField] private MissionTrigger deliveryMarker;

        public MissionData ActiveMission => activeMission;
        public float RemainingTimer => remainingTimer;
        public bool IsMissionActive => isMissionActive;
        public bool HasReachedPickup => hasReachedPickup;
        public IReadOnlyList<MissionData> MissionsCatalog => missionsCatalog;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            EnsureWorldMarkers();
        }

        private void Update()
        {
            if (!isMissionActive || activeMission == null) return;

            // Đếm ngược thời gian nếu nhiệm vụ có giới hạn
            if (activeMission.TimeLimit > 0f)
            {
                remainingTimer -= Time.deltaTime;
                if (remainingTimer <= 0f)
                {
                    remainingTimer = 0f;
                    FailMission("Hết thời gian quy định!");
                }
            }
        }

        public void RegisterMission(MissionData mission)
        {
            if (mission != null && !missionsCatalog.Contains(mission))
            {
                missionsCatalog.Add(mission);
            }
        }

        public bool StartMission(string missionId)
        {
            MissionData mission = missionsCatalog.Find(m => m != null && m.MissionId == missionId);
            if (mission == null)
            {
                Debug.LogWarning($"[MissionManager] Không tìm thấy nhiệm vụ với ID: {missionId}");
                return false;
            }

            return StartMission(mission);
        }

        public bool StartMission(MissionData mission)
        {
            if (mission == null) return false;

            activeMission = mission;
            isMissionActive = true;
            remainingTimer = mission.TimeLimit;
            hasReachedPickup = false;

            EnsureWorldMarkers();

            // Thiết lập vị trí các marker trên bản đồ
            if (pickupMarker != null)
            {
                pickupMarker.Configure(mission.MissionId, MissionTrigger.TriggerRole.Pickup, mission.StartPosition, mission.TargetRadius);
                pickupMarker.SetActive(true);
            }

            if (deliveryMarker != null)
            {
                deliveryMarker.Configure(mission.MissionId, MissionTrigger.TriggerRole.Delivery, mission.TargetPosition, mission.TargetRadius);
                deliveryMarker.SetActive(false); // Ẩn điểm giao cho đến khi nhận hàng
            }

            GameEvents.TriggerMissionStarted(activeMission);
            GameEvents.TriggerNotification($"Bắt đầu nhiệm vụ: {mission.Title}", 3.5f, new Color(0.2f, 0.7f, 1.0f));
            Debug.Log($"<color=cyan>[MissionManager]</color> Bắt đầu nhiệm vụ '{mission.Title}' (Thưởng: {mission.RewardMoney:N0}đ, Thời gian: {mission.TimeLimit:F0}s)");

            return true;
        }

        public void CompleteMission()
        {
            if (!isMissionActive || activeMission == null) return;

            MissionData completed = activeMission;
            int reward = completed.RewardMoney;

            isMissionActive = false;
            activeMission = null;

            if (pickupMarker != null) pickupMarker.SetActive(false);
            if (deliveryMarker != null) deliveryMarker.SetActive(false);

            GameEvents.TriggerMissionCompleted(completed, reward);
            GameEvents.TriggerNotification($"Hoàn thành xuất sắc: {completed.Title}! (+{reward:N0}đ)", 4.0f, new Color(0.2f, 0.9f, 0.3f));
            Debug.Log($"<color=green>[MissionManager]</color> Hoàn tất nhiệm vụ '{completed.Title}'! Phần thưởng: {reward:N0}đ");
        }

        public void FailMission(string reason)
        {
            if (!isMissionActive || activeMission == null) return;

            MissionData failed = activeMission;
            isMissionActive = false;
            activeMission = null;

            if (pickupMarker != null) pickupMarker.SetActive(false);
            if (deliveryMarker != null) deliveryMarker.SetActive(false);

            GameEvents.TriggerMissionFailed(failed, reason);
            GameEvents.TriggerNotification($"Nhiệm vụ thất bại: {reason}", 4.0f, new Color(1.0f, 0.25f, 0.25f));
            Debug.Log($"<color=red>[MissionManager]</color> Nhiệm vụ '{failed.Title}' thất bại: {reason}");
        }

        public void CancelMission()
        {
            if (!isMissionActive) return;
            FailMission("Người chơi đã hủy nhiệm vụ.");
        }

        private void EnsureWorldMarkers()
        {
            if (pickupMarker == null)
            {
                GameObject pObj = CreateMarkerVisual("Mission_Marker_Pickup", new Color(1.0f, 0.85f, 0.1f));
                pickupMarker = pObj.AddComponent<MissionTrigger>();
                pickupMarker.OnPlayerEnteredZone += HandleTriggerEntered;
                pickupMarker.SetActive(false);
            }

            if (deliveryMarker == null)
            {
                GameObject dObj = CreateMarkerVisual("Mission_Marker_Delivery", new Color(0.15f, 0.95f, 0.3f));
                deliveryMarker = dObj.AddComponent<MissionTrigger>();
                deliveryMarker.OnPlayerEnteredZone += HandleTriggerEntered;
                deliveryMarker.SetActive(false);
            }
        }

        private GameObject CreateMarkerVisual(string name, Color color)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(transform);

            // Cột sáng / Vòng sáng đơn giản
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = "Beacon_Beam";
            cylinder.transform.SetParent(root.transform);
            cylinder.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            cylinder.transform.localScale = new Vector3(3.0f, 1.25f, 3.0f);

            Destroy(cylinder.GetComponent<Collider>());

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(litShader);
            mat.color = new Color(color.r, color.g, color.b, 0.65f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 1.5f);

            Renderer rend = cylinder.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = mat;

            return root;
        }

        private void HandleTriggerEntered(MissionTrigger trigger)
        {
            if (!isMissionActive || activeMission == null) return;

            if (trigger.Role == MissionTrigger.TriggerRole.Pickup && !hasReachedPickup)
            {
                hasReachedPickup = true;
                pickupMarker.SetActive(false);
                deliveryMarker.SetActive(true);

                GameEvents.TriggerMissionProgress(activeMission, 0.5f, "Đã nhận hàng! Hãy chuyển đến điểm giao.");
                GameEvents.TriggerNotification("Đã nhận hàng! Hãy đến điểm trả hàng.", 3.0f, Color.yellow);
            }
            else if (trigger.Role == MissionTrigger.TriggerRole.Delivery && hasReachedPickup)
            {
                CompleteMission();
            }
        }
    }
}
