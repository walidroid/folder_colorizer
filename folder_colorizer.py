"""
Folder Colorizer — Windows 10/11
---------------------------------
• Right-click any folder → "Change Folder Color / Texture"
• Picks a colour or texture, writes desktop.ini, refreshes Explorer

Run as Administrator the FIRST time to install the context-menu entry.
After that, right-clicking folders opens the picker automatically.
"""

import os
import sys
import shutil
import subprocess
import ctypes
import tkinter as tk
from tkinter import ttk, messagebox
from pathlib import Path
import winreg                 # stdlib – Windows only

# ── paths ──────────────────────────────────────────────────────────────────────
APP_DIR   = Path(sys.executable).parent if getattr(sys, "frozen", False) \
            else Path(__file__).resolve().parent
ICONS_DIR = APP_DIR / "icons"

INSTALL_DIR = Path(os.environ.get("LOCALAPPDATA", "")) / "FolderColorizer"
INSTALLED_ICONS = INSTALL_DIR / "icons"

# ── icon catalogue ─────────────────────────────────────────────────────────────
COLORS = {
    "Yellow":   ("#F5C518", "yellow"),
    "Blue":     ("#4A90D9", "blue"),
    "Green":    ("#27AE60", "green"),
    "Red":      ("#E74C3C", "red"),
    "Purple":   ("#8E44AD", "purple"),
    "Orange":   ("#E67E22", "orange"),
    "Pink":     ("#FF69B4", "pink"),
    "Teal":     ("#1ABC9C", "teal"),
    "Gray":     ("#7F8C8D", "gray"),
    "Brown":    ("#795548", "brown"),
    "White":    ("#F0F0F0", "white"),
    "Black":    ("#2C2C2C", "black"),
}

TEXTURES = {
    "Gradient":   ("gradient",   "#D4A843"),
    "Striped":    ("striped",    "#D4A843"),
    "Dots":       ("dots",       "#D4A843"),
    "Carbon":     ("carbon",     "#555555"),
    "Wood":       ("wood",       "#8B5E3C"),
    "Metallic":   ("metallic",   "#A0A0A0"),
    "Neon Blue":  ("neon_blue",  "#001122"),
    "Neon Green": ("neon_green", "#001108"),
    "Neon Pink":  ("neon_pink",  "#110011"),
}

REGISTRY_KEY  = r"Directory\shell\FolderColorizer"
REGISTRY_CMD  = r"Directory\shell\FolderColorizer\command"


# ═══════════════════════════════════════════════════════════════════════════════
#  Registry helpers
# ═══════════════════════════════════════════════════════════════════════════════

def is_admin():
    try:
        return ctypes.windll.shell32.IsUserAnAdmin()
    except Exception:
        return False


def install_context_menu():
    """Add 'Change Folder Color / Texture' to the folder right-click menu."""
    exe = str(INSTALL_DIR / "folder_colorizer.exe") \
          if (INSTALL_DIR / "folder_colorizer.exe").exists() \
          else sys.executable

    app_script = str(INSTALL_DIR / "folder_colorizer.py") \
                 if (INSTALL_DIR / "folder_colorizer.py").exists() \
                 else str(__file__)

    # For .py distribution: use pythonw so no console flashes up
    if exe.lower().endswith("python.exe") or exe.lower().endswith("pythonw.exe"):
        pythonw = exe.replace("python.exe", "pythonw.exe")
        if os.path.exists(pythonw):
            exe = pythonw
        cmd_value = f'"{exe}" "{app_script}" "%1"'
    else:
        cmd_value = f'"{exe}" "%1"'

    # Icon for the menu entry itself
    menu_icon = str(INSTALLED_ICONS / "orange.ico")

    with winreg.CreateKey(winreg.HKEY_CLASSES_ROOT, REGISTRY_KEY) as k:
        winreg.SetValueEx(k, "",      0, winreg.REG_SZ, "Change Folder Color / Texture")
        winreg.SetValueEx(k, "Icon",  0, winreg.REG_SZ, menu_icon)

    with winreg.CreateKey(winreg.HKEY_CLASSES_ROOT, REGISTRY_CMD) as k:
        winreg.SetValueEx(k, "", 0, winreg.REG_SZ, cmd_value)

    print("Context menu installed.")


def uninstall_context_menu():
    """Remove the context menu entry."""
    try:
        winreg.DeleteKey(winreg.HKEY_CLASSES_ROOT, REGISTRY_CMD)
        winreg.DeleteKey(winreg.HKEY_CLASSES_ROOT, REGISTRY_KEY)
        print("Context menu removed.")
    except FileNotFoundError:
        print("Context menu entry not found (already removed?).")


def context_menu_installed() -> bool:
    try:
        winreg.OpenKey(winreg.HKEY_CLASSES_ROOT, REGISTRY_KEY)
        return True
    except FileNotFoundError:
        return False


# ═══════════════════════════════════════════════════════════════════════════════
#  Folder icon application
# ═══════════════════════════════════════════════════════════════════════════════

def apply_icon(folder_path: str, icon_name: str):
    """
    Write / update desktop.ini in *folder_path* to use *icon_name*.ico,
    set folder attributes, then tell Explorer to refresh.
    """
    folder   = Path(folder_path).resolve()
    icon_src = INSTALLED_ICONS / f"{icon_name}.ico"

    if not icon_src.exists():
        # Fall back to source icons (dev mode)
        icon_src = ICONS_DIR / f"{icon_name}.ico"
    if not icon_src.exists():
        messagebox.showerror("Icon not found",
                             f"Cannot find icon file:\n{icon_src}")
        return False

    # Copy icon next to the folder so it works portably (optional but robust)
    local_icon_dir  = folder / ".folder_icons"
    local_icon_dir.mkdir(exist_ok=True)
    local_icon_path = local_icon_dir / f"{icon_name}.ico"
    shutil.copy2(icon_src, local_icon_path)

    # Hide the icon cache directory
    try:
        subprocess.run(["attrib", "+H", "+S", str(local_icon_dir)],
                       check=False, capture_output=True)
    except Exception:
        pass

    # Write desktop.ini
    ini_path = folder / "desktop.ini"
    ini_content = (
        "[.ShellClassInfo]\n"
        f"IconResource={local_icon_path},0\n"
        "IconIndex=0\n"
        "[ViewState]\n"
        "Mode=\nVid=\nFolderType=Generic\n"
    )
    try:
        # Remove read-only / system before writing
        if ini_path.exists():
            subprocess.run(["attrib", "-R", "-S", "-H", str(ini_path)],
                           check=False, capture_output=True)
        ini_path.write_text(ini_content, encoding="utf-8")
        subprocess.run(["attrib", "+R", "+S", "+H", str(ini_path)],
                       check=False, capture_output=True)
    except PermissionError as e:
        messagebox.showerror("Permission Error",
                             f"Could not write desktop.ini:\n{e}\n\n"
                             "Try running as Administrator.")
        return False

    # Make the folder itself System (required for desktop.ini to work)
    subprocess.run(["attrib", "+R", "+S", str(folder)],
                   check=False, capture_output=True)

    # Refresh Explorer shell
    _refresh_explorer(str(folder))
    return True


def reset_icon(folder_path: str):
    """Remove custom icon and restore default folder appearance."""
    folder   = Path(folder_path).resolve()
    ini_path = folder / "desktop.ini"

    if ini_path.exists():
        subprocess.run(["attrib", "-R", "-S", "-H", str(ini_path)],
                       check=False, capture_output=True)
        ini_path.unlink(missing_ok=True)

    local_icon_dir = folder / ".folder_icons"
    if local_icon_dir.exists():
        subprocess.run(["attrib", "-H", "-S", str(local_icon_dir)],
                       check=False, capture_output=True)
        shutil.rmtree(local_icon_dir, ignore_errors=True)

    subprocess.run(["attrib", "-R", "-S", str(folder)],
                   check=False, capture_output=True)
    _refresh_explorer(str(folder))


def _refresh_explorer(path: str):
    """Notify the shell that a folder's attributes changed."""
    try:
        SHCNE_UPDATEDIR = 0x00001000
        SHCNF_PATHW     = 0x0005
        ctypes.windll.shell32.SHChangeNotify(
            SHCNE_UPDATEDIR, SHCNF_PATHW,
            ctypes.c_wchar_p(path), None
        )
    except Exception:
        pass


# ═══════════════════════════════════════════════════════════════════════════════
#  GUI
# ═══════════════════════════════════════════════════════════════════════════════

SWATCH = 52   # size of each colour swatch in px
COLS   = 6    # swatches per row

class FolderColorizerApp(tk.Tk):
    def __init__(self, folder_path: str | None = None):
        super().__init__()
        self.folder_path = folder_path
        self.title("Folder Colorizer")
        self.resizable(False, False)
        self.configure(bg="#1E1E2E")
        self._build_ui()
        self._center()

    # ── layout ────────────────────────────────────────────────────────────────
    def _build_ui(self):
        # ── header ──
        hdr = tk.Frame(self, bg="#13131F", pady=8)
        hdr.pack(fill="x")
        tk.Label(hdr, text="🎨  Folder Colorizer",
                 font=("Segoe UI", 14, "bold"),
                 fg="#E0E0F0", bg="#13131F").pack()

        if self.folder_path:
            short = (self.folder_path[:55] + "…") \
                    if len(self.folder_path) > 58 else self.folder_path
            tk.Label(hdr, text=short, font=("Segoe UI", 8),
                     fg="#888", bg="#13131F").pack()

        # ── tab strip ──
        nb = ttk.Notebook(self)
        nb.pack(fill="both", expand=True, padx=12, pady=(10, 4))
        self._style_notebook(nb)

        tab_colors   = tk.Frame(nb, bg="#1E1E2E", padx=10, pady=10)
        tab_textures = tk.Frame(nb, bg="#1E1E2E", padx=10, pady=10)
        nb.add(tab_colors,   text="  Colors  ")
        nb.add(tab_textures, text="  Textures  ")

        self._build_color_tab(tab_colors)
        self._build_texture_tab(tab_textures)

        # ── bottom bar ──
        bar = tk.Frame(self, bg="#13131F", pady=8)
        bar.pack(fill="x")

        self._btn(bar, "↩  Reset to Default",
                  self._reset, "#555", "#FFF").pack(side="left", padx=12)

        if not self.folder_path:
            # launched standalone → show install / uninstall
            self._btn(bar, "⚙  Install Right-Click Menu",
                      self._install, "#27AE60", "#FFF").pack(side="right", padx=4)
            self._btn(bar, "✕  Uninstall",
                      self._uninstall, "#E74C3C", "#FFF").pack(side="right", padx=4)

        status_text = "✔ Context menu installed" \
                      if context_menu_installed() \
                      else "⚠ Context menu NOT installed (run as Admin to install)"
        tk.Label(bar, text=status_text, font=("Segoe UI", 7),
                 fg="#888", bg="#13131F").pack(side="right", padx=12)

    def _build_color_tab(self, parent):
        tk.Label(parent, text="Choose a colour:",
                 font=("Segoe UI", 9), fg="#AAA", bg="#1E1E2E").pack(anchor="w")
        grid = tk.Frame(parent, bg="#1E1E2E")
        grid.pack(pady=6)

        for i, (label, (hex_col, icon_name)) in enumerate(COLORS.items()):
            col_frame = tk.Frame(grid, bg="#1E1E2E")
            col_frame.grid(row=i // COLS, column=i % COLS, padx=5, pady=5)
            self._swatch(col_frame, hex_col, icon_name, label)

    def _build_texture_tab(self, parent):
        tk.Label(parent, text="Choose a texture:",
                 font=("Segoe UI", 9), fg="#AAA", bg="#1E1E2E").pack(anchor="w")
        grid = tk.Frame(parent, bg="#1E1E2E")
        grid.pack(pady=6)

        for i, (label, (icon_name, preview_hex)) in enumerate(TEXTURES.items()):
            col_frame = tk.Frame(grid, bg="#1E1E2E")
            col_frame.grid(row=i // COLS, column=i % COLS, padx=5, pady=5)
            self._swatch(col_frame, preview_hex, icon_name, label)

    # ── widgets ───────────────────────────────────────────────────────────────
    def _swatch(self, parent, hex_col, icon_name, label):
        """A coloured square button with a label below it."""
        btn = tk.Canvas(parent, width=SWATCH, height=SWATCH,
                        bg=hex_col, highlightthickness=2,
                        highlightbackground="#333", cursor="hand2")
        btn.pack()
        btn.bind("<Button-1>", lambda e, n=icon_name, l=label: self._pick(n, l))
        btn.bind("<Enter>",    lambda e, b=btn: b.config(highlightbackground="#FFF"))
        btn.bind("<Leave>",    lambda e, b=btn: b.config(highlightbackground="#333"))

        tk.Label(parent, text=label, font=("Segoe UI", 7),
                 fg="#CCC", bg="#1E1E2E", wraplength=60,
                 justify="center").pack()

    def _btn(self, parent, text, cmd, bg, fg):
        return tk.Button(parent, text=text, command=cmd,
                         bg=bg, fg=fg, font=("Segoe UI", 9),
                         relief="flat", padx=10, pady=4,
                         activebackground="#444", cursor="hand2")

    def _style_notebook(self, nb):
        style = ttk.Style()
        style.theme_use("default")
        style.configure("TNotebook",
                        background="#1E1E2E", borderwidth=0, tabmargins=0)
        style.configure("TNotebook.Tab",
                        background="#2C2C3E", foreground="#AAA",
                        padding=[14, 6], font=("Segoe UI", 10))
        style.map("TNotebook.Tab",
                  background=[("selected", "#1E1E2E")],
                  foreground=[("selected", "#FFF")])

    # ── actions ───────────────────────────────────────────────────────────────
    def _pick(self, icon_name: str, label: str):
        if not self.folder_path:
            messagebox.showinfo("No folder selected",
                                "Right-click a folder in Explorer to use this tool.")
            return
        ok = apply_icon(self.folder_path, icon_name)
        if ok:
            messagebox.showinfo("Done ✓",
                                f'Folder color set to "{label}".\n\n'
                                "You may need to press F5 in Explorer to refresh.")
            self.destroy()

    def _reset(self):
        if not self.folder_path:
            messagebox.showinfo("No folder", "Right-click a folder first.")
            return
        reset_icon(self.folder_path)
        messagebox.showinfo("Done ✓",
                            "Folder icon reset to default.\n"
                            "Press F5 in Explorer if needed.")
        self.destroy()

    def _install(self):
        if not is_admin():
            messagebox.showwarning("Administrator required",
                                   "Please re-run this script as Administrator\n"
                                   "to install the context menu entry.")
            return
        try:
            _copy_self_to_install_dir()
            install_context_menu()
            messagebox.showinfo("Installed ✓",
                                "Right-click menu installed!\n\n"
                                "Right-click any folder in Explorer and choose\n"
                                '"Change Folder Color / Texture".')
            self._refresh_status_label()
        except Exception as e:
            messagebox.showerror("Error", str(e))

    def _uninstall(self):
        if not is_admin():
            messagebox.showwarning("Administrator required",
                                   "Please re-run this script as Administrator\n"
                                   "to remove the context menu entry.")
            return
        try:
            uninstall_context_menu()
            messagebox.showinfo("Uninstalled", "Context menu entry removed.")
            self._refresh_status_label()
        except Exception as e:
            messagebox.showerror("Error", str(e))

    def _refresh_status_label(self):
        # Quick rebuild of bottom bar would be cleaner; restart for now
        self.destroy()
        FolderColorizerApp(self.folder_path).mainloop()

    def _center(self):
        self.update_idletasks()
        w, h = self.winfo_width(), self.winfo_height()
        x = (self.winfo_screenwidth()  - w) // 2
        y = (self.winfo_screenheight() - h) // 2
        self.geometry(f"+{x}+{y}")


# ═══════════════════════════════════════════════════════════════════════════════
#  Self-install helper
# ═══════════════════════════════════════════════════════════════════════════════

def _copy_self_to_install_dir():
    """Copy script + icons to %LOCALAPPDATA%\\FolderColorizer."""
    INSTALL_DIR.mkdir(parents=True, exist_ok=True)
    INSTALLED_ICONS.mkdir(parents=True, exist_ok=True)

    # Copy icons
    for ico in ICONS_DIR.glob("*.ico"):
        shutil.copy2(ico, INSTALLED_ICONS / ico.name)

    # Copy script
    dest_py = INSTALL_DIR / "folder_colorizer.py"
    shutil.copy2(__file__, dest_py)

    print(f"Installed to: {INSTALL_DIR}")


# ═══════════════════════════════════════════════════════════════════════════════
#  Entry point
# ═══════════════════════════════════════════════════════════════════════════════

def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else None
    if folder and not os.path.isdir(folder):
        print(f"Not a valid directory: {folder}", file=sys.stderr)
        sys.exit(1)
    app = FolderColorizerApp(folder)
    app.mainloop()


if __name__ == "__main__":
    main()
