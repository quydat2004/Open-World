using UnityEngine;

namespace HueCity.Player
{
    /// <summary>
    /// Đọc input điều khiển nhân vật từ người chơi.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Key Bindings")]
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode interactKey = KeyCode.F;
        [SerializeField] private KeyCode secondaryInteractKey = KeyCode.E;

        public Vector2 MoveInput { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool JumpTriggered { get; private set; }
        public bool InteractTriggered { get; private set; }

        private bool isInputEnabled = true;

        public void SetInputEnabled(bool enabled)
        {
            isInputEnabled = enabled;
            if (!enabled)
            {
                MoveInput = Vector2.zero;
                IsSprinting = false;
                JumpTriggered = false;
                InteractTriggered = false;
            }
        }

        private void Update()
        {
            if (!isInputEnabled)
            {
                return;
            }

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            MoveInput = new Vector2(horizontal, vertical).normalized;

            IsSprinting = Input.GetKey(sprintKey);
            JumpTriggered = Input.GetKeyDown(jumpKey);
            InteractTriggered = Input.GetKeyDown(interactKey) || Input.GetKeyDown(secondaryInteractKey);
        }
    }
}
