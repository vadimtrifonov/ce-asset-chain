using CreationEngine.AssetChain.Archives;
using CreationEngine.AssetChain.Games;
using CreationEngine.AssetChain.Mo2;

namespace CreationEngine.AssetChain.Query;

internal static class AssetChainQuery
{
    internal static IReadOnlyList<AssetProviderRow> Execute(
        IGameSupport game,
        Mo2Profile profile,
        IReadOnlyList<LogicalArchive> archives,
        IReadOnlyList<string> assetPaths)
    {
        var requested = assetPaths.ToHashSet(StringComparer.Ordinal);
        var archiveMembers = ArchiveCatalog.Read(game, archives, requested);
        var rows = new List<AssetProviderRow>();

        foreach (var assetPath in assetPaths)
        {
            rows.AddRange(ResolveChain(profile, archives, archiveMembers, assetPath));
        }

        return rows;
    }

    private static IReadOnlyList<AssetProviderRow> ResolveChain(
        Mo2Profile profile,
        IReadOnlyList<LogicalArchive> archives,
        IReadOnlyDictionary<PhysicalArchiveCopy, IReadOnlyDictionary<string, string>> archiveMembers,
        string assetPath)
    {
        var candidates = new List<ProviderCandidate>();
        ProviderCandidate? archiveWinner = null;

        foreach (var archive in archives)
        {
            var winningPhysicalCopy = archive.PhysicalCopies[^1];

            foreach (var copy in archive.PhysicalCopies)
            {
                if (!archiveMembers[copy].TryGetValue(assetPath, out var storedPath))
                {
                    continue;
                }

                var candidate = ProviderCandidate.CreateArchive(archive, copy, storedPath);
                candidates.Add(candidate);
                if (ReferenceEquals(copy, winningPhysicalCopy))
                {
                    archiveWinner = candidate;
                }
            }
        }

        ProviderCandidate? looseWinner = null;
        foreach (var file in profile.ResolveDataFiles(assetPath))
        {
            var candidate = ProviderCandidate.CreateLoose(file);
            candidates.Add(candidate);
            looseWinner = candidate;
        }

        var winner = looseWinner ?? archiveWinner;
        var rows = new List<AssetProviderRow>(candidates.Count);
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var archive = candidate.Archive;
            rows.Add(new AssetProviderRow(
                AssetPath: assetPath,
                ProviderIndex: index,
                SourceKind: archive is null ? "loose" : "archive",
                SourceOrigin: candidate.Layer.Origin,
                SourcePath: NormalizePhysicalPath(candidate.SourcePath),
                SourceAssetPath: candidate.SourceAssetPath,
                Archive: archive?.Name,
                ArchiveLoadMechanism: archive is null ? null : FormatLoadMechanism(archive.LoadMechanism),
                ArchiveLoadSource: archive?.LoadSource,
                ArchiveLoadIndex: archive?.LoadIndex,
                AssociatedPlugin: archive?.AssociatedPlugin,
                PluginLoadOrderIndex: archive?.PluginLoadOrderIndex,
                ModlistIndex: candidate.Layer.ModlistIndex,
                Winner: ReferenceEquals(candidate, winner)));
        }

        return rows;
    }

    private static string NormalizePhysicalPath(string path) =>
        Path.GetFullPath(path).Replace('\\', '/');

    private static string FormatLoadMechanism(ArchiveLoadMechanism mechanism) => mechanism switch
    {
        ArchiveLoadMechanism.IniList => "ini-list",
        ArchiveLoadMechanism.PluginAssociation => "plugin-association",
        ArchiveLoadMechanism.EngineDefault => "engine-default",
        _ => throw new ArgumentOutOfRangeException(nameof(mechanism), mechanism, null)
    };

    private sealed class ProviderCandidate(
        SourceLayer layer, string sourcePath, string sourceAssetPath, LogicalArchive? archive)
    {
        internal SourceLayer Layer { get; } = layer;
        internal string SourcePath { get; } = sourcePath;
        internal string SourceAssetPath { get; } = sourceAssetPath;
        internal LogicalArchive? Archive { get; } = archive;

        internal static ProviderCandidate CreateArchive(
            LogicalArchive archive,
            PhysicalArchiveCopy copy,
            string storedPath) => new(copy.Layer, copy.Path, storedPath, archive);

        internal static ProviderCandidate CreateLoose(PhysicalSourceFile file) =>
            new(file.Layer, file.Path, file.RelativePath, archive: null);
    }
}

internal sealed record AssetProviderRow(
    string AssetPath,
    int ProviderIndex,
    string SourceKind,
    string SourceOrigin,
    string SourcePath,
    string SourceAssetPath,
    string? Archive,
    string? ArchiveLoadMechanism,
    string? ArchiveLoadSource,
    int? ArchiveLoadIndex,
    string? AssociatedPlugin,
    int? PluginLoadOrderIndex,
    int? ModlistIndex,
    bool Winner);
