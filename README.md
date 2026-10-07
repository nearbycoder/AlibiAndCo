<div align="center">

<img src="docs/media/teaser.webp" alt="Alibi &amp; Co. gameplay: a card is pinned and its ribbon turns red, a lie is stamped FALSE, and an alibi breaks open" width="100%">

# Alibi & Co.

**Pin the evidence to the timeline. Then find the alibi that can't be true.**

A cozy-noir deduction game about physically assembling a timeline, and then breaking it.

[![Unity 6000.6](https://img.shields.io/badge/Unity-6000.6.2f1%20·%20URP-222?logo=unity&logoColor=white)](https://unity.com/releases/editor/archive)
[![Platform: Linux](https://img.shields.io/badge/platform-Linux%20x86__64-2f5d8a?logo=linux&logoColor=white)](https://github.com/nearbycoder/AlibiAndCo/releases)
[![Cases: 5 + a daily docket, proven airtight](https://img.shields.io/badge/cases-5%20%2B%20a%20daily%20docket%2C%20proven%20airtight-8e2b2b)](#tech-highlights)
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

It's played with a mouse and keyboard, a gamepad, or the keyboard alone:

| Gamepad | Action |
|---|---|
| **Left stick** (D-pad for fine steps) | Move the cursor |
| **LB / RB** | Jump to the previous / next card (or button, in menus) |
| **A** | Click: pin a card, open a statement, press a button. **Hold A and steer** to drag |
| **B** | Send a pinned card back to the tray; back or close in menus |
| **X** / **Y** | Ask Connie for a hint / the notebook |
| **Start** | Pause and resume |

Or with the keyboard alone:

| Keyboard | Action |
|---|---|
| **Arrow keys** | Move the cursor (slow at first, faster the longer they're held) |
| **Q / E** | Jump to the previous / next card (or button, in menus) |
| **Enter** | Click: pin a card, open a statement, press a button. **Hold Enter and steer** with the arrows to drag |
| **Backspace** | Send a pinned card back to the tray; back or close in menus |
| **Tab**, **H**, **Space**, **Esc** | The notebook, a hint, skip a memo, pause (as above) |

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

**Readable without red.** Every card in a contradiction also wears a dark warning triangle on its
corner, and the locks say COVERED or OPEN with different icons, so the board can be read by
colour-blind players too (checked with protanopia, deuteranopia and tritanopia simulations).

**Confront, and be wrong.** Liars crack, the mistaken correct themselves, and the truthful stand
firm and cost you a badge. Being lied to isn't the same as finding the culprit.

**Identity by elimination.** Who's the figure in the oilskin? Unknown-person cards cross out
candidate faces live as the records rule people out.

![Case 3: an unknown figure in a press photo, with candidate faces being crossed out](docs/media/screenshot-unknown-faces.jpg)

**Connie, the notebook and hints.** Short typed memos from your mentor teach each idea the first
time it comes up. The notebook (Tab) keeps every question, witness reply, clock and alibi status.
Hints point at the next step, never the answer. When an honest witness stands firm and costs you a
badge, Connie tells you what made their story red (usually a clock nobody has checked yet, by name).

**The accusation and the reconstruction.** Drag the incident across the board: it refuses the
covered lines and drops into the one with a hole in it. A brass pawn then walks the culprit's
route across the map while the night replays, the CASE CLOSED stamp lands, and the *Wrenhaven
Gazette* prints the front page.

**Restrained detective audio.** Brushed-drum noir jazz, rain on the window, a ticking desk clock,
typewriter keys for memos, a paper-and-pin foley set, and a glass-and-piano hit when a lock
breaks. All of it is synthesized from code.

**A new docket every day.** Once the second case is closed, the case files offer the *Daily
Docket*: a short generated case for the day, with three of the town's regulars, a small crime and
three stories. It's built from the town map and its people, and the same validator that checks the
handwritten cases proves each one airtight before it's offered. On some days a wrong clock puts an
honest story in the red. The last seven days stay in the docket drawer, so a missed day can still be
played for a week, and the case files keep each day's best result. **Copy result** puts a
spoiler-free line on the clipboard to send a friend (the day, the crime, the stars, the time and the
seals).

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

**The Daily Docket** is a sixth, endless file: a three-suspect case generated for each calendar day
(about 3 minutes, case 1's size), on a rota of fifteen small crimes around Wrenhaven. About a third
of days add a wrong clock and an honest witness in the red. It opens when case 2 is closed, and the
last seven days' dockets stay in the drawer.

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
| `Tools/validate.sh --docket N [yyyy-MM-dd]` / `--docket-show yyyy-MM-dd` | Generates and proves N consecutive Daily Dockets (and reports how many variations the worst day needed), or prints one day's docket in full with its solution. |
| `Tools/validate.sh --docket-phrases N [yyyy-MM-dd]` | Lists the sentences that turn up on more than a quarter of N consecutive dockets: the template showing through. |
| `Tools/unity.sh validate` / `Tools/unity.sh test` | The same validator inside Unity, and the EditMode tests in `Assets/Tests/EditMode`. |
| `Tools/autoplay.sh [outdir]` | Launches the built game, plays every case and five Daily Dockets (today's, three fixed days, and one from earlier in the week opened through the docket drawer) through the real session code with the solver's moves. It confronts an honest witness on purpose in case 4 and on a clock day, to check they stand firm and that Connie names the clock to blame. It checks every contradiction carries its marker, saves a screenshot per step (to `Captures/autoplay` by default) and prints PASS/FAIL. |
| `Tools/play.sh -alibiInputTest [outdir]` | Drives case 1 with simulated mouse input (drag, hover, right-click, Confront, the incident drag) and checks every gesture lands. |
| `Tools/play.sh -alibiPadTest [outdir]` | Plays case 1 to the end with a simulated gamepad only (stick, LB/RB jumps, A to pin and drag, B, X, Y, Start), then steers through the case files and the docket drawer to an earlier day's board, and prints PASS/FAIL. The mouse input test also goes on to the drawer. |
| `Tools/play.sh -alibiKeysTest [outdir]` | The same with simulated key presses only (arrows, Q/E, Enter, Backspace, Tab, H, Esc). |
| `Tools/play.sh [-alibiClockAt yyyy-MM-ddTHH:mm:ss] -alibiMidnightTest [outdir]` | Leaves today's docket in progress, opens the docket drawer and waits for midnight (within 20 minutes; `-alibiClockAt` starts the game's clock at a chosen moment), then checks the drawer redrew itself for the new day and that Continue resumes yesterday's docket. |
| `Tools/play.sh -alibiClipboardCheck -alibiShareCheck [outdir]` | Solves today's docket, clicks Copy result and holds the line on the system clipboard for 8 seconds, so a script can read it from outside. Without `-alibiClipboardCheck`, automated runs never touch the system clipboard. |
| `XDG_CONFIG_HOME=<scratch> Tools/play.sh -alibiSaveCheck [outdir]` | Loads the save the way a normal launch does (falling back to the backup if the main file is unreadable), logs what came back, captures the title and case files, saves once and quits. It refuses to run against the real save folder. |
| `node Tools/webtest.mjs [--engine chromium,firefox,webkit] [--only autoplay,pad,keys,reload]` | Serves `Builds/WebGL/` locally and, in headless browsers, runs autoplay (`?autoplay`, then reads back the copied docket result), the pad and keyboard tests (`?padtest`, `?keystest`) and a reload check that the save persists (`?savecheck`), logging load time, frame rate and console errors. It needs `playwright-core` and/or `puppeteer-core` from elsewhere (see the script's header); they aren't dependencies of this repo. |
| `Tools/record.sh [out.mp4] [cases]` | Records the game playing itself at a locked 30 fps and rebuilds the soundtrack offline from a per-frame voice log. |

Automated runs use a blank in-memory save, so they never touch your progress.

Saves are crash-safe: each one is written to a temporary file and renamed into place, and the
previous save is kept as `alibi_save.json.bak`. If the save can't be read (say, after a crash or a
full disk), the game loads the backup and moves the broken file aside as
`alibi_save.unreadable-<date>.json` rather than overwriting it.

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
- **Generated cases, proven at runtime.** The Daily Docket writes ordinary case JSON from a
  date-seeded generator of its own (SplitMix64, so a date gives the same case in Unity, the browser
  and .NET), and the game runs the full validator on it before offering it, trying the next
  variation if it fails. Ten years of dates (3,653 days) all come out airtight; the worst day needs
  6 of the 40 variations allowed, and the slowest takes about a tenth of a second. The words are
  drawn from a second generator, so rewording a docket never changes its puzzle.
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

*Alibi & Co.* is a complete, small game: five cases, start to finish, plus a generated Daily
Docket. The released version is **0.1.0** (three cases). Cases 4 and 5, the Docket and the other
changes since then aren't released yet.

- **Linux only** for now. The release has a Linux x86_64 build, with no Windows, macOS or web
  build yet. macOS and browser builds can be made from source (below), but neither is published.
- **The browser build was tested in headless Chromium and Firefox**, on the dev machine's Radeon
  8060S (`node Tools/webtest.mjs`, last run in improvement round 5). In both, autoplay plays all five
  cases and the Daily Dockets (one opened from the docket drawer) to CASE CLOSED with no console
  errors, the simulated-gamepad and keyboard-only tests pass, a real mouse click on Copy result puts
  the line on the page's clipboard, and progress survives a page reload. It's 26.5 MB and loads from
  localhost in 2–5 seconds at 30–60 fps on a busy machine (under 2 seconds at 60 fps on a quiet one,
  in round 4). It hasn't been tried
  in Safari: Playwright's WebKit build needs Ubuntu libraries this machine doesn't have. It also
  hasn't been tried on a phone (touch isn't supported) or with a person watching, and its sound
  wasn't checked. There's no Quit button or resolution picker in the browser, where the page sets
  the size.
- **The macOS build is untested on a Mac.** It builds on Linux as a Universal app with the bundle id
  `com.nearbycoder.alibiandco` and Unity's ad-hoc signature, and both architectures and the bundle
  layout were checked, but it has never been launched. It isn't notarized, so macOS will block the
  first launch: right-click the app and choose Open, or allow it under System Settings → Privacy &
  Security.
- **The docket drawer was checked by autoplay and by moving a cursor**: autoplay presses its buttons
  in code at 1920×1080, 1280×800 and 1280×720 and in both browsers, and the mouse, pad and keyboard
  tests move their cursor through the case files and the drawer to an earlier day's board at
  1920×1080 and 1280×800 (simulated input, not a person's hands).
- **Gamepad and keyboard-only play were tested with simulated input only.** Scripted tests
  (`-alibiPadTest`, `-alibiKeysTest`) play case 1 to the end and open a docket from the drawer with
  nothing but stick and button events, or key presses, in the Linux build and in the browser. It hasn't been tried with a physical controller or on a Steam Deck, and there's no touch
  support. The layout was checked at the Deck's 1280×800.
- **The Daily Docket is short and formulaic by design.** Each one is about case 1's size (two or
  three moves): one false alibi that hides the culprit, one lie that turns out innocent, one honest
  story, and on about a third of days a wrong clock. The text is assembled from hand-written
  pieces with several variants each, so no sentence turns up on more than about a third of days,
  but a regular player will still recognise the shape. Every docket is proven airtight before it's
  offered. The generated stories have had two read-throughs of ten days each during development,
  not a playtest. The day follows the computer's local date and moves on at midnight, even with
  the case files or the drawer on screen (tested across a real midnight, 6–7 October 2026, and with
  the clock started just before the ends of October and December). A docket stays playable from the
  drawer for seven days.
- **Copy result** puts a docket's result on the clipboard as one line (no names, no moves). In
  automated runs the game read the line back from Unity's clipboard and headless Chromium read it
  from the page's, but on this Linux desktop a *simulated* click didn't reach the desktop clipboard
  (KDE's Klipper never saw it, likely because Wayland only lets a real input event set the
  clipboard). A real click on Linux, and the browsers' clipboard prompts for a real person, haven't
  been tried.
- **Colour-blind players were simulated, not consulted.** Contradictions carry a dark warning
  triangle as well as the red glow, and the locks differ by icon and word. That was checked on
  protanopia, deuteranopia and tritanopia simulations of the board, not with colour-blind players.
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
