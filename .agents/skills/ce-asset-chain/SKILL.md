---
name: ce-asset-chain
description: List loose-file and archive providers of an asset, including the winning copy, in Skyrim SE/AE, Skyrim VR or Starfield MO2 profiles.
---

# Creation Engine Asset Chain

Use this skill directory as the working directory.
Requires Windows.

## Setup

```powershell
mise trust mise.toml
mise install
```

## Query

Run outside MO2:

```powershell
mise exec -- ce-asset-chain.exe `
  --game <SkyrimSE|SkyrimVR|Starfield> `
  --mo2-root "<folder containing ModOrganizer.ini>" `
  --profile "<profile name>" `
  "<Data-relative asset path>"
```

Case and slash direction do not matter in asset paths.
For a batch, replace the asset-path argument with `--paths-from <path>` to read a file, or `--paths-from -` to read from standard input.
The input contains one asset path per line.
Empty or duplicate lines are errors.

## Resolution

The tool reads the profile as saved on disk.
Each loose file or archive comes from Overwrite, enabled mods by MO2 priority, or game `Data`, whichever contains it first.
Loose files override archived assets.
Among MO2's selected archive files, later archives win.
MO2 skip rules apply to enabled mods and Overwrite, whose `Root` directories map to the game folder through Root Builder.

Archive settings start with game defaults, then user INIs selected by MO2's local-INI setting.
An explicit empty setting clears its inherited value.
Missing archives are skipped.

Game-specific loading rules and limits: [Skyrim](references/skyrim.md), [Starfield](references/starfield.md).

## Output

Standard output contains one JSON line for each matching loose file or archive member.
Archive rows follow archive-load order, with copies of each archive listed from lowest to highest MO2 priority.
Loose-file rows follow, also from lowest to highest priority.
Chains follow the input order.

| Field | Meaning |
|---|---|
| `assetPath`, `providerIndex` | Normalized Data-relative path and zero-based position in its chain. |
| `sourceKind` | `loose` or `archive`. |
| `sourceOrigin` | `Game Data`, a mod name, or `Overwrite`. |
| `sourcePath` | The loose file or archive that was read. |
| `sourceAssetPath` | Asset path with its stored spelling. |
| `modlistIndex` | Zero-based position among enabled mods in `modlist.txt`; 0 is highest priority; `null` for game files and Overwrite. |
| `archive` | BSA or BA2 filename. |
| `archiveLoadMechanism` | `ini-list` for an INI archive list, `plugin-association` for an active plugin, or `engine-default` for a fallback archive. |
| `archiveLoadSource`, `archiveLoadIndex` | INI setting, plugin or game that registered the archive, and its zero-based archive-load position. |
| `associatedPlugin`, `pluginLoadOrderIndex` | Plugin that registered the archive and its zero-based load-order position; `null` for other registrations. |
| `winner` | The copy selected by the game. |

Archive and plugin fields are `null` for loose files.
Matching members from overridden copies of loaded archives are included.
A chain has no winner when only overridden archive files contain the asset.
Use `winner`, not row position, to identify the selected copy.

No rows for a path means no matching provider.
An empty result succeeds.
On error, the tool writes to standard error and nothing to standard output, with exit code 2 for invalid arguments or 1 for other errors.

## Limits

The tool reads archive indexes.

The whole query fails for:

- A missing active plugin or unreadable archive index, including an index in an overridden archive file.
- A higher-priority directory masking a lower-priority file.
- A Root Builder mod containing `Root\Data`, which Root Builder would exclude.

The tool excludes:

- Custom Root Builder rules and additional file mappings from other MO2 plugins.
- Archives mounted by game menus or archive-loader extensions.
