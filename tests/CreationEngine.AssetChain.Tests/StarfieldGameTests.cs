using System.Text.Json;
using Xunit;
using static CreationEngine.AssetChain.Tests.AssetChainTestDriver;

namespace CreationEngine.AssetChain.Tests;

public sealed class StarfieldGameTests : IClassFixture<AssetChainFixture>, IDisposable
{
    private readonly AssetChainTestDriver _driver;
    private readonly AssetChainFixture _fixture;
    private readonly string _root;
    private string Game => Path.Combine(_root, "Game Root");
    private string Data => Path.Combine(Game, "Data");
    private string Low => Path.Combine(_root, "managed-mods", "Low");
    private string High => Path.Combine(_root, "managed-mods", "High");
    private string Profile => Path.Combine(_root, "named-profiles", _fixture.ProfileName);

    public StarfieldGameTests(AssetChainFixture fixture)
    {
        _fixture = fixture;
        _driver = new AssetChainTestDriver(fixture);
        _root = fixture.CreateCopy();
        fixture.ConfigureStarfield(_root);
    }

    [Theory]
    [InlineData("scripts/shared.pex", "Omega - Main.ba2")]
    [InlineData("textures/shared.dds", "Omega - Textures.ba2")]
    public void QueriesGeneralAndTextureArchives(string path, string winner)
    {
        var rows = Rows(path);
        Assert.Equal(winner, Winner(rows).GetProperty("archive").GetString());
        Assert.Equal("Omega.esm", Winner(rows).GetProperty("associatedPlugin").GetString());
        Assert.DoesNotContain(rows, row => new[] { "CacheOnly.ba2", "Inactive - Main.ba2", "Ghost - Main.ba2" }
            .Contains(row.GetProperty("archive").GetString()));
    }

    [Fact]
    public void SelectedArchiveCanHideLowerMemberWithoutWinning()
    {
        Copy("patch", Path.Combine(Low, "Omega - Main.ba2"));
        Copy("other", Path.Combine(High, "Omega - Main.ba2"));
        var rows = Rows();
        Assert.Equal("Alpha - Main.ba2", Winner(rows).GetProperty("archive").GetString());
        var shadowed = Assert.Single(rows, row => row.GetProperty("archive").GetString() == "Omega - Main.ba2");
        Assert.Equal("Low", shadowed.GetProperty("sourceOrigin").GetString());
        Assert.False(shadowed.GetProperty("winner").GetBoolean());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnnumberedArchiveReplacesContiguousNumberedSeries(bool unnumbered)
    {
        File.Delete(Path.Combine(Low, "Alpha - Main.ba2"));
        Copy("general", Path.Combine(Low, "Alpha - Main01.ba2"));
        Copy("patch", Path.Combine(Low, "Alpha - Main02.ba2"));
        Copy("patch", Path.Combine(Low, "Alpha - Main04.ba2"));
        if (unnumbered)
        {
            Copy("general", Path.Combine(Low, "Alpha - Main.ba2"));
        }
        var names = Rows().Where(row => row.GetProperty("associatedPlugin").GetString() == "Alpha.esp")
            .Select(row => row.GetProperty("archive").GetString()).ToArray();
        Assert.Equal(unnumbered ? ["Alpha - Main.ba2"] : new[] { "Alpha - Main01.ba2", "Alpha - Main02.ba2" }, names);
    }

    [Fact]
    public void IniArchivesFollowTheirLoadingPhases()
    {
        Copy("general", Path.Combine(Data, "Index.ba2"));
        Copy("general", Path.Combine(Data, "Misc.ba2"));
        File.AppendAllText(Path.Combine(Profile, "StarfieldCustom.ini"),
            "sResourceIndexFileList='Index.ba2'\n" +
            "sResourceMiscArchiveMemoryCacheList=\"Misc.ba2\"\n");
        var names = Rows().Where(row => row.GetProperty("archiveLoadMechanism").GetString() == "ini-list")
            .Select(row => row.GetProperty("archive").GetString()).ToArray();
        Assert.Equal(new[] { "Startup.ba2", "Base.ba2", "Misc.ba2", "Index.ba2", "EnglishVoices.ba2" }, names);
    }

    [Fact]
    public void PluginRegistrationReplacesEarlierIniRegistration()
    {
        File.AppendAllText(Path.Combine(Profile, "StarfieldCustom.ini"),
            "sResourceArchiveList=Omega - Main.ba2, Base.ba2\n");
        var rows = Rows();
        var omega = Assert.Single(rows, row => row.GetProperty("archive").GetString() == "Omega - Main.ba2");
        Assert.Equal("plugin-association", omega.GetProperty("archiveLoadMechanism").GetString());
        Assert.Equal("Omega.esm", omega.GetProperty("associatedPlugin").GetString());
        Assert.Equal("Omega - Main.ba2", Winner(rows).GetProperty("archive").GetString());
    }

    [Fact]
    public void EmptyArchiveSettingClearsInheritedList()
    {
        File.AppendAllText(Path.Combine(Profile, "StarfieldCustom.ini"), "sResourceArchiveList=\"\"\n");
        Assert.DoesNotContain(Rows(), row => row.GetProperty("archive").GetString() == "Base.ba2");
    }

    [Fact]
    public void MissingIniArchivesAreSkippedWithoutChangingOutput()
    {
        var expected = Run();
        Assert.NotEmpty(ParseRows(expected));
        File.AppendAllText(Path.Combine(Profile, "StarfieldCustom.ini"),
            "sResourceArchiveList=Missing.ba2, Base.ba2\n");
        Assert.Equal(expected, Run());
    }

    [Fact]
    public void OfficialContentUsesBroadPrefixAndCategoryMatching()
    {
        AssetChainFixture.WriteStarfieldPlugin(Path.Combine(Data, "ShatteredSpace.esm"));
        AssetChainFixture.WriteStarfieldPlugin(Path.Combine(High, "ShatteredSpaceFixes.esm"));
        File.AppendAllText(Path.Combine(Profile, "plugins.txt"), "ShatteredSpaceFixes.esm\n");
        var names = new[]
        {
            "ShatteredSpace - Localization.ba2", "ShatteredSpace - Main01.ba2",
            "ShatteredSpace - Main03.ba2", "ShatteredSpaceFixes - Main.ba2"
        };
        foreach (var name in names)
        {
            Copy("general", Path.Combine(Data, name));
        }
        var rows = Rows();
        var officialArchives = rows
            .Where(row => row.GetProperty("associatedPlugin").GetString() == "ShatteredSpace.esm")
            .Select(row => row.GetProperty("archive").GetString());
        Assert.Equal(names, officialArchives);
        Assert.Equal("Omega.esm", Winner(rows).GetProperty("associatedPlugin").GetString());
    }

    [Fact]
    public void DocumentsCccOverridesRootBuilderCccButDocumentsDataIsExcluded()
    {
        AssetChainFixture.WriteStarfieldPlugin(Path.Combine(Low, "RootCcc.esm"));
        AssetChainFixture.WriteStarfieldPlugin(Path.Combine(Low, "DocumentsCcc.esm"));
        Copy("general", Path.Combine(Low, "RootCcc - Main.ba2"));
        Copy("general", Path.Combine(Low, "DocumentsCcc - Main.ba2"));
        Directory.CreateDirectory(Path.Combine(High, "Root"));
        File.WriteAllText(Path.Combine(High, "Root", "Starfield.ccc"), "RootCcc.esm\n");
        Assert.Contains(Rows(), row => row.GetProperty("associatedPlugin").GetString() == "RootCcc.esm");

        var myGames = Path.Combine(_root, "Documents", "My Games", "Starfield");
        Directory.CreateDirectory(Path.Combine(myGames, "Data"));
        File.WriteAllText(Path.Combine(myGames, "Starfield.ccc"), "DocumentsCcc.esm\n");
        Copy("other", Path.Combine(myGames, "Data", "Base.ba2"));
        var rows = Rows();
        Assert.DoesNotContain(rows, row => new[] { "GameCcc.esm", "RootCcc.esm" }.Contains(row.GetProperty("associatedPlugin").GetString()));
        Assert.Contains(rows, row => row.GetProperty("associatedPlugin").GetString() == "DocumentsCcc.esm");
        var baseline = Assert.Single(rows, row => row.GetProperty("archive").GetString() == "Base.ba2");
        Assert.Equal(NormalizePath(Path.Combine(Data, "Base.ba2")), baseline.GetProperty("sourcePath").GetString());
    }

    [Theory]
    [InlineData(false, "BlueprintShips-Starfield.esm")]
    [InlineData(true, "BlueprintShips-Alpha.esm")]
    public void BlueprintOrderUsesSelectedPluginTimestampAndDescendingName(bool tie, string expectedWinner)
    {
        var old = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (var name in new[] { "BlueprintShips-Starfield", "BlueprintShips-Alpha" })
        {
            var path = Path.Combine(High, name + ".esm");
            AssetChainFixture.WriteStarfieldPlugin(path, blueprint: true);
            File.SetLastWriteTimeUtc(path, old);
            Copy("patch", Path.Combine(High, name + " - Main.ba2"));
        }
        if (!tie)
        {
            File.SetLastWriteTimeUtc(Path.Combine(High, "BlueprintShips-Starfield.esm"), old.AddDays(1));
        }
        AssetChainFixture.WriteStarfieldPlugin(Path.Combine(Low, "BlueprintShips-Alpha.esm"), blueprint: true);
        File.SetLastWriteTimeUtc(Path.Combine(Low, "BlueprintShips-Alpha.esm"), old.AddYears(10));
        File.WriteAllText(Path.Combine(Profile, "loadorder.txt"), "BlueprintShips-Starfield.esm\nBlueprintShips-Alpha.esm\n");
        var rows = Rows();
        Assert.Equal(expectedWinner, Winner(rows).GetProperty("associatedPlugin").GetString());
        File.Delete(Path.Combine(Profile, "loadorder.txt"));
        Assert.Equal(expectedWinner, Winner(Rows()).GetProperty("associatedPlugin").GetString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void VoiceArchivesUseGameLanguageIniAndSelectedPreferences(bool localized, bool localInis)
    {
        var myGames = Path.Combine(_root, "Documents", "My Games", "Starfield");
        Directory.CreateDirectory(myGames);
        var folder = localInis ? Profile : myGames;
        if (!localInis)
        {
            File.WriteAllText(Path.Combine(Profile, "settings.ini"), "[General]\nLocalSettings=false\n");
            File.Move(Path.Combine(Profile, "StarfieldCustom.ini"), Path.Combine(folder, "StarfieldCustom.ini"));
        }

        // No Steam manifest: sLanguage selects the game-folder language INI.
        var oppositePreference = $"[Controls]\nbUseLocaleVoices={(localized ? 0 : 1)}\n";
        File.AppendAllText(Path.Combine(Game, "Starfield.ini"), "[General]\nsLanguage=fr\n" + oppositePreference);
        File.WriteAllText(Path.Combine(Game, "Starfield_fr.ini"),
            "[Archive]\nsResourceLocaleVoiceList=FrenchVoices.ba2\n" + oppositePreference);
        File.WriteAllText(Path.Combine(myGames, "Starfield_fr.ini"),
            "[Archive]\nsResourceLocaleVoiceList=WrongVoices.ba2\n");
        File.AppendAllText(Path.Combine(folder, "StarfieldCustom.ini"), oppositePreference);
        File.AppendAllText(Path.Combine(Profile, "initweaks.ini"), oppositePreference);
        File.WriteAllText(Path.Combine(folder, "StarfieldPrefs.ini"),
            $"[Controls]\nbUseLocaleVoices={(localized ? 1 : 0)}\n");
        foreach (var name in new[]
                 { "FrenchVoices.ba2", "WrongVoices.ba2", "Omega - Voices_en.ba2", "Omega - Voices_fr.ba2" })
        {
            Copy("general", Path.Combine(Data, name));
        }

        var rows = Rows();
        Assert.Equal(localized ? "Omega - Voices_fr.ba2" : "Omega - Voices_en.ba2",
            Winner(rows).GetProperty("archive").GetString());
        Assert.Contains(rows, row => row.GetProperty("archive").GetString() ==
            (localized ? "FrenchVoices.ba2" : "EnglishVoices.ba2"));
        Assert.DoesNotContain(rows, row => row.GetProperty("archive").GetString() ==
            (localized ? "EnglishVoices.ba2" : "FrenchVoices.ba2"));
        Assert.DoesNotContain(rows, row => row.GetProperty("archive").GetString() == "WrongVoices.ba2");
    }

    [Fact]
    public void SteamLanguageSelectsRootBuilderLanguageIni()
    {
        File.AppendAllText(Path.Combine(Game, "Starfield.ini"), "[General]\nsLanguage=en\n");
        File.WriteAllText(Path.Combine(Game, "Starfield_fr.ini"),
            "[General]\nsLanguage=fr\n[Archive]\nsResourceLocaleVoiceList=WrongVoices.ba2\n");
        var root = Path.Combine(High, "Root");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Starfield_fr.ini"),
            "[General]\nsLanguage=fr\n[Archive]\nsResourceLocaleVoiceList=FrenchVoices.ba2\n");
        File.WriteAllText(Path.Combine(Profile, "StarfieldPrefs.ini"), "[Controls]\nbUseLocaleVoices=1\n");
        foreach (var name in new[] { "FrenchVoices.ba2", "WrongVoices.ba2", "Omega - Voices_fr.ba2" })
        {
            Copy("general", Path.Combine(Data, name));
        }

        var steam = Path.Combine(_root, "steamapps");
        var installed = Path.Combine(steam, "common", "Starfield");
        Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
        Directory.Move(Game, installed);
        File.WriteAllText(Path.Combine(steam, "appmanifest_1716740.acf"),
            "\"AppState\" { \"UserConfig\" { \"language\" \"french\" } }");
        var organizer = Path.Combine(_root, "ModOrganizer.ini");
        File.WriteAllText(organizer, System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(organizer),
            @"(?m)^gamePath=.*$", "gamePath=" + installed.Replace('\\', '/')));

        var rows = Rows();
        Assert.Equal("Omega - Voices_fr.ba2", Winner(rows).GetProperty("archive").GetString());
        Assert.Contains(rows, row => row.GetProperty("archive").GetString() == "FrenchVoices.ba2");
        Assert.DoesNotContain(rows, row => row.GetProperty("archive").GetString() == "WrongVoices.ba2");
    }

    [Fact]
    public void TestFileGuardUsesOnlyEffectiveGameInis()
    {
        File.AppendAllText(Path.Combine(Game, "Starfield.ini"), "[General]\nsTestFile1=Inactive.esm\n");
        File.WriteAllText(Path.Combine(Profile, "initweaks.ini"), "[General]\nsTestFile1=\n");
        var result = Run();
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains("sTestFile1", result.Stderr);

        File.AppendAllText(Path.Combine(Profile, "StarfieldCustom.ini"), "[General] ; comment\nsTestFile1=\"\"\n");
        File.WriteAllText(Path.Combine(Profile, "initweaks.ini"), "[General]\nsTestFile1=Inactive.esm\n");
        Assert.NotEmpty(Rows());
    }

    [Fact]
    public void LooseFileWinsAndBatchOrderIsPreserved()
    {
        Directory.CreateDirectory(Path.Combine(_root, "output", "scripts"));
        File.WriteAllText(Path.Combine(_root, "output", "scripts", "shared.pex"), "loose");
        var rows = ParseRows(_driver.RunBatch("scripts/shared.pex\ntextures/shared.dds\n", game: "Starfield", root: _root));
        var scripts = rows.Where(row => row.GetProperty("assetPath").GetString() == "scripts/shared.pex").ToList();
        Assert.Equal("Overwrite", Winner(scripts).GetProperty("sourceOrigin").GetString());
        Assert.Equal("textures/shared.dds", rows[^1].GetProperty("assetPath").GetString());
    }

    [Fact]
    public void UnreadableShadowedArchiveFailsWholeBatch()
    {
        File.WriteAllText(Path.Combine(Low, "Omega - Main.ba2"), "not a BA2");
        var result = _driver.RunBatch("scripts/looseonly.pex\nscripts/shared.pex\n", game: "Starfield", root: _root);
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains("Omega - Main.ba2", result.Stderr);
        Assert.Contains("Low", result.Stderr);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    public void RejectsMissingOrOutOfBoundsNameTable(long offset)
    {
        using (var stream = File.OpenWrite(Path.Combine(Data, "Base.ba2")))
        using (var writer = new BinaryWriter(stream))
        {
            stream.Position = 16;
            writer.Write(offset);
        }
        var result = Run();
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains("member-name table", result.Stderr);
    }

    [Theory]
    [InlineData("0", "", "bInvalidateOlderFiles")]
    [InlineData("1", "STRINGS\\", "sResourceDataDirsFinal")]
    public void RejectsUnsupportedLooseFileConfiguration(string invalidateOlderFiles, string dataDirs, string diagnostic)
    {
        File.WriteAllText(Path.Combine(Profile, "settings.ini"),
            "[General]\nLocalSettings=true\nAutomaticArchiveInvalidation=false\n");
        File.WriteAllText(Path.Combine(Profile, "StarfieldCustom.ini"),
            $"[Archive]\nbInvalidateOlderFiles={invalidateOlderFiles}\nsResourceDataDirsFinal={dataDirs}\n");
        var result = Run();
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains(diagnostic, result.Stderr);
    }

    private RunResult Run(string path = "scripts/shared.pex") => _driver.Run(path, game: "Starfield", root: _root);
    private List<JsonElement> Rows(string path = "scripts/shared.pex") => ParseRows(Run(path));
    private static JsonElement Winner(IEnumerable<JsonElement> rows) => Assert.Single(rows, row => row.GetProperty("winner").GetBoolean());
    private static void Copy(string name, string destination) => File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"starfield-{name}.ba2"), destination, overwrite: true);
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
