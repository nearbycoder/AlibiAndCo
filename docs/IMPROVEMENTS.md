# Alibi & Co. — Improvement plan

Written 6 October 2026 on the `improvements` branch, after a baseline run of every automated check
and a look at captures at 1920×1080 and 1280×720. Spoilers for the cases are kept to what the
README already says.

## Baseline (6 Oct 2026, commit a9d1db9)

| Check | Result |
|---|---|
| `Tools/validate.sh` (.NET, shared logic) | **Pass.** All three cases are airtight. Reachable states: case 1 has 8, case 2 has 5, case 3 has 12. Shortest solutions are 3, 4 and 5 moves. |
| `Tools/unity.sh test` (EditMode) | **Pass.** 10/10 tests. |
| `Tools/unity.sh build-linux` | **Pass.** 133 MB, 0 errors. The build in `Builds/` was older than the last commits, so it was rebuilt. |
| `Tools/autoplay.sh` at 1920×1080 | **Pass.** 3/3 cases with 3 badges each, 0 exceptions, 33 screenshots, about 3 minutes. |
| Autoplay capture at 1280×720 | **Pass.** 3/3 cases (screenshots were reviewed for readability; see item 1). |
| `Tools/play.sh -alibiInputTest` | **Pass.** Drag, hover, right-click, click, the UI button and the incident drag all land. |
| Player log | Clean: no exceptions and no game warnings. |

Not checked: the audio by ear (no speakers in this loop), XWayland startup without
`-force-wayland` (deliberately avoided because it can hang), and anything on macOS or Windows.

### What I saw in the running game

- **The core loop works and looks finished.** Pin, red ribbon, confront, link, slide and the
  incident drag all read clearly at 1080p. The juice list from PLAN.md is in place.
- **Readability falls apart below 1080p.** At 1280×720, case 3's chips become about 8 px text.
  Bertram Cole's lane squeezes three chips into a few minutes of timeline: the 22:14 and 22:36
  chips shrink to about 5 px text and overlap the 22:15 chip. The "Clocks in this case" header note
  and the memo slip are 6–9 px. Text size (Normal/Large/Larger) only affects the screen-space UI
  and the hover inspector (`Settings.TextScale`); it doesn't touch chips, memo slips or board
  labels. 1280×800 (Steam Deck) and 1366×768 laptops are in this range.
- **The controls strip covers the desk.** The two-line control hint at the bottom of the board
  HUD (`Screens.cs:423`) sits on top of the tray. In the dealt state, it covers the printed times
  of the tray cards (20:52, 21:10, 21:12) and runs over the newspaper. It never goes away, even in
  case 3.
- **Depth is thin, and the cases are linear.** The solver finds only 5–12 reachable states per
  case. In case 1, every statement that turns red is a lie, so a player can't make a mistake
  there. Badges can only be lost in cases 2 and 3. After about 30 minutes, the game is over, and
  replaying a solved case is the same walk.
- **Platform reach doesn't match the blog.** The blog lists Windows · macOS · Linux, but the
  v0.1.0 release has only `AlibiAndCo-v0.1.0-linux-x86_64.zip`. This machine has Linux, Mac and
  WebGL build support but not Windows. Standalone builds use the Mono backend, so a Mac build can
  be made from Linux. Nothing in the code is platform-specific (`grep` finds no
  `UNITY_STANDALONE`/`Application.platform` branches).
- **The Wayland workaround isn't shipped.** The player's only switch to Wayland is the
  `-force-wayland` flag (I checked `UnityPlayer.so` for an editor setting or other switch). The
  zip makes users type the flag by hand, and `Tools/play.sh` isn't part of the zip.

## Ranked improvements

Impact is for a real player. Effort: S is under half a day, M is about a day, L is several days.
Risk covers regressions, unverifiable results or design uncertainty.

| # | Improvement | Impact | Effort | Risk |
|---|---|---|---|---|
| 1 | **Readability pass**: chips, memo slips and board labels follow the text size and stay legible at 720p; the controls strip becomes contextual and stops covering the tray | High: accessibility, laptops, Deck-size screens, and the README's known gap | M | Medium: dense lanes in case 3 need re-layout |
| 2 | **Case 4, a finale** that combines clocks, identity and a red herring (a true statement in the red that a link clears, not a confrontation), using the returning cast, and proven airtight | High: about 30% more content and the first case where reflex-confronting costs you | L | Medium: writing quality and an airtight design. The validator removes the logic risk, not the fun risk |
| 3 | **Release packaging and macOS build**: `build-mac`, `Tools/package.sh` (versioned zips plus checksums) and a Linux launcher that uses Wayland when available | High: puts the blog's macOS claim in reach and fixes the startup hang for Linux users | S–M | Low to build. The macOS runtime **can't be verified here** (no Mac). The app would be unsigned, so Gatekeeper asks users to right-click and choose Open |
| 4 | **WebGL build (go/no-go spike)** | High if it holds: a 5-minute-case deduction game is ideal for the browser, and the mouse-only input already suits it | M | Medium–High: URP post effects (SSAO) on WebGL2, download size (52 MB of WAV sources compressed to Vorbis), the audio-unlock click, and fullscreen, resolution and quit settings that don't apply on the web |
| 5 | **Windows build** | High reach | S once the module is installed | **Blocked**: needs Windows Build Support installed by the owner |
| 6 | **Per-case goals on the case files** (no hints, par time, no badge lost) so a solved case has something to replay for | Medium | S–M | Low |
| 7 | **Gamepad and Steam Deck**: a virtual cursor, bumpers to cycle cards, face buttons for Confront, Hint and Notes, and button prompts | Medium (only Deck and couch players) | M–L | Medium: drag-and-drop under a virtual cursor needs real tuning |
| 8 | **Daily Docket**: generated mini-cases, checked by the same validator (PLAN §16) | Potentially high replay | L+ | High: generated cases risk reading as arithmetic, not stories |
| 9 | **Audio checked by ear** (mix, rain against music, the lock-break peak) | Medium | S | Needs a human listener. Owner task |
| 10 | **Small polish**: the last "NEW EVIDENCE" memo stays on the desk behind the case-closed panel, and the case-files row is a fixed 1560 px with each folder stretched to fit, so a fourth folder would lose about a quarter of its width | Low | S | Low |

## Round 1 scope

I'd implement items 1–4 in that order. Item 4 is a go/no-go spike: if the browser build doesn't
hold up, it's dropped with a written reason, not forced. Items 5–10 wait for this round to land or
for an owner decision.

### 1. Readability pass

- Chips, memo slips, the board header ("Clocks in this case", lane labels and the ruler) and the
  lock captions scale with Text size. At Large and Larger, the board lays out with fewer, larger
  chips per row (or stacks overlapping chips in two rows) instead of shrinking them.
- Chips get a minimum on-screen text height: chips don't shrink below it when a lane is crowded.
  Chips that would collide stagger vertically instead.
- The controls strip shows only what's relevant now (for example "Drag a card onto the board"
  until the first pin, then "click a statement in the red to confront" once there's a
  contradiction). It sits on a dark band above the desk edge, and it fades out after the player
  has done each action once. It comes back from the pause menu or with F1.

**Acceptance:** at 1280×720 and 1920×1080, in every autoplay screenshot, no chip text is below
about 10 px cap height (Normal size), no two chips overlap, and nothing covers a tray card's
printed time. At Larger, chips and memo slips are visibly larger, not just the inspector. The
README's "Text size doesn't enlarge everything" note is removed or rewritten to match.
**Verify:** `Tools/autoplay.sh` at 1920×1080 and 1280×720, plus `-alibiTextSize 2`. I'll review
the case 3 "pinned" and "dealt" shots side by side with the baseline, and the input test still
passes.

### 2. Case 4, "a finale"

- A new `case4.json` on the shared town map: about 4 suspects plus TOWN, roughly 20 cards, 15–20
  minutes. It combines a clock correction with an unknown-person card. It also has at least one
  **true** statement that sits in the red until a link clears it, so a reflex confrontation costs
  a badge. That gives the game a third act, not just a fourth tutorial.
- It reuses existing Wrenhaven people where the story allows (the README's cast is already a small
  town). Any new suspect's portrait comes from `ArtSource/portraits.py`, the newspaper comes from
  `Tools/newspaper.py`, and new press photos come from `ArtSource/photos.py`. Nothing is
  hand-made.
- The case files screen fits four folders. The case-3 "Next case" button leads to it.

**Acceptance:** the validator proves case 4 airtight, with a reachable-state count larger than any
existing case. At least one false-path trap is visible to the solver: a true card that's in an
established conflict in some reachable state. The case-3 "next case" flow and the case files show
four cases. The EditMode tests cover case 4.
**Verify:** `Tools/validate.sh --verbose` and `Tools/unity.sh test`. `Tools/autoplay.sh` must
report 4/4 PASS with screenshots. I'll do a manual read-through of the case text, then a
scripted wrong-confront check through the input test (a badge is lost and the witness stands
firm).

### 3. Release packaging and macOS build

- `Tools/unity.sh build-mac` → `Builds/macOS/AlibiAndCo.app` (Universal: Intel and Apple Silicon,
  Mono), validated first like the Linux build.
- `Tools/package.sh <version>` → `Builds/Release/AlibiAndCo-<v>-linux-x86_64.zip` and
  `…-macos-universal.zip` plus `SHA256SUMS`. The Linux zip gains `AlibiAndCo.sh`, which adds
  `-force-wayland` when `WAYLAND_DISPLAY` is set, as `Tools/play.sh` already does.
- The README's "Play it" and "Status" sections get updated honestly. The macOS build is marked
  untested on hardware, with Gatekeeper instructions. The version moves to 0.2.0 only if the owner
  wants a release.

**Acceptance:** both zips build from a clean checkout with one command each. The Linux zip
unpacks and runs through `AlibiAndCo.sh` and passes autoplay. The `.app` bundle structure,
`Info.plist` (bundle id, version, icon) and both architectures (checked with `file`/`lipo -info`
where available) are confirmed.
**Verify:** run the packaged Linux build from `/tmp`, plus a structural check of the macOS bundle.
**Can't be verified here:** whether the Mac build launches and plays. That needs the owner, or
someone else, on a Mac before it's announced.

### 4. WebGL go/no-go spike

- `Tools/unity.sh build-webgl` with a web quality tier (no SSAO, lighter post) and WebGL
  conditionals: hide Quit, Resolution and Fullscreen-by-key, and use a canvas fullscreen button
  instead. Audio starts after the first click.
- Measure the compressed download size, load time and frame rate in a local browser.

**Go if:** the download is under about 60 MB compressed, it loads in under 20 s locally, it holds
about 60 fps on this iGPU in case 3, and autoplay-style checks show no console errors. **No-go:**
document why in this file and leave the code paths out.
**Verify:** serve the build locally and drive it in a browser preview, taking screenshots of the
title, a pinned board and the case-closed panel.

## Round 1 results (6 Oct 2026)

All four items landed on `improvements`. Screenshots are in `docs/media/improvements/`
(`r1-*` readability, `r2-*` case 4, `r4-*` web).

| # | Item | Commit | Verified by | Result |
|---|---|---|---|---|
| 1 | Readability | 1b65ef1 | Autoplay at 1920×1080 and 1280×720, at Normal and at Larger (`-alibiTextSize 2`); input test, which now changes the text size mid-case | Met, with one honest gap (below) |
| 3 | Packaging and macOS | 75bfde0, f91c0d6 | `Tools/package.sh`. The Linux zip was unpacked in /tmp and passed autoplay through `AlibiAndCo.sh`, which picked the Wayland backend. The Mac bundle round-trips byte-identical; `file` shows x86_64 + arm64; both slices are ad-hoc signed; Info.plist has the bundle id and version | Met. **Not run on a Mac** |
| 2 | Case 4 | 563d647 | `Tools/validate.sh`: airtight, 24 reachable states (the old maximum was 12), trap `a_maud`; EditMode 15/15; autoplay 4/4, which confronts Maud on purpose and checks she stands firm and costs a badge | Met |
| 4 | WebGL spike | 0235988 | Headless Chrome on the Radeon 8060S (ANGLE GL-EGL): cold load from localhost about 6 s, 60 fps on a full board at 1920×993 (worst frame 20 ms), progress survives a reload | **Go**. 26 MB, under the 60 MB limit |

What the round found and fixed along the way:

- **A real bug behind the "small chips".** `Transform.Punch` saved whatever scale the card body
  had when it started. A pin landing (punch at 0.3 s) during the 0.44 s chip/full-card swap
  saved an in-between scale and "restored" it, so some chips stayed stuck at about 0.6× or 1.5×.
  The card body now always rests at scale 1.
- **The intro's press cutting** was placed by `GetPreferredValues`, which reports about 13 px at
  that point, so case 3's last line ran into the cutting. That was already true at baseline. The
  cutting is now placed after layout, only when the text clears it.
- **The browser build lost progress on reload** until the page template turned on
  `autoSyncPersistentDataPath`.

Honest gaps:

- **Item 1:** at 1280×720 in the busiest case (case 3), a chip's time is about 10 px tall, but
  its place and source lines are only 6–7 px, even at Larger. Chips can't grow past their lane's
  height. Windows under 900 px tall now start at Large, which is set in code but untested in a
  non-automated run (automated runs force Normal and never touch the player's prefs). The
  acceptance target of "no chip text under about 10 px at 720p" is met for times, not for the
  small lines.
- **Unity's native Wayland backend** segfaulted once in about a dozen automated runs (inside
  `wl_display_dispatch_queue_pending`, between cases). A rerun passed. This is noted in the
  README.
- **Web:** tested only in headless Chrome. Not tested in Firefox, Safari or on phones (touch
  isn't supported), and the sound wasn't checked.

## Needs a decision from the owner

- **Windows builds:** install *Windows Build Support (Mono)* for 6000.6.2f1 in Unity Hub. The entry
  points are already in place (`Tools/unity.sh build-windows`, `Tools/package.sh windows`). The
  build refuses cleanly until the module is there. It's untested.
- **Publishing:** whether a v0.2.0 release (macOS zip, and the web build if it's a go) should be
  cut, and where a web build would live (itch.io, GitHub Pages or the blog). Nothing gets pushed
  or published from this round.
- **macOS signing:** an unsigned and un-notarized app works with right-click → Open. Proper
  signing needs an Apple Developer account.
- **Licence:** the README says none has been chosen. That's fine for "all rights reserved", but
  worth deciding before shipping more platforms.
- **Audio by ear:** the mix still needs one listen on headphones and on speakers.

## Round 2 scope (6 Oct 2026, branch `improvements-2`)

Picked from the ranked list and the gaps round 1 left, in the order I'll build them. The riskiest
item (a fifth case) goes last so the others land regardless. Screenshots go to
`docs/media/improvements/round2/`.

### R2-1. Chip legibility on small screens (round 1's open gap)

Case 3's chips at 1280×720 have about 10 px times but 6–7 px place and source lines. The fix:
first add a measurement, then make the second and third lines larger and darker inside the
chip, and give person lanes more of the board's height (the TOWN lane is taller than its one
row of chips needs).

**Acceptance:** autoplay logs the smallest on-screen em height of any chip's place and source
line per case (a new `[Legibility]` line). At 1280×720 and Large (the default at that height),
case 3's smallest line is at least 25% larger than the baseline measured with the same code
before the change. No chips overlap and nothing else regresses in the 1080p and 720p captures.
**Verify:** autoplay at 1280×720 with `-alibiTextSize 1`, before and after; side-by-side crops.

### R2-2. Case seals: something to replay for (ranked #6), plus the stray memo (#10)

Each case awards three seals: **Clean** (no badge lost), **Unaided** (no hint), and **Swift**
(under the case's par time, a new optional `par` field in the case JSON). The case-closed
panel shows which seals this run earned, and the case files show the best ever. The last
"NEW EVIDENCE" memo no longer lingers behind the case-closed panel.

**Acceptance:** a clean autoplay earns all three seals. A run that asks for a hint doesn't earn
Unaided. Seals persist in the save, and old saves without seals load fine. The validator still
passes (the logic change is only parsing `par`).
**Verify:** autoplay (seals logged per case), a hint-using step in the input test, EditMode tests,
and screenshots of the closed panel and the case files.

### R2-3. Gamepad and Steam Deck controls (ranked #7)

A virtual cursor on the left stick (with acceleration) drives the existing mouse code through a
virtual mouse device, drawn as a software cursor while the pad is in use. A is click and hold
to drag. B sends a card back (on the board) or acts as back/close (in menus). X is a hint, Y the
notebook, Start pauses, and LB/RB jump the cursor to the previous or next card. When the pad is
in use, the controls strip and the pause-menu card show pad buttons. Touching the mouse hands
control back.

**Acceptance:** a new `-alibiPadTest` run drives case 1 from start to CASE CLOSED using only a
simulated gamepad: pins by A-drag, jumps with RB, sends a card back with B, confronts through
the actions panel, uses Start, Y and X, and drags the incident. It reports PASS with 0 errors.
The mouse input test and autoplay still pass. Screenshots show the software cursor and the pad
prompts.
**Verify:** `-alibiPadTest` from the built player, plus the existing suites. **Not verifiable
here:** a physical controller and a real Steam Deck.

### R2-4. Case 5, if the validator can prove it

A new case built on an idea the first four don't use: a **chain of clocks**, where a corrected
clock becomes trusted and can correct another one. It uses existing portraits and generated
assets only.

**Acceptance:** the validator says AIRTIGHT, and the case needs a two-step clock chain (no
solution corrects the second clock directly from a reference). Autoplay 5/5, EditMode tests
updated, case files fit five folders, and the README and PLAN are updated. **If it isn't airtight
by the end of the round, it doesn't ship.**
**Verify:** `Tools/validate.sh --verbose`, EditMode tests, autoplay at 1080p and 720p, and screenshots.

Deferred again: Windows builds (owner installs the module), audio by ear (needs a person),
Daily Docket (too risky for one round), hosting, signing and licences (owner decisions).

## Round 2 results (6 Oct 2026)

All four items landed on `improvements-2`. Screenshots are in `docs/media/improvements/round2/`
(`r2-1-*` legibility, `r2-2-*` seals, `r2-3-*` gamepad, `r2-4-*` case 5).

| # | Item | Commits | Verified by | Result |
|---|---|---|---|---|
| R2-1 | Chip legibility | cb2f706, fd88f80 | A new `[Legibility]` autoplay log: the smallest on-screen em height of every chip line, measured on `main` and after, at 1280×720 / Large | Met. Case 3: place 9.0 → 11.3 px (+26%), source 8.0 → 10.5 px (+31%). Case 4: 9.0 → 10.8, 8.5 → 9.5. No overlaps in the 1080p or 720p captures |
| R2-2 | Case seals and the stray memo | 873f185 | Autoplay asserts every case's seals; the input test asks for a hint and checks Unaided is withheld; EditMode; screenshots | Met |
| R2-3 | Gamepad | 53a1717, 175fa50 | `-alibiPadTest` plays case 1 from the dealt tray to CASE CLOSED with a simulated pad only, at 1920×1080 and 1280×720: 0 errors. Mouse input test and autoplay still pass | Met with a simulated pad. **No physical controller or Steam Deck was tried** |
| R2-4 | Case 5, "The Wrenhaven Lily" | 204135a | Validator: AIRTIGHT, 8 states, trap `n_penrose`, 60 pin orders clean. EditMode 24/24, including a chain-of-clocks test. Autoplay 5/5 at 1080p and 720p | Met. Shipped |

Found along the way:

- **A real identity bug, now fixed (175fa50).** Unknown cards crossed out faces using only the
  records already pinned. A player pinning one card at a time could have a card confirmed,
  permanently, to the wrong person if the deciding record was still in the tray. The validator
  pins everything at once, so it couldn't see this. Cases 1–4 happened to be safe; case 5's first
  draft wasn't (autoplay caught it). Elimination now counts every unlocked record, and the
  validator and EditMode tests replay each solution in 60 random pin orders.
- **The pad test failed once** when the virtual mouse stopped getting input partway through
  (the shared desktop's real pointer and device ordering). `PadCursor` now follows whichever pad
  is in use, and the test ignores the real pointer until the step that checks the hand-back. It
  passed on four later runs. A real-world hitch here is possible and untested.
- Minor: the pin-order EditMode test went in with the case 5 commit, not the fix commit, and
  case 4's epilogue no longer calls itself the last case.

Not done, and why:

- **Windows builds, signing and notarization, hosting the web build, licence, releases and
  tags:** owner decisions, unchanged from round 1.
- **The web build wasn't rebuilt this round.** `Tools/unity.sh build-webgl` still works, but this
  round's changes weren't re-tested in a browser.
- **Audio by ear:** still needs a person.
- **Daily Docket:** still too risky for one round.

## Round 3 scope (6 Oct 2026, branch `improvements-3`)

Baseline on `bab361d`: the validator proves all five cases airtight (60 pin orders each), and
autoplay passes 5/5 at 1920×1080 and at 1280×800 with Large text (Steam Deck size; 16:10 hadn't
been captured before, and its layout holds up). Two things a player would hit stood out:

- **Red is the game's main signal, and it fades for colour-blind players.** In a protanopia or
  deuteranopia simulation (Machado 2009, full severity) of a case 4 board, the red glow that marks
  the cards in a contradiction turns into a faint olive edge, and the OPEN and COVERED locks come
  out the same colour. Only the "TWO PLACES AT ONCE" label still reads clearly. About 1 man in 12
  has some red-green colour blindness.
- **One crash can wipe a player's progress.** The save is written in place with
  `File.WriteAllText`, and a crash during that write (Unity's Wayland backend has segfaulted
  before) leaves a broken file. On the next launch the game can't read it, starts a blank save, and
  overwrites the broken file the first time it saves.

I'll build these items in this order. The riskiest one goes last, so the others land regardless.
Screenshots go to `docs/media/improvements/round3/`.

### R3-1. Crash-safe saves

Saves are written to a temporary file and then moved into place, so a crash leaves either the old
save or the new one, never half of one. The previous good save is kept as `alibi_save.json.bak`.
If the main file can't be read, the game loads the backup, and the unreadable file is moved aside
(`alibi_save.unreadable.json`) instead of being overwritten. The file logic lives in plain C#
next to the board logic so it can be tested outside Unity.

**Acceptance:** EditMode tests cover a normal save and load, a truncated main file (the backup
loads), a missing main file with a backup present, both files unreadable (blank save, nothing
deleted), and a stale temporary file left by a crash. A real player run, pointed at a throwaway
config folder that holds a truncated save and a good backup, comes up with the backup's progress
and logs the recovery. The real save under `~/.config/unity3d` is byte-identical before and
after the round.
**Verify:** `Tools/unity.sh test`, the throwaway-folder run with a screenshot of the case files,
and checksums of the real save files.

### R3-2. Contradictions you can see without colour

Every card in a contradiction also gets a shape cue that doesn't rely on red: a warning tab on
the chip (the same triangle as the "TWO PLACES AT ONCE" label) and a heavier, dashed outline.
The cue is still there with Reduced motion on, when the glow's pulse stops. The lock captions
get distinct icons and weights, not just a red or green tint.

**Acceptance:** in protanopia, deuteranopia and tritanopia simulations of the autoplay
screenshots (case 2 after the pin, case 4 pinned, case 5 pinned), every card in a contradiction
is marked and no other card is. That's checked by eye on the simulated images and logged by
autoplay (a new `[Conflicts]` line listing the marked chips against the board's established
conflicts). Autoplay, the input test and the pad test still pass.
**Verify:** autoplay at 1080p and 1280×800, CVD simulations before and after, side-by-side crops.

### R3-3. The browser build, rebuilt and played to the end in two engines

Round 2 didn't rebuild the web build. I'll rebuild it, and add a way to run autoplay inside the
browser: the page passes `?autoplay` (and `?padtest`) to the player as its command-line flags,
which only switches on the self-tests and changes nothing else. A small Playwright script
(`Tools/webtest.mjs`) serves `Builds/WebGL/` locally, plays every case in headless Chromium and in
WebKit (Safari's engine), and records the load time, console errors and screenshots.

**Acceptance:** the build is under 60 MB compressed. Autoplay reports 5/5 PASS with 0 errors in
Chromium, and also in WebKit if WebKit can run WebGL 2 headless here (if it can't, that's written
down, not glossed over). Progress survives a page reload. The README's browser notes are updated
with what was actually tested.
**Verify:** `Tools/webtest.mjs` output and screenshots. **Still not tested:** Firefox (no
Playwright Firefox build on this machine), real Safari on a Mac, phones, and the sound.

### R3-4. Daily Docket (gated: it ships only if it holds up)

A short generated case for each calendar day: three of the town's regulars, a small crime at one
of the map's places, a few records and statements, one liar who isn't the culprit, and on some
days a wrong clock. The generator is seeded by the date and lives in the logic core, and
**every docket is proven airtight by the same `CaseValidator` before it's offered.** If a seed
doesn't produce an airtight case, the generator moves on to the next variation. The case files get
a Docket folder that keeps the best result per day.

**Acceptance:** for 1,000 consecutive dates, the generator produces a case that the validator
proves airtight (including the 60 pin orders), with no date failing. Autoplay plays today's
docket and three fixed dates to CASE CLOSED. An EditMode test covers the generator. I'll read
ten dockets through and they have to make sense as small stories, not just sums. **If any of
this doesn't hold by the end of the round, the Docket doesn't ship** and this file says why.
**Verify:** a validator mode that sweeps dates (`Tools/validate.sh --docket 1000`), EditMode
tests, autoplay, screenshots and a read-through.

Deferred: Windows builds, signing and notarization, hosting, the licence, releases and tags (owner
decisions); audio by ear and a physical controller or Steam Deck (they need a person and
hardware).

## Round 3 results (6 Oct 2026)

All four items landed on `improvements-3`. Screenshots are in `docs/media/improvements/round3/`
(`r3-1-*` save recovery, `r3-2-*` colour-blind simulations, `r3-3-*` browsers).

| # | Item | Commits | Verified by | Result |
|---|---|---|---|---|
| R3-1 | Crash-safe saves | f0959c4 | 6 new EditMode tests (truncated save, missing save, both unreadable, stale temp file, the JSON check). A real player run (`-alibiSaveCheck`) against a throwaway `XDG_CONFIG_HOME` holding a save cut off mid-write and a good backup: it moved the broken file aside, loaded the backup (cases 1–3 closed, Continue offered for case 4), and the next save kept all three files. The check refuses to run against the real folder (tried) | Met |
| R3-2 | Contradictions you can see without colour | b8ea1f8 | Protanopia, deuteranopia and tritanopia simulations of cases 2, 4 and 5 before and after. A new `[Conflicts]` autoplay check after every step (41 per run) compares the marked chips with the board's established contradictions. Autoplay at 1080p and 1280×800, input test, pad test | Met. The locks already differed by icon and word, so they weren't changed |
| R3-3 | Browser build, played to the end | df1636b | `node Tools/webtest.mjs` on the final build: headless Chromium (Playwright, ANGLE on the Radeon) and the system Firefox 157 (WebDriver BiDi via puppeteer-core) | Met in Chromium and Firefox: autoplay 9/9 (5 cases, 4 dockets), the pad test and the reload check pass with 0 console errors, 60 fps, loads in 1.2–1.6 s, 26.5 MB. **WebKit wasn't run**: its Playwright build needs Ubuntu's ICU 74, flite and libjxl 0.8, which aren't on this machine |
| R3-4 | Daily Docket | f5513b5 | `Tools/validate.sh --docket 1000` and a ten-year sweep (3,653 days, all airtight; worst day 7 of 40 variations; slowest 38 ms). 5 new EditMode tests (60 days, determinism, the crime rota, ids, spoken times). Autoplay plays today's docket and three fixed days (two clock days, one with the trap confronted on purpose) at 1080p, 1280×800 and in both browsers. A read-through of ten days | Met, and shipped. The same date gives the same docket in .NET, the Mono player and the WebAssembly build |

EditMode tests: 35/35 (24 before the round). The validator still proves cases 1–5 airtight with
60 pin orders each.

Found along the way:

- **The contradiction badge never drew.** Every chip in a contradiction was meant to carry a red
  disc with a white warning sign, but the disc never rendered (only a white triangle, invisible on
  cream, poked past the corner). It's been replaced by the dark triangle with a cream rim.
- **The card that caused a contradiction got its border late.** Markers were only set on cards
  that had finished shrinking into a chip, so the card just pinned stayed unmarked until something
  else changed. The new `[Conflicts]` check caught it in cases 1 and 5.
- **The web build logged an error at startup**: `CreatePrimitive(Quad)` asked for a `MeshCollider`
  that WebGL code stripping had removed. `Assets/link.xml` keeps it.
- **The pad test could stall** when the game window was throttled to about 11 fps on the shared
  desktop: one frame of the slowest cursor speed overshot the test's 3-pixel target, so every move
  ran to its 900-frame cap. The tolerance now scales with the frame time, and screenshots log
  their frame count. It now passes in 74 s at that frame rate. This is probably the "virtual mouse
  stopped" flake from round 2.
- **First docket drafts read badly in places:** the same crime on consecutive days, two people's
  paper at the same spot, and clocks up to 29 minutes wrong. Fixed with a fixed 13-day crime rota,
  distinct places per story and smaller clock errors (at most about 17 minutes).

Not done, and why:

- **Keeping Unity's `TestResults.xml` out of `~/.config/unity3d`.** The EditMode test runner
  writes a copy of its results next to the game's save, as it did in earlier rounds. Pointing
  `XDG_CONFIG_HOME` elsewhere breaks licensing whenever no licensing client is already running (exit
  code 198), so that change was reverted (2dfe4c0). The real `alibi_save.json` and `prefs` were
  byte-identical before and after the round.
- **Safari/WebKit, phones, a physical controller, a Steam Deck, the sound by ear and colour-blind
  players**: they need hardware, system packages or people this loop doesn't have.
- **Windows builds, signing and notarization, hosting the web build, the licence, releases and
  tags**: owner decisions, unchanged.

Owner decisions this round adds:

- **WebKit testing** needs Ubuntu's ICU 74, flite and libjxl 0.8 (or a container) installed outside
  the repo. Alternatively, test on a Mac in Safari.
- **The Daily Docket in a release**: whether to announce it with the next release (the trailer
  doesn't show it), and whether opening it only after case 2 is the right gate.

## Round 4 scope (6 Oct 2026, branch `improvements-4`)

Baseline on `91203dc`: the validator proves cases 1–5 airtight (60 pin orders each) and the Linux
build is clean (134 MB). The ranked list above is nearly used up: what's left needs the owner
(Windows, signing, hosting, the licence) or a person (audio by ear, a real controller). So this
round looks at what a player meets **after** the five cases. That's the Daily Docket, which is
the game's replay hook, plus the moment the game most often feels unfair: losing a badge.

Reading eight days of dockets in a row (`--docket-show`, 6–13 Oct) showed the problems:

- **The same words every day.** The culprit's confession is always "Fine. I left X at T. I went
  for a walk. Needed some air. That's not a crime.", and their second statement always ends "Then
  I walked about a bit. That's the truth." The liar's always starts "…All right. I wasn't at X." A
  daily player sees the joins by day three.
- **Props that don't fit the place.** On a clock day, the note that times the wrong clock is
  always an "Order pad note… written on the back of the order pad", even at the pier turnstile, the
  Grand's lobby or the Odeon box office.
- **A missed day is gone.** The case files only offer today's docket. Skip a day and that case
  can never be played. The day is also fixed when the game starts, so a game left open past
  midnight keeps offering yesterday's.
- **A badge lost and no reason why.** Confronting an honest witness costs a badge, and the only
  feedback is their firm reply ("I've told you…"). The game never says what made an honest story
  turn red, though that's exactly the lesson of cases 2, 4 and 5 and of every clock day.

I'll build these in this order. Screenshots go to `docs/media/improvements/round4/`.

### R4-1. Connie explains a firm stand

When a confrontation costs a badge, Connie follows the witness's reply with a one-line memo on
what made that true story red, worked out from the board (plain C# in the logic core, so it's
testable): the card against it was timed by a clock nobody has checked yet (and she names the
clock), or it's one story against another, or the conflict involves a guess about an unknown
person. It explains the mistake you've just paid for. It doesn't count as a hint, so the Unaided
seal is kept.

**Acceptance:** an EditMode test covers each kind of explanation, using case 4's trap (Maud,
Town Hall clock), case 5's and a clock-day docket. Autoplay's two deliberate traps (case 4 and the
7 October docket) check that a Connie memo follows the firm reply and names the right clock, and
that Unaided is still earned. Autoplay, the input test and the pad test still pass.
**Verify:** `Tools/unity.sh test`, autoplay, and a screenshot of the memo.

### R4-2. The docket in other words

A writing pass on the generator. Each part of a docket gets several hand-written variants instead
of one: the culprit's confession and second statement, the liar's confession, the opening line of
each story, what each place is for, the clock note (each place with a clock gets its own prop: a
turnstile tally card, a porter's message pad, a box-office float slip…), and Connie's memos. Two
crimes are added at places without one yet (the bus depot and the cliff path), so the rota goes
from 13 to 15 days. The puzzle logic doesn't change.

**Acceptance:** a new validator mode (`--docket-phrases N`) counts, over N consecutive days, the
sentences that turn up on more than a quarter of them. Over 28 days that count drops by at least
half from the baseline. Every clock note's prop matches its place. A ten-year sweep (3,653 days)
still comes out all airtight, with the worst day within the 40 variations allowed. The EditMode
docket tests pass. I'll read ten new dockets through.
**Verify:** `Tools/validate.sh --docket-phrases 28` before and after, `--docket 3653`, EditMode
tests, a read-through, and autoplay on four dockets.

### R4-3. The docket week

The Daily Docket button opens a small drawer of the last seven days, today first. Each row shows
the date, the day's crime and your result (stars and time, IN PROGRESS, or not yet played). Any of
them can be opened, so a missed day can still be played for a week. The day is checked again
whenever the case files open, so the docket rolls over at midnight. Older dockets in the save
still load and Continue still works. A docket's case-closed panel offers "Back to the dockets".

**Acceptance:** autoplay opens the drawer through its button, screenshots it, opens a day three
days back from it, plays it to CASE CLOSED, and checks that the drawer then shows that day's
result. A test for the week's date list covers month and year boundaries. Autoplay, input and pad
tests still pass at 1920×1080, and the drawer is checked at 1280×720 and 1280×800.
**Verify:** autoplay, EditMode tests, screenshots at three sizes.

### R4-4. The browser build, checked again

Rebuild the web build with this round's changes and run `Tools/webtest.mjs` in Chromium and
Firefox (autoplay, the pad test, the reload check).

**Acceptance:** 0 console errors, every check PASS, size still under 60 MB compressed.
**Verify:** `node Tools/webtest.mjs --engine chromium,firefox`. WebKit stays untested (system
libraries, owner's call).

Not in this round: keyboard-only play (useful, but not something the current audience is
missing), a share-your-result line (the browser clipboard needs a JavaScript plugin and can't be
checked headless), and the owner and hardware items listed under round 3.

## Round 4 results (6 Oct 2026)

All four items landed on `improvements-4`. Screenshots are in `docs/media/improvements/round4/`
(`r4-1-*` Connie on a firm stand, `r4-2-*` a clock day's new prop, `r4-3-*` the docket drawer at
1080p and 720p).

| # | Item | Commits | Verified by | Result |
|---|---|---|---|---|
| R4-1 | Connie explains a firm stand | 5eb6af0 | 5 new EditMode tests (the traps of cases 2, 4 and 5, every clock day in October 2026, clock names in a sentence). Autoplay checks that the memo after both deliberate traps names the right clock ("the Town Hall clock", "the pier turnstile clock") and that Unaided is still earned | Met |
| R4-2 | The docket in other words | 2c584d5 | `Tools/validate.sh --docket-phrases 28`, before and after, both measured with the final tool: **41 → 14** sentences on more than a quarter of the days (from 7 Oct 2026) and **31 → 11** (from 1 Mar 2027). Before, 15 sentences appeared on every one of the 28 days; now none appears on more than 10. `--docket 3653`: all airtight, worst day 6 of 40 variations. EditMode test for the clock-day props and the intro order. Two read-throughs of 3 and 7 days | Met. The read-throughs fixed six clumsy lines |
| R4-3 | The docket week | 1590c62 | Autoplay opens the drawer through its button, opens the day three days back from its row, plays it to CASE CLOSED and checks the row then shows ★★★ and the time, at 1920×1080, 1280×800 and 1280×720 (Large text). EditMode test for the week across a month, a year and a leap day | Met. Rollover at midnight is in code (the day is re-read whenever the case files open) but wasn't run past a real midnight |
| R4-4 | Browser build, checked again | — (no code) | `node Tools/webtest.mjs` on a fresh build: Chromium (Playwright 1.62, ANGLE on the Radeon) and the system Firefox (puppeteer-core) | Met. Both: autoplay 10/10 with the drawer, pad test and reload check PASS, 0 console errors, 26.5 MB, 60 fps, loads in 1.3–1.7 s |

EditMode tests: 42/42 (35 before the round). Autoplay at 1080p: 10/10 (5 cases, 5 dockets), 0
errors. The input test passed at a load average of about 17 and the pad test at about 25. The real
`alibi_save.json` and `prefs` were byte-identical before and after the round.

Found along the way:

- **The intro always named the culprit first.** "Three regulars were out and about that evening:
  X, Y and Z": X was the culprit every single day. The names now follow the shuffled suspect order.
- **"the The Lantern's bar clock".** Connie's clock hint put "the" in front of a clock name that
  already starts with "The" (case 2), and case 4's incident label could do the same. Clocks now
  have a sentence form (`ClockDef.InSentence`, with an optional `phrase` in the case JSON for case 3's
  "press camera's date-back").
- **`Tools/validate.sh` didn't work on a fresh clone** (5cf28a8). The repo-wide `*.csproj` ignore,
  meant for Unity's generated projects, also caught the validator's own project file. It's tracked
  now.
- **Autoplay screenshots silently failed with a relative output folder**, because the player resolves
  paths from its own folder. `Tools/autoplay.sh` now makes the path absolute and defaults to the
  repo's `Captures/` instead of the shared /tmp (1225c44).
- **Firefox's first run measured 28–49 fps and a 4.8 s cold load** at a load average of 20–35 (other
  sessions on the machine). A rerun at a load average of 5 gave 60 fps and 1.7 s, so that was the
  machine, not the build. Chromium needed the Playwright copy that matches its installed browser
  (1.62 for chromium 1234; 1.63 looks for 1243, which isn't installed).

Not done, and why:

- **Keyboard-only play and a share-your-result line**: left out of scope (see above).
- **The drawer with a real pad or mouse**: autoplay presses its buttons in code; the mouse and pad
  tests still cover case 1 only.
- **The docket's words with players**: two read-throughs during development, not a playtest.
- **Owner and hardware items** (Windows, signing, hosting, the licence, releases, WebKit, a real
  controller or Steam Deck, the sound by ear, colour-blind players): unchanged from round 3.

Owner decisions this round adds:

- **Seven days on file** is my choice of window for the drawer. A longer archive (or every day since
  the docket opened) is a one-line change (`Docket.DaysOnFile`) if you'd rather.
- **The docket's day changed for every date.** Two new crimes and the new wording mean that, for
  example, 7 October's docket is now "The Coastguard's Glasses", not "The Night Till". No release
  has the docket yet, so no player loses anything, but dates that appear in earlier notes or
  screenshots now show different cases.

## Round 5 scope (6 Oct 2026, branch `improvements-5`)

Baseline on `8f7c96b` (main = origin/main). Round 4 left four gaps a player could fall into: the
docket drawer has never been driven by a moving mouse or a pad cursor, the daily rollover has never
run across a midnight, the game can't be played without a pointing device, and a daily puzzle has no
way to tell a friend how it went. Reading the code also showed that the rollover only happens when
the case files are *re-opened*: a player who leaves the case files (or the drawer) on screen past
midnight still sees yesterday marked TODAY.

I'll build these in this order. The midnight item goes first because tonight's real midnight is the
only one in this round. Screenshots go to `docs/media/improvements/round5/`.

### R5-1. The docket at midnight

The case files and the drawer notice the date changing while they're on screen and redraw: the new
day becomes TODAY, yesterday's row says YESTERDAY, the day that fell off the week goes, and an
in-progress docket from yesterday still continues. The local time goes through one seam
(`Cases.Now`), which tests can start at a chosen moment (`-alibiClockAt yyyy-MM-ddTHH:mm:ss`, then
it runs forward in real time). A new self-test, `-alibiMidnightTest`, starts a docket, leaves it in
progress, opens the drawer and waits for midnight.

**Acceptance:** with the clock started at 23:59:40, the test sees the drawer redraw on its own within
a couple of seconds of midnight with the new day first, the old day as YESTERDAY and IN PROGRESS, the
week still seven rows; Continue then resumes yesterday's docket with its pins. Month and year
boundaries are covered (31 Oct, 31 Dec). Once, the same test runs across the **real** midnight of 6–7
October 2026 with no clock override. Autoplay still passes.
**Verify:** `-alibiMidnightTest` logs and screenshots before and after midnight (simulated and real).

### R5-2. The drawer by mouse and pad

The mouse input test and the pad test go on past case 1: to the case files, the Daily Docket button,
the drawer (each row reached and read), Close and B to shut it, and a row three days back opened to
its intro and begun. Whatever doesn't work by hand gets fixed.

**Acceptance:** `-alibiInputTest` and `-alibiPadTest` both reach the drawer by moving the cursor, open
an earlier day's docket from its row and see it begin, with PASS and 0 errors, at 1920×1080 and
1280×800.
**Verify:** both tests, with screenshots of the cursor on the drawer.

### R5-3. Keyboard-only play

The pad's virtual cursor also answers to the keyboard: arrow keys move it (faster the longer they're
held), Q and E jump to the previous and next card (or button), Enter clicks and **held Enter with the
arrows drags**, Backspace sends a card back (and closes menus), with the existing H, Tab, Space and Esc
unchanged. The controls strip and the pause menu's controls list show keys while the keyboard drives.
Touching the mouse hands control back.

**Acceptance:** a new `-alibiKeysTest` plays case 1 from the dealt tray to CASE CLOSED with simulated
key presses only (pins by Enter and by an Enter-held drag, a send-back, a confront, the incident drag),
then opens the drawer and a docket the same way, with PASS and 0 errors. The mouse and pad tests still
pass. Typing elsewhere doesn't move anything (there are no text fields in the game).
**Verify:** `-alibiKeysTest`, the other input suites, screenshots of the key prompts.

### R5-4. Share your docket

A docket's case-closed panel gets a **Copy result** button that puts a spoiler-free line on the
clipboard, for example `Alibi & Co. Daily Docket, Wed 7 Oct 2026: The Coastguard's Glasses ★★★ 2:41
(Clean · Unaided · Swift)`. It names no suspect and no move. In the browser it uses the page's
clipboard (a small `.jslib`); on the desktop, Unity's clipboard.

**Acceptance:** an EditMode test pins the line's format (stars, time, seals, no names). Autoplay presses
the button after a docket and reads the clipboard back; on Linux the text is also read from outside the
game (`wl-paste`). In headless Chromium, `webtest.mjs` reads it back with clipboard permission granted.
If a platform can't be checked, that's written down.
**Verify:** EditMode tests, autoplay log, `wl-paste`, webtest.

### R5-5. The browser build, checked again

Rebuild the web build with this round's changes and run `Tools/webtest.mjs` in Chromium and Firefox.

**Acceptance:** 0 console errors, autoplay, pad and reload checks PASS, size under 60 MB compressed.
**Verify:** `node Tools/webtest.mjs --engine chromium,firefox`.

Not in this round: the drawer's seven-day window and WebKit (owner's call), and the hardware and owner
items listed under round 3.

## Round 5 results (7 Oct 2026)

All five items landed on `improvements-5`. Screenshots are in `docs/media/improvements/round5/`
(`r5-1-*` the real midnight, `r5-2-*` the drawer by mouse and pad, `r5-3-*` keyboard play, `r5-4-*`
Copy result).

| # | Item | Commits | Verified by | Result |
|---|---|---|---|---|
| R5-1 | The docket at midnight | 6b52513 | `-alibiMidnightTest` with the clock started at 23:59:40 on 6 Oct and 23:59:45 on 31 Oct and 31 Dec (the drawer redrew itself within a second of midnight each time, with the new day TODAY, the old one YESTERDAY and IN PROGRESS, the eighth day gone), and once across the **real** midnight of 6–7 October 2026 with no clock override: redrawn 0.4 s after 00:00:00, and Continue resumed 6 October's docket with its two pins | Met |
| R5-2 | The drawer by hand | 5f752e5 | The mouse and pad tests now go on from case 1's closed panel through the case files and the drawer (every row reachable, and by RB with the pad), close it, open the day three days back, go back, and open its board, at 1920×1080 and 1280×800, and in Chromium and Firefox | Met. It turned up the resize bug below |
| R5-3 | Keyboard-only play | b9c8da7 | New `-alibiKeysTest`: case 1 to CASE CLOSED and on to a docket's board with simulated key presses only, at 1920×1080 and 1280×800 and in Chromium and Firefox. Screenshots of the key prompts and the pause menu's key list | Met |
| R5-4 | Share your docket | c4f1330 | EditMode test of the line (format, and 15 days' lines checked against every name and clock in their cases). Autoplay presses Copy result after every docket and checks the line. In headless Chromium and Firefox, `webtest.mjs` clicks the button with a real (trusted) mouse click and reads the page's clipboard back: the exact line in both | Met in the browser. **Not met on the Linux desktop with simulated input**: Unity's clipboard holds the line, but KDE's Klipper never received it (see below) |
| R5-5 | The browser build, checked again | 997a33f | `node Tools/webtest.mjs --engine chromium,firefox` on the final build | Met. Both engines: autoplay 9/9 (5 cases, 4 dockets), pad, keyboard, share and reload checks PASS, 0 console errors, 26.5 MB. Chromium 55–60 fps; Firefox 29–57 fps and loads of 3.3–4.6 s, slower than round 4's 60 fps and 1.7 s at a load of 5, and not re-measured on a quiet machine |

EditMode tests: 43/43 (42 before the round). The validator proves cases 1–5 airtight with 60 pin
orders each, and `--docket 365` from 7 October 2026 is all airtight (worst day 5 of 40 variations).
Autoplay at 1080p: 10/10 (5 cases, 5 dockets), 0 errors, with the share line checked after each
docket. The input, pad and keyboard tests pass at 1920×1080 and 1280×800. Load averages were high
(about 20–40, other sessions) for most of these runs; nothing in this round is timed, and the
browser figures were measured at a load average of about 14–16. The real `alibi_save.json` and `prefs` were
byte-identical before and after the round.

Found along the way:

- **Resizing the window in a case's first second threw an exception** (0d15d43). A new window
  shape rebuilds the board and ends the old session, but Connie's delayed opening memo, and two
  delayed screen shakes, still ran against the destroyed memo desk and stage. The pad test at
  1280×800, whose resolution change lands just after case 1 opens, hit it. They now check the
  session is still alive.
- **Copy result on the Linux desktop.** Unity's Wayland backend (SDL) does publish a clipboard, but
  a click made of simulated Input System events carries no Wayland input serial, which the
  compositor needs before it accepts a new clipboard owner; that's the likely reason Klipper never
  saw the line. The X11 path couldn't be tried: it hung at startup again (the known XWayland hang)
  and had to be killed. So a real click on Linux is untested. Automated runs keep the line to
  themselves unless `-alibiClipboardCheck` is passed, and the one deliberate check put the desktop's
  previous clipboard text back afterwards.
- **Firefox refuses a clipboard write that isn't part of a click.** Autoplay presses Copy result in
  code, so in Firefox the write is refused (Chromium allows it with the permission granted). The
  share run, with a real click, works in both.
- **Today's date moved during the round** (it crossed midnight), so browser autoplay played 4
  dockets instead of 5: 7 October is both "today" and one of autoplay's fixed days.

Not done, and why:

- **A real click on Copy result on Linux, and a person's hands on the drawer, the pad and the
  keyboard**: the tests use simulated input. A physical controller or Steam Deck is still untested.
- **Owner and hardware items** (Windows, signing, hosting, the licence, releases, WebKit, the sound
  by ear, colour-blind players): unchanged from round 3.

Owner decisions this round adds:

- **Keyboard keys.** Q/E jump and Backspace sends back, chosen because nothing used them; Tab stays
  the notebook. Rebinding isn't offered.
- **What the share line says.** It gives the date, the crime's title, stars, time and seals, and
  nothing else. Whether to add a link (to a hosted web build, once there is one) is your call.

## Round 6 scope (7 Oct 2026, branch `improvements-6`)

Baseline on `956cedc` (main = origin/main). Most of the ranked list is done or waiting on the owner,
so this round went back to the board itself and read the input code for ways a real player loses
something they didn't mean to. Three stood out:

- **A slip of the hand costs a badge.** Dropping a dragged card onto *any* other card is a link
  ("these are the same moment"), and a link between two different moments costs a badge. A card
  pins at its printed time wherever it's dropped on its lane, so the natural move is to drop it
  anywhere on the lane, and in the busier cases the lanes are full of chips. Dropping a card back
  on the desk on top of another tray card is a link too. Nothing but a blue glow says a link is
  coming; there's no label. One stray drop also loses the Clean seal.
- **The case timer runs while you're away.** The player is set to keep running in the background
  (`runInBackground`, so automated runs work unfocused), and the timer only stops in the pause
  menu. On the desktop, alt-tabbing to answer a message, or leaving the window minimised, still
  counts towards the Swift seal and the time in the docket's share line.
- **The browser build can't be played on a tablet.** Nothing reads the touchscreen, and a touch
  drag never reaches the mouse code. That matters as soon as the web build is hosted.

I'll build these in this order; touch is the biggest and goes last, so the others land regardless.
Screenshots go to `docs/media/improvements/round6/`.

### R6-1. No badge for a slip of the hand

A dragged card only becomes a link once it has been held over the same card for a moment (about
half a second). Until then a drop does what it would do with no card underneath: pin to the lane
on the board, or go back to the tray on the desk. Once the link is armed, the card shows full size
as now, the target glows as now, and a **LINK · same moment?** tag appears by the cursor, so it's
never a surprise. The rules don't change: a deliberate wrong link still costs a badge.

**Acceptance:** the mouse input test goes on to case 2 and checks, with a moving cursor: a tray card
dropped in one movement onto a pinned chip of a different moment pins to its own lane with no
badge lost and no "NOT THE SAME MOMENT" memo; a tray card dropped quickly onto another tray card
goes back to the tray with no badge lost; and a deliberate link, held over the target, shows the tag
(screenshot) and corrects the clock. The pad and keyboard tests and autoplay still pass.
**Verify:** `-alibiInputTest` at 1920×1080 and 1280×800, the pad and keys tests, autoplay.

### R6-2. The clock stops when you look away

The case timer only runs while the game has focus (desktop) or while its page is visible and
focused (browser). Automated runs keep their timing as now. Nothing else pauses: memos still type
and music still plays, and the pause menu doesn't pop up uninvited.

**Acceptance:** in the browser, with real focus changes (a second page brought to the front, then
back), the case timer doesn't advance while the game's page is in the background, and does once
it's back. A Linux-build self-test (`-alibiFocusTest`) checks the same through the handler Unity
calls on a focus change. The real OS focus signal on the Linux desktop isn't driven (moving focus on
the shared desktop would disturb other sessions); if it can't be checked, that's written down.
**Verify:** `-alibiFocusTest`, a `focus` run in `Tools/webtest.mjs` in Chromium and Firefox.

### R6-3. Touch in the browser (gated: it ships only if it holds up)

A touchscreen drives the same mouse code the pad and keyboard already use: a tap is a click, a
finger drag is a drag (pin, link, the incident), and a press held still on a chip shows the hover
card without clicking. Sending a card back goes through its panel's *Back to the tray*, and the
HUD's Hint, Notes and Menu buttons cover the keys. Touching the mouse, pad or keyboard hands control
back. Phones aren't a target: the board is built for a landscape screen of tablet size or larger.

**Acceptance:** a new `-alibiTouchTest` (a simulated touchscreen, like the pad test) plays case 1
from the dealt tray to CASE CLOSED with touches only: pins by tap and by drag, a press-and-hold
inspector, a confront through the panel, Back to the tray, the HUD buttons and the incident drag,
with PASS and 0 errors, at 1920×1080 and 1280×800. In Chromium, with **real** touch events from the
browser (CDP), taps and a drag reach the game. The mouse, pad and keyboard tests still pass. **If the
browser's touches don't reach the game by the end of the round, it doesn't ship.**
**Verify:** `-alibiTouchTest`, a `touch` run in `Tools/webtest.mjs`, screenshots.

### R6-4. The browser build, checked again (and Firefox on a quieter machine)

Rebuild the web build with this round's changes and run `Tools/webtest.mjs` in Chromium and
Firefox. Round 5 measured Firefox at 29–57 fps under a load of 14–16; measure again at the lowest
load I can find this round and write down the load next to the figures.

**Acceptance:** 0 console errors, every check PASS, size under 60 MB compressed, Firefox frame rate
and load time recorded with the load average.
**Verify:** `node Tools/webtest.mjs --engine chromium,firefox`.

Not in this round: the hardware and owner items listed under round 3 (Windows, signing, hosting,
the licence, releases, WebKit, a real controller or Steam Deck, the sound by ear, colour-blind
players), a real click on Copy result on the Linux desktop (it needs real input on the shared
desktop), and phones.

## Round 6 results (7 Oct 2026)

All four items landed on `improvements-6`. Screenshots are in `docs/media/improvements/round6/`
(`r6-1-*` the LINK tag, `r6-2-*` the timer after a real focus change, `r6-3-*` touch).

| # | Item | Commit | Verified by | Result |
|---|---|---|---|---|
| R6-1 | No badge for a slip of the hand | 088388d | The mouse input test now goes on to case 2 (played in code to its first link), at 1920×1080 and 1280×800: a card dropped in one movement onto a chip of another moment pins to its own lane, a card tossed onto another tray card stays in the tray, neither costs a badge or posts "NOT THE SAME MOMENT", and a card held on its twin shows the LINK tag and corrects the Lantern's clock. Pad, keyboard and autoplay still pass | Met |
| R6-2 | The clock stops when you look away | d6ae2da | `-alibiFocusTest` through Unity's focus handler (away 3.1–3.4 s, timer moved 0.00 s; back 2 s, it moved 2.0–2.1 s). A **real** focus change on Linux, inside a private nested KWin session (`kwin_wayland --virtual` under its own D-Bus and config folder, so the shared desktop wasn't touched): a second window took focus for 5.8 s and the timer moved 0.08 s. Firefox with another tab brought to the front: away 5.2 s, 0.04 s. Chromium with blur and focus events (its headless mode doesn't move focus between pages): 5.0 s, 0.02 s | Met. Not tried on macOS or Windows |
| R6-3 | Touch in the browser | 5605ccb | New `-alibiTouchTest` (simulated touchscreen, case 1 to CASE CLOSED) at 1920×1080 and 1280×800 in the Linux build, and in headless Chromium and Firefox. New `touchreal` webtest run: **real** touch events from the browser (CDP in Chromium, puppeteer's touchscreen over WebDriver BiDi in Firefox) pin a card by tap, pin one by finger drag, read a held chip without opening it, and open the notebook with one tap on Notes. Mouse, pad and keyboard tests still pass | Met, and shipped. **Not tried on a real tablet or phone** |
| R6-4 | Browser build, checked again | — (no code) | `node Tools/webtest.mjs --engine chromium,firefox`, every run (autoplay, pad, keys, share, reload, focus, touch, touchreal) on the final build | Met. All runs PASS in both engines with 0 console errors; 26.5 MB. Chromium: 58–60 fps, loads 3.3–4.0 s. Firefox: 53–60 fps, loads 3.7–4.7 s, at a load average of about 8–12 during its autoplay (8–17 over the whole run). Earlier in the round, at a load that rose from 5 to 29, the previous build gave Firefox 60/60/58/53/60/38 fps and a 1.7 s load |

EditMode tests: 43/43 (unchanged: this round's code is input handling in MonoBehaviours, which the
player self-tests above cover rather than EditMode). The validator proves cases 1–5 airtight with
60 pin orders each (case 2's opening memo changed wording only). Autoplay at 1080p: 9/9 (5 cases,
4 dockets; today is also one of the fixed days), 0 errors. Load averages were noted with every
timed or input-driven run; input runs waited for a load under 24. The real `alibi_save.json` and
`prefs` were byte-identical before and after the round (Unity's test runner rewrote its own
`TestResults.xml` next to them, as in earlier rounds).

Found along the way:

- **The first link check failed because of the test, not the game.** At about 10 fps (load 71 from
  other sessions), the test's eased glide spent half a second over the target chip, which is a hold
  by the game's real-time rule, and the link armed. The quick drop now moves in a quarter of a
  second of real time and lets go on arrival, like a player dropping a card on its lane.
- **Touch lost presses until the hover rule followed the virtual mouse.** "Nothing is hovered once
  the finger lifts" switched off a frame before the board saw the press arrive, so some taps landed
  on nothing. It now follows the virtual mouse's button.
- **Touch timestamps weren't usable for "held still".** The device's last update time moves on after
  the finger lifts, so quick taps read as half-second holds. The hold is now measured as observed,
  and must also span at least four frames, so at a crawling frame rate a tap is still a tap.
- **The UI would have pressed every button twice on a tap**: once from its own touch bindings and once
  from the virtual mouse. The UI input module now only listens to mice and pens.
- **A page error after quitting.** The page's new focus listeners called into the player after the
  self-tests had quit it ("null function"). They now stop once a call fails.
- **The nested KWin session left two helpers running.** Opening a dialog inside it activated a
  desktop portal and `ksecretd` through systemd's user manager, and they outlived the session. I
  stopped those two processes by PID (both carried the session's scratch config folder); nothing in
  the real desktop's settings or wallet changed. The script for that check stayed in the gitignored
  `Logs/`; a rerun should open a Wayland client that doesn't start portals.

Not done, and why:

- **A real tablet or phone, and touch on the desktop builds** (a Linux touchscreen, a Steam Deck's
  screen): no hardware here. The hold time (0.5 s) and the drag threshold were chosen by hand.
- **The link hold (0.45 s) with players**: chosen by hand, not tuned.
- **Focus on macOS and Windows**: no machines here.
- **A real click on Copy result on the Linux desktop**: unchanged from round 5 (it needs real input on
  the shared desktop).
- **Owner and hardware items** (Windows, signing, hosting, the licence, releases, WebKit, a real
  controller or Steam Deck, the sound by ear, colour-blind players): unchanged from round 3.

Owner decisions this round adds:

- **Touch is in, phones aren't.** It's built for a landscape tablet or larger. Whether to advertise
  touch once the web build is hosted, and whether phones are worth a layout of their own, is your
  call.
- **The timer waits, the game doesn't.** Losing focus stops the case timer but doesn't open the pause
  menu or mute the music. Some players would rather the game paused itself; that's a small change if
  you prefer it.
- **The link hold.** A link now needs the card held on the other one for about half a second. A
  deliberate wrong link still costs a badge.

## Round 7 scope (7 Oct 2026, branch `improvements-7`)

Baseline on `898e380` (main = origin/main): the Linux build is clean (134 MB), the validator proves
cases 1–5 airtight with 60 pin orders each, and autoplay passes. The ranked list is used up apart
from owner and hardware items, so this round read the save, focus and menu code for things a player
would trip over, and found three:

- **Opening one case throws away another's board.** The save holds a single in-progress board.
  Leave case 4 half-solved (15–20 minutes of pins, links and confrontations), open today's docket
  from the case files, pin one card, and case 4's board is silently gone: its folder says OPEN
  again, and *Continue* offers the docket. The case intro's *Start over* also wipes a board with no
  question asked (the pause menu's *Restart* does ask).
- **The game works as hard in the background as in front of you.** The desktop build keeps running
  when it loses focus (it has to, for the music and the automated runs) and keeps drawing up to 120
  frames a second of a desk nobody is looking at. That's battery and fan on a laptop or a Deck.
- **The notebook's older pages need a mouse.** The notebook's memo log (every reply and Connie's
  explanations, newest first) only scrolls with a mouse wheel or a drag. Its footer says "scroll for
  older notes" to pad and keyboard players too, who have no wheel.

Plus one smaller thing for replays: the case file never says what the seals ask for. The Swift seal's
par time first appears on the case-closed panel, after the run it applies to.

I'll build these in this order; screenshots go to `docs/media/improvements/round7/`.

### R7-1. Every case keeps its board

Each case and each docket keeps its own board in the save. Leaving one board for another shelves
it; its folder (or drawer row) still says IN PROGRESS, and opening it offers *Continue the board*.
The title's *Continue* resumes the board played most recently. A docket's board is dropped once the
day leaves the drawer. Older saves (one board) load as before. *Start over* on the case intro asks
first, like *Restart this case*.

**Acceptance:** a new `-alibiBoardsTest` self-test leaves case 1 with two pins, case 2 with one, and
today's docket with one, then checks: both folders and the drawer row say IN PROGRESS; *Continue*
resumes the docket; each case reopens with its own pins, badges and timer; *Start over* asks, and
"No" keeps the board. A save in the old format (one board) and one with three boards, in a throwaway
`XDG_CONFIG_HOME`, load with every board after a relaunch (`-alibiSaveCheck`). The real save is
untouched. Autoplay and the input tests still pass.
**Verify:** `-alibiBoardsTest` at 1920×1080, `-alibiSaveCheck` twice in a scratch config folder,
autoplay, the browser's reload check (`webtest.mjs --only reload`).

### R7-2. Rest when you look away

While the game doesn't have focus (desktop) or its page is hidden or unfocused (browser), it draws at
most 10 frames a second instead of up to 120; it goes back to full speed the moment focus returns.
Sound, memos and the (already stopped) case timer are unaffected. Automated runs keep full speed.

**Acceptance:** `-alibiFocusTest` also checks the frame rate drops to about 10 fps while away and
recovers once back. The game process's CPU time, read from `/proc` over 10 s at the title and on a
case-1 board, is measured attended and away, and drops by at least half. Load average noted with
each measurement.
**Verify:** `-alibiFocusTest`, a CPU-time script in `Logs/`, the `focus` run in `webtest.mjs`.

### R7-3. The notebook without a mouse

With the notebook open, the right stick or D-pad (pad), the Up/Down arrows and Page Up/Page Down
(keyboard) scroll the memo log; a finger drag already does. The footer names the controls for
whatever is steering (pad, keys, touch or mouse), and the title screen's footer stops saying "Mouse
or gamepad" only.

**Acceptance:** the pad and keyboard tests open the notebook in case 1 once its log is longer than
the page, scroll to the oldest note and back with only the pad or keys, and check the scroll position
moved both ways; the touch test drags the log. Screenshots of each footer.
**Verify:** `-alibiPadTest`, `-alibiKeysTest`, `-alibiTouchTest`, at 1920×1080 and 1280×800.

### R7-4. Seals on the case file

The case intro lists the three seals, what each asks for (no badge lost, no hint, under the par
time) and which ones the case files already hold, so a replay has a stated goal. Dockets show theirs
too.

**Acceptance:** the intro shows the line for every case and a docket, with par times matching the
case data, at 1920×1080 and 1280×720 (Large text), without overlapping the suspects column in the
four-suspect cases. Autoplay checks the line's text.
**Verify:** autoplay at both sizes, screenshots.

### R7-5. The browser build, checked again

Rebuild the web build with this round's changes and run every `webtest.mjs` check in Chromium and
Firefox, with the load average noted. The save format changed in R7-1, so the reload check matters.

**Acceptance:** 0 console errors, every check PASS, size under 60 MB compressed.
**Verify:** `node Tools/webtest.mjs --engine chromium,firefox`.

Not in this round: the hardware and owner items (Windows, signing, hosting, the licence, releases,
WebKit, a real controller, tablet or Steam Deck, the sound by ear, colour-blind players, focus on
macOS or Windows), a real click on Copy result on the Linux desktop, and the owner's open calls from
round 6 (pausing on focus loss, the link hold, advertising touch).

## Round 7 results (7 Oct 2026)

All five items landed on `improvements-7`. Screenshots are in `docs/media/improvements/round7/`
(`r7-1-*` boards kept, `r7-3-*` the notebook scrolled by pad, keys and touch, `r7-4-*` the seals
line). Unlike round 6, each item's commit was built on its own (the later items stashed) before the
next was staged, and R7-1 to R7-3 were tested on their own builds.

| # | Item | Commit | Verified by | Result |
|---|---|---|---|---|
| R7-1 | Every case keeps its board | 104dee6 | New `-alibiBoardsTest`: case 1 left with 2 pins, case 2 with 1 pin and a badge lost, today's docket with 1 pin; both folders and the drawer row say IN PROGRESS, *Continue* resumes the docket, case 1 reopens with its 2 pins at 4.1 s on its timer, case 2 with its pin and 2 badges, *Start over* asks and "No" keeps the board, "Yes" starts afresh without touching the others, and solving case 2 drops only its board (*Continue* moves to case 1). `-alibiSaveCheck` twice each, in throwaway `XDG_CONFIG_HOME` folders, on a save in the old one-board format and one with three boards: every board came back, and again after the game rewrote the file. | Met. The browser's reload check passes too, but its save holds no boards, so the shelf's round trip was only checked on the desktop |
| R7-2 | Rest when you look away | 7248307, 9f406a9 | `-alibiFocusTest` (Unity's focus handler) now measures frames a second and the CPU time of the game's threads, read from `/proc`: away, 10.0 fps at the title and on a board, and full speed again on return; the case timer still stands still. The browser's focus run: in Chromium (the page's blur and focus events) 60 fps with focus, 10 away, 60 back; in Firefox (another tab in front) 58 fps with focus and about 1 in the background tab | The cap is met. **The CPU saving isn't shown on the desktop build**: on this shared machine the Linux build only reaches about 11 fps even with focus, at load averages from 11 to 30 alike (rounds 5 and 6 logged the same rate, so it's likely the shared desktop's compositor or GPU, not the game), so on a board the game's threads went from 15–18% of a core to 11–14%. The browser shows the drop from 60 to 10 fps; its CPU use can't be read from inside the page. The browser run also showed the timer counting 0.10 s while away, the length of one 10 fps frame, against 0.02 s in round 6; the follow-up commit leaves the frame after focus returns out of the timer, and the rerun measured 0.00 s in both browsers and on Linux |
| R7-3 | The notebook without a mouse | 17e2936 | The pad, keys and touch tests open the notebook after case 1's confrontations (when the notes run past the page) and scroll to the oldest note and back (1.00 → 0.00 → 1.00) with the right stick and D-pad, Down and Page Up, and finger drags, at 1920×1080 and 1280×800; the footer screenshots name each pointer's controls | Met |
| R7-4 | Seals on the case file | 7974fa3 | Autoplay checks the line on every case and docket intro (9 files), with the par time from the case data, clear of the buttons, the press cutting, the suspects and the paper's edge, at 1920×1080 and at 1280×720 with Large text; the boards test checks the widest button rows (a case and a docket in progress) | Met, after a fix: the first placement, under the stamp, ran into case 4's long date line, so the line moved to the foot of the file |
| R7-5 | Browser build, checked again | — (no code) | `node Tools/webtest.mjs --engine chromium,firefox`, every run (autoplay, pad, keys, share, reload, focus, touch, touchreal) on the R7-4 build, then focus, reload and autoplay again on the final build after the timer fix | Met. Every run PASS in both engines with 0 console errors; 26.6 MB. Final build: Chromium autoplay 59–60 fps, loads 3.2–4.0 s; Firefox autoplay 58–60 fps, loads 4.2–4.8 s, at a load average of about 7–17. On the R7-4 build, at about 9–24, Chromium's autoplay dipped to 26–60 fps and Firefox's to 39–46 |

EditMode tests: 43/43 (unchanged: this round's code lives in the Unity side, which the player
self-tests cover). The validator proves cases 1–5 airtight with 60 pin orders each, and
`--docket 365` from 7 October 2026 is all airtight (worst day 5 of 40 variations); no case or logic
file changed. Autoplay: 9/9 (5 cases, 4 dockets), 0 errors, at 1920×1080 and at 1280×720 with Large
text. Mouse input test at 1920×1080 and 1280×800; pad, keys and touch tests at both sizes; the
focus, boards and midnight tests (the clock started at 23:59:30: the drawer redrew 0.9 s after
midnight and *Continue* resumed yesterday's docket with its pins). Every input-driven run waited for
a load average under 24 before it started (it rose to 47–59 during two of them, from other
sessions). The real `alibi_save.json` and `prefs` were byte-identical before and after the round
(Unity's test runner rewrote its own `TestResults.xml` next to them, as in earlier rounds).

Found along the way:

- **Unity's `HIDInput` thread uses about 58% of a core** in every Unity 6.6 player on this machine,
  this game's and the other sessions' alike, attended or not. It's the engine's own input-polling
  thread and the game has no switch for it. The only input device the user account can read directly
  is an 8BitDo Pro 3 receiver, but whether that's the cause is untested (it can't be unplugged from
  here). The focus test now reports it apart from the game's own threads. Noted in the README.
- **Splitting a commit by `-U0` hunks misplaced lines.** The first attempt to stage R7-1 alone put
  two identical-looking lines in each other's place and one insertion in the wrong block. The build
  caught it (it didn't compile), and the hunk tool now renumbers each hunk for the subset it stages.
  The commits that landed were each built on their own.
- **A solved case can still have a board on the shelf.** Replaying a closed case and leaving it
  part-way keeps that replay's board; the folder shows the closed record (as before), and the file
  offers *Continue the board*.

Not done, and why:

- **A measured CPU or battery saving from R7-2**: needs a machine where the game runs well above
  10 fps with focus. The 10 fps background rate was chosen by hand.
- **A real click on Copy result on the Linux desktop, touch on the desktop builds, a real tablet or
  phone, focus on macOS or Windows**: unchanged from round 6 (they need real input on the shared
  desktop, or hardware this machine doesn't have).
- **Owner and hardware items** (Windows, signing, hosting, the licence, releases, WebKit, a real
  controller or Steam Deck, the sound by ear, colour-blind players): unchanged from round 3.

Owner decisions this round adds:

- **The background frame rate.** 10 frames a second while unfocused is my choice; someone watching
  the game on a second screen will see it step. 30, or off, is a one-line change
  (`GameRoot.AwayFrameRate`).
- **How many boards to keep.** Every case and every docket still in the drawer keeps its board, so a
  save holds at most 12. Nothing asks before a board is shelved, because nothing is lost.

## Round 8 scope (7 Oct 2026, branch `improvements-8`)

Baseline on `a1644ff` (main = origin/main): the validator proves cases 1–5 airtight with 60 pin
orders each, and the Linux build is clean (134 MB). Two quick measurements before planning changed
round 7's open questions:

- **The ~11 fps on this machine is the shared desktop, not the game.** Run inside a private nested
  KWin (`kwin_wayland --virtual`, its own D-Bus session and config folder), where the window is
  certainly on screen, `-alibiFocusTest` measured 52 fps on the title and 53 on a case-1 board
  with focus (load average 14). Away, at 10 fps, the game's own threads dropped from 49% to 14% of
  a core on the title and from 64% to 16% on the board. So round 7's background cap does save CPU;
  on the shared desktop the compositor was already throttling the window.
- **The 8BitDo receiver isn't what keeps `HIDInput` busy.** With the player sandboxed so it can't
  open a single input device (`bwrap` with a fresh `/dev` holding only the GPU, sound and shared
  memory), the thread still used 51–56% of a core. Sampling it shows it running or in
  `epoll_wait`, about 115 ticks in 2 s: the engine's own loop. The project already uses the Input
  System only (`activeInputHandler: 1`), so the game has no switch left for it.

The ranked list is used up apart from owner and hardware items, so this round reads the game as a
player meets it. Four things stood out:

- **The pad prompts are Xbox letters for every pad.** The game shows `[A]`, `[B]`, `[LB]`,
  `[Start]` whatever is plugged in. On a PlayStation pad there's no A; on a Switch Pro controller the
  button labelled A is the *east* one, so "press [A]" makes a Switch player press the button that
  sends a card back. The Input System already knows both layouts on Linux, macOS, Windows and (by
  name) in the browser.
- **A hint names cards and leaves you to find them.** Connie's second hint says "“Darts slate” and
  “Exchange log” are the same moment" or "Clem's statement doesn't hold up"; on a four-suspect board
  at 720p that's a hunt across twenty chips, worse with a pad or keyboard cursor.
- **The Daily Docket opens without a word.** Closing case 2 unlocks it, but the closed panel only
  offers the next case; the docket button appears at the foot of the case files the next time
  they're opened. After case 5 the panel says "Five for five" and offers only the case files.
- **Input tests run on a throttled window.** On the shared desktop the player runs at about 11 fps,
  which has stalled or skewed input-driven tests in earlier rounds, and two round 7 test windows
  briefly went fullscreen on the shared desktop.

I'll build these in this order; screenshots go to `docs/media/improvements/round8/`.

### R8-1. Pad prompts that match the pad

Every pad prompt (the controls strip, the pause menu's controls list, the notebook's footer,
Connie's how-to lines) names the buttons of the pad in use: Xbox-style **A B X Y, LB RB, Start**
by default, PlayStation **✕ ○ □ △, L1 R1, Options**, and Nintendo **B A Y X, L R, +** (the Switch
Pro layout reads the bottom button as B, so the prompt names the button the player actually
presses). The family comes from the Input System's layout (DualShock/DualSense, Switch Pro HID)
or, failing that, the device's name (browser gamepads report the vendor and product in a string).

**Acceptance:** `-alibiPadTest` gains `-alibiPadLayout DualShock4GamepadHID` and
`SwitchProControllerHID`: with each, it plays case 1 to CASE CLOSED through that simulated device
and checks the strip, the pause menu and the notebook footer show that family's labels and no Xbox
letters; the default run still shows Xbox letters. An EditMode test covers the name-based detection
(browser strings for DualSense, DualShock 4, a Switch Pro controller, an Xbox pad, unknown).
Screenshots show the PlayStation shapes rendering (not missing-glyph boxes).
**Verify:** the pad test three ways at 1920×1080 and once at 1280×800, EditMode tests, screenshots.
**Not verifiable here:** a physical PlayStation or Switch pad.

### R8-2. Hints point at the cards they name

When Connie's hint names cards, those cards wear a small gold **CONNIE** tag (and the pad and
keyboard jumps, LB/RB and Q/E, start from the first of them) until the board changes or the
next hint. The first hint for a clock marks the cards on the clock it names; the second marks
the two cards to link; a confront hint marks the statement; the incident hint marks the incident
card. It doesn't give away more than the words already did.

**Acceptance:** autoplay asks for a hint at each step of one case and one docket (in a separate
run, since hints withhold Unaided) and checks the tagged cards equal the cards each hint names,
and that the tags clear after the move. Screenshots at 1920×1080 and 1280×720 (Large text).
The input, pad, keys and touch tests still pass.
**Verify:** autoplay with `-alibiHintTour`, the input suites, screenshots.

### R8-3. The docket announces itself

When case 2's closed panel is the one that opens the Daily Docket, it says so in a line above the
buttons ("The Daily Docket is open: a short new case every day, in the case files"). After the
last case, the panel offers **Today's docket** next to *Back to the case files*, and it opens
today's docket file (or its board, if one is in progress).

**Acceptance:** autoplay checks the line on case 2's first close (and not on a replay), and that
case 5's panel has a *Today's docket* button that opens today's docket intro. Screenshots at
1920×1080 and 1280×720 (Large text) with no overlap.
**Verify:** autoplay at both sizes, the boards test, screenshots.

### R8-4. Self-tests on a private desktop

`Tools/nested.sh` runs any `Tools/play.sh` self-test inside a private nested KWin (its own D-Bus
session, config folder and Wayland socket, closed afterwards), so the window is on screen, runs at
its real frame rate, can't go fullscreen on the shared desktop and can't be disturbed by the real
pointer. It refuses to run if `kwin_wayland` is missing, and kills only the PIDs it started.

**Acceptance:** the input, pad, keys, touch and focus tests pass through it at 1920×1080, with
their frame rate and time logged next to the same test on the shared desktop and the load average.
No process it started outlives it (checked by PID after each run). The README's tests table and
the round 7 notes on the frame rate and `HIDInput` are corrected with the measurements above.
**Verify:** each test through `Tools/nested.sh`, a process check after each, the README.

### R8-5. The browser build, checked again

Rebuild the web build and run every `webtest.mjs` check in Chromium and Firefox, with the load
average noted (R8-1 changes how browser pads are named, R8-2 and R8-3 add UI).

**Acceptance:** 0 console errors, every check PASS, size under 60 MB compressed.
**Verify:** `node Tools/webtest.mjs --engine chromium,firefox`.

Not in this round: the hardware and owner items (Windows, signing, hosting, the licence, releases,
WebKit, a real controller, tablet or Steam Deck, the sound by ear, colour-blind players, focus on
macOS or Windows), a real click on Copy result on the Linux desktop, and the owner's open calls
(the background frame rate, pausing on focus loss, the link hold, advertising touch, how many
boards to keep). A docket streak was considered and left for later: the drawer already shows the
week.

## Round 8 results (7 Oct 2026)

Every item landed on `improvements-8`. Screenshots are in `docs/media/improvements/round8/` (`r8-1-*` pad
prompts, `r8-2-*` hint tags, `r8-3-*` the docket announced). `Tools/nested.sh` (R8-4) was built
first, so every input-driven test this round ran inside it.

| # | Item | Commit | Verified by | Result |
|---|---|---|---|---|
| R8-1 | Pad prompts that match the pad | 0457abb | `-alibiPadTest` with `-alibiPadLayout ps` (a simulated `DualShockGamepad`, found by its layout) and `nintendo` (a plain pad described as Nintendo's "Pro Controller", found by its name, as a browser reports one), and the default pad, at 1920×1080, plus PlayStation at 1280×800: each plays case 1 to CASE CLOSED and on to a docket, and checks the controls strip, Connie's how-to tip, the pause menu's controls and the notebook footer against labels written out by hand in the test. 22 new EditMode cases (browser id strings from Chromium and Firefox for DualSense, DualShock 4, Switch Pro and Xbox pads; the one-pass A/B swap) | Met. The PlayStation shapes render through the DejaVu Sans fallback (screenshots). **No physical PlayStation or Switch pad was tried**, so which family a real pad reads as on Linux (its HID layout or the name its driver gives it) is untested |
| R8-2 | Hints point at the cards they name | e663dde | New `-alibiHintTour`: case 4 and the 7 October docket, two hints before every move. The tags matched the named cards at every one of 26 checks and cleared after every move, and the first E after a hint landed on a tagged card, at 1920×1080 and at 1280×720 with Large text. The keys test still passes | Met. At 720p the hover card can cover the top of a tag on the lane below it |
| R8-3 | The docket announces itself | 9cf94ec | Autoplay checks case 2's closing line and case 5's, measures each against the epilogue at its full length (it may still be typing) and the buttons, and presses *Today's docket*, which opened today's file; 9/9 at 1920×1080 and at 1280×720 with Large text | Met. That the line stays off a *replayed* case 2 is in code (`firstClear`) but no run replayed case 2 to its close |
| R8-4 | Self-tests on a private desktop | cefded1, a711869 | The input, pad, keys, touch, focus and boards tests through `Tools/nested.sh` at 1920×1080: 52–56 fps against 11.0–11.5 for the same tests on the shared desktop in round 7, and the focus test on the shared desktop today gave 11.8 fps at a load average of 2.9, so the cap there is the compositor, not the load. No KWin or game process outlived a run (checked after each) | Met. One keys-test run in four failed ("Backspace didn't send the card back"); see below |
| R8-5 | Browser build, checked again | — (no code) | `node Tools/webtest.mjs --engine chromium,firefox`, every run (autoplay, pad, keys, share, reload, focus, touch, touchreal) on a fresh build with R8-1 to R8-3 | Met. Every run PASS in both engines with 0 console errors; 26.6 MB. Chromium autoplay 59–60 fps, Firefox 54–59; loads 1.4–4.8 s, at a load average of about 4–17 |

EditMode tests: 65/65 (43 before the round; the 22 new cases are PadLabels'). The validator proves
cases 1–5 airtight with 60 pin orders each, and `--docket 365` from 7 October 2026 is all airtight
(worst day 5 of 40 variations); no case changed. The final build was then run through the whole
suite inside `Tools/nested.sh`, at a load average of 2–5: the input, pad (Xbox, PlayStation,
Nintendo), keys, touch, focus and boards tests and the hint tour at 1920×1080, the input, keys, touch
and PlayStation pad tests at 1280×800, and autoplay (9/9, 5 cases and 4 dockets): all PASS, 0 errors,
54.6–56.4 fps. Autoplay also passed at 1280×720 with Large text on the R8-3 build. The real
`alibi_save.json` and `prefs` were byte-identical before and after the round (Unity's test runner
rewrote its own `TestResults.xml` next to them, as in earlier rounds).

Found along the way:

- **The ~11 fps every earlier round measured was the shared desktop.** The same build ran at 51–57 fps
  with focus in a nested KWin at any load, and at 11.8 fps on the shared desktop at a load average of
  2.9. So round 7's background cap does save CPU where the window is visible: on a board the game's
  own threads went from 25% of a core to 10% at a load of 3, and from 52–68% to 11–16% at loads of
  10–30. That's CPU time; battery wasn't measured.
- **`HIDInput` isn't the 8BitDo receiver.** Sandboxed (`bwrap`, a fresh `/dev` with only the GPU,
  sound and shared memory) so it couldn't open any input device, the player's `HIDInput` thread still
  used 51–56% of a core at a load of 19. Its share moves with the machine's load (29% at a load of 3,
  35–43% at 12, 51–59% at 14–30), and sampling shows it running or in `epoll_wait`. The project uses
  the Input System only, so the game has no switch left for it.
- **A test-side flake, not chased to its cause.** One keys-test run in four (load about 17) reported
  "Backspace didn't send the card back"; the three reruns and both final runs passed. That step doesn't
  touch anything this round changed. The test now logs the cursor, the card's position and the hover
  state if it happens again.
- **A hint tag's jump check needed the pad test's tolerance.** The cursor lands on the chip, the chip
  lifts as it's hovered, and the cursor is then about 20 px from its centre; the check uses the
  40 px the pad test already allows.

Not done, and why:

- **A physical PlayStation or Switch pad, a Steam Deck**: no hardware here. Which family a real pad
  reads as depends on the layout or name its driver or browser gives it; anything unrecognised gets
  Xbox letters, as before.
- **A replayed case 2 to its close** (the docket line should stay off): in code, not run.
- **A docket streak**: considered and left out; the drawer already shows the week.
- **Owner and hardware items** (Windows, signing, hosting, the licence, releases, WebKit, a real
  tablet, focus on macOS or Windows, a real click on Copy result on Linux, the sound by ear,
  colour-blind players): unchanged.

Owner decisions this round adds:

- **Nintendo pads name the bottom button B.** Pinning is on the bottom face button for every pad, so
  on a Switch controller the prompts say "[B] pins" and "[A] sends a card back", the reverse of
  Nintendo's own habit. Swapping confirm and back for Nintendo pads is a small change if you'd rather.
- **Hints tag cards.** The second hint already named the cards; the tags only show where they are. The
  vague first hint about a statement tags nothing, and the first hint about a clock tags every card on
  that clock. Hints still withhold the Unaided seal.
- **`Tools/nested.sh` needs KDE's KWin.** It's a test tool, not part of the game; on another desktop it
  refuses to run, and the tests still work through `Tools/play.sh`.
