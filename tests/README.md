# Settings regression checks

Build the launcher with `build.ps1`, then run `tests/Run-SettingsCheck.ps1` in Windows PowerShell. An optional `-AssemblyPath` selects an already built launcher. The runner does not rebuild or install it.

The checks instantiate the actual settings dialog without displaying it. They use a disposable `HKCU\Software\EIStarterValidation\settings-<GUID>` profile and remove that exact key afterwards. The active game profile and game files are never written. Temporary compiled test files are removed after the run.

Covered behavior: absent settings and custom values survive an unchanged save; explicit edits preserve unrelated keys; retired graphics and Addon values remain byte-exact and cannot be edited by hidden legacy controls; four navigation sections retain their page bindings; registry transactions roll back after an injected write failure; concurrent external edits are rejected; camera and path validation happens before writing; unsupported controls are absent; cancel does not save.

These checks require Windows registry transactions and .NET Framework 4.7.2. They do not prove the visual or gameplay effect of each setting in a running game.

Owned renderer settings: run `tests/Run-OwnedRendererCheck.ps1` against Release, and pass `-AssemblyPath EIStarter/bin/Wine/EIStarter-Wine.exe` for Wine. It uses a disposable Engine directory with a one-byte DLL presence fixture; it never loads or hashes the DLL. Checks cover all 49 player options plus window height, four presets, first-run handling, byte-preserving INI writes, no-op, unknown keys, conflicts, rollback, validation, actual graphics-page binding, F10 hint, Save/Cancel and Apply Paths isolation. No active game file is written. The test instantiates the dialog without showing it; live layout and game effects require owner acceptance.

WineEnvironmentCheck.cs: compile as an x86 console test with the Framework C# compiler and run against the Wine Release assembly. It launches only its own hidden child, verifies WINEDEBUG=warn+heap for absent/disabled/custom/already-set values, preserves an unrelated variable and checks the parent environment is unchanged. Windows child inheritance is tested; actual Wine heap behavior still requires the player to retry without a manual PortProton override.

Inline settings: run tests/Run-InlineControlsCheck.ps1 for either Release assembly. It checks click/model/keyboard/accessibility binding, unknown selections, narrow-width wrapping, the TableLayoutPanel unconstrained-width sentinel, disposal, the shared seven-size icon resource and section drawing in both themes. Optional -PreviewDirectory writes offscreen section previews to an existing directory. It uses no registry profile and shows no windows. Actual-form checks in the settings and owned-renderer suites also exercise inline sound/shadow/preset buttons and unknown INI values. Offscreen rendering does not certify interactive DPI or PortProton behavior.
