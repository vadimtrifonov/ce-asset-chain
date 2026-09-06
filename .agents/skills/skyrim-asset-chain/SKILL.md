---
name: skyrim-asset-chain
description: Query loose-file and BSA provider chains and runtime winners for SE/AE or VR assets in MO2 profiles.
---

# Skyrim Asset Chain

Use this skill directory as the working directory.

## Setup

```bash
mise trust mise.toml
mise install
```

## Query inputs

Each query supplies an explicit game, MO2 root, profile, and asset-path input.

`--game` accepts `SkyrimSE` or `SkyrimVR`. `SkyrimSE` covers Special Edition and Anniversary Edition.
`--mo2-root` identifies the instance directory that contains `ModOrganizer.ini`.
`--profile` identifies a profile by name, not by directory path.

Asset paths identify files relative to the game `Data` directory. The tool normalizes path case and separators.

`<asset-input>` in the command template means exactly one of:

- `"<Data-relative-asset-path>"`
- `--paths-from "<path|->"` for one asset path per line

`-` reads the CLI process's standard input. Empty, invalid, and duplicate batch entries are errors.

## Run context

Run the command directly, outside MO2. `ModOrganizer.exe run` injects USVFS, which hides physical file origins.
The tool rejects a process when it detects injected USVFS.

```powershell
mise exec -- skyrim-asset-chain.exe `
  --game <SkyrimSE|SkyrimVR> `
  --mo2-root "<MO2 root>" `
  --profile "<profile>" `
  <asset-input>
```

The tool reads the physical game files, enabled mods, and `Overwrite` for the selected profile.
It also applies conventional Root Builder `Root` mappings and MO2 skip rules.

## Output

The command writes compact JSONL to standard output.
Each row represents one matching loose file or BSA member.
For batch input, rows follow input order and remain contiguous for each asset path.
Archive rows follow archive-load order. Loose-file rows follow weak-to-strong MO2 priority.
Use `winner` to identify the copy that the game uses instead of inferring it from row position.

Each row contains:

- Asset position: `assetPath`, `providerIndex`
- Physical source: `sourceKind`, `sourceOrigin`, `sourcePath`, `sourceAssetPath`, `modlistIndex`
- Archive registration: `archive`, `archiveLoadMechanism`, `archiveLoadSource`, `archiveLoadIndex`
- Plugin association: `associatedPlugin`, `pluginLoadOrderIndex`
- Runtime result: `winner`

`sourceKind` is `loose` or `archive`.
`sourceOrigin` identifies game `Data`, an enabled mod, or `Overwrite`.

`archiveLoadMechanism` has these values:

- `ini-list`: An archive-list INI setting registered the archive.
- `plugin-association`: An active plugin registered its associated archive.
- `engine-default`: The game registered a default archive.

The output can include several physical copies of one registered BSA. Only the copy selected by MO2 can win.
A successful chain can contain no winner when all matching BSA copies are shadowed.

A requested path with no matching loose file or BSA member produces no row.
A request with no matches succeeds with empty standard output.

Diagnostics use standard error. An error returns a nonzero exit code and leaves standard output empty, including for batch input.
Capture standard error separately. Include relevant diagnostics in the report because missing registered archives can produce warnings.
