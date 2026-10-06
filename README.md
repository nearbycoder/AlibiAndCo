<div align="center">

<img src="docs/media/teaser.webp" alt="Alibi &amp; Co. gameplay: a card is pinned and its ribbon turns red, a lie is stamped FALSE, and an alibi breaks open" width="100%">

# Alibi & Co.

**Pin the evidence to the timeline. Then find the alibi that can't be true.**

A cozy-noir deduction game about physically assembling a timeline, and then breaking it.

[![Unity 6000.6](https://img.shields.io/badge/Unity-6000.6.2f1%20·%20URP-222?logo=unity&logoColor=white)](https://unity.com/releases/editor/archive)
[![Platform: Linux](https://img.shields.io/badge/platform-Linux%20x86__64-2f5d8a?logo=linux&logoColor=white)](https://github.com/nearbycoder/AlibiAndCo/releases)
[![Cases: 5, proven airtight](https://img.shields.io/badge/cases-5%2C%20proven%20airtight-8e2b2b)](#tech-highlights)
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
Connie Alibi's two-desk agency. Five small crimes, five nights, one cork board.

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

It's played with a mouse and keyboard, or a gamepad:

| Gamepad | Action |
|---|---|
| **Left stick** (D-pad for fine steps) | Move the cursor |
| **LB / RB** | Jump to the previous / next card (or button, in menus) |
| **A** | Click: pin a card, open a statement, press a button. **Hold A and steer** to drag |
| **B** | Send a pinned card back to the tray; back or close in menus |
| **X** / **Y** | Ask Connie for a hint / the notebook |
| **Start** | Pause and resume |

Touching the mouse hands control straight back. There's no touch support.

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
- Each case is rated with three badges (one lost per wrong confrontation or link) and a timer,
  and awards up to three seals: **Clean** (no badge lost), **Unaided** (no hint) and **Swift**
  (under the case's par time). The case files keep the best of each, so a solved case still has
  something to replay for.

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

Five handcrafted cases on one shared town map. The first three each introduce one idea, and the
last two combine them in new ways. Every case is proven airtight by the solver (exactly one consistent
answer, and no way to accuse the wrong person).

| Case | Night | Suspects | What it teaches |
|---|---|---|---|
| **1. Sugar and Spite** | Friday 10 October 1986 | 3 | Pinning, ribbons, contradictions, confronting, two-person statements, the incident pin. About 5 minutes. |
| **2. The Regatta Cup** | Saturday 18 October 1986 | 3 + Town | Clocks and links: a calibration can clear one person and sink another. About 10 minutes. |
| **3. The Last Light** | Saturday 1 November 1986 | 4 + Town | Unknown-person cards, camera date-back clocks and mistaken identity. About 15 minutes. |
| **4. Remember, Remember** | Wednesday 5 November 1986 | 4 + Town | Returning faces. The crime itself was timed by a wrong clock, an honest witness turns red until it's fixed, and a nameless entry in a door book has to be traced. About 15–20 minutes. |
| **5. The Wrenhaven Lily** | Saturday 13 December 1986 | 3 + Town | A chain of clocks. One clock can't be checked against anything reliable, only against another wrong clock once that one is mended, and that clock timed the crime. About 15–20 minutes. |

Cases unlock in order, and the case files keep your best rating, time and seals for each.

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

Archives made with `Tools/package.sh` (see below) also include `AlibiAndCo.sh`, which adds
`-force-wayland` by itself on a Wayland desktop. The v0.1.0 zip predates it.

A browser build also works (`Tools/unity.sh build-webgl`, about 27 MB compressed). Serve
`Builds/WebGL/` over HTTP (for example `python3 -m http.server` inside it) and open `index.html`.
It isn't hosted anywhere yet.

There are no published Windows or macOS builds yet. A macOS build can be made from source on Linux
(`Tools/unity.sh build-mac`), but it hasn't been run on a Mac. Windows needs Unity's Windows Build
Support module.

## Build from source

**Requirements:** Unity **6000.6.2f1** (Unity 6.6) with the Universal Render Pipeline, which is
set up by the project. To regenerate assets you also need Blender **4.5** and Python 3 with
`Tools/requirements.txt` (NumPy, Pillow, SciPy), plus FFmpeg for recordings and the trailer.

```sh
git clone https://github.com/nearbycoder/AlibiAndCo.git && cd AlibiAndCo
Tools/unity.sh build-linux      # batch build to Builds/Linux/AlibiAndCo.x86_64 (validates the cases first)
Tools/play.sh                   # run the build at 1920x1080 (native Wayland when available)
Tools/unity.sh                  # or open the project in the Editor

Tools/unity.sh build-mac        # Builds/macOS/AlibiAndCo.app: Universal (Intel + Apple Silicon), Mono
Tools/unity.sh build-windows    # Builds/Windows/AlibiAndCo.exe (needs Windows Build Support installed)
Tools/unity.sh build-webgl      # Builds/WebGL: browser build (Assets/WebGLTemplates/Alibi page)
Tools/package.sh [--no-build] [version] [linux] [mac] [windows] [webgl]
                                # build, then zip into Builds/Release/ with SHA256SUMS (nothing is uploaded)
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
| `Tools/autoplay.sh [outdir]` | Launches the built game, plays every case through the real session code with the solver's moves (and, in the finale, confronts the honest witness on purpose to check she stands firm), saves a screenshot per step and prints PASS/FAIL. |
| `Tools/play.sh -alibiInputTest [outdir]` | Drives case 1 with simulated mouse input (drag, hover, right-click, Confront, the incident drag) and checks every gesture lands. |
| `Tools/play.sh -alibiPadTest [outdir]` | Plays case 1 to the end with a simulated gamepad only (stick, LB/RB jumps, A to pin and drag, B, X, Y, Start) and prints PASS/FAIL. |
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

The capture plays the first three cases (the trailer's cut) with scripted mouse and keyboard input, stages the beats the
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
  right person, and that no state allows a wrong accusation. It also replays every solution with
  the cards pinned in 60 random orders, because players pin one at a time and an identity, once
  confirmed, is permanent. And it lists the case's traps: true statements that can turn red, where
  confronting costs a badge and the real fix is elsewhere.
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

*Alibi & Co.* is a complete, small game: five cases, start to finish. The released version is
**0.1.0** (three cases). Cases 4 and 5 and the other changes since then aren't released yet.

- **Linux only** for now. The release has a Linux x86_64 build, with no Windows, macOS or web
  build yet. macOS and browser builds can be made from source (below), but neither is published.
- **The browser build was tested in headless Chrome only.** On the dev machine's Radeon 8060S it
  loads cold from localhost in about 6 seconds and holds 60 fps on a full board at 1920×993.
  Progress survives a page reload. It hasn't been tried in Firefox or Safari or on a phone (touch
  isn't supported), and its sound wasn't checked. There's no Quit button or resolution picker in
  the browser, where the page sets the size.
- **The macOS build is untested on a Mac.** It builds on Linux as a Universal app with the bundle id
  `com.nearbycoder.alibiandco` and Unity's ad-hoc signature, and both architectures and the bundle
  layout were checked, but it has never been launched. It isn't notarized, so macOS will block the
  first launch: right-click the app and choose Open, or allow it under System Settings → Privacy &
  Security.
- **Gamepad support was tested with a simulated pad only.** A scripted test (`-alibiPadTest`)
  plays case 1 to the end with nothing but stick and button events. It hasn't been tried with a
  physical controller or on a Steam Deck, and there's no touch support.
- **The mix was balanced by measurement**, not by ear: loudness per clip, peaks and loop seams
  were checked with `Tools/audio_qa.py`, not on speakers or headphones.
- **Small screens are tight.** At 1280×720 (which starts at Large), the smallest chip text in the
  busiest cases (four suspects) is about 11 pixels per em, against about 14 in the three-suspect
  cases. Autoplay logs these figures as `[Legibility]`. That's readable but small. Hover a chip to
  read the full, scaled card, or open the notebook. Large panels shrink back to fit the screen at
  the larger sizes.
- **Window backends:** on some Wayland desktops the default X11/XWayland path can hang at
  startup. Use `-force-wayland` (as `Tools/play.sh` and the packaged `AlibiAndCo.sh` do; set
  `ALIBI_X11=1` to make the launcher skip it). The native Wayland backend isn't perfect either:
  in about a dozen automated runs on the development machine it crashed once (a segfault inside
  Unity's Wayland event dispatch, between cases). Progress is saved after every action.
- The build scripts assume Unity Hub's default install path and are tested on Linux only.
- **No licence has been chosen yet.** Until one is added, all rights are reserved by the author.
