# Chuẩn hoá dữ liệu MediaPipe -> CSV toạ độ Unity (đã làm mượt, bỏ hướng xoay theo camera)
import sys, json, numpy as np
src, dst, sigma, yaw_sigma = sys.argv[1], sys.argv[2], float(sys.argv[3]), float(sys.argv[4])
tilt_t0 = float(sys.argv[5]) if len(sys.argv) > 5 else -1e9; tilt_t1 = float(sys.argv[6]) if len(sys.argv) > 6 else 1e9
d = json.load(open(src)); fr = d["frames"]; n = len(fr)
P = np.full((n, 33, 3), np.nan); T = np.array([f["t"] for f in fr])
for i, f in enumerate(fr):
    if f["ok"]:
        w = np.array(f["w"])[:, :3]; P[i] = np.stack([w[:, 0], -w[:, 1], w[:, 2]], 1)  # MediaPipe (phải, xuống, xa) -> Unity (phải, lên, xa)
# điền khung hỏng bằng nội suy
ok = ~np.isnan(P[:, 0, 0])
for j in range(33):
    for k in range(3):
        P[:, j, k] = np.interp(np.arange(n), np.where(ok)[0], P[ok, j, k])
def gsmooth(a, s):
    if s <= 0: return a
    r = int(3 * s); ker = np.exp(-0.5 * (np.arange(-r, r + 1) / s) ** 2); ker /= ker.sum()
    pad = np.concatenate([a[:1].repeat(r, 0), a, a[-1:].repeat(r, 0)], 0)
    out = np.zeros_like(a)
    for i in range(len(a)): out[i] = np.tensordot(ker, pad[i:i + 2 * r + 1], axes=(0, 0))
    return out
P = gsmooth(P, sigma)
hip = (P[:, 23] + P[:, 24]) / 2; sh = (P[:, 11] + P[:, 12]) / 2
R = P[:, 24] - P[:, 23]; U = sh - hip
F = np.cross(R, U)   # cùng công thức Vector3.Cross của Unity
nose_dot = np.einsum('ij,ij->i', P[:, 0] - sh, F)
if np.mean(nose_dot) < 0: F = -F
yaw = np.unwrap(np.arctan2(F[:, 0], F[:, 2]))
yaw = gsmooth(yaw[:, None], yaw_sigma)[:, 0]
out = []
for i in range(n):
    c, s = np.cos(-yaw[i]), np.sin(-yaw[i])
    rot = np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])  # xoay quanh Y một góc -yaw
    q = (P[i] - hip[i]) @ rot.T
    out.append([T[i]] + q.reshape(-1).round(5).tolist())
# Bù góc nghiêng của camera: trung bình vector (giữa 2 cổ chân -> giữa hông) phải thẳng đứng
arr = np.array(out); pts = arr[:, 1:].reshape(n, 33, 3)
sel = (T >= tilt_t0) & (T <= tilt_t1)
v = (((pts[sel, 23] + pts[sel, 24]) / 2) - ((pts[sel, 27] + pts[sel, 28]) / 2)).mean(0); v /= np.linalg.norm(v)
up = np.array([0, 1, 0.]); ax = np.cross(v, up); sn = np.linalg.norm(ax); cs = v @ up
if sn > 1e-6:
    ax /= sn; K = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]])
    Rm = np.eye(3) + sn * K + (1 - cs) * K @ K
    pts = pts @ Rm.T
print(f"tilt corrected {np.degrees(np.arctan2(sn, cs)):.1f}deg")
out = np.concatenate([arr[:, :1], pts.reshape(n, -1)], 1)
np.savetxt(dst, np.array(out), delimiter=",", fmt="%.5f")
print(f"{dst}: n={n} filled={n - ok.sum()} noseFront={np.mean(nose_dot > 0) if np.mean(nose_dot) > 0 else np.mean(nose_dot < 0):.2f} yawRange={np.degrees(yaw.max() - yaw.min()):.0f}deg")
