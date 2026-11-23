#if UNITY_EDITOR
#pragma warning disable CS0618
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
    public static class Phase2SceneBuilder
    {
        [MenuItem("HueCity/Build Phase 2 Map Blocking Scene", false, 20)]
        public static void BuildScene()
        {
            // 1. Tạo scene mới
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Thiết lập Ánh sáng cơ bản
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 3. Hệ thống Quản lý Giác quan thế giới (WorldStimulusManager)
            GameObject stimulusObj = new GameObject("WorldStimulusManager");
            stimulusObj.AddComponent<WorldStimulusManager>();

            // 4. Load và đưa Model Bản đồ TP. Huế vào Scene
            string fbxPath = "Assets/_Project/Models/Map_Hue_Greybox.fbx";
            GameObject mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

            GameObject mapInstance = null;
            if (mapPrefab != null)
            {
                mapInstance = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab);
                mapInstance.name = "Hue_Map_Blocking";
                mapInstance.transform.position = Vector3.zero;

                // Gắn MeshCollider và đánh dấu Static cho NavMesh
                SetupCollidersAndStatic(mapInstance);
            }
            else
            {
                Debug.LogWarning($"[HueCity] Chưa tìm thấy model tại {fbxPath}, đang tạo mặt đất mặc định.");
                GameObject fallbackGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallbackGround.transform.localScale = new Vector3(500f, 1f, 500f);
                fallbackGround.transform.position = new Vector3(0f, -0.5f, 0f);
                GameObjectUtility.SetStaticEditorFlags(fallbackGround, StaticEditorFlags.NavigationStatic);
            }

            // 5. Bake NavMesh tự động cho bản đồ
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            Debug.Log("[HueCity] Đã bake xong NavMesh cho vỉa hè và mạng lưới đường sá.");

            // 6. Tạo Nhân vật (Player) tại ngã ba Lê Lợi - Hùng Vương
            GameObject player = CreatePlayerObject(new Vector3(0f, 1.2f, 0f));
            player.tag = "Player";

            // 7. Tạo Xe hơi Taxi Huế (Xanh SM)
            GameObject car = CreateTaxiObject(new Vector3(4f, 0.5f, 6f));

            // 8. Đặt Xe máy, Xích lô và Bộ bàn ghế vỉa hè (gắn sẵn ActivityPoint)
            SpawnDecorativeProps();

            // 9. Tạo hệ thống NPC đám đông (18 NPC với FSM 5 trạng thái + Pool Manager)
            SetupNPCCrowd(player);

            // 10. Thiết lập Cinemachine Camera
            CreateCameraSetup(player, car);

            // 11. Lưu Scene vào Assets/_Project/Scenes/Phase2_MapBlocking.unity
            string scenePath = "Assets/_Project/Scenes/Phase2_MapBlocking.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>[HueCity]</color> Đã tạo thành công Scene Giai đoạn 2 kèm Đám đông NPC tại: {scenePath}");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("HueCity - Giai đoạn 2 & NPC Crowds", 
                    "Đã khởi tạo thành công Bản đồ TP. Huế + Đám đông NPC kiểu GTA!\n" +
                    "- Đường sá mở rộng +20% phục vụ lái xe tốc độ cao.\n" +
                    "- 18 NPC độc lập với FSM 5 trạng thái (Wander, Sit, Alert, Flee, Confront).\n" +
                    "- Thử bấm còi (phím H) khi lái xe hoặc đâm xe để quan sát phản ứng của NPC!\n" +
                    "- Vị trí: Chân cầu Trường Tiền / Ngã ba Lê Lợi & Hùng Vương.", "Bắt đầu Playtest");
            }
        }

        private static void SetupCollidersAndStatic(GameObject mapRoot)
        {
            MeshFilter[] meshFilters = mapRoot.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in meshFilters)
            {
                string objName = mf.gameObject.name;
                
                if (objName.Contains("Road") || objName.Contains("Sidewalk") || objName.Contains("Buildings"))
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

        private static void SpawnDecorativeProps()
        {
            // Xe máy (Scooter) đậu ở lề đường
            string scooterPath = "Assets/_Project/Models/Vehicle_Scooter.fbx";
            GameObject scooterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(scooterPath);
            if (scooterPrefab != null)
            {
                GameObject scooter = (GameObject)PrefabUtility.InstantiatePrefab(scooterPrefab);
                scooter.name = "Street_Scooter";
                scooter.transform.position = new Vector3(-3.2f, 0.25f, 4.0f);
                scooter.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
                BoxCollider bc = scooter.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.6f, 1.1f, 1.8f);
                bc.center = new Vector3(0f, 0.55f, 0f);
            }

            // Xích lô Huế đậu ven bờ sông
            string cycloPath = "Assets/_Project/Models/Vehicle_XichLo.fbx";
            GameObject cycloPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cycloPath);
            if (cycloPrefab != null)
            {
                GameObject cyclo = (GameObject)PrefabUtility.InstantiatePrefab(cycloPrefab);
                cyclo.name = "Street_XichLo_Hue";
                cyclo.transform.position = new Vector3(-8.5f, 0.25f, 6.5f);
                cyclo.transform.rotation = Quaternion.Euler(0f, -40f, 0f);
                BoxCollider bc = cyclo.AddComponent<BoxCollider>();
                bc.size = new Vector3(1.1f, 1.4f, 2.2f);
                bc.center = new Vector3(0f, 0.7f, 0f);
            }

            // Bộ bàn ghế nhựa vỉa hè có gắn sẵn ActivityPoint cho NPC
            string chairsPath = "Assets/_Project/Models/Props_Street_Chairs.fbx";
            GameObject chairsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(chairsPath);
            if (chairsPrefab != null)
            {
                GameObject chairs = (GameObject)PrefabUtility.InstantiatePrefab(chairsPrefab);
                chairs.name = "Street_Cafe_PlasticChairs";
                chairs.transform.position = new Vector3(-4.5f, 0.25f, 0f);
                BoxCollider bc = chairs.AddComponent<BoxCollider>();
                bc.size = new Vector3(1.4f, 0.6f, 1.4f);
                bc.center = new Vector3(0f, 0.3f, 0f);

                // Gắn ActivityPoint lên từng chiếc ghế nhựa để NPC có thể vào ngồi
                foreach (Transform child in chairs.transform)
                {
                    if (child.name.Contains("Chair"))
                    {
                        ActivityPoint ap = child.gameObject.AddComponent<ActivityPoint>();
                    }
                }
            }
        }

        private static void SetupNPCCrowd(GameObject player)
        {
            GameObject poolObj = new GameObject("NPCPoolManager");
            NPCPoolManager poolMgr = poolObj.AddComponent<NPCPoolManager>();

            // Tạo các mẫu NPC với màu trang phục đa dạng (lấy cảm hứng từ npc_01 - npc_05)
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
                nma.speed = 1.4f;
                nma.radius = 0.3f;
                nma.height = 1.8f;
                nma.stoppingDistance = 0.2f;

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
            mainCamObj.transform.position = new Vector3(0f, 3f, -5f);
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
