using HueCity.Vehicle;
using UnityEngine;

namespace HueCity.Player
{
    /// <summary>
    /// Phát hiện và tương tác với các đối tượng trong môi trường (Ví dụ: xe cộ, ghế ngồi).
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private float interactionRadius = 2.8f;
        [SerializeField] private LayerMask interactableLayer = ~0; // Mặc định quét mọi layer

        private PlayerInputHandler inputHandler;
        private readonly Collider[] overlapResults = new Collider[8];

        private void Awake()
        {
            inputHandler = GetComponent<PlayerInputHandler>();
        }

        private void Update()
        {
            if (inputHandler.InteractTriggered)
            {
                TryInteract();
            }
        }

        private void TryInteract()
        {
            int hits = Physics.OverlapSphereNonAlloc(transform.position, interactionRadius, overlapResults, interactableLayer);
            
            VehicleSeat closestSeat = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < hits; i++)
            {
                Collider col = overlapResults[i];
                if (col == null) continue;

                // Tìm component VehicleSeat ở collider hoặc cha của collider
                VehicleSeat seat = col.GetComponentInParent<VehicleSeat>();
                if (seat != null && !seat.IsOccupied)
                {
                    float dist = Vector3.Distance(transform.position, seat.transform.position);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestSeat = seat;
                    }
                }
            }

            if (closestSeat != null)
            {
                closestSeat.EnterVehicle(gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
