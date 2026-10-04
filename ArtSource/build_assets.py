"""Entry point for every Blender-made asset in Alibi & Co.

    blender -b -P ArtSource/build_assets.py -- props [--only lamp,mug] [--preview]
    blender -b -P ArtSource/build_assets.py -- cards [--preview]
    blender -b -P ArtSource/build_assets.py -- portraits [--preview]
    blender -b -P ArtSource/build_assets.py -- map
    blender -b -P ArtSource/build_assets.py -- photos
    blender -b -P ArtSource/build_assets.py -- all --preview

FBX models go to Assets/Resources/Models, rendered textures to Assets/Resources/{Portraits,Photos,Textures},
previews to ArtSource/renders. Each group also saves its .blend source in ArtSource/ (zstd-compressed
to keep the repository lean).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

import lib  # noqa: E402


def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    groups = [x for x in a if not x.startswith("--") and "," not in x]
    only = None
    if "--only" in a:
        only = a[a.index("--only") + 1].split(",")
        groups = [g for g in groups if g != a[a.index("--only") + 1]]
    return groups or ["all"], only, "--preview" in a


def do_props(only, preview):
    import props
    names = [n for n in props.BUILDERS if only is None or n in only]
    for n in names:
        lib.reset()
        root = props.BUILDERS[n]()
        lib.export_fbx(root, n)
        print(f"[props] exported {n}")
        if preview:
            size = props.PREVIEW_SIZE.get(n, 0.3)
            _, extra = lib.setup_preview_scene(size, top_down=(n == "desk"))
            lib.render_preview(os.path.join(lib.RENDERS, f"prop_{n}.png"))
    # Save an editable source with everything laid out in a row.
    lib.reset()
    x = 0.0
    for n in props.BUILDERS:
        root = props.BUILDERS[n]()
        if n != "desk":
            root.location.x = x
            root.location.y = 2.0
            x += props.PREVIEW_SIZE.get(n, 0.3) * 1.6
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib.ROOT, "ArtSource", "props.blend"), compress=True)


def do_cards(only, preview):
    import cards
    cards.build_all(only, preview)


def do_portraits(only, preview):
    import portraits
    portraits.build_all(only, preview)


def do_map(only, preview):
    import town_map
    town_map.build(preview)


def do_photos(only, preview):
    import photos
    photos.build_all(only, preview)


if __name__ == "__main__":
    groups, only, preview = args()
    if "all" in groups:
        groups = ["props", "cards", "portraits", "map", "photos"]
    for g in groups:
        {"props": do_props, "cards": do_cards, "portraits": do_portraits, "map": do_map, "photos": do_photos}[g](only, preview)
    print("[build_assets] done:", ", ".join(groups))
