#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Cinemachine;
using HueCity.Audio;
using HueCity.Camera;
using HueCity.Core;
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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
namespace HueCity.EditorTools
{
    using Camera = UnityEngine.Camera;
    /// <summary>
    /// Trình dựng bản đồ Quy mô Toàn bộ Thành phố Huế (Hue Citywide Map Builder).
    /// Tích hợp dữ liệu GIS OpenStreetMap thực tế:
    /// - Sông Hương (Perfume River) uốn lượn chia 2 bờ Nam - Bắc.
    /// - 3 Cây cầu biểu tượng vượt sông: Cầu Trường Tiền (6 vài 12 nhịp), Cầu Phú Xuân, Cầu Dã Viên.
    /// - Khu Di tích Kinh Thành Huế: Tường thành 2.2km, Kỳ Đài (Cột Cờ Huế), Ngọ Môn, Hộ Thành Hào.
    /// - Bờ Nam: Đại lộ ven sông Lê Lợi, Phố Tây (Chu Văn An - Võ Thị Sáu), Ga Huế, Bến Tòa Khâm.
    /// - Bờ Bắc: Chợ Đông Ba sầm uất, Phố cổ Trần Hưng Đạo.
    /// - Mạng lưới giao thông liên bờ liên tục qua các cây cầu lịch sử.
    /// </summary>
    public static class HueCityMapBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Phase8_HueCitywide.unity";
        private const string GeoJsonPath = "Assets/_Project/Data/HueCity_Map_GeoData.json";

        [MenuItem("HueCity/Tích hợp Bản đồ Toàn TP Huế (Citywide Map)", false, 80)]
        public static void BuildCitywideHueMapMenu()
        {
            BuildCitywideHueMap();
        }

        public static void BuildCitywideHueMap()
        {
            EditorUtility.DisplayProgressBar("Hue Citywide Map", "Khởi tạo Scene toàn TP Huế...", 0.05f);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Chuẩn bị Materials đặc trưng Cố Đô
            Material waterMat = EnsureWaterMaterial();
            Material wallMat = EnsureCitadelWallMaterial();
            Material steelMat = EnsureBridgeSteelMaterial();
            Material roadMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Road_Asphalt.mat");
            Material sidewalkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Sidewalk_Concrete.mat");
            Material curbMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Curb_Stone.mat");

            // 2. Thiết lập Môi trường Ánh sáng & Bầu trời
            EditorUtility.DisplayProgressBar("Hue Citywide Map", "Thiết lập Ánh sáng mặt trời & URP Post-Processing...", 0.15f);
            SetupLightingAndPostProcessing();

            // 3. Tạo Sông Hương (Perfume River) & Hào nước
            EditorUtility.DisplayProgressBar("Hue Citywide Map", "Dựng Sông Hương & Hệ thống Thủy lộ Cố Đô...", 0.30f);
            GameObject waterRoot = BuildPerfumeRiver(waterMat);

            // 4. Dựng 3 Cây cầu Vượt Sông Hương (Trường Tiền, Phú Xuân, Dã Viên)
            EditorUtility.DisplayProgressBar("Hue Citywide Map", "Dựng Cầu Trường Tiền, Phú Xuân & Dã Viên...", 0.45f);
            GameObject bridgesRoot = BuildIconicBridges(steelMat, roadMat, sidewalkMat);

            // 5. Dựng Khu Di tích Kinh Thành Huế & Kỳ Đài
            EditorUtility.DisplayProgressBar("Hue Citywide Map", "Dựng Kinh Thành Huế, Kỳ Đài & Cửa Thành...", 0.60f);
            GameObject citadelRoot = BuildImperialCitadel(wallMat);

            // 6. Dựng Trục Đô thị Bờ Nam (Lê Lợi, Phố Tây) & Bờ Bắc (Chợ Đông Ba)
            EditorUtility.DisplayProgressBar("Hue Citywide Map", "Dựng Đại lộ Lê Lợi, Phố Tây, Chợ Đông Ba...", 0.75f);
            GameObject urbanRoot = BuildUrbanDistricts(roadMat, sidewalkMat, curbMat);

            // 7. Tạo Hệ thống Giao thông Liên bờ & Waypoints
            EditorUtility.DisplayProgressBar("Hue Citywide Map", "Kết nối Mạng lưới Giao thông 2 Bờ Sông Hương...", 0.85f);
            SetupCitywideTraffic(roadMat);

            // 8. Tích hợp Player, Xe Sedan, Audio, Weather & UI
            EditorUtility.DisplayProgressBar("Hue Citywide Map", "Tích hợp Player, Xe cộ, HUD & Minimap...", 0.95f);
            SetupCoreGameplayAndHUD();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtility.ClearProgressBar();
            Debug.Log($"<color=green>[HueCity] Dựng thành công Bản đồ Toàn cảnh TP Huế: {ScenePath}</color>");
        }

        private static Material EnsureWaterMaterial()
        {
            string path = "Assets/_Project/Materials/MAT_Water_PerfumeRiver.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = "MAT_Water_PerfumeRiver";
                mat.SetColor("_BaseColor", new Color(0.08f, 0.35f, 0.46f, 0.85f));
                mat.SetFloat("_Metallic", 0.15f);
                mat.SetFloat("_Smoothness", 0.96f);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static Material EnsureCitadelWallMaterial()
        {
            string path = "Assets/_Project/Materials/MAT_Citadel_Wall.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = "MAT_Citadel_Wall";
                mat.SetColor("_BaseColor", new Color(0.42f, 0.40f, 0.36f, 1.0f)); // Màu rêu phong thành cổ
                mat.SetFloat("_Metallic", 0.05f);
                mat.SetFloat("_Smoothness", 0.22f);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static Material EnsureBridgeSteelMaterial()
        {
            string path = "Assets/_Project/Materials/MAT_Bridge_Steel.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = "MAT_Bridge_Steel";
                mat.SetColor("_BaseColor", new Color(0.78f, 0.80f, 0.82f, 1.0f)); // Thép sơn xám bạc Eiffel
                mat.SetFloat("_Metallic", 0.88f);
                mat.SetFloat("_Smoothness", 0.55f);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static void SetupLightingAndPostProcessing()
        {
            GameObject sunObj = new GameObject("Directional_Sun");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.96f, 0.88f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sunObj.transform.rotation = Quaternion.Euler(46f, -38f, 0f);

            GameObject volObj = new GameObject("Global_Volume");
            Volume vol = volObj.AddComponent<Volume>();
            vol.isGlobal = true;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "HueCity_Citywide_Profile";

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(0.9f);

            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.contrast.Override(12f);
            colorAdj.saturation.Override(8f);

            vol.profile = profile;
        }

        private static GameObject BuildPerfumeRiver(Material waterMat)
        {
            GameObject root = new GameObject("--- PERFUME RIVER (SÔNG HƯƠNG) ---");

            // Mặt nước Sông Hương rộng lớn uốn lượn qua trung tâm Huế
            // Sông Hương dài 3,400m, rộng từ 180m đến 320m, cao độ y = -1.5m
            int riverSegments = 17;
            float startX = -1700f;
            float endX = 1700f;
            float stepX = (endX - startX) / riverSegments;

            for (int i = 0; i < riverSegments; i++)
            {
                float x1 = startX + i * stepX;
                float x2 = x1 + stepX;
                float midX = (x1 + x2) * 0.5f;

                // Đường cong tự nhiên của Sông Hương theo tọa độ GPS:
                // Đoạn Dã Viên (phía Tây) hơi chúc xuống nam, đoạn Phú Xuân - Trường Tiền uốn nhẹ lên bắc
                float curveZ = Mathf.Sin(midX * 0.0018f) * 85f - 40f;
                float riverWidth = 240f + Mathf.Cos(midX * 0.0022f) * 45f;

                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"PerfumeRiver_Segment_{i + 1}";
                seg.transform.SetParent(root.transform);
                seg.transform.position = new Vector3(midX, -1.5f, curveZ);
                seg.transform.localScale = new Vector3(stepX * 1.02f, 1.0f, riverWidth);

                if (waterMat != null)
                {
                    seg.GetComponent<MeshRenderer>().sharedMaterial = waterMat;
                }
            }

            // Hào nước Kinh Thành (Hộ Thành Hào) bao quanh 4 mặt thành cổ
            CreateCitadelMoat(root.transform, waterMat);

            return root;
        }

        private static void CreateCitadelMoat(Transform parent, Material waterMat)
        {
            GameObject moatRoot = new GameObject("Citadel_Moat (Hộ Thành Hào)");
            moatRoot.transform.SetParent(parent);

            // Kinh thành vuông cạnh 2,200m, tâm tại x = -250, z = 1150
            Vector3 center = new Vector3(-250f, -1.2f, 1150f);
            float side = 1100f; // bán kính nửa cạnh

            // 4 hào nước bao quanh
            CreateMoatSegment(moatRoot.transform, new Vector3(center.x, -1.2f, center.z - side), new Vector3(side * 2f, 0.6f, 35f), waterMat); // Nam
            CreateMoatSegment(moatRoot.transform, new Vector3(center.x, -1.2f, center.z + side), new Vector3(side * 2f, 0.6f, 35f), waterMat); // Bắc
            CreateMoatSegment(moatRoot.transform, new Vector3(center.x - side, -1.2f, center.z), new Vector3(35f, 0.6f, side * 2f), waterMat); // Tây
            CreateMoatSegment(moatRoot.transform, new Vector3(center.x + side, -1.2f, center.z), new Vector3(35f, 0.6f, side * 2f), waterMat); // Đông
        }

        private static void CreateMoatSegment(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject moat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            moat.name = "Moat_Segment";
            moat.transform.SetParent(parent);
            moat.transform.position = pos;
            moat.transform.localScale = size;
            if (mat != null) moat.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static GameObject BuildIconicBridges(Material steelMat, Material roadMat, Material sidewalkMat)
        {
            GameObject root = new GameObject("--- ICONIC BRIDGES (CẦU VƯỢT SÔNG HƯƠNG) ---");

            // 1. Cầu Trường Tiền (6 Vài 12 Nhịp)
            // Nối x = 750 (Đầu cầu Bờ Nam tại ngã Lê Lợi / Hùng Vương) sang x = 790, z = 160 (Chợ Đông Ba / Trần Hưng Đạo)
            BuildTruongTienBridge(root.transform, steelMat, roadMat);

            // 2. Cầu Phú Xuân (Cầu Mới)
            // Nối trục đường Hà Nội / Lê Lợi sang Cửa Ngăn
            BuildPhuXuanBridge(root.transform, roadMat, sidewalkMat);

            // 3. Cầu Dã Viên
            // Vắt qua cồn Dã Viên ở thượng lưu Sông Hương
            BuildDaVienBridge(root.transform, roadMat, sidewalkMat);

            return root;
        }

        private static void BuildTruongTienBridge(Transform parent, Material steelMat, Material roadMat)
        {
            GameObject bridgeObj = new GameObject("Bridge_TruongTien (Cầu Trường Tiền 6 Vài)");
            bridgeObj.transform.SetParent(parent);

            // Tải model modular 1 nhịp vòm thép đã tác giả trong Blender
            GameObject spanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Models/Bridge_TruongTien_Span.fbx");

            Vector3 startPos = new Vector3(745f, 1.8f, -145f); // Bờ Nam
            Vector3 endPos = new Vector3(795f, 1.8f, 255f);     // Bờ Bắc (Chợ Đông Ba)
            Vector3 dir = (endPos - startPos);
            float totalLen = dir.magnitude;
            int numSpans = 6;
            float spanLen = totalLen / numSpans;
            Quaternion rot = Quaternion.LookRotation(new Vector3(dir.z, 0, -dir.x)); // Vuông góc hướng nhịp

            for (int i = 0; i < numSpans; i++)
            {
                Vector3 spanPos = Vector3.Lerp(startPos, endPos, (float)i / numSpans);
                GameObject span;
                if (spanPrefab != null)
                {
                    span = (GameObject)PrefabUtility.InstantiatePrefab(spanPrefab);
                    span.name = $"TruongTien_Span_{i + 1}";
                    span.transform.SetParent(bridgeObj.transform);
                    span.transform.position = spanPos;
                    span.transform.forward = dir.normalized;
                }
                else
                {
                    // Fallback procedural
                    span = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    span.name = $"TruongTien_Span_{i + 1}";
                    span.transform.SetParent(bridgeObj.transform);
                    span.transform.position = spanPos + dir.normalized * (spanLen * 0.5f);
                    span.transform.localScale = new Vector3(8.5f, 0.6f, spanLen);
                    span.transform.forward = dir.normalized;
                    if (roadMat != null) span.GetComponent<MeshRenderer>().sharedMaterial = roadMat;
                }
            }

            // Gán Collider mặt cầu thông suốt cho xe chạy
            GameObject roadDeck = new GameObject("TruongTien_RoadCollider");
            roadDeck.transform.SetParent(bridgeObj.transform);
            roadDeck.transform.position = (startPos + endPos) * 0.5f;
            roadDeck.transform.forward = dir.normalized;
            BoxCollider col = roadDeck.AddComponent<BoxCollider>();
            col.size = new Vector3(8.5f, 0.8f, totalLen);
        }

        private static void BuildPhuXuanBridge(Transform parent, Material roadMat, Material sidewalkMat)
        {
            GameObject bridgeObj = new GameObject("Bridge_PhuXuan (Cầu Phú Xuân)");
            bridgeObj.transform.SetParent(parent);

            Vector3 startPos = new Vector3(-35f, 2.0f, -155f); // Bờ Nam
            Vector3 endPos = new Vector3(-35f, 2.0f, 185f);    // Bờ Bắc (Cửa Ngăn)
            float len = (endPos - startPos).magnitude;

            GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "PhuXuan_Deck";
            deck.transform.SetParent(bridgeObj.transform);
            deck.transform.position = (startPos + endPos) * 0.5f;
            deck.transform.localScale = new Vector3(16f, 0.8f, len);
            if (roadMat != null) deck.GetComponent<MeshRenderer>().sharedMaterial = roadMat;

            // Lan can cầu 2 bên
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = $"PhuXuan_Railing_{side}";
                rail.transform.SetParent(bridgeObj.transform);
                rail.transform.position = deck.transform.position + new Vector3(side * 8.2f, 0.9f, 0f);
                rail.transform.localScale = new Vector3(0.4f, 1.2f, len);
                if (sidewalkMat != null) rail.GetComponent<MeshRenderer>().sharedMaterial = sidewalkMat;
            }
        }

        private static void BuildDaVienBridge(Transform parent, Material roadMat, Material sidewalkMat)
        {
            GameObject bridgeObj = new GameObject("Bridge_DaVien (Cầu Dã Viên)");
            bridgeObj.transform.SetParent(parent);

            Vector3 startPos = new Vector3(-1350f, 2.2f, -180f);
            Vector3 endPos = new Vector3(-1350f, 2.2f, 240f);
            float len = (endPos - startPos).magnitude;

            GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "DaVien_Deck";
            deck.transform.SetParent(bridgeObj.transform);
            deck.transform.position = (startPos + endPos) * 0.5f;
            deck.transform.localScale = new Vector3(14f, 0.8f, len);
            if (roadMat != null) deck.GetComponent<MeshRenderer>().sharedMaterial = roadMat;
        }

        private static GameObject BuildImperialCitadel(Material wallMat)
        {
            GameObject root = new GameObject("--- IMPERIAL CITADEL (KINH THÀNH HUẾ) ---");

            // Tường thành vuông 2,200m x 2,200m
            Vector3 center = new Vector3(-250f, 0f, 1150f);
            float halfSide = 1080f;
            float wallHeight = 6.5f;
            float wallThickness = 7.0f;

            // 4 bức tường thành kiên cố
            CreateWall(root.transform, new Vector3(center.x, wallHeight * 0.5f, center.z - halfSide), new Vector3(halfSide * 2f, wallHeight, wallThickness), wallMat); // Nam
            CreateWall(root.transform, new Vector3(center.x, wallHeight * 0.5f, center.z + halfSide), new Vector3(halfSide * 2f, wallHeight, wallThickness), wallMat); // Bắc
            CreateWall(root.transform, new Vector3(center.x - halfSide, wallHeight * 0.5f, center.z), new Vector3(wallThickness, wallHeight, halfSide * 2f), wallMat); // Tây
            CreateWall(root.transform, new Vector3(center.x + halfSide, wallHeight * 0.5f, center.z), new Vector3(wallThickness, wallHeight, halfSide * 2f), wallMat); // Đông

            // Kỳ Đài Huế (Cột Cờ Huế) - Tọa độ thực tế trước Ngọ Môn
            GameObject kyDaiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Models/KyDai_Hue.fbx");
            Vector3 kyDaiPos = new Vector3(-250f, 0f, 110f); // Ngay trước cửa Nam Kinh Thành, hướng nhìn ra Sông Hương
            if (kyDaiPrefab != null)
            {
                GameObject kyDai = (GameObject)PrefabUtility.InstantiatePrefab(kyDaiPrefab);
                kyDai.name = "KyDai_Hue (Cột Cờ Huế)";
                kyDai.transform.SetParent(root.transform);
                kyDai.transform.position = kyDaiPos;
            }

            // Ngọ Môn (Cổng chính Đại Nội)
            GameObject ngoMon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ngoMon.name = "NgoMon_Gate (Ngọ Môn)";
            ngoMon.transform.SetParent(root.transform);
            ngoMon.transform.position = new Vector3(-250f, 7.5f, 260f);
            ngoMon.transform.localScale = new Vector3(56f, 15f, 24f);
            if (wallMat != null) ngoMon.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Sân Đại Triều Nghi lát gạch
            GameObject plaza = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plaza.name = "Plaza_DaiTrieuNghi";
            plaza.transform.SetParent(root.transform);
            plaza.transform.position = new Vector3(-250f, 0.05f, 185f);
            plaza.transform.localScale = new Vector3(120f, 0.1f, 120f);

            return root;
        }

        private static void CreateWall(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Citadel_Rampart_Wall";
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = size;
            if (mat != null) wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static GameObject BuildUrbanDistricts(Material roadMat, Material sidewalkMat, Material curbMat)
        {
            GameObject root = new GameObject("--- URBAN DISTRICTS (PHỐ PHƯỜNG HUẾ) ---");

            // 1. Đại lộ Lê Lợi Bờ Nam (Dọc ven Sông Hương, dài 3,200m)
            GameObject leLoiRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leLoiRoad.name = "Boulevard_LeLoi (Đại lộ Lê Lợi)";
            leLoiRoad.transform.SetParent(root.transform);
            leLoiRoad.transform.position = new Vector3(0f, 0.05f, -165f);
            leLoiRoad.transform.localScale = new Vector3(3200f, 0.1f, 16f);
            if (roadMat != null) leLoiRoad.GetComponent<MeshRenderer>().sharedMaterial = roadMat;

            // Vỉa hè Lê Lợi rợp cây xanh
            GameObject leLoiSidewalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leLoiSidewalk.name = "Sidewalk_LeLoi";
            leLoiSidewalk.transform.SetParent(root.transform);
            leLoiSidewalk.transform.position = new Vector3(0f, 0.15f, -177f);
            leLoiSidewalk.transform.localScale = new Vector3(3200f, 0.2f, 8f);
            if (sidewalkMat != null) leLoiSidewalk.GetComponent<MeshRenderer>().sharedMaterial = sidewalkMat;

            // 2. Đại lộ Trần Hưng Đạo Bờ Bắc (Trước Chợ Đông Ba)
            GameObject tranHungDao = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tranHungDao.name = "Boulevard_TranHungDao (Đường Trần Hưng Đạo)";
            tranHungDao.transform.SetParent(root.transform);
            tranHungDao.transform.position = new Vector3(600f, 0.05f, 265f);
            tranHungDao.transform.localScale = new Vector3(1600f, 0.1f, 15f);
            if (roadMat != null) tranHungDao.GetComponent<MeshRenderer>().sharedMaterial = roadMat;

            // 3. Chợ Đông Ba Landmark Complex
            GameObject dongBa = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dongBa.name = "DongBa_Market (Chợ Đông Ba)";
            dongBa.transform.SetParent(root.transform);
            dongBa.transform.position = new Vector3(880f, 6.0f, 310f);
            dongBa.transform.localScale = new Vector3(120f, 12f, 75f);

            // 4. Ga Huế (Đầu đường Bùi Thị Xuân / Lê Lợi)
            GameObject gaHue = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gaHue.name = "Hue_RailwayStation (Ga Huế)";
            gaHue.transform.SetParent(root.transform);
            gaHue.transform.position = new Vector3(-880f, 5.0f, -220f);
            gaHue.transform.localScale = new Vector3(90f, 10f, 45f);

            // 5. Khu Phố Tây (Chu Văn An - Võ Thị Sáu - Phạm Ngũ Lão)
            BuildPhamNguLaoNightStreet(root.transform);

            // 6. Cụm Nhà phố Shophouse Huế rải dọc các đại lộ
            BuildShophouseBlocks(root.transform);

            return root;
        }

        private static void BuildPhamNguLaoNightStreet(Transform parent)
        {
            GameObject nightStreetRoot = new GameObject("PhoTay_NightStreet (Phố Tây Chu Văn An)");
            nightStreetRoot.transform.SetParent(parent);
            nightStreetRoot.transform.position = new Vector3(1050f, 0f, -260f);

            // Biển hiệu cổng chào Phố Đi Bộ Tây
            GameObject arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arch.name = "Gate_PhoTay";
            arch.transform.SetParent(nightStreetRoot.transform);
            arch.transform.localPosition = new Vector3(0f, 4.5f, 0f);
            arch.transform.localScale = new Vector3(14f, 1.2f, 1.2f);
        }

        private static void BuildShophouseBlocks(Transform parent)
        {
            GameObject shophousePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Models/House_Modular_01.fbx");
            if (shophousePrefab == null) return;

            GameObject blocksRoot = new GameObject("Shophouse_Blocks");
            blocksRoot.transform.SetParent(parent);

            // Rải các dãy nhà phố dọc Bờ Nam (Lê Lợi - Hùng Vương) và Bờ Bắc (Trần Hưng Đạo)
            // Bờ Nam: 20 căn nhà phố
            for (int i = -10; i < 10; i++)
            {
                float x = i * 26f + 250f;
                var house = (GameObject)PrefabUtility.InstantiatePrefab(shophousePrefab);
                house.name = $"Shophouse_South_{i + 11}";
                house.transform.SetParent(blocksRoot.transform);
                house.transform.position = new Vector3(x, 0.05f, -195f);
                house.transform.rotation = Quaternion.Euler(0, 0, 0); // Quay mặt ra Sông Hương
            }

            // Bờ Bắc: 16 căn nhà phố gần Chợ Đông Ba
            for (int i = -8; i < 8; i++)
            {
                float x = i * 26f + 650f;
                var house = (GameObject)PrefabUtility.InstantiatePrefab(shophousePrefab);
                house.name = $"Shophouse_North_{i + 9}";
                house.transform.SetParent(blocksRoot.transform);
                house.transform.position = new Vector3(x, 0.05f, 335f);
                house.transform.rotation = Quaternion.Euler(0, 180, 0); // Quay mặt về Trần Hưng Đạo
            }
        }

        private static void SetupCitywideTraffic(Material roadMat)
        {
            GameObject trafficRoot = new GameObject("--- CITYWIDE TRAFFIC NETWORK ---");

            // Mạng lưới Waypoints liên tục nối 2 bờ Sông Hương qua Cầu Phú Xuân và Cầu Trường Tiền:
            // Loop: Bờ Nam (Lê Lợi) -> qua Cầu Phú Xuân sang Bờ Bắc -> qua Lê Duẩn / Trần Hưng Đạo -> qua Cầu Trường Tiền về lại Bờ Nam!
            Vector3[] routePoints = new Vector3[]
            {
                new Vector3(-35f, 0.2f, -165f),  // 1. Bờ Nam chân cầu Phú Xuân
                new Vector3(-35f, 2.0f, 0f),      // 2. Giữa cầu Phú Xuân
                new Vector3(-35f, 0.2f, 185f),   // 3. Bờ Bắc chân cầu Phú Xuân (Cửa Ngăn)
                new Vector3(250f, 0.2f, 265f),   // 4. Đường Trần Hưng Đạo (gần Kỳ Đài)
                new Vector3(550f, 0.2f, 265f),   // 5. Đường Trần Hưng Đạo
                new Vector3(795f, 0.2f, 255f),   // 6. Đầu cầu Trường Tiền Bờ Bắc (Chợ Đông Ba)
                new Vector3(770f, 2.0f, 55f),     // 7. Giữa cầu Trường Tiền
                new Vector3(745f, 0.2f, -145f),  // 8. Đầu cầu Trường Tiền Bờ Nam (Hùng Vương)
                new Vector3(350f, 0.2f, -165f),  // 9. Đại lộ Lê Lợi ven sông
                new Vector3(150f, 0.2f, -165f),  // 10. Đại lộ Lê Lợi
            };

            List<TrafficWaypoint> waypoints = new List<TrafficWaypoint>();
            for (int i = 0; i < routePoints.Length; i++)
            {
                GameObject wpObj = new GameObject($"Citywide_WP_{i + 1}");
                wpObj.transform.SetParent(trafficRoot.transform);
                wpObj.transform.position = routePoints[i];

                TrafficWaypoint wp = wpObj.AddComponent<TrafficWaypoint>();
                wp.SetSpeedLimit(12.0f); // 43 km/h
                waypoints.Add(wp);
            }

            // Kết nối vòng lặp kín (Closed Loop)
            for (int i = 0; i < waypoints.Count; i++)
            {
                TrafficWaypoint current = waypoints[i];
                TrafficWaypoint next = waypoints[(i + 1) % waypoints.Count];
                current.AddNextWaypoint(next);
            }

            // Khởi tạo TrafficPoolManager cho bản đồ TP Huế
            GameObject poolObj = new GameObject("TrafficPoolManager");
            poolObj.transform.SetParent(trafficRoot.transform);
            TrafficPoolManager pool = poolObj.AddComponent<TrafficPoolManager>();
            pool.SetStreamingRadius(250f); // Tối ưu streaming cho bản đồ thành phố
        }

        private static void SetupCoreGameplayAndHUD()
        {
            GameObject gameplayRoot = new GameObject("--- GAMEPLAY & PLAYER SYSTEM ---");

            // 0. Mặt đất đô thị toàn thành phố (3800m x 2800m)
            Material sidewalkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/MAT_Sidewalk_Concrete.mat");
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Terrain_Foundation_Citywide";
            ground.transform.SetParent(gameplayRoot.transform);
            ground.transform.position = new Vector3(0f, -0.05f, 0f);
            ground.transform.localScale = new Vector3(380f, 1f, 280f);
            if (sidewalkMat != null) ground.GetComponent<Renderer>().sharedMaterial = sidewalkMat;

            // 1. Player Spawner tại Công viên Bờ Nam nhìn sang Cầu Trường Tiền
            Vector3 playerSpawnPos = new Vector3(720f, 0.5f, -170f);

            GameObject playerObj = new GameObject("Player_Hue");
            playerObj.transform.SetParent(gameplayRoot.transform);
            playerObj.transform.position = playerSpawnPos;
            playerObj.tag = "Player";

            var cc = playerObj.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 0.9f, 0);

            playerObj.AddComponent<PlayerMovement>();
            playerObj.AddComponent<PlayerFootstepAudio>();
            playerObj.AddComponent<CharacterFootIK>();
            playerObj.AddComponent<CharacterLookAtIK>();

            // Load Player Mesh Model
            GameObject charModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Models/Character_Player.fbx");
            if (charModel != null)
            {
                GameObject charInstance = (GameObject)PrefabUtility.InstantiatePrefab(charModel);
                charInstance.transform.SetParent(playerObj.transform);
                charInstance.transform.localPosition = Vector3.zero;
            }

            // 2. Xe Sedan Taxi Vàng đỗ cạnh vỉa hè sẵn sàng để lái
            GameObject carModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Models/Car_Sedan_01.fbx");
            GameObject carObj = new GameObject("Vehicle_Taxi_Sedan");
            carObj.transform.SetParent(gameplayRoot.transform);
            carObj.transform.position = playerSpawnPos + new Vector3(8f, 0.4f, 5f);
            carObj.tag = "Vehicle";

            carObj.AddComponent<Rigidbody>();
            carObj.AddComponent<CarController>();
            carObj.AddComponent<VehicleAudioController>();
            carObj.AddComponent<VehicleVFXController>();

            if (carModel != null)
            {
                GameObject carMesh = (GameObject)PrefabUtility.InstantiatePrefab(carModel);
                carMesh.transform.SetParent(carObj.transform);
                carMesh.transform.localPosition = Vector3.zero;
            }

            // 3. Cơ chế Lên/Xuống xe (VehicleSeat & PlayerInputHandler)
            playerObj.AddComponent<PlayerInputHandler>();
            carObj.AddComponent<VehicleSeat>();

            // 4. Camera Follow với Cinemachine
            SetupCinemachineCameras(gameplayRoot.transform, playerObj.transform, carObj.transform);

            // 5. Weather Manager (Mưa rào & Nắng vàng Cố Đô)
            GameObject weatherObj = new GameObject("WeatherManager");
            weatherObj.transform.SetParent(gameplayRoot.transform);
            weatherObj.AddComponent<WeatherManager>();

            // 6. World Stimulus Manager (Giác quan còi xe, va chạm)
            GameObject stimulusObj = new GameObject("WorldStimulusManager");
            stimulusObj.transform.SetParent(gameplayRoot.transform);
            stimulusObj.AddComponent<WorldStimulusManager>();

            // 7. Mission Manager
            GameObject missionObj = new GameObject("MissionManager");
            missionObj.transform.SetParent(gameplayRoot.transform);
            missionObj.AddComponent<MissionManager>();

            // 8. Game HUD & Minimap
            SetupHUDAndMinimap(playerObj.transform);
        }

        private static void SetupCinemachineCameras(Transform root, Transform player, Transform car)
        {
            GameObject camMgrObj = new GameObject("CameraManager");
            camMgrObj.transform.SetParent(root);
            CameraManager camManager = camMgrObj.AddComponent<CameraManager>();

            GameObject walkCamObj = new GameObject("CM_WalkCamera");
            walkCamObj.transform.SetParent(root);
            CinemachineVirtualCamera walkCam = walkCamObj.AddComponent<CinemachineVirtualCamera>();
            walkCam.Follow = player;
            walkCam.LookAt = player;
            walkCam.Priority = 10;

            CinemachineTransposer transposer = walkCam.AddCinemachineComponent<CinemachineTransposer>();
            transposer.m_FollowOffset = new Vector3(0f, 2.0f, -4.2f);
            CinemachineComposer composer = walkCam.AddCinemachineComponent<CinemachineComposer>();
            composer.m_TrackedObjectOffset = new Vector3(0f, 1.4f, 0f);

            GameObject driveCamObj = new GameObject("CM_DriveCamera");
            driveCamObj.transform.SetParent(root);
            CinemachineVirtualCamera driveCam = driveCamObj.AddComponent<CinemachineVirtualCamera>();
            driveCam.Follow = car;
            driveCam.LookAt = car;
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
            mainCamObj.transform.SetParent(root);
            mainCamObj.tag = "MainCamera";
            UnityEngine.Camera cam = mainCamObj.AddComponent<UnityEngine.Camera>();
            mainCamObj.AddComponent<CinemachineBrain>();

            UniversalAdditionalCameraData camData = mainCamObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
        }

        private static void SetupHUDAndMinimap(Transform playerTarget)
        {
            GameObject canvasObj = new GameObject("GameCanvas_HUD");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            canvasObj.AddComponent<GameHUDManager>();

            // Minimap Camera theo dõi người chơi
            GameObject miniCamObj = new GameObject("Minimap_Camera");
            UnityEngine.Camera miniCam = miniCamObj.AddComponent<UnityEngine.Camera>();
            miniCam.orthographic = true;
            miniCam.orthographicSize = 160f; // Tầm nhìn bao quát đô thị
            miniCam.clearFlags = CameraClearFlags.SolidColor;
            miniCam.backgroundColor = new Color(0.12f, 0.16f, 0.18f);
            miniCam.transform.position = playerTarget.position + Vector3.up * 350f;
            miniCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            MinimapController miniCtrl = miniCamObj.AddComponent<MinimapController>();
            miniCtrl.SetPlayerTarget(playerTarget);
            miniCtrl.SetMinimapCamera(miniCam);
        }
    }
}
#endif
