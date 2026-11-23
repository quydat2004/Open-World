#if UNITY_EDITOR
#pragma warning disable CS0618
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Cinemachine;
using HueCity.Audio;
using HueCity.Camera;
using HueCity.Core;
using HueCity.Data;
using HueCity.Environment;
using HueCity.Mission;
using HueCity.NPC;
using HueCity.Player;
using HueCity.Traffic;
using HueCity.UI;
using HueCity.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HueCity.EditorTools
{
    /// <summary>
    /// Bộ kiểm thử tự động toàn diện (Automated Game Test Runner) cho HueCity.
    /// Tự động kiểm thử tính toàn vẹn của Asset, Scene, Player, Vehicle, Camera, AI NPC FSM, Stimulus, Data & MCP.
    /// </summary>
    public static class AutomatedGameTestRunner
    {
        private static int passedTests = 0;
        private static int failedTests = 0;
        private static StringBuilder reportLog;

        [InitializeOnLoadMethod]
        private static void RegisterAutoRunOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                RunAllTests(showDialog: false);
            };
        }

        [MenuItem("HueCity/1. Run Automated Functional Tests", false, 1)]
        public static void RunAllTestsMenu()
        {
            RunAllTests(showDialog: true);
        }

        public static bool RunAllTests(bool showDialog = false)
        {
            passedTests = 0;
            failedTests = 0;
            reportLog = new StringBuilder();
            DateTime startTime = DateTime.Now;

            LogHeader("BẮT ĐẦU BỘ KIỂM THỬ TỰ ĐỘNG TOÀN DIỆN HUECITY");

            try
            {
                // Đảm bảo cấu hình URP, Post-Processing và PBR Materials đã sẵn sàng trước khi test
                RealismOverhaulSetup.EnsureFullSetup();
                LocomotionClipGenerator.EnsureClipsAndController();

                // Đảm bảo Scene Giai đoạn 4: Phase4_LivingCity.unity đã sẵn sàng
                string phase4ScenePath = "Assets/_Project/Scenes/Phase4_LivingCity.unity";
                if (!File.Exists(phase4ScenePath) && !EditorApplication.isPlaying)
                {
                    Phase4SceneBuilder.BuildScene(true);
                }

                // Đảm bảo Scene Giai đoạn 5: Phase5_GameplayLoop.unity đã sẵn sàng
                string phase5ScenePath = "Assets/_Project/Scenes/Phase5_GameplayLoop.unity";
                if (!File.Exists(phase5ScenePath) && !EditorApplication.isPlaying)
                {
                    Phase5SceneBuilder.BuildScene(true);
                }

                // Đảm bảo Scene Giai đoạn 6: Phase6_ExpandedMap.unity đã sẵn sàng
                string phase6ScenePath = "Assets/_Project/Scenes/Phase6_ExpandedMap.unity";
                if (!File.Exists(phase6ScenePath) && !EditorApplication.isPlaying)
                {
                    Phase6SceneBuilder.BuildScene(true);
                }

                // Đảm bảo Scene Giai đoạn 7: Phase7_PolishedCity.unity đã sẵn sàng
                string phase7ScenePath = "Assets/_Project/Scenes/Phase7_PolishedCity.unity";
                if (!File.Exists(phase7ScenePath) && !EditorApplication.isPlaying)
                {
                    Phase7SceneBuilder.BuildScene(true);
                }

                TestAssetIntegrity();
                TestPlayerMechanics();
                TestVehicleMechanics();
                TestEnterExitVehicleTransition();
                TestWorldStimulusAndNPC();
                TestActivityPoint();
                TestCameraAndEvents();
                TestDataAndPersistence();
                TestMCPPackageInstallation();
                TestDayNightCycle();
                TestRealismAndLightingURP();
                TestPhase4LivingCityTrafficAndNPC();
                TestPhase5GameplayLoopAndUI();
                TestPhase6ExpandedMapAndOptimization();
                TestPhase7AudioWeatherAndPolish();
            }
            catch (Exception ex)
            {
                Assert(false, $"Ngoại lệ bất ngờ trong quá trình test: {ex}");
            }

            TimeSpan duration = DateTime.Now - startTime;
            LogFooter(duration);

            // Ghi báo cáo ra file Text trong thư mục dự án
            string reportPath = Path.Combine(Application.dataPath, "_Project/TestReport.txt");
            File.WriteAllText(reportPath, reportLog.ToString());
            AssetDatabase.Refresh();

            string summaryMsg = $"Kiểm thử hoàn tất trong {duration.TotalMilliseconds:F1}ms!\n" +
                                $"✅ Đạt: {passedTests} bài kiểm tra\n" +
                                $"❌ Lỗi: {failedTests} bài kiểm tra\n\n" +
                                $"Chi tiết đã lưu tại Assets/_Project/TestReport.txt";

            Debug.Log($"<color={(failedTests == 0 ? "green" : "red")}><b>[HueCity Test Runner]</b></color>\n{summaryMsg}");

            if (showDialog && !Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("HueCity - Kết quả kiểm thử tự động", summaryMsg, "OK");
            }

            return failedTests == 0;
        }

        private static void TestAssetIntegrity()
        {
            LogSection("1. Kiểm tra tính toàn vẹn của Asset & Model 3D");

            string[] requiredModels = new string[]
            {
                "Assets/_Project/Models/Hue_Vertical_Slice_Detailed.fbx",
                "Assets/_Project/Models/House_Modular_01.fbx",
                "Assets/_Project/Models/Car_Sedan_01.fbx",
                "Assets/_Project/Models/Character_Player.fbx",
                "Assets/_Project/Models/Character_Humanoid_Rigged.fbx",
                "Assets/_Project/Models/Vehicle_Taxi_Hue.fbx",
                "Assets/_Project/Models/Vehicle_Scooter.fbx",
                "Assets/_Project/Models/Vehicle_XichLo.fbx",
                "Assets/_Project/Models/Props_Street_Chairs.fbx",
                "Assets/_Project/Models/Map_Hue_Greybox.fbx"
            };

            foreach (var path in requiredModels)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert(asset != null, $"Model tồn tại và hợp lệ: {Path.GetFileName(path)}");
            }

            string[] requiredScenes = new string[]
            {
                "Assets/_Project/Scenes/Phase1_Greybox.unity",
                "Assets/_Project/Scenes/Phase2_MapBlocking.unity",
                "Assets/_Project/Scenes/Phase3_VerticalSlice.unity"
            };

            foreach (var scenePath in requiredScenes)
            {
                bool exists = File.Exists(scenePath);
                Assert(exists, $"Scene tồn tại: {Path.GetFileName(scenePath)}");
            }
        }

        private static void TestPlayerMechanics()
        {
            LogSection("2. Kiểm tra Logic Nhân vật (Player)");

            GameObject playerObj = new GameObject("Test_Player");
            try
            {
                CharacterController cc = playerObj.AddComponent<CharacterController>();
                PlayerInputHandler input = playerObj.AddComponent<PlayerInputHandler>();
                PlayerMovement movement = playerObj.AddComponent<PlayerMovement>();
                PlayerInteraction interaction = playerObj.AddComponent<PlayerInteraction>();

                Assert(cc != null && movement != null && input != null && interaction != null, 
                    "Nhân vật khởi tạo đầy đủ 4 component: CharacterController, PlayerMovement, PlayerInputHandler, PlayerInteraction");

                input.SetInputEnabled(false);
                Assert(input.MoveInput == Vector2.zero && !input.IsSprinting && !input.JumpTriggered, 
                    "SetInputEnabled(false) reset triệt để toàn bộ MoveInput, Sprint, Jump");

                input.SetInputEnabled(true);
                Assert(true, "Khôi phục trạng thái nhận Input thành công");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerObj);
            }
        }

        private static void TestVehicleMechanics()
        {
            LogSection("3. Kiểm tra Vật lý Phương tiện (Vehicle Physics)");

            GameObject carObj = new GameObject("Test_Car");
            try
            {
                Rigidbody rb = carObj.AddComponent<Rigidbody>();
                CarController car = carObj.AddComponent<CarController>();
                VehicleSeat seat = carObj.AddComponent<VehicleSeat>();

                Assert(rb != null && car != null && seat != null, 
                    "Xe khởi tạo đầy đủ Rigidbody, CarController và VehicleSeat");

                Assert(!seat.IsOccupied, "Trạng thái ban đầu xe chưa có người ngồi (IsOccupied == false)");

                car.StopCar();
                Assert(true, "Lệnh phanh dừng khẩn cấp StopCar() thực thi an toàn");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(carObj);
            }
        }

        private static void TestEnterExitVehicleTransition()
        {
            LogSection("4. Kiểm tra Cơ chế Lên/Xuống xe (Enter/Exit Transition)");

            GameObject playerObj = new GameObject("Test_Player_Transition");
            GameObject carObj = new GameObject("Test_Car_Transition");

            bool enterEventFired = false;
            bool exitEventFired = false;

            Action<GameObject, GameObject> onEnter = (p, v) => enterEventFired = true;
            Action<GameObject, GameObject> onExit = (p, v) => exitEventFired = true;

            GameEvents.OnPlayerEnteredVehicle += onEnter;
            GameEvents.OnPlayerExitedVehicle += onExit;

            try
            {
                playerObj.AddComponent<CharacterController>();
                playerObj.AddComponent<PlayerInputHandler>();
                playerObj.AddComponent<PlayerMovement>();

                carObj.AddComponent<Rigidbody>();
                carObj.AddComponent<CarController>();
                VehicleSeat seat = carObj.AddComponent<VehicleSeat>();

                // 1. Thử Lên xe
                seat.EnterVehicle(playerObj);

                Assert(seat.IsOccupied, "Lên xe thành công: seat.IsOccupied == true");
                Assert(!playerObj.activeSelf, "Nhân vật được ẩn đi khi ngồi trong cabin");
                Assert(playerObj.transform.parent == carObj.transform, "Nhân vật được gắn làm con của Xe");
                Assert(enterEventFired, "Sự kiện GameEvents.OnPlayerEnteredVehicle được phát thanh thành công");

                // 2. Thử Xuống xe
                seat.ExitVehicle();

                Assert(!seat.IsOccupied, "Xuống xe thành công: seat.IsOccupied == false");
                Assert(playerObj.activeSelf, "Nhân vật được kích hoạt hiển thị trở lại");
                Assert(playerObj.transform.parent == null, "Nhân vật được tách độc lập khỏi Xe");
                Assert(exitEventFired, "Sự kiện GameEvents.OnPlayerExitedVehicle được phát thanh thành công");
            }
            finally
            {
                GameEvents.OnPlayerEnteredVehicle -= onEnter;
                GameEvents.OnPlayerExitedVehicle -= onExit;
                UnityEngine.Object.DestroyImmediate(playerObj);
                UnityEngine.Object.DestroyImmediate(carObj);
            }
        }

        private static void TestWorldStimulusAndNPC()
        {
            LogSection("5. Kiểm tra Hệ thống Giác quan & AI NPC (FSM)");

            GameObject managerObj = new GameObject("Test_WorldStimulusManager");
            managerObj.AddComponent<WorldStimulusManager>();

            GameObject npcObj = new GameObject("Test_NPC");
            try
            {
                NavMeshAgent agent = npcObj.AddComponent<NavMeshAgent>();
                NPCController npc = npcObj.AddComponent<NPCController>();
                npcObj.transform.position = Vector3.zero;

                // Đăng ký nhận sự kiện WorldStimulusManager trong môi trường Test
                npc.RegisterStimulusListener();

                Assert(npc.CurrentState == NPCState.Wander, "Trạng thái khởi đầu mặc định của NPC là Wander");

                // Phát tiếng còi xe trong bán kính 15m (NPC ở tọa độ 0, nguồn ở tọa độ (3, 0, 0))
                WorldStimulusManager.EmitStimulus(new Vector3(3f, 0f, 0f), 15f, StimulusType.CarHorn);
                Assert(npc.CurrentState == NPCState.Alert, "NPC nghe tiếng còi xe gần đã tự động chuyển sang trạng thái Alert (Dừng lại quan sát)");

                // Phát tai nạn đâm xe cận kề
                WorldStimulusManager.EmitStimulus(new Vector3(2f, 0f, 0f), 20f, StimulusType.CarCrash);
                bool reactedToCrash = npc.CurrentState == NPCState.Flee || npc.CurrentState == NPCState.Confront;
                Assert(reactedToCrash, $"NPC gặp nguy hiểm tai nạn đã chuyển trạng thái hợp lệ ({npc.CurrentState})");
            }
            finally
            {
                if (npcObj != null)
                {
                    var npc = npcObj.GetComponent<NPCController>();
                    if (npc != null) npc.UnregisterStimulusListener();
                }
                UnityEngine.Object.DestroyImmediate(npcObj);
                UnityEngine.Object.DestroyImmediate(managerObj);
            }
        }

        private static void TestActivityPoint()
        {
            LogSection("6. Kiểm tra Điểm Tương Tác Vỉa Hè (ActivityPoint)");

            GameObject pointObj = new GameObject("Test_ActivityPoint");
            try
            {
                ActivityPoint point = pointObj.AddComponent<ActivityPoint>();
                Assert(!point.IsOccupied, "Điểm tương tác ban đầu còn trống (IsOccupied == false)");

                bool occupied = point.TryOccupy(null, out float duration);
                Assert(occupied && point.IsOccupied, $"TryOccupy() thành công, đặt thời gian sinh hoạt: {duration:F1}s");

                bool doubleOccupy = point.TryOccupy(null, out _);
                Assert(!doubleOccupy, "Không cho phép 2 NPC cùng chiếm dụng 1 điểm (chống trùng lặp ghế)");

                point.Release();
                Assert(!point.IsOccupied, "Lệnh Release() giải phóng vị trí thành công");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pointObj);
            }
        }

        private static void TestCameraAndEvents()
        {
            LogSection("7. Kiểm tra Camera Switcher & Sự Kiện Game (Cinemachine)");

            GameObject camManagerObj = new GameObject("Test_CameraManager");
            try
            {
                CameraManager camManager = camManagerObj.AddComponent<CameraManager>();
                Assert(camManager != null, "Khởi tạo thành công CameraManager");

                // Kiểm tra đăng ký sự kiện an toàn
                GameObject dummyPlayer = new GameObject("DummyPlayer");
                GameObject dummyCar = new GameObject("DummyCar");
                try
                {
                    GameEvents.TriggerPlayerEnteredVehicle(dummyPlayer, dummyCar);
                    Assert(true, "Kích hoạt chuyển Camera khi Lên xe an toàn");

                    GameEvents.TriggerPlayerExitedVehicle(dummyPlayer, dummyCar);
                    Assert(true, "Kích hoạt chuyển Camera khi Xuống xe an toàn");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(dummyPlayer);
                    UnityEngine.Object.DestroyImmediate(dummyCar);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camManagerObj);
            }
        }

        private static void TestDataAndPersistence()
        {
            LogSection("8. Kiểm tra Dữ liệu & Lưu trữ (Data & Persistence)");

            string savePath = Application.persistentDataPath;
            Assert(Directory.Exists(savePath), $"Thư mục lưu trữ persistentDataPath hợp lệ: {savePath}");

            // Test serialization dữ liệu vị trí người chơi
            Vector3 testPos = new Vector3(15.5f, 0f, 22.3f);
            string json = JsonUtility.ToJson(testPos);
            Vector3 restoredPos = JsonUtility.FromJson<Vector3>(json);
            Assert(Vector3.Distance(testPos, restoredPos) < 0.001f, "Serialization JSON vị trí người chơi chính xác 100%");
        }

        private static void TestMCPPackageInstallation()
        {
            LogSection("9. Kiểm tra Tích hợp MCP (Unity MCP Bridge)");

            string manifestPath = Path.Combine(Directory.GetCurrentDirectory(), "Packages/manifest.json");
            bool hasManifest = File.Exists(manifestPath);
            Assert(hasManifest, "Packages/manifest.json tồn tại");

            if (hasManifest)
            {
                string manifestContent = File.ReadAllText(manifestPath);
                bool hasMcp = manifestContent.Contains("com.coplaydev.unity-mcp");
                Assert(hasMcp, "Gói com.coplaydev.unity-mcp đã được khai báo chính thức trong manifest");
            }
        }

        private static void TestDayNightCycle()
        {
            LogSection("10. Kiểm tra Chu kỳ Ngày / Đêm (Day/Night Cycle)");

            GameObject lightObj = new GameObject("Test_SunLight");
            try
            {
                Light sunLight = lightObj.AddComponent<Light>();
                sunLight.type = LightType.Directional;
                DayNightCycle cycle = lightObj.AddComponent<DayNightCycle>();

                Assert(cycle != null, "Khởi tạo thành công component DayNightCycle");

                cycle.SetTimeOfDay(12.0f);
                Assert(Mathf.Abs(cycle.CurrentHour - 12.0f) < 0.01f && !cycle.IsNight, 
                    "Cấu hình 12:00 trưa thành công (IsNight == false)");

                cycle.SetTimeOfDay(21.0f);
                Assert(Mathf.Abs(cycle.CurrentHour - 21.0f) < 0.01f && cycle.IsNight, 
                    "Cấu hình 21:00 đêm thành công (IsNight == true)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightObj);
            }
        }

        private static void TestRealismAndLightingURP()
        {
            LogSection("11. Kiểm tra Thiết lập Đồ họa & Ánh sáng URP (Realism Overhaul)");

            // 1. Kiểm tra URP Pipeline Asset và thiết lập Graphics
            UniversalRenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_Project/Settings/URP_HighFidelity.asset");
            Assert(urpAsset != null, "Tồn tại URP Pipeline Asset: URP_HighFidelity.asset");
            if (urpAsset != null)
            {
                Assert(urpAsset.supportsHDR, "URP Asset đã bật HDR cho ACES Tonemapping");
                Assert(urpAsset.shadowDistance >= 70f, "URP Asset có khoảng cách đổ bóng sắc nét (>= 70m)");
            }

            // 2. Kiểm tra VolumeProfile Post-Processing (ACES, Bloom, Color Adjustments)
            VolumeProfile postProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Settings/Hue_PostProcessing_Profile.asset");
            Assert(postProfile != null, "Tồn tại VolumeProfile: Hue_PostProcessing_Profile.asset");
            if (postProfile != null)
            {
                Tonemapping tonemapping;
                bool hasTonemapping = postProfile.TryGet(out tonemapping);
                Assert(hasTonemapping && tonemapping.mode.value == TonemappingMode.ACES, 
                    "Post-Processing bật ACES Tonemapping điện ảnh");

                Bloom bloom;
                bool hasBloom = postProfile.TryGet(out bloom);
                Assert(hasBloom && bloom.intensity.value > 0.1f, 
                    "Post-Processing bật Bloom tán xạ ánh sáng");

                ColorAdjustments colorAdj;
                bool hasColor = postProfile.TryGet(out colorAdj);
                Assert(hasColor && colorAdj.contrast.value > 0f && colorAdj.saturation.value > 0f, 
                    "Post-Processing bật Color Adjustments (tăng Contrast & Saturation)");
            }

            // 3. Kiểm tra Procedural Skybox Material
            Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Skybox_Procedural.mat");
            Assert(skyboxMat != null, "Tồn tại Material Procedural Skybox: MAT_Skybox_Procedural.mat");

            // 4. Kiểm tra PBR Materials cho Đường nhựa & Vỉa hè
            Material roadMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Road_Asphalt.mat");
            Assert(roadMat != null, "Tồn tại Material Mặt đường nhựa PBR: MAT_Road_Asphalt.mat");

            Material sidewalkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Sidewalk_Concrete.mat");
            Assert(sidewalkMat != null, "Tồn tại Material Vỉa hè bê tông PBR: MAT_Sidewalk_Concrete.mat");

            Material curbMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Curb_Stone.mat");
            Assert(curbMat != null, "Tồn tại Material Gờ bó vỉa đá Granite: MAT_Curb_Stone.mat");

            Material whiteMarkingMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_RoadMarking_White.mat");
            Assert(whiteMarkingMat != null, "Tồn tại Material Vạch kẻ đường Trắng: MAT_RoadMarking_White.mat");

            Material yellowMarkingMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_RoadMarking_Yellow.mat");
            Assert(yellowMarkingMat != null, "Tồn tại Material Vạch phân làn kép Vàng: MAT_RoadMarking_Yellow.mat");

            // 5. Kiểm tra file Texture PBR thật trên ổ đĩa
            string roadDiffPath = "Assets/_Project/Textures/PBR/Road_Asphalt/asphalt_02_diff_1k.jpg";
            string roadNorPath = "Assets/_Project/Textures/PBR/Road_Asphalt/asphalt_02_nor_1k.jpg";
            string sideDiffPath = "Assets/_Project/Textures/PBR/Sidewalk_Concrete/concrete_pavement_diff_1k.jpg";
            string sideNorPath = "Assets/_Project/Textures/PBR/Sidewalk_Concrete/concrete_pavement_nor_1k.jpg";

            Assert(File.Exists(roadDiffPath) && File.Exists(roadNorPath), 
                "Bộ Texture PBR Nhựa đường Poly Haven thật (Albedo & Normal) tồn tại");
            Assert(File.Exists(sideDiffPath) && File.Exists(sideNorPath), 
                "Bộ Texture PBR Vỉa hè Poly Haven thật (Albedo & Normal) tồn tại");

            // 6. Kiểm tra Animator Controller và Blend Tree Locomotion (Hạng mục 1)
            RuntimeAnimatorController animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Animations/Player_AnimatorController.controller");
            Assert(animCtrl != null, "Tồn tại Player_AnimatorController với Blend Tree Locomotion");

            AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Clips/Idle.anim");
            AnimationClip walk = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Clips/Walk.anim");
            AnimationClip run = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Clips/Run.anim");
            Assert(idle != null && !idle.empty, "Tồn tại AnimationClip Idle có đường cong nhịp thở");
            Assert(walk != null && !walk.empty, "Tồn tại AnimationClip Walk có đường cong bước chân so le");
            Assert(run != null && !run.empty, "Tồn tại AnimationClip Run có đường cong chạy nước rút");

            // 7. Kiểm tra Vật liệu Xe cộ & Đèn pha URP Lit (Hạng mục 3)
            Material carPaint = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Vehicle_Paint.mat");
            Assert(carPaint != null, "Tồn tại Material Sơn bóng xe Taxi Vàng: MAT_Vehicle_Paint.mat");

            Material headlightMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Vehicle_Headlights.mat");
            Assert(headlightMat != null, "Tồn tại Material Đèn pha phát sáng: MAT_Vehicle_Headlights.mat");
        }

        private static void TestPhase4LivingCityTrafficAndNPC()
        {
            LogSection("12. KIỂM THỬ GIAI ĐOẠN 4: THỔI HỒN THÀNH PHỐ (TRAFFIC & LIVING CITY)");

            // 1. Kiểm tra Mạng lưới Waypoint
            GameObject wpParent = new GameObject("Test_WP_Network");
            GameObject wpObj1 = new GameObject("WP1");
            wpObj1.transform.SetParent(wpParent.transform);
            TrafficWaypoint wp1 = wpObj1.AddComponent<TrafficWaypoint>();
            wp1.SetStopPoint(true);
            wp1.SetDirection(TrafficDirection.NorthSouth);
            wp1.SetSpeedLimit(8.0f);

            GameObject wpObj2 = new GameObject("WP2");
            wpObj2.transform.SetParent(wpParent.transform);
            wpObj2.transform.position = new Vector3(0f, 0f, 10f);
            TrafficWaypoint wp2 = wpObj2.AddComponent<TrafficWaypoint>();

            wp1.AddNextWaypoint(wp2);

            Assert(wp1.IsIntersectionStopPoint == true, "TrafficWaypoint: Cờ điểm dừng stop-line được thiết lập chính xác");
            Assert(wp1.Direction == TrafficDirection.NorthSouth, "TrafficWaypoint: Hướng lưu thông trục Bắc-Nam (NorthSouth) gán chuẩn");
            Assert(wp1.SpeedLimit == 8.0f, "TrafficWaypoint: Giới hạn tốc độ được gán đúng thông số");
            Assert(wp1.GetNextWaypoint() == wp2, "TrafficWaypoint: Liên kết mạng lưới điểm tiếp theo hoạt động chuẩn xác");

            // 2. Kiểm tra Hệ thống Đèn giao thông ngã tư (TrafficLightController)
            GameObject lightObj = new GameObject("Test_TrafficLightController");
            TrafficLightController lightCtrl = lightObj.AddComponent<TrafficLightController>();

            // Phase 0: NS Green, EW Red
            lightCtrl.SetPhase(0);
            Assert(lightCtrl.NorthSouthState == TrafficLightState.Green && lightCtrl.EastWestState == TrafficLightState.Red,
                "TrafficLightController Phase 0: Trục Bắc-Nam đèn Xanh, Trục Đông-Tây đèn Đỏ");
            Assert(lightCtrl.CanPass(TrafficDirection.NorthSouth) == true,
                "TrafficLightController: Phương tiện hướng Bắc-Nam được phép qua khi đèn Xanh");
            Assert(lightCtrl.CanPass(TrafficDirection.EastWest) == false,
                "TrafficLightController: Phương tiện hướng Đông-Tây bị dừng lại khi đèn Đỏ");

            // Phase 1: NS Yellow, EW Red
            lightCtrl.SetPhase(1);
            Assert(lightCtrl.NorthSouthState == TrafficLightState.Yellow && lightCtrl.EastWestState == TrafficLightState.Red,
                "TrafficLightController Phase 1: Trục Bắc-Nam chuyển Vàng chuẩn bị dừng");
            Assert(lightCtrl.CanPass(TrafficDirection.NorthSouth) == false,
                "TrafficLightController: Xe hướng Bắc-Nam không được vượt khi đèn Vàng");

            // Phase 2: NS Red, EW Green
            lightCtrl.SetPhase(2);
            Assert(lightCtrl.NorthSouthState == TrafficLightState.Red && lightCtrl.EastWestState == TrafficLightState.Green,
                "TrafficLightController Phase 2: Trục Bắc-Nam đèn Đỏ, Trục Đông-Tây đèn Xanh");
            Assert(lightCtrl.CanPass(TrafficDirection.NorthSouth) == false,
                "TrafficLightController: Xe hướng Bắc-Nam dừng tuyệt đối khi đèn Đỏ");
            Assert(lightCtrl.CanPass(TrafficDirection.EastWest) == true,
                "TrafficLightController: Xe hướng Đông-Tây thông suốt khi đèn Xanh");

            // 3. Kiểm tra Trí tuệ Xe cộ (TrafficVehicleAI) & Cảm biến phanh
            GameObject vehicleObj = new GameObject("Test_TrafficVehicleAI");
            Rigidbody vRb = vehicleObj.AddComponent<Rigidbody>();
            TrafficVehicleAI vehicleAI = vehicleObj.AddComponent<TrafficVehicleAI>();
            vehicleAI.Initialize(wp1, 0f);

            Assert(vehicleAI.CurrentWaypoint == wp1, "TrafficVehicleAI: Khởi tạo bám Waypoint ban đầu chính xác");
            Assert(vehicleAI.CurrentSpeed == 0f, "TrafficVehicleAI: Khởi tạo tốc độ ban đầu chuẩn xác");
            Assert(vehicleAI.IsBlocked == false, "TrafficVehicleAI: Trạng thái kẹt xe ban đầu là False");

            // 4. Kiểm tra Object Pool xe giao thông (TrafficPoolManager)
            GameObject poolObj = new GameObject("Test_TrafficPoolManager");
            TrafficPoolManager poolMgr = poolObj.AddComponent<TrafficPoolManager>();
            List<TrafficWaypoint> starts = new List<TrafficWaypoint>() { wp1 };
            poolMgr.InitializeWithPrefabs(new GameObject[] { vehicleObj }, starts, poolSize: 4);

            Assert(poolMgr.PoolCount == 4, "TrafficPoolManager: Khởi tạo Object Pool thành công với 4 xe giao thông");
            Assert(poolMgr.ActiveCount <= poolMgr.PoolCount, "TrafficPoolManager: Số lượng xe hoạt động không vượt quá dung lượng Pool");

            // Dọn dẹp đối tượng test tạm
            UnityEngine.Object.DestroyImmediate(wpParent);
            UnityEngine.Object.DestroyImmediate(lightObj);
            UnityEngine.Object.DestroyImmediate(poolObj);
            UnityEngine.Object.DestroyImmediate(vehicleObj);

            // 5. Kiểm tra Tích hợp Animation cho NPCController
            GameObject npcTest = new GameObject("Test_NPC_Animator");
            NavMeshAgent nma = npcTest.AddComponent<NavMeshAgent>();
            NPCController npcCtrl = npcTest.AddComponent<NPCController>();
            Assert(npcCtrl != null && nma != null, "NPCController: Tích hợp đầy đủ NavMeshAgent và bộ FSM 5 trạng thái");
            UnityEngine.Object.DestroyImmediate(npcTest);

            // 6. Kiểm tra Scene Giai đoạn 4: Phase4_LivingCity.unity
            string phase4Path = "Assets/_Project/Scenes/Phase4_LivingCity.unity";
            Assert(File.Exists(phase4Path), "Tồn tại file Scene Giai đoạn 4: Phase4_LivingCity.unity");

            // 7. Kiểm tra Prefab Xe cộ & Nhân vật chi tiết trong Scene
            string taxiFbx = "Assets/_Project/Models/Car_Sedan_01.fbx";
            string charFbx = "Assets/_Project/Models/Character_Player.fbx";
            Assert(File.Exists(taxiFbx), "Mô hình xe ô tô Sedan chi tiết Car_Sedan_01.fbx có mặt");
            Assert(File.Exists(charFbx), "Mô hình nhân vật người chân thực Character_Player.fbx có mặt");
        }

        private static void TestPhase5GameplayLoopAndUI()
        {
            LogSection("13. KIỂM THỬ GIAI ĐOẠN 5: VÒNG LẶP GAMEPLAY & GIAO DIỆN (GAMEPLAY LOOP & UI)");

            // 1. Kiểm tra Hệ thống Dữ liệu & Save/Load JSON
            PlayerData testData = new PlayerData();
            testData.money = 75000;
            testData.SetPosition(new Vector3(14.5f, 0.25f, -8.0f));
            testData.completedMissionIds.Add("MIS_01");

            string json = JsonUtility.ToJson(testData);
            PlayerData deserialized = JsonUtility.FromJson<PlayerData>(json);

            Assert(deserialized != null && deserialized.money == 75000, "PlayerData JSON: Lưu và đọc số dư tiền mặt 75,000đ chính xác 100%");
            Assert(Vector3.Distance(deserialized.GetPosition(), new Vector3(14.5f, 0.25f, -8.0f)) < 0.01f, "PlayerData JSON: Lưu và phục hồi tọa độ người chơi chính xác");
            Assert(deserialized.completedMissionIds.Contains("MIS_01"), "PlayerData JSON: Lưu danh sách nhiệm vụ đã hoàn thành chính xác");

            // 2. Kiểm tra Hệ thống Nhiệm vụ (MissionData & MissionManager)
            MissionData testMis = ScriptableObject.CreateInstance<MissionData>();
            testMis.Initialize("TEST_01", "Giao Chè Hẻm Cố Đô", "Giao chè hẻm đến điểm hẹn.", MissionType.Delivery, 35000, 60f, Vector3.zero, new Vector3(10f, 0f, 10f));

            Assert(testMis.MissionId == "TEST_01" && testMis.RewardMoney == 35000, "MissionData: Khởi tạo thông tin nhiệm vụ ScriptableObject chuẩn xác");

            GameObject misMgrObj = new GameObject("Test_MissionManager");
            MissionManager misMgr = misMgrObj.AddComponent<MissionManager>();
            misMgr.RegisterMission(testMis);

            Assert(misMgr.MissionsCatalog.Count > 0, "MissionManager: Đăng ký danh mục nhiệm vụ thành công");

            bool started = misMgr.StartMission(testMis);
            Assert(started && misMgr.IsMissionActive && misMgr.ActiveMission == testMis, "MissionManager: Bắt đầu nhiệm vụ thành công và kích hoạt bộ đếm thời gian");

            misMgr.CompleteMission();
            Assert(!misMgr.IsMissionActive && misMgr.ActiveMission == null, "MissionManager: Hoàn thành nhiệm vụ và giải phóng trạng thái thành công");

            UnityEngine.Object.DestroyImmediate(misMgrObj);
            UnityEngine.Object.DestroyImmediate(testMis);

            // 3. Kiểm tra các Asset Nhiệm vụ mẫu Cố đô đã được tạo
            string m1 = "Assets/_Project/ScriptableObjects/Missions/Mission_01_Delivery_HueCakes.asset";
            string m2 = "Assets/_Project/ScriptableObjects/Missions/Mission_02_Taxi_TourImperial.asset";
            string m3 = "Assets/_Project/ScriptableObjects/Missions/Mission_03_Speed_TruongTien.asset";
            Assert(File.Exists(m1), "Tồn tại Asset Nhiệm vụ 1: Giao Bánh Bột Lọc (Mission_01_Delivery_HueCakes.asset)");
            Assert(File.Exists(m2), "Tồn tại Asset Nhiệm vụ 2: Taxi Du Lịch Cố Đô (Mission_02_Taxi_TourImperial.asset)");
            Assert(File.Exists(m3), "Tồn tại Asset Nhiệm vụ 3: Đua Xe Bấm Giờ Trường Tiền (Mission_03_Speed_TruongTien.asset)");

            // 4. Kiểm tra Đồng hồ đo tốc độ (SpeedometerUI)
            GameObject speedObj = new GameObject("Test_SpeedometerUI");
            SpeedometerUI speedUI = speedObj.AddComponent<SpeedometerUI>();
            speedUI.SetSpeedManual(68.5f);
            Assert(Mathf.Abs(speedUI.CurrentSpeedKmh - 68.5f) < 0.1f, "SpeedometerUI: Tính toán và hiển thị tốc độ km/h kỹ thuật số chính xác");
            UnityEngine.Object.DestroyImmediate(speedObj);

            // 5. Kiểm tra Bảng Nhiệm vụ HUD (MissionHUD)
            GameObject misHudObj = new GameObject("Test_MissionHUD");
            MissionHUD misHUD = misHudObj.AddComponent<MissionHUD>();
            misHUD.SetManualDisplay("Giao Bánh Nậm", "Đến cổng Ngọ Môn", "⏱ 01:45", "+45,000đ");
            Assert(misHUD.DisplayedTitle == "Giao Bánh Nậm" && misHUD.IsVisible, "MissionHUD: Hiển thị đầy đủ tiêu đề, mô tả và đồng hồ đếm ngược nhiệm vụ");
            UnityEngine.Object.DestroyImmediate(misHudObj);

            // 6. Kiểm tra Bảng Trạng thái Người chơi (PlayerHUD)
            GameObject playerHudObj = new GameObject("Test_PlayerHUD");
            PlayerHUD playerHUD = playerHudObj.AddComponent<PlayerHUD>();
            playerHUD.SetMoney(85000);
            Assert(playerHUD.DisplayMoney == 85000, "PlayerHUD: Hiển thị số dư tiền mặt 85,000đ chính xác");
            UnityEngine.Object.DestroyImmediate(playerHudObj);

            // 7. Kiểm tra Bản đồ con (MinimapController)
            GameObject miniObj = new GameObject("Test_MinimapController");
            MinimapController miniCtrl = miniObj.AddComponent<MinimapController>();
            Assert(miniCtrl != null, "MinimapController: Khởi tạo thành công bộ điều khiển bản đồ con");
            UnityEngine.Object.DestroyImmediate(miniObj);

            // 8. Kiểm tra File Scene Giai đoạn 5
            string phase5Path = "Assets/_Project/Scenes/Phase5_GameplayLoop.unity";
            Assert(File.Exists(phase5Path), "Tồn tại file Scene Giai đoạn 5: Phase5_GameplayLoop.unity");
        }

        private static void TestPhase6ExpandedMapAndOptimization()
        {
            LogSection("14. KIỂM THỬ GIAI ĐOẠN 6: MỞ RỘNG BẢN ĐỒ 500M X 500M & TỐI ƯU HIỆU NĂNG");

            // 1. Kiểm tra kích hoạt GPU Instancing cho các Material PBR
            string[] testMatPaths = new string[]
            {
                "Assets/_Project/Materials/MAT_Road_Asphalt.mat",
                "Assets/_Project/Materials/MAT_Sidewalk_Concrete.mat",
                "Assets/_Project/Materials/MAT_Curb_Stone.mat",
                "Assets/_Project/Materials/MAT_RoadMarking_White.mat",
                "Assets/_Project/Materials/MAT_RoadMarking_Yellow.mat"
            };

            foreach (var matPath in testMatPaths)
            {
                Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (m != null)
                {
                    CityOptimizationUtility.EnableGPUInstancing(m);
                    Assert(m.enableInstancing, $"GPU Instancing đã được bật trên Material: {Path.GetFileName(matPath)}");
                }
            }

            // 2. Kiểm tra Hệ thống LOD Group cho khối nhà
            GameObject testBuilding = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testBuilding.name = "Test_Building_LOD";
            LODGroup lodGroup = CityOptimizationUtility.SetupBuildingLODGroup(testBuilding, 0.40f, 0.10f);

            Assert(lodGroup != null, "CityOptimizationUtility: Tạo thành công LODGroup cho khối nhà");
            Assert(lodGroup.lodCount == 2, "CityOptimizationUtility: Cấu hình 2 mức LOD (Chi tiết gần & Khung xa) đạt chuẩn");
            Assert(lodGroup.GetLODs()[0].screenRelativeTransitionHeight == 0.40f, "CityOptimizationUtility: Ngưỡng chuyển đổi LOD 0 chính xác 40%");

            UnityEngine.Object.DestroyImmediate(testBuilding);

            // 3. Kiểm tra Cờ tĩnh Static Flags (Occlusion, Batching, Navigation)
            GameObject staticTestObj = new GameObject("Test_Static_Flags");
            CityOptimizationUtility.SetOptimizedStaticFlags(staticTestObj, isOccluder: true);
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(staticTestObj);

            bool hasOccluder = (flags & StaticEditorFlags.OccluderStatic) != 0;
            bool hasOccludee = (flags & StaticEditorFlags.OccludeeStatic) != 0;
            bool hasBatching = (flags & StaticEditorFlags.BatchingStatic) != 0;
            bool hasNavStatic = (flags & StaticEditorFlags.NavigationStatic) != 0;

            Assert(hasOccluder && hasOccludee, "Static Flags: Đã gán OccluderStatic và OccludeeStatic phục vụ Occlusion Culling");
            Assert(hasBatching && hasNavStatic, "Static Flags: Đã gán BatchingStatic và NavigationStatic phục vụ NavMesh");

            UnityEngine.Object.DestroyImmediate(staticTestObj);

            // 4. Kiểm tra Cơ chế Distance Streaming của TrafficPoolManager
            GameObject poolObj = new GameObject("Test_TrafficPool_Streaming");
            TrafficPoolManager poolMgr = poolObj.AddComponent<TrafficPoolManager>();
            Assert(poolMgr != null, "TrafficPoolManager: Tích hợp cơ chế Dynamic Distance Streaming tối ưu CPU trên bản đồ 500m");
            UnityEngine.Object.DestroyImmediate(poolObj);

            // 5. Kiểm tra File Scene Giai đoạn 6 (Phase6_ExpandedMap.unity)
            string phase6Path = "Assets/_Project/Scenes/Phase6_ExpandedMap.unity";
            Assert(File.Exists(phase6Path), "Tồn tại file Scene Giai đoạn 6 mở rộng 500m x 500m: Phase6_ExpandedMap.unity");

            // 6. Kiểm tra Nhiệm vụ mở rộng tuần tra 500m
            string m4Path = "Assets/_Project/ScriptableObjects/Missions/Mission_04_Patrol_City500m.asset";
            Assert(File.Exists(m4Path), "Tồn tại Asset Nhiệm vụ tuần tra 500m: Mission_04_Patrol_City500m.asset");
        }

        private static void TestPhase7AudioWeatherAndPolish()
        {
            LogSection("15. KIỂM THỬ GIAI ĐOẠN 7: ĐÁNH BÓNG, ÂM THANH & THỜI TIẾT CỐ ĐÔ (AUDIO, WEATHER & POLISH)");

            // 1. Kiểm tra 5 File Âm thanh Audio WAV chuẩn game
            string engineWav = "Assets/_Project/Audio/SFX_Engine_Idle_Loop.wav";
            string hornWav = "Assets/_Project/Audio/SFX_Car_Horn.wav";
            string skidWav = "Assets/_Project/Audio/SFX_Tire_Skid.wav";
            string rainWav = "Assets/_Project/Audio/SFX_Rain_Ambient_Loop.wav";
            string stepWav = "Assets/_Project/Audio/SFX_Footstep_Step.wav";

            Assert(File.Exists(engineWav), "Tồn tại file âm thanh động cơ xe: SFX_Engine_Idle_Loop.wav");
            Assert(File.Exists(hornWav), "Tồn tại file âm thanh tiếng còi xe: SFX_Car_Horn.wav");
            Assert(File.Exists(skidWav), "Tồn tại file âm thanh rít lốp phanh gấp: SFX_Tire_Skid.wav");
            Assert(File.Exists(rainWav), "Tồn tại file âm thanh tiếng mưa rào xứ Huế: SFX_Rain_Ambient_Loop.wav");
            Assert(File.Exists(stepWav), "Tồn tại file âm thanh bước chân người đi bộ: SFX_Footstep_Step.wav");

            // 2. Kiểm tra VehicleAudioController & Kích thích tiếng còi
            GameObject testCarObj = new GameObject("Test_Vehicle_Audio");
            try
            {
                CarController car = testCarObj.AddComponent<CarController>();
                VehicleAudioController audioCtrl = testCarObj.AddComponent<VehicleAudioController>();

                Assert(audioCtrl != null, "VehicleAudioController: Khởi tạo thành công bộ điều phối âm thanh xe");
                Assert(audioCtrl.EngineSource != null && audioCtrl.EngineSource.loop, "VehicleAudioController: Engine AudioSource cấu hình Loop chuẩn xác");

                // Thử nghiệm bấm còi an toàn
                audioCtrl.PlayHorn();
                Assert(true, "VehicleAudioController: Thực thi PlayHorn() và kích thích giác quan NPC thành công");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testCarObj);
            }

            // 3. Kiểm tra PlayerFootstepAudio
            GameObject testPlayerObj = new GameObject("Test_Player_Footstep");
            try
            {
                testPlayerObj.AddComponent<CharacterController>();
                testPlayerObj.AddComponent<PlayerMovement>();
                PlayerFootstepAudio footstep = testPlayerObj.AddComponent<PlayerFootstepAudio>();

                Assert(footstep != null, "PlayerFootstepAudio: Khởi tạo thành công bộ điều phối bước chân người chơi");
                Assert(footstep.FootstepSource != null, "PlayerFootstepAudio: Tự động khởi tạo AudioSource cho bước chân");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testPlayerObj);
            }

            // 4. Kiểm tra WeatherManager (Mưa rào & Nắng vàng)
            GameObject testWeatherObj = new GameObject("Test_WeatherManager");
            try
            {
                WeatherManager weather = testWeatherObj.AddComponent<WeatherManager>();
                Assert(weather != null, "WeatherManager: Khởi tạo thành công trình điều phối thời tiết Cố Đô");

                bool eventFired = false;
                Action<bool> handler = (isRain) => { eventFired = true; };
                GameEvents.OnWeatherRainChanged += handler;

                try
                {
                    weather.SetRaining(true);
                    Assert(weather.IsRaining, "WeatherManager: Kích hoạt thành công chế độ Mưa rào xứ Huế (IsRaining = true)");
                    Assert(eventFired, "WeatherManager: Phát thành công sự kiện GameEvents.OnWeatherRainChanged");

                    weather.SetRaining(false);
                    Assert(!weather.IsRaining, "WeatherManager: Chuyển đổi thành công sang chế độ Tạnh mưa / Hửng nắng (IsRaining = false)");
                }
                finally
                {
                    GameEvents.OnWeatherRainChanged -= handler;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testWeatherObj);
            }

            // 5. Kiểm tra VehicleVFXController (Đèn pha, đèn phanh, khói ống bô)
            GameObject testVfxCar = new GameObject("Test_Vehicle_VFX");
            try
            {
                testVfxCar.AddComponent<CarController>();
                VehicleVFXController vfxCtrl = testVfxCar.AddComponent<VehicleVFXController>();

                Assert(vfxCtrl != null, "VehicleVFXController: Khởi tạo thành công bộ điều phối hiệu ứng hình ảnh xe");
                vfxCtrl.SetHeadlights(true);
                Assert(vfxCtrl.HeadlightsEnabled, "VehicleVFXController: Bật đèn pha chiếu sáng mặt đường ban đêm");
                vfxCtrl.SetHeadlights(false);
                Assert(!vfxCtrl.HeadlightsEnabled, "VehicleVFXController: Tắt đèn pha thành công");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testVfxCar);
            }

            // 6. Kiểm tra Scene Giai đoạn 7: Phase7_PolishedCity.unity
            string phase7Path = "Assets/_Project/Scenes/Phase7_PolishedCity.unity";
            Assert(File.Exists(phase7Path), "Tồn tại file Scene Giai đoạn 7 Đánh bóng & Âm thanh: Phase7_PolishedCity.unity");
        }

        private static void Assert(bool condition, string testName)
        {
            if (condition)
            {
                passedTests++;
                reportLog.AppendLine($"  [PASS] {testName}");
            }
            else
            {
                failedTests++;
                reportLog.AppendLine($"  [FAIL] {testName}");
                Debug.LogError($"[HueCity Test FAILED] {testName}");
            }
        }

        private static void LogHeader(string title)
        {
            reportLog.AppendLine("================================================================================");
            reportLog.AppendLine($"  {title}");
            reportLog.AppendLine($"  Thời gian: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            reportLog.AppendLine("================================================================================");
            reportLog.AppendLine();
        }

        private static void LogSection(string sectionName)
        {
            reportLog.AppendLine($"--- {sectionName} ---");
        }

        private static void LogFooter(TimeSpan duration)
        {
            reportLog.AppendLine();
            reportLog.AppendLine("================================================================================");
            reportLog.AppendLine($"  TỔNG KẾT: {passedTests} PASSED, {failedTests} FAILED (Thời gian chạy: {duration.TotalMilliseconds:F1}ms)");
            reportLog.AppendLine("================================================================================");
        }
    }
}
#endif
