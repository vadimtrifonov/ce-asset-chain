using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using Xunit;
using static CreationEngine.AssetChain.Tests.AssetChainTestDriver;

namespace CreationEngine.AssetChain.Tests;

public sealed class SkyrimGameTests(AssetChainFixture fixture) : IClassFixture<AssetChainFixture>, IDisposable
{
    private readonly AssetChainTestDriver _driver = new(fixture);
    private readonly string _root = fixture.CreateCopy();
    private string ProfileFolder => Path.Combine(_root, "named-profiles", fixture.ProfileName);

    [Fact]
    public void VrUsesPluginsTxtRatherThanCreationClubListing()
    {
        fixture.ConfigureVr(_root);
        Assert.Empty(ParseRows(Run("scripts/creationclub.pex", "SkyrimVR")));

        File.AppendAllText(Path.Combine(ProfileFolder, "plugins.txt"), "\n*ccFixture.esl\n");
        var row = Assert.Single(ParseRows(Run("scripts/creationclub.pex", "SkyrimVR")));
        Assert.Equal("ccFixture.esl", row.GetProperty("associatedPlugin").GetString());
        Assert.True(row.GetProperty("winner").GetBoolean());
    }

    [Theory]
    [InlineData("[Archive] ; comment\nsResourceArchiveList=BaseA.bsa\n")]
    [InlineData("[Archive]\nsResourceArchiveList=\"BaseA.bsa\"\n")]
    [InlineData("[archive]\nsresourcearchivelist='BaseA.bsa'\n")]
    public void ReadsGameIniWithWindowsSemantics(string contents)
    {
        var original = Run();
        Assert.NotEmpty(ParseRows(original));
        File.WriteAllText(Path.Combine(ProfileFolder, "skyrimcustom.ini"), contents);
        Assert.Equal(original, Run());
    }

    [Fact]
    public void ReadsCompleteArchiveList()
    {
        var original = Run();
        Assert.NotEmpty(ParseRows(original));
        var list = string.Join(", ", Enumerable.Repeat("BaseA.bsa", 200));
        File.WriteAllText(Path.Combine(ProfileFolder, "skyrimcustom.ini"), $"[Archive]\nsResourceArchiveList={list}\n");
        Assert.Equal(original, Run());
    }

    [Fact]
    public void EmptySettingClearsEarlierArchiveList()
    {
        File.AppendAllText(Path.Combine(ProfileFolder, "skyrimcustom.ini"), "\nsResourceArchiveList2=\"\"\n");
        var rows = ParseRows(Run());
        Assert.DoesNotContain(rows, row => row.GetProperty("archive").GetString() == "BaseB.bsa");
        Assert.Single(rows, row => row.GetProperty("archive").GetString() == "BaseA.bsa");
    }

    [Theory]
    [InlineData("SkyrimSE", "Skyrim Special Edition", "skyrim.ini")]
    [InlineData("SkyrimVR", "Skyrim VR", "skyrimvr.ini")]
    public void SharedGameInisUseIsolatedDocuments(string game, string folder, string mainIni)
    {
        if (game == "SkyrimVR")
        {
            fixture.ConfigureVr(_root);
        }
        File.WriteAllText(Path.Combine(ProfileFolder, "settings.ini"), "[General]\nLocalSettings=false\n");
        var documents = Path.Combine(_root, "Documents", "My Games", folder);
        Directory.CreateDirectory(documents);
        File.WriteAllText(Path.Combine(documents, mainIni),
            "[Archive]\nsResourceArchiveList=BaseB.bsa\nsResourceArchiveList2=\"\"\n");
        File.WriteAllText(Path.Combine(documents, "skyrimcustom.ini"), "[Archive]\nsResourceArchiveList=BaseA.bsa\n");
        File.WriteAllText(Path.Combine(ProfileFolder, "initweaks.ini"), "[Archive]\nsResourceArchiveList=BaseB.bsa\n");

        var rows = ParseRows(Run(game: game));
        Assert.Single(rows, row => row.GetProperty("archive").GetString() == "BaseA.bsa");
        Assert.DoesNotContain(rows, row => row.GetProperty("archive").GetString() == "BaseB.bsa");
    }

    [Theory]
    [InlineData("Inactive.esp")]
    [InlineData("*Ghost.esp.ghost")]
    public void InactiveAndGhostedPluginsDoNotRegisterArchives(string listing)
    {
        var original = Run();
        Assert.NotEmpty(ParseRows(original));
        File.AppendAllText(Path.Combine(ProfileFolder, "plugins.txt"), $"\n{listing}\n");
        Assert.Equal(original, Run());
    }

    [Fact]
    public void DuplicatePluginFailsWithoutOutput()
    {
        File.AppendAllText(Path.Combine(ProfileFolder, "plugins.txt"), "\n*Omega.esp\n");
        var result = Run();
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Stdout);
        Assert.Contains("duplicate", result.Stderr);
        Assert.Contains("Omega.esp", result.Stderr);
    }

    [Fact]
    public void MembershipQueryDoesNotReadAssetPayload()
    {
        var path = Path.Combine(_root, "managed-mods", "Low", "ccFixture.bsa");
        var size = Assert.Single(Archive.CreateReader(GameRelease.SkyrimSE, path).Files).Size;
        Assert.True(size > 0);
        using (var stream = File.OpenWrite(path))
        {
            stream.SetLength(stream.Length - size);
        }

        var row = Assert.Single(ParseRows(Run("scripts/creationclub.pex")));
        Assert.Equal("ccFixture.bsa", row.GetProperty("archive").GetString());
        Assert.True(row.GetProperty("winner").GetBoolean());
    }

    private RunResult Run(string path = "scripts/shared.pex", string game = "SkyrimSE") =>
        _driver.Run(path, game: game, root: _root);

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
