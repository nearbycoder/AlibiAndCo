"""Evidence card silhouettes: each kind of paper gets its own outline, extruded to a thin card.

Modelled directly in game units (the card sizes in CardView.cs: full 4.1 x 2.65, chip 1.5 x 0.72)
so they import at scale 1. Top face UVs map the card rectangle 0..1 for the paper texture.
"""
import math
import random

import bmesh
import bpy
from mathutils import Vector

import lib

SIZES = {"full": (4.1, 2.65, 0.03), "chip": (1.5, 0.72, 0.03)}
SHAPES = ["index", "receipt", "ledger", "slip", "ticket", "photo", "cutting"]


def rounded_rect(w, h, r, seg=4):
    pts = []
    for cx, cy, a0 in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)):
        for i in range(seg + 1):
            a = math.radians(a0 + 90 * i / seg)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def zigzag_edge(x0, x1, y, tooth, depth, up):
    """Points from x0 to x1 along a zig-zag edge at height y (teeth point outward)."""
    n = max(2, int(abs(x1 - x0) / tooth))
    pts = []
    for i in range(n + 1):
        x = x0 + (x1 - x0) * i / n
        out = depth if i % 2 == 1 else 0
        pts.append((x, y + (out if up else -out)))
    return pts


def torn_edge(x0, x1, y, amp, rnd, up, step=0.06):
    n = max(2, int(abs(x1 - x0) / step))
    pts = []
    for i in range(n + 1):
        x = x0 + (x1 - x0) * i / n
        pts.append((x, y + (rnd.uniform(0, amp) if up else -rnd.uniform(0, amp))))
    return pts


def outline(shape, w, h, rnd):
    hw, hh = w / 2, h / 2
    small = w < 2
    if shape == "index":
        return rounded_rect(w, h, 0.05 if small else 0.09)
    if shape == "photo":
        return rounded_rect(w, h, 0.02 if small else 0.04, 2)
    if shape == "receipt":
        tooth = 0.08 if small else 0.16
        depth = 0.035 if small else 0.07
        top = zigzag_edge(hw, -hw, hh - depth, tooth, depth, True)
        bottom = zigzag_edge(-hw, hw, -hh + depth, tooth, depth, False)
        return top + bottom
    if shape == "ledger":
        top = torn_edge(hw, -hw, hh - 0.03, 0.03 if small else 0.05, rnd, True, 0.04 if small else 0.07)
        return top + [(-hw, -hh), (hw, -hh)]
    if shape == "slip":
        c = 0.08 if small else 0.18
        return [(hw, hh - c), (hw - c, hh), (-hw, hh), (-hw, -hh), (hw, -hh)]
    if shape == "ticket":
        r = 0.11 if small else 0.26
        pts = [(hw, hh), (-hw, hh)]
        for i in range(9):  # left notch (semicircle cut in)
            a = math.radians(90 - 180 * i / 8)
            pts.append((-hw + r * math.cos(a), r * math.sin(a)))
        pts += [(-hw, -hh), (hw, -hh)]
        for i in range(9):
            a = math.radians(-90 - 180 * i / 8)
            pts.append((hw + r * math.cos(a), r * math.sin(a)))
        return pts
    if shape == "cutting":
        amp = 0.025 if small else 0.05
        st = 0.04 if small else 0.07
        top = torn_edge(hw, -hw, hh - amp, amp, rnd, True, st)
        bottom = torn_edge(-hw, hw, -hh + amp, amp, rnd, False, st)
        return top + bottom
    return rounded_rect(w, h, 0.05)


def build_card(shape, size_name):
    w, h, t = SIZES[size_name]
    rnd = random.Random(hash((shape, size_name)) & 0xFFFF)
    pts = outline(shape, w, h, rnd)
    bm = bmesh.new()
    verts = [bm.verts.new((x, y, 0)) for x, y in pts]
    face = bm.faces.new(verts)
    if face.normal.z < 0:
        face.normal_flip()
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    top_verts = [e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, vec=Vector((0, 0, t)), verts=top_verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    uv = bm.loops.layers.uv.new("uv")
    for f in bm.faces:
        for loop in f.loops:
            co = loop.vert.co
            loop[uv].uv = (co.x / w + 0.5, co.y / h + 0.5)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    mat = lib.material("paper_FFFFFF", "FFFFFF", rough=0.8)
    ob = lib.mesh_obj(f"card_{shape}_{size_name}", bm, mat)
    root = lib.parent_all(f"card_{shape}_{size_name}", [ob])
    ob.name = "paper"
    return root


def build_all(only, preview):
    for shape in SHAPES:
        for size in SIZES:
            name = f"card_{shape}_{size}"
            if only and name not in only and shape not in only:
                continue
            lib.reset()
            root = build_card(shape, size)
            lib.export_fbx(root, name)
            print("[cards] exported", name)
    if preview:
        lib.reset()
        x = 0
        for shape in SHAPES:
            r = build_card(shape, "full")
            r.location = (x, 0, 0)
            x += 4.5
        cam, extra = lib.setup_preview_scene(6.0, top_down=True, res=(1600, 500))
        cam.location = (x / 2 - 2.25, 0, 16)
        cam.rotation_euler = (0, 0, 0)
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = x + 1
        lib.render_preview(__import__('os').path.join(lib.RENDERS, "cards.png"))
