using UnityEngine;

namespace HueCity.NPC
{
    /// <summary>
    /// Điều khiển các cử chỉ nhỏ tự nhiên khi đứng yên (Idle Variations).
    /// Giúp nhân vật không bị đứng cứng đờ như tượng sáp:
    /// Tự động đổi dáng đứng, chuyển trọng tâm chân, khoanh tay, nhìn quanh sau mỗi 8-15 giây.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class NPCIdleBehavior : MonoBehaviour
    {
        [Header("Variation Timers")]
        [SerializeField] private float minInterval = 8.0f;
        [SerializeField] private float maxInterval = 15.0f;
        [SerializeField] private int totalVariations = 3;

        private Animator animator;
        private float timer;
        private int currentVariation = 0;
        private static readonly int IdleVariationHash = Animator.StringToHash("IdleVariation");

        public int CurrentVariation => currentVariation;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            ResetTimer();
        }

        private void Update()
        {
            if (animator == null) return;

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                TriggerRandomVariation();
                ResetTimer();
            }
        }

        private void TriggerRandomVariation()
        {
            // Chọn ngẫu nhiên biến thể (0: Đứng thở tự nhiên, 1: Nhìn quanh đổi trọng tâm, 2: Khoanh tay xốc áo)
            int nextVar = Random.Range(0, totalVariations);
            currentVariation = nextVar;
            animator.SetInteger(IdleVariationHash, currentVariation);
        }

        private void ResetTimer()
        {
            timer = Random.Range(minInterval, maxInterval);
        }

        public void ForceVariation(int variationIndex)
        {
            currentVariation = Mathf.Clamp(variationIndex, 0, totalVariations - 1);
            if (animator != null)
            {
                animator.SetInteger(IdleVariationHash, currentVariation);
            }
        }
    }
}
