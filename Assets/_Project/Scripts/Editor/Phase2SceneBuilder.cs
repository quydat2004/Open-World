#if UNITY_EDITOR
using Cinemachine;
using HueCity.Camera;
using HueCity.Player;
using HueCity.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            light.intensity = 1.2f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 3. Load và đưa Model Bản đồ TP. Huế vào Scene
            string fbxPath = "Assets/_Project/Models/Map_Hue_Greybox.fbx";
            GameObject mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

            GameObject mapInstance = null;
            if (mapPrefab != null)
            {
                mapInstance = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab);
                mapInstance.name = "Hue_Map_Blocking";
                mapInstance.transform.position = Vector3.zero;

                // Gắn MeshCollider cho các bộ phận của bản đồ
                SetupColliders(mapInstance);
            }
            else
            {
                Debug.LogWarning($"[HueCity] Chưa tìm thấy model tại {fbxPath}, đang tạo mặt đất mặc định.");
                GameObject fallbackGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallbackGround.transform.localScale = new Vector3(500f, 1f, 500f);
                fallbackGround.transform.position = new Vector3(0f, -0.5f, 0f);
            }

            // 4. Tạo Nhân vật (Player với Mesh Rigged hoặc Capsule fallback)
            GameObject player = CreatePlayerObject(new Vector3(0f, 1.2f, 0f));

            // 5. Tạo Xe hơi Taxi Huế (Xanh SM)
            GameObject car = CreateTaxiObject(new Vector3(4f, 0.5f, 6f));

            // 6. Đặt Xe máy (Scooter), Xích lô (Cyclo) và Bộ bàn ghế vỉa hè trang trí
            SpawnDecorativeProps();

            // 7. Thiết lập Cinemachine Camera
            CreateCameraSetup(player, car);

            // 8. Lưu Scene vào Assets/_Project/Scenes/Phase2_MapBlocking.unity
            string scenePath = "Assets/_Project/Scenes/Phase2_MapBlocking.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>[HueCity]</color> Đã tạo thành công Scene Giai đoạn 2 tại: {scenePath}");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("HueCity - Giai đoạn 2", 
                    "Đã khởi tạo thành công Scene Bản đồ thực tế TP. Huế!\n" +
                    "- Đường sá đã được mở rộng +20% phục vụ lái xe tốc độ cao.\n" +
                    "- Vị trí xuất phát: Ngã ba Lê Lợi & Hùng Vương (chân cầu Trường Tiền).\n" +
                    "Bấm Play để lái xe khám phá cung đường!", "Bắt đầu Playtest");
            }
        }

        private static void SetupColliders(GameObject mapRoot)
        {
            MeshFilter[] meshFilters = mapRoot.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in meshFilters)
            {
                string objName = mf.gameObject.name;
                
                // Gắn MeshCollider cho mặt đường và vỉa hè
                if (objName.Contains("Road") || objName.Contains("Sidewalk") || objName.Contains("Buildings"))
                {
                    MeshCollider mc = mf.gameObject.GetComponent<MeshCollider>();
                    if (mc == null)
                    {
                        mc = mf.gameObject.AddComponent<MeshCollider>();
                    }
                    mc.sharedMesh = mf.sharedMesh;
                }
            }
        }

        private static GameObject CreatePlayerObject(Vector3 spawnPos)
        {
            GameObject player = new GameObject("Player_Character");
            player.transform.position = spawnPos;

            // Kiểm tra xem có model nhân vật Rigged không
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
                // Fallback capsule
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "Visual";
                capsule.transform.SetParent(player.transform);
                capsule.transform.localPosition = new Vector3(0f, 1f, 0f);
                Object.DestroyImmediate(capsule.GetComponent<Collider>());
            }

            // Components
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

            // Rigidbody
            Rigidbody rb = car.GetComponent<Rigidbody>();
            if (rb == null) rb = car.AddComponent<Rigidbody>();
            rb.mass = 1450f;
            rb.drag = 0.05f;
            rb.angularDrag = 1.6f;

            // Chassis Box Collider
            BoxCollider col = car.GetComponent<BoxCollider>();
            if (col == null) col = car.AddComponent<BoxCollider>();
            col.size = new Vector3(1.85f, 0.9f, 4.4f);
            col.center = new Vector3(0f, 0.65f, 0f);

            // Exit Point
            GameObject exitPt = new GameObject("DriverExitPoint");
            exitPt.transform.SetParent(car.transform);
            exitPt.transform.localPosition = new Vector3(-2.2f, 0f, 0f);

            // Tìm hoặc tạo WheelColliders
            float trackHalf = 0.85f;
            float baseHalf = 1.35f;
            WheelCollider flCol = CreateWheelCollider(car.transform, "WheelCol_FL", new Vector3(-trackHalf, 0.34f, baseHalf));
            WheelCollider frCol = CreateWheelCollider(car.transform, "WheelCol_FR", new Vector3(trackHalf, 0.34f, baseHalf));
            WheelCollider rlCol = CreateWheelCollider(car.transform, "WheelCol_RL", new Vector3(-trackHalf, 0.34f, -baseHalf));
            WheelCollider rrCol = CreateWheelCollider(car.transform, "WheelCol_RR", new Vector3(trackHalf, 0.34f, -baseHalf));

            // Tìm visual wheels trong prefab nếu có
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

            // Bộ bàn ghế nhựa vỉa hè
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
            }
        }
            Object.DestroyImmediate(cabin.GetComponent<Collider>());

            // Exit Point
            GameObject exitPt = new GameObject("DriverExitPoint");
            exitPt.transform.SetParent(car.transform);
            exitPt.transform.localPosition = new Vector3(-2.2f, 0f, 0f);

            // Tạo 4 bánh xe
            WheelCollider flCol = CreateWheelCollider(car.transform, "Wheel_FL", new Vector3(-0.95f, 0.35f, 1.35f));
            WheelCollider frCol = CreateWheelCollider(car.transform, "Wheel_FR", new Vector3(0.95f, 0.35f, 1.35f));
            WheelCollider rlCol = CreateWheelCollider(car.transform, "Wheel_RL", new Vector3(-0.95f, 0.35f, -1.35f));
            WheelCollider rrCol = CreateWheelCollider(car.transform, "Wheel_RR", new Vector3(0.95f, 0.35f, -1.35f));

            Transform flMesh = CreateWheelMesh(car.transform, "Mesh_FL");
            Transform frMesh = CreateWheelMesh(car.transform, "Mesh_FR");
            Transform rlMesh = CreateWheelMesh(car.transform, "Mesh_RL");
            Transform rrMesh = CreateWheelMesh(car.transform, "Mesh_RR");

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

        private static Transform CreateWheelMesh(Transform parent, string name)
        {
            GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            meshObj.name = name;
            meshObj.transform.SetParent(parent);
            meshObj.transform.localScale = new Vector3(0.76f, 0.15f, 0.76f);
            meshObj.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Object.DestroyImmediate(meshObj.GetComponent<Collider>());
            return meshObj.transform;
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
