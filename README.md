# ReStarter64

Нативный лаунчер EIRebuild64 для Windows x64. Запускает движок с выбранным модом. Настройки рендера, локализация, фон, иконка и лицензии входят в EXE.

Из проектного PowerShell 7.6.10 запустите `./build.ps1 -OutputPath <каталог>`. Сборка использует Visual Studio Build Tools 2022, MSVC v143 и Windows SDK 10.0.22621.0. Результат — ReStarter64.exe и ReStarter64.pdb.

Исходники интерфейса и работы с профилями лежат в native, схема настроек F10 — в native/schema.cpp, встраиваемые файлы — в native/resources.
