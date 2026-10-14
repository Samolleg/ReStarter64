# ReStarter

Самостоятельный локальный стартер EIRebuild на основе Evilauncher `5572879`.
Проект импортирован отдельным первым коммитом; прежний Evilauncher не меняется.
Сохраняются оформление, локализация, выбор мода и форма настроек рендера.

ReStarter запускает только `Engine\EIRebuild.exe` со встроенным рендером,
передаёт абсолютный `-profile "…\restarter-settings.xml"` и рабочий каталог
`Engine`. Проверок по хешам совместимых компонентов нет.

Настройки: `restarter.config`, `restarter-settings.xml`,
`restarter-defaults.xml`, `Engine\rddraw.ini`. Мьютекс `ReStarterSharp`,
служебная папка `%LOCALAPPDATA%\ReStarter`. Реестр для обычной работы не нужен.
При отсутствии нового XML-профиля один раз импортируется `portable-settings.xml`,
либо старый `EI_Starter` через строго read-only код. Настройки стартера
импортируются из `starter.config` без `ExeName`, рендер — из `eid3d11.ini`.
Существующие собственные config/defaults/INI сохраняются. Старые источники не
изменяются. Встроенные defaults происходят из исходного Evilauncher.

XML использует исходные имена секций/опций и числовые `RegistryValueKind`.
Опции публикуются атомарной заменой файла; прочитанные значения проверяются
на конфликт, непрочитанные чужие значения и XML-метаданные сохраняются.
Кнопка «По умолчанию» загружает собственные defaults, изменения записываются
при применении; пути ресурсов сохраняются. Настройки встроенного рендера имеют отдельные
пресеты в прежней форме.

Меняйте настройки при закрытом EIRebuild: протокол согласованной одновременной
записи двух разных процессов в один XML не предусмотрен. Атомарная замена
предотвращает частичный XML, но не обеспечивает межпроцессную транзакцию.
Совместимость с текущим EIRebuild предполагает реализацию RESTARTER-CONTRACT.

Сборка: .NET Framework 4.7.2 и Visual Studio Build Tools, команда
`powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1`.
Результат: `EIStarter/bin/Release/ReStarter.exe` и зависимости. Исходное имя
каталога и namespace оставлены для ресурсных привязок импортированного UI.
Проверки: [tests/README.md](tests/README.md).

Для установки владельцем скопировать только ReStarter.exe, .config,
зависимости и background.jpg рядом с EIStarter.exe. EIStarter.exe,
game.exe, ddraw.dll и прежние XML/INI оставляются на месте. EIRebuild/RDDraw
устанавливаются отдельно владельцем из их проверенного комплекта.
По первому запуску ReStarter создаст собственные файлы рядом с собой.
В этой задаче установка не выполняется.

Авторы исходного Evilauncher: Atm; благодарности Demoth, Void Dragon,
Lost Soul, Asp Adm. Лицензия зависимостей: distribution/Microsoft-MIT.txt.
