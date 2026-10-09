# Creation Engine Asset Chain

Reports the loose files and archive members that provide an asset in a Mod Organizer 2 profile, including the winning copy.
Supports Skyrim SE/AE, Skyrim VR and Starfield.

## Requirements

- Windows
- .NET 10 runtime

## Usage

```powershell
ce-asset-chain.exe `
  --game SkyrimSE `
  --mo2-root "C:\Games\Skyrim\My MO2 Instance" `
  --profile Default `
  "scripts/QF_MQ101_0003372B.pex"
```

`--game` is `SkyrimSE`, `SkyrimVR` or `Starfield`.
`SkyrimSE` also covers Anniversary Edition.
`--mo2-root` is the MO2 instance folder that contains `ModOrganizer.ini`.
`--profile` is a profile name, not a path.

Asset paths are relative to `Data`.
Case and slash direction do not matter.
To query several assets, supply one path per line and replace the asset-path argument with:

- `--paths-from <path>` to read asset paths from a file.
- `--paths-from -` to read from standard input.

Run the tool outside MO2.
It reads the profile as saved on disk and locates files itself.

## Resolution

For a loose file or an archive name, MO2 uses the first location that contains it:

1. Overwrite.
2. Enabled mods, from highest to lowest MO2 priority.
3. The game's `Data` folder.

Among the selected archives, those loaded later override earlier ones.
Loose files override archived assets.

MO2's `skip_file_suffixes` and `skip_directories` settings apply to enabled mods and Overwrite, with defaults `.mohidden` and `.git`.
A `Root` directory in an enabled mod or Overwrite maps to the game folder through Root Builder.

MO2's local-INI setting selects user settings from the profile or the game's `Documents\My Games` folder.
The INIs below apply in the stated order.
An absent setting inherits its earlier value; an explicit empty value clears it.
Archive-list keys are in `[Archive]`, and missing archive files are skipped.

### Skyrim SE/AE and VR

Skyrim SE loads its five implicit plugins, installed game-folder `Skyrim.ccc` entries, then enabled, non-ghosted `plugins.txt` entries.
Skyrim VR loads its six implicit plugins, including `SkyrimVR.esm`, then enabled, non-ghosted `plugins.txt` entries.

BSAs load in this order:

1. `sResourceArchiveList`.
2. `sResourceArchiveList2`.
3. Active plugins' associated archives, in plugin order.
   `Example.esp` loads `Example.bsa`, then `Example - Textures.bsa`.
4. In VR, `sVrResourceArchiveList`.
   If none of its archives are found, `Skyrim_VR - Main.bsa` is used when present.

The last registration of a BSA determines its load position and source.

Settings start with game defaults, including game-folder `Skyrim_Default.ini` (SE) or `Skyrim.ini` (VR).
User `Skyrim.ini` / `SkyrimVR.ini`, then `SkyrimCustom.ini`, override those values.

### Starfield

Starfield loads installed official plugins, installed `Starfield.ccc` entries, then enabled, non-ghosted `plugins.txt` entries.
`Documents\My Games\Starfield\Starfield.ccc` takes precedence over the game-folder copy.
An active `Example.esm` or `Example.esp` also activates an installed `BlueprintShips-Example.esm`, which must carry the Blueprint flag.
BlueprintShips plugins load last, ordered by their selected files' modification times, oldest first.
Equal timestamps are ordered by descending uppercased filename.

BA2 loading follows Starfield 1.16.244:

1. INI lists: `sResourceStartUpArchiveList`, `sResourceArchiveList`, `sResourceMiscArchiveMemoryCacheList`, then `sResourceIndexFileList`.
2. `sResourceEnglishVoiceList`, or a nonempty `sResourceLocaleVoiceList` when localized voices are enabled.
3. Official and CCC plugin archives, except `Starfield.esm`.
4. Active plugins' Main and Textures archives, except `Starfield.esm`, in plugin order.
5. Plugin voice archives, in plugin order.

An archive's load position and source come from its first plugin registration, or its last INI-list registration if no plugin registers it.

Official and CCC plugins match `<name>*.ba2` filenames containing `- Main`, `- Textures` or `- Localization`, ordered by filename.
For example, `ShatteredSpace.esm` can load `ShatteredSpaceFixes - Main.ba2` even if `ShatteredSpaceFixes.esm` is inactive.

Ordinary archive discovery checks `Example - Main.ba2`, then `Example - Textures.ba2`.
For each suffix, the unnumbered archive is used if present; otherwise the `01`–`99` series loads up to the first missing number.
Voice archives use `Example - Voices_<suffix>.ba2`, with `sDLCDefaultVoiceSuffix` (default `en`), then `sLanguage` when localized voices are enabled.

Archive settings start with game defaults, overridden by game-folder `Starfield.ini` and `Starfield_<language>.ini`, then user `StarfieldCustom.ini`.
`sResourceMiscArchiveMemoryCacheList` defaults to `Starfield - Misc.ba2`.
`sResourceArchiveMemoryCacheList` controls caching, not archive registration.

Steam's app manifest selects the installed language; when absent, selection uses `[General] sLanguage`, defaulting to `en`.
Localized voices are enabled by `[Controls] bUseLocaleVoices=1`, read from game-folder `Starfield.ini`, then user `StarfieldPrefs.ini`.

Starfield queries require `[Archive] bInvalidateOlderFiles=1`, empty `sResourceDataDirsFinal`, and empty or absent `sResourcePrefixList` so loose files override archives.

## Output

Standard output contains one compact JSON line for each matching loose file or archive member.
Archive rows follow archive-load order, with copies of each archive listed from lowest to highest MO2 priority.
Loose-file rows follow, also from lowest to highest priority.
Chains follow the input order.

```jsonl
{"assetPath":"scripts/qf_mq101_0003372b.pex","providerIndex":0,"sourceKind":"archive","sourceOrigin":"Game Data","sourcePath":"C:/Game/Data/Skyrim - Misc.bsa","sourceAssetPath":"scripts/qf_mq101_0003372b.pex","archive":"Skyrim - Misc.bsa","archiveLoadMechanism":"ini-list","archiveLoadSource":"sResourceArchiveList","archiveLoadIndex":0,"associatedPlugin":null,"pluginLoadOrderIndex":null,"modlistIndex":null,"winner":true}
```

| Field | Meaning |
|---|---|
| `assetPath` | Normalized Data-relative path. |
| `providerIndex` | Zero-based position in this chain. |
| `sourceKind` | `loose` or `archive`. |
| `sourceOrigin` | `Game Data`, a mod name, or `Overwrite`. |
| `sourcePath` | The loose file or archive that was read. |
| `sourceAssetPath` | Asset path with its stored spelling. |
| `modlistIndex` | Zero-based position among enabled mods in `modlist.txt`; 0 is highest priority; `null` for game files and Overwrite. |
| `archive` | BSA or BA2 filename. |
| `archiveLoadMechanism` | `ini-list` for an INI archive list, `plugin-association` for an active plugin, or `engine-default` for VR's fallback BSA. |
| `archiveLoadSource` | INI setting, plugin name or game that registered the archive. |
| `archiveLoadIndex` | Zero-based position in archive-load order. |
| `associatedPlugin`, `pluginLoadOrderIndex` | Plugin that registered the archive and its zero-based load-order position; `null` for other registrations. |
| `winner` | The copy selected by the game. |

Archive and plugin fields are `null` for loose files.
Matching members from overridden copies of loaded archives are included.
A chain has no winner when only overridden archive files contain the asset.
Use `winner`, not row position, to identify the selected copy.

A path with no matching provider produces no rows.
A query without any matches succeeds with empty output.

On error, the tool writes the message to standard error and nothing to standard output.
The exit code is 2 for invalid arguments and 1 for other errors.

## Limits

Supported formats are Skyrim SE/VR BSA (`0x69`) and Starfield version-2/3 BA2 (`GNRL` and `DX10`) with member-name tables.
The tool reads archive indexes without validating member contents.

The whole query fails for:

- A missing active plugin or unreadable archive index, including an index in an overridden archive file.
- A higher-priority directory masking a lower-priority file.
- A nonempty archive list in an active Skyrim plugin's sidecar INI.
- A Root Builder mod containing `Root\Data`, which Root Builder would exclude.

The tool excludes:

- Unmanaged files in `Documents\My Games\Starfield\Data`, which the game can load alongside the profile's files.
- Custom Root Builder rules and additional file mappings from other MO2 plugins.
- Archives mounted by game menus or archive-loader extensions.

## Development

Source builds use [mise](https://mise.jdx.dev/), which installs the .NET SDK.

### Build

```powershell
mise trust
mise install
mise run build
```

### Test

```powershell
mise run test
```

Tests use small archive fixtures and temporary MO2 profiles, so they need no game installation.
The [fixture notes](tests/CreationEngine.AssetChain.Tests/Fixtures/README.md) describe the archive formats.

### Publish

```powershell
mise run publish
```

The task writes the release folder and ZIP file to `artifacts`.
