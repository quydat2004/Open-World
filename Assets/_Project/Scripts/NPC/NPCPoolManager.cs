using System.Collections.Generic;
using HueCity.Player;
using UnityEngine;
using UnityEngine.AI;

namespace HueCity.NPC
{
    /// <summary>
    /// Quản lý Object Pool cho NPC đi bộ.
    /// Duy trì ổn định 15 - 25 NPC xung quanh người chơi mà không Instantiate/Destroy liên tục.
    /// </summary>
    public class NPCPoolManager : MonoBehaviour
    {
        [Header("Pool Configuration")]
        [SerializeField] private GameObject[] npcPrefabs;
        [SerializeField] private int maxActiveNPCs = 20;
        [SerializeField] private float spawnRadiusMin = 18f;
        [SerializeField] private float spawnRadiusMax = 42f;
        [SerializeField] private float despawnDistance = 55f;
        [SerializeField] private float checkInterval = 2f;

        private readonly List<NPCController> pool = new List<NPCController>();
        private readonly List<NPCController> activeNPCs = new List<NPCController>();
        private Transform playerTransform;
        private float nextCheckTime;

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }

            InitializePool();
        }

        private void InitializePool()
        {
            if (npcPrefabs == null || npcPrefabs.Length == 0)
            {
                Debug.LogWarning("[NPCPoolManager] Chưa gán Prefab NPC! Sẽ tự tạo prefab mặc định.");
                return;
            }

            for (int i = 0; i < maxActiveNPCs; i++)
            {
                GameObject prefab = npcPrefabs[i % npcPrefabs.Length];
                GameObject obj = Instantiate(prefab, transform);
                obj.name = $"NPC_Pooled_{i}";
                NPCController controller = obj.GetComponent<NPCController>();
                if (controller == null)
                {
                    controller = obj.AddComponent<NPCController>();
                }
                if (obj.GetComponent<CharacterFootIK>() == null) obj.AddComponent<CharacterFootIK>();
                if (obj.GetComponent<CharacterLookAtIK>() == null) obj.AddComponent<CharacterLookAtIK>();
                if (obj.GetComponent<NPCIdleBehavior>() == null) obj.AddComponent<NPCIdleBehavior>();
                obj.SetActive(false);
                pool.Add(controller);
            }
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                // Thử tìm player nếu chưa cache
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
                return;
            }

            if (Time.time >= nextCheckTime)
            {
                nextCheckTime = Time.time + checkInterval;
                MaintainCrowd();
            }
        }

        private void MaintainCrowd()
        {
            Vector3 playerPos = playerTransform.position;
            float despawnSqr = despawnDistance * despawnDistance;

            // 1. Thu hồi các NPC đi quá xa người chơi
            for (int i = activeNPCs.Count - 1; i >= 0; i--)
            {
                NPCController npc = activeNPCs[i];
                if ((npc.transform.position - playerPos).sqrMagnitude > despawnSqr)
                {
                    RecycleNPC(npc);
                }
            }

            // 2. Spawn thêm nếu số lượng hiện tại ít hơn giới hạn
            while (activeNPCs.Count < maxActiveNPCs && pool.Count > 0)
            {
                if (TryGetSpawnPosition(playerPos, out Vector3 spawnPos))
                {
                    SpawnNPC(spawnPos);
                }
                else
                {
                    break;
                }
            }
        }

        private bool TryGetSpawnPosition(Vector3 center, out Vector3 result)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(spawnRadiusMin, spawnRadiusMax);
                Vector3 candidate = center + new Vector3(circle.x, 0f, circle.y);

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }
            result = Vector3.zero;
            return false;
        }

        private void SpawnNPC(Vector3 position)
        {
            if (pool.Count == 0) return;

            NPCController npc = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);

            npc.gameObject.SetActive(true);
            npc.Initialize(position);
            activeNPCs.Add(npc);
        }

        private void RecycleNPC(NPCController npc)
        {
            activeNPCs.Remove(npc);
            npc.gameObject.SetActive(false);
            pool.Add(npc);
        }
    }
}
