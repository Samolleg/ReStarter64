# ReStarter64 (X64P25)

Сборка общего проекта: powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -Configuration Release64.
Результат: EIStarter/bin/Release64/ReStarter64.exe и соседние зависимости.
Профиль Release64 задаёт PlatformTarget=x64 и RESTARTER64; обычный Release остаётся прежним.

Размещение: ReStarter64.exe с зависимостями в корне отдельного комплекта/игры,
EIRebuild64.exe и его зависимости — в Engine64. Данные Base и MODS находятся в корне.
Путь можно изменить в restarter64.config, секция [Settings]:
ExecutablePath=Engine64\EIRebuild64.exe

Относительный путь разрешается от папки ReStarter64. Имя EXE должно оставаться
EIRebuild64.exe; каталог Engine этого корня запрещён, чтобы не писать x86 rddraw.ini.
Не размещайте x64 EXE рядом с x86 EXE в другом общем каталоге: рендер использует
rddraw.ini и rddraw_profiles рядом с EXE.

Файлы стартера: restarter64.config, restarter64-settings.xml, restarter64-defaults.xml.
Первый запуск создаёт профиль из встроенных defaults, без импорта x86/EIStarter
и без доступа к реестру. XML kind сохраняет прежний формат, но не означает работу
с реестром. Сброс настроек читает только restarter64-defaults.xml.
Mutex и LocalAppData-каталог также отдельные: ReStarter64Sharp и ReStarter64.
Общие ресурсы оформления, языки, Base и описания модов остаются читаемыми.

Запуск: выбранный EXE, рабочий каталог — его папка, аргументы:
-profile "<корень>\restarter64-settings.xml" --mod "<абсолютная папка мода>".
Профиль должен существовать и успешно читаться; мод должен содержать config.reg.
Выбранный мод также записывается в Settings/AddonPath профиля.
Имя addon.dll в корневом значении AddonPath — только формат пути: DLL не запускается.
Настройки графики редактируют rddraw.ini рядом с выбранным EIRebuild64.exe.

## Ограничение текущего EIRebuild64

На базе 1dbe772 P24: Saves64 реализован, но Config64/Logs64 ещё не реализованы
полностью. Журнал идёт в Engine64/Logs; GameMain пишет Base/config/settings.ini.
Без -profile движок всё ещё выбирает x86 restarter-settings.xml, а при отсутствующем
профиле может читать x86 defaults. Стартер передаёт существующий собственный профиль,
но сам по себе не исправляет общую запись GameMain.
До отдельного исправления путей движка не запускать этот комплект рядом с рабочим x86.
Исходники EIRebuild64 в P25 не меняются.

После исправления движка владелец проверяет первый запуск, выбор мода, сохранение
и сброс опций, изменение ExecutablePath, F10 и повторное открытие графических опций,
новую игру/save/load с Saves64 и неизменность файлов x86. Тесты и приложения при
подготовке P25 не запускаются.
