#if UNITY_EDITOR
#pragma warning disable CS0618
using System.Collections.Generic;
using System.IO;
using Cinemachine;
using HueCity.Camera;
using HueCity.Core;
using HueCity.NPC;
using HueCity.Player;
using HueCity.Traffic;
using HueCity.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HueCity.EditorTools
{
    /// <summary>
    /// Trình dựng Scene Giai đoạn 4: Thổi hồn thành phố (Phase 4 Living City Scene Builder).
    /// Tích hợp đầy đủ:
    /// - Hệ thống Đèn giao thông 4 ngã rẽ (TrafficLightController) với đèn tín hiệu Xanh - Vàng - Đỏ.
    /// - Mạng lưới Waypoint 4 trục lưu thông (Bắc, Nam, Đông, Tây) tuân thủ đèn đỏ và rẽ nhánh.
    /// - Hệ thống xe cộ tự hành (TrafficVehicleAI) có cảm biến chống đâm đụng và còi xe.
    /// - Object Pool quản lý xe giao thông (TrafficPoolManager).
    /// - Đám đông NPC 18 người với FSM 5 trạng thái (Wander, Activity, Alert, Flee, Confront).
    /// </summary>
    public static class Phase4SceneBuilder
    {
        [InitializeOnLoadMethod]
        private static void AutoRegisterSceneBuild()
        {
            EditorApplication.delayCall += () =>
            {
                string markerPath = "Temp/Phase4_LivingCity_Built.marker";
                if (!File.Exists(markerPath))
                {
                    File.WriteAllText(markerPath, "built");
                    Debug.Log("[HueCity Phase 4] Tự động khởi tạo Scene Giai đoạn 4: Thổi hồn thành phố (Living City)...");
                    BuildScene(true);
                }
            };
        }

        [MenuItem("HueCity/Build Phase 4 Living City Scene", false, 40)]
        public static void BuildSceneMenu()
        {
            BuildScene(false);
        }

        public static void BuildScene(bool silent)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[HueCity Phase 4] Không thể build scene khi đang trong Play Mode. Vui lòng dừng Play Mode.");
                return;
            }

            // 0. Đảm bảo toàn bộ URP Asset, Volume Profile, Skybox & PBR Materials đã sẵn sàng
            RealismOverhaulSetup.EnsureFullSetup();
            LocomotionClipGenerator.EnsureClipsAndController();

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

            // 3. Global Volume Post-Processing (ACES, Bloom, SSAO)
            SetupGlobalPostProcessingVolume();

            // 4. WorldStimulusManager
            GameObject stimulusObj = new GameObject("WorldStimulusManager");
            stimulusObj.AddComponent<WorldStimulusManager>();

            // 5. Model Ngã tư đường và Vỉa hè chi tiết
            string detailedFbxPath = "Assets/_Project/Models/Hue_Vertical_Slice_Detailed.fbx";
            GameObject detailedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(detailedFbxPath);
            GameObject intersectionInstance = null;
            if (detailedPrefab != null)
            {
                intersectionInstance = (GameObject)PrefabUtility.InstantiatePrefab(detailedPrefab);
                intersectionInstance.name = "Hue_Intersection_VerticalSlice";
                intersectionInstance.transform.position = Vector3.zero;
                SetupCollidersAndNavigation(intersectionInstance);
            }

            // 6. Đường sá & Vỉa hè có chiều sâu PBR
            SetupPBRRoadAndSidewalkDepth(intersectionInstance);

            // 7. Nhà phố Modular 3 tầng tại 4 góc
            SpawnModularShophouses();

            // 8. Đạo cụ đường phố (Quán nước vỉa hè, Ghế nhựa, Xích lô, Xe máy)
            SpawnStreetCultureProps();

            // 9. Bake NavMesh tự động cho người đi bộ
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            Debug.Log("[HueCity Phase 4] Đã bake thành công NavMesh cho Ngã tư và Vỉa hè!");

            // 10. Tạo Nhân vật chính (Player)
            Vector3 playerSpawn = new Vector3(13.0f, 0.25f, 13.0f);
            GameObject player = CreatePlayerObject(playerSpawn);

            // 11. Tạo Xe Taxi người chơi lái thử
            Vector3 taxiSpawn = new Vector3(3.5f, 0.35f, 16.0f);
            GameObject playerTaxi = CreatePlayerTaxiObject(taxiSpawn);

            // 12. Tạo Cinemachine Camera
            CreateCameraSetup(player, playerTaxi);

            // 13. Tạo Đèn giao thông ngã tư 4 hướng (TrafficLightController & Cột đèn)
            TrafficLightController trafficLights = SetupTrafficLights();

            // 14. Tạo Mạng lưới Waypoints giao thông (4 trục Bắc - Nam, Đông - Tây)
            List<TrafficWaypoint> startWaypoints = SetupTrafficWaypoints();

            // 15. Tạo Trình quản lý Xe giao thông (TrafficPoolManager)
            SetupTrafficPool(startWaypoints);

            // 16. Tạo Đám đông NPC (18 NPC với FSM 5 trạng thái và Animator)
            SetupNPCCrowd(player);

            // 17. Lưu Scene vào cả Phase 4 và cập nhật Phase 3
            string phase4Path = "Assets/_Project/Scenes/Phase4_LivingCity.unity";
            EditorSceneManager.SaveScene(newScene, phase4Path);

            string phase3Path = "Assets/_Project/Scenes/Phase3_VerticalSlice.unity";
            EditorSceneManager.SaveScene(newScene, phase3Path);

            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[HueCity Phase 4]</color> Đã hoàn tất và lưu Scene Đô thị sống động tại: {phase4Path}");
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

        private static void SetupCollidersAndNavigation(GameObject root)
        {
            if (root == null) return;
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                {
                    MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
                GameObjectUtility.SetStaticEditorFlags(mf.gameObject, StaticEditorFlags.NavigationStatic);
            }
        }

        private static void SetupPBRRoadAndSidewalkDepth(GameObject root)
        {
            Material roadMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Road_Asphalt.mat");
            Material sidewalkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Sidewalk_Concrete.mat");
            Material curbMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Curb_Stone.mat");
            Material whiteLineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_RoadMarking_White.mat");
            Material yellowLineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_RoadMarking_Yellow.mat");

            GameObject roadSystem = new GameObject("PBR_Road_And_Sidewalk_System");

            // Mặt đường nhựa Y = 0.0m
            GameObject asphalt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            asphalt.name = "PBR_Asphalt_Roadbed";
            asphalt.transform.SetParent(roadSystem.transform);
            asphalt.transform.position = new Vector3(0f, -0.05f, 0f);
            asphalt.transform.localScale = new Vector3(70.0f, 0.10f, 70.0f);
            if (roadMat != null) asphalt.GetComponent<Renderer>().sharedMaterial = roadMat;
            GameObjectUtility.SetStaticEditorFlags(asphalt, StaticEditorFlags.NavigationStatic);

            // Vỉa hè nâng cao Y = +0.15m tại 4 góc
            Vector3[] sidewalkCenters = new Vector3[]
            {
                new Vector3(21.0f, 0.075f, 21.0f),
                new Vector3(-21.0f, 0.075f, 21.0f),
                new Vector3(21.0f, 0.075f, -21.0f),
                new Vector3(-21.0f, 0.075f, -21.0f)
            };

            for (int i = 0; i < sidewalkCenters.Length; i++)
            {
                GameObject sw = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sw.name = $"PBR_Sidewalk_Corner_{i}";
                sw.transform.SetParent(roadSystem.transform);
                sw.transform.position = sidewalkCenters[i];
                sw.transform.localScale = new Vector3(28.0f, 0.15f, 28.0f);
                if (sidewalkMat != null) sw.GetComponent<Renderer>().sharedMaterial = sidewalkMat;
                GameObjectUtility.SetStaticEditorFlags(sw, StaticEditorFlags.NavigationStatic);

                CreateCurbBorders(roadSystem.transform, sidewalkCenters[i], curbMat);
            }

            CreateRoadMarkings(roadSystem.transform, whiteLineMat, yellowLineMat);
        }

        private static void CreateCurbBorders(Transform parent, Vector3 center, Material curbMat)
        {
            float signX = Mathf.Sign(center.x);
            float signZ = Mathf.Sign(center.z);

            GameObject curbX = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curbX.name = "Curb_Border_X";
            curbX.transform.SetParent(parent);
            curbX.transform.position = new Vector3(center.x, 0.08f, center.z - signZ * 14.1f);
            curbX.transform.localScale = new Vector3(28.0f, 0.16f, 0.25f);
            if (curbMat != null) curbX.GetComponent<Renderer>().sharedMaterial = curbMat;

            GameObject curbZ = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curbZ.name = "Curb_Border_Z";
            curbZ.transform.SetParent(parent);
            curbZ.transform.position = new Vector3(center.x - signX * 14.1f, 0.08f, center.z);
            curbZ.transform.localScale = new Vector3(0.25f, 0.16f, 28.0f);
            if (curbMat != null) curbZ.GetComponent<Renderer>().sharedMaterial = curbMat;
        }

        private static void CreateRoadMarkings(Transform parent, Material whiteMat, Material yellowMat)
        {
            GameObject markingsParent = new GameObject("PBR_Road_Markings_Y0005");
            markingsParent.transform.SetParent(parent);

            float markY = 0.005f;

            // Vạch đôi vàng phân làn trung tâm
            CreateCenterYellowLine(markingsParent.transform, new Vector3(0f, markY, 21.0f), 28.0f, false, yellowMat);
            CreateCenterYellowLine(markingsParent.transform, new Vector3(0f, markY, -21.0f), 28.0f, false, yellowMat);
            CreateCenterYellowLine(markingsParent.transform, new Vector3(21.0f, markY, 0f), 28.0f, true, yellowMat);
            CreateCenterYellowLine(markingsParent.transform, new Vector3(-21.0f, markY, 0f), 28.0f, true, yellowMat);

            // Vạch dừng Stop Line màu trắng trước ngã tư
            CreateStopLine(markingsParent.transform, new Vector3(0f, markY, 8.5f), 13.0f, true, whiteMat);
            CreateStopLine(markingsParent.transform, new Vector3(0f, markY, -8.5f), 13.0f, true, whiteMat);
            CreateStopLine(markingsParent.transform, new Vector3(8.5f, markY, 0f), 13.0f, false, whiteMat);
            CreateStopLine(markingsParent.transform, new Vector3(-8.5f, markY, 0f), 13.0f, false, whiteMat);

            // Vạch qua đường cho người đi bộ (Zebra Crossings)
            CreateZebraCrossing(markingsParent.transform, new Vector3(0f, markY, 10.5f), false, whiteMat);
            CreateZebraCrossing(markingsParent.transform, new Vector3(0f, markY, -10.5f), false, whiteMat);
            CreateZebraCrossing(markingsParent.transform, new Vector3(10.5f, markY, 0f), true, whiteMat);
            CreateZebraCrossing(markingsParent.transform, new Vector3(-10.5f, markY, 0f), true, whiteMat);
        }

        private static void CreateCenterYellowLine(Transform parent, Vector3 center, float length, bool isHorizontal, Material mat)
        {
            float spacing = 0.18f;
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

        private static void CreateStopLine(Transform parent, Vector3 center, float width, bool isHorizontal, Material mat)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Quad);
            line.name = "White_Stop_Line";
            line.transform.SetParent(parent);
            line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            line.transform.position = center;
            line.transform.localScale = isHorizontal ? new Vector3(width, 0.45f, 1f) : new Vector3(0.45f, width, 1f);

            Object.DestroyImmediate(line.GetComponent<Collider>());
            if (mat != null) line.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void CreateZebraCrossing(Transform parent, Vector3 center, bool isHorizontal, Material mat)
        {
            int stripeCount = 8;
            float stripeWidth = 0.50f;
            float stripeLength = 3.6f;
            float stripeGap = 0.50f;
            float totalSpan = stripeCount * (stripeWidth + stripeGap);
            float startOffset = -totalSpan * 0.5f + (stripeWidth + stripeGap) * 0.5f;

            for (int i = 0; i < stripeCount; i++)
            {
                GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Quad);
                stripe.name = $"Zebra_Stripe_{i}";
                stripe.transform.SetParent(parent);
                stripe.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                float offset = startOffset + i * (stripeWidth + stripeGap);
                if (isHorizontal)
                {
                    stripe.transform.position = new Vector3(center.x, center.y, center.z + offset);
                    stripe.transform.localScale = new Vector3(stripeLength, stripeWidth, 1f);
                }
                else
                {
                    stripe.transform.position = new Vector3(center.x + offset, center.y, center.z);
                    stripe.transform.localScale = new Vector3(stripeWidth, stripeLength, 1f);
                }

                Object.DestroyImmediate(stripe.GetComponent<Collider>());
                if (mat != null) stripe.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        private static void SpawnModularShophouses()
        {
            string houseFbxPath = "Assets/_Project/Models/House_Modular_01.fbx";
            GameObject housePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(houseFbxPath);
            if (housePrefab == null) return;

            GameObject blocksParent = new GameObject("Modular_Shophouses_Blocks");

            Vector3[] positions = new Vector3[]
            {
                new Vector3(14.0f, 0.15f, 18.0f),
                new Vector3(19.2f, 0.15f, 18.0f),
                new Vector3(24.4f, 0.15f, 18.0f),

                new Vector3(-14.0f, 0.15f, 18.0f),
                new Vector3(-19.2f, 0.15f, 18.0f),
                new Vector3(-24.4f, 0.15f, 18.0f),

                new Vector3(14.0f, 0.15f, -18.0f),
                new Vector3(19.2f, 0.15f, -18.0f),
                new Vector3(24.4f, 0.15f, -18.0f),
            };

            float[] rotations = new float[]
            {
                180f, 180f, 180f,
                180f, 180f, 180f,
                0f, 0f, 0f,
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject house = (GameObject)PrefabUtility.InstantiatePrefab(housePrefab);
                house.name = $"Shophouse_Block_{i + 1}";
                house.transform.SetParent(blocksParent.transform);
                house.transform.position = positions[i];
                house.transform.rotation = Quaternion.Euler(0f, rotations[i], 0f);

                MeshFilter[] mfs = house.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in mfs)
                {
                    if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    {
                        MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                    }
                    GameObjectUtility.SetStaticEditorFlags(mf.gameObject, StaticEditorFlags.NavigationStatic);
                }
            }
        }

        private static GameObject CreatePlayerObject(Vector3 spawnPos)
        {
            GameObject player = new GameObject("Player_Character");
            player.transform.position = spawnPos;
            player.tag = "Player";

            string charFbxPath = "Assets/_Project/Models/Character_Player.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(charFbxPath) == null)
            {
                charFbxPath = "Assets/_Project/Models/Character_Humanoid_Rigged.fbx";
            }
            GameObject charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(charFbxPath);

            if (charPrefab != null)
            {
                GameObject visualObj = (GameObject)PrefabUtility.InstantiatePrefab(charPrefab);
                visualObj.name = "Visual_Humanoid";
                visualObj.transform.SetParent(player.transform);
                visualObj.transform.localPosition = Vector3.zero;
                visualObj.transform.localRotation = Quaternion.identity;

                Animator animator = visualObj.GetComponent<Animator>();
                if (animator == null) animator = visualObj.AddComponent<Animator>();

                RuntimeAnimatorController animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Animations/Player_AnimatorController.controller");
                if (animController != null) animator.runtimeAnimatorController = animController;

                // Nạp Humanoid Avatar hợp lệ
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
                if (avatar != null) animator.avatar = avatar;
            }

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            player.AddComponent<PlayerInputHandler>();
            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerInteraction>();

            return player;
        }

        private static GameObject CreatePlayerTaxiObject(Vector3 spawnPos)
        {
            GameObject taxi = new GameObject("Taxi_Hue_XanhSM");
            taxi.transform.position = spawnPos;
            taxi.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            Rigidbody rb = taxi.AddComponent<Rigidbody>();
            rb.mass = 1650f;
            rb.drag = 0.05f;
            rb.angularDrag = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            BoxCollider bc = taxi.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.9f, 1.4f, 4.4f);
            bc.center = new Vector3(0f, 0.8f, 0f);

            CarController carController = taxi.AddComponent<CarController>();
            taxi.AddComponent<VehicleSeat>();

            string taxiFbxPath = "Assets/_Project/Models/Car_Sedan_01.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(taxiFbxPath) == null)
            {
                taxiFbxPath = "Assets/_Project/Models/Vehicle_Taxi_Hue.fbx";
            }
            GameObject taxiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(taxiFbxPath);

            Transform wheelFL_Mesh = null;
            Transform wheelFR_Mesh = null;
            Transform wheelRL_Mesh = null;
            Transform wheelRR_Mesh = null;

            if (taxiPrefab != null)
            {
                GameObject taxiVisual = (GameObject)PrefabUtility.InstantiatePrefab(taxiPrefab);
                taxiVisual.name = "Visual_Taxi";
                taxiVisual.transform.SetParent(taxi.transform);
                taxiVisual.transform.localPosition = Vector3.zero;
                taxiVisual.transform.localRotation = Quaternion.identity;

                wheelFL_Mesh = taxiVisual.transform.Find("Wheel_FL");
                wheelFR_Mesh = taxiVisual.transform.Find("Wheel_FR");
                wheelRL_Mesh = taxiVisual.transform.Find("Wheel_RL");
                wheelRR_Mesh = taxiVisual.transform.Find("Wheel_RR");
            }

            WheelCollider wheelFL = CreateWheelCollider(taxi.transform, "Col_Wheel_FL", new Vector3(-0.86f, 0.35f, 1.35f));
            WheelCollider wheelFR = CreateWheelCollider(taxi.transform, "Col_Wheel_FR", new Vector3(0.86f, 0.35f, 1.35f));
            WheelCollider wheelRL = CreateWheelCollider(taxi.transform, "Col_Wheel_RL", new Vector3(-0.86f, 0.35f, -1.35f));
            WheelCollider wheelRR = CreateWheelCollider(taxi.transform, "Col_Wheel_RR", new Vector3(0.86f, 0.35f, -1.35f));

            SerializedObject carSo = new SerializedObject(carController);
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

        private static void SpawnStreetCultureProps()
        {
            // Xe máy
            string scooterPath = "Assets/_Project/Models/Vehicle_Scooter.fbx";
            GameObject scooterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(scooterPath);
            if (scooterPrefab != null)
            {
                GameObject scooter = (GameObject)PrefabUtility.InstantiatePrefab(scooterPrefab);
                scooter.name = "Street_Scooter_Sidewalk";
                scooter.transform.position = new Vector3(10.5f, 0.25f, 12.0f);
                scooter.transform.rotation = Quaternion.Euler(0f, 75f, 0f);
            }

            // Xích lô Huế
            string cycloPath = "Assets/_Project/Models/Vehicle_XichLo.fbx";
            GameObject cycloPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cycloPath);
            if (cycloPrefab != null)
            {
                GameObject cyclo = (GameObject)PrefabUtility.InstantiatePrefab(cycloPrefab);
                cyclo.name = "Street_XichLo_Hotel";
                cyclo.transform.position = new Vector3(-13.0f, 0.25f, 6.0f);
                cyclo.transform.rotation = Quaternion.Euler(0f, -50f, 0f);
            }

            // Bàn ghế quán cà phê vỉa hè
            string chairsPath = "Assets/_Project/Models/Props_Street_Chairs.fbx";
            GameObject chairsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(chairsPath);
            if (chairsPrefab != null)
            {
                GameObject chairs = (GameObject)PrefabUtility.InstantiatePrefab(chairsPrefab);
                chairs.name = "Street_Cafe_Awning_Chairs";
                chairs.transform.position = new Vector3(14.0f, 0.25f, -14.5f);

                foreach (Transform child in chairs.transform)
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
        }

        private static void CreateCameraSetup(GameObject player, GameObject taxi)
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

            // Main Camera URP
            GameObject mainCamObj = new GameObject("Main Camera");
            mainCamObj.tag = "MainCamera";
            UnityEngine.Camera cam = mainCamObj.AddComponent<UnityEngine.Camera>();
            mainCamObj.AddComponent<CinemachineBrain>();

            UniversalAdditionalCameraData camData = mainCamObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
        }

        private static TrafficLightController SetupTrafficLights()
        {
            GameObject lightsObj = new GameObject("TrafficLightController");
            TrafficLightController controller = lightsObj.AddComponent<TrafficLightController>();

            // 4 Cột đèn giao thông tại 4 góc vỉa hè ngã tư
            Vector3[] postPositions = new Vector3[]
            {
                new Vector3(7.5f, 0.15f, 7.5f),    // Đông Bắc
                new Vector3(-7.5f, 0.15f, 7.5f),   // Tây Bắc
                new Vector3(-7.5f, 0.15f, -7.5f),  // Tây Nam
                new Vector3(7.5f, 0.15f, -7.5f)    // Đông Nam
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
                GameObject post = CreateTrafficLightPost($"TrafficLight_Post_{i + 1}", postPositions[i], postRotations[i], lightsObj.transform,
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

            // Cột thép
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Steel_Pole";
            pole.transform.SetParent(postRoot.transform);
            pole.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            pole.transform.localScale = new Vector3(0.18f, 2.2f, 0.18f);

            // Hộp đèn
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Signal_Box";
            box.transform.SetParent(postRoot.transform);
            box.transform.localPosition = new Vector3(0f, 4.0f, 0.25f);
            box.transform.localScale = new Vector3(0.45f, 1.35f, 0.35f);

            // Đèn Đỏ (trên cùng)
            GameObject red = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            red.name = "Lens_Red";
            red.transform.SetParent(box.transform);
            red.transform.localPosition = new Vector3(0f, 0.33f, 0.52f);
            red.transform.localScale = new Vector3(0.48f, 0.48f, 0.25f);
            redLens = red.GetComponent<Renderer>();

            // Đèn Vàng (giữa)
            GameObject yellow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            yellow.name = "Lens_Yellow";
            yellow.transform.SetParent(box.transform);
            yellow.transform.localPosition = new Vector3(0f, 0.0f, 0.52f);
            yellow.transform.localScale = new Vector3(0.48f, 0.48f, 0.25f);
            yellowLens = yellow.GetComponent<Renderer>();

            // Đèn Xanh (dưới cùng)
            GameObject green = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            green.name = "Lens_Green";
            green.transform.SetParent(box.transform);
            green.transform.localPosition = new Vector3(0f, -0.33f, 0.52f);
            green.transform.localScale = new Vector3(0.48f, 0.48f, 0.25f);
            greenLens = green.GetComponent<Renderer>();

            return postRoot;
        }

        private static List<TrafficWaypoint> SetupTrafficWaypoints()
        {
            GameObject trafficNetwork = new GameObject("Traffic_Waypoint_Network");
            List<TrafficWaypoint> startPoints = new List<TrafficWaypoint>();

            // 1. Tuyến Bắc -> Nam (North to South, làn X = -3.5m)
            TrafficWaypoint n_start = CreateWaypoint("WP_North_Entry", new Vector3(-3.5f, 0.2f, 32.0f), trafficNetwork.transform);
            TrafficWaypoint n_mid1  = CreateWaypoint("WP_North_Mid",   new Vector3(-3.5f, 0.2f, 16.0f), trafficNetwork.transform);
            TrafficWaypoint n_stop  = CreateWaypoint("WP_North_Stop",  new Vector3(-3.5f, 0.2f, 8.0f),  trafficNetwork.transform);
            n_stop.SetStopPoint(true);
            n_stop.SetDirection(TrafficDirection.NorthSouth);

            TrafficWaypoint n_center= CreateWaypoint("WP_Center_NS",   new Vector3(-3.5f, 0.2f, 0.0f),  trafficNetwork.transform);
            TrafficWaypoint n_exit1 = CreateWaypoint("WP_South_Mid",   new Vector3(-3.5f, 0.2f, -16.0f),trafficNetwork.transform);
            TrafficWaypoint n_exit2 = CreateWaypoint("WP_South_Exit",  new Vector3(-3.5f, 0.2f, -32.0f),trafficNetwork.transform);

            n_start.AddNextWaypoint(n_mid1);
            n_mid1.AddNextWaypoint(n_stop);
            n_stop.AddNextWaypoint(n_center);
            n_center.AddNextWaypoint(n_exit1);
            n_exit1.AddNextWaypoint(n_exit2);

            startPoints.Add(n_start);

            // 2. Tuyến Nam -> Bắc (South to North, làn X = +3.5m)
            TrafficWaypoint s_start = CreateWaypoint("WP_South_Entry", new Vector3(3.5f, 0.2f, -32.0f), trafficNetwork.transform);
            TrafficWaypoint s_mid1  = CreateWaypoint("WP_South_Mid",   new Vector3(3.5f, 0.2f, -16.0f), trafficNetwork.transform);
            TrafficWaypoint s_stop  = CreateWaypoint("WP_South_Stop",  new Vector3(3.5f, 0.2f, -8.0f),  trafficNetwork.transform);
            s_stop.SetStopPoint(true);
            s_stop.SetDirection(TrafficDirection.NorthSouth);

            TrafficWaypoint s_center= CreateWaypoint("WP_Center_SN",   new Vector3(3.5f, 0.2f, 0.0f),   trafficNetwork.transform);
            TrafficWaypoint s_exit1 = CreateWaypoint("WP_North_Mid",   new Vector3(3.5f, 0.2f, 16.0f),  trafficNetwork.transform);
            TrafficWaypoint s_exit2 = CreateWaypoint("WP_North_Exit",  new Vector3(3.5f, 0.2f, 32.0f),  trafficNetwork.transform);

            s_start.AddNextWaypoint(s_mid1);
            s_mid1.AddNextWaypoint(s_stop);
            s_stop.AddNextWaypoint(s_center);
            s_center.AddNextWaypoint(s_exit1);
            s_exit1.AddNextWaypoint(s_exit2);

            startPoints.Add(s_start);

            // 3. Tuyến Đông -> Tây (East to West, làn Z = +3.5m)
            TrafficWaypoint e_start = CreateWaypoint("WP_East_Entry",  new Vector3(32.0f, 0.2f, 3.5f),  trafficNetwork.transform);
            TrafficWaypoint e_mid1  = CreateWaypoint("WP_East_Mid",    new Vector3(16.0f, 0.2f, 3.5f),  trafficNetwork.transform);
            TrafficWaypoint e_stop  = CreateWaypoint("WP_East_Stop",   new Vector3(8.0f, 0.2f, 3.5f),   trafficNetwork.transform);
            e_stop.SetStopPoint(true);
            e_stop.SetDirection(TrafficDirection.EastWest);

            TrafficWaypoint e_center= CreateWaypoint("WP_Center_EW",   new Vector3(0.0f, 0.2f, 3.5f),   trafficNetwork.transform);
            TrafficWaypoint e_exit1 = CreateWaypoint("WP_West_Mid",    new Vector3(-16.0f, 0.2f, 3.5f), trafficNetwork.transform);
            TrafficWaypoint e_exit2 = CreateWaypoint("WP_West_Exit",   new Vector3(-32.0f, 0.2f, 3.5f), trafficNetwork.transform);

            e_start.AddNextWaypoint(e_mid1);
            e_mid1.AddNextWaypoint(e_stop);
            e_stop.AddNextWaypoint(e_center);
            e_center.AddNextWaypoint(e_exit1);
            e_exit1.AddNextWaypoint(e_exit2);

            startPoints.Add(e_start);

            // 4. Tuyến Tây -> Đông (West to East, làn Z = -3.5m)
            TrafficWaypoint w_start = CreateWaypoint("WP_West_Entry",  new Vector3(-32.0f, 0.2f, -3.5f),trafficNetwork.transform);
            TrafficWaypoint w_mid1  = CreateWaypoint("WP_West_Mid",    new Vector3(-16.0f, 0.2f, -3.5f),trafficNetwork.transform);
            TrafficWaypoint w_stop  = CreateWaypoint("WP_West_Stop",   new Vector3(-8.0f, 0.2f, -3.5f), trafficNetwork.transform);
            w_stop.SetStopPoint(true);
            w_stop.SetDirection(TrafficDirection.EastWest);

            TrafficWaypoint w_center= CreateWaypoint("WP_Center_WE",   new Vector3(0.0f, 0.2f, -3.5f),  trafficNetwork.transform);
            TrafficWaypoint w_exit1 = CreateWaypoint("WP_East_Mid",    new Vector3(16.0f, 0.2f, -3.5f), trafficNetwork.transform);
            TrafficWaypoint w_exit2 = CreateWaypoint("WP_East_Exit",   new Vector3(32.0f, 0.2f, -3.5f), trafficNetwork.transform);

            w_start.AddNextWaypoint(w_mid1);
            w_mid1.AddNextWaypoint(w_stop);
            w_stop.AddNextWaypoint(w_center);
            w_center.AddNextWaypoint(w_exit1);
            w_exit1.AddNextWaypoint(w_exit2);

            startPoints.Add(w_start);

            // Tạo các nhánh rẽ phải tự nhiên tại ngã tư
            n_center.AddNextWaypoint(w_exit1); // Bắc rẽ Tây
            s_center.AddNextWaypoint(e_exit1); // Nam rẽ Đông
            e_center.AddNextWaypoint(n_exit1); // Đông rẽ Bắc
            w_center.AddNextWaypoint(s_exit1); // Tây rẽ Nam

            return startPoints;
        }

        private static TrafficWaypoint CreateWaypoint(string name, Vector3 pos, Transform parent)
        {
            GameObject wpObj = new GameObject(name);
            wpObj.transform.SetParent(parent);
            wpObj.transform.position = pos;
            return wpObj.AddComponent<TrafficWaypoint>();
        }

        private static void SetupTrafficPool(List<TrafficWaypoint> startPoints)
        {
            GameObject poolObj = new GameObject("TrafficPoolManager");
            TrafficPoolManager poolMgr = poolObj.AddComponent<TrafficPoolManager>();

            // Danh sách các mẫu xe traffic
            List<GameObject> vehiclePrefabs = new List<GameObject>();

            // 1. Sedan Taxi
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

            // 2. Xe máy Scooter
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

            poolMgr.InitializeWithPrefabs(vehiclePrefabs.ToArray(), startPoints, poolSize: 8);
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
                new Color(0.85f, 0.2f, 0.2f),   // Đỏ
                new Color(0.2f, 0.6f, 0.85f),  // Xanh dương
                new Color(0.9f, 0.85f, 0.2f),  // Vàng
                new Color(0.3f, 0.75f, 0.3f),  // Xanh lá
                new Color(0.85f, 0.85f, 0.85f) // Trắng
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
            if (maxProp != null) maxProp.intValue = 18;
            poolSo.ApplyModifiedProperties();
        }
    }
}
#endif
