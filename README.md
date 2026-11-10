# ReStarter64 на C++ x64

Кандидат RS64CPP01 пока не готов к приёмке: после единственной разрешённой сборки
исправлен исходник раскладки. Актуальный исходник не пересобран; готового `kit` нет.
Полная карта C# → C++, форматы, отличия и оставшиеся работы:
[docs/native64-migration.md](docs/native64-migration.md).

## Сборка

Нужны Visual Studio 2022 Build Tools, MSVC v143 x64 и Windows SDK 10.0.22621.0.
Зависимости не скачиваются. Из корня репозитория:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build-native.ps1 -OutputPath <папка>
```

Результат команды — `ReStarter64.exe` и `ReStarter64.pdb`. C++17, `/MT`, `/O2`,
`/Zi`, `/MACHINE:X64`, полный PDB. Схема F10 генерируется из сохранённого C#-источника.
Иконка, штатный фон, локализация, defaults и лицензии встроены в EXE.
Внешний пользовательский `background.jpg` имеет приоритет.

## Комплект и размещение

Целевой комплект — один нативный `ReStarter64.exe` + PDB (и SHA256SUMS задания).
PDB остаётся в builds. .NET, System.*.dll, exe.config, VC++ Redistributable
и дополнительные пользовательские DLL не нужны. EXE использует системные Win32,
GDI+, DXGI, COM/MSXML6 и Crypt32. Проверка импортов первой сборки показала только
системные DLL; MSXML6 создаётся через COM, поэтому не отображается в таблице импортов.

Стартер размещается в корне игры; обычный путь движка — `Engine64/EIRebuild64.exe`.
Собственные файлы: `restarter64.config`, `restarter64-settings.xml`,
`restarter64-defaults.xml`, `Config64/rddraw.ini` и активный профиль HUD.
Переноса старых x86-настроек и обращения к реестру нет. Запуск передаёт
`-profile "<абсолютный XML>" --mod "<абсолютный каталог мода>"`.
Форматы и сохранение посторонних значений описаны в карте миграции.

C#-версия остаётся до приёмки и в нативный комплект не включается.
Её сборка: `powershell -File build.ps1 -Configuration Release64`, .NET Framework 4.7.2.
Историческая документация C# x64: [docs/ReStarter64.md](docs/ReStarter64.md).

Wine/Proton, визуальная приёмка и игровой запуск не подтверждены.
Установка, merge/push/PR и запуск игры не выполнялись.
