using CreationEngine.AssetChain.Archives;
using CreationEngine.AssetChain.Mo2;

namespace CreationEngine.AssetChain.Games.Starfield;

internal static class StarfieldArchives
{
    private static readonly string[] ContentArchiveSuffixes = ["- Main", "- Textures", "- Localization"];

    internal static IReadOnlyList<LogicalArchive> Build(
        Mo2Profile profile, StarfieldPluginList plugins, StarfieldSettings settings)
    {
        var archives = new List<LogicalArchive>();
        var byName = new Dictionary<string, LogicalArchive>(StringComparer.OrdinalIgnoreCase);
        foreach (var (setting, names) in settings.ArchiveLists())
        {
            foreach (var name in names)
            {
                Add(name, ArchiveLoadMechanism.IniList, setting);
            }
        }

        // Official/CCC content is mounted before ordinary plugin archives.
        // RVA 0x2CC8110 uses <stem>*.ba2 and category substrings, without a plugin-name boundary.
        // Starfield 1.16.244: RVAs 0x2CC8110 and 0x1894EB0.
        var namesInData = profile.DataFileNames
            .Where(name => name.EndsWith(".ba2", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var plugin in plugins.Plugins.Where(plugin => plugins.PrimaryNames.Contains(plugin.Name)))
        {
            var stem = Path.GetFileNameWithoutExtension(plugin.Name);
            var matchingNames = namesInData.Where(name =>
                name.StartsWith(stem, StringComparison.OrdinalIgnoreCase) &&
                ContentArchiveSuffixes.Any(suffix => name.Contains(suffix, StringComparison.OrdinalIgnoreCase)));
            foreach (var name in matchingNames)
            {
                Add(name, ArchiveLoadMechanism.PluginAssociation, plugin.Name, plugin);
            }
        }

        foreach (var plugin in plugins.Plugins)
        {
            if (plugin.Name.Equals("Starfield.esm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var stem = Path.GetFileNameWithoutExtension(plugin.Name);
            AddSeries(stem + " - Main", plugin);
            AddSeries(stem + " - Textures", plugin);
        }
        foreach (var plugin in plugins.Plugins)
        {
            var stem = Path.GetFileNameWithoutExtension(plugin.Name);
            Add($"{stem} - Voices_{settings.DefaultVoiceSuffix}.ba2",
                ArchiveLoadMechanism.PluginAssociation, plugin.Name, plugin);
            if (settings.LocaleVoices &&
                !settings.Language.Equals(settings.DefaultVoiceSuffix, StringComparison.OrdinalIgnoreCase))
            {
                Add($"{stem} - Voices_{settings.Language}.ba2",
                    ArchiveLoadMechanism.PluginAssociation, plugin.Name, plugin);
            }
        }
        // Later entries replace duplicate resource IDs in both registries:
        // Starfield 1.16.244, RVAs 0x27DD030 (GNRL) and 0x248F02C (DX10).
        return archives.Select((archive, index) => archive with { LoadIndex = index }).ToArray();

        void AddSeries(string stem, ActivePlugin plugin)
        {
            // RVA 0x5A8FE0: an unnumbered archive replaces the contiguous 01..99 series.
            if (Add(stem + ".ba2", ArchiveLoadMechanism.PluginAssociation, plugin.Name, plugin))
            {
                return;
            }
            for (var number = 1; number < 100; number++)
            {
                if (!Add($"{stem}{number:D2}.ba2", ArchiveLoadMechanism.PluginAssociation, plugin.Name, plugin))
                {
                    break;
                }
            }
        }

        bool Add(string name, ArchiveLoadMechanism mechanism, string source, ActivePlugin? plugin = null)
        {
            // The plugin-phase registration tracker starts after the INI lists (RVA 0x1894F49).
            if (byName.TryGetValue(name, out var existing) && plugin is not null &&
                existing.LoadMechanism == ArchiveLoadMechanism.PluginAssociation)
            {
                return true;
            }
            var copies = profile.ResolveDataFiles(name)
                .Select(file => new PhysicalArchiveCopy(file.Layer, file.Path))
                .ToArray();
            if (copies.Length == 0)
            {
                return false;
            }
            if (existing is not null)
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
