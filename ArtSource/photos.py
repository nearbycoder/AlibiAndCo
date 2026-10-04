"""Wendell Fry's press photographs from the Harvest Ball (case 3), rendered as small scenes.

    flicker  the ballroom mid-waltz as the chandeliers dip
    terrace  the Grand's terrace in the rain; far behind, a figure in a yellow oilskin on the cliff path
    toast    Capt. Rook toasts the Mayor beneath the big chandelier
    raffle   Capt. Rook draws the raffle, hair oddly damp
"""
import math
import os
import random

import bpy
from mathutils import Vector

import lib
import portraits
from lib import material, box, cylinder, sphere, torus, link

rnd = random.Random(11)


def emissive(name, hexcol, strength):
    m = material(name, hexcol, rough=0.4)
    b = m.node_tree.nodes.get("Principled BSDF")
    r, g, bb = (lib.srgb_to_linear(v) for v in lib.hex_rgb(hexcol))
    b.inputs["Emission Color"].default_value = (r, g, bb, 1)
    b.inputs["Emission Strength"].default_value = strength
    return m


def figure(loc, coat="1B1B1E", skin="E2B596", height=1.7, yaw=0.0, arms_up=False, dress=False, hat=None, head=True):
    """A simple standing person (capsule body, head, legs)."""
    parts = []
    cm = material("fabric_" + coat, coat, rough=0.8)
    sm = material("skin_" + skin, skin, rough=0.55)
    s = height / 1.7
    if dress:
        parts.append(cylinder("skirt", 0.32 * s, 0.85 * s, (0, 0, 0), cm, seg=24, r2=0.16 * s))
    else:
        for sx in (-1, 1):
            parts.append(cylinder("leg", 0.07 * s, 0.85 * s, (sx * 0.09 * s, 0, 0), material("fabric_141414", "141414"), seg=12))
    body = cylinder("body", 0.2 * s, 0.6 * s, (0, 0, 0.82 * s), cm, seg=20, r2=0.17 * s)
    body.scale = (1, 0.65, 1)
    parts.append(body)
    if head:
        parts.append(sphere("head", 0.11 * s, (0, 0, 1.55 * s), sm, scale=(0.85, 0.9, 1.1)))
    for sx in (-1, 1):
        arm = cylinder("arm", 0.05 * s, 0.6 * s, (0, 0, 0), cm, seg=10)
        arm.location = (sx * 0.24 * s, 0, 1.38 * s if not arms_up else 1.3 * s)
        arm.rotation_euler = (0, math.radians(180 + sx * (20 if not arms_up else -150)), 0)
        parts.append(arm)
    if hat == "hood":
        parts.append(sphere("hood", 0.14 * s, (0, 0.02, 1.6 * s), cm, scale=(0.95, 1.0, 1.0)))
    root = lib.parent_all("figure", parts)
    root.location = loc
    root.rotation_euler = (0, 0, yaw)
    return root


def chandelier(loc, mat_glow, scale=1.0):
    brass = material("brass", "C9A24A", metallic=0.9, rough=0.3)
    parts = [torus("ring", 0.45 * scale, 0.02 * scale, (0, 0, 0), brass), cylinder("stem", 0.02 * scale, 0.8 * scale, (0, 0, 0), brass)]
    for i in range(10):
        a = i / 10 * math.pi * 2
        parts.append(sphere("bulb", 0.05 * scale, (math.cos(a) * 0.45 * scale, math.sin(a) * 0.45 * scale, 0.06 * scale), mat_glow))
    for i in range(24):
        a = i / 24 * math.pi * 2
        parts.append(sphere("crystal", 0.02 * scale, (math.cos(a) * 0.35 * scale, math.sin(a) * 0.35 * scale, -0.15 * scale), material("glass_E8EEF0", "E8EEF0", rough=0.05)))
    r = lib.parent_all("chandelier", parts)
    r.location = loc
    return r


def camera(loc, look, lens=35):
    cd = bpy.data.cameras.new("cam")
    cd.lens = lens
    cam = bpy.data.objects.new("cam", cd)
    cam.location = loc
    cam.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    link(cam)
    bpy.context.scene.camera = cam
    return cam


def light(kind, loc, energy, color=(1, 0.9, 0.75), size=1.0, look=(0, 0, 1)):
    L = bpy.data.lights.new("l", kind)
    L.energy = energy
    L.color = color
    if kind == "AREA":
        L.size = size
    if kind == "SPOT":
        L.spot_size = math.radians(60)
    o = bpy.data.objects.new("l", L)
    o.location = loc
    o.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    link(o)
    return o


def setup(res=512):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE_NEXT"
    sc.render.resolution_x = sc.render.resolution_y = res
    sc.eevee.taa_render_samples = 48
    try:
        sc.view_settings.view_transform = "AgX"
    except TypeError:
        pass
    w = bpy.data.worlds.new("w")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.01, 0.012, 0.016, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.4


def ballroom(dim=1.0):
    floor = box("floor", (14, 14, 0.05), (0, 0, -0.025), material("wood_6E4A2C", "6E4A2C", rough=0.25))
    wall = box("wall", (14, 0.2, 6), (0, 5, 3), material("col_6B2E2A", "6B2E2A", rough=0.8))
    for x in (-4, -1.5, 1, 3.5):
        box("pilaster", (0.5, 0.3, 6), (x, 4.85, 3), material("col_C9B48A", "C9B48A", rough=0.6))
    glow = emissive("glow_bulb_%d" % int(dim * 10), "FFD9A0", 18 * dim)
    chandelier((0, 1.5, 3.6), glow)
    chandelier((-3.5, 2.5, 3.8), glow, 0.8)
    chandelier((3.5, 2.5, 3.8), glow, 0.8)
    return glow


def scene_flicker():
    setup()
    ballroom(0.35)
    couples = [(-1.5, 1.0), (0.4, 0.6), (2.0, 1.6), (-0.4, 2.6), (1.2, 3.0), (-2.6, 2.2)]
    for i, (x, y) in enumerate(couples):
        figure((x - 0.16, y, 0), "141418", yaw=rnd.uniform(0, 6.28))
        figure((x + 0.16, y + 0.05, 0), rnd.choice(["6E2F4A", "2F4A6E", "7A6A3A", "3E5E4A"]), height=1.6, yaw=rnd.uniform(0, 6.28), dress=True)
    camera((0, -4.5, 1.7), (0, 2, 1.5), 32)
    light("POINT", (0, -3.5, 1.8), 120, (1.0, 0.95, 0.9))  # the press flash
    light("AREA", (0, 2, 4.5), 80, (1.0, 0.75, 0.5), 6, (0, 2, 0))


def scene_terrace():
    setup()
    stone = material("col_9A958C", "9A958C", rough=0.35)
    box("terrace", (8, 5, 0.1), (0, 0, -0.05), material("col_5E5A55", "5E5A55", rough=0.15))
    # Balustrade along the front edge.
    box("rail", (8, 0.25, 0.12), (0, 2.2, 1.0), stone)
    for i in range(16):
        x = -3.75 + i * 0.5
        cylinder("baluster", 0.07, 0.9, (x, 2.2, 0.05), stone, seg=10, r2=0.05)
    # Dark sea, the cliff path beyond (a pale line along the headland) and the lighthouse far off.
    box("sea", (200, 200, 0.1), (0, 100, -6), material("col_10181C", "10181C", rough=0.2))
    cliff = box("cliff", (40, 6, 6), (10, 16.5, -1.9), material("col_3A4034", "3A4034", rough=0.9))
    path = box("path", (40, 0.9, 0.1), (10, 14.2, 1.1), material("col_A39E8A", "A39E8A", rough=0.9))
    for i in range(7):
        cylinder("post", 0.06, 0.9, (-6 + i * 4, 13.6, 1.1), material("col_3A3328", "3A3328"), seg=6)
    lh = cylinder("lighthouse", 0.8, 9, (24, 60, -3), material("col_D8D3C8", "D8D3C8"), seg=16, r2=0.55)
    emissive("glow_lh", "FFE8B0", 6)
    sphere("lamp", 0.6, (24, 60, 6.3), bpy.data.materials["glow_lh"])
    # The figure: yellow oilskin, hood up, walking east along the cliff path.
    fig = figure((4.2, 14.2, 1.15), "E3B62A", skin="E3B62A", height=1.75, yaw=math.radians(-80), hat="hood")
    cylinder("lampPost", 0.05, 2.6, (5.4, 13.7, 1.15), material("col_1E1E1E", "1E1E1E"), seg=8)
    sphere("lampGlass", 0.12, (5.4, 13.7, 3.8), emissive("glow_path", "FFD08A", 8))
    light("POINT", (5.4, 13.4, 3.6), 120, (1.0, 0.85, 0.55))   # a path lamp the figure is passing
    # Rain streaks caught in the flash.
    rain = emissive("glow_rain", "C8D4DA", 1.4)
    for i in range(140):
        x, y, z = rnd.uniform(-3, 3), rnd.uniform(0.6, 4), rnd.uniform(0.0, 2.6)
        st = cylinder("rain", 0.004, rnd.uniform(0.15, 0.35), (x, y, z), rain, seg=4)
        st.rotation_euler = (math.radians(rnd.uniform(-8, -4)), math.radians(rnd.uniform(3, 9)), 0)
    # A couple of potted plants and a table at the edges.
    cylinder("pot", 0.3, 0.5, (-2.7, 1.2, 0), material("col_7A4A32", "7A4A32"), seg=16, r2=0.38)
    sphere("bush", 0.5, (-2.7, 1.2, 0.85), material("col_2F3E2A", "2F3E2A"))
    cylinder("table", 0.45, 0.04, (2.4, 0.6, 0.72), material("col_EDE6D6", "EDE6D6"), seg=24)
    cylinder("tleg", 0.04, 0.72, (2.4, 0.6, 0), stone, seg=8)
    camera((0, -2.5, 1.65), (1.8, 14, 1.4), 32)
    light("POINT", (0, -2.2, 1.6), 160, (1.0, 0.97, 0.92))  # flash
    light("SUN", (0, 0, 10), 0.6, (0.55, 0.65, 0.85), look=(0.3, 1, -0.6))


def scene_toast(draw_raffle=False):
    setup()
    ballroom(1.0)
    rook = portraits.build_character("rook")
    rook.location = (-0.35, 0.6, 1.45)
    rook.rotation_euler = (0, 0, math.radians(20))
    rbody = figure((-0.35, 0.65, 0), "141416", height=1.55, head=False)
    # The Mayor: a generic bust with a chain of office.
    portraits.CHARACTERS["mayor"] = dict(skin="E7B9A0", hair="bald_sides", hair_col="7A726A", mustache="pencil", clothes="suit",
                                         cloth_col="1E1E24", shirt_col="F1EFE9", tie="bow", tie_col="141414", brow=-1, mouth=0.3,
                                         age=0.8, face_w=1.1)
    mayor = portraits.build_character("mayor")
    mayor.location = (0.5, 0.75, 1.42)
    mayor.rotation_euler = (0, 0, math.radians(-25))
    figure((0.5, 0.8, 0), "1E1E24", height=1.52, head=False)
    gold = material("brass", "C9A24A", metallic=0.9, rough=0.3)
    chain = torus("chain", 0.17, 0.012, (0.5, 0.62, 1.2), gold)
    chain.rotation_euler = (math.radians(70), 0, math.radians(-25))
    glass = material("glass_E8EEF0", "E8EEF0", rough=0.05)
    if not draw_raffle:
        for x in (-0.15, 0.3):
            cylinder("flute", 0.03, 0.22, (x, 0.35, 1.5), glass, seg=12, r2=0.04)
    else:
        drum = cylinder("drum", 0.35, 0.6, (0, 0, 0), material("brass", "C9A24A", metallic=0.9, rough=0.3), seg=24)
        drum.rotation_euler = (0, math.radians(90), 0)
        drum.location = (-0.6, 0.1, 1.05)
        box("stand", (0.8, 0.4, 0.8), (-0.6, 0.1, 0.4), material("wood_4A2E1A", "4A2E1A"))
        ticket = box("ticket", (0.08, 0.005, 0.05), (-0.15, 0.25, 1.62), material("col_E05A4A", "E05A4A"))
        for i in range(6):
            figure((rnd.uniform(-2.5, 2.5), rnd.uniform(2.0, 3.5), 0), rnd.choice(["141418", "6E2F4A", "2F4A6E"]), height=rnd.uniform(1.55, 1.8),
                   dress=rnd.random() < 0.5)
    camera((0.1, -2.3, 1.55), (0.05, 0.7, 1.45), 40)
    light("POINT", (0.3, -2.0, 1.8), 110, (1.0, 0.96, 0.9))
    light("AREA", (0, 1.5, 4), 60, (1.0, 0.8, 0.55), 4, (0, 1, 0))


SCENES = {
    "flicker": scene_flicker,
    "terrace": scene_terrace,
    "toast": lambda: scene_toast(False),
    "raffle": lambda: scene_toast(True),
}


def build_all(only, preview):
    for pid, fn in SCENES.items():
        if only and pid not in only:
            continue
        lib.reset()
        fn()
        out = os.path.join(lib.RENDERS, f"photo_{pid}.png")
        lib.render_preview(out)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib.ROOT, "ArtSource", f"photo_{pid}.blend"))
        print("[photos] rendered", pid)
