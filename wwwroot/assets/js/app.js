// Clean JavaScript for index.html

let currentState = {
  title: null,
  artist: null,
  album: null,
  durationMs: 0,
  positionMs: 0,
  lastUpdatedMs: 0,
  isPlaying: false,
  albumArt: "",
  volumePercent: null,
};

let currentSettings = {
  theme: "dark",
  playerAppearance: "boxy",
  tintColor: "1",
  magicColors: false,
  coverGlow: false,
  opacity: 0.95,
  cornerRadius: 8,
  borderWidth: 0,
  borderColor: "#E2E8F0",
  shadowIntensity: 0.3,
  backgroundBlur: false,
  customBackgroundColor: null,
  showAlbumArt: true,
  showArtistName: true,
  showAlbumName: true,
  showTrackTime: true,
  showProgressBar: true,
  showVolumeBar: true,
  enableVisualizer: true,
  fontSize: 14,
  textAlignment: "left",
  coverStyle: "picture",
  enableAnimations: true,
  animationSpeed: "normal",
  fadeInOut: true,
};

// ===== INITIALIZATION =====
if (window.chrome && window.chrome.webview) {
  try {
    window.chrome.webview.postMessage(JSON.stringify({ type: "ready" }));
  } catch (e) {
    console.error("Failed to send ready message:", e);
  }
}

function safeParse(s) {
  try {
    return JSON.parse(s);
  } catch {
    return {};
  }
}

// ===== MESSAGE HANDLING =====
if (window.chrome && window.chrome.webview) {
  window.chrome.webview.addEventListener("message", (event) => {
    try {
      const d =
        typeof event.data === "string"
          ? safeParse(event.data)
          : event.data || {};

      // Handle settings messages
      if (d.type === "applySettings" && d.data) {
        currentSettings = { ...currentSettings, ...d.data };
        applySettingsToDOM();
        return;
      }

      // Handle track data
      if (!isSpotifyPayload(d)) return;

      // Handle volume-only updates
      if (
        Object.prototype.hasOwnProperty.call(d, "volumePercent") &&
        Object.keys(d).length === 1
      ) {
        currentState.volumePercent =
          d.volumePercent === undefined || d.volumePercent === null
            ? null
            : Number(d.volumePercent);
        return;
      }

      // Update current state
      updateStateFromPayload(d);
      updateUI();
    } catch (error) {
      console.error("Error processing message:", error);
    }
  });
}

function updateStateFromPayload(d) {
  if (Object.prototype.hasOwnProperty.call(d, "title"))
    currentState.title = d.title || "";
  if (Object.prototype.hasOwnProperty.call(d, "artist"))
    currentState.artist = d.artist || "";
  if (Object.prototype.hasOwnProperty.call(d, "album"))
    currentState.album = d.album || "";
  if (Object.prototype.hasOwnProperty.call(d, "durationMs"))
    currentState.durationMs = d.durationMs || 0;
  if (Object.prototype.hasOwnProperty.call(d, "positionMs"))
    currentState.positionMs = d.positionMs || 0;
  if (Object.prototype.hasOwnProperty.call(d, "lastUpdatedMs"))
    currentState.lastUpdatedMs = d.lastUpdatedMs || Date.now();
  if (Object.prototype.hasOwnProperty.call(d, "isPlaying"))
    currentState.isPlaying = !!d.isPlaying;
  if (Object.prototype.hasOwnProperty.call(d, "albumArt"))
    currentState.albumArt = d.albumArt || "";
  if (Object.prototype.hasOwnProperty.call(d, "volumePercent")) {
    currentState.volumePercent =
      d.volumePercent === undefined || d.volumePercent === null
        ? null
        : Number(d.volumePercent);
  }
}

// ===== COVER STYLE MANAGEMENT =====
function applyCoverStyle(style = "picture") {
  const body = document.body;
  const coverWrap = document.getElementById("coverWrap");
  const cover = document.getElementById("cover");

  if (!coverWrap || !cover) {
    console.error("coverWrap or cover element not found!");
    return;
  }

  console.log("=== APPLYING COVER STYLE ===");
  console.log("Target style:", style);
  console.log("Body classes before:", body.className);

  // Remove ALL existing cover style classes and states
  body.classList.remove(
    "cover-picture",
    "cover-spinning-disc",
    "cover-circle",
    "cover-square",
    "cover-rounded",
    "is-playing",
    "is-paused"
  );

  // Force reset styles
  cover.style.animation = "none";
  cover.style.transform = "none";
  cover.style.borderRadius = "";
  coverWrap.style.borderRadius = "";
  coverWrap.style.overflow = "";

  // Remove existing vinyl grooves
  const existingGrooves = coverWrap.querySelector(".vinyl-grooves");
  if (existingGrooves) {
    existingGrooves.remove();
    console.log("Removed existing vinyl grooves");
  }

  // Add the new style class
  const newClass = `cover-${style}`;
  body.classList.add(newClass);

  console.log("Added class:", newClass);
  console.log("Body classes after:", body.className);

  // Handle style-specific setup
  if (style === "spinning-disc") {
    // Add vinyl grooves for spinning disc
    const vinylGrooves = document.createElement("div");
    vinylGrooves.className = "vinyl-grooves";
    coverWrap.appendChild(vinylGrooves);
    console.log("Added vinyl grooves for spinning disc");
  }

  // Force immediate style application
  void coverWrap.offsetHeight; // Trigger reflow
  void cover.offsetHeight;

  // Check if styles are applied
  setTimeout(() => {
    const computedCoverStyle = getComputedStyle(cover);
    const computedWrapStyle = getComputedStyle(coverWrap);

    console.log("=== STYLE CHECK ===");
    console.log("Cover border-radius:", computedCoverStyle.borderRadius);
    console.log("CoverWrap border-radius:", computedWrapStyle.borderRadius);
    console.log("CoverWrap overflow:", computedWrapStyle.overflow);
    console.log("Cover animation:", computedCoverStyle.animation);

    // Update play state after style is applied
    updateCoverPlayState();
  }, 100);
}

function updateCoverPlayState() {
  const body = document.body;
  const coverWrap = document.getElementById("coverWrap");
  const cover = document.getElementById("cover");

  if (!coverWrap || !cover) return;

  const hasArt = !!(currentState.albumArt && currentState.albumArt.length > 10);
  const isSpinningDisc = body.classList.contains("cover-spinning-disc");

  console.log("=== UPDATE COVER PLAY STATE ===");
  console.log("Current body classes:", body.className);
  console.log("isSpinningDisc:", isSpinningDisc);
  console.log("isPlaying:", currentState.isPlaying);
  console.log("hasArt:", hasArt);

  // Remove existing play state classes
  body.classList.remove("is-playing", "is-paused");
  // Update glow intensity/color on play state change (no heavy sampling here)
  const coverImg2 = document.getElementById("cover");
  if (currentSettings.coverGlow) {
    applyCoverGlowDebounced(
      coverImg2 || currentState.albumArt || null,
      {
        enabled: true,
        magicColors: currentSettings.magicColors,
        tintColor: currentSettings.tintColor,
        isPlaying: currentState.isPlaying,
      },
      80
    );
  }

  // Handle art state
  if (!hasArt) {
    body.classList.add("no-art");
  } else {
    body.classList.remove("no-art");
  }

  // Only apply play state logic for spinning disc
  if (isSpinningDisc) {
    if (currentState.isPlaying && hasArt) {
      body.classList.add("is-playing");
      console.log("Added is-playing class");
    } else {
      body.classList.add("is-paused");
      console.log("Added is-paused class");
    }
  } else {
    // For non-spinning styles, ensure no animation
    cover.style.animation = "none";
    console.log("Non-spinning style - animation disabled");
  }

  console.log("Final body classes:", body.className);

  // Check computed styles after state change
  setTimeout(() => {
    const computedStyle = getComputedStyle(cover);
    console.log("Final cover animation:", computedStyle.animation);
  }, 50);
}

function applyTintColor(colorIndex) {
  const root = document.documentElement;
  const colors = {
    1: { primary: "#3182CE", secondary: "#2B6CB0" },
    2: { primary: "#38A169", secondary: "#2F855A" },
    3: { primary: "#D69E2E", secondary: "#B7791F" },
    4: { primary: "#DD6B20", secondary: "#C05621" },
    5: { primary: "#E53E3E", secondary: "#C53030" },
    6: { primary: "#ED64A6", secondary: "#D53F8C" },
    7: { primary: "#9F7AEA", secondary: "#805AD5" },
    8: { primary: "#805AD5", secondary: "#6B46C1" },
  };

  const color = colors[colorIndex] || colors["1"];
  root.style.setProperty("--accent-a", color.primary);
  root.style.setProperty("--accent-b", color.secondary);
  root.style.setProperty("--accent-contrast", color.secondary);
}

function toggleElementVisibility(elementId, visible) {
  const element = document.getElementById(elementId);
  if (element) {
    element.style.display = visible ? "" : "none";
  }
}

// ===== UI UPDATE =====
function updateUI() {
  // Update content
  if (!currentState.title || currentState.title === "No playing") {
    const titleEl = document.getElementById("title");
    if (titleEl) {
      titleEl.textContent = "No playing";
      titleEl.style.animation = "none";
      titleEl.removeAttribute("data-marquee-name");
    }
    const artistEl = document.getElementById("artist");
    if (artistEl) artistEl.textContent = "";
    setCoverPlaceholder();
    document.body.classList.add("no-playing");
  } else {
    document.body.classList.remove("no-playing");

    const titleEl = document.getElementById("title");
    const artistEl = document.getElementById("artist");

    if (titleEl) titleEl.textContent = currentState.title;
    if (artistEl && currentSettings.showArtistName)
      artistEl.textContent = currentState.artist || "";

    if (currentState.albumArt && currentSettings.showAlbumArt) {
      setCoverSafely(currentState.albumArt);
      if (currentSettings.magicColors) {
        applyAccentFromAlbumArt(currentState.albumArt);
      }
    } else {
      setCoverPlaceholder();
      if (currentSettings.magicColors) {
        applyAccentFromAlbumArt(null);
      }
    }

    if (currentSettings.enableAnimations) {
      setupTitleMarquee();
    }
  }

  // CRITICAL: Update cover play state after all other updates
  updateCoverPlayState();
  updateTimesAndProgress();
}

function setCoverSafely(url) {
  const img = document.getElementById("cover");
  if (!img) return;
  if (!url) {
    setCoverPlaceholder();
    return;
  }
  const same = img.src === url;
  img.onerror = () => setCoverPlaceholder();

  img.onload = () => {
    // Debounced glow application (uses sampleDominantColor internally if magicColors)
    applyCoverGlowDebounced(
      img,
      {
        enabled: currentSettings.coverGlow,
        magicColors: currentSettings.magicColors,
        tintColor: currentSettings.tintColor,
        isPlaying: currentState.isPlaying,
      },
      180
    );

    // Ensure disc visibility + play state updated after image loaded
    if (document.body.classList.contains("cover-spinning-disc")) {
      const disc = document.getElementById("disc");
      if (disc) disc.style.display = "";
    }
    updateCoverPlayState();
  };

  if (!same) {
    img.src = url;
  } else {
    if (img.complete) img.onload();
  }
}

function setCoverPlaceholder() {
  const img = document.getElementById("cover");
  if (!img) return;

  img.onerror = null;
  img.src =
    'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64"><rect width="64" height="64" fill="%23333" rx="8"/></svg>';
  updateCoverPlayState();
}

// ===== TIME FORMATTING AND PROGRESS =====
function formatTime(seconds) {
  if (isNaN(seconds) || seconds < 0) return "0:00:00";
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = Math.floor(seconds % 60);
  return `${h}:${m.toString().padStart(2, "0")}:${s
    .toString()
    .padStart(2, "0")}`;
}

let rafId = null;

function updateTimesAndProgress() {
  const nowMs = Date.now();
  let posMs = currentState.positionMs || 0;
  if (currentState.isPlaying && currentState.lastUpdatedMs) {
    const delta = nowMs - currentState.lastUpdatedMs;
    posMs = posMs + delta;
  }
  const durMs = currentState.durationMs || 0;
  if (durMs > 0 && posMs > durMs) posMs = durMs;

  const posS = Math.floor(posMs / 1000 || 0);
  const durS = Math.floor(durMs / 1000 || 0);

  const posEl = document.getElementById("pos");
  const durEl = document.getElementById("dur");
  const fillEl = document.getElementById("progressFill");

  if (posEl) posEl.textContent = formatTime(posS);
  if (durEl) durEl.textContent = formatTime(durS);
  const pct = durMs ? Math.max(0, Math.min(100, (posMs / durMs) * 100)) : 0;
  if (fillEl) fillEl.style.width = pct + "%";
}

function startProgressLoop() {
  if (rafId) return;
  const step = () => {
    updateTimesAndProgress();
    rafId = requestAnimationFrame(step);
  };
  rafId = requestAnimationFrame(step);
}

function stopProgressLoop() {
  if (rafId) {
    cancelAnimationFrame(rafId);
    rafId = null;
  }
}

setInterval(() => {
  if (currentState.isPlaying) startProgressLoop();
  else {
    updateTimesAndProgress();
    stopProgressLoop();
  }
}, 500);

// ===== SPOTIFY FILTER =====
const ACCEPT_ONLY_SPOTIFY = false;

function isSpotifyPayload(d) {
  if (!ACCEPT_ONLY_SPOTIFY) return true;
  if (!d || typeof d !== "object") return false;

  const keys = ["source", "app", "service", "provider", "appName"];
  for (const k of keys) {
    if (d.hasOwnProperty(k) && String(d[k]).toLowerCase().includes("spotify"))
      return true;
  }

  if (typeof d.uri === "string" && d.uri.toLowerCase().startsWith("spotify:"))
    return true;

  const art = (d.albumArt || d.cover || d.thumbnail || "")
    .toString()
    .toLowerCase();
  if (art.includes("scdn") || art.includes("spotify")) return true;

  return false;
}
function getCurrentCoverStyle() {
  const body = document.body;
  if (body.classList.contains("cover-spinning-disc")) return "spinning-disc";
  if (body.classList.contains("cover-circle")) return "circle";
  if (body.classList.contains("cover-square")) return "square";
  if (body.classList.contains("cover-rounded")) return "rounded";
  return "picture"; // default
}

// Add debug function to manually test styles
function debugTestStyle(style) {
  console.log(`\n=== DEBUG TEST: ${style.toUpperCase()} ===`);
  applyCoverStyle(style);

  // Add fake album art for testing
  if (!currentState.albumArt) {
    currentState.albumArt =
      "data:image/svg+xml;base64," +
      btoa(`
      <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64">
        <rect width="64" height="64" fill="#${Math.floor(
          Math.random() * 16777215
        ).toString(16)}" rx="8"/>
        <text x="32" y="32" text-anchor="middle" dy=".3em" fill="white" font-size="8">${style.toUpperCase()}</text>
      </svg>
    `);
  }

  updateUI();
}

window.debugTestStyle = debugTestStyle;
window.debugApplyCoverStyle = applyCoverStyle;
window.debugUpdateCoverPlayState = updateCoverPlayState;
window.debugGetCurrentStyle = getCurrentCoverStyle;
// ===== DEMO FUNCTION FOR TESTING =====
// Enhanced demo function for better testing
function togglePlayState() {
  console.log("\n=== TOGGLE PLAY STATE ===");

  currentState.isPlaying = !currentState.isPlaying;

  if (!currentState.title || currentState.title === "No playing") {
    currentState.title = `Demo Song - ${getCurrentCoverStyle().toUpperCase()} Test`;
    currentState.artist = "Demo Artist";
    currentState.album = "Demo Album";
    currentState.durationMs = 180000;
    currentState.positionMs = 30000;
    currentState.lastUpdatedMs = Date.now();
    currentState.albumArt =
      "data:image/svg+xml;base64," +
      btoa(`
      <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64">
        <defs>
          <radialGradient id="grad" cx="50%" cy="50%">
            <stop offset="0%" style="stop-color:#FF646E;stop-opacity:1" />
            <stop offset="100%" style="stop-color:#FF4246;stop-opacity:1" />
          </radialGradient>
        </defs>
        <rect width="64" height="64" fill="url(#grad)" rx="8"/>
        <circle cx="32" cy="32" r="16" fill="#fff" opacity="0.3"/>
        <circle cx="32" cy="32" r="3" fill="#333"/>
        <text x="32" y="45" text-anchor="middle" fill="white" font-size="6">${getCurrentCoverStyle()}</text>
      </svg>
    `);
  }

  updateUI();
  console.log(
    "Play state toggled:",
    currentState.isPlaying,
    "Style:",
    getCurrentCoverStyle()
  );
}

// Enhanced applySettingsToDOM
function applySettingsToDOM() {
  const body = document.body;
  const root = document.documentElement;

  console.log("\n=== APPLY SETTINGS TO DOM ===");
  console.log("Settings:", currentSettings);

  // Apply theme
  body.className = body.className.replace(/theme-\w+/g, "");
  body.classList.add(`theme-${currentSettings.theme || "dark"}`);

  // Apply appearance
  body.className = body.className.replace(/appearance-\w+/g, "");
  body.classList.add(
    `appearance-${currentSettings.playerAppearance || "boxy"}`
  );

  // Apply cover style - CRITICAL
  const targetStyle = currentSettings.coverStyle || "picture";
  console.log("Applying cover style:", targetStyle);
  applyCoverStyle(targetStyle);

  // Apply CSS custom properties
  root.style.setProperty("--opacity", currentSettings.opacity || 1.0);
  root.style.setProperty(
    "--corner-radius",
    `${currentSettings.cornerRadius || 8}px`
  );
  root.style.setProperty(
    "--border-width",
    `${currentSettings.borderWidth || 0}px`
  );
  root.style.setProperty(
    "--border-color",
    currentSettings.borderColor || "#E2E8F0"
  );
  root.style.setProperty(
    "--shadow-intensity",
    currentSettings.shadowIntensity || 0.4
  );
  root.style.setProperty("--font-size", `${currentSettings.fontSize || 14}px`);
  root.style.setProperty(
    "--text-alignment",
    currentSettings.textAlignment || "left"
  );

  // Apply tint color if not using magic colors
  if (!currentSettings.magicColors) {
    applyTintColor(currentSettings.tintColor || "1");
  }

  // Apply visual effects
  body.classList.toggle("glow-enabled", currentSettings.coverGlow);
  // ensure glow color is applied immediately when settings change
  // after: body.classList.toggle("glow-enabled", currentSettings.coverGlow);
  const coverImg = document.getElementById("cover");
  if (currentSettings.coverGlow) {
    applyCoverGlowDebounced(
      coverImg || currentState.albumArt || null,
      {
        enabled: true,
        magicColors: currentSettings.magicColors,
        tintColor: currentSettings.tintColor,
        isPlaying: currentState.isPlaying,
      },
      180
    );
  } else {
    // remove any previously set var
    const wrap = document.getElementById("coverWrap");
    if (wrap) wrap.style.removeProperty("--cover-glow-color");
    document.body.classList.remove("glow-enabled");
  }

  body.classList.toggle("blur-enabled", currentSettings.backgroundBlur);

  // Animation settings
  body.classList.toggle(
    "animations-disabled",
    !currentSettings.enableAnimations
  );
  root.style.setProperty(
    "--animation-speed",
    currentSettings.animationSpeed === "slow"
      ? "2s"
      : currentSettings.animationSpeed === "fast"
      ? "0.5s"
      : "1s"
  );

  // Visibility settings
  toggleElementVisibility("album", currentSettings.showAlbumName);
  toggleElementVisibility("progressBar", currentSettings.showProgressBar);
  toggleElementVisibility("visualizerCanvas", currentSettings.enableVisualizer);

  console.log("Settings applied - Final body classes:", body.className);
}

// Add initialization logging
document.addEventListener("DOMContentLoaded", function () {
  console.log("\n=== DOM CONTENT LOADED ===");
  console.log("Initial body classes:", document.body.className);
  console.log("coverWrap element:", document.getElementById("coverWrap"));
  console.log("cover element:", document.getElementById("cover"));

  const coverWrap = document.getElementById("coverWrap");
  if (coverWrap) {
    coverWrap.addEventListener("click", function (event) {
      console.log("Cover clicked - toggling demo play state");
      event.stopPropagation();
      togglePlayState();
    });
  }
});

// ===== TITLE MARQUEE =====
function setupTitleMarquee() {
  const titleEl = document.getElementById("title");
  const container = document.getElementById("titleContainer");
  if (!titleEl || !container) return;

  const textW = titleEl.scrollWidth;
  const containerW = container.clientWidth;

  titleEl.style.animation = "none";

  if (textW > containerW) {
    const distance = textW + containerW;
    const duration = distance / 60;
    const animName = "scrollTitle";

    const keyframes = `
      @keyframes ${animName} {
        0% { transform: translateX(${containerW}px); }
        100% { transform: translateX(-${textW}px); }
      }
    `;

    let styleTag = document.getElementById("title-marquee-style");
    if (!styleTag) {
      styleTag = document.createElement("style");
      styleTag.id = "title-marquee-style";
      document.head.appendChild(styleTag);
    }
    styleTag.innerHTML = keyframes;

    titleEl.style.animation = `${animName} ${duration}s linear 1`;
    titleEl.addEventListener(
      "animationend",
      () => {
        titleEl.style.animation = "none";
        titleEl.style.transform = "translateX(0)";
        setTimeout(setupTitleMarquee, 2000);
      },
      { once: true }
    );
  } else {
    titleEl.style.transform = "translateX(0)";
  }
}

// ===== MAGIC COLORS =====
async function sampleDominantColor(url) {
  return new Promise((resolve) => {
    if (!url) return resolve(null);

    const img = new Image();
    img.crossOrigin = "Anonymous";
    img.onload = () => {
      try {
        const w = 40,
          h = 40;
        const c = document.createElement("canvas");
        c.width = w;
        c.height = h;
        const cx = c.getContext("2d");
        cx.drawImage(img, 0, 0, w, h);
        const data = cx.getImageData(0, 0, w, h).data;

        let r = 0,
          g = 0,
          b = 0,
          count = 0;
        const step = 4 * 3;
        for (let i = 0; i < data.length; i += step) {
          const alpha = data[i + 3];
          if (alpha === 0) continue;
          r += data[i];
          g += data[i + 1];
          b += data[i + 2];
          count++;
        }
        if (count === 0) return resolve(null);
        r = Math.round(r / count);
        g = Math.round(g / count);
        b = Math.round(b / count);
        resolve([r, g, b]);
      } catch (e) {
        resolve(null);
      }
    };
    img.onerror = () => resolve(null);
    img.src = url;
  });
}

function rgbToHsl(r, g, b) {
  r /= 255;
  g /= 255;
  b /= 255;
  const max = Math.max(r, g, b),
    min = Math.min(r, g, b);
  let h = 0,
    s = 0,
    l = (max + min) / 2;
  if (max !== min) {
    const d = max - min;
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    switch (max) {
      case r:
        h = (g - b) / d + (g < b ? 6 : 0);
        break;
      case g:
        h = (b - r) / d + 2;
        break;
      case b:
        h = (r - g) / d + 4;
        break;
    }
    h /= 6;
  }
  return [Math.round(h * 360), Math.round(s * 100), Math.round(l * 100)];
}

function hslString(h, s, l) {
  return `hsl(${h} ${s}% ${l}%)`;
}

function stringToHue(s) {
  let h = 0;
  for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) >>> 0;
  return h % 360;
}

async function applyAccentFromAlbumArt(url) {
  let rgb = null;
  if (url) rgb = await sampleDominantColor(url);

  if (!rgb) {
    const key =
      ((currentState.title || "") + "|" + (currentState.artist || "")).trim() ||
      "default";
    const hue = stringToHue(key);
    document.documentElement.style.setProperty(
      "--accent-a",
      hslString(hue, 60, 45)
    );
    document.documentElement.style.setProperty(
      "--accent-b",
      hslString((hue + 30) % 360, 65, 55)
    );
    document.documentElement.style.setProperty(
      "--accent-contrast",
      hslString(hue, 80, 18)
    );
    return;
  }

  const [r, g, b] = rgb;
  const [h, s, l] = rgbToHsl(r, g, b);
  const toneA = hslString(
    h,
    Math.min(85, Math.round(s * 1.05)),
    Math.max(12, l - 10)
  );
  const toneB = hslString(
    (h + 18) % 360,
    Math.min(90, Math.round(s * 1.15)),
    Math.min(72, l + 12)
  );
  const contrast = hslString(
    h,
    Math.min(95, Math.round(s * 1.3)),
    Math.max(8, l - 30)
  );

  document.documentElement.style.setProperty("--accent-a", toneA);
  document.documentElement.style.setProperty("--accent-b", toneB);
  document.documentElement.style.setProperty("--accent-contrast", contrast);
}

// ===== COVER GLOW: compute color & apply (debounced) =====

/**
 * Map tint index -> rgba string (fallback palette)
 */
const COVER_GLOW_PALETTE = {
  1: "rgba(49,130,206,0.44)", // blue
  2: "rgba(56,161,105,0.44)", // green
  3: "rgba(214,158,46,0.44)", // yellow
  4: "rgba(221,107,32,0.44)", // orange
  5: "rgba(229,62,62,0.44)", // red
  6: "rgba(237,100,166,0.44)", // pink
  7: "rgba(159,122,234,0.44)", // purple
  8: "rgba(128,90,213,0.44)", // indigo
};

/**
 * Convert [r,g,b] + alpha to rgba(...) string
 */
function rgbArrayToRgba(a, alpha = 0.44) {
  if (!a || a.length < 3) return null;
  return `rgba(${a[0]},${a[1]},${a[2]},${alpha})`;
}

/**
 * Compute glow color: if magicColors true -> sample image; else mapping by tintColor.
 * Returns a promise that resolves to rgba(...) string (or null).
 *
 * imgSrc: image URL (must be same-origin or have CORS allowed for sampling),
 * options: { magicColors: bool, tintColor: string|int, alpha: number }
 */
async function computeCoverGlowColor(imgSrc, options = {}) {
  const alpha = typeof options.alpha === "number" ? options.alpha : 0.44;

  if (options.magicColors && imgSrc) {
    try {
      const rgb = await sampleDominantColor(imgSrc); // existing function returns [r,g,b] or null
      if (rgb && rgb.length === 3) {
        return rgbArrayToRgba(rgb, alpha);
      }
    } catch (e) {
      // ignore and fallback to tint mapping
    }
  }

  // fallback: tint palette mapping
  const key = String(options.tintColor ?? currentSettings.tintColor ?? "1");
  return COVER_GLOW_PALETTE[key] || COVER_GLOW_PALETTE["1"];
}

/**
 * Apply glow to coverWrap element.
 * options = { enabled: bool, magicColors: bool, tintColor, isPlaying: bool }
 */
async function applyCoverGlow(imgElementOrSrc, options = {}) {
  try {
    const enabled = !!options.enabled;
    const magic = !!options.magicColors;
    const tint = options.tintColor ?? currentSettings.tintColor;
    const isPlaying = !!options.isPlaying;

    if (!enabled) {
      // remove inline var and ensure class toggles handled elsewhere
      const wrap = document.getElementById("coverWrap");
      if (wrap) wrap.style.removeProperty("--cover-glow-color");
      return;
    }

    const imgSrc =
      typeof imgElementOrSrc === "string"
        ? imgElementOrSrc
        : imgElementOrSrc && imgElementOrSrc.src
        ? imgElementOrSrc.src
        : null;

    // compute color (may await sampleDominantColor)
    const color = await computeCoverGlowColor(imgSrc, {
      magicColors: magic,
      tintColor: tint,
      alpha: 0.44,
    });

    if (!color) return;

    // set CSS variable on coverWrap (prefer local scope to allow multiple widgets)
    const wrap = document.getElementById("coverWrap");
    if (wrap) {
      wrap.style.setProperty("--cover-glow-color", color);
      // ensure body class glow-enabled already toggled by applySettingsToDOM
      // if not toggled, add it temporarily
      // but prefer to respect currentSettings:
      if (currentSettings.coverGlow) {
        document.body.classList.add("glow-enabled");
        // also set .is-playing / .is-paused class to change intensity
        document.body.classList.toggle("is-playing", isPlaying);
        document.body.classList.toggle("is-paused", !isPlaying);
      }
    }
  } catch (e) {
    // silent fail - glow is cosmetic
    console.warn("applyCoverGlow error", e);
  }
}

/* Debounce helper so we don't sample/paint too often */
let _coverGlowDebounceTimer = null;
function applyCoverGlowDebounced(imgElementOrSrc, options = {}, delay = 180) {
  if (_coverGlowDebounceTimer) clearTimeout(_coverGlowDebounceTimer);
  _coverGlowDebounceTimer = setTimeout(() => {
    applyCoverGlow(imgElementOrSrc, options);
  }, delay);
}

// ===== EVENT HANDLERS =====
document.addEventListener("DOMContentLoaded", function () {
  const coverWrap = document.getElementById("coverWrap");
  if (coverWrap) {
    coverWrap.addEventListener("click", function (event) {
      event.stopPropagation();
      togglePlayState();
    });
  }
});

// ===== VISUALIZER =====
const canvas = document.getElementById("visualizerCanvas");
const ctx = canvas ? canvas.getContext("2d") : null;

let dpr = window.devicePixelRatio || 1;
let V_WIDTH = 0,
  V_HEIGHT = 0;

const BAR_COUNT = 16;
const BAR_GAP_RATIO = 0.48;
const SMOOTH = 0.14;
const BASELINE = 0.5;
let barHeights = new Array(BAR_COUNT).fill(0);

let lastFrameTs = 0;
let cachedAccentA = null;
let cachedAccentB = null;
let cachedBarW = null;
let cachedGap = null;
let cachedStartX = null;
let cachedMidY = null;
let cachedVWidth = 0;
let cachedVHeight = 0;

function resizeCanvas() {
  if (!canvas || !ctx) return;
  const rect = canvas.getBoundingClientRect();
  dpr = window.devicePixelRatio || 1;
  V_WIDTH = Math.max(1, Math.floor(rect.width));
  V_HEIGHT = Math.max(1, Math.floor(rect.height));
  canvas.width = Math.floor(V_WIDTH * dpr);
  canvas.height = Math.floor(V_HEIGHT * dpr);
  canvas.style.width = V_WIDTH + "px";
  canvas.style.height = V_HEIGHT + "px";
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
}

function updateCachedGeometry() {
  if (V_WIDTH === cachedVWidth && V_HEIGHT === cachedVHeight) return;
  cachedVWidth = V_WIDTH;
  cachedVHeight = V_HEIGHT;

  const totalGapFactor = BAR_GAP_RATIO * (BAR_COUNT - 1);
  cachedBarW = Math.max(3, Math.floor(V_WIDTH / (BAR_COUNT + totalGapFactor)));
  cachedGap = Math.max(2, Math.floor(cachedBarW * BAR_GAP_RATIO));
  const totalUsed = BAR_COUNT * cachedBarW + (BAR_COUNT - 1) * cachedGap;
  cachedStartX = Math.round((V_WIDTH - totalUsed) / 2);
  cachedMidY = V_HEIGHT * BASELINE;
}

function updateCachedColors() {
  try {
    const cs = getComputedStyle(document.documentElement);
    cachedAccentA =
      cs.getPropertyValue("--accent-a").trim() || "rgba(255,100,110,0.95)";
    cachedAccentB =
      cs.getPropertyValue("--accent-b").trim() || "rgba(255,70,70,0.9)";
  } catch (e) {
    cachedAccentA = "rgba(255,100,110,0.95)";
    cachedAccentB = "rgba(255,70,70,0.9)";
  }
}

function rand(seed) {
  return Math.abs(Math.sin(seed) * 10000) % 1;
}

function drawRoundedRect(ctx, x, y, w, h, r) {
  const radius = Math.min(r, w / 2, h / 2);
  ctx.beginPath();
  ctx.moveTo(x + radius, y);
  ctx.arcTo(x + w, y, x + w, y + h, radius);
  ctx.arcTo(x + w, y + h, x, y + h, radius);
  ctx.arcTo(x, y + h, x, y, radius);
  ctx.arcTo(x, y, x + w, y, radius);
  ctx.closePath();
  ctx.fill();
}

function animateVisualizer(ts) {
  if (!ctx || document.hidden) return requestAnimationFrame(animateVisualizer);

  const currentFPSInterval = currentState.isPlaying ? 1000 / 60 : 1000 / 8;
  if (!lastFrameTs) lastFrameTs = ts;
  const dtMs = ts - lastFrameTs;

  if (dtMs < currentFPSInterval) {
    return requestAnimationFrame(animateVisualizer);
  }
  lastFrameTs = ts;

  updateCachedGeometry();
  updateCachedColors();

  let amplitude;
  const vol = currentState.volumePercent;
  if (vol !== null && vol !== undefined && vol >= 0) {
    amplitude = Math.max(0, Math.min(1, vol / 100));
  } else {
    amplitude = currentState.isPlaying ? 0.56 : 0.06;
  }

  const t = ts / 1000;
  const targets = new Array(BAR_COUNT);

  for (let i = 0; i < BAR_COUNT; i++) {
    const phase = i * 0.45;
    const freq = 1.2 + i * 0.06;
    const wave = Math.abs(Math.sin(t * freq + phase));
    const noise = (Math.sin(t * (9 + i * 0.9) + i) + rand(i + t) * 0.5) * 0.45;
    let v = amplitude * (0.28 + 0.72 * wave) + 0.12 * noise;
    if (!currentState.isPlaying) v *= 0.18;
    targets[i] = v < 0 ? 0 : v > 1 ? 1 : v;
  }

  for (let i = 0; i < BAR_COUNT; i++) {
    barHeights[i] += (targets[i] - barHeights[i]) * SMOOTH;
  }

  ctx.clearRect(0, 0, V_WIDTH, V_HEIGHT);

  const barW = cachedBarW;
  const gap = cachedGap;
  const startX = cachedStartX;
  const midY = cachedMidY;

  ctx.shadowBlur = Math.max(4, Math.round(V_HEIGHT * 0.12));
  ctx.shadowColor = cachedAccentB || "rgba(0,0,0,0.18)";

  for (let i = 0; i < BAR_COUNT; i++) {
    const h = Math.max(1, Math.floor(barHeights[i] * V_HEIGHT));
    const x = startX + i * (barW + gap);

    const half = Math.floor(h / 2);
    const topY = Math.round(midY - half);
    const botY = Math.round(midY + half);

    let grad;
    try {
      grad = ctx.createLinearGradient(0, topY, 0, botY);
      grad.addColorStop(0, cachedAccentA);
      grad.addColorStop(1, cachedAccentB);
    } catch (e) {
      grad = null;
    }
    ctx.fillStyle = grad || "#ccc";

    const radius = Math.max(2, Math.floor(barW / 2));

    drawRoundedRect(ctx, x, topY, barW, Math.max(1, half), radius);
    drawRoundedRect(ctx, x, midY, barW, Math.max(1, half), radius);
  }

  ctx.shadowBlur = 0;
  requestAnimationFrame(animateVisualizer);
}

window.addEventListener("resize", resizeCanvas);
resizeCanvas();
updateCachedColors();
requestAnimationFrame(animateVisualizer);

updateTimesAndProgress();
