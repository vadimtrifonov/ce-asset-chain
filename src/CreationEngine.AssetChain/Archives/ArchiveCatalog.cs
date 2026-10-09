using CreationEngine.AssetChain.Games;

namespace CreationEngine.AssetChain.Archives;

internal static class ArchiveCatalog
{
    internal static IReadOnlyDictionary<PhysicalArchiveCopy, IReadOnlyDictionary<string, string>> Read(
        IGameSupport game,
        IReadOnlyList<LogicalArchive> archives,
        IReadOnlySet<string> requested)
    {
        var result = new Dictionary<PhysicalArchiveCopy, IReadOnlyDictionary<string, string>>();
        foreach (var archive in archives)
        {
            foreach (var copy in archive.PhysicalCopies)
            {
                if (result.ContainsKey(copy))
                {
                    continue;
                }

                var matches = new Dictionary<string, string>(StringComparer.Ordinal);
                try
                {
                    foreach (var path in game.ReadArchivePaths(copy.Path))
                    {
                        var canonical = CommandLine.NormalizeAssetPath(path);
                        if (requested.Contains(canonical) &&
                            !matches.TryAdd(canonical, path.Replace('\\', '/').TrimStart('/')))
                        {
                            throw new InvalidDataException(
                                $"Archive contains duplicate case-insensitive member path '{canonical}'.");
                        }
                    }
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Cannot read archive '{archive.Name}' from '{copy.Layer.Origin}': " +
                        $"{copy.Path}. {exception.Message}", exception);
                }
                result.Add(copy, matches);
            }
        }
        return result;
    }
}
