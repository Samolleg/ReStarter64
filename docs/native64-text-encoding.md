# Источники текста ReStarter64

Сверено с сохранёнными исходниками C#; правила записи файлов не менялись.

| Источник | Поведение C# и C++ | Вывод / изменение |
|---|---|---|
| MODS/*/config.reg, имена разделов и ключей | Windows-1251 (`EIRegFile.ReadSection/ReadValue`) | Имена оставлены CP1251. |
| config.reg, строковые значения Title, Version, Author, AuthorEmail, URL, date_DMY, pluginpath, plugintext | `EIRegFile.DecodeString`: удалить UTF-8 BOM, строгий UTF-8, при ошибке CP1251 | Исправлено обязательное CP1251 в C++; название, автор и прочие поля теперь декодируются как в C#. REG не записывается. |
| mod.config и restarter64.config | `IniFile`: Unicode GetPrivateProfileStringW; UTF-16LE с BOM, иначе системная ANSI-кодировка | C++ хранит исходные байты, читает CP_ACP либо UTF-16LE; заменяет только значения, сохраняет кодировку и окончания строк. UTF-8 без BOM здесь не определяется C# автоматически. |
| Localization/UiText.tsv (ресурс 102) | UTF-8, в C# StreamReader с определением BOM | Встроена та же таблица, все восемь столбцов: ключ и en/ru/de/fr/pl/ko/zh. C++ читает UTF-8, поддерживает UTF-8/UTF-16LE BOM, заменяет `<rn>`. |
| lang/* | Каталоги определяют список переключения; при отсутствии — en | Одинаковое перечисление каталогов. Рабочая C# Localise использует UiText; lang.ini остался только в ветке экспорта переводов. C++ также использует встроенную таблицу. Неизвестный код выбирает английские строки. |
| restarter64-settings.xml, restarter64-defaults.xml | C# XML reader, декларация кодировки/BOM; штатные профили UTF-8 | C++ MSXML получает Unicode после decode (UTF-8, UTF-16LE BOM, CP1251 fallback); существующий XML редактирует через DOM, сериализация сохраняет декларацию кодировки. Формат записи не менялся. |
| Config64/rddraw.ini, rddraw_profiles/*.ini | OwnedRendererSettings: ISO-8859-1 как обратимое отображение байтов; interface_profile отдельно UTF-8 | C++ использует 28591; interface_profile читает UTF-8; остальные байты сохраняет. |
| «Информация», лицензии и notices | C# встроенные UTF-8 ресурсы и Unicode строки | C++ resourceText/decode; Unicode литералы, компилятор /utf-8. |
| «О моде», readme/changelog, пути | Метаданные из REG/INI; документы открываются внешней программой | C++ рисует Unicode метаданные; ShellExecuteW передаёт путь внешней программе, содержимое readme/changelog не декодирует. |

Окна, заголовки, сообщения об ошибках, ввод и рисование используют W-функции
Win32 (CreateWindowExW, Set/GetWindowTextW, MessageBoxW, DrawTextW,
SendMessageW); файловые пути — CreateFileW/FindFirstFileW/ShellExecuteW.
Настройки рендера и встроенные авторские пояснения имеют русскую и английскую
версии, как в C#; общие подписи берутся из общей таблицы семи языков.
