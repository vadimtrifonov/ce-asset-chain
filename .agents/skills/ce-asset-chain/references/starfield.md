# Starfield

Support covers the Steam edition.
Supports version-2/3 BA2 (`GNRL` and `DX10`) with member-name tables.

Starfield loads installed official plugins, installed `Starfield.ccc` entries, then enabled, non-ghosted `plugins.txt` entries.
`Documents\My Games\Starfield\Starfield.ccc` takes precedence over the game-folder copy.
An active `Example.esm` or `Example.esp` also activates an installed `BlueprintShips-Example.esm`, which must carry the Blueprint flag.
BlueprintShips plugins load last by the selected files' modification times, oldest first, with descending uppercased filename breaking ties.

Starfield loads INI-listed BA2s, official and CCC plugin archives, then active plugins' Main and Textures archives, and finally plugin voice archives.
Both the official/CCC and Main/Textures phases exclude `Starfield.esm`.
Main and Textures discovery checks `Example - Main.ba2` and `Example - Textures.ba2`.
For each suffix, an unnumbered file takes precedence over the `01`–`99` series, which stops at the first missing number.

Official and CCC plugins match `<name>*.ba2` containing `- Main`, `- Textures` or `- Localization`.
Thus `ShatteredSpace.esm` can load `ShatteredSpaceFixes - Main.ba2` even if `ShatteredSpaceFixes.esm` is inactive.

Localized voices use game-folder `Starfield_<language>.ini` and `[Controls] bUseLocaleVoices=1`, read from `Starfield.ini` then user `StarfieldPrefs.ini`.

Starfield requires `[Archive] bInvalidateOlderFiles=1`, empty `sResourceDataDirsFinal`, and empty or absent `sResourcePrefixList` so loose files override archives.

The tool excludes unmanaged files in `Documents\My Games\Starfield\Data`, which the game can load alongside the profile's files.
