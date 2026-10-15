#include "core.hpp"
#include <objidl.h>
#include <gdiplus.h>
#include <commctrl.h>
#include <shellapi.h>
#include <shlobj.h>
#include <set>
#include <tuple>
#include <windowsx.h>
#pragma comment(lib, "gdiplus.lib")
#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "oleaut32.lib")
#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "uuid.lib")

namespace ei
{
static HINSTANCE instance;
static HWND mainWindow;
static HFONT font, headingFont, eyebrowFont, captionFont, choiceFont, playFont;
static HFONT sectionFont, settingsHeadingFont, settingsBoldFont, cardHeadingFont;
static UINT dpi = 96;
static bool dark = true;
static Profile profile;
static Game game;
static std::vector<Mod> modifications;
static int selected = -1;
static HWND modCombo, playButton;
static std::unique_ptr<Gdiplus::Bitmap> artwork;
static std::vector<HWND> mainControls;
static const S exeName = L"EIRebuild.exe";
static void (*ensureVisible)(HWND) = nullptr;
static int px(int n)
{
    return MulDiv(n, dpi, 96);
}
static COLORREF background()
{
    return dark ? RGB(32, 33, 36) : RGB(245, 246, 248);
}
static COLORREF surface()
{
    return dark ? RGB(46, 48, 52) : RGB(255, 255, 255);
}
static COLORREF ink()
{
    return dark ? RGB(232, 234, 237) : RGB(32, 33, 36);
}
static COLORREF gold = RGB(205, 151, 69);
static constexpr int footerHeight = 184;
static HBRUSH bgBrush, surfaceBrush;
static void brushes()
{
    if (bgBrush)
        DeleteObject(bgBrush);
    if (surfaceBrush)
        DeleteObject(surfaceBrush);
    bgBrush = CreateSolidBrush(background());
    surfaceBrush = CreateSolidBrush(surface());
}
static S windowText(HWND w)
{
    int n = GetWindowTextLengthW(w);
    S s(n + 1, L'\0');
    GetWindowTextW(w, &s[0], n + 1);
    s.resize(n);
    return s;
}
static void error(HWND w, const std::exception &e)
{
    std::string a = e.what();
    MessageBoxW(w, decode(Bytes(a.begin(), a.end())).c_str(), L"ReStarter", MB_ICONERROR);
}

static void open(HWND w, const S &path)
{
    auto h = (INT_PTR)ShellExecuteW(w, L"open", path.c_str(), nullptr, root.c_str(), SW_SHOWNORMAL);
    if (h <= 32)
        fail(L"Cannot open: " + path);
}
static HWND control(HWND parent, const wchar_t *cls, const S &caption, int id, DWORD style = 0)
{
    HWND w = CreateWindowExW((wcscmp(cls, L"EDIT") == 0) ? WS_EX_CLIENTEDGE : 0, cls, caption.c_str(),
                             WS_CHILD | WS_VISIBLE | style, 0, 0, 0, 0, parent, (HMENU)(INT_PTR)id, instance,
                             nullptr);
    if (!w)
        check(GetLastError(), L"CreateWindow");
    SendMessageW(w, WM_SETFONT, (WPARAM)font, TRUE);
    return w;
}
static HWND button(HWND p, const S &s, int id)
{
    HWND w = control(p, L"BUTTON", s, id, WS_TABSTOP | BS_OWNERDRAW);
    SetWindowSubclass(
        w,
        [](HWND w, UINT m, WPARAM a, LPARAM b, UINT_PTR, DWORD_PTR) -> LRESULT {
            // Owner drawing fills the whole button; the system erase can flash white.
            if (m == WM_ERASEBKGND)
                return 1;
            if (m == WM_SETFOCUS)
                SendMessageW(w, WM_CHANGEUISTATE, MAKEWPARAM(UIS_INITIALIZE, 0), 0);
            else if (m == WM_LBUTTONDOWN || m == WM_LBUTTONDBLCLK)
                SendMessageW(w, WM_CHANGEUISTATE, MAKEWPARAM(UIS_SET, UISF_HIDEFOCUS), 0);
            if (m == WM_SETFOCUS && ensureVisible)
                ensureVisible(w);
            if (m == WM_MOUSEMOVE && !GetPropW(w, L"hover"))
            {
                SetPropW(w, L"hover", (HANDLE)1);
                TRACKMOUSEEVENT track{sizeof(track), TME_LEAVE, w, 0};
                TrackMouseEvent(&track);
                InvalidateRect(w, nullptr, FALSE);
            }
            else if (m == WM_MOUSELEAVE || m == WM_KILLFOCUS)
            {
                RemovePropW(w, L"hover");
                InvalidateRect(w, nullptr, FALSE);
            }
            return DefSubclassProc(w, m, a, b);
        },
        1, 0);
    return w;
}
static void titleTheme(HWND window)
{
    // The same optional title-bar treatment as LauncherTheme.cs. Windows 7
    // simply keeps its system title bar when the attribute is unsupported.
    wchar_t system[MAX_PATH];
    if (!GetSystemDirectoryW(system, MAX_PATH))
        return;
    HMODULE dwm = LoadLibraryW(join(system, L"dwmapi.dll").c_str());
    if (!dwm)
        return;
    using SetAttribute = HRESULT(WINAPI *)(HWND, DWORD, LPCVOID, DWORD);
    auto set = (SetAttribute)GetProcAddress(dwm, "DwmSetWindowAttribute");
    if (set)
    {
        BOOL enabled = dark;
        set(window, 20, &enabled, sizeof(enabled));
    }
    FreeLibrary(dwm);
}
static void place(HWND w, int x, int y, int width, int height)
{
    MoveWindow(w, px(x), px(y), px(width), px(height), TRUE);
}
static void icon(HDC dc, int x, int y, int kind, COLORREF color)
{
    Gdiplus::Graphics g(dc);
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    g.TranslateTransform((float)x, (float)y);
    g.ScaleTransform(dpi / 96.F, dpi / 96.F);
    Gdiplus::Pen pen(Gdiplus::Color(255, GetRValue(color), GetGValue(color), GetBValue(color)), 1.6F);
    pen.SetStartCap(Gdiplus::LineCapRound);
    pen.SetEndCap(Gdiplus::LineCapRound);
    pen.SetLineJoin(Gdiplus::LineJoinRound);
    auto lines = [&](std::initializer_list<Gdiplus::Point> points) {
        std::vector<Gdiplus::Point> p(points);
        g.DrawLines(&pen, p.data(), (int)p.size());
    };
    if (kind == 0)
    {
        g.DrawRectangle(&pen, 1, 2, 20, 14);
        g.DrawLine(&pen, 8, 20, 14, 20);
        g.DrawLine(&pen, 11, 16, 11, 20);
        lines({{5, 12}, {9, 8}, {12, 11}, {17, 5}});
    }
    if (kind == 1)
    {
        g.DrawEllipse(&pen, 7, 2, 7, 7);
        g.DrawArc(&pen, 4, 10, 13, 16, 190, 160);
        g.DrawArc(&pen, 0, 5, 6, 6, 70, 270);
        g.DrawArc(&pen, 16, 5, 6, 6, 200, 270);
        g.DrawArc(&pen, -2, 12, 10, 12, 190, 100);
        g.DrawArc(&pen, 14, 12, 10, 12, 250, 100);
    }
    if (kind == 2)
    {
        lines({{1, 7}, {5, 7}, {7, 3}, {14, 3}, {16, 7}, {21, 7}, {21, 19}, {1, 19}, {1, 7}});
        g.DrawEllipse(&pen, 7, 8, 8, 8);
        g.DrawLine(&pen, 18, 10, 19, 10);
    }
    if (kind == 3)
    {
        lines({{1, 18}, {1, 4}, {8, 4}, {11, 7}, {20, 7}, {20, 10}});
        lines({{1, 18}, {5, 10}, {22, 10}, {18, 19}, {1, 19}, {1, 18}});
    }
}
static LRESULT paintButton(DRAWITEMSTRUCT *d)
{
    S caption = windowText(d->hwndItem);
    int id = GetDlgCtrlID(d->hwndItem);
    bool main = GetParent(d->hwndItem) == mainWindow;
    bool navigation = (main && id >= 11 && id <= 15) || GetPropW(d->hwndItem, L"navigation");
    bool theme = main && (id == 16 || id == 17);
    bool chosen = GetPropW(d->hwndItem, L"selected") != nullptr;
    if (theme)
        chosen = id == 16 ? dark : !dark;
    bool pressed = (d->itemState & ODS_SELECTED) != 0, disabled = (d->itemState & ODS_DISABLED) != 0;
    bool primary = id == 10 || id == 201 || id == 800;
    bool toggle = GetPropW(d->hwndItem, L"toggle") != nullptr;
    COLORREF fill = primary      ? gold
                    : chosen     ? (dark ? RGB(61, 53, 40) : RGB(248, 234, 210))
                    : navigation ? background()
                                 : surface();
    if (!disabled && (pressed || GetPropW(d->hwndItem, L"hover")))
        fill = primary ? RGB(232, 181, 94) : dark ? RGB(60, 63, 69) : RGB(226, 230, 235);
    HBRUSH b = CreateSolidBrush(fill);
    FillRect(d->hDC, &d->rcItem, b);
    DeleteObject(b);
    HPEN p = CreatePen(PS_SOLID, 1,
                       toggle       ? fill
                       : navigation ? fill
                       : primary    ? RGB(232, 187, 112)
                       : chosen     ? RGB(181, 135, 68)
                       : dark       ? RGB(77, 80, 86)
                                    : RGB(200, 203, 208));
    auto old = SelectObject(d->hDC, p);
    auto ob = SelectObject(d->hDC, GetStockObject(HOLLOW_BRUSH));
    Rectangle(d->hDC, d->rcItem.left, d->rcItem.top, d->rcItem.right, d->rcItem.bottom);
    SelectObject(d->hDC, ob);
    SelectObject(d->hDC, old);
    DeleteObject(p);
    SetBkMode(d->hDC, TRANSPARENT);
    COLORREF c = disabled  ? RGB(125, 125, 125)
                 : primary ? RGB(27, 24, 19)
                 : chosen  ? (dark ? RGB(239, 194, 123) : RGB(102, 65, 14))
                           : ink();
    SetTextColor(d->hDC, c);
    auto selectedFont = (HFONT)SendMessageW(d->hwndItem, WM_GETFONT, 0, 0);
    SelectObject(d->hDC, selectedFont ? selectedFont : font);
    RECT r = d->rcItem;
    r.left += px(10);
    r.right -= px(10);
    if (id >= 210 && id <= 213)
    {
        icon(d->hDC, d->rcItem.left + px(12), r.top + (r.bottom - r.top - px(22)) / 2, id - 210, c);
        r.left = d->rcItem.left + px(43);
    }
    if (toggle)
    {
        Gdiplus::Graphics g(d->hDC);
        g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
        float s = dpi / 96.F, h = 20 * s, w = 38 * s, x = 6 * s, y = (d->rcItem.bottom - h) / 2;
        Gdiplus::GraphicsPath path;
        path.AddArc(x, y, h, h, 90, 180);
        path.AddArc(x + w - h, y, h, h, 270, 180);
        path.CloseFigure();
        Gdiplus::SolidBrush track(chosen ? Gdiplus::Color(255, 205, 151, 69)
                                         : Gdiplus::Color(255, 105, 108, 114));
        g.FillPath(&track, &path);
        Gdiplus::SolidBrush thumb(Gdiplus::Color(255, 255, 255, 255));
        bool unknown = GetPropW(d->hwndItem, L"unknown") != nullptr;
        g.FillEllipse(&thumb,
                      x +
                          (unknown  ? (w - h) / 2
                           : chosen ? w - h
                                    : 0) +
                          3 * s,
                      y + 3 * s, h - 6 * s, h - 6 * s);
        RECT textRect = d->rcItem;
        textRect.left = (int)(x + w + 8 * s);
        DrawTextW(d->hDC, caption.c_str(), -1, &textRect, DT_VCENTER | DT_SINGLELINE | DT_LEFT | DT_NOPREFIX);
    }
    else if (theme)
    {
        Gdiplus::Graphics graphics(d->hDC);
        graphics.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
        float scale = dpi / 96.F;
        float x = (d->rcItem.left + d->rcItem.right) / 2.F;
        float y = (d->rcItem.top + d->rcItem.bottom) / 2.F;
        Gdiplus::Color color(255, GetRValue(c), GetGValue(c), GetBValue(c));
        Gdiplus::Pen pen(color, 1.7F * scale);
        if (id == 16)
        {
            Gdiplus::GraphicsPath path;
            path.AddArc(x - 10 * scale, y - 10 * scale, 20 * scale, 20 * scale, -90, -270);
            path.AddBezier(x + 10 * scale, y, x - scale, y + 5 * scale, x - 5 * scale, y - scale, x,
                           y - 10 * scale);
            path.CloseFigure();
            Gdiplus::SolidBrush brush(color);
            graphics.FillPath(&brush, &path);
        }
        else
        {
            graphics.DrawEllipse(&pen, x - 5 * scale, y - 5 * scale, 10 * scale, 10 * scale);
            for (int i = 0; i < 8; i++)
            {
                double angle = i * 3.141592653589793 / 4;
                graphics.DrawLine(&pen, x + (float)cos(angle) * 8 * scale, y + (float)sin(angle) * 8 * scale,
                                  x + (float)cos(angle) * 11 * scale, y + (float)sin(angle) * 11 * scale);
            }
        }
    }
    else
    {
        if (main && id == 20)
            r.left += px(8);
        DrawTextW(d->hDC, caption.c_str(), -1, &r,
                  ((main && id == 20) || (id >= 210 && id <= 213) || GetPropW(d->hwndItem, L"left")
                       ? DT_LEFT
                       : DT_CENTER) |
                      DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
    }
    if ((d->itemState & ODS_FOCUS) && !(d->itemState & ODS_NOFOCUSRECT) &&
        !(SendMessageW(d->hwndItem, WM_QUERYUISTATE, 0, 0) & UISF_HIDEFOCUS))
    {
        RECT focus = d->rcItem;
        InflateRect(&focus, -px(3), -px(3));
        DrawFocusRect(d->hDC, &focus);
    }
    return TRUE;
}
static LRESULT drawButton(DRAWITEMSTRUCT *item)
{
    const int width = item->rcItem.right - item->rcItem.left;
    const int height = item->rcItem.bottom - item->rcItem.top;
    if (width <= 0 || height <= 0)
        return TRUE;
    struct Frame
    {
        HDC dc;
        HBITMAP bitmap;
        HGDIOBJ previous = nullptr;
        Frame(HDC target, int width, int height)
            : dc(CreateCompatibleDC(target)), bitmap(CreateCompatibleBitmap(target, width, height))
        {
            if (dc && bitmap)
                previous = SelectObject(dc, bitmap);
        }
        ~Frame()
        {
            if (previous)
                SelectObject(dc, previous);
            if (bitmap)
                DeleteObject(bitmap);
            if (dc)
                DeleteDC(dc);
        }
        Frame(const Frame &) = delete;
        Frame &operator=(const Frame &) = delete;
    } frame(item->hDC, width, height);
    if (!frame.previous)
    {
        const int saved = SaveDC(item->hDC);
        LRESULT result = paintButton(item);
        if (saved)
            RestoreDC(item->hDC, saved);
        return result;
    }
    DRAWITEMSTRUCT buffered = *item;
    buffered.hDC = frame.dc;
    buffered.rcItem = {0, 0, width, height};
    // GDI/GDI+ draw in separate steps. Present only the finished background,
    // icon, caption and focus cue, including draws outside the scroll viewport.
    paintButton(&buffered);
    BitBlt(item->hDC, item->rcItem.left, item->rcItem.top, width, height, frame.dc, 0, 0, SRCCOPY);
    return TRUE;
}

static void drawArtwork(HDC dc, const RECT &rect)
{
    int width = rect.right - rect.left, height = rect.bottom - rect.top;
    if (width < 1 || height < 1)
        return;
    Gdiplus::Graphics g(dc);
    g.SetClip(Gdiplus::Rect(rect.left, rect.top, width, height));
    Gdiplus::SolidBrush base(Gdiplus::Color(255, 24, 25, 29));
    g.FillRectangle(&base, rect.left, rect.top, width, height);
    if (artwork)
    {
        float iw = (float)artwork->GetWidth(), ih = (float)artwork->GetHeight(),
              scale = std::max(width / iw, height / ih);
        g.SetInterpolationMode(Gdiplus::InterpolationModeHighQualityBicubic);
        g.DrawImage(artwork.get(), rect.left + (width - iw * scale) / 2, rect.top + (height - ih * scale) / 2,
                    iw * scale, ih * scale);
    }
    Gdiplus::LinearGradientBrush shade(Gdiplus::Point(rect.left, rect.top),
                                       Gdiplus::Point(rect.right, rect.top), Gdiplus::Color(155, 10, 12, 17),
                                       Gdiplus::Color(0, 0, 0, 0));
    shade.SetWrapMode(Gdiplus::WrapModeTileFlipXY);
    g.FillRectangle(&shade, rect.left, rect.top, width, height);
    int fadeHeight = std::max(1, height / 4);
    Gdiplus::LinearGradientBrush fade(Gdiplus::Point(rect.left, rect.bottom - fadeHeight),
                                      Gdiplus::Point(rect.left, rect.bottom), Gdiplus::Color(0, 0, 0, 0),
                                      Gdiplus::Color(200, 22, 23, 27));
    fade.SetWrapMode(Gdiplus::WrapModeTileFlipXY);
    g.FillRectangle(&fade, rect.left, rect.bottom - fadeHeight, width, fadeHeight);
}
static HWND modalWindow(const wchar_t *name, const S &title, int width, int height, HWND owner)
{
    DWORD style = WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN, extra = WS_EX_CONTROLPARENT;
    RECT rect{0, 0, px(width), px(height)}, parent{};
    AdjustWindowRectEx(&rect, style, FALSE, extra);
    GetWindowRect(owner, &parent);
    int w = rect.right - rect.left, h = rect.bottom - rect.top;
    MONITORINFO monitor{sizeof(monitor)};
    GetMonitorInfoW(MonitorFromWindow(owner, MONITOR_DEFAULTTONEAREST), &monitor);
    int x = std::clamp((parent.left + parent.right - w) / 2, monitor.rcWork.left,
                       std::max(monitor.rcWork.left, monitor.rcWork.right - w));
    int y = std::clamp((parent.top + parent.bottom - h) / 2, monitor.rcWork.top,
                       std::max(monitor.rcWork.top, monitor.rcWork.bottom - h));
    return CreateWindowExW(extra, name, title.c_str(), style, x, y, w, h, owner, nullptr, instance, nullptr);
}
#include "settings.inl"

static void mainLayout()
{
    RECT r;
    GetClientRect(mainWindow, &r);
    int w = MulDiv(r.right, 96, dpi), h = MulDiv(r.bottom, 96, dpi);
    int footer = h - footerHeight;
    int aboutWidth = 116;
    int flexible = w - 64 - aboutWidth;
    int left = flexible * 60 / 100;
    int right = w - 32 - (32 + left + aboutWidth + 20);
    place(modCombo, 32, footer + 45, left - 10, 44);
    place(mainControls[5], 32 + left, footer + 45, aboutWidth - 24, 44);
    place(playButton, w - 32 - right, footer + 20, right, 78);
    int x = 32;
    HDC dc = GetDC(mainWindow);
    auto previous = SelectObject(dc, font);
    for (size_t i : {2u, 3u, 4u, 6u})
    {
        S label = windowText(mainControls[i]);
        SIZE size{};
        GetTextExtentPoint32W(dc, label.c_str(), (int)label.size(), &size);
        int bw = MulDiv(size.cx, 96, dpi) + 36;
        place(mainControls[i], x, footer + 124, bw, 40);
        x += bw + 8;
    }
    SelectObject(dc, previous);
    ReleaseDC(mainWindow, dc);
    place(mainControls[7], w - 32 - 94, footer + 124, 44, 40);
    place(mainControls[8], w - 32 - 44, footer + 124, 44, 40);
    InvalidateRect(mainWindow, nullptr, TRUE);
}
static void saveLauncher()
{
    Map<S> m;
    if (selected >= 0 && selected < (int)modifications.size())
    {
        S p = modifications[selected].config;
        if (lower(p).find(lower(root) + L"\\") == 0)
            p = p.substr(root.size() + 1);
        m[L"ModSel"] = p;
    }
    const wchar_t *langs[] = {L"", L"en", L"ru", L"de", L"fr", L"pl", L"ko", L"zh"};
    m[L"language"] = langs[language];
    m[L"UiTheme"] = dark ? L"graphite" : L"light";
    settings(m);
    if (selected >= 0 && selected < (int)modifications.size())
        selectMod(modifications[selected]);
}
static void mainLabels()
{
    SetWindowTextW(playButton, tr(L"Play").c_str());
    const wchar_t *labels[] = {L"Settings", L"Information", L"Website", L"AboutMod"};
    for (int i = 0; i < 4; i++)
        SetWindowTextW(mainControls[i + 2], tr(labels[i]).c_str());
    const wchar_t *langs[] = {L"", L"EN", L"RU", L"DE", L"FR", L"PL", L"KO", L"ZH"};
    SetWindowTextW(mainControls[6], langs[language]);
    SetWindowTextW(mainControls[7], tr(L"DarkTheme").c_str());
    SetWindowTextW(mainControls[8], tr(L"LightTheme").c_str());
    S title = selected >= 0 ? modifications[selected].title : tr(L"NoMods");
    if (selected >= 0 && !modifications[selected].version.empty())
        title += L"  ·  " + modifications[selected].version;
    SetWindowTextW(modCombo, (title + L"   ›").c_str());
    titleTheme(mainWindow);
    mainLayout();
    InvalidateRect(mainWindow, nullptr, TRUE);
}
static void mainCreate()
{
    modCombo = button(mainWindow, L"", 20);
    playButton = button(mainWindow, tr(L"Play"), 10);
    SendMessageW(modCombo, WM_SETFONT, (WPARAM)choiceFont, FALSE);
    SendMessageW(playButton, WM_SETFONT, (WPARAM)playFont, FALSE);
    mainControls = {modCombo, playButton};
    for (int i = 0; i < 7; i++)
        mainControls.push_back(button(mainWindow, L"", 11 + i));
    S last = setting(L"ModSel");
    for (size_t i = 0; i < modifications.size(); i++)
    {
        auto &m = modifications[i];
        if (lower(full(last.empty() ? L"." : last)) == lower(full(m.config)))
            selected = (int)i;
    }
    if (selected < 0 && !modifications.empty())
        selected = 0;
    if (selected < 0)
    {
        EnableWindow(modCombo, FALSE);
    }
    EnableWindow(playButton, selected >= 0);
    EnableWindow(mainControls[5], selected >= 0);
    mainLabels();
    mainLayout();
}
#include "card.inl"

static void paintMain(HWND w)
{
    PAINTSTRUCT ps;
    HDC dc = BeginPaint(w, &ps);
    RECT c;
    GetClientRect(w, &c);
    HDC memory = CreateCompatibleDC(dc);
    HBITMAP bmp = CreateCompatibleBitmap(dc, c.right, c.bottom);
    auto old = SelectObject(memory, bmp);
    FillRect(memory, &c, bgBrush);
    int bottom = c.bottom - px(footerHeight);
    RECT hero{0, 0, c.right, bottom};
    drawArtwork(memory, hero);
    SetBkMode(memory, TRANSPARENT);
    SetTextColor(memory, RGB(248, 238, 218));
    SelectObject(memory, headingFont);
    RECT title{px(36), px(54), c.right - px(32), px(200)};
    S heading = language == 2 ? L"ПРОКЛЯТЫЕ\r\nЗЕМЛИ" : L"EVIL\r\nISLANDS";
    DrawTextW(memory, heading.c_str(), -1, &title, DT_LEFT);
    SelectObject(memory, eyebrowFont);
    RECT eyebrow{px(39), px(28), c.right, px(50)};
    DrawTextW(memory, L"E V I L   I S L A N D S", -1, &eyebrow, DT_LEFT);
    SetTextColor(memory, ink());
    SelectObject(memory, captionFont);
    RECT modLabel{px(35), bottom + px(23), c.right - px(310), bottom + px(43)};
    DrawTextW(memory, tr(L"SelectMod").c_str(), -1, &modLabel, DT_SINGLELINE);
    BitBlt(dc, 0, 0, c.right, c.bottom, memory, 0, 0, SRCCOPY);
    SelectObject(memory, old);
    DeleteObject(bmp);
    DeleteDC(memory);
    EndPaint(w, &ps);
}
static void startGame()
{
    if (selected < 0)
        fail(tr(L"NoMods"));
    saveLauncher();
    game.start(modifications[selected]);
    DestroyWindow(mainWindow);
}
struct ModPicker
{
    HWND window = nullptr, search = nullptr, searchHost = nullptr, list = nullptr, close = nullptr;
    std::vector<int> indices;
};
static ModPicker *modPicker = nullptr;
static void filterMods()
{
    auto &picker = *modPicker;
    S query = lower(trim(windowText(picker.search)));
    SendMessageW(picker.list, LB_RESETCONTENT, 0, 0);
    picker.indices.clear();
    int index = 0;
    for (size_t i = 0; i < modifications.size(); i++)
    {
        const auto &mod = modifications[i];
        S label = mod.title + (mod.version.empty() ? L"" : L"  ·  " + mod.version);
        if (!query.empty() && lower(label).find(query) == S::npos)
            continue;
        if ((int)i == selected)
            label = L"✓  " + label;
        SendMessageW(picker.list, LB_ADDSTRING, 0, (LPARAM)label.c_str());
        picker.indices.push_back((int)i);
        if ((int)i == selected)
            SendMessageW(picker.list, LB_SETCURSEL, index, 0);
        index++;
    }
}
static void acceptMod()
{
    int index = (int)SendMessageW(modPicker->list, LB_GETCURSEL, 0, 0);
    if (index >= 0 && index < (int)modPicker->indices.size())
    {
        selected = modPicker->indices[index];
        mainLabels();
        saveLauncher();
        DestroyWindow(modPicker->window);
    }
}
static LRESULT CALLBACK pickerProc(HWND w, UINT m, WPARAM a, LPARAM b)
{
    switch (m)
    {
    case WM_GETMINMAXINFO:
        ((MINMAXINFO *)b)->ptMinTrackSize = {px(480), px(320)};
        return 0;
    case WM_SIZE:
        if (modPicker && modPicker->search)
        {
            RECT c;
            GetClientRect(w, &c);
            int width = MulDiv(c.right, 96, dpi), height = MulDiv(c.bottom, 96, dpi);
            locate(modPicker->searchHost, 24, 94, width - 48, 38);
            locate(modPicker->search, 12, 10, width - 72, 18);
            locate(modPicker->list, 24, 148, width - 48, std::max(30, height - 226));
            int bw = std::max(80, measured(tr(L"Close"), sectionFont, 200) + 30);
            locate(modPicker->close, width - 24 - bw, height - 62, bw, 38);
            RedrawWindow(w, nullptr, nullptr, RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN);
        }
        return 0;
    case WM_COMMAND:
        try
        {
            if (LOWORD(a) == 901 && HIWORD(a) == EN_CHANGE)
                filterMods();
            if (LOWORD(a) == 902 && HIWORD(a) == LBN_DBLCLK)
                acceptMod();
            if (LOWORD(a) == 903)
                DestroyWindow(w);
        }
        catch (const std::exception &e)
        {
            error(w, e);
        }
        return 0;
    case WM_DRAWITEM: {
        auto *d = (DRAWITEMSTRUCT *)b;
        if (d->CtlType != ODT_LISTBOX)
            return drawButton(d);
        if (d->itemID == (UINT)-1)
            return TRUE;
        bool chosen = d->itemID < modPicker->indices.size() && modPicker->indices[d->itemID] == selected;
        FillRect(d->hDC, &d->rcItem, bgBrush);
        RECT item = d->rcItem;
        item.bottom -= px(8);
        item.right -= px(8);
        HBRUSH brush = CreateSolidBrush(chosen ? (dark ? RGB(61, 53, 40) : RGB(248, 234, 210)) : surface());
        FillRect(d->hDC, &item, brush);
        DeleteObject(brush);
        HBRUSH border = CreateSolidBrush(chosen ? RGB(181, 135, 68)
                                         : dark ? RGB(77, 80, 86)
                                                : RGB(200, 203, 208));
        FrameRect(d->hDC, &item, border);
        DeleteObject(border);
        int size = (int)SendMessageW(d->hwndItem, LB_GETTEXTLEN, d->itemID, 0);
        S label(size + 1, L'\0');
        SendMessageW(d->hwndItem, LB_GETTEXT, d->itemID, (LPARAM)&label[0]);
        SetTextColor(d->hDC, chosen ? RGB(239, 194, 123) : ink());
        SetBkMode(d->hDC, TRANSPARENT);
        SelectObject(d->hDC, sectionFont);
        RECT r = item;
        InflateRect(&r, -px(12), 0);
        DrawTextW(d->hDC, label.c_str(), -1, &r, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
        if (d->itemState & ODS_FOCUS)
            DrawFocusRect(d->hDC, &r);
        return TRUE;
    }
    case WM_CTLCOLORSTATIC:
    case WM_CTLCOLOREDIT:
    case WM_CTLCOLORLISTBOX:
        SetTextColor((HDC)a, ink());
        SetBkColor((HDC)a, surface());
        return (LRESULT)surfaceBrush;
    case WM_ERASEBKGND: {
        RECT c;
        GetClientRect(w, &c);
        FillRect((HDC)a, &c, bgBrush);
        return 1;
    }
    case WM_PAINT: {
        PAINTSTRUCT ps;
        HDC dc = BeginPaint(w, &ps);
        SetTextColor(dc, ink());
        SetBkMode(dc, TRANSPARENT);
        SelectObject(dc, settingsHeadingFont);
        RECT heading{px(24), px(24), px(556), px(58)};
        DrawTextW(dc, tr(L"ModPicker").c_str(), -1, &heading, DT_LEFT | DT_SINGLELINE);
        SelectObject(dc, font);
        RECT r{px(24), px(70), px(556), px(92)};
        DrawTextW(dc, tr(L"Search").c_str(), -1, &r, DT_LEFT | DT_SINGLELINE);
        EndPaint(w, &ps);
        return 0;
    }
    case WM_CLOSE:
        DestroyWindow(w);
        return 0;
    }
    return DefWindowProcW(w, m, a, b);
}
static void pickMod()
{
    ModPicker picker;
    modPicker = &picker;
    picker.window = modalWindow(L"ReStarter.Native.ModPicker", tr(L"ModPicker"), 580,
                                std::min(460, 210 + (int)modifications.size() * 48), mainWindow);
    try
    {
        picker.searchHost = input(picker.window, L"", 901, false, picker.search);
        picker.list = control(picker.window, L"LISTBOX", L"", 902,
                              WS_TABSTOP | WS_VSCROLL | LBS_NOTIFY | LBS_OWNERDRAWFIXED | LBS_HASSTRINGS |
                                  LBS_NOINTEGRALHEIGHT);
        SetWindowSubclass(
            picker.list,
            [](HWND w, UINT m, WPARAM a, LPARAM b, UINT_PTR, DWORD_PTR) -> LRESULT {
                auto result = DefSubclassProc(w, m, a, b);
                if (m == WM_LBUTTONUP && HIWORD(SendMessageW(w, LB_ITEMFROMPOINT, 0, b)) == 0)
                    PostMessageW(GetParent(w), WM_COMMAND, MAKEWPARAM(902, LBN_DBLCLK), (LPARAM)w);
                return result;
            },
            2, 0);
        SendMessageW(picker.list, LB_SETITEMHEIGHT, 0, px(46));
        picker.close = button(picker.window, tr(L"Close"), 903);
        SendMessageW(picker.close, WM_SETFONT, (WPARAM)sectionFont, FALSE);
        filterMods();
        SendMessageW(picker.window, WM_SIZE, 0, 0);
        titleTheme(picker.window);
        EnableWindow(mainWindow, FALSE);
        ShowWindow(picker.window, SW_SHOW);
        SetFocus(picker.search);
        MSG msg;
        while (IsWindow(picker.window) && GetMessageW(&msg, nullptr, 0, 0) > 0)
        {
            if (msg.message == WM_KEYDOWN && msg.wParam == VK_ESCAPE)
            {
                DestroyWindow(picker.window);
                continue;
            }
            if (msg.message == WM_KEYDOWN && msg.wParam == VK_RETURN)
            {
                try
                {
                    acceptMod();
                }
                catch (const std::exception &e)
                {
                    error(picker.window, e);
                }
                continue;
            }
            if (!IsDialogMessageW(picker.window, &msg))
            {
                TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
        }
    }
    catch (...)
    {
        if (IsWindow(picker.window))
            DestroyWindow(picker.window);
        modPicker = nullptr;
        EnableWindow(mainWindow, TRUE);
        throw;
    }
    modPicker = nullptr;
    EnableWindow(mainWindow, TRUE);
    SetForegroundWindow(mainWindow);
}
static LRESULT CALLBACK mainProc(HWND w, UINT m, WPARAM a, LPARAM b)
{
    switch (m)
    {
    case WM_CREATE:
        mainWindow = w;
        mainCreate();
        return 0;
    case WM_SIZE:
        if (modCombo)
            mainLayout();
        return 0;
    case WM_GETMINMAXINFO:
        ((MINMAXINFO *)b)->ptMinTrackSize = {px(880), px(600)};
        return 0;
    case WM_PAINT:
        paintMain(w);
        return 0;
    case WM_ERASEBKGND:
        return 1;
    case WM_DRAWITEM:
        return drawButton((DRAWITEMSTRUCT *)b);
    case WM_CTLCOLORSTATIC:
    case WM_CTLCOLOREDIT:
    case WM_CTLCOLORLISTBOX:
        SetTextColor((HDC)a, ink());
        SetBkColor((HDC)a, surface());
        return (LRESULT)surfaceBrush;
    case WM_COMMAND:
        try
        {
            int id = LOWORD(a);
            if (id == 20 && HIWORD(a) == CBN_SELCHANGE)
            {
                selected = (int)SendMessageW(modCombo, CB_GETCURSEL, 0, 0);
                saveLauncher();
            }
            else if (HIWORD(a) == BN_CLICKED)
            {
                switch (id)
                {
                case 10:
                    startGame();
                    break;
                case 20:
                    pickMod();
                    break;
                case 11:
                    options();
                    break;
                case 12: {
                    const wchar_t *langs[] = {L"", L"en", L"ru", L"de", L"fr", L"pl", L"ko", L"zh"};
                    S p = join(root, L"lang\\" + S(langs[language]) + L"\\readme.txt");
                    if (!exists(p))
                        p = join(root, L"lang\\en\\readme.txt");
                    if (!exists(p))
                        p = join(root, L"ПРОЧИТАЙ.txt");
                    open(w, p);
                    break;
                }
                case 13:
                    open(w, L"https://vk.ru/ei_dejavu");
                    break;
                case 14:
                    about();
                    break;
                case 15:
                    language = language % 7 + 1;
                    mainLabels();
                    saveLauncher();
                    break;
                case 16:
                case 17:
                    dark = id == 16;
                    brushes();
                    mainLabels();
                    RedrawWindow(w, nullptr, nullptr, RDW_INVALIDATE | RDW_ALLCHILDREN);
                    saveLauncher();
                    break;
                }
            }
        }
        catch (const std::exception &e)
        {
            error(w, e);
        }
        return 0;
    case WM_CLOSE:
        try
        {
            saveLauncher();
            DestroyWindow(w);
        }
        catch (const std::exception &e)
        {
            error(w, e);
        }
        return 0;
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(w, m, a, b);
}
static void loadArtwork()
{
    S path = join(root, L"background.jpg");
    if (!exists(path))
        return;
    auto b = readFile(path);
    HGLOBAL mem = GlobalAlloc(GMEM_MOVEABLE, b.size());
    if (!mem)
        return;
    void *p = GlobalLock(mem);
    if (!p)
    {
        GlobalFree(mem);
        return;
    }
    memcpy(p, b.data(), b.size());
    GlobalUnlock(mem);
    IStream *stream = nullptr;
    if (FAILED(CreateStreamOnHGlobal(mem, TRUE, &stream)))
    {
        GlobalFree(mem);
        return;
    }
    auto image = Gdiplus::Bitmap::FromStream(stream);
    if (image && image->GetLastStatus() == Gdiplus::Ok)
        artwork.reset(image->Clone(0, 0, image->GetWidth(), image->GetHeight(), PixelFormat32bppARGB));
    delete image;
    stream->Release();
}
int run(HINSTANCE h)
{
    instance = h;
    wchar_t path[32768];
    DWORD n = GetModuleFileNameW(nullptr, path, 32768);
    if (!n || n >= 32768)
        fail(L"Cannot resolve launcher directory");
    root = path;
    root = root.substr(0, root.find_last_of(L"\\"));
    SetCurrentDirectoryW(root.c_str());
    profile.open();
    S lang = setting(L"language", PRIMARYLANGID(GetUserDefaultUILanguage()) == LANG_RUSSIAN ? L"ru" : L"en");
    const wchar_t *langs[] = {L"", L"en", L"ru", L"de", L"fr", L"pl", L"ko", L"zh"};
    language = 1;
    for (int i = 1; i <= 7; i++)
        if (lang == langs[i])
            language = i;
    dark = setting(L"UiTheme", L"graphite") != L"light";
    // System-DPI aware works on Windows 7 and keeps a consistent pixel contract.
    SetProcessDPIAware();
    HDC screen = GetDC(nullptr);
    dpi = GetDeviceCaps(screen, LOGPIXELSX);
    ReleaseDC(nullptr, screen);
    font =
        CreateFontW(-MulDiv(11, dpi, 72), 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET,
                    OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH, L"Segoe UI");
    headingFont =
        CreateFontW(-MulDiv(32, dpi, 72), 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE, DEFAULT_CHARSET,
                    OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH, L"Georgia");
    auto uiFont = [](int points, bool bold) {
        return CreateFontW(-MulDiv(points, dpi, 72), 0, 0, 0, bold ? FW_BOLD : FW_NORMAL, FALSE, FALSE, FALSE,
                           DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
                           DEFAULT_PITCH, L"Segoe UI");
    };
    eyebrowFont = uiFont(9, false);
    captionFont = uiFont(10, true);
    choiceFont = uiFont(12, false);
    playFont = uiFont(17, true);
    sectionFont = uiFont(10, false);
    settingsHeadingFont = uiFont(18, true);
    settingsBoldFont = uiFont(11, true);
    cardHeadingFont = uiFont(26, true);
    INITCOMMONCONTROLSEX common{sizeof(common), ICC_WIN95_CLASSES | ICC_STANDARD_CLASSES};
    if (!InitCommonControlsEx(&common))
        fail(L"Common controls initialization failed");
    brushes();
    modifications = mods();
    loadArtwork();
    WNDCLASSEXW wc{sizeof(wc)};
    wc.hInstance = h;
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.hIcon = (HICON)LoadImageW(h, MAKEINTRESOURCEW(101), IMAGE_ICON, 32, 32, LR_DEFAULTCOLOR);
    wc.hIconSm = (HICON)LoadImageW(h, MAKEINTRESOURCEW(101), IMAGE_ICON, px(16), px(16), LR_DEFAULTCOLOR);
    wc.lpfnWndProc = mainProc;
    wc.lpszClassName = L"ReStarter.Native.Main";
    wc.style = CS_HREDRAW | CS_VREDRAW;
    RegisterClassExW(&wc);
    wc.lpfnWndProc = settingsProc;
    wc.lpszClassName = L"ReStarter.Native.Settings";
    RegisterClassExW(&wc);
    wc.lpfnWndProc = viewportProc;
    wc.lpszClassName = L"ReStarter.Native.Viewport";
    RegisterClassExW(&wc);
    wc.lpfnWndProc = canvasProc;
    wc.lpszClassName = L"ReStarter.Native.Canvas";
    RegisterClassExW(&wc);
    wc.lpfnWndProc = inputProc;
    wc.lpszClassName = L"ReStarter.Native.Input";
    RegisterClassExW(&wc);
    wc.lpfnWndProc = cardProc;
    wc.lpszClassName = L"ReStarter.Native.Card";
    RegisterClassExW(&wc);
    wc.lpfnWndProc = cardBodyProc;
    wc.lpszClassName = L"ReStarter.Native.CardBody";
    RegisterClassExW(&wc);
    wc.lpfnWndProc = pickerProc;
    wc.lpszClassName = L"ReStarter.Native.ModPicker";
    RegisterClassExW(&wc);
    RECT initial{0, 0, px(960), px(650)};
    AdjustWindowRectEx(&initial, WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN, FALSE, WS_EX_CONTROLPARENT);
    HWND w = CreateWindowExW(WS_EX_CONTROLPARENT, L"ReStarter.Native.Main", L"ReStarter",
                             WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN, CW_USEDEFAULT, CW_USEDEFAULT,
                             initial.right - initial.left, initial.bottom - initial.top, nullptr, nullptr, h,
                             nullptr);
    if (!w)
        check(GetLastError(), L"Create main window");
    ShowWindow(w, SW_SHOW);
    UpdateWindow(w);
    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0) > 0)
    {
        if (!IsDialogMessageW(w, &msg))
        {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
    }
    artwork.reset();
    DeleteObject(font);
    DeleteObject(headingFont);
    DeleteObject(eyebrowFont);
    DeleteObject(captionFont);
    DeleteObject(choiceFont);
    DeleteObject(playFont);
    DeleteObject(sectionFont);
    DeleteObject(settingsHeadingFont);
    DeleteObject(settingsBoldFont);
    DeleteObject(cardHeadingFont);
    DeleteObject(bgBrush);
    DeleteObject(surfaceBrush);
    return (int)msg.wParam;
}
} // namespace ei
int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, LPWSTR, int)
{
    HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    if (FAILED(hr))
        return 1;
    Gdiplus::GdiplusStartupInput in;
    ULONG_PTR token = 0;
    if (Gdiplus::GdiplusStartup(&token, &in, nullptr) != Gdiplus::Ok)
    {
        CoUninitialize();
        return 1;
    }
    HANDLE mutex = CreateMutexW(nullptr, TRUE, L"ReStarterSharp");
    if (!mutex || GetLastError() == ERROR_ALREADY_EXISTS)
    {
        MessageBoxW(nullptr, L"ReStarter is already running.", L"ReStarter", MB_ICONINFORMATION);
        if (mutex)
            CloseHandle(mutex);
        Gdiplus::GdiplusShutdown(token);
        CoUninitialize();
        return 1;
    }
    int result = 1;
    try
    {
        result = ei::run(instance);
    }
    catch (const std::exception &e)
    {
        ei::error(nullptr, e);
    }
    ei::artwork.reset();
    ReleaseMutex(mutex);
    CloseHandle(mutex);
    Gdiplus::GdiplusShutdown(token);
    CoUninitialize();
    return result;
}
