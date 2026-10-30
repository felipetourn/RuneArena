"""Skeleton gladiator to Skeleton.fbx: one skinned mesh, decimated, with its 8 materials baked into one
texture set (albedo with the cloth's holes in alpha, and a normal map).

    blender -b -P skeleton.py -- "Skeleton - low tier enemy.glb" <output folder> 0.07 2048

0.07 is how much of the body's 75k triangles stay (the whole model ends near 11.6k); 2048 is the bake
size, saved at half for less aliasing.
"""
import bpy, sys, os, math
import numpy as np
sys.path.append(os.path.dirname(__file__))
import common
from mathutils import Vector
args = sys.argv[sys.argv.index("--")+1:]
src, out, ratio, size = args[0], args[1], float(args[2]), int(args[3])
arm = common.load(src)
arm.data.pose_position = 'REST'
bpy.context.view_layer.update()
meshes = [o for o in bpy.data.objects if o.type == 'MESH']

# Rigid parts (helmet, sword) hang from a bone through empties: skin them fully to that bone.
for o in meshes:
    if any(m.type == 'ARMATURE' for m in o.modifiers):
        continue
    p = o
    while p.parent_type != 'BONE':
        p = p.parent
    group = o.vertex_groups.new(name=p.parent_bone)
    group.add(range(len(o.data.vertices)), 1.0, 'REPLACE')
    mod = o.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm

# High copies to bake from.
high = []
for o in meshes:
    c = o.copy(); c.data = o.data.copy(); bpy.context.scene.collection.objects.link(c)
    mw = o.matrix_world.copy(); c.parent = None; c.matrix_world = mw
    high.append(c)

body = next(o for o in meshes if o.name == "Object_7")
# Height from the feet to the top of the helmet: the sword hangs below the feet.
helm = next(o for o in meshes if o.name == "Object_23")
height = max((helm.matrix_world @ v.co).z for v in helm.data.vertices) - min((body.matrix_world @ v.co).z for v in body.data.vertices)
mod = body.modifiers.new("Decimate", 'DECIMATE'); mod.ratio = ratio; mod.use_symmetry = False
common.select([body]); bpy.ops.object.modifier_apply(modifier="Decimate")
# The bones had no layout at all (their color is per vertex), so they get one.
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.01)
bpy.ops.object.mode_set(mode='OBJECT')
for o in meshes:
    mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
common.select([body] + [o for o in meshes if o != body])
bpy.ops.object.join()
low = body
low.name = "Body"
for o in list(bpy.data.objects):
    if o.type == 'EMPTY':
        bpy.data.objects.remove(o)

# The atlas starts from the original layouts: same islands, evened out to one texel density and packed.
low.data.uv_layers.active = low.data.uv_layers["UVMap"]
uv = low.data.uv_layers.new(name="Atlas", do_init=True)
low.data.uv_layers.active = uv
common.select([low])
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.context.scene.tool_settings.use_uv_select_sync = True
bpy.ops.uv.select_all(action='SELECT')
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
bpy.ops.object.mode_set(mode='OBJECT')
uv.active_render = True

sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.samples = 4; sc.cycles.use_denoising = False
sc.render.bake.use_selected_to_active = True
sc.render.bake.cage_extrusion = 0.15
sc.render.bake.max_ray_distance = 0.6
sc.render.bake.margin = 6
sc.render.bake.use_clear = False

# Baked one material at a time, each part only from its own originals: otherwise the bones pick up
# the torn cloth around them, holes included.
common.select([low])
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.separate(type='MATERIAL')
bpy.ops.object.mode_set(mode='OBJECT')
parts = [o for o in bpy.data.objects if o.type == 'MESH' and o not in high]

def image(name, color):
    img = bpy.data.images.new(name, size, size, alpha=False, float_buffer=False)
    if not color: img.colorspace_settings.name = 'Non-Color'
    return img

# Each original material can show its plain shader, or emit what feeds one of its inputs.
wiring = {}
for m in {s.material for h in high for s in h.material_slots}:
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    outn = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    em = nt.nodes.new('ShaderNodeEmission')
    wiring[m] = (nt, bsdf, outn, em)

def show(socket):
    for nt, bsdf, outn, em in wiring.values():
        if socket is None:
            nt.links.new(bsdf.outputs[0], outn.inputs[0]); continue
        for l in [l for l in nt.links if l.to_node == em]: nt.links.remove(l)
        src = bsdf.inputs[socket]
        linked = [l for l in nt.links if l.to_node == bsdf and l.to_socket == src]
        if linked: nt.links.new(linked[0].from_socket, em.inputs[0])
        else:
            v = src.default_value
            em.inputs[0].default_value = tuple(v) if socket == "Base Color" else (v, v, v, 1)
        nt.links.new(em.outputs[0], outn.inputs[0])

def bake(img, kind, **kw):
    for part in parts:
        mat = part.material_slots[0].material
        nt = mat.node_tree
        n = nt.nodes.get("BakeTarget") or nt.nodes.new('ShaderNodeTexImage')
        n.name = "BakeTarget"; n.image = img; nt.nodes.active = n
        sources = [h for h in high if any(s.material == mat for s in h.material_slots)]
        bpy.ops.object.select_all(action='DESELECT')
        for h in sources: h.select_set(True)
        part.select_set(True); bpy.context.view_layer.objects.active = part
        bpy.ops.object.bake(type=kind, **kw)

albedo = image("Albedo", True); show("Base Color"); bake(albedo, 'EMIT')
alpha = image("Alpha", False); show("Alpha"); bake(alpha, 'EMIT')
normal = image("Normal", False); normal.generated_color = (0.5, 0.5, 1, 1); show(None); bake(normal, 'NORMAL', normal_space='TANGENT')

common.select(parts)
bpy.ops.object.join()
low = bpy.context.view_layer.objects.active
low.name = "Body"

def make_death(knockdown, landed=7, fall=8):
    """There is no death clip, but the big hit knocks it flat before it gets back up. The death is that
    clip up to where it lies still (frame landed), with the fall, a single frame in the original,
    stretched to fall frames so it reads as falling instead of a jump cut."""
    death = knockdown.copy()
    death.name = "Lesha_death_GLTF_created_0"
    for fc in death.fcurves:
        points = fc.keyframe_points
        for i in reversed(range(len(points))):
            if points[i].co[0] > landed:
                points.remove(points[i])
        for k in points:
            f = k.co[0]
            k.co[0] = 1 + (f - 1) * fall if f <= 2 else f + fall - 1
            k.handle_left_type = k.handle_right_type = 'AUTO_CLAMPED'
        fc.update()


def pixels(img):
    return np.array(img.pixels[:], dtype=np.float32).reshape(size, size, 4)
rgba = pixels(albedo); rgba[..., 3] = pixels(alpha)[..., 0]
final = bpy.data.images.new("Skeleton_Albedo", size, size, alpha=True)
final.pixels[:] = rgba.ravel()
half = size // 2
for img, name in ((final, "Albedo"), (normal, "Normal")):
    img.scale(half, half)
    common.save_image(img, os.path.join(out, f"Skeleton_{name}.png"))

for h in high:
    bpy.data.objects.remove(h)
mat = bpy.data.materials.new("Skeleton")
for p in low.data.polygons: p.material_index = 0
low.data.materials.clear(); low.data.materials.append(mat)
for name in [l.name for l in low.data.uv_layers if l.name != "Atlas"]:
    low.data.uv_layers.remove(low.data.uv_layers[name])
low.data.uv_layers["Atlas"].name = "UVMap"
for c in list(low.data.color_attributes): low.data.color_attributes.remove(c)
low.data.name = "Skeleton"

arm.data.pose_position = 'POSE'
make_death(next(a for a in bpy.data.actions if a.name.startswith("Lesha_hitted_2")))
common.keep_actions({"Lesha_attack_1": "Attack", "Lesha_hitted_1": "Hit", "Lesha_hitted_2": "HitBig",
                     "Lesha_idle": "Idle", "Lesha_walk": "Walk", "Lesha_death": "Death"})
low.parent = arm
low.matrix_parent_inverse = arm.matrix_world.inverted()
# The idle is hunched: this leaves it about 1.8 m tall standing there.
common.flatten(arm, [low], 2.1 / height, "Skeleton")
common.export(os.path.join(out, "Skeleton.fbx"), [arm, low])
