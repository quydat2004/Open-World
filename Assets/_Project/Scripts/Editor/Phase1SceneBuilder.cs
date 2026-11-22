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
    public static class Phase1SceneBuilder
    {
        [MenuItem("HueCity/Build Phase 1 Greybox Scene", false, 10)]
        public static void BuildScene()
        {
            // 1. Tạo scene mới
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Thiết lập Ánh sáng cơ bản
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 3. Mặt đất và Rào chắn / Dốc kiểm thử
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(200f, 1f, 200f);

            // Tạo dốc thử nghiệm (Ramp)
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Test_Ramp";
            ramp.transform.position = new Vector3(15f, 1.5f, 20f);
            ramp.transform.rotation = Quaternion.Euler(-15f, 0f, 0f);
            ramp.transform.localScale = new Vector3(10f, 0.5f, 15f);

            // 4. Tạo Nhân vật (Player Greybox)
            GameObject player = CreatePlayerObject();

            // 5. Tạo Xe hơi (Car Greybox)
            GameObject car = CreateCarObject();

            // 6. Tạo Camera Cinemachine
            CreateCameraSetup(player, car);

            // 7. Lưu Scene vào Assets/_Project/Scenes/
            string scenePath = "Assets/_Project/Scenes/Phase1_Greybox.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>[HueCity]</color> Đã tạo thành công Scene Giai đoạn 1 tại: {scenePath}");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("HueCity - Giai đoạn 1", "Đã khởi tạo thành công Scene Greybox!\nBấm Play để bắt đầu kiểm thử di chuyển và lái xe.", "OK");
            }
        }

        private static GameObject CreatePlayerObject()
        {
            GameObject player = new GameObject("Player_Greybox");
            player.transform.position = new Vector3(0f, 1f, 0f);

            // Mesh đại diện (Capsule)
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Visual";
            capsule.transform.SetParent(player.transform);
            capsule.transform.localPosition = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(capsule.GetComponent<Collider>()); // Bỏ capsule collider, dùng CharacterController

            // Mũi chỉ hướng mặt (Direction indicator)
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose_Indicator";
            nose.transform.SetParent(player.transform);
            nose.transform.localPosition = new Vector3(0f, 1.5f, 0.45f);
            nose.transform.localScale = new Vector3(0.25f, 0.25f, 0.4f);
            Object.DestroyImmediate(nose.GetComponent<Collider>());

            // Components
            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;
            cc.center = new Vector3(0f, 1f, 0f);

            player.AddComponent<PlayerInputHandler>();
            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerInteraction>();

            return player;
        }

        private static GameObject CreateCarObject()
        {
            GameObject car = new GameObject("Car_Greybox");
            car.transform.position = new Vector3(6f, 0.6f, 4f);

            // Rigidbody
            Rigidbody rb = car.AddComponent<Rigidbody>();
            rb.mass = 1400f;
            rb.drag = 0.05f;
            rb.angularDrag = 1.5f;

            // Chassis Box Collider
            BoxCollider col = car.AddComponent<BoxCollider>();
            col.size = new Vector3(2f, 0.9f, 4.2f);
            col.center = new Vector3(0f, 0.65f, 0f);

            // Visual Body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Car_Body_Visual";
            body.transform.SetParent(car.transform);
            body.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            body.transform.localScale = new Vector3(2f, 0.9f, 4.2f);
            Object.DestroyImmediate(body.GetComponent<Collider>());

            // Cabin
            GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "Cabin_Visual";
            cabin.transform.SetParent(car.transform);
            cabin.transform.localPosition = new Vector3(0f, 1.35f, -0.3f);
            cabin.transform.localScale = new Vector3(1.7f, 0.7f, 2.2f);
            Object.DestroyImmediate(cabin.GetComponent<Collider>());

            // Exit Point cho người chơi
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

            // CarController & VehicleSeat
            CarController carController = car.AddComponent<CarController>();
            VehicleSeat seat = car.AddComponent<VehicleSeat>();

            // Gán dữ liệu vào serialized field
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
            col.suspensionDistance = 0.2f;

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
            // Main Camera
            GameObject mainCamObj = new GameObject("Main Camera");
            mainCamObj.tag = "MainCamera";
            mainCamObj.transform.position = new Vector3(0f, 3f, -5f);
            mainCamObj.AddComponent<UnityEngine.Camera>();
            mainCamObj.AddComponent<AudioListener>();
            CinemachineBrain brain = mainCamObj.AddComponent<CinemachineBrain>();
            brain.m_DefaultBlend.m_Time = 0.8f;

            // Player Virtual Camera (Priority 20)
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

            // Vehicle Virtual Camera (Priority 10)
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

            // CameraManager
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
