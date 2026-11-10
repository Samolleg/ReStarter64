#pragma once
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <string>
#include <vector>
#include <map>
#include <memory>
#include <stdexcept>
#include <algorithm>
#include <cmath>
#include <functional>

namespace ei
{
using S = std::wstring;
using Bytes = std::vector<BYTE>;
struct Less
{
    bool operator()(const S &a, const S &b) const
    {
        return _wcsicmp(a.c_str(), b.c_str()) < 0;
    }
};
template <class T> using Map = std::map<S, T, Less>;
extern S root;
extern int language;
S tr(const S &key);
S text(const wchar_t *en, const wchar_t *ru);
S full(const S &path);
S join(const S &a, const S &b);
bool exists(const S &path);
bool directory(const S &path);
[[noreturn]] void fail(const S &message);
void check(LONG error, const S &message);
Bytes readFile(const S &path);
void atomicFile(const S &path, const Bytes &data);
S decode(const Bytes &bytes, UINT fallback = 1251);
Bytes utf8(const S &s);
S trim(S s);
S lower(S s);
S executable();
S setting(const S &key, const S &def = L"");
void settings(const Map<S> &changes);
S number(double v);
double numeric(S s);
struct Value
{
    S section, name;
    DWORD kind = REG_NONE;
    Bytes bytes;
    bool present = false;
    S id() const
    {
        return section + L"\n" + name;
    }
    bool operator==(const Value &b) const
    {
        return present == b.present && (!present || (kind == b.kind && bytes == b.bytes));
    }
};
Value readSetting(const S &section, const S &name);
Value strValue(const S &section, const S &name, const S &value);
Value intValue(const S &section, const S &name, DWORD value);
Value floatValue(const S &section, const S &name, float value);
S valueText(const Value &v);
using Values = Map<Value>;
void refreshProfile();
void commitProfile(const Values &before, const Values &changes);
Values loadProfile(const S &path);
Values loadProfileBytes(const Bytes &bytes, const S &context);
Values loadDefaultProfile();
void saveProfile(const S &path, const Values &values);
struct Profile
{
    void open();
};
struct Mod
{
    S title, path, config, version, author, email, site, date, plugin, pluginText;
    bool single = true, multi = true;
};
std::vector<Mod> mods();
void selectMod(const Mod &mod);
bool gameRunning(const S &exe);
std::vector<S> resolutions();
struct Game
{
    void start(const Mod &mod);
};
struct Option
{
    const wchar_t *key;
    int category, type;
    double initial, minimum, maximum, step;
    std::vector<double> presets;
    std::vector<S> choices;
    const wchar_t *english, *russian;
    std::vector<S> russianChoices;
    const wchar_t *textDefault = L"";
};
const std::vector<Option> &schema();
struct Ini
{
    S path;
    Bytes original;
    bool had = false, utf16 = false, renderer = false;
    std::string content, newline = "\r\n";
    explicit Ini(S p);
    S get(const S &section, const S &key, const S &def = L"") const;
    void set(const S &section, const S &key, const S &value);
    S getAny(const S &key, const S &def) const;
    void setAny(const S &key, const S &value);
    void validate() const;
    void save(const std::function<void()> &other);
};
} // namespace ei
