using System.Text.RegularExpressions;
using CreationEngine.AssetChain.Mo2;

namespace CreationEngine.AssetChain.Games.Starfield;

internal sealed class StarfieldSettings
{
    private readonly string[] _paths;

    private StarfieldSettings(string[] paths, string language, bool localeVoices)
    {
        _paths = paths;
        Language = language;
        LocaleVoices = localeVoices;
        DefaultVoiceSuffix = Read("Archive", "sDLCDefaultVoiceSuffix") ?? "en";
    }

    internal string Language { get; }
    internal bool LocaleVoices { get; }
    internal string DefaultVoiceSuffix { get; }

    internal static StarfieldSettings Read(Mo2Profile profile)
    {
        var main = profile.ResolveGameFiles("Starfield.ini").LastOrDefault()
            ?? throw new FileNotFoundException("Starfield.ini cannot be resolved through the selected MO2 profile.");
        var folder = profile.GetIniFolder("Starfield");
        var custom = Path.Combine(folder, "StarfieldCustom.ini");
        var paths = new List<string> { main.Path, custom };
        var installedLanguage = ReadSteamLanguage(profile.GameRoot)
            ?? ReadValue(paths, "General", "sLanguage") ?? "en";
        ValidateName(installedLanguage, "language");
        var languageIni = profile.ResolveGameFiles($"Starfield_{installedLanguage}.ini").LastOrDefault();
        if (languageIni is not null)
        {
            paths.Insert(1, languageIni.Path);
        }
        var language = ReadValue(paths, "General", "sLanguage") ?? installedLanguage;
        ValidateName(language, "sLanguage");

        // Preferences use a separate INI collection (Starfield 1.16.244, RVA 0x1898E0A).
        var voicePaths = new[] { main.Path, Path.Combine(folder, "StarfieldPrefs.ini") };
        var localeVoices = ReadValue(voicePaths, "Controls", "bUseLocaleVoices") == "1";
        var settings = new StarfieldSettings(paths.ToArray(), language, localeVoices);

        for (var index = 1; index <= 10; index++)
        {
            var key = $"sTestFile{index}";
            if (settings.Read("General", key) is { Length: > 0 })
            {
                throw new InvalidOperationException(
                    $"Unsupported Starfield {key}; activate plugins through MO2's plugin list instead.");
            }
        }
        if (settings.Read("Archive", "bInvalidateOlderFiles") != "1" ||
            settings.Read("Archive", "sResourceDataDirsFinal") != string.Empty ||
            !string.IsNullOrEmpty(settings.Read("Archive", "sResourcePrefixList")))
        {
            throw new InvalidOperationException(
                "Starfield queries require [Archive] bInvalidateOlderFiles=1, an empty sResourceDataDirsFinal, " +
                "and an empty sResourcePrefixList in the effective game INIs.");
        }
        ValidateName(settings.DefaultVoiceSuffix, "sDLCDefaultVoiceSuffix");
        return settings;
    }

    internal IEnumerable<(string Setting, IReadOnlyList<string> Names)> ArchiveLists()
    {
        // Starfield 1.16.244: RVAs 0x1893B40 and 0x1894EB0.
        // The general memory-cache list configures caching, not archive activation.
        foreach (var setting in new[] { "sResourceStartUpArchiveList", "sResourceArchiveList" })
        {
            yield return (setting, ParseList(Required(setting), setting));
        }
        const string misc = "sResourceMiscArchiveMemoryCacheList";
        yield return (misc, ParseList(Read("Archive", misc) ?? "Starfield - Misc.ba2", misc));
        const string textures = "sResourceIndexFileList";
        yield return (textures, ParseList(Required(textures), textures));
        var locale = Read("Archive", "sResourceLocaleVoiceList");
        var voiceKey = LocaleVoices && !string.IsNullOrEmpty(locale)
            ? "sResourceLocaleVoiceList" : "sResourceEnglishVoiceList";
        yield return (voiceKey, ParseList(Read("Archive", voiceKey) ?? "Starfield - Voices.ba2", voiceKey));
    }

    private static string? ReadSteamLanguage(string gameRoot)
    {
        var manifest = Path.GetFullPath(Path.Combine(gameRoot, "..", "..", "appmanifest_1716740.acf"));
        if (!File.Exists(manifest))
        {
            return null;
        }
        var match = Regex.Match(File.ReadAllText(manifest),
            "\"UserConfig\"\\s*\\{[^{}]*?\"language\"\\s*\"(?<language>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Groups["language"].Value.ToLowerInvariant() switch
        {
            "" or "english" => "en",
            "german" => "de",
            "french" => "fr",
            "italian" => "it",
            "spanish" => "es",
            "japanese" => "ja",
            "polish" => "pl",
            "brazilian" => "ptbr",
            "schinese" => "zhhans",
            var language => throw new InvalidOperationException(
                $"Unsupported Starfield Steam language '{language}' in {manifest}.")
        };
    }

    private string Required(string setting) => Read("Archive", setting)
        ?? throw new InvalidOperationException($"Missing Starfield [Archive] {setting}; retain the game's archive settings.");

    private string? Read(string section, string key) => ReadValue(_paths, section, key);

    private static string? ReadValue(IEnumerable<string> paths, string section, string key)
    {
        string? value = null;
        foreach (var path in paths)
        {
            value = GameIni.ReadValue(path, section, key) ?? value;
        }
        return value;
    }

    private static IReadOnlyList<string> ParseList(string value, string setting)
    {
        var names = value.Split(',').Select(name => name.Trim()).Where(name => name.Length != 0).ToArray();
        foreach (var name in names)
        {
            ValidateName(name, setting);
            if (!name.EndsWith(".ba2", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Invalid BA2 filename in [Archive] {setting}: {name}");
            }
        }
        return names;
    }

    private static void ValidateName(string value, string setting)
    {
        if (value.Length == 0 || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            Path.GetFileName(value) != value)
        {
            throw new InvalidOperationException($"Invalid Starfield {setting}: {value}");
        }
    }
}
