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

        public static void EnsureFullSetup()
        {
            EnsureDirectories();
            UniversalRenderPipelineAsset urpAsset = SetupURPAssetsAndRenderer();
            SetupPostProcessingProfile();
            SetupProceduralSkybox();
            SetupPBRMaterials();
            SetupVehicleMaterials();
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

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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
            skyboxMat.SetFloat("_AtmosphereThickness", 1.08f);
            skyboxMat.SetColor("_SkyTint", new Color(0.55f, 0.72f, 0.95f)); // Xanh da trời trong trẻo
            skyboxMat.SetColor("_GroundColor", new Color(0.38f, 0.35f, 0.32f)); // Màu đất trung hòa
            skyboxMat.SetFloat("_Exposure", 1.15f);

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
            EditorUtility.SetDirty(yellowLineMat);
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
            carPaint.SetColor("_BaseColor", new Color(0.96f, 0.72f, 0.05f)); // Vàng Taxi Huế
            carPaint.SetFloat("_Metallic", 0.80f);
            carPaint.SetFloat("_Smoothness", 0.85f);
            EditorUtility.SetDirty(carPaint);

            // 2. Đèn pha phát sáng (Emission Headlights)
            string headlightPath = "Assets/_Project/Materials/MAT_Vehicle_Headlights.mat";
            Material headlightMat = AssetDatabase.LoadAssetAtPath<Material>(headlightPath);
            if (headlightMat == null)
            {
                headlightMat = new Material(litShader);
                headlightMat.name = "MAT_Vehicle_Headlights";
                AssetDatabase.CreateAsset(headlightMat, headlightPath);
            }
            headlightMat.SetColor("_BaseColor", new Color(1.0f, 0.98f, 0.85f));
            headlightMat.EnableKeyword("_EMISSION");
            headlightMat.SetColor("_EmissionColor", new Color(2.5f, 2.4f, 1.8f)); // Ánh sáng đèn pha phát quang
            EditorUtility.SetDirty(headlightMat);
        }

        public static void SetupHumanoidCharacterImporter()
        {
            string fbxPath = "Assets/_Project/Models/Character_Humanoid_Rigged.fbx";
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.animationType != ModelImporterAnimationType.Humanoid)
                {
                    importer.animationType = ModelImporterAnimationType.Humanoid;
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
                    Debug.Log("[HueCity] Đã cấu hình ModelImporter sang Humanoid Rig cho Character_Humanoid_Rigged.fbx!");
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

            // Thiết lập Blend Tree cho Locomotion (Idle -> Walk -> Run)
            AnimatorStateMachine rootSm = controller.layers[0].stateMachine;
            ChildAnimatorState locomotionChild = Array.Find(rootSm.states, s => s.state.name == "Locomotion");
            
            if (locomotionChild.state == null)
            {
                AnimatorState locState = controller.CreateBlendTreeInController("Locomotion", out BlendTree blendTree);
                locState.name = "Locomotion";
                blendTree.name = "Locomotion_1D_BlendTree";
                blendTree.blendType = BlendTreeType.Simple1D;
                blendTree.blendParameter = "Speed";

                // Tìm kiếm Animation Clips nếu người dùng đã bỏ vào Assets/_Project/Animations/Clips/
                AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Clips/Idle.anim")
                                       ?? AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Clips/Idle.fbx");
                AnimationClip walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Clips/Walk.anim")
                                       ?? AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Clips/Walk.fbx");
                AnimationClip runClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Clips/Run.anim")
                                      ?? AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Clips/Run.fbx");

                // Tạo placeholder clip nếu chưa có file
                if (idleClip == null) idleClip = EnsurePlaceholderClip("Idle_Placeholder");
                if (walkClip == null) walkClip = EnsurePlaceholderClip("Walk_Placeholder");
                if (runClip == null) runClip = EnsurePlaceholderClip("Run_Placeholder");

                blendTree.AddAnimationClip(idleClip, 0.0f);
                blendTree.AddAnimationClip(walkClip, 1.5f);
                blendTree.AddAnimationClip(runClip, 4.5f);

                rootSm.defaultState = locState;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip EnsurePlaceholderClip(string clipName)
        {
            string clipPath = $"Assets/_Project/Animations/Clips/{clipName}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                clip.name = clipName;
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            return clip;
        }
    }
}
#endif
