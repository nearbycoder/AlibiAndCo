// The --play session of Tools/mobile-check.mjs: a short game played with real touches on a phone or
// tablet profile, or with the mouse on a desktop one, checking the touch controls along the way.
//
// On a touchscreen: the touch buttons are up before any touch; the first tap (on empty table) starts
// the sound; Case Files, the first case and its write-up are opened by tapping the game's own buttons
// (found by asking the game where they are: GameRoot.LogTargets); on the board a card is dragged up
// and another tapped to pin them, and a pinned one held to read; two fingers pinch the board in, Fit
// brings it back, and Notes, Back, Hint and Menu work from the buttons beside the picture. Portrait
// profiles only check that the page asks to be turned. On a desktop: the touch buttons never show,
// before or after a mouse click and a key.
export async function play({ page, prof, name, r, lines, until, shot, framesOver, probe, fingers, sleep }) {
  const p = { steps: [] };
  const problem = (s) => { r.problems.push(s); p.steps.push("FAIL " + s); };
  const ok = (s) => p.steps.push("ok " + s);
  const railState = () => page.evaluate(() => ({ state: document.querySelector("#rail").dataset.state, touch: document.documentElement.classList.contains("touch"),
    layout: document.documentElement.classList.contains("touch-layout"), shown: getComputedStyle(document.querySelector("#rail")).display !== "none",
    buttons: [...document.querySelectorAll("#rail button")].filter((b) => getComputedStyle(b).display !== "none").map((b) => { const q = b.getBoundingClientRect(); return { cmd: b.dataset.cmd, w: Math.round(q.width), h: Math.round(q.height), x: Math.round(q.x + q.width / 2), y: Math.round(q.y + q.height / 2) }; }),
    rotate: getComputedStyle(document.querySelector("#rotate")).display !== "none", view: innerWidth + "x" + innerHeight }));

  // ---- desktop: the buttons must never show
  if (prof.desktop) {
    const box = await page.evaluate(() => { const q = document.querySelector("#unity-canvas").getBoundingClientRect(); return { w: q.width, h: q.height }; });
    const before = await railState();
    await page.mouse.move(box.w * 0.75, box.h * 0.12); await sleep(200);
    await page.mouse.down(); await sleep(80); await page.mouse.up(); await sleep(800);
    await page.keyboard.press("Shift"); await sleep(500);
    const after = await railState();
    p.desktop = { before, after };
    for (const [when, s] of [["at the title", before], ["after a click and a key", after]])
      if (s.touch || s.layout || s.shown || s.buttons.length || s.rotate) problem(`desktop ${when}: touch controls showing (${JSON.stringify(s)})`);
      else ok(`desktop ${when}: no touch controls`);
    if (box.w < 1900) problem(`desktop: the canvas is ${box.w}x${box.h}, not the whole window`);
    return p;
  }

  // ---- portrait: the page asks to be turned
  if (/portrait/.test(name)) {
    const s = await railState();
    p.portrait = s;
    if (s.rotate) ok("portrait: the page asks to turn the phone"); else problem("portrait: no prompt to turn the phone");
    await shot("01-portrait");
    return p;
  }

  const t0 = probe();
  if (!t0 || !t0.touchUi) problem("the touch buttons weren't up before the first touch"); else ok("touch buttons up at the title");
  if (t0 && t0.audio.some((a) => a === "running")) problem(`sound running before any touch (${t0.audio})`);

  // The game's buttons and cards, in CSS pixels.
  async function targets() {
    const n = lines.length;
    await page.evaluate(() => window.alibiGame.SendMessage("GameRoot", "LogTargets", ""));
    const l = await until(n, /^\[Targets\] /, 5000);
    if (!l) return [];
    const [size, ...items] = l.slice(10).split(";");
    const [W, H] = size.split("x").map(Number);
    const q = await page.evaluate(() => { const b = document.querySelector("#unity-canvas").getBoundingClientRect(); return { x: b.left, y: b.top, w: b.width, h: b.height }; });
    return items.filter(Boolean).map((s) => {
      const at = s.lastIndexOf("@"), [x, y] = s.slice(at + 1).split(",").map(Number);
      return { name: s.slice(0, at), x: q.x + x * q.w / W, y: q.y + (H - y) * q.h / H };
    });
  }
  const find = (list, re) => list.find((t) => re.test(t.name));
  const rail = async (cmd) => {
    const s = await railState();
    const b = s.buttons.find((x) => x.cmd === cmd);
    if (!b) { problem(`no ${cmd} button showing (state "${s.state}", showing ${s.buttons.map((x) => x.cmd)})`); return false; }
    // The browser's own tap: a page's synthetic touches (WebKit's here) never become a click.
    await fingers.quickTap(b.x, b.y, 1200);
    return true;
  };
  const expectState = async (want, what) => {
    let s;
    for (let i = 0; i < 12; i++) { s = await railState(); if (s.state === want) break; await sleep(250); }
    if (s.state === want) ok(`${what}: buttons for "${want}" (${s.buttons.map((b) => b.cmd).join(", ") || "none"})`);
    else problem(`${what}: the buttons are for "${s.state}", not "${want}"`);
    return s;
  };
  const view = await page.evaluate(() => ({ w: innerWidth, h: innerHeight }));

  // 1. The first tap, on empty table at the top of the picture: the sound starts.
  await fingers.quickTap(view.w * 0.55, view.h * 0.06, 1500);
  await sleep(600);
  const a = probe();
  p.audio = { firstInputMs: a.firstInput, contexts: a.audio, standIn: r.audioStandIn || null };
  if (a.audio.length && a.audio.every((x) => x === "running")) ok("first tap started the sound"); else problem(`sound after the first tap: ${JSON.stringify(a.audio)}`);
  await shot("01-title");
  const title = await expectState("title", "title");
  p.railButtons = title.buttons;
  // Thumb-sized: 44 CSS pixels (points) or more.
  const all = await page.evaluate(() => [...document.querySelectorAll("#rail button")].map((b) => { const was = b.style.display; b.style.display = "flex"; const q = b.getBoundingClientRect(); b.style.display = was; return { cmd: b.dataset.cmd, w: Math.round(q.width), h: Math.round(q.height) }; }));
  p.buttonSizes = all;
  if (all.every((b) => b.w >= 44 && b.h >= 44)) ok(`every touch button at least 44 pt (${all.map((b) => `${b.cmd} ${b.w}x${b.h}`).join(", ")})`);
  else problem(`touch buttons under 44 pt: ${JSON.stringify(all.filter((b) => b.w < 44 || b.h < 44))}`);

  // 2. Case Files, by tapping the game's own button.
  let tg = await targets();
  p.titleTargets = tg.map((t) => t.name);
  let b = find(tg, /^Case Files$/);
  if (!b) { problem(`no Case Files button among ${p.titleTargets}`); return p; }
  p.caseFilesButton = { x: Math.round(b.x), y: Math.round(b.y) };
  let n = lines.length;
  await fingers.tap(b.x, b.y, 2500);
  await shot("02-case-files");
  await expectState("back", "case files");

  // 3. The first case (the one marked "Case 1" or the first in the list), then its write-up's Begin.
  tg = await targets();
  p.caseTargets = tg.map((t) => t.name);
  b = tg.find((t) => /^(?!Back|Daily|Settings)/i.test(t.name) && /\b1\b|Sugar|Spite/i.test(t.name)) || tg.find((t) => !/Back|Docket|Settings|week/i.test(t.name));
  if (!b) { problem(`no case to open among ${p.caseTargets}`); return p; }
  p.caseButton = b.name;
  n = lines.length;
  await fingers.tap(b.x, b.y, 2500);
  if (!(await until(n, /^\[Intro\] /, 6000))) problem(`tapping "${b.name}" didn't open a write-up`); else ok(`tapped "${b.name}": its write-up opened`);
  await shot("03-intro");
  tg = await targets();
  p.introTargets = tg.map((t) => t.name);
  b = find(tg, /Open the board|Begin|Start|Continue/i);
  if (!b) { problem(`no Begin button among ${p.introTargets}`); return p; }
  await fingers.tap(b.x, b.y, 4500);
  await shot("04-board");
  await expectState("board", "board");

  // 4. Pin two cards: one dragged up onto the board, one tapped.
  tg = await targets();
  const cards = tg.filter((t) => t.name === "card");
  const tray = cards.filter((c) => c.y > view.h * 0.62).sort((x, y) => x.x - y.x);
  p.trayCards = tray.length;
  if (tray.length < 2) { problem(`only ${tray.length} card(s) in the tray to pin (cards at ${cards.map((c) => `${Math.round(c.x)},${Math.round(c.y)}`)})`); return p; }
  const pinnedBefore = await pinnedCount(page);
  await fingers.drag(tray[0].x, tray[0].y, tray[0].x, view.h * 0.4);
  await sleep(1200);
  await fingers.tap(tray[1].x, tray[1].y, 2000);
  await sleep(3000);   // the save is written, then synced to IndexedDB a moment later
  const pinnedAfter = await pinnedCount(page);
  p.pinned = { before: pinnedBefore, after: pinnedAfter };
  if (pinnedAfter >= pinnedBefore + 2) ok(`a drag and a tap pinned two cards (${pinnedBefore} -> ${pinnedAfter} in the save)`);
  else problem(`pinned cards in the save went ${pinnedBefore} -> ${pinnedAfter} after a drag and a tap`);
  await shot("05-pinned");

  // 5. Hold a finger on a pinned card: it reads, it doesn't click.
  tg = await targets();
  const onBoard = tg.filter((t) => t.name === "card" && t.y < view.h * 0.6 && t.x > view.w * 0.1);
  if (onBoard.length) {
    n = lines.length;
    // Two seconds: the game counts a hold only once it has seen the finger down for four frames, and
    // headless WebKit draws a tablet-sized picture at two or three frames a second.
    await fingers.hold(onBoard[0].x, onBoard[0].y, 2000);
    if (await until(n, /^\[Touch\] held still/, 2500)) ok("a held finger read a card without clicking it"); else problem("holding a finger on a card wasn't read as a hold");
  } else problem("no pinned card on the board to hold");

  // 6. Pinch the board in, then Fit.
  n = lines.length;
  await fingers.pinch(view.w * 0.4, view.h * 0.35, 90, 260);
  const done = await until(n, /^\[Touch\] pinch done at /, 4000);
  const zoom = done ? Number(/at ([\d.]+)x/.exec(done)[1]) : 1;
  p.pinchZoom = zoom;
  if (zoom > 1.3) ok(`two fingers zoomed the board to ${zoom}x`); else problem(`a pinch zoomed the board to ${zoom}x`);
  await shot("06-zoomed");
  await expectState("board zoomed", "zoomed");
  p.fpsBoardZoomed = await framesOver(4000);
  if (await rail("fit")) {
    if (await until(n, /^\[Touch\] zoom [\d.]+x back to the whole desk/, 3000)) ok("Fit went back to the whole desk"); else problem("Fit didn't zoom out");
    await expectState("board", "after Fit");
  }

  // 7. Notes, Back; Hint; Menu, Back.
  n = lines.length;
  if (await rail("notes")) { await shot("07-notes"); await expectState("back", "notes open"); }
  if (await rail("back")) await expectState("board", "notes closed");
  n = lines.length;
  if (await rail("hint")) {
    if (await until(n, /^\[Touch\] button: hint/, 2000)) ok("Hint reached the game"); else problem("the Hint button didn't reach the game");
    await sleep(1500);
    await shot("08-hint");
  }
  if (await rail("menu")) { await shot("09-menu"); await expectState("back", "menu open"); }
  if (await rail("back")) await expectState("board", "menu closed");
  p.fpsBoard = await framesOver(5000);
  await shot("10-board-end");
  return p;
}

// Pinned cards of the case in progress, from the save in the page's storage.
async function pinnedCount(page) {
  return page.evaluate(() => new Promise((ok) => {
    const rq = indexedDB.open("/idbfs");
    rq.onerror = () => ok(-1);
    rq.onsuccess = () => {
      const db = rq.result;
      try {
        const all = db.transaction("FILE_DATA").objectStore("FILE_DATA").openCursor();
        let n = 0;
        all.onsuccess = () => {
          const c = all.result;
          if (!c) { ok(n); db.close(); return; }
          if (/alibi_save\.json$/.test(String(c.key)) && c.value && c.value.contents) {
            try { const s = JSON.parse(new TextDecoder().decode(c.value.contents)); n = s.inProgress ? (s.inProgress.pinned || []).length : 0; } catch { n = -2; }
          }
          c.continue();
        };
        all.onerror = () => { ok(-1); db.close(); };
      } catch { ok(0); db.close(); }
    };
  }));
}
