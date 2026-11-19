# Lục Hợp Đại Thương (bản realistic): thân 2 nửa có khớp nối giữa (tháo ra xếp song song như gậy đôi),
# đầu thương + mảnh vải đỏ lấy từ model "Spear of the fire", móc treo tua nhỏ. Xương cho physics + 4 action.
# Material PBR procedural của thân được bake ra 4 bộ texture (Shaft / Cord / Metal / Tassel) cho HDRP;
# đầu thương + vải dùng texture gốc của model kia (bộ Fire).
# Chạy: blender -b --python build_spear.py -- [--preview] [--nobake]
import bpy, bmesh, math, sys, os, random
import numpy as np
from mathutils import Vector, Matrix

OUT_DIR = r"G:/UnityProject/DKM/SourceArt/LucHopDaiThuong"
UNITY_DIR = r"G:/UnityProject/DKM/Assets/_Asset/3D/Weapon/LucHopDaiThuong"
FBX_PATH = UNITY_DIR + "/LucHopDaiThuong.fbx"
TEX_DIR = UNITY_DIR + "/Textures"
LADY_FBX = r"G:/UnityProject/DKM/Assets/_Asset/Character/DMC5_Chr_Lady/DMC5_Chr_Lady/Lady.fbx"
HDRI_DIR = os.path.join(os.path.dirname(bpy.app.binary_path), f"{bpy.app.version[0]}.{bpy.app.version[1]}", "datafiles", "studiolights", "world")

LADY_H = 1.85          # chiều cao Lady trong Unity (đo từ Lady.fbx, globalScale 100)
RATIO = 1.8            # thương ~1.8 lần chiều cao người cầm, như trong clip
L = round(LADY_H * RATIO, 2)   # 3.33 m
R = 0.017              # bán kính thân ở chuôi
R_TOP = 0.0145         # bán kính thân sát đầu thương
ZH = L * 0.5           # mặt nối hai nửa
SIDE = 0.052           # khi tháo, nửa trên nằm cạnh nửa dưới cách nhau SIDE
SEG = 32
METAL_SEG = 20         # số cạnh tròn cho chi tiết kim loại (đai, khớp, đốc)

# ---- đầu thương + mảnh vải lấy từ "Spear of the fire" (đơn vị z gốc của Pear.fbx, đo bằng inspect) ----
FIRE_DIR = r"C:/Users/nbvho/Downloads/3d_Ripper_Pro_v110b/Downloads/AscoDelavol39e/01- Spear.of.the.fire"
FIRE_HEAD_Z = 0.0330           # từ đây lên: họng vàng + lưỡi
FIRE_KNOT_Z = (0.0296, 0.0307)  # vòng vải đỏ buộc quanh thân

def fire_components(me):
    """Chia mesh thành các mảnh rời -> [(chỉ số đỉnh, min, max)]."""
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    seen = set(); out = []
    for v in bm.verts:
        if v.index in seen: continue
        stack = [v]; comp = []; seen.add(v.index)
        while stack:
            x = stack.pop(); comp.append(x.index)
            for e in x.link_edges:
                y = e.other_vert(x)
                if y.index not in seen: seen.add(y.index); stack.append(y)
        cs = [bm.verts[i].co for i in comp]
        out.append((comp, Vector([min(c[k] for c in cs) for k in range(3)]), Vector([max(c[k] for c in cs) for k in range(3)])))
    bm.free()
    return out

def load_fire():
    """Nhập Pear.fbx, trả về mesh đã nhân ma trận world (đơn vị gốc) và xoá object tạm."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=FIRE_DIR + "/Pear.fbx")
    new = [o for o in bpy.data.objects if o not in before]
    src = next(o for o in new if o.type == "MESH")
    me = src.data.copy(); me.transform(src.matrix_world)
    for o in new: bpy.data.objects.remove(o)
    return me

def classify_fire(me):
    comps = fire_components(me)
    head = [c for c in comps if c[1].z >= FIRE_HEAD_Z]
    knot = [c for c in comps if FIRE_KNOT_Z[0] <= c[1].z and c[2].z <= FIRE_KNOT_Z[1] and len(c[0]) > 100]
    flag = [c for c in comps if c[1].x < -0.002]
    # đoạn thân ngay dưới họng -> bán kính để khớp tỉ lệ với thân Lục Hợp
    neck = [c for c in comps if 0.030 < c[1].z < 0.031 and 0.0325 < c[2].z < FIRE_HEAD_Z + 0.0005]
    r_neck = max(math.hypot(me.vertices[i].co.x, me.vertices[i].co.y) for c in neck for i in c[0] if me.vertices[i].co.z > 0.032)
    return head, knot, flag, r_neck

def measure_fire():
    me = load_fire()
    head, knot, flag, r_neck = classify_fire(me)
    S = R_TOP / r_neck
    top = max(c[2].z for c in head)
    fz0 = min(c[1].z for c in flag); fz1 = max(c[2].z for c in flag)
    kz1 = max(c[2].z for c in knot)
    vs = [me.vertices[i].co for c in flag for i in c[0]]
    fx = sum(v.x for v in vs) / len(vs); fy = sum(v.y for v in vs) / len(vs)
    bpy.data.meshes.remove(me)
    return {"S": S, "head_len": (top - FIRE_HEAD_Z) * S, "flag_top": (kz1 - FIRE_HEAD_Z) * S,
            "flag_bot": (fz0 - FIRE_HEAD_Z) * S, "flag_xy": (-fy * S, fx * S)}   # xoay 90° quanh Z: (x,y)->(-y,x)

FIRE = measure_fire()
Z_HEAD = L - FIRE["head_len"]
CHUNK = 0.3            # cắt UV thân dài thành đoạn để xếp texture đặc hơn

# nhóm texture: mỗi nhóm một bộ BaseColor / Normal / MaskMap
GROUPS = {"Shaft": 2048, "Cord": 2048, "Metal": 2048, "Tassel": 1024}
random.seed(7)

# ============================================================ geometry builder
class Builder:
    def __init__(self):
        self.bm = bmesh.new()
        self.wl = self.bm.verts.layers.deform.verify()
        self.uv = self.bm.loops.layers.uv.verify()
        self.auto = self.bm.faces.layers.int.new("auto_uv")
        self.mats = []            # tên material nguồn theo index
        self.bone_index = {}
        self.island = 0           # mỗi island UV dịch đi một khoảng để không dính nhau
        self.rec = None

    def start(self):
        self.rec = []

    def finish(self):
        """Tính lại hướng normal cho các mặt dựng từ start() (hình khép kín tự quay ra ngoài)."""
        bmesh.ops.recalc_face_normals(self.bm, faces=self.rec)
        self.rec = None

    def mi(self, name):
        if name not in self.mats: self.mats.append(name)
        return self.mats.index(name)

    def bidx(self, name):
        return self.bone_index.setdefault(name, len(self.bone_index))

    def vert(self, p, wfn):
        v = self.bm.verts.new(p)
        for bn, w in wfn(Vector(p)).items():
            v[self.wl][self.bidx(bn)] = w
        return v

    def face(self, vs, mat, uvs=None, smooth=True, auto=False):
        f = self.bm.faces.new(vs)
        f.material_index = self.mi(mat); f.smooth = smooth
        if self.rec is not None: self.rec.append(f)
        if uvs:
            for lp, uv in zip(f.loops, uvs): lp[self.uv].uv = uv
        else:
            f[self.auto] = 1
        return f

    def next_island(self):
        self.island += 1
        return self.island * 3.0

    def lathe(self, profile, mat, wfn, seg=SEG, center=(0, 0), cap=True, chunk=None, bevel=0.0):
        """profile [(z, r)] dọc trục Z. UV: u = cung tròn, v = chiều dài profile (m).
        Kim loại: ít cạnh hơn (METAL_SEG), không vát bằng hình học mà để góc cứng - độ bo cạnh nằm trong normal map bake."""
        metal = SRC_GROUP.get(mat) == "Metal"
        if metal:
            seg = min(seg, METAL_SEG); bevel = 0.0
        if bevel > 0: profile = chamfer(profile, bevel)
        rings = []
        for z, r in profile:
            rings.append([self.vert((center[0] + r * math.cos(2 * math.pi * i / seg),
                                     center[1] + r * math.sin(2 * math.pi * i / seg), z), wfn) for i in range(seg)])
        s = [0.0]
        for (z0, r0), (z1, r1) in zip(profile, profile[1:]):
            s.append(s[-1] + math.hypot(z1 - z0, r1 - r0))
        rref = max(r for _, r in profile)
        circ = 2 * math.pi * rref
        base = self.next_island()
        for j in range(len(rings) - 1):
            c = int(((s[j] + s[j + 1]) * 0.5) // chunk) if chunk else 0
            off = base + c * 0.5
            v0 = s[j] - (c * chunk if chunk else 0); v1 = s[j + 1] - (c * chunk if chunk else 0)
            a, b = rings[j], rings[j + 1]
            for i in range(seg):
                u0 = circ * i / seg; u1 = circ * (i + 1) / seg
                self.face((a[i], a[(i + 1) % seg], b[(i + 1) % seg], b[i]), mat,
                          [(off + u0, v0), (off + u1, v0), (off + u1, v1), (off + u0, v1)])
        if metal:   # góc gãy > 30° -> cạnh sắc (tách normal) thay cho vát
            for j in range(1, len(profile) - 1):
                d0 = Vector(profile[j]) - Vector(profile[j - 1]); d1 = Vector(profile[j + 1]) - Vector(profile[j])
                if d0.length > 1e-7 and d1.length > 1e-7 and d0.angle(d1) > math.radians(30):
                    for i in range(seg):
                        e = self.bm.edges.get((rings[j][i], rings[j][(i + 1) % seg]))
                        if e: e.smooth = False
        if cap:
            for ring, rev in ((rings[0], True), (rings[-1], False)):
                o = self.next_island()
                vs = list(reversed(ring)) if rev else ring
                f = self.face(vs, mat, [(o + v.co.x - center[0], v.co.y - center[1]) for v in vs], smooth=False)
                for e in f.edges: e.smooth = False
        return rings

def chamfer(profile, b):
    """Vát mọi góc gãy của profile -> highlight mềm ở cạnh như đồ thật."""
    out = [profile[0]]
    for i in range(1, len(profile) - 1):
        p0, p1, p2 = (Vector(profile[i - 1]), Vector(profile[i]), Vector(profile[i + 1]))
        d0 = (p1 - p0); d1 = (p2 - p1)
        if d0.length < 1e-6 or d1.length < 1e-6:
            out.append(profile[i]); continue
        ang = d0.angle(d1)
        if ang < math.radians(20):
            out.append(profile[i]); continue
        bb = min(b, d0.length * 0.4, d1.length * 0.4)
        out.append(tuple(p1 - d0.normalized() * bb)); out.append(tuple(p1 + d1.normalized() * bb))
    out.append(profile[-1])
    return out

def rigid(bone):
    return lambda p: {bone: 1.0}

def chain_weights(z, bones):
    """bones: [(name, z0, z1)] -> trọng số tent quanh tâm từng xương (thân uốn mượt khi physics)."""
    ws = {}
    for n, z0, z1 in bones:
        c = (z0 + z1) * 0.5; h = abs(z1 - z0)
        w = max(0.0, 1.0 - abs(z - c) / h)
        if w > 0: ws[n] = w
    if not ws:
        ws = {min(bones, key=lambda b: abs(z - (b[1] + b[2]) * 0.5))[0]: 1.0}
    s = sum(ws.values())
    return {k: v / s for k, v in ws.items()}

# ============================================================ bones
N_LOW = 4; N_UP = 4
LOW = [(f"Shaft_{i+1:02d}", ZH * i / N_LOW, ZH * (i + 1) / N_LOW) for i in range(N_LOW)]
UP = [(f"Shaft_{N_LOW+i+1:02d}", ZH + (Z_HEAD - ZH) * i / N_UP, ZH + (Z_HEAD - ZH) * (i + 1) / N_UP) for i in range(N_UP)]
Z_CHARM = Z_HEAD - 0.035
CHARM_X = R_TOP + 0.006
CHARM = [("Charm_1", 0.000, 0.035), ("Charm_2", 0.035, 0.060), ("Charm_3", 0.060, 0.095), ("Charm_4", 0.095, 0.130)]
# mảnh vải: chuỗi xương thẳng đứng theo tâm lá vải, từ nút buộc xuống mép dưới
CLOTH_N = 4
_ct, _cb = Z_HEAD + FIRE["flag_top"], Z_HEAD + FIRE["flag_bot"]
CLOTH = [(f"Cloth_{i+1}", _ct + (_cb - _ct) * i / CLOTH_N, _ct + (_cb - _ct) * (i + 1) / CLOTH_N) for i in range(CLOTH_N)]

def build_armature():
    arm = bpy.data.armatures.new("LucHopDaiThuong_Rig")
    ob = bpy.data.objects.new("LucHopDaiThuong_Rig", arm)
    bpy.context.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.edit_bones
    def nb(name, h, t, parent=None, connect=False):
        b = eb.new(name); b.head = h; b.tail = t; b.roll = 0
        if parent: b.parent = eb[parent]; b.use_connect = connect
    nb("Root", (0, 0, 0), (0, 0, 0.15))
    prev = "Root"
    for n, z0, z1 in LOW:
        nb(n, (0, 0, z0), (0, 0, z1), prev, connect=(prev != "Root")); prev = n
    nb("Upper", (0, 0, ZH), (0, 0, ZH + 0.08), prev)       # animation tháo/lắp chỉ key xương này
    prev = "Upper"
    for n, z0, z1 in UP:
        nb(n, (0, 0, z0), (0, 0, z1), prev, connect=(prev != "Upper")); prev = n
    nb("Head", (0, 0, Z_HEAD), (0, 0, L), prev, connect=True)
    p = UP[-1][0]
    for n, d0, d1 in CHARM:
        nb(n, (CHARM_X, 0, Z_CHARM - d0), (CHARM_X, 0, Z_CHARM - d1), p, connect=(p != UP[-1][0])); p = n
    p = UP[-1][0]; fx, fy = FIRE["flag_xy"]
    for n, z0, z1 in CLOTH:
        nb(n, (fx, fy, z0), (fx, fy, z1), p, connect=(p != UP[-1][0])); p = n
    nb("Grip", (0, 0, 0.55), (0, 0, 0.65), "Shaft_02")
    bpy.ops.object.mode_set(mode="OBJECT")
    return ob

# ============================================================ parts
r_at = lambda z: R + (R_TOP - R) * (z / L)
lw = lambda p: chain_weights(p.z, LOW)
uw = lambda p: chain_weights(p.z, UP)

CORD_PITCH = 0.006     # bước xoắn của dây giả (vẽ trong normal map, xem S_Cord)
CORD_THICK = 0.0024

def helix_wrap(B, z0, z1, wfn, mat="S_Cord"):
    """Dây đỏ quấn (fake): ống bọc trơn, vân dây xoắn chỉ nằm trong texture bake."""
    n = max(2, int((z1 - z0) / 0.03))
    prof = [(z0, r_at(z0) + 0.0006), (z0 + 0.0012, r_at(z0) + CORD_THICK * 0.85)]
    prof += [(z0 + 0.0025 + (z1 - z0 - 0.005) * i / n, r_at(z0 + (z1 - z0) * i / n) + CORD_THICK) for i in range(n + 1)]
    prof += [(z1 - 0.0012, r_at(z1) + CORD_THICK * 0.85), (z1, r_at(z1) + 0.0006)]
    B.lathe(prof, mat, wfn, chunk=CHUNK)

def box(B, c, s, mat, wfn, bev=0.0006):
    """Hộp vát cạnh."""
    bm_tmp = bmesh.new()
    bmesh.ops.create_cube(bm_tmp, size=1.0)
    bmesh.ops.scale(bm_tmp, vec=Vector(s) * 2, verts=bm_tmp.verts)
    bmesh.ops.bevel(bm_tmp, geom=list(bm_tmp.edges), offset=bev, segments=1, affect="EDGES", profile=0.5)
    vm = {v: B.vert(Vector(c) + v.co, wfn) for v in bm_tmp.verts}
    for f in bm_tmp.faces:
        B.face([vm[v] for v in f.verts], mat)
    bm_tmp.free()

def oval_plate(B, cz, side, rc, bone):
    """Tấm khoá bầu dục có vát (ref 2)."""
    B.start()
    oval_parts(B, cz, side, rc, bone)
    B.finish()
    box(B, (0, side * (rc + 0.0092), cz), (0.0018, 0.0009, 0.0085), "S_Glow", rigid(bone), 0.0003)

def oval_parts(B, cz, side, rc, bone):
    n = 14; v = []; prof =[(1.0, 0.0), (1.0, 0.0018), (0.88, 0.0032), (0.72, 0.0035)]
    rings = []
    for sc, hgt in prof:
        rings.append([B.vert((0.0085 * sc * math.cos(2 * math.pi * i / n), side * (rc + 0.0052 + hgt), cz + 0.015 * sc * math.sin(2 * math.pi * i / n)), rigid(bone)) for i in range(n)])
    for a, b in zip(rings, rings[1:]):
        for i in range(n):
            j = (i + 1) % n
            B.face((a[i], a[j], b[j], b[i]) if side < 0 else (a[i], b[i], b[j], a[j]), "S_Gunmetal")
    top = rings[-1] if side < 0 else list(reversed(rings[-1]))
    B.face(top[::-1] if side < 0 else top, "S_Gunmetal", smooth=False)

def coupler(B, sign, bone, wfn):
    rc = r_at(ZH)
    zz = lambda d: ZH + sign * d
    prof = [(0.0, rc + 0.0065), (0.012, rc + 0.0068), (0.016, rc + 0.005), (0.045, rc + 0.005), (0.05, rc + 0.0078),
            (0.062, rc + 0.0078), (0.066, rc + 0.005), (0.15, rc + 0.0042), (0.16, rc + 0.001)]
    p = [(zz(d), r) for d, r in prof]
    if sign < 0: p = p[::-1]
    B.lathe(p, "S_Gunmetal", rigid(bone), bevel=0.0007)
    g0, g1 = (zz(0.0015), zz(0.0045)) if sign > 0 else (zz(0.0045), zz(0.0015))
    B.lathe([(g0, rc + 0.0071), (g1, rc + 0.0071)], "S_Glow", rigid(bone), cap=False)   # đường sáng ở mặt nối
    for d0, d1 in ((0.044, 0.051), (0.061, 0.068)):
        q0, q1 = sorted((zz(d0), zz(d1)))
        B.lathe([(q0, rc + 0.0076), (q0, rc + 0.0084), (q1, rc + 0.0084), (q1, rc + 0.0076)], "S_Silver", rigid(bone), cap=False)
    for side in (-1, 1):
        oval_plate(B, zz(0.03), side, rc, bone)
    # dây quấn đỏ cạnh khớp nối + đai bạc chặn
    q0, q1 = sorted((zz(0.17), zz(0.33)))
    helix_wrap(B, q0, q1, wfn)
    collar(B, zz(0.158) if sign > 0 else zz(0.17), wfn, length=0.012, glow=False)
    collar(B, zz(0.335) if sign > 0 else zz(0.335) - 0.03, wfn)

def collar(B, z, wfn, length=0.03, glow=True):
    """Đai bạc có vạch sáng cyan ở giữa (concept)."""
    # profile bắt đầu/kết thúc ở mặt thân -> không cần nắp (nắp nằm trong thân, phí tris)
    r = r_at(z); Lc = length
    if glow:
        a, b = z + Lc * 0.38, z + Lc * 0.62
        B.lathe([(z, r), (z, r + 0.0044), (a, r + 0.0044), (a, r + 0.003), (b, r + 0.003), (b, r + 0.0044),
                 (z + Lc, r + 0.0044), (z + Lc, r)], "S_Silver", wfn, cap=False)
        B.lathe([(a, r + 0.0037), (b, r + 0.0037)], "S_Glow", wfn, cap=False)
    else:
        B.lathe([(z, r), (z, r + 0.0042), (z + Lc, r + 0.0042), (z + Lc, r)], "S_Silver", wfn, cap=False)

def build_mesh():
    B = Builder()
    lowB, upB = LOW[-1][0], UP[0][0]
    # thân kim loại đen
    zs = [0.10 + (ZH - 0.15 - 0.10) * i / 48 for i in range(49)]
    B.lathe([(z, r_at(z)) for z in zs], "S_Shaft", lw, chunk=CHUNK)
    zs = [ZH + 0.15 + (Z_HEAD + 0.02 - ZH - 0.15) * i / 48 for i in range(49)]
    B.lathe([(z, r_at(z)) for z in zs], "S_Shaft", uw, chunk=CHUNK)
    # đốc chuôi: mũi bạc + ống gunmetal + vạch sáng (concept)
    B.lathe([(-0.075, 0.0008), (-0.07, 0.003), (-0.04, 0.0085), (-0.015, 0.0125), (-0.01, R + 0.003), (0.0, R + 0.003),
             (0.004, R + 0.0012)], "S_Silver", lw, bevel=0.0006)
    B.lathe([(0.0, R + 0.0045), (0.003, R + 0.0062), (0.03, R + 0.0062), (0.034, R + 0.0048), (0.09, R + 0.0045),
             (0.094, R + 0.0055), (0.105, R + 0.0055), (0.11, R + 0.002), (0.125, R + 0.0006)], "S_Gunmetal", lw, bevel=0.0007)
    B.lathe([(0.058, R + 0.0049), (0.066, R + 0.0049)], "S_Glow", lw, cap=False)
    collar(B, 0.125, lw)
    # khớp nối giữa
    coupler(B, -1, lowB, lw)
    coupler(B, +1, upB, uw)
    B.lathe([(ZH - 0.002, r_at(ZH) * 0.55), (ZH + 0.018, r_at(ZH) * 0.55), (ZH + 0.024, r_at(ZH) * 0.38)], "S_Steel", rigid(lowB), seg=16, bevel=0.001)
    # dây đỏ chỗ cầm + đoạn gần đầu, chặn bằng đai bạc
    helix_wrap(B, 0.40, 0.78, lw)
    collar(B, 0.37, lw); collar(B, 0.78, lw)
    zw0, zw1 = Z_HEAD - 0.40, Z_HEAD - 0.15          # chừa chỗ cho nút vải buộc dưới đầu thương
    helix_wrap(B, zw0, zw1, uw)
    collar(B, zw0 - 0.03, uw); collar(B, zw1, uw)
    collar(B, 1.15, lw, length=0.024)
    rt = R_TOP
    # đầu thương + mảnh vải lấy từ Spear of the fire: ghép ở build_fire()
    # móc treo: vòng, khoen, dây, hạt, tua nhiều sợi
    up4 = UP[-1][0]
    B.lathe([(Z_CHARM - 0.004, rt + 0.0012), (Z_CHARM - 0.0025, rt + 0.0036), (Z_CHARM + 0.0025, rt + 0.0036), (Z_CHARM + 0.004, rt + 0.0012)], "S_Gold", rigid(up4), cap=False)
    cw = lambda p: chain_weights(Z_CHARM - p.z, CHARM)
    cx = (CHARM_X, 0)
    # khoen tròn (torus nhỏ), dây, tua: tính lại normal sau khi dựng
    B.start()
    ring = []
    for i in range(16):
        a = 2 * math.pi * i / 16
        cc = Vector((CHARM_X, 0.0035 * math.cos(a), Z_CHARM - 0.0075 + 0.0035 * math.sin(a)))
        nn = Vector((0, math.cos(a), math.sin(a)))
        ring.append([B.vert(cc + nn * 0.001 * math.cos(2 * math.pi * k / 8) + Vector((0.001 * math.sin(2 * math.pi * k / 8), 0, 0)), cw) for k in range(8)])
    for i in range(16):
        a, b = ring[i], ring[(i + 1) % 16]
        for k in range(8):
            B.face((a[k], b[k], b[(k + 1) % 8], a[(k + 1) % 8]), "S_Gold")
    # dây xoắn 2 sợi
    for ph in (0, math.pi):
        prof_rows = []
        for i in range(21):
            t = i / 20
            zc = Z_CHARM - 0.011 - 0.041 * t
            a = ph + t * 6 * math.pi
            c = Vector((CHARM_X + 0.0007 * math.cos(a), 0.0007 * math.sin(a), zc))
            prof_rows.append([B.vert(c + Vector((0.00075 * math.cos(2 * math.pi * k / 6), 0.00075 * math.sin(2 * math.pi * k / 6), 0)), cw) for k in range(6)])
        for a, b in zip(prof_rows, prof_rows[1:]):
            for k in range(6):
                B.face((a[k], a[(k + 1) % 6], b[(k + 1) % 6], b[k]), "S_Tassel")
    B.finish()
    bead = [(Z_CHARM - 0.06 + 0.0085 * (1 - math.cos(math.pi * i / 10)), 0.0072 * math.sin(math.pi * i / 10) + 0.0005) for i in range(11)]
    B.lathe(bead, "S_Gold", cw, seg=12, center=cx)
    zt = Z_CHARM - 0.061
    B.lathe([(zt - 0.009, 0.0042), (zt - 0.007, 0.0049), (zt - 0.0015, 0.0049), (zt, 0.0035), (zt + 0.0015, 0.0018)], "S_Gold", cw, seg=12, center=cx)
    # tua: 40 sợi chỉ xoè dần
    B.start()
    for s in range(40):
        a = random.uniform(0, 2 * math.pi); r0 = math.sqrt(random.random()) * 0.0034
        r1 = r0 * 1.6 + random.uniform(0.0, 0.0035)
        ln = random.uniform(0.058, 0.068)
        rows = []
        for i in range(9):
            t = i / 8
            rr = r0 + (r1 - r0) * (t ** 0.7)
            c = Vector((CHARM_X + rr * math.cos(a), rr * math.sin(a), zt - 0.008 - ln * t))
            rad = 0.0008 * (1 - 0.5 * t)
            rows.append([B.vert(c + Vector((rad * math.cos(2 * math.pi * k / 4 + a), rad * math.sin(2 * math.pi * k / 4 + a), 0)), cw) for k in range(4)])
        for a0, b0 in zip(rows, rows[1:]):
            for k in range(4):
                B.face((a0[k], a0[(k + 1) % 4], b0[(k + 1) % 4], b0[k]), "S_Tassel")
        B.face(list(reversed(rows[-1])), "S_Tassel")
    B.finish()

    ngons = [f for f in B.bm.faces if len(f.verts) > 4]
    bmesh.ops.triangulate(B.bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
    me = bpy.data.meshes.new("LucHopDaiThuong")
    B.bm.to_mesh(me); B.bm.free()
    for n in B.mats:
        me.materials.append(make_source_material(n))
    ob = bpy.data.objects.new("LucHopDaiThuong", me)
    bpy.context.collection.objects.link(ob)
    inv = {i: n for n, i in B.bone_index.items()}
    for i in range(len(inv)): ob.vertex_groups.new(name=inv[i])
    unwrap(ob)
    return ob

# ============================================================ UV
SRC_GROUP = {"S_Shaft": "Shaft", "S_Cord": "Cord", "S_Silver": "Metal", "S_Gunmetal": "Metal", "S_Glow": "Metal",
             "S_Gold": "Metal", "S_Steel": "Metal", "S_Tassel": "Tassel"}
GLOW_RGB = (0.05, 0.78, 1.0)

def unwrap(ob):
    me = ob.data
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.selected_objects: o.select_set(False)
    ob.select_set(True)
    ts = bpy.context.scene.tool_settings
    ts.use_uv_select_sync = True
    auto = me.attributes["auto_uv"].data
    for p in me.polygons: p.select = bool(auto[p.index].value)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_mode(type="FACE")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.0, scale_to_bounds=False, correct_aspect=True)
    bpy.ops.object.mode_set(mode="OBJECT")
    # smart project đưa island về 0-1 theo tỉ lệ riêng -> chuẩn hoá lại về mét để mật độ texel đều
    import bmesh as _b
    for g in GROUPS:
        idx = {i for i, m in enumerate(me.materials) if SRC_GROUP[m.name] == g}
        for p in me.polygons: p.select = p.material_index in idx
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.uv.average_islands_scale()
        try:
            bpy.ops.uv.pack_islands(rotate=True, margin=0.004, shape_method="CONCAVE")
        except TypeError:
            bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
        bpy.ops.object.mode_set(mode="OBJECT")
    for p in me.polygons: p.select = False
    me.attributes.remove(me.attributes["auto_uv"])

# ============================================================ procedural materials
class NB:
    def __init__(self, mat):
        self.t = mat.node_tree; self.n = self.t.nodes; self.l = self.t.links
    def node(self, typ, **props):
        nd = self.n.new(typ)
        for k, v in props.items(): setattr(nd, k, v)
        return nd
    def link(self, a, b): self.l.new(a, b)
    def sock(self, nd, name, out=False, typ=None):
        lst = nd.outputs if out else nd.inputs
        for s in lst:
            if s.name == name and (typ is None or s.type == typ) and getattr(s, "enabled", True): return s
        return next(s for s in lst if s.name == name)
    def val(self, x):
        if isinstance(x, (int, float)):
            nd = self.node("ShaderNodeValue"); nd.outputs[0].default_value = x; return nd.outputs[0]
        return x
    def math(self, op, a, b=0.0, clamp=False):
        nd = self.node("ShaderNodeMath", operation=op, use_clamp=clamp)
        for i, x in enumerate((a, b)):
            if isinstance(x, (int, float)): nd.inputs[i].default_value = x
            else: self.link(x, nd.inputs[i])
        return nd.outputs[0]
    def mix(self, fac, a, b, color=True):
        nd = self.node("ShaderNodeMix", data_type="RGBA" if color else "FLOAT", clamp_factor=True)
        typ = "RGBA" if color else "VALUE"
        fs = self.sock(nd, "Factor", typ="VALUE")
        if isinstance(fac, (int, float)): fs.default_value = fac
        else: self.link(fac, fs)
        for nm, x in (("A", a), ("B", b)):
            s = self.sock(nd, nm, typ=typ)
            if isinstance(x, (tuple, list)): s.default_value = (*x, 1) if len(x) == 3 else x
            elif isinstance(x, (int, float)): s.default_value = x
            else: self.link(x, s)
        return self.sock(nd, "Result", out=True, typ=typ)
    def coord(self, scale=(1, 1, 1)):
        tc = self.node("ShaderNodeTexCoord")
        mp = self.node("ShaderNodeMapping"); mp.inputs["Scale"].default_value = scale
        self.link(tc.outputs["Object"], mp.inputs["Vector"])
        return mp.outputs[0]
    def noise(self, vec, scale, detail=4.0, rough=0.5, dist=0.0):
        nd = self.node("ShaderNodeTexNoise")
        self.link(vec, nd.inputs["Vector"])
        nd.inputs["Scale"].default_value = scale; nd.inputs["Detail"].default_value = detail
        nd.inputs["Roughness"].default_value = rough; nd.inputs["Distortion"].default_value = dist
        return nd.outputs["Fac"]
    def voronoi(self, vec, scale, feature="F1"):
        nd = self.node("ShaderNodeTexVoronoi", feature=feature)
        self.link(vec, nd.inputs["Vector"]); nd.inputs["Scale"].default_value = scale
        return nd.outputs["Distance"]
    def maprange(self, x, a, b, c=0.0, d=1.0):
        nd = self.node("ShaderNodeMapRange", clamp=True)
        self.link(x, nd.inputs[0])
        for i, v in zip((1, 2, 3, 4), (a, b, c, d)): nd.inputs[i].default_value = v
        return nd.outputs[0]
    def scratches(self, scale, stretch, rot, width=0.014, sparse=0.5, off=0.0):
        """Vết xước mảnh, thẳng: cạnh ô Voronoi trên toạ độ bị kéo dài theo 1 hướng,
        cắt thành đoạn ngắn và chỉ giữ ở vài vùng."""
        tc = self.node("ShaderNodeTexCoord"); mp = self.node("ShaderNodeMapping")
        mp.inputs["Rotation"].default_value = rot
        mp.inputs["Scale"].default_value = (scale, scale / stretch, scale)
        mp.inputs["Location"].default_value = (off, off * 0.7, off * 1.3)
        self.link(tc.outputs["Object"], mp.inputs["Vector"])
        d = self.voronoi(mp.outputs[0], 1.0, "DISTANCE_TO_EDGE")
        line = self.maprange(d, 0.0, width, 1.0, 0.0)
        seg = self.maprange(self.noise(mp.outputs[0], 2.5, 1, 0.5), 0.5, 0.56)
        mask = self.maprange(self.noise(self.coord(), scale * 0.05, 2, 0.5), sparse, sparse + 0.08)
        return self.math("MULTIPLY", line, self.math("MULTIPLY", seg, mask))
    def ao(self, dist, inside=False):
        nd = self.node("ShaderNodeAmbientOcclusion", inside=inside, only_local=True, samples=16)
        nd.inputs["Distance"].default_value = dist
        return nd.outputs["AO"]

def make_source_material(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    t = m.node_tree
    for nd in list(t.nodes): t.nodes.remove(nd)
    b = NB(m)
    out = b.node("ShaderNodeOutputMaterial")
    bsdf = b.node("ShaderNodeBsdfPrincipled")
    emit = b.node("ShaderNodeEmission")
    # cạnh lồi: normal đã bo (Bevel) lệch khỏi normal thật -> 1 ở cạnh
    bev_e = b.node("ShaderNodeBevel", samples=8); bev_e.inputs["Radius"].default_value = 0.0012
    geo = b.node("ShaderNodeNewGeometry")
    dot = b.node("ShaderNodeVectorMath", operation="DOT_PRODUCT")
    b.link(bev_e.outputs["Normal"], dot.inputs[0]); b.link(geo.outputs["Normal"], dot.inputs[1])
    edge = b.maprange(b.sock(dot, "Value", out=True), 0.93, 0.995, 1.0, 0.0)
    cav = b.maprange(b.ao(0.012), 0.35, 0.95, 1.0, 0.0)                     # 1 trong hốc
    big = b.noise(b.coord(), 9.0, 6, 0.6)
    glow = (0.0, 0.0, 0.0)
    # độ cũ dùng chung: bụi bẩn từng mảng + xước (2 hướng)
    grime = b.maprange(b.noise(b.coord(), 7.0, 5, 0.6), 0.45, 0.78)
    def scr(s1=150, s2=240, w=0.03, sp=0.5):
        return b.math("MAXIMUM", b.scratches(s1, 60, (0.4, 0.2, 0.9), w, sp),
                      b.scratches(s2, 80, (1.3, 0.1, 0.2), w * 0.8, sp + 0.04, 3.1))
    if name == "S_Shaft":          # ống thép sơn đen: xước lộ màu xám, tróc sơn ở cạnh/vùng hay chạm
        brush = b.noise(b.coord((2200, 2200, 9)), 1.0, 3, 0.5)
        sc = scr(150, 240, 0.03, 0.48)
        chips = b.maprange(b.math("ADD", b.noise(b.coord(), 55.0, 10, 0.7), b.math("MULTIPLY", edge, 0.35)), 0.67, 0.70)
        col = b.mix(b.maprange(big, 0.3, 0.7), (0.018, 0.018, 0.02), (0.03, 0.03, 0.034))
        col = b.mix(b.math("MULTIPLY", grime, 0.55), col, (0.055, 0.048, 0.04))
        col = b.mix(b.math("MULTIPLY", sc, 0.85), col, (0.13, 0.13, 0.135))
        col = b.mix(chips, col, (0.4, 0.4, 0.42))
        metal = b.math("MAXIMUM", b.mix(chips, 0.25, 1.0, color=False), b.math("MULTIPLY", sc, 0.6))
        rough = b.math("ADD", b.math("ADD", 0.34, b.math("MULTIPLY", brush, 0.06)),
                       b.math("ADD", b.math("MULTIPLY", grime, 0.25), b.math("MULTIPLY", sc, 0.12)))
        height = b.math("SUBTRACT", b.math("MULTIPLY", brush, 0.2), b.math("ADD", b.math("MULTIPLY", sc, 0.5), b.math("MULTIPLY", chips, 0.8)))
        bump = 0.00008
    elif name == "S_Cord":         # dây thừng đỏ bện
        weave = b.node("ShaderNodeTexWave", wave_type="BANDS", bands_direction="DIAGONAL")
        b.link(b.coord(), weave.inputs["Vector"]); weave.inputs["Scale"].default_value = 900.0
        weave.inputs["Distortion"].default_value = 1.5; weave.inputs["Detail"].default_value = 2.0
        wv = weave.outputs["Fac"]
        fuzz = b.noise(b.coord(), 2500.0, 3, 0.6)
        # dây xoắn giả: pha xoắn = z/bước - góc/2π, mặt cắt dây tròn -> ridge 0..1
        tc = b.node("ShaderNodeTexCoord"); sep = b.node("ShaderNodeSeparateXYZ")
        b.link(tc.outputs["Object"], sep.inputs[0])
        turn = b.math("MULTIPLY", b.math("ARCTAN2", sep.outputs["Y"], sep.outputs["X"]), 1.0 / (2 * math.pi))
        f = b.math("FRACT", b.math("SUBTRACT", b.math("MULTIPLY", sep.outputs["Z"], 1.0 / CORD_PITCH), turn))
        t = b.math("SUBTRACT", b.math("MULTIPLY", f, 2.0), 1.0)
        ridge = b.math("SQRT", b.math("MAXIMUM", b.math("SUBTRACT", 1.0, b.math("MULTIPLY", t, t)), 0.0))
        groove = b.math("POWER", b.math("SUBTRACT", 1.0, ridge), 3.0)
        col = b.mix(b.maprange(wv, 0.2, 0.8), (0.17, 0.008, 0.014), (0.36, 0.025, 0.035))
        col = b.mix(b.maprange(fuzz, 0.45, 0.75, 0, 0.3), col, (0.5, 0.06, 0.06))
        fade = b.maprange(b.noise(b.coord(), 18.0, 4, 0.6), 0.55, 0.75)          # chỗ bạc màu do cầm nắm
        col = b.mix(b.math("MULTIPLY", fade, 0.45), col, (0.33, 0.12, 0.1))
        col = b.mix(b.math("MULTIPLY", grime, 0.6), col, (0.07, 0.03, 0.022))      # bám bẩn
        fray = b.maprange(b.noise(b.coord(), 4000.0, 2, 0.5), 0.68, 0.74)        # sợi xơ
        col = b.mix(b.math("MULTIPLY", fray, b.math("SUBTRACT", 1.0, groove)), col, (0.55, 0.2, 0.17))
        col = b.mix(b.math("MAXIMUM", b.math("MULTIPLY", cav, 0.85), b.math("MULTIPLY", groove, 0.9)), col, (0.03, 0.003, 0.004))
        metal = 0.0
        rough = b.math("ADD", b.math("ADD", 0.74, b.math("MULTIPLY", fuzz, 0.12)), b.math("ADD", b.math("MULTIPLY", groove, 0.1), b.math("MULTIPLY", grime, 0.08)))
        height = b.math("ADD", b.math("MULTIPLY", ridge, 0.85), b.math("ADD", b.math("MULTIPLY", wv, 0.1), b.math("MULTIPLY", fuzz, 0.05)))
        bump = 0.0018
    elif name == "S_Silver":       # đai bạc thép mài
        brush = b.noise(b.coord((1, 1, 1)), 900.0, 2, 0.4)
        sc = scr(180, 300, 0.03, 0.5)
        tarn = b.maprange(b.noise(b.coord(), 40.0, 6, 0.65), 0.5, 0.72)        # xỉn màu
        dent = b.noise(b.coord(), 220.0, 3, 0.5)                                 # móp nhẹ
        col = b.mix(b.maprange(big, 0.3, 0.7), (0.55, 0.555, 0.57), (0.68, 0.685, 0.7))
        col = b.mix(b.math("MULTIPLY", tarn, 0.55), col, (0.3, 0.27, 0.21))
        col = b.mix(b.math("MULTIPLY", grime, 0.4), col, (0.16, 0.14, 0.11))
        col = b.mix(b.math("MULTIPLY", edge, 0.7), col, (0.88, 0.88, 0.9))
        col = b.mix(b.math("MULTIPLY", sc, 0.7), col, (0.9, 0.9, 0.92))
        col = b.mix(b.math("MULTIPLY", cav, 0.85), col, (0.07, 0.065, 0.055))
        metal = 1.0
        rough = b.math("ADD", b.math("SUBTRACT", 0.3, b.math("MULTIPLY", edge, 0.1)),
                       b.math("ADD", b.math("ADD", b.math("MULTIPLY", brush, 0.06), b.math("MULTIPLY", cav, 0.35)),
                              b.math("ADD", b.math("MULTIPLY", tarn, 0.2), b.math("MULTIPLY", sc, 0.12))))
        height = b.math("SUBTRACT", b.math("ADD", b.math("MULTIPLY", brush, 0.2), b.math("MULTIPLY", dent, 0.3)), b.math("MULTIPLY", sc, 0.6))
        bump = 0.00006
    elif name == "S_Gunmetal":     # thép xám đen, cạnh mòn sáng
        brush = b.noise(b.coord((1, 1, 1)), 450.0, 3, 0.5)
        sc = scr(170, 280, 0.028, 0.55)
        col = b.mix(b.maprange(big, 0.35, 0.7), (0.045, 0.047, 0.052), (0.075, 0.077, 0.085))
        col = b.mix(b.math("MULTIPLY", grime, 0.5), col, (0.06, 0.052, 0.042))
        col = b.mix(b.math("MULTIPLY", edge, 0.95), col, (0.5, 0.5, 0.52))
        col = b.mix(b.math("MULTIPLY", sc, 0.85), col, (0.42, 0.42, 0.44))
        col = b.mix(b.math("MULTIPLY", cav, 0.7), col, (0.012, 0.011, 0.01))
        metal = 1.0
        rough = b.math("ADD", b.math("SUBTRACT", 0.42, b.math("MULTIPLY", edge, 0.2)),
                       b.math("ADD", b.math("MULTIPLY", brush, 0.12), b.math("MULTIPLY", grime, 0.2)))
        height = b.math("SUBTRACT", b.math("MULTIPLY", brush, 0.3), b.math("MULTIPLY", sc, 0.6))
        bump = 0.00008
    elif name == "S_Glow":         # vạch phát sáng cyan
        col = b.mix(0.0, (0.03, 0.08, 0.1), (0.03, 0.08, 0.1))
        metal = 0.0
        rough = 0.25
        height = 0.0
        bump = 0.0
        glow = GLOW_RGB
    elif name == "S_Steel":
        brush = b.noise(b.coord((2500, 2500, 12)), 1.0, 2, 0.4)        # vân mài dọc lưỡi
        smudge = b.maprange(b.noise(b.coord(), 30.0, 5, 0.6), 0.4, 0.7)
        col = b.mix(smudge, (0.72, 0.72, 0.74), (0.62, 0.62, 0.64))
        col = b.mix(b.math("MULTIPLY", cav, 0.6), col, (0.2, 0.18, 0.16))
        col = b.mix(b.math("MULTIPLY", edge, 0.6), col, (0.86, 0.86, 0.88))
        metal = 1.0
        rough = b.math("ADD", b.math("ADD", 0.12, b.math("MULTIPLY", brush, 0.06)), b.math("ADD", b.math("MULTIPLY", smudge, 0.08), b.math("MULTIPLY", cav, 0.25)))
        height = brush
        bump = 0.00002
    elif name == "S_Gold":
        col = b.mix(b.maprange(big, 0.3, 0.7), (0.66, 0.46, 0.19), (0.82, 0.6, 0.3))
        col = b.mix(b.math("MULTIPLY", grime, 0.5), col, (0.25, 0.18, 0.09))
        col = b.mix(b.math("MULTIPLY", cav, 0.8), col, (0.1, 0.07, 0.035))
        metal = 1.0
        rough = b.math("ADD", b.math("ADD", 0.26, b.math("MULTIPLY", cav, 0.35)), b.math("MULTIPLY", grime, 0.2))
        height = b.math("MULTIPLY", big, 0.2)
        bump = 0.00005
    else:  # S_Tassel - chỉ lụa
        fib = b.noise(b.coord((2500, 2500, 20)), 1.0, 3, 0.5)
        col = b.mix(b.maprange(fib, 0.3, 0.7), (0.28, 0.008, 0.01), (0.55, 0.03, 0.025))
        col = b.mix(b.math("MULTIPLY", grime, 0.5), col, (0.12, 0.04, 0.03))
        col = b.mix(b.math("MULTIPLY", cav, 0.7), col, (0.08, 0.004, 0.004))
        metal = 0.0
        rough = b.math("ADD", 0.55, b.math("MULTIPLY", fib, 0.2))
        height = fib
        bump = 0.0001
    bev = b.node("ShaderNodeBevel", samples=8); bev.inputs["Radius"].default_value = 0.0006
    bp = b.node("ShaderNodeBump"); bp.inputs["Distance"].default_value = bump; bp.inputs["Strength"].default_value = 1.0
    b.link(b.val(height), bp.inputs["Height"]); b.link(bev.outputs["Normal"], bp.inputs["Normal"])
    b.link(col, bsdf.inputs["Base Color"])
    b.link(b.val(metal), bsdf.inputs["Metallic"])
    b.link(b.val(rough), bsdf.inputs["Roughness"])
    b.link(bp.outputs["Normal"], bsdf.inputs["Normal"])
    b.link(bsdf.outputs[0], out.inputs["Surface"])
    # giữ socket cho bake
    g = b.node("ShaderNodeRGB"); g.outputs[0].default_value = (*glow, 1)
    if any(glow):
        b.link(g.outputs[0], bsdf.inputs["Emission Color"]); bsdf.inputs["Emission Strength"].default_value = 6.0
    PASS_SOCK[name] = {"color": col, "metal": b.val(metal), "rough": b.val(rough), "glow": g.outputs[0]}
    PASS_NODE[name] = (out, bsdf, emit)
    return m

PASS_SOCK, PASS_NODE = {}, {}

def set_pass(mat, which):
    out, bsdf, emit = PASS_NODE[mat.name]
    lk = mat.node_tree.links
    for l in list(out.inputs["Surface"].links): lk.remove(l)
    for l in list(emit.inputs["Color"].links): lk.remove(l)
    if which in ("normal", "ao"):
        lk.new(bsdf.outputs[0], out.inputs["Surface"])
    else:
        lk.new(PASS_SOCK[mat.name][which], emit.inputs["Color"])
        lk.new(emit.outputs[0], out.inputs["Surface"])

# ============================================================ bake
def bake(ob):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 24
    sc.render.bake.margin = 12
    sc.render.bake.use_clear = True
    mats = [m for m in ob.data.materials]
    imgs = {}
    def new_img(name, size, noncolor):
        im = bpy.data.images.new(name, size, size, alpha=False, float_buffer=False)
        im.colorspace_settings.name = "Non-Color" if noncolor else "sRGB"
        return im
    for which, nc in (("color", False), ("metal", True), ("rough", True), ("normal", True), ("ao", True), ("glow", False)):
        for g, size in GROUPS.items():
            imgs[(g, which)] = new_img(f"LHDT_{g}_{which}", size, nc)
        for m in mats:
            set_pass(m, which)
            nd = m.node_tree.nodes.get("BAKE_TARGET") or m.node_tree.nodes.new("ShaderNodeTexImage")
            nd.name = "BAKE_TARGET"
            nd.image = imgs[(SRC_GROUP[m.name], which)]
            m.node_tree.nodes.active = nd
        bpy.ops.object.select_all(action="DESELECT")
        ob.select_set(True); bpy.context.view_layer.objects.active = ob
        t = {"color": "EMIT", "metal": "EMIT", "rough": "EMIT", "normal": "NORMAL", "ao": "AO", "glow": "EMIT"}[which]
        kw = dict(type=t, margin=12, use_clear=True)
        if t == "NORMAL": kw.update(normal_space="TANGENT")
        print("BAKE", which, flush=True)
        bpy.ops.object.bake(**kw)
    for m in mats: set_pass(m, "normal")
    # ghi texture cho Unity: BaseColor, Normal (OpenGL, Unity dùng y+), MaskMap HDRP (R metal, G AO, B detail, A smooth)
    os.makedirs(TEX_DIR, exist_ok=True)
    out = {}
    for g, size in GROUPS.items():
        def arr(which):
            a = np.empty(size * size * 4, dtype=np.float32); imgs[(g, which)].pixels.foreach_get(a); return a.reshape(-1, 4)
        met, ao, ro = arr("metal"), arr("ao"), arr("rough")
        mask = np.stack([met[:, 0], ao[:, 0], np.zeros(size * size, np.float32), 1.0 - ro[:, 0]], axis=1)
        mi = bpy.data.images.new(f"LHDT_{g}_MaskMap", size, size, alpha=True)
        mi.colorspace_settings.name = "Non-Color"
        mi.pixels.foreach_set(mask.ravel())
        saves = [("BaseColor", imgs[(g, "color")]), ("Normal", imgs[(g, "normal")]), ("MaskMap", mi)]
        if g == "Metal": saves.append(("Emissive", imgs[(g, "glow")]))
        for which, im in saves:
            p = f"{TEX_DIR}/LHDT_{g}_{which}.png"
            im.filepath_raw = p; im.file_format = "PNG"; im.save()
            out[(g, which)] = p
        print("SAVED", g, flush=True)
    return out

def final_materials(ob, tex):
    """Thay material nguồn bằng 4 material dùng texture đã bake (giống những gì Unity nhận)."""
    me = ob.data
    grp_mats = {}
    for g in GROUPS:
        m = bpy.data.materials.new(f"LHDT_{g}")
        m.use_nodes = True
        b = NB(m)
        bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        def timg(which, nc):
            nd = b.node("ShaderNodeTexImage")
            nd.image = bpy.data.images.load(tex[(g, which)], check_existing=True)
            nd.image.colorspace_settings.name = "Non-Color" if nc else "sRGB"
            return nd
        bc = timg("BaseColor", False); mk = timg("MaskMap", True); nm = timg("Normal", True)
        b.link(bc.outputs["Color"], bsdf.inputs["Base Color"])
        sep = b.node("ShaderNodeSeparateColor"); b.link(mk.outputs["Color"], sep.inputs[0])
        b.link(sep.outputs[0], bsdf.inputs["Metallic"])
        b.link(b.math("SUBTRACT", 1.0, mk.outputs["Alpha"]), bsdf.inputs["Roughness"])
        nmap = b.node("ShaderNodeNormalMap"); b.link(nm.outputs["Color"], nmap.inputs["Color"])
        b.link(nmap.outputs[0], bsdf.inputs["Normal"])
        if (g, "Emissive") in tex:
            em = timg("Emissive", False)
            b.link(em.outputs["Color"], bsdf.inputs["Emission Color"]); bsdf.inputs["Emission Strength"].default_value = 6.0
        grp_mats[g] = m
    remap = [list(GROUPS).index(SRC_GROUP[m.name]) for m in me.materials]
    idx = np.empty(len(me.polygons), dtype=np.int32); me.polygons.foreach_get("material_index", idx)
    idx = np.array(remap, dtype=np.int32)[idx]
    me.materials.clear()
    for g in GROUPS: me.materials.append(grp_mats[g])
    me.polygons.foreach_set("material_index", idx)
    me.update()

# ============================================================ animation tháo / lắp
def ease(t):
    t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)

PULL = 0.045
TWIST = 90.0

def key_upper(arm, action, samples):
    """Nửa trên vặn quanh trục thân, rút lên, rồi lật 180° quanh điểm cạnh thân để nằm song song.
    samples: [(frame, pull 0..1, twist 0..1, swing_deg)]"""
    pb = arm.pose.bones["Upper"]
    pb.rotation_mode = "QUATERNION"
    rest = arm.data.bones["Upper"].matrix_local.copy()
    arm.animation_data.action = action
    prev_q = None
    for f, pull, tw, sw in samples:
        lift = PULL * pull
        piv = Vector((SIDE * 0.5, 0, ZH + lift))
        M = (Matrix.Translation(piv) @ Matrix.Rotation(math.radians(sw), 4, "Y") @ Matrix.Translation(-piv)
             @ Matrix.Translation((0, 0, lift)) @ Matrix.Rotation(math.radians(TWIST * tw), 4, "Z") @ rest)
        pb.matrix = M
        bpy.context.view_layer.update()
        q = pb.rotation_quaternion.copy()
        if prev_q is not None: q.make_compatible(prev_q)
        pb.rotation_quaternion = q; prev_q = q
        pb.keyframe_insert("location", frame=f)
        pb.keyframe_insert("rotation_quaternion", frame=f)
    action.use_fake_user = True

def fold_samples(n=26):
    s = []
    for f in range(1, n + 1):
        t = (f - 1) / (n - 1)
        tw = ease(t / 0.2)
        pull = ease((t - 0.15) / 0.15)
        sw = 180 * ease((t - 0.3) / 0.55)
        if t > 0.85:
            k = (t - 0.85) / 0.15
            sw = 180 + 6 * math.sin(math.pi * k) * (1 - k)
        drop = ease((t - 0.7) / 0.2)
        s.append((f, pull * (1 - drop), tw, sw))
    return s

def make_actions(arm):
    arm.animation_data_create()
    fold = bpy.data.actions.new("Spear_Fold")
    fs = fold_samples()
    key_upper(arm, fold, fs)
    unfold = bpy.data.actions.new("Spear_Unfold")
    key_upper(arm, unfold, [(i + 1, p, tw, sw) for i, (_, p, tw, sw) in enumerate(reversed(fs))])
    ext = bpy.data.actions.new("Spear_Extended")
    key_upper(arm, ext, [(1, 0, 0, 0), (2, 0, 0, 0)])
    fd = bpy.data.actions.new("Spear_Folded")
    key_upper(arm, fd, [(1, 0, 1, 180), (2, 0, 1, 180)])
    arm.animation_data.action = None
    pb = arm.pose.bones["Upper"]
    pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0)
    return [fold, unfold, ext, fd]

def set_action(ob, act, frame):
    ob.animation_data.action = act
    if hasattr(ob.animation_data, "action_slot") and getattr(act, "slots", None):
        ob.animation_data.action_slot = act.slots[0]
    bpy.context.scene.frame_set(frame)

# ============================================================ main
def fire_material():
    """Material + texture HDRP cho đầu thương/vải từ texture gốc (MaskMap: R metal, G AO=1, A smooth)."""
    import shutil
    os.makedirs(TEX_DIR, exist_ok=True)
    src = {k: f"{FIRE_DIR}/{f}" for k, f in (("BaseColor", "_spear_Spear_LP_BaseColor.png"), ("Normal", "_spear_Spear_LP_Normal.png"),
                                              ("Metallic", "R_Lowpoly_spear_Spear_LP_Metallic.png"), ("Roughness", "_spear_Spear_LP_Roughness.png"))}
    out = {k: f"{TEX_DIR}/LHDT_Fire_{k}.png" for k in ("BaseColor", "Normal", "MaskMap")}
    shutil.copyfile(src["BaseColor"], out["BaseColor"])
    shutil.copyfile(src["Normal"], out["Normal"])
    met = bpy.data.images.load(src["Metallic"]); rou = bpy.data.images.load(src["Roughness"])
    w, h = met.size
    if tuple(rou.size) != (w, h): rou.scale(w, h)
    a = np.empty(w * h * 4, np.float32); met.pixels.foreach_get(a); a = a.reshape(-1, 4)
    r = np.empty(w * h * 4, np.float32); rou.pixels.foreach_get(r); r = r.reshape(-1, 4)
    mask = np.stack([a[:, 0], np.ones(w * h, np.float32), np.zeros(w * h, np.float32), 1.0 - r[:, 0]], axis=1)
    mi = bpy.data.images.new("LHDT_Fire_MaskMap", w, h, alpha=True); mi.colorspace_settings.name = "Non-Color"
    mi.pixels.foreach_set(mask.ravel()); mi.filepath_raw = out["MaskMap"]; mi.file_format = "PNG"; mi.save()
    m = bpy.data.materials.new("LHDT_Fire"); m.use_nodes = True
    b = NB(m); bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    def timg(path, nc):
        nd = b.node("ShaderNodeTexImage"); nd.image = bpy.data.images.load(path, check_existing=True)
        nd.image.colorspace_settings.name = "Non-Color" if nc else "sRGB"; return nd
    b.link(timg(out["BaseColor"], False).outputs["Color"], bsdf.inputs["Base Color"])
    mk = timg(out["MaskMap"], True)
    sep = b.node("ShaderNodeSeparateColor"); b.link(mk.outputs["Color"], sep.inputs[0])
    b.link(sep.outputs[0], bsdf.inputs["Metallic"])
    b.link(b.math("SUBTRACT", 1.0, mk.outputs["Alpha"]), bsdf.inputs["Roughness"])
    nm = b.node("ShaderNodeNormalMap"); b.link(timg(out["Normal"], True).outputs["Color"], nm.inputs["Color"])
    b.link(nm.outputs[0], bsdf.inputs["Normal"])
    return m

def build_fire(main_ob):
    """Cắt đầu thương + vải từ Spear of the fire, scale cho họng khớp thân, xoay bản lưỡi theo trục Y, ghép vào mesh chính."""
    me = load_fire()
    head, knot, flag, _ = classify_fire(me)
    tag = {}
    for parts, t in ((head, 1), (knot, 2), (flag, 3)):
        for c in parts:
            for i in c[0]: tag[i] = t
    M = (Matrix.Translation((0, 0, Z_HEAD)) @ Matrix.Rotation(math.radians(90), 4, "Z")
         @ Matrix.Scale(FIRE["S"], 4) @ Matrix.Translation((0, 0, -FIRE_HEAD_Z)))
    me.transform(M)
    # Blender 5 lưu tên vertex group trong mesh: xoá group gốc ("Spear_Spear_LP_0") trước,
    # nếu không chỉ số group bị lệch và trọng số Head rơi vào group không phải xương
    ob = bpy.data.objects.new("FirePart", me); bpy.context.collection.objects.link(ob)
    ob.vertex_groups.clear()
    names = ["Head", UP[-1][0]] + [n for n, _, _ in CLOTH]
    for n in names: ob.vertex_groups.new(name=n)
    bm = bmesh.new(); bm.from_mesh(me)
    lay = bm.verts.layers.int.new("part")
    bm.verts.ensure_lookup_table()
    for v in bm.verts: v[lay] = tag.get(v.index, 0)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v[lay] == 0], context="VERTS")
    dl = bm.verts.layers.deform.verify()
    for v in bm.verts:
        t = v[lay]
        v[dl].clear()          # bỏ trọng số xương gốc của file FBX
        ws = {"Head": 1.0} if t == 1 else ({UP[-1][0]: 1.0} if t == 2 else chain_weights(v.co.z, CLOTH))
        for n, w in ws.items(): v[dl][names.index(n)] = w
    bm.verts.layers.int.remove(lay)
    bm.to_mesh(me); bm.free()
    me.materials.clear(); me.materials.append(fire_material())
    for p in me.polygons: p.material_index = 0
    me.uv_layers[0].name = main_ob.data.uv_layers[0].name
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True); main_ob.select_set(True)
    bpy.context.view_layer.objects.active = main_ob
    bpy.ops.object.join()
    print("FIRE joined: S=%.2f head_len=%.3f" % (FIRE["S"], FIRE["head_len"]), flush=True)

def main():
    preview = "--preview" in sys.argv
    nobake = "--nobake" in sys.argv
    bpy.ops.wm.read_factory_settings(use_empty=True)
    arm = build_armature()
    me = build_mesh()
    me.parent = arm
    tris = sum(len(p.vertices) - 2 for p in me.data.polygons)
    print("TRIS", tris, flush=True)
    if not nobake:
        tex = bake(me)
        final_materials(me, tex)
    build_fire(me)
    mod = me.modifiers.new("Armature", "ARMATURE"); mod.object = arm
    actions = make_actions(arm)

    os.makedirs(UNITY_DIR, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT_DIR + "/LucHopDaiThuong.blend")
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True); me.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        mesh_smooth_type="OFF", use_tspace=True, use_armature_deform_only=False, path_mode="STRIP")
    print("EXPORTED", FBX_PATH, "len", L, flush=True)
    if preview:
        render_preview(arm, me, actions)

def render_preview(arm, me, actions):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 96
    try:
        sc.cycles.use_denoising = True
    except Exception:
        pass
    for vt in ("Khronos PBR Neutral", "AgX", "Standard"):
        try:
            sc.view_settings.view_transform = vt; break
        except TypeError:
            pass
    sc.view_settings.exposure = -0.6
    w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
    wn = w.node_tree.nodes; bg = next(n for n in wn if n.type == "BACKGROUND")
    env = wn.new("ShaderNodeTexEnvironment")
    hdri = os.path.join(HDRI_DIR, "interior.exr")
    if os.path.exists(hdri):
        env.image = bpy.data.images.load(hdri)
        w.node_tree.links.new(env.outputs[0], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 0.45
    # nền tối + đèn chính / đèn viền
    def area(name, loc, rot, size, power):
        ld = bpy.data.lights.new(name, "AREA"); ld.size = size; ld.energy = power
        lo = bpy.data.objects.new(name, ld); lo.location = loc; lo.rotation_euler = rot
        bpy.context.collection.objects.link(lo)
    area("Key", (1.2, -1.2, 3.0), (math.radians(50), 0, math.radians(45)), 1.2, 260)
    area("Rim", (-1.2, 1.5, 2.6), (math.radians(-60), 0, math.radians(-140)), 1.0, 220)
    sc.render.film_transparent = False
    cam_d = bpy.data.cameras.new("Cam"); cam_d.lens = 85
    cam = bpy.data.objects.new("Cam", cam_d); bpy.context.collection.objects.link(cam)
    sc.camera = cam
    def look_at(loc, target):
        cam.location = loc
        cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    def shot(name, loc, target, lens, rx, ry):
        look_at(loc, target); cam_d.lens = lens
        sc.render.resolution_x = rx; sc.render.resolution_y = ry
        sc.render.filepath = OUT_DIR + f"/render_{name}.png"
        bpy.ops.render.render(write_still=True)
        print("RENDER", name, flush=True)
    set_action(arm, actions[2], 1)
    shot("head", (0.55, -0.35, L - 0.18), (0, 0, L - 0.33), 85, 900, 900)
    shot("coupler", (0.32, -0.3, ZH + 0.05), (0, 0, ZH - 0.02), 85, 900, 900)
    shot("grip", (0.3, -0.32, 0.55), (0, 0, 0.48), 85, 900, 900)
    shot("shaft", (0.22, -0.2, 1.0), (0, 0, 0.95), 85, 900, 900)
    shot("charm", (0.12, -0.12, Z_CHARM - 0.02), (CHARM_X, 0, Z_CHARM - 0.05), 85, 800, 800)
    shot("cloth", (0.9, -1.1, Z_HEAD - 0.05), (0, 0, Z_HEAD - 0.12), 85, 900, 1100)
    shot("full", (2.6, -4.2, 1.9), (0, 0, 1.65), 50, 900, 1200)
    set_action(arm, actions[3], 1)
    shot("folded", (1.6, -2.6, 1.1), (0.03, 0, 0.85), 50, 900, 1100)
    print("PREVIEW DONE", flush=True)

main()
