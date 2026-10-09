using Mutagen.Bethesda;
using CreationEngine.AssetChain.Mo2;

namespace CreationEngine.AssetChain.Games.Skyrim;

internal static class SkyrimArchiveSettings
{
    private static readonly string[] DefaultArchiveList =
    [
        "Skyrim - Misc.bsa",
        "Skyrim - Shaders.bsa",
        "Skyrim - Interface.bsa",
        "Skyrim - Animations.bsa",
        "Skyrim - Meshes0.bsa",
        "Skyrim - Meshes1.bsa",
        "Skyrim - Sounds.bsa"
    ];

    private static readonly string[] DefaultArchiveList2 =
    [
        "Skyrim - Voices_en0.bsa",
        "Skyrim - Textures0.bsa",
        "Skyrim - Textures1.bsa",
        "Skyrim - Textures2.bsa",
        "Skyrim - Textures3.bsa",
        "Skyrim - Textures4.bsa",
        "Skyrim - Textures5.bsa",
        "Skyrim - Textures6.bsa",
        "Skyrim - Textures7.bsa",
        "Skyrim - Textures8.bsa",
        "Skyrim - Patch.bsa"
    ];

    private static readonly string[] ArchiveListSettingNames =
    [
        "sResourceArchiveList",
        "sResourceArchiveList2",
        "sVrResourceArchiveList"
    ];

    internal static ArchiveIniSettings Read(Mo2Profile profile, GameRelease release)
    {
        var iniFolder = profile.GetIniFolder(release == GameRelease.SkyrimSE
            ? "Skyrim Special Edition" : "Skyrim VR");
        var mainIniName = release == GameRelease.SkyrimSE ? "skyrim.ini" : "skyrimvr.ini";
        var values = ReadDefaultArchiveValues(profile, release);
        OverlayArchiveValues(values, Path.Combine(iniFolder, mainIniName));
        OverlayArchiveValues(values, Path.Combine(iniFolder, "skyrimcustom.ini"));

        return new ArchiveIniSettings(
            ParseArchiveList(values["sResourceArchiveList"], "sResourceArchiveList"),
            ParseArchiveList(values["sResourceArchiveList2"], "sResourceArchiveList2"),
            ParseArchiveList(values["sVrResourceArchiveList"], "sVrResourceArchiveList"));
    }

    internal static void RejectPluginSidecars(Mo2Profile profile, IReadOnlyList<ActivePlugin> activePlugins)
    {
        foreach (var plugin in activePlugins)
        {
            var sidecarName = Path.ChangeExtension(plugin.Name, ".ini");
            var sidecar = profile.ResolveDataFiles(sidecarName).LastOrDefault();
            if (sidecar is null)
            {
                continue;
            }

            foreach (var setting in ArchiveListSettingNames)
            {
                if (string.IsNullOrWhiteSpace(GameIni.ReadValue(sidecar.Path, "Archive", setting)))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Unsupported plugin-sidecar archive setting [Archive] {setting}: " +
                    $"active plugin '{plugin.Name}' uses '{sidecarName}' from " +
                    $"'{sidecar.Layer.Origin}': {sidecar.Path}");
            }
        }
    }

    private static Dictionary<string, string> ReadDefaultArchiveValues(
        Mo2Profile profile, GameRelease release)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sResourceArchiveList"] = string.Join(", ", DefaultArchiveList),
            ["sResourceArchiveList2"] = string.Join(", ", DefaultArchiveList2),
            ["sVrResourceArchiveList"] = string.Empty
        };

        var defaultName = release == GameRelease.SkyrimSE ? "Skyrim_Default.ini" : "Skyrim.ini";
        var defaultIni = profile.ResolveGameFiles(defaultName).LastOrDefault();
        if (defaultIni is not null)
        {
            OverlayArchiveValues(values, defaultIni.Path);
        }

        return values;
    }

    private static void OverlayArchiveValues(IDictionary<string, string> values, string path)
    {
        foreach (var key in ArchiveListSettingNames)
        {
            if (GameIni.ReadValue(path, "Archive", key) is { } value)
            {
                values[key] = value;
            }
        }
    }

    private static IReadOnlyList<string> ParseArchiveList(string value, string setting)
    {
        var names = new List<string>();
        foreach (var part in value.Split(','))
        {
            var name = part.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            if (!Path.GetExtension(name).Equals(".bsa", StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(name).Equals(name, StringComparison.Ordinal) ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidOperationException(
                    $"Invalid BSA filename in [Archive] {setting}: {name}");
            }

            names.Add(name);
        }

        return names;
    }
}

internal sealed record ArchiveIniSettings(
    IReadOnlyList<string> ResourceArchiveList,
    IReadOnlyList<string> ResourceArchiveList2,
    IReadOnlyList<string> VrResourceArchiveList);
