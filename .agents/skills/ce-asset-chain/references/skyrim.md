# Skyrim SE/AE and VR

`SkyrimSE` also covers Anniversary Edition.
Supports BSA (`0x69`).

Skyrim loads its implicit plugins, installed `Skyrim.ccc` entries (SE only), then enabled, non-ghosted `plugins.txt` entries.

Skyrim loads INI-listed BSAs, then `Example.bsa` and `Example - Textures.bsa` for each active `Example.esp` in plugin order.
VR's `sVrResourceArchiveList` follows those archives, with `Skyrim_VR - Main.bsa` as the `engine-default` fallback when none of its listed files are found.

The whole query fails for a nonempty archive list in an active plugin's sidecar INI.
