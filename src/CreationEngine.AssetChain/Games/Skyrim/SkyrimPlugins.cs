using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using CreationEngine.AssetChain.Mo2;

namespace CreationEngine.AssetChain.Games.Skyrim;

internal static class SkyrimPlugins
{
    internal static IReadOnlyList<ActivePlugin> Read(Mo2Profile profile, GameRelease release)
    {
        var listings = PluginList.ReadExplicit(profile.PluginsPath, release);
        var keys = Implicits.Get(release).Listings.ToList();
        if (release == GameRelease.SkyrimSE &&
            profile.ResolveGameFiles("Skyrim.ccc").LastOrDefault() is { } ccc)
        {
            keys.AddRange(PluginList.ReadNames(ccc.Path)
                .Where(key => profile.ResolveDataFiles(key.FileName).Count != 0));
        }
        keys.AddRange(listings.Where(listing => listing.Enabled && !listing.Ghosted)
            .Select(listing => listing.ModKey));
        return PluginList.Resolve(profile, keys);
    }
}
