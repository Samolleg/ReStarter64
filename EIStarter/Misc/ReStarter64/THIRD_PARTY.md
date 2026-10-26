# Third-party notices for ReStarter64 X64RS01

This launcher-only update embeds the NVIDIA DLSS license and Microsoft MIT
notice. The EIRebuild64 notices below describe the unchanged X64DLSS03 runtime;
its license/source files belong to that complete runtime bundle.

# EIRebuild64 / ReStarter64 X64DLSS03


This is a private local review bundle, not a public release.
THIRD_PARTY_SOURCE.md preserves the repository manifest; its P06/P19 planned-work
statements are historical. FFmpeg DLLs and the x64 embedded renderer are built now.

Included notices: licenses/miniaudio.txt (MIT-0/public domain), LuaJIT.txt (MIT),
nlohmann-json.txt (MIT), imgui.txt, meshoptimizer.txt, SMAA.txt (MIT),
DirectXTex.txt and DirectXTK.txt (Microsoft MIT), and licenses/ffmpeg/ (LGPL 2.1+).
Microsoft license texts were obtained from the jun2024 release tags:
https://raw.githubusercontent.com/microsoft/DirectXTex/jun2024/LICENSE
https://raw.githubusercontent.com/microsoft/DirectXTK/jun2024/LICENSE
Pinned NuGet packages: 2024.6.5.1 for both components.

FFmpeg 8.1.3 is dynamically linked. Matching unmodified source archive and build
recipe/configuration are in sources/ffmpeg; the source archive is not game data.
The source digest is recorded in source.lock. Signature verification remains open
as recorded in the original provenance. No GPL/nonfree/version3 libraries enabled.
The recipe has its original relative directory layout; no build runs on installation.

LuaJIT 2.1 GC64, pin a4c064539ea931274d407e1249b4bb575424a0f4, is statically linked.
miniaudio 0.11.25 uses WASAPI. XAudio2 Redist, Miles, Bink SDK and LuaJIT DLL are
not included. Renderer shaders and camp artwork are linked as EXE resources;
camp artwork provenance is preserved in licenses/camp-provenance.txt.

No general license for the renderer was found in the vendor snapshot. Its
third-party licenses do not license the whole renderer. Public redistribution
is not approved by this bundle. Game/mod resources and private runtime files
are not included. Windows/VC++ runtime files must not be copied from x86.
AMD FidelityFX Super Resolution 2.2.1 (FSR 2), MIT: licenses/FSR2.txt.
Vendored upstream tag v2.2.1, commit 1680d1edd5c034f88ebbbb793d8b88f8842cf804.

## NVIDIA DLSS / NGX (X64DLSS01)

NVIDIA DLSS SDK v310.9.16, NVIDIA/DLSS tag v310.9.16, commit
`9981c6350dc58c0ce21f024fd65e31e851c31689`.
Source: https://github.com/NVIDIA/DLSS/tree/v310.9.16 .
This product uses NVIDIA DLSS and NVIDIA NGX. NVIDIA and GeForce RTX are
trademarks and/or registered trademarks of NVIDIA Corporation in the U.S. and
other countries. Copyright NVIDIA Corporation. All rights reserved.

The SDK is external, not vendored. Run tools/prepare-worktree.ps1; its
-DlssSdkSource parameter or EIREBUILD64_DLSS_SDK environment variable overrides
the default work/deps64/dlss-sdk/v310.9.16 source. Build property DlssSdkRoot
can override the prepared dependencies/dlss/v310.9.16 location. The host links
the Windows x64 nvsdk_ngx_d.lib (/MD), preserving the existing CRT. Only the
release nvngx_dlss.dll is shipped beside EIRebuild64.exe, with the unchanged
SDK LICENSE.txt at licenses/NVIDIA-DLSS-LICENSE.txt. Headers/import libraries
and development DLLs are excluded from the handoff and Git.

Release obligations remain with the owner: NVIDIA RTX SDK License plus RTX
Supplement (March 14, 2024), especially base sections 1(c), 2 and supplement
1, 4, 6.2, 7.1(b), 7.2(c). Notify NVIDIA before commercial release; provide
the required application attribution and NVIDIA Marks on splash screens,
About (if present) and game credits; obtain applicable prior approval for
mark usage. This documentation notice does not replace those application
requirements. No notification, trademark approval or acceptance on behalf
of the owner is performed by the build. Review distribution/EULA conditions
and resolve these obligations before public release.


## ReStarter64

The bundled System.* runtime libraries use the Microsoft MIT license:
licenses/ReStarter-Microsoft-MIT.txt. The launcher includes text-only NVIDIA
DLSS / NGX attribution in its Information window; no NVIDIA logos or NVIDIA Marks
artwork have been added. Trademark placement/approval obligations remain open
for the owner before public distribution. Runtime binaries are in Engine64;
licenses and matching FFmpeg sources are at this bundle's root.

