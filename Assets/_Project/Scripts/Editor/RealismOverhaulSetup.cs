#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HueCity.EditorTools
{
    /// <summary>
    /// Bộ cài đặt & cấu hình Toàn diện Hạng mục 4 (Lighting & Post-Processing URP), Hạng mục 2 (Đường sá PBR),
    /// Hạng mục 1 (Sửa Nhân vật: Humanoid Rig, Blend Tree, Animator Controller) và Hạng mục 3 (Vật liệu xe cộ & đèn pha).
    /// </summary>
    [InitializeOnLoad]
    public static class RealismOverhaulSetup
    {
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const string MaterialsFolder = "Assets/_Project/Materials";
        private const string AnimationsFolder = "Assets/_Project/Animations";
        private const string AnimationClipsFolder = "Assets/_Project/Animations/Clips";
        private const string UrpAssetPath = "Assets/_Project/Settings/URP_HighFidelity.asset";
        private const string UrpRendererPath = "Assets/_Project/Settings/URP_HighFidelity_Renderer.asset";
        private const string PostProfilePath = "Assets/_Project/Settings/Hue_PostProcessing_Profile.asset";
        private const string SkyboxMatPath = "Assets/_Project/Materials/MAT_Skybox_Procedural.mat";
        private const string PlayerAnimControllerPath = "Assets/_Project/Animations/Player_AnimatorController.controller";

        static RealismOverhaulSetup()
        {
            EditorApplication.delayCall += EnsureFullSetup;
        }

        [MenuItem("HueCity/4. Configure URP, Lighting & PBR Materials", false, 4)]
        public static void EnsureFullSetupMenu()
        {
            EnsureFullSetup();
            Debug.Log("<color=green>[HueCity Realism Overhaul]</color> Đã thiết lập hoàn tất URP, SSAO, Post-Processing, Blend Tree và PBR Materials!");
        }

        private static bool isSettingUp = false;

        public static void EnsureFullSetup()
        {
            if (isSettingUp) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            try
            {
                isSettingUp = true;
                EnsureDirectories();
                UniversalRenderPipelineAsset urpAsset = SetupURPAssetsAndRenderer();
                SetupPostProcessingProfile();
                SetupProceduralSkybox();
                SetupPBRMaterials();
                SetupVehicleMaterials();
                SetupShophouseMaterials();
                SetupHumanoidCharacterImporter();
                SetupPlayerAnimatorController();

                // Kích hoạt URP cho toàn bộ GraphicsSettings và QualitySettings
                if (urpAsset != null)
                {
                    if (GraphicsSettings.renderPipelineAsset != urpAsset)
                    {
                        GraphicsSettings.renderPipelineAsset = urpAsset;
                        Debug.Log("[HueCity] Đã gán URP_HighFidelity vào GraphicsSettings.renderPipelineAsset.");
                    }
                    if (QualitySettings.renderPipeline != urpAsset)
                    {
                        QualitySettings.renderPipeline = urpAsset;
                        Debug.Log("[HueCity] Đã gán URP_HighFidelity vào QualitySettings.renderPipeline.");
                    }
                }

                // Tự động dựng mới nhất Scene Lát cắt dọc chi tiết (Phase3_VerticalSlice.unity)
                Phase3SceneBuilder.BuildScene(true);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                isSettingUp = false;
            }
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(SettingsFolder)) Directory.CreateDirectory(SettingsFolder);
            if (!Directory.Exists(MaterialsFolder)) Directory.CreateDirectory(MaterialsFolder);
            if (!Directory.Exists(AnimationsFolder)) Directory.CreateDirectory(AnimationsFolder);
            if (!Directory.Exists(AnimationClipsFolder)) Directory.CreateDirectory(AnimationClipsFolder);
        }

        private static void SafeAddSubAsset(UnityEngine.Object subAsset, UnityEngine.Object mainAsset)
        {
            if (subAsset != null && mainAsset != null)
            {
                if (!AssetDatabase.Contains(subAsset))
                {
                    AssetDatabase.AddObjectToAsset(subAsset, mainAsset);
                }
            }
        }

        private static UniversalRenderPipelineAsset SetupURPAssetsAndRenderer()
        {
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UrpRendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                rendererData.name = "URP_HighFidelity_Renderer";
                AssetDatabase.CreateAsset(rendererData, UrpRendererPath);
                Debug.Log($"[HueCity] Đã tạo UniversalRendererData tại {UrpRendererPath}");
            }

            // Kích hoạt Ambient Occlusion (SSAO) trong Renderer Feature
            EnsureSSAOFeature(rendererData);

            UniversalRenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urpAsset == null)
            {
                urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
                urpAsset.name = "URP_HighFidelity";
                AssetDatabase.CreateAsset(urpAsset, UrpAssetPath);
                Debug.Log($"[HueCity] Đã tạo UniversalRenderPipelineAsset tại {UrpAssetPath}");
            }

            // Cấu hình thông số đồ họa chất lượng cao
            urpAsset.supportsHDR = true;
            urpAsset.shadowDistance = 75.0f;
            urpAsset.msaaSampleCount = 4;
            urpAsset.renderScale = 1.0f;
            urpAsset.supportsCameraDepthTexture = true;
            urpAsset.supportsCameraOpaqueTexture = true;

            EditorUtility.SetDirty(urpAsset);
            EditorUtility.SetDirty(rendererData);
            return urpAsset;
        }

        private static void EnsureSSAOFeature(UniversalRendererData rendererData)
        {
            FieldInfo featuresField = typeof(ScriptableRendererData).GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
            if (featuresField != null)
            {
                var features = featuresField.GetValue(rendererData) as List<ScriptableRendererFeature>;
                if (features != null)
                {
                    bool hasSSAO = features.Exists(f => f != null && f.GetType().Name.Contains("ScreenSpaceAmbientOcclusion"));
                    if (!hasSSAO)
                    {
                        System.Type ssaoType = System.Type.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime");
                        if (ssaoType != null)
                        {
                            var ssao = ScriptableObject.CreateInstance(ssaoType) as ScriptableRendererFeature;
                            if (ssao != null)
                            {
                                ssao.name = "ScreenSpaceAmbientOcclusion";
                                AssetDatabase.AddObjectToAsset(ssao, rendererData);
                                features.Add(ssao);
                                rendererData.SetDirty();
                                Debug.Log("[HueCity] Đã thêm ScreenSpaceAmbientOcclusion (SSAO) vào URP Renderer Feature!");
                            }
                        }
                    }
                }
            }
        }

        public static VolumeProfile SetupPostProcessingProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Hue_PostProcessing_Profile";
                AssetDatabase.CreateAsset(profile, PostProfilePath);
                Debug.Log($"[HueCity] Đã tạo VolumeProfile tại {PostProfilePath}");
            }

            // 1. ACES Tonemapping
            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
                SafeAddSubAsset(tonemapping, profile);
            }
            tonemapping.mode.Override(TonemappingMode.ACES);

            // 2. Bloom (0.2 - 0.4)
            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
                SafeAddSubAsset(bloom, profile);
            }
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.0f);
            bloom.scatter.Override(0.70f);

            // 3. Color Adjustments: Contrast +10, Saturation +5
            if (!profile.TryGet(out ColorAdjustments colorAdj))
            {
                colorAdj = profile.Add<ColorAdjustments>(true);
                SafeAddSubAsset(colorAdj, profile);
            }
            colorAdj.contrast.Override(10.0f);
            colorAdj.saturation.Override(5.0f);
            colorAdj.postExposure.Override(0.12f);

            // 4. White Balance: Nắng chiều Cố Đô ấm áp
            if (!profile.TryGet(out WhiteBalance whiteBalance))
            {
                whiteBalance = profile.Add<WhiteBalance>(true);
                SafeAddSubAsset(whiteBalance, profile);
            }
            whiteBalance.temperature.Override(5.5f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        public static Material SetupProceduralSkybox()
        {
            Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMatPath);
            Shader skyboxShader = Shader.Find("Skybox/Procedural");
            if (skyboxShader == null) skyboxShader = Shader.Find("Mobile/Skybox");

            if (skyboxMat == null)
            {
                skyboxMat = new Material(skyboxShader);
                skyboxMat.name = "MAT_Skybox_Procedural";
                AssetDatabase.CreateAsset(skyboxMat, SkyboxMatPath);
            }
            else
            {
                skyboxMat.shader = skyboxShader;
            }

            skyboxMat.SetFloat("_SunSize", 0.04f);
            skyboxMat.SetFloat("_SunSizeConvergence", 5.0f);
            skyboxMat.SetFloat("_AtmosphereThickness", 0.8f);
            skyboxMat.SetColor("_SkyTint", new Color(0.498f, 0.710f, 0.902f)); // #7FB5E6 xanh da trời tự nhiên
            skyboxMat.SetColor("_GroundColor", new Color(0.431f, 0.471f, 0.502f)); // #6E7880 xám xanh chân trời
            skyboxMat.SetFloat("_Exposure", 1.1f);

            RenderSettings.skybox = skyboxMat;
            EditorUtility.SetDirty(skyboxMat);
            return skyboxMat;
        }

        public static void SetupPBRMaterials()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            // 1. Mặt đường Nhựa PBR (Asphalt)
            string roadMatPath = "Assets/_Project/Materials/MAT_Road_Asphalt.mat";
            Material roadMat = AssetDatabase.LoadAssetAtPath<Material>(roadMatPath);
            if (roadMat == null)
            {
                roadMat = new Material(litShader);
                roadMat.name = "MAT_Road_Asphalt";
                AssetDatabase.CreateAsset(roadMat, roadMatPath);
            }
            Texture2D roadDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/PBR/Road_Asphalt/asphalt_02_diff_1k.jpg");
            Texture2D roadNor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/PBR/Road_Asphalt/asphalt_02_nor_1k.jpg");
            if (roadDiff != null) roadMat.SetTexture("_BaseMap", roadDiff);
            roadMat.SetTextureScale("_BaseMap", new Vector2(8f, 8f));
            if (roadNor != null)
            {
                roadMat.SetTexture("_BumpMap", roadNor);
                roadMat.SetTextureScale("_BumpMap", new Vector2(8f, 8f));
                roadMat.SetFloat("_BumpScale", 1.0f);
                roadMat.EnableKeyword("_NORMALMAP");
            }
            roadMat.SetFloat("_Smoothness", 0.35f);
            roadMat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f));
            EditorUtility.SetDirty(roadMat);

            // 2. Vỉa hè Bê tông PBR (Concrete Sidewalk)
            string sidewalkMatPath = "Assets/_Project/Materials/MAT_Sidewalk_Concrete.mat";
            Material sidewalkMat = AssetDatabase.LoadAssetAtPath<Material>(sidewalkMatPath);
            if (sidewalkMat == null)
            {
                sidewalkMat = new Material(litShader);
                sidewalkMat.name = "MAT_Sidewalk_Concrete";
                AssetDatabase.CreateAsset(sidewalkMat, sidewalkMatPath);
            }
            Texture2D sideDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/PBR/Sidewalk_Concrete/concrete_pavement_diff_1k.jpg");
            Texture2D sideNor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/PBR/Sidewalk_Concrete/concrete_pavement_nor_1k.jpg");
            if (sideDiff != null) sidewalkMat.SetTexture("_BaseMap", sideDiff);
            sidewalkMat.SetTextureScale("_BaseMap", new Vector2(6f, 6f));
            if (sideNor != null)
            {
                sidewalkMat.SetTexture("_BumpMap", sideNor);
                sidewalkMat.SetTextureScale("_BumpMap", new Vector2(6f, 6f));
                sidewalkMat.SetFloat("_BumpScale", 0.8f);
                sidewalkMat.EnableKeyword("_NORMALMAP");
            }
            sidewalkMat.SetFloat("_Smoothness", 0.25f);
            sidewalkMat.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.92f));
            EditorUtility.SetDirty(sidewalkMat);

            // 3. Gờ bó vỉa đá Granite (Curb Stone)
            string curbMatPath = "Assets/_Project/Materials/MAT_Curb_Stone.mat";
            Material curbMat = AssetDatabase.LoadAssetAtPath<Material>(curbMatPath);
            if (curbMat == null)
            {
                curbMat = new Material(litShader);
                curbMat.name = "MAT_Curb_Stone";
                AssetDatabase.CreateAsset(curbMat, curbMatPath);
            }
            curbMat.SetColor("_BaseColor", new Color(0.40f, 0.40f, 0.43f));
            curbMat.SetFloat("_Smoothness", 0.40f);
            EditorUtility.SetDirty(curbMat);

            // 4. Vạch kẻ đường Trắng (White Road Marking & Zebra)
            string whiteLinePath = "Assets/_Project/Materials/MAT_RoadMarking_White.mat";
            Material whiteLineMat = AssetDatabase.LoadAssetAtPath<Material>(whiteLinePath);
            if (whiteLineMat == null)
            {
                whiteLineMat = new Material(litShader);
                whiteLineMat.name = "MAT_RoadMarking_White";
                AssetDatabase.CreateAsset(whiteLineMat, whiteLinePath);
            }
            whiteLineMat.SetColor("_BaseColor", new Color(0.96f, 0.96f, 0.96f));
            whiteLineMat.SetFloat("_Smoothness", 0.20f);
            EditorUtility.SetDirty(whiteLineMat);

            // 5. Vạch phân làn kép Vàng (Yellow Road Marking)
            string yellowLinePath = "Assets/_Project/Materials/MAT_RoadMarking_Yellow.mat";
            Material yellowLineMat = AssetDatabase.LoadAssetAtPath<Material>(yellowLinePath);
            if (yellowLineMat == null)
            {
                yellowLineMat = new Material(litShader);
                yellowLineMat.name = "MAT_RoadMarking_Yellow";
                AssetDatabase.CreateAsset(yellowLineMat, yellowLinePath);
            }
            yellowLineMat.SetColor("_BaseColor", new Color(0.96f, 0.74f, 0.12f));
            yellowLineMat.SetFloat("_Smoothness", 0.25f);
            yellowLineMat.enableInstancing = true;
            EditorUtility.SetDirty(yellowLineMat);

            // 6. Mặt đất mở rộng Cỏ PBR (Terrain Grass 500m x 500m)
            string grassMatPath = "Assets/_Project/Materials/MAT_Terrain_Grass.mat";
            Material grassMat = AssetDatabase.LoadAssetAtPath<Material>(grassMatPath);
            if (grassMat == null)
            {
                grassMat = new Material(litShader);
                grassMat.name = "MAT_Terrain_Grass";
                AssetDatabase.CreateAsset(grassMat, grassMatPath);
            }
            Texture2D grassDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/PBR/Terrain_Grass/grass_diff_1k.jpg");
            Texture2D grassNor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/PBR/Terrain_Grass/grass_nor_1k.jpg");
            if (grassDiff != null) grassMat.SetTexture("_BaseMap", grassDiff);
            grassMat.SetTextureScale("_BaseMap", new Vector2(50f, 50f));
            if (grassNor != null)
            {
                grassMat.SetTexture("_BumpMap", grassNor);
                grassMat.SetTextureScale("_BumpMap", new Vector2(50f, 50f));
                grassMat.SetFloat("_BumpScale", 1.0f);
                grassMat.EnableKeyword("_NORMALMAP");
            }
            grassMat.SetFloat("_Smoothness", 0.15f);
            grassMat.SetFloat("_Metallic", 0.0f);
            grassMat.SetColor("_BaseColor", new Color(0.92f, 0.96f, 0.88f));
            grassMat.enableInstancing = true;
            EditorUtility.SetDirty(grassMat);

            // 7. Vật liệu Tán lá Cây cối & Hoa cỏ đô thị (Foliage Cards)
            string foliageMatPath = "Assets/_Project/Materials/MAT_Foliage_Leaves.mat";
            Material foliageMat = AssetDatabase.LoadAssetAtPath<Material>(foliageMatPath);
            if (foliageMat == null)
            {
                foliageMat = new Material(litShader);
                foliageMat.name = "MAT_Foliage_Leaves";
                AssetDatabase.CreateAsset(foliageMat, foliageMatPath);
            }
            Texture2D leafDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/PBR/Foliage/leaves_diff_512.png");
            if (leafDiff != null) foliageMat.SetTexture("_BaseMap", leafDiff);
            foliageMat.SetFloat("_Cutoff", 0.35f);
            foliageMat.SetFloat("_Surface", 0.0f); // Opaque with cutout or alpha test
            foliageMat.EnableKeyword("_ALPHATEST_ON");
            foliageMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            foliageMat.SetFloat("_Smoothness", 0.20f);
            foliageMat.SetFloat("_Cull", 0.0f); // Double-sided
            foliageMat.enableInstancing = true;
            EditorUtility.SetDirty(foliageMat);

            // 8. Vật liệu Thân gỗ Cây xanh (Tree Bark)
            string barkMatPath = "Assets/_Project/Materials/MAT_Tree_Bark.mat";
            Material barkMat = AssetDatabase.LoadAssetAtPath<Material>(barkMatPath);
            if (barkMat == null)
            {
                barkMat = new Material(litShader);
                barkMat.name = "MAT_Tree_Bark";
                AssetDatabase.CreateAsset(barkMat, barkMatPath);
            }
            Texture2D barkDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/PBR/Wood_Dark/dark_wood_diff_1k.jpg");
            Texture2D barkNor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/PBR/Wood_Dark/dark_wood_nor_1k.jpg");
            if (barkDiff != null) barkMat.SetTexture("_BaseMap", barkDiff);
            barkMat.SetTextureScale("_BaseMap", new Vector2(2f, 6f));
            if (barkNor != null)
            {
                barkMat.SetTexture("_BumpMap", barkNor);
                barkMat.SetTextureScale("_BumpMap", new Vector2(2f, 6f));
                barkMat.SetFloat("_BumpScale", 1.2f);
                barkMat.EnableKeyword("_NORMALMAP");
            }
            barkMat.SetFloat("_Smoothness", 0.15f);
            barkMat.SetColor("_BaseColor", new Color(0.45f, 0.38f, 0.32f));
            barkMat.enableInstancing = true;
            EditorUtility.SetDirty(barkMat);

            // 9. Biển tên đường Hùng Vương & Lê Lợi
            SetupStreetSignMaterial("MAT_Sign_HungVuong", "Assets/_Project/Textures/PBR/Signs/sign_hung_vuong.png", litShader);
            SetupStreetSignMaterial("MAT_Sign_LeLoi", "Assets/_Project/Textures/PBR/Signs/sign_le_loi.png", litShader);
        }

        private static void SetupStreetSignMaterial(string matName, string texPath, Shader shader)
        {
            string matPath = $"Assets/_Project/Materials/{matName}.mat";
            Material signMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (signMat == null)
            {
                signMat = new Material(shader);
                signMat.name = matName;
                AssetDatabase.CreateAsset(signMat, matPath);
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null) signMat.SetTexture("_BaseMap", tex);
            signMat.SetFloat("_Smoothness", 0.45f);
            signMat.enableInstancing = true;
            EditorUtility.SetDirty(signMat);
        }

        public static void SetupVehicleMaterials()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            // 1. Sơn bóng xe Taxi Vàng Huế (Metallic 0.8, Smoothness 0.85)
            string carPaintPath = "Assets/_Project/Materials/MAT_Vehicle_Paint.mat";
            Material carPaint = AssetDatabase.LoadAssetAtPath<Material>(carPaintPath);
            if (carPaint == null)
            {
                carPaint = new Material(litShader);
                carPaint.name = "MAT_Vehicle_Paint";
                AssetDatabase.CreateAsset(carPaint, carPaintPath);
            }
            carPaint.shader = litShader;
            carPaint.SetColor("_BaseColor", new Color(0.96f, 0.72f, 0.05f)); // Vàng Taxi Huế
            carPaint.SetFloat("_Metallic", 0.80f);
            carPaint.SetFloat("_Smoothness", 0.85f);
            carPaint.enableInstancing = true;
            EditorUtility.SetDirty(carPaint);

            // 2. Kính xe ô tô tối màu phản chiếu cao (Glass Tinted)
            string carGlassPath = "Assets/_Project/Materials/MAT_Vehicle_Glass.mat";
            Material carGlass = AssetDatabase.LoadAssetAtPath<Material>(carGlassPath);
            if (carGlass == null)
            {
                carGlass = new Material(litShader);
                carGlass.name = "MAT_Vehicle_Glass";
                AssetDatabase.CreateAsset(carGlass, carGlassPath);
            }
            carGlass.shader = litShader;
            carGlass.SetColor("_BaseColor", new Color(0.12f, 0.15f, 0.18f, 0.75f));
            carGlass.SetFloat("_Metallic", 0.20f);
            carGlass.SetFloat("_Smoothness", 0.95f);
            carGlass.enableInstancing = true;
            EditorUtility.SetDirty(carGlass);

            // 3. Đèn pha phát sáng (Emission Headlights)
            string headlightPath = "Assets/_Project/Materials/MAT_Vehicle_Headlights.mat";
            Material headlightMat = AssetDatabase.LoadAssetAtPath<Material>(headlightPath);
            if (headlightMat == null)
            {
                headlightMat = new Material(litShader);
                headlightMat.name = "MAT_Vehicle_Headlights";
                AssetDatabase.CreateAsset(headlightMat, headlightPath);
            }
            headlightMat.shader = litShader;
            headlightMat.SetColor("_BaseColor", new Color(1.0f, 0.98f, 0.85f));
            headlightMat.EnableKeyword("_EMISSION");
            headlightMat.SetColor("_EmissionColor", new Color(2.5f, 2.4f, 1.8f)); // Ánh sáng đèn pha phát quang
            headlightMat.enableInstancing = true;
            EditorUtility.SetDirty(headlightMat);

            // 4. Sơn xe máy / xe tay ga (Scooter Body)
            string scooterPaintPath = "Assets/_Project/Materials/MAT_Scooter_Paint.mat";
            Material scooterPaint = AssetDatabase.LoadAssetAtPath<Material>(scooterPaintPath);
            if (scooterPaint == null)
            {
                scooterPaint = new Material(litShader);
                scooterPaint.name = "MAT_Scooter_Paint";
                AssetDatabase.CreateAsset(scooterPaint, scooterPaintPath);
            }
            scooterPaint.shader = litShader;
            scooterPaint.SetColor("_BaseColor", new Color(0.08f, 0.52f, 0.72f)); // Xanh ngọc thể thao
            scooterPaint.SetFloat("_Metallic", 0.70f);
            scooterPaint.SetFloat("_Smoothness", 0.82f);
            scooterPaint.enableInstancing = true;
            EditorUtility.SetDirty(scooterPaint);

            // 5. Da yên xe máy (Scooter Seat Leather)
            string scooterSeatPath = "Assets/_Project/Materials/MAT_Scooter_Seat.mat";
            Material scooterSeat = AssetDatabase.LoadAssetAtPath<Material>(scooterSeatPath);
            if (scooterSeat == null)
            {
                scooterSeat = new Material(litShader);
                scooterSeat.name = "MAT_Scooter_Seat";
                AssetDatabase.CreateAsset(scooterSeat, scooterSeatPath);
            }
            scooterSeat.shader = litShader;
            Texture2D seatLeather = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/PBR/Fabric_Leather/fabric_leather_01_diff_1k.jpg");
            if (seatLeather != null) scooterSeat.SetTexture("_BaseMap", seatLeather);
            scooterSeat.SetColor("_BaseColor", new Color(0.20f, 0.20f, 0.20f));
            scooterSeat.SetFloat("_Metallic", 0.0f);
            scooterSeat.SetFloat("_Smoothness", 0.25f);
            scooterSeat.enableInstancing = true;
            EditorUtility.SetDirty(scooterSeat);
        }

        public static void SetupShophouseMaterials()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // 1. Tường vôi vàng đặc trưng Cố Đô Huế (French colonial ochre / Hue mustard yellow)
            SetupLitMaterial("plastered_wall_02", new Color(0.92f, 0.74f, 0.38f), 0.0f, 0.15f, litShader);

            // 2. Đá hoa cương ốp tầng trệt (Dark Polished Granite)
            SetupLitMaterial("MAT_House_Granite", new Color(0.18f, 0.19f, 0.21f), 0.15f, 0.80f, litShader);

            // 3. Cửa cuốn kim loại tầng trệt (Roller Door)
            SetupLitMaterial("MAT_House_RollerDoor", new Color(0.68f, 0.70f, 0.74f), 0.85f, 0.45f, litShader);

            // 4. Khung kim loại & Ban công sắt đen (Dark Metal Rails & Trims)
            SetupLitMaterial("MAT_House_Metal_Dark", new Color(0.12f, 0.12f, 0.13f), 0.90f, 0.35f, litShader);

            // 5. Kính cửa sổ & ban công phản chiếu (Architectural Glass)
            SetupLitMaterial("MAT_House_Glass", new Color(0.15f, 0.22f, 0.25f, 0.75f), 0.10f, 0.95f, litShader);

            // 6. Biển hiệu kinh doanh shophouse Việt Nam (Signboard)
            string signMatPath = "Assets/_Project/Materials/MAT_House_Sign.mat";
            Material signMat = AssetDatabase.LoadAssetAtPath<Material>(signMatPath);
            if (signMat == null)
            {
                signMat = new Material(litShader);
                signMat.name = "MAT_House_Sign";
                AssetDatabase.CreateAsset(signMat, signMatPath);
            }
            Texture2D signTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/PBR/Shophouse/shophouse_sign_01.jpg");
            if (signTex != null) signMat.SetTexture("_BaseMap", signTex);
            signMat.SetFloat("_Smoothness", 0.40f);
            signMat.enableInstancing = true;
            EditorUtility.SetDirty(signMat);

            // 7. Bồn nước Inox mái nhà (Rooftop Water Tank)
            SetupLitMaterial("MAT_House_WaterTank", new Color(0.85f, 0.88f, 0.92f), 0.95f, 0.75f, litShader);

            // 8. Cục nóng máy lạnh (AC Outdoor Condenser Unit)
            SetupLitMaterial("MAT_House_AC", new Color(0.90f, 0.90f, 0.88f), 0.05f, 0.28f, litShader);
        }

        private static Material SetupLitMaterial(string matName, Color color, float metallic, float smoothness, Shader shader)
        {
            string path = $"Assets/_Project/Materials/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = matName;
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void SetupHumanoidCharacterImporter()
        {
            string[] fbxPaths = new string[]
            {
                "Assets/_Project/Models/Character_Player.fbx",
                "Assets/_Project/Models/Character_Humanoid_Rigged.fbx"
            };

            foreach (var fbxPath in fbxPaths)
            {
                ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer != null)
                {
                    bool changed = false;
                    if (importer.animationType != ModelImporterAnimationType.Human)
                    {
                        importer.animationType = ModelImporterAnimationType.Human;
                        changed = true;
                    }
                    if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                    {
                        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                        changed = true;
                    }
                    if (changed)
                    {
                        importer.SaveAndReimport();
                        Debug.Log($"[HueCity] Đã cấu hình ModelImporter sang Humanoid Rig cho {fbxPath}!");
                    }
                }
            }
        }

        public static AnimatorController SetupPlayerAnimatorController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerAnimControllerPath);
                Debug.Log($"[HueCity] Đã tạo Animator Controller tại {PlayerAnimControllerPath}");
            }

            // Đảm bảo đủ các tham số điều khiển
            bool hasSpeed = false, hasGrounded = false, hasJump = false;
            foreach (var p in controller.parameters)
            {
                if (p.name == "Speed") hasSpeed = true;
                if (p.name == "IsGrounded") hasGrounded = true;
                if (p.name == "Jump") hasJump = true;
            }

            if (!hasSpeed) controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            if (!hasGrounded) controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            if (!hasJump) controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            // Nạp hoặc trích xuất/tạo mới các AnimationClips có đường cong cử động chân thực
            AnimationClip idleClip = GetOrExtractMotionClip("Idle", "Assets/_Project/Models/Character_Player.fbx");
            AnimationClip walkClip = GetOrExtractMotionClip("Walk", "Assets/_Project/Models/Character_Player.fbx");
            AnimationClip runClip = GetOrExtractMotionClip("Run", "Assets/_Project/Models/Character_Player.fbx");

            // Thiết lập hoặc cập nhật Blend Tree cho Locomotion (Idle -> Walk -> Run)
            AnimatorStateMachine rootSm = controller.layers[0].stateMachine;
            ChildAnimatorState locomotionChild = Array.Find(rootSm.states, s => s.state.name == "Locomotion");
            
            AnimatorState locState;
            BlendTree blendTree;

            if (locomotionChild.state == null)
            {
                locState = controller.CreateBlendTreeInController("Locomotion", out blendTree);
                locState.name = "Locomotion";
                blendTree.name = "Locomotion_1D_BlendTree";
                blendTree.blendType = BlendTreeType.Simple1D;
                blendTree.blendParameter = "Speed";
                rootSm.defaultState = locState;
            }
            else
            {
                locState = locomotionChild.state;
                blendTree = locState.motion as BlendTree;
                if (blendTree == null)
                {
                    blendTree = new BlendTree();
                    blendTree.name = "Locomotion_1D_BlendTree";
                    blendTree.blendType = BlendTreeType.Simple1D;
                    blendTree.blendParameter = "Speed";
                    SafeAddSubAsset(blendTree, controller);
                    locState.motion = blendTree;
                }
            }

            // Xóa các motion cũ và gán lại motion có chuyển động chân thực
            blendTree.children = new ChildMotion[0];
            blendTree.AddChild(idleClip, 0.0f);
            blendTree.AddChild(walkClip, 1.5f);
            blendTree.AddChild(runClip, 4.5f);

            EditorUtility.SetDirty(blendTree);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[HueCity] Đã cập nhật Locomotion Blend Tree với các Animation Clips chân thực (Idle, Walk, Run)!");
            return controller;
        }

        private static AnimationClip GetOrExtractMotionClip(string clipName, string fbxSourcePath)
        {
            string destPath = $"Assets/_Project/Animations/Clips/{clipName}.anim";
            
            // 1. Thử tìm clip nhúng trong FBX
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxSourcePath);
            foreach (var a in assets)
            {
                if (a is AnimationClip fbClip && fbClip.name.IndexOf(clipName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AnimationClip extracted = new AnimationClip();
                    EditorUtility.CopySerialized(fbClip, extracted);
                    extracted.name = clipName;
                    AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(extracted);
                    s.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(extracted, s);
                    AssetDatabase.CreateAsset(extracted, destPath);
                    Debug.Log($"[HueCity] Đã trích xuất AnimationClip '{clipName}' từ FBX sang {destPath}!");
                    return extracted;
                }
            }

            // 2. Tạo Procedural Humanoid Muscle Clip nếu chưa có file hợp lệ
            AnimationClip clip = new AnimationClip();
            clip.name = clipName;

            if (clipName == "Idle")
            {
                Keyframe[] keys = new Keyframe[]
                {
                    new Keyframe(0f, 0f, 0f, 0.12f),
                    new Keyframe(1.2f, 0.10f, 0f, 0f),
                    new Keyframe(2.4f, 0f, -0.12f, 0f)
                };
                clip.SetCurve("", typeof(Animator), "Spine Front-Back", new AnimationCurve(keys));
                clip.SetCurve("", typeof(Animator), "Chest Front-Back", new AnimationCurve(keys));
            }
            else if (clipName == "Walk")
            {
                Keyframe[] lLegKeys = new Keyframe[]
                {
                    new Keyframe(0.00f,  0.42f),
                    new Keyframe(0.25f,  0.10f),
                    new Keyframe(0.50f, -0.38f),
                    new Keyframe(0.75f, -0.10f),
                    new Keyframe(1.00f,  0.42f)
                };
                Keyframe[] rLegKeys = new Keyframe[]
                {
                    new Keyframe(0.00f, -0.38f),
                    new Keyframe(0.25f, -0.10f),
                    new Keyframe(0.50f,  0.42f),
                    new Keyframe(0.75f,  0.10f),
                    new Keyframe(1.00f, -0.38f)
                };
                Keyframe[] lKneeKeys = new Keyframe[]
                {
                    new Keyframe(0.00f,  0.10f),
                    new Keyframe(0.25f,  0.30f),
                    new Keyframe(0.50f,  0.05f),
                    new Keyframe(0.75f,  0.75f),
                    new Keyframe(1.00f,  0.10f)
                };
                Keyframe[] rKneeKeys = new Keyframe[]
                {
                    new Keyframe(0.00f,  0.05f),
                    new Keyframe(0.25f,  0.75f),
                    new Keyframe(0.50f,  0.10f),
                    new Keyframe(0.75f,  0.30f),
                    new Keyframe(1.00f,  0.05f)
                };
                Keyframe[] lArmKeys = new Keyframe[]
                {
                    new Keyframe(0.00f, -0.35f),
                    new Keyframe(0.50f,  0.35f),
                    new Keyframe(1.00f, -0.35f)
                };
                Keyframe[] rArmKeys = new Keyframe[]
                {
                    new Keyframe(0.00f,  0.35f),
                    new Keyframe(0.50f, -0.35f),
                    new Keyframe(1.00f,  0.35f)
                };

                clip.SetCurve("", typeof(Animator), "Left Upper Leg Front-Back", new AnimationCurve(lLegKeys));
                clip.SetCurve("", typeof(Animator), "Right Upper Leg Front-Back", new AnimationCurve(rLegKeys));
                clip.SetCurve("", typeof(Animator), "Left Lower Leg Stretch", new AnimationCurve(lKneeKeys));
                clip.SetCurve("", typeof(Animator), "Right Lower Leg Stretch", new AnimationCurve(rKneeKeys));
                clip.SetCurve("", typeof(Animator), "Left Arm Front-Back", new AnimationCurve(lArmKeys));
                clip.SetCurve("", typeof(Animator), "Right Arm Front-Back", new AnimationCurve(rArmKeys));
            }
            else if (clipName == "Run")
            {
                Keyframe[] lLegKeys = new Keyframe[]
                {
                    new Keyframe(0.000f,  0.75f),
                    new Keyframe(0.162f,  0.20f),
                    new Keyframe(0.325f, -0.60f),
                    new Keyframe(0.487f, -0.15f),
                    new Keyframe(0.650f,  0.75f)
                };
                Keyframe[] rLegKeys = new Keyframe[]
                {
                    new Keyframe(0.000f, -0.60f),
                    new Keyframe(0.162f, -0.15f),
                    new Keyframe(0.325f,  0.75f),
                    new Keyframe(0.487f,  0.20f),
                    new Keyframe(0.650f, -0.60f)
                };
                Keyframe[] lKneeKeys = new Keyframe[]
                {
                    new Keyframe(0.000f,  0.20f),
                    new Keyframe(0.162f,  0.45f),
                    new Keyframe(0.325f,  0.10f),
                    new Keyframe(0.487f,  0.95f),
                    new Keyframe(0.650f,  0.20f)
                };
                Keyframe[] rKneeKeys = new Keyframe[]
                {
                    new Keyframe(0.000f,  0.10f),
                    new Keyframe(0.162f,  0.95f),
                    new Keyframe(0.325f,  0.20f),
                    new Keyframe(0.487f,  0.45f),
                    new Keyframe(0.650f,  0.10f)
                };
                Keyframe[] lArmKeys = new Keyframe[]
                {
                    new Keyframe(0.000f, -0.65f),
                    new Keyframe(0.325f,  0.65f),
                    new Keyframe(0.650f, -0.65f)
                };
                Keyframe[] rArmKeys = new Keyframe[]
                {
                    new Keyframe(0.000f,  0.65f),
                    new Keyframe(0.325f, -0.65f),
                    new Keyframe(0.650f,  0.65f)
                };

                clip.SetCurve("", typeof(Animator), "Left Upper Leg Front-Back", new AnimationCurve(lLegKeys));
                clip.SetCurve("", typeof(Animator), "Right Upper Leg Front-Back", new AnimationCurve(rLegKeys));
                clip.SetCurve("", typeof(Animator), "Left Lower Leg Stretch", new AnimationCurve(lKneeKeys));
                clip.SetCurve("", typeof(Animator), "Right Lower Leg Stretch", new AnimationCurve(rKneeKeys));
                clip.SetCurve("", typeof(Animator), "Left Arm Front-Back", new AnimationCurve(lArmKeys));
                clip.SetCurve("", typeof(Animator), "Right Arm Front-Back", new AnimationCurve(rArmKeys));
                clip.SetCurve("", typeof(Animator), "Spine Front-Back", AnimationCurve.Constant(0f, 0.65f, 0.20f));
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, destPath);
            Debug.Log($"[HueCity] Đã tạo Procedural Locomotion Clip '{clipName}' có đầy đủ chuyển động bước chân!");
            return clip;
        }
    }
}
#endif
