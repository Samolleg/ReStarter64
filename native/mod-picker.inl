// Identical UI component in EIStarter and ReStarter; persistence stays in saveLauncher().
struct ModPicker
{
    HWND window = nullptr, search = nullptr, searchHost = nullptr, list = nullptr, close = nullptr;
    std::vector<int> indices;
    int hover = -1, pressed = -1;
    bool searchable = false;
};
static ModPicker *modPicker = nullptr;
static constexpr int modRowHeight = 42;
static constexpr UINT acceptModMessage = WM_APP + 45;
static int modListTop()
{
    return modPicker->searchable ? 100 : 52;
}
static void layoutModPicker()
{
    auto &picker = *modPicker;
    if (!picker.list || !picker.close)
        return;
    RECT c;
    GetClientRect(picker.window, &c);
    const int width = MulDiv(c.right, 96, dpi), height = MulDiv(c.bottom, 96, dpi);
    locate(picker.searchHost, 20, 48, width - 40, 36);
    locate(picker.search, 36, 8, width - 88, 20);
    const int available = std::max(px(modRowHeight), (int)c.bottom - px(modListTop() + 66));
    const int rows = std::max(1, available / px(modRowHeight));
    SetWindowPos(picker.list, nullptr, px(20), px(modListTop()), c.right - px(40), rows * px(modRowHeight),
                 SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOREDRAW | SWP_NOCOPYBITS);
    const int top = (int)SendMessageW(picker.list, LB_GETTOPINDEX, 0, 0);
    const int lastTop = std::max(0, (int)picker.indices.size() - rows);
    if (top > lastTop)
        SendMessageW(picker.list, LB_SETTOPINDEX, lastTop, 0);
    ShowScrollBar(picker.list, SB_VERT, (int)picker.indices.size() > rows);
    const int closeWidth = std::max(80, measured(tr(L"Close"), sectionFont, 200) + 28);
    locate(picker.close, width - 20 - closeWidth, height - 52, closeWidth, 34);
    RedrawWindow(picker.window, nullptr, nullptr, RDW_INVALIDATE | RDW_ALLCHILDREN);
}
static void filterMods()
{
    auto &picker = *modPicker;
    const S query = lower(trim(windowText(picker.search)));
    // Publish the complete mapping before ListBox can request owner drawing.
    std::vector<int> matches;
    for (size_t i = 0; i < modifications.size(); ++i)
    {
        const auto &mod = modifications[i];
        if (query.empty() || lower(mod.title + L" " + mod.version).find(query) != S::npos)
            matches.push_back((int)i);
    }
    SendMessageW(picker.list, WM_SETREDRAW, FALSE, 0);
    struct RedrawList
    {
        HWND list;
        ~RedrawList()
        {
            SendMessageW(list, WM_SETREDRAW, TRUE, 0);
            InvalidateRect(list, nullptr, FALSE);
        }
    } redraw{picker.list};
    picker.indices = std::move(matches);
    picker.hover = picker.pressed = -1;
    SendMessageW(picker.list, LB_RESETCONTENT, 0, 0);
    int cursor = 0;
    for (size_t i = 0; i < picker.indices.size(); ++i)
    {
        const auto &mod = modifications[picker.indices[i]];
        S label = mod.title + (mod.version.empty() ? L"" : L" · " + mod.version);
        if (SendMessageW(picker.list, LB_ADDSTRING, 0, (LPARAM)label.c_str()) < 0)
            fail(L"Cannot populate mod selection");
        if (picker.indices[i] == selected)
            cursor = (int)i;
    }
    if (!picker.indices.empty())
    {
        SendMessageW(picker.list, LB_SETCURSEL, cursor, 0);
    }
    layoutModPicker();
}
static void acceptMod()
{
    const int index = (int)SendMessageW(modPicker->list, LB_GETCURSEL, 0, 0);
    if (index < 0 || index >= (int)modPicker->indices.size())
        return;
    const int previous = selected;
    selected = modPicker->indices[index];
    try
    {
        saveLauncher();
    }
    catch (...)
    {
        selected = previous;
        throw;
    }
    mainLabels();
    DestroyWindow(modPicker->window);
}
static int modAtPoint(HWND list, POINT point)
{
    RECT bounds;
    GetClientRect(list, &bounds);
    if (!PtInRect(&bounds, point))
        return -1;
    const DWORD hit = (DWORD)SendMessageW(list, LB_ITEMFROMPOINT, 0, MAKELPARAM(point.x, point.y));
    const int index = LOWORD(hit);
    if (HIWORD(hit) || index >= (int)modPicker->indices.size())
        return -1;
    RECT item;
    if (SendMessageW(list, LB_GETITEMRECT, index, (LPARAM)&item) == LB_ERR)
        return -1;
    item.bottom -= px(6);
    return PtInRect(&item, point) ? index : -1;
}
static void paintModItem(const DRAWITEMSTRUCT &d)
{
    if (d.itemID >= modPicker->indices.size())
        return;
    const auto &mod = modifications[modPicker->indices[d.itemID]];
    const bool chosen = modPicker->indices[d.itemID] == selected;
    const bool focused = (d.itemState & ODS_FOCUS) && GetFocus() == d.hwndItem;
    const bool hovered = (int)d.itemID == modPicker->hover;
    FillRect(d.hDC, &d.rcItem, bgBrush);
    RECT item = d.rcItem;
    item.bottom -= px(6);
    const COLORREF color = chosen               ? (dark ? RGB(61, 53, 40) : RGB(248, 234, 210))
                           : hovered || focused ? (dark ? RGB(60, 63, 69) : RGB(235, 238, 242))
                                                : surface();
    HBRUSH fill = CreateSolidBrush(color);
    FillRect(d.hDC, &item, fill);
    DeleteObject(fill);
    HBRUSH edge = CreateSolidBrush(chosen || focused ? RGB(181, 135, 68)
                                   : dark            ? RGB(77, 80, 86)
                                                     : RGB(200, 203, 208));
    FrameRect(d.hDC, &item, edge);
    DeleteObject(edge);
    SetBkMode(d.hDC, TRANSPARENT);
    SelectObject(d.hDC, sectionFont);
    RECT title = item;
    title.left += px(34);
    title.right -= px(12);
    const COLORREF foreground = chosen ? (dark ? RGB(239, 194, 123) : RGB(102, 65, 14)) : ink();
    if (chosen)
    {
        HPEN tick = CreatePen(PS_SOLID, std::max(1, px(2)), foreground);
        auto previous = SelectObject(d.hDC, tick);
        const int x = item.left + px(12), y = (item.top + item.bottom) / 2;
        MoveToEx(d.hDC, x, y, nullptr);
        LineTo(d.hDC, x + px(4), y + px(4));
        LineTo(d.hDC, x + px(11), y - px(5));
        SelectObject(d.hDC, previous);
        DeleteObject(tick);
    }
    if (!mod.version.empty())
    {
        SIZE size{};
        GetTextExtentPoint32W(d.hDC, mod.version.c_str(), (int)mod.version.size(), &size);
        const int width = std::min(size.cx, (title.right - title.left) / 3);
        RECT version = title;
        version.left = version.right - width;
        SetTextColor(d.hDC, chosen ? foreground : dark ? RGB(167, 171, 179) : RGB(101, 108, 119));
        DrawTextW(d.hDC, mod.version.c_str(), -1, &version,
                  DT_RIGHT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
        title.right = version.left - px(16);
    }
    SetTextColor(d.hDC, foreground);
    DrawTextW(d.hDC, mod.title.c_str(), -1, &title,
              DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
}
static LRESULT drawModItem(const DRAWITEMSTRUCT &item)
{
    if (item.itemID == (UINT)-1)
        return TRUE;
    const int width = item.rcItem.right - item.rcItem.left;
    const int height = item.rcItem.bottom - item.rcItem.top;
    if (width <= 0 || height <= 0)
        return TRUE;
    HDC memory = CreateCompatibleDC(item.hDC);
    HBITMAP bitmap = CreateCompatibleBitmap(item.hDC, width, height);
    if (memory && bitmap)
    {
        auto previous = SelectObject(memory, bitmap);
        DRAWITEMSTRUCT buffered = item;
        buffered.hDC = memory;
        buffered.rcItem = {0, 0, width, height};
        paintModItem(buffered);
        BitBlt(item.hDC, item.rcItem.left, item.rcItem.top, width, height, memory, 0, 0, SRCCOPY);
        SelectObject(memory, previous);
    }
    else
    {
        const int saved = SaveDC(item.hDC);
        paintModItem(item);
        if (saved)
            RestoreDC(item.hDC, saved);
    }
    if (bitmap)
        DeleteObject(bitmap);
    if (memory)
        DeleteDC(memory);
    return TRUE;
}
static LRESULT CALLBACK modListProc(HWND w, UINT m, WPARAM a, LPARAM b, UINT_PTR, DWORD_PTR)
{
    if (m == WM_MOUSEMOVE)
    {
        int hover = modAtPoint(w, {GET_X_LPARAM(b), GET_Y_LPARAM(b)});
        if (hover != modPicker->hover)
        {
            modPicker->hover = hover;
            InvalidateRect(w, nullptr, FALSE);
        }
        TRACKMOUSEEVENT track{sizeof(track), TME_LEAVE, w, 0};
        TrackMouseEvent(&track);
    }
    else if (m == WM_MOUSELEAVE)
    {
        modPicker->hover = -1;
        InvalidateRect(w, nullptr, FALSE);
    }
    else if (m == WM_MOUSEWHEEL || m == WM_VSCROLL)
    {
        modPicker->hover = -1;
        InvalidateRect(w, nullptr, FALSE);
    }
    else if (m == WM_SETCURSOR && LOWORD(b) == HTCLIENT)
    {
        POINT point;
        GetCursorPos(&point);
        ScreenToClient(w, &point);
        SetCursor(LoadCursorW(nullptr, modAtPoint(w, point) >= 0 ? IDC_HAND : IDC_ARROW));
        return TRUE;
    }
    else if (m == WM_LBUTTONDOWN)
        modPicker->pressed = modAtPoint(w, {GET_X_LPARAM(b), GET_Y_LPARAM(b)});
    else if (m == WM_LBUTTONUP)
    {
        const int hit = modAtPoint(w, {GET_X_LPARAM(b), GET_Y_LPARAM(b)});
        const int pressed = modPicker->pressed;
        modPicker->pressed = -1;
        const auto result = DefSubclassProc(w, m, a, b);
        if (hit >= 0 && hit == pressed)
            PostMessageW(GetParent(w), acceptModMessage, 0, 0);
        return result;
    }
    else if (m == WM_ERASEBKGND)
    {
        RECT bounds;
        GetClientRect(w, &bounds);
        FillRect((HDC)a, &bounds, bgBrush);
        return 1;
    }
    else if (m == WM_PAINT && modPicker->indices.empty())
    {
        PAINTSTRUCT paint;
        HDC dc = BeginPaint(w, &paint);
        RECT bounds;
        GetClientRect(w, &bounds);
        FillRect(dc, &bounds, bgBrush);
        SetBkMode(dc, TRANSPARENT);
        SetTextColor(dc, ink());
        SelectObject(dc, sectionFont);
        DrawTextW(dc, tr(L"NoChoices").c_str(), -1, &bounds,
                  DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
        EndPaint(w, &paint);
        return 0;
    }
    return DefSubclassProc(w, m, a, b);
}
static LRESULT CALLBACK modSearchProc(HWND w, UINT m, WPARAM a, LPARAM b, UINT_PTR, DWORD_PTR)
{
    if (m == WM_PAINT)
    {
        PAINTSTRUCT paint;
        HDC dc = BeginPaint(w, &paint);
        RECT bounds;
        GetClientRect(w, &bounds);
        FillRect(dc, &bounds, surfaceBrush);
        HBRUSH edge = CreateSolidBrush(GetFocus() == modPicker->search ? gold
                                       : dark                          ? RGB(77, 80, 86)
                                                                       : RGB(200, 203, 208));
        FrameRect(dc, &bounds, edge);
        DeleteObject(edge);
        {
            Gdiplus::Graphics graphics(dc);
            graphics.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
            const COLORREF color = dark ? RGB(167, 171, 179) : RGB(101, 108, 119);
            Gdiplus::Pen pen(Gdiplus::Color(255, GetRValue(color), GetGValue(color), GetBValue(color)),
                             1.5F * dpi / 96.F);
            const float scale = dpi / 96.F;
            graphics.DrawEllipse(&pen, 12 * scale, 10 * scale, 11 * scale, 11 * scale);
            graphics.DrawLine(&pen, 21 * scale, 19 * scale, 27 * scale, 25 * scale);
        }
        EndPaint(w, &paint);
        return 0;
    }
    return DefSubclassProc(w, m, a, b);
}
static LRESULT CALLBACK pickerProc(HWND w, UINT m, WPARAM a, LPARAM b)
{
    switch (m)
    {
    case WM_GETMINMAXINFO: {
        RECT minimum{0, 0, px(380), px(modListTop() + 66) + px(modRowHeight)};
        AdjustWindowRectEx(&minimum, (DWORD)GetWindowLongPtrW(w, GWL_STYLE), FALSE,
                           (DWORD)GetWindowLongPtrW(w, GWL_EXSTYLE));
        ((MINMAXINFO *)b)->ptMinTrackSize = {minimum.right - minimum.left, minimum.bottom - minimum.top};
        return 0;
    }
    case WM_SIZE:
        layoutModPicker();
        return 0;
    case acceptModMessage:
    case WM_COMMAND:
        try
        {
            if (m == acceptModMessage)
                acceptMod();
            else if (LOWORD(a) == 901 && HIWORD(a) == EN_CHANGE && modPicker->list)
                filterMods();
            else if (LOWORD(a) == 903)
                DestroyWindow(w);
        }
        catch (const std::exception &e)
        {
            error(w, e);
        }
        return 0;
    case WM_DRAWITEM: {
        auto *item = (DRAWITEMSTRUCT *)b;
        return item->CtlType == ODT_LISTBOX ? drawModItem(*item) : drawButton(item);
    }
    case WM_CTLCOLORLISTBOX:
        SetTextColor((HDC)a, ink());
        SetBkColor((HDC)a, background());
        return (LRESULT)bgBrush;
    case WM_ERASEBKGND:
        return 1;
    case WM_PAINT: {
        PAINTSTRUCT paint;
        HDC dc = BeginPaint(w, &paint);
        RECT bounds;
        GetClientRect(w, &bounds);
        FillRect(dc, &bounds, bgBrush);
        SetTextColor(dc, ink());
        SetBkMode(dc, TRANSPARENT);
        SelectObject(dc, settingsBoldFont);
        RECT title{px(20), px(16), bounds.right - px(20), px(40)};
        DrawTextW(dc, tr(L"ModPicker").c_str(), -1, &title,
                  DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
        EndPaint(w, &paint);
        return 0;
    }
    case WM_CLOSE:
        DestroyWindow(w);
        return 0;
    }
    return DefWindowProcW(w, m, a, b);
}
static void pickMod(const wchar_t *windowClass)
{
    ModPicker picker;
    picker.searchable = modifications.size() > 5;
    modPicker = &picker;
    MONITORINFO monitor{sizeof(monitor)};
    if (!GetMonitorInfoW(MonitorFromWindow(mainWindow, MONITOR_DEFAULTTONEAREST), &monitor))
        check(GetLastError(), L"Mod selection monitor");
    RECT frame{};
    AdjustWindowRectEx(&frame, WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN, FALSE, WS_EX_CONTROLPARENT);
    const int availableHeight =
        MulDiv(monitor.rcWork.bottom - monitor.rcWork.top - frame.bottom + frame.top, 96, dpi) - 16;
    const int availableWidth =
        MulDiv(monitor.rcWork.right - monitor.rcWork.left - frame.right + frame.left, 96, dpi) - 16;
    const int visibleRows = (int)std::clamp(modifications.size(), size_t(1), size_t(7));
    // Sum the actual pixel row heights before converting back to dialog units.
    // At fractional DPI, px(n * rowHeight) can be smaller than n * px(rowHeight).
    const int contentPixels = px(modListTop() + 66) + visibleRows * px(modRowHeight);
    const int height = std::min(availableHeight, (int)((contentPixels * 96LL + dpi - 1) / dpi));
    picker.window =
        modalWindow(windowClass, tr(L"ModPicker"), std::min(520, availableWidth), height, mainWindow);
    if (!picker.window)
    {
        modPicker = nullptr;
        check(GetLastError(), L"Create mod selection window");
    }
    try
    {
        // Compose controls together; individual rows are buffered as well.
        SetWindowLongPtrW(picker.window, GWL_EXSTYLE,
                          GetWindowLongPtrW(picker.window, GWL_EXSTYLE) | WS_EX_COMPOSITED);
        SetWindowLongPtrW(picker.window, GWL_STYLE,
                          GetWindowLongPtrW(picker.window, GWL_STYLE) & ~(WS_MINIMIZEBOX | WS_MAXIMIZEBOX));
        SetWindowPos(picker.window, nullptr, 0, 0, 0, 0,
                     SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        picker.searchHost = input(picker.window, L"", 901, false, picker.search);
        SetWindowSubclass(picker.searchHost, modSearchProc, 10, 0);
        SendMessageW(picker.search, EM_SETCUEBANNER, TRUE, (LPARAM)tr(L"Search").c_str());
        SetWindowSubclass(
            picker.search,
            [](HWND w, UINT m, WPARAM a, LPARAM b, UINT_PTR, DWORD_PTR) -> LRESULT {
                auto result = DefSubclassProc(w, m, a, b);
                if (m == WM_SETFOCUS || m == WM_KILLFOCUS)
                    InvalidateRect(GetParent(w), nullptr, FALSE);
                return result;
            },
            10, 0);
        ShowWindow(picker.searchHost, picker.searchable ? SW_SHOW : SW_HIDE);
        picker.list = control(picker.window, L"LISTBOX", L"", 902,
                              WS_TABSTOP | WS_VSCROLL | LBS_NOTIFY | LBS_OWNERDRAWFIXED | LBS_HASSTRINGS |
                                  LBS_NOINTEGRALHEIGHT);
        SetWindowSubclass(picker.list, modListProc, 10, 0);
        SendMessageW(picker.list, LB_SETITEMHEIGHT, 0, px(modRowHeight));
        picker.close = button(picker.window, tr(L"Close"), 903);
        SendMessageW(picker.close, WM_SETFONT, (WPARAM)sectionFont, FALSE);
        filterMods();
        titleTheme(picker.window);
        EnableWindow(mainWindow, FALSE);
        ShowWindow(picker.window, SW_SHOW);
        SetFocus(picker.searchable ? picker.search : picker.list);
        MSG msg;
        while (IsWindow(picker.window) && GetMessageW(&msg, nullptr, 0, 0) > 0)
        {
            if (msg.message == WM_KEYDOWN && msg.wParam == VK_ESCAPE)
            {
                DestroyWindow(picker.window);
                continue;
            }
            if (msg.message == WM_KEYDOWN && msg.hwnd == picker.search &&
                (msg.wParam == VK_DOWN || msg.wParam == VK_UP))
            {
                SetFocus(picker.list);
                continue;
            }
            if (msg.message == WM_KEYDOWN && msg.wParam == VK_RETURN)
            {
                if (GetFocus() == picker.close)
                    DestroyWindow(picker.window);
                else
                    SendMessageW(picker.window, acceptModMessage, 0, 0);
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
    SetFocus(modCombo);
}
