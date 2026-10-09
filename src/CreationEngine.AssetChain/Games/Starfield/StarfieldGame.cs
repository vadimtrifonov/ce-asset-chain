using System.Text;
using CreationEngine.AssetChain.Archives;
using CreationEngine.AssetChain.Mo2;

namespace CreationEngine.AssetChain.Games.Starfield;

internal sealed class StarfieldGame : IGameSupport
{
    public string Mo2GameName => "Starfield";

    public IReadOnlyList<LogicalArchive> ReadArchives(Mo2Profile profile)
    {
        var settings = StarfieldSettings.Read(profile);
        return StarfieldArchives.Build(profile, StarfieldPlugins.Read(profile), settings);
    }

    public IEnumerable<string> ReadArchivePaths(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt32() != 0x58445442) // BTDX
        {
            throw new InvalidDataException($"Invalid BA2 signature: {path}");
        }
        var version = reader.ReadUInt32();
        var type = reader.ReadUInt32();
        if (version is not (2 or 3) || type is not (0x4C524E47 or 0x30315844)) // GNRL / DX10
        {
            throw new InvalidDataException($"Unsupported Starfield BA2 header; expected version 2 or 3 GNRL/DX10: {path}");
        }
        var count = reader.ReadUInt32();
        var namesOffset = reader.ReadUInt64();
        var headerSize = version == 3 ? 36u : 32u;
        if (stream.Length < headerSize)
        {
            throw new EndOfStreamException("Incomplete BA2 header.");
        }
        if (count == 0)
        {
            yield break;
        }
        if (namesOffset < headerSize || namesOffset > (ulong)stream.Length ||
            count > ((ulong)stream.Length - namesOffset) / 3)
        {
            throw new InvalidDataException("BA2 member-name table is missing or incomplete.");
        }

        // The name table is independent of compression and texture chunk layouts.
        stream.Position = (long)namesOffset;
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        for (uint index = 0; index < count; index++)
        {
            var length = reader.ReadUInt16();
            if (length == 0)
            {
                throw new InvalidDataException("BA2 contains an empty member name.");
            }
            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Incomplete BA2 member name.");
            }
            yield return encoding.GetString(bytes);
        }
    }
}
