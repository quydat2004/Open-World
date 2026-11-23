using HueCity.Core;
using UnityEngine;

namespace HueCity.NPC
{
    /// <summary>
    /// Điều khiển giác quan và xoay đầu tự nhiên (LookAt IK Controller).
    /// Khi người chơi đến gần hoặc có tiếng còi xe / va chạm, nhân vật tự động quay đầu
    /// và liếc mắt nhìn về hướng phát sinh sự kiện trong giới hạn góc tự nhiên (tối đa 60 độ).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterLookAtIK : MonoBehaviour
    {
        [Header("LookAt Sense Settings")]
        [SerializeField] private bool enableLookAt = true;
        [SerializeField] private float maxLookDistance = 7.5f;
        [SerializeField] private float maxLookAngle = 60.0f; // Giới hạn góc cổ người tự nhiên (60 độ)
        [SerializeField] private float lookWeightSmoothSpeed = 4.5f;

        [Header("Weights Distribution")]
        [SerializeField] [Range(0f, 1f)] private float targetWeight = 0.85f;
        [SerializeField] private float bodyWeight = 0.15f;
        [SerializeField] private float headWeight = 0.75f;
        [SerializeField] private float eyesWeight = 1.0f;
        [SerializeField] private float clampWeight = 0.5f;

        private Animator animator;
        private Transform playerTarget;
        private Vector3 currentLookPosition;
        private float currentWeight;
        private bool hasStimulusTarget;
        private Vector3 stimulusPosition;
        private float stimulusTimer;

        public bool EnableLookAt
        {
            get => enableLookAt;
            set => enableLookAt = value;
        }

        public float MaxLookAngle => maxLookAngle;
        public float CurrentWeight => currentWeight;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTarget = player.transform;
            }
        }

        private void OnEnable()
        {
            GameEvents.OnStimulusEmitted += HandleStimulusEvent;
        }

        private void OnDisable()
        {
            GameEvents.OnStimulusEmitted -= HandleStimulusEvent;
        }

        private void Update()
        {
            if (hasStimulusTarget)
            {
                stimulusTimer -= Time.deltaTime;
                if (stimulusTimer <= 0f)
                {
                    hasStimulusTarget = false;
                }
            }
        }

        private void HandleStimulusEvent(Vector3 origin, float radius, StimulusType type, GameObject source)
        {
            if (!enableLookAt) return;

            float dist = Vector3.Distance(transform.position, origin);
            if (dist <= radius)
            {
                stimulusPosition = origin;
                hasStimulusTarget = true;
                stimulusTimer = 3.5f; // Duy trì nhìn về phía âm thanh trong 3.5 giây
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!enableLookAt || animator == null) return;

            Vector3 targetPos = Vector3.zero;
            bool shouldLook = false;

            // 1. Ưu tiên hướng nhìn về nguồn âm thanh kích thích (Còi xe, va chạm)
            if (hasStimulusTarget)
            {
                targetPos = stimulusPosition;
                shouldLook = true;
            }
            // 2. Nếu không có âm thanh, nhìn về phía người chơi nếu ở gần
            else if (playerTarget != null)
            {
                float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);
                if (distToPlayer <= maxLookDistance)
                {
                    targetPos = playerTarget.position + Vector3.up * 1.55f; // Ngang tầm mắt
                    shouldLook = true;
                }
            }

            // 3. Kiểm tra góc nhìn tự nhiên (không quay ngoắt ra sau lưng quá 60 độ)
            if (shouldLook)
            {
                Vector3 dirToTarget = (targetPos - transform.position).normalized;
                dirToTarget.y = 0f;
                float angle = Vector3.Angle(transform.forward, dirToTarget);

                if (angle <= maxLookAngle)
                {
                    // Hướng nhìn hợp lệ trong tầm mắt
                    currentLookPosition = Vector3.Lerp(currentLookPosition, targetPos, Time.deltaTime * lookWeightSmoothSpeed);
                    currentWeight = Mathf.Lerp(currentWeight, targetWeight, Time.deltaTime * lookWeightSmoothSpeed);
                }
                else
                {
                    // Ngoài tầm mắt tự nhiên, từ từ nhìn thẳng lại
                    currentWeight = Mathf.Lerp(currentWeight, 0f, Time.deltaTime * lookWeightSmoothSpeed);
                }
            }
            else
            {
                currentWeight = Mathf.Lerp(currentWeight, 0f, Time.deltaTime * lookWeightSmoothSpeed);
            }

            // 4. Áp dụng LookAt IK vào Animator
            if (currentWeight > 0.01f)
            {
                animator.SetLookAtWeight(currentWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
                animator.SetLookAtPosition(currentLookPosition);
            }
        }

        public void SetLookTarget(Transform target)
        {
            playerTarget = target;
        }
    }
}
