# Alibi & Co. — Game Design & Technical Plan

## 1. One-sentence pitch

Pin receipts, phone logs, photographs and witness statements onto a shared timeline, watch the
walking-time ribbons between them turn red when a story becomes physically impossible, and break
the one alibi that was too perfect.

## 2. Design pillars

1. **The board does the arithmetic, you do the doubting.** The game computes travel time and
   feasibility instantly and visibly. The player decides what goes where, which clock to distrust
   and whom to confront. The game never deduces for you; it only shows you what can't be true.
2. **Paper beats people.** Records (receipts, ticket stubs, exchange logs) are never false, but
   their clocks can be wrong. Testimony can be false, and a false statement isn't proof of guilt.
   Witnesses can be mistaken without being guilty.
3. **Physical and tactile.** Cards lift with a shadow, tilt with the drag, snap onto the cork
   with a pin, and slide along the timeline when a clock is corrected. Everything is an object on
   a lamp-lit desk.
4. **Airtight.** Every case has exactly one consistent solution. Every contradiction can be
   discovered, and a validator proves both before the game ships.
5. **Restraint.** It's cozy noir, not grimdark: muted palette, warm lamp light, soft brushed
   jazz, and short, wry, typed memos from your mentor instead of walls of text.

## 3. Premise and narrative frame

Wrenhaven is a small harbour town in autumn 1986. Retired Detective Inspector **Constance
"Connie" Alibi** runs a two-desk agency called **Alibi & Co.**, and you are the "& Co.", her new
partner. Connie teaches through short typed memos clipped to the board. Over three nights in
October and November the town's small crimes grow larger: a sabotaged cake, a stolen trophy, and
a lighthouse that went dark on a stormy night.

## 4. Core loop

```
Case file opens (incident + first cards)
  └─> PIN cards onto people's lines ──> travel ribbons appear between pins
        ├─ ribbon turns red  ──> a CONTRADICTION ──> a new question (sometimes a new lead card)
        │     ├─ two cards are one moment on two clocks? ──> LINK them (the clock is corrected and
        │     │                                               every card on it slides)
        │     └─ a statement can't be true? ──> CONFRONT (strike it: the witness admits, or stands firm)
        ├─ unknown-person card ──> faces are crossed out as the board rules people out
        └─ every alibi holds? ──> "Someone's cover is false." Find the crack.
  └─> when the incident fits exactly one line, PIN THE INCIDENT there ──> reconstruction ──> case closed
```

One session is one case (5 to 15 minutes). The rating (three badges, timer) rewards clean
deduction, which gives a reason to replay.

## 5. Mechanics in detail

### 5.1 The board
- Horizontal **time axis** for the case's span (e.g. 20:00 to 22:30), with ticks every 5 minutes
  and labels every 15.
- One **lane per person** (suspects plus anyone whose movements matter) with a polaroid portrait
  and a name. Some cases also have a **TOWN lane** for events with no person attached, such as the
  radio news, a power dip, or a photo of the ballroom.
- The **incident card** (red border) sits in the header: crime location, time window, and the
  minutes the job took. A translucent red band shows its window across every lane.

### 5.2 Cards (evidence)
Each card is a claim that **someone was at a place at a time**, either an instant or an interval.

| Field | Meaning |
|---|---|
| kind | receipt, statement, photo, call log, ticket, ledger, note |
| source | `record` (paper) or the person who said it (testimony) |
| subjects | whose lane(s) it goes on. A statement like "I was with Bram" lands on two lanes, joined by red string. Can be **unknown** with a candidate list. |
| location | one of the town's places |
| time / from-to | as **shown** on a particular clock |
| clock | which clock produced the time (exchange, church bells, café till, the Lantern's wall clock, a press camera's date-back…) |

Hidden truth data (used only by the validator and to answer confrontations): whether the card is
true, a lie, or mistaken; the true subject of unknown cards; and the `event` id shared by cards
that describe the same moment.

### 5.3 PIN
Drag a card from the tray onto the board. Known-person cards fly to their own lane and snap to
their printed time (a tray card can be dropped anywhere on the board). Unknown-person cards can be
pinned to any candidate lane as a *hypothesis* (dashed pin). Right-click returns a card to the tray.

### 5.4 Travel ribbons (the overlay)
Between chronologically adjacent pins in a lane, a ribbon starts at the earlier card's end with
length = walking time between the two places (shortest path on the town map). If it ends before
the next pin it's ink-blue, and the leftover slack shows as a dotted tail. If it overshoots, the
overshoot is red and hatched, the pins tremble, and the label reads e.g. *"needs 12 min · has 7"*.
Two intervals that overlap in different places show a red overlap bracket. Hovering a ribbon
highlights the route on the map.

### 5.5 Contradictions
A contradiction is a pair of pinned cards in one lane that can't both be true given walking
times. The first time a designed contradiction appears, it posts a **question** in the notebook
("Ines can't be at the Grand and the Lantern at 21:20. Who's wrong, or whose clock?") and may
deliver a **lead** (a new card).

### 5.6 LINK (clocks)
Drag a card onto another card and hold it there to claim *"these are the same moment."* (The link
arms after about half a second over the same card, when a **LINK · same moment?** tag appears; a
card that only lands on another one on its way to a lane or back to the tray pins or returns as
usual. Added in improvement round 6, so a slip of the hand never costs a badge.) If they share an `event`
and one side's clock is trusted (a reference clock or one already corrected), the other clock's
offset is established. Every card on that clock slides along the timeline to its true time (the
trailer moment). A wrong link costs a badge ("Those aren't the same moment").
Reference clocks: the telephone exchange, the bank's authorisation log, the BBC, the Electricity
Board, the church bells. Some clocks are already known from earlier cases (the Lantern is 10
minutes slow).

### 5.7 CONFRONT (strike)
Click a testimony card that's in a contradiction, then **Confront**. You can only confront with
cards that are pinned to a definite person (not with hypotheses). Records can't be confronted.
- If the statement is false (a lie or a mistake), the witness admits it. The card is stamped
  **FALSE** and greyed out, the witness's reply is typed onto a memo, and follow-up cards may arrive.
- If it's true, the witness **stands firm** and their reply contains a hint ("Twenty past, by the
  clock over the bar, I'm certain"). The card stays and you lose a badge.

### 5.8 Unknown-person cards (identity by elimination)
Cash receipts and background figures in photos show a row of candidate faces. Each face is
crossed out live when that person's **records** (paper beats people) make the card impossible
for them. When exactly one face is left, the card stamps **ONLY <NAME> FITS**, flies to that
lane, and becomes established evidence that can be used to confront. If every face is crossed
out, the card reads *"Fits no one, so something else on the board is wrong"*: usually a clock.

### 5.9 Alibi status and the incident
Each suspect's lane shows a live lock: **COVERED** if, with that lane's current non-struck
evidence, they couldn't have been at the scene for the required minutes inside the window, and
**OPEN · 3 min to spare** otherwise. Dragging the incident card over a lane previews the best
slot and the routes to and from it.

The incident card can be **pinned** (the accusation) only when:
1. every available card is on the board (tray empty),
2. no unknown card is still unconfirmed,
3. there are no contradictions, and
4. the incident fits **exactly one** lane: that one.

Otherwise Connie explains what's missing ("Not yet: the incident also fits Rolf's line"). A wrong
accusation is impossible by construction, which is what the validator proves.

### 5.10 Rating
Three badges per case, minus one per wrong confrontation or wrong link (minimum one). Also
recorded: time and first-try clears. The case select shows the best result.

## 6. Cases (all on one shared town map)

### Wrenhaven map (walking minutes)
Seafront: Boathouse—10—Harbour Café—4—Pier—3—The Lantern—5—Bandstand—4—Grand Hotel—4—Cliff
Path—8—Wren Point Lighthouse. Uphill: Café—6—Fenwick's Hardware—6—Penhallow's Bakery—4—Town
Hall—8—Bus Depot; Lantern—7—Bakery; Town Hall—6—Odeon Cinema—7—Depot; Depot—9—Grand Hotel;
Hardware—8—St Brigid's Church—7—Town Hall; Church—9—Station—10—Glasshouse—11—Boathouse;
Hardware—9—Glasshouse. The game uses all-pairs shortest paths.

### Case 1 — "Sugar and Spite" (tutorial, about 5 min)
*Friday 10 October.* Champion baker Maud Penhallow's Bake-Off showstopper was found tipped onto
the bakery floor. She locked up at 20:30 and came back at 21:15. The vandal needed 5 minutes.
- Lanes: **Agnes Trelawney** (rival baker), **Bram Okafor** (Maud's apprentice), **Clem Hollis**
  (caterer, Bram's chess partner).
- Teaches: PIN, ribbons, contradiction, CONFRONT, two-person statements, the incident pin.
- Beats: Agnes swears she was at the pictures, but her own bar tab puts her in the Lantern. She
  lied to hide the sherry, not a crime, and the landlord then gives her an airtight alibi. Clem
  and Bram cover for each other ("chess at the café till 21:20"), but Clem's bus ticket and Bram's
  hardware receipt break the story. Clem's corrected statement leaves Bram a 4-minute window.
  **Culprit: Bram.**
- Purpose: in about five minutes a player learns that lies aren't guilt, that red means
  impossible, and that the incident goes in the one line it fits.

### Case 2 — "The Regatta Cup" (clocks, about 10 min)
*Saturday 18 October.* The Regatta Cup vanished from the Yacht Club boathouse between 21:30
(cleaner, church bells) and 22:16 (the police log). Picking the cabinet took 4 minutes.
- Lanes: **Marlow Quint** (treasurer), **Ines Delacroix** (rival skipper), **Rolf Abernethy**
  (steward with the key), plus TOWN.
- Teaches: clocks and LINK, and that a calibration can clear one person and sink another.
- Beats: The landlord Sid's statement puts Ines at the Lantern at the same minute the Grand's
  ledger has her drinking brandy. Confronting Sid fails: "by the clock over the bar". The radio
  news (21:00) and Sid's "the news came on at ten to nine by our clock" are one moment, so the
  Lantern runs 10 minutes slow. Linking them clears Ines, closes Rolf's suspicious gap, and puts
  Marlow's claim of "café from twenty to ten" into conflict with his own bar slate. Confronted,
  Marlow shrugs: coffee at 21:52, brandy paid by card at 22:12, and twenty minutes isn't enough
  to get to the boathouse and back. **Everyone is covered.** The bank's log for Rolf's pie (21:05)
  and the café till receipt for it (21:10) are one moment, so the café till runs 5 minutes fast.
  **The coffee receipt slides five minutes earlier**, the waitress's "he never left" now collides
  with the Lantern slate, she admits she was in the back washing up, and Marlow's lock bursts open
  with one minute to spare. **Culprit: Marlow.**

### Case 3 — "The Last Light" (identity, about 15 min)
*Saturday 1 November, gale warning.* Wren Point's lamp was seen burning at 22:05 (coastguard)
and reported dark at 22:36. In the dark the trawler *Merry Wren* went onto the Mallow Rocks.
Nobody died, but the boat was lost. Someone spent at least 6 minutes jamming the lamp's clockwork.
- Lanes: **Nell Garrow** (keeper's daughter, supposed to be minding the lamp), **Elias Garrow**
  (keeper, night off), **Bertram Cole** (fisherman with a grudge, doorman at the Harvest Ball),
  **Capt. Augustus Rook** (owner of the *Merry Wren*, at the ball all night), plus TOWN.
- Teaches: unknown-person cards, camera date-back clocks, mistaken identity, and a twist culprit.
- Beats: Nell's "I minded the lamp all night" puts her at the scene, but the Odeon's ledger says
  otherwise. She lied to hide a date, and her sweetheart and a kiosk receipt cover her. Mrs
  Pengelly saw "Bertram Cole in his yellow oilskin" on the cliff path at a quarter past ten, but
  the exchange has Cole phoning a taxi from the hotel desk a minute earlier. She admits she only
  saw the coat. The press photographer's terrace shot shows the oilskin figure in the background
  (date-back 22:27). Its faces: Cole and Rook remain. The ballroom photo of the lights flickering
  (date-back 22:10) and the Electricity Board's supply dip (21:58) are one moment, so the camera
  runs 12 minutes fast. Every photo slides, Cole's face is crossed out, **only Rook fits**, and
  his "never left the ballroom" (and his wife's echo of it) collapse. **Culprit: the man who
  reported his own boat lost.**

### Case 4 — "Remember, Remember" (about 15–20 min; added in improvement round 1)
*Wednesday 5 November, Bonfire Night.* The Lifeboat Appeal box was taken from the Mayor's
parlour at the Town Hall, locked away at 20:50 and found gone at 21:40, both **by the Town Hall
clock**. Forcing the door and the bureau took 5 minutes.
- Lanes: four returning faces: **Agnes Trelawney**, **Clem Hollis**, **Elias Garrow**,
  **Rolf Abernethy**, plus TOWN. No new portraits are needed.
- Teaches: the crime's own window can be on a wrong clock, and a red card isn't always a liar.
- Beats: three innocent lies sit in the red from the start (Agnes was at Maud's bakery, Clem
  nipped to the Lantern for a rum, Elias stopped at Fenwick's for a torch for Nell), and each
  confession brings its own paper. Agnes's confession unlocks Maud's statement, which is **true**
  but timed by the Town Hall clock, so it collides with Agnes's 22:02 bus pass. Confronting Maud
  costs a badge (the validator lists her as the case's trap). Under the printed window Clem's
  lock reads OPEN, a decoy. The caretaker's "first rocket at twenty to nine by our clock" and the
  coastguard's maroon at 20:30 are one moment, so the Town Hall clock runs 10 minutes fast.
  Linking them clears Maud, closes Clem's lock, **slides the incident window to 20:40–21:30**,
  and crosses Elias's face off the anonymous door-book entry ("tin No. 6 handed in, collector
  didn't sign"): only Rolf fits. His "on the pier till ten to nine" is now against his own tin,
  handed in at 20:42, and with Sid's (known-slow) clock putting him in the Lantern at 20:58 he
  has two minutes to spare. **Culprit: Rolf**, the collector everyone trusted.
- The validator finds 24 reachable states (the most of any case) and one solution.

### Case 5 — "The Wrenhaven Lily" (about 15–20 min; added in improvement round 2)
*Saturday 13 December, hard frost.* The Wrenhaven Lily, an orchid that blooms once in seven
years, was cut from its pot in the Park Glasshouse. Penrose's last round was at 21:15 and he found
the door forced at 22:06, both **by the glasshouse clock**. Lifting the lily took 6 minutes.
- Lanes: **Ines Delacroix**, **Bertram Cole** and **Nell Garrow**, plus TOWN. All have existing
  portraits.
- Teaches: **a chain of clocks**. A mended clock is as good as the church, so it can mend another.
- Beats: Nell's lock reads OPEN under the printed window (she was the last in the orchid house),
  but Penrose's true statement about her collides with her own left-luggage ticket. That's the
  trap, and it stays red until his clock is fixed. Cole lied about leaving the Grand's door (he
  fetched a sack of orchid compost from the depot). The coastguard's 21:05 radio check and the
  Yacht Club's radio book are one moment, so the Club clock is 7 minutes fast. That unlocks the
  glasshouse frost log. Its "heaters tripped" and the Club's "lights out" are the same power cut,
  so the glasshouse clock is 9 minutes slow. The crime moves to 21:24–22:15, Penrose and Nell
  clear, and the gate book's small bootprints fit only Ines. Her "never left the Club" falls.
  With the Club's tab now at 21:52 and her Roscoff call at 22:22, she has two minutes to spare.
  **Culprit: Ines**, taking her grandmother's lily home.
- Nothing on a reliable clock shares a moment with the glasshouse clock, so the chain can't be
  shortcut. An EditMode test checks this. The validator finds 8 reachable states and one
  solution.

### Difficulty curve
| Case | Lanes | Cards | Contradictions | New idea | Target time |
|---|---|---|---|---|---|
| 1 | 3 | 8 | 3 | pin / confront / incident | 4–6 min |
| 2 | 3 + TOWN | 14 | 4 | clocks and links | 8–12 min |
| 3 | 4 + TOWN | 18 | 5 | identity, camera clocks | 12–18 min |
| 4 | 4 + TOWN | 19 | 5 | the crime's own clock, an honest witness in the red | 15–20 min |
| 5 | 3 + TOWN | 18 | 3 | a chain of clocks: mend one clock with another | 15–20 min |

### Validator ("airtight" proof)
`Tools/CaseValidator` (a .NET console app compiled against the **same C# logic files the game
uses**) and a Unity EditMode test both run these checks on every case:
1. **Ground truth**: each person's true itinerary is walkable; every record and true statement
   matches it (after true clock offsets); every false card contradicts it; the culprit's
   itinerary contains the incident and nobody else's does.
2. **Reachable-state search** over (unlocked cards, corrected clocks, struck cards) using only
   legal moves (valid links, confrontations backed by established evidence, unlock triggers).
3. **Exactly one solution**: in *every* reachable state where an accusation is allowed, the
   incident fits exactly the culprit. No premature wrong accusation is possible.
4. **Solvable**: the fully solved state is reachable from the start, and from every reachable state.
5. **Every contradiction is discoverable**: every designed contradiction appears in some reachable
   state, and every false card is in a confrontable contradiction in some reachable state.
6. **No accidental contradictions**: in the solved state no true card is in conflict.
7. **Identity**: unknown cards are only ever confirmed to their true subject.
8. Every card gets unlocked, and there are no dangling ids.
9. **Pin order** (added in improvement round 2): the solution is replayed with the tray pinned in
   60 seeded random orders. No unknown card may ever be confirmed to the wrong person, and the case
   must still close. Identity elimination counts every unlocked record, pinned or not, so it
   doesn't depend on the order the player pins in.

## 7. Art direction

- **Scene**: one lamp-lit corner of the agency. A cork wall board in a dark oak frame fills the
  upper ~70% of the view. The near desk edge (in perspective) holds the card tray, the town map,
  Connie's memos, a brass desk clock, a rotary phone, a mug, a magnifier and a rubber stamp. Rain
  streaks a window at the left edge in cool blue night light.
- **Palette**: lamp amber `#F2B65A`, cork `#B88A5A`, paper cream `#F1E6CF`, ink navy `#1F2A3A`,
  oxblood `#8E2B2B` (contradictions, string), brass `#C9A24A`, night teal `#203B45`. Location
  colours are muted sea-glass tones (teal, mustard, sage, dusty rose, slate, terracotta…).
- **Shapes**: paper rectangles with slight curl, zig-zag receipt edges, polaroids, round
  push-pins. Type: a typewriter face for records and memos, handwriting for testimony, a serif for
  titles.
- **Lighting**: warm key light from the desk lamp (soft shadows from cards and pins), a cool
  window rim, low ambient. URP with SSAO, bloom, vignette, slight film grain, warm grading.
- **Camera**: a fixed perspective shot, nearly square-on to the board (no foreshortening of
  text), with a slight mouse parallax. It pushes in on the board for the reconstruction.
- **Characters**: stylised polaroid portraits rendered in Blender from a parametric bust
  generator (head, hair, hat, glasses, collar, colours per character).

## 8. Audio direction

All synthesized with Python and numpy (no samples). It should be **restrained detective audio**.
- **Music**: brushed-drum noir jazz: a Karplus-Strong upright-bass walking line, FM Rhodes
  voicings, a soft vibraphone, brush swishes, and a ride, with room reverb. Three cues: title
  ("Wrenhaven After Dark"), investigation loop (sparse, so it doesn't fight concentration), and a
  case-closed resolution sting. The music ducks under memos and the reconstruction.
- **SFX**: paper lift, paper drop, pin push, card slide on cork, clock ratchet ticks while cards
  slide, a contradiction cue (a low bowed-string tension swell and a paper rattle), a soft
  two-note resolve chime, a stamp thump, a "stands firm" muted thud, a paper-clip link snap,
  typewriter keys for memos, a lock-break (glass and a piano chord), the case-closed stamp plus a
  chord, and UI ticks.
- **Ambience**: rain on glass, the desk clock's tick, an occasional distant foghorn.

## 9. UI / UX and controls

| Input | Action |
|---|---|
| Left-drag | pick up / move a card; drop on a lane to pin; hold it on another card until LINK shows, then drop, to link |
| Hover | lift and enlarge the card (full text); hover a ribbon to show the route on the map |
| Left-click a pinned card | open its detail panel: **Confront**, Unpin |
| Right-click | return a card to the tray |
| Drag the incident card | preview fit per lane; drop to accuse |
| Esc | pause (resume, settings, case select, quit) |
| Touch (browser, round 6) | tap = click, finger drag = drag, press and hold = read a card; the HUD's Hint / Notes / Menu buttons |
| Tab | notebook (questions, objectives, the clocks you know) |
| F1 / ? | Connie's hint for the next step (marks the case "with help", no badge loss) |

Screens: title (lamp flickers on, logo stamped), case select (three folders, badges, best
time), case intro (typewritten case file), board, pause, settings (master/music/SFX volume,
fullscreen, resolution, text size, reduced motion), and case closed (reconstruction, stamp,
rating, epilogue, next case). There's no tutorial wall: Connie's one-line memos trigger from board
state the first time something happens (first pin, first red, first confront, first "everyone's
covered", first link, first unknown card).

## 10. Game feel and juice list
- A card lifts on pickup (scale 1.06, shadow grows, tilts toward drag velocity, paper sound).
- Drop: it eases into the slot with overshoot, the pin stabs in (scale punch, thunk), and a
  little cork dust puff appears.
- Clock correction: a ratchet tick per minute while every card on that clock glides, ribbons
  re-flow in real time, and a clock badge flips to "−5 min ✓".
- Contradiction: the ribbon tears red with hatching, both pins tremble, a low string swell plays,
  and the camera gives a tiny nudge.
- Confront: the card shakes, then **FALSE** is stamped (scale-slam, ink splat, desaturate) and
  the reply types out on a memo.
- Stands firm: the card bounces back, a badge drops off the rating rail, and a muted thud plays.
- Lock: COVERED to OPEN plays a glass crack with shards, the lock icon breaks, and a piano chord.
- Unknown card: faces get crossed out with a pen-stroke animation, then an "ONLY X FITS" stamp,
  and the card flies to the lane.
- Incident pin: the camera pushes in, the reconstruction plays (a pawn walks the culprit's route
  on the map, a timeline cursor sweeps), then **CASE CLOSED** is stamped and the music resolves.
- Ambient life: a slow lamp flicker, rain, dust motes in the lamp beam, and parallax.

## 11. Code architecture

```
Assets/Scripts/Logic/     pure C# (no UnityEngine) — shared with the validator
  MiniJson.cs             tiny JSON reader
  CaseData.cs             town, people, clocks, cards, incident, triggers (parsed from JSON)
  TownMap.cs              all-pairs shortest walking times
  Board.cs                state (unlocked, calibrated, struck, hypotheses) and derived facts:
                          true times, lane events, conflicts, unknown candidates, incident fit
  BoardActions.cs         Link / Confront / unlock triggers (oracle answers from truth data)
  CaseValidator.cs        reachable-state search and the checks in §6
Assets/Scripts/Game/      Unity side
  GameRoot.cs             bootstrap, screen flow, save/settings
  BoardView.cs            lanes, ruler, lane portraits, alibi locks
  CardView.cs             3D card object (TMP text, materials, hover/lift/tilt)
  DragController.cs       picking, drag plane, drop targets, link targets
  RibbonView.cs           travel ribbons and conflict hatching (procedural meshes)
  IncidentView.cs, MapView.cs, Reconstruction.cs, Fx.cs (tweens, shake, particles)
Assets/Scripts/UI/        runtime-built uGUI + TMP: title, case select, pause, settings, memos, notebook
Assets/Scripts/Audio/     AudioDirector (music crossfade, ducking), Sfx bank
Assets/Resources/         Cases/*.json, Town.json, Audio, Models, Fonts, Textures
Assets/Editor/            BuildScript, ProjectSetup (URP/volume/TMP assets), validator menu
Assets/Tests/EditMode/    runs CaseValidator on every case
Tools/CaseValidator/      dotnet console project linking Assets/Scripts/Logic/*.cs
Tools/                    synth_audio.py, build_models.sh, play.sh, unity.sh, capture helpers
ArtSource/                Blender generator scripts + .blend sources
```

Everything gameplay-critical lives in `Logic/`, so the game and the validator can never disagree.

## 12. Asset list (Blender, `ArtSource/`)
| Asset | Notes |
|---|---|
| Cork board + oak frame | bevelled frame, cork slab with procedural noise material |
| Desk | oak top with rounded front edge, drawer line, leather blotter |
| Desk lamp | banker's lamp: brass stem, green glass shade (emissive bulb) |
| Push-pins | round-head pin (colour set in Unity), plus a brass thumbtack |
| Card meshes | index card (slight curl), receipt (zig-zag tear), ticket stub, polaroid frame, ledger slip, memo |
| Rotary telephone, typewriter (partial), mug, magnifier, fountain pen, rubber stamp, ink pad, brass desk clock, case folders | desk dressing |
| Window frame | left edge, rain via shader |
| Character busts | parametric heads for 13 characters, rendered to polaroid portraits |
| Town map | Wrenhaven diorama rendered top-down in a vintage-map style (coast, streets, landmark buildings) |
| Pawn | little brass detective pawn for the reconstruction route |

## 13. Milestones
1. **M0 Plan and setup**: PLAN.md, git, Unity URP project, tooling scripts.
2. **M1 Logic and validator**: shared C# logic, three cases in JSON, the validator passing.
3. **M2 Core prototype**: board, lanes, cards, drag/snap, ribbons, conflicts, confront, link,
   incident pin. Playable case 1 with placeholder visuals, screenshot, iterate on feel.
4. **M3 Content**: all three cases playable, memos and onboarding, notebook, unknown cards,
   reconstruction.
5. **M4 Art**: Blender assets, portraits, map, materials, lighting, post-processing.
6. **M5 Audio**: synthesized music and SFX, mixing, ducking.
7. **M6 Shell and polish**: title, case select, pause, settings, save, transitions, juice pass.
8. **M7 Verify and ship**: EditMode tests, autoplay smoke test, screenshots, Linux build, README.

## 14. Risks and mitigations
| Risk | Mitigation |
|---|---|
| Puzzle logic has holes | shared-code validator with exhaustive reachable-state search; run on every change |
| Dense board becomes unreadable | compact chips on lanes, full card on hover, two-row staggering, fixed text sizes |
| 3D cards make text blurry | TextMeshPro SDF, near-square-on camera, readability tests in screenshots |
| Players guess instead of deduce | confront only with established evidence; badge cost for wrong links/confronts |
| Unity iteration is slow | most logic tested in .NET outside Unity; batch builds; runtime-built UI |
| Linux editor quirks (libxml2) | wrapper script sets LD_LIBRARY_PATH; Wayland flag for the player |
| Shared machine load | batch mode and headless Blender; close the Editor after use |
| Synthesized audio sounds cheap | layered synthesis, envelopes, reverb, careful mix, short tasteful cues |

## 15. The 5-minute prototype test
A new player opens Case 1. Within 30 seconds they've pinned a card and seen a walking-time
ribbon. Within 2 minutes they've watched two believable statements turn red together and
confronted a liar, who turns out to be innocent ("Agnes lied about the sherry, not the cake").
Within 5 minutes they drag the incident card across the board, feel it refuse two lanes, and snap
it into Bram's four-minute gap. The reconstruction plays and the CASE CLOSED stamp lands. Then the
Case 2 folder slides in, titled "The Regatta Cup", with Connie's memo: *"Clocks lie too."*
**Pass condition:** the player asks to open the next folder. If they don't, the first
contradiction isn't dramatic enough, or the incident pin isn't satisfying enough. Those two
moments get the polish budget first.

## 16. Stretch goals (only after everything above is polished)
- ~~"Daily Docket": procedurally generated mini-cases, built from random itineraries and checked
  by the same validator.~~ Built in improvement round 3 (`Assets/Scripts/Logic/Docket.cs`).
  Each calendar day gets a three-suspect case from the cast of ten and the town's places, on a
  fixed rota of fifteen small crimes (thirteen in round 3; round 4 added the bus depot and the cliff path):
  - the **culprit** claims one place all evening, slipped out to the scene, and a record somewhere
    else after the crime breaks the claim; confronting it brings a second, true statement that
    leaves the hole;
  - an **innocent liar** claims one place but was at another (a record shows it); confronting the
    lie brings a witness there whose statement covers the whole window;
  - an **honest** suspect's story is true, with paper to match. On about a third of days that
    paper is timed by a wrong clock (a café till, the Grand's lobby clock…) and sits in the red
    until it's linked through one moment seen on two clocks (a power dip, the lifeboat maroons, an
    exchange fault or the ferry's horn, noted on a prop from the wrong clock's own place).
    Confronting it costs a badge, and Connie then names the clock to blame.
  The generator writes ordinary case JSON and only offers it once `CaseValidator` proves it
  airtight (pin orders included); otherwise it tries the next of 40 seeded variations. It uses its
  own SplitMix64 generator, so a date produces the same docket in Unity, the browser and .NET. The
  validator console sweeps dates (`--docket N`) and prints one in full (`--docket-show`). The
  words come from a second generator seeded the same way, with several hand-written variants per
  line, so rewording never changes a puzzle (`--docket-phrases N` counts what still recurs). The
  case files keep the last seven days in a drawer, so a missed day stays playable for a week, and
  they roll over at midnight even while they're on screen. A solved docket's **Copy result** puts a
  spoiler-free line on the clipboard (`Docket.ShareLine`: date, crime, stars, time, seals).
- A case editor menu in the Unity Editor.
