# Đổi file BVH (xuất từ Mocap Studio) sang FBX để Unity nhập dạng Humanoid.
# Chạy bằng Blender ở chế độ nền:
#   blender -b --factory-startup -P bvh_to_fbx.py -- a.bvh [b.bvh ...]
# FBX được ghi cạnh file BVH, cùng tên. Tên take/clip trong Unity = tên file.
import bpy, sys, os


def convert(src):
    dst = os.path.splitext(src)[0] + ".fbx"
    name = os.path.splitext(os.path.basename(src))[0]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_anim.bvh(
        filepath=src, global_scale=1.0, frame_start=1, use_fps_scale=False,
        update_scene_fps=True, update_scene_duration=True, rotate_mode='NATIVE',
        axis_forward='-Z', axis_up='Y')
    arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    arm.name = name
    arm.data.name = name + "_Skeleton"
    scene = bpy.context.scene
    if arm.animation_data and arm.animation_data.action:
        act = arm.animation_data.action
        act.name = name
        # Độ dài scene = đúng độ dài action (mặc định Blender là 250 khung)
        first, last = int(act.frame_range[0]), int(act.frame_range[1])
        # FBX lấy pose ở khung hiện tại làm pose gốc -> chèn khung T-pose (ngoài đoạn xuất) và đứng ở đó,
        # để Unity tạo Avatar Humanoid từ đúng T-pose.
        for pb in arm.pose.bones:
            pb.location = (0, 0, 0)
            pb.rotation_quaternion = (1, 0, 0, 0)
            pb.rotation_euler = (0, 0, 0)
            pb.keyframe_insert("location", frame=first - 1)
            pb.keyframe_insert("rotation_quaternion" if pb.rotation_mode == 'QUATERNION' else "rotation_euler", frame=first - 1)
        scene.frame_start, scene.frame_end = first, last
        scene.frame_set(first - 1)
    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=False, object_types={'ARMATURE'},
        add_leaf_bones=False, armature_nodetype='NULL',
        primary_bone_axis='Y', secondary_bone_axis='X',
        axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0)
    print(f"[OK] {dst}  ({scene.frame_end - scene.frame_start + 1} khung @ {scene.render.fps} fps)")


args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
files = [a for a in args if a.lower().endswith(".bvh")]
if not files:
    print("Cách dùng: blender -b -P bvh_to_fbx.py -- file1.bvh [file2.bvh ...]")
for f in files:
    try:
        convert(os.path.abspath(f))
    except Exception as e:
        print(f"[LOI] {f}: {e}")
