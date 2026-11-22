# Project Guidelines: Open-World City Game (Unity + Blender Pipeline)

## 1. Tổng quan & Nguyên tắc vận hành
- **Thể loại:** Game thế giới mở góc nhìn thứ 3 (phong cách GTA quy mô nhỏ, bản đồ 500m × 500m, offline chơi đơn).
- **Công cụ:** Unity (C#) + Blender (kết hợp Blosm xuất bản đồ OpenStreetMap).
- **Quy tắc Phase-Gated (Bắt buộc):** Thực hiện tuần tự từng giai đoạn từ 1 đến 7. **Tuyệt đối không** nhảy sang giai đoạn tiếp theo khi người dùng chưa bấm Play kiểm thử thực tế trong Unity và xác nhận đạt yêu cầu.

---

## 2. Quy chuẩn nền tảng Unity C# & Dữ liệu (Base Unity Rules)
- **Cấu trúc thư mục bắt buộc:** Mọi tài nguyên tự tạo phải nằm trong `Assets/_Project/`:
  - `Scripts/` (chia rõ: `Core/`, `Player/`, `Vehicle/`, `Traffic/`, `NPC/`, `Mission/`, `UI/`, `Data/`)
  - `Prefabs/`, `Models/`, `Materials/`, `ScriptableObjects/`, `Scenes/`, `Audio/`
- **Hiệu năng (Performance):**
  - Cấm gọi `GetComponent()`, `Find()`, `FindObjectOfType()` bên trong `Update()` hoặc `FixedUpdate()`. Luôn cache tham chiếu trong `Awake()` hoặc `Start()`.
  - Dùng **Object Pooling** cho toàn bộ NPC đi bộ, xe giao thông (Traffic) và hiệu ứng (VFX). Không `Instantiate`/`Destroy` liên tục trong runtime.
  - Dùng **C# Events / Action** hoặc `ScriptableObject` để giao tiếp giữa các hệ thống (`MissionManager`, `UI`, `StimulusManager`, `Player`), tránh phụ thuộc chéo (spaghetti code).
- **Kiến trúc Dữ liệu (Không dùng CSDL ngoài):**
  - **Dữ liệu tĩnh** (thông số xe, danh sách nhiệm vụ, cấu hình tính cách NPC): Lưu bằng **`ScriptableObject`**.
  - **Dữ liệu Save/Load** (vị trí nhân vật, tiền, tiến độ nhiệm vụ): Lưu dạng file **JSON** tại `Application.persistentDataPath`.
  - **Cài đặt hệ thống** (âm lượng, độ nhạy chuột, đồ họa): Lưu bằng **`PlayerPrefs`**.
- **Kiểm lỗi tự động:** Sau mỗi lần tạo hoặc sửa script C#, luôn kiểm tra Unity Console để đảm bảo 0 lỗi compile trước khi báo hoàn thành.

---

## 3. Quy chuẩn Blender → Unity & Chiến lược Tài nguyên (Base 3D Rules)
- **Chiến lược nguồn Asset (Không tự code những thứ phức tạp):**
  - **Giai đoạn 1 & 2:** Chỉ dùng khối hộp trắng (Primitives) và mesh thô từ Blosm.
  - **Xe cộ, Cây cối, Đèn đường:** Ưu tiên dùng Asset sẵn có (Unity Asset Store, Sketchfab, Poly Haven) đã tách sẵn bánh xe và tối ưu polycount.
  - **Nhà cửa đặc trưng & Props địa phương:** Dựng dạng **Modular** trong Blender từ ảnh tham chiếu.
  - **Nhân vật & NPC:** Nhập từ công cụ Image-to-3D + Rigging Mixamo (định dạng `.fbx` Humanoid).
- **Quy chuẩn xuất file từ Blender:**
  - Hệ đơn vị mét (`1 Blender Unit = 1 Unity Meter`).
  - Trước khi xuất `.fbx` vào `Assets/_Project/Models/`, bắt buộc chạy: `Apply All Transforms (Scale/Rotation)`, `Merge by Distance`, và `Recalculate Normals Outside`.
- **Quy chuẩn Vật liệu (Texturing):**
  - **Cấm** dùng Procedural Pattern (`Wave`, `Checker`, `Brick Texture`) để giả lập vải quần áo hoặc da người.
  - Quần áo nhân vật (nếu xử lý trong Blender) phải tách thành lưới riêng (Separate Mesh + Solidify mỏng), không gán màu trực tiếp lên từng mặt lưới của thân người.
  - Luôn dùng bộ **PBR Texture thật** (Albedo, Roughness, Normal) từ Poly Haven hoặc thư mục texture local.

---

## 4. Kiến trúc NPC AI kiểu GTA (2 Tầng)
Áp dụng chính ở Giai đoạn 4 (Tầng 1) và tùy chọn mở rộng ở Giai đoạn 7 (Tầng 2):

### Tầng 1: Game AI truyền thống (Bắt buộc — Chạy mượt cho 15–25 NPC)
1. **Finite State Machine (FSM):** Mỗi NPC dùng `NavMeshAgent` và quản lý 5 trạng thái độc lập:
   - `Wander`: Đi dạo ngẫu nhiên dọc vỉa hè.
   - `Activity`: Dừng lại sinh hoạt (ngồi ghế nhựa, đứng bấm điện thoại, trò chuyện, trú mưa).
   - `Alert`: Quay đầu nhìn khi nghe tiếng còi xe, nẹt pô hoặc người chơi chạy sát qua.
   - `Flee`: Chạy ngược hướng nguồn nguy hiểm khi có tai nạn hoặc xe lao lên vỉa hè.
   - `Confront`: Phản kháng (đứng chửi hoặc lao tới đánh) nếu bị va chạm và có chỉ số tính cách hung hăng cao.
2. **Hệ thống Sự kiện / Giác quan (`WorldStimulusManager`):**
   - Không cho 25 NPC tự quét môi trường mỗi frame.
   - Khi có tác động (bấm còi, tông xe, phóng nhanh), phát sự kiện kèm `(Vector3 origin, float radius, StimulusType type)`.
   - NPC trong bán kính nhận sự kiện và tự chuyển trạng thái dựa trên 2 chỉ số ngẫu nhiên gắn lúc spawn: `courage` (0–1) và `aggression` (0–1).
3. **Điểm tương tác thông minh (`ActivityPoint`):**
   - Script gắn lên ghế vỉa hè, mái hiên, trạm xe buýt (có cờ `isOccupied` và `isSheltered`).
   - NPC đi ngang qua có xác suất vào sử dụng trong 15–30 giây rồi đi tiếp. Khi trời mưa, NPC tự ưu tiên tìm điểm có `isSheltered = true`.

### Tầng 2: Tích hợp LLM AI (Tùy chọn — Cuối Giai đoạn 7)
- **Quy tắc sống còn:** **Không bao giờ** chạy LLM đồng thời cho đám đông. Chỉ kích hoạt gọi LLM cho **duy nhất 1 NPC** khi người chơi tiến lại gần và bấm nút tương tác (`E`).
- Dùng `UnityWebRequest` (async) gọi API tốc độ cao (Gemini Flash) hoặc Ollama local, giới hạn câu trả lời ngắn gọn dưới 2 câu theo hồ sơ tính cách của NPC đó.

---

## 5. Lộ trình thực thi 7 Giai đoạn (Execution Checklist)
- **Giai đoạn 1:** Greyboxing & Cơ chế cốt lõi (Player movement, Cinemachine, Vehicle driving, Enter/Exit vehicle).
- **Giai đoạn 2:** Dựng khung bản đồ thực tế (Map Blocking từ Blosm, colliders, mở rộng làn đường 15-20%).
- **Giai đoạn 3:** Hoàn thiện Lát cắt dọc (Vertical Slice 1 ngã tư, Modular buildings, Props, Lighting).
- **Giai đoạn 4:** Thổi hồn thành phố (Traffic Splines, NavMesh, Object pooling, FSM 5 trạng thái NPC, Stimulus).
- **Giai đoạn 5:** Vòng lặp Gameplay & Giao diện (Missions via ScriptableObject, Minimap, Speedometer, Save/Load JSON).
- **Giai đoạn 6:** Mở rộng bản đồ & Tối ưu hiệu năng (500m x 500m, Occlusion Culling, LOD, GPU Instancing, Lightmap).
- **Giai đoạn 7:** Đánh bóng, Âm thanh & Phát hành (Engine audio pitch, VFX, rain, LLM optional, build .exe).
