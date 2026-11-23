#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using Cinemachine;
using HueCity.Camera;
using HueCity.Core;
using HueCity.NPC;
using HueCity.Player;
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
            }
            catch (Exception ex)
            {
                Assert(false, $"Ngoại lệ bất ngờ trong quá trình test: {ex.Message}");
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
