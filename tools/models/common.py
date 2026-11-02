"""Shared steps for turning the Sketchfab downloads (glTF) into FBX models for Unity."""
import bpy
from mathutils import Matrix


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    for o in list(bpy.data.objects):
        if o.name.startswith("Icosphere"):
            bpy.data.objects.remove(o)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    return arm


def load_fbx(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, ignore_leaf_bones=True)
    return next(o for o in bpy.data.objects if o.type == 'ARMATURE')


def rename_clips(clips):
    """clips maps the end of each wanted take's name (FBX takes come as "Armature|Take|BaseLayer") to its
    clip name in Unity. The rest are dropped."""
    for a in list(bpy.data.actions):
        take = a.name.split("|")[1] if "|" in a.name else a.name
        key = next((k for k in clips if take.endswith(k)), None)
        if key is None:
            bpy.data.actions.remove(a)
        else:
            a.name = clips[key]
            a.use_fake_user = True


def top(arm, meshes, action):
    """Highest point of the meshes halfway through action, in world units."""
    arm.animation_data.action = action
    bpy.context.scene.frame_set(int(sum(action.frame_range) / 2))
    dg = bpy.context.evaluated_depsgraph_get()
    return max((o.evaluated_get(dg).matrix_world @ v.co).z for o in meshes for v in o.evaluated_get(dg).to_mesh().vertices)


def cut(action, start, end, name, pingpong=False):
    """A new action with the stretch of action between start and end seconds, moved to start at 0.
    pingpong plays it forward and then back, for a loop out of a stretch that doesn't loop. Keys are
    sampled every frame, which also keeps the cut edges exact."""
    fps = bpy.context.scene.render.fps
    first = round(start * fps + action.frame_range[0])
    count = round((end - start) * fps) + 1
    frames = list(range(count))
    if pingpong:
        frames += list(range(count, 2 * count - 1))
    clip = bpy.data.actions.new(name)
    for fc in action.fcurves:
        values = [fc.evaluate(first + f) for f in range(count)]
        if pingpong:
            values += values[-2::-1]
        out = clip.fcurves.new(fc.data_path, index=fc.array_index, action_group=fc.group.name if fc.group else "")
        out.keyframe_points.add(len(frames))
        co = [x for pair in zip(frames, values) for x in pair]
        out.keyframe_points.foreach_set("co", co)
        out.keyframe_points.foreach_set("interpolation", [1] * len(frames))
        out.update()
    clip.use_fake_user = True
    return clip


def select(objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def flatten(arm, meshes, factor, name):
    """Takes the armature and its meshes out of the importer's empties and scales them by factor.
    The transforms stay on the objects: baking them in breaks the skinning of these files."""
    for o in [arm] + meshes:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
    arm.matrix_world = Matrix.Scale(factor, 4) @ arm.matrix_world
    for o in meshes:
        o.matrix_world = Matrix.Scale(factor, 4) @ o.matrix_world
    for o in list(bpy.data.objects):
        if o.type == 'EMPTY':
            bpy.data.objects.remove(o)
    arm.name = name
    arm.data.name = name


def keep_actions(renames):
    """renames maps the importer's action names (without the suffix) to the clip names in Unity."""
    for a in list(bpy.data.actions):
        short = a.name.replace("_GLTF_created_0", "")
        if short in renames:
            a.name = renames[short]
            a.use_fake_user = True
        else:
            bpy.data.actions.remove(a)


def save_image(image, path):
    image.filepath_raw = path
    image.file_format = 'PNG'
    image.save()


def export(path, objs):
    select(objs)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
                             use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.5,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE')
