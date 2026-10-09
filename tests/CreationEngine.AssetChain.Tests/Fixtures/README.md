# Archive fixtures

The archives contain generated test data.
BSArch v0.9c created them.

## Skyrim

The BSA fixtures use `-sse` (version `0x69`), except `archive-skyrim-le.bsa`, which uses `-tes5` (version `0x68`) to test format rejection.
Files with `compressed` in the name also use `-z`.

## Starfield

`starfield-general.ba2`, `starfield-patch.ba2` and `starfield-other.ba2` use `-sf1 -z` (version 2, `GNRL`).
`starfield-textures.ba2` uses `-sf1dds -z` (version 3, `DX10`).
Its two small generated DDS files exercise membership reading independently of texture chunks and compression.

`starfield-textures-v2.ba2` is an uncompressed version-3 texture archive converted to version 2 by removing the four-byte compression field and adjusting the name-table and chunk offsets.
BSArch verifies both the version-2 index and its extracted textures.

`BSArch64.exe <archive> -list` verifies every fixture's stored paths.
