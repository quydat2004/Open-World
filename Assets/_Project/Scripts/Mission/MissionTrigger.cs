using System;
using UnityEngine;

namespace HueCity.Mission
{
    /// <summary>
    /// Điểm đánh dấu tương tác nhiệm vụ trong thế giới 3D (Mission World Trigger).
    /// Hiển thị cột sáng / vòng sáng dẫn đường và phát hiện người chơi hoặc xe tiếp cận.
    /// </summary>
    public class MissionTrigger : MonoBehaviour
    {
        public enum TriggerRole
        {
            Pickup,
            Delivery,
            Checkpoint
        }

        [SerializeField] private TriggerRole role = TriggerRole.Pickup;
        [SerializeField] private float triggerRadius = 3.5f;
        [SerializeField] private bool isActive = false;
        [SerializeField] private string targetMissionId = "";

        public event Action<MissionTrigger> OnPlayerEnteredZone;

        public TriggerRole Role => role;
        public float TriggerRadius => triggerRadius;
        public bool IsActive => isActive;
        public string TargetMissionId => targetMissionId;

        private SphereCollider sphereCollider;
        private Renderer markerRenderer;

        private void Awake()
        {
            sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider == null)
            {
                sphereCollider = gameObject.AddComponent<SphereCollider>();
            }
            sphereCollider.isTrigger = true;
            sphereCollider.radius = triggerRadius;

            markerRenderer = GetComponentInChildren<Renderer>();
        }

        public void Configure(string missionId, TriggerRole triggerRole, Vector3 position, float radius = 3.5f)
        {
            targetMissionId = missionId;
            role = triggerRole;
            transform.position = position;
            triggerRadius = radius;

            if (sphereCollider != null)
            {
                sphereCollider.radius = radius;
            }

            SetActive(true);
        }

        public void SetActive(bool active)
        {
            isActive = active;
            gameObject.SetActive(active);

            if (markerRenderer != null)
            {
                markerRenderer.enabled = active;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActive) return;

            // Kiểm tra xem là Người chơi hoặc Xe mà người chơi đang lái
            if (other.CompareTag("Player") || other.GetComponentInParent<HueCity.Player.PlayerMovement>() != null ||
                other.GetComponentInParent<HueCity.Vehicle.CarController>() != null)
            {
                OnPlayerEnteredZone?.Invoke(this);
            }
        }

        private void Update()
        {
            // Hiệu ứng xoay tròn và nhấp nhô nhẹ cho marker trong thế giới
            if (isActive)
            {
                transform.Rotate(Vector3.up, 45f * Time.deltaTime, Space.World);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = (role == TriggerRole.Delivery) ? new Color(0.2f, 1.0f, 0.2f, 0.5f) : new Color(1.0f, 0.8f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius);
        }
    }
}
