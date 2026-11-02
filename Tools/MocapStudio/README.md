# Mocap Studio

Phần mềm motion capture từ video, chạy độc lập (không cần Unity, không cần cài đặt):
quét video → dựng xương → gắn model chạy thử → xuất BVH → đổi FBX → dùng trong Unity (Humanoid).

## Chạy

Mở `index.html` bằng Chrome hoặc Edge (nháy đúp). Cần mạng ở lần đầu để tải three.js và MediaPipe.

## Quy trình

1. **Video**: chọn video có **1 người, thấy toàn thân**, camera đứng yên là tốt nhất.
   Chọn đoạn (giây), FPS lấy mẫu, độ chính xác (heavy = chậm nhưng chuẩn hơn) → **Quét chuyển động**.
   Có thể **Lưu dữ liệu quét** (.mocap.json) để lần sau mở lại, không phải quét lại.
2. **Xử lý xương** (đổi là cập nhật ngay):
   - *Làm mượt*: tăng nếu xương rung, giảm nếu động tác nhanh bị nhòe.
   - *Hướng nhân vật*: "Theo video" giữ các cú xoay người; "Luôn nhìn thẳng" bỏ xoay (hợp cho idle, đánh tại chỗ).
   - *Gốc*: "Ghim chân xuống đất" = anim tại chỗ (hợp với KCC); "Theo video" = di chuyển ngang/nhảy theo hông trong video (ước lượng 2D).
   - *Bù nghiêng camera*: tự dựng thẳng người khi camera hơi chúi.
   - *Lật trái ↔ phải*: khi quay qua gương / cần bản đối xứng.
3. **Gắn model chạy thử**: nạp FBX/GLB humanoid (Mixamo, VRoid, Unity, UE, Rigify, Biped…). Tên xương được tự dò
   và bù khác biệt A-pose/T-pose. Model nên nhìn về +Z (mũi tên đỏ); nếu quay lưng thì tích "Xoay model 180°".
   Phím tắt: Space = phát/dừng, ← → = từng khung.
4. **Xuất**: đặt đoạn "Xuất từ … đến …", tên clip → **Xuất BVH**.

## BVH → FBX (cho Unity)

Kéo thả một hoặc nhiều file `.bvh` vào `bvh_to_fbx.bat` (tự tìm Blender trong `Program Files\Blender Foundation`;
cài chỗ khác thì đặt biến môi trường `BLENDER_EXE`). FBX được ghi cạnh file BVH.
FBX có pose gốc T-pose và tên xương kiểu Mixamo (Hips, Spine, Chest, Neck, Head, LeftArm, LeftForeArm, LeftUpLeg…).

Dòng lệnh tương đương:

```
blender -b --factory-startup -P bvh_to_fbx.py -- clip1.bvh clip2.bvh
```

## Trong Unity

1. Kéo FBX vào `Assets/`.
2. Inspector → **Rig** → Animation Type = **Humanoid**, Avatar Definition = *Create From This Model* → Apply.
3. Tab **Animation**: clip mang tên file; bật *Loop Time* nếu cần, *Root Transform Rotation/Position (Y)* → Bake Into Pose
   khi dùng chế độ "Ghim chân xuống đất".
4. Kéo clip vào Animator Controller của bất kỳ model Humanoid nào (vd Dante).

## Giới hạn

- MediaPipe Pose có 33 điểm: không có ngón tay chi tiết, xoay cổ tay/bàn chân là ước lượng.
- Độ sâu (trước/sau) kém chính xác hơn trái/phải — quay video hơi chéo 30–45° thường cho kết quả tốt hơn quay thẳng.
- Chỉ 1 người mỗi video; bị che khuất lâu sẽ được nội suy.
