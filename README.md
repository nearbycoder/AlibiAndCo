<div align="center">

<img src="docs/media/teaser.webp" alt="Alibi &amp; Co. gameplay: a card is pinned and its ribbon turns red, a lie is stamped FALSE, and an alibi breaks open" width="100%">

# Alibi & Co.

**Pin the evidence to the timeline. Then find the alibi that can't be true.**

A cozy-noir deduction game about physically assembling a timeline, and then breaking it.

[![Unity 6000.6](https://img.shields.io/badge/Unity-6000.6.2f1%20·%20URP-222?logo=unity&logoColor=white)](https://unity.com/releases/editor/archive)
[![Platform: Linux](https://img.shields.io/badge/platform-Linux%20x86__64-2f5d8a?logo=linux&logoColor=white)](https://github.com/nearbycoder/AlibiAndCo/releases)
[![Cases: 3, proven airtight](https://img.shields.io/badge/cases-3%2C%20proven%20airtight-8e2b2b)](#tech-highlights)
[![Blender 4.5](https://img.shields.io/badge/art-Blender%204.5-e87d0d?logo=blender&logoColor=white)](#rebuilding-the-generated-assets)
[![Audio: synthesized](https://img.shields.io/badge/audio-100%25%20synthesized-c9a24a)](#rebuilding-the-generated-assets)

[**Download for Linux**](https://github.com/nearbycoder/AlibiAndCo/releases/latest) ·
[Watch the trailer](docs/media/alibi-and-co-trailer.mp4) ·
[How to play](#how-to-play) ·
[Build from source](#build-from-source)

</div>

## Trailer

[![Watch the feature trailer (1:50, H.264 MP4)](docs/media/trailer-poster.jpg)](docs/media/alibi-and-co-trailer.mp4)

*Click the poster to open the trailer (1:50, 1080p, sound on). It's also attached to the
[v0.1.0 release](https://github.com/nearbycoder/AlibiAndCo/releases/tag/v0.1.0) as a direct download.*

## About

Wrenhaven, a harbour town in autumn 1986. You're the "& Co." at retired Detective Inspector
Connie Alibi's two-desk agency. Three small crimes, three nights, one cork board.

Every receipt, phone log, ticket stub, press photo and witness statement says that **someone was
somewhere at a certain time**. Pin them onto each suspect's line and the board measures the walk
between them on a real town map. Blue ribbons mean there was time. A red ribbon means a story
has just become physically impossible, and somebody, or some clock, is wrong.

Confront the liars. Link two records of the same moment to catch a clock running slow. Watch
every card that clock stamped slide to its true time, and an alibi that looked perfect a minute
ago splits open with a minute to spare. Then drag the incident card into the only line it fits.

**The board does the arithmetic. You do the doubting.**

![Case 2 mid-investigation: the café clock has been corrected and Marlow's lock has broken open](docs/media/screenshot-alibi-breaks.jpg)

## How to play

| Input | Action |
|---|---|
| **Drag** a card from the tray onto the board (or click it) | Pin it to its person's line at its printed time |
| **Hover** a card | Lift it, read it in full, and see the walk to and from it on the town map |
| **Hover** the town map | Zoom in on Wrenhaven |
| **Drop a card onto another card** | **Link**: "these are the same moment, seen on two clocks" |
| **Click** a pinned statement | Open its panel: **Confront** the witness, or send it back |
| **Right-click** a pinned card | Send it back to the tray |
| **Drag the incident card** | Preview where the crime fits; drop it on a line to accuse |
| **Tab** (or *Notes*) | The notebook: where the case stands, what each clock does, every memo and reply |
| **H** or **F1** | Ask Connie for a hint (the case is marked "with Connie's help") |
| **Space** or click | Skip a memo or the reconstruction |
| **Esc** | Pause: resume, restart, case files, settings, quit |
| **F11** / **F12** | Toggle fullscreen / save a screenshot |

It's played with a mouse and keyboard. There's no gamepad or touch support yet.

### The rules

- **Paper beats people.** Records (receipts, logs, tickets, photos) are never false, but their
  clocks can be wrong. Statements can be false, and a lie isn't proof of guilt.
- **Confront** a statement that's in a contradiction. If it's false, the witness admits it and
  the card is stamped FALSE (or MISTAKEN). If it's true, they stand firm and you lose a badge.
- **Link** two cards that describe the same moment. If one clock is trusted, the other clock's
  error is found, and every card stamped by it slides to its true time. A wrong link costs a badge.
- **Unknown-person cards** (a cash receipt, a figure in a photo) show candidate faces. Each face
  is crossed out when that person's paper trail rules them out. When one is left, the card flies
  to their line.
- Each suspect shows a lock: **COVERED** if they couldn't have reached the scene for long enough
  inside the incident window, **OPEN** if they could. The incident can only be pinned when the
  tray is empty, nothing is contradicting, and it fits exactly one line, so a wrong accusation is
  impossible by construction.
- Each case is rated with three badges (one lost per wrong confrontation or link) and a timer.

## Features

**A timeline you build with your hands.** Cards lift, tilt with the drag and snap onto the cork
with a pin. Each one lands on its person's line at its printed time, and the gaps between them
fill with walking-time ribbons measured on the town map.

![Hovering the bar tab in case 1: two stories in the red, routes drawn on the map](docs/media/screenshot-board-ribbons.jpg)

**Clocks that lie.** A pub clock, a café till, a press camera's date-back: every card names the
clock that timed it, and some of those clocks are wrong. Link a record to one you can trust (the
telephone exchange, the BBC, the church bells, the Electricity Board) and every card on the bad
clock glides to its true time. That's how one receipt moving five minutes can sink an alibi.

**Confront, and be wrong.** Liars crack, the mistaken correct themselves, and the truthful stand
firm and cost you a badge. Being lied to isn't the same as finding the culprit.

**Identity by elimination.** Who's the figure in the oilskin? Unknown-person cards cross out
candidate faces live as the records rule people out.

![Case 3: an unknown figure in a press photo, with candidate faces being crossed out](docs/media/screenshot-unknown-faces.jpg)

**Connie, the notebook and hints.** Short typed memos from your mentor teach each idea the first
time it comes up. The notebook (Tab) keeps every question, witness reply, clock and alibi status.
Hints point at the next step, never the answer.

**The accusation and the reconstruction.** Drag the incident across the board: it refuses the
covered lines and drops into the one with a hole in it. A brass pawn then walks the culprit's
route across the map while the night replays, the CASE CLOSED stamp lands, and the *Wrenhaven
Gazette* prints the front page.

**Restrained detective audio.** Brushed-drum noir jazz, rain on the window, a ticking desk clock,
typewriter keys for memos, a paper-and-pin foley set, and a glass-and-piano hit when a lock
breaks. All of it is synthesized from code.

**Settings that matter.** Master, music and effects volume; resolution; text size (Normal,
Large, Larger) for menus, the HUD, the notebook, the hover card, the chips pinned on the board,
the board's labels and the memo slips (windows under 900 pixels tall start at Large); fullscreen;
reduced motion; and an optional case timer. Progress and settings save automatically.

**Controls when you need them.** A single line on the board's frame shows the gesture that matters
right now (pin, confront, link, accuse), and each tip retires once you've used it. The full
controls list is in the pause menu.

## Content

Three handcrafted cases on one shared town map. Each introduces one idea and is proven airtight
by the solver (exactly one consistent answer, and no way to accuse the wrong person).

| Case | Night | Suspects | What it teaches |
|---|---|---|---|
| **1. Sugar and Spite** | Friday 10 October 1986 | 3 | Pinning, ribbons, contradictions, confronting, two-person statements, the incident pin. About 5 minutes. |
| **2. The Regatta Cup** | Saturday 18 October 1986 | 3 + Town | Clocks and links: a calibration can clear one person and sink another. About 10 minutes. |
| **3. The Last Light** | Saturday 1 November 1986 | 4 + Town | Unknown-person cards, camera date-back clocks and mistaken identity. About 15 minutes. |

Cases unlock in order, and the case files keep your best rating and time for each.

## Screenshots

| | |
|---|---|
| ![Title screen: the polaroid wall](docs/media/screenshot-title.jpg) | ![Case file 1, typed up, with the Gazette's front page](docs/media/screenshot-case-file.jpg) |
| ![The town map zoomed in, with walking times](docs/media/screenshot-town-map.jpg) | ![The notebook in case 2](docs/media/screenshot-notebook.jpg) |
| ![Case 3 in progress: four suspects, a town lane and one of Connie's memos](docs/media/screenshot-late-game.jpg) | ![The case files with all three cases closed](docs/media/screenshot-case-files.jpg) |

## Play it

1. Download `AlibiAndCo-v0.1.0-linux-x86_64.zip` from the
   [latest release](https://github.com/nearbycoder/AlibiAndCo/releases/latest).
2. Unzip it anywhere and run `./AlibiAndCo.x86_64`.

It needs 64-bit Linux and a GPU with Vulkan or OpenGL 4.5 support. On a Wayland desktop, if the
window doesn't appear, start it with `./AlibiAndCo.x86_64 -force-wayland` (Unity's native Wayland
backend). Progress and settings are stored in `~/.config/unity3d/AlibiAndCo/`.

There are no Windows or macOS builds yet, but nothing in the project is Linux-specific, so you can
build one from source.

## Build from source

**Requirements:** Unity **6000.6.2f1** (Unity 6.6) with the Universal Render Pipeline, which is
set up by the project. To regenerate assets you also need Blender **4.5** and Python 3 with
`Tools/requirements.txt` (NumPy, Pillow, SciPy), plus FFmpeg for recordings and the trailer.

```sh
git clone https://github.com/nearbycoder/AlibiAndCo.git && cd AlibiAndCo
Tools/unity.sh build-linux      # batch build to Builds/Linux/AlibiAndCo.x86_64 (validates the cases first)
Tools/play.sh                   # run the build at 1920x1080 (native Wayland when available)
Tools/unity.sh                  # or open the project in the Editor
```

The scripts expect the Editor at `~/Unity/Hub/Editor/6000.6.2f1/Editor/Unity`. Set `UNITY=/path/to/Unity`
to use another location. On distributions that only ship `libxml2.so.16`, the Editor needs
`libxml2.so.2`: install your distro's legacy libxml2 package (for example `libxml2-legacy`), or
put a copy in `~/.local/share/ptt-unity-libs/`, which `Tools/unity.sh` adds to the loader path.

### Tests and validators

| Command | What it does |
|---|---|
| `Tools/validate.sh [--verbose]` | Compiles the game's own `Assets/Scripts/Logic/` into a console app and proves every case airtight. It uses a system `dotnet` or the .NET 8 SDK inside the Unity Editor. `--verbose` walks through each solution. |
| `Tools/unity.sh validate` / `Tools/unity.sh test` | The same validator inside Unity, and the EditMode tests in `Assets/Tests/EditMode`. |
| `Tools/autoplay.sh [outdir]` | Launches the built game, plays all three cases through the real session code with the solver's moves, saves a screenshot per step and prints PASS/FAIL. |
| `Tools/play.sh -alibiInputTest [outdir]` | Drives case 1 with simulated mouse input (drag, hover, right-click, Confront, the incident drag) and checks every gesture lands. |
| `Tools/record.sh [out.mp4] [cases]` | Records the game playing itself at a locked 30 fps and rebuilds the soundtrack offline from a per-frame voice log. |

Automated runs use a blank in-memory save, so they never touch your progress.

### Rebuilding the generated assets

Every model, portrait, press photo and the town map is generated by scripts in `ArtSource/` and
exported into `Assets/Resources/`. The `.blend` files next to them are the generators' saved
scenes (zstd-compressed). The suspects are built from fused ellipsoids, voxel-remeshed and
smoothed into one surface. The town map is built from `Assets/Resources/Data/town.json`, so its
streets match the game's walking times exactly.

```sh
blender -b -P ArtSource/build_assets.py -- props [--only lamp,mug] [--preview]
blender -b -P ArtSource/build_assets.py -- cards [--preview]       # card silhouettes per evidence kind
blender -b -P ArtSource/build_assets.py -- portraits [--preview]
blender -b -P ArtSource/build_assets.py -- map
blender -b -P ArtSource/build_assets.py -- photos
blender -b -P ArtSource/build_assets.py -- all

python3 -m venv .venv && .venv/bin/pip install -r Tools/requirements.txt
.venv/bin/python Tools/photo_finish.py portraits   # 1980s print look for portraits ...
.venv/bin/python Tools/photo_finish.py photos      # ... and press photos
.venv/bin/python Tools/map_finish.py               # paper finish for the town map
.venv/bin/python Tools/make_textures.py            # every texture and icon, procedurally
.venv/bin/python Tools/newspaper.py                # each case's Gazette front page
.venv/bin/python Tools/make_icon.py                # the app icon
.venv/bin/python Tools/synth_audio.py              # all music, ambience and SFX (deterministic)
.venv/bin/python Tools/audio_qa.py                 # loudness, peaks, DC offset, clipping, loop seams
```

### Rebuilding the trailer and README media

```sh
Tools/capture_trailer.sh                       # the game plays itself in trailer mode -> Captures/trailer/
.venv/bin/python Tools/make_trailer.py         # -> docs/media/alibi-and-co-trailer.mp4, trailer-poster.jpg, teaser.webp
```

The capture plays all three cases with scripted mouse and keyboard input, stages the beats the
solver never plays (a witness standing firm, a wrong link, a hint, the notebook, the settings),
logs a frame-numbered marker per beat and saves cursor-free stills. `make_trailer.py` cuts the
trailer from those markers, so every shot is a named beat rather than a timestamp.

## Project structure

```
Assets/Scripts/Logic/   pure C# board logic (no UnityEngine), shared by the game and the validator
Assets/Scripts/Game/    the Unity side: stage, board, cards, drag, map, memos, reconstruction,
                        autoplay, showcase/recorder
Assets/Scripts/UI/      runtime-built uGUI screens: title, case files, intro, notebook, pause, settings, closed
Assets/Scripts/Audio/   music crossfades, ducking, the SFX bank
Assets/Resources/       Data (case and town JSON), Models, Portraits, Photos, Audio, Textures, Icons, Fonts
Assets/Editor/          build script and project setup (materials, fonts, URP, scene)
Assets/Tests/EditMode/  validator and solution-replay tests
ArtSource/              Blender generator scripts and their saved .blend scenes
Tools/                  build, play, autoplay, record, validate, asset, audio and trailer scripts
docs/PLAN.md            the full design and technical plan (spoilers: it walks through every solution)
docs/media/             README and trailer media
```

## Tech highlights

- **One logic core, two hosts.** Everything that decides the game (`Board`, `CaseData`,
  `TownMap`, the solver) is plain C# in `Assets/Scripts/Logic/`. Unity renders it, and a .NET
  console app compiles the same files to check it, so the game and the validator can't disagree.
- **Proven-airtight cases.** `CaseValidator` searches every reachable board state (cards
  unlocked, clocks corrected, statements struck) using only legal moves. It checks that each
  case has exactly one consistent solution, that the solved state is reachable from every state,
  that every designed contradiction can be discovered, that unknown cards only ever resolve to the
  right person, and that no state allows a wrong accusation.
- **Clocks as data.** Every card names the clock that timed it, and each clock has a hidden
  offset. A link between two cards that share an event calibrates the untrusted clock, and the
  board recomputes every card's true time and walking feasibility (all-pairs shortest paths
  over the town graph) in one pass.
- **Recordings at a locked frame rate.** `VideoRecorder` pipes raw frames to FFmpeg while the
  game clock steps exactly one frame at a time, and `AudioDirector` logs what every voice plays
  per frame. `Tools/mix_recording.py` rebuilds the soundtrack offline, as a full mix or as
  separate effects and music stems, so captures are smooth even on a busy machine.
- **A procedural pipeline end to end.** Blender scripts model and render the props, portraits,
  press photos and the map. Python and NumPy synthesize the score (Karplus-Strong bass, FM Rhodes,
  vibraphone, brushes) and the foley, with loudness-matched cues and seamless loops. Pillow
  finishes the photos and sets the newspapers.

## Credits

Design, code, writing, art, music and sound: made for this project with Unity, Blender and
Python. See [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) for details.

- **Fonts:** Abril Fatface, Playfair Display, IBM Plex Sans Condensed, Courier Prime and Caveat
  (SIL Open Font License 1.1); Special Elite (Apache License 2.0); DejaVu Sans (Bitstream Vera /
  DejaVu licence); Liberation Sans (OFL, via TextMesh Pro). Licence texts are in
  `Assets/Fonts/Licenses/`.
- **Engine:** Unity 6 with URP, the Input System, uGUI and TextMesh Pro.
- **Tooling:** Blender 4.5, Python (NumPy, SciPy, Pillow), FFmpeg, .NET 8.

There are no stock assets, samples or third-party models in the project.

## Status and known issues

*Alibi & Co.* is a complete, small game: three cases, start to finish, at version **0.1.0**.

- **Linux only** for now. The release has a Linux x86_64 build, with no Windows, macOS or web
  build yet.
- **Mouse and keyboard only.** There's no gamepad or touch support.
- **The mix was balanced by measurement**, not by ear: loudness per clip, peaks and loop seams
  were checked with `Tools/audio_qa.py`, not on speakers or headphones.
- **Small screens are tight.** At 1280×720, a chip's time is about 10 pixels tall at Large and
  Larger, but its place and source lines are only 6–7 pixels in the busiest case. Chips can't grow
  past the height of their lane. Hover a chip to read the full, scaled card, or open the notebook.
  Large panels shrink back to fit the screen at the larger sizes.
- **Window backends:** on some Wayland desktops the default X11/XWayland path can hang at
  startup. Use `-force-wayland` (as `Tools/play.sh` does).
- The build scripts assume Unity Hub's default install path and are tested on Linux only.
- **No licence has been chosen yet.** Until one is added, all rights are reserved by the author.
