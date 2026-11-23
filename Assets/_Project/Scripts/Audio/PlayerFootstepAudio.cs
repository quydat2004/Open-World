using HueCity.Player;
using UnityEngine;

namespace HueCity.Audio
{
    /// <summary>
    /// Điều khiển âm thanh tiếng bước chân cho người chơi (Player Footstep Audio).
    /// Tự động phát âm thanh nhịp chân ngắt quãng khi di chuyển trên vỉa hè / mặt đường.
    /// Nhịp chân tự động tăng tốc khi người chơi chuyển sang chế độ chạy nước rút (Sprint).
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerFootstepAudio : MonoBehaviour
    {
        [Header("Audio Configuration")]
        [SerializeField] private AudioSource footstepSource;
        [SerializeField] private AudioClip footstepClip;

        [Header("Step Interval Settings")]
        [SerializeField] private float walkStepInterval = 0.48f;
        [SerializeField] private float runStepInterval = 0.30f;
        [SerializeField] private float baseVolume = 0.55f;

        private PlayerMovement playerMovement;
        private CharacterController characterController;
        private float stepTimer;

        public AudioSource FootstepSource
        {
            get
            {
                if (footstepSource == null) SetupAudioSources();
                return footstepSource;
            }
        }

        private void Awake()
        {
            SetupAudioSources();
        }

        private void SetupAudioSources()
        {
            if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
            if (characterController == null) characterController = GetComponent<CharacterController>();

            if (footstepSource == null)
            {
                footstepSource = gameObject.AddComponent<AudioSource>();
                footstepSource.spatialBlend = 0.5f;
                footstepSource.playOnAwake = false;
                footstepSource.loop = false;
                footstepSource.volume = baseVolume;
            }

            if (footstepClip != null && footstepSource.clip == null)
            {
                footstepSource.clip = footstepClip;
            }
        }

        private void Update()
        {
            if (playerMovement == null || characterController == null) return;

            // Chỉ phát tiếng bước chân khi đang tiếp đất và di chuyển
            bool isMoving = playerMovement.IsMoving && characterController.isGrounded;

            if (isMoving)
            {
                float currentInterval = playerMovement.IsSprinting ? runStepInterval : walkStepInterval;
                stepTimer += Time.deltaTime;

                if (stepTimer >= currentInterval)
                {
                    PlayFootstep(playerMovement.IsSprinting);
                    stepTimer = 0f;
                }
            }
            else
            {
                stepTimer = 0.1f; // Sẵn sàng phát ngay bước đầu tiên khi bắt đầu bước đi
            }
        }

        private void PlayFootstep(bool isSprinting)
        {
            if (footstepSource == null || footstepClip == null) return;

            // Biến thiên nhẹ pitch (0.92 - 1.08) để bước chân không bị đơn điệu máy móc
            float randomPitch = Random.Range(0.92f, 1.08f);
            if (isSprinting) randomPitch += 0.12f;

            footstepSource.pitch = randomPitch;
            footstepSource.PlayOneShot(footstepClip, isSprinting ? baseVolume * 1.25f : baseVolume);
        }

        public void ConfigureClip(AudioClip clip)
        {
            footstepClip = clip;
            if (footstepSource != null) footstepSource.clip = clip;
        }
    }
}
