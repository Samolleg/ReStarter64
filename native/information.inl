// The four pages and notices are embedded, as in ReStarter64Information.cs.
static HWND infoWindow, infoHeading, infoBody;
static std::vector<HWND> infoButtons;
static S resourceText(int id)
{
    auto resource = FindResourceW(instance, MAKEINTRESOURCEW(id), RT_RCDATA);
    if (!resource) fail(L"Missing embedded information page");
    auto data = (const BYTE *)LockResource(LoadResource(instance, resource));
    return decode(Bytes(data, data + SizeofResource(instance, resource)));
}
static S infoTitle(int page)
{
    if (page == 0) return tr(L"Information");
    if (page == 1) return text(L"NVIDIA DLSS license", L"Лицензия NVIDIA DLSS");
    if (page == 2) return text(L"Third-party components", L"Сторонние компоненты");
    return L"Microsoft MIT";
}
static void selectInfo(int page)
{
    S body;
    if (page) body = resourceText(109 + page);
    else body = text(
        L"ReStarter64 is a standalone local launcher for EIRebuild64.\r\n\r\nStarts EIRebuild64 with the selected mod. Its settings are restarter64.config, restarter64-settings.xml, restarter64-defaults.xml and Config64/rddraw.ini. Renderer settings match F10; Apply saves your edits.\r\n\r\nThe NVIDIA DLSS license and third-party notices are included in the other tabs. No NVIDIA logos or NVIDIA Marks artwork are used.\r\n\r\n",
        L"ReStarter64 — самостоятельный локальный стартер EIRebuild64.\r\n\r\nЗапускает только EIRebuild64 с выбранным модом. Собственные настройки: restarter64.config, restarter64-settings.xml, restarter64-defaults.xml и Config64/rddraw.ini. Параметры рендера соответствуют меню F10; изменения сохраняются при нажатии «Применить».\r\n\r\nЛицензия NVIDIA DLSS и сведения о сторонних компонентах приведены на соседних вкладках. Логотипы и изображения NVIDIA Marks не используются.\r\n\r\n") +
        L"This product uses NVIDIA DLSS and NVIDIA NGX.\r\nNVIDIA DLSS SDK. Copyright NVIDIA Corporation. All rights reserved.\r\nNVIDIA and GeForce RTX are trademarks and/or registered trademarks of NVIDIA Corporation in the U.S. and other countries.\r\n\r\nDLSS SDK v310.9.16: https://github.com/NVIDIA/DLSS/tree/v310.9.16";
    SetWindowTextW(infoHeading, infoTitle(page).c_str());
    SetWindowTextW(infoBody, body.c_str());
    SendMessageW(infoBody, EM_SETSEL, 0, 0);
    SendMessageW(infoBody, EM_SCROLLCARET, 0, 0);
    for (int i=0;i<4;++i)
    {
        if (i==page) SetPropW(infoButtons[i], L"selected", (HANDLE)1);
        else RemovePropW(infoButtons[i], L"selected");
        InvalidateRect(infoButtons[i],nullptr,FALSE);
    }
}
static void layoutInfo()
{
    RECT r; GetClientRect(infoWindow,&r);
    int width=MulDiv(r.right,96,dpi), height=MulDiv(r.bottom,96,dpi);
    for(int i=0;i<4;++i) locate(infoButtons[i],16,16+i*58,245,50);
    locate(infoHeading,280,22,width-300,42);
    locate(infoBody,280,80,width-300,height-158);
    locate(infoButtons[4],width-120,height-60,100,40);
}
static LRESULT CALLBACK informationProc(HWND w, UINT m, WPARAM a, LPARAM b)
{
    if(m==WM_GETMINMAXINFO) { ((MINMAXINFO*)b)->ptMinTrackSize={px(820),px(500)}; return 0; }
    if(m==WM_SIZE && infoBody) { layoutInfo(); return 0; }
    if(m==WM_COMMAND)
    {
        int id=LOWORD(a);
        if(id>=900 && id<904) selectInfo(id-900);
        if(id==904) DestroyWindow(w);
        return 0;
    }
    if(m==WM_DRAWITEM) return drawButton((DRAWITEMSTRUCT*)b);
    if(m==WM_CTLCOLORSTATIC || m==WM_CTLCOLOREDIT)
    { SetTextColor((HDC)a,ink()); SetBkColor((HDC)a,background()); return (LRESULT)bgBrush; }
    if(m==WM_ERASEBKGND) { RECT r; GetClientRect(w,&r); FillRect((HDC)a,&r,bgBrush); return 1; }
    if(m==WM_CLOSE) { DestroyWindow(w); return 0; }
    if(m==WM_DESTROY) { EnableWindow(mainWindow,TRUE); SetForegroundWindow(mainWindow); return 0; }
    return DefWindowProcW(w,m,a,b);
}
static void information()
{
    infoButtons.clear(); infoBody=nullptr;
    infoWindow=modalWindow(L"ReStarter64.Information", L"ReStarter64 — "+tr(L"Information"),920,600,mainWindow);
    infoHeading=control(infoWindow,L"STATIC",L"",0,SS_LEFT);
    SendMessageW(infoHeading,WM_SETFONT,(WPARAM)settingsHeadingFont,FALSE);
    infoBody=control(infoWindow,L"EDIT",L"",0,ES_MULTILINE|ES_READONLY|ES_AUTOVSCROLL|WS_VSCROLL|WS_TABSTOP);
    SendMessageW(infoBody,EM_SETLIMITTEXT,1024*1024,0);
    for(int i=0;i<4;++i) infoButtons.push_back(button(infoWindow,infoTitle(i),900+i));
    infoButtons.push_back(button(infoWindow,tr(L"Close"),904));
    selectInfo(0); layoutInfo(); titleTheme(infoWindow);
    EnableWindow(mainWindow,FALSE); ShowWindow(infoWindow,SW_SHOW);
    MSG msg;
    while(IsWindow(infoWindow) && GetMessageW(&msg,nullptr,0,0)>0)
    {
        if(msg.message==WM_KEYDOWN && msg.wParam==VK_ESCAPE) { DestroyWindow(infoWindow); continue; }
        if(!IsDialogMessageW(infoWindow,&msg)) { TranslateMessage(&msg); DispatchMessageW(&msg); }
    }
    infoBody=nullptr; infoButtons.clear();
}
