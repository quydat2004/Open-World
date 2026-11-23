using UnityEngine;

namespace HueCity.Traffic
{
    /// <summary>
    /// Hướng di chuyển của luồng giao thông tại ngã tư.
    /// </summary>
    public enum TrafficDirection
    {
        NorthSouth, // Luồng trục Bắc - Nam (qua đường trục Z)
        EastWest    // Luồng trục Đông - Tây (qua đường trục X)
    }

    /// <summary>
    /// Trạng thái đèn tín hiệu giao thông.
    /// </summary>
    public enum TrafficLightState
    {
        Green,  // Được phép lưu thông
        Yellow, // Giảm tốc độ, chuẩn bị dừng
        Red     // Dừng lại trước vạch dừng
    }

    /// <summary>
    /// Phân loại phương tiện giao thông.
    /// </summary>
    public enum TrafficVehicleType
    {
        SedanTaxi,      // Taxi Sedan VinFast / truyền thống
        Scooter,        // Xe máy xe ga đường phố
        XichLo          // Xích lô du lịch Cố Đô
    }
}
