#if UNITY_EDITOR
#pragma warning disable CS0618
using System.Collections.Generic;
using System.IO;
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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HueCity.EditorTools
{
    using Camera = UnityEngine.Camera;

    /// <summary>
    /// Trình dựng Scene Giai đoạn 7: Đánh bóng, Âm thanh & Thời tiết Cố Đô (Phase 7 Polished City Scene Builder).
    /// Tích hợp đầy đủ:
    /// - Âm thanh động cơ xe Dynamic Engine Pitch, tiếng rít phanh lốp xe, tiếng còi xe phát xung quanh (VehicleAudioController).
    /// - Âm thanh nhịp bước chân người đi bộ (PlayerFootstepAudio).
    /// - Hệ thống Thời tiết & Mưa rào xứ Huế (WeatherManager): Hạt mưa rơi, đường ướt phản chiếu bóng bẩy (Wetness Reflection), tiếng mưa rơi rào rào.
    /// - Hiệu ứng hình ảnh xe cộ (VehicleVFXController): Khói ống bô (Exhaust Smoke), Đèn pha chiếu rọi (Headlights), Đèn phanh đỏ rực rỡ (Brake Lights).
    /// - Bản đồ đô thị mở rộng 500m x 500m (Phase 6), 40+ Shophouses, LOD Groups, GPU Instancing, Minimap 500m và chuỗi Nhiệm vụ.
    /// </summary>
    public static class Phase7SceneBuilder
    {
        [InitializeOnLoadMethod]
        private static void AutoRegisterSceneBuild()
        {
            EditorApplication.delayCall += () =>
            {
                string markerPath = "Temp/Phase7_PolishedCity_Built.marker";
                if (!File.Exists(markerPath))
                {
                    File.WriteAllText(markerPath, "built");
                    Debug.Log("[HueCity Phase 7] Tự động khởi tạo Scene Giai đoạn 7: Đánh bóng, Âm thanh & Mưa xứ Huế...");
                    BuildScene(true);
                }
            };
        }

        [MenuItem("HueCity/Build Phase 7 Polished City (Audio, Weather & VFX)", false, 70)]
        public static void BuildSceneMenu()
        {
            BuildScene(false);
        }

        public static void BuildScene(bool silent)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[HueCity Phase 7] Không thể build scene khi đang trong Play Mode.");
                return;
            }

            // 0. Khởi tạo toàn bộ Asset mẫu, URP, Audio Clips & GPU Instancing
            RealismOverhaulSetup.EnsureFullSetup();
            LocomotionClipGenerator.EnsureClipsAndController();
            ProceduralAudioGenerator.EnsureAudioAssets();
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

            // 5. Dựng Mạng lưới Đường sá & Vỉa hè mở rộng quy mô 500m x 500m
            GameObject roadGridRoot = SetupExpanded500mRoadAndSidewalkGrid();

            // 6. Bố trí 40+ Tòa nhà phố Modular có gắn 2-level LOD Group
            SpawnExpandedCityShophouses();

            // 7. Bố trí Đạo cụ Văn hóa vỉa hè Huế
            SpawnExpandedStreetCultureProps();

            // 8. Tự động Bake NavMesh cho người đi bộ trên toàn bộ vỉa hè
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            Debug.Log("[HueCity Phase 7] Đã bake thành công NavMesh mở rộng cho bản đồ 500m x 500m!");

            // 9. Tạo Nhân vật chính (Player) kèm Bước chân âm thanh (Footstep Audio)
            Vector3 playerSpawn = new Vector3(13.0f, 0.25f, 13.0f);
            GameObject player = CreatePolishedPlayerObject(playerSpawn);

            // 10. Tạo Xe Taxi người chơi lái thử có Engine Sound, Horn, Skid Audio & VFX
            Vector3 taxiSpawn = new Vector3(3.5f, 0.35f, 16.0f);
            GameObject playerTaxi = CreatePolishedTaxiObject(taxiSpawn);

            // 11. Tạo Cinemachine Camera Switcher
            Camera mainCam = CreateCameraSetup(player, playerTaxi);

            // 12. Thiết lập Hệ thống Thời tiết & Mưa xứ Huế (WeatherManager & Rain Particle)
            SetupHueWeatherSystem(player.transform);

            // 13. Tạo Đèn giao thông ngã tư 4 hướng
            TrafficLightController trafficLights = SetupTrafficLights();

            // 14. Tạo Mạng lưới Waypoints giao thông mở rộng
            List<TrafficWaypoint> startWaypoints = SetupExpandedTrafficWaypoints();

            // 15. Tạo Trình quản lý Xe giao thông AI (TrafficPoolManager)
            SetupExpandedTrafficPool(startWaypoints, playerTaxi);

            // 16. Tạo Đám đông NPC (22 NPC với FSM 5 trạng thái)
            SetupNPCCrowd(player);

            // 17. Thiết lập SaveLoadManager JSON
            GameObject saveLoadObj = new GameObject("SaveLoadManager");
            SaveLoadManager saveLoadMgr = saveLoadObj.AddComponent<SaveLoadManager>();

            // 18. Thiết lập MissionManager
            GameObject missionMgrObj = new GameObject("MissionManager");
            MissionManager missionMgr = missionMgrObj.AddComponent<MissionManager>();
            foreach (var m in sampleMissions)
            {
                missionMgr.RegisterMission(m);
            }

            // 19. Tạo Giao diện Game HUD uGUI hoàn chỉnh
            GameHUDManager hudManager;
            GameObject canvasObj = GameHUDManager.CreateFullHUDCanvas(player.transform, mainCam, out hudManager);

            // 20. Cấu hình Camera Minimap bao quát bản đồ 500m
            SetupExpandedMinimapCamera(hudManager.Minimap, player.transform);

            // 21. Bắt đầu nhiệm vụ mẫu đầu tiên
            if (sampleMissions.Count > 0)
            {
                missionMgr.StartMission(sampleMissions[0]);
            }

            // 22. Lưu Scene Phase 7 chính thức
            string phase7Path = "Assets/_Project/Scenes/Phase7_PolishedCity.unity";
            EditorSceneManager.SaveScene(newScene, phase7Path);

            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[HueCity Phase 7]</color> Đã hoàn tất và lưu Scene Đánh bóng, Âm thanh & Mưa Huế tại: {phase7Path}");
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
        }

        private static void SetupHueWeatherSystem(Transform playerTransform)
        {
            GameObject weatherObj = new GameObject("WeatherManager");
            WeatherManager weatherMgr = weatherObj.AddComponent<WeatherManager>();

            // 1. Tạo Rain Particle System
            GameObject rainParticleObj = new GameObject("VFX_Hue_Rain_Particles");
            rainParticleObj.transform.SetParent(weatherObj.transform);
            rainParticleObj.transform.position = new Vector3(0f, 15f, 0f);
            rainParticleObj.transform.rotation = Quaternion.Euler(75f, 15f, 0f); // Mưa rơi nghiêng theo gió

            ParticleSystem ps = rainParticleObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 0.9f;
            main.startSpeed = 24.0f;
            main.startSize = 0.08f;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.85f, 0.92f, 1.0f, 0.55f);

            var emission = ps.emission;
            emission.rateOverTime = 850f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(85f, 85f, 1f);

            ParticleSystemRenderer psRenderer = rainParticleObj.GetComponent<ParticleSystemRenderer>();
            psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            psRenderer.lengthScale = 4.5f;
            psRenderer.velocityScale = 0.15f;

            // 2. Tải Audio & Material
            Material roadMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Road_Asphalt.mat");
            AudioClip rainClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX_Rain_Ambient_Loop.wav");

            weatherMgr.Configure(ps, roadMat, rainClip);
            ps.Stop(); // Bắt đầu ở trạng thái tạnh ráo, người chơi bấm P để bật mưa
        }

        private static GameObject CreatePolishedPlayerObject(Vector3 spawnPos)
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
                if (prefabAnim != null && prefabAnim.avatar != null && prefabAnim.avatar.isValid)
                {
                    avatar = prefabAnim.avatar;
                }
                if (avatar == null)
                {
                    foreach (var a in AssetDatabase.LoadAllAssetsAtPath(charFbxPath))
                    {
                        if (a is Avatar av && av.isValid) { avatar = av; break; }
                    }
                }
                if (avatar != null) anim.avatar = avatar;
            }

            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerInputHandler>();
            player.AddComponent<PlayerInteraction>();

            // Gắn Footstep Audio
            PlayerFootstepAudio footstepAudio = player.AddComponent<PlayerFootstepAudio>();
            AudioClip stepClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX_Footstep_Step.wav");
            if (stepClip != null) footstepAudio.ConfigureClip(stepClip);

            return player;
        }

        private static GameObject CreatePolishedTaxiObject(Vector3 spawnPos)
        {
            GameObject taxi = new GameObject("Taxi_Hue_XanhSM_Polished");
            taxi.tag = "Vehicle";
            taxi.transform.position = spawnPos;
            taxi.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

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

            // 1. Gắn VehicleAudioController
            VehicleAudioController audioCtrl = taxi.AddComponent<VehicleAudioController>();
            AudioClip engineClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX_Engine_Idle_Loop.wav");
            AudioClip skidClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX_Tire_Skid.wav");
            AudioClip hornClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX_Car_Horn.wav");
            audioCtrl.ConfigureClips(engineClip, skidClip, hornClip);

            // 2. Gắn VehicleVFXController (Headlights, Brake Lights, Exhaust Smoke)
            VehicleVFXController vfxCtrl = taxi.AddComponent<VehicleVFXController>();

            // Đèn pha trước (2 Spotlights)
            GameObject headLObj = new GameObject("Headlight_L");
            headLObj.transform.SetParent(taxi.transform);
            headLObj.transform.localPosition = new Vector3(-0.65f, 0.65f, 2.2f);
            headLObj.transform.localRotation = Quaternion.Euler(5f, -2f, 0f);
            Light headLightL = headLObj.AddComponent<Light>();
            headLightL.type = LightType.Spot;
            headLightL.range = 28f;
            headLightL.spotAngle = 55f;
            headLightL.color = new Color(1f, 0.96f, 0.88f);
            headLightL.intensity = 2.5f;

            GameObject headRObj = new GameObject("Headlight_R");
            headRObj.transform.SetParent(taxi.transform);
            headRObj.transform.localPosition = new Vector3(0.65f, 0.65f, 2.2f);
            headRObj.transform.localRotation = Quaternion.Euler(5f, 2f, 0f);
            Light headLightR = headRObj.AddComponent<Light>();
            headLightR.type = LightType.Spot;
            headLightR.range = 28f;
            headLightR.spotAngle = 55f;
            headLightR.color = new Color(1f, 0.96f, 0.88f);
            headLightR.intensity = 2.5f;

            // Đèn phanh sau (2 Pointlights đỏ)
            GameObject brakeLObj = new GameObject("BrakeLight_L");
            brakeLObj.transform.SetParent(taxi.transform);
            brakeLObj.transform.localPosition = new Vector3(-0.75f, 0.75f, -2.2f);
            Light brakeLightL = brakeLObj.AddComponent<Light>();
            brakeLightL.type = LightType.Point;
            brakeLightL.range = 3.5f;
            brakeLightL.color = Color.red;
            brakeLightL.intensity = 0.4f;

            GameObject brakeRObj = new GameObject("BrakeLight_R");
            brakeRObj.transform.SetParent(taxi.transform);
            brakeRObj.transform.localPosition = new Vector3(0.75f, 0.75f, -2.2f);
            Light brakeLightR = brakeRObj.AddComponent<Light>();
            brakeLightR.type = LightType.Point;
            brakeLightR.range = 3.5f;
            brakeLightR.color = Color.red;
            brakeLightR.intensity = 0.4f;

            // Khói xả ống bô (Exhaust Smoke Particle System)
            GameObject smokeObj = new GameObject("VFX_Exhaust_Smoke");
            smokeObj.transform.SetParent(taxi.transform);
            smokeObj.transform.localPosition = new Vector3(0.55f, 0.28f, -2.25f);
            smokeObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            ParticleSystem smokePs = smokeObj.AddComponent<ParticleSystem>();
            var smMain = smokePs.main;
            smMain.loop = true;
            smMain.startLifetime = 0.8f;
            smMain.startSpeed = 2.5f;
            smMain.startSize = 0.25f;
            smMain.startColor = new Color(0.7f, 0.7f, 0.7f, 0.25f);
            smMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var smEmission = smokePs.emission;
            smEmission.rateOverTime = 8f;

            var smShape = smokePs.shape;
            smShape.shapeType = ParticleSystemShapeType.Cone;
            smShape.angle = 12f;
            smShape.radius = 0.05f;

            vfxCtrl.Configure(smokePs, headLightL, headLightR, brakeLightL, brakeLightR);

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

        private static void SetupExpandedMinimapCamera(MinimapController minimapCtrl, Transform playerTarget)
        {
            if (minimapCtrl == null) return;

            GameObject miniCamObj = new GameObject("Minimap_TopDown_Camera_500m");
            Camera miniCam = miniCamObj.AddComponent<Camera>();
            miniCam.orthographic = true;
            miniCam.orthographicSize = 90.0f;
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

        private static GameObject SetupExpanded500mRoadAndSidewalkGrid()
        {
            GameObject cityGridRoot = new GameObject("HueCity_500m_Grid");

            Material roadMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Road_Asphalt.mat");
            Material sidewalkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Sidewalk_Concrete.mat");
            Material curbMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Curb_Stone.mat");
            Material whiteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_RoadMarking_White.mat");
            Material yellowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_RoadMarking_Yellow.mat");

            // Mặt đất đô thị
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Terrain_Foundation_500m";
            ground.transform.SetParent(cityGridRoot.transform);
            ground.transform.position = new Vector3(0f, -0.05f, 0f);
            ground.transform.localScale = new Vector3(55f, 1f, 55f);
            if (sidewalkMat != null) ground.GetComponent<Renderer>().sharedMaterial = sidewalkMat;
            CityOptimizationUtility.SetOptimizedStaticFlags(ground, isOccluder: false);

            // 1. Trục đại lộ Bắc - Nam (X = 0, Z từ -250 đến +250)
            CreateRoadCorridor(cityGridRoot.transform, "Avenue_NorthSouth_Main", new Vector3(0f, 0.01f, 0f), new Vector3(16f, 0.02f, 500f), roadMat);

            // 2. Trục đại lộ Đông - Tây (Z = 0, X từ -250 đến +250)
            CreateRoadCorridor(cityGridRoot.transform, "Avenue_EastWest_Main", new Vector3(0f, 0.01f, 0f), new Vector3(500f, 0.02f, 16f), roadMat);

            // 3. Các tuyến phố phụ (Secondary Streets)
            CreateRoadCorridor(cityGridRoot.transform, "Street_Sub_North", new Vector3(0f, 0.01f, 130f), new Vector3(500f, 0.02f, 10f), roadMat);
            CreateRoadCorridor(cityGridRoot.transform, "Street_Sub_South", new Vector3(0f, 0.01f, -130f), new Vector3(500f, 0.02f, 10f), roadMat);
            CreateRoadCorridor(cityGridRoot.transform, "Street_Sub_East", new Vector3(130f, 0.01f, 0f), new Vector3(10f, 0.02f, 500f), roadMat);
            CreateRoadCorridor(cityGridRoot.transform, "Street_Sub_West", new Vector3(-130f, 0.01f, 0f), new Vector3(10f, 0.02f, 500f), roadMat);

            // 4. Vỉa hè 9 đại khối phố
            float[] blockCenters = new float[] { -190f, 0f, 190f };
            int bIndex = 1;
            foreach (float bx in blockCenters)
            {
                foreach (float bz in blockCenters)
                {
                    if (Mathf.Abs(bx) < 1f && Mathf.Abs(bz) < 1f) continue;

                    GameObject blockSidewalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    blockSidewalk.name = $"Sidewalk_Block_{bIndex++}";
                    blockSidewalk.transform.SetParent(cityGridRoot.transform);
                    blockSidewalk.transform.position = new Vector3(bx, 0.08f, bz);
                    blockSidewalk.transform.localScale = new Vector3(110f, 0.16f, 110f);
                    if (sidewalkMat != null) blockSidewalk.GetComponent<Renderer>().sharedMaterial = sidewalkMat;
                    CityOptimizationUtility.SetOptimizedStaticFlags(blockSidewalk, isOccluder: false);

                    CreateBlockCurbs(cityGridRoot.transform, new Vector3(bx, 0.08f, bz), 110f, 110f, curbMat);
                }
            }

            // Vạch kẻ đường
            CreateExtendedRoadMarkings(cityGridRoot.transform, whiteMat, yellowMat);

            return cityGridRoot;
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
            float curbH = 0.18f;
            float curbW = 0.35f;

            CreateCurbStrip(parent, new Vector3(center.x, curbH * 0.5f, center.z + halfZ), new Vector3(sizeX, curbH, curbW), curbMat);
            CreateCurbStrip(parent, new Vector3(center.x, curbH * 0.5f, center.z - halfZ), new Vector3(sizeX, curbH, curbW), curbMat);
            CreateCurbStrip(parent, new Vector3(center.x + halfX, curbH * 0.5f, center.z), new Vector3(curbW, curbH, sizeZ), curbMat);
            CreateCurbStrip(parent, new Vector3(center.x - halfX, curbH * 0.5f, center.z), new Vector3(curbW, curbH, sizeZ), curbMat);
        }

        private static void CreateCurbStrip(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curb.name = "Curb_Granite";
            curb.transform.SetParent(parent);
            curb.transform.position = pos;
            curb.transform.localScale = size;
            if (mat != null) curb.GetComponent<Renderer>().sharedMaterial = mat;
            CityOptimizationUtility.SetOptimizedStaticFlags(curb, isOccluder: false);
        }

        private static void CreateExtendedRoadMarkings(Transform parent, Material whiteMat, Material yellowMat)
        {
            CreateCenterDoubleYellow(parent, Vector3.zero, 500f, isHorizontal: false, yellowMat);
            CreateCenterDoubleYellow(parent, Vector3.zero, 500f, isHorizontal: true, yellowMat);
            CreateIntersectionCrossings(parent, Vector3.zero, whiteMat);
        }

        private static void CreateCenterDoubleYellow(Transform parent, Vector3 center, float length, bool isHorizontal, Material mat)
        {
            float lineW = 0.15f;
            float gap = 0.25f;
            float lineY = 0.025f;

            if (isHorizontal)
            {
                CreateMarkingStrip(parent, "DoubleYellow_H1", new Vector3(center.x, lineY, center.z + gap * 0.5f), new Vector3(length, 0.005f, lineW), mat);
                CreateMarkingStrip(parent, "DoubleYellow_H2", new Vector3(center.x, lineY, center.z - gap * 0.5f), new Vector3(length, 0.005f, lineW), mat);
            }
            else
            {
                CreateMarkingStrip(parent, "DoubleYellow_V1", new Vector3(center.x + gap * 0.5f, lineY, center.z), new Vector3(lineW, 0.005f, length), mat);
                CreateMarkingStrip(parent, "DoubleYellow_V2", new Vector3(center.x - gap * 0.5f, lineY, center.z), new Vector3(lineW, 0.005f, length), mat);
            }
        }

        private static void CreateMarkingStrip(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = name;
            strip.transform.SetParent(parent);
            strip.transform.position = pos;
            strip.transform.localScale = size;
            Object.DestroyImmediate(strip.GetComponent<Collider>());
            if (mat != null) strip.GetComponent<Renderer>().sharedMaterial = mat;
            CityOptimizationUtility.SetOptimizedStaticFlags(strip, isOccluder: false);
        }

        private static void CreateIntersectionCrossings(Transform parent, Vector3 center, Material whiteMat)
        {
            CreateZebraStripes(parent, new Vector3(center.x, 0.025f, center.z + 10.5f), isHorizontal: true, whiteMat);
            CreateZebraStripes(parent, new Vector3(center.x, 0.025f, center.z - 10.5f), isHorizontal: true, whiteMat);
            CreateZebraStripes(parent, new Vector3(center.x + 10.5f, 0.025f, center.z), isHorizontal: false, whiteMat);
            CreateZebraStripes(parent, new Vector3(center.x - 10.5f, 0.025f, center.z), isHorizontal: false, whiteMat);
        }

        private static void CreateZebraStripes(Transform parent, Vector3 center, bool isHorizontal, Material mat)
        {
            int stripeCount = 8;
            float stripeW = 0.55f;
            float stripeL = 3.2f;
            float spacing = 1.1f;

            for (int i = 0; i < stripeCount; i++)
            {
                float offset = (i - (stripeCount - 1) * 0.5f) * spacing;
                Vector3 pos = isHorizontal ? new Vector3(center.x + offset, center.y, center.z)
                                           : new Vector3(center.x, center.y, center.z + offset);
                Vector3 size = isHorizontal ? new Vector3(stripeW, 0.005f, stripeL)
                                            : new Vector3(stripeL, 0.005f, stripeW);

                CreateMarkingStrip(parent, $"Zebra_{i}", pos, size, mat);
            }
        }

        private static void SpawnExpandedCityShophouses()
        {
            string houseFbxPath = "Assets/_Project/Models/House_Modular_01.fbx";
            GameObject housePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(houseFbxPath);
            if (housePrefab == null) return;

            GameObject buildingsRoot = new GameObject("HueCity_Shophouse_Corridors");

            List<Vector3> streetPositions = new List<Vector3>();

            // Phố Bắc (Dãy phố nhìn ra đại lộ)
            for (int i = -3; i <= 3; i++)
            {
                if (Mathf.Abs(i) < 1) continue;
                streetPositions.Add(new Vector3(i * 12.0f, 0f, 14.5f));
                streetPositions.Add(new Vector3(i * 12.0f, 0f, -14.5f));
            }

            // Dãy phố Đông & Tây
            for (int i = -3; i <= 3; i++)
            {
                if (Mathf.Abs(i) < 1) continue;
                streetPositions.Add(new Vector3(14.5f, 0f, i * 12.0f));
                streetPositions.Add(new Vector3(-14.5f, 0f, i * 12.0f));
            }

            // Dãy phố mở rộng các ô phố vệ tinh (+130m, -130m)
            float[] satelliteOffsets = new float[] { -130f, 130f };
            foreach (float soX in satelliteOffsets)
            {
                for (int i = -2; i <= 2; i++)
                {
                    streetPositions.Add(new Vector3(soX + i * 14.0f, 0f, 60.0f));
                    streetPositions.Add(new Vector3(soX + i * 14.0f, 0f, -60.0f));
                }
            }

            int count = 0;
            foreach (var pos in streetPositions)
            {
                GameObject house = (GameObject)PrefabUtility.InstantiatePrefab(housePrefab);
                house.name = $"Shophouse_Modular_{count++:00}";
                house.transform.SetParent(buildingsRoot.transform);
                house.transform.position = pos;

                float rotY = 0f;
                if (pos.z > 0 && Mathf.Abs(pos.z) > Mathf.Abs(pos.x)) rotY = 180f;
                else if (pos.z < 0 && Mathf.Abs(pos.z) > Mathf.Abs(pos.x)) rotY = 0f;
                else if (pos.x > 0) rotY = -90f;
                else rotY = 90f;

                house.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

                // Gắn 2-level LOD Group
                CityOptimizationUtility.SetupBuildingLODGroup(house, lod0ScreenHeight: 0.35f, lod1ScreenHeight: 0.08f);

                // Gán cờ Static Occluder/Occludee
                CityOptimizationUtility.SetOptimizedStaticFlags(house, isOccluder: true);
            }
        }

        private static void SpawnExpandedStreetCultureProps()
        {
            GameObject propsRoot = new GameObject("Street_Culture_Props");

            string scooterPath = "Assets/_Project/Models/Vehicle_Scooter.fbx";
            string xichLoPath = "Assets/_Project/Models/Vehicle_XichLo.fbx";
            string chairsPath = "Assets/_Project/Models/Props_Street_Chairs.fbx";

            GameObject scooterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(scooterPath);
            GameObject xichLoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(xichLoPath);
            GameObject chairsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(chairsPath);

            if (chairsPrefab != null)
            {
                SpawnProp(chairsPrefab, "Cafe_Chairs_North", new Vector3(14.0f, 0.16f, 12.0f), -90f, propsRoot.transform);
                SpawnProp(chairsPrefab, "Cafe_Chairs_South", new Vector3(-14.0f, 0.16f, -12.0f), 90f, propsRoot.transform);
                SpawnProp(chairsPrefab, "Cafe_Chairs_East", new Vector3(12.0f, 0.16f, -14.0f), 0f, propsRoot.transform);
            }

            if (scooterPrefab != null)
            {
                SpawnProp(scooterPrefab, "Scooter_Parked_1", new Vector3(11.5f, 0.16f, 13.5f), 15f, propsRoot.transform);
                SpawnProp(scooterPrefab, "Scooter_Parked_2", new Vector3(-11.5f, 0.16f, -13.5f), -165f, propsRoot.transform);
            }

            if (xichLoPrefab != null)
            {
                SpawnProp(xichLoPrefab, "XichLo_Hue_1", new Vector3(15.5f, 0.16f, 9.5f), -80f, propsRoot.transform);
                SpawnProp(xichLoPrefab, "XichLo_Hue_2", new Vector3(-15.5f, 0.16f, -9.5f), 100f, propsRoot.transform);
            }

            AttachActivityPoints(propsRoot);
        }

        private static GameObject SpawnProp(GameObject prefab, string name, Vector3 pos, float yRot, Transform parent)
        {
            GameObject prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            prop.name = name;
            prop.transform.SetParent(parent);
            prop.transform.position = pos;
            prop.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
            CityOptimizationUtility.SetOptimizedStaticFlags(prop, isOccluder: false);
            return prop;
        }

        private static void AttachActivityPoints(GameObject root)
        {
            foreach (Transform child in root.transform)
            {
                if (child.name.Contains("Chairs") || child.name.Contains("XichLo"))
                {
                    if (child.GetComponent<ActivityPoint>() == null)
                    {
                        ActivityPoint ap = child.gameObject.AddComponent<ActivityPoint>();
                        SerializedObject apSo = new SerializedObject(ap);
                        apSo.FindProperty("isSheltered").boolValue = true;
                        apSo.ApplyModifiedProperties();
                    }
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

        private static TrafficLightController SetupTrafficLights()
        {
            GameObject tlObj = new GameObject("TrafficLight_Intersection");
            TrafficLightController tlc = tlObj.AddComponent<TrafficLightController>();
            return tlc;
        }

        private static void SetupExpandedTrafficPool(List<TrafficWaypoint> startPoints, GameObject playerTaxi)
        {
            GameObject poolObj = new GameObject("TrafficPoolManager");
            TrafficPoolManager poolMgr = poolObj.AddComponent<TrafficPoolManager>();

            List<GameObject> vehiclePrefabs = new List<GameObject>();

            string carFbxPath = "Assets/_Project/Models/Car_Sedan_01.fbx";
            GameObject carFbx = AssetDatabase.LoadAssetAtPath<GameObject>(carFbxPath);
            if (carFbx != null)
            {
                GameObject carTemplate = new GameObject("Template_Car_Sedan_AI");
                carTemplate.transform.SetParent(poolObj.transform);
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(carFbx);
                visual.transform.SetParent(carTemplate.transform);
                visual.transform.localPosition = Vector3.zero;

                BoxCollider bc = carTemplate.AddComponent<BoxCollider>();
                bc.size = new Vector3(1.85f, 1.4f, 4.4f);
                bc.center = new Vector3(0f, 0.7f, 0f);
                carTemplate.AddComponent<Rigidbody>();
                carTemplate.AddComponent<TrafficVehicleAI>();
                carTemplate.SetActive(false);
                vehiclePrefabs.Add(carTemplate);
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
            if (maxProp != null) maxProp.intValue = 22;
            poolSo.ApplyModifiedProperties();
        }

        private static List<MissionData> EnsureExtendedMissions()
        {
            string dir = "Assets/_Project/ScriptableObjects/Missions";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            List<MissionData> missions = new List<MissionData>();

            string m1Path = $"{dir}/Mission_01_Delivery_HueCakes.asset";
            MissionData m1 = AssetDatabase.LoadAssetAtPath<MissionData>(m1Path);
            if (m1 == null)
            {
                m1 = ScriptableObject.CreateInstance<MissionData>();
                m1.Initialize("MIS_01", "Giao Bánh Lọc Cố Đô",
                    "Nhận bánh bột lọc nóng hổi tại quán bà Đỏ và giao đến điểm hẹn tại bờ sông Hương.",
                    MissionType.Delivery, 50000, 90f,
                    new Vector3(14.0f, 0.25f, 12.0f), new Vector3(-14.0f, 0.25f, -15.0f), 3.5f);
                AssetDatabase.CreateAsset(m1, m1Path);
            }
            missions.Add(m1);

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
    }
}
#endif
