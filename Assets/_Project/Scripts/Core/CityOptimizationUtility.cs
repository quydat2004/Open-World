#pragma warning disable CS0618
using UnityEngine;

namespace HueCity.Core
{
    /// <summary>
    /// Tiện ích Tối ưu hóa Hiệu năng Đô thị (City Optimization Utility).
    /// Hỗ trợ thiết lập GPU Instancing, LOD Group (Level of Detail), và cờ Occlusion Culling tĩnh.
    /// Giúp duy trì 60+ FPS mượt mà trên bản đồ mở rộng 500m x 500m.
    /// </summary>
    public static class CityOptimizationUtility
    {
        /// <summary>
        /// Kích hoạt GPU Instancing cho Material giúp gộp hàng trăm khối nhà, vỉa hè và mặt đường thành 1 Draw Call.
        /// </summary>
        public static void EnableGPUInstancing(Material mat)
        {
            if (mat != null && !mat.enableInstancing)
            {
                mat.enableInstancing = true;
            }
        }

        /// <summary>
        /// Thiết lập cấu trúc LOD Group cho khối nhà hoặc cụm kiến trúc.
        /// LOD 0: Chi tiết đầy đủ (Khoảng cách gần)
        /// LOD 1: Lưới giản lược / Giảm tải xử lý pixel (Khoảng cách trung bình)
        /// Culled: Tự động loại bỏ hoàn toàn ngoài tầm nhìn (Tiết kiệm Draw Calls tuyệt đối)
        /// </summary>
        public static LODGroup SetupBuildingLODGroup(GameObject buildingRoot, float lod0ScreenHeight = 0.35f, float lod1ScreenHeight = 0.08f)
        {
            if (buildingRoot == null) return null;

            LODGroup lodGroup = buildingRoot.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                lodGroup = buildingRoot.AddComponent<LODGroup>();
            }

            Renderer[] allRenderers = buildingRoot.GetComponentsInChildren<Renderer>(true);
            if (allRenderers == null || allRenderers.Length == 0) return lodGroup;

            LOD[] lods = new LOD[2];

            // LOD 0: Hiển thị đầy đủ mọi chi tiết ngoại thất, ban công, cửa sổ
            lods[0] = new LOD(lod0ScreenHeight, allRenderers);

            // LOD 1: Tối ưu khoảng cách xa (chỉ giữ lại khối khung chính của tòa nhà)
            var lod1Renderers = new System.Collections.Generic.List<Renderer>();
            foreach (var r in allRenderers)
            {
                if (r != null && !r.name.Contains("Balcony") && !r.name.Contains("Chair") && !r.name.Contains("Prop"))
                {
                    lod1Renderers.Add(r);
                }
            }

            lods[1] = new LOD(lod1ScreenHeight, lod1Renderers.Count > 0 ? lod1Renderers.ToArray() : allRenderers);

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            return lodGroup;
        }

        /// <summary>
        /// Gán cờ tĩnh tối ưu hóa (Static Flags) phục vụ Occlusion Culling, Batching và NavMesh.
        /// </summary>
        public static void SetOptimizedStaticFlags(GameObject obj, bool isOccluder = true)
        {
            if (obj == null) return;

#if UNITY_EDITOR
            UnityEditor.StaticEditorFlags flags = UnityEditor.StaticEditorFlags.BatchingStatic |
                                                  UnityEditor.StaticEditorFlags.OccludeeStatic |
                                                  UnityEditor.StaticEditorFlags.NavigationStatic;

            if (isOccluder)
            {
                flags |= UnityEditor.StaticEditorFlags.OccluderStatic;
            }

            UnityEditor.GameObjectUtility.SetStaticEditorFlags(obj, flags);

            foreach (Transform child in obj.transform)
            {
                SetOptimizedStaticFlags(child.gameObject, isOccluder);
            }
#endif
        }
    }
}
