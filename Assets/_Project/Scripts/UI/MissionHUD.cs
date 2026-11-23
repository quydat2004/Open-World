using HueCity.Core;
using HueCity.Mission;
using UnityEngine;
using UnityEngine.UI;

namespace HueCity.UI
{
    /// <summary>
    /// Bảng hiển thị thông tin Nhiệm vụ (Mission HUD) góc trên bên trái màn hình.
    /// Cập nhật thời gian thực tiêu đề, mô tả mục tiêu, tiền thưởng và đồng hồ đếm ngược.
    /// </summary>
    public class MissionHUD : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text rewardText;

        private string manualTitle = "";

        public bool IsVisible => panelRoot != null ? panelRoot.activeSelf : gameObject.activeSelf;
        public string DisplayedTitle => titleText != null ? titleText.text : manualTitle;

        private void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.OnMissionStarted += HandleMissionStarted;
            GameEvents.OnMissionProgress += HandleMissionProgress;
            GameEvents.OnMissionCompleted += HandleMissionCompleted;
            GameEvents.OnMissionFailed += HandleMissionFailed;
        }

        private void OnDisable()
        {
            GameEvents.OnMissionStarted -= HandleMissionStarted;
            GameEvents.OnMissionProgress -= HandleMissionProgress;
            GameEvents.OnMissionCompleted -= HandleMissionCompleted;
            GameEvents.OnMissionFailed -= HandleMissionFailed;
        }

        private void Update()
        {
            if (panelRoot == null || !panelRoot.activeSelf) return;

            // Cập nhật đồng hồ đếm ngược
            if (MissionManager.Instance != null && MissionManager.Instance.IsMissionActive)
            {
                float timeLeft = MissionManager.Instance.RemainingTimer;
                if (timerText != null)
                {
                    if (MissionManager.Instance.ActiveMission != null && MissionManager.Instance.ActiveMission.TimeLimit > 0f)
                    {
                        int minutes = Mathf.FloorToInt(timeLeft / 60f);
                        int seconds = Mathf.FloorToInt(timeLeft % 60f);
                        timerText.text = $"⏱ {minutes:00}:{seconds:00}";
                        timerText.color = (timeLeft < 30f) ? new Color(1.0f, 0.2f, 0.2f) : Color.white;
                    }
                    else
                    {
                        timerText.text = "⏱ Không giới hạn";
                        timerText.color = Color.white;
                    }
                }
            }
        }

        private void HandleMissionStarted(MissionData mission)
        {
            if (mission == null) return;

            if (titleText != null) titleText.text = mission.Title;
            if (objectiveText != null) objectiveText.text = "Đến điểm nhận hàng màu vàng trên bản đồ.";
            if (rewardText != null) rewardText.text = $"Thưởng: +{mission.RewardMoney:N0}đ";

            if (panelRoot != null) panelRoot.SetActive(true);
        }

        private void HandleMissionProgress(MissionData mission, float progress, string status)
        {
            if (objectiveText != null) objectiveText.text = status;
        }

        private void HandleMissionCompleted(MissionData mission, int reward)
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void HandleMissionFailed(MissionData mission, string reason)
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void SetManualDisplay(string title, string obj, string timer, string reward)
        {
            manualTitle = title;
            if (panelRoot == null) panelRoot = gameObject;
            if (titleText != null) titleText.text = title;
            if (objectiveText != null) objectiveText.text = obj;
            if (timerText != null) timerText.text = timer;
            if (rewardText != null) rewardText.text = reward;
            panelRoot.SetActive(true);
        }
    }
}
