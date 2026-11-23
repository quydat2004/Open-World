using HueCity.Core;
using UnityEngine;

namespace HueCity.Environment
{
    /// <summary>
    /// Điều khiển Thời tiết & Khí hậu Cố Đô Huế (Weather Manager).
    /// Hỗ trợ chuyển đổi giữa Trời quang (Sunny) và Mưa rào xứ Huế (Rainy):
    /// - Điều khiển hệ thống hạt mưa rơi (Particle System).
    /// - Điều khiển âm thanh mưa rơi rào rào (Rain Ambient Loop AudioSource).
    /// - Hiệu ứng đường ướt (Wet Road Reflection): Tự động tăng Smoothness của vật liệu mặt đường nhựa khi trời mưa tạo độ bóng bẩy phản chiếu ánh đèn.
    /// - Phát sự kiện GameEvents.OnWeatherRainChanged để NPC tìm nơi trú mưa (Shelter).
    /// </summary>
    public class WeatherManager : MonoBehaviour
    {
        public static WeatherManager Instance { get; private set; }

        [Header("Weather Status")]
        [SerializeField] private bool isRaining = false;
        [SerializeField] private float weatherTransitionSpeed = 0.5f;

        [Header("Rain Visuals")]
        [SerializeField] private ParticleSystem rainParticleSystem;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private float drySmoothness = 0.28f;
        [SerializeField] private float wetSmoothness = 0.82f;

        [Header("Rain Audio")]
        [SerializeField] private AudioSource rainAudioSource;
        [SerializeField] private AudioClip rainAudioClip;
        [SerializeField] private float maxRainVolume = 0.65f;

        [Header("Player Tracking")]
        [SerializeField] private Transform playerTransform;

        private float currentWetness;
        private float targetWetness;
        private float currentVolume;

        public bool IsRaining => isRaining;
        public float CurrentWetness => currentWetness;
        public ParticleSystem RainParticles => rainParticleSystem;
        public AudioSource RainAudio => rainAudioSource;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SetupAudio();
        }

        private void SetupAudio()
        {
            if (rainAudioSource == null)
            {
                rainAudioSource = gameObject.AddComponent<AudioSource>();
                rainAudioSource.loop = true;
                rainAudioSource.playOnAwake = false;
                rainAudioSource.spatialBlend = 0f; // 2D Ambient
                rainAudioSource.volume = 0f;
            }

            if (rainAudioClip != null)
            {
                rainAudioSource.clip = rainAudioClip;
            }
        }

        private void Start()
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
            }

            currentWetness = isRaining ? wetSmoothness : drySmoothness;
            targetWetness = currentWetness;
            ApplyRoadWetness(currentWetness);

            if (isRaining)
            {
                if (rainParticleSystem != null && !rainParticleSystem.isPlaying) rainParticleSystem.Play();
                if (rainAudioSource != null && rainAudioSource.clip != null) rainAudioSource.Play();
            }
        }

        private void Update()
        {
            // Phím tắt P để người chơi bấm thử nghiệm thời tiết mưa/nắng
            if (Input.GetKeyDown(KeyCode.P))
            {
                ToggleRain();
            }

            // Theo chân người chơi để hạt mưa luôn bao phủ xung quanh camera
            if (playerTransform != null && rainParticleSystem != null)
            {
                Vector3 pPos = playerTransform.position;
                rainParticleSystem.transform.position = new Vector3(pPos.x, pPos.y + 12f, pPos.z);
            }

            // Chuyển đổi mượt độ ướt mặt đường
            targetWetness = isRaining ? wetSmoothness : drySmoothness;
            currentWetness = Mathf.MoveTowards(currentWetness, targetWetness, Time.deltaTime * weatherTransitionSpeed);
            ApplyRoadWetness(currentWetness);

            // Chuyển đổi mượt âm lượng mưa
            if (rainAudioSource != null && rainAudioSource.clip != null)
            {
                float targetVol = isRaining ? maxRainVolume : 0f;
                currentVolume = Mathf.MoveTowards(rainAudioSource.volume, targetVol, Time.deltaTime * 0.8f);
                rainAudioSource.volume = currentVolume;

                if (isRaining && !rainAudioSource.isPlaying && currentVolume > 0.01f)
                {
                    rainAudioSource.Play();
                }
                else if (!isRaining && rainAudioSource.isPlaying && currentVolume <= 0.001f)
                {
                    rainAudioSource.Stop();
                }
            }
        }

        private void ApplyRoadWetness(float smoothness)
        {
            if (roadMaterial != null)
            {
                roadMaterial.SetFloat("_Smoothness", smoothness);
            }
        }

        public void SetRaining(bool rain)
        {
            if (isRaining == rain) return;

            isRaining = rain;

            if (isRaining)
            {
                if (rainParticleSystem != null) rainParticleSystem.Play();
                GameEvents.TriggerNotification("🌧 Trời bắt đầu đổ mưa rào xứ Huế...", 3.0f, new Color(0.4f, 0.8f, 1f));
            }
            else
            {
                if (rainParticleSystem != null) rainParticleSystem.Stop();
                GameEvents.TriggerNotification("☀️ Mưa tạnh, Cố Đô hửng nắng vàng rực rỡ.", 3.0f, new Color(1f, 0.9f, 0.3f));
            }

            GameEvents.TriggerWeatherRainChanged(isRaining);
        }

        public void ToggleRain()
        {
            SetRaining(!isRaining);
        }

        public void Configure(ParticleSystem ps, Material roadMat, AudioClip rainClip)
        {
            rainParticleSystem = ps;
            roadMaterial = roadMat;
            rainAudioClip = rainClip;
            SetupAudio();
        }
    }
}
