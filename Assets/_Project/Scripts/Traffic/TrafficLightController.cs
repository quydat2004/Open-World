using System;
using UnityEngine;

namespace HueCity.Traffic
{
    /// <summary>
    /// Bộ điều khiển Đèn tín hiệu giao thông ngã tư 4 hướng (Traffic Light Controller).
    /// Chu kỳ luân chuyển mượt mà giữa trục Bắc-Nam (NorthSouth) và Đông-Tây (EastWest).
    /// </summary>
    public class TrafficLightController : MonoBehaviour
    {
        public static TrafficLightController Instance { get; private set; }

        [Header("Cycle Durations (Seconds)")]
        [SerializeField] private float greenDuration = 22f;
        [SerializeField] private float yellowDuration = 3.5f;

        [Header("Current States")]
        [SerializeField] private TrafficLightState northSouthState = TrafficLightState.Green;
        [SerializeField] private TrafficLightState eastWestState = TrafficLightState.Red;

        [Header("Visual Light Meshes References")]
        [SerializeField] private Renderer[] nsRedLenses;
        [SerializeField] private Renderer[] nsYellowLenses;
        [SerializeField] private Renderer[] nsGreenLenses;

        [SerializeField] private Renderer[] ewRedLenses;
        [SerializeField] private Renderer[] ewYellowLenses;
        [SerializeField] private Renderer[] ewGreenLenses;

        public TrafficLightState NorthSouthState => northSouthState;
        public TrafficLightState EastWestState => eastWestState;

        public static event Action<TrafficDirection, TrafficLightState> OnLightStateChanged;

        private float currentPhaseTimer = 0f;
        private int currentPhase = 0;
        // Phase 0: NS Green, EW Red
        // Phase 1: NS Yellow, EW Red
        // Phase 2: NS Red, EW Green
        // Phase 3: NS Red, EW Yellow

        private Material matRedActive;
        private Material matYellowActive;
        private Material matGreenActive;
        private Material matOff;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            CreateLensMaterials();
        }

        private void Start()
        {
            SetPhase(0);
        }

        private void Update()
        {
            currentPhaseTimer -= Time.deltaTime;
            if (currentPhaseTimer <= 0f)
            {
                int nextPhase = (currentPhase + 1) % 4;
                SetPhase(nextPhase);
            }
        }

        public void SetPhase(int phase)
        {
            currentPhase = phase;
            switch (phase)
            {
                case 0:
                    // NS Green, EW Red
                    northSouthState = TrafficLightState.Green;
                    eastWestState = TrafficLightState.Red;
                    currentPhaseTimer = greenDuration;
                    break;
                case 1:
                    // NS Yellow, EW Red
                    northSouthState = TrafficLightState.Yellow;
                    eastWestState = TrafficLightState.Red;
                    currentPhaseTimer = yellowDuration;
                    break;
                case 2:
                    // NS Red, EW Green
                    northSouthState = TrafficLightState.Red;
                    eastWestState = TrafficLightState.Green;
                    currentPhaseTimer = greenDuration;
                    break;
                case 3:
                    // NS Red, EW Yellow
                    northSouthState = TrafficLightState.Red;
                    eastWestState = TrafficLightState.Yellow;
                    currentPhaseTimer = yellowDuration;
                    break;
            }

            UpdateVisualLenses();
            OnLightStateChanged?.Invoke(TrafficDirection.NorthSouth, northSouthState);
            OnLightStateChanged?.Invoke(TrafficDirection.EastWest, eastWestState);
        }

        /// <summary>
        /// Kiểm tra phương tiện theo hướng có được phép đi qua vạch dừng hay không.
        /// </summary>
        public bool CanPass(TrafficDirection direction)
        {
            TrafficLightState state = (direction == TrafficDirection.NorthSouth) ? northSouthState : eastWestState;
            return state == TrafficLightState.Green;
        }

        public void AssignLenses(
            Renderer[] nsRed, Renderer[] nsYellow, Renderer[] nsGreen,
            Renderer[] ewRed, Renderer[] ewYellow, Renderer[] ewGreen)
        {
            nsRedLenses = nsRed;
            nsYellowLenses = nsYellow;
            nsGreenLenses = nsGreen;
            ewRedLenses = ewRed;
            ewYellowLenses = ewYellow;
            ewGreenLenses = ewGreen;

            UpdateVisualLenses();
        }

        private void CreateLensMaterials()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            matOff = new Material(litShader);
            matOff.color = new Color(0.12f, 0.12f, 0.12f);
            matOff.SetFloat("_Smoothness", 0.8f);

            matRedActive = new Material(litShader);
            matRedActive.color = new Color(1.0f, 0.05f, 0.05f);
            matRedActive.EnableKeyword("_EMISSION");
            matRedActive.SetColor("_EmissionColor", new Color(1.0f, 0.05f, 0.05f) * 4.0f);

            matYellowActive = new Material(litShader);
            matYellowActive.color = new Color(1.0f, 0.75f, 0.05f);
            matYellowActive.EnableKeyword("_EMISSION");
            matYellowActive.SetColor("_EmissionColor", new Color(1.0f, 0.75f, 0.05f) * 4.0f);

            matGreenActive = new Material(litShader);
            matGreenActive.color = new Color(0.05f, 0.95f, 0.25f);
            matGreenActive.EnableKeyword("_EMISSION");
            matGreenActive.SetColor("_EmissionColor", new Color(0.05f, 0.95f, 0.25f) * 4.0f);
        }

        private void UpdateVisualLenses()
        {
            if (matOff == null) CreateLensMaterials();

            // Cập nhật đèn Bắc - Nam
            SetRenderersMaterial(nsRedLenses, northSouthState == TrafficLightState.Red ? matRedActive : matOff);
            SetRenderersMaterial(nsYellowLenses, northSouthState == TrafficLightState.Yellow ? matYellowActive : matOff);
            SetRenderersMaterial(nsGreenLenses, northSouthState == TrafficLightState.Green ? matGreenActive : matOff);

            // Cập nhật đèn Đông - Tây
            SetRenderersMaterial(ewRedLenses, eastWestState == TrafficLightState.Red ? matRedActive : matOff);
            SetRenderersMaterial(ewYellowLenses, eastWestState == TrafficLightState.Yellow ? matYellowActive : matOff);
            SetRenderersMaterial(ewGreenLenses, eastWestState == TrafficLightState.Green ? matGreenActive : matOff);
        }

        private void SetRenderersMaterial(Renderer[] renderers, Material mat)
        {
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (r != null) r.sharedMaterial = mat;
            }
        }
    }
}
