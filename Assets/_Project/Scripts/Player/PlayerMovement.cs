using UnityEngine;

namespace HueCity.Player
{
    /// <summary>
    /// Xử lý di chuyển góc nhìn thứ 3 (Đi bộ, Chạy nhanh, Nhảy, Trọng lực)
    /// Điều hướng theo hướng nhìn của Camera.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 8.5f;
        [SerializeField] private float rotationSmoothTime = 0.12f;

        [Header("Jump & Gravity")]
        [SerializeField] private float jumpHeight = 1.25f;
        [SerializeField] private float gravity = -19.62f; // Trọng lực mượt hơn cho cảm giác game hành động

        private CharacterController characterController;
        private PlayerInputHandler inputHandler;
        private Transform cameraTransform;

        private Vector3 verticalVelocity;
        private float currentRotationVelocity;

        public bool IsGrounded => characterController.isGrounded;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            inputHandler = GetComponent<PlayerInputHandler>();
        }

        private void Start()
        {
            if (UnityEngine.Camera.main != null)
            {
                cameraTransform = UnityEngine.Camera.main.transform;
            }
        }

        private void Update()
        {
            HandleGroundAndGravity();
            HandleMovement();
            HandleJump();
        }

        private void HandleGroundAndGravity()
        {
            if (characterController.isGrounded && verticalVelocity.y < 0f)
            {
                // Giữ nhân vật bám sát mặt đất khi xuống dốc
                verticalVelocity.y = -2f;
            }

            verticalVelocity.y += gravity * Time.deltaTime;
            characterController.Move(verticalVelocity * Time.deltaTime);
        }

        private void HandleMovement()
        {
            Vector2 input = inputHandler.MoveInput;
            if (input.sqrMagnitude < 0.01f)
            {
                return;
            }

            // Tính toán hướng di chuyển tương đối theo Camera
            float targetAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
            if (cameraTransform != null)
            {
                targetAngle += cameraTransform.eulerAngles.y;
            }

            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref currentRotationVelocity, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            float currentSpeed = inputHandler.IsSprinting ? sprintSpeed : walkSpeed;

            characterController.Move(moveDirection.normalized * (currentSpeed * Time.deltaTime));
        }

        private void HandleJump()
        {
            if (inputHandler.JumpTriggered && characterController.isGrounded)
            {
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
    }
}
