using Mutagen.Bethesda;
using CreationEngine.AssetChain.Archives;
using CreationEngine.AssetChain.Mo2;

namespace CreationEngine.AssetChain.Games.Skyrim;

internal static class SkyrimArchives
{
    internal static IReadOnlyList<LogicalArchive> Build(
        Mo2Profile profile,
        IReadOnlyList<ActivePlugin> plugins,
        ArchiveIniSettings settings,
        GameRelease release)
    {
        var archives = new List<LogicalArchive>();
        var byName = new Dictionary<string, LogicalArchive>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in settings.ResourceArchiveList)
        {
            Add(name, ArchiveLoadMechanism.IniList, "sResourceArchiveList");
        }
        foreach (var name in settings.ResourceArchiveList2)
        {
            Add(name, ArchiveLoadMechanism.IniList, "sResourceArchiveList2");
        }

        foreach (var plugin in plugins)
        {
            var stem = Path.GetFileNameWithoutExtension(plugin.Name);
            Add(stem + ".bsa", ArchiveLoadMechanism.PluginAssociation, plugin.Name, plugin);
            Add(stem + " - Textures.bsa", ArchiveLoadMechanism.PluginAssociation, plugin.Name, plugin);
        }

        if (release == GameRelease.SkyrimVR)
        {
            var loadedVrArchive = false;
            foreach (var name in settings.VrResourceArchiveList)
            {
                loadedVrArchive |= Add(name, ArchiveLoadMechanism.IniList, "sVrResourceArchiveList");
            }
            if (!loadedVrArchive)
            {
                Add("Skyrim_VR - Main.bsa", ArchiveLoadMechanism.EngineDefault, "Skyrim VR");
            }
        }

        return archives.Select((archive, index) => archive with { LoadIndex = index }).ToArray();

        bool Add(string name, ArchiveLoadMechanism mechanism, string source, ActivePlugin? plugin = null)
        {
            var copies = profile.ResolveDataFiles(name)
                .Select(file => new PhysicalArchiveCopy(file.Layer, file.Path))
                .ToArray();
            if (copies.Length == 0)
            {
                return false;
            }

            if (byName.TryGetValue(name, out var existing))
            {
                archives.Remove(existing);
            }
            var archive = new LogicalArchive(
                name, mechanism, source, archives.Count, plugin?.Name, plugin?.LoadOrderIndex, copies);
            archives.Add(archive);
            byName[name] = archive;
            return true;
        }
    }
}
