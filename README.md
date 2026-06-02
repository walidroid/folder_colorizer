# 🎨 Folder Painter

> Right-click any Windows folder to paint it with a custom color or texture.

![Windows 10+](https://img.shields.io/badge/Windows-10%2B-0078D6?logo=windows)
![.NET 6](https://img.shields.io/badge/.NET-6.0-512BD4?logo=dotnet)
![Single EXE](https://img.shields.io/badge/distribution-single%20EXE-brightgreen)

---

## ✨ Features

| Feature | Details |
|---|---|
| **14 preset colors** | Red, Orange, Yellow, Lime, Green, Teal, Sky Blue, Blue, Indigo, Purple, Pink, Dark, White, Default |
| **Custom color picker** | Full Windows color dialog |
| **6 texture overlays** | None, Dots, Grid, Diagonal, Crosshatch, Brick |
| **Live preview** | See the folder icon before applying |
| **Non-destructive** | Custom icon stored in hidden `.FolderPainter` subfolder |
| **One-click reset** | Restore the default folder icon at any time |
| **Single EXE** | No installation, no .NET runtime required on target machine |
| **Windows 10 / 11** | Full DPI-aware, Per-Monitor v2 |

---

## 🚀 Getting Started

### Option A — Download the pre-built EXE

1. Go to the [**Releases**](../../releases) page.
2. Download `FolderPainter.exe` from the latest release.

### Option B — Build from source

```bash
git clone https://github.com/YOUR_USERNAME/FolderPainter
cd FolderPainter
dotnet publish src/FolderPainter.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/
```

---

## 🔧 Setup (one time)

1. **Run `FolderPainter.exe`** — the Setup window opens.
2. Click **Register** (run as Administrator if Windows asks).
3. Done — the right-click menu entry is now active.

To remove it: run `FolderPainter.exe` again → click **Unregister**.

---

## 🖱 Usage

1. Right-click any folder in Windows Explorer.
2. Choose **🎨 Paint Folder**.
3. Pick a color (or click **+ Custom**) and optionally a texture.
4. Click **Apply** — the folder icon updates immediately.

To reset a folder: open the painter → click **Reset**.

---

## 📁 Project Structure

```
FolderPainter/
├── src/
│   ├── FolderPainter.csproj   # Project file
│   ├── Program.cs             # Entry point
│   ├── MainForm.cs            # Color / texture picker UI + icon engine
│   ├── Controls.cs            # Custom UI controls (swatches, buttons)
│   ├── SetupForm.cs           # Register / unregister context menu
│   └── app.manifest           # DPI awareness + Windows 10/11 compatibility
├── .github/
│   └── workflows/
│       └── build.yml          # GitHub Actions – auto-build & release EXE
└── README.md
```

---

## ⚙️ How it works

When you click **Apply**, Folder Painter:

1. Renders your chosen color + texture into a 6-resolution `.ico` file
   (16 × 16 → 256 × 256, stored as PNG inside ICO for Windows 10+ clarity).
2. Writes a `desktop.ini` in the target folder pointing to the icon.
3. Marks the folder with the `System` attribute so Windows loads the ini.
4. Calls `SHChangeNotify` to tell Explorer to refresh immediately.

Everything is stored in a hidden `.FolderPainter` subfolder — no system files
are modified. Clicking **Reset** deletes that subfolder and the `desktop.ini`.

---

## 🤖 GitHub Actions

Every push to `main`/`master`:
- Builds a **Release** self-contained single-file EXE for `win-x64`.
- Uploads it as a downloadable **Build Artifact**.

Push a tag like `v1.0.0`:
- Everything above, **plus** a new **GitHub Release** is created automatically
  with the EXE attached.

```bash
git tag v1.0.0
git push origin v1.0.0
```

---

## 📋 Requirements

- Windows 10 or Windows 11 (x64)
- No .NET runtime needed — the EXE is fully self-contained

---

## 📄 License

MIT — feel free to use, modify, and distribute.
