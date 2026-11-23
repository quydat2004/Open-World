using System;
using HueCity.NPC;
using UnityEngine;

namespace HueCity.Traffic
{
    /// <summary>
    /// Trí tuệ nhân tạo (AI) điều khiển xe lưu thông tự động trong thành phố (Traffic AI).
    /// Tuân thủ đèn giao thông, bám làn đường theo Waypoint, và tự động phanh tránh va chạm bằng cảm biến phía trước.
    /// </summary>
    public class TrafficVehicleAI : MonoBehaviour
    {
        [Header("Movement & Steering")]
        [SerializeField] private float maxSpeed = 6.0f; // ~22 km/h trong phố
        [SerializeField] private float acceleration = 4.0f;
        [SerializeField] private float brakeDeceleration = 9.0f;
        [SerializeField] private float steerSpeed = 4.5f;

        [Header("Obstacle Detection Sensor")]
        [SerializeField] private float sensorLength = 7.5f;
        [SerializeField] private float emergencyStopDistance = 3.2f;
        [SerializeField] private float sensorRadius = 0.8f;
        [SerializeField] private LayerMask obstacleLayers = ~0; // Tất cả layer hoặc xe cộ, người

        [Header("Waypoint Tracking")]
        [SerializeField] private TrafficWaypoint currentWaypoint;
        [SerializeField] private float waypointReachThreshold = 2.0f;

        [Header("Vehicle Classification")]
        [SerializeField] private TrafficVehicleType vehicleType = TrafficVehicleType.SedanTaxi;

        private float currentSpeed = 0f;
        private bool isBlocked = false;
        private float blockedTimer = 0f;
        private Rigidbody rb;

        public float CurrentSpeed => currentSpeed;
        public bool IsBlocked => isBlocked;
        public TrafficWaypoint CurrentWaypoint => currentWaypoint;
        public TrafficVehicleType VehicleType => vehicleType;

        public event Action<TrafficVehicleAI> OnRouteCompleted;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; // Dùng kinematic movement cho xe traffic trong pool để tối ưu hiệu năng
            }
        }

        public void Initialize(TrafficWaypoint startWaypoint, float initialSpeed = 0f)
        {
            currentWaypoint = startWaypoint;
            currentSpeed = initialSpeed;
            isBlocked = false;
            blockedTimer = 0f;

            if (startWaypoint != null)
            {
                transform.position = startWaypoint.transform.position;
                if (startWaypoint.NextWaypoints.Count > 0 && startWaypoint.NextWaypoints[0] != null)
                {
                    Vector3 lookDir = (startWaypoint.NextWaypoints[0].transform.position - transform.position).normalized;
                    if (lookDir.sqrMagnitude > 0.01f)
                    {
                        transform.rotation = Quaternion.LookRotation(lookDir);
                    }
                }
            }
        }

        private void Update()
        {
            if (currentWaypoint == null) return;

            // 1. Cảm biến phát hiện chướng ngại vật & Đèn giao thông
            bool shouldStop = CheckObstaclesAhead() || CheckTrafficLight();

            // 2. Điều chỉnh tốc độ (Ga / Phanh)
            UpdateSpeed(shouldStop);

            // 3. Di chuyển & Bẻ lái theo Waypoint
            DriveTowardsWaypoint();

            // 4. Kiểm tra đến Waypoint thì chuyển điểm tiếp theo
            CheckWaypointProgress();

            // 5. Bấm còi nếu bị kẹt xe quá 3.5 giây
            HandleHornStimulus();
        }

        private bool CheckObstaclesAhead()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 forward = transform.forward;

            RaycastHit hit;
            if (Physics.SphereCast(origin, sensorRadius, forward, out hit, sensorLength, obstacleLayers))
            {
                // Bỏ qua chính bản thân xe
                if (hit.collider.gameObject != gameObject && !hit.collider.transform.IsChildOf(transform))
                {
                    if (hit.distance <= emergencyStopDistance)
                    {
                        isBlocked = true;
                        return true;
                    }
                    else
                    {
                        // Giảm tốc từ từ khi có vật cản ở tầm trung
                        currentSpeed = Mathf.Min(currentSpeed, maxSpeed * 0.4f);
                        isBlocked = true;
                        return false;
                    }
                }
            }

            isBlocked = false;
            return false;
        }

        private bool CheckTrafficLight()
        {
            if (currentWaypoint == null || !currentWaypoint.IsIntersectionStopPoint) return false;

            float distToStop = Vector3.Distance(transform.position, currentWaypoint.transform.position);
            if (distToStop <= currentWaypoint.StopCheckDistance)
            {
                if (TrafficLightController.Instance != null)
                {
                    bool canPass = TrafficLightController.Instance.CanPass(currentWaypoint.Direction);
                    if (!canPass)
                    {
                        return true; // Dừng trước đèn đỏ
                    }
                }
            }

            return false;
        }

        private void UpdateSpeed(bool shouldStop)
        {
            float targetSpeed = shouldStop ? 0f : (currentWaypoint != null ? currentWaypoint.SpeedLimit : maxSpeed);

            if (currentSpeed < targetSpeed)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, brakeDeceleration * Time.deltaTime);
            }
        }

        private void DriveTowardsWaypoint()
        {
            if (currentWaypoint == null) return;

            Vector3 targetPos = currentWaypoint.transform.position;
            Vector3 moveDir = (targetPos - transform.position);
            moveDir.y = 0f;

            if (moveDir.sqrMagnitude > 0.05f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, steerSpeed * Time.deltaTime);
            }

            transform.position += transform.forward * (currentSpeed * Time.deltaTime);
        }

        private void CheckWaypointProgress()
        {
            if (currentWaypoint == null) return;

            Vector3 diff = transform.position - currentWaypoint.transform.position;
            diff.y = 0f;

            if (diff.magnitude <= waypointReachThreshold)
            {
                TrafficWaypoint next = currentWaypoint.GetNextWaypoint();
                if (next != null)
                {
                    currentWaypoint = next;
                }
                else
                {
                    // Đã đi hết tuyến đường, thông báo để Pool thu hồi
                    OnRouteCompleted?.Invoke(this);
                }
            }
        }

        private void HandleHornStimulus()
        {
            if (isBlocked && currentSpeed < 0.1f)
            {
                blockedTimer += Time.deltaTime;
                if (blockedTimer >= 4.0f)
                {
                    blockedTimer = 0f;
                    // Phát tiếng còi xe vào thế giới để người đi bộ ngoái nhìn (Alert)
                    WorldStimulusManager.EmitStimulus(transform.position, 14.0f, StimulusType.CarHorn, gameObject);
                }
            }
            else
            {
                blockedTimer = 0f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isBlocked ? Color.red : Color.green;
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Gizmos.DrawRay(origin, transform.forward * sensorLength);
            Gizmos.DrawWireSphere(origin + transform.forward * emergencyStopDistance, sensorRadius);
        }
    }
}
