using CreationEngine.AssetChain.Mo2;

namespace CreationEngine.AssetChain.Archives;

internal sealed record LogicalArchive(
    string Name,
    ArchiveLoadMechanism LoadMechanism,
    string LoadSource,
    int LoadIndex,
    string? AssociatedPlugin,
    int? PluginLoadOrderIndex,
    IReadOnlyList<PhysicalArchiveCopy> PhysicalCopies);

internal sealed record PhysicalArchiveCopy(SourceLayer Layer, string Path);

internal enum ArchiveLoadMechanism
{
    IniList,
    PluginAssociation,
    EngineDefault
}
