using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using CreationEngine.AssetChain.Mo2;

namespace CreationEngine.AssetChain.Games;

internal static class PluginList
{
    internal static ILoadOrderListingGetter[] ReadExplicit(string path, GameRelease release)
    {
        var listings = PluginListings.RawLoadOrderListingsFromPath(path, release).ToArray();
        var duplicate = listings.GroupBy(listing => listing.ModKey)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Plugin list contains duplicate plugin '{duplicate.Key.FileName}': {path}");
        }
        return listings;
    }

    internal static IEnumerable<ModKey> ReadNames(string path)
    {
        var seen = new HashSet<ModKey>();
        var lineNumber = 0;
        foreach (var raw in File.ReadLines(path))
        {
            lineNumber++;
            var name = raw.Trim();
            if (name.Length == 0 || name.StartsWith('#'))
            {
                continue;
            }
            if (!ModKey.TryFromNameAndExtension(name, out var key) || !seen.Add(key))
            {
                throw new InvalidOperationException(
                    $"Malformed or duplicate plugin in {path}, line {lineNumber}: {raw}");
            }
            yield return key;
        }
    }

    internal static IReadOnlyList<ActivePlugin> Resolve(Mo2Profile profile, IEnumerable<ModKey> keys) =>
        keys.Distinct().Select((key, index) =>
        {
            var name = key.FileName.ToString();
            var file = profile.ResolveDataFiles(name).LastOrDefault() ?? throw new FileNotFoundException(
                $"Active plugin '{name}' cannot be resolved through the selected MO2 profile.");
            return new ActivePlugin(name, index, file.Path);
        }).ToArray();
}

internal sealed record ActivePlugin(string Name, int LoadOrderIndex, string PhysicalPath);
