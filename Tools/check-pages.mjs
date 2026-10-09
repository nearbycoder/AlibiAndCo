#!/usr/bin/env node
// Checks the browser build where it's served (the live GitHub Pages site, or Builds/Pages served
// locally under /AlibiAndCo/). Exits 0 only if, in every engine asked for, the page loads the game
// to its title screen with no console errors, no page errors and no failed requests.
//
//   node Tools/check-pages.mjs <url> [--engine chromium,firefox] [--play] [--out Logs/pages/check]
//                              [--size 1920x1080] [--timeout 180]
//
// The basic check opens the plain page (no self-test flags), waits for the loader to go and for the
// game to apply its graphics step at the title ("[Fidelity] ..." in the console), then watches a few
// more seconds for errors. It records the load time, the bytes downloaded and the WebGL renderer.
//
// --play adds a short session with real mouse input, as a player would: audio must be held until the
// first click and running after it; Settings > Low graphics; Case Files > the first case > its
// write-up > the board, pinning a card; then a reload of the page, which must come back on Low (the
// setting kept in the browser's storage) with a "Continue" case. It's meant for a fresh profile (each
// run makes one) at 1920x1080, where the clicks below land on the game's buttons.
//
// Chromium comes from playwright-core (PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core, with its
// browsers cached under ~/.cache/ms-playwright); Firefox is the system Firefox driven over WebDriver
// BiDi by puppeteer-core (PUPPETEER_CORE=/path/to/node_modules/puppeteer-core, FIREFOX=/usr/bin/firefox).
// Every browser runs headless, in a throwaway profile.
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf("--" + name); return i >= 0 && i + 1 < args.length ? args[i + 1] : def; };
const VALUED = ["--engine", "--out", "--size", "--timeout"];
const url = args.find((a, i) => !a.startsWith("--") && !VALUED.includes(args[i - 1]));
if (!url) { console.error("usage: node Tools/check-pages.mjs <url> [--engine chromium,firefox] [--play] [--out dir] [--size WxH] [--timeout s]"); process.exit(2); }
const engines = opt("engine", "chromium").split(",");
const play = args.includes("--play");
const [W, H] = opt("size", "1920x1080").split("x").map(Number);
const TIMEOUT = Number(opt("timeout", "180")) * 1000;
const OUT = path.resolve(ROOT, opt("out", "Logs/pages/check"));
const LOG_CAP = 2 * 1024 * 1024;
const sleep = (ms) => new Promise((ok) => setTimeout(ok, ms));
const req = createRequire(import.meta.url);
function load(names, hint) {
  for (const n of names.filter(Boolean)) { try { return req(n); } catch { /* next */ } }
  throw new Error(hint);
}

// Counts the page's AudioContexts, so the check can see them held until the first input.
const AUDIO_PROBE = `(() => {
  const made = window.alibiAudioContexts = [];
  for (const name of ["AudioContext", "webkitAudioContext"]) {
    const C = window[name];
    if (!C) continue;
    window[name] = new Proxy(C, { construct(t, a, nt) { const c = Reflect.construct(t, a, nt); made.push(c); return c; } });
  }
})();`;

async function launch(engine) {
  const dir = path.join(OUT, engine);
  fs.mkdirSync(dir, { recursive: true });
  if (engine === "firefox") {
    const profile = path.join(dir, "profile");
    fs.rmSync(profile, { recursive: true, force: true });
    const puppeteer = load([process.env.PUPPETEER_CORE, "puppeteer-core"], "puppeteer-core not found: set PUPPETEER_CORE");
    const browser = await puppeteer.launch({ browser: "firefox", protocol: "webDriverBiDi", headless: true,
      executablePath: process.env.FIREFOX || "/usr/bin/firefox", userDataDir: profile, timeout: 120000,
      // Puppeteer's profile lets every page play sound; put back a release Firefox's blocking.
      extraPrefsFirefox: { "media.autoplay.default": 1, "media.autoplay.block-webaudio": true, "media.autoplay.blocking_policy": 0 } });
    const context = await browser.createBrowserContext();
    return { browser, context, dir, puppet: true };
  }
  const pw = load([process.env.PLAYWRIGHT_CORE, "playwright-core", "playwright"], "playwright-core not found: set PLAYWRIGHT_CORE");
  // No autoplay override: the page must wait for a real gesture, as it does for a player.
  const opts = { headless: true, args: ["--use-angle=gl-egl", "--enable-gpu", "--ignore-gpu-blocklist"] };
  let browser;
  try { browser = await pw.chromium.launch(opts); }
  catch (e) {
    // This playwright-core wants a browser revision that isn't cached: use the newest one that is.
    if (!/Executable doesn't exist/.test(e.message)) throw e;
    const cache = process.env.PLAYWRIGHT_BROWSERS_PATH || path.join(process.env.HOME, ".cache/ms-playwright");
    const found = fs.readdirSync(cache).filter((d) => /^chromium(_headless_shell)?-\d+$/.test(d))
      .sort((a, b) => Number(b.split("-").pop()) - Number(a.split("-").pop()) || (a.includes("headless") ? -1 : 1))
      .map((d) => d.includes("headless") ? path.join(cache, d, "chrome-headless-shell-linux64/chrome-headless-shell") : path.join(cache, d, "chrome-linux64/chrome"))
      .find((f) => fs.existsSync(f));
    if (!found) throw e;
    browser = await pw.chromium.launch({ ...opts, executablePath: found });
  }
  const context = await browser.newContext({ viewport: { width: W, height: H } });
  return { browser, context, dir, puppet: false };
}

async function check(engine) {
  const r = { engine, url, ok: false, problems: [] };
  let b;
  try { b = await launch(engine); }
  catch (e) { r.problems.push("couldn't launch: " + e.message.split("\n")[0]); return r; }
  const { browser, context, dir, puppet } = b;
  const logFile = path.join(dir, "console.log");
  fs.writeFileSync(logFile, "");
  let logged = 0;
  const append = (s) => { if (logged < LOG_CAP) { fs.appendFileSync(logFile, s + "\n"); logged += s.length + 1; } };
  const lines = [];
  const errors = [];
  const failed = [];

  async function open(page) {
    if (puppet) { await page.setViewport({ width: W, height: H }); await page.evaluateOnNewDocument(AUDIO_PROBE); }
    else await page.addInitScript(AUDIO_PROBE);
    page.on("console", (m) => {
      const t = m.text();
      append(`[${m.type()}] ${t}`);
      lines.push(t);
      if (m.type() === "error") errors.push(t);
    });
    page.on("pageerror", (e) => { append("[pageerror] " + (e.message || e)); errors.push("pageerror: " + (e.message || e)); });
    page.on("response", (res) => { if (res.status() >= 400) { failed.push(`${res.status()} ${res.url()}`); append(`[http ${res.status()}] ${res.url()}`); } });
  }
  const waitFor = (fn, ms) => puppet ? page.waitForFunction(fn, { timeout: ms }) : page.waitForFunction(fn, null, { timeout: ms });
  const since = (n, re) => lines.slice(n).find((l) => re.test(l));
  async function until(n, re, ms) {
    const end = Date.now() + ms;
    while (Date.now() < end) { const l = since(n, re); if (l) return l; await sleep(250); }
    return null;
  }
  const shot = (name) => page.screenshot({ path: path.join(dir, name + ".png") });
  const click = async (x, y, settle = 900) => {
    await page.mouse.move(x, y);
    await sleep(150);
    await page.mouse.down();
    await sleep(90);
    await page.mouse.up();
    await sleep(settle);
  };
  const audio = () => page.evaluate(() => (window.alibiAudioContexts || []).map((c) => c.state));

  // Loads the page to its title: the loader gone and the graphics step applied.
  async function toTitle(what) {
    const n = lines.length;
    const t0 = Date.now();
    await page.goto(url, { waitUntil: "load", timeout: TIMEOUT });
    try { await waitFor(() => !document.querySelector("#loader") || window.alibiLoadError, TIMEOUT); }
    catch { r.problems.push(`${what}: the loader was still up after ${TIMEOUT / 1000} s`); return null; }
    const loadError = await page.evaluate(() => window.alibiLoadError || null);
    if (loadError) { r.problems.push(`${what}: the page reported "${loadError}"`); return null; }
    const fid = await until(n, /^\[Fidelity\] (Low|Medium|High|Ultra):/, 30000);
    if (!fid) { r.problems.push(`${what}: the game never reached its title (no "[Fidelity]" line)`); return null; }
    await sleep(3000);   // the title settles; errors at startup would show by now
    return { ms: Date.now() - t0, step: /^\[Fidelity\] (\w+)/.exec(fid)[1] };
  }

  let page;
  try {
    page = await context.newPage();
    await open(page);
    const first = await toTitle("first load");
    if (first) {
      r.loadSeconds = +(first.ms / 1000).toFixed(1);
      r.graphicsAtLaunch = first.step;
      Object.assign(r, await page.evaluate(() => {
        const res = performance.getEntriesByType("resource");
        const sum = (k) => res.reduce((s, e) => s + (e[k] || 0), 0);
        const gl = document.createElement("canvas").getContext("webgl2");
        const ext = gl && gl.getExtension("WEBGL_debug_renderer_info");
        const c = document.querySelector("#unity-canvas").getBoundingClientRect();
        return { downloadedMB: +(sum("transferSize") / 1048576).toFixed(1), encodedMB: +(sum("encodedBodySize") / 1048576).toFixed(1),
          renderer: gl ? (ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER)) : "no WebGL 2",
          canvas: `${Math.round(c.width)}x${Math.round(c.height)} of ${innerWidth}x${innerHeight}` };
      }));
      await shot("01-title");
      r.title = true;
    }

    if (play && first) {
      const p = r.play = {};
      // What the browser allows a page's audio before any input ("allowed" in a profile that lets
      // everything play; a player's browser normally holds it until the first click or key).
      p.autoplayPolicy = await page.evaluate(() => navigator.getAutoplayPolicy ? navigator.getAutoplayPolicy("audiocontext") : "unknown");
      p.audioBeforeInput = await audio();
      // The first click: on empty table, away from every button.
      await click(W * 0.75, H * 0.12, 1500);
      p.audioAfterClick = await audio();
      const held = p.autoplayPolicy === "allowed" || !p.audioBeforeInput.includes("running");
      p.audioStarted = p.audioAfterClick.length > 0 && p.audioAfterClick.every((st) => st === "running");
      if (!held || !p.audioStarted) r.problems.push(`audio: ${JSON.stringify(p.audioBeforeInput)} before input (policy ${p.autoplayPolicy}), ${JSON.stringify(p.audioAfterClick)} after a click`);

      // Settings > Low > Done.
      let n = lines.length;
      await click(...TITLE_SETTINGS, 1500);
      await shot("02-settings");
      await click(...SETTINGS_LOW, 1200);
      p.settingSet = await until(n, /^\[Fidelity\] Low:/, 5000);
      if (!p.settingSet) r.problems.push("settings: a click on Low didn't set the graphics to Low");
      await shot("03-settings-low");
      // Fullscreen, where the browser allows it (a headless one may not): on, then off again.
      await click(...SETTINGS_FULLSCREEN, 1500);
      p.fullscreen = await page.evaluate(() => !!document.fullscreenElement);
      if (p.fullscreen) {
        // Back out the way a player's Esc would leave the browser's fullscreen, so the clicks below land.
        await page.evaluate(() => document.exitFullscreen());
        await sleep(1500);
        p.fullscreenLeft = await page.evaluate(() => !document.fullscreenElement);
      }
      await click(...SETTINGS_DONE, 1200);

      // Case Files > the first case > its write-up > the board, and a card pinned.
      n = lines.length;
      await click(...TITLE_CASE_FILES, 2500);
      await shot("04-case-files");
      await click(...FIRST_CASE, 2500);
      p.intro = await until(n, /^\[Intro\] /, 8000);
      if (!p.intro) r.problems.push("play: the first case's write-up didn't open");
      await shot("05-intro");
      await click(...INTRO_BEGIN, 4000);
      await shot("06-board");
      for (const at of TRAY_CARDS) await click(...at, 1200);
      await sleep(1500);
      await shot("07-pinned");
      // Unity writes the save, then syncs it to IndexedDB a moment later.
      await sleep(2500);
      p.saved = await page.evaluate(listSaves);
      if (!(p.saved.inProgress?.caseId && p.saved.inProgress.pinned > 0)) r.problems.push(`play: no case in progress with pinned cards in the save (${JSON.stringify(p.saved)})`);

      // Back to the page from scratch: the setting and the case in progress must come back.
      const again = await toTitle("reload");
      if (again) {
        p.reloadSeconds = +(again.ms / 1000).toFixed(1);
        p.graphicsAfterReload = again.step;
        if (again.step !== "Low") r.problems.push(`reload: graphics came back as ${again.step}, not Low`);
        p.savedAfterReload = await page.evaluate(listSaves);
        if (JSON.stringify(p.savedAfterReload.inProgress) !== JSON.stringify(p.saved.inProgress)) r.problems.push(`reload: the case in progress came back as ${JSON.stringify(p.savedAfterReload.inProgress)}`);
        await shot("08-title-after-reload");
      }
    }
  } catch (e) {
    r.problems.push("error: " + e.message.split("\n")[0]);
  } finally {
    try { if (page) await shot("final"); } catch { /* the page may be gone */ }
    await context.close().catch(() => {});
    await browser.close().catch(() => {});
  }
  r.consoleErrors = errors.slice(0, 20);
  r.failedRequests = failed.slice(0, 20);
  if (errors.length) r.problems.push(`${errors.length} console error(s)`);
  if (failed.length) r.problems.push(`${failed.length} failed request(s)`);
  r.ok = !!r.title && r.problems.length === 0;
  r.log = path.relative(ROOT, logFile);
  return r;
}

// The files in the page's persistent storage (IndexedDB /idbfs), and the case in progress in the save.
async function listSaves() {
  const names = await (indexedDB.databases ? indexedDB.databases() : Promise.resolve([{ name: "/idbfs" }]));
  if (!names.some((d) => d.name === "/idbfs")) return { files: [] };
  return new Promise((ok) => {
    const rq = indexedDB.open("/idbfs");
    rq.onerror = () => ok({ files: ["unreadable"] });
    rq.onsuccess = () => {
      const db = rq.result;
      try {
        const all = db.transaction("FILE_DATA").objectStore("FILE_DATA").openCursor();
        const out = { files: [], inProgress: null };
        all.onsuccess = () => {
          const c = all.result;
          if (!c) { ok(out); db.close(); return; }
          const name = String(c.key).replace(/^\/idbfs\/[^/]+\/?/, "");
          if (c.value && c.value.contents) {
            out.files.push(name);
            if (name === "alibi_save.json") {
              try {
                const save = JSON.parse(new TextDecoder().decode(c.value.contents));
                if (save.inProgress) out.inProgress = { caseId: save.inProgress.caseId, pinned: (save.inProgress.pinned || []).length };
              } catch (e) { out.inProgress = "unreadable: " + e.name; }
            }
          }
          c.continue();
        };
        all.onerror = () => { ok({ files: ["unreadable"] }); db.close(); };
      } catch (e) { ok({ files: ["unreadable: " + e.name] }); db.close(); }
    };
  });
}

// Where the --play clicks land at 1920x1080 (CSS pixels from the top left), at the game's default text size.
const TITLE_CASE_FILES = [410, 646], TITLE_SETTINGS = [410, 728], SETTINGS_LOW = [640, 679], SETTINGS_DONE = [960, 890], SETTINGS_FULLSCREEN = [907, 580];
const FIRST_CASE = [232, 570], INTRO_BEGIN = [566, 902], TRAY_CARDS = [[670, 900], [1250, 900]];

fs.mkdirSync(OUT, { recursive: true });
const results = [];
for (const e of engines) {
  const r = await check(e);
  results.push(r);
  console.log(JSON.stringify(r));
}
fs.writeFileSync(path.join(OUT, "summary.json"), JSON.stringify(results, null, 2));
const bad = results.filter((r) => !r.ok);
console.log(bad.length ? `check-pages: FAIL (${bad.map((r) => r.engine).join(", ")})` : `check-pages: PASS (${results.map((r) => r.engine).join(", ")})`);
process.exit(bad.length ? 1 : 0);
