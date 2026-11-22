#if UNITY_EDITOR
using Cinemachine;
using HueCity.Camera;
using HueCity.NPC;
using HueCity.Player;
using HueCity.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace HueCity.EditorTools
{
    public static class Phase3SceneBuilder
    {
        [MenuItem("HueCity/Build Phase 3 Vertical Slice Scene", false, 30)]
        public static void BuildScene()
        {
            // 1. Tạo scene mới
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Thiết lập Ánh sáng Điện ảnh & Nắng vàng Cố Đô (Golden Hour Lighting)
            GameObject lightObj = new GameObject("Directional Light - Hue Sun");
            Light sunLight = lightObj.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1.0f, 0.93f, 0.82f); // Ánh nắng vàng ấm đặc trưng miền Trung
            sunLight.intensity = 1.35f;
            sunLight.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(42f, -38f, 0f);

            // Thêm Ambient Light ấm áp
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.45f);

            // 3. Hệ thống Quản lý Giác quan thế giới
            GameObject stimulusObj = new GameObject("WorldStimulusManager");
            stimulusObj.AddComponent<WorldStimulusManager>();

            // 4. Đưa Model Lát cắt dọc chi tiết (Ngã tư, Nhà ống, Biệt thự Pháp, Đèn, Cây xanh) vào Scene
            string detailedFbxPath = "Assets/_Project/Models/Hue_Vertical_Slice_Detailed.fbx";
            GameObject detailedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(detailedFbxPath);

            GameObject intersectionInstance = null;
            if (detailedPrefab != null)
            {
                intersectionInstance = (GameObject)PrefabUtility.InstantiatePrefab(detailedPrefab);
                intersectionInstance.name = "Hue_Intersection_VerticalSlice";
                intersectionInstance.transform.position = Vector3.zero;

                // Gắn MeshCollider và thiết lập Static cho NavMesh
                SetupCollidersAndNavigation(intersectionInstance);
            }
            else
            {
                Debug.LogWarning($"[HueCity] Chưa tìm thấy model tại {detailedFbxPath}");
            }

            // 5. Bake NavMesh tự động cho toàn bộ vỉa hè, vạch đi bộ và lòng đường ngã tư
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            Debug.Log("[HueCity] Đã bake xong NavMesh cho Ngã tư Lát cắt dọc!");

            // 6. Tạo Nhân vật (Player) đi bộ trên vỉa hè
            GameObject player = CreatePlayerObject(new Vector3(13f, 1.2f, 13f));
            player.tag = "Player";

            // 7. Tạo Xe Taxi Huế (Xanh SM) dừng trước vạch đèn đỏ
            GameObject taxi = CreateTaxiObject(new Vector3(3.5f, 0.5f, 18f));

            // 8. Đặt các phương tiện văn hóa và quán cà phê vỉa hè
            SpawnStreetCultureProps();

            // 9. Tạo Đám đông NPC (18 NPC đi dạo, qua vạch đi bộ, ngồi quán nước)
            SetupNPCCrowd(player);

            // 10. Thiết lập Cinemachine Camera
            CreateCameraSetup(player, taxi);

            // 11. Lưu Scene vào Assets/_Project/Scenes/Phase3_VerticalSlice.unity
            string scenePath = "Assets/_Project/Scenes/Phase3_VerticalSlice.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>[HueCity]</color> Đã tạo thành công Scene Lát cắt dọc chi tiết tại: {scenePath}");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("HueCity - Giai đoạn 3: Vertical Slice", 
                    "Đã hoàn thành Lát cắt dọc 1 ngã tư biểu tượng TP. Huế!\n\n" +
                    "• Đường sá chân thực: Nhựa đường sẫm, vạch kẻ đường, vạch Zebra người đi bộ, bó vỉa đá granite.\n" +
                    "• Kiến trúc thực tế: Nhà ống hiện đại (cửa cuốn, ban công kính, bồn inox, điều hòa), Biệt thự Pháp vôi vàng ngói đỏ, Khách sạn góc phố sang trọng, Quán cà phê mái hiên bạt.\n" +
                    "• Đèn giao thông, đèn cổ điển ven sông, bồn cây xanh bóng mát.\n" +
                    "• Bấm Play để trải nghiệm!", "Playtest Ngay");
            }
        }

        private static void SetupCollidersAndNavigation(GameObject root)
        {
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in meshFilters)
            {
                string objName = mf.gameObject.name;
                
                // Gắn MeshCollider cho đường, vỉa hè, nhà cửa và bồn cây
                if (objName.Contains("Road") || objName.Contains("Sidewalk") || objName.Contains("Building") || objName.Contains("Planter"))
                {
                    MeshCollider mc = mf.gameObject.GetComponent<MeshCollider>();
                    if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;

                    // Đánh dấu Static cho NavMesh
                    GameObjectUtility.SetStaticEditorFlags(mf.gameObject, StaticEditorFlags.NavigationStatic);
                }
            }
        }

        private static GameObject CreatePlayerObject(Vector3 spawnPos)
        {
            GameObject player = new GameObject("Player_Character");
            player.transform.position = spawnPos;

            string charFbxPath = "Assets/_Project/Models/Character_Humanoid_Rigged.fbx";
            GameObject charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(charFbxPath);

            if (charPrefab != null)
            {
                GameObject charInstance = (GameObject)PrefabUtility.InstantiatePrefab(charPrefab);
                charInstance.name = "Visual_Humanoid";
                charInstance.transform.SetParent(player.transform);
                charInstance.transform.localPosition = Vector3.zero;
                charInstance.transform.localRotation = Quaternion.identity;
            }
            else
            {
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "Visual";
                capsule.transform.SetParent(player.transform);
                capsule.transform.localPosition = new Vector3(0f, 1f, 0f);
                Object.DestroyImmediate(capsule.GetComponent<Collider>());
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

        private static GameObject CreateTaxiObject(Vector3 spawnPos)
        {
            string taxiFbxPath = "Assets/_Project/Models/Vehicle_Taxi_Hue.fbx";
            GameObject taxiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(taxiFbxPath);

            GameObject car;
            if (taxiPrefab != null)
            {
                car = (GameObject)PrefabUtility.InstantiatePrefab(taxiPrefab);
                car.name = "Vehicle_Taxi_Hue";
                car.transform.position = spawnPos;
                car.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Hướng vào ngã tư
            }
            else
            {
                car = new GameObject("Car_Fallback");
                car.transform.position = spawnPos;
            }

            Rigidbody rb = car.GetComponent<Rigidbody>();
            if (rb == null) rb = car.AddComponent<Rigidbody>();
            rb.mass = 1450f;
            rb.drag = 0.05f;
            rb.angularDrag = 1.6f;

            BoxCollider col = car.GetComponent<BoxCollider>();
            if (col == null) col = car.AddComponent<BoxCollider>();
            col.size = new Vector3(1.85f, 0.9f, 4.4f);
            col.center = new Vector3(0f, 0.65f, 0f);

            GameObject exitPt = new GameObject("DriverExitPoint");
            exitPt.transform.SetParent(car.transform);
            exitPt.transform.localPosition = new Vector3(-2.2f, 0f, 0f);

            float trackHalf = 0.85f;
            float baseHalf = 1.35f;
            WheelCollider flCol = CreateWheelCollider(car.transform, "WheelCol_FL", new Vector3(-trackHalf, 0.34f, baseHalf));
            WheelCollider frCol = CreateWheelCollider(car.transform, "WheelCol_FR", new Vector3(trackHalf, 0.34f, baseHalf));
            WheelCollider rlCol = CreateWheelCollider(car.transform, "WheelCol_RL", new Vector3(-trackHalf, 0.34f, -baseHalf));
            WheelCollider rrCol = CreateWheelCollider(car.transform, "WheelCol_RR", new Vector3(trackHalf, 0.34f, -baseHalf));

            Transform flMesh = car.transform.Find("Wheel_FL");
            Transform frMesh = car.transform.Find("Wheel_FR");
            Transform rlMesh = car.transform.Find("Wheel_RL");
            Transform rrMesh = car.transform.Find("Wheel_RR");

            CarController carController = car.AddComponent<CarController>();
            VehicleSeat seat = car.AddComponent<VehicleSeat>();

            SerializedObject carSo = new SerializedObject(carController);
            SetWheelPair(carSo, "frontWheels", flCol, frCol, flMesh, frMesh);
            SetWheelPair(carSo, "rearWheels", rlCol, rrCol, rlMesh, rrMesh);
            carSo.ApplyModifiedProperties();

            SerializedObject seatSo = new SerializedObject(seat);
            seatSo.FindProperty("exitPoint").objectReferenceValue = exitPt.transform;
            seatSo.ApplyModifiedProperties();

            return car;
        }

        private static void SpawnStreetCultureProps()
        {
            // Xe máy Scooter dựng chân chống trước nhà ống hiện đại
            string scooterPath = "Assets/_Project/Models/Vehicle_Scooter.fbx";
            GameObject scooterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(scooterPath);
            if (scooterPrefab != null)
            {
                GameObject scooter = (GameObject)PrefabUtility.InstantiatePrefab(scooterPrefab);
                scooter.name = "Street_Scooter_Parked";
                scooter.transform.position = new Vector3(12.5f, 0.25f, 5.0f);
                scooter.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
                BoxCollider bc = scooter.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.6f, 1.1f, 1.8f);
                bc.center = new Vector3(0f, 0.55f, 0f);
            }

            // Xích lô Huế đậu trước sảnh khách sạn góc phố
            string cycloPath = "Assets/_Project/Models/Vehicle_XichLo.fbx";
            GameObject cycloPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cycloPath);
            if (cycloPrefab != null)
            {
                GameObject cyclo = (GameObject)PrefabUtility.InstantiatePrefab(cycloPrefab);
                cyclo.name = "Street_XichLo_Hotel";
                cyclo.transform.position = new Vector3(-13.0f, 0.25f, 6.0f);
                cyclo.transform.rotation = Quaternion.Euler(0f, -50f, 0f);
                BoxCollider bc = cyclo.AddComponent<BoxCollider>();
                bc.size = new Vector3(1.1f, 1.4f, 2.2f);
                bc.center = new Vector3(0f, 0.7f, 0f);
            }

            // Quán cà phê cóc vỉa hè với bàn ghế nhựa đặt dưới mái hiên bạt
            string chairsPath = "Assets/_Project/Models/Props_Street_Chairs.fbx";
            GameObject chairsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(chairsPath);
            if (chairsPrefab != null)
            {
                GameObject chairs = (GameObject)PrefabUtility.InstantiatePrefab(chairsPrefab);
                chairs.name = "Street_Cafe_Awning_Chairs";
                chairs.transform.position = new Vector3(14.0f, 0.25f, -14.5f);
                BoxCollider bc = chairs.AddComponent<BoxCollider>();
                bc.size = new Vector3(1.4f, 0.6f, 1.4f);
                bc.center = new Vector3(0f, 0.3f, 0f);

                // Gắn ActivityPoint cho từng chiếc ghế nhựa để NPC ngồi uống cà phê
                foreach (Transform child in chairs.transform)
                {
                    if (child.name.Contains("Chair"))
                    {
                        child.gameObject.AddComponent<ActivityPoint>();
                    }
                }
            }
        }

        private static void SetupNPCCrowd(GameObject player)
        {
            GameObject poolObj = new GameObject("NPCPoolManager");
            NPCPoolManager poolMgr = poolObj.AddComponent<NPCPoolManager>();

            string charFbxPath = "Assets/_Project/Models/Character_Humanoid_Rigged.fbx";
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
                }
                else
                {
                    GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    capsule.transform.SetParent(npc.transform);
                    capsule.transform.localPosition = new Vector3(0f, 1f, 0f);
                    Object.DestroyImmediate(capsule.GetComponent<Collider>());
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
            prefabsProp.arraySize = npcPrefabs.Length;
            for (int i = 0; i < npcPrefabs.Length; i++)
            {
                prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = npcPrefabs[i];
            }
            poolSo.FindProperty("maxActiveNPCs").intValue = 18;
            poolSo.ApplyModifiedProperties();
        }

        private static WheelCollider CreateWheelCollider(Transform parent, string name, Vector3 localPos)
        {
            GameObject wheelObj = new GameObject(name);
            wheelObj.transform.SetParent(parent);
            wheelObj.transform.localPosition = localPos;

            WheelCollider col = wheelObj.AddComponent<WheelCollider>();
            col.radius = 0.38f;
            col.suspensionDistance = 0.22f;

            JointSpring spring = col.suspensionSpring;
            spring.spring = 28000f;
            spring.damper = 3200f;
            spring.targetPosition = 0.5f;
            col.suspensionSpring = spring;

            WheelFrictionCurve forward = col.forwardFriction;
            forward.stiffness = 1.5f;
            col.forwardFriction = forward;

            WheelFrictionCurve sideways = col.sidewaysFriction;
            sideways.stiffness = 1.8f;
            col.sidewaysFriction = sideways;

            return col;
        }

        private static void SetWheelPair(SerializedObject so, string propertyName, WheelCollider lCol, WheelCollider rCol, Transform lMesh, Transform rMesh)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.FindPropertyRelative("leftWheel").objectReferenceValue = lCol;
                prop.FindPropertyRelative("rightWheel").objectReferenceValue = rCol;
                prop.FindPropertyRelative("leftMesh").objectReferenceValue = lMesh;
                prop.FindPropertyRelative("rightMesh").objectReferenceValue = rMesh;
            }
        }

        private static void CreateCameraSetup(GameObject player, GameObject car)
        {
            GameObject mainCamObj = new GameObject("Main Camera");
            mainCamObj.tag = "MainCamera";
            mainCamObj.transform.position = new Vector3(13f, 4f, 8f);
            mainCamObj.AddComponent<UnityEngine.Camera>();
            mainCamObj.AddComponent<AudioListener>();
            CinemachineBrain brain = mainCamObj.AddComponent<CinemachineBrain>();
            brain.m_DefaultBlend.m_Time = 0.8f;

            GameObject playerVcamObj = new GameObject("CM_Player_Camera");
            CinemachineVirtualCamera playerVcam = playerVcamObj.AddComponent<CinemachineVirtualCamera>();
            playerVcam.Priority = 20;
            playerVcam.Follow = player.transform;
            playerVcam.LookAt = player.transform;

            CinemachineTransposer transposer = playerVcam.AddCinemachineComponent<CinemachineTransposer>();
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
            transposer.m_FollowOffset = new Vector3(0f, 2.5f, -4.5f);

            CinemachineComposer composer = playerVcam.AddCinemachineComponent<CinemachineComposer>();
            composer.m_TrackedObjectOffset = new Vector3(0f, 1.2f, 0f);

            GameObject vehicleVcamObj = new GameObject("CM_Vehicle_Camera");
            CinemachineVirtualCamera vehicleVcam = vehicleVcamObj.AddComponent<CinemachineVirtualCamera>();
            vehicleVcam.Priority = 10;
            vehicleVcam.Follow = car.transform;
            vehicleVcam.LookAt = car.transform;

            CinemachineTransposer carTransposer = vehicleVcam.AddCinemachineComponent<CinemachineTransposer>();
            carTransposer.m_BindingMode = CinemachineTransposer.BindingMode.SimpleFollowWithWorldUp;
            carTransposer.m_FollowOffset = new Vector3(0f, 3.2f, -6.8f);

            CinemachineComposer carComposer = vehicleVcam.AddCinemachineComponent<CinemachineComposer>();
            carComposer.m_TrackedObjectOffset = new Vector3(0f, 1f, 1f);

            GameObject camMgrObj = new GameObject("CameraManager");
            CameraManager camMgr = camMgrObj.AddComponent<CameraManager>();

            SerializedObject camSo = new SerializedObject(camMgr);
            camSo.FindProperty("playerCamera").objectReferenceValue = playerVcam;
            camSo.FindProperty("vehicleCamera").objectReferenceValue = vehicleVcam;
            camSo.ApplyModifiedProperties();
        }
    }
}
#endif
