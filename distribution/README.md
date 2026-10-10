# Distribution packaging

The distribution is an update for an existing Starter installation with Engine/Mods, not a game installer. Install.cmd opens the folder picker; Install.ps1 also accepts -StarterPath for unattended validation. No registry settings are written by the installer. The launcher itself continues using the existing shared game settings.

Build Release with build.ps1. Release omits debug data to avoid embedding private PDB paths. Build-Distribution.ps1 accepts the verified 0a0ef2e asset handoff, a new OutputDirectory and the current SourceCommit. It validates the original asset manifest and the separately verified no-debug EXE hash. Change pinned hashes only after checking the next build.

Run tests/Test-Distribution.ps1 with -Package pointing to the resulting directory and -Workspace pointing to a disposable test location. It uses artificial Engine/Mods folders and never launches the game. It checks installation, preservation, idempotence, update backup, conflicting libraries, wrong target, corrupt input and rollback after a locked destination causes a write failure. Test directories are retained for inspection.

Package only the prepared directory, then extract the ZIP into another location and verify manifest hashes. Do not include starter.config, game binaries, mods, source, tests, logs, PDBs or local evidence. Runtime IL and embedded resources of the first no-debug build were compared with the accepted functional baseline, and settings checks were rerun. Digital signing and validation on other computers remain separate work.
