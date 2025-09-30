const API = "http://localhost:5005/settings";
let currentSettings = {};

// Position selection handling
document.querySelectorAll(".position-option").forEach((option) => {
  option.addEventListener("click", () => {
    // Remove selected class from all options
    document
      .querySelectorAll(".position-option")
      .forEach((o) => o.classList.remove("selected"));
    // Add selected class to clicked option
    option.classList.add("selected");

    // Update the popup position value
    currentSettings.popupPosition = option.dataset.position;
  });
});

async function loadSettings() {
  try {
    const res = await fetch(API);
    if (!res.ok) throw new Error("Failed to load");
    const s = await res.json();
    currentSettings = s;

    document.getElementById("playerAppearance").value =
      s.playerAppearance || "boxy";
    document.getElementById("theme").value = s.theme || "dark";
    document.getElementById("tintColor").value = s.tintColor || "1";
    document.getElementById("coverStyle").value = s.coverStyle || "picture";
    document.getElementById("magicColors").checked = !!s.magicColors;
    document.getElementById("coverGlow").checked = !!s.coverGlow;
    document.getElementById("alwaysOnTop").checked = !!s.alwaysOnTop;
    document.getElementById("rememberPosition").checked = !!s.rememberPosition;

    // Update position selection
    const selectedPosition = s.popupPosition || "bottom-right";
    document
      .querySelectorAll(".position-option")
      .forEach((o) => o.classList.remove("selected"));
    document
      .querySelector(`[data-position="${selectedPosition}"]`)
      ?.classList.add("selected");

    console.log("Loaded settings:", s);
  } catch (e) {
    alert("Error loading settings: " + e.message);
  }
}

async function saveSettings() {
  const settings = {
    ...currentSettings,
    playerAppearance: document.getElementById("playerAppearance").value,
    theme: document.getElementById("theme").value,
    tintColor: document.getElementById("tintColor").value,
    coverStyle: document.getElementById("coverStyle").value,
    magicColors: document.getElementById("magicColors").checked,
    coverGlow: document.getElementById("coverGlow").checked,
    alwaysOnTop: document.getElementById("alwaysOnTop").checked,
    rememberPosition: document.getElementById("rememberPosition").checked,
    popupPosition:
      document.querySelector(".position-option.selected")?.dataset.position ||
      "bottom-right",
  };

  try {
    const res = await fetch(API, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(settings),
    });
    if (res.ok) {
      alert("Settings saved successfully!");
      currentSettings = settings;
    } else {
      alert("Failed to save settings");
    }
  } catch (e) {
    alert("Error: " + e.message);
  }
}

async function applyPositionNow() {
  // Save current settings first
  await saveSettings();

  // Send a special message to immediately apply position
  const message = {
    type: "applyPositionNow",
    data: {
      popupPosition:
        document.querySelector(".position-option.selected")?.dataset.position ||
        "bottom-right",
    },
  };

  // If we have access to the parent window (C# WebView2)
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.postMessage(JSON.stringify(message));
  }

  alert("Position applied!");
}

// Load on startup
loadSettings();
