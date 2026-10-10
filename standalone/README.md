# Portable distribution profile

`portable-defaults.xml` is the initial game/addon profile distributed beside EIStarter.exe.
It contains no installation paths, display ordinal, fixed resolution, user commands or diagnostic flags.
Game appearance/camera preferences come from the owner-approved working profile. Safe sound (`Audio=2`)
is the first-run choice following the remote player's successful workaround; quest light is RGB120/72/120,
radius5. The game difficulty retains the original default0; saves and game data are not changed.
Addon preferences use defaults verified in the packaged addon's Config.cpp (WideScreen1, draw radii100,
AutoRecover1; optional multiplayer/diagnostics off). A foreign computer's settings are never used as defaults.

The main launcher mutex owns a temporary registry lease for EvilIslands, Settings and root Path/AddonPath.
Before the UI opens, prior values are atomically journaled in the user's LocalAppData/EIStarter directory,
then package preferences replace that scoped set using a registry transaction. The ordinary options dialog
and legacy engine can keep their existing registry interfaces. After the child exits (or UI is closed),
preferences are atomically saved to `portable-settings.xml` beside the launcher and previous host values
are restored. Unrelated registry branches are not changed. Empty keys created by the engine can remain;
their original absent values are restored to absence. A backup includes exact registry types and raw bytes.

After an interrupted launcher, a subsequent updated launcher restores the global recovery journal before
doing any other work. A running game prevents recovery; close it first. Do not launch another game directly
during the lease or edit the leased registry sections externally. Recovery restores host settings, but
preference edits from the interrupted session may be lost. No recovery depends on finding the old package
folder, and no complete registry export belongs in a distributable. Corrupt profiles/journals fail closed.

Supported saved resolutions are retained. On a different display, an unsupported saved resolution is
replaced by its current mode. Enumeration failure keeps an already valid choice. Initial fallback800x600
is used only when a valid mode cannot be determined. DPI compatibility applies only to the game child.

The owned renderer uses Direct3D 11 and saves its settings in `Engine/eid3d11.ini`; F10 opens its in-game menu. .NET Framework4.7.2+ and a compatible Windows
graphics driver remain OS prerequisites, not files copied from an original installation. The launcher does not read or require
the release manifest and does not enforce a fixed file inventory. Mods and other files may be updated or
renamed; paths follow the selected mod. Base or portable-defaults.xml identifies standalone mode.
Optional Verify-Files checks the original delivery snapshot separately and may report intentional changes.

Checks: Run-PortableCheck.ps1 (isolated HKCU test root), Run-StandaloneCheck.ps1 and Run-SettingsCheck.ps1.
GUI/game runtime acceptance must separately cover the final unpacked package, menu, same-save quest light,
sound/speech/music/video, new game, transitions and save/load. No native binaries are rebuilt for this change.
