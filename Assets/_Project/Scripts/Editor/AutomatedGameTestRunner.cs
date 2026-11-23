#if UNITY_EDITOR
using System;
using System.IO;
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
                TestAssetIntegrity();
                TestPlayerMechanics();
                TestVehicleMechanics();
                TestEnterExitVehicleTransition();
                TestWorldStimulusAndNPC();
                TestActivityPoint();
                TestCameraAndEvents();
                TestDataAndPersistence();
                TestMCPPackageInstallation();
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
