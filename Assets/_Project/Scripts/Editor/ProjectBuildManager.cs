#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HueCity.EditorTools
{
    /// <summary>
    /// Công cụ Đóng gói (Build Pipeline) và Kiểm thử Toàn diện cho HueCity.
    /// Cho phép chạy kiểm thử trước khi Build, đóng gói Windows Standalone 64-bit và xuất báo cáo Build chi tiết.
    /// </summary>
    public static class ProjectBuildManager
    {
        [MenuItem("HueCity/2. Build Standalone Player (.exe)", false, 2)]
        public static void BuildStandalonePlayer()
        {
            BuildProject(true);
        }

        [MenuItem("HueCity/3. Full Test & Build Automation", false, 3)]
        public static void FullTestAndBuild()
        {
            Debug.Log("<color=cyan><b>[HueCity Build Manager]</b></color> Bắt đầu kiểm thử toàn diện trước khi đóng gói...");
            bool testsPassed = AutomatedGameTestRunner.RunAllTests(false);
            if (!testsPassed)
            {
                EditorUtility.DisplayDialog("HueCity - Kiểm thử thất bại", 
                    "Không thể tiếp tục Build vì có bài kiểm thử chức năng bị lỗi.\nVui lòng xem file Assets/_Project/TestReport.txt để xử lý.", "OK");
                return;
            }

            Debug.Log("<color=green><b>[HueCity Build Manager]</b></color> Toàn bộ kiểm thử thành công! Bắt đầu đóng gói Player...");
            BuildProject(true);
        }

        public static bool BuildProject(bool showDialog = true)
        {
            string buildFolder = Path.Combine(Directory.GetCurrentDirectory(), "Builds/HueCity_Windows64");
            if (!Directory.Exists(buildFolder))
            {
                Directory.CreateDirectory(buildFolder);
            }

            string exePath = Path.Combine(buildFolder, "HueCity.exe");

            string[] scenes = new string[]
            {
                "Assets/_Project/Scenes/Phase3_VerticalSlice.unity",
                "Assets/_Project/Scenes/Phase8_HueCitywide.unity",
                "Assets/_Project/Scenes/Phase7_PolishedCity.unity",
                "Assets/_Project/Scenes/Phase6_ExpandedMap.unity",
                "Assets/_Project/Scenes/Phase5_GameplayLoop.unity",
                "Assets/_Project/Scenes/Phase4_LivingCity.unity",
                "Assets/_Project/Scenes/Phase2_MapBlocking.unity",
                "Assets/_Project/Scenes/Phase1_Greybox.unity"
            };

            // Đảm bảo các scene nằm trong EditorBuildSettings
            EditorBuildSettingsScene[] buildSettingsScenes = new EditorBuildSettingsScene[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
            {
                buildSettingsScenes[i] = new EditorBuildSettingsScene(scenes[i], true);
            }
            EditorBuildSettings.scenes = buildSettingsScenes;

            PlayerSettings.productName = "HueCity - Cố Đô Mở";
            PlayerSettings.companyName = "HueCity Team";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            DateTime startTime = DateTime.Now;
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            TimeSpan duration = DateTime.Now - startTime;
            BuildSummary summary = report.summary;

            StringBuilder reportLog = new StringBuilder();
            reportLog.AppendLine("================================================================================");
            reportLog.AppendLine("  BÁO CÁO ĐÓNG GÓI DỰ ÁN (BUILD REPORT) - HUECITY");
            reportLog.AppendLine($"  Thời gian thực hiện: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            reportLog.AppendLine("================================================================================");
            reportLog.AppendLine($"Trạng thái (Result): {summary.result}");
            reportLog.AppendLine($"Thời gian đóng gói: {duration.TotalSeconds:F1}s");
            reportLog.AppendLine($"Kích thước bản Build: {summary.totalSize / (1024f * 1024f):F2} MB");
            reportLog.AppendLine($"Đường dẫn Executable: {exePath}");
            reportLog.AppendLine($"Tổng số lỗi (Errors): {summary.totalErrors}");
            reportLog.AppendLine($"Tổng số cảnh báo (Warnings): {summary.totalWarnings}");
            reportLog.AppendLine("--------------------------------------------------------------------------------");
            foreach (BuildStep step in report.steps)
            {
                foreach (BuildStepMessage msg in step.messages)
                {
                    if (msg.type == LogType.Error || msg.type == LogType.Exception)
                    {
                        reportLog.AppendLine($"[ERROR] [{step.name}] {msg.content}");
                    }
                    else if (msg.type == LogType.Warning)
                    {
                        reportLog.AppendLine($"[WARN] [{step.name}] {msg.content}");
                    }
                }
            }
            reportLog.AppendLine("================================================================================");

            string buildReportPath = Path.Combine(Application.dataPath, "_Project/BuildReport.txt");
            File.WriteAllText(buildReportPath, reportLog.ToString());
            AssetDatabase.Refresh();

            if (summary.result == BuildResult.Succeeded)
            {
                string msg = $"Build thành công trong {duration.TotalSeconds:F1}s!\n" +
                             $"Dung lượng: {summary.totalSize / (1024f * 1024f):F2} MB\n" +
                             $"Đường dẫn: {exePath}";
                Debug.Log($"<color=green><b>[HueCity Build Manager]</b></color> {msg}");

                if (showDialog && !Application.isBatchMode)
                {
                    bool openFolder = EditorUtility.DisplayDialog(
                        "HueCity - Build Thành Công!",
                        $"Đã đóng gói thành công bản Build Windows 64-bit!\n\n" +
                        $"• Thời gian: {duration.TotalSeconds:F1}s\n" +
                        $"• Dung lượng: {summary.totalSize / (1024f * 1024f):F2} MB\n" +
                        $"• File: {Path.GetFileName(exePath)}\n\n" +
                        $"Bạn có muốn mở thư mục chứa file .exe không?",
                        "Mở thư mục Build", "Đóng"
                    );

                    if (openFolder)
                    {
                        EditorUtility.RevealInFinder(exePath);
                    }
                }
                return true;
            }
            else
            {
                Debug.LogError($"[HueCity Build Manager] Build thất bại với {summary.totalErrors} lỗi. Xem file Assets/_Project/BuildReport.txt để biết chi tiết.");
                if (showDialog && !Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("HueCity - Build Thất Bại", 
                        $"Quá trình đóng gói gặp {summary.totalErrors} lỗi.\nChi tiết đã ghi lại trong Assets/_Project/BuildReport.txt", "OK");
                }
                return false;
            }
        }
    }
}
#endif
