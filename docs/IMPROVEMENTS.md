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
