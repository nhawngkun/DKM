# Open World - map thế giới mở (bản dựng 2)

Scene: `Assets/_Asset/World/OpenWorld.unity` · Dựng lại: `Tools/World/Build Open World`
(`Assets/_Asset/Script/Editor/World/OpenWorldBuilder.cs`) · Bản đồ: `gamedesign/env/world/world_map_labeled.jpg`
(vẽ lại bằng `python gamedesign/env/_build_world_map.py`) · Ảnh kiểm tra: `gamedesign/env/world/world_*.jpg`

> Asset môi trường lấy từ `Naraka_Env` (tách từ Naraka: Bladepoint, NetEase) qua `NarakaWorld.unitypackage`: chỉ dùng
> nội bộ, **không đưa vào bản phát hành**. Thêm asset sẵn có của DKM: FeudalMap (torii, chùa tháp, điện, đền, đèn đá,
> anh đào, trúc), Erbeilo3D (dương xỉ, việt quất, đèn đá, đá). Bố cục tự thiết kế.

## Tham chiếu (nguyên tắc, không chép bản đồ)

| Game | Lấy | Không lấy |
|---|---|---|
| Ghost of Tsushima | mỗi vùng một màu chủ đạo (đồng lau vàng, rừng phong đỏ, đồi hoa trắng) [Quan sát]; trảng trống rộng để thấy landmark; đền trên đồi dẫn bằng hàng torii [Quan sát]; nắng giờ vàng có sương | bản đồ Tsushima, địa danh, gió dẫn đường |
| Black Myth: Wukong | quần thể chùa núi với tượng lớn làm landmark [Quan sát]; rừng cổ thụ rậm nhiều tầng; vùng đặc trưng tách hẳn nhau (hẻm cát vàng, núi tuyết) [Quan sát] | kiến trúc, nhân vật, bố cục chương |

## Vùng (Voronoi, biên pha trộn hẹp ~40 m, độ cao pha rộng hơn để không thành vách)

| Vùng | Địa hình | Thực vật / vật thể |
|---|---|---|
| Dãy Tuyết Sơn | sống núi tới ~350 m, hạ dần ra biển | thông thưa, đá, vách đá, tuyết trên ~250 m |
| Rừng Thông Cổ | đồi 70-165 m | thông dày (cụm), đa, dương xỉ, việt quất, đá rêu |
| Thung Lũng Phong Đỏ | đồi 48-100 m, sông chảy qua | phong đỏ dày + anh đào, cỏ, hoa |
| Hẻm Cát Vàng | cao nguyên bậc thang, hẻm cắt | đá đỏ/cát, cỏ khô, vách đá |
| Rừng Trúc | đồi 40-95 m | trúc Naraka + trúc FeudalMap, bụi trúc, dương xỉ |
| Đồng Lau Vàng | đồng bằng rộng 25-34 m | thảm lau/cỏ/hoa (terrain detail), cây lẻ |
| Đầm Sen | thấp, hồ lớn có đảo, vũng nước | lau sậy, sen, đa, anh đào |
| Đồi Hoa Trắng | đồi thoải 30-68 m | thảm hoa trắng/tím, anh đào lẻ |
| Bờ Biển Nam | bãi thoải | thông ven biển, bụi, đá |

## Điểm đến

| Tên | Loại | Nội dung |
|---|---|---|
| Làng Lau | làng | 12 nhà + 2 điện nhỏ, giếng, chuông, đèn đá, đồ làng; spawn người chơi |
| Làng Chài | làng | 8 nhà, giếng, đèn đá |
| Làng Trúc | làng | 6 nhà + điện nhỏ, giếng, đèn đá |
| Chùa Tuyết Sơn | chùa | điện chính, 2 chùa tháp, tượng Phật lớn, chuông, 14 đèn đá, lư lửa, hàng torii |
| Đền Phong | đền | đền + chùa tháp trên đồi, hàng torii, đèn đá |
| Đền Sen | đền | đền trên đảo giữa hồ, đường đắp qua hồ |
| Đền Biển | đền | đền + torii đen trên đồi ven biển, hàng torii |
| Trạm Hẻm Cát, Chòi Tiều Phu | trạm | tháp canh, nhà, cổng / lư lửa |

Công trình chỉ đặt ở chỗ chênh cao < 4 m (nới tới 9 m), san nền theo cao độ trung bình. Đền/chùa xếp theo trục lối vào:
điện chính phía sau, tượng giữa sân, tháp hai bên, đèn đá hai hàng dọc lối vào.

## Ánh sáng

Volume gốc FeudalMap chỉnh lại cho trong và sáng: phơi sáng cố định -0.25 (FeudalMap 0.4), sky gradient nhạt
xanh-kem, nắng 30 lux ấm chiếu xiên từ tây-tây nam, đèn fill 10 lux không bóng, ambient **động** từ sky (volume gốc lấy
ambient từ bake, map này không bake nên trước đó mặt khuất nắng đen kịt), indirect x2.1, sương thể tích mỏng (2000 m,
cao 120 m), bỏ film grain, bloom 0.08, contrast +14, saturation +12.
**Không dùng reflection probe realtime**: 4 probe realtime + thảm cỏ dày từng làm GPU treo (D3D12 device removed, Editor crash)
khi vào play mode ngày 2026-11-21. Nếu cần phản chiếu: dùng probe Baked và bake trong Editor.
Chỉnh nhanh không cần build lại: `Assets/_Asset/World/VP_OpenWorld.asset` (Exposure.fixedExposure: thấp hơn = sáng hơn),
nhưng nhớ sửa cùng giá trị trong `Lighting()` của builder, nếu không lần build sau sẽ ghi đè.

## Material thực vật

Lá Naraka: HDRP/Lit Translucent + diffusion profile Foliage, hai mặt (mirror), màu gốc tối thì đưa về trắng, mip giữ
coverage để cây xa không trơ cành. Bị loại: cỏ `grass_com_green_*` (material không texture, thành mảng trắng), dây leo
`shrub_com_vine_*` (dây treo, đặt dưới đất thành vật lơ lửng), măng trúc `shrub_com_bamboo_01/03` (khối nón lạ),
mẫu `__miss`. Catalog từng mẫu: `gamedesign/env/world/catalog/`.

## Việc còn lại

- Thảm cỏ/hoa (terrain detail) chỉ hiện trong 180 m; xa hơn đồng lau/đồi hoa trông là đất trống. Cân nhắc tăng khoảng
  cách hoặc tô terrain đậm màu hơn theo vùng.
- Tóc Dante (shader `RE_Hair`) ám xanh trong ảnh chụp; chưa xem được trong play mode (Editor crash). Nếu còn thì chỉnh
  material tóc (dùng chung mọi scene) hoặc thêm probe Baked.
- Thảm cỏ: ~830k cụm, hiện trong 150 m (giảm từ 1,19 triệu / 180 m sau sự cố GPU).
- Thân cây phong ở gần hơi ám xanh lam; lá cỏ gần camera còn răng cưa ở mép alpha.
- `Pine_0-2` (FeudalMap) builder không đọc được renderer, đang dùng thông Naraka thay.
- Ảnh chụp trong batchmode vẫn đen mặt khuất (HDRP cần nhiều khung để cập nhật ambient): build trong Editor để có ảnh đúng.
- Chưa NavMesh, chưa tối ưu (LOD/occlusion), chưa playtest.
