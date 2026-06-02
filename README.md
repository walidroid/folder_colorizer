# 🎨 Folder Colorizer

Change any Windows folder's color or texture with a right-click — no bloatware, no subscriptions, no Python required.

Built entirely in native **C++ / Win32** — a single lightweight EXE with no runtime dependencies.

---

## Requirements

- **Windows 10 or 11** (64-bit)
- No Python, no .NET, no runtimes needed

---

## Quick Start

### Option A — Use the installer (recommended)

Download `FolderColorizer-Setup.exe` from the [Releases](../../releases) page and run it. The installer registers the context menu automatically.

### Option B — Manual setup

1. Right-click **`INSTALL.bat`** → **Run as administrator**
2. The app window will open. Click **"⚙ Install Right-Click Menu"**.
3. Done — the context menu is now registered system-wide.

### Step 2 — Use it

Right-click **any folder** in Windows Explorer and choose:

> **Change Folder Color / Texture**

Pick a color or texture from the grid, and the folder icon updates instantly.

---

## Available Colors

| Color | Color | Color | Color |
|-------|-------|-------|-------|
| 🟡 Yellow | 🔵 Blue | 🟢 Green | 🔴 Red |
| 🟣 Purple | 🟠 Orange | 🩷 Pink | 🩵 Teal |
| ⬜ White | ⬛ Black | 🩶 Gray | 🟫 Brown |

## Available Textures

| Texture | Texture | Texture |
|---------|---------|---------|
| Gradient | Striped | Dots |
| Carbon Fiber | Wood Grain | Metallic |
| Neon Blue | Neon Green | Neon Pink |

---

## How It Works

1. Copies a custom `.ico` file into a hidden `.folder_icons` subfolder inside your chosen folder.
2. Writes a `desktop.ini` file (hidden system file) that tells Windows Explorer to use the custom icon.
3. Sends a shell notification (`SHChangeNotify`) so Explorer refreshes immediately.

> **Tip:** If the icon doesn't update immediately, press **F5** in Explorer.

---

## Reset to Default

Right-click the folder → **Change Folder Color / Texture** → click **"↩ Reset to Default"**.

This removes the `desktop.ini` and the `.folder_icons` folder, restoring the normal yellow folder icon.

---

## Uninstall the Right-Click Menu

Run **`UNINSTALL.bat`** as Administrator. This removes the registry entry and optionally removes the installed app files.

---

## File Structure

```
folder_colorizer/
├── src/
│   ├── main.cpp           ← Full C++/Win32 application source
│   └── app.rc             ← Windows resource file (icon + version info)
├── CMakeLists.txt         ← CMake build script
├── INSTALL.bat            ← One-click installer
├── UNINSTALL.bat          ← Removes context menu
├── installer.iss          ← Inno Setup installer script
├── README.md
└── icons/                 ← Pre-built .ico files (33 icons)
    ├── yellow.ico
    ├── blue.ico
    ├── ...
    ├── gradient.ico
    └── neon_pink.ico
```

---

## Building from Source

### Requirements

- CMake 3.20+
- Visual Studio 2019/2022 (with "Desktop development with C++" workload)

### Steps

```powershell
cmake -B build -A x64
cmake --build build --config Release
# Output: build\Release\folder_colorizer.exe
```

To build the installer (requires [Inno Setup 6](https://jrsoftware.org/isinfo.php)):

```powershell
iscc installer.iss
# Output: output\FolderColorizer-Setup-v1.0.0.exe
```

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Context menu doesn't appear | Make sure you ran INSTALL.bat as **Administrator** |
| Icon doesn't change | Press **F5** in Explorer, or log out and back in |
| "Permission Error" on desktop.ini | Run the app as **Administrator** for system/protected folders |

---

## Privacy

This app is 100% local. No internet connection, no telemetry, no cloud.
