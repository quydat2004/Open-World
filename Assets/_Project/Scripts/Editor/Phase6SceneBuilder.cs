#if UNITY_EDITOR
#pragma warning disable CS0618
using System.Collections.Generic;
using System.IO;
using Cinemachine;
using HueCity.Camera;
using HueCity.Core;
using HueCity.Data;
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
using UnityEngine.UI;
using Camera = UnityEngine.Camera;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HueCity.EditorTools
{
    using Camera = UnityEngine.Camera;

    /// <summary>
    /// Trình dựng Scene Giai đoạn 6: Mở rộng Bản đồ 500m x 500m & Tối ưu Hiệu năng (Phase 6 Expanded Map Scene Builder).
    /// Tích hợp đầy đủ:
    /// - Bản đồ đô thị mở rộng quy mô 500m x 500m (-250m đến +250m trên cả 2 trục X và Z).
    /// - Hệ thống phố xá bàn cờ: 2 trục đại lộ chính và 4 tuyến phố nhánh phân ô 9 đại khối phố (City Super-Blocks).
    /// - 40+ Tòa nhà phố Modular bố trí thành các dãy phố sầm uất.
    /// - Tối ưu hóa hiệu năng AAA: GPU Instancing cho toàn bộ Material, hệ thống LOD Group 2 cấp độ và Occlusion Static.
    /// - Mạng lưới giao thông và Waypoint mở rộng toàn thành phố.
    /// - Vòng lặp Gameplay & Giao diện hoàn chỉnh (Minimap 500m, Speedometer, Mission HUD, Player HUD, JSON Save/Load).
    /// </summary>
    public static class Phase6SceneBuilder
    {
        [InitializeOnLoadMethod]
        private static void AutoRegisterSceneBuild()
        {
            EditorApplication.delayCall += () =>
            {
                string markerPath = "Temp/Phase6_ExpandedMap_Built.marker";
                if (!File.Exists(markerPath))
                {
                    File.WriteAllText(markerPath, "built");
                    Debug.Log("[HueCity Phase 6] Tự động khởi tạo Scene Giai đoạn 6: Mở rộng Bản đồ 500m x 500m...");
                    BuildScene(true);
                }
            };
        }

        [MenuItem("HueCity/Build Phase 6 Expanded Map (500m x 500m)", false, 60)]
        public static void BuildSceneMenu()
        {
            BuildScene(false);
        }

        public static void BuildScene(bool silent)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[HueCity Phase 6] Không thể build scene khi đang trong Play Mode.");
                return;
            }

            // 0. Chuẩn bị Asset, URP, Locomotion & Bật GPU Instancing
            RealismOverhaulSetup.EnsureFullSetup();
            LocomotionClipGenerator.EnsureClipsAndController();
            EnableAllMaterialsGPUInstancing();
            List<MissionData> sampleMissions = EnsureExtendedMissions();

            // 1. Tạo scene mới
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Thiết lập Mặt trời & Chu kỳ Ngày/Đêm
            GameObject lightObj = new GameObject("Directional Light - Hue Sun");
            Light sunLight = lightObj.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1.0f, 0.957f, 0.878f);
            sunLight.intensity = 1.35f;
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowResolution = LightShadowResolution.VeryHigh;
            lightObj.transform.rotation = Quaternion.Euler(45f, -38f, 0f);

            DayNightCycle dayNight = lightObj.AddComponent<DayNightCycle>();
            dayNight.SetTimeOfDay(16.5f);

            Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Skybox_Procedural.mat");
            if (skyboxMat != null) RenderSettings.skybox = skyboxMat;
            RenderSettings.sun = sunLight;
            RenderSettings.ambientMode = AmbientMode.Skybox;

            // 3. Global Volume Post-Processing
            SetupGlobalPostProcessingVolume();

            // 4. WorldStimulusManager
            GameObject stimulusObj = new GameObject("WorldStimulusManager");
            stimulusObj.AddComponent<WorldStimulusManager>();

            // 5. Dựng Hệ thống Đường sá & Vỉa hè mở rộng quy mô 500m x 500m
            GameObject roadSystem = SetupExpanded500mRoadAndSidewalkGrid();

            // 6. Dựng 40+ Dãy Nhà phố Modular dọc các khối phố (kèm LOD Group & Occlusion Static)
            SpawnExpandedCityShophouses();

            // 7. Đạo cụ văn hóa Cố đô (Quán nước, Xích lô, Xe máy)
            SpawnExpandedStreetCultureProps();

            // 8. Bake NavMesh tự động cho toàn bộ vỉa hè bản đồ 500m
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            Debug.Log("[HueCity Phase 6] Đã bake thành công NavMesh mở rộng cho bản đồ 500m x 500m!");

            // 9. Tạo Nhân vật chính (Player)
            Vector3 playerSpawn = new Vector3(13.0f, 0.25f, 13.0f);
            GameObject player = CreatePlayerObject(playerSpawn);

            // 10. Tạo Xe Taxi người chơi lái
            Vector3 taxiSpawn = new Vector3(3.5f, 0.35f, 16.0f);
            GameObject playerTaxi = CreatePlayerTaxiObject(taxiSpawn);

            // 11. Tạo Cinemachine Camera
            Camera mainCam = CreateCameraSetup(player, playerTaxi);

            // 12. Tạo Đèn giao thông tại ngã tư trung tâm
            TrafficLightController trafficLights = SetupTrafficLights();

            // 13. Tạo Mạng lưới Waypoints giao thông mở rộng nối các trục đường chính
            List<TrafficWaypoint> startWaypoints = SetupExpandedTrafficWaypoints();

            // 14. Tạo Trình quản lý Xe giao thông (TrafficPoolManager)
            SetupTrafficPool(startWaypoints);

            // 15. Tạo Đám đông NPC (20 NPC với FSM 5 trạng thái và Streaming theo khoảng cách)
            SetupNPCCrowd(player);

            // 16. Tạo Save/Load Manager (Persistence JSON)
            GameObject saveLoadObj = new GameObject("SaveLoadManager");
            saveLoadObj.AddComponent<SaveLoadManager>();

            // 17. Tạo Trình quản lý Nhiệm vụ (MissionManager)
            GameObject missionMgrObj = new GameObject("MissionManager");
            MissionManager missionMgr = missionMgrObj.AddComponent<MissionManager>();
            foreach (var m in sampleMissions)
            {
                missionMgr.RegisterMission(m);
            }

            // 18. Tạo Giao diện Người dùng (uGUI Canvas với Minimap 500m, Speedometer, MissionHUD, PlayerHUD)
            GameHUDManager hudManager;
            GameObject canvasObj = GameHUDManager.CreateFullHUDCanvas(player.transform, mainCam, out hudManager);

            // 19. Cấu hình Camera Minimap bao quát bản đồ 500m
            SetupExpandedMinimapCamera(hudManager.Minimap, player.transform);

            // 20. Bắt đầu nhiệm vụ mẫu đầu tiên
            if (sampleMissions.Count > 0)
            {
                missionMgr.StartMission(sampleMissions[0]);
            }

            // 21. Lưu Scene Phase 6 chính thức và cập nhật các Scene tiền nhiệm
            string phase6Path = "Assets/_Project/Scenes/Phase6_ExpandedMap.unity";
            EditorSceneManager.SaveScene(newScene, phase6Path);

            string phase5Path = "Assets/_Project/Scenes/Phase5_GameplayLoop.unity";
            EditorSceneManager.SaveScene(newScene, phase5Path);

            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[HueCity Phase 6]</color> Đã hoàn tất và lưu Scene Bản đồ Mở rộng 500m x 500m tại: {phase6Path}");
        }

        private static void EnableAllMaterialsGPUInstancing()
        {
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new string[] { "Assets/_Project/Materials" });
            foreach (var guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                {
                    CityOptimizationUtility.EnableGPUInstancing(mat);
                    EditorUtility.SetDirty(mat);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[HueCity Phase 6] Đã kích hoạt GPU Instancing cho 100% PBR Materials trong dự án!");
        }

        private static GameObject SetupExpanded500mRoadAndSidewalkGrid()
        {
            Material roadMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Road_Asphalt.mat");
            Material sidewalkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Sidewalk_Concrete.mat");
            Material curbMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Curb_Stone.mat");
            Material whiteLineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_RoadMarking_White.mat");
            Material yellowLineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_RoadMarking_Yellow.mat");

            GameObject rootSystem = new GameObject("Expanded_500m_City_Grid");

            // 1. Mặt đất nền đô thị bao phủ toàn bộ 500m x 500m (-250m đến +250m)
            GameObject baseGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseGround.name = "Ground_Terrain_Base_500m";
            baseGround.transform.SetParent(rootSystem.transform);
            baseGround.transform.position = new Vector3(0f, -0.15f, 0f);
            baseGround.transform.localScale = new Vector3(500.0f, 0.20f, 500.0f);
            if (roadMat != null) baseGround.GetComponent<Renderer>().sharedMaterial = roadMat;
            CityOptimizationUtility.SetOptimizedStaticFlags(baseGround, isOccluder: false);

            // 2. Hai trục đại lộ chính (Trục Bắc-Nam & Trục Đông-Tây) rộng 14m, dài 500m
            CreateRoadCorridor(rootSystem.transform, "Main_Avenue_NorthSouth", new Vector3(0f, -0.04f, 0f), new Vector3(14.0f, 0.10f, 500.0f), roadMat);
            CreateRoadCorridor(rootSystem.transform, "Main_Boulevard_EastWest",  new Vector3(0f, -0.04f, 0f), new Vector3(500.0f, 0.10f, 14.0f), roadMat);

            // 3. Bốn trục đường phố nhánh song song (Z = ±125m, X = ±125m) rộng 11m
            CreateRoadCorridor(rootSystem.transform, "Secondary_Street_North_Z125", new Vector3(0f, -0.04f, 125.0f), new Vector3(500.0f, 0.10f, 11.0f), roadMat);
            CreateRoadCorridor(rootSystem.transform, "Secondary_Street_South_Z_125", new Vector3(0f, -0.04f, -125.0f), new Vector3(500.0f, 0.10f, 11.0f), roadMat);
            CreateRoadCorridor(rootSystem.transform, "Secondary_Street_East_X125", new Vector3(125.0f, -0.04f, 0f), new Vector3(11.0f, 0.10f, 500.0f), roadMat);
            CreateRoadCorridor(rootSystem.transform, "Secondary_Street_West_X_125", new Vector3(-125.0f, -0.04f, 0f), new Vector3(11.0f, 0.10f, 500.0f), roadMat);

            // 4. Vỉa hè nâng cao (+0.15m) cho 9 đại khối phố (City Super-Blocks)
            float[] blockCoords = new float[] { -187.5f, 0f, 187.5f };

            int blockIdx = 0;
            for (int bx = 0; bx < 3; bx++)
            {
                for (int bz = 0; bz < 3; bz++)
                {
                    blockIdx++;
                    Vector3 blockCenter = new Vector3(blockCoords[bx], 0.075f, blockCoords[bz]);
                    float sizeX = (bx == 1) ? 104.0f : 108.0f;
                    float sizeZ = (bz == 1) ? 104.0f : 108.0f;

                    GameObject sidewalkBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    sidewalkBlock.name = $"Sidewalk_SuperBlock_{blockIdx}_X{bx}_Z{bz}";
                    sidewalkBlock.transform.SetParent(rootSystem.transform);
                    sidewalkBlock.transform.position = blockCenter;
                    sidewalkBlock.transform.localScale = new Vector3(sizeX, 0.15f, sizeZ);
                    if (sidewalkMat != null) sidewalkBlock.GetComponent<Renderer>().sharedMaterial = sidewalkMat;
                    CityOptimizationUtility.SetOptimizedStaticFlags(sidewalkBlock, isOccluder: false);

                    // Gờ bó vỉa đá xung quanh mép
                    CreateBlockCurbs(rootSystem.transform, blockCenter, sizeX, sizeZ, curbMat);
                }
            }

            // 5. Vạch kẻ đường trung tâm và vạch qua đường
            CreateExtendedRoadMarkings(rootSystem.transform, whiteLineMat, yellowLineMat);

            return rootSystem;
        }

        private static void CreateRoadCorridor(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = name;
            road.transform.SetParent(parent);
            road.transform.position = pos;
            road.transform.localScale = size;
            if (mat != null) road.GetComponent<Renderer>().sharedMaterial = mat;
            CityOptimizationUtility.SetOptimizedStaticFlags(road, isOccluder: false);
        }

        private static void CreateBlockCurbs(Transform parent, Vector3 center, float sizeX, float sizeZ, Material curbMat)
        {
            float halfX = sizeX * 0.5f;
            float halfZ = sizeZ * 0.5f;

            // Curb cạnh Z Bắc & Nam
            CreateCurbStrip(parent, new Vector3(center.x, 0.08f, center.z + halfZ), new Vector3(sizeX, 0.16f, 0.25f), curbMat);
            CreateCurbStrip(parent, new Vector3(center.x, 0.08f, center.z - halfZ), new Vector3(sizeX, 0.16f, 0.25f), curbMat);

            // Curb cạnh X Đông & Tây
            CreateCurbStrip(parent, new Vector3(center.x + halfX, 0.08f, center.z), new Vector3(0.25f, 0.16f, sizeZ), curbMat);
            CreateCurbStrip(parent, new Vector3(center.x - halfX, 0.08f, center.z), new Vector3(0.25f, 0.16f, sizeZ), curbMat);
        }

        private static void CreateCurbStrip(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curb.name = "Curb_Border";
            curb.transform.SetParent(parent);
            curb.transform.position = pos;
            curb.transform.localScale = size;
            if (mat != null) curb.GetComponent<Renderer>().sharedMaterial = mat;
            CityOptimizationUtility.SetOptimizedStaticFlags(curb, isOccluder: false);
        }

        private static void CreateExtendedRoadMarkings(Transform parent, Material whiteMat, Material yellowMat)
        {
            GameObject markingsParent = new GameObject("PBR_Road_Markings_500m");
            markingsParent.transform.SetParent(parent);

            float markY = 0.005f;

            // Vạch đôi vàng phân làn cho các trục đại lộ chính
            CreateCenterDoubleYellow(markingsParent.transform, new Vector3(0f, markY, 0f), 500.0f, false, yellowMat);
            CreateCenterDoubleYellow(markingsParent.transform, new Vector3(0f, markY, 0f), 500.0f, true, yellowMat);

            // Vạch qua đường tại 5 ngã tư trọng điểm: (0,0), (125,125), (-125,125), (125,-125), (-125,-125)
            Vector2[] intersections = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(125f, 125f),
                new Vector2(-125f, 125f),
                new Vector2(125f, -125f),
                new Vector2(-125f, -125f)
            };

            foreach (var inter in intersections)
            {
                CreateIntersectionCrossings(markingsParent.transform, new Vector3(inter.x, markY, inter.y), whiteMat);
            }
        }

        private static void CreateCenterDoubleYellow(Transform parent, Vector3 center, float length, bool isHorizontal, Material mat)
        {
            float spacing = 0.20f;
            float width = 0.15f;

            for (int i = -1; i <= 1; i += 2)
            {
                GameObject line = GameObject.CreatePrimitive(PrimitiveType.Quad);
                line.name = "Yellow_Center_Line";
                line.transform.SetParent(parent);
                line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                if (isHorizontal)
                {
                    line.transform.position = new Vector3(center.x, center.y, center.z + i * (spacing * 0.5f));
                    line.transform.localScale = new Vector3(length, width, 1f);
                }
                else
                {
                    line.transform.position = new Vector3(center.x + i * (spacing * 0.5f), center.y, center.z);
                    line.transform.localScale = new Vector3(width, length, 1f);
                }

                Object.DestroyImmediate(line.GetComponent<Collider>());
                if (mat != null) line.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        private static void CreateIntersectionCrossings(Transform parent, Vector3 center, Material whiteMat)
        {
            float offset = 9.0f;
            CreateZebraStripes(parent, new Vector3(center.x, center.y, center.z + offset), false, whiteMat);
            CreateZebraStripes(parent, new Vector3(center.x, center.y, center.z - offset), false, whiteMat);
            CreateZebraStripes(parent, new Vector3(center.x + offset, center.y, center.z), true, whiteMat);
            CreateZebraStripes(parent, new Vector3(center.x - offset, center.y, center.z), true, whiteMat);
        }

        private static void CreateZebraStripes(Transform parent, Vector3 center, bool isHorizontal, Material mat)
        {
            int stripeCount = 6;
            float stripeWidth = 0.50f;
            float stripeLength = 3.2f;
            float stripeGap = 0.50f;
            float totalSpan = stripeCount * (stripeWidth + stripeGap);
            float startOffset = -totalSpan * 0.5f + (stripeWidth + stripeGap) * 0.5f;

            for (int i = 0; i < stripeCount; i++)
            {
                GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Quad);
                stripe.name = $"Zebra_Stripe_{i}";
                stripe.transform.SetParent(parent);
                stripe.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                float off = startOffset + i * (stripeWidth + stripeGap);
                if (isHorizontal)
                {
                    stripe.transform.position = new Vector3(center.x, center.y, center.z + off);
                    stripe.transform.localScale = new Vector3(stripeLength, stripeWidth, 1f);
                }
                else
                {
                    stripe.transform.position = new Vector3(center.x + off, center.y, center.z);
                    stripe.transform.localScale = new Vector3(stripeWidth, stripeLength, 1f);
                }

                Object.DestroyImmediate(stripe.GetComponent<Collider>());
                if (mat != null) stripe.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        private static void SpawnExpandedCityShophouses()
        {
            string houseFbxPath = "Assets/_Project/Models/House_Modular_01.fbx";
            GameObject housePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(houseFbxPath);
            if (housePrefab == null) return;

            GameObject cityBuildingsParent = new GameObject("City_Buildings_Corridors_500m");

            // Tọa độ 40+ căn nhà phố bố trí dọc 4 dãy phố chính
            List<Vector3> positions = new List<Vector3>();
            List<float> rotations = new List<float>();

            // 1. Dãy phố Ngã tư trung tâm (8 căn)
            float[] offsets = new float[] { 16f, 23f, 30f, 37f };
            foreach (var off in offsets)
            {
                positions.Add(new Vector3(off, 0.15f, 16f)); rotations.Add(180f);
                positions.Add(new Vector3(-off, 0.15f, 16f)); rotations.Add(180f);
                positions.Add(new Vector3(off, 0.15f, -16f)); rotations.Add(0f);
                positions.Add(new Vector3(-off, 0.15f, -16f)); rotations.Add(0f);
            }

            // 2. Dãy phố Phía Đông (Chợ Đông Ba & Phố ẩm thực - 12 căn)
            float[] eastOffsets = new float[] { 110f, 117f, 124f, 131f, 138f, 145f };
            foreach (var off in eastOffsets)
            {
                positions.Add(new Vector3(off, 0.15f, 25f)); rotations.Add(180f);
                positions.Add(new Vector3(off, 0.15f, -25f)); rotations.Add(0f);
            }

            // 3. Dãy phố Phía Tây (Phố Khách sạn Cố Đô - 12 căn)
            foreach (var off in eastOffsets)
            {
                positions.Add(new Vector3(-off, 0.15f, 25f)); rotations.Add(180f);
                positions.Add(new Vector3(-off, 0.15f, -25f)); rotations.Add(0f);
            }

            // 4. Dãy phố Phía Bắc (Đại lộ Ngọ Môn - 8 căn)
            float[] northOffsets = new float[] { 110f, 117f, 124f, 131f };
            foreach (var off in northOffsets)
            {
                positions.Add(new Vector3(25f, 0.15f, off)); rotations.Add(270f);
                positions.Add(new Vector3(-25f, 0.15f, off)); rotations.Add(90f);
            }

            for (int i = 0; i < positions.Count; i++)
            {
                GameObject house = (GameObject)PrefabUtility.InstantiatePrefab(housePrefab);
                house.name = $"Shophouse_500m_{i + 1}";
                house.transform.SetParent(cityBuildingsParent.transform);
                house.transform.position = positions[i];
                house.transform.rotation = Quaternion.Euler(0f, rotations[i], 0f);

                // Gán MeshCollider cho khối tường
                MeshFilter[] mfs = house.GetComponentsInChildren<MeshFilter>();
                foreach (var mf in mfs)
                {
                    if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null)
                    {
                        MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                    }
                }

                // Thiết lập LOD Group và Cờ Occlusion Static
                CityOptimizationUtility.SetupBuildingLODGroup(house, 0.35f, 0.08f);
                CityOptimizationUtility.SetOptimizedStaticFlags(house, isOccluder: true);
            }

            Debug.Log($"[HueCity Phase 6] Đã bố trí {positions.Count} căn nhà phố Modular kèm hệ thống LOD Group và Occlusion Static!");
        }

        private static void SpawnExpandedStreetCultureProps()
        {
            string scooterPath = "Assets/_Project/Models/Vehicle_Scooter.fbx";
            GameObject scooterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(scooterPath);

            string cycloPath = "Assets/_Project/Models/Vehicle_XichLo.fbx";
            GameObject cycloPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cycloPath);

            string chairsPath = "Assets/_Project/Models/Props_Street_Chairs.fbx";
            GameObject chairsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(chairsPath);

            GameObject propsGroup = new GameObject("Street_Culture_Props_500m");

            if (scooterPrefab != null)
            {
                SpawnProp(scooterPrefab, "Scooter_Cafe_1", new Vector3(10.5f, 0.25f, 12.0f), 75f, propsGroup.transform);
                SpawnProp(scooterPrefab, "Scooter_Hotel_2", new Vector3(-125f, 0.25f, 30.0f), -45f, propsGroup.transform);
                SpawnProp(scooterPrefab, "Scooter_Market_3", new Vector3(128f, 0.25f, -30.0f), 110f, propsGroup.transform);
            }

            if (cycloPrefab != null)
            {
                SpawnProp(cycloPrefab, "XichLo_Imperial_1", new Vector3(-13.0f, 0.25f, 6.0f), -50f, propsGroup.transform);
                SpawnProp(cycloPrefab, "XichLo_Market_2", new Vector3(135.0f, 0.25f, 15.0f), 30f, propsGroup.transform);
            }

            if (chairsPrefab != null)
            {
                GameObject chairs1 = SpawnProp(chairsPrefab, "Cafe_Chairs_Center", new Vector3(14.0f, 0.25f, -14.5f), 0f, propsGroup.transform);
                AttachActivityPoints(chairs1);

                GameObject chairs2 = SpawnProp(chairsPrefab, "Cafe_Chairs_EastMarket", new Vector3(122.0f, 0.25f, 32.0f), 90f, propsGroup.transform);
                AttachActivityPoints(chairs2);
            }
        }

        private static GameObject SpawnProp(GameObject prefab, string name, Vector3 pos, float yRot, Transform parent)
        {
            GameObject prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            prop.name = name;
            prop.transform.SetParent(parent);
            prop.transform.position = pos;
            prop.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
            return prop;
        }

        private static void AttachActivityPoints(GameObject root)
        {
            if (root == null) return;
            foreach (Transform child in root.transform)
            {
                if (child.name.Contains("Chair"))
                {
                    ActivityPoint ap = child.gameObject.AddComponent<ActivityPoint>();
                    SerializedObject apSo = new SerializedObject(ap);
                    apSo.FindProperty("isSheltered").boolValue = true;
                    apSo.ApplyModifiedProperties();
                }
            }
        }

        private static List<TrafficWaypoint> SetupExpandedTrafficWaypoints()
        {
            GameObject trafficNetwork = new GameObject("Expanded_Traffic_Waypoint_Network_500m");
            List<TrafficWaypoint> startPoints = new List<TrafficWaypoint>();

            // Trục chính Bắc -> Nam (500m)
            TrafficWaypoint n_entry = CreateWaypoint("WP_North_500m_Entry", new Vector3(-3.5f, 0.2f, 240.0f), trafficNetwork.transform);
            TrafficWaypoint n_mid1  = CreateWaypoint("WP_North_Mid_Z125",   new Vector3(-3.5f, 0.2f, 125.0f), trafficNetwork.transform);
            TrafficWaypoint n_stop  = CreateWaypoint("WP_North_Stop_Z8",     new Vector3(-3.5f, 0.2f, 8.0f),   trafficNetwork.transform);
            n_stop.SetStopPoint(true);
            n_stop.SetDirection(TrafficDirection.NorthSouth);
            TrafficWaypoint n_center= CreateWaypoint("WP_Center_NS_Z0",     new Vector3(-3.5f, 0.2f, 0.0f),   trafficNetwork.transform);
            TrafficWaypoint n_mid2  = CreateWaypoint("WP_South_Mid_Z_125",  new Vector3(-3.5f, 0.2f, -125.0f),trafficNetwork.transform);
            TrafficWaypoint n_exit  = CreateWaypoint("WP_South_500m_Exit",  new Vector3(-3.5f, 0.2f, -240.0f),trafficNetwork.transform);

            n_entry.AddNextWaypoint(n_mid1);
            n_mid1.AddNextWaypoint(n_stop);
            n_stop.AddNextWaypoint(n_center);
            n_center.AddNextWaypoint(n_mid2);
            n_mid2.AddNextWaypoint(n_exit);
            startPoints.Add(n_entry);

            // Trục chính Nam -> Bắc (500m)
            TrafficWaypoint s_entry = CreateWaypoint("WP_South_500m_Entry", new Vector3(3.5f, 0.2f, -240.0f), trafficNetwork.transform);
            TrafficWaypoint s_mid1  = CreateWaypoint("WP_South_Mid_Z_125",  new Vector3(3.5f, 0.2f, -125.0f), trafficNetwork.transform);
            TrafficWaypoint s_stop  = CreateWaypoint("WP_South_Stop_Z_8",   new Vector3(3.5f, 0.2f, -8.0f),   trafficNetwork.transform);
            s_stop.SetStopPoint(true);
            s_stop.SetDirection(TrafficDirection.NorthSouth);
            TrafficWaypoint s_center= CreateWaypoint("WP_Center_SN_Z0",     new Vector3(3.5f, 0.2f, 0.0f),    trafficNetwork.transform);
            TrafficWaypoint s_mid2  = CreateWaypoint("WP_North_Mid_Z125",   new Vector3(3.5f, 0.2f, 125.0f),  trafficNetwork.transform);
            TrafficWaypoint s_exit  = CreateWaypoint("WP_North_500m_Exit",  new Vector3(3.5f, 0.2f, 240.0f),  trafficNetwork.transform);

            s_entry.AddNextWaypoint(s_mid1);
            s_mid1.AddNextWaypoint(s_stop);
            s_stop.AddNextWaypoint(s_center);
            s_center.AddNextWaypoint(s_mid2);
            s_mid2.AddNextWaypoint(s_exit);
            startPoints.Add(s_entry);

            // Trục Đông -> Tây (500m)
            TrafficWaypoint e_entry = CreateWaypoint("WP_East_500m_Entry",  new Vector3(240.0f, 0.2f, 3.5f),  trafficNetwork.transform);
            TrafficWaypoint e_mid1  = CreateWaypoint("WP_East_Mid_X125",    new Vector3(125.0f, 0.2f, 3.5f),  trafficNetwork.transform);
            TrafficWaypoint e_stop  = CreateWaypoint("WP_East_Stop_X8",     new Vector3(8.0f, 0.2f, 3.5f),    trafficNetwork.transform);
            e_stop.SetStopPoint(true);
            e_stop.SetDirection(TrafficDirection.EastWest);
            TrafficWaypoint e_center= CreateWaypoint("WP_Center_EW_X0",     new Vector3(0.0f, 0.2f, 3.5f),    trafficNetwork.transform);
            TrafficWaypoint e_mid2  = CreateWaypoint("WP_West_Mid_X_125",   new Vector3(-125.0f, 0.2f, 3.5f), trafficNetwork.transform);
            TrafficWaypoint e_exit  = CreateWaypoint("WP_West_500m_Exit",   new Vector3(-240.0f, 0.2f, 3.5f), trafficNetwork.transform);

            e_entry.AddNextWaypoint(e_mid1);
            e_mid1.AddNextWaypoint(e_stop);
            e_stop.AddNextWaypoint(e_center);
            e_center.AddNextWaypoint(e_mid2);
            e_mid2.AddNextWaypoint(e_exit);
            startPoints.Add(e_entry);

            // Trục Tây -> Đông (500m)
            TrafficWaypoint w_entry = CreateWaypoint("WP_West_500m_Entry",  new Vector3(-240.0f, 0.2f, -3.5f),trafficNetwork.transform);
            TrafficWaypoint w_mid1  = CreateWaypoint("WP_West_Mid_X_125",   new Vector3(-125.0f, 0.2f, -3.5f),trafficNetwork.transform);
            TrafficWaypoint w_stop  = CreateWaypoint("WP_West_Stop_X_8",    new Vector3(-8.0f, 0.2f, -3.5f),  trafficNetwork.transform);
            w_stop.SetStopPoint(true);
            w_stop.SetDirection(TrafficDirection.EastWest);
            TrafficWaypoint w_center= CreateWaypoint("WP_Center_WE_X0",     new Vector3(0.0f, 0.2f, -3.5f),   trafficNetwork.transform);
            TrafficWaypoint w_mid2  = CreateWaypoint("WP_East_Mid_X125",    new Vector3(125.0f, 0.2f, -3.5f), trafficNetwork.transform);
            TrafficWaypoint w_exit  = CreateWaypoint("WP_East_500m_Exit",   new Vector3(240.0f, 0.2f, -3.5f), trafficNetwork.transform);

            w_entry.AddNextWaypoint(w_mid1);
            w_mid1.AddNextWaypoint(w_stop);
            w_stop.AddNextWaypoint(w_center);
            w_center.AddNextWaypoint(w_mid2);
            w_mid2.AddNextWaypoint(w_exit);
            startPoints.Add(w_entry);

            // Nhánh rẽ phải tự nhiên tại ngã tư trung tâm
            n_center.AddNextWaypoint(w_mid2);
            s_center.AddNextWaypoint(e_mid2);
            e_center.AddNextWaypoint(n_mid2);
            w_center.AddNextWaypoint(s_mid2);

            return startPoints;
        }

        private static TrafficWaypoint CreateWaypoint(string name, Vector3 pos, Transform parent)
        {
            GameObject wpObj = new GameObject(name);
            wpObj.transform.SetParent(parent);
            wpObj.transform.position = pos;
            return wpObj.AddComponent<TrafficWaypoint>();
        }

        private static void SetupExpandedMinimapCamera(MinimapController minimapCtrl, Transform playerTarget)
        {
            if (minimapCtrl == null) return;

            GameObject miniCamObj = new GameObject("Minimap_TopDown_Camera_500m");
            Camera miniCam = miniCamObj.AddComponent<Camera>();
            miniCam.orthographic = true;
            miniCam.orthographicSize = 90.0f; // Bao quát tầm nhìn rộng hơn cho bản đồ 500m
            miniCam.nearClipPlane = 0.5f;
            miniCam.farClipPlane = 200.0f;
            miniCam.clearFlags = CameraClearFlags.Color;
            miniCam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);

            string rtPath = "Assets/_Project/Textures/RT_Minimap.renderTexture";
            RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
            if (rt == null)
            {
                rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
                rt.name = "RT_Minimap";
                AssetDatabase.CreateAsset(rt, rtPath);
            }

            miniCam.targetTexture = rt;
            minimapCtrl.SetMinimapCamera(miniCam);

            // Gán Texture vào RawImage nền Minimap (tạo GameObject con riêng để không xung đột với Image)
            RawImage rawImg = minimapCtrl.GetComponentInChildren<RawImage>();
            if (rawImg == null)
            {
                GameObject rtChild = new GameObject("Minimap_RT_Display");
                rtChild.transform.SetParent(minimapCtrl.transform, false);
                rtChild.transform.SetAsFirstSibling();
                RectTransform rtRect = rtChild.AddComponent<RectTransform>();
                rtRect.anchorMin = Vector2.zero;
                rtRect.anchorMax = Vector2.one;
                rtRect.sizeDelta = Vector2.zero;
                rawImg = rtChild.AddComponent<RawImage>();
                rawImg.raycastTarget = false;
            }
            rawImg.texture = rt;
        }

        private static List<MissionData> EnsureExtendedMissions()
        {
            List<MissionData> missions = new List<MissionData>();
            string dir = "Assets/_Project/ScriptableObjects/Missions";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // Mission 1: Giao hàng ẩm thực Cố đô
            string m1Path = $"{dir}/Mission_01_Delivery_HueCakes.asset";
            MissionData m1 = AssetDatabase.LoadAssetAtPath<MissionData>(m1Path);
            if (m1 == null)
            {
                m1 = ScriptableObject.CreateInstance<MissionData>();
                m1.Initialize("MIS_01", "Giao Đặc Sản Bánh Bột Lọc",
                    "Nhận bánh bột lọc từ quán cà phê vỉa hè và giao sang đoàn khách tại góc đối diện.",
                    MissionType.Delivery, 50000, 90f,
                    new Vector3(14.0f, 0.25f, -14.0f), new Vector3(-14.0f, 0.25f, 14.0f), 3.5f);
                AssetDatabase.CreateAsset(m1, m1Path);
            }
            missions.Add(m1);

            // Mission 2: Taxi du lịch Cố đô
            string m2Path = $"{dir}/Mission_02_Taxi_TourImperial.asset";
            MissionData m2 = AssetDatabase.LoadAssetAtPath<MissionData>(m2Path);
            if (m2 == null)
            {
                m2 = ScriptableObject.CreateInstance<MissionData>();
                m2.Initialize("MIS_02", "Chở Du Khách Tham Quan",
                    "Lái xe taxi đón khách tại trạm và chở qua ngã tư hướng về cổng Ngọ Môn.",
                    MissionType.TaxiPassenger, 75000, 120f,
                    new Vector3(3.5f, 0.35f, 16.0f), new Vector3(-3.5f, 0.35f, -25.0f), 4.0f);
                AssetDatabase.CreateAsset(m2, m2Path);
            }
            missions.Add(m2);

            // Mission 3: Đua xe bấm giờ
            string m3Path = $"{dir}/Mission_03_Speed_TruongTien.asset";
            MissionData m3 = AssetDatabase.LoadAssetAtPath<MissionData>(m3Path);
            if (m3 == null)
            {
                m3 = ScriptableObject.CreateInstance<MissionData>();
                m3.Initialize("MIS_03", "Thử Thách Tốc Độ Cố Đô",
                    "Lái xe đạt tốc độ cao vượt qua 2 giao lộ trước khi hết thời gian.",
                    MissionType.TimeTrial, 100000, 45f,
                    new Vector3(-25.0f, 0.35f, -3.5f), new Vector3(25.0f, 0.35f, -3.5f), 4.5f);
                AssetDatabase.CreateAsset(m3, m3Path);
            }
            missions.Add(m3);

            // Mission 4: Tuần tra toàn bộ bản đồ 500m
            string m4Path = $"{dir}/Mission_04_Patrol_City500m.asset";
            MissionData m4 = AssetDatabase.LoadAssetAtPath<MissionData>(m4Path);
            if (m4 == null)
            {
                m4 = ScriptableObject.CreateInstance<MissionData>();
                m4.Initialize("MIS_04", "Tuần Tra Đô Thị 500m",
                    "Tuần tra từ ngã tư trung tâm đến chốt kiểm soát phía Đông bản đồ 500m.",
                    MissionType.Checkpoint, 150000, 150f,
                    new Vector3(0.0f, 0.25f, 0.0f), new Vector3(180.0f, 0.25f, 0.0f), 5.0f);
                AssetDatabase.CreateAsset(m4, m4Path);
            }
            missions.Add(m4);

            AssetDatabase.SaveAssets();
            return missions;
        }

        private static void SetupGlobalPostProcessingVolume()
        {
            GameObject volumeObj = new GameObject("Global_PostProcessing_Volume");
            Volume volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1.0f;

            string profilePath = "Assets/_Project/Settings/Hue_PostProcessing_Profile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile != null) volume.sharedProfile = profile;
        }

        private static GameObject CreatePlayerObject(Vector3 spawnPos)
        {
            GameObject player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = spawnPos;

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.88f, 0f);
            cc.stepOffset = 0.3f;

            string charFbxPath = "Assets/_Project/Models/Character_Player.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(charFbxPath) == null)
            {
                charFbxPath = "Assets/_Project/Models/Character_Humanoid_Rigged.fbx";
            }
            GameObject charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(charFbxPath);
            if (charPrefab != null)
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(charPrefab);
                visual.name = "Character_Visual";
                visual.transform.SetParent(player.transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                Animator anim = visual.GetComponent<Animator>();
                if (anim == null) anim = visual.AddComponent<Animator>();

                RuntimeAnimatorController animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Animations/Player_AnimatorController.controller");
                if (animController != null) anim.runtimeAnimatorController = animController;

                Avatar avatar = null;
                Animator prefabAnim = charPrefab.GetComponent<Animator>();
                if (prefabAnim != null && prefabAnim.avatar != null && prefabAnim.avatar.isValid) avatar = prefabAnim.avatar;
                if (avatar == null)
                {
                    foreach (var a in AssetDatabase.LoadAllAssetsAtPath(charFbxPath))
                    {
                        if (a is Avatar av && av.isValid) { avatar = av; break; }
                    }
                }
                if (avatar != null) anim.avatar = avatar;
            }

            player.AddComponent<PlayerInputHandler>();
            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerInteraction>();

            return player;
        }

        private static GameObject CreatePlayerTaxiObject(Vector3 spawnPos)
        {
            string taxiFbxPath = "Assets/_Project/Models/Car_Sedan_01.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(taxiFbxPath) == null)
            {
                taxiFbxPath = "Assets/_Project/Models/Vehicle_Taxi_Hue.fbx";
            }
            GameObject taxiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(taxiFbxPath);

            GameObject taxi = new GameObject("Player_Taxi_Vehicle");
            taxi.transform.position = spawnPos;

            Transform wheelFL_Mesh = null;
            Transform wheelFR_Mesh = null;
            Transform wheelRL_Mesh = null;
            Transform wheelRR_Mesh = null;

            if (taxiPrefab != null)
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(taxiPrefab);
                visual.name = "Taxi_Body_Visual";
                visual.transform.SetParent(taxi.transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                wheelFL_Mesh = visual.transform.Find("Wheel_FL");
                wheelFR_Mesh = visual.transform.Find("Wheel_FR");
                wheelRL_Mesh = visual.transform.Find("Wheel_RL");
                wheelRR_Mesh = visual.transform.Find("Wheel_RR");
            }

            Rigidbody rb = taxi.AddComponent<Rigidbody>();
            rb.mass = 1400f;
            rb.drag = 0.05f;
            rb.angularDrag = 0.8f;
            rb.centerOfMass = new Vector3(0f, -0.3f, 0f);

            BoxCollider boxCol = taxi.AddComponent<BoxCollider>();
            boxCol.size = new Vector3(1.85f, 1.35f, 4.4f);
            boxCol.center = new Vector3(0f, 0.75f, 0f);

            GameObject seatObj = new GameObject("VehicleSeat");
            seatObj.transform.SetParent(taxi.transform);
            seatObj.transform.localPosition = new Vector3(-0.5f, 0.45f, 0f);
            VehicleSeat seat = seatObj.AddComponent<VehicleSeat>();

            GameObject exitObj = new GameObject("ExitPoint");
            exitObj.transform.SetParent(taxi.transform);
            exitObj.transform.localPosition = new Vector3(-1.6f, 0.1f, 0f);

            CarController car = taxi.AddComponent<CarController>();

            WheelCollider wheelFL = CreateWheelCollider(taxi.transform, "WheelCollider_FL", new Vector3(-0.85f, 0.34f, 1.35f));
            WheelCollider wheelFR = CreateWheelCollider(taxi.transform, "WheelCollider_FR", new Vector3(0.85f, 0.34f, 1.35f));
            WheelCollider wheelRL = CreateWheelCollider(taxi.transform, "WheelCollider_RL", new Vector3(-0.85f, 0.34f, -1.35f));
            WheelCollider wheelRR = CreateWheelCollider(taxi.transform, "WheelCollider_RR", new Vector3(0.85f, 0.34f, -1.35f));

            SerializedObject carSo = new SerializedObject(car);
            SetWheelPair(carSo, "frontWheels", wheelFL, wheelFR, wheelFL_Mesh, wheelFR_Mesh);
            SetWheelPair(carSo, "rearWheels", wheelRL, wheelRR, wheelRL_Mesh, wheelRR_Mesh);
            carSo.ApplyModifiedProperties();

            return taxi;
        }

        private static WheelCollider CreateWheelCollider(Transform parent, string name, Vector3 pos)
        {
            GameObject wheelObj = new GameObject(name);
            wheelObj.transform.SetParent(parent);
            wheelObj.transform.localPosition = pos;
            WheelCollider wc = wheelObj.AddComponent<WheelCollider>();
            wc.radius = 0.34f;
            wc.suspensionDistance = 0.15f;
            wc.mass = 40f;
            return wc;
        }

        private static void SetWheelPair(SerializedObject so, string propertyName, WheelCollider leftCol, WheelCollider rightCol, Transform leftMesh, Transform rightMesh)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                var lw = prop.FindPropertyRelative("leftWheel") ?? prop.FindPropertyRelative("leftWheelCollider");
                if (lw != null) lw.objectReferenceValue = leftCol;

                var rw = prop.FindPropertyRelative("rightWheel") ?? prop.FindPropertyRelative("rightWheelCollider");
                if (rw != null) rw.objectReferenceValue = rightCol;

                var lm = prop.FindPropertyRelative("leftMesh") ?? prop.FindPropertyRelative("leftWheelMesh");
                if (lm != null) lm.objectReferenceValue = leftMesh;

                var rm = prop.FindPropertyRelative("rightMesh") ?? prop.FindPropertyRelative("rightWheelMesh");
                if (rm != null) rm.objectReferenceValue = rightMesh;
            }
        }

        private static Camera CreateCameraSetup(GameObject player, GameObject taxi)
        {
            GameObject camMgrObj = new GameObject("CameraManager");
            CameraManager camManager = camMgrObj.AddComponent<CameraManager>();

            GameObject walkCamObj = new GameObject("CM_WalkCamera");
            CinemachineVirtualCamera walkCam = walkCamObj.AddComponent<CinemachineVirtualCamera>();
            walkCam.Follow = player.transform;
            walkCam.LookAt = player.transform;
            walkCam.Priority = 10;

            CinemachineTransposer transposer = walkCam.AddCinemachineComponent<CinemachineTransposer>();
            transposer.m_FollowOffset = new Vector3(0f, 2.0f, -4.2f);
            CinemachineComposer composer = walkCam.AddCinemachineComponent<CinemachineComposer>();
            composer.m_TrackedObjectOffset = new Vector3(0f, 1.4f, 0f);

            GameObject driveCamObj = new GameObject("CM_DriveCamera");
            CinemachineVirtualCamera driveCam = driveCamObj.AddComponent<CinemachineVirtualCamera>();
            driveCam.Follow = taxi.transform;
            driveCam.LookAt = taxi.transform;
            driveCam.Priority = 5;

            CinemachineTransposer driveTrans = driveCam.AddCinemachineComponent<CinemachineTransposer>();
            driveTrans.m_FollowOffset = new Vector3(0f, 3.2f, -6.5f);
            CinemachineComposer driveComp = driveCam.AddCinemachineComponent<CinemachineComposer>();
            driveComp.m_TrackedObjectOffset = new Vector3(0f, 1.0f, 0f);

            SerializedObject camSo = new SerializedObject(camManager);
            SerializedProperty pCam = camSo.FindProperty("playerCamera") ?? camSo.FindProperty("walkCamera");
            if (pCam != null) pCam.objectReferenceValue = walkCam;
            SerializedProperty vCam = camSo.FindProperty("vehicleCamera") ?? camSo.FindProperty("driveCamera");
            if (vCam != null) vCam.objectReferenceValue = driveCam;
            camSo.ApplyModifiedProperties();

            GameObject mainCamObj = new GameObject("Main Camera");
            mainCamObj.tag = "MainCamera";
            Camera cam = mainCamObj.AddComponent<Camera>();
            mainCamObj.AddComponent<CinemachineBrain>();

            UniversalAdditionalCameraData camData = mainCamObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            return cam;
        }

        private static TrafficLightController SetupTrafficLights()
        {
            GameObject lightsObj = new GameObject("TrafficLightController_Center");
            TrafficLightController controller = lightsObj.AddComponent<TrafficLightController>();

            Vector3[] postPositions = new Vector3[]
            {
                new Vector3(7.5f, 0.15f, 7.5f),
                new Vector3(-7.5f, 0.15f, 7.5f),
                new Vector3(-7.5f, 0.15f, -7.5f),
                new Vector3(7.5f, 0.15f, -7.5f)
            };

            float[] postRotations = new float[] { 225f, 135f, 45f, 315f };

            List<Renderer> nsRed = new List<Renderer>();
            List<Renderer> nsYellow = new List<Renderer>();
            List<Renderer> nsGreen = new List<Renderer>();

            List<Renderer> ewRed = new List<Renderer>();
            List<Renderer> ewYellow = new List<Renderer>();
            List<Renderer> ewGreen = new List<Renderer>();

            for (int i = 0; i < postPositions.Length; i++)
            {
                CreateTrafficLightPost($"TrafficLight_Post_{i + 1}", postPositions[i], postRotations[i], lightsObj.transform,
                    out Renderer redL, out Renderer yellowL, out Renderer greenL);

                if (i == 0 || i == 2)
                {
                    nsRed.Add(redL);
                    nsYellow.Add(yellowL);
                    nsGreen.Add(greenL);
                }
                else
                {
                    ewRed.Add(redL);
                    ewYellow.Add(yellowL);
                    ewGreen.Add(greenL);
                }
            }

            controller.AssignLenses(nsRed.ToArray(), nsYellow.ToArray(), nsGreen.ToArray(),
                                   ewRed.ToArray(), ewYellow.ToArray(), ewGreen.ToArray());

            return controller;
        }

        private static GameObject CreateTrafficLightPost(string name, Vector3 pos, float yRot, Transform parent,
            out Renderer redLens, out Renderer yellowLens, out Renderer greenLens)
        {
            GameObject postRoot = new GameObject(name);
            postRoot.transform.SetParent(parent);
            postRoot.transform.position = pos;
            postRoot.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Steel_Pole";
            pole.transform.SetParent(postRoot.transform);
            pole.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            pole.transform.localScale = new Vector3(0.18f, 2.2f, 0.18f);

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Signal_Box";
            box.transform.SetParent(postRoot.transform);
            box.transform.localPosition = new Vector3(0f, 4.0f, 0.25f);
            box.transform.localScale = new Vector3(0.45f, 1.35f, 0.35f);

            GameObject red = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            red.name = "Lens_Red";
            red.transform.SetParent(box.transform);
            red.transform.localPosition = new Vector3(0f, 0.33f, 0.52f);
            red.transform.localScale = new Vector3(0.48f, 0.48f, 0.25f);
            redLens = red.GetComponent<Renderer>();

            GameObject yellow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            yellow.name = "Lens_Yellow";
            yellow.transform.SetParent(box.transform);
            yellow.transform.localPosition = new Vector3(0f, 0.0f, 0.52f);
            yellow.transform.localScale = new Vector3(0.48f, 0.48f, 0.25f);
            yellowLens = yellow.GetComponent<Renderer>();

            GameObject green = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            green.name = "Lens_Green";
            green.transform.SetParent(box.transform);
            green.transform.localPosition = new Vector3(0f, -0.33f, 0.52f);
            green.transform.localScale = new Vector3(0.48f, 0.48f, 0.25f);
            greenLens = green.GetComponent<Renderer>();

            return postRoot;
        }

        private static void SetupTrafficPool(List<TrafficWaypoint> startPoints)
        {
            GameObject poolObj = new GameObject("TrafficPoolManager");
            TrafficPoolManager poolMgr = poolObj.AddComponent<TrafficPoolManager>();

            List<GameObject> vehiclePrefabs = new List<GameObject>();

            string carPath = "Assets/_Project/Models/Car_Sedan_01.fbx";
            GameObject carFbx = AssetDatabase.LoadAssetAtPath<GameObject>(carPath);
            if (carFbx != null)
            {
                GameObject sedanTemplate = new GameObject("Template_Sedan_AI");
                sedanTemplate.transform.SetParent(poolObj.transform);
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(carFbx);
                visual.transform.SetParent(sedanTemplate.transform);
                visual.transform.localPosition = Vector3.zero;

                BoxCollider bc = sedanTemplate.AddComponent<BoxCollider>();
                bc.size = new Vector3(1.9f, 1.4f, 4.4f);
                bc.center = new Vector3(0f, 0.7f, 0f);
                sedanTemplate.AddComponent<Rigidbody>();
                sedanTemplate.AddComponent<TrafficVehicleAI>();
                sedanTemplate.SetActive(false);
                vehiclePrefabs.Add(sedanTemplate);
            }

            string scooterPath = "Assets/_Project/Models/Vehicle_Scooter.fbx";
            GameObject scooterFbx = AssetDatabase.LoadAssetAtPath<GameObject>(scooterPath);
            if (scooterFbx != null)
            {
                GameObject scooterTemplate = new GameObject("Template_Scooter_AI");
                scooterTemplate.transform.SetParent(poolObj.transform);
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(scooterFbx);
                visual.transform.SetParent(scooterTemplate.transform);
                visual.transform.localPosition = Vector3.zero;

                BoxCollider bc = scooterTemplate.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.6f, 1.1f, 1.8f);
                bc.center = new Vector3(0f, 0.55f, 0f);
                scooterTemplate.AddComponent<Rigidbody>();
                scooterTemplate.AddComponent<TrafficVehicleAI>();
                scooterTemplate.SetActive(false);
                vehiclePrefabs.Add(scooterTemplate);
            }

            poolMgr.InitializeWithPrefabs(vehiclePrefabs.ToArray(), startPoints, poolSize: 10);
        }

        private static void SetupNPCCrowd(GameObject player)
        {
            GameObject poolObj = new GameObject("NPCPoolManager");
            NPCPoolManager poolMgr = poolObj.AddComponent<NPCPoolManager>();

            string charFbxPath = "Assets/_Project/Models/Character_Player.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(charFbxPath) == null)
            {
                charFbxPath = "Assets/_Project/Models/Character_Humanoid_Rigged.fbx";
            }
            GameObject charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(charFbxPath);

            Color[] shirtColors = new Color[]
            {
                new Color(0.85f, 0.2f, 0.2f),
                new Color(0.2f, 0.6f, 0.85f),
                new Color(0.9f, 0.85f, 0.2f),
                new Color(0.3f, 0.75f, 0.3f),
                new Color(0.85f, 0.85f, 0.85f)
            };

            GameObject[] npcPrefabs = new GameObject[shirtColors.Length];

            for (int i = 0; i < shirtColors.Length; i++)
            {
                GameObject npc = new GameObject($"NPC_Template_{i}");
                npc.transform.SetParent(poolObj.transform);

                if (charPrefab != null)
                {
                    GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(charPrefab);
                    visual.transform.SetParent(npc.transform);
                    visual.transform.localPosition = Vector3.zero;

                    Animator anim = visual.GetComponent<Animator>();
                    if (anim == null) anim = visual.AddComponent<Animator>();

                    RuntimeAnimatorController animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Animations/Player_AnimatorController.controller");
                    if (animController != null) anim.runtimeAnimatorController = animController;

                    Avatar avatar = null;
                    Animator prefabAnim = charPrefab.GetComponent<Animator>();
                    if (prefabAnim != null && prefabAnim.avatar != null && prefabAnim.avatar.isValid) avatar = prefabAnim.avatar;
                    if (avatar == null)
                    {
                        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(charFbxPath))
                        {
                            if (a is Avatar av && av.isValid) { avatar = av; break; }
                        }
                    }
                    if (avatar != null) anim.avatar = avatar;
                }

                NavMeshAgent nma = npc.AddComponent<NavMeshAgent>();
                nma.speed = 1.35f;
                nma.radius = 0.3f;
                nma.height = 1.8f;
                nma.stoppingDistance = 0.25f;

                npc.AddComponent<NPCController>();
                npc.SetActive(false);
                npcPrefabs[i] = npc;
            }

            SerializedObject poolSo = new SerializedObject(poolMgr);
            SerializedProperty prefabsProp = poolSo.FindProperty("npcPrefabs");
            if (prefabsProp != null)
            {
                prefabsProp.arraySize = npcPrefabs.Length;
                for (int i = 0; i < npcPrefabs.Length; i++)
                {
                    prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = npcPrefabs[i];
                }
            }
            var maxProp = poolSo.FindProperty("maxActiveNPCs") ?? poolSo.FindProperty("poolSize");
            if (maxProp != null) maxProp.intValue = 22;
            poolSo.ApplyModifiedProperties();
        }
    }
}
#endif
