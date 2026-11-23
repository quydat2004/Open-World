#if UNITY_EDITOR
using System.IO;
using HueCity.NPC;
using HueCity.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HueCity.EditorTools
{
    /// <summary>
    /// Xây dựng và hoàn thiện toàn bộ Pipeline Rigging & Animation Nhân vật:
    /// 1. Cấu hình ModelImporter sang Humanoid Avatar cho Character_Player.fbx và Character_Humanoid_Rigged.fbx.
    /// 2. Tạo bộ AnimationClip tự nhiên chất lượng cao: Idle (thở, chuyển trọng tâm), Walk (bước nhịp, không trượt chân), Run (chạy nước rút), Idle Variations.
    /// 3. Tạo Animator Controller với Blend Tree Locomotion mượt mà.
    /// 4. Gắn Foot IK (CharacterFootIK) và LookAt IK (CharacterLookAtIK) lên nhân vật.
    /// </summary>
    public static class CharacterAnimationPipelineBuilder
    {
        private const string PlayerFbxPath = "Assets/_Project/Models/Character_Player.fbx";
        private const string RiggedFbxPath = "Assets/_Project/Models/Character_Humanoid_Rigged.fbx";
        private const string ControllerPath = "Assets/_Project/Animations/Player_AnimatorController.controller";
        private const string ClipsFolder = "Assets/_Project/Animations/Clips";

        [MenuItem("HueCity/Cấu hình Pipeline Nhân vật (A-Pose, Humanoid, BlendTree, IK)", false, 82)]
        public static void BuildCharacterPipelineMenu()
        {
            BuildCharacterPipeline();
        }

        public static void BuildCharacterPipeline()
        {
            EditorUtility.DisplayProgressBar("Character Pipeline", "1. Cấu hình Humanoid Avatar cho FBX...", 0.15f);
            ConfigureHumanoidAvatar(PlayerFbxPath);
            ConfigureHumanoidAvatar(RiggedFbxPath);

            EditorUtility.DisplayProgressBar("Character Pipeline", "2. Tạo các Animation Clips tự nhiên...", 0.40f);
            EnsureClipsFolder();
            AnimationClip idleClip = CreateNaturalIdleClip();
            AnimationClip walkClip = CreateNaturalWalkClip();
            AnimationClip runClip = CreateNaturalRunClip();
            AnimationClip lookAroundClip = CreateIdleLookAroundClip();
            AnimationClip crossArmsClip = CreateIdleCrossArmsClip();

            EditorUtility.DisplayProgressBar("Character Pipeline", "3. Tạo Animator Controller & Blend Tree...", 0.70f);
            BuildAnimatorController(idleClip, walkClip, runClip, lookAroundClip, crossArmsClip);

            EditorUtility.DisplayProgressBar("Character Pipeline", "4. Cập nhật Player & NPC trong Scene...", 0.90f);
            UpdateSceneCharacters();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
            Debug.Log("<color=green>[HueCity] Hoàn thành nâng cấp toàn diện Pipeline Nhân vật A-Pose, Humanoid Avatar, Blend Tree và Foot/LookAt IK!</color>");
        }

        public static void ConfigureHumanoidAvatar(string fbxPath)
        {
            if (!File.Exists(fbxPath)) return;

            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            importer.animationType = ModelImporterAnimationType.Humanoid;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.importAnimation = true;

            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
        }

        private static void EnsureClipsFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Animations"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Animations");
            }
            if (!AssetDatabase.IsValidFolder(ClipsFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Animations", "Clips");
            }
        }

        private static AnimationClip CreateNaturalIdleClip()
        {
            string path = $"{ClipsFolder}/Idle.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) clip = new AnimationClip();
            clip.name = "Idle";
            clip.frameRate = 30f;

            // Nhịp thở tự nhiên: Hông và ngực phập phồng nhẹ (chu kỳ 3.2s)
            float duration = 3.2f;

            // 1. Nhịp thở ngực (Chest local scale/rotation micro-expansion)
            AnimationCurve chestBreath = new AnimationCurve();
            chestBreath.AddKey(0f, 0f);
            chestBreath.AddKey(duration * 0.45f, 1.8f); // Hít vào nhẹ
            chestBreath.AddKey(duration, 0f);           // Thở ra
            clip.SetCurve("Armature_Humanoid/Hips/Spine/Chest", typeof(Transform), "localEulerAngles.x", chestBreath);

            // 2. Chuyển trọng tâm nhẹ giữa 2 chân (Hips x sway)
            AnimationCurve hipSway = new AnimationCurve();
            hipSway.AddKey(0f, 0f);
            hipSway.AddKey(duration * 0.25f, 0.008f);
            hipSway.AddKey(duration * 0.75f, -0.008f);
            hipSway.AddKey(duration, 0f);
            clip.SetCurve("Armature_Humanoid/Hips", typeof(Transform), "localPosition.x", hipSway);

            // 3. Độ cao trọng tâm thở
            AnimationCurve hipBreathY = new AnimationCurve();
            hipBreathY.AddKey(0f, 0.95f);
            hipBreathY.AddKey(duration * 0.45f, 0.958f);
            hipBreathY.AddKey(duration, 0.95f);
            clip.SetCurve("Armature_Humanoid/Hips", typeof(Transform), "localPosition.y", hipBreathY);

            // 4. Cánh tay buông thõng tự nhiên (A-Pose 45 độ)
            AnimationCurve leftArm = new AnimationCurve();
            leftArm.AddKey(0f, -44f);
            leftArm.AddKey(duration * 0.5f, -45.5f);
            leftArm.AddKey(duration, -44f);
            clip.SetCurve("Armature_Humanoid/Hips/Spine/Chest/UpperChest/LeftShoulder/LeftUpperArm", typeof(Transform), "localEulerAngles.z", leftArm);

            AnimationCurve rightArm = new AnimationCurve();
            rightArm.AddKey(0f, 44f);
            rightArm.AddKey(duration * 0.5f, 45.5f);
            rightArm.AddKey(duration, 44f);
            clip.SetCurve("Armature_Humanoid/Hips/Spine/Chest/UpperChest/RightShoulder/RightUpperArm", typeof(Transform), "localEulerAngles.z", rightArm);

            // Loop settings
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (!File.Exists(path)) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateNaturalWalkClip()
        {
            string path = $"{ClipsFolder}/Walk.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) clip = new AnimationClip();
            clip.name = "Walk";
            clip.frameRate = 30f;

            float duration = 1.0f; // 1 giây cho 2 bước đi bộ (120 bước/phút)

            // 1. Chân trái (Left Leg) vung tới trước / lui sau (±24 độ)
            AnimationCurve leftThigh = new AnimationCurve();
            leftThigh.AddKey(0.0f, -24f); // Chân trái vung ra trước (Heel strike)
            leftThigh.AddKey(0.25f, 0f);  // Mid-stance
            leftThigh.AddKey(0.5f, 22f);  // Push-off (vung ra sau)
            leftThigh.AddKey(0.75f, 5f);  // Swing phase
            leftThigh.AddKey(1.0f, -24f);
            clip.SetCurve("Armature_Humanoid/Hips/LeftUpperLeg", typeof(Transform), "localEulerAngles.x", leftThigh);

            // 2. Chân phải (Right Leg) ngược pha 180 độ
            AnimationCurve rightThigh = new AnimationCurve();
            rightThigh.AddKey(0.0f, 22f);
            rightThigh.AddKey(0.25f, 5f);
            rightThigh.AddKey(0.5f, -24f);
            rightThigh.AddKey(0.75f, 0f);
            rightThigh.AddKey(1.0f, 22f);
            clip.SetCurve("Armature_Humanoid/Hips/RightUpperLeg", typeof(Transform), "localEulerAngles.x", rightThigh);

            // 3. Đầu gối gập khi co chân (Knee flexion)
            AnimationCurve leftKnee = new AnimationCurve();
            leftKnee.AddKey(0.0f, 2f);
            leftKnee.AddKey(0.5f, 38f); // Gập gối khi nhấc chân
            leftKnee.AddKey(0.85f, 5f);
            leftKnee.AddKey(1.0f, 2f);
            clip.SetCurve("Armature_Humanoid/Hips/LeftUpperLeg/LeftLowerLeg", typeof(Transform), "localEulerAngles.x", leftKnee);

            AnimationCurve rightKnee = new AnimationCurve();
            rightKnee.AddKey(0.0f, 38f);
            rightKnee.AddKey(0.35f, 5f);
            rightKnee.AddKey(0.5f, 2f);
            rightKnee.AddKey(1.0f, 38f);
            clip.SetCurve("Armature_Humanoid/Hips/RightUpperLeg/RightLowerLeg", typeof(Transform), "localEulerAngles.x", rightKnee);

            // 4. Tay vung so le ngược chiều chân (Left Arm vung cùng pha chân phải)
            AnimationCurve leftArmSwing = new AnimationCurve();
            leftArmSwing.AddKey(0.0f, 18f); // Vung ra sau khi chân trái ra trước
            leftArmSwing.AddKey(0.5f, -18f); // Vung ra trước khi chân trái ra sau
            leftArmSwing.AddKey(1.0f, 18f);
            clip.SetCurve("Armature_Humanoid/Hips/Spine/Chest/UpperChest/LeftShoulder/LeftUpperArm", typeof(Transform), "localEulerAngles.x", leftArmSwing);

            AnimationCurve rightArmSwing = new AnimationCurve();
            rightArmSwing.AddKey(0.0f, -18f);
            rightArmSwing.AddKey(0.5f, 18f);
            rightArmSwing.AddKey(1.0f, -18f);
            clip.SetCurve("Armature_Humanoid/Hips/Spine/Chest/UpperChest/RightShoulder/RightUpperArm", typeof(Transform), "localEulerAngles.x", rightArmSwing);

            // 5. Trọng tâm hông nhấp nhô 2 nhịp / chu kỳ
            AnimationCurve hipBounce = new AnimationCurve();
            hipBounce.AddKey(0.0f, 0.93f); // Heel strike (thấp nhất)
            hipBounce.AddKey(0.25f, 0.96f); // Mid-stance (cao nhất)
            hipBounce.AddKey(0.5f, 0.93f);
            hipBounce.AddKey(0.75f, 0.96f);
            hipBounce.AddKey(1.0f, 0.93f);
            clip.SetCurve("Armature_Humanoid/Hips", typeof(Transform), "localPosition.y", hipBounce);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (!File.Exists(path)) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateNaturalRunClip()
        {
            string path = $"{ClipsFolder}/Run.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) clip = new AnimationClip();
            clip.name = "Run";
            clip.frameRate = 30f;

            float duration = 0.65f; // Chạy nhanh 0.65s / chu kỳ

            // Thân người hơi chúi về phía trước (Pitch 10 độ)
            AnimationCurve spineLean = new AnimationCurve();
            spineLean.AddKey(0f, 11f);
            spineLean.AddKey(duration, 11f);
            clip.SetCurve("Armature_Humanoid/Hips/Spine", typeof(Transform), "localEulerAngles.x", spineLean);

            // Chân sải rộng hơn (±38 độ)
            AnimationCurve leftThigh = new AnimationCurve();
            leftThigh.AddKey(0.0f, -38f);
            leftThigh.AddKey(duration * 0.5f, 34f);
            leftThigh.AddKey(duration, -38f);
            clip.SetCurve("Armature_Humanoid/Hips/LeftUpperLeg", typeof(Transform), "localEulerAngles.x", leftThigh);

            AnimationCurve rightThigh = new AnimationCurve();
            rightThigh.AddKey(0.0f, 34f);
            rightThigh.AddKey(duration * 0.5f, -38f);
            rightThigh.AddKey(duration, 34f);
            clip.SetCurve("Armature_Humanoid/Hips/RightUpperLeg", typeof(Transform), "localEulerAngles.x", rightThigh);

            // Đầu gối gập mạnh khi nâng đùi (65 độ)
            AnimationCurve leftKnee = new AnimationCurve();
            leftKnee.AddKey(0.0f, 8f);
            leftKnee.AddKey(duration * 0.5f, 65f);
            leftKnee.AddKey(duration, 8f);
            clip.SetCurve("Armature_Humanoid/Hips/LeftUpperLeg/LeftLowerLeg", typeof(Transform), "localEulerAngles.x", leftKnee);

            AnimationCurve rightKnee = new AnimationCurve();
            rightKnee.AddKey(0.0f, 65f);
            rightKnee.AddKey(duration * 0.5f, 8f);
            rightKnee.AddKey(duration, 65f);
            clip.SetCurve("Armature_Humanoid/Hips/RightUpperLeg/RightLowerLeg", typeof(Transform), "localEulerAngles.x", rightKnee);

            // Đánh tay mạnh mẽ (khuỷu tay gập ~75 độ, vai vung ±34 độ)
            AnimationCurve leftArm = new AnimationCurve();
            leftArm.AddKey(0.0f, 34f);
            leftArm.AddKey(duration * 0.5f, -34f);
            leftArm.AddKey(duration, 34f);
            clip.SetCurve("Armature_Humanoid/Hips/Spine/Chest/UpperChest/LeftShoulder/LeftUpperArm", typeof(Transform), "localEulerAngles.x", leftArm);

            AnimationCurve rightArm = new AnimationCurve();
            rightArm.AddKey(0.0f, -34f);
            rightArm.AddKey(duration * 0.5f, 34f);
            rightArm.AddKey(duration, -34f);
            clip.SetCurve("Armature_Humanoid/Hips/Spine/Chest/UpperChest/RightShoulder/RightUpperArm", typeof(Transform), "localEulerAngles.x", rightArm);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (!File.Exists(path)) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateIdleLookAroundClip()
        {
            string path = $"{ClipsFolder}/Idle_LookAround.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) clip = new AnimationClip();
            clip.name = "Idle_LookAround";
            clip.frameRate = 30f;

            float duration = 4.0f;

            // Đầu quay nhẹ sang trái 25 độ, quay sang phải 22 độ, rồi về giữa
            AnimationCurve headYaw = new AnimationCurve();
            headYaw.AddKey(0f, 0f);
            headYaw.AddKey(1.2f, -25f);
            headYaw.AddKey(2.5f, 22f);
            headYaw.AddKey(duration, 0f);
            clip.SetCurve("Armature_Humanoid/Hips/Spine/Chest/UpperChest/Neck/Head", typeof(Transform), "localEulerAngles.y", headYaw);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (!File.Exists(path)) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateIdleCrossArmsClip()
        {
            string path = $"{ClipsFolder}/Idle_CrossArms.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) clip = new AnimationClip();
            clip.name = "Idle_CrossArms";
            clip.frameRate = 30f;

            float duration = 4.5f;

            // 2 tay khoanh trước ngực
            AnimationCurve leftArm = new AnimationCurve();
            leftArm.AddKey(0f, -44f);
            leftArm.AddKey(1.0f, -12f);
            leftArm.AddKey(3.5f, -12f);
            leftArm.AddKey(duration, -44f);
            clip.SetCurve("Armature_Humanoid/Hips/Spine/Chest/UpperChest/LeftShoulder/LeftUpperArm", typeof(Transform), "localEulerAngles.z", leftArm);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (!File.Exists(path)) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void BuildAnimatorController(AnimationClip idle, AnimationClip walk, AnimationClip run, AnimationClip lookAround, AnimationClip crossArms)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            // Xóa parameters cũ và tạo mới chuẩn xác
            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IdleVariation", AnimatorControllerParameterType.Int);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // Xóa states cũ
            ChildAnimatorState[] states = sm.states;
            for (int i = states.Length - 1; i >= 0; i--)
            {
                sm.RemoveState(states[i].state);
            }

            // 1. Tạo Blend Tree Locomotion
            BlendTree blendTree;
            AnimatorState locomotionState = controller.CreateBlendTreeInController("Locomotion", out blendTree);
            locomotionState.name = "Locomotion";
            blendTree.name = "Locomotion_BlendTree";
            blendTree.blendType = BlendTreeType.Simple1D;
            blendTree.blendParameter = "Speed";
            blendTree.useAutomaticThresholds = false;

            blendTree.AddChild(idle, 0.0f);
            blendTree.AddChild(walk, 3.8f);
            blendTree.AddChild(run, 7.8f);

            sm.defaultState = locomotionState;

            // 2. Tạo states Idle Variations
            AnimatorState lookAroundState = sm.AddState("Idle_LookAround");
            lookAroundState.motion = lookAround;

            AnimatorState crossArmsState = sm.AddState("Idle_CrossArms");
            crossArmsState.motion = crossArms;

            // 3. Transitions
            // Locomotion -> LookAround (khi Speed < 0.1 và IdleVariation == 1)
            var toLook = locomotionState.AddTransition(lookAroundState);
            toLook.AddCondition(AnimatorConditionMode.Less, 0.15f, "Speed");
            toLook.AddCondition(AnimatorConditionMode.Equals, 1, "IdleVariation");
            toLook.duration = 0.35f;

            var fromLook = lookAroundState.AddTransition(locomotionState);
            fromLook.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");
            fromLook.duration = 0.25f;

            var fromLookIdle = lookAroundState.AddTransition(locomotionState);
            fromLookIdle.AddCondition(AnimatorConditionMode.Equals, 0, "IdleVariation");
            fromLookIdle.duration = 0.35f;

            // Locomotion -> CrossArms (khi Speed < 0.1 và IdleVariation == 2)
            var toCross = locomotionState.AddTransition(crossArmsState);
            toCross.AddCondition(AnimatorConditionMode.Less, 0.15f, "Speed");
            toCross.AddCondition(AnimatorConditionMode.Equals, 2, "IdleVariation");
            toCross.duration = 0.35f;

            var fromCross = crossArmsState.AddTransition(locomotionState);
            fromCross.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");
            fromCross.duration = 0.25f;

            var fromCrossIdle = crossArmsState.AddTransition(locomotionState);
            fromCrossIdle.AddCondition(AnimatorConditionMode.Equals, 0, "IdleVariation");
            fromCrossIdle.duration = 0.35f;

            EditorUtility.SetDirty(controller);
        }

        private static void UpdateSceneCharacters()
        {
            // Tìm tất cả Player và NPC trong Scene mở hiện tại để gán Animator, FootIK, LookAtIK
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                SetupCharacterComponents(player, isPlayer: true);
            }

            GameObject[] npcs = GameObject.FindGameObjectsWithTag("NPC");
            foreach (var npc in npcs)
            {
                SetupCharacterComponents(npc, isPlayer: false);
            }
        }

        public static void SetupCharacterComponents(GameObject target, bool isPlayer)
        {
            if (target == null) return;

            Animator anim = target.GetComponent<Animator>();
            if (anim == null) anim = target.AddComponent<Animator>();

            RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;

            // Gán Humanoid Avatar từ Character_Player.fbx
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFbxPath);
            if (fbx != null)
            {
                Animator fbxAnim = fbx.GetComponent<Animator>();
                if (fbxAnim != null && fbxAnim.avatar != null)
                {
                    anim.avatar = fbxAnim.avatar;
                }
            }

            // Gắn Foot IK
            CharacterFootIK footIK = target.GetComponent<CharacterFootIK>();
            if (footIK == null) footIK = target.AddComponent<CharacterFootIK>();
            footIK.EnableFootIK = true;

            // Gắn LookAt IK
            CharacterLookAtIK lookIK = target.GetComponent<CharacterLookAtIK>();
            if (lookIK == null) lookIK = target.AddComponent<CharacterLookAtIK>();
            lookIK.EnableLookAt = true;

            // Nếu là NPC, gắn thêm Idle Behavior
            if (!isPlayer)
            {
                NPCIdleBehavior idleBehavior = target.GetComponent<NPCIdleBehavior>();
                if (idleBehavior == null) idleBehavior = target.AddComponent<NPCIdleBehavior>();
            }
        }
    }
}
#endif
