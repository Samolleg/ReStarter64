# RS64CPP01: карта C# ReStarter64 → C++ x64

## Статус передачи

Кандидат НЕ ГОТОВ к приёмке: единственная разрешённая сборка закончилась успешно,
но её предупреждение C4456 выявило ошибочную привязку `else` в `buildSettings`.
В исходнике добавлены скобки, исправлена работа ещё не созданных полей пресетов,
дополнены проверки HUD и сохранение языка. Эти изменения не пересобирались.
Первый EXE/PDB оставлен только в `tasks/RS64CPP01/tmp/first-build`, вне `kit`.
Нужна разрешённая повторная сборка актуального исходника; до неё комплект отсутствует.
В таблицах «перенесено» означает реализацию в исходнике, а не подтверждение запуском.

## Исходные точки входа

Исследована ветка `RESTARTER64`: `Misc/LauncherProfile.cs`, `FirstRunImport.cs`,
`RebuildLaunch.cs`, `Program.cs`, `LauncherTheme.cs`, `NvidiaAdapter.cs`, `UiText.cs`,
`Forms/MainForm.cs`, `MainForm.Appearance.cs`, `ModInfo*`, `Options*`,
`ReStarter64Information.cs`, `REG_INI_IO/SettingsProfile.cs`,
`OwnedRendererSchema64.cs`, `OwnedRendererSettings.cs`, `INIfile.cs`.
Скрытые старые WinForms-контролы не являются дополнительными экранами x64.
Схема исходного x86 `OwnedRendererSchema.cs` для нового EXE не используется.

## Экраны и действия

| C# экран / действие | C++ | Статус |
|---|---|---|
| Главный экран: фон, заголовок, выбранный мод и версия, Играть, навигация | `app.cpp` | Перенесено; GDI/GDI+ вместо WinForms, системный DPI |
| Выбор мода: строки, версия, поиск при >5 модов, прокрутка, клавиатура | `mod-picker.inl` | Перенесено из существующего native |
| Карточка мода: автор, версия, дата, сайт, email, single/multi, readme/changelog, папка, plugin | `card.inl` | Перенесено, действие plugin через системный cmd.exe /C |
| Графитовая / светлая тема, сохранение UiTheme | `app.cpp`, owner draw | Перенесено; нативный заголовок окна |
| Язык стартера: каталоги lang; встроенные en/ru/de/fr/pl/ko/zh, неизвестный → en | `app.cpp`, `storage.cpp::tr` | Перенесено; язык игры настраивается отдельно |
| Настройки: Графика, Сеть, Камера, Пути; Применить/Отмена/По умолчанию | `settings.inl` | Перенесено; исходник раскладки исправлен после сборки |
| F10: язык игры, пресеты Low/Medium/High/Ultra/Custom; Auto только до завершения первого подбора | `settings.inl`, сгенерированная `schema.cpp` | Перенесено; сохранение first_run_done при ручном пресете/изменении его параметров |
| Все 13 разделов F10, числовые поля, шаг +/−, выборы, неизвестные значения | `Generate-Schema.ps1`, `settings.inl` | Перенесено; полный перечень ниже |
| Доступность DLSS и зависимых параметров | `app.cpp::detectNvidia`, `settings.inl::availability` | Перенесено: однократный фоновый DXGI, VendorId=0x10DE; NGX проверяет игра |
| HUD: активный профиль, параметры видимости/масштабов/позиции/выравнивания, боевые эффекты и ID | `settings.inl`, `Ini::getAny/setAny` | Перенесено: наложение активного профиля поверх основного INI, запись обоих |
| Отложенное создание закрытых групп рендера и страницы камеры | `rowFields` | Перенесено в исходнике; нагрузка и раскладка не измерялись |
| Информация: обзор, лицензия NVIDIA DLSS, THIRD_PARTY, Microsoft MIT | `information.inl`, ресурсы 110–112 | Перенесено, все тексты встроены; lang/readme не открывается |
| Сайт | ShellExecuteW | Перенесено: https://vk.ru/ei_dejavu |
| Играть | `Game::start` | Перенесено: только EIRebuild64, собственный profile и выбранный mod |

Перетаскивание HUD, создание/переименование/удаление профилей, живая память и каталог
эффектов остаются функциями F10: исходный C# тоже не предоставляет этих действий.

## Файлы и форматы

| Файл | Чтение / запись и контракт |
|---|---|
| `restarter64.config` | [Settings]: ExecutablePath, ModSel, language, UiTheme; сохраняются Theme, ModSkins, UsingINIconfigs. Win32 ANSI (текущая ACP) либо существующий UTF-16LE BOM; неизвестные строки не удаляются |
| `restarter64-settings.xml` | XML `<profile version="1"><value section="…" name="…" kind="…">…`; строки, signed DWORD/QWORD, binary/base64 и multi-string. Нет импорта x86/реестра; новый профиль из собственных defaults |
| `restarter64-defaults.xml` | Читается при создании профиля и сбросе XML-параметров; создаётся из встроенного `Misc/restarter-defaults.xml`, когда отсутствует |
| `Config64/rddraw.ini` | [graphics] — все ключи схемы; [setup] first_run_done. Побайтовое сохранение посторонних строк, отступов, BOM и CR/LF. Renderer читает/пишет однобайтные Latin-1 значения; interface_profile декодируется из UTF-8 |
| `Config64/rddraw_profiles/<имя>.ini` | Ключи категории 11 и категории 8 кроме confine_cursor/highlight_*. Поиск по всем секциям, обычная секция добавления [interface]; отсутствующий профиль готовится из главного INI и defaults в памяти, записывается при изменении HUD |
| `MODS/*/config.reg` | Только чтение: бинарный 0x45AB3EFB, секции MOD/Starter, метаданные мода, Windows-1251 |
| `MODS/*/mod.config` | Только чтение вместо config.reg при UsingINIconfigs=True; MOD/Starter |
| `background.jpg` | Только чтение: пользовательский фон; в EXE также встроен штатный фон, внешний файл не нужен для автономного комплекта |
| `lang/*` | Каталоги для переключения языка; тексты UI встроены из UiText.tsv |
| readme/changelog/plugin выбранного мода | Карточка открывает файлы стандартным обработчиком; игра при подготовке задания не запускается |

XML обновляется через MSXML6 с сохранением посторонних узлов и метаданных.
Реестровые API удалены из native: `RegistryValueKind` остался лишь числовым форматом XML.
Перед сохранением XML сверяются прочитанные значения; перед заменой INI — исходные байты.
Запись XML и двух INI связана с откатом при ошибке. В отличие от C#,
native использует ReplaceFile без постоянной резервной копии; точная сериализация XML
MSXML может отличаться пробелами от XDocument при том же формате и значениях.

Имя профиля HUD: UTF-8 не более 48 байт, без начальной точки, крайних пробелов,
управляющих символов и `\/:*?"<>|`; недопустимое имя → default.
Старое dlss_mode 1–10 даёт upscaler 1/2 и quality=(mode−1)%5 только при отсутствии новых ключей.
Неизменённые неизвестные значения не нормализуются. Изменённые числа проверяются
по min/max/type, размеры окна — оба 0 либо оба ненулевые.

## XML-настройки вне F10

- Сеть: Settings / MP console, MQ client, MP Speed-control (DWORD, неизвестные toggle сохраняются).
- Камера: EvilIslands\camera settings / CameraFlipXAxis/YAxis (DWORD),
  FarClipDistance, NearClipDistance (binary float), CameraTerrainSensetiveArea,
  CameraBorderScrollArea, CameraFrameSelectionSensetiveArea; группы Min/Max/Default
  для Height и DistanceToCarrier, Min/Max для LimitDistanceToCarrier,
  DefaultPitch и Briefing LimitDistanceToCarrier/DistanceToCarrier/Pitch;
  CameraDefault{Pitch,Rotate,Scroll,Zoom}{Speed,PowerMouse,PowerKeyboard};
  CameraDefaultXDeploy/YDeploy; CameraShortcut 0–3 {Distance,AxisX,AxisY,AxisZ,Angle,Pitch}.
- Пути: EvilIslands\Path Settings / CD-ROM Path, Resources Path, Work Path (строки);
  отдельное применение путей, проверка каталога, выбор папки. Имя EXE показывается read-only.
- Устаревшие videoresolution/audio остаются в XML, но скрыты и не записываются.
- Сброс использует defaults для камеры/сети, не меняет пути и рендер.

Выбор мода записывает корневые Path=<root>\, AddonPath=<mod>\addon.dll
и Settings/AddonPath=<mod>\.

## Запуск

ExecutablePath по умолчанию `Engine64\EIRebuild64.exe`; имя файла обязано быть
EIRebuild64.exe, каталог `<root>/Engine` запрещён. Запускается CreateProcessW,
без shell, с рабочим каталогом выбранного EXE, унаследованным окружением,
`-profile "<абсолютный root>/restarter64-settings.xml" --mod "<абсолютный mod>"`.
Требуются EXE, валидный XML и config.reg выбранного мода. После успеха стартер закрывается.
Нет WINEDEBUG, временного реестра, совместимости game.exe и запасного x86-пути.
Мьютекс ReStarter64Sharp. Запись настроек запрещена при работающем EIRebuild64.exe.

## Отличия и оставшиеся вопросы

Нативная компоновка сохраняет разделы, подписи и двухколоночную сетку,
но Win32-шрифты, размеры системных элементов и перенос строк отличаются от WinForms.
Добавлены стандартные списки render_scale/frame_limit рядом с полями и +/−,
язык вынесен над пресетом. Ошибка чтения/разбора INI отключает только рендер,
не мешая камере, сети и путям; ошибка внешнего фона игнорируется.
Действие plugin карточки запускается через системный cmd.exe /C, как в C#.
Эти доработки, как и исправление раскладки, внесены после первой сборки.
Визуальное совпадение, ошибки файлов/конфликты, повторное открытие настроек,
реальный запуск EIRebuild64, Wine/Proton не проверены; тесты и игровые прогоны запрещены BRIEF.

## Перечень F10

Генератор сборки переносит каждую запись непосредственно из OwnedRendererSchema64.cs
и прекращает сборку, если число разобранных записей не совпадает с исходником.
Тип, default, границы, шаг, четыре значения пресета, en/ru подписи и варианты,
а также текстовые defaults входят в C++ без ручного дублирования.
Число записей: 218. Все строки ниже перенесены в исходник; runtime не подтверждён.

| Ключ | Раздел | Тип | По умолчанию | Min…Max / шаг | Пресеты |
|---|---:|---|---|---|---|
| `ui_language` | 10 | Integer | `0` | 0…2 / 1 | — |
| `display_mode` | 0 | Integer | `0` | 0…1 / 1 | — |
| `window_width` | 0 | Integer | `0` | 0…16384 / 1 | — |
| `window_height` | 0 | Integer | `0` | 0…16384 / 1 | — |
| `upscaler` | 0 | Integer | `0` | 0…2 / 1 | — |
| `upscaler_quality` | 0 | Integer | `0` | 0…4 / 1 | — |
| `upscale_sharpness` | 0 | Float | `0.2` | 0…1 / 0.05 | — |
| `render_scale` | 0 | Integer | `100` | 50…100 / 1 | { 67, 85, 100, 100 } |
| `smaa` | 0 | Boolean | `1` | 0…1 / 1 | { 0, 1, 1, 1 } |
| `anisotropy` | 0 | Integer | `3` | 0…4 / 1 | { 2, 3, 3, 4 } |
| `vsync` | 0 | Boolean | `0` | 0…1 / 1 | — |
| `frame_limit` | 0 | Integer | `0` | 0…500 / 1 | — |
| `background_mode` | 0 | Integer | `0` | 0…1 / 1 | — |
| `background_frame_limit` | 0 | Integer | `30` | 0…240 / 1 | — |
| `hero_light` | 1 | Boolean | `0` | 0…1 / 1 | — |
| `shadows` | 1 | Boolean | `1` | 0…1 / 1 | { 1, 1, 1, 1 } |
| `shadow_cascade_quality` | 1 | Integer | `1` | 0…2 / 1 | { 0, 1, 1, 2 } |
| `shadow_close_cascades` | 1 | Integer | `3` | 1…3 / 1 | { 1, 3, 3, 3 } |
| `shadow_far` | 1 | Boolean | `1` | 0…1 / 1 | { 0, 1, 1, 1 } |
| `shadow_neighbour_casters` | 1 | Integer | `2` | 0…2 / 1 | { 1, 1, 2, 2 } |
| `shadow_softness` | 1 | Integer | `1` | 0…2 / 1 | { 0, 1, 1, 2 } |
| `point_shadows` | 1 | Boolean | `1` | 0…1 / 1 | { 0, 1, 1, 1 } |
| `point_shadow_quality` | 1 | Integer | `2` | 0…2 / 1 | { 0, 1, 2, 2 } |
| `ao_quality` | 1 | Integer | `2` | 0…2 / 1 | { 0, 1, 2, 2 } |
| `emissive_lights` | 1 | Integer | `1` | 0…2 / 1 | { 0, 1, 1, 2 } |
| `cave_brightness` | 1 | Float | `0.7` | 0.25…1.25 / 0.05 | — |
| `terrain_quality` | 2 | Integer | `1` | 0…1 / 1 | { 0, 0, 1, 1 } |
| `terrain_blend` | 2 | Integer | `1` | 0…2 / 1 | { 0, 1, 1, 2 } |
| `terrain_cliffs` | 2 | Boolean | `1` | 0…1 / 1 | { 0, 1, 1, 1 } |
| `contact_blend` | 2 | Float | `0.7` | 0…1 / 0.05 | { 0, 0.7, 0.7, 0.7 } |
| `terrain_tessellation` | 2 | Integer | `1` | 0…1 / 1 | { 0, 0, 1, 1 } |
| `terrain_lod` | 2 | Boolean | `0` | 0…1 / 1 | { 1, 1, 0, 0 } |
| `water_simple` | 3 | Boolean | `0` | 0…1 / 1 | — |
| `water_reflections` | 3 | Boolean | `1` | 0…1 / 1 | { 0, 0, 1, 1 } |
| `water_reflection_quality` | 3 | Integer | `2` | 0…2 / 1 | { 0, 1, 2, 2 } |
| `water_physical` | 3 | Boolean | `1` | 0…1 / 1 | { 0, 0, 1, 1 } |
| `water_surf_quality` | 3 | Integer | `2` | 0…2 / 1 | { 0, 1, 2, 2 } |
| `ground_cover` | 4 | Integer | `2` | 0…3 / 1 | { 1, 1, 2, 3 } |
| `ground_cover_distance` | 4 | Integer | `200` | 50…1000 / 25 | { 75, 100, 200, 300 } |
| `ground_cover_shadows` | 4 | Integer | `1` | 0…2 / 1 | — |
| `tree_sway` | 4 | Boolean | `1` | 0…1 / 1 | — |
| `sky_clouds` | 5 | Integer | `2` | 0…2 / 1 | { 0, 1, 2, 2 } |
| `fog_preset` | 5 | Integer | `2` | 0…2 / 1 | { 0, 1, 2, 2 } |
| `fog_volume_quality` | 5 | Integer | `1` | 0…1 / 1 | { 0, 0, 1, 1 } |
| `sun_shafts` | 5 | Integer | `2` | 0…3 / 1 | { 0, 1, 2, 3 } |
| `fog_strength` | 5 | Float | `1` | 0…3 / 0.05 | — |
| `weather_wind_variation` | 5 | Boolean | `1` | 0…1 / 1 | — |
| `bloom` | 6 | Integer | `2` | 0…2 / 1 | { 0, 1, 2, 2 } |
| `depth_of_field` | 6 | Integer | `0` | 0…2 / 1 | — |
| `depth_of_field_strength` | 6 | Float | `0.2` | 0…3 / 0.05 | — |
| `ambient_particles` | 6 | Integer | `1` | 0…2 / 1 | { 0, 1, 1, 2 } |
| `wind_trails` | 6 | Integer | `1` | 0…2 / 1 | { 0, 1, 1, 2 } |
| `precipitation` | 6 | Integer | `3` | 0…4 / 1 | { 1, 2, 3, 4 } |
| `precip_strength` | 6 | Integer | `0` | 0…3 / 1 | — |
| `effect_distance` | 6 | Integer | `300` | 100…1000 / 25 | { 100, 200, 300, 500 } |
| `neighbour_maps` | 7 | Boolean | `1` | 0…1 / 1 | { 0, 1, 1, 1 } |
| `draw_distance` | 7 | Integer | `300` | 100…1000 / 25 | { 100, 200, 300, 500 } |
| `neighbour_fog_range` | 7 | Float | `1` | 0.5…4 / 0.25 | — |
| `critters` | 9 | Integer | `2` | 0…2 / 1 | { 1, 1, 2, 2 } |
| `critter_rats` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_rats_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `critter_spiders` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_spiders_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `critter_larvae` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_larvae_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `critter_frogs` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_frogs_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `critter_lizards` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_lizards_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `critter_snakes` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_snakes_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `critter_scolopendras` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_scolopendras_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `critter_dung_beetles` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_dung_beetles_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `critter_small_birds` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_small_birds_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `critter_corvids` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_corvids_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `critter_vultures` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_vultures_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `critter_crabs` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_crabs_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `critter_snow_birds` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_snow_birds_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `critter_snow_mice` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_snow_mice_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `critter_bats` | 9 | Boolean | `1` | 0…1 / 1 | — |
| `critter_bats_weight` | 9 | Integer | `100` | 0…1000 / 5 | — |
| `hud_fade_duration` | 8 | Integer | `250` | 0…10000 / 50 | — |
| `hud_fade_in_duration` | 8 | Integer | `250` | 0…2000 / 50 | — |
| `hud_fade_delay` | 8 | Integer | `2000` | 0…60000 / 100 | — |
| `spell_bar_auto_fade` | 8 | Boolean | `0` | 0…1 / 1 | — |
| `spell_bar_fade_transparency` | 8 | Integer | `80` | 0…100 / 5 | — |
| `ability_bar_auto_fade` | 8 | Boolean | `0` | 0…1 / 1 | — |
| `ability_bar_fade_transparency` | 8 | Integer | `80` | 0…100 / 5 | — |
| `minimap_auto_fade` | 8 | Boolean | `0` | 0…1 / 1 | — |
| `minimap_fade_transparency` | 8 | Integer | `80` | 0…100 / 5 | — |
| `character_info_auto_fade` | 8 | Boolean | `0` | 0…1 / 1 | — |
| `character_info_fade_transparency` | 8 | Integer | `80` | 0…100 / 5 | — |
| `portraits_auto_fade` | 8 | Boolean | `0` | 0…1 / 1 | — |
| `portraits_fade_transparency` | 8 | Integer | `80` | 0…100 / 5 | — |
| `weapon_switcher_auto_fade` | 8 | Boolean | `0` | 0…1 / 1 | — |
| `weapon_switcher_fade_transparency` | 8 | Integer | `80` | 0…100 / 5 | — |
| `quick_items_auto_fade` | 8 | Boolean | `0` | 0…1 / 1 | — |
| `quick_items_fade_transparency` | 8 | Integer | `80` | 0…100 / 5 | — |
| `message_panel_auto_fade` | 8 | Boolean | `0` | 0…1 / 1 | — |
| `message_panel_fade_transparency` | 8 | Integer | `80` | 0…100 / 5 | — |
| `stance_auto_fade` | 8 | Boolean | `0` | 0…1 / 1 | — |
| `stance_fade_transparency` | 8 | Integer | `80` | 0…100 / 5 | — |
| `game_speed_auto_fade` | 8 | Boolean | `0` | 0…1 / 1 | — |
| `game_speed_fade_transparency` | 8 | Integer | `80` | 0…100 / 5 | — |
| `stance_scale` | 8 | Float | `100` | 50…300 / 5 | — |
| `game_speed_scale` | 8 | Float | `100` | 50…300 / 5 | — |
| `ui_scale` | 8 | Integer | `100` | 50…120 / 5 | — |
| `combat_text_scale` | 8 | Integer | `100` | 50…300 / 5 | — |
| `spell_bar_direction` | 8 | Integer | `0` | 0…3 / 1 | — |
| `ability_bar_direction` | 8 | Integer | `0` | 0…3 / 1 | — |
| `portrait_direction` | 8 | Integer | `0` | 0…4 / 1 | — |
| `message_panel_text_scale` | 8 | Integer | `100` | 50…300 / 5 | — |
| `hud_width` | 8 | Integer | `2` | 0…2 / 1 | — |
| `pillarbox_fill` | 8 | Integer | `1` | 0…1 / 1 | — |
| `camp_ui_layout` | 8 | Integer | `1` | 0…1 / 1 | — |
| `cursor_scale` | 8 | Integer | `100` | 100…400 / 25 | — |
| `confine_cursor` | 8 | Boolean | `1` | 0…1 / 1 | — |
| `highlight_briefing` | 8 | Boolean | `0` | 0…1 / 1 | — |
| `highlight_selected` | 8 | Boolean | `0` | 0…1 / 1 | — |
| `highlight_hover_characters` | 8 | Boolean | `1` | 0…1 / 1 | — |
| `highlight_hover_objects` | 8 | Boolean | `1` | 0…1 / 1 | — |
| `quest_items_auto_fade` | 11 | Boolean | `0` | 0…1 / 1 | — |
| `quest_items_fade_transparency` | 11 | Integer | `80` | 0…100 / 5 | — |
| `hud_border_scale` | 11 | Integer | `100` | 0…300 / 5 | — |
| `hud_border_transparency` | 11 | Integer | `0` | 0…100 / 5 | — |
| `spell_bar_scale` | 11 | Float | `100` | 50…300 / 0.1 | — |
| `spell_bar_x` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `spell_bar_y` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `spell_bar_padding` | 11 | Integer | `0` | -100…100 / 1 | — |
| `spell_bar_preview` | 11 | Boolean | `0` | 0…1 / 1 | — |
| `spell_bar_preview_count` | 11 | Integer | `3` | 1…32 / 1 | — |
| `spell_bar_slots_per_column` | 11 | Integer | `0` | 0…32 / 1 | — |
| `ability_bar_scale` | 11 | Float | `100` | 50…300 / 0.1 | — |
| `ability_bar_x` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `ability_bar_y` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `ability_bar_padding` | 11 | Integer | `0` | -100…100 / 1 | — |
| `ability_bar_preview` | 11 | Boolean | `0` | 0…1 / 1 | — |
| `ability_bar_preview_count` | 11 | Integer | `3` | 1…32 / 1 | — |
| `ability_bar_slots_per_column` | 11 | Integer | `0` | 0…32 / 1 | — |
| `minimap_scale` | 11 | Float | `100` | 50…300 / 0.1 | — |
| `minimap_x` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `minimap_y` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `minimap_buttons_scale` | 11 | Integer | `100` | 50…200 / 5 | — |
| `minimap_border_padding` | 11 | Integer | `3` | -100…100 / 1 | — |
| `minimap_buttons_side` | 11 | Integer | `0` | 0…5 / 1 | — |
| `character_info_scale` | 11 | Float | `100` | 50…300 / 0.1 | — |
| `character_info_x` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `character_info_y` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `character_info_buttons_scale` | 11 | Integer | `100` | 50…200 / 5 | — |
| `character_info_border_padding` | 11 | Integer | `3` | -100…100 / 1 | — |
| `character_info_buttons_side` | 11 | Integer | `0` | 0…5 / 1 | — |
| `portraits_scale` | 11 | Float | `100` | 50…300 / 0.1 | — |
| `portraits_x` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `portraits_y` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `portraits_padding` | 11 | Integer | `0` | -100…100 / 1 | — |
| `portraits_preview` | 11 | Boolean | `0` | 0…1 / 1 | — |
| `portraits_preview_count` | 11 | Integer | `3` | 1…32 / 1 | — |
| `weapon_switcher_scale` | 11 | Float | `100` | 50…300 / 0.1 | — |
| `weapon_switcher_x` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `weapon_switcher_y` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `weapon_switcher_direction` | 11 | Integer | `2` | 0…3 / 1 | — |
| `weapon_switcher_padding` | 11 | Integer | `0` | -100…100 / 1 | — |
| `weapon_switcher_preview` | 11 | Boolean | `0` | 0…1 / 1 | — |
| `weapon_switcher_preview_count` | 11 | Integer | `4` | 1…4 / 1 | — |
| `weapon_switcher_slots_per_column` | 11 | Integer | `0` | 0…32 / 1 | — |
| `quick_items_scale` | 11 | Float | `100` | 50…300 / 0.1 | — |
| `quick_items_x` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `quick_items_y` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `quick_items_direction` | 11 | Integer | `3` | 0…3 / 1 | — |
| `quick_items_padding` | 11 | Integer | `0` | -100…100 / 1 | — |
| `quick_items_preview` | 11 | Boolean | `0` | 0…1 / 1 | — |
| `quick_items_preview_count` | 11 | Integer | `4` | 1…4 / 1 | — |
| `quick_items_slots_per_column` | 11 | Integer | `0` | 0…32 / 1 | — |
| `message_panel_width` | 11 | Float | `100` | 50…800 / 0.1 | — |
| `message_panel_height` | 11 | Float | `100` | 50…600 / 0.1 | — |
| `message_panel_x` | 11 | Float | `50` | 0…100 / 0.1 | — |
| `message_panel_y` | 11 | Float | `0` | 0…100 / 0.1 | — |
| `message_panel_buttons_scale` | 11 | Integer | `100` | 50…200 / 5 | — |
| `message_panel_border_padding` | 11 | Integer | `3` | -100…100 / 1 | — |
| `message_panel_buttons_side` | 11 | Integer | `0` | 0…5 / 1 | — |
| `quest_items_scale` | 11 | Float | `100` | 50…300 / 0.1 | — |
| `quest_items_x` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `quest_items_y` | 11 | Float | `-1` | -1…100 / 0.1 | — |
| `combat_portrait_effects` | 12 | Boolean | `1` | 0…1 / 1 | — |
| `combat_healthplates` | 12 | Boolean | `1` | 0…1 / 1 | — |
| `combat_plate_combat` | 12 | Boolean | `0` | 0…1 / 1 | — |
| `combat_plate_alt` | 12 | Boolean | `1` | 0…1 / 1 | — |
| `combat_plate_paused` | 12 | Boolean | `1` | 0…1 / 1 | — |
| `combat_plate_hover` | 12 | Boolean | `1` | 0…1 / 1 | — |
| `combat_plate_all` | 12 | Boolean | `0` | 0…1 / 1 | — |
| `combat_plate_idle_opacity` | 12 | Integer | `50` | 0…100 / 1 | — |
| `combat_plate_width` | 12 | Integer | `120` | 30…320 / 1 | — |
| `combat_plate_height` | 12 | Integer | `8` | 3…40 / 1 | — |
| `combat_plate_spacing` | 12 | Integer | `28` | 0…40 / 1 | — |
| `combat_plate_stacking` | 12 | Boolean | `1` | 0…1 / 1 | — |
| `combat_plate_stack_gap` | 12 | Integer | `4` | 0…80 / 1 | — |
| `combat_icon_scale` | 12 | Float | `1.15` | 0.5…2.5 / 0.05 | — |
| `combat_split_effects` | 12 | Boolean | `1` | 0…1 / 1 | — |
| `combat_effect_outlines` | 12 | Boolean | `1` | 0…1 / 1 | — |
| `combat_hp_values` | 12 | Boolean | `1` | 0…1 / 1 | — |
| `combat_hp_percent` | 12 | Boolean | `0` | 0…1 / 1 | — |
| `combat_hp_text_size` | 12 | Integer | `18` | 8…32 / 1 | — |
| `combat_max_effects` | 12 | Integer | `3` | 0…32 / 1 | — |
| `combat_durations` | 12 | Boolean | `1` | 0…1 / 1 | — |
| `combat_duration_size` | 12 | Integer | `10` | 8…32 / 1 | — |
| `combat_priority` | 12 | Text | `29,34,25,32,30,15,38` | 0…0 / 1 | — |
| `combat_effect_priorities` | 12 | Text | `9:99,10:99,11:99,15:102,25:102,26:101,29:102,30:102,31:101,33:101,34:101,38:102` | 0…0 / 1 | — |
| `combat_blacklist` | 12 | Text | `13,14,12` | 0…0 / 1 | — |
| `combat_buff_overrides` | 12 | Text | `` | 0…0 / 1 | — |
| `combat_debuff_overrides` | 12 | Text | `16,50` | 0…0 / 1 | — |
