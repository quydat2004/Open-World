#if UNITY_EDITOR
#pragma warning disable CS0618
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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HueCity.EditorTools
{
    /// <summary>
    /// Trình dựng Scene Lát cắt dọc chi tiết (Vertical Slice Scene Builder) cho TP. Huế - Giai đoạn 3.
    /// Tích hợp toàn diện Đợt nâng cấp đồ họa chân thực (Realism Overhaul):
    /// - Hạng mục 4: URP ACES Tonemapping, Bloom, Color Adjustments, SSAO, Procedural Skybox, Nắng chiều Huế 45° ấm (#FFF4E0).
    /// - Hạng mục 2: Tách rõ mặt đường nhựa PBR (Y=0.0m), vỉa hè nâng cao (Y=+0.15m, gờ bó vỉa đá granite), vạch kẻ đường PBR (Y=+0.005m).
    /// - Phương tiện, props văn hóa vỉa hè, dây điện chằng chịt, biển tên đường, 18 NPC FSM thông minh.
    /// </summary>
    public static class Phase3SceneBuilder
    {
        [InitializeOnLoadMethod]
        private static void AutoEnsureSceneBuiltOnCompile()
        {
            EditorApplication.playModeStateChanged += (PlayModeStateChange state) =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.delayCall += () =>
                    {
                        Debug.Log("[HueCity] Đã chuyển về Edit Mode. Tự động đồng bộ Scene Giai đoạn 3...");
                        BuildScene(true);
                    };
                }
            };

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying)
                {
                    Debug.Log("[HueCity] Phát hiện Unity đang ở Play Mode. Đang dừng Play Mode để cập nhật Scene...");
                    EditorApplication.isPlaying = false;
                    return;
                }

                Debug.Log("[HueCity] Tự động đồng bộ và xây dựng Scene Lát cắt dọc Giai đoạn 3 (Master Realism)...");
                BuildScene(true);
            };
        }

        [MenuItem("HueCity/Build Phase 3 Vertical Slice Scene", false, 30)]
        public static void BuildScene()
        {
            BuildScene(false);
        }

        public static void BuildScene(bool silent)
        {
            // 0. Đảm bảo toàn bộ URP Asset, Volume Profile, Skybox & PBR Materials đã sẵn sàng
            RealismOverhaulSetup.EnsureFullSetup();

            // 1. Tạo scene mới
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Thiết lập Ánh sáng Điện ảnh & Chu kỳ Ngày/Đêm (Hạng mục 4)
            GameObject lightObj = new GameObject("Directional Light - Hue Sun");
            Light sunLight = lightObj.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            // Nắng vàng ấm đặc trưng miền Trung (#FFF4E0)
            sunLight.color = new Color(1.0f, 0.957f, 0.878f);
            sunLight.intensity = 1.35f;
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowResolution = LightShadowResolution.VeryHigh;
            // Góc chiếu nghiêng khoảng 45 độ theo đúng yêu cầu
            lightObj.transform.rotation = Quaternion.Euler(45f, -38f, 0f);

            DayNightCycle dayNight = lightObj.AddComponent<DayNightCycle>();
            dayNight.SetTimeOfDay(17.0f); // Bắt đầu ở Giờ Vàng (Golden Hour) hoàng hôn Huế

            // Gán Mặt trời và Procedural Skybox vào RenderSettings
            Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Skybox_Procedural.mat");
            if (skyboxMat != null)
            {
                RenderSettings.skybox = skyboxMat;
            }
            RenderSettings.sun = sunLight;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

            // 3. Thiết lập Global Volume (Post-Processing) URP (Hạng mục 4)
            SetupGlobalPostProcessingVolume();

            // 4. Hệ thống Quản lý Giác quan thế giới
            GameObject stimulusObj = new GameObject("WorldStimulusManager");
            stimulusObj.AddComponent<WorldStimulusManager>();

            // 5. Đưa Model Lát cắt dọc chi tiết (Ngã tư, Nhà ống, Biệt thự Pháp, Đèn, Cây xanh) vào Scene
            string detailedFbxPath = "Assets/_Project/Models/Hue_Vertical_Slice_Detailed.fbx";
            GameObject detailedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(detailedFbxPath);

            GameObject intersectionInstance = null;
            if (detailedPrefab != null)
            {
                intersectionInstance = (GameObject)PrefabUtility.InstantiatePrefab(detailedPrefab);
                intersectionInstance.name = "Hue_Intersection_VerticalSlice";
                intersectionInstance.transform.position = Vector3.zero;

                // Gán MeshCollider và thiết lập Static cho NavMesh
                SetupCollidersAndNavigation(intersectionInstance);
            }
            else
            {
                Debug.LogWarning($"[HueCity] Chưa tìm thấy model tại {detailedFbxPath}");
            }

            // 6. Xây dựng Cấu trúc Đường & Vỉa hè có Chiều sâu PBR (Hạng mục 2)
            SetupPBRRoadAndSidewalkDepth(intersectionInstance);

            // 6b. Bố trí Dãy Nhà phố Modular Chân thực 3 tầng (House_Modular_01.fbx)
            SpawnModularShophouses();

            // 7. Bake NavMesh tự động cho toàn bộ vỉa hè, vạch đi bộ và lòng đường ngã tư
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            Debug.Log("[HueCity] Đã bake xong NavMesh cho Ngã tư Lát cắt dọc!");

            // 8. Tạo Nhân vật (Player) đi bộ trên vỉa hè
            GameObject player = CreatePlayerObject(new Vector3(13f, 1.2f, 13f));
            player.tag = "Player";

            // 9. Tạo Xe Taxi Huế (Xanh SM) dừng trước vạch đèn đỏ
            GameObject taxi = CreateTaxiObject(new Vector3(3.5f, 0.5f, 18f));

            // 10. Đặt các phương tiện văn hóa và quán cà phê vỉa hè
            SpawnStreetCultureProps();

            // 11. Dây điện chằng chịt đô thị & Biển báo giao thông đường phố Huế
            SetupUrbanStreetCablesAndSigns();

            // 12. Tạo Đám đông NPC (18 NPC đi dạo, qua vạch đi bộ, ngồi quán nước)
            SetupNPCCrowd(player);

            // 13. Thiết lập Cinemachine Camera & URP Camera Post-Processing
            CreateCameraSetup(player, taxi);

            // 14. Lưu Scene vào Assets/_Project/Scenes/Phase3_VerticalSlice.unity
            string scenePath = "Assets/_Project/Scenes/Phase3_VerticalSlice.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>[HueCity]</color> Đã tạo thành công Scene Lát cắt dọc chi tiết (Realism Overhaul) tại: {scenePath}");
        }

        private static void SetupGlobalPostProcessingVolume()
        {
            GameObject volumeObj = new GameObject("Global_PostProcessing_Volume");
            Volume volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1.0f;

            string profilePath = "Assets/_Project/Settings/Hue_PostProcessing_Profile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile != null)
            {
                volume.sharedProfile = profile;
                Debug.Log("[HueCity] Đã liên kết Hue_PostProcessing_Profile (ACES, Bloom, SSAO, Color Adjustments) vào Global Volume!");
            }
        }

        private static void SetupPBRRoadAndSidewalkDepth(GameObject root)
        {
            Material roadMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Road_Asphalt.mat");
            Material sidewalkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Sidewalk_Concrete.mat");
            Material curbMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Curb_Stone.mat");
            Material whiteLineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_RoadMarking_White.mat");
            Material yellowLineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_RoadMarking_Yellow.mat");

            // 1. Gán Material PBR vào Mesh trong Model FBX nếu có
            if (root != null)
            {
                MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var mr in renderers)
                {
                    string name = mr.gameObject.name;
                    if (name.Contains("Road") && roadMat != null)
                    {
                        mr.sharedMaterial = roadMat;
                    }
                    else if (name.Contains("Sidewalk") && sidewalkMat != null)
                    {
                        mr.sharedMaterial = sidewalkMat;
                    }
                    else if (name.Contains("Zebra") || name.Contains("Marking"))
                    {
                        mr.sharedMaterial = whiteLineMat;
                    }
                }
            }

            // 2. Tạo hệ thống Khối hình học Đường sá & Vỉa hè chuẩn phân tầng chiều sâu:
            // - Lòng đường nhựa: Y = 0.0m
            // - Vỉa hè nâng cao: Y = +0.15m (với gờ bó vỉa đá granite)
            // - Vạch kẻ đường: Y = +0.005m (5mm trên mặt đường nhựa, triệt tiêu hoàn toàn Z-fighting)
            GameObject roadSystem = new GameObject("Road_And_Sidewalk_System_PBR");

            // A. Mặt đường nhựa (Asphalt Road Surface) tại Y = 0.0m
            GameObject roadPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            roadPlane.name = "PBR_Asphalt_Roadway";
            roadPlane.transform.SetParent(roadSystem.transform);
            roadPlane.transform.position = new Vector3(0f, 0.0f, 0f);
            roadPlane.transform.localScale = new Vector3(7.0f, 1.0f, 7.0f); // 70m x 70m ngã tư
            if (roadMat != null) roadPlane.GetComponent<Renderer>().sharedMaterial = roadMat;
            GameObjectUtility.SetStaticEditorFlags(roadPlane, StaticEditorFlags.NavigationStatic);

            // B. Bốn khối Vỉa hè nâng cao Y = +0.15m với Gờ bó vỉa Granite
            Vector3[] sidewalkCenters = new Vector3[]
            {
                new Vector3(21.0f, 0.075f, 21.0f),   // Đông Bắc
                new Vector3(-21.0f, 0.075f, 21.0f),  // Tây Bắc
                new Vector3(21.0f, 0.075f, -21.0f),  // Đông Nam
                new Vector3(-21.0f, 0.075f, -21.0f)  // Tây Nam
            };

            for (int i = 0; i < sidewalkCenters.Length; i++)
            {
                GameObject sidewalkBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sidewalkBox.name = $"Raised_Sidewalk_{i}";
                sidewalkBox.transform.SetParent(roadSystem.transform);
                sidewalkBox.transform.position = sidewalkCenters[i];
                sidewalkBox.transform.localScale = new Vector3(28.0f, 0.15f, 28.0f);
                if (sidewalkMat != null) sidewalkBox.GetComponent<Renderer>().sharedMaterial = sidewalkMat;
                GameObjectUtility.SetStaticEditorFlags(sidewalkBox, StaticEditorFlags.NavigationStatic);

                // Gờ bó vỉa đá Granite dọc theo mép trong tiếp giáp mặt đường
                CreateCurbBorders(roadSystem.transform, sidewalkCenters[i], curbMat);
            }

            // C. Vạch kẻ đường (Road Markings) tại Y = +0.005m (5mm)
            CreateRoadMarkings(roadSystem.transform, whiteLineMat, yellowLineMat);
        }

        private static void CreateCurbBorders(Transform parent, Vector3 center, Material curbMat)
        {
            float signX = Mathf.Sign(center.x);
            float signZ = Mathf.Sign(center.z);

            // Gờ bó vỉa cạnh X
            GameObject curbX = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curbX.name = "Curb_Border_X";
            curbX.transform.SetParent(parent);
            curbX.transform.position = new Vector3(center.x, 0.08f, center.z - signZ * 14.1f);
            curbX.transform.localScale = new Vector3(28.0f, 0.16f, 0.25f);
            if (curbMat != null) curbX.GetComponent<Renderer>().sharedMaterial = curbMat;

            // Gờ bó vỉa cạnh Z
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

            float markY = 0.005f; // Cao hơn mặt đường 5mm để không bị nhấp nháy Z-fighting

            // 1. Vạch đôi vàng phân làn trung tâm (Double Yellow Center Line)
            // Nhánh Bắc (Z: 7m -> 35m)
            CreateCenterYellowLine(markingsParent.transform, new Vector3(0f, markY, 21.0f), 28.0f, false, yellowMat);
            // Nhánh Nam (Z: -7m -> -35m)
            CreateCenterYellowLine(markingsParent.transform, new Vector3(0f, markY, -21.0f), 28.0f, false, yellowMat);
            // Nhánh Đông (X: 7m -> 35m)
            CreateCenterYellowLine(markingsParent.transform, new Vector3(21.0f, markY, 0f), 28.0f, true, yellowMat);
            // Nhánh Tây (X: -7m -> -35m)
            CreateCenterYellowLine(markingsParent.transform, new Vector3(-21.0f, markY, 0f), 28.0f, true, yellowMat);

            // 2. Vạch dừng xe (Stop Line) màu trắng trước ngã tư
            CreateStopLine(markingsParent.transform, new Vector3(0f, markY, 8.5f), 13.0f, true, whiteMat);
            CreateStopLine(markingsParent.transform, new Vector3(0f, markY, -8.5f), 13.0f, true, whiteMat);
            CreateStopLine(markingsParent.transform, new Vector3(8.5f, markY, 0f), 13.0f, false, whiteMat);
            CreateStopLine(markingsParent.transform, new Vector3(-8.5f, markY, 0f), 13.0f, false, whiteMat);

            // 3. Vạch qua đường cho người đi bộ (Zebra Crossings) tại 4 nhánh
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
            if (housePrefab == null)
            {
                Debug.LogWarning($"[HueCity] Chưa tìm thấy prefab nhà modular tại {houseFbxPath}");
                return;
            }

            GameObject houseGroup = new GameObject("Modular_Shophouses_Blocks");

            // Bố trí các căn nhà phố dọc theo mép lùi vỉa hè (mỗi căn rộng 5m, sâu 10m, cao 10.5m)
            // Hướng mặt tiền quay ra trục đường chính (trục X và Z)
            var housePlacements = new (Vector3 pos, float rotY)[]
            {
                // Khu phố Đông Bắc (mặt tiền hướng Nam về phía lòng đường)
                (new Vector3(15.0f, 0.15f, 18.0f), 0f),
                (new Vector3(20.5f, 0.15f, 18.0f), 0f),
                (new Vector3(26.0f, 0.15f, 18.0f), 0f),

                // Khu phố Tây Bắc (mặt tiền hướng Nam về phía lòng đường)
                (new Vector3(-15.0f, 0.15f, 18.0f), 0f),
                (new Vector3(-20.5f, 0.15f, 18.0f), 0f),

                // Khu phố Đông Nam (mặt tiền hướng Bắc về phía lòng đường)
                (new Vector3(15.0f, 0.15f, -18.0f), 180f),
                (new Vector3(20.5f, 0.15f, -18.0f), 180f),

                // Khu phố Tây Nam (mặt tiền hướng Bắc về phía lòng đường)
                (new Vector3(-15.0f, 0.15f, -18.0f), 180f),
                (new Vector3(-20.5f, 0.15f, -18.0f), 180f)
            };

            for (int i = 0; i < housePlacements.Length; i++)
            {
                var p = housePlacements[i];
                GameObject house = (GameObject)PrefabUtility.InstantiatePrefab(housePrefab);
                house.name = $"House_Modular_{i + 1:D2}";
                house.transform.SetParent(houseGroup.transform);
                house.transform.position = p.pos;
                house.transform.rotation = Quaternion.Euler(0f, p.rotY, 0f);

                // Gán MeshCollider và đánh dấu Static để bake NavMesh và cản nhân vật
                MeshFilter[] mfs = house.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in mfs)
                {
                    if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    {
                        MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                    }
                    GameObjectUtility.SetStaticEditorFlags(mf.gameObject, StaticEditorFlags.NavigationStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                }
            }

            Debug.Log($"[HueCity] Đã bố trí {housePlacements.Length} căn Nhà phố Modular 3 tầng (House_Modular_01.fbx) vào các góc phố!");
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

            string charFbxPath = "Assets/_Project/Models/Character_Player.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(charFbxPath) == null)
            {
                charFbxPath = "Assets/_Project/Models/Character_Humanoid_Rigged.fbx";
            }
            GameObject charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(charFbxPath);
            GameObject visualObj = null;

            if (charPrefab != null)
            {
                visualObj = (GameObject)PrefabUtility.InstantiatePrefab(charPrefab);
                visualObj.name = "Visual_Humanoid";
                visualObj.transform.SetParent(player.transform);
                visualObj.transform.localPosition = Vector3.zero;
                visualObj.transform.localRotation = Quaternion.identity;

                // Gắn Animator & Controller với Blend Tree mượt mà (Hạng mục 1)
                Animator animator = visualObj.GetComponent<Animator>();
                if (animator == null) animator = visualObj.AddComponent<Animator>();

                RuntimeAnimatorController animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Animations/Player_AnimatorController.controller");
                if (animController != null)
                {
                    animator.runtimeAnimatorController = animController;
                }

                // Cấu hình Avatar từ Humanoid Model
                Avatar avatar = null;
                Animator prefabAnim = charPrefab.GetComponent<Animator>();
                if (prefabAnim != null && prefabAnim.avatar != null && prefabAnim.avatar.isValid)
                {
                    avatar = prefabAnim.avatar;
                }
                if (avatar == null)
                {
                    UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(charFbxPath);
                    foreach (var a in subAssets)
                    {
                        if (a is Avatar av && av.isValid)
                        {
                            avatar = av;
                            break;
                        }
                    }
                }
                if (avatar != null)
                {
                    animator.avatar = avatar;
                }
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

                // Gán vật liệu sơn bóng và đèn pha URP Lit
                Material carPaint = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Vehicle_Paint.mat");
                if (carPaint != null)
                {
                    var renderers = taxiVisual.GetComponentsInChildren<MeshRenderer>();
                    foreach (var mr in renderers)
                    {
                        if (!mr.gameObject.name.Contains("Wheel") && !mr.gameObject.name.Contains("Glass"))
                        {
                            mr.sharedMaterial = carPaint;
                        }
                    }
                }

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

            Material cableMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            cableMat.color = new Color(0.08f, 0.08f, 0.08f);
            cableMat.SetFloat("_Smoothness", 0.1f);

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

            Material poleMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            poleMat.color = new Color(0.72f, 0.74f, 0.78f);
            poleMat.SetFloat("_Metallic", 0.9f);
            poleMat.SetFloat("_Smoothness", 0.4f);
            pole.GetComponent<Renderer>().material = poleMat;

            // Biển tên đường chữ nhật màu xanh biển báo
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Plate";
            plate.transform.SetParent(sign.transform);
            plate.transform.localPosition = new Vector3(0f, 2.65f, 0f);
            plate.transform.localScale = new Vector3(0.85f, 0.28f, 0.04f);

            Material plateMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            plateMat.color = new Color(0.06f, 0.28f, 0.72f); // Xanh dương biển chỉ dẫn
            plateMat.SetFloat("_Smoothness", 0.5f);
            plate.GetComponent<Renderer>().material = plateMat;
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
            UnityEngine.Camera cam = mainCamObj.AddComponent<UnityEngine.Camera>();
            mainCamObj.AddComponent<AudioListener>();

            // Bật URP Post-Processing trên Main Camera
            UniversalAdditionalCameraData camData = mainCamObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

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
