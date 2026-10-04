"""Top-down 'tourist map' render of Wrenhaven, built from Assets/Resources/Data/town.json so streets
and places line up exactly with the game's overlays.

Paper space: X in [0, 1.6], Y in [0, 1] (1.6:1 sheet). The town -> paper transform below is
mirrored in MapView.cs (keep them in sync).
"""
import json
import math
import os
import random

import bmesh
import bpy
from mathutils import Vector

import lib
from lib import link, mesh_obj

PAPER_W, PAPER_H = 1.6, 1.0
TOWN_MIN = (5.5, 0.5)
TOWN_MAX = (47.5, 29.5)
CONTENT = (0.08, 0.07, 1.52, 0.88)  # x0, y0, x1, y1 in paper units


def transform():
    tw, th = TOWN_MAX[0] - TOWN_MIN[0], TOWN_MAX[1] - TOWN_MIN[1]
    cw, ch = CONTENT[2] - CONTENT[0], CONTENT[3] - CONTENT[1]
    s = min(cw / tw, ch / th)
    ox = CONTENT[0] + (cw - tw * s) / 2
    oy = CONTENT[1] + (ch - th * s) / 2
    return s, ox, oy


S, OX, OY = transform()


def P(x, y, z=0.0):
    return Vector((OX + (x - TOWN_MIN[0]) * S, OY + (y - TOWN_MIN[1]) * S, z))


def flat(name, hexcol):
    """Shadeless colour (emission) so the render reads as printed ink."""
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    r, g, b = (lib.srgb_to_linear(v) for v in lib.hex_rgb(hexcol))
    em.inputs[0].default_value = (r, g, b, 1)
    em.inputs[1].default_value = 1.0
    nt.links.new(em.outputs[0], out.inputs[0])
    return m


def poly(name, pts, mat, z):
    bm = bmesh.new()
    vs = [bm.verts.new((p.x, p.y, z)) for p in pts]
    bm.faces.new(vs)
    return mesh_obj(name, bm, mat)


def strip(name, pts, width, mat, z):
    bm = bmesh.new()
    left, right = [], []
    for i, p in enumerate(pts):
        a = pts[max(0, i - 1)]
        b = pts[min(len(pts) - 1, i + 1)]
        d = (b - a)
        d.z = 0
        d.normalize()
        n = Vector((-d.y, d.x, 0)) * width / 2
        left.append(bm.verts.new((p.x + n.x, p.y + n.y, z)))
        right.append(bm.verts.new((p.x - n.x, p.y - n.y, z)))
    for i in range(len(pts) - 1):
        bm.faces.new((left[i], right[i], right[i + 1], left[i + 1]))
    return mesh_obj(name, bm, mat)


def disc(name, c, r, mat, z, seg=24):
    bm = bmesh.new()
    vs = [bm.verts.new((c.x + r * math.cos(2 * math.pi * i / seg), c.y + r * math.sin(2 * math.pi * i / seg), z)) for i in range(seg)]
    bm.faces.new(vs)
    return mesh_obj(name, bm, mat)


def rect(name, c, w, h, mat, z, angle=0.0):
    ca, sa = math.cos(angle), math.sin(angle)
    pts = []
    for dx, dy in ((-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)):
        pts.append(Vector((c.x + dx * ca - dy * sa, c.y + dx * sa + dy * ca, z)))
    return poly(name, pts, mat, z)


def text(body, loc, size, mat, font, align="LEFT", z=0.02, rot=0.0):
    cu = bpy.data.curves.new("t", "FONT")
    cu.body = body
    cu.size = size
    cu.align_x = align
    if font:
        cu.font = font
    ob = bpy.data.objects.new("text", cu)
    ob.location = (loc.x, loc.y, z)
    ob.rotation_euler = (0, 0, rot)
    link(ob)
    ob.data.materials.append(mat)
    return ob


def coast_y(x):
    """Coastline (town units). Sea is below. A bay for the harbour, the headland at Wren Point."""
    y = 7.2 + 0.9 * math.sin(x * 0.35) + 0.4 * math.sin(x * 0.9 + 1.0)
    if 15 < x < 30:
        y -= 1.6 * math.sin((x - 15) / 15 * math.pi)  # harbour bay
    if x > 39:
        y -= (x - 39) * 0.55  # headland slopes out to the point
    return y


def build(preview):
    lib.reset()
    raw = open(os.path.join(lib.ROOT, "Assets", "Resources", "Data", "town.json")).read()
    town = json.loads("\n".join(l for l in raw.splitlines() if not l.strip().startswith("//")))
    locs = {l["id"]: l for l in town["locations"]}
    rnd = random.Random(7)

    paper = flat("paper", "EDE2C6")
    sea = flat("sea", "AFC5C2")
    sea_line = flat("seaLine", "93AEAD")
    sand = flat("sand", "E3D2A6")
    road_case = flat("roadCase", "8A7A62")
    road = flat("road", "F7F1E0")
    block = flat("block", "C98F6B")
    block_dark = flat("blockDark", "B07A5A")
    park = flat("park", "B9C79A")
    tree = flat("tree", "8FA474")
    water_ink = flat("ink", "3E4A52")
    red = flat("red", "A23B2E")
    rail = flat("rail", "5B5048")
    glass = flat("glass", "C8DCDC")

    # Paper.
    poly("paper", [Vector((0, 0)), Vector((PAPER_W, 0)), Vector((PAPER_W, PAPER_H)), Vector((0, PAPER_H))], paper, 0)

    # Sea polygon under the coastline, with a beach strip and wave contour lines.
    x_left = TOWN_MIN[0] - OX / S - 1
    x_right = TOWN_MIN[0] + (PAPER_W - OX) / S + 1
    xs = [x_left + i * 0.5 for i in range(int((x_right - x_left) / 0.5) + 1)]
    coast = [P(x, coast_y(x)) for x in xs]
    seapts = [P(xs[0], TOWN_MIN[1] - 6)] + coast + [P(xs[-1], TOWN_MIN[1] - 6)]
    poly("sea", seapts, sea, 0.001)
    strip("beach", coast, 0.012, sand, 0.0015)
    for k in range(1, 6):
        off = [P(x, coast_y(x) - k * 0.9 - 0.25 * math.sin(x * 0.5 + k)) for x in xs]
        strip(f"wave{k}", off, 0.0022, sea_line, 0.0016)

    # Pier into the sea from the pier location, with a pavilion at the end.
    pier = locs["pier"]
    p0 = P(pier["x"], coast_y(pier["x"]) + 0.3)
    p1 = P(pier["x"], coast_y(pier["x"]) - 4.5)
    strip("pier", [p0, p1], 0.018, flat("pierWood", "B89B72"), 0.004)
    disc("pavilion", p1, 0.016, block_dark, 0.0045)
    # Harbour wall.
    hw = [P(x, coast_y(x) - 0.8) for x in [16, 17, 18.5, 20]]
    strip("harbourWall", hw, 0.008, flat("stone", "9C9283"), 0.004)

    # Park around the glasshouse and a green by the church.
    g = locs["glasshouse"]
    blob = [P(g["x"] + 3.2 * math.cos(a) * (1 + 0.15 * math.sin(3 * a)), g["y"] + 2.6 * math.sin(a)) for a in [i * math.pi / 12 for i in range(24)]]
    poly("park", blob, park, 0.002)
    for _ in range(26):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(0.6, 2.4)
        disc("tree", P(g["x"] + math.cos(a) * r * 1.2, g["y"] + math.sin(a) * r), rnd.uniform(0.006, 0.01), tree, 0.003, 12)
    c = locs["church"]
    poly("green", [P(c["x"] - 1.6, c["y"] - 1.2), P(c["x"] + 1.6, c["y"] - 1.2), P(c["x"] + 1.6, c["y"] + 1.4), P(c["x"] - 1.6, c["y"] + 1.4)], park, 0.002)

    # Railway leaving the station to the north-west.
    st = locs["station"]
    rail_pts = [P(st["x"], st["y"]), P(st["x"] - 3, st["y"] + 1.5), P(st["x"] - 9, st["y"] + 2.4)]
    strip("rail", rail_pts, 0.006, rail, 0.0042)
    for i in range(18):
        t = i / 17
        a = rail_pts[0].lerp(rail_pts[1], t * 2) if t < 0.5 else rail_pts[1].lerp(rail_pts[2], (t - 0.5) * 2)
        rect("sleeper", a, 0.004, 0.014, rail, 0.0043, math.radians(30))

    # Streets: cased roads.
    for st_ in town["streets"]:
        a, b = locs[st_["a"]], locs[st_["b"]]
        pa, pb = P(a["x"], a["y"]), P(b["x"], b["y"])
        strip("roadCase", [pa, pb], 0.022, road_case, 0.005)
    for st_ in town["streets"]:
        a, b = locs[st_["a"]], locs[st_["b"]]
        pa, pb = P(a["x"], a["y"]), P(b["x"], b["y"])
        strip("road", [pa, pb], 0.015, road, 0.006)
    for l in town["locations"]:
        disc("junction", P(l["x"], l["y"]), 0.0105, road_case, 0.0055, 20)
        disc("junction", P(l["x"], l["y"]), 0.0072, road, 0.0062, 20)

    # Houses along every street (both sides), skipping water, roads and landmarks.
    def near_road(p, margin):
        for st_ in town["streets"]:
            a, b = locs[st_["a"]], locs[st_["b"]]
            pa, pb = Vector((a["x"], a["y"])), Vector((b["x"], b["y"]))
            ab = pb - pa
            t = max(0, min(1, (p - pa).dot(ab) / ab.length_squared))
            if (pa + ab * t - p).length < margin:
                return True
        return False

    def near_place(p, margin):
        return any((Vector((l["x"], l["y"])) - p).length < margin for l in town["locations"])

    for st_ in town["streets"]:
        a, b = locs[st_["a"]], locs[st_["b"]]
        pa, pb = Vector((a["x"], a["y"])), Vector((b["x"], b["y"]))
        d = (pb - pa)
        L = d.length
        d.normalize()
        n = Vector((-d.y, d.x))
        ang = math.atan2(d.y, d.x)
        steps = int(L / 1.15)
        for i in range(1, steps):
            for side in (-1, 1):
                if rnd.random() < 0.2:
                    continue
                p = pa + d * (i * L / steps) + n * side * rnd.uniform(1.0, 1.35)
                if p.y < coast_y(p.x) + 0.6 or near_place(p, 1.6) or near_road(p, 0.75):
                    continue
                rect("house", P(p.x, p.y), rnd.uniform(0.016, 0.026), rnd.uniform(0.012, 0.018),
                     block if rnd.random() < 0.7 else block_dark, 0.0065, ang + rnd.uniform(-0.05, 0.05))

    # Landmarks.
    def lm(lid, kind):
        l = locs[lid]
        p = P(l["x"], l["y"])
        off = Vector((0, 0.03, 0))
        q = p + off
        if kind == "church":
            rect("nave", q, 0.05, 0.02, block_dark, 0.007)
            rect("transept", q, 0.02, 0.04, block_dark, 0.0071)
            disc("tower", q + Vector((-0.03, 0, 0)), 0.01, block_dark, 0.0072, 16)
        elif kind == "lighthouse":
            disc("tower", p, 0.014, flat("lhWhite", "F4EFE4"), 0.008, 24)
            disc("band", p, 0.009, red, 0.0081, 24)
            disc("lamp", p, 0.005, flat("lhLamp", "E8C25A"), 0.0082, 16)
        elif kind == "hotel":
            rect("hotel", q, 0.07, 0.03, block_dark, 0.007)
            rect("wing", q + Vector((0.03, -0.012, 0)), 0.02, 0.03, block_dark, 0.0071)
        elif kind == "glasshouse":
            rect("glasshouse", q + Vector((0, -0.01, 0)), 0.05, 0.022, glass, 0.007)
            disc("dome", q + Vector((0, -0.01, 0)), 0.013, glass, 0.0071, 20)
        elif kind == "station":
            rect("station", q, 0.06, 0.016, block_dark, 0.007, math.radians(25))
        elif kind == "boathouse":
            rect("boathouse", p + Vector((-0.012, -0.022, 0)), 0.035, 0.024, flat("shed", "8E6E52"), 0.007)
            strip("slip", [p + Vector((-0.012, -0.034, 0)), p + Vector((-0.012, -0.07, 0))], 0.012, flat("slipway", "A79C8A"), 0.0068)
        elif kind == "townhall":
            rect("townhall", q, 0.045, 0.03, block_dark, 0.007)
            disc("clock", q + Vector((0, 0.02, 0)), 0.006, flat("clockface", "F4EFE4"), 0.0072, 16)
            poly("square", [p + Vector((-0.03, -0.035, 0)), p + Vector((0.03, -0.035, 0)), p + Vector((0.03, -0.012, 0)), p + Vector((-0.03, -0.012, 0))],
                 flat("paving", "E2D6BC"), 0.0066)
        else:
            rect(lid, q, 0.032, 0.02, block_dark, 0.007)

    for lid, kind in [("church", "church"), ("lighthouse", "lighthouse"), ("hotel", "hotel"), ("glasshouse", "glasshouse"),
                      ("station", "station"), ("boathouse", "boathouse"), ("townhall", "townhall"), ("cinema", "x"),
                      ("depot", "x"), ("bakery", "x"), ("hardware", "x"), ("cafe", "x"), ("lantern", "x"), ("bandstand", "bandstand")]:
        if kind == "bandstand":
            p = P(locs[lid]["x"], locs[lid]["y"])
            disc("bandstand", p + Vector((0, 0.025, 0)), 0.012, flat("bandstandRoof", "6E8B5E"), 0.007, 8)
        else:
            lm(lid, kind)

    # Cartouche, compass and scale bar.
    font = None
    try:
        font = bpy.data.fonts.load(os.path.join(lib.ROOT, "Assets", "Fonts", "AbrilFatface-Regular.ttf"))
    except Exception:
        pass
    font2 = None
    try:
        font2 = bpy.data.fonts.load(os.path.join(lib.ROOT, "Assets", "Fonts", "IBMPlexSansCondensed-Medium.ttf"))
    except Exception:
        pass
    ink = flat("inkText", "3A3128")
    text("Wrenhaven", Vector((0.06, 0.915)), 0.06, ink, font)
    text("TOWN PLAN  ·  WALKING TIMES IN MINUTES", Vector((0.36, 0.925)), 0.018, ink, font2)
    text("THE CHANNEL", Vector((0.62, 0.05)), 0.022, flat("seaText", "6F8A8A"), font2)
    # Compass.
    cpos = Vector((1.5, 0.83, 0))
    poly("compassN", [cpos + Vector((0, 0.05, 0)), cpos + Vector((0.012, 0, 0)), cpos + Vector((-0.012, 0, 0))], red, 0.009)
    poly("compassS", [cpos + Vector((0, -0.05, 0)), cpos + Vector((0.012, 0, 0)), cpos + Vector((-0.012, 0, 0))], ink, 0.009)
    text("N", cpos + Vector((0, 0.058, 0)), 0.02, ink, font2, "CENTER")
    # Scale bar: 5 minutes.
    sb0 = Vector((1.18, 0.12, 0))
    w5 = 5 * S
    rect("scale", sb0 + Vector((w5 / 2, 0, 0)), w5, 0.006, ink, 0.009)
    text("5 MIN WALK", sb0 + Vector((w5 / 2, 0.012, 0)), 0.014, ink, font2, "CENTER")

    # Camera and render.
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE_NEXT"
    sc.render.resolution_x, sc.render.resolution_y = 2048, 1280
    sc.eevee.taa_render_samples = 32
    try:
        sc.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = PAPER_W
    cam = bpy.data.objects.new("cam", cam_data)
    cam.location = (PAPER_W / 2, PAPER_H / 2, 1.0)
    link(cam)
    sc.camera = cam
    world = bpy.data.worlds.new("w")
    sc.world = world
    out = os.path.join(lib.RENDERS, "map_town_raw.png")
    lib.render_preview(out)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib.ROOT, "ArtSource", "town_map.blend"))
    print("[map] rendered", out, "S=", S, "OX=", OX, "OY=", OY)
