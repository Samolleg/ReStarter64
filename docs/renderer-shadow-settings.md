# Starter-only shadow settings

The original implementation b45736bb36f457d0cbe01f99be8be26887e3443d was accepted
by the owner on 2026-09-13. Later that day the owner requested removal of the
DDraw SHA-256 restriction. The current launcher implements that request.

The renderer options expose four startup modes and a terrain-shadow preference.
Settings apply on the next game launch. The optimizations-only mode disables the
terrain control while preserving its stored preference. DDraw no longer handles
shadow-changing F9/F10/F11 or modified F12 commands; plain F12 captures diagnostics.

RuntimeMode is offered when DDraw.dll exists and the configuration is valid,
without opening, hashing or loading the DLL. Replacing its contents does not
block a pending save. Missing DLLs still prevent changing the mode. The INI
editor preserves unrelated bytes and rejects ambiguous or concurrently changed
settings. Failed
settings writes roll back within the operation; power-loss atomicity across INI
and registry is not promised.

## Historical accepted R2 pair

- EIStarter.exe SHA-256: 55BDDF91BFE78903D5E2A9A5F91A89C96EE51F17CFB0A6C2C56C1ACF05E3B0A5.
- DDraw.dll SHA-256: 40B85C6FB92C2F57877DA6E35B8F0CE8296148EA96457DB10B816657BDAFB9D1.
- DDraw local source commit: 385163a0c2a7a2eb8667a3d5256e0ae84507b343,
  in the separate parent-project work/src_ddraw_starter_only_20260913 repository.
  Its verified imported baseline is 40e8f38b4a04921cc4d7fc9560b24a52f3f726bf.

## Validation and limits

The current Release build passed 85 mode, 51 renderer and 31 general settings
checks (167 total). Regression fixtures confirm different/replaced/locked DLL
contents do not block settings, while missing DLL and INI conflicts are rejected.
The rebuild was installed on 2026-09-13. The owner subsequently confirmed that
it works successfully and requested a launcher commit. The installed executable
was rechecked against the accepted artifact:

- Implementation: 76cf5bbf99fc19711554c6dbc727e76dbdac1034.
- EIStarter.exe SHA-256: 9D5A297997B60CCBFB2D14B54E6D825624301C513A2B907D37013C79D603F2E7.
- Package: parent-project work/handoffs/starter-no-ddraw-hash-20260913.
- Evidence: parent-project docs/testing/starter-no-ddraw-hash-20260913.md.

This acceptance update changes documentation only. The successful Release build
and 167 checks remain applicable to the unchanged implementation and artifact.
The owner's confirmation covers the requested launcher behavior; individual
mode/save-load/zone/DPI scenarios were not separately itemized. It does not
certify arbitrary DDraw implementations or extend native production coverage.

The following results refer to the earlier R2 pair:

The Release build passed, with 81 mode, 51 legacy renderer and 31 settings checks
(163 total). Seven language layouts were checked headlessly, including visual
inspection of the Russian render. Native validation is documented in the DDraw
repository's STARTER_ONLY_R2.md. Existing build evidence is reused because the
accepted source and binary identities have not changed.

The owner's confirmation covers the requested feature generally; individual
mode/save-load/zone/DPI cases were not separately itemized. No production 3/3
certification is inferred. Later DDraw builds do not require a launcher hash update. They must still
implement RuntimeMode and EnableTerrainSelfShadows for the settings to affect
the game; the launcher no longer attempts to infer support from DLL identity.

The parent project retains detailed evidence at
docs/testing/starter-only-shadows-r2-20260913.md and the immutable package at
work/handoffs/starter-only-shadows-r2-20260913. These commits are local; they do
not publish a release or change the installed game.
