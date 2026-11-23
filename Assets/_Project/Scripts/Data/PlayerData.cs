using System;
using System.Collections.Generic;
using UnityEngine;

namespace HueCity.Data
{
    /// <summary>
    /// Cấu trúc dữ liệu lưu trữ tiến độ người chơi (Save/Load Data).
    /// Tuân thủ quy chuẩn AGENTS.md: Lưu dạng file JSON tại Application.persistentDataPath.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public int money = 20000; // Tiền khởi đầu (20,000 VNĐ)
        public float posX = 13.0f;
        public float posY = 0.25f;
        public float posZ = 13.0f;
        public float rotY = 0.0f;

        public List<string> completedMissionIds = new List<string>();
        public string currentMissionId = "";
        public string saveTimestamp = "";

        public Vector3 GetPosition()
        {
            return new Vector3(posX, posY, posZ);
        }

        public void SetPosition(Vector3 pos)
        {
            posX = pos.x;
            posY = pos.y;
            posZ = pos.z;
        }

        public Quaternion GetRotation()
        {
            return Quaternion.Euler(0f, rotY, 0f);
        }

        public void SetRotation(Quaternion rot)
        {
            rotY = rot.eulerAngles.y;
        }
    }
}
