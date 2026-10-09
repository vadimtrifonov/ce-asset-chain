using CreationEngine.AssetChain.Archives;
using CreationEngine.AssetChain.Mo2;

namespace CreationEngine.AssetChain.Games;

internal interface IGameSupport
{
    string Mo2GameName { get; }
    IReadOnlyList<LogicalArchive> ReadArchives(Mo2Profile profile);
    IEnumerable<string> ReadArchivePaths(string path);
}
