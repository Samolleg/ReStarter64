// File-backed settings for ReStarter. The legacy namespace is read-only and
// used only before restarter64-settings.xml has been created.
static Values profileValues;
static S profilePath()
{
    return join(root, L"restarter64-settings.xml");
}
void refreshProfile()
{
    profileValues = loadProfile(profilePath());
}
Value readSetting(const S &section, const S &name)
{
    Value missing{section, name};
    auto found = profileValues.find(missing.id());
    return found == profileValues.end() ? missing : found->second;
}
static S logicalSection(S section)
{
    auto first = section.find_first_not_of(L'\\');
    return first == S::npos ? S{} : section.substr(first);
}
static Bytes serializeDocument(IXMLDOMDocument2 *document)
{
    Com<IStream> stream;
    check(CreateStreamOnHGlobal(nullptr, TRUE, &stream.p), L"Profile output stream");
    VARIANT destination{};
    destination.vt = VT_UNKNOWN;
    destination.punkVal = stream.p; // Borrowed for the synchronous save call.
    check(document->save(destination), L"Serialize settings profile");
    STATSTG stat{};
    check(stream->Stat(&stat, STATFLAG_NONAME), L"Profile output size");
    if (stat.cbSize.QuadPart > 16 * 1024 * 1024)
        fail(L"Settings profile is too large");
    LARGE_INTEGER start{};
    check(stream->Seek(start, STREAM_SEEK_SET, nullptr), L"Profile output position");
    Bytes bytes((size_t)stat.cbSize.QuadPart);
    ULONG count = 0;
    if (!bytes.empty())
        check(stream->Read(bytes.data(), (ULONG)bytes.size(), &count), L"Profile output data");
    if (count != bytes.size())
        fail(L"Incomplete settings profile output");
    return bytes;
}
static void setAttribute(IXMLDOMElement *element, const wchar_t *name, const S &value)
{
    Bstr content(value);
    VARIANT attributeValue{};
    attributeValue.vt = VT_BSTR;
    attributeValue.bstrVal = content.p;
    check(element->setAttribute(Bstr(name), attributeValue), L"Profile attribute");
}
static S encodedProfileValue(const Value &value)
{
    if (value.kind == REG_SZ || value.kind == REG_EXPAND_SZ)
        return valueText(value);
    if (value.kind == REG_DWORD && value.bytes.size() == 4)
    {
        int number;
        memcpy(&number, value.bytes.data(), sizeof(number));
        return std::to_wstring(number);
    }
    if (value.kind == REG_QWORD && value.bytes.size() == 8)
    {
        long long number;
        memcpy(&number, value.bytes.data(), sizeof(number));
        return std::to_wstring(number);
    }
    Bytes bytes = value.bytes;
    if (value.kind == REG_MULTI_SZ && bytes.size() >= 4)
        bytes.resize(bytes.size() - 4);
    return base64(bytes);
}
void commitProfile(const Values &before, const Values &changes)
{
    if (changes.empty())
        return;
    if (gameRunning(L"EIRebuild64.exe"))
        fail(text(L"Close EIRebuild before saving settings.",
                  L"Закройте EIRebuild перед сохранением настроек."));
    const S path = profilePath();
    const Bytes original = readFile(path);
    const Values current = loadProfileBytes(original, path);
    for (const auto &entry : before)
    {
        const auto found = current.find(entry.first);
        const Value now =
            found == current.end() ? Value{entry.second.section, entry.second.name} : found->second;
        if (!(now == entry.second))
            fail(tr(L"SettingsChanged"));
    }
    Com<IXMLDOMDocument2> document;
    loadProfileDocument(original, path, document);
    Com<IXMLDOMElement> top;
    check(document->get_documentElement(&top.p), L"Profile root");
    for (const auto &change : changes)
    {
        const auto &value = change.second;
        Com<IXMLDOMNodeList> nodes;
        check(document->selectNodes(Bstr(L"/profile/value"), &nodes.p), L"Profile values");
        long count = 0;
        check(nodes->get_length(&count), L"Profile value count");
        Com<IXMLDOMNode> node;
        for (long index = 0; index < count; ++index)
        {
            Com<IXMLDOMNode> candidate;
            check(nodes->get_item(index, &candidate.p), L"Profile value");
            Value key{logicalSection(attribute(candidate.p, L"section")), attribute(candidate.p, L"name")};
            if (_wcsicmp(key.id().c_str(), value.id().c_str()) == 0)
            {
                node.p = candidate.p;
                node.p->AddRef();
                break;
            }
        }
        if (!value.present)
        {
            if (node.p)
            {
                Com<IXMLDOMNode> removed;
                check(top->removeChild(node.p, &removed.p), L"Remove profile value");
            }
            continue;
        }
        Com<IXMLDOMElement> element;
        if (node.p)
            check(node->QueryInterface(__uuidof(IXMLDOMElement), (void **)&element.p), L"Profile element");
        else
        {
            check(document->createElement(Bstr(L"value"), &element.p), L"New profile value");
            setAttribute(element.p, L"section", value.section);
            setAttribute(element.p, L"name", value.name);
            Com<IXMLDOMNode> added;
            check(top->appendChild(element.p, &added.p), L"Append profile value");
        }
        setAttribute(element.p, L"kind", std::to_wstring(value.kind == REG_NONE ? -1 : (int)value.kind));
        check(element->put_text(Bstr(encodedProfileValue(value))), L"Profile value data");
    }
    Bytes next = serializeDocument(document.p);
    Values nextValues = loadProfileBytes(next, path);
    if (readFile(path) != original)
        fail(tr(L"SettingsChanged"));
    atomicFile(path, next);
    profileValues = std::move(nextValues);
}
void selectMod(const Mod &mod)
{
    if (mod.path.empty())
        fail(tr(L"NoMods"));
    refreshProfile();
    Values before, changes;
    for (const auto &value :
         {strValue(L"", L"Path", root + L"\\"), strValue(L"", L"AddonPath", join(mod.path, L"addon.dll")),
          strValue(L"Settings", L"AddonPath", mod.path + L"\\")})
    {
        auto previous = readSetting(value.section, value.name);
        if (!(previous == value))
        {
            before[value.id()] = previous;
            changes[value.id()] = value;
        }
    }
    commitProfile(before, changes);
}
void Profile::open()
{
    if (!exists(profilePath()))
    {
        const S defaults = join(root, L"restarter64-defaults.xml");
        if (!exists(defaults)) atomicFile(defaults, embeddedDefaultProfile());
        Ini config(join(root, L"restarter64.config"));
        if (config.get(L"Settings", L"ExecutablePath").empty())
        {
            config.set(L"Settings", L"ExecutablePath", L"Engine64\\EIRebuild64.exe");
            config.save([] {});
        }
        auto bytes = readFile(defaults);
        loadProfileBytes(bytes, defaults);
        atomicFile(profilePath(), bytes);
    }
    refreshProfile();
}
