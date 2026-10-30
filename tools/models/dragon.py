"""Skeleton dragon (CC-BY 4.0, TRSgamings on Sketchfab) to Dragon.fbx and its textures.

    blender -b -P dragon.py -- skeleton_dragon_ff.glb <output folder>

It keeps 8k triangles and its material; only the clips the flying enemy uses are kept, plus a dive
made here.
"""
import bpy, sys, os, math
sys.path.append(os.path.dirname(__file__))
import common
from mathutils import Matrix, Vector

# The dive, a pose made here: there is no clip for it. Bone name prefix -> turns about world axes, in
# degrees, applied in order and parents first. The wings sweep back into a V, the legs trail, the
# neck reaches forward and the tail straightens.
DIVE = {
    "Bip001 Xtra01_": [((0, 1, 0), -10), ((0, 0, 1), -42)],
    "Bip001 Xtra01Opp_": [((0, 1, 0), 10), ((0, 0, 1), 42)],
    "Bip001 Xtra0102_": [((0, 0, 1), -25)],
    "Bip001 Xtra01Opp02_": [((0, 0, 1), 25)],
    "Bip001 L Thigh_": [((1, 0, 0), 60)],
    "Bip001 R Thigh_": [((1, 0, 0), 60)],
    "Bip001 L Clavicle_": [((1, 0, 0), 50)],
    "Bip001 R Clavicle_": [((1, 0, 0), 50)],
    "Bip001 Neck_": [((1, 0, 0), 20)],
    "Bone013_": [((1, 0, 0), 25)],
}


def turn(arm, pose):
    to_arm = arm.matrix_world.to_3x3().inverted()
    for prefix, turns in pose.items():
        pb = next(b for b in arm.pose.bones if b.name.startswith(prefix))
        bpy.context.view_layer.update()
        m = pb.matrix.copy()
        head = m.translation.copy()
        for axis, degrees in turns:
            r = Matrix.Rotation(math.radians(degrees), 4, (to_arm @ Vector(axis)).normalized())
            m = Matrix.Translation(head) @ r @ Matrix.Translation(-head) @ m
        pb.matrix = m
    bpy.context.view_layer.update()


def make_dive(arm, hover, frames=24):
    """A one second loop from the first frame of hover with the DIVE pose on top: the wing tips shiver
    in the wind three times and the tail sways once."""
    arm.animation_data.action = hover
    bpy.context.scene.frame_set(int(hover.frame_range[0]))
    base = {b.name: b.matrix_basis.copy() for b in arm.pose.bones}
    dive = bpy.data.actions.new("dive_GLTF_created_0")
    arm.animation_data.action = dive
    for f in range(0, frames + 1, 2):
        for b in arm.pose.bones:
            b.matrix_basis = base[b.name]
        shiver = 4 * math.sin(2 * math.pi * 3 * f / frames)
        sway = 6 * math.sin(2 * math.pi * f / frames)
        pose = dict(DIVE)
        pose["Bip001 Xtra0102_"] = DIVE["Bip001 Xtra0102_"] + [((1, 0, 0), shiver)]
        pose["Bip001 Xtra01Opp02_"] = DIVE["Bip001 Xtra01Opp02_"] + [((1, 0, 0), shiver)]
        pose["Bone013_"] = DIVE["Bone013_"] + [((0, 0, 1), sway)]
        turn(arm, pose)
        for b in arm.pose.bones:
            b.keyframe_insert("location", frame=f, group=b.name)
            b.keyframe_insert("rotation_quaternion", frame=f, group=b.name)


args = sys.argv[sys.argv.index("--")+1:]
src, out = args
arm = common.load(src)
mesh = next(o for o in bpy.data.objects if o.type == 'MESH')
mat = mesh.material_slots[0].material

# Flying clip: the aerial attack, a hover with wing beats. Brought down to the height of the ground
# clips, so the pivot is under the body in every clip.
acts = {a.name.replace("_GLTF_created_0", ""): a for a in bpy.data.actions}
def first(action, index):
    fc = action.fcurves.find('pose.bones["Bip001_00_11"].location', index=index)
    return fc, fc.keyframe_points[0].co[1]
fly, fly_z = first(acts["attack_5"], 2)
_, ground_z = first(acts["pettyidle"], 2)
for k in fly.keyframe_points:
    for p in (k.co, k.handle_left, k.handle_right):
        p[1] -= fly_z - ground_z

make_dive(arm, acts["attack_5"])
common.keep_actions({"attack_5": "Fly", "dive": "Dive", "hurtsmall": "Hit", "dead": "Death"})
# About 2.2 m wide.
common.flatten(arm, [mesh], 0.8, "Dragon")
while len(mesh.data.uv_layers) > 1:
    mesh.data.uv_layers.remove(mesh.data.uv_layers[-1])
mesh.name = "Body"
mesh.data.name = "Dragon"

# The glossiness map is left out: Unity's Lit gets a flat smoothness instead.
names = {"BSDF_DIFFUSE": "Albedo", "NORMAL_MAP": "Normal", "EMISSION": "Emission"}
for n in mat.node_tree.nodes:
    if n.type == 'TEX_IMAGE':
        for l in mat.node_tree.links:
            if l.from_node == n and l.to_node.type in names:
                common.save_image(n.image, os.path.join(out, f"Dragon_{names[l.to_node.type]}.png"))
                break
common.export(os.path.join(out, "Dragon.fbx"), [arm, mesh])
