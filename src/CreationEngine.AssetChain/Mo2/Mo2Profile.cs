namespace CreationEngine.AssetChain.Mo2;

internal sealed class Mo2Profile
{
    private static readonly string[] DefaultSkipFileSuffixes = [".mohidden"];
    private static readonly string[] DefaultSkipDirectories = [".git"];

    private readonly SourceFileSystem _sourceFiles;
    private readonly string _documentsFolder;
    private readonly bool _useLocalInis;

    private Mo2Profile(
        string gameRoot,
        string profileFolder,
        SourceFileSystem sourceFiles,
        string pluginsPath,
        bool useLocalInis,
        string documentsFolder)
    {
        GameRoot = gameRoot;
        ProfileFolder = profileFolder;
        PluginsPath = pluginsPath;
        _sourceFiles = sourceFiles;
        _useLocalInis = useLocalInis;
        _documentsFolder = documentsFolder;
    }

    internal string GameRoot { get; }
    internal string ProfileFolder { get; }
    internal string PluginsPath { get; }

    internal static Mo2Profile Load(
        string expectedGameName, string instanceRoot, string profileName, string documentsFolder)
    {
        var organizerIniPath = Path.Combine(instanceRoot, "ModOrganizer.ini");
        var organizerIni = IniFile.Read(organizerIniPath);
        ValidateConfiguredGame(
            expectedGameName,
            QSettingsValue.DecodeString(
                organizerIni.Get("General", "gameName"),
                $"[General] gameName in {organizerIniPath}"),
            organizerIniPath);

        var gamePathValue = QSettingsValue.DecodeString(
            organizerIni.Get("General", "gamePath"),
            $"[General] gamePath in {organizerIniPath}");
        if (string.IsNullOrWhiteSpace(gamePathValue))
        {
            throw new InvalidOperationException($"{organizerIniPath} has no [General] gamePath value.");
        }

        var gameRoot = ResolvePath(gamePathValue, instanceRoot, instanceRoot, "gamePath");
        RequireDirectory(Path.Combine(gameRoot, "Data"), "Physical game Data directory");

        var baseValue = QSettingsValue.DecodeString(
            organizerIni.Get("Settings", "base_directory"),
            $"[Settings] base_directory in {organizerIniPath}");
        var baseDirectory = string.IsNullOrWhiteSpace(baseValue)
            ? instanceRoot
            : ResolvePath(baseValue, instanceRoot, instanceRoot, "base_directory");

        var modsFolder = ResolveConfiguredDirectory(
            organizerIni,
            organizerIniPath,
            "mod_directory",
            "mods",
            instanceRoot,
            baseDirectory);
        var profilesFolder = ResolveConfiguredDirectory(
            organizerIni,
            organizerIniPath,
            "profiles_directory",
            "profiles",
            instanceRoot,
            baseDirectory);
        var overwriteFolder = ResolveConfiguredDirectory(
            organizerIni,
            organizerIniPath,
            "overwrite_directory",
            "overwrite",
            instanceRoot,
            baseDirectory);

        RequireDirectory(modsFolder, "MO2 mods directory");
        RequireDirectory(profilesFolder, "MO2 profiles directory");

        var profileFolder = Path.GetFullPath(Path.Combine(profilesFolder, profileName));
        var profilesPrefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(profilesFolder)) + Path.DirectorySeparatorChar;
        if (!profileFolder.StartsWith(profilesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Profile resolves outside the MO2 profiles directory: {profileName}");
        }

        RequireDirectory(profileFolder, $"MO2 profile '{profileName}'");
        var modlistPath = RequireFile(profileFolder, "modlist.txt");
        var pluginsPath = RequireFile(profileFolder, "plugins.txt");
        var settingsPath = RequireFile(profileFolder, "settings.ini");
        var local = IniFile.Read(settingsPath).Get("General", "LocalSettings")
            ?? organizerIni.Get("Settings", "profile_local_inis") ?? "true";
        var useLocalInis = local.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "on" => true,
            "false" or "0" or "no" or "off" => false,
            _ => throw new InvalidOperationException($"Invalid profile local-INI setting: {local}")
        };

        var enabledMods = Mo2ModList.ReadEnabled(modlistPath, modsFolder);
        var skipRules = ReadSkipRules(organizerIni, organizerIniPath);
        var sourceFiles = SourceFileSystem.Create(gameRoot, enabledMods, overwriteFolder, skipRules);
        return new Mo2Profile(gameRoot, profileFolder, sourceFiles, pluginsPath, useLocalInis, documentsFolder);
    }

    internal IReadOnlyList<PhysicalSourceFile> ResolveDataFiles(string canonicalPath) =>
        _sourceFiles.ResolveDataFiles(canonicalPath);

    internal IReadOnlyList<PhysicalSourceFile> ResolveGameFiles(string canonicalPath) =>
        _sourceFiles.ResolveGameFiles(canonicalPath);

    internal string GetMyGamesFolder(string gameFolder) =>
        Path.Combine(_documentsFolder, "My Games", gameFolder);

    internal string GetIniFolder(string gameFolder) => _useLocalInis
        ? ProfileFolder
        : GetMyGamesFolder(gameFolder);

    internal IEnumerable<string> DataFileNames => _sourceFiles.DataFileNames;

    private static Mo2SkipRules ReadSkipRules(IniFile ini, string iniPath) =>
        new(
            ReadQSettingsStringList(
                ini,
                iniPath,
                "skip_file_suffixes",
                DefaultSkipFileSuffixes),
            ReadQSettingsStringList(
                ini,
                iniPath,
                "skip_directories",
                DefaultSkipDirectories));

    private static IReadOnlyList<string> ReadQSettingsStringList(
        IniFile ini,
        string iniPath,
        string key,
        IReadOnlyList<string> defaultValue)
    {
        if (!ini.TryGet("Settings", key, out var raw))
        {
            return defaultValue.ToArray();
        }

        return QSettingsValue.DecodeStringList(
                raw,
                $"[Settings] {key} in {iniPath}")
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveConfiguredDirectory(
        IniFile ini,
        string iniPath,
        string key,
        string defaultName,
        string instanceRoot,
        string baseDirectory)
    {
        var value = QSettingsValue.DecodeString(
            ini.Get("Settings", key),
            $"[Settings] {key} in {iniPath}");
        value = string.IsNullOrWhiteSpace(value) ? $"%BASE_DIR%/{defaultName}" : value;
        return ResolvePath(value, instanceRoot, baseDirectory, key);
    }

    private static string ResolvePath(
        string value,
        string relativeRoot,
        string baseDirectory,
        string setting)
    {
        value = ReplaceOrdinalIgnoreCase(value, "%BASE_DIR%", baseDirectory)
            .Replace('/', Path.DirectorySeparatorChar);
        try
        {
            return Path.GetFullPath(Path.IsPathFullyQualified(value)
                ? value
                : Path.Combine(relativeRoot, value));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidOperationException($"Invalid MO2 path setting {setting}: {value}", exception);
        }
    }

    private static string ReplaceOrdinalIgnoreCase(string value, string oldValue, string newValue)
    {
        var index = value.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            value = string.Concat(value.AsSpan(0, index), newValue, value.AsSpan(index + oldValue.Length));
            index = value.IndexOf(oldValue, index + newValue.Length, StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }

    private static void ValidateConfiguredGame(string expectedName, string? configuredName, string iniPath)
    {
        if (string.IsNullOrWhiteSpace(configuredName))
        {
            throw new InvalidOperationException($"{iniPath} has no [General] gameName value.");
        }

        if (!configuredName.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Requested game requires {iniPath} gameName '{expectedName}', but found '{configuredName}'.");
        }
    }

    private static void RequireDirectory(string path, string description)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"{description} does not exist: {path}");
        }
    }

    private static string RequireFile(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"MO2 profile file does not exist: {path}");
        }

        return path;
    }
}
