#!/usr/bin/env node
// Checks the browser build on phone and tablet profiles, and that desktops don't get the touch
// controls. Serves a site folder (Builds/Pages by default) under /AlibiAndCo/ the way GitHub Pages
// does (no Content-Encoding, so the page unpacks the Brotli files itself), then for each device:
//
//   - loads the page to the game's title and records the load time, the bytes served, the WebGL
//     renderer and the canvas's backing size;
//   - measures memory as it goes: the wasm heap (every WebAssembly.Memory the page makes), the
//     WebGL objects the page creates (textures, buffers and renderbuffers, by their sizes and
//     formats, and how many textures went up uncompressed), and the browser processes' resident
//     memory (the peak of their sum, and each one's own high-water mark, read from /proc);
//   - counts frames for a few seconds at the title (and on the board with --play);
//   - with --play, plays a short session with real touches as a player would (Tools/mobile-play.mjs
//     knows the steps): the first tap must start the sound, the touch controls must show, and the
//     title, case files, write-up and board must all work by tap, drag, hold and pinch.
//
// Devices: iphone, iphone-portrait, ipad, pixel (phones and tablets, landscape unless named),
// chromium and firefox (desktop at 1920x1080: the touch controls must never show).
//
//   node Tools/mobile-check.mjs [--site Builds/Pages] [--device iphone,ipad,pixel] [--play]
//                               [--out Captures/mobile/run] [--port 18431] [--timeout 240]
//
// WebKit is Playwright's (PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core, WEBKIT=/path/to/
// pw_run.sh if it isn't the cached one); Chromium is that playwright-core's cached Chromium; Firefox
// is the system Firefox over WebDriver BiDi (PUPPETEER_CORE, FIREFOX). Every browser runs headless in
// a throwaway profile; the server listens on 127.0.0.1 only and stops at the end.
import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf("--" + name); return i >= 0 && i + 1 < args.length ? args[i + 1] : def; };
const SITE = path.resolve(ROOT, opt("site", "Builds/Pages"));
const DEVICES = opt("device", "iphone,ipad,pixel").split(",");
const PLAY = args.includes("--play");
const OUT = path.resolve(ROOT, opt("out", "Captures/mobile/run"));
const PORT = Number(opt("port", "18431"));
const TIMEOUT = Number(opt("timeout", "240")) * 1000;
const LOG_CAP = 2 * 1024 * 1024;
const sleep = (ms) => new Promise((ok) => setTimeout(ok, ms));
const req = createRequire(import.meta.url);
function load(names, hint) {
  for (const n of names.filter(Boolean)) { try { return req(n); } catch { /* next */ } }
  throw new Error(hint);
}
const MB = (b) => +(b / 1048576).toFixed(1);

// ---------------------------------------------------------------- the site, as GitHub Pages serves it
const TYPES = { ".html": "text/html; charset=utf-8", ".js": "application/javascript; charset=utf-8", ".png": "image/png",
  ".json": "application/json", ".wasm": "application/wasm", ".css": "text/css; charset=utf-8", ".svg": "image/svg+xml" };
let served = 0;
function serve() {
  const server = http.createServer((rq, res) => {
    let rel = decodeURIComponent(new URL(rq.url, "http://x").pathname);
    if (!rel.startsWith("/AlibiAndCo/")) { res.writeHead(404); res.end(); return; }
    rel = rel.slice("/AlibiAndCo".length);
    if (rel.endsWith("/")) rel += "index.html";
    const file = path.join(SITE, path.normalize(rel));
    if (!file.startsWith(SITE) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) { res.writeHead(404); res.end(); return; }
    const size = fs.statSync(file).size;
    res.writeHead(200, { "Content-Type": TYPES[path.extname(file)] || "application/octet-stream", "Content-Length": size, "Cache-Control": "max-age=600" });
    served += size;
    fs.createReadStream(file).pipe(res);
  });
  return new Promise((ok, bad) => { server.on("error", bad); server.listen(PORT, "127.0.0.1", () => ok(server)); });
}

// ---------------------------------------------------------------- what the page measures about itself
// Installed before the page's own scripts. It reports through the console ("[probe] {...}" four times
// a second), so the check never has to call into the page (an evaluate() counts as a user gesture,
// which would unlock the sound before the first real touch).
const PROBE = `(() => {
  const t0 = performance.now();
  const P = { wasm: 0, wasmPeak: 0, tex: 0, texPeak: 0, buf: 0, bufPeak: 0, rb: 0, rbPeak: 0, gpuPeak: 0,
              texUp: { compressed: 0, compressedMB: 0, plain: 0, plainMB: 0 }, formats: {}, big: [], frames: 0, firstInput: null,
              audio: [], ext: null, canvas: null, dpr: devicePixelRatio, renderer: null };
  window.alibiProbe = P;
  const mems = new Set();
  const M = WebAssembly.Memory;
  const Mem = function (d) { const m = new M(d); mems.add(m); return m; };
  Mem.prototype = M.prototype;
  WebAssembly.Memory = Mem;
  const grab = (r) => { const ex = (r && (r.instance || r).exports) || {}; for (const k in ex) if (ex[k] instanceof M) mems.add(ex[k]); return r; };
  for (const f of ["instantiate", "instantiateStreaming"]) {
    const o = WebAssembly[f];
    if (o) WebAssembly[f] = function () { return o.apply(this, arguments).then(grab); };
  }

  // WebGL objects by size. Bytes per pixel of the internal formats Unity uses; compressed ones per block.
  const BPP = { 0x8058: 4, 0x8C43: 4, 0x1908: 4, 0x1907: 4, 0x881A: 8, 0x8814: 16, 0x8C3A: 4, 0x8051: 4, 0x8229: 1, 0x822B: 2,
    0x822D: 2, 0x822F: 4, 0x822E: 4, 0x8230: 8, 0x88F0: 4, 0x81A6: 4, 0x8CAC: 4, 0x8CAD: 8, 0x81A5: 2, 0x8059: 4, 0x8056: 2,
    0x8D62: 2, 0x1906: 1, 0x1909: 1, 0x190A: 2, 0x8C41: 4, 0x881B: 6, 0x8815: 12, 0x8D7C: 4, 0x8D8E: 4, 0x8232: 1, 0x8236: 4 };
  const CBPP = { 0x83F0: .5, 0x83F1: .5, 0x83F2: 1, 0x83F3: 1, 0x8C4C: .5, 0x8C4D: .5, 0x8C4E: 1, 0x8C4F: 1, 0x9274: .5, 0x9275: .5,
    0x9278: 1, 0x9279: 1, 0x9270: .5, 0x9272: 1, 0x8D64: .5, 0x8E8C: 1, 0x8E8D: 1, 0x8E8E: 1, 0x8E8F: 1, 0x8DBB: .5, 0x8DBD: 1 };
  for (let i = 0; i < 14; i++) { const b = [16, 12.8, 10.67, 8.53, 7.11, 6.4, 5.33, 4.57, 4, 3.2, 2.67, 2.56, 2.13, 1.78][i] / 16; CBPP[0x93B0 + i] = b; CBPP[0x93D0 + i] = b; }
  const sizes = new Map();   // object -> { key -> bytes }
  const kind = new Map();    // object -> "tex" | "buf" | "rb"
  const set = (obj, k, key, bytes) => {
    if (!obj) return;
    let m = sizes.get(obj); if (!m) { m = new Map(); sizes.set(obj, m); kind.set(obj, k); }
    P[k] += bytes - (m.get(key) || 0);
    m.set(key, bytes);
    P[k + "Peak"] = Math.max(P[k + "Peak"], P[k]);
    P.gpuPeak = Math.max(P.gpuPeak, P.tex + P.buf + P.rb);
  };
  const drop = (obj) => { const m = sizes.get(obj); if (!m) return; for (const b of m.values()) P[kind.get(obj)] -= b; sizes.delete(obj); kind.delete(obj); };
  const st = new WeakMap();
  const S = (gl) => { let s = st.get(gl); if (!s) { s = { unit: 0, tex: {}, buf: {}, rb: null }; st.set(gl, s); } return s; };
  const tex = (gl, target) => { const s = S(gl); const t = (target >= 0x8515 && target <= 0x851A) ? 0x8513 : target; return s.tex[s.unit + ":" + t]; };
  const fmt = (f) => { const k = "0x" + f.toString(16); P.formats[k] = (P.formats[k] || 0) + 1; };
  const note = (w, h, f, compressed, bytes) => {
    const u = P.texUp; if (compressed) { u.compressed++; u.compressedMB += bytes / 1048576; } else { u.plain++; u.plainMB += bytes / 1048576; }
    if (bytes >= 4 * 1048576) P.big.push(w + "x" + h + " 0x" + f.toString(16) + (compressed ? " compressed" : "") + " " + (bytes / 1048576).toFixed(1) + " MB");
  };
  const C = window.WebGL2RenderingContext;
  if (C) {
    const p = C.prototype, wrap = (name, fn) => { const o = p[name]; p[name] = function () { try { fn.call(this, arguments); } catch {} return o.apply(this, arguments); }; };
    wrap("activeTexture", function (a) { S(this).unit = a[0] - 0x84C0; });
    wrap("bindTexture", function (a) { S(this).tex[S(this).unit + ":" + a[0]] = a[1]; });
    wrap("bindBuffer", function (a) { S(this).buf[a[0]] = a[1]; });
    wrap("bindRenderbuffer", function (a) { S(this).rb = a[1]; });
    wrap("deleteTexture", function (a) { drop(a[0]); });
    wrap("deleteBuffer", function (a) { drop(a[0]); });
    wrap("deleteRenderbuffer", function (a) { drop(a[0]); });
    wrap("texImage2D", function (a) {
      if (a.length < 8) { const img = a[5]; const w = img.width || img.videoWidth || 0, h = img.height || img.videoHeight || 0; set(tex(this, a[0]), "tex", a[0] + ":" + a[1], w * h * 4); return; }
      const b = a[3] * a[4] * (BPP[a[2]] || 4); set(tex(this, a[0]), "tex", a[0] + ":" + a[1], b); if (a[1] === 0) note(a[3], a[4], a[2], false, b);
    });
    wrap("texStorage2D", function (a) {
      let w = a[3], h = a[4], b = 0; const c = CBPP[a[2]];
      for (let l = 0; l < a[1]; l++) { b += c ? Math.ceil(w / 4) * Math.ceil(h / 4) * 16 * c : w * h * (BPP[a[2]] || 4); w = Math.max(1, w >> 1); h = Math.max(1, h >> 1); }
      if (a[0] === 0x8513) b *= 6;
      set(tex(this, a[0]), "tex", "storage", b); note(a[3], a[4], a[2], !!c, b); fmt(a[2]);
    });
    wrap("texStorage3D", function (a) { set(tex(this, a[0]), "tex", "storage", a[3] * a[4] * a[5] * (BPP[a[2]] || 4) * 1.34); });
    wrap("texImage3D", function (a) { set(tex(this, a[0]), "tex", a[0] + ":" + a[1], a[3] * a[4] * a[5] * (BPP[a[2]] || 4)); });
    wrap("compressedTexImage2D", function (a) {
      // WebGL 2's form, as Emscripten calls it: (…, HEAPU8, offset, length).
      const b = typeof a[6] === "number" ? a[6] : a.length >= 9 && a[8] ? a[8] : a[6] ? a[6].byteLength - (a[7] || 0) : 0;
      fmt(a[2]);
      set(tex(this, a[0]), "tex", a[0] + ":" + a[1], b); if (a[1] === 0) note(a[3], a[4], a[2], true, b);
    });
    wrap("bufferData", function (a) {
      const b = typeof a[1] === "number" ? a[1] : a.length >= 5 && a[4] ? a[4] : a[1] ? a[1].byteLength - (a[3] || 0) : 0;
      set(S(this).buf[a[0]], "buf", "data", b);
    });
    wrap("renderbufferStorage", function (a) { set(S(this).rb, "rb", "s", a[2] * a[3] * (BPP[a[1]] || 4)); });
    wrap("renderbufferStorageMultisample", function (a) { set(S(this).rb, "rb", "s", a[3] * a[4] * (BPP[a[2]] || 4) * Math.max(1, a[1])); });
  }

  const frame = () => { P.frames++; requestAnimationFrame(frame); };
  requestAnimationFrame(frame);
  for (const ev of ["pointerdown", "mousedown", "keydown", "touchstart"])
    addEventListener(ev, (e) => { if (e.isTrusted && P.firstInput === null) P.firstInput = Math.round(performance.now() - t0); }, true);
  for (const name of ["AudioContext", "webkitAudioContext"]) {
    const A = window[name];
    if (!A) continue;
    window[name] = new Proxy(A, { construct(t, a, nt) {
      const c = Reflect.construct(t, a, nt);
      const i = P.audio.push(c.state) - 1;
      c.addEventListener("statechange", () => { P.audio[i] = c.state; });
      return c;
    } });
  }
  setInterval(() => { try {
    let w = 0; for (const m of mems) w += m.buffer.byteLength;
    P.wasm = w; P.wasmPeak = Math.max(P.wasmPeak, w);
    const cv = document.querySelector("#unity-canvas");
    if (cv) P.canvas = cv.width + "x" + cv.height;
    if (!P.ext) {
      try {
        // A canvas of its own: asking the game's canvas for a context first would choose its settings.
        const gl = document.createElement("canvas").getContext("webgl2");
        if (gl) {
          P.ext = gl.getSupportedExtensions().filter((e) => /compressed/.test(e)).map((e) => e.replace(/^WEBGL_compressed_texture_|^EXT_texture_compression_/, ""));
          const d = gl.getExtension("WEBGL_debug_renderer_info");
          P.renderer = d ? gl.getParameter(d.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
        }
      } catch {}
    }
    const r = { t: Math.round(performance.now() - t0), wasm: P.wasm, wasmPeak: P.wasmPeak, tex: Math.round(P.tex), texPeak: Math.round(P.texPeak),
      buf: P.buf, bufPeak: P.bufPeak, rb: P.rb, rbPeak: P.rbPeak, gpuPeak: Math.round(P.gpuPeak), texUp: P.texUp, formats: P.formats, big: P.big.slice(0, 12),
      frames: P.frames, firstInput: P.firstInput, audio: P.audio, ext: P.ext, canvas: P.canvas, dpr: devicePixelRatio, renderer: P.renderer,
      heap: performance.memory ? performance.memory.usedJSHeapSize : null, view: innerWidth + "x" + innerHeight,
      touchUi: document.documentElement.classList.contains("touch") };
    console.log("[probe] " + JSON.stringify(r));
  } catch (e) { console.log("[probe-error] " + e); } }, 250);
})();`;

// WebKit only, this machine only: its GStreamer has no MP4 demuxer (qtdemux) and no audio sink
// (autoaudiosink), both in gst-plugins-good, which isn't installed. So Playwright's Linux WebKit
// can neither decode the game's sound (AAC in MP4, which Safari decodes natively) nor open an
// output for an AudioContext, and either one takes the whole tab down. In WebKit runs the page gets
// a stand-in AudioContext: an OfflineAudioContext (which needs no output) that starts suspended and
// runs once resume() is called after a user gesture (navigator.userActivation), and whose decodeAudioData() returns a second of silence, and <audio>
// elements that never load. It can show
// that the game asks for sound on the first touch, not that Safari grants it. --real-audio turns it off.
const WEBKIT_AUDIO = `(() => {
  const Off = window.OfflineAudioContext;
  if (!Off) return;
  function StandIn(opts) {
    const ctx = new Off({ numberOfChannels: 2, length: 44100, sampleRate: (opts && opts.sampleRate) || 44100 });
    let state = "suspended";
    const to = (s) => { if (state === s) return Promise.resolve(); state = s; ctx.dispatchEvent(new Event("statechange")); return Promise.resolve(); };
    Object.defineProperty(ctx, "state", { get: () => state });
    Object.defineProperty(ctx, "baseLatency", { get: () => 0.01 });
    Object.defineProperty(ctx, "outputLatency", { get: () => 0.02 });
    // Like Safari: no sound until the page has had a user gesture.
    ctx.resume = () => navigator.userActivation && !navigator.userActivation.hasBeenActive ? Promise.resolve() : to("running");
    ctx.suspend = () => to("suspended");
    ctx.close = () => to("closed");
    ctx.createMediaElementSource = (el) => { const g = ctx.createGain(); g.mediaElement = el; return g; };
    ctx.decodeAudioData = function (data, ok) {
      const buf = ctx.createBuffer(2, ctx.sampleRate, ctx.sampleRate);
      if (ok) setTimeout(() => ok(buf), 0);
      return Promise.resolve(buf);
    };
    return ctx;
  }
  window.AudioContext = window.webkitAudioContext = StandIn;
  // Unity plays its streamed clips through <audio> elements fed to the context; here they never load.
  class StandInAudio extends EventTarget {
    constructor() { super(); Object.assign(this, { src: "", preload: "", autoplay: false, loop: false, currentTime: 0, duration: 1, paused: true, volume: 1, muted: false, playbackRate: 1, readyState: 4 }); }
    play() { this.paused = false; return Promise.resolve(); }
    pause() { this.paused = true; }
    load() {}
    removeAttribute(n) { if (n === "src") this.src = ""; }
    setAttribute(n, v) { this[n] = v; }
    canPlayType() { return "maybe"; }
  }
  window.Audio = StandInAudio;
  window.alibiAudioStandIn = true;
})();`;

// Phones' GPUs: Playwright's profiles run on this desktop GPU, which offers the desktop texture
// formats (S3TC/DXT, BPTC, RGTC) most phones don't have. Phone and tablet runs hide them, so the page
// and the game see what an iPhone or a Mali/Adreno phone offers (ASTC and ETC). --desktop-formats keeps them.
const PHONE_FORMATS = `(() => {
  const hide = /s3tc|bptc|rgtc/i;
  for (const C of [window.WebGL2RenderingContext, window.WebGLRenderingContext]) {
    if (!C) continue;
    const ext = C.prototype.getExtension, list = C.prototype.getSupportedExtensions;
    C.prototype.getExtension = function (n) { return hide.test(n) ? null : ext.call(this, n); };
    C.prototype.getSupportedExtensions = function () { const l = list.call(this); return l && l.filter((n) => !hide.test(n)); };
  }
})();`;

// ---------------------------------------------------------------- the browser processes' memory
function descendants(root) {
  const parent = new Map();
  for (const d of fs.readdirSync("/proc")) {
    if (!/^\d+$/.test(d)) continue;
    try { const s = fs.readFileSync(`/proc/${d}/stat`, "utf8"); parent.set(Number(d), Number(s.slice(s.lastIndexOf(")") + 2).split(" ")[1])); } catch { /* gone */ }
  }
  const out = [];
  const walk = (p) => { for (const [c, pp] of parent) if (pp === p) { out.push(c); walk(c); } };
  walk(root);
  return out;
}
function procInfo(pid) {
  try {
    const s = fs.readFileSync(`/proc/${pid}/status`, "utf8");
    const kb = (k) => Number((new RegExp(`^${k}:\\s+(\\d+)`, "m").exec(s) || [0, 0])[1]) * 1024;
    const cmd = fs.readFileSync(`/proc/${pid}/cmdline`, "utf8").split("\0");
    const name = fs.readFileSync(`/proc/${pid}/comm`, "utf8").trim();
    const type = ((/--type=(\S+)/.exec(cmd.join(" ")) || [])[1] || "");
    return { name: type ? `${name}:${type}` : name, rss: kb("VmRSS"), hwm: kb("VmHWM") };
  } catch { return null; }
}
class Meter {
  constructor() { this.peakSum = 0; this.peakAt = null; this.byPid = new Map(); this.timer = setInterval(() => this.sample(), 400); this.t0 = Date.now(); }
  sample() {
    let sum = 0;
    for (const pid of descendants(process.pid)) {
      const i = procInfo(pid);
      if (!i || /^(pw_run\.sh|bash|sh|cat|node)$/.test(i.name)) continue;
      sum += i.rss;
      const k = this.byPid.get(pid) || { name: i.name, hwm: 0, rss: 0 };
      k.hwm = Math.max(k.hwm, i.hwm); k.rss = Math.max(k.rss, i.rss);
      this.byPid.set(pid, k);
    }
    if (sum > this.peakSum) { this.peakSum = sum; this.peakAt = Date.now() - this.t0; }
  }
  stop() {
    clearInterval(this.timer);
    this.sample();
    const procs = [...this.byPid.values()].filter((p) => p.hwm > 30 * 1048576).sort((a, b) => b.hwm - a.hwm)
      .map((p) => `${p.name} ${MB(p.hwm)} MB`);
    return { peakRssSumMB: MB(this.peakSum), peakAtSeconds: +(this.peakAt / 1000).toFixed(1), highWaterMarks: procs };
  }
}

// ---------------------------------------------------------------- browsers
const PROFILES = {
  iphone: { engine: "webkit", device: "iPhone 15 landscape" },
  "iphone-portrait": { engine: "webkit", device: "iPhone 15" },
  ipad: { engine: "webkit", device: "iPad Pro 11 landscape" },
  "ipad-portrait": { engine: "webkit", device: "iPad Pro 11" },
  pixel: { engine: "chromium", device: "Pixel 7 landscape" },
  "pixel-portrait": { engine: "chromium", device: "Pixel 7" },
  chromium: { engine: "chromium", desktop: true },
  firefox: { engine: "firefox", desktop: true },
};

async function launch(name) {
  const prof = PROFILES[name];
  if (!prof) throw new Error("unknown device " + name);
  if (prof.engine === "firefox") {
    const puppeteer = load([process.env.PUPPETEER_CORE, "puppeteer-core"], "puppeteer-core not found: set PUPPETEER_CORE");
    const profile = path.join(OUT, name, "profile");
    fs.rmSync(profile, { recursive: true, force: true });
    const browser = await puppeteer.launch({ browser: "firefox", protocol: "webDriverBiDi", headless: true,
      executablePath: process.env.FIREFOX || "/usr/bin/firefox", userDataDir: profile, timeout: 120000 });
    const context = await browser.createBrowserContext();
    return { browser, context, prof, puppet: true, cleanup: () => fs.rmSync(profile, { recursive: true, force: true }) };
  }
  const pw = load([process.env.PLAYWRIGHT_CORE, "playwright-core"], "playwright-core not found: set PLAYWRIGHT_CORE");
  let browser;
  if (prof.engine === "webkit") {
    browser = await pw.webkit.launch({ headless: true, executablePath: process.env.WEBKIT || path.join(process.env.HOME, ".cache/webkit-libs/webkit-2359/pw_run.sh") });
  } else {
    const opts = { headless: true, args: ["--use-angle=gl-egl", "--enable-gpu", "--ignore-gpu-blocklist", "--autoplay-policy=document-user-activation-required"] };
    try { browser = await pw.chromium.launch(opts); }
    catch (e) {
      if (!/Executable doesn't exist/.test(e.message)) throw e;
      const cache = process.env.PLAYWRIGHT_BROWSERS_PATH || path.join(process.env.HOME, ".cache/ms-playwright");
      const found = fs.readdirSync(cache).filter((d) => /^chromium-\d+$/.test(d)).sort((a, b) => Number(b.split("-")[1]) - Number(a.split("-")[1]))
        .map((d) => path.join(cache, d, "chrome-linux64/chrome")).find((f) => fs.existsSync(f));
      if (!found) throw e;
      browser = await pw.chromium.launch({ ...opts, executablePath: found });
    }
  }
  const context = await browser.newContext(prof.desktop ? { viewport: { width: 1920, height: 1080 } } : { ...pw.devices[prof.device] });
  return { browser, context, prof, puppet: false, cleanup: () => {} };
}

// ---------------------------------------------------------------- touches
// Taps are the browser's own (trusted) touches. Drags, holds and pinches use CDP's touch input in
// Chromium; WebKit has no API for those, so they're touch events dispatched on the element under
// the finger (the game and the page treat them the same; they don't count as a user gesture).
function fingers(page, context, engine) {
  let cdp = null;
  const cdpReady = engine === "chromium" ? context.newCDPSession(page).then((s) => (cdp = s)) : Promise.resolve();
  const synth = (type, pts) => page.evaluate(([type, pts]) => {
    const target = document.elementFromPoint(pts.length ? pts[0].x : (window.__lastTouch || { x: 1, y: 1 }).x, pts.length ? pts[0].y : (window.__lastTouch || { x: 1, y: 1 }).y) || document.body;
    // This WebKit won't construct Touch objects for a page: then it's a plain event carrying the same fields.
    let real = true;
    try { new Touch({ identifier: 0, target, clientX: 0, clientY: 0 }); } catch { real = false; }
    const mk = (p, i) => real ? new Touch({ identifier: i, target, clientX: p.x, clientY: p.y, pageX: p.x, pageY: p.y, screenX: p.x, screenY: p.y, radiusX: 8, radiusY: 8, force: 1 })
      : { identifier: i, target, clientX: p.x, clientY: p.y, pageX: p.x, pageY: p.y, screenX: p.x, screenY: p.y, radiusX: 8, radiusY: 8, force: 1 };
    const list = (a) => { if (real) return a; const l = a.slice(); l.item = (i) => l[i] || null; return l; };
    const touches = pts.map(mk);
    const changed = type === "touchend" ? (window.__lastPts || []).map(mk) : touches;
    if (pts.length) { window.__lastTouch = pts[0]; window.__lastPts = pts; }
    let ev;
    if (real) ev = new TouchEvent(type, { bubbles: true, cancelable: true, composed: true, touches, targetTouches: touches, changedTouches: changed });
    else {
      ev = new Event(type, { bubbles: true, cancelable: true, composed: true });
      for (const [k, v] of [["touches", list(type === "touchend" ? [] : touches)], ["targetTouches", list(type === "touchend" ? [] : touches)], ["changedTouches", list(changed)]])
        Object.defineProperty(ev, k, { value: v });
    }
    target.dispatchEvent(ev);
  }, [type, pts]);
  const send = async (type, pts) => {
    await cdpReady;
    if (cdp) await cdp.send("Input.dispatchTouchEvent", { type: { touchstart: "touchStart", touchmove: "touchMove", touchend: "touchEnd" }[type], touchPoints: pts.map((p, i) => ({ x: p.x, y: p.y, id: i })) });
    else await synth(type, pts);
  };
  return {
    // The browser's own tap: down and up at once (a trusted gesture, so it may start the sound).
    quickTap: async (x, y, settle = 900) => { await page.touchscreen.tap(x, y); await sleep(settle); },
    // A finger's tap: down for about a tenth of a second, as a person's is.
    tap: async (x, y, settle = 900) => { await send("touchstart", [{ x, y }]); await sleep(90); await send("touchend", []); await sleep(settle); },
    hold: async (x, y, ms = 900) => { await send("touchstart", [{ x, y }]); await sleep(ms); await send("touchend", []); await sleep(600); },
    drag: async (x0, y0, x1, y1, steps = 14) => {
      await send("touchstart", [{ x: x0, y: y0 }]); await sleep(120);
      for (let i = 1; i <= steps; i++) { await send("touchmove", [{ x: x0 + (x1 - x0) * i / steps, y: y0 + (y1 - y0) * i / steps }]); await sleep(40); }
      await sleep(120); await send("touchend", []); await sleep(800);
    },
    pinch: async (cx, cy, from, to, steps = 12) => {
      const pts = (d) => [{ x: cx - d / 2, y: cy }, { x: cx + d / 2, y: cy }];
      await send("touchstart", pts(from)); await sleep(100);
      for (let i = 1; i <= steps; i++) { await send("touchmove", pts(from + (to - from) * i / steps)); await sleep(40); }
      await send("touchend", []); await sleep(800);
    },
  };
}

// ---------------------------------------------------------------- one device
async function check(name, url) {
  const r = { device: name, ok: false, problems: [] };
  const dir = path.join(OUT, name);
  fs.mkdirSync(dir, { recursive: true });
  const logFile = path.join(dir, "console.log");
  fs.writeFileSync(logFile, "");
  let logged = 0;
  const append = (s) => { if (logged < LOG_CAP) { fs.appendFileSync(logFile, s + "\n"); logged += s.length + 1; } };
  const meter = new Meter();
  let b;
  try { b = await launch(name); }
  catch (e) { meter.stop(); r.problems.push("couldn't launch: " + e.message.split("\n")[0]); return r; }
  const { browser, context, prof, puppet } = b;
  r.engine = prof.engine;
  const lines = [], errors = [];
  let probe = null, page;
  const since = (n, re) => lines.slice(n).find((l) => re.test(l));
  const until = async (n, re, ms) => { const end = Date.now() + ms; while (Date.now() < end) { const l = since(n, re); if (l) return l; await sleep(250); } return null; };
  const shot = async (label) => { try { await page.screenshot({ path: path.join(dir, label + ".png") }); } catch (e) { append("[shot] " + label + ": " + e.message.split("\n")[0]); } };
  const framesOver = async (ms) => { const a = probe; await sleep(ms); const z = probe; return a && z ? +((z.frames - a.frames) / ((z.t - a.t) / 1000)).toFixed(1) : null; };
  try {
    page = await context.newPage();
    if (prof.engine === "webkit" && !args.includes("--real-audio")) { await page.addInitScript(WEBKIT_AUDIO); r.audioStandIn = "OfflineAudioContext (no GStreamer demuxer or sink on this machine)"; }
    if (!prof.desktop && !args.includes("--desktop-formats")) { await page.addInitScript(PHONE_FORMATS); r.textureFormatsOffered = "phone (ASTC, ETC; no S3TC, BPTC or RGTC)"; }
    const probeJs = args.includes("--no-probe") ? "" : PROBE;
    if (puppet) { await page.setViewport({ width: 1920, height: 1080 }); if (probeJs) await page.evaluateOnNewDocument(probeJs); }
    else if (probeJs) await page.addInitScript(probeJs);
    page.on("console", (m) => {
      const t = m.text();
      if (t.startsWith("[probe] ")) { try { probe = JSON.parse(t.slice(8)); } catch { /* partial */ } return; }
      append(`[${m.type()}] ${t}`);
      lines.push(t);
      if (m.type() === "error") errors.push(t);
    });
    page.on("pageerror", (e) => { append("[pageerror] " + (e.message || e)); errors.push("pageerror: " + (e.message || e)); });
    page.on("crash", () => { append("[crash] the page crashed"); r.problems.push("the page crashed"); });
    served = 0;
    const t0 = Date.now();
    await page.goto(url, { waitUntil: "load", timeout: TIMEOUT });
    const seen = await until(0, /^\[Fidelity\] (Low|Medium|High|Ultra):|^\[Page\] /, TIMEOUT);
    r.title = !!seen && !seen.startsWith("[Page]");
    if (!r.title) r.problems.push(seen ? `the page said: ${seen}` : `no title in ${TIMEOUT / 1000} s`);
    else {
      r.loadSeconds = +((Date.now() - t0) / 1000).toFixed(1);
      r.graphics = /^\[Fidelity\] (\w+)/.exec(seen)[1];
    }
    await sleep(3000);
    r.servedMB = MB(served);
    if (r.title) r.fpsTitle = await framesOver(5000);
    r.atTitle = probe && { touchUi: probe.touchUi, audioBeforeInput: probe.audio, firstInput: probe.firstInput };
    if (PLAY && r.title) {
      const { play } = await import("./mobile-play.mjs");
      r.play = await play({ page, prof, name, dir, r, lines, until, shot, framesOver, probe: () => probe, fingers: prof.desktop ? null : fingers(page, context, prof.engine), sleep, append });
    } else await shot("01-title");
  } catch (e) {
    r.problems.push("error: " + e.message.split("\n")[0]);
  } finally {
    await sleep(1200);   // the last probe line
    r.memory = probe && { wasmPeakMB: MB(probe.wasmPeak), wasmNowMB: MB(probe.wasm), webglPeakMB: MB(probe.gpuPeak), texturesPeakMB: MB(probe.texPeak),
      buffersPeakMB: MB(probe.bufPeak), renderbuffersPeakMB: MB(probe.rbPeak), textureUploads: { ...probe.texUp, compressedMB: +probe.texUp.compressedMB.toFixed(1), plainMB: +probe.texUp.plainMB.toFixed(1) },
      textureFormats: probe.formats, bigTextures: probe.big, jsHeapMB: probe.heap ? MB(probe.heap) : null };
    if (probe) Object.assign(r, { canvas: probe.canvas, dpr: probe.dpr, view: probe.view, renderer: probe.renderer, compressedFormats: probe.ext });
    r.processes = meter.stop();
    try { if (page) await shot("final"); } catch { /* gone */ }
    await context.close().catch(() => {});
    await browser.close().catch(() => {});
    b.cleanup();
  }
  r.consoleErrors = errors.slice(0, 20);
  if (errors.length) r.problems.push(`${errors.length} console error(s)`);
  r.ok = !!r.title && r.problems.length === 0;
  r.log = path.relative(ROOT, logFile);
  return r;
}

fs.mkdirSync(OUT, { recursive: true });
const server = await serve();
const url = `http://127.0.0.1:${PORT}/AlibiAndCo/`;
const results = [];
try {
  for (const d of DEVICES) {
    const r = await check(d, url);
    results.push(r);
    console.log(JSON.stringify(r));
  }
} finally { server.close(); }
fs.writeFileSync(path.join(OUT, "summary.json"), JSON.stringify(results, null, 2));
const bad = results.filter((r) => !r.ok);
console.log(bad.length ? `mobile-check: FAIL (${bad.map((r) => r.device).join(", ")})` : `mobile-check: PASS (${results.map((r) => r.device).join(", ")})`);
process.exit(bad.length ? 1 : 0);
