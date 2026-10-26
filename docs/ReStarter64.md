# ReStarter64

Сборка: `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -Configuration Release64`.
Результат: `EIStarter/bin/Release64/ReStarter64.exe`, его config и пять System.* DLL.
`RESTARTER64` и PlatformTarget=x64 задаются только в Release64. x86-профиль и UI сохранены.

Размещение: ReStarter64 с зависимостями в корне игры, отдельный движок
`Engine64/EIRebuild64.exe`. Путь можно изменить в `restarter64.config`,
`[Settings] ExecutablePath=Engine64\EIRebuild64.exe`; имя EXE фиксировано,
каталог Engine запрещён. Данные Base/MODS и пользовательский background.jpg общие,
в комплект обновления стартера они не входят.

Настройки: `restarter64.config`, `restarter64-settings.xml`,
`restarter64-defaults.xml`; новый профиль создаётся из встроенных defaults,
без импорта x86 и без обращения к реестру. Запуск передаёт собственный
`-profile` и `--mod`. Сохранения и журналы принадлежат EIRebuild64.

Параметры рендера читаются из `Config64/rddraw.ini`.
Схема X64RS01 соответствует F10 EIRebuild64 `fb5a071`:
`graphics_settings.cpp`, `renderer_device.cpp`, `renderer_ui_ru.h`,
`critter_summary.h`, `combat_ui.cpp` и редактор HUD в `debug_overlay.cpp`.
Старые bootstrap-разрешение и режим аудиодрайвера скрыты; их XML-значения не записываются.
Режим окна и размер задаёт рендер. Камера, пути и прочие прежние разделы сохранены.

F10 применяет активный `Config64/rddraw_profiles/<имя>.ini` поверх главного INI.
Стартер читает его значения групп Interface/HUD layout и сохраняет изменения
в оба файла с прежними byte guards и rollback при ошибке сохранения XML.
Неизвестные ключи, байты, отступы, окончания строк и невыбранные значения сохраняются.
Управление профилями, перетаскивание панелей, текущая память и каталог эффектов
остаются действиями F10; статические параметры HUD и списки ID доступны в стартере.

DXGI запускается один раз в фоновом потоке при создании главного окна.
До результата DLSS неактивен; отсутствие NVIDIA и ошибка DXGI оставляют его
неактивным, сохраняя ранее выбранное значение. Возможности NGX проверяет игра.
Закрытые группы рендера и камера строятся только при открытии.

«Информация» встроена в EXE: сведения о ReStarter64/EIRebuild64, текстовая
атрибуция NVIDIA, полный NVIDIA-DLSS-LICENSE.txt, THIRD_PARTY.md и Microsoft MIT.
Обращения к прежнему `lang/<язык>/readme.txt` в ReStarter64 нет.
Логотипы и изображения NVIDIA Marks не добавляются.

X64RS01: одна обычная Release64-сборка; без тестов, стендов, headless,
запуска стартера/игры и установки. Владельцу остаются визуальная приёмка,
скорость открытия и сохранение/повторное открытие параметров с проверкой F10.
