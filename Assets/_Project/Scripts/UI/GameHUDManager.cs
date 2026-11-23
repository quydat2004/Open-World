using UnityEngine;
using UnityEngine.UI;

namespace HueCity.UI
{
    /// <summary>
    /// Trình điều phối toàn bộ giao diện trong game (Game HUD Manager).
    /// Quản lý đồng bộ: Minimap, Speedometer, Mission HUD, và Player HUD.
    /// Hỗ trợ tạo dựng cấu trúc Canvas tự động bằng mã nguồn C#.
    /// </summary>
    public class GameHUDManager : MonoBehaviour
    {
        public static GameHUDManager Instance { get; private set; }

        [Header("Sub-HUD References")]
        [SerializeField] private MinimapController minimap;
        [SerializeField] private SpeedometerUI speedometer;
        [SerializeField] private MissionHUD missionHUD;
        [SerializeField] private PlayerHUD playerHUD;

        public MinimapController Minimap => minimap;
        public SpeedometerUI Speedometer => speedometer;
        public MissionHUD MissionHUD => missionHUD;
        public PlayerHUD PlayerHUD => playerHUD;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        public void BindComponents(MinimapController mini, SpeedometerUI speed, MissionHUD mis, PlayerHUD play)
        {
            minimap = mini;
            speedometer = speed;
            missionHUD = mis;
            playerHUD = play;
        }

        /// <summary>
        /// Tạo dựng toàn bộ cấu trúc uGUI Canvas hoàn chỉnh bằng mã C#.
        /// Đảm bảo hoạt động độc lập và hoàn hảo không phụ thuộc vào Prefab có sẵn.
        /// </summary>
        public static GameObject CreateFullHUDCanvas(Transform playerTarget, Camera mainCam, out GameHUDManager manager)
        {
            GameObject canvasObj = new GameObject("GameHUD_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            manager = canvasObj.AddComponent<GameHUDManager>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 1. Tạo Player HUD (Góc trên bên phải: Tiền & Banner giữa màn hình)
            GameObject playerHudObj = new GameObject("PlayerHUD_Panel");
            playerHudObj.transform.SetParent(canvasObj.transform, false);
            PlayerHUD playerHudComp = playerHudObj.AddComponent<PlayerHUD>();

            // Tiền mặt (Top-Right)
            GameObject moneyObj = CreateTextElement("Money_Text", playerHudObj.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-25f, -25f), new Vector2(280f, 50f),
                "💰 20,000 đ", 26, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.15f), defaultFont);
            Text moneyTextComp = moneyObj.GetComponent<Text>();

            // Banner thông báo (Top-Center)
            GameObject bannerObj = CreatePanelElement("Banner_Notification", playerHudObj.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(620f, 65f),
                new Color(0.1f, 0.12f, 0.18f, 0.9f));
            GameObject bannerTextObj = CreateTextElement("Banner_Text", bannerObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 55f),
                "Chào mừng đến với Cố Đô Huế!", 22, TextAnchor.MiddleCenter, Color.white, defaultFont);

            SetSerializedField(playerHudComp, "moneyText", moneyTextComp);
            SetSerializedField(playerHudComp, "bannerRoot", bannerObj);
            SetSerializedField(playerHudComp, "bannerText", bannerTextObj.GetComponent<Text>());
            SetSerializedField(playerHudComp, "bannerBackground", bannerObj.GetComponent<Image>());

            // 2. Tạo Mission HUD (Top-Left)
            GameObject missionHudObj = CreatePanelElement("MissionHUD_Panel", canvasObj.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(25f, -25f), new Vector2(380f, 150f),
                new Color(0.12f, 0.15f, 0.22f, 0.85f));
            MissionHUD missionHudComp = missionHudObj.AddComponent<MissionHUD>();

            GameObject mTitleObj = CreateTextElement("Mission_Title", missionHudObj.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(-20f, 35f),
                "NHIỆM VỤ CỐ ĐÔ", 20, TextAnchor.MiddleLeft, new Color(0.3f, 0.85f, 1f), defaultFont);

            GameObject mObjDesc = CreateTextElement("Mission_Desc", missionHudObj.transform,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(-20f, 50f),
                "Giao hàng đến điểm hẹn.", 16, TextAnchor.MiddleLeft, Color.white, defaultFont);

            GameObject mTimerObj = CreateTextElement("Mission_Timer", missionHudObj.transform,
                new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(15f, 20f), new Vector2(160f, 30f),
                "⏱ 02:00", 17, TextAnchor.MiddleLeft, new Color(1f, 0.9f, 0.2f), defaultFont);

            GameObject mRewardObj = CreateTextElement("Mission_Reward", missionHudObj.transform,
                new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-15f, 20f), new Vector2(160f, 30f),
                "Thưởng: +50,000đ", 17, TextAnchor.MiddleRight, new Color(0.3f, 1f, 0.4f), defaultFont);

            SetSerializedField(missionHudComp, "panelRoot", missionHudObj);
            SetSerializedField(missionHudComp, "titleText", mTitleObj.GetComponent<Text>());
            SetSerializedField(missionHudComp, "objectiveText", mObjDesc.GetComponent<Text>());
            SetSerializedField(missionHudComp, "timerText", mTimerObj.GetComponent<Text>());
            SetSerializedField(missionHudComp, "rewardText", mRewardObj.GetComponent<Text>());

            // 3. Tạo Speedometer UI (Bottom-Right)
            GameObject speedObj = CreatePanelElement("Speedometer_Panel", canvasObj.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-25f, 25f), new Vector2(180f, 180f),
                new Color(0.08f, 0.1f, 0.15f, 0.85f));
            SpeedometerUI speedComp = speedObj.AddComponent<SpeedometerUI>();

            GameObject numSpeed = CreateTextElement("Speed_Number", speedObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(140f, 70f),
                "0", 52, TextAnchor.MiddleCenter, Color.white, defaultFont);

            GameObject unitSpeed = CreateTextElement("Speed_Unit", speedObj.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(120f, 30f),
                "KM/H", 18, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f), defaultFont);

            GameObject needleObj = CreatePanelElement("Gauge_Needle", speedObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 75f),
                new Color(1f, 0.2f, 0.2f));

            SetSerializedField(speedComp, "panelRoot", speedObj);
            SetSerializedField(speedComp, "speedText", numSpeed.GetComponent<Text>());
            SetSerializedField(speedComp, "unitText", unitSpeed.GetComponent<Text>());
            SetSerializedField(speedComp, "needleTransform", needleObj.GetComponent<RectTransform>());

            // 4. Tạo Minimap UI (Bottom-Left)
            GameObject minimapObj = CreatePanelElement("Minimap_Panel", canvasObj.transform,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(25f, 25f), new Vector2(200f, 200f),
                new Color(0.1f, 0.12f, 0.16f, 0.95f));
            MinimapController minimapComp = minimapObj.AddComponent<MinimapController>();

            // Icon người chơi (trung tâm minimap)
            GameObject pIcon = CreatePanelElement("Player_Icon", minimapObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 22f),
                new Color(0.2f, 0.85f, 1f));

            // GPS Marker mục tiêu nhiệm vụ
            GameObject mMarker = CreatePanelElement("Mission_Target_Marker", minimapObj.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f),
                new Color(1f, 0.85f, 0.1f));

            GameObject distObj = CreateTextElement("Distance_Text", minimapObj.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 15f), new Vector2(120f, 24f),
                "150m", 15, TextAnchor.MiddleCenter, Color.white, defaultFont);

            SetSerializedField(minimapComp, "playerIcon", pIcon.GetComponent<RectTransform>());
            SetSerializedField(minimapComp, "missionMarkerIcon", mMarker.GetComponent<RectTransform>());
            SetSerializedField(minimapComp, "distanceText", distObj.GetComponent<Text>());

            if (playerTarget != null)
            {
                minimapComp.SetPlayerTarget(playerTarget);
            }

            // Gắn vào GameHUDManager
            manager.BindComponents(minimapComp, speedComp, missionHudComp, playerHudComp);

            return canvasObj;
        }

        private static GameObject CreatePanelElement(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = obj.AddComponent<Image>();
            img.color = color;

            return obj;
        }

        private static GameObject CreateTextElement(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, string text, int fontSize, TextAnchor alignment, Color color, Font font)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Text txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = color;
            txt.font = font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            return obj;
        }

        private static void SetSerializedField(Component target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }
}
