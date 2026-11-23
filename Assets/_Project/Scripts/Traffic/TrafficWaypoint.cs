using System.Collections.Generic;
using UnityEngine;

namespace HueCity.Traffic
{
    /// <summary>
    /// Điểm mốc dẫn đường (Traffic Waypoint) cho xe cộ tự hành trong đô thị.
    /// Hỗ trợ phân nhánh ngã tư (Branching) và điểm dừng trước đèn tín hiệu giao thông (Stop Point).
    /// </summary>
    public class TrafficWaypoint : MonoBehaviour
    {
        [Header("Traffic Flow Settings")]
        [Tooltip("Tốc độ tối đa quy định trên đoạn đường này (m/s). Mặc định 6.5 m/s (~23 km/h trong nội thị).")]
        [SerializeField] private float speedLimit = 6.5f;

        [Tooltip("Hướng luồng giao thông gắn liền với nút này")]
        [SerializeField] private TrafficDirection direction = TrafficDirection.NorthSouth;

        [Tooltip("Đánh dấu đây là điểm dừng trước vạch đèn đỏ của ngã tư")]
        [SerializeField] private bool isIntersectionStopPoint = false;

        [Tooltip("Bán kính vùng ngã tư để phương tiện kiểm tra đèn")]
        [SerializeField] private float stopCheckDistance = 3.5f;

        [Header("Connected Next Waypoints")]
        [SerializeField] private List<TrafficWaypoint> nextWaypoints = new List<TrafficWaypoint>();

        public float SpeedLimit => speedLimit;
        public TrafficDirection Direction => direction;
        public bool IsIntersectionStopPoint => isIntersectionStopPoint;
        public float StopCheckDistance => stopCheckDistance;
        public IReadOnlyList<TrafficWaypoint> NextWaypoints => nextWaypoints;

        public void SetSpeedLimit(float speed) => speedLimit = speed;
        public void SetDirection(TrafficDirection dir) => direction = dir;
        public void SetStopPoint(bool isStop) => isIntersectionStopPoint = isStop;

        public void AddNextWaypoint(TrafficWaypoint wp)
        {
            if (wp != null && !nextWaypoints.Contains(wp))
            {
                nextWaypoints.Add(wp);
            }
        }

        public void ClearNextWaypoints()
        {
            nextWaypoints.Clear();
        }

        /// <summary>
        /// Lựa chọn ngẫu nhiên điểm đến tiếp theo (hỗ trợ xe rẽ nhánh hoặc đi thẳng tự nhiên).
        /// </summary>
        public TrafficWaypoint GetNextWaypoint()
        {
            if (nextWaypoints == null || nextWaypoints.Count == 0) return null;
            if (nextWaypoints.Count == 1) return nextWaypoints[0];

            int randIdx = Random.Range(0, nextWaypoints.Count);
            return nextWaypoints[randIdx];
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = isIntersectionStopPoint ? Color.red : Color.cyan;
            Gizmos.DrawSphere(transform.position, 0.4f);

            if (nextWaypoints != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var next in nextWaypoints)
                {
                    if (next != null)
                    {
                        Gizmos.DrawLine(transform.position, next.transform.position);
                        // Vẽ mũi tên chỉ hướng
                        Vector3 dir = (next.transform.position - transform.position).normalized;
                        Gizmos.DrawRay(next.transform.position - dir * 0.8f, Quaternion.Euler(0, 30, 0) * (-dir * 0.6f));
                        Gizmos.DrawRay(next.transform.position - dir * 0.8f, Quaternion.Euler(0, -30, 0) * (-dir * 0.6f));
                    }
                }
            }
        }
    }
}
