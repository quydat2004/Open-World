using HueCity.Core;
using HueCity.Player;
using UnityEngine;

namespace HueCity.Vehicle
{
    /// <summary>
    /// Quản lý chỗ ngồi, cơ chế Lên/Xuống xe (Enter/Exit) và nhận Input điều khiển xe khi người chơi ngồi bên trong.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class VehicleSeat : MonoBehaviour
    {
        [Header("Exit Point")]
        [SerializeField] private Transform exitPoint;
        [SerializeField] private KeyCode exitKey = KeyCode.F;
        [SerializeField] private KeyCode secondaryExitKey = KeyCode.E;

        private CarController carController;
        private GameObject currentOccupant;
        private PlayerMovement occupantMovement;
        private PlayerInputHandler occupantInput;
        private CharacterController occupantCharacterController;

        public bool IsOccupied => currentOccupant != null;

        private void Awake()
        {
            carController = GetComponent<CarController>();
            
            if (exitPoint == null)
            {
                // Tự tạo một điểm exitPoint bên cạnh xe nếu chưa gán
                GameObject ep = new GameObject("DefaultExitPoint");
                ep.transform.SetParent(transform);
                ep.transform.localPosition = new Vector3(-2f, 0f, 0f);
                exitPoint = ep.transform;
            }
        }

        public void EnterVehicle(GameObject player)
        {
            if (IsOccupied || player == null) return;

            currentOccupant = player;
            occupantMovement = player.GetComponent<PlayerMovement>();
            occupantInput = player.GetComponent<PlayerInputHandler>();
            occupantCharacterController = player.GetComponent<CharacterController>();

            // Vô hiệu hóa điều khiển và collider của nhân vật
            if (occupantInput != null) occupantInput.SetInputEnabled(false);
            if (occupantMovement != null) occupantMovement.enabled = false;
            if (occupantCharacterController != null) occupantCharacterController.enabled = false;

            // Ẩn nhân vật và gắn tạm vào xe
            player.transform.SetParent(transform);
            player.transform.localPosition = Vector3.zero;
            player.SetActive(false);

            // Bắn event thông báo (để CameraManager chuyển camera)
            GameEvents.TriggerPlayerEnteredVehicle(player, gameObject);
        }

        public void ExitVehicle()
        {
            if (!IsOccupied) return;

            GameObject player = currentOccupant;
            currentOccupant = null;

            // Dừng xe và áp phanh
            carController.StopCar();

            // Tính toán vị trí ra an toàn
            Vector3 spawnPosition = exitPoint != null ? exitPoint.position : transform.position + transform.right * -2f;
            
            // Tách player ra khỏi xe
            player.transform.SetParent(null);
            player.transform.position = spawnPosition;
            player.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            player.SetActive(true);

            // Bật lại điều khiển và collider của nhân vật
            if (occupantCharacterController != null) occupantCharacterController.enabled = true;
            if (occupantMovement != null) occupantMovement.enabled = true;
            if (occupantInput != null) occupantInput.SetInputEnabled(true);

            // Bắn event thông báo
            GameEvents.TriggerPlayerExitedVehicle(player, gameObject);
        }

        private void Update()
        {
            if (!IsOccupied) return;

            // Kiểm tra nút xuống xe
            if (Input.GetKeyDown(exitKey) || Input.GetKeyDown(secondaryExitKey))
            {
                ExitVehicle();
                return;
            }

            // Đọc Input lái xe
            float throttle = Input.GetAxis("Vertical");
            float steer = Input.GetAxis("Horizontal");
            bool brake = Input.GetKey(KeyCode.Space);

            carController.SetInput(throttle, steer, brake);
        }
    }
}
