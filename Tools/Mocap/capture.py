# Bắt khớp 3D từng khung hình bằng MediaPipe Pose Landmarker (chạy offline)
import sys, json, cv2, numpy as np, mediapipe as mp
from mediapipe.tasks.python import vision, BaseOptions
video, t0, t1, out_json, sheet, model = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), sys.argv[4], sys.argv[5], sys.argv[6]
opts = vision.PoseLandmarkerOptions(base_options=BaseOptions(model_asset_path=model), running_mode=vision.RunningMode.VIDEO,
    num_poses=1, min_pose_detection_confidence=0.4, min_pose_presence_confidence=0.4, min_tracking_confidence=0.4)
lm = vision.PoseLandmarker.create_from_options(opts)
cap = cv2.VideoCapture(video); fps = cap.get(cv2.CAP_PROP_FPS)
cap.set(cv2.CAP_PROP_POS_MSEC, t0 * 1000)
frames, thumbs, i = [], [], 0
conns = vision.PoseLandmarksConnections.POSE_LANDMARKS
while True:
    ok, img = cap.read()
    if not ok: break
    t = cap.get(cv2.CAP_PROP_POS_MSEC) / 1000.0
    if t > t1: break
    res = lm.detect_for_video(mp.Image(image_format=mp.ImageFormat.SRGB, data=cv2.cvtColor(img, cv2.COLOR_BGR2RGB)), int(t * 1000))
    rec = {"t": round(t - t0, 4), "ok": bool(res.pose_world_landmarks)}
    if res.pose_world_landmarks:
        rec["w"] = [[round(p.x, 4), round(p.y, 4), round(p.z, 4), round(p.visibility or 0, 3)] for p in res.pose_world_landmarks[0]]
        rec["i"] = [[round(p.x, 4), round(p.y, 4)] for p in res.pose_landmarks[0]]
    frames.append(rec)
    if i % 6 == 0:
        h, w = img.shape[:2]; v = img.copy()
        if rec["ok"]:
            pts = [(int(x * w), int(y * h)) for x, y in rec["i"]]
            for c in conns: cv2.line(v, pts[c.start], pts[c.end], (0, 255, 0), 3)
            for p in pts: cv2.circle(v, p, 4, (0, 0, 255), -1)
        v = cv2.resize(v, (480, int(480 * h / w))); cv2.putText(v, f"{t:.2f}s", (5, 22), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 255), 2)
        thumbs.append(v)
    i += 1
json.dump({"fps": fps, "t0": t0, "frames": frames}, open(out_json, "w"))
rows = []
for k in range(0, len(thumbs), 4):
    r = thumbs[k:k + 4]
    while len(r) < 4: r.append(np.zeros_like(thumbs[0]))
    rows.append(np.hstack(r))
cv2.imwrite(sheet, np.vstack(rows))
print(f"frames={len(frames)} detected={sum(f['ok'] for f in frames)}")
