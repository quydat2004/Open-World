using System;
using UnityEngine;

namespace HueCity.Core
{
    /// <summary>
    /// Hệ thống Chu kỳ Ngày / Đêm (Day/Night Cycle) cho Cố Đô Huế.
    /// Quản lý vị trí mặt trời, nhiệt độ màu ánh sáng, ambient light và phát sự kiện bật/tắt đèn đường.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        [Header("Time Configuration")]
        [Tooltip("Thời gian hiện tại trong ngày (0 - 24 giờ)")]
        [Range(0f, 24f)] [SerializeField] private float timeOfDay = 17.0f; // Mặc định 17h00 - Giờ vàng Cố Đô

        [Tooltip("Thời gian (giây) để trải qua 1 ngày đầy đủ. Mặc định 360 giây (6 phút).")]
        [SerializeField] private float dayDurationInSeconds = 360f;

        [Tooltip("Tạm dừng chu kỳ thời gian để cố định góc chiếu nắng (thích hợp cho testing/look-dev)")]
        [SerializeField] private bool pauseTime = false;

        [Header("Sun & Lighting References")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Gradient sunColorGradient;
        [SerializeField] private AnimationCurve sunIntensityCurve;

        [Header("Street Lighting Automation")]
        [SerializeField] private GameObject[] nightLights;

        public static event Action<float> OnHourChanged;
        public static event Action<bool> OnNightStateChanged;

        public float CurrentHour => timeOfDay;
        public bool IsNight => timeOfDay < 5.5f || timeOfDay > 18.2f;

        private bool previousNightState = false;
        private int previousHour = -1;

        private void Awake()
        {
            if (sunLight == null)
            {
                sunLight = GetComponent<Light>();
                if (sunLight == null)
                {
                    Light[] lights = FindObjectsOfType<Light>();
                    foreach (var l in lights)
                    {
                        if (l.type == LightType.Directional)
                        {
                            sunLight = l;
                            break;
                        }
                    }
                }
            }

            SetupDefaultCurves();
        }

        private void Start()
        {
            UpdateLighting();
        }

        private void Update()
        {
            if (!pauseTime && dayDurationInSeconds > 0f)
            {
                timeOfDay += (Time.deltaTime / dayDurationInSeconds) * 24f;
                if (timeOfDay >= 24f)
                {
                    timeOfDay -= 24f;
                }
            }

            UpdateLighting();
            CheckEvents();
        }

        public void SetTimeOfDay(float hour)
        {
            timeOfDay = Mathf.Clamp(hour, 0f, 24f);
            UpdateLighting();
            CheckEvents();
        }

        private void UpdateLighting()
        {
            if (sunLight == null) return;

            // Góc quay mặt trời theo thời gian
            // 0h: -90 độ (dưới đáy), 6h: 0 độ (mọc), 12h: 90 độ (đỉnh đầu), 18h: 180 độ (lặn)
            float sunAngle = (timeOfDay / 24f) * 360f - 90f;
            sunLight.transform.rotation = Quaternion.Euler(sunAngle, -35f, 0f);

            // Tỉ lệ chuẩn hóa thời gian (0.0 đến 1.0)
            float normalizedTime = timeOfDay / 24f;

            // Đổi màu sắc ánh nắng và cường độ
            if (sunColorGradient != null)
            {
                sunLight.color = sunColorGradient.Evaluate(normalizedTime);
            }

            if (sunIntensityCurve != null)
            {
                sunLight.intensity = sunIntensityCurve.Evaluate(normalizedTime);
            }

            // Đồng bộ Ambient Light cho bầu không khí tự nhiên
            Color ambientColor = Color.Lerp(
                new Color(0.12f, 0.15f, 0.22f), // Ban đêm xanh sẫm
                new Color(0.40f, 0.42f, 0.46f), // Ban ngày sáng ấm
                Mathf.Clamp01(sunLight.intensity / 1.2f)
            );
            RenderSettings.ambientLight = ambientColor;
        }

        private void CheckEvents()
        {
            int currentIntHour = Mathf.FloorToInt(timeOfDay);
            if (currentIntHour != previousHour)
            {
                previousHour = currentIntHour;
                OnHourChanged?.Invoke(timeOfDay);
            }

            bool nightState = IsNight;
            if (nightState != previousNightState)
            {
                previousNightState = nightState;
                OnNightStateChanged?.Invoke(nightState);

                // Tự động bật/tắt đèn đường đêm
                if (nightLights != null)
                {
                    foreach (var lightObj in nightLights)
                    {
                        if (lightObj != null)
                        {
                            lightObj.SetActive(nightState);
                        }
                    }
                }
            }
        }

        private void SetupDefaultCurves()
        {
            if (sunColorGradient == null || sunColorGradient.colorKeys.Length <= 1)
            {
                sunColorGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[5];
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];

                // 00:00 - Đêm (Ánh trăng xanh mờ)
                colorKeys[0] = new GradientColorKey(new Color(0.18f, 0.22f, 0.35f), 0.0f);
                // 05:30 - Bình minh sông Hương (Cam ửng hồng)
                colorKeys[1] = new GradientColorKey(new Color(1.0f, 0.65f, 0.45f), 0.23f);
                // 12:00 - Giữa trưa (Vàng tươi trong trẻo)
                colorKeys[2] = new GradientColorKey(new Color(1.0f, 0.98f, 0.92f), 0.50f);
                // 17:00 - Nắng vàng Cố Đô (Hoàng hôn mật ong)
                colorKeys[3] = new GradientColorKey(new Color(1.0f, 0.82f, 0.55f), 0.71f);
                // 19:00 - Chạng vạng (Tím hoàng hôn)
                colorKeys[4] = new GradientColorKey(new Color(0.35f, 0.25f, 0.45f), 0.79f);

                alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
                alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);

                sunColorGradient.SetKeys(colorKeys, alphaKeys);
            }

            if (sunIntensityCurve == null || sunIntensityCurve.length <= 1)
            {
                sunIntensityCurve = new AnimationCurve();
                sunIntensityCurve.AddKey(new Keyframe(0.0f, 0.08f));   // Đêm
                sunIntensityCurve.AddKey(new Keyframe(0.22f, 0.15f));  // Rạng đông
                sunIntensityCurve.AddKey(new Keyframe(0.28f, 0.85f));  // Sáng sớm
                sunIntensityCurve.AddKey(new Keyframe(0.50f, 1.35f));  // Trưa
                sunIntensityCurve.AddKey(new Keyframe(0.71f, 1.25f));  // Hoàng hôn Cố Đô
                sunIntensityCurve.AddKey(new Keyframe(0.78f, 0.25f));  // Chạng vạng
                sunIntensityCurve.AddKey(new Keyframe(0.85f, 0.08f));  // Tối
                sunIntensityCurve.AddKey(new Keyframe(1.0f, 0.08f));   // Đêm
            }
        }
    }
}
