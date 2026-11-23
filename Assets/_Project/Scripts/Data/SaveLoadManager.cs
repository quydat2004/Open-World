using System;
using System.IO;
using HueCity.Core;
using HueCity.Mission;
using UnityEngine;

namespace HueCity.Data
{
    /// <summary>
    /// Trình quản lý Lưu và Tải dữ liệu tiến độ chơi (Save/Load Manager).
    /// Ghi và đọc dữ liệu định dạng JSON tại Application.persistentDataPath/huecity_save.json.
    /// Tự động lắng nghe sự kiện hoàn thành nhiệm vụ để Auto-Save.
    /// </summary>
    public class SaveLoadManager : MonoBehaviour
    {
        public static SaveLoadManager Instance { get; private set; }

        private const string SaveFileName = "huecity_save.json";
        private string saveFilePath;
        private PlayerData currentData;

        public PlayerData CurrentData => currentData;
        public string SaveFilePath => saveFilePath;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                InitializePath();
                LoadGame();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            GameEvents.OnMissionCompleted += HandleMissionCompleted;
        }

        private void OnDisable()
        {
            GameEvents.OnMissionCompleted -= HandleMissionCompleted;
        }

        private void InitializePath()
        {
            saveFilePath = Path.Combine(Application.persistentDataPath, SaveFileName);
        }

        public void SaveGame()
        {
            if (currentData == null)
            {
                currentData = new PlayerData();
            }

            currentData.saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            try
            {
                string json = JsonUtility.ToJson(currentData, true);
                File.WriteAllText(saveFilePath, json);
                Debug.Log($"<color=green>[SaveLoadManager]</color> Đã lưu tiến độ game vào: {saveFilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoadManager] Lỗi khi lưu file JSON: {ex.Message}");
            }
        }

        public PlayerData LoadGame()
        {
            InitializePath();

            if (File.Exists(saveFilePath))
            {
                try
                {
                    string json = File.ReadAllText(saveFilePath);
                    currentData = JsonUtility.FromJson<PlayerData>(json);
                    Debug.Log($"<color=green>[SaveLoadManager]</color> Đã tải tiến độ game thành công từ JSON (Tiền: {currentData.money:N0}đ)");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SaveLoadManager] Không thể đọc file save JSON ({ex.Message}), khởi tạo dữ liệu mặc định.");
                    currentData = new PlayerData();
                }
            }
            else
            {
                currentData = new PlayerData();
                SaveGame();
            }

            GameEvents.TriggerMoneyChanged(currentData.money, 0);
            return currentData;
        }

        public void AddMoney(int amount)
        {
            if (currentData == null) currentData = new PlayerData();
            currentData.money += amount;
            GameEvents.TriggerMoneyChanged(currentData.money, amount);
            SaveGame();
        }

        public void SetMoney(int amount)
        {
            if (currentData == null) currentData = new PlayerData();
            int delta = amount - currentData.money;
            currentData.money = amount;
            GameEvents.TriggerMoneyChanged(currentData.money, delta);
            SaveGame();
        }

        public void MarkMissionCompleted(string missionId)
        {
            if (currentData == null) currentData = new PlayerData();
            if (!currentData.completedMissionIds.Contains(missionId))
            {
                currentData.completedMissionIds.Add(missionId);
                SaveGame();
            }
        }

        public bool IsMissionCompleted(string missionId)
        {
            return currentData != null && currentData.completedMissionIds.Contains(missionId);
        }

        public void DeleteSaveData()
        {
            InitializePath();
            if (File.Exists(saveFilePath))
            {
                File.Delete(saveFilePath);
                Debug.Log("[SaveLoadManager] Đã xóa file lưu game JSON.");
            }
            currentData = new PlayerData();
        }

        private void HandleMissionCompleted(MissionData mission, int rewardMoney)
        {
            if (mission != null)
            {
                MarkMissionCompleted(mission.MissionId);
            }
            AddMoney(rewardMoney);
            Debug.Log($"<color=yellow>[SaveLoadManager]</color> Tự động lưu game khi hoàn thành nhiệm vụ (+{rewardMoney:N0}đ)!");
        }
    }
}
