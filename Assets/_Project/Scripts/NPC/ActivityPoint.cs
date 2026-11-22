using System.Collections.Generic;
using UnityEngine;

namespace HueCity.NPC
{
    /// <summary>
    /// Điểm tương tác thông minh cho NPC (ghế nhựa vỉa hè, trạm xe buýt, quán trà đá).
    /// </summary>
    public class ActivityPoint : MonoBehaviour
    {
        public static readonly List<ActivityPoint> AllPoints = new List<ActivityPoint>();

        [Header("Configuration")]
        [SerializeField] private bool isSheltered = false; // Có mái che che mưa hay không
        [SerializeField] private float minDuration = 15f;
        [SerializeField] private float maxDuration = 30f;
        [SerializeField] private Transform interactionSpot; // Vị trí đứng/ngồi

        public bool IsOccupied { get; private set; }
        public bool IsSheltered => isSheltered;
        public Transform Spot => interactionSpot != null ? interactionSpot : transform;

        private NPCController currentOccupant;

        private void OnEnable()
        {
            AllPoints.Add(this);
        }

        private void OnDisable()
        {
            AllPoints.Remove(this);
            Release();
        }

        public bool TryOccupy(NPCController npc, out float duration)
        {
            duration = 0f;
            if (IsOccupied) return false;

            IsOccupied = true;
            currentOccupant = npc;
            duration = Random.Range(minDuration, maxDuration);
            return true;
        }

        public void Release()
        {
            IsOccupied = false;
            currentOccupant = null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsOccupied ? Color.red : (isSheltered ? Color.cyan : Color.green);
            Vector3 pos = Spot.position;
            Gizmos.DrawWireSphere(pos, 0.35f);
            Gizmos.DrawRay(pos, Spot.forward * 0.5f);
        }
    }
}
