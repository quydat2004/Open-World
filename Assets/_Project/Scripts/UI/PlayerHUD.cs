using System.Collections;
using HueCity.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HueCity.UI
{
    /// <summary>
    /// Bảng trạng thái người chơi & Thông báo (Player Status & Notification HUD).
    /// Hiển thị số dư tiền mặt hiện tại và Banner pop-up thông báo khi có sự kiện.
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Money Counter")]
        [SerializeField] private Text moneyText;
        [SerializeField] private int displayMoney = 0;

        [Header("Banner Notification")]
        [SerializeField] private GameObject bannerRoot;
        [SerializeField] private Text bannerText;
        [SerializeField] private Image bannerBackground;

        private Coroutine bannerCoroutine;

        public int DisplayMoney => displayMoney;
        public bool IsBannerVisible => bannerRoot != null && bannerRoot.activeSelf;

        private void Awake()
        {
            if (bannerRoot != null) bannerRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.OnMoneyChanged += HandleMoneyChanged;
            GameEvents.OnNotificationRequested += ShowNotification;
        }

        private void OnDisable()
        {
            GameEvents.OnMoneyChanged -= HandleMoneyChanged;
            GameEvents.OnNotificationRequested -= ShowNotification;
        }

        public void SetMoney(int amount)
        {
            displayMoney = amount;
            if (moneyText != null)
            {
                moneyText.text = $"💰 {amount:N0} đ";
            }
        }

        private void HandleMoneyChanged(int totalMoney, int deltaMoney)
        {
            SetMoney(totalMoney);
        }

        public void ShowNotification(string message, float duration = 3.0f, Color bannerColor = default)
        {
            if (bannerCoroutine != null)
            {
                StopCoroutine(bannerCoroutine);
            }

            bannerCoroutine = StartCoroutine(NotificationRoutine(message, duration, bannerColor));
        }

        private IEnumerator NotificationRoutine(string message, float duration, Color bannerColor)
        {
            if (bannerRoot == null || bannerText == null) yield break;

            bannerText.text = message;
            if (bannerBackground != null && bannerColor != default)
            {
                bannerBackground.color = new Color(bannerColor.r, bannerColor.g, bannerColor.b, 0.85f);
            }

            bannerRoot.SetActive(true);

            yield return new WaitForSeconds(duration);

            bannerRoot.SetActive(false);
            bannerCoroutine = null;
        }
    }
}
