/*
 * Folder Colorizer — Windows 10/11  (C++ / Win32)
 * ------------------------------------------------
 * • Right-click any folder → "Change Folder Color / Texture"
 * • Picks a colour or texture, writes desktop.ini, refreshes Explorer
 *
 * Build:  cmake -B build -A x64  &&  cmake --build build --config Release
 * Usage:  folder_colorizer.exe [folder_path]
 */

// UNICODE and _UNICODE are defined by CMakeLists.txt on the command line.
#define _WIN32_WINNT 0x0A00   // Windows 10

#include <windows.h>
#include <windowsx.h>
#include <commctrl.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <shellapi.h>
#include <shobjidl.h>
#include <uxtheme.h>

#include <string>
#include <vector>
#include <array>
#include <fstream>
#include <sstream>
#include <algorithm>
#include <stdexcept>
#include <cstdio>

#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "uxtheme.lib")
#pragma comment(lib, "gdi32.lib")

// ── ManifestComCtl6 ──────────────────────────────────────────────────────────
#pragma comment(linker,"/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' "\
    "version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")

// ── Constants ─────────────────────────────────────────────────────────────────
static const COLORREF BG_DARK     = RGB(0x1E, 0x1E, 0x2E);
static const COLORREF BG_DARKER   = RGB(0x13, 0x13, 0x1F);
static const COLORREF BG_CARD     = RGB(0x2C, 0x2C, 0x3E);
static const COLORREF FG_WHITE    = RGB(0xFF, 0xFF, 0xFF);
static const COLORREF FG_GRAY     = RGB(0xAA, 0xAA, 0xAA);
static const COLORREF FG_DIM      = RGB(0x88, 0x88, 0x88);
static const COLORREF ACCENT_GREEN= RGB(0x27, 0xAE, 0x60);
static const COLORREF ACCENT_RED  = RGB(0xE7, 0x4C, 0x3C);
static const COLORREF SWATCH_BORDER   = RGB(0x44, 0x44, 0x55);
static const COLORREF SWATCH_HOVER    = RGB(0xFF, 0xFF, 0xFF);

static const int SWATCH_SIZE  = 52;   // px
static const int SWATCH_COLS  = 6;
static const int SWATCH_PAD   = 6;
static const int HEADER_H     = 56;
static const int FOOTER_H     = 44;
static const int TAB_CONTENT_PAD = 12;

// ── Colour / Texture catalogue ────────────────────────────────────────────────
struct Entry { const wchar_t* label; const wchar_t* iconName; COLORREF preview; };

static const Entry COLORS[] = {
    { L"Yellow",   L"yellow",   RGB(0xF5,0xC5,0x18) },
    { L"Blue",     L"blue",     RGB(0x4A,0x90,0xD9) },
    { L"Green",    L"green",    RGB(0x27,0xAE,0x60) },
    { L"Red",      L"red",      RGB(0xE7,0x4C,0x3C) },
    { L"Purple",   L"purple",   RGB(0x8E,0x44,0xAD) },
    { L"Orange",   L"orange",   RGB(0xE6,0x7E,0x22) },
    { L"Pink",     L"pink",     RGB(0xFF,0x69,0xB4) },
    { L"Teal",     L"teal",     RGB(0x1A,0xBC,0x9C) },
    { L"Gray",     L"gray",     RGB(0x7F,0x8C,0x8D) },
    { L"Brown",    L"brown",    RGB(0x79,0x55,0x48) },
    { L"White",    L"white",    RGB(0xF0,0xF0,0xF0) },
    { L"Black",    L"black",    RGB(0x2C,0x2C,0x2C) },
};
static const int COLOR_COUNT = (int)(sizeof(COLORS)/sizeof(COLORS[0]));

static const Entry TEXTURES[] = {
    { L"Gradient",   L"gradient",   RGB(0xD4,0xA8,0x43) },
    { L"Striped",    L"striped",    RGB(0xD4,0xA8,0x43) },
    { L"Dots",       L"dots",       RGB(0xD4,0xA8,0x43) },
    { L"Carbon",     L"carbon",     RGB(0x55,0x55,0x55) },
    { L"Wood",       L"wood",       RGB(0x8B,0x5E,0x3C) },
    { L"Metallic",   L"metallic",   RGB(0xA0,0xA0,0xA0) },
    { L"Neon Blue",  L"neon_blue",  RGB(0x00,0x11,0x22) },
    { L"Neon Green", L"neon_green", RGB(0x00,0x11,0x08) },
    { L"Neon Pink",  L"neon_pink",  RGB(0x11,0x00,0x11) },
};
static const int TEXTURE_COUNT = (int)(sizeof(TEXTURES)/sizeof(TEXTURES[0]));

// ── Registry keys ─────────────────────────────────────────────────────────────
static const wchar_t* REG_KEY = L"Directory\\shell\\FolderColorizer";
static const wchar_t* REG_CMD = L"Directory\\shell\\FolderColorizer\\command";

// ── Global state ──────────────────────────────────────────────────────────────
static std::wstring g_folderPath;
static HBRUSH  g_hbrDark      = nullptr;
static HBRUSH  g_hbrDarker    = nullptr;
static HBRUSH  g_hbrCard      = nullptr;
static HFONT   g_hFontTitle   = nullptr;
static HFONT   g_hFontNormal  = nullptr;
static HFONT   g_hFontSmall   = nullptr;
static HWND    g_hwndMain     = nullptr;
static HWND    g_hwndTab      = nullptr;
static HWND    g_hwndStatus   = nullptr;
static HWND    g_hwndColorPane= nullptr;
static HWND    g_hwndTexPane  = nullptr;
static int     g_hoveredSwatch = -1;  // index into combined swatches
static bool    g_hoverIsTexture= false;

// ═══════════════════════════════════════════════════════════════════════════════
//  Helpers
// ═══════════════════════════════════════════════════════════════════════════════

static std::wstring GetExePath()
{
    wchar_t buf[MAX_PATH] = {};
    GetModuleFileNameW(nullptr, buf, MAX_PATH);
    return buf;
}

static std::wstring GetExeDir()
{
    std::wstring path = GetExePath();
    size_t pos = path.find_last_of(L"\\/");
    return (pos != std::wstring::npos) ? path.substr(0, pos) : path;
}

static std::wstring GetInstallDir()
{
    wchar_t appdata[MAX_PATH] = {};
    SHGetFolderPathW(nullptr, CSIDL_LOCAL_APPDATA, nullptr, 0, appdata);
    return std::wstring(appdata) + L"\\FolderColorizer";
}

static std::wstring GetIconPath(const std::wstring& iconName)
{
    // Prefer installed icons, fall back to exe-side icons
    std::wstring installed = GetInstallDir() + L"\\icons\\" + iconName + L".ico";
    if (PathFileExistsW(installed.c_str())) return installed;

    std::wstring local = GetExeDir() + L"\\icons\\" + iconName + L".ico";
    return local;
}

static bool IsAdmin()
{
    BOOL result = FALSE;
    SID_IDENTIFIER_AUTHORITY NtAuthority = SECURITY_NT_AUTHORITY;
    PSID adminGroup = nullptr;
    if (AllocateAndInitializeSid(&NtAuthority, 2,
            SECURITY_BUILTIN_DOMAIN_RID, DOMAIN_ALIAS_RID_ADMINS,
            0,0,0,0,0,0, &adminGroup))
    {
        CheckTokenMembership(nullptr, adminGroup, &result);
        FreeSid(adminGroup);
    }
    return result == TRUE;
}

static void RunAttrib(const std::wstring& flags, const std::wstring& path)
{
    std::wstring cmd = L"attrib " + flags + L" \"" + path + L"\"";
    STARTUPINFOW si = {}; si.cb = sizeof(si);
    PROCESS_INFORMATION pi = {};
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;
    std::vector<wchar_t> buf(cmd.begin(), cmd.end());
    buf.push_back(L'\0');
    CreateProcessW(nullptr, buf.data(), nullptr, nullptr, FALSE,
                   CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi);
    WaitForSingleObject(pi.hProcess, 5000);
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);
}

static void RefreshExplorer(const std::wstring& path)
{
    SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, path.c_str(), nullptr);
}

static void MsgError(const wchar_t* title, const wchar_t* msg)
{
    MessageBoxW(g_hwndMain, msg, title, MB_OK | MB_ICONERROR);
}

static void MsgInfo(const wchar_t* title, const wchar_t* msg)
{
    MessageBoxW(g_hwndMain, msg, title, MB_OK | MB_ICONINFORMATION);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Registry — context menu
// ═══════════════════════════════════════════════════════════════════════════════

static bool IsContextMenuInstalled()
{
    HKEY hk = nullptr;
    bool ok = (RegOpenKeyExW(HKEY_CLASSES_ROOT, REG_KEY, 0, KEY_READ, &hk) == ERROR_SUCCESS);
    if (hk) RegCloseKey(hk);
    return ok;
}

static bool InstallContextMenu()
{
    std::wstring exe     = GetExePath();
    std::wstring iconPath= GetIconPath(L"orange");

    // menu label
    HKEY hk = nullptr;
    if (RegCreateKeyExW(HKEY_CLASSES_ROOT, REG_KEY, 0, nullptr,
            REG_OPTION_NON_VOLATILE, KEY_WRITE, nullptr, &hk, nullptr) != ERROR_SUCCESS)
        return false;

    const wchar_t* label = L"Change Folder Color / Texture";
    RegSetValueExW(hk, L"",     0, REG_SZ, (BYTE*)label,     (DWORD)((wcslen(label)+1)*2));
    RegSetValueExW(hk, L"Icon", 0, REG_SZ, (BYTE*)iconPath.c_str(), (DWORD)((iconPath.size()+1)*2));
    RegCloseKey(hk);

    // command
    HKEY hkCmd = nullptr;
    if (RegCreateKeyExW(HKEY_CLASSES_ROOT, REG_CMD, 0, nullptr,
            REG_OPTION_NON_VOLATILE, KEY_WRITE, nullptr, &hkCmd, nullptr) != ERROR_SUCCESS)
        return false;

    std::wstring cmd = L"\"" + exe + L"\" \"%1\"";
    RegSetValueExW(hkCmd, L"", 0, REG_SZ, (BYTE*)cmd.c_str(), (DWORD)((cmd.size()+1)*2));
    RegCloseKey(hkCmd);
    return true;
}

static bool UninstallContextMenu()
{
    RegDeleteKeyW(HKEY_CLASSES_ROOT, REG_CMD);
    RegDeleteKeyW(HKEY_CLASSES_ROOT, REG_KEY);
    return true;
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Copy EXE + icons to %LOCALAPPDATA%\FolderColorizer
// ═══════════════════════════════════════════════════════════════════════════════

static bool CopySelfToInstallDir()
{
    std::wstring installDir  = GetInstallDir();
    std::wstring iconsDir    = installDir + L"\\icons";

    CreateDirectoryW(installDir.c_str(), nullptr);
    CreateDirectoryW(iconsDir.c_str(), nullptr);

    // Copy EXE
    std::wstring srcExe = GetExePath();
    std::wstring dstExe = installDir + L"\\folder_colorizer.exe";
    if (!CopyFileW(srcExe.c_str(), dstExe.c_str(), FALSE)) return false;

    // Copy icons
    std::wstring srcIcons = GetExeDir() + L"\\icons\\*.ico";
    WIN32_FIND_DATAW fd;
    HANDLE hFind = FindFirstFileW(srcIcons.c_str(), &fd);
    if (hFind != INVALID_HANDLE_VALUE)
    {
        do {
            std::wstring src = GetExeDir() + L"\\icons\\" + fd.cFileName;
            std::wstring dst = iconsDir + L"\\" + fd.cFileName;
            CopyFileW(src.c_str(), dst.c_str(), FALSE);
        } while (FindNextFileW(hFind, &fd));
        FindClose(hFind);
    }
    return true;
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Apply / Reset folder icon
// ═══════════════════════════════════════════════════════════════════════════════

static bool ApplyIcon(const std::wstring& folder, const std::wstring& iconName)
{
    std::wstring iconSrc = GetIconPath(iconName);
    if (!PathFileExistsW(iconSrc.c_str()))
    {
        std::wstring msg = L"Icon file not found:\n" + iconSrc;
        MsgError(L"Icon Not Found", msg.c_str());
        return false;
    }

    // Create hidden .folder_icons dir inside the target folder
    std::wstring iconDir  = folder + L"\\.folder_icons";
    std::wstring iconDest = iconDir + L"\\" + iconName + L".ico";
    CreateDirectoryW(iconDir.c_str(), nullptr);
    CopyFileW(iconSrc.c_str(), iconDest.c_str(), FALSE);

    // Hide the cache dir
    RunAttrib(L"+H +S", iconDir);

    // Write desktop.ini
    std::wstring iniPath = folder + L"\\desktop.ini";
    // Remove existing attributes
    RunAttrib(L"-R -S -H", iniPath);
    {
        std::wofstream f(iniPath);
        if (!f.is_open())
        {
            MsgError(L"Permission Error",
                     L"Could not write desktop.ini.\nTry running as Administrator.");
            return false;
        }
        f << L"[.ShellClassInfo]\r\n";
        f << L"IconResource=" << iconDest << L",0\r\n";
        f << L"IconIndex=0\r\n";
        f << L"[ViewState]\r\n";
        f << L"Mode=\r\nVid=\r\nFolderType=Generic\r\n";
    }
    RunAttrib(L"+R +S +H", iniPath);

    // Make folder itself system (required for desktop.ini to take effect)
    RunAttrib(L"+R +S", folder);

    RefreshExplorer(folder);
    return true;
}

static void ResetIcon(const std::wstring& folder)
{
    std::wstring iniPath  = folder + L"\\desktop.ini";
    std::wstring iconDir  = folder + L"\\.folder_icons";

    RunAttrib(L"-R -S -H", iniPath);
    DeleteFileW(iniPath.c_str());

    RunAttrib(L"-H -S", iconDir);
    // Remove directory recursively (simple shell delete)
    {
        SHFILEOPSTRUCTW op = {};
        std::wstring from = iconDir + L'\0';
        op.hwnd   = g_hwndMain;
        op.wFunc  = FO_DELETE;
        op.pFrom  = from.c_str();
        op.fFlags = FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT;
        SHFileOperationW(&op);
    }

    RunAttrib(L"-R -S", folder);
    RefreshExplorer(folder);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  GUI — IDs
// ═══════════════════════════════════════════════════════════════════════════════
enum {
    ID_TAB          = 100,
    ID_BTN_RESET    = 200,
    ID_BTN_INSTALL  = 201,
    ID_BTN_UNINSTALL= 202,
    ID_STATUS_LABEL = 203,

    // Swatches: Colors 1000..1011, Textures 2000..2008
    ID_SWATCH_COLOR_BASE   = 1000,
    ID_SWATCH_TEXTURE_BASE = 2000,
};

// ═══════════════════════════════════════════════════════════════════════════════
//  Swatch owner-draw button
// ═══════════════════════════════════════════════════════════════════════════════

static LRESULT CALLBACK SwatchProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp,
                                    UINT_PTR, DWORD_PTR dwRef)
{
    static bool s_trackingMouse = false;

    switch (msg)
    {
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hwnd, &ps);
        RECT rc; GetClientRect(hwnd, &rc);

        COLORREF fill = (COLORREF)GetWindowLongPtrW(hwnd, GWLP_USERDATA);
        HBRUSH hbr = CreateSolidBrush(fill);
        FillRect(hdc, &rc, hbr);
        DeleteObject(hbr);

        // border — white on hover, dim otherwise
        bool hover = (GetWindowLongPtrW(hwnd, GWLP_ID) == (LONG_PTR)GetPropW(hwnd, L"hover"));
        COLORREF borderCol = hover ? SWATCH_HOVER : SWATCH_BORDER;
        HPEN hpen = CreatePen(PS_SOLID, 2, borderCol);
        HPEN old  = (HPEN)SelectObject(hdc, hpen);
        HBRUSH hbrNull = (HBRUSH)GetStockObject(NULL_BRUSH);
        HBRUSH oldBr   = (HBRUSH)SelectObject(hdc, hbrNull);
        Rectangle(hdc, rc.left+1, rc.top+1, rc.right-1, rc.bottom-1);
        SelectObject(hdc, old);
        SelectObject(hdc, oldBr);
        DeleteObject(hpen);

        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_MOUSEMOVE:
    {
        // Track hover
        SetPropW(hwnd, L"hover", (HANDLE)(LONG_PTR)GetWindowLongPtrW(hwnd, GWLP_ID));
        TRACKMOUSEEVENT tme = { sizeof(tme), TME_LEAVE, hwnd, 0 };
        TrackMouseEvent(&tme);
        SetCursor(LoadCursorW(nullptr, IDC_HAND));
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    }
    case WM_MOUSELEAVE:
        RemovePropW(hwnd, L"hover");
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    case WM_SETCURSOR:
        SetCursor(LoadCursorW(nullptr, IDC_HAND));
        return TRUE;
    }
    return DefSubclassProc(hwnd, msg, wp, lp);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Build swatch grid
// ═══════════════════════════════════════════════════════════════════════════════

static HWND BuildSwatchPane(HWND parent, const Entry* entries, int count,
                             int idBase, int parentW)
{
    // Calculate pane height
    int rows  = (count + SWATCH_COLS - 1) / SWATCH_COLS;
    int paneH = TAB_CONTENT_PAD + 20 + SWATCH_PAD +
                rows * (SWATCH_SIZE + SWATCH_PAD + 18 + SWATCH_PAD);

    HWND pane = CreateWindowExW(0, L"STATIC", L"",
        WS_CHILD | WS_CLIPCHILDREN,
        0, 0, parentW, paneH,
        parent, nullptr, nullptr, nullptr);

    // Label
    HWND lbl = CreateWindowExW(0, L"STATIC",
        idBase == ID_SWATCH_COLOR_BASE ? L"Choose a colour:" : L"Choose a texture:",
        WS_CHILD | WS_VISIBLE | SS_LEFT,
        TAB_CONTENT_PAD, TAB_CONTENT_PAD,
        parentW - TAB_CONTENT_PAD*2, 18,
        pane, nullptr, nullptr, nullptr);
    SendMessageW(lbl, WM_SETFONT, (WPARAM)g_hFontSmall, TRUE);

    // Swatches
    int gridX = TAB_CONTENT_PAD;
    int gridY = TAB_CONTENT_PAD + 20 + SWATCH_PAD;

    for (int i = 0; i < count; ++i)
    {
        int col = i % SWATCH_COLS;
        int row = i / SWATCH_COLS;
        int x   = gridX + col * (SWATCH_SIZE + SWATCH_PAD);
        int y   = gridY + row * (SWATCH_SIZE + 18 + SWATCH_PAD*2);

        // Colour square
        HWND sw = CreateWindowExW(0, L"BUTTON", L"",
            WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
            x, y, SWATCH_SIZE, SWATCH_SIZE,
            pane, (HMENU)(UINT_PTR)(idBase + i), nullptr, nullptr);
        SetWindowLongPtrW(sw, GWLP_USERDATA, (LONG_PTR)entries[i].preview);
        SetWindowSubclass(sw, SwatchProc, (UINT_PTR)(idBase+i), 0);

        // Label below swatch
        HWND lbSw = CreateWindowExW(0, L"STATIC", entries[i].label,
            WS_CHILD | WS_VISIBLE | SS_CENTER,
            x, y + SWATCH_SIZE + 2, SWATCH_SIZE, 16,
            pane, nullptr, nullptr, nullptr);
        SendMessageW(lbSw, WM_SETFONT, (WPARAM)g_hFontSmall, TRUE);
    }

    return pane;
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Owner-draw button helper (for Install / Uninstall / Reset)
// ═══════════════════════════════════════════════════════════════════════════════

struct BtnStyle { COLORREF bg; COLORREF fg; };

static HWND CreateFlatButton(HWND parent, const wchar_t* text, int id,
                              int x, int y, int w, int h,
                              BtnStyle style)
{
    HWND btn = CreateWindowExW(0, L"BUTTON", text,
        WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
        x, y, w, h,
        parent, (HMENU)(UINT_PTR)id, nullptr, nullptr);
    // Store colours in window extra bytes via properties
    SetPropW(btn, L"bgColor", (HANDLE)(ULONG_PTR)style.bg);
    SetPropW(btn, L"fgColor", (HANDLE)(ULONG_PTR)style.fg);
    SendMessageW(btn, WM_SETFONT, (WPARAM)g_hFontNormal, TRUE);
    return btn;
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Subclass proc for dark STATIC labels & panes
// ═══════════════════════════════════════════════════════════════════════════════

static LRESULT CALLBACK DarkStaticProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp,
                                        UINT_PTR, DWORD_PTR)
{
    if (msg == WM_CTLCOLORSTATIC || msg == WM_CTLCOLORBTN)
    {
        HDC hdc = (HDC)wp;
        SetBkMode(hdc, TRANSPARENT);
        SetTextColor(hdc, FG_GRAY);
        return (LRESULT)g_hbrDark;
    }
    return DefSubclassProc(hwnd, msg, wp, lp);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Main window proc
// ═══════════════════════════════════════════════════════════════════════════════

static void UpdateStatusLabel()
{
    if (!g_hwndStatus) return;
    bool installed = IsContextMenuInstalled();
    const wchar_t* txt = installed
        ? L"✔ Context menu installed"
        : L"⚠ Context menu NOT installed (run as Admin to install)";
    SetWindowTextW(g_hwndStatus, txt);
}

static void HandleSwatchClick(int id)
{
    bool isTexture = (id >= ID_SWATCH_TEXTURE_BASE);
    int  idx       = isTexture ? (id - ID_SWATCH_TEXTURE_BASE)
                               : (id - ID_SWATCH_COLOR_BASE);
    const Entry& e = isTexture ? TEXTURES[idx] : COLORS[idx];

    if (g_folderPath.empty())
    {
        MsgInfo(L"No Folder Selected",
                L"Right-click a folder in Explorer to use this tool.");
        return;
    }

    if (ApplyIcon(g_folderPath, e.iconName))
    {
        std::wstring msg = std::wstring(L"Folder color set to \"") + e.label + L"\".\n\n"
                           L"You may need to press F5 in Explorer to refresh.";
        MsgInfo(L"Done ✓", msg.c_str());
        DestroyWindow(g_hwndMain);
    }
}

static LRESULT CALLBACK MainWndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    switch (msg)
    {
    // ── Painting ──────────────────────────────────────────────────────────
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hwnd, &ps);

        RECT rc; GetClientRect(hwnd, &rc);
        int W = rc.right;

        // Full background
        FillRect(hdc, &rc, g_hbrDark);

        // Header bar
        RECT hdrRc = { 0, 0, W, HEADER_H };
        FillRect(hdc, &hdrRc, g_hbrDarker);

        // Title text
        SetBkMode(hdc, TRANSPARENT);
        SetTextColor(hdc, FG_WHITE);
        HFONT oldFont = (HFONT)SelectObject(hdc, g_hFontTitle);
        RECT titleRc = { 0, 10, W, 38 };
        DrawTextW(hdc, L"\U0001F3A8  Folder Colorizer", -1, &titleRc,
                  DT_CENTER | DT_SINGLELINE);

        // Folder path subtitle
        if (!g_folderPath.empty())
        {
            SelectObject(hdc, g_hFontSmall);
            SetTextColor(hdc, FG_DIM);
            std::wstring disp = g_folderPath;
            if (disp.size() > 58) disp = disp.substr(0, 55) + L"…";
            RECT subRc = { 0, 36, W, HEADER_H - 4 };
            DrawTextW(hdc, disp.c_str(), -1, &subRc, DT_CENTER | DT_SINGLELINE);
        }

        SelectObject(hdc, oldFont);

        // Footer bar
        RECT ftrRc = { 0, rc.bottom - FOOTER_H, W, rc.bottom };
        FillRect(hdc, &ftrRc, g_hbrDarker);

        EndPaint(hwnd, &ps);
        return 0;
    }

    // ── Colour propagation for child controls ─────────────────────────────
    case WM_CTLCOLORSTATIC:
    {
        HDC hdc = (HDC)wp;
        SetBkMode(hdc, TRANSPARENT);
        HWND hwndCtrl = (HWND)lp;
        if (hwndCtrl == g_hwndStatus)
            SetTextColor(hdc, FG_DIM);
        else
            SetTextColor(hdc, FG_GRAY);
        return (LRESULT)g_hbrDark;
    }
    case WM_CTLCOLORBTN:
        return (LRESULT)g_hbrDark;

    // ── Owner-draw buttons ────────────────────────────────────────────────
    case WM_DRAWITEM:
    {
        LPDRAWITEMSTRUCT dis = (LPDRAWITEMSTRUCT)lp;
        if (dis->CtlType != ODT_BUTTON) break;

        int id = dis->CtlID;

        // Swatch buttons
        if ((id >= ID_SWATCH_COLOR_BASE   && id < ID_SWATCH_COLOR_BASE   + COLOR_COUNT) ||
            (id >= ID_SWATCH_TEXTURE_BASE && id < ID_SWATCH_TEXTURE_BASE + TEXTURE_COUNT))
        {
            bool isTexture = (id >= ID_SWATCH_TEXTURE_BASE);
            int  idx       = isTexture ? (id - ID_SWATCH_TEXTURE_BASE) : (id - ID_SWATCH_COLOR_BASE);
            const Entry& e = isTexture ? TEXTURES[idx] : COLORS[idx];

            RECT& rc = dis->rcItem;
            COLORREF fill = e.preview;
            HBRUSH hbr = CreateSolidBrush(fill);
            FillRect(dis->hDC, &rc, hbr);
            DeleteObject(hbr);

            bool hover = (dis->itemState & ODS_HOTLIGHT) != 0;
            COLORREF bc = hover ? SWATCH_HOVER : SWATCH_BORDER;
            HPEN pen = CreatePen(PS_SOLID, 2, bc);
            HPEN oldPen = (HPEN)SelectObject(dis->hDC, pen);
            HBRUSH nb   = (HBRUSH)GetStockObject(NULL_BRUSH);
            HBRUSH ob   = (HBRUSH)SelectObject(dis->hDC, nb);
            Rectangle(dis->hDC, rc.left+1, rc.top+1, rc.right-1, rc.bottom-1);
            SelectObject(dis->hDC, oldPen);
            SelectObject(dis->hDC, ob);
            DeleteObject(pen);
            return TRUE;
        }

        // Flat action buttons (Reset / Install / Uninstall)
        {
            COLORREF bg = (COLORREF)(ULONG_PTR)GetPropW(dis->hwndItem, L"bgColor");
            COLORREF fg = (COLORREF)(ULONG_PTR)GetPropW(dis->hwndItem, L"fgColor");

            bool pressed = (dis->itemState & ODS_SELECTED) != 0;
            if (pressed) {
                bg = RGB(
                    (int)(GetRValue(bg)*0.8),
                    (int)(GetGValue(bg)*0.8),
                    (int)(GetBValue(bg)*0.8)
                );
            }

            RECT& rc = dis->rcItem;
            HBRUSH hbr = CreateSolidBrush(bg);
            FillRect(dis->hDC, &rc, hbr);
            DeleteObject(hbr);

            SetBkMode(dis->hDC, TRANSPARENT);
            SetTextColor(dis->hDC, fg);

            wchar_t text[128] = {};
            GetWindowTextW(dis->hwndItem, text, 128);
            HFONT oldFont = (HFONT)SelectObject(dis->hDC, g_hFontNormal);
            DrawTextW(dis->hDC, text, -1, &rc, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
            SelectObject(dis->hDC, oldFont);
            return TRUE;
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────
    case WM_COMMAND:
    {
        int id = LOWORD(wp);

        // Swatch clicks
        if (HIWORD(wp) == BN_CLICKED)
        {
            if ((id >= ID_SWATCH_COLOR_BASE && id < ID_SWATCH_COLOR_BASE + COLOR_COUNT) ||
                (id >= ID_SWATCH_TEXTURE_BASE && id < ID_SWATCH_TEXTURE_BASE + TEXTURE_COUNT))
            {
                HandleSwatchClick(id);
                return 0;
            }

            switch (id)
            {
            case ID_BTN_RESET:
                if (g_folderPath.empty())
                    MsgInfo(L"No Folder", L"Right-click a folder first.");
                else {
                    ResetIcon(g_folderPath);
                    MsgInfo(L"Done ✓",
                            L"Folder icon reset to default.\nPress F5 in Explorer if needed.");
                    DestroyWindow(hwnd);
                }
                break;

            case ID_BTN_INSTALL:
                if (!IsAdmin())
                    MsgError(L"Administrator Required",
                             L"Please re-run as Administrator to install the context menu entry.");
                else {
                    CopySelfToInstallDir();
                    if (InstallContextMenu())
                    {
                        MsgInfo(L"Installed ✓",
                                L"Right-click menu installed!\n\n"
                                L"Right-click any folder in Explorer and choose\n"
                                L"\"Change Folder Color / Texture\".");
                        UpdateStatusLabel();
                    }
                    else
                        MsgError(L"Error", L"Failed to install context menu. Check registry permissions.");
                }
                break;

            case ID_BTN_UNINSTALL:
                if (!IsAdmin())
                    MsgError(L"Administrator Required",
                             L"Please re-run as Administrator to remove the context menu entry.");
                else {
                    UninstallContextMenu();
                    MsgInfo(L"Uninstalled", L"Context menu entry removed.");
                    UpdateStatusLabel();
                }
                break;
            }
        }
        return 0;
    }

    // ── Tab selection — show/hide panes ───────────────────────────────────
    case WM_NOTIFY:
    {
        NMHDR* nm = (NMHDR*)lp;
        if (nm->idFrom == ID_TAB && nm->code == TCN_SELCHANGE)
        {
            int sel = TabCtrl_GetCurSel(g_hwndTab);
            ShowWindow(g_hwndColorPane, sel == 0 ? SW_SHOW : SW_HIDE);
            ShowWindow(g_hwndTexPane,   sel == 1 ? SW_SHOW : SW_HIDE);
        }
        return 0;
    }

    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Build the main window
// ═══════════════════════════════════════════════════════════════════════════════

static void CreateFonts()
{
    g_hFontTitle  = CreateFontW(20,0,0,0,FW_BOLD,FALSE,FALSE,FALSE,
                                DEFAULT_CHARSET,OUT_DEFAULT_PRECIS,CLIP_DEFAULT_PRECIS,
                                CLEARTYPE_QUALITY,DEFAULT_PITCH|FF_SWISS,L"Segoe UI");
    g_hFontNormal = CreateFontW(16,0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,
                                DEFAULT_CHARSET,OUT_DEFAULT_PRECIS,CLIP_DEFAULT_PRECIS,
                                CLEARTYPE_QUALITY,DEFAULT_PITCH|FF_SWISS,L"Segoe UI");
    g_hFontSmall  = CreateFontW(12,0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,
                                DEFAULT_CHARSET,OUT_DEFAULT_PRECIS,CLIP_DEFAULT_PRECIS,
                                CLEARTYPE_QUALITY,DEFAULT_PITCH|FF_SWISS,L"Segoe UI");
}

static void CreateBrushes()
{
    g_hbrDark   = CreateSolidBrush(BG_DARK);
    g_hbrDarker = CreateSolidBrush(BG_DARKER);
    g_hbrCard   = CreateSolidBrush(BG_CARD);
}

static void DestroyResources()
{
    DeleteObject(g_hbrDark);
    DeleteObject(g_hbrDarker);
    DeleteObject(g_hbrCard);
    DeleteObject(g_hFontTitle);
    DeleteObject(g_hFontNormal);
    DeleteObject(g_hFontSmall);
}

static HWND CreateMainWindow()
{
    // Width = COLS*swatch + padding on both sides + gaps
    int tabW = SWATCH_COLS * (SWATCH_SIZE + SWATCH_PAD) + TAB_CONTENT_PAD * 2 + SWATCH_PAD;
    tabW = std::max(tabW, 420);

    // Height: header + tab bar(30) + content rows(colors=2 rows, 2*(52+18+12)+30) + footer
    int colorRows  = (COLOR_COUNT   + SWATCH_COLS - 1) / SWATCH_COLS;
    int texRows    = (TEXTURE_COUNT + SWATCH_COLS - 1) / SWATCH_COLS;
    int contentH   = std::max(colorRows, texRows) * (SWATCH_SIZE + 18 + SWATCH_PAD*2)
                     + 40 + TAB_CONTENT_PAD*2;
    int totalH     = HEADER_H + 30 + contentH + FOOTER_H + 16;

    WNDCLASSEXW wc = {};
    wc.cbSize        = sizeof(wc);
    wc.lpfnWndProc   = MainWndProc;
    wc.hInstance     = GetModuleHandleW(nullptr);
    wc.hCursor       = LoadCursorW(nullptr, IDC_ARROW);
    wc.hbrBackground = g_hbrDark;
    wc.lpszClassName = L"FolderColorizerWnd";
    wc.hIcon         = LoadIconW(GetModuleHandleW(nullptr), MAKEINTRESOURCEW(1));
    RegisterClassExW(&wc);

    // Centre on screen
    int sx = GetSystemMetrics(SM_CXSCREEN);
    int sy = GetSystemMetrics(SM_CYSCREEN);
    int wx = (sx - tabW)  / 2;
    int wy = (sy - totalH) / 2;

    HWND hwnd = CreateWindowExW(
        WS_EX_APPWINDOW,
        L"FolderColorizerWnd",
        L"Folder Colorizer",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
        wx, wy, tabW, totalH,
        nullptr, nullptr, GetModuleHandleW(nullptr), nullptr);

    g_hwndMain = hwnd;

    // ── Tab control ───────────────────────────────────────────────────────
    RECT clientRc; GetClientRect(hwnd, &clientRc);
    int CW = clientRc.right;
    int tabTop = HEADER_H + 8;

    g_hwndTab = CreateWindowExW(0, WC_TABCONTROLW, L"",
        WS_CHILD | WS_VISIBLE | TCS_FLATBUTTONS,
        8, tabTop, CW - 16, contentH + 22,
        hwnd, (HMENU)ID_TAB, nullptr, nullptr);
    SendMessageW(g_hwndTab, WM_SETFONT, (WPARAM)g_hFontNormal, TRUE);

    // Style tab: dark background
    SetWindowTheme(g_hwndTab, L"", L"");

    TCITEMW tie = {};
    tie.mask    = TCIF_TEXT;
    tie.pszText = (wchar_t*)L"  Colors  ";
    TabCtrl_InsertItem(g_hwndTab, 0, &tie);
    tie.pszText = (wchar_t*)L"  Textures  ";
    TabCtrl_InsertItem(g_hwndTab, 1, &tie);

    // Get display rect for tab content
    RECT tabContentRc = { 0, 0, CW - 16, contentH + 22 };
    TabCtrl_AdjustRect(g_hwndTab, FALSE, &tabContentRc);
    int paneW = tabContentRc.right - tabContentRc.left;
    int paneX = 8 + tabContentRc.left;
    int paneY = tabTop + tabContentRc.top;

    // ── Swatch panes ──────────────────────────────────────────────────────
    g_hwndColorPane = BuildSwatchPane(g_hwndTab, COLORS,   COLOR_COUNT,
                                       ID_SWATCH_COLOR_BASE,   paneW);
    g_hwndTexPane   = BuildSwatchPane(g_hwndTab, TEXTURES, TEXTURE_COUNT,
                                       ID_SWATCH_TEXTURE_BASE, paneW);

    // Position inside tab
    SetWindowPos(g_hwndColorPane, nullptr,
                 tabContentRc.left, tabContentRc.top, paneW, contentH,
                 SWP_NOZORDER);
    SetWindowPos(g_hwndTexPane, nullptr,
                 tabContentRc.left, tabContentRc.top, paneW, contentH,
                 SWP_NOZORDER);

    ShowWindow(g_hwndColorPane, SW_SHOW);
    ShowWindow(g_hwndTexPane,   SW_HIDE);

    // ── Footer buttons ────────────────────────────────────────────────────
    int footerY = clientRc.bottom - FOOTER_H;
    int btnH    = 28;
    int btnY    = footerY + (FOOTER_H - btnH) / 2;

    CreateFlatButton(hwnd, L"↩  Reset to Default", ID_BTN_RESET,
                     10, btnY, 160, btnH,
                     { RGB(0x55,0x55,0x55), FG_WHITE });

    if (g_folderPath.empty())
    {
        // Standalone launch: show install / uninstall
        CreateFlatButton(hwnd, L"✕  Uninstall", ID_BTN_UNINSTALL,
                         CW - 10 - 110, btnY, 110, btnH,
                         { ACCENT_RED, FG_WHITE });
        CreateFlatButton(hwnd, L"⚙  Install Right-Click Menu", ID_BTN_INSTALL,
                         CW - 10 - 110 - 8 - 230, btnY, 230, btnH,
                         { ACCENT_GREEN, FG_WHITE });
    }

    // ── Status label ──────────────────────────────────────────────────────
    g_hwndStatus = CreateWindowExW(0, L"STATIC", L"",
        WS_CHILD | WS_VISIBLE | SS_RIGHT,
        g_folderPath.empty() ? CW/2 : 180,
        footerY + 4, CW - (g_folderPath.empty() ? CW/2 : 180) - 8, 16,
        hwnd, (HMENU)ID_STATUS_LABEL, nullptr, nullptr);
    SendMessageW(g_hwndStatus, WM_SETFONT, (WPARAM)g_hFontSmall, TRUE);
    UpdateStatusLabel();

    return hwnd;
}

// ═══════════════════════════════════════════════════════════════════════════════
//  WinMain
// ═══════════════════════════════════════════════════════════════════════════════

int WINAPI wWinMain(HINSTANCE, HINSTANCE, LPWSTR lpCmdLine, int)
{
    // Init common controls
    INITCOMMONCONTROLSEX icc = { sizeof(icc), ICC_TAB_CLASSES | ICC_STANDARD_CLASSES };
    InitCommonControlsEx(&icc);

    // Parse command line — the folder path may be quoted or not
    int argc = 0;
    wchar_t** argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (argc > 1)
    {
        g_folderPath = argv[1];
        // Validate
        DWORD attr = GetFileAttributesW(g_folderPath.c_str());
        if (attr == INVALID_FILE_ATTRIBUTES || !(attr & FILE_ATTRIBUTE_DIRECTORY))
        {
            MessageBoxW(nullptr,
                        (L"Not a valid directory:\n" + g_folderPath).c_str(),
                        L"Folder Colorizer", MB_OK | MB_ICONERROR);
            LocalFree(argv);
            return 1;
        }
    }
    LocalFree(argv);

    CreateFonts();
    CreateBrushes();

    HWND hwnd = CreateMainWindow();
    ShowWindow(hwnd, SW_SHOW);
    UpdateWindow(hwnd);

    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    DestroyResources();
    return (int)msg.wParam;
}
