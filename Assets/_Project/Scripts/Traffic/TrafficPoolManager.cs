using System.Collections.Generic;
using UnityEngine;

namespace HueCity.Traffic
{
    /// <summary>
    /// Trình quản lý Object Pool xe giao thông (Traffic Pool Manager).
    /// Duy trì 8 - 12 phương tiện lưu thông tự động liên tục quanh ngã tư.
    /// Tuân thủ quy chuẩn: 100% Object Pooling, cấm Instantiate/Destroy lúc runtime.
    /// </summary>
    public class TrafficPoolManager : MonoBehaviour
    {
        public static TrafficPoolManager Instance { get; private set; }

        [Header("Pool Configuration")]
        [SerializeField] private int maxActiveVehicles = 8;
        [SerializeField] private float spawnInterval = 3.5f;

        [Header("Route Entry Points")]
        [Tooltip("Các điểm khởi đầu tuyến đường ở 4 rìa bản đồ (Bắc, Nam, Đông, Tây)")]
        [SerializeField] private List<TrafficWaypoint> routeStartPoints = new List<TrafficWaypoint>();

        [Header("Vehicle Prefab Templates")]
        [SerializeField] private GameObject[] vehiclePrefabs;

        private List<TrafficVehicleAI> vehiclePool = new List<TrafficVehicleAI>();
        private float spawnTimer = 0f;

        public int PoolCount => vehiclePool.Count;
        public int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (var v in vehiclePool)
                {
                    if (v != null && v.gameObject.activeSelf) count++;
                }
                return count;
            }
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            InitializePool();
        }

        [Header("Distance Streaming (500m City Optimization)")]
        [SerializeField] private float maxDistanceToPlayer = 110.0f;
        private Transform playerTransform;

        public void SetStreamingRadius(float radius)
        {
            maxDistanceToPlayer = radius;
        }

        private void Update()
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                spawnTimer = spawnInterval;
                TrySpawnNextVehicle();
            }

            CheckDistanceStreaming();
        }

        private void CheckDistanceStreaming()
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
                return;
            }

            Vector3 playerPos = playerTransform.position;
            float maxDistSqr = maxDistanceToPlayer * maxDistanceToPlayer;

            foreach (var v in vehiclePool)
            {
                if (v != null && v.gameObject.activeSelf)
                {
                    if ((v.transform.position - playerPos).sqrMagnitude > maxDistSqr)
                    {
                        v.gameObject.SetActive(false);
                    }
                }
            }
        }

        public void RegisterStartPoint(TrafficWaypoint startWp)
        {
            if (startWp != null && !routeStartPoints.Contains(startWp))
            {
                routeStartPoints.Add(startWp);
            }
        }

        public void InitializeWithPrefabs(GameObject[] prefabs, List<TrafficWaypoint> startPoints, int poolSize = 8)
        {
            vehiclePrefabs = prefabs;
            routeStartPoints = startPoints;
            maxActiveVehicles = poolSize;
            InitializePool();
        }

        private void InitializePool()
        {
            if (vehiclePrefabs == null || vehiclePrefabs.Length == 0) return;

            // Xóa pool cũ nếu có
            foreach (var v in vehiclePool)
            {
                if (v != null)
                {
                    if (Application.isPlaying) Destroy(v.gameObject);
                    else DestroyImmediate(v.gameObject);
                }
            }
            vehiclePool.Clear();

            // Khởi tạo các xe vào Pool
            for (int i = 0; i < maxActiveVehicles; i++)
            {
                GameObject prefab = vehiclePrefabs[i % vehiclePrefabs.Length];
                if (prefab == null) continue;

                GameObject vehicleObj = Instantiate(prefab, transform);
                vehicleObj.name = $"Traffic_Vehicle_{i}";

                TrafficVehicleAI ai = vehicleObj.GetComponent<TrafficVehicleAI>();
                if (ai == null) ai = vehicleObj.AddComponent<TrafficVehicleAI>();

                ai.OnRouteCompleted += HandleVehicleRouteCompleted;
                vehicleObj.SetActive(false);
                vehiclePool.Add(ai);
            }

            // Kích hoạt ngay một vài xe ở các nhánh đường khi đang trong Play Mode
            if (Application.isPlaying)
            {
                for (int i = 0; i < Mathf.Min(4, vehiclePool.Count); i++)
                {
                    TrySpawnNextVehicle();
                }
            }
        }

        private void TrySpawnNextVehicle()
        {
            if (ActiveCount >= maxActiveVehicles || routeStartPoints.Count == 0) return;

            TrafficVehicleAI inactiveCar = GetInactiveVehicle();
            if (inactiveCar == null) return;

            // Chọn ngẫu nhiên một nhánh đường xuất phát
            TrafficWaypoint startPoint = routeStartPoints[Random.Range(0, routeStartPoints.Count)];
            if (startPoint == null) return;

            // Kiểm tra điểm xuất phát có xe nào đang chắn không
            Collider[] colliders = Physics.OverlapSphere(startPoint.transform.position, 2.5f);
            foreach (var col in colliders)
            {
                if (col.GetComponentInParent<TrafficVehicleAI>() != null)
                {
                    return; // Đang có xe ở điểm spawn, chờ lượt sau
                }
            }

            inactiveCar.gameObject.SetActive(true);
            inactiveCar.Initialize(startPoint, Random.Range(3.0f, 5.0f));
        }

        private TrafficVehicleAI GetInactiveVehicle()
        {
            foreach (var v in vehiclePool)
            {
                if (v != null && !v.gameObject.activeSelf)
                {
                    return v;
                }
            }
            return null;
        }

        private void HandleVehicleRouteCompleted(TrafficVehicleAI vehicle)
        {
            if (vehicle != null)
            {
                vehicle.gameObject.SetActive(false);
            }
        }
    }
}
