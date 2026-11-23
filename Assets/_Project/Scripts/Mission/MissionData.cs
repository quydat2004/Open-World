using UnityEngine;

namespace HueCity.Mission
{
    /// <summary>
    /// Dữ liệu tĩnh của Nhiệm vụ được lưu dưới dạng ScriptableObject.
    /// Tuân thủ quy chuẩn AGENTS.md: Dữ liệu tĩnh lưu bằng ScriptableObject.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMission", menuName = "HueCity/Mission Data", order = 10)]
    public class MissionData : ScriptableObject
    {
        [Header("General Info")]
        [SerializeField] private string missionId = "MIS_01";
        [SerializeField] private string title = "Giao Đặc Sản Cố Đô";
        [TextArea(2, 4)]
        [SerializeField] private string description = "Nhận bánh bột lọc từ đường Lê Lợi và giao đến khách sạn bên kia ngã tư.";
        [SerializeField] private MissionType missionType = MissionType.Delivery;

        [Header("Rewards & Constraints")]
        [SerializeField] private int rewardMoney = 50000;
        [SerializeField] private float timeLimit = 120.0f; // 0 = Không giới hạn thời gian

        [Header("World Locations")]
        [SerializeField] private Vector3 startPosition = new Vector3(12.0f, 0.2f, 10.0f);
        [SerializeField] private Vector3 targetPosition = new Vector3(-12.0f, 0.2f, -10.0f);
        [SerializeField] private float targetRadius = 3.5f;

        public string MissionId => missionId;
        public string Title => title;
        public string Description => description;
        public MissionType Type => missionType;
        public int RewardMoney => rewardMoney;
        public float TimeLimit => timeLimit;
        public Vector3 StartPosition => startPosition;
        public Vector3 TargetPosition => targetPosition;
        public float TargetRadius => targetRadius;

        public void Initialize(string id, string t, string desc, MissionType type, int reward, float time, Vector3 start, Vector3 target, float radius = 3.5f)
        {
            missionId = id;
            title = t;
            description = desc;
            missionType = type;
            rewardMoney = reward;
            timeLimit = time;
            startPosition = start;
            targetPosition = target;
            targetRadius = radius;
        }
    }
}
