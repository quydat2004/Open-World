using HueCity.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HueCity.UI
{
    /// <summary>
    /// Đồng hồ đo tốc độ xe cộ (Speedometer UI) góc dưới bên phải màn hình.
    /// Tự động hiển thị khi người chơi ngồi vào ghế lái và ẩn đi khi rời xe.
    /// Hiển thị chỉ số km/h kỹ thuật số và kim chỉ tốc độ trực quan.
    /// </summary>
    public class SpeedometerUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text speedText;
        [SerializeField] private Text unitText;
        [SerializeField] private RectTransform needleTransform;

        [Header("Gauge Configuration")]
        [SerializeField] private float maxSpeedKmh = 140.0f;
        [SerializeField] private float minNeedleAngle = 125.0f; // Góc kim ở 0 km/h
        [SerializeField] private float maxNeedleAngle = -125.0f; // Góc kim ở Max km/h

        private Rigidbody activeVehicleRb;
        private float currentDisplaySpeed = 0f;

        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;
        public float CurrentSpeedKmh => currentDisplaySpeed;

        private void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.OnPlayerEnteredVehicle += HandleEnteredVehicle;
            GameEvents.OnPlayerExitedVehicle += HandleExitedVehicle;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerEnteredVehicle -= HandleEnteredVehicle;
            GameEvents.OnPlayerExitedVehicle -= HandleExitedVehicle;
        }

        private void HandleEnteredVehicle(GameObject player, GameObject vehicle)
        {
            if (vehicle != null)
            {
                activeVehicleRb = vehicle.GetComponent<Rigidbody>();
                if (panelRoot != null) panelRoot.SetActive(true);
            }
        }

        private void HandleExitedVehicle(GameObject player, GameObject vehicle)
        {
            activeVehicleRb = null;
            currentDisplaySpeed = 0f;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void Update()
        {
            if (activeVehicleRb == null || !panelRoot.activeSelf) return;

            // Tính toán vận tốc km/h từ vận tốc mét/giây của Rigidbody
            float realSpeedKmh = activeVehicleRb.velocity.magnitude * 3.6f;
            currentDisplaySpeed = Mathf.Lerp(currentDisplaySpeed, realSpeedKmh, Time.deltaTime * 10f);

            // Cập nhật số điện tử
            if (speedText != null)
            {
                speedText.text = Mathf.RoundToInt(currentDisplaySpeed).ToString();
            }

            // Cập nhật góc quay của kim chỉ số
            if (needleTransform != null)
            {
                float t = Mathf.Clamp01(currentDisplaySpeed / maxSpeedKmh);
                float angle = Mathf.Lerp(minNeedleAngle, maxNeedleAngle, t);
                needleTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        public void SetSpeedManual(float speedKmh)
        {
            currentDisplaySpeed = speedKmh;
            if (speedText != null) speedText.text = Mathf.RoundToInt(speedKmh).ToString();
            if (needleTransform != null)
            {
                float t = Mathf.Clamp01(speedKmh / maxSpeedKmh);
                float angle = Mathf.Lerp(minNeedleAngle, maxNeedleAngle, t);
                needleTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }
}
