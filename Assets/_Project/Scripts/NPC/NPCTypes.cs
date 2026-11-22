using UnityEngine;

namespace HueCity.NPC
{
    public enum NPCState
    {
        Wander,     // Đi dạo ngẫu nhiên dọc vỉa hè
        Activity,   // Ngồi ghế nhựa uống trà đá, đứng bấm điện thoại, trú mưa
        Alert,      // Quay đầu nhìn khi nghe tiếng còi, xe chạy sát
        Flee,       // Chạy hoảng loạn trốn xe lao lên vỉa hè hoặc tai nạn
        Confront    // Phản kháng (chửi bới, tranh cãi nếu bị va quẹt)
    }

    public enum StimulusType
    {
        CarHorn,    // Tiếng còi xe
        EngineRev,  // Nẹt pô, xe phóng nhanh
        CarCrash,   // Tai nạn đâm xe
        DangerZone  // Xe lao lên vỉa hè
    }

    public struct StimulusData
    {
        public Vector3 Origin;
        public float Radius;
        public StimulusType Type;
        public GameObject Instigator;

        public StimulusData(Vector3 origin, float radius, StimulusType type, GameObject instigator = null)
        {
            Origin = origin;
            Radius = radius;
            Type = type;
            Instigator = instigator;
        }
    }
}
