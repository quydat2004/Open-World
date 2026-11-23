using HueCity.Vehicle;
using UnityEngine;

namespace HueCity.Vehicle
{
    /// <summary>
    /// Điều khiển hiệu ứng hình ảnh (VFX & Ánh sáng) cho phương tiện xe hơi.
    /// Bao gồm:
    /// - Khói xả ống bô (Exhaust Smoke Particle System): lượng khói tăng cường khi đạp ga.
    /// - Đèn pha chiếu sáng (Headlights Spotlights): chiếu sáng rọi mặt đường khi trời tối hoặc trời mưa.
    /// - Đèn phanh đỏ (Brake Lights Pointlights & Emission): rực sáng tức thì khi người chơi đạp phanh.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class VehicleVFXController : MonoBehaviour
    {
        [Header("Exhaust Smoke VFX")]
        [SerializeField] private ParticleSystem exhaustSmoke;
        [SerializeField] private float idleEmissionRate = 5f;
        [SerializeField] private float maxThrottleEmissionRate = 28f;

        [Header("Headlights (Đèn pha)")]
        [SerializeField] private Light leftHeadlight;
        [SerializeField] private Light rightHeadlight;
        [SerializeField] private bool headlightsEnabled = true;

        [Header("Brake Lights (Đèn hậu / Đèn phanh)")]
        [SerializeField] private Light leftBrakeLight;
        [SerializeField] private Light rightBrakeLight;
        [SerializeField] private float normalBrakeIntensity = 0.2f;
        [SerializeField] private float activeBrakeIntensity = 1.8f;

        private CarController carController;
        private ParticleSystem.EmissionModule emissionModule;

        public bool HeadlightsEnabled => headlightsEnabled;
        public bool IsBrakingVFXActive => carController != null && carController.IsBraking;
        public ParticleSystem ExhaustSmoke => exhaustSmoke;

        private void Awake()
        {
            carController = GetComponent<CarController>();

            if (exhaustSmoke != null)
            {
                emissionModule = exhaustSmoke.emission;
            }

            SetupDefaultLights();
        }

        private void SetupDefaultLights()
        {
            SetHeadlights(headlightsEnabled);
            SetBrakeLights(false);
        }

        private void Update()
        {
            UpdateExhaustSmoke();
            UpdateBrakeLights();
            HandleLightToggleInput();
        }

        private void UpdateExhaustSmoke()
        {
            if (exhaustSmoke == null || carController == null) return;

            float speedRatio = Mathf.Clamp01(carController.CurrentSpeedKmh / 80f);
            float targetRate = Mathf.Lerp(idleEmissionRate, maxThrottleEmissionRate, speedRatio);
            emissionModule.rateOverTime = targetRate;
        }

        private void UpdateBrakeLights()
        {
            if (carController == null) return;

            bool isBraking = carController.IsBraking;
            SetBrakeLights(isBraking);
        }

        private void HandleLightToggleInput()
        {
            // Phím L để bật/tắt đèn pha xe
            if (Input.GetKeyDown(KeyCode.L))
            {
                SetHeadlights(!headlightsEnabled);
            }
        }

        public void SetHeadlights(bool enable)
        {
            headlightsEnabled = enable;
            if (leftHeadlight != null) leftHeadlight.enabled = enable;
            if (rightHeadlight != null) rightHeadlight.enabled = enable;
        }

        private void SetBrakeLights(bool isBraking)
        {
            float intensity = isBraking ? activeBrakeIntensity : normalBrakeIntensity;
            if (leftBrakeLight != null)
            {
                leftBrakeLight.intensity = intensity;
                leftBrakeLight.enabled = isBraking || normalBrakeIntensity > 0.05f;
            }
            if (rightBrakeLight != null)
            {
                rightBrakeLight.intensity = intensity;
                rightBrakeLight.enabled = isBraking || normalBrakeIntensity > 0.05f;
            }
        }

        public void Configure(ParticleSystem smoke, Light leftHead, Light rightHead, Light leftBrake, Light rightBrake)
        {
            exhaustSmoke = smoke;
            if (exhaustSmoke != null) emissionModule = exhaustSmoke.emission;
            leftHeadlight = leftHead;
            rightHeadlight = rightHead;
            leftBrakeLight = leftBrake;
            rightBrakeLight = rightBrake;
            SetupDefaultLights();
        }
    }
}
