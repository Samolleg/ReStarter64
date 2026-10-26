// The layout contract mirrors Options.Appearance.cs and SelectionControls.cs.
// Scroll moves one clipped canvas; child windows are never individually moved
// during scrolling, and old pixels are never copied into their new positions.
struct Row
{
    S label, key, section, initial, value, tip;
    int page = 0, category = -1, type = 0, choice = -1, knownChoices = 0;
    const Option *option = nullptr;
    std::vector<S> choices;
    double minimum = 0;
    HWND labelWindow = nullptr, edit = nullptr, host = nullptr, drop = nullptr;
    std::vector<HWND> buttons;
    Value baseline;
    bool path = false, locked = false, toggle = false;
};
struct Block
{
    int page = 0, kind = 0, row = -1, group = -1;
    S text;
    HWND window = nullptr, label = nullptr;
};
struct Settings
{
    HWND window = nullptr, viewport = nullptr, canvas = nullptr, ok = nullptr, cancel = nullptr,
         reset = nullptr, tooltips = nullptr;
    std::vector<HWND> nav, headers;
    std::vector<Row> rows;
    std::vector<Block> blocks;
    std::vector<S> groupTitles;
    std::vector<bool> expanded;
    int page = 0, scroll = 0, total = 0, layoutWidth = 0, layoutDepth = 0;
    bool updating = false, rendererAvailable = false, autoPreset = true, confirmPreset = false;
    std::unique_ptr<Ini> renderer;
    Values baseline;
    Ini launcher{join(root, L"restarter.config")};
    std::vector<S> displayModes;
};
static Settings *currentSettings = nullptr;
static const wchar_t *categories[] = {
    L"OwnedCategoryDisplay", L"OwnedCategoryLighting",   L"OwnedCategoryTerrain",
    L"OwnedCategoryWater",   L"OwnedCategoryVegetation", L"OwnedCategoryAtmosphere",
    L"OwnedCategoryEffects", L"OwnedCategoryWorld",      L"OwnedCategoryInterface"};
static S choiceLabel(const S &s)
{
    if (s == L"Off" || s == L"On")
        return tr(s);
    if (language != 2)
        return s;
    static const Map<S> labels = {{L"Borderless fullscreen", L"Без рамки на весь экран"},
                                  {L"Windowed", L"В окне"},
                                  {L"Original", L"Оригинальное"},
                                  {L"Faithful", L"Как в оригинале"},
                                  {L"Atmospheric", L"Атмосферный"},
                                  {L"Volumetric", L"Объёмный"},
                                  {L"Fast", L"Быстрое"},
                                  {L"Full", L"Полное"},
                                  {L"Fine", L"Точное"},
                                  {L"Simple", L"Простые"},
                                  {L"Both", L"Оба"},
                                  {L"Dense", L"Плотные"},
                                  {L"Normal", L"Обычное"},
                                  {L"Low", L"Низкое"},
                                  {L"Medium", L"Среднее"},
                                  {L"High", L"Высокое"}};
    auto i = labels.find(s);
    return i == labels.end() ? s : i->second;
}
static S raw(const Option &o, double n)
{
    return o.type == 0 ? (n == 0 ? L"false" : L"true") : number(n);
}
static double parsed(const Option &o, S s)
{
    s = lower(trim(s));
    double n = 0;
    if (o.type == 0)
    {
        if (s == L"true" || s == L"on" || s == L"1")
            n = 1;
        else if (s != L"false" && s != L"off" && s != L"0")
            fail(L"Invalid boolean");
    }
    else
        n = numeric(s);
    if (n < o.minimum || n > o.maximum || (o.type == 1 && n != floor(n)))
        fail(L"Out of range: " + S(o.key));
    return n;
}
static int measured(const S &text, HFONT f, int width, bool wrap = false)
{
    HDC dc = GetDC(nullptr);
    auto old = SelectObject(dc, f);
    RECT r{0, 0, px(std::max(1, width)), 0};
    DrawTextW(dc, text.c_str(), -1, &r, DT_CALCRECT | DT_NOPREFIX | (wrap ? DT_WORDBREAK : DT_SINGLELINE));
    SelectObject(dc, old);
    ReleaseDC(nullptr, dc);
    return MulDiv(wrap ? r.bottom : r.right, 96, dpi);
}
static HWND input(HWND parent, const S &value, int id, bool locked, HWND &editor)
{
    HWND host = CreateWindowExW(WS_EX_CONTROLPARENT, L"ReStarter.Native.Input", L"",
                                WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN, 0, 0, 0, 0, parent, nullptr,
                                instance, nullptr);
    editor = CreateWindowExW(0, L"EDIT", value.c_str(),
                             WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_AUTOHSCROLL | (locked ? ES_READONLY : 0),
                             0, 0, 0, 0, host, (HMENU)(INT_PTR)id, instance, nullptr);
    SendMessageW(editor, WM_SETFONT, (WPARAM)font, FALSE);
    SendMessageW(editor, EM_SETLIMITTEXT, 32700, 0);
    SetWindowSubclass(
        editor,
        [](HWND w, UINT m, WPARAM a, LPARAM b, UINT_PTR, DWORD_PTR) -> LRESULT {
            if (m == WM_SETFOCUS && ensureVisible)
                ensureVisible(w);
            if (m == WM_MOUSEWHEEL && currentSettings)
                return SendMessageW(currentSettings->viewport, m, a, b);
            return DefSubclassProc(w, m, a, b);
        },
        3, 0);
    return host;
}
static LRESULT paintSurface(HWND window, HBRUSH brush)
{
    PAINTSTRUCT paint;
    HDC dc = BeginPaint(window, &paint);
    FillRect(dc, &paint.rcPaint, brush);
    EndPaint(window, &paint);
    return 0;
}
static LRESULT CALLBACK inputProc(HWND w, UINT m, WPARAM a, LPARAM b)
{
    if (m == WM_COMMAND)
        return SendMessageW(GetParent(w), m, a, b);
    if (m == WM_CTLCOLOREDIT || m == WM_CTLCOLORSTATIC)
    {
        SetTextColor((HDC)a, ink());
        SetBkColor((HDC)a, surface());
        return (LRESULT)surfaceBrush;
    }
    if (m == WM_ERASEBKGND)
        return 1;
    if (m == WM_PAINT)
        return paintSurface(w, surfaceBrush);
    if (m == WM_MOUSEWHEEL && currentSettings)
        return SendMessageW(currentSettings->viewport, m, a, b);
    return DefWindowProcW(w, m, a, b);
}
static void select(Row &r, int choice)
{
    if (choice < 0 || choice >= (int)r.choices.size())
        choice = -1;
    r.choice = choice;
    if (r.toggle)
    {
        auto w = r.buttons[0];
        S label = choice < 0 ? tr(L"Unspecified") : tr(choice ? L"On" : L"Off");
        if (choice == 1)
            SetPropW(w, L"selected", (HANDLE)1);
        else
            RemovePropW(w, L"selected");
        if (choice < 0)
            SetPropW(w, L"unknown", (HANDLE)1);
        else
            RemovePropW(w, L"unknown");
        // WM_SETTEXT may paint synchronously: publish the complete state first.
        SetWindowTextW(w, label.c_str());
        InvalidateRect(w, nullptr, FALSE);
    }
    else
        for (int i = 0; i < (int)r.buttons.size(); i++)
        {
            if (i == choice)
                SetPropW(r.buttons[i], L"selected", (HANDLE)1);
            else
                RemovePropW(r.buttons[i], L"selected");
            InvalidateRect(r.buttons[i], nullptr, FALSE);
        }
    if (choice >= 0)
    {
        if (r.key == L"$preset")
            return;
        if (choice >= r.knownChoices)
        {
            r.value = r.initial;
            return;
        }
        r.value = r.option ? raw(*r.option, r.minimum + choice) : std::to_wstring(choice);
    }
}
static void addProfileSetting(Settings &s, int page, S label, S section, S key, int type, bool toggle = false)
{
    Row r;
    r.page = page;
    r.label = label;
    r.section = section;
    r.key = key;
    r.type = type;
    r.toggle = toggle;
    r.baseline = readSetting(section, key);
    s.baseline[r.baseline.id()] = r.baseline;
    r.initial = r.value = valueText(r.baseline);
    if (toggle)
        r.choices = {tr(L"Off"), tr(L"On")};
    s.rows.push_back(r);
}
static void addBlock(Settings &s, int page, int kind, S caption = L"", int row = -1, int group = -1)
{
    Block b;
    b.page = page;
    b.kind = kind;
    b.text = caption;
    b.row = row;
    b.group = group;
    if (kind == 0 || kind == 1 || kind == 2 || kind == 5)
    {
        b.window = control(s.canvas, L"STATIC", caption, 0, SS_LEFT | SS_NOPREFIX);
        SendMessageW(b.window, WM_SETFONT,
                     (WPARAM)(kind == 0   ? settingsHeadingFont
                              : kind == 1 ? settingsBoldFont
                                          : font),
                     FALSE);
        if (kind == 5)
            b.label = control(s.canvas, L"STATIC", tr(L"lbAdapter"), 0, SS_LEFT | SS_NOPREFIX);
    }
    else if (kind == 4)
    {
        b.window = button(s.canvas, caption, 500 + group);
        SendMessageW(b.window, WM_SETFONT, (WPARAM)sectionFont, FALSE);
        SetPropW(b.window, L"left", (HANDLE)1);
        s.headers.push_back(b.window);
    }
    else if (kind == 6)
    {
        b.window = button(s.canvas, tr(L"btApplyPaths"), 204);
        SendMessageW(b.window, WM_SETFONT, (WPARAM)sectionFont, FALSE);
    }
    s.blocks.push_back(b);
}
static void cameraRows(Settings &s)
{
    auto add = [&](S label, S key, int type = 0, bool toggle = false) {
        addProfileSetting(s, 2, label, L"EvilIslands\\camera settings", key, type, toggle);
    };
    add(tr(L"Flip X Axis"), L"CameraFlipXAxis", 1, true);
    add(tr(L"Flip Y Axis"), L"CameraFlipYAxis", 1, true);
    add(tr(L"Clip Distance: Far"), L"FarClipDistance");
    add(tr(L"Clip Distance: Near"), L"NearClipDistance");
    add(tr(L"Terrain Sensetive"), L"CameraTerrainSensetiveArea");
    add(tr(L"Border Scroll"), L"CameraBorderScrollArea", 1);
    add(tr(L"Frame Selection Sensetive"), L"CameraFrameSelectionSensetiveArea", 1);
    for (auto spec : std::vector<std::tuple<S, S, S, bool>>{
             {L"Camera{0}Height", L"Camera", L"Height", true},
             {L"Camera{0}DistanceToCarrier", L"Camera", L"Distance To Carrier", true},
             {L"Camera{0}LimitDistanceToCarrier", L"Camera", L"Distance To Carrier Limit", false},
             {L"CameraDefault{0}Pitch", L"Camera", L"Pitch", false},
             {L"CameraBriefing{0}DistanceToCarrier", L"Briefing Camera", L"Distance To Carrier", false},
             {L"CameraBriefing{0}LimitDistanceToCarrier", L"Briefing Camera", L"Distance To Carrier Limit",
              false},
             {L"CameraBriefing{0}Pitch", L"Briefing Camera", L"Pitch", false}})
    {
        int group = (int)s.groupTitles.size();
        s.groupTitles.push_back(tr(std::get<1>(spec)) + L" · " + tr(std::get<2>(spec)));
        std::vector<S> limits = {L"Min", L"Max"};
        if (std::get<3>(spec))
            limits.push_back(L"Default");
        for (auto limit : limits)
        {
            S key = std::get<0>(spec);
            key.replace(key.find(L"{0}"), 3, limit);
            add(tr(limit), key);
            s.rows.back().category = group;
        }
    }
    for (auto axis : {L"Pitch", L"Rotate", L"Scroll", L"Zoom"})
    {
        int group = (int)s.groupTitles.size();
        s.groupTitles.push_back(tr(L"General") + L" · " + tr(axis));
        for (auto kind : {L"Speed", L"PowerMouse", L"PowerKeyboard"})
        {
            add(tr(kind), L"CameraDefault" + S(axis) + kind);
            s.rows.back().category = group;
        }
    }
    add(tr(L"Deploy X"), L"CameraDefaultXDeploy");
    add(tr(L"Deploy Y"), L"CameraDefaultYDeploy");
    for (int i = 0; i < 4; i++)
    {
        int group = (int)s.groupTitles.size();
        s.groupTitles.push_back(tr(L"Shortcuts") + L" · " + tr(L"Shortcut") + L" " + std::to_wstring(i));
        for (auto key : {L"Distance", L"AxisX", L"AxisY", L"AxisZ", L"Angle", L"Pitch"})
        {
            add(tr(key), L"CameraShortcut " + std::to_wstring(i) + L" " + key);
            s.rows.back().category = group;
        }
    }
}
static void rowFields(Settings &s)
{
    s.tooltips =
        CreateWindowExW(WS_EX_TOPMOST, TOOLTIPS_CLASSW, nullptr, WS_POPUP | TTS_ALWAYSTIP, CW_USEDEFAULT,
                        CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, s.window, nullptr, instance, nullptr);
    SendMessageW(s.tooltips, TTM_SETMAXTIPWIDTH, 0, px(420));
    for (int index = 0; index < (int)s.rows.size(); index++)
    {
        auto &r = s.rows[index];
        r.knownChoices = (int)r.choices.size();
        r.labelWindow = control(s.canvas, L"STATIC", r.label, 0, SS_LEFT | SS_NOPREFIX);
        if (r.key == L"videoresolution")
        {
            r.drop =
                control(s.canvas, L"COMBOBOX", L"", 1000 + index,
                        WS_TABSTOP | CBS_DROPDOWNLIST | CBS_OWNERDRAWFIXED | CBS_HASSTRINGS | WS_VSCROLL);
            SendMessageW(r.drop, WM_SETFONT, (WPARAM)sectionFont, FALSE);
            SendMessageW(r.drop, CB_SETITEMHEIGHT, (WPARAM)-1, px(20));
            SendMessageW(r.drop, CB_SETITEMHEIGHT, 0, px(24));
            int selectedIndex = -1;
            for (int i = 0; i < (int)r.choices.size(); i++)
            {
                SendMessageW(r.drop, CB_ADDSTRING, 0, (LPARAM)r.choices[i].c_str());
                if (r.choices[i] == r.value)
                    selectedIndex = i;
            }
            if (selectedIndex < 0)
            {
                selectedIndex = (int)r.choices.size();
                r.choices.push_back(r.initial);
                SendMessageW(r.drop, CB_ADDSTRING, 0, (LPARAM)r.initial.c_str());
            }
            SendMessageW(r.drop, CB_SETCURSEL, selectedIndex, 0);
        }
        else if (r.choices.empty())
            r.host = input(s.canvas, r.value, 1000 + index, r.locked, r.edit);
        else
        {
            int initial = -1;
            if (r.key != L"$preset")
                try
                {
                    double n = r.option ? parsed(*r.option, r.value) : numeric(r.value);
                    if (n == floor(n))
                        initial = (int)(n - r.minimum);
                }
                catch (...)
                {
                }
            if (r.toggle)
            {
                auto w = button(s.canvas, L"", 3000 + index * 8);
                SetPropW(w, L"toggle", (HANDLE)1);
                SendMessageW(w, WM_SETFONT, (WPARAM)sectionFont, FALSE);
                r.buttons.push_back(w);
            }
            else
            {
                if (r.option && (initial < 0 || initial >= r.knownChoices))
                {
                    r.choices.push_back(tr(L"OwnedKeepValue") + L": " + r.initial);
                    initial = (int)r.choices.size() - 1;
                }
                for (int j = 0; j < (int)r.choices.size(); j++)
                {
                    auto w = button(s.canvas, choiceLabel(r.choices[j]), 3000 + index * 8 + j);
                    SendMessageW(w, WM_SETFONT, (WPARAM)sectionFont, FALSE);
                    r.buttons.push_back(w);
                }
            }
            select(r, initial);
        }
        if (r.drop)
            SetWindowSubclass(
                r.drop,
                [](HWND w, UINT m, WPARAM a, LPARAM b, UINT_PTR, DWORD_PTR) -> LRESULT {
                    if (m == WM_SETFOCUS && ensureVisible)
                        ensureVisible(w);
                    if (m == WM_ERASEBKGND)
                        return 1;
                    if (m == WM_PAINT)
                    {
                        PAINTSTRUCT ps;
                        HDC dc = BeginPaint(w, &ps);
                        RECT rect;
                        GetClientRect(w, &rect);
                        FillRect(dc, &rect, surfaceBrush);
                        HBRUSH border = CreateSolidBrush(dark ? RGB(77, 80, 86) : RGB(200, 203, 208));
                        FrameRect(dc, &rect, border);
                        DeleteObject(border);
                        RECT arrow{rect.right - px(18), 1, rect.right - 1, rect.bottom - 1};
                        HBRUSH fill = CreateSolidBrush(RGB(225, 226, 229));
                        FillRect(dc, &arrow, fill);
                        DeleteObject(fill);
                        POINT points[] = {{arrow.left + px(6), rect.bottom / 2 - px(1)},
                                          {arrow.right - px(5), rect.bottom / 2 - px(1)},
                                          {(arrow.left + arrow.right) / 2, rect.bottom / 2 + px(3)}};
                        auto pen = SelectObject(dc, GetStockObject(BLACK_PEN)),
                             brush = SelectObject(dc, GetStockObject(BLACK_BRUSH));
                        Polygon(dc, points, 3);
                        SelectObject(dc, pen);
                        SelectObject(dc, brush);
                        SetTextColor(dc, ink());
                        SetBkMode(dc, TRANSPARENT);
                        SelectObject(dc, sectionFont);
                        RECT textRect = rect;
                        textRect.left += px(6);
                        textRect.right = arrow.left - px(4);
                        auto text = windowText(w);
                        DrawTextW(dc, text.c_str(), -1, &textRect,
                                  DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX | DT_END_ELLIPSIS);
                        EndPaint(w, &ps);
                        return 0;
                    }
                    return DefSubclassProc(w, m, a, b);
                },
                4, 0);
        if (r.path && !r.locked)
        {
            auto w = button(s.canvas, L"…", 6000 + index);
            SendMessageW(w, WM_SETFONT, (WPARAM)sectionFont, FALSE);
            r.buttons.push_back(w);
        }
        if (!r.choices.empty() && !r.toggle && !r.drop)
            for (auto w : r.buttons)
                SetWindowSubclass(
                    w,
                    [](HWND w, UINT m, WPARAM a, LPARAM b, UINT_PTR, DWORD_PTR) -> LRESULT {
                        if (m == WM_GETDLGCODE)
                            return DefSubclassProc(w, m, a, b) | DLGC_WANTARROWS;
                        if (m == WM_KEYDOWN && currentSettings &&
                            (a == VK_LEFT || a == VK_RIGHT || a == VK_HOME || a == VK_END))
                        {
                            int id = GetDlgCtrlID(w), row = (id - 3000) / 8, choice = (id - 3000) % 8;
                            auto &buttons = currentSettings->rows[row].buttons;
                            int count = (int)buttons.size();
                            int next = a == VK_HOME  ? 0
                                       : a == VK_END ? count - 1
                                                     : (choice + (a == VK_LEFT ? -1 : 1) + count) % count;
                            HWND target = buttons[next];
                            SetFocus(target);
                            SendMessageW(GetParent(target), WM_COMMAND,
                                         MAKEWPARAM(GetDlgCtrlID(target), BN_CLICKED), (LPARAM)target);
                            return 0;
                        }
                        return DefSubclassProc(w, m, a, b);
                    },
                    5, 0);
        if (r.option)
        {
            r.tip = S(r.option->key) + L" · " + number(r.option->minimum) + L"–" + number(r.option->maximum);
            if (r.key == L"window_width" || r.key == L"window_height")
                r.tip += L" · " + tr(L"OwnedWindowAuto");
            if (r.key == L"frame_limit")
                r.tip += L" · " + tr(L"OwnedFrameUnlimited");
            std::vector<HWND> targets = r.buttons;
            if (r.edit)
                targets.push_back(r.edit);
            for (HWND w : targets)
            {
                TOOLINFOW info{sizeof(info)};
                info.uFlags = TTF_IDISHWND | TTF_SUBCLASS;
                info.hwnd = s.window;
                info.uId = (UINT_PTR)w;
                info.lpszText = &r.tip[0];
                SendMessageW(s.tooltips, TTM_ADDTOOLW, 0, (LPARAM)&info);
            }
        }
    }
}
static void syncPreset(Settings &s, bool manualChoice = false)
{
    if (manualChoice)
    {
        s.autoPreset = false;
        s.confirmPreset = true;
    }
    int detected = 4;
    for (int p = 0; !s.autoPreset && p < 4; p++)
    {
        bool match = true;
        for (auto &r : s.rows)
            if (r.option && !r.option->presets.empty())
            {
                try
                {
                    S value = r.edit ? windowText(r.edit) : r.value;
                    if (std::abs(parsed(*r.option, value) - r.option->presets[p]) > .0001)
                        match = false;
                }
                catch (...)
                {
                    match = false;
                }
            }
        if (match)
        {
            detected = p;
            break;
        }
    }
    for (auto &r : s.rows)
        if (r.key == L"$preset")
            select(r, s.autoPreset ? 0 : detected + 1);
}
static void buildSettings(Settings &s)
{
    refreshProfile();
    s.renderer = std::make_unique<Ini>(join(root, L"Engine\\rddraw.ini"));
    s.renderer->validate();
    s.rendererAvailable = exists(join(root, L"Engine\\EIRebuild.exe"));
    for (auto key : categories)
        s.groupTitles.push_back(tr(key));
    addProfileSetting(s, 0, tr(L"lbResolution"), L"EvilIslands\\General Settings", L"videoresolution", 2);
    s.displayModes = resolutions();
    s.rows.back().choices = s.displayModes;
    addProfileSetting(s, 0, tr(L"lbAudio"), L"EvilIslands\\General Settings", L"audio", 1);
    s.rows.back().choices = {tr(L"Off"), tr(L"Normal"), tr(L"Safe")};
    Row preset;
    preset.label = tr(L"OwnedRendererPreset");
    preset.key = L"$preset";
    preset.locked = !s.rendererAvailable;
    S firstRun = lower(s.renderer->get(L"setup", L"first_run_done"));
    s.autoPreset = !(firstRun == L"true" || firstRun == L"on" || firstRun == L"1");
    preset.choices.push_back(tr(L"OwnedPresetAuto"));
    for (int i = 0; i < 5; i++)
        preset.choices.push_back(tr(L"OwnedPreset" + std::to_wstring(i)));
    s.rows.push_back(preset);
    for (auto &o : schema())
    {
        Row r;
        r.label = language == 2 ? o.russian : o.english;
        r.key = o.key;
        r.option = &o;
        r.category = o.category;
        r.type = o.type;
        r.initial = r.value = s.renderer->get(L"graphics", o.key, raw(o, o.initial));
        r.minimum = o.minimum;
        r.locked = !s.rendererAvailable;
        r.choices = o.type == 0 ? std::vector<S>{tr(L"Off"), tr(L"On")} : o.choices;
        s.rows.push_back(r);
    }
    for (auto spec : std::vector<std::pair<S, S>>{{L"cbMPConsoleSupport", L"MP console"},
                                                  {L"cbMPDisableMQ", L"MQ client"},
                                                  {L"cbMPSecondSpeed", L"MP Speed-control"}})
        addProfileSetting(s, 1, tr(spec.first), L"Settings", spec.second, 1, true);
    cameraRows(s);
    s.expanded.resize(s.groupTitles.size(), false);
    s.expanded[0] = true;
    for (auto spec : std::vector<std::pair<S, S>>{{L"lbPathCD", L"CD-ROM Path"},
                                                  {L"lbPathRes", L"Resources Path"},
                                                  {L"lbPathGameWorkdir", L"Work Path"}})
    {
        addProfileSetting(s, 3, tr(spec.first), L"EvilIslands\\Path Settings", spec.second, 2);
        auto &r = s.rows.back();
        r.path = true;
        r.initial = r.value;
    }
    Row exe;
    exe.page = 3;
    exe.key = L"$exe";
    exe.label = tr(L"lbEXEName");
    exe.initial = exe.value = L"Engine\\EIRebuild.exe";
    exe.locked = true;
    s.rows.push_back(exe);
    s.updating = true;
    rowFields(s);
    syncPreset(s);
    s.updating = false;
    const wchar_t *tabs[] = {L"tabGraphics", L"tabMultiplayer", L"tabCamera", L"tabPaths"};
    for (int page = 0; page < 4; page++)
    {
        addBlock(s, page, 0, tr(tabs[page]));
        if (page == 0)
        {
            addBlock(s, 0, 1, tr(L"GameSettingsHeading"));
            addBlock(s, 0, 2, tr(L"GameEngineHint"));
            addBlock(s, 0, 5, tr(L"GameManagedAdapter"));
            addBlock(s, 0, 2, tr(L"GameSettingsDefaults"));
            addBlock(s, 0, 3, L"", 0);
            addBlock(s, 0, 3, L"", 1);
            addBlock(s, 0, 1, tr(L"OwnedRendererHeading"));
            addBlock(s, 0, 2, tr(L"OwnedRendererF10Hint"));
            if (s.rendererAvailable)
            {
                addBlock(s, 0, 3, L"", 2);
                addBlock(s, 0, 2, tr(L"OwnedPresetNote"));
                addBlock(s, 0, 2, tr(L"OwnedFirstRunNote"));
            }
            else
                addBlock(s, 0, 2, tr(L"OwnedRendererUnavailable"));
        }
        int previous = -1;
        for (int i = 0; i < (int)s.rows.size(); i++)
        {
            auto &r = s.rows[i];
            if (r.page != page || (page == 0 && (!r.option || !s.rendererAvailable)))
                continue;
            if (r.category >= 0 && previous != r.category)
                addBlock(s, page, 4, s.groupTitles[r.category], -1, r.category);
            addBlock(s, page, 3, L"", i, r.category);
            previous = r.category;
        }
        if (page == 3)
            addBlock(s, 3, 6);
    }
}
static void locate(HWND w, int x, int y, int width, int height)
{
    SetWindowPos(w, nullptr, px(x), px(y), px(std::max(1, width)), px(std::max(1, height)),
                 SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOREDRAW | SWP_NOCOPYBITS);
}
static void layoutSettings(Settings &s);
static void scrollSettings(Settings &s)
{
    RECT r;
    GetClientRect(s.viewport, &r);
    int height = MulDiv(r.bottom, 96, dpi);
    s.scroll = std::clamp(s.scroll, 0, std::max(0, s.total - height));
    SetWindowPos(s.canvas, nullptr, 0, -px(s.scroll), r.right, px(std::max(height, s.total)),
                 SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOREDRAW | SWP_NOCOPYBITS);
    SCROLLINFO info{sizeof(info), SIF_RANGE | SIF_PAGE | SIF_POS, 0, std::max(0, s.total - 1), (UINT)height,
                    s.scroll};
    SetScrollInfo(s.viewport, SB_VERT, &info, TRUE);
    RECT actual;
    GetClientRect(s.viewport, &actual);
    if (actual.right != s.layoutWidth && s.layoutDepth < 2)
    {
        layoutSettings(s);
        return;
    }
    // Queue one composed repaint, rather than exposing each child's erase/paint
    // during every scrollbar notification. Further scroll input can be coalesced.
    RedrawWindow(s.viewport, nullptr, nullptr, RDW_INVALIDATE | RDW_ALLCHILDREN);
}
static void layoutSettings(Settings &s)
{
    s.layoutDepth++;
    RECT c;
    GetClientRect(s.window, &c);
    int width = MulDiv(c.right, 96, dpi), height = MulDiv(c.bottom, 96, dpi);
    SendMessageW(s.viewport, WM_SETREDRAW, FALSE, 0);
    for (int i = 0; i < 4; i++)
    {
        locate(s.nav[i], 19, 19 + i * 50, 182, 42);
        if (i == s.page)
            SetPropW(s.nav[i], L"selected", (HANDLE)1);
        else
            RemovePropW(s.nav[i], L"selected");
    }
    locate(s.viewport, 213, 19, width - 232, height - 105);
    int cancelWidth = std::max(80, measured(tr(L"btCancel"), sectionFont, 100) + 30);
    int okWidth = 80, resetWidth = std::max(80, measured(tr(L"ResetDefaults"), sectionFont, 200) + 30);
    locate(s.cancel, width - 24 - cancelWidth, height - 70, cancelWidth, 42);
    locate(s.ok, width - 32 - cancelWidth - okWidth, height - 70, okWidth, 42);
    locate(s.reset, width - 40 - cancelWidth - okWidth - resetWidth, height - 70, resetWidth, 42);
    RECT view;
    GetClientRect(s.viewport, &view);
    s.layoutWidth = view.right;
    int pageWidth = MulDiv(view.right, 96, dpi), inner = std::max(100, pageWidth - 28), y = 8;
    for (auto &r : s.rows)
    {
        for (auto w : {r.labelWindow, r.host, r.drop})
            if (w)
                ShowWindow(w, SW_HIDE);
        for (auto w : r.buttons)
            ShowWindow(w, SW_HIDE);
    }
    for (auto &b : s.blocks)
    {
        if (b.window)
            ShowWindow(b.window, SW_HIDE);
        if (b.label)
            ShowWindow(b.label, SW_HIDE);
    }
    for (auto &b : s.blocks)
    {
        if (b.page != s.page)
            continue;
        if (b.kind == 0 || b.kind == 1 || b.kind == 2 || b.kind == 5)
        {
            int h = 0;
            if (b.kind == 0)
            {
                h = measured(b.text, settingsHeadingFont, inner, true);
                locate(b.window, 14, y, inner, h);
                y += h + 14;
            }
            else if (b.kind == 1)
            {
                y += 18;
                h = measured(b.text, settingsBoldFont, inner, true);
                locate(b.window, 14, y, inner, h);
                y += h + 8;
            }
            else if (b.kind == 5)
            {
                int label = inner * 36 / 100;
                S caption = tr(L"lbAdapter");
                // Static adapter row shares the exact same two-column grid.
                ShowWindow(b.label, SW_SHOW);
                int lh = measured(caption, font, label - 16, true),
                    vh = measured(b.text, font, inner - label, true);
                h = std::max(lh + 8, vh);
                locate(b.label, 14, y + 4, label - 16, lh);
                locate(b.window, 14 + label, y, inner - label, vh);
                y += h + 6;
            }
            else
            {
                h = measured(b.text, font, inner, true);
                locate(b.window, 14, y, inner, h);
                y += h + 12;
            }
            ShowWindow(b.window, SW_SHOW);
            continue;
        }
        if (b.kind == 4)
        {
            SetWindowTextW(b.window, (b.text + (s.expanded[b.group] ? L"   −" : L"   +")).c_str());
            ShowWindow(b.window, SW_SHOW);
            locate(b.window, 14, y, inner, 38);
            y += 46;
            continue;
        }
        if (b.kind == 6)
        {
            ShowWindow(b.window, SW_SHOW);
            locate(b.window, 14, y, std::max(80, measured(tr(L"btApplyPaths"), sectionFont, 300) + 30), 38);
            y += 54;
            continue;
        }
        if (b.group >= 0 && !s.expanded[b.group])
            continue;
        auto &r = s.rows[b.row];
        int indent = b.group >= 0 ? 12 : 0, available = inner - indent, label = available * 36 / 100;
        int x = 14 + indent + label, area = available - label,
            lh = measured(r.label, font, std::max(1, label - 16), true), rh = 0;
        locate(r.labelWindow, 14 + indent, y + 4, label - 16, lh);
        ShowWindow(r.labelWindow, SW_SHOW);
        if (r.drop)
        {
            locate(r.drop, x, y, area, 240);
            ShowWindow(r.drop, SW_SHOW);
            rh = 26;
        }
        else if (r.host)
        {
            int entryHeight = measured(L"Ag", font, 200, true),
                entryWidth = area - (r.path && !r.locked ? 48 : 0);
            locate(r.host, x, y, entryWidth, entryHeight + 20);
            locate(r.edit, 12, 10, entryWidth - 24, entryHeight);
            ShowWindow(r.host, SW_SHOW);
            rh = entryHeight + 20;
            if (r.path && !r.locked)
            {
                locate(r.buttons[0], x + entryWidth + 8, y, 40, 40);
                ShowWindow(r.buttons[0], SW_SHOW);
                rh = std::max(rh, 40);
            }
        }
        else if (r.toggle)
        {
            int bw = std::max({112, measured(tr(L"Unspecified"), sectionFont, 200) + 60,
                               measured(tr(L"On"), sectionFont, 100) + 60,
                               measured(tr(L"Off"), sectionFont, 100) + 60});
            locate(r.buttons[0], x, y, bw, 38);
            ShowWindow(r.buttons[0], SW_SHOW);
            rh = 38;
        }
        else
        {
            int bx = 0, by = 0;
            for (auto w : r.buttons)
            {
                int bw = std::min(area, std::max(52, measured(windowText(w), sectionFont, area) + 24));
                if (bx && bx + bw + 5 > area)
                {
                    bx = 0;
                    by += 37;
                }
                locate(w, x + bx, y + by, bw, 32);
                ShowWindow(w, SW_SHOW);
                EnableWindow(w, !r.locked);
                bx += bw + 5;
            }
            rh = by + 37;
        }
        y += std::max(rh, lh + 8) + 6;
    }
    s.total = y + 12;
    SendMessageW(s.viewport, WM_SETREDRAW, TRUE, 0);
    scrollSettings(s);
    s.layoutDepth--;
    RedrawWindow(s.window, nullptr, nullptr, RDW_INVALIDATE | RDW_ALLCHILDREN);
}
static void revealSetting(HWND window)
{
    auto *s = currentSettings;
    if (!s || !IsChild(s->canvas, window))
        return;
    RECT child, view;
    GetWindowRect(window, &child);
    GetWindowRect(s->viewport, &view);
    int delta = child.top < view.top         ? child.top - view.top
                : child.bottom > view.bottom ? child.bottom - view.bottom
                                             : 0;
    if (delta)
    {
        s->scroll += MulDiv(delta, 96, dpi);
        scrollSettings(*s);
    }
}
static void preset(Settings &s, int index)
{
    if (!s.rendererAvailable || index < 0 || index > 5)
        return;
    s.updating = true;
    s.autoPreset = index == 0;
    s.confirmPreset = !s.autoPreset;
    if (s.autoPreset)
    {
        for (auto &r : s.rows)
            if (r.option && !r.option->presets.empty())
            {
                r.value = r.initial;
                if (r.edit)
                    SetWindowTextW(r.edit, r.value.c_str());
                else
                {
                    int initialIndex = -1;
                    try
                    {
                        initialIndex = (int)(parsed(*r.option, r.value) - r.minimum);
                    }
                    catch (...)
                    {
                    }
                    if (initialIndex < 0)
                        initialIndex = r.knownChoices;
                    select(r, initialIndex);
                }
            }
    }
    else
    {
        int p = index - 1;
        if (p >= 0 && p < 4)
        {
            for (auto &r : s.rows)
                if (r.option && !r.option->presets.empty())
                {
                    double n = r.option->presets[p];
                    r.value = raw(*r.option, n);
                    if (r.edit)
                        SetWindowTextW(r.edit, r.value.c_str());
                    else
                        select(r, (int)(n - r.minimum));
                }
        }
    }
    for (auto &r : s.rows)
        if (r.key == L"$preset")
            select(r, index);
    s.updating = false;
    layoutSettings(s);
}

static void validateCamera(Settings &, const Map<double> &numbers, const std::set<S, Less> &changed)
{
    auto pair = [&](S a, S b, S def = L"") {
        if (!changed.count(a) && !changed.count(b) && !changed.count(def))
            return;
        if (!numbers.count(a) || !numbers.count(b) || numbers.at(a) > numbers.at(b) ||
            (!def.empty() && numbers.count(def) &&
             (numbers.at(def) < numbers.at(a) || numbers.at(def) > numbers.at(b))))
            fail(tr(L"InvalidCameraRange"));
    };
    pair(L"NearClipDistance", L"FarClipDistance");
    if (changed.count(L"NearClipDistance") && numbers.at(L"NearClipDistance") <= 0)
        fail(tr(L"InvalidCameraRange"));
    for (auto spec : std::vector<std::pair<S, bool>>{{L"Camera{0}Height", true},
                                                     {L"Camera{0}DistanceToCarrier", true},
                                                     {L"Camera{0}LimitDistanceToCarrier", false},
                                                     {L"CameraDefault{0}Pitch", false},
                                                     {L"CameraBriefing{0}LimitDistanceToCarrier", false},
                                                     {L"CameraBriefing{0}DistanceToCarrier", false},
                                                     {L"CameraBriefing{0}Pitch", false}})
    {
        auto key = [&](S t) {
            S r = spec.first;
            r.replace(r.find(L"{0}"), 3, t);
            return r;
        };
        pair(key(L"Min"), key(L"Max"), spec.second ? key(L"Default") : L"");
    }
}
static void saveSettings(Settings &s, bool pathsOnly = false)
{
    if (gameRunning(exeName))
        fail(text(L"Close the game before saving settings.", L"Закройте игру перед сохранением настроек."));
    Values changes;
    Map<double> camera;
    std::set<S, Less> changedCamera;
    Ini next = *s.renderer;
    bool graphicsChanged = false;
    for (auto &r : s.rows)
    {
        if (pathsOnly ? r.page != 3 : r.page == 3)
            continue;
        S value = r.edit ? windowText(r.edit) : r.value;
        if (r.drop)
        {
            int index = (int)SendMessageW(r.drop, CB_GETCURSEL, 0, 0);
            if (index >= 0 && index < (int)r.choices.size())
                value = r.choices[index];
            else
                value = r.initial;
        }
        if (r.key == L"$preset")
            continue;
        if (r.page == 2 && !value.empty())
        {
            try
            {
                camera[r.key] = numeric(value);
            }
            catch (...)
            {
                if (value != r.initial)
                    throw;
            }
        }
        if (value == r.initial || r.locked)
            continue;
        if (r.option)
        {
            double n = parsed(*r.option, value);
            bool same = false;
            try
            {
                same = std::abs(parsed(*r.option, r.initial) - n) < 0.0001;
            }
            catch (...)
            {
            }
            if (same)
                continue;
            next.set(L"graphics", r.key, raw(*r.option, n));
            if (!r.option->presets.empty())
                graphicsChanged = true;
            continue;
        }
        if (r.page == 2)
        {
            changedCamera.insert(r.key);
            double n = numeric(value);
            if ((r.type == 1 || r.key.find(L"Speed") != S::npos || r.key.find(L"Power") != S::npos) && n < 0)
                fail(tr(L"InvalidCameraRange"));
        }
        Value v;
        if (r.type == 2)
        {
            if (r.path && !directory(value))
                fail(tr(L"InvalidSettingsPath"));
            if (r.key == L"videoresolution")
            {
                unsigned a = 0, b = 0;
                wchar_t tail = 0;
                if (swscanf_s(value.c_str(), L"%ux%u%c", &a, &b, &tail, 1) != 2 || a < 640 || b < 480 ||
                    a > 65535 || b > 65535 || a < b)
                    fail(tr(L"InvalidResolution"));
            }
            v = strValue(r.section, r.key, value);
        }
        else if (r.type == 1)
        {
            double n = numeric(value);
            if (n < 0 || n > 2147483647 || n != floor(n))
                fail(L"Invalid integer: " + r.label);
            v = intValue(r.section, r.key, (DWORD)n);
        }
        else
        {
            double n = numeric(value);
            if (!std::isfinite((float)n))
                fail(tr(L"EnterNumber"));
            v = floatValue(r.section, r.key, (float)n);
        }
        changes[v.id()] = v;
    }
    validateCamera(s, camera, changedCamera);
    if (!pathsOnly && s.rendererAvailable)
    {
        // Auto uses the renderer's existing one-time GPU setup; the renderer
        // sets this back to true only after the player confirms its suggestion.
        if (s.autoPreset)
            next.set(L"setup", L"first_run_done", L"false");
        else if (s.confirmPreset || graphicsChanged)
            next.set(L"setup", L"first_run_done", L"true");
    }
    if (!pathsOnly && (s.confirmPreset || graphicsChanged || next.content != s.renderer->content) &&
        !s.rendererAvailable)
        fail(tr(L"OwnedRendererUnavailable"));
    next.save([&] { s.launcher.save([&] { commitProfile(s.baseline, changes); }); });
    if (pathsOnly)
        for (auto &r : s.rows)
            if (r.page == 3)
            {
                r.initial = r.value = r.edit ? windowText(r.edit) : r.value;
                if (r.key != L"$exe")
                {
                    r.baseline = readSetting(r.section, r.key);
                    s.baseline[r.baseline.id()] = r.baseline;
                }
            }
}
static void resetSettings(Settings &s)
{
    Values defaults = loadDefaultProfile();
    s.updating = true;
    for (auto &r : s.rows)
    {
        if (r.option || r.key == L"$preset" || r.page == 3)
            continue;
        auto i = defaults.find(r.baseline.id());
        if (i == defaults.end())
            continue;
        S value = valueText(i->second);
        r.value = value;
        if (r.edit)
            SetWindowTextW(r.edit, value.c_str());
        else if (r.drop)
        {
            auto choice = std::find(r.choices.begin(), r.choices.end(), value);
            if (choice != r.choices.end())
                SendMessageW(r.drop, CB_SETCURSEL, choice - r.choices.begin(), 0);
        }
        else
        {
            int n = -1;
            try
            {
                n = (int)numeric(value);
            }
            catch (...)
            {
            }
            select(r, n);
        }
    }
    s.updating = false;
    layoutSettings(s);
}
static void chooseFolder(Row &r)
{
    BROWSEINFOW b{};
    b.hwndOwner = currentSettings->window;
    b.lpszTitle = r.label.c_str();
    b.ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE;
    auto p = SHBrowseForFolderW(&b);
    if (p)
    {
        wchar_t path[MAX_PATH];
        if (SHGetPathFromIDListW(p, path))
            SetWindowTextW(r.edit, path);
        CoTaskMemFree(p);
    }
}
static void handleSettingCommand(Settings &s, int id, int code)
{
    if (s.updating)
        return;
    if (id >= 210 && id < 214)
    {
        s.page = id - 210;
        s.scroll = 0;
        layoutSettings(s);
        return;
    }
    if (id >= 500 && id < 500 + (int)s.groupTitles.size())
    {
        s.expanded[id - 500] = !s.expanded[id - 500];
        layoutSettings(s);
        return;
    }
    if (id >= 6000 && id < 6000 + (int)s.rows.size())
    {
        chooseFolder(s.rows[id - 6000]);
        return;
    }
    if (id >= 1000 && id < 1000 + (int)s.rows.size())
    {
        if (code == EN_CHANGE || code == CBN_SELCHANGE)
        {
            auto &r = s.rows[id - 1000];
            if (r.option)
                syncPreset(s, !r.option->presets.empty());
        }
        return;
    }
    if (id >= 3000 && id < 3000 + (int)s.rows.size() * 8)
    {
        int i = (id - 3000) / 8, j = (id - 3000) % 8;
        auto &r = s.rows[i];
        if (r.toggle)
        {
            select(r, r.choice == 1 ? 0 : 1);
            return;
        }
        if (j >= (int)r.choices.size())
            return;
        if (r.key == L"$preset")
        {
            preset(s, j);
            return;
        }
        select(r, j);
        if (r.option)
            syncPreset(s, !r.option->presets.empty());
        return;
    }
    if (id == 201)
    {
        saveSettings(s);
        DestroyWindow(s.window);
    }
    else if (id == 202)
        DestroyWindow(s.window);
    else if (id == 203)
        resetSettings(s);
    else if (id == 204)
        saveSettings(s, true);
}
static LRESULT drawSettingsItem(DRAWITEMSTRUCT *d)
{
    if (d->CtlType != ODT_COMBOBOX)
        return drawButton(d);
    int length = (int)SendMessageW(d->hwndItem, CB_GETLBTEXTLEN, d->itemID, 0);
    S value;
    if (d->itemID != (UINT)-1 && length >= 0)
    {
        value.resize(length + 1);
        SendMessageW(d->hwndItem, CB_GETLBTEXT, d->itemID, (LPARAM)&value[0]);
    }
    bool highlighted = (d->itemState & ODS_SELECTED) != 0;
    HBRUSH brush = CreateSolidBrush(highlighted ? (dark ? RGB(61, 53, 40) : RGB(248, 234, 210)) : surface());
    FillRect(d->hDC, &d->rcItem, brush);
    DeleteObject(brush);
    SetTextColor(d->hDC, ink());
    SetBkMode(d->hDC, TRANSPARENT);
    SelectObject(d->hDC, sectionFont);
    RECT rect = d->rcItem;
    rect.left += px(6);
    DrawTextW(d->hDC, value.c_str(), -1, &rect, DT_SINGLELINE | DT_LEFT | DT_VCENTER | DT_NOPREFIX);
    return TRUE;
}
static LRESULT CALLBACK canvasProc(HWND w, UINT m, WPARAM a, LPARAM b)
{
    auto *s = currentSettings;
    if (!s)
        return DefWindowProcW(w, m, a, b);
    switch (m)
    {
    case WM_COMMAND:
        try
        {
            handleSettingCommand(*s, LOWORD(a), HIWORD(a));
        }
        catch (const std::exception &e)
        {
            error(s->window, e);
        }
        return 0;
    case WM_DRAWITEM:
        return drawSettingsItem((DRAWITEMSTRUCT *)b);
    case WM_MEASUREITEM:
        ((MEASUREITEMSTRUCT *)b)->itemHeight = px(24);
        return TRUE;
    case WM_CTLCOLORSTATIC:
    case WM_CTLCOLOREDIT:
    case WM_CTLCOLORLISTBOX:
        SetTextColor((HDC)a, ink());
        SetBkColor((HDC)a, m == WM_CTLCOLORLISTBOX ? surface() : background());
        return (LRESULT)(m == WM_CTLCOLORLISTBOX ? surfaceBrush : bgBrush);
    case WM_ERASEBKGND:
        return 1;
    case WM_MOUSEWHEEL:
        return SendMessageW(s->viewport, m, a, b);
    case WM_PAINT:
        return paintSurface(w, bgBrush);
    }
    return DefWindowProcW(w, m, a, b);
}
static LRESULT CALLBACK viewportProc(HWND w, UINT m, WPARAM a, LPARAM b)
{
    auto *s = currentSettings;
    if (!s)
        return DefWindowProcW(w, m, a, b);
    if (m == WM_ERASEBKGND)
        return 1;
    if (m == WM_PAINT)
        return paintSurface(w, bgBrush);
    if (m == WM_MOUSEWHEEL)
    {
        s->scroll -= GET_WHEEL_DELTA_WPARAM(a) / WHEEL_DELTA * 66;
        scrollSettings(*s);
        return 0;
    }
    if (m == WM_VSCROLL)
    {
        SCROLLINFO info{sizeof(info), SIF_ALL};
        GetScrollInfo(w, SB_VERT, &info);
        switch (LOWORD(a))
        {
        case SB_LINEUP:
            s->scroll -= 26;
            break;
        case SB_LINEDOWN:
            s->scroll += 26;
            break;
        case SB_PAGEUP:
            s->scroll -= info.nPage;
            break;
        case SB_PAGEDOWN:
            s->scroll += info.nPage;
            break;
        case SB_THUMBTRACK:
        case SB_THUMBPOSITION:
            s->scroll = info.nTrackPos;
            break;
        case SB_TOP:
            s->scroll = 0;
            break;
        case SB_BOTTOM:
            s->scroll = s->total;
            break;
        }
        scrollSettings(*s);
        return 0;
    }
    return DefWindowProcW(w, m, a, b);
}
static LRESULT CALLBACK settingsProc(HWND w, UINT m, WPARAM a, LPARAM b)
{
    auto *s = currentSettings;
    switch (m)
    {
    case WM_GETMINMAXINFO:
        ((MINMAXINFO *)b)->ptMinTrackSize = {px(820), px(550)};
        return 0;
    case WM_SIZE:
        if (s && s->canvas && !s->blocks.empty())
            layoutSettings(*s);
        return 0;
    case WM_COMMAND:
        try
        {
            if (s)
                handleSettingCommand(*s, LOWORD(a), HIWORD(a));
        }
        catch (const std::exception &e)
        {
            error(w, e);
        }
        return 0;
    case WM_DRAWITEM:
        return drawSettingsItem((DRAWITEMSTRUCT *)b);
    case WM_MEASUREITEM:
        ((MEASUREITEMSTRUCT *)b)->itemHeight = px(24);
        return TRUE;
    case WM_ERASEBKGND: {
        RECT r;
        GetClientRect(w, &r);
        FillRect((HDC)a, &r, bgBrush);
        return 1;
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
static void options()
{
    Settings s;
    currentSettings = &s;
    try
    {
        s.window = modalWindow(L"ReStarter.Native.Settings", L"ReStarter — " + tr(L"Settings"), 900, 650,
                               mainWindow);
        // Buffer the viewport and its native children together, including edits
        // and labels. Per-control buffering alone cannot make scrolling atomic.
        s.viewport = CreateWindowExW(WS_EX_CONTROLPARENT | WS_EX_COMPOSITED, L"ReStarter.Native.Viewport",
                                     L"", WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN | WS_VSCROLL, 0, 0, 0, 0,
                                     s.window, nullptr, instance, nullptr);
        s.canvas = CreateWindowExW(WS_EX_CONTROLPARENT, L"ReStarter.Native.Canvas", L"",
                                   WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN, 0, 0, 0, 0, s.viewport, nullptr,
                                   instance, nullptr);
        const wchar_t *tabs[] = {L"tabGraphics", L"tabMultiplayer", L"tabCamera", L"tabPaths"};
        for (int i = 0; i < 4; i++)
        {
            auto w = button(s.window, tr(tabs[i]), 210 + i);
            SendMessageW(w, WM_SETFONT, (WPARAM)sectionFont, FALSE);
            s.nav.push_back(w);
        }
        s.ok = button(s.window, tr(L"btOk"), 201);
        s.cancel = button(s.window, tr(L"btCancel"), 202);
        s.reset = button(s.window, tr(L"ResetDefaults"), 203);
        for (auto w : {s.ok, s.cancel, s.reset})
            SendMessageW(w, WM_SETFONT, (WPARAM)sectionFont, FALSE);
        buildSettings(s);
        layoutSettings(s);
        ensureVisible = revealSetting;
        EnableWindow(mainWindow, FALSE);
        titleTheme(s.window);
        ShowWindow(s.window, SW_SHOW);
        SetFocus(s.nav[0]);
        MSG msg;
        while (IsWindow(s.window) && GetMessageW(&msg, nullptr, 0, 0) > 0)
        {
            if (msg.message == WM_KEYDOWN && msg.wParam == VK_ESCAPE)
            {
                DestroyWindow(s.window);
                continue;
            }
            if (!IsDialogMessageW(s.window, &msg))
            {
                TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
        }
    }
    catch (...)
    {
        if (IsWindow(s.window))
            DestroyWindow(s.window);
        currentSettings = nullptr;
        ensureVisible = nullptr;
        throw;
    }
    currentSettings = nullptr;
    ensureVisible = nullptr;
}
