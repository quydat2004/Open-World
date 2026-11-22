using System;
using UnityEngine;

namespace HueCity.Core
{
    /// <summary>
    /// Event Bus tĩnh để giao tiếp giữa các hệ thống (Player, Vehicle, Camera, UI)
    /// Tránh phụ thuộc chéo (decoupled architecture).
    /// </summary>
    public static class GameEvents
    {
        // Sự kiện khi người chơi lên xe
        public static event Action<GameObject, GameObject> OnPlayerEnteredVehicle; // (player, vehicle)
        
        // Sự kiện khi người chơi xuống xe
        public static event Action<GameObject, GameObject> OnPlayerExitedVehicle;  // (player, vehicle)

        public static void TriggerPlayerEnteredVehicle(GameObject player, GameObject vehicle)
        {
            OnPlayerEnteredVehicle?.Invoke(player, vehicle);
        }

        public static void TriggerPlayerExitedVehicle(GameObject player, GameObject vehicle)
        {
            OnPlayerExitedVehicle?.Invoke(player, vehicle);
        }
    }
}
