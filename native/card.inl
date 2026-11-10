struct MetaRow
{
    S label, value, target;
    RECT hit{};
    int top = 0, height = 0;
};
struct ModCard
{
    HWND window = nullptr, body = nullptr;
    std::vector<HWND> buttons;
    std::vector<S> paths;
    std::vector<MetaRow> rows;
    S title, pluginPath;
    int scroll = 0, total = 0;
};
static ModCard *card = nullptr;
static void cardMetrics()
{
    RECT rect;
    GetClientRect(card->body, &rect);
    int width = MulDiv(rect.right, 96, dpi), height = MulDiv(rect.bottom, 96, dpi);
    int inner = std::max(100, width - 48), left = inner / 4, y = 18;
    for (auto &r : card->rows)
    {
        r.top = y + 5;
        r.height =
            std::max(measured(r.label, font, left - 18, true), measured(r.value, font, inner - left, true));
        y += r.height + 17;
    }
    card->total = y + 8;
    card->scroll = std::clamp(card->scroll, 0, std::max(0, card->total - height));
    SCROLLINFO info{
        sizeof(info), SIF_RANGE | SIF_PAGE | SIF_POS, 0, std::max(0, card->total - 1), (UINT)height,
        card->scroll};
    SetScrollInfo(card->body, SB_VERT, &info, TRUE);
    InvalidateRect(card->body, nullptr, FALSE);
}
static void cardLayout()
{
    RECT rect;
    GetClientRect(card->window, &rect);
    int width = MulDiv(rect.right, 96, dpi), height = MulDiv(rect.bottom, 96, dpi);
    int x = 24, y = 14, line = 0;
    for (HWND w : card->buttons)
    {
        int bw = std::min(width - 48, std::max(80, measured(windowText(w), sectionFont, width - 48) + 30));
        if (x > 24 && x + bw + 8 > width - 24)
        {
            x = 24;
            y += 46;
        }
        locate(w, x, y, bw, 38);
        x += bw + 8;
        line = y + 46;
    }
    int footer = line + 20;
    for (HWND w : card->buttons)
    {
        RECT r;
        GetWindowRect(w, &r);
        POINT p{r.left, r.top};
        ScreenToClient(card->window, &p);
        locate(w, MulDiv(p.x, 96, dpi), height - footer + MulDiv(p.y, 96, dpi),
               MulDiv(r.right - r.left, 96, dpi), 38);
    }
    locate(card->body, 0, 140, width, std::max(30, height - 140 - footer));
    cardMetrics();
    RedrawWindow(card->window, nullptr, nullptr, RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN);
}
static LRESULT CALLBACK cardBodyProc(HWND w, UINT m, WPARAM a, LPARAM b)
{
    if (!card)
        return DefWindowProcW(w, m, a, b);
    switch (m)
    {
    case WM_ERASEBKGND:
        return 1;
    case WM_MOUSEWHEEL:
        card->scroll -= GET_WHEEL_DELTA_WPARAM(a) / WHEEL_DELTA * 50;
        cardMetrics();
        return 0;
    case WM_VSCROLL: {
        SCROLLINFO info{sizeof(info), SIF_ALL};
        GetScrollInfo(w, SB_VERT, &info);
        switch (LOWORD(a))
        {
        case SB_LINEUP:
            card->scroll -= 24;
            break;
        case SB_LINEDOWN:
            card->scroll += 24;
            break;
        case SB_PAGEUP:
            card->scroll -= info.nPage;
            break;
        case SB_PAGEDOWN:
            card->scroll += info.nPage;
            break;
        case SB_THUMBPOSITION:
        case SB_THUMBTRACK:
            card->scroll = info.nTrackPos;
            break;
        }
        cardMetrics();
        return 0;
    }
    case WM_LBUTTONUP: {
        POINT p{GET_X_LPARAM(b), GET_Y_LPARAM(b)};
        for (auto &r : card->rows)
            if (!r.target.empty() && PtInRect(&r.hit, p))
                try
                {
                    open(w, r.target);
                }
                catch (const std::exception &e)
                {
                    error(w, e);
                }
        return 0;
    }
    case WM_SETCURSOR: {
        POINT p;
        GetCursorPos(&p);
        ScreenToClient(w, &p);
        for (auto &r : card->rows)
            if (!r.target.empty() && PtInRect(&r.hit, p))
            {
                SetCursor(LoadCursorW(nullptr, IDC_HAND));
                return TRUE;
            }
        break;
    }
    case WM_PAINT: {
        PAINTSTRUCT ps;
        HDC dc = BeginPaint(w, &ps);
        RECT c;
        GetClientRect(w, &c);
        if (c.right > 0 && c.bottom > 0)
        {
            HDC buffer = CreateCompatibleDC(dc);
            auto bitmap = CreateCompatibleBitmap(dc, c.right, c.bottom);
            auto old = SelectObject(buffer, bitmap);
            FillRect(buffer, &c, bgBrush);
            SelectObject(buffer, font);
            LOGFONTW linkStyle{};
            GetObjectW(font, sizeof(linkStyle), &linkStyle);
            linkStyle.lfUnderline = TRUE;
            HFONT linkFont = CreateFontIndirectW(&linkStyle);
            SetBkMode(buffer, TRANSPARENT);
            int width = MulDiv(c.right, 96, dpi), left = (width - 48) / 4;
            for (auto &r : card->rows)
            {
                int y = r.top - card->scroll;
                RECT label{px(24), px(y), px(24 + left - 18), px(y + r.height)};
                SetTextColor(buffer, RGB(128, 128, 128));
                SelectObject(buffer, font);
                DrawTextW(buffer, r.label.c_str(), -1, &label, DT_LEFT | DT_WORDBREAK | DT_NOPREFIX);
                RECT value{px(24 + left), px(y), c.right - px(24), px(y + r.height)};
                r.hit = value;
                SetTextColor(buffer, r.target.empty() ? ink() : dark ? RGB(135, 206, 250) : RGB(25, 90, 150));
                SelectObject(buffer, r.target.empty() ? font : linkFont);
                DrawTextW(buffer, r.value.c_str(), -1, &value, DT_LEFT | DT_WORDBREAK | DT_NOPREFIX);
            }
            BitBlt(dc, 0, 0, c.right, c.bottom, buffer, 0, 0, SRCCOPY);
            SelectObject(buffer, font);
            DeleteObject(linkFont);
            SelectObject(buffer, old);
            DeleteObject(bitmap);
            DeleteDC(buffer);
        }
        EndPaint(w, &ps);
        return 0;
    }
    }
    return DefWindowProcW(w, m, a, b);
}
static LRESULT CALLBACK cardProc(HWND w, UINT m, WPARAM a, LPARAM b)
{
    switch (m)
    {
    case WM_GETMINMAXINFO:
        ((MINMAXINFO *)b)->ptMinTrackSize = {px(600), px(460)};
        return 0;
    case WM_SIZE:
        if (card && card->body)
            cardLayout();
        return 0;
    case WM_DRAWITEM:
        return drawButton((DRAWITEMSTRUCT *)b);
    case WM_COMMAND:
        try
        {
            int id = LOWORD(a);
            if (id == 800)
                DestroyWindow(w);
            else if (id >= 801 && id < 801 + (int)card->paths.size())
            {
                const S &path=card->paths[id-801];
                if(path==card->pluginPath)
                {
                    wchar_t system[MAX_PATH];
                    if(!GetSystemDirectoryW(system,MAX_PATH)) check(GetLastError(),L"System directory");
                    S command=L"/C \""+path+L"\"";
                    auto result=(INT_PTR)ShellExecuteW(w,L"open",join(system,L"cmd.exe").c_str(),command.c_str(),nullptr,SW_SHOWNORMAL);
                    if(result<=32) fail(L"Cannot start mod action");
                }
                else open(w,path);
            }
        }
        catch (const std::exception &e)
        {
            error(w, e);
        }
        return 0;
    case WM_ERASEBKGND:
        return 1;
    case WM_PAINT: {
        PAINTSTRUCT ps;
        HDC dc = BeginPaint(w, &ps);
        RECT rect;
        GetClientRect(w, &rect);
        FillRect(dc, &rect, bgBrush);
        RECT hero{0, 0, rect.right, px(140)};
        drawArtwork(dc, hero);
        SelectObject(dc, cardHeadingFont);
        SetTextColor(dc, RGB(248, 238, 218));
        SetBkMode(dc, TRANSPARENT);
        RECT title{px(28), px(18), rect.right - px(28), px(122)};
        DrawTextW(dc, card->title.c_str(), -1, &title,
                  DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);
        EndPaint(w, &ps);
        return 0;
    }
    case WM_CLOSE:
        DestroyWindow(w);
        return 0;
    case WM_DESTROY:
        EnableWindow(mainWindow, TRUE);
        SetForegroundWindow(mainWindow);
        return 0;
    }
    return DefWindowProcW(w, m, a, b);
}
static void about()
{
    if (selected < 0)
        return;
    auto &m = modifications[selected];
    ModCard data;
    card = &data;
    data.title = m.title;
    auto add = [&](S label, S value, bool link = false) {
        if (value.empty())
            return;
        MetaRow row;
        row.label = tr(label);
        row.value = value;
        S target = value;
        if (link && target.find(L':') == S::npos && target.find(L'@') != S::npos)
            target = L"mailto:" + target;
        S normalized = lower(target);
        if (link && (normalized.find(L"https://") == 0 || normalized.find(L"http://") == 0 ||
                     normalized.find(L"mailto:") == 0))
            row.target = target;
        data.rows.push_back(row);
    };
    add(L"Version", m.version);
    add(L"Authors", m.author);
    add(L"Date", m.date);
    add(L"Website", m.site, true);
    add(L"Contact", m.email, true);
    add(L"SinglePlayer", tr(m.single ? L"Yes" : L"No"));
    add(L"Multiplayer", tr(m.multi ? L"Yes" : L"No"));
    data.window = modalWindow(L"ReStarter.Native.Card", tr(L"ModInfo"), 700, 520, mainWindow);
    data.body = CreateWindowExW(0, L"ReStarter.Native.CardBody", L"", WS_CHILD | WS_VISIBLE | WS_VSCROLL, 0,
                                0, 0, 0, data.window, nullptr, instance, nullptr);
    S readme, changelog;
    WIN32_FIND_DATAW f{};
    HANDLE find = FindFirstFileW(join(m.path, L"*").c_str(), &f);
    if (find != INVALID_HANDLE_VALUE)
    {
        do
        {
            if (f.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
                continue;
            S name = lower(f.cFileName);
            if (name.find(L"read") != S::npos && name.find(L"me") != S::npos)
                readme = join(m.path, f.cFileName);
            if (name.find(L"change") != S::npos && name.find(L"log") != S::npos)
                changelog = join(m.path, f.cFileName);
        } while (FindNextFileW(find, &f));
        FindClose(find);
    }
    auto action = [&](S label, S path) {
        if (path.empty() || !exists(path))
            return;
        auto w = button(data.window, label, 801 + (int)data.paths.size());
        SendMessageW(w, WM_SETFONT, (WPARAM)sectionFont, FALSE);
        data.buttons.push_back(w);
        data.paths.push_back(path);
    };
    action(tr(L"btReadme"), readme);
    action(tr(L"btChangelog"), changelog);
    action(tr(L"btModFolder"), m.path);
    if (!m.plugin.empty() && exists(join(m.path, m.plugin)))
    {
        data.pluginPath=join(m.path,m.plugin);
        action(m.pluginText.empty() ? L"Plugin" : m.pluginText, data.pluginPath);
    }
    auto close = button(data.window, tr(L"Close"), 800);
    SendMessageW(close, WM_SETFONT, (WPARAM)sectionFont, FALSE);
    data.buttons.push_back(close);
    cardLayout();
    titleTheme(data.window);
    EnableWindow(mainWindow, FALSE);
    ShowWindow(data.window, SW_SHOW);
    MSG msg;
    while (IsWindow(data.window) && GetMessageW(&msg, nullptr, 0, 0) > 0)
    {
        if (msg.message == WM_KEYDOWN && msg.wParam == VK_ESCAPE)
        {
            DestroyWindow(data.window);
            continue;
        }
        if (!IsDialogMessageW(data.window, &msg))
        {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
    }
    card = nullptr;
}
