using System.Buffers.Binary;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using CreationEngine.AssetChain.Archives;
using CreationEngine.AssetChain.Mo2;

namespace CreationEngine.AssetChain.Games.Skyrim;

internal sealed class SkyrimGame(GameRelease release) : IGameSupport
{
    public string Mo2GameName => release == GameRelease.SkyrimSE
        ? "Skyrim Special Edition"
        : "Skyrim VR";

    public IReadOnlyList<LogicalArchive> ReadArchives(Mo2Profile profile)
    {
        var plugins = SkyrimPlugins.Read(profile, release);
        var settings = SkyrimArchiveSettings.Read(profile, release);
        SkyrimArchiveSettings.RejectPluginSidecars(profile, plugins);
        return SkyrimArchives.Build(profile, plugins, settings, release);
    }

    public IEnumerable<string> ReadArchivePaths(string path)
    {
        const uint supportedVersion = 0x69;
        Span<byte> header = stackalloc byte[8];
        using (var stream = File.OpenRead(path))
        {
            stream.ReadExactly(header);
        }

        if (!header[..4].SequenceEqual("BSA\0"u8))
        {
            throw new InvalidDataException($"Invalid BSA signature: {path}");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
        if (version != supportedVersion)
        {
            throw new InvalidDataException(
                $"Unsupported BSA version 0x{version:X2} for {release}; expected 0x{supportedVersion:X2}: {path}");
        }

        return Archive.CreateReader(release, path).Files.Select(file => file.Path);
    }
}
