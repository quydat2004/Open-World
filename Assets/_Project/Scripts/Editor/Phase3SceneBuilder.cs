#if UNITY_EDITOR
using System.IO;
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
using UnityEngine.SceneManagement;

namespace HueCity.EditorTools
{
    /// <summary>
    /// Trình dựng Scene Lát cắt dọc chi tiết (Vertical Slice Scene Builder) cho TP. Huế - Giai đoạn 3.
    /// Dựng hoàn chỉnh 1 ngã tư biểu tượng: 4 kiểu kiến trúc Huế, đường nhựa & vạch Zebra, đèn giao thông,
    /// bồn cây, quán cà phê vỉa hè, dây điện đô thị, biển báo tên đường, hệ thống Ngày/Đêm và 18 NPC.
    /// </summary>
    public static class Phase3SceneBuilder
    {
        [InitializeOnLoadMethod]
        private static void AutoEnsureSceneBuiltOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                string markerPath = "Temp/Phase3_V3_Built.marker";
                if (!File.Exists(markerPath))
                {
                    File.WriteAllText(markerPath, "built");
                    Debug.Log("[HueCity] Tự động khởi tạo và lưu Scene Lát cắt dọc Giai đoạn 3...");
                    BuildScene(true);
                }
            };
        }

        [MenuItem("HueCity/Build Phase 3 Vertical Slice Scene", false, 30)]
        public static void BuildScene()
        {
            BuildScene(false);
        }

        public static void BuildScene(bool silent)
        {
            // 1. Tạo scene mới
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Thiết lập Ánh sáng Điện ảnh & Chu kỳ Ngày/Đêm (Day/Night Cycle)
            GameObject lightObj = new GameObject("Directional Light - Hue Sun");
            Light sunLight = lightObj.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1.0f, 0.88f, 0.65f); // Nắng vàng ấm đặc trưng miền Trung
            sunLight.intensity = 1.35f;
            sunLight.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(42f, -38f, 0f);

            DayNightCycle dayNight = lightObj.AddComponent<DayNightCycle>();
            dayNight.SetTimeOfDay(17.0f); // Bắt đầu ở Giờ Vàng (Golden Hour) hoàng hôn Huế

            // Thêm Ambient Light ấm áp
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.38f, 0.40f, 0.46f);

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

            // 9. Dây điện chằng chịt đô thị & Biển báo giao thông đường phố Huế
            SetupUrbanStreetCablesAndSigns();

            // 10. Tạo Đám đông NPC (18 NPC đi dạo, qua vạch đi bộ, ngồi quán nước)
            SetupNPCCrowd(player);

            // 11. Thiết lập Cinemachine Camera
            CreateCameraSetup(player, taxi);

            // 12. Lưu Scene vào Assets/_Project/Scenes/Phase3_VerticalSlice.unity
            string scenePath = "Assets/_Project/Scenes/Phase3_VerticalSlice.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>[HueCity]</color> Đã tạo thành công Scene Lát cắt dọc chi tiết tại: {scenePath}");
            if (!Application.isBatchMode && !silent)
            {
                EditorUtility.DisplayDialog("HueCity - Giai đoạn 3: Vertical Slice", 
                    "Đã hoàn thành Lát cắt dọc 1 ngã tư biểu tượng TP. Huế!\n\n" +
                    "• Đường sá chân thực: Nhựa đường sẫm, vạch kẻ đường, vạch Zebra người đi bộ, bó vỉa đá granite.\n" +
                    "• Kiến trúc thực tế: Nhà ống hiện đại (cửa cuốn, ban công kính, bồn inox, điều hòa), Biệt thự Pháp vôi vàng ngói đỏ, Khách sạn góc phố sang trọng, Quán cà phê mái hiên bạt.\n" +
                    "• Dây điện đô thị & Biển báo đường Hùng Vương / Lê Lợi.\n" +
                    "• Chu kỳ Ngày/Đêm (Day/Night Cycle) với nắng vàng hoàng hôn Cố Đô.\n" +
                    "• Đèn giao thông, đèn cổ điển ven sông, bồn cây xanh bóng mát.\n" +
                    "• 18 NPC FSM thông minh đi bộ & ngồi quán nước.\n" +
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

            string taxiFbxPath = "Assets/_Project/Models/Vehicle_Taxi_Hue.fbx";
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

            // Gắn 4 WheelColliders
            WheelCollider wheelFL = CreateWheelCollider(taxi.transform, "Col_Wheel_FL", new Vector3(-0.85f, 0.38f, 1.35f));
            WheelCollider wheelFR = CreateWheelCollider(taxi.transform, "Col_Wheel_FR", new Vector3(0.85f, 0.38f, 1.35f));
            WheelCollider wheelRL = CreateWheelCollider(taxi.transform, "Col_Wheel_RL", new Vector3(-0.85f, 0.38f, -1.35f));
            WheelCollider wheelRR = CreateWheelCollider(taxi.transform, "Col_Wheel_RR", new Vector3(0.85f, 0.38f, -1.35f));

            SerializedObject carSo = new SerializedObject(carController);
            SetWheelPair(carSo, "frontWheels", wheelFL, wheelFR, wheelFL_Mesh, wheelFR_Mesh);
            SetWheelPair(carSo, "rearWheels", wheelRL, wheelRR, wheelRL_Mesh, wheelRR_Mesh);
            carSo.ApplyModifiedProperties();

            return taxi;
        }

        private static void SpawnStreetCultureProps()
        {
            // Xe máy dựng chân chống bên mép vỉa hè
            string scooterPath = "Assets/_Project/Models/Vehicle_Scooter.fbx";
            GameObject scooterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(scooterPath);
            if (scooterPrefab != null)
            {
                GameObject scooter = (GameObject)PrefabUtility.InstantiatePrefab(scooterPrefab);
                scooter.name = "Street_Scooter_Sidewalk";
                scooter.transform.position = new Vector3(10.5f, 0.25f, 12.0f);
                scooter.transform.rotation = Quaternion.Euler(0f, 75f, 0f);
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

        private static void SetupUrbanStreetCablesAndSigns()
        {
            GameObject urbanProps = new GameObject("Urban_Props_Network");

            // 1. Chùm dây điện chằng chịt đặc trưng đô thị Việt Nam (Overhead Cables)
            GameObject cableParent = new GameObject("Overhead_Electrical_Cables");
            cableParent.transform.SetParent(urbanProps.transform);

            Vector3[] startPoles = new Vector3[]
            {
                new Vector3(-11.5f, 6.2f, 14.5f),
                new Vector3(11.5f, 6.2f, 14.5f),
                new Vector3(-11.5f, 6.2f, -14.5f),
                new Vector3(11.5f, 6.2f, -14.5f)
            };

            Vector3[] endPoles = new Vector3[]
            {
                new Vector3(11.5f, 6.0f, 14.5f),
                new Vector3(11.5f, 6.0f, -14.5f),
                new Vector3(-11.5f, 6.0f, -14.5f),
                new Vector3(-11.5f, 6.0f, 14.5f)
            };

            Material cableMat = new Material(Shader.Find("Standard"));
            cableMat.color = new Color(0.08f, 0.08f, 0.08f);
            cableMat.SetFloat("_Glossiness", 0.1f);

            for (int i = 0; i < startPoles.Length; i++)
            {
                for (int wire = 0; wire < 3; wire++)
                {
                    GameObject wireObj = new GameObject($"Cable_Wire_{i}_{wire}");
                    wireObj.transform.SetParent(cableParent.transform);
                    LineRenderer lr = wireObj.AddComponent<LineRenderer>();
                    lr.material = cableMat;
                    lr.startWidth = 0.032f;
                    lr.endWidth = 0.032f;
                    lr.positionCount = 12;
                    lr.useWorldSpace = true;

                    Vector3 p0 = startPoles[i] + new Vector3(0f, wire * 0.14f, wire * 0.06f);
                    Vector3 p1 = endPoles[i] + new Vector3(0f, wire * 0.14f, wire * 0.06f);
                    float sagScale = 0.60f + wire * 0.18f;

                    for (int step = 0; step < 12; step++)
                    {
                        float t = (float)step / 11f;
                        Vector3 pos = Vector3.Lerp(p0, p1, t);
                        float sag = 4f * sagScale * t * (1f - t);
                        pos.y -= sag;
                        lr.SetPosition(step, pos);
                    }
                }
            }

            // 2. Biển tên đường "ĐƯỜNG HÙNG VƯƠNG" & "ĐƯỜNG LÊ LỢI"
            CreateStreetSign(urbanProps.transform, new Vector3(10.5f, 0f, 10.5f), "Sign_HungVuong", 0f);
            CreateStreetSign(urbanProps.transform, new Vector3(-10.5f, 0f, 10.5f), "Sign_LeLoi", 90f);
        }

        private static void CreateStreetSign(Transform parent, Vector3 position, string signName, float rotationY)
        {
            GameObject sign = new GameObject(signName);
            sign.transform.SetParent(parent);
            sign.transform.position = position;
            sign.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);

            // Cột biển báo
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(sign.transform);
            pole.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            pole.transform.localScale = new Vector3(0.08f, 1.4f, 0.08f);

            Material poleMat = new Material(Shader.Find("Standard"));
            poleMat.color = new Color(0.72f, 0.74f, 0.78f);
            poleMat.SetFloat("_Metallic", 0.9f);
            poleMat.SetFloat("_Glossiness", 0.4f);
            pole.GetComponent<Renderer>().material = poleMat;

            // Biển tên đường chữ nhật màu xanh biển báo
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Plate";
            plate.transform.SetParent(sign.transform);
            plate.transform.localPosition = new Vector3(0f, 2.65f, 0f);
            plate.transform.localScale = new Vector3(0.85f, 0.28f, 0.04f);

            Material plateMat = new Material(Shader.Find("Standard"));
            plateMat.color = new Color(0.06f, 0.28f, 0.72f); // Xanh dương biển chỉ dẫn
            plateMat.SetFloat("_Glossiness", 0.5f);
            plate.GetComponent<Renderer>().material = plateMat;
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
