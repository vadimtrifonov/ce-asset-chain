using System.Text;

namespace CreationEngine.AssetChain.Tests;

public sealed class AssetChainFixture : IDisposable
{
    public string Root { get; } = Path.Combine(
        Path.GetTempPath(),
        "ce-asset-chain-tests",
        Guid.NewGuid().ToString("N"));

    public string GameRoot => Path.Combine(Root, "Game Root");
    public string DataFolder => Path.Combine(GameRoot, "Data");
    public string ModsFolder => Path.Combine(Root, "managed-mods");
    public string ProfilesFolder => Path.Combine(Root, "named-profiles");
    public string ProfileName => "Test Profile";
    public string ProfileFolder => Path.Combine(ProfilesFolder, ProfileName);
    public string OverwriteFolder => Path.Combine(Root, "output");

    public AssetChainFixture()
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(ModsFolder);
        Directory.CreateDirectory(ProfileFolder);
        Directory.CreateDirectory(OverwriteFolder);

        WriteInstanceSettings();
        WriteProfile();
        WriteSources();
    }

    public string CreateCopy(string namePrefix = "")
    {
        var copy = Path.Combine(
            Path.GetTempPath(),
            "ce-asset-chain-tests",
            namePrefix + Guid.NewGuid().ToString("N"));
        CopyDirectory(Root, copy);
        WriteInstanceSettings(copy);
        return copy;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    internal void ConfigureVr(string root, bool includeVrListSetting = false)
    {
        var organizerIni = Path.Combine(root, "ModOrganizer.ini");
        File.WriteAllText(organizerIni, File.ReadAllText(organizerIni).Replace(
            "gameName=Skyrim Special Edition", "gameName=Skyrim VR", StringComparison.Ordinal));
        var data = Path.Combine(root, "Game Root", "Data");
        File.WriteAllText(Path.Combine(data, "SkyrimVR.esm"), string.Empty);
        var archiveName = includeVrListSetting ? "VrLate.bsa" : "Skyrim_VR - Main.bsa";
        CopyArchive("archive-b-compressed.bsa", Path.Combine(data, archiveName));

        var profile = Path.Combine(root, "named-profiles", ProfileName);
        var vrSetting = includeVrListSetting ? "sVrResourceArchiveList=VrLate.bsa\n" : string.Empty;
        File.WriteAllText(Path.Combine(profile, "skyrimvr.ini"),
            "[Archive]\nsResourceArchiveList=Obsolete.bsa\nsResourceArchiveList2=BaseB.bsa\n" + vrSetting);
    }

    internal void ConfigureStarfield(string root)
    {
        var ini = Path.Combine(root, "ModOrganizer.ini");
        File.WriteAllText(ini, File.ReadAllText(ini).Replace("gameName=Skyrim Special Edition", "gameName=Starfield"));
        var game = Path.Combine(root, "Game Root");
        var data = Path.Combine(game, "Data");
        var low = Path.Combine(root, "managed-mods", "Low");
        var high = Path.Combine(root, "managed-mods", "High");
        var profile = Path.Combine(root, "named-profiles", ProfileName);
        WriteStarfieldPlugin(Path.Combine(data, "Starfield.esm"));
        WriteStarfieldPlugin(Path.Combine(low, "GameCcc.esm"));
        WriteStarfieldPlugin(Path.Combine(high, "Alpha.esp"));
        WriteStarfieldPlugin(Path.Combine(high, "Omega.esm"));
        File.WriteAllText(Path.Combine(game, "Starfield.ccc"), "GameCcc.esm\n");
        File.WriteAllText(Path.Combine(profile, "plugins.txt"), "*Alpha.esp\n*Omega.esm\nInactive.esm\n*Ghost.esm.ghost\n");
        File.WriteAllText(Path.Combine(game, "Starfield.ini"),
            "[Archive]\nsResourceStartUpArchiveList=Startup.ba2\nsResourceArchiveList=Base.ba2\n" +
            "sResourceIndexFileList=BaseTextures.ba2\nsResourceMiscArchiveMemoryCacheList=\n" +
            "sResourceEnglishVoiceList=EnglishVoices.ba2\nsResourceArchiveMemoryCacheList=CacheOnly.ba2\n");
        File.WriteAllText(Path.Combine(profile, "StarfieldCustom.ini"),
            "[Archive]\nbInvalidateOlderFiles=1\nsResourceDataDirsFinal=\n");
        CopyArchive("starfield-general.ba2", Path.Combine(data, "Startup.ba2"));
        CopyArchive("starfield-general.ba2", Path.Combine(data, "Base.ba2"));
        CopyArchive("starfield-textures-v2.ba2", Path.Combine(data, "BaseTextures.ba2"));
        CopyArchive("starfield-general.ba2", Path.Combine(data, "EnglishVoices.ba2"));
        CopyArchive("starfield-patch.ba2", Path.Combine(data, "CacheOnly.ba2"));
        CopyArchive("starfield-general.ba2", Path.Combine(low, "GameCcc - Main.ba2"));
        CopyArchive("starfield-general.ba2", Path.Combine(low, "Alpha - Main.ba2"));
        CopyArchive("starfield-patch.ba2", Path.Combine(high, "Omega - Main.ba2"));
        CopyArchive("starfield-textures.ba2", Path.Combine(high, "Omega - Textures.ba2"));
        CopyArchive("starfield-patch.ba2", Path.Combine(high, "Inactive - Main.ba2"));
        CopyArchive("starfield-patch.ba2", Path.Combine(high, "Ghost - Main.ba2"));
    }

    internal static void WriteStarfieldPlugin(string path, bool blueprint = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write("TES4"u8);
        writer.Write(18u);
        writer.Write(blueprint ? 0x801u : 1u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write("HEDR"u8);
        writer.Write((ushort)12);
        writer.Write(0.96f);
        writer.Write(0u);
        writer.Write(0x800u);
    }

    private void WriteInstanceSettings() => WriteInstanceSettings(Root);

    private static void WriteInstanceSettings(string root)
    {
        var gameRoot = Path.Combine(root, "Game Root");
        File.WriteAllText(
            Path.Combine(root, "ModOrganizer.ini"),
            $$"""
            [General]
            gameName=Skyrim Special Edition
            gamePath={{EncodeQSettingsByteArray(gameRoot)}}

            [Settings]
            base_directory={{root.Replace("\\", "/", StringComparison.Ordinal)}}
            mod_directory=%BASE_DIR%/managed-mods
            profiles_directory=%BASE_DIR%/named-profiles
            overwrite_directory=%BASE_DIR%/output
            profile_local_inis=true
            """.ReplaceLineEndings());
    }

    private static string EncodeQSettingsByteArray(string value)
    {
        var encoded = new StringBuilder("@ByteArray(");
        var previousWasHexEscape = false;
        foreach (var valueByte in Encoding.UTF8.GetBytes(value))
        {
            if (valueByte == '\\')
            {
                encoded.Append("\\\\");
                previousWasHexEscape = false;
            }
            else if (valueByte is >= 0x20 and < 0x7F &&
                     !(previousWasHexEscape && IsHexDigit(valueByte)))
            {
                encoded.Append((char)valueByte);
                previousWasHexEscape = false;
            }
            else
            {
                encoded.Append($"\\x{valueByte:x2}");
                previousWasHexEscape = true;
            }
        }

        var result = encoded.Append(')').ToString();
        return value.IndexOfAny([';', ',', '=']) >= 0 ? $"\"{result}\"" : result;

        static bool IsHexDigit(byte valueByte) =>
            valueByte is >= (byte)'0' and <= (byte)'9' or
                >= (byte)'A' and <= (byte)'F' or
                >= (byte)'a' and <= (byte)'f';
    }

    private void WriteProfile()
    {
        File.WriteAllText(
            Path.Combine(ProfileFolder, "modlist.txt"),
            """
            # Highest managed priority first
            +High
            +Middle
            +Low
            -Disabled
            *DLC: Dawnguard
            -Visual_separator
            """.ReplaceLineEndings());

        File.WriteAllText(
            Path.Combine(ProfileFolder, "plugins.txt"),
            """
            # Explicit plugins
            *Alpha.esp
            *Split.esp
            *Foo.esp
            *Shadow.esp
            *Omega.esp
            Disabled.esp
            """.ReplaceLineEndings());

        File.WriteAllText(
            Path.Combine(ProfileFolder, "loadorder.txt"),
            """
            # Complete MO2 order
            Skyrim.esm
            Update.esm
            Dawnguard.esm
            HearthFires.esm
            Dragonborn.esm
            ccFixture.esl
            Alpha.esp
            Split.esp
            Foo.esp
            Shadow.esp
            Omega.esp
            Disabled.esp
            """.ReplaceLineEndings());

        File.WriteAllText(
            Path.Combine(ProfileFolder, "settings.ini"),
            """
            [General]
            LocalSaves=false
            LocalSettings=true
            AutomaticArchiveInvalidation=true
            """.ReplaceLineEndings());

        File.WriteAllText(
            Path.Combine(ProfileFolder, "skyrim.ini"),
            """
            [Archive]
            sResourceArchiveList=Obsolete.bsa
            sResourceArchiveList2=BaseB.bsa
            """.ReplaceLineEndings());

        File.WriteAllText(
            Path.Combine(ProfileFolder, "skyrimcustom.ini"),
            """
            [Archive]
            sResourceArchiveList=BaseA.bsa
            """.ReplaceLineEndings());

        File.WriteAllText(
            Path.Combine(ProfileFolder, "skyrimprefs.ini"),
            "[Display]\nbFull Screen=0\n".ReplaceLineEndings());

        File.WriteAllText(
            Path.Combine(ProfileFolder, "initweaks.ini"),
            "[Archive]\nbInvalidateOlderFiles=1\n".ReplaceLineEndings());
    }

    private void WriteSources()
    {
        foreach (var implicitPlugin in new[]
                 {
                     "Skyrim.esm", "Update.esm", "Dawnguard.esm", "HearthFires.esm", "Dragonborn.esm"
                 })
        {
            File.WriteAllText(Path.Combine(DataFolder, implicitPlugin), string.Empty);
        }

        File.WriteAllText(Path.Combine(GameRoot, "Skyrim.ccc"), "ccFixture.esl\n");
        CopyArchive("archive-a.bsa", Path.Combine(DataFolder, "BaseA.bsa"));
        CopyArchive("archive-b-compressed.bsa", Path.Combine(DataFolder, "BaseB.bsa"));
        WriteLoose(DataFolder, "Scripts/LooseOnly.PEX", "game");
        WriteLoose(DataFolder, "Scripts/Physical.pex.mohidden", "physical game Data");

        var low = CreateMod("Low");
        File.WriteAllText(Path.Combine(low, "Alpha.esp"), "low plugin");
        File.WriteAllText(Path.Combine(low, "ccFixture.esl"), "creation club");
        CopyArchive("archive-cc.bsa", Path.Combine(low, "ccFixture.bsa"));
        CopyArchive("archive-a.bsa", Path.Combine(low, "Alpha.bsa"));
        CopyArchive("archive-blocked.bsa", Path.Combine(low, "Foo.bsa"));
        CopyArchive("archive-a.bsa", Path.Combine(low, "Shadow.bsa"));
        WriteLoose(low, "scripts/looseonly.pex", "low");
        WriteLoose(low, "Scripts/Hidden.pex", "visible lower copy");
        WriteLoose(low, "Data/Scripts/Nested.pex", "must stay nested");

        var middle = CreateMod("Middle");
        File.WriteAllText(Path.Combine(middle, "Split.esp"), "split plugin");
        File.WriteAllText(Path.Combine(middle, "Foo.esp"), "foo plugin");
        File.WriteAllText(Path.Combine(middle, "Shadow.esp"), "shadow plugin");
        CopyArchive("split-main.bsa", Path.Combine(middle, "Split.bsa"));
        CopyArchive("split-textures-compressed.bsa", Path.Combine(middle, "Split - Textures.bsa"));
        CopyArchive("archive-blocked.bsa", Path.Combine(middle, "Split - Meshes.bsa"));
        WriteLoose(middle, "Scripts/Hidden.pex", "visible higher copy");
        WriteLoose(middle, "Scripts/Hidden.pex.mohidden", "separately skipped file");
        WriteLoose(middle, "Scripts/Physical.pex.mohidden", "skipped mapped file");
        WriteLoose(middle, ".git/Skipped.pex", "skipped directory");

        var high = CreateMod("High");
        File.WriteAllText(Path.Combine(high, "Alpha.esp"), "winning plugin copy");
        File.WriteAllText(Path.Combine(high, "Omega.esp"), "omega plugin");
        File.WriteAllText(Path.Combine(high, "Omega.esp.mohidden"), "separately skipped plugin");
        CopyArchive("archive-no-shared.bsa", Path.Combine(high, "Foo.bsa"));
        CopyArchive("archive-b-compressed.bsa", Path.Combine(high, "Shadow.bsa"));
        CopyArchive("archive-b-compressed.bsa", Path.Combine(high, "Omega.bsa"));
        CopyArchive("archive-blocked.bsa", Path.Combine(high, "Omega.bsa.mohidden"));
        WriteLoose(high, "SCRIPTS/LooseOnly.pex", "high");

        var disabled = CreateMod("Disabled");
        File.WriteAllText(Path.Combine(disabled, "Disabled.esp"), string.Empty);
        CopyArchive("archive-blocked.bsa", Path.Combine(disabled, "Disabled.bsa"));
        WriteLoose(disabled, "Scripts/DisabledOnly.pex", "disabled");

        WriteLoose(OverwriteFolder, "Scripts/OverwriteOnly.pex", "overwrite");
    }

    private string CreateMod(string name)
    {
        var path = Path.Combine(ModsFolder, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteLoose(string root, string relativePath, string contents)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private static void CopyArchive(string fixtureName, string destination)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        File.Copy(source, destination, overwrite: true);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
