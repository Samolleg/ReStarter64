#include "core.hpp"
#include <shlobj.h>
#include <tlhelp32.h>
#include <msxml6.h>
#include <wincrypt.h>
#include <sstream>
#include <iomanip>
#include <set>
#include <locale>

namespace ei
{
S root;
int language = 2;
S text(const wchar_t *en, const wchar_t *ru)
{
    return language == 2 ? ru : en;
}
S lower(S s)
{
    for (auto &c : s)
        c = (wchar_t)towlower(c);
    return s;
}
S trim(S s)
{
    auto a = s.find_first_not_of(L" \t\r\n");
    if (a == S::npos)
        return L"";
    return s.substr(a, s.find_last_not_of(L" \t\r\n") - a + 1);
}
S join(const S &a, const S &b)
{
    return a + (a.empty() || a.back() == L'\\' ? L"" : L"\\") + b;
}
S full(const S &s)
{
    DWORD n = GetFullPathNameW(s.c_str(), 0, nullptr, nullptr);
    if (!n)
        fail(L"Invalid path: " + s);
    std::vector<wchar_t> b(n);
    if (!GetFullPathNameW(s.c_str(), n, b.data(), nullptr))
        fail(s);
    return b.data();
}
bool exists(const S &p)
{
    return GetFileAttributesW(p.c_str()) != INVALID_FILE_ATTRIBUTES;
}
bool directory(const S &p)
{
    DWORD a = GetFileAttributesW(p.c_str());
    return a != INVALID_FILE_ATTRIBUTES && (a & FILE_ATTRIBUTE_DIRECTORY);
}
[[noreturn]] void fail(const S &s)
{
    auto b = utf8(s);
    throw std::runtime_error(std::string(b.begin(), b.end()));
}
void check(LONG e, const S &s)
{
    if (e != ERROR_SUCCESS)
    {
        wchar_t *b = nullptr;
        FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM |
                           FORMAT_MESSAGE_IGNORE_INSERTS,
                       nullptr, e, 0, (wchar_t *)&b, 0, nullptr);
        S m = s + L" (" + std::to_wstring(e) + L"): " + (b ? b : L"");
        if (b)
            LocalFree(b);
        auto u = utf8(m);
        throw std::runtime_error(std::string(u.begin(), u.end()));
    }
}
struct Handle
{
    HANDLE h = INVALID_HANDLE_VALUE;
    explicit Handle(HANDLE p) : h(p) {}
    ~Handle()
    {
        if (h && h != INVALID_HANDLE_VALUE)
            CloseHandle(h);
    }
};
Bytes readFile(const S &p)
{
    Handle f(CreateFileW(p.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr));
    if (f.h == INVALID_HANDLE_VALUE)
        check(GetLastError(), p);
    LARGE_INTEGER n{};
    if (!GetFileSizeEx(f.h, &n))
        check(GetLastError(), p);
    if (n.QuadPart > 16 * 1024 * 1024)
        fail(L"File too large: " + p);
    Bytes b((size_t)n.QuadPart);
    DWORD got = 0;
    if (!b.empty() && (!ReadFile(f.h, b.data(), (DWORD)b.size(), &got, nullptr) || got != b.size()))
        fail(L"Cannot read: " + p);
    return b;
}
void atomicFile(const S &p, const Bytes &b)
{
    S tmp = p + L".native-" + std::to_wstring(GetCurrentProcessId()) + L".tmp";
    try
    {
        {
            Handle f(CreateFileW(tmp.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL,
                                 nullptr));
            if (f.h == INVALID_HANDLE_VALUE)
                check(GetLastError(), tmp);
            DWORD n = 0;
            if ((!b.empty() && (!WriteFile(f.h, b.data(), (DWORD)b.size(), &n, nullptr) || n != b.size())) ||
                !FlushFileBuffers(f.h))
                check(GetLastError(), tmp);
        }
        if (exists(p))
        {
            if (!ReplaceFileW(p.c_str(), tmp.c_str(), nullptr, 0, nullptr, nullptr))
                check(GetLastError(), p);
        }
        else if (!MoveFileExW(tmp.c_str(), p.c_str(), MOVEFILE_WRITE_THROUGH))
            check(GetLastError(), p);
    }
    catch (...)
    {
        DeleteFileW(tmp.c_str());
        throw;
    }
}
Bytes utf8(const S &s)
{
    int n = WideCharToMultiByte(CP_UTF8, 0, s.c_str(), (int)s.size(), nullptr, 0, nullptr, nullptr);
    Bytes b(n);
    if (n)
        WideCharToMultiByte(CP_UTF8, 0, s.c_str(), (int)s.size(), (char *)b.data(), n, nullptr, nullptr);
    return b;
}
S decode(const Bytes &b, UINT fallback)
{
    if (b.size() >= 2 && b[0] == 255 && b[1] == 254)
    {
        if (b.size() % 2)
            fail(L"Invalid UTF-16 file");
        return S((wchar_t *)(b.data() + 2), (b.size() - 2) / 2);
    }
    int off = b.size() >= 3 && b[0] == 239 && b[1] == 187 && b[2] == 191 ? 3 : 0;
    int len = (int)b.size() - off;
    if (!len)
        return L"";
    UINT cp = CP_UTF8;
    int n = MultiByteToWideChar(cp, MB_ERR_INVALID_CHARS, (char *)b.data() + off, len, nullptr, 0);
    if (!n)
    {
        cp = fallback;
        n = MultiByteToWideChar(cp, 0, (char *)b.data() + off, len, nullptr, 0);
    }
    S s(n, L'\0');
    if (n)
        MultiByteToWideChar(cp, cp == CP_UTF8 ? MB_ERR_INVALID_CHARS : 0, (char *)b.data() + off, len, &s[0],
                            n);
    return s;
}
S number(double n)
{
    std::wostringstream s;
    s.imbue(std::locale::classic());
    s << std::setprecision(8) << n;
    return s.str();
}
double numeric(S s)
{
    std::replace(s.begin(), s.end(), L',', L'.');
    std::wistringstream in(trim(s));
    in.imbue(std::locale::classic());
    double v;
    in >> v;
    if (!in || !std::isfinite(v))
        fail(text(L"Enter a valid number", L"Введите корректное число"));
    in >> std::ws;
    if (!in.eof())
        fail(text(L"Invalid number", L"Некорректное число"));
    return v;
}
S setting(const S &k, const S &d)
{
    return Ini(join(root, L"restarter64.config")).get(L"Settings", k, d);
}
void settings(const Map<S> &edits)
{
    Ini ini(join(root, L"restarter64.config"));
    for (auto &e : edits)
        ini.set(L"Settings", e.first, e.second);
    ini.save([] {});
}
S tr(const S &key)
{
    static Map<std::vector<S>> words;
    if (words.empty())
    {
        HRSRC r = FindResourceW(nullptr, MAKEINTRESOURCEW(102), RT_RCDATA);
        if (r)
        {
            auto p = (BYTE *)LockResource(LoadResource(nullptr, r));
            Bytes b(p, p + SizeofResource(nullptr, r));
            std::wistringstream lines(decode(b));
            S line;
            while (std::getline(lines, line))
            {
                if (!line.empty() && line.back() == L'\r')
                    line.pop_back();
                std::vector<S> cols;
                size_t at = 0;
                for (;;)
                {
                    size_t e = line.find(L'|', at);
                    cols.push_back(line.substr(at, e == S::npos ? e : e - at));
                    if (e == S::npos)
                        break;
                    at = e + 1;
                }
                if (cols.size() > 2)
                    words[cols[0]] = cols;
            }
        }
    }
    auto i = words.find(key);
    if (i == words.end())
        return key;
    size_t c = (size_t)language < i->second.size() ? (size_t)language : 1;
    S s = i->second[c];
    size_t p;
    while ((p = s.find(L"<rn>")) != S::npos)
        s.replace(p, 4, L"\r\n");
    return s;
}
Value strValue(const S &s, const S &n, const S &data)
{
    Value v{s, n, REG_SZ, {}, true};
    auto p = (BYTE *)data.c_str();
    v.bytes.assign(p, p + (data.size() + 1) * 2);
    return v;
}
Value intValue(const S &s, const S &n, DWORD d)
{
    Value v{s, n, REG_DWORD, {}, true};
    v.bytes.resize(4);
    memcpy(v.bytes.data(), &d, 4);
    return v;
}
Value floatValue(const S &s, const S &n, float f)
{
    Value v = intValue(s, n, 0);
    v.kind = REG_BINARY;
    memcpy(v.bytes.data(), &f, 4);
    return v;
}
S valueText(const Value &v)
{
    if (!v.present)
        return L"";
    if (v.kind == REG_SZ || v.kind == REG_EXPAND_SZ)
    {
        S s((wchar_t *)v.bytes.data(), v.bytes.size() / 2);
        while (!s.empty() && !s.back())
            s.pop_back();
        return s;
    }
    if (v.kind == REG_DWORD && v.bytes.size() == 4)
    {
        DWORD n;
        memcpy(&n, v.bytes.data(), 4);
        return std::to_wstring(n);
    }
    if (v.kind == REG_BINARY && v.bytes.size() == 4)
    {
        float f;
        memcpy(&f, v.bytes.data(), 4);
        return number(f);
    }
    return L"";
}
static S escape(S s)
{
    S o;
    for (auto c : s)
    {
        switch (c)
        {
        case L'&':
            o += L"&amp;";
            break;
        case L'<':
            o += L"&lt;";
            break;
        case L'>':
            o += L"&gt;";
            break;
        case L'"':
            o += L"&quot;";
            break;
        case L'\r':
            o += L"&#13;";
            break;
        case L'\n':
            o += L"&#10;";
            break;
        case L'\t':
            o += L"&#9;";
            break;
        default:
            o += c;
        }
    }
    return o;
}
static S base64(const Bytes &b)
{
    if (b.empty())
        return L"";
    DWORD n = 0;
    if (!CryptBinaryToStringW(b.data(), (DWORD)b.size(), CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF, nullptr,
                              &n))
        check(GetLastError(), L"base64");
    S s(n, L'\0');
    CryptBinaryToStringW(b.data(), (DWORD)b.size(), CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF, &s[0], &n);
    s.resize(n);
    while (!s.empty() && s.back() == 0)
        s.pop_back();
    return s;
}
static Bytes unbase64(const S &s)
{
    if (s.empty())
        return {};
    DWORD n = 0;
    if (!CryptStringToBinaryW(s.c_str(), (DWORD)s.size(), CRYPT_STRING_BASE64, nullptr, &n, nullptr, nullptr))
        fail(L"Invalid profile base64");
    Bytes b(n);
    if (!CryptStringToBinaryW(s.c_str(), (DWORD)s.size(), CRYPT_STRING_BASE64, b.data(), &n, nullptr,
                              nullptr))
        fail(L"Invalid profile base64");
    return b;
}
template <class T> struct Com
{
    T *p = nullptr;
    ~Com()
    {
        if (p)
            p->Release();
    }
    T *operator->()
    {
        return p;
    }
};
struct Bstr
{
    BSTR p;
    explicit Bstr(const S &s) : p(SysAllocStringLen(s.data(), (UINT)s.size())) {}
    ~Bstr()
    {
        SysFreeString(p);
    }
    operator BSTR()
    {
        return p;
    }
};
static S attribute(IXMLDOMNode *n, const wchar_t *name)
{
    Com<IXMLDOMNamedNodeMap> a;
    check(n->get_attributes(&a.p), L"XML attributes");
    Com<IXMLDOMNode> x;
    check(a->getNamedItem(Bstr(name), &x.p), L"XML attribute");
    if (!x.p)
        fail(L"Missing profile attribute");
    BSTR t = nullptr;
    x->get_text(&t);
    S s = t ? t : L"";
    SysFreeString(t);
    return s;
}
static void loadProfileDocument(const Bytes &bytes, const S &path, Com<IXMLDOMDocument2> &d)
{
    HRESULT hr = CoCreateInstance(__uuidof(DOMDocument60), nullptr, CLSCTX_INPROC_SERVER,
                                  __uuidof(IXMLDOMDocument2), (void **)&d.p);
    if (FAILED(hr))
        fail(L"Windows XML component (MSXML6) is unavailable");
    d->put_async(VARIANT_FALSE);
    d->put_preserveWhiteSpace(VARIANT_TRUE);
    d->put_validateOnParse(VARIANT_FALSE);
    d->put_resolveExternals(VARIANT_FALSE);
    VARIANT prohibit{};
    prohibit.vt = VT_BOOL;
    prohibit.boolVal = VARIANT_TRUE;
    d->setProperty(Bstr(L"ProhibitDTD"), prohibit);
    auto content = decode(bytes);
    VARIANT_BOOL ok = VARIANT_FALSE;
    d->loadXML(Bstr(content), &ok);
    if (ok != VARIANT_TRUE)
        fail(L"Invalid profile XML: " + path);
}
Values loadProfileBytes(const Bytes &bytes, const S &path)
{
    Com<IXMLDOMDocument2> d;
    loadProfileDocument(bytes, path, d);
    Com<IXMLDOMElement> top;
    d->get_documentElement(&top.p);
    if (!top.p || attribute(top.p, L"version") != L"1")
        fail(L"Unsupported profile: " + path);
    BSTR tag = nullptr;
    top->get_tagName(&tag);
    S tagName = tag ? tag : L"";
    SysFreeString(tag);
    if (tagName != L"profile")
        fail(L"Invalid profile root");
    Com<IXMLDOMNodeList> nodes;
    d->selectNodes(Bstr(L"/profile/value"), &nodes.p);
    long count = 0;
    nodes->get_length(&count);
    Values out;
    for (long i = 0; i < count; i++)
    {
        Com<IXMLDOMNode> n;
        nodes->get_item(i, &n.p);
        Value v;
        v.section = attribute(n.p, L"section");
        const auto first = v.section.find_first_not_of(L'\\');
        v.section = first == S::npos ? S{} : v.section.substr(first);
        v.name = attribute(n.p, L"name");
        int kind = std::stoi(attribute(n.p, L"kind"));
        v.kind = kind == -1 ? REG_NONE : kind;
        v.present = true;
        if (v.section.find(L'/') != S::npos || (L"\\" + v.section + L"\\").find(L"\\..\\") != S::npos ||
            out.count(v.id()))
            fail(L"Invalid profile scope: " + path);
        BSTR t = nullptr;
        n->get_text(&t);
        S s = t ? t : L"";
        SysFreeString(t);
        if (v.kind == REG_SZ || v.kind == REG_EXPAND_SZ)
        {
            auto z = strValue(v.section, v.name, s);
            v.bytes = z.bytes;
        }
        else if (v.kind == REG_DWORD)
        {
            long long q = std::stoll(s);
            DWORD x = (DWORD)q;
            v.bytes.resize(4);
            memcpy(v.bytes.data(), &x, 4);
        }
        else if (v.kind == REG_QWORD)
        {
            long long q = std::stoll(s);
            v.bytes.resize(8);
            memcpy(v.bytes.data(), &q, 8);
        }
        else if (v.kind == REG_BINARY || v.kind == REG_NONE || v.kind == REG_MULTI_SZ)
        {
            v.bytes = unbase64(s);
            if (v.kind == REG_MULTI_SZ)
            {
                v.bytes.push_back(0);
                v.bytes.push_back(0);
                v.bytes.push_back(0);
                v.bytes.push_back(0);
            }
        }
        else
            fail(L"Unsupported profile value type");
        out[v.id()] = v;
    }
    return out;
}
Values loadProfile(const S &path)
{
    return loadProfileBytes(readFile(path), path);
}
static Bytes embeddedDefaultProfile()
{
    HMODULE module = GetModuleHandleW(nullptr);
    HRSRC resource = FindResourceW(module, MAKEINTRESOURCEW(103), RT_RCDATA);
    if (!resource)
        fail(L"Embedded ReStarter defaults are missing");
    HGLOBAL data = LoadResource(module, resource);
    const auto bytes = data ? (const BYTE *)LockResource(data) : nullptr;
    DWORD size = SizeofResource(module, resource);
    if (!bytes || !size)
        fail(L"Cannot read embedded ReStarter defaults");
    return Bytes(bytes, bytes + size);
}
Values loadDefaultProfile()
{
    const S path = join(root, L"restarter64-defaults.xml");
    return exists(path) ? loadProfile(path)
                        : loadProfileBytes(embeddedDefaultProfile(), L"embedded ReStarter defaults");
}
void saveProfile(const S &path, const Values &values)
{
    S xml = L"<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<profile version=\"1\">\r\n";
    for (auto &x : values)
    {
        auto &v = x.second;
        if (!v.present)
            continue;
        S data;
        int kind = (int)v.kind;
        if (v.kind == REG_SZ || v.kind == REG_EXPAND_SZ)
            data = valueText(v);
        else if (v.kind == REG_DWORD)
        {
            int n;
            memcpy(&n, v.bytes.data(), 4);
            data = std::to_wstring(n);
        }
        else if (v.kind == REG_QWORD)
        {
            long long n;
            memcpy(&n, v.bytes.data(), 8);
            data = std::to_wstring(n);
        }
        else
        {
            Bytes b = v.bytes;
            if (v.kind == REG_MULTI_SZ && b.size() >= 4)
            {
                b.resize(b.size() - 4);
            }
            data = base64(b);
            if (v.kind == REG_NONE)
                kind = -1;
        }
        xml += L"  <value section=\"" + escape(v.section) + L"\" name=\"" + escape(v.name) + L"\" kind=\"" +
               std::to_wstring(kind) + L"\">" + escape(data) + L"</value>\r\n";
    }
    xml += L"</profile>\r\n";
    atomicFile(path, utf8(xml));
}
#include "profile.inl"
bool gameRunning(const S &exe)
{
    Handle h(CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0));
    if (h.h == INVALID_HANDLE_VALUE)
        check(GetLastError(), L"Process list");
    PROCESSENTRY32W p{};
    p.dwSize = sizeof(p);
    if (Process32FirstW(h.h, &p))
        do
        {
            if (!_wcsicmp(p.szExeFile, exe.c_str()))
                return true;
        } while (Process32NextW(h.h, &p));
    return false;
}
std::vector<S> resolutions()
{
    std::set<std::pair<int, int>> modes;
    DEVMODEW d{};
    d.dmSize = sizeof(d);
    for (DWORD i = 0; EnumDisplaySettingsW(nullptr, i, &d); i++)
        if (d.dmPelsWidth >= 640 && d.dmPelsHeight >= 480 && d.dmPelsWidth >= d.dmPelsHeight)
            modes.emplace(d.dmPelsWidth, d.dmPelsHeight);
    std::vector<S> r;
    for (auto &m : modes)
        r.push_back(std::to_wstring(m.first) + L"x" + std::to_wstring(m.second));
    return r;
}
S executable()
{
    S selected = setting(L"ExecutablePath", L"Engine64\\EIRebuild64.exe");
    bool absolute = selected.size() > 1 && (selected[1] == L':' || selected[0] == L'\\');
    S path = full(absolute ? selected : join(root, selected));
    S parent = path.substr(0, path.find_last_of(L"\\/"));
    if (lower(path.substr(path.find_last_of(L"\\/") + 1)) != L"eirebuild64.exe" ||
        lower(parent) == lower(join(root, L"Engine")))
        fail(L"Use a separate EIRebuild64.exe directory (default: Engine64).");
    return path;
}
void Game::start(const Mod &mod)
{
    if (gameRunning(L"EIRebuild64.exe"))
        fail(text(L"Close EIRebuild before starting it again.", L"Сначала закройте запущенный EIRebuild."));
    const S path = executable(), engine = path.substr(0, path.find_last_of(L"\\"));
    if (!exists(path))
        fail(L"EIRebuild executable is missing: " + path);
    if (!directory(mod.path) ||
        !exists(join(mod.path, L"config.reg")))
        fail(tr(L"NoMods"));
    selectMod(mod);
    loadProfile(profilePath());
    S command = L"\"" + path + L"\" -profile \"" + profilePath() + L"\" --mod \"" + mod.path + L"\"";
    std::vector<wchar_t> arguments(command.begin(), command.end());
    arguments.push_back(0);
    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION child{};
    if (!CreateProcessW(path.c_str(), arguments.data(), nullptr, nullptr, FALSE, 0, nullptr, engine.c_str(),
                        &startup, &child))
        check(GetLastError(), L"Start EIRebuild");
    CloseHandle(child.hThread);
    CloseHandle(child.hProcess);
}

// INI editing keeps untouched bytes (including unknown keys/comments) and guards
// against another process changing the file while the dialog is open.
struct Line
{
    size_t start, end, next;
    std::string s;
};
static std::vector<Line> lines(const std::string &s)
{
    std::vector<Line> out;
    for (size_t p = 0; p < s.size();)
    {
        size_t e = s.find_first_of("\r\n", p);
        if (e == std::string::npos)
            e = s.size();
        size_t n = e;
        if (n < s.size() && s[n] == '\r')
            n++;
        if (n < s.size() && s[n] == '\n')
            n++;
        out.push_back({p, e, n, s.substr(p, e - p)});
        p = n;
    }
    return out;
}
static std::string narrow(const S &s)
{
    auto b = utf8(s);
    return {b.begin(), b.end()};
}
static std::string strip(std::string s)
{
    size_t a = s.find_first_not_of(" \t\r");
    return a == s.npos ? "" : s.substr(a, s.find_last_not_of(" \t\r") - a + 1);
}
static std::string low(std::string s)
{
    for (auto &c : s)
        if (c >= 'A' && c <= 'Z')
            c += 32;
    return s;
}
static std::string header(std::string s)
{
    s = strip(s);
    if (s.size() >= 3 && s.compare(0, 3, "\xEF\xBB\xBF") == 0)
        s.erase(0, 3);
    return s.size() > 1 && s.front() == '[' && s.back() == ']' ? low(strip(s.substr(1, s.size() - 2))) : "";
}
Ini::Ini(S p) : path(std::move(p))
{
    renderer = lower(path).find(L"rddraw") != S::npos;
    had = exists(path);
    if (had)
        original = readFile(path);
    if ((original.size() < 2 || original[0] != 255 || original[1] != 254) &&
        std::find(original.begin(), original.end(), 0) != original.end())
        fail(L"Unsupported INI encoding: " + path);
    if (renderer &&
        std::find(original.begin(), original.end(), 0) != original.end())
        fail(L"Unsupported renderer INI encoding: " + path);
    if(renderer && original.size()>1024*1024) fail(L"Renderer INI is too large");
    utf16 = original.size() >= 2 && original[0] == 255 && original[1] == 254;
    if (utf16)
    {
        auto converted = utf8(decode(original));
        content.assign(converted.begin(), converted.end());
    }
    else
        content.assign(original.begin(), original.end());
    if (content.find("\r\n") != content.npos)
        newline = "\r\n";
    else if (content.find('\n') != content.npos)
        newline = "\n";
    else if (content.find('\r') != content.npos)
        newline = "\r";
}
static S decodeCodepage(const std::string &s,UINT cp)
{
    int n=MultiByteToWideChar(cp,0,s.data(),(int)s.size(),nullptr,0);
    S out(n,L'\0'); if(n) MultiByteToWideChar(cp,0,s.data(),(int)s.size(),out.data(),n); return out;
}
static std::string encodeCodepage(const S &s,UINT cp)
{
    int n=WideCharToMultiByte(cp,0,s.data(),(int)s.size(),nullptr,0,nullptr,nullptr);
    std::string out(n,'\0'); if(n) WideCharToMultiByte(cp,0,s.data(),(int)s.size(),out.data(),n,nullptr,nullptr); return out;
}
S Ini::get(const S &sec, const S &key, const S &def) const
{
    auto section = narrow(lower(sec)), name = narrow(lower(key));
    std::string current;
    for (auto &l : lines(content))
    {
        auto h = header(l.s);
        if (!h.empty())
        {
            current = h;
            continue;
        }
        auto s = strip(l.s);
        if (current != section || s.empty() || s[0] == ';' || s[0] == '#')
            continue;
        auto e = l.s.find('=');
        if (e != s.npos && low(strip(l.s.substr(0, e))) == name)
        {
            auto v = strip(l.s.substr(e + 1));
            return renderer && lower(key)==L"interface_profile" ? decode(Bytes(v.begin(),v.end())) : decodeCodepage(v,utf16?CP_UTF8:renderer?28591:CP_ACP);
        }
    }
    return def;
}
void Ini::set(const S &sec, const S &key, const S &value)
{
    std::string name = narrow(lower(key)), section = narrow(lower(sec)), current;
    size_t end = content.size();
    bool found = false;
    for (auto &l : lines(content))
    {
        auto h = header(l.s);
        if (!h.empty())
        {
            if (found)
            {
                end = l.start;
                break;
            }
            current = h;
            if (h == section)
                found = true;
            continue;
        }
        if (!found)
            continue;
        auto t = strip(l.s);
        auto e = l.s.find('=');
        if (t.empty() || t[0] == ';' || t[0] == '#' || e == l.s.npos || low(strip(l.s.substr(0, e))) != name)
            continue;
        size_t a = e + 1;
        while (a < l.s.size() && (l.s[a] == ' ' || l.s[a] == '\t'))
            a++;
        size_t b = l.s.find_last_not_of(" \t");
        b = b == l.s.npos || b < a ? a : b + 1;
        content.replace(l.start + a, b - a, encodeCodepage(value,utf16?CP_UTF8:renderer?28591:CP_ACP));
        return;
    }
    std::string insert;
    if (!found)
        insert = "[" + narrow(sec) + "]" + newline;
    insert += narrow(key) + "=" + encodeCodepage(value,utf16?CP_UTF8:renderer?28591:CP_ACP) + newline;
    if (end && content[end - 1] != '\n' && content[end - 1] != '\r')
        insert = newline + insert;
    content.insert(end, insert);
}
S Ini::getAny(const S &key, const S &def) const
{
    for (const auto &l : lines(content))
    {
        auto t=strip(l.s); auto e=t.find('=');
        if(t.empty() || t[0]==';' || t[0]=='#' || e==t.npos) continue;
        if(low(strip(t.substr(0,e)))==narrow(lower(key)))
        { auto v=strip(t.substr(e+1)); return decodeCodepage(v,renderer?28591:CP_ACP); }
    }
    return def;
}
void Ini::setAny(const S &key, const S &value)
{
    for(const auto &l:lines(content))
    {
        auto t=strip(l.s); auto e=l.s.find('=');
        if(t.empty() || t[0]==';' || t[0]=='#' || e==l.s.npos) continue;
        if(low(strip(l.s.substr(0,e)))!=narrow(lower(key))) continue;
        size_t a=e+1; while(a<l.s.size() && (l.s[a]==' ' || l.s[a]=='\t')) ++a;
        size_t b=l.s.find_last_not_of(" \t"); b=b==l.s.npos||b<a?a:b+1;
        content.replace(l.start+a,b-a,encodeCodepage(value,renderer?28591:CP_ACP)); return;
    }
    set(L"interface",key,value);
}
void Ini::validate() const
{
    std::set<std::string> sections, keys;
    std::set<std::string> known;
    for (auto &option : schema())
        known.insert(narrow(option.key));
    bool hud=lower(path).find(L"rddraw_profiles")!=S::npos;
    std::set<std::string> hudKeys;
    for(auto &o:schema()) if(o.category==11 || (o.category==8 && S(o.key)!=L"confine_cursor" && S(o.key).find(L"highlight_")!=0)) hudKeys.insert(narrow(o.key));
    std::string current;
    for (auto &l : lines(content))
    {
        auto h = header(l.s);
        if (!h.empty())
        {
            current = h;
            if ((h == "graphics" || h == "setup") && !sections.insert(h).second)
                fail(L"Duplicate INI section: " + path);
            continue;
        }
        auto s = strip(l.s);
        auto e = s.find('=');
        if (s.empty() || s[0] == ';' || s[0] == '#' || e == s.npos)
            continue;
        auto name = low(strip(s.substr(0, e)));
        if (!((hud ? hudKeys.count(name)!=0 : current == "graphics" && known.count(name)) ||
              (current == "setup" && name == "first_run_done")))
            continue;
        auto id = (hud && hudKeys.count(name) ? std::string("interface") : current) + "\n" + name;
        if (!keys.insert(id).second)
            fail(L"Duplicate INI key: " + path);
    }
}
void Ini::save(const std::function<void()> &other)
{
    Bytes next(content.begin(), content.end());
    if (utf16)
    {
        S wide = decode(next);
        auto p = (BYTE *)wide.data();
        next = {255, 254};
        next.insert(next.end(), p, p + wide.size() * 2);
    }
    if (next == original)
    {
        other();
        return;
    }
    if (exists(path) != had || (had && readFile(path) != original))
        fail(text(L"Configuration changed externally. Reopen settings.",
                  L"Конфигурация изменена извне. Откройте настройки заново."));
    atomicFile(path, next);
    try
    {
        other();
    }
    catch (...)
    {
        if (had)
            atomicFile(path, original);
        else if (!DeleteFileW(path.c_str()))
            check(GetLastError(), path);
        throw;
    }
    original = next;
    had = true;
}

std::vector<Mod> mods()
{
    std::vector<Mod> out;
    WIN32_FIND_DATAW data{};
    Handle find(INVALID_HANDLE_VALUE);
    HANDLE h = FindFirstFileW(join(root, L"MODS\\*").c_str(), &data);
    if (h == INVALID_HANDLE_VALUE)
        return out;
    try
    {
        do
        {
            if (!(data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) || data.cFileName[0] == L'.')
                continue;
            Mod m;
            m.path = join(join(root, L"MODS"), data.cFileName);
            S reg = join(m.path, L"config.reg"), ini = join(m.path, L"mod.config");
            bool useIni = lower(setting(L"UsingINIconfigs")) == L"true" && exists(ini);
            if (!exists(reg) && !useIni)
                continue;
            Map<S> props;
            if (useIni)
            {
                Ini file(ini);
                for (auto key : {L"Title", L"Version", L"Author", L"AuthorEmail", L"URL", L"date_DMY",
                                 L"Single", L"Multi"})
                    props[key] = file.get(L"MOD", key);
                props[L"pluginpath"] = file.get(L"Starter", L"pluginpath");
                props[L"plugintext"] = file.get(L"Starter", L"plugintext");
                m.config = ini;
            }
            else
            {
                Bytes b = readFile(reg);
                auto u16 = [&](size_t p) -> size_t {
                    if (p + 2 > b.size())
                        fail(L"Invalid config.reg: " + reg);
                    return b[p] | (b[p + 1] << 8);
                };
                auto u32 = [&](size_t p) -> DWORD {
                    if (p + 4 > b.size())
                        fail(L"Invalid config.reg: " + reg);
                    DWORD n;
                    memcpy(&n, &b[p], 4);
                    return n;
                };
                auto str = [&](size_t p, size_t n, UINT cp) {
                    if (p + n > b.size())
                        fail(L"Invalid config.reg: " + reg);
                    return decodeCodepage(std::string(b.begin()+p,b.begin()+p+n),cp);
                };
                if (u32(0) != 0x45AB3EFB)
                    fail(L"Invalid config.reg signature: " + reg);
                for (size_t i = 0; i < u16(4); i++)
                {
                    size_t start = u16(6 + i * 6 + 2), count = u16(start), len = u16(start + 2);
                    S section = str(start + 4, len, 1251);
                    if (_wcsicmp(section.c_str(), L"MOD") && _wcsicmp(section.c_str(), L"Starter"))
                        continue;
                    size_t index = start + 4 + len;
                    for (size_t j = 0; j < count; j++)
                    {
                        size_t p = start + u16(index + j * 6 + 2);
                        if (p + 3 > b.size())
                            fail(reg);
                        int type = b[p];
                        size_t nl = u16(p + 1);
                        S name = str(p + 3, nl, 1251);
                        size_t v = p + 3 + nl;
                        if (type == 2)
                            {
                            const size_t length = u16(v);
                            if (v + 2 + length > b.size())
                                fail(L"Invalid config.reg: " + reg);
                            // EIRegFile.DecodeString: strict UTF-8 (optional BOM),
                            // then Windows-1251. Section/key names remain CP1251.
                            size_t offset = v + 2, size = length;
                            if (size >= 3 && b[offset] == 0xEF && b[offset + 1] == 0xBB &&
                                b[offset + 2] == 0xBF)
                            {
                                offset += 3;
                                size -= 3;
                            }
                            const char *data = (const char *)b.data() + offset;
                            const UINT cp = !size || MultiByteToWideChar(
                                CP_UTF8, MB_ERR_INVALID_CHARS, data, (int)size, nullptr, 0)
                                                ? CP_UTF8 : 1251;
                            props[name] = decodeCodepage(std::string(data, size), cp);
                        }
                        else if (type == 0)
                            props[name] = std::to_wstring(u32(v));
                    }
                }
                m.config = reg;
            }
            m.title = props[L"Title"].empty() ? data.cFileName : props[L"Title"];
            m.version = props[L"Version"];
            m.author = props[L"Author"];
            m.email = props[L"AuthorEmail"];
            m.site = props[L"URL"];
            m.date = props[L"date_DMY"];
            m.single = props[L"Single"] != L"0" && lower(props[L"Single"]) != L"false";
            m.multi = props[L"Multi"] != L"0" && lower(props[L"Multi"]) != L"false";
            m.plugin = props[L"pluginpath"];
            m.pluginText = props[L"plugintext"];
            out.push_back(m);
        } while (FindNextFileW(h, &data));
    }
    catch (...)
    {
        FindClose(h);
        throw;
    }
    FindClose(h);
    std::sort(out.begin(), out.end(), [](auto &a, auto &b) { return Less{}(a.title, b.title); });
    return out;
}
} // namespace ei
