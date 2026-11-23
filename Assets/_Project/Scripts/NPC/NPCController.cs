using UnityEngine;
using UnityEngine.AI;

namespace HueCity.NPC
{
    /// <summary>
    /// Điều khiển NPC thông minh kiểu GTA (Tầng 1 Game AI).
    /// Finite State Machine (FSM) gồm 5 trạng thái: Wander, Activity, Alert, Flee, Confront.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NPCController : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 1.4f;
        [SerializeField] private float runSpeed = 4.2f;

        [Header("Personality Traits (0 to 1)")]
        [Range(0f, 1f)] [SerializeField] private float courage = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float aggression = 0.3f;

        [Header("Wander Settings")]
        [SerializeField] private float wanderRadius = 25f;

        private NavMeshAgent agent;
        private NPCState currentState = NPCState.Wander;
        private float stateTimer = 0f;
        private ActivityPoint currentActivityPoint;
        private Vector3 stimulusSourcePosition;

        public NPCState CurrentState => currentState;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        public void RegisterStimulusListener()
        {
            WorldStimulusManager.OnStimulusEmitted -= HandleStimulus;
            WorldStimulusManager.OnStimulusEmitted += HandleStimulus;
        }

        public void UnregisterStimulusListener()
        {
            WorldStimulusManager.OnStimulusEmitted -= HandleStimulus;
        }

        private void OnEnable()
        {
            RegisterStimulusListener();
            // Khởi tạo tính cách ngẫu nhiên khi spawn từ Pool
            courage = Random.value;
            aggression = Random.value;
            EnterState(NPCState.Wander);
        }

        private void OnDisable()
        {
            UnregisterStimulusListener();
            if (currentActivityPoint != null)
            {
                currentActivityPoint.Release();
                currentActivityPoint = null;
            }
        }

        public void Initialize(Vector3 spawnPosition)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.Warp(spawnPosition);
            }
            else
            {
                transform.position = spawnPosition;
            }
            EnterState(NPCState.Wander);
        }

        private void Update()
        {
            switch (currentState)
            {
                case NPCState.Wander:
                    UpdateWanderState();
                    break;
                case NPCState.Activity:
                    UpdateActivityState();
                    break;
                case NPCState.Alert:
                    UpdateAlertState();
                    break;
                case NPCState.Flee:
                    UpdateFleeState();
                    break;
                case NPCState.Confront:
                    UpdateConfrontState();
                    break;
            }
        }

        private void EnterState(NPCState newState)
        {
            currentState = newState;
            stateTimer = 0f;

            if (agent == null) agent = GetComponent<NavMeshAgent>();

            switch (newState)
            {
                case NPCState.Wander:
                    if (agent != null && agent.isOnNavMesh)
                    {
                        agent.isStopped = false;
                        agent.speed = walkSpeed;
                        PickNewWanderDestination();
                    }
                    break;

                case NPCState.Activity:
                    if (agent != null && agent.isOnNavMesh)
                    {
                        agent.isStopped = true;
                    }
                    break;

                case NPCState.Alert:
                    if (agent != null && agent.isOnNavMesh)
                    {
                        agent.isStopped = true;
                    }
                    // Quay mặt nhìn về hướng âm thanh/nguồn kích thích
                    LookAtPosition(stimulusSourcePosition);
                    stateTimer = Random.Range(2.0f, 3.5f);
                    break;

                case NPCState.Flee:
                    if (agent != null && agent.isOnNavMesh)
                    {
                        agent.isStopped = false;
                        agent.speed = runSpeed;
                        // Chạy trốn theo hướng ngược lại nguồn nguy hiểm
                        Vector3 fleeDir = (transform.position - stimulusSourcePosition).normalized;
                        Vector3 fleeTarget = transform.position + fleeDir * 20f;
                        SetDestinationSafe(fleeTarget);
                    }
                    stateTimer = Random.Range(5.0f, 8.0f);
                    break;

                case NPCState.Confront:
                    if (agent != null && agent.isOnNavMesh)
                    {
                        agent.isStopped = true;
                    }
                    LookAtPosition(stimulusSourcePosition);
                    stateTimer = Random.Range(3.5f, 6.0f);
                    break;
            }
        }

        private void UpdateWanderState()
        {
            if (agent == null || !agent.isOnNavMesh) return;

            // Kiểm tra đến đích thì chọn điểm dạo mới
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f)
            {
                // Xác suất 25% rẽ vào điểm hoạt động (ghế nhựa vỉa hè) nếu có gần đó
                if (Random.value < 0.25f && TryFindNearbyActivityPoint())
                {
                    return;
                }
                PickNewWanderDestination();
            }
        }

        private void UpdateActivityState()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                if (currentActivityPoint != null)
                {
                    currentActivityPoint.Release();
                    currentActivityPoint = null;
                }
                EnterState(NPCState.Wander);
            }
        }

        private void UpdateAlertState()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                EnterState(NPCState.Wander);
            }
        }

        private void UpdateFleeState()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                EnterState(NPCState.Wander);
            }
        }

        private void UpdateConfrontState()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                EnterState(NPCState.Wander);
            }
        }

        private void HandleStimulus(StimulusData data)
        {
            // Kiểm tra bán kính tác động (so sánh bình phương khoảng cách)
            float sqrDist = (transform.position - data.Origin).sqrMagnitude;
            if (sqrDist > data.Radius * data.Radius) return;

            stimulusSourcePosition = data.Origin;

            // Xử lý chuyển trạng thái dựa trên tính cách
            if (data.Type == StimulusType.DangerZone || data.Type == StimulusType.CarCrash)
            {
                // Nguy hiểm cận kề: Sợ hãi thì Flee, hung hăng cao thì Confront
                if (courage < 0.65f || aggression < 0.7f)
                {
                    EnterState(NPCState.Flee);
                }
                else
                {
                    EnterState(NPCState.Confront);
                }
            }
            else if (data.Type == StimulusType.CarHorn || data.Type == StimulusType.EngineRev)
            {
                // Tiếng còi xe/nẹt pô: Dừng lại quay đầu nhìn
                if (currentState != NPCState.Flee && currentState != NPCState.Confront)
                {
                    EnterState(NPCState.Alert);
                }
            }
        }

        private bool TryFindNearbyActivityPoint()
        {
            float checkDistSqr = 12f * 12f;
            foreach (var point in ActivityPoint.AllPoints)
            {
                if (point != null && !point.IsOccupied)
                {
                    float distSqr = (transform.position - point.Spot.position).sqrMagnitude;
                    if (distSqr < checkDistSqr)
                    {
                        if (point.TryOccupy(this, out float duration))
                        {
                            currentActivityPoint = point;
                            stateTimer = duration;
                            EnterState(NPCState.Activity);
                            transform.position = point.Spot.position;
                            transform.rotation = point.Spot.rotation;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void PickNewWanderDestination()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (agent == null || !agent.isOnNavMesh) return;

            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection += transform.position;

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }

        private void SetDestinationSafe(Vector3 target)
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (agent == null || !agent.isOnNavMesh) return;

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }

        private void LookAtPosition(Vector3 pos)
        {
            Vector3 dir = (pos - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }
}
