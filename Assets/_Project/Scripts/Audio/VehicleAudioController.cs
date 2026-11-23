using HueCity.Core;
using HueCity.NPC;
using HueCity.Vehicle;
using UnityEngine;

namespace HueCity.Audio
{
    /// <summary>
    /// Điều khiển toàn diện âm thanh xe cộ (Vehicle Audio Controller).
    /// Tích hợp:
    /// - Dynamic Engine Pitch: Thay đổi cao độ tiếng gầm rú động cơ theo tốc độ di chuyển thực tế.
    /// - Tire Skid Audio: Phát tiếng rít phanh khi đạp thắng ở tốc độ cao.
    /// - Car Horn Audio: Phát tiếng còi xe khi bấm phím H và tự động kích thích NPC Alert xung quanh qua WorldStimulusManager!
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class VehicleAudioController : MonoBehaviour
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource engineAudioSource;
        [SerializeField] private AudioSource skidAudioSource;
        [SerializeField] private AudioSource hornAudioSource;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip engineIdleClip;
        [SerializeField] private AudioClip skidClip;
        [SerializeField] private AudioClip hornClip;

        [Header("Engine Pitch Parameters")]
        [SerializeField] private float minPitch = 0.75f;
        [SerializeField] private float maxPitch = 1.95f;
        [SerializeField] private float maxSpeedForPitch = 90f; // km/h
        [SerializeField] private float pitchSmoothTime = 0.12f;

        [Header("Horn Settings")]
        [SerializeField] private float hornStimulusRadius = 25.0f;

        private CarController carController;
        private float currentPitchVelocity;
        private float targetPitch;

        public AudioSource EngineSource
        {
            get
            {
                if (engineAudioSource == null) SetupAudioSources();
                return engineAudioSource;
            }
        }

        public AudioSource SkidSource
        {
            get
            {
                if (skidAudioSource == null) SetupAudioSources();
                return skidAudioSource;
            }
        }

        public AudioSource HornSource
        {
            get
            {
                if (hornAudioSource == null) SetupAudioSources();
                return hornAudioSource;
            }
        }

        private void Awake()
        {
            carController = GetComponent<CarController>();
            SetupAudioSources();
        }

        private void SetupAudioSources()
        {
            if (carController == null)
            {
                carController = GetComponent<CarController>();
            }

            if (engineAudioSource == null)
            {
                engineAudioSource = gameObject.AddComponent<AudioSource>();
                engineAudioSource.spatialBlend = 0.75f; // 3D Audio
                engineAudioSource.minDistance = 3.0f;
                engineAudioSource.maxDistance = 50.0f;
                engineAudioSource.loop = true;
                engineAudioSource.playOnAwake = false;
            }

            if (skidAudioSource == null)
            {
                skidAudioSource = gameObject.AddComponent<AudioSource>();
                skidAudioSource.spatialBlend = 0.75f;
                skidAudioSource.minDistance = 3.0f;
                skidAudioSource.maxDistance = 40.0f;
                skidAudioSource.loop = false;
                skidAudioSource.playOnAwake = false;
            }

            if (hornAudioSource == null)
            {
                hornAudioSource = gameObject.AddComponent<AudioSource>();
                hornAudioSource.spatialBlend = 0.6f;
                hornAudioSource.minDistance = 5.0f;
                hornAudioSource.maxDistance = 80.0f;
                hornAudioSource.loop = false;
                hornAudioSource.playOnAwake = false;
            }

            if (engineIdleClip != null && engineAudioSource.clip == null) engineAudioSource.clip = engineIdleClip;
            if (skidClip != null && skidAudioSource.clip == null) skidAudioSource.clip = skidClip;
            if (hornClip != null && hornAudioSource.clip == null) hornAudioSource.clip = hornClip;
        }

        private void Start()
        {
            if (engineAudioSource != null && engineAudioSource.clip != null)
            {
                engineAudioSource.pitch = minPitch;
                engineAudioSource.Play();
            }
        }

        private void Update()
        {
            UpdateEngineSound();
            UpdateSkidSound();
            HandleHornInput();
        }

        private void UpdateEngineSound()
        {
            if (engineAudioSource == null || carController == null) return;

            float speedKmh = carController.CurrentSpeedKmh;
            float speedRatio = Mathf.Clamp01(speedKmh / maxSpeedForPitch);

            // Pitch biến thiên phi tuyến theo tốc độ động cơ
            targetPitch = Mathf.Lerp(minPitch, maxPitch, speedRatio);
            engineAudioSource.pitch = Mathf.SmoothDamp(engineAudioSource.pitch, targetPitch, ref currentPitchVelocity, pitchSmoothTime);

            // Âm lượng tăng nhẹ khi tăng tốc
            engineAudioSource.volume = Mathf.Lerp(0.55f, 0.95f, speedRatio);
        }

        private void UpdateSkidSound()
        {
            if (skidAudioSource == null || carController == null || skidClip == null) return;

            // Phát tiếng phanh rít khi đang đạp phanh ở tốc độ trên 12 km/h
            bool shouldSkid = carController.IsBraking && carController.CurrentSpeedKmh > 12f;

            if (shouldSkid)
            {
                if (!skidAudioSource.isPlaying)
                {
                    skidAudioSource.Play();
                }
            }
            else
            {
                if (skidAudioSource.isPlaying)
                {
                    skidAudioSource.Stop();
                }
            }
        }

        private void HandleHornInput()
        {
            if (Input.GetKeyDown(KeyCode.H) || Input.GetMouseButtonDown(2))
            {
                PlayHorn();
            }
        }

        public void PlayHorn()
        {
            if (hornAudioSource != null && hornClip != null)
            {
                hornAudioSource.PlayOneShot(hornClip);
            }

            // Kích phát sự kiện âm thanh tiếng còi ra môi trường để NPC FSM nghe thấy
            WorldStimulusManager.EmitStimulus(transform.position, hornStimulusRadius, StimulusType.CarHorn, gameObject);
        }

        public void ConfigureClips(AudioClip engine, AudioClip skid, AudioClip horn)
        {
            engineIdleClip = engine;
            skidClip = skid;
            hornClip = horn;
            SetupAudioSources();
        }
    }
}
