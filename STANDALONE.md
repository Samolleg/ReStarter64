# Standalone distribution

An unpacked distribution contains `Base/`, `Engine/`, `MODS/`, the launcher and its managed dependencies. `Base/` or `standalone.files.sha256` identifies this layout automatically; no runtime configuration switch was added. Regular installations without that layout retain their existing launch behavior.

Base resources remain separate from mod overlays. The executable directory determines every path, including after relocation. Before starting the game, the launcher validates key base files and the selected mod, then transactionally binds the legacy registry's starter path, explicit addon DLL, selected mod directory and three game paths. The standalone mod selector does not write the live registry. The paths page displays bundled paths and prevents changing them.

The Work Path must contain `3dfpfpu.dll` and `2dintmmx.dll`; copies in Engine alone are insufficient. Both Base files are required by preflight. On first launch, an absent or invalid VIDEORESOLUTION is initialized from the current primary display mode (800x600 if the query fails or the mode is outside the engine's limits). A valid existing choice is retained. This resolution setting persists in the normal game profile and is separate from the six temporary path values. There is no need to run the optional checksum script before playing.

The launcher remains running invisibly until its child exits. It then restores the previous six values, including their types or absence. A failed process start restores immediately. Values edited by another writer are left alone; a transaction conflict prevents overwriting a concurrent change. Other graphics/audio preferences still use the existing shared settings profile.

Do not forcibly terminate the launcher while the game runs: process termination or power loss cannot execute the restoration handler. Simultaneous games sharing the legacy profile are unsupported. The launcher rejects an already running executable with the selected name. Runtime validation on a clean Windows profile and gameplay acceptance are separate from the headless checks.

Build with the existing Release workflow. Run `tests/Run-StandaloneCheck.ps1` for disposable registry and file fixtures, relocation, clean/previous settings, concurrent edits, failed process start, child lifetime and restoration. `tests/Run-SettingsCheck.ps1` checks the existing settings behavior. Neither runner starts the game.

The standalone game package is prepared separately from `distribution/`, which remains the launcher-only update installer. Do not use its historical pinned artifact hashes to package this build.
