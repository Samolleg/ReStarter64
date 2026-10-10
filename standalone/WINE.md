# Wine launcher variant

Build the same local project with MSBuild:

```text
MSBuild.exe EIStarter/EIStarter.csproj /t:Build /p:Configuration=Release /p:WineBuild=true /p:PostBuildEvent=
```

Output: `EIStarter/bin/Wine/EIStarter-Wine.exe` and its `.exe.config`.
The Wine output and intermediate directories are separate from Release. This is
an x86 .NET Framework 4.7.2 application running inside Wine, not a native Linux app.
Copy both files beside the existing Windows launcher in the autonomous package.
Keep the existing managed dependencies, profile files, Engine, Base and MODS.
Select EIStarter-Wine.exe in PortProton; do not rename it or run it from a separate
patch folder. There is no new runtime setting and no file-inventory gate.

Both launcher variants now edit the owned renderer's `Engine/eid3d11.ini` through
the Graphics page. The old DirectDraw proxy and dgVoodoo controls are removed.
The launcher never selects a Wine translation backend. Choose the desired
Direct3D 11 handling in PortProton; press F10 in the game for the renderer menu.

`WINE_COMPAT` excludes all SettingsProfile imports of KtmW32 and transacted
registry functions. The Windows compilation retains its original implementation.
The Wine path checks the snapshot before writing, rechecks individual values,
and reverses its writes after a caught failure. Detected concurrent edits are
reported and are not overwritten during compensating rollback.

This is not a kernel transaction: another process can observe partial updates
or race the checks. The existing shared launcher mutex serializes our launchers;
do not edit leased registry settings externally or start the game directly while
the launcher owns them. In autonomous mode the existing durable PortableProfile
journal is written before registry modification. A subsequent launch restores
the host profile after interruption, including a partial commit; an unsuccessful
restore keeps the journal for retry. Interrupted preference edits may be lost.
Outside autonomous mode there is no whole-session durable profile journal, so
only caught write failures have compensating rollback.

Validation uses PortableCheck, StandaloneCheck, SettingsCheck,
OwnedRendererCheck and WineRegistryCheck against the Wine assembly on isolated Windows test keys.
WineRegistryCheck checks absence of native transaction imports, exact type/value
rollback, mid-commit conflicts and a child process terminated after the first
write followed by actual journal recovery. These checks do not certify Mono,
Wine filesystem semantics, WinForms, display enumeration, or the game itself.

Required player acceptance: PortProtonQt 1.4.1 + Wine_LG_11-10, same Wine prefix
for launcher and game. Open launcher, change and save an option, start the game,
check menu and sound, exit and reopen to confirm preference persistence. Test
forced termination/recovery only in a disposable copy/prefix with the game
closed. Never delete a pending recovery journal as a workaround for an error.
