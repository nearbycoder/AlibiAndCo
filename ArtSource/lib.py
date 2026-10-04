"""Shared helpers for the Alibi & Co. Blender generators (Blender 4.5, bpy).

Conventions
- Model in metres, Z up. FBX export maps Z->Y for Unity; Unity imports 1 m = 1 unit and the
  game scales props by 10 (the set is built in decimetres).
- Material names encode how Unity should shade them (see Art.RemapImported):
  col_RRGGBB, metal_RRGGBB, brass, glow_RRGGBB, glass_RRGGBB, wood_RRGGBB, ceramic_RRGGBB,
  plastic_RRGGBB, leather_RRGGBB, paper_RRGGBB, fabric_RRGGBB.
"""
import math
import os

import bpy
import bmesh
from mathutils import Vector, Matrix

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS = os.path.join(ROOT, "Assets", "Resources", "Models")
RENDERS = os.path.join(ROOT, "ArtSource", "renders")
os.makedirs(MODELS, exist_ok=True)
os.makedirs(RENDERS, exist_ok=True)


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def material(name, color=None, metallic=0.0, rough=0.5, emission=None, alpha=1.0, transmission=0.0):
    """Get or create a Principled material. `name` is what Unity sees."""
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if color is None:
        part = name.split("_")[-1]
        color = part if len(part) == 6 else "888888"
    r, g, b = (srgb_to_linear(v) for v in hex_rgb(color))
    bsdf.inputs["Base Color"].default_value = (r, g, b, 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    if emission:
        er, eg, eb = (srgb_to_linear(v) for v in hex_rgb(emission))
        bsdf.inputs["Emission Color"].default_value = (er, eg, eb, 1)
        bsdf.inputs["Emission Strength"].default_value = 4.0
    if transmission:
        bsdf.inputs["Transmission Weight"].default_value = transmission
    if alpha < 1:
        bsdf.inputs["Alpha"].default_value = alpha
    m.diffuse_color = (r, g, b, 1)
    return m


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for c in (bpy.data.meshes, bpy.data.materials, bpy.data.objects):
        for x in list(c):
            c.remove(x)


def link(obj, coll=None):
    (coll or bpy.context.scene.collection).objects.link(obj)
    return obj


def mesh_obj(name, bm, mat=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    link(ob)
    if mat:
        ob.data.materials.append(mat)
    return ob


def smooth(ob, angle=40):
    for p in ob.data.polygons:
        p.use_smooth = True
    if hasattr(ob.data, "set_sharp_from_angle"):
        ob.data.set_sharp_from_angle(angle=math.radians(angle))
    return ob


def bevel(ob, width=0.002, segments=2, limit=30):
    mod = ob.modifiers.new("bevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(limit)
    return ob


def apply_mods(ob):
    bpy.context.view_layer.objects.active = ob
    for m in list(ob.modifiers):
        try:
            bpy.ops.object.modifier_apply(modifier=m.name)
        except RuntimeError:
            pass


def box(name, size, loc=(0, 0, 0), mat=None, bev=0.0, seg=2):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    ob = mesh_obj(name, bm, mat)
    if bev > 0:
        bevel(ob, bev, seg)
    return ob


def cylinder(name, r, h, loc=(0, 0, 0), mat=None, seg=32, r2=None, cap=True):
    """Cylinder (or cone frustum if r2 given) standing on loc (base at loc.z)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=h)
    bmesh.ops.translate(bm, vec=Vector((loc[0], loc[1], loc[2] + h / 2)), verts=bm.verts)
    return mesh_obj(name, bm, mat)


def sphere(name, r, loc=(0, 0, 0), mat=None, seg=24, rings=12, scale=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return smooth(mesh_obj(name, bm, mat))


def torus(name, R, r, loc=(0, 0, 0), mat=None, seg=48, rseg=12, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=seg, minor_segments=rseg, location=loc, rotation=rot)
    ob = bpy.context.active_object
    ob.name = name
    if mat:
        ob.data.materials.append(mat)
    return smooth(ob)


def lathe(name, profile, mat=None, seg=48, loc=(0, 0, 0)):
    """Revolve a list of (radius, z) points around Z."""
    bm = bmesh.new()
    verts = [bm.verts.new((r, 0, z)) for r, z in profile]
    for a, b in zip(verts, verts[1:]):
        bm.edges.new((a, b))
    bmesh.ops.spin(bm, geom=bm.verts[:] + bm.edges[:], cent=(0, 0, 0), axis=(0, 0, 1), angle=math.radians(360), steps=seg, use_merge=True)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return smooth(mesh_obj(name, bm, mat), 50)


def extrude_profile(name, profile2d, length, mat=None, axis="X"):
    """Extrude a closed 2D profile (y, z) along X from -length/2 to +length/2."""
    bm = bmesh.new()
    vs0 = [bm.verts.new((-length / 2, y, z)) for y, z in profile2d]
    vs1 = [bm.verts.new((length / 2, y, z)) for y, z in profile2d]
    n = len(profile2d)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((vs0[i], vs0[j], vs1[j], vs1[i]))
    bm.faces.new(vs0[::-1])
    bm.faces.new(vs1)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return mesh_obj(name, bm, mat)


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
        apply_mods(o)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.active_object
    ob.name = name
    return ob


def parent_all(name, objs):
    root = bpy.data.objects.new(name, None)
    link(root)
    for o in objs:
        o.parent = root
    return root


def export_fbx(root, filename):
    bpy.ops.object.select_all(action="DESELECT")
    stack = [root]
    while stack:
        o = stack.pop()
        o.select_set(True)
        for m in list(getattr(o, "modifiers", [])):
            bpy.context.view_layer.objects.active = o
            try:
                bpy.ops.object.modifier_apply(modifier=m.name)
            except RuntimeError:
                pass
        stack.extend(o.children)
    bpy.context.view_layer.objects.active = root
    path = os.path.join(MODELS, filename + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH", "EMPTY"},
        mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
        path_mode="STRIP")
    return path


# --------------------------------------------------------------------------- preview rendering

def setup_preview_scene(target_size=0.3, top_down=False, res=(640, 480)):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.film_transparent = False
    world = bpy.data.worlds.new("w") if not sc.world else sc.world
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.05, 0.045, 0.04, 1)
    bg.inputs[1].default_value = 0.6
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    link(cam)
    d = target_size * 2.6
    if top_down:
        cam.location = (0, -d * 0.12, d)
        cam.rotation_euler = (math.radians(7), 0, 0)
    else:
        cam.location = (d * 0.7, -d * 0.9, d * 0.7)
        direction = Vector((0, 0, target_size * 0.25)) - cam.location
        cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = 50
    sc.camera = cam
    key = bpy.data.lights.new("key", "AREA")
    key.energy = 120 * (target_size / 0.3) ** 2
    key.size = target_size
    key.color = (1.0, 0.85, 0.65)
    k = bpy.data.objects.new("key", key)
    k.location = (-target_size * 1.5, -target_size, target_size * 2.5)
    k.rotation_euler = (math.radians(35), math.radians(-30), 0)
    link(k)
    fill = bpy.data.lights.new("fill", "AREA")
    fill.energy = 40 * (target_size / 0.3) ** 2
    fill.size = target_size * 2
    fill.color = (0.6, 0.75, 1.0)
    f = bpy.data.objects.new("fill", fill)
    f.location = (target_size * 2, target_size, target_size * 1.5)
    f.rotation_euler = (math.radians(50), math.radians(40), 0)
    link(f)
    floor = box("floor", (target_size * 8, target_size * 8, 0.002), (0, 0, -0.001), material("preview_floor", "5A3A22", rough=0.6))
    return cam, [k, f, floor, cam]


def render_preview(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def cleanup(objs):
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)
