#!/usr/bin/env node
// Browser self-test for the WebGL build (Tools/unity.sh build-webgl).
//
// Serves Builds/WebGL on 127.0.0.1, then in each engine:
//   autoplay  ?autoplay: every case played to CASE CLOSED through the real session code
//   pad       ?padtest: case 1 with a simulated gamepad only
//   reload    ?savecheck twice: the save written on the first load must come back after a reload
// and records the load time, frame rate, WebGL renderer, console errors and periodic screenshots.
//
//   node Tools/webtest.mjs [--engine chromium,firefox,webkit|all] [--only autoplay,pad,reload]
//                          [--size 1920x1080] [--out Captures/webtest]
//
// Needs playwright-core and its browsers (npx playwright install chromium webkit). It isn't a
// dependency of this repo: set PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core, or run it
// where `require("playwright-core")` resolves. PLAYWRIGHT_CORE_CHROMIUM / PLAYWRIGHT_CORE_WEBKIT pick a
// different copy per engine (each playwright-core version expects its own browser revisions).
// Firefox is the system Firefox, driven over WebDriver BiDi by puppeteer-core: set
// PUPPETEER_CORE=/path/to/node_modules/puppeteer-core (and FIREFOX=/path/to/firefox if it isn't
// /usr/bin/firefox).
import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf("--" + name); return i >= 0 && i + 1 < args.length ? args[i + 1] : def; };
const engines = opt("engine", "all") === "all" ? ["chromium", "firefox", "webkit"] : opt("engine").split(",");
const only = opt("only", "autoplay,pad,reload").split(",");
const [W, H] = opt("size", "1920x1080").split("x").map(Number);
const OUT = path.resolve(ROOT, opt("out", "Captures/webtest"));
const WEB = path.join(ROOT, "Builds/WebGL");
const LOG_CAP = 4 * 1024 * 1024;   // bytes of console log kept per run

const sleep = (ms) => new Promise((ok) => setTimeout(ok, ms));

function loadPuppeteer() {
  const req = createRequire(import.meta.url);
  for (const t of [process.env.PUPPETEER_CORE, "puppeteer-core"].filter(Boolean)) { try { return req(t); } catch { /* next */ } }
  throw new Error("puppeteer-core not found: set PUPPETEER_CORE=/path/to/node_modules/puppeteer-core");
}

function loadPlaywright(engine) {
  const req = createRequire(import.meta.url);
  const tries = [process.env["PLAYWRIGHT_CORE_" + engine.toUpperCase()], process.env.PLAYWRIGHT_CORE, "playwright-core", "playwright"].filter(Boolean);
  for (const t of tries) { try { return req(t); } catch { /* next */ } }
  console.error("playwright-core not found: set PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core");
  process.exit(2);
}

// ---------------------------------------------------------------- static server
const TYPES = { ".html": "text/html; charset=utf-8", ".js": "application/javascript", ".wasm": "application/wasm",
  ".data": "application/octet-stream", ".json": "application/json", ".png": "image/png", ".ico": "image/x-icon" };

function serve() {
  const server = http.createServer((req, res) => {
    let rel = decodeURIComponent(new URL(req.url, "http://x").pathname);
    if (rel.endsWith("/")) rel += "index.html";
    const file = path.join(WEB, path.normalize(rel));
    if (!file.startsWith(WEB) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) { res.writeHead(404); res.end(); return; }
    const headers = { "Cache-Control": "no-store" };
    let ext = path.extname(file);
    if (ext === ".br" || ext === ".gz") {
      headers["Content-Encoding"] = ext === ".br" ? "br" : "gzip";
      ext = path.extname(file.slice(0, -ext.length));
    }
    headers["Content-Type"] = TYPES[ext] || "application/octet-stream";
    res.writeHead(200, headers);
    fs.createReadStream(file).pipe(res);
  });
  return new Promise((ok) => server.listen(0, "127.0.0.1", () => ok(server)));
}

function buildSize() {
  let total = 0;
  for (const f of fs.readdirSync(path.join(WEB, "Build"))) total += fs.statSync(path.join(WEB, "Build", f)).size;
  return total;
}

// ---------------------------------------------------------------- one run
async function launch(engine) {
  if (engine === "firefox") {
    const profile = path.join(OUT, "firefox-profile");
    fs.rmSync(profile, { recursive: true, force: true });
    return loadPuppeteer().launch({ browser: "firefox", protocol: "webDriverBiDi", headless: true,
      executablePath: process.env.FIREFOX || "/usr/bin/firefox", userDataDir: profile });
  }
  const type = loadPlaywright(engine)[engine];
  const opts = { headless: true };
  if (engine === "chromium") opts.args = ["--use-angle=gl-egl", "--enable-gpu", "--ignore-gpu-blocklist", "--autoplay-policy=no-user-gesture-required"];
  return type.launch(opts);
}

// Playwright and puppeteer differ in a few calls; these keep run() engine-agnostic.
const puppet = (engine) => engine === "firefox";
async function newContext(browser, engine) {
  return puppet(engine) ? browser.createBrowserContext() : browser.newContext({ viewport: { width: W, height: H } });
}

async function run(browser, engine, mode, base, context) {
  const own = !context;
  context = context || await newContext(browser, engine);
  const page = await context.newPage();
  if (puppet(engine)) await page.setViewport({ width: W, height: H });
  const dir = path.join(OUT, `${engine}-${mode}`);
  fs.mkdirSync(dir, { recursive: true });
  const logFile = path.join(dir, "console.log");
  let logged = 0;
  const lines = [];
  const errors = [];
  const append = (s) => { if (logged < LOG_CAP) { fs.appendFileSync(logFile, s + "\n"); logged += s.length + 1; } };
  page.on("console", (m) => {
    const t = m.text();
    append(`[${m.type()}] ${t}`);
    if (/\[(AutoPilot|SaveCheck|Save|Seals|Conflicts)\]/.test(t)) lines.push(t);
    if (m.type() === "error") errors.push(t);
  });
  page.on("pageerror", (e) => { append("[pageerror] " + e.message); errors.push("pageerror: " + e.message); });

  const query = mode === "autoplay" ? "?autoplay" : mode === "pad" ? "?padtest" : "?savecheck";
  const t0 = Date.now();
  await page.goto(base + "/" + query, { waitUntil: "load" });
  if (puppet(engine)) await page.waitForFunction(() => !document.querySelector("#loader"), { timeout: 180000 });
  else await page.waitForFunction(() => !document.querySelector("#loader"), null, { timeout: 180000 });
  const loadMs = Date.now() - t0;
  const renderer = await page.evaluate(() => {
    const gl = document.createElement("canvas").getContext("webgl2");
    if (!gl) return "no WebGL 2";
    const ext = gl.getExtension("WEBGL_debug_renderer_info");
    return ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
  });

  const doneRe = mode === "autoplay" ? /\[AutoPilot\] done/ : mode === "pad" ? /\[AutoPilot\] (PASS|FAIL) pad test/ : /\[SaveCheck\] (done|FAIL)/;
  const limit = (mode === "autoplay" ? 40 : 15) * 60000 * (engine === "webkit" ? 2 : 1);
  let shot = 0, fps = [];
  const start = Date.now();
  while (!lines.some((l) => doneRe.test(l)) && Date.now() - start < limit) {
    await sleep(mode === "reload" ? 1000 : 15000);
    if (mode !== "reload" && shot < 80) await page.screenshot({ path: path.join(dir, `${String(++shot).padStart(2, "0")}.jpg`), type: "jpeg", quality: 70 });
    if (mode === "autoplay" && fps.length < 6) {
      fps.push(await page.evaluate(() => new Promise((ok) => {
        let n = 0; const t = performance.now();
        const tick = () => { n++; if (performance.now() - t < 3000) requestAnimationFrame(tick); else ok(n / ((performance.now() - t) / 1000)); };
        requestAnimationFrame(tick);
      })));
    }
  }
  const finished = lines.some((l) => doneRe.test(l));
  if (mode === "reload") await sleep(3000);   // let the IndexedDB sync land before the page goes
  await page.screenshot({ path: path.join(dir, "final.jpg"), type: "jpeg", quality: 80 });
  await page.close();
  if (own) await context.close();
  return { engine, mode, loadMs, renderer, finished, minutes: +((Date.now() - start) / 60000).toFixed(1),
    fps: fps.map((f) => Math.round(f)), errors: errors.slice(0, 20), errorCount: errors.length,
    results: lines.filter((l) => /PASS|FAIL|done|loadedFrom|files after/.test(l)) };
}

// ---------------------------------------------------------------- main
fs.mkdirSync(OUT, { recursive: true });
const server = await serve();
const base = `http://127.0.0.1:${server.address().port}`;
const summary = { build: `${(buildSize() / 1048576).toFixed(1)} MB in Builds/WebGL/Build`, viewport: `${W}x${H}`, runs: [] };
console.log(summary.build);
try {
  for (const engine of engines) {
    let browser;
    try { browser = await launch(engine); }
    catch (e) { summary.runs.push({ engine, error: "couldn't launch: " + e.message.replace(/[║╔╗╚╝═]/g, "").replace(/\s+/g, " ").trim().slice(0, 300) }); continue; }
    for (const mode of only) {
      let r;
      try {
        if (mode === "reload") {
          // One context, two loads: the second must find the save the first one wrote.
          const ctx = await newContext(browser, engine);
          const a = await run(browser, engine, "reload", base, ctx);
          const b = await run(browser, engine, "reload", base, ctx);
          await ctx.close();
          const second = b.results.find((l) => l.includes("loadedFrom")) || "";
          r = { ...b, first: a.results, persisted: /loadedFrom=Main/.test(second) };
        } else r = await run(browser, engine, mode, base);
      } catch (e) { r = { engine, mode, error: e.message.split("\n")[0] }; }
      summary.runs.push(r);
      console.log(JSON.stringify(r));
    }
    await browser.close();
  }
} finally {
  server.close();
  fs.writeFileSync(path.join(OUT, `summary-${engines.join("+")}-${only.join("+")}.json`), JSON.stringify(summary, null, 2));
}
const bad = summary.runs.filter((r) => r.error || !r.finished || r.errorCount > 0
  || (r.mode === "reload" && !r.persisted) || r.results?.some((l) => /FAIL/.test(l)));
console.log(bad.length ? `webtest: ${bad.length} run(s) failed` : "webtest: all runs passed");
process.exit(bad.length ? 1 : 0);
