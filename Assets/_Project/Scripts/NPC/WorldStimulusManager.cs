using System;
using UnityEngine;

namespace HueCity.NPC
{
    /// <summary>
    /// Hệ thống Sự kiện / Giác quan thế giới (World Stimulus Manager).
    /// Thay vì 25 NPC tự quét môi trường mỗi frame, nguồn kích thích (còi xe, đâm xe, nẹt pô)
    /// phát sự kiện tới các NPC xung quanh bán kính tác động.
    /// </summary>
    public class WorldStimulusManager : MonoBehaviour
    {
        public static WorldStimulusManager Instance { get; private set; }

        public static event Action<StimulusData> OnStimulusEmitted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public static void EmitStimulus(Vector3 origin, float radius, StimulusType type, GameObject instigator = null)
        {
            StimulusData data = new StimulusData(origin, radius, type, instigator);
            OnStimulusEmitted?.Invoke(data);
        }
    }
}
