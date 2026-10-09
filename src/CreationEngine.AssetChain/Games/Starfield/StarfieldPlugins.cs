using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Headers;
using CreationEngine.AssetChain.Mo2;

namespace CreationEngine.AssetChain.Games.Starfield;

internal static class StarfieldPlugins
{
    private const string BlueprintPrefix = "BlueprintShips-";
    private const uint BlueprintFlag = 0x800;

    private static readonly string[] OfficialPlugins =
    [
        "Starfield.esm", "Constellation.esm", "OldMars.esm", "ShatteredSpace.esm",
        "SFBGS00D.esm", "SFBGS050.esm", "SFBGS003.esm", "SFBGS004.esm",
        "SFBGS006.esm", "SFBGS007.esm", "SFBGS008.esm", "SFBGS047.esm"
    ];

    internal static StarfieldPluginList Read(Mo2Profile profile)
    {
        var listings = PluginList.ReadExplicit(profile.PluginsPath, GameRelease.Starfield);
        var primary = OfficialPlugins.Select(name => ModKey.FromFileName(name)).ToList();
        var ccc = Path.Combine(profile.GetMyGamesFolder("Starfield"), "Starfield.ccc");
        if (!File.Exists(ccc))
        {
            ccc = profile.ResolveGameFiles("Starfield.ccc").LastOrDefault()?.Path;
        }
        if (ccc is not null)
        {
            primary.AddRange(PluginList.ReadNames(ccc));
        }
        var baseMaster = ModKey.FromFileName("Starfield.esm");
        var installedPrimary = primary
            .Where(key => key == baseMaster || profile.ResolveDataFiles(key.FileName).Count != 0)
            .Distinct()
            .ToArray();
        var enabled = listings.Where(listing => listing.Enabled && !listing.Ghosted)
            .Select(listing => listing.ModKey);
        var keys = installedPrimary.Concat(enabled).Distinct().ToArray();
        var ordinary = PluginList.Resolve(profile, keys.Where(key => !HasBlueprintPrefix(key.FileName)));
        var blueprints = new Dictionary<string, ActivePlugin>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in ordinary)
        {
            if (IsBlueprint(plugin.Name, plugin.PhysicalPath))
            {
                throw new InvalidOperationException(
                    $"Blueprint plugin '{plugin.Name}' requires the {BlueprintPrefix} prefix and a paired active plugin.");
            }
            var pairedName = $"{BlueprintPrefix}{Path.GetFileNameWithoutExtension(plugin.Name)}.esm";
            if (blueprints.ContainsKey(pairedName))
            {
                continue;
            }
            var file = profile.ResolveDataFiles(pairedName).LastOrDefault();
            if (file is null)
            {
                continue;
            }
            if (!IsBlueprint(pairedName, file.Path))
            {
                throw new InvalidOperationException(
                    $"Paired blueprint '{pairedName}' is missing the Blueprint header flag: {file.Path}");
            }
            blueprints.Add(pairedName, new ActivePlugin(pairedName, 0, file.Path));
        }
        foreach (var key in keys.Where(key => HasBlueprintPrefix(key.FileName)))
        {
            if (!blueprints.ContainsKey(key.FileName))
            {
                throw new InvalidOperationException(
                    $"Blueprint plugin '{key.FileName}' has no installed blueprint paired with an active main plugin.");
            }
        }
        var ordered = blueprints.Values
            .OrderBy(plugin => File.GetLastWriteTimeUtc(plugin.PhysicalPath))
            .ThenByDescending(plugin => plugin.Name.ToUpperInvariant(), StringComparer.Ordinal);
        var plugins = ordinary.Concat(ordered)
            .Select((plugin, index) => plugin with { LoadOrderIndex = index })
            .ToArray();
        var primaryNames = installedPrimary.Where(key => key != baseMaster)
            .Select(key => key.FileName.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new StarfieldPluginList(plugins, primaryNames);
    }

    private static bool HasBlueprintPrefix(string name) =>
        name.StartsWith(BlueprintPrefix, StringComparison.OrdinalIgnoreCase);

    private static bool IsBlueprint(string name, string path)
    {
        var modPath = new ModPath(ModKey.FromFileName(name), path);
        return (ModHeaderFrame.FromPath(modPath, GameRelease.Starfield).Flags & BlueprintFlag) != 0;
    }
}

internal sealed record StarfieldPluginList(IReadOnlyList<ActivePlugin> Plugins, IReadOnlySet<string> PrimaryNames);
