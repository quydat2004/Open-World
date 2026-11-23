using UnityEngine;

namespace HueCity.Player
{
    /// <summary>
    /// Điều khiển Foot IK (Grounding) cho nhân vật (Player & NPC).
    /// Đảm bảo bàn chân luôn bám sát và song song với độ dốc mặt đường / gờ bó vỉa hè.
    /// Khắc phục triệt để hiện tượng bàn chân bị lơ lửng trên không hoặc lún sâu xuống đất.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterFootIK : MonoBehaviour
    {
        [Header("Foot IK Configuration")]
        [SerializeField] private bool enableFootIK = true;
        [SerializeField] [Range(0f, 1f)] private float ikWeight = 1.0f;
        [SerializeField] private LayerMask groundLayer = ~0; // Mặc định raycast mọi collider mặt đất
        [SerializeField] private float raycastDownDistance = 1.1f;
        [SerializeField] private float footYOffset = 0.08f;
        [SerializeField] private float pelvisSmoothSpeed = 8.0f;

        [Header("Pelvis / Hip Height Adjustment")]
        [SerializeField] private bool adjustPelvisHeight = true;
        [SerializeField] private float maxPelvisOffset = 0.35f;

        private Animator animator;
        private Vector3 leftFootIKPosition;
        private Vector3 rightFootIKPosition;
        private Quaternion leftFootIKRotation;
        private Quaternion rightFootIKRotation;
        private float lastPelvisOffsetY;

        public bool EnableFootIK
        {
            get => enableFootIK;
            set => enableFootIK = value;
        }

        public float IKWeight
        {
            get => ikWeight;
            set => ikWeight = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!enableFootIK || animator == null) return;

            // 1. Lấy vị trí gốc của 2 bàn chân từ Animation
            Vector3 leftFootPos = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            Vector3 rightFootPos = animator.GetIKPosition(AvatarIKGoal.RightFoot);

            // 2. Tính toán Foot IK cho chân trái
            bool leftHit = SolveFootIK(AvatarIKGoal.LeftFoot, leftFootPos, ref leftFootIKPosition, ref leftFootIKRotation);

            // 3. Tính toán Foot IK cho chân phải
            bool rightHit = SolveFootIK(AvatarIKGoal.RightFoot, rightFootPos, ref rightFootIKPosition, ref rightFootIKRotation);

            // 4. Hạ thấp trọng tâm hông (Pelvis) nếu một trong hai chân phải bước xuống bậc thấp
            if (adjustPelvisHeight && (leftHit || rightHit))
            {
                float leftDiff = leftFootIKPosition.y - leftFootPos.y;
                float rightDiff = rightFootIKPosition.y - rightFootPos.y;
                float lowestDiff = Mathf.Min(leftDiff, rightDiff);

                if (lowestDiff < 0f)
                {
                    float targetOffset = Mathf.Clamp(lowestDiff, -maxPelvisOffset, 0f);
                    lastPelvisOffsetY = Mathf.Lerp(lastPelvisOffsetY, targetOffset, Time.deltaTime * pelvisSmoothSpeed);
                    Vector3 pelvisPos = animator.bodyPosition + Vector3.up * lastPelvisOffsetY;
                    animator.bodyPosition = pelvisPos;
                }
                else
                {
                    lastPelvisOffsetY = Mathf.Lerp(lastPelvisOffsetY, 0f, Time.deltaTime * pelvisSmoothSpeed);
                }
            }

            // 5. Áp dụng vị trí và góc xoay bám dốc mặt đường cho 2 bàn chân
            if (leftHit)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, ikWeight);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, ikWeight);
                animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFootIKPosition);
                animator.SetIKRotation(AvatarIKGoal.LeftFoot, leftFootIKRotation);
            }

            if (rightHit)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, ikWeight);
                animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, ikWeight);
                animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFootIKPosition);
                animator.SetIKRotation(AvatarIKGoal.RightFoot, rightFootIKRotation);
            }
        }

        private bool SolveFootIK(AvatarIKGoal foot, Vector3 footOriginalPos, ref Vector3 targetIKPos, ref Quaternion targetIKRot)
        {
            // Bắn tia Raycast từ phía trên bàn chân xuống dưới để tìm mặt đường
            Vector3 rayOrigin = footOriginalPos + Vector3.up * 0.45f;
            Ray ray = new Ray(rayOrigin, Vector3.down);

            if (Physics.Raycast(ray, out RaycastHit hit, raycastDownDistance, groundLayer, QueryTriggerInteraction.Ignore))
            {
                // Vị trí bàn chân khớp chuẩn cao độ mặt đất + khoảng đệm bàn chân
                targetIKPos = footOriginalPos;
                targetIKPos.y = hit.point.y + footYOffset;

                // Góc xoay bàn chân song song với pháp tuyến mặt dốc / vỉa hè
                Quaternion footRotation = animator.GetIKRotation(foot);
                targetIKRot = Quaternion.FromToRotation(transform.up, hit.normal) * footRotation;
                return true;
            }

            return false;
        }
    }
}
