#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HueCity.EditorTools
{
    [InitializeOnLoad]
    public static class LocomotionClipGenerator
    {
        private const string ClipsDir = "Assets/_Project/Animations/Clips";
        private const string ControllerPath = "Assets/_Project/Animations/Player_AnimatorController.controller";

        static LocomotionClipGenerator()
        {
            EditorApplication.delayCall += EnsureClipsAndController;
        }

        [MenuItem("HueCity/5. Force Rebuild Locomotion Clips & BlendTree", false, 5)]
        public static void EnsureClipsAndController()
        {
            if (!Directory.Exists(ClipsDir)) Directory.CreateDirectory(ClipsDir);

            // 1. Tạo 3 clips chuyển động với đường cong Humanoid Muscle thật
            AnimationClip idleClip = BuildIdleClip();
            AnimationClip walkClip = BuildWalkClip();
            AnimationClip runClip = BuildRunClip();

            // 2. Gán 3 clips vào Locomotion BlendTree của Player_AnimatorController
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            EnsureParameters(controller);
            UpdateBlendTreeMotions(controller, idleClip, walkClip, runClip);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[HueCity Locomotion]</color> Đã tạo và cấu hình thành công 3 Clips cử động (Idle, Walk, Run) vào BlendTree!");
        }

        private static void EnsureParameters(AnimatorController controller)
        {
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
        }

        private static void UpdateBlendTreeMotions(AnimatorController controller, AnimationClip idle, AnimationClip walk, AnimationClip run)
        {
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            ChildAnimatorState stateEntry = Array.Find(sm.states, s => s.state.name == "Locomotion");
            
            AnimatorState locState;
            BlendTree blendTree;

            if (stateEntry.state == null)
            {
                locState = controller.CreateBlendTreeInController("Locomotion", out blendTree);
                locState.name = "Locomotion";
                blendTree.name = "Locomotion_1D_BlendTree";
                blendTree.blendType = BlendTreeType.Simple1D;
                blendTree.blendParameter = "Speed";
                sm.defaultState = locState;
            }
            else
            {
                locState = stateEntry.state;
                blendTree = locState.motion as BlendTree;
                if (blendTree == null)
                {
                    blendTree = new BlendTree();
                    blendTree.name = "Locomotion_1D_BlendTree";
                    blendTree.blendType = BlendTreeType.Simple1D;
                    blendTree.blendParameter = "Speed";
                    AssetDatabase.AddObjectToAsset(blendTree, controller);
                    locState.motion = blendTree;
                }
            }

            // Gán lại chính xác 3 motions
            blendTree.children = new ChildMotion[0];
            blendTree.AddChild(idle, 0.0f);
            blendTree.AddChild(walk, 1.5f);
            blendTree.AddChild(run, 4.5f);

            EditorUtility.SetDirty(blendTree);
            EditorUtility.SetDirty(controller);
        }

        private static AnimationClip BuildIdleClip()
        {
            string path = $"{ClipsDir}/Idle.anim";
            AnimationClip clip = new AnimationClip();
            clip.name = "Idle";
            clip.frameRate = 30;

            // Nhịp thở nhấp nhô lồng ngực và cử động cơ vai (Chu kỳ 2.0s lặp mượt mà)
            Keyframe[] breathKeys = new Keyframe[]
            {
                new Keyframe(0.0f,  0.0f,  0.0f,  0.08f),
                new Keyframe(1.0f,  0.06f, 0.0f,  0.0f),
                new Keyframe(2.0f,  0.0f, -0.08f, 0.0f)
            };
            AnimationCurve breathCurve = new AnimationCurve(breathKeys);

            clip.SetCurve("", typeof(Animator), "Spine Front-Back", breathCurve);
            clip.SetCurve("", typeof(Animator), "Chest Front-Back", breathCurve);

            // Cử động tay thả lỏng nhẹ bên hông
            Keyframe[] armKeys = new Keyframe[]
            {
                new Keyframe(0.0f, -0.02f),
                new Keyframe(1.0f,  0.02f),
                new Keyframe(2.0f, -0.02f)
            };
            clip.SetCurve("", typeof(Animator), "Left Arm Down-Up", new AnimationCurve(armKeys));
            clip.SetCurve("", typeof(Animator), "Right Arm Down-Up", new AnimationCurve(armKeys));

            SaveClipWithLoop(clip, path);
            return clip;
        }

        private static AnimationClip BuildWalkClip()
        {
            string path = $"{ClipsDir}/Walk.anim";
            AnimationClip clip = new AnimationClip();
            clip.name = "Walk";
            clip.frameRate = 30;

            // Bước chân đi bộ tự nhiên (Chu kỳ 1.0s, 30 frames)
            // Chân trái & Chân phải so le tiến/lùi
            Keyframe[] lLeg = new Keyframe[]
            {
                new Keyframe(0.00f,  0.42f, 0f, -2.5f),
                new Keyframe(0.25f,  0.10f),
                new Keyframe(0.50f, -0.38f, -2.5f, 0f),
                new Keyframe(0.75f, -0.10f),
                new Keyframe(1.00f,  0.42f, 0f, -2.5f)
            };
            Keyframe[] rLeg = new Keyframe[]
            {
                new Keyframe(0.00f, -0.38f, -2.5f, 0f),
                new Keyframe(0.25f, -0.10f),
                new Keyframe(0.50f,  0.42f, 0f, -2.5f),
                new Keyframe(0.75f,  0.10f),
                new Keyframe(1.00f, -0.38f, -2.5f, 0f)
            };

            // Gập đầu gối (Lower Leg Stretch) để nhấc chân qua mặt đất
            Keyframe[] lKnee = new Keyframe[]
            {
                new Keyframe(0.00f, 0.08f),
                new Keyframe(0.25f, 0.28f),
                new Keyframe(0.50f, 0.05f),
                new Keyframe(0.75f, 0.72f),
                new Keyframe(1.00f, 0.08f)
            };
            Keyframe[] rKnee = new Keyframe[]
            {
                new Keyframe(0.00f, 0.05f),
                new Keyframe(0.25f, 0.72f),
                new Keyframe(0.50f, 0.08f),
                new Keyframe(0.75f, 0.28f),
                new Keyframe(1.00f, 0.05f)
            };

            // Đánh tay so le ngược hướng chân để giữ thăng bằng
            Keyframe[] lArm = new Keyframe[]
            {
                new Keyframe(0.00f, -0.32f),
                new Keyframe(0.50f,  0.32f),
                new Keyframe(1.00f, -0.32f)
            };
            Keyframe[] rArm = new Keyframe[]
            {
                new Keyframe(0.00f,  0.32f),
                new Keyframe(0.50f, -0.32f),
                new Keyframe(1.00f,  0.32f)
            };

            // Nhún hông nhẹ theo nhịp bước
            Keyframe[] hipBob = new Keyframe[]
            {
                new Keyframe(0.00f,  0.0f),
                new Keyframe(0.25f, -0.04f),
                new Keyframe(0.50f,  0.0f),
                new Keyframe(0.75f, -0.04f),
                new Keyframe(1.00f,  0.0f)
            };

            clip.SetCurve("", typeof(Animator), "Left Upper Leg Front-Back", new AnimationCurve(lLeg));
            clip.SetCurve("", typeof(Animator), "Right Upper Leg Front-Back", new AnimationCurve(rLeg));
            clip.SetCurve("", typeof(Animator), "Left Lower Leg Stretch", new AnimationCurve(lKnee));
            clip.SetCurve("", typeof(Animator), "Right Lower Leg Stretch", new AnimationCurve(rKnee));
            clip.SetCurve("", typeof(Animator), "Left Arm Front-Back", new AnimationCurve(lArm));
            clip.SetCurve("", typeof(Animator), "Right Arm Front-Back", new AnimationCurve(rArm));
            clip.SetCurve("", typeof(Animator), "RootT.y", new AnimationCurve(hipBob));

            SaveClipWithLoop(clip, path);
            return clip;
        }

        private static AnimationClip BuildRunClip()
        {
            string path = $"{ClipsDir}/Run.anim";
            AnimationClip clip = new AnimationClip();
            clip.name = "Run";
            clip.frameRate = 30;

            // Nhịp chạy nước rút (Chu kỳ 0.65s, 20 frames)
            Keyframe[] lLeg = new Keyframe[]
            {
                new Keyframe(0.000f,  0.78f),
                new Keyframe(0.162f,  0.18f),
                new Keyframe(0.325f, -0.62f),
                new Keyframe(0.487f, -0.12f),
                new Keyframe(0.650f,  0.78f)
            };
            Keyframe[] rLeg = new Keyframe[]
            {
                new Keyframe(0.000f, -0.62f),
                new Keyframe(0.162f, -0.12f),
                new Keyframe(0.325f,  0.78f),
                new Keyframe(0.487f,  0.18f),
                new Keyframe(0.650f, -0.62f)
            };

            Keyframe[] lKnee = new Keyframe[]
            {
                new Keyframe(0.000f, 0.15f),
                new Keyframe(0.162f, 0.40f),
                new Keyframe(0.325f, 0.08f),
                new Keyframe(0.487f, 0.95f),
                new Keyframe(0.650f, 0.15f)
            };
            Keyframe[] rKnee = new Keyframe[]
            {
                new Keyframe(0.000f, 0.08f),
                new Keyframe(0.162f, 0.95f),
                new Keyframe(0.325f, 0.15f),
                new Keyframe(0.487f, 0.40f),
                new Keyframe(0.650f, 0.08f)
            };

            Keyframe[] lArm = new Keyframe[]
            {
                new Keyframe(0.000f, -0.68f),
                new Keyframe(0.325f,  0.68f),
                new Keyframe(0.650f, -0.68f)
            };
            Keyframe[] rArm = new Keyframe[]
            {
                new Keyframe(0.000f,  0.68f),
                new Keyframe(0.325f, -0.68f),
                new Keyframe(0.650f,  0.68f)
            };

            clip.SetCurve("", typeof(Animator), "Left Upper Leg Front-Back", new AnimationCurve(lLeg));
            clip.SetCurve("", typeof(Animator), "Right Upper Leg Front-Back", new AnimationCurve(rLeg));
            clip.SetCurve("", typeof(Animator), "Left Lower Leg Stretch", new AnimationCurve(lKnee));
            clip.SetCurve("", typeof(Animator), "Right Lower Leg Stretch", new AnimationCurve(rKnee));
            clip.SetCurve("", typeof(Animator), "Left Arm Front-Back", new AnimationCurve(lArm));
            clip.SetCurve("", typeof(Animator), "Right Arm Front-Back", new AnimationCurve(rArm));

            // Thân người hơi chúi về phía trước khi chạy
            clip.SetCurve("", typeof(Animator), "Spine Front-Back", AnimationCurve.Constant(0f, 0.65f, 0.22f));

            SaveClipWithLoop(clip, path);
            return clip;
        }

        private static void SaveClipWithLoop(AnimationClip clip, string path)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, path);
        }
    }
}
#endif
