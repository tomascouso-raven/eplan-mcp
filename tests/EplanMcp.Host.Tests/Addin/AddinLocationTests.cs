using EplanMcp.Host.Addin;

namespace EplanMcp.Host.Tests.Addin;

/// <summary>Which add-in DLL install-addin registers: the installed layout, else the build of the same configuration. Medium.</summary>
[Trait("Size", "Medium")]
public sealed class AddinLocationTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("eplanmcp-addin-location-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Find_InAnInstalledApp_IsTheDllUnderBridgeAndUnblocksAllOfBridge()
    {
        string app = Path.Combine(_root, "EplanMcp", "app");
        string dll = Put(app, "bridge", "net481", "EplanMcp.Bridge.Eplan.dll");

        Assert.Equal(new AddinLocation(dll, Path.Combine(app, "bridge")), AddinLocation.Find(app, null, out _));
    }

    [Theory]
    [InlineData("Release")]
    [InlineData("Debug")]
    public void Find_InADevelopmentBuild_IsTheAddinOfTheSameConfiguration(string configuration)
    {
        string src = Path.Combine(_root, "repo", "src");
        string host = Path.Combine(src, "EplanMcp.Host", "bin", configuration, "net10.0");
        Directory.CreateDirectory(host);
        string dll = Put(src, "EplanMcp.Bridge.Eplan", "bin", configuration, "net481", "EplanMcp.Bridge.Eplan.dll");
        Put(src, "EplanMcp.Bridge.Eplan", "bin", configuration == "Debug" ? "Release" : "Debug", "net481", "EplanMcp.Bridge.Eplan.dll");

        Assert.Equal(new AddinLocation(dll, Path.GetDirectoryName(dll)!), AddinLocation.Find(host + Path.DirectorySeparatorChar, null, out _));
    }

    [Fact]
    public void Find_WithoutTheBuildOfTheSameConfiguration_IsNullAndSaysWhereItLooked()
    {
        string src = Path.Combine(_root, "repo", "src");
        string host = Path.Combine(src, "EplanMcp.Host", "bin", "Debug", "net10.0");
        Directory.CreateDirectory(host);
        Put(src, "EplanMcp.Bridge.Eplan", "bin", "Release", "net481", "EplanMcp.Bridge.Eplan.dll");

        Assert.Null(AddinLocation.Find(host, null, out IReadOnlyList<string> searched));
        Assert.Equal(
            [Path.Combine(host, "bridge", "net481", "EplanMcp.Bridge.Eplan.dll"), Path.Combine(src, "EplanMcp.Bridge.Eplan", "bin", "Debug", "net481", "EplanMcp.Bridge.Eplan.dll")],
            searched);
    }

    [Fact]
    public void Find_OutsideBothLayouts_LooksOnlyNextToTheHost()
    {
        string other = Path.Combine(_root, "tools", "bin", "Release", "net10.0");
        Directory.CreateDirectory(other);

        Assert.Null(AddinLocation.Find(other, null, out IReadOnlyList<string> searched));
        Assert.Equal([Path.Combine(other, "bridge", "net481", "EplanMcp.Bridge.Eplan.dll")], searched);
    }

    [Fact]
    public void Find_InAnInstalledAppWithPerVersionBuilds_IsTheBuildOfThatVersion()
    {
        string app = Path.Combine(_root, "EplanMcp", "app");
        string build2025 = Put(app, "bridge", "net481", "eplan2025", "EplanMcp.Bridge.Eplan.dll");
        string build2026 = Put(app, "bridge", "net481", "eplan2026", "EplanMcp.Bridge.Eplan.dll");

        Assert.Equal(new AddinLocation(build2025, Path.Combine(app, "bridge")), AddinLocation.Find(app, "2025.0.3.23616", out _));
        Assert.Equal(new AddinLocation(build2026, Path.Combine(app, "bridge")), AddinLocation.Find(app, "2026.0.3.25421", out _));
    }

    [Fact]
    public void Find_WithoutTheBuildOfThatVersion_IsTheSingleBuildOfEarlierReleases_NeverAnotherVersionsBuild()
    {
        string app = Path.Combine(_root, "EplanMcp", "app");
        Put(app, "bridge", "net481", "eplan2026", "EplanMcp.Bridge.Eplan.dll");

        Assert.Null(AddinLocation.Find(app, "2025.0.3.23616", out IReadOnlyList<string> searched));
        Assert.Equal(
            [Path.Combine(app, "bridge", "net481", "eplan2025", "EplanMcp.Bridge.Eplan.dll"), Path.Combine(app, "bridge", "net481", "EplanMcp.Bridge.Eplan.dll")],
            searched);

        string single = Put(app, "bridge", "net481", "EplanMcp.Bridge.Eplan.dll");
        Assert.Equal(new AddinLocation(single, Path.Combine(app, "bridge")), AddinLocation.Find(app, "2025.0.3.23616", out _));
    }

    [Fact]
    public void Find_InADevelopmentBuildForAVersion_IsThatVersionsOutputFolder()
    {
        string src = Path.Combine(_root, "repo", "src");
        string host = Path.Combine(src, "EplanMcp.Host", "bin", "Release", "net10.0");
        Directory.CreateDirectory(host);
        string dll = Put(src, "EplanMcp.Bridge.Eplan", "bin", "Release", "net481", "eplan2025", "EplanMcp.Bridge.Eplan.dll");

        Assert.Equal(new AddinLocation(dll, Path.GetDirectoryName(dll)!), AddinLocation.Find(host, "2025.0.3.23616", out _));
        Assert.Null(AddinLocation.Find(host, "2026.0.3.25421", out _));
    }

    [Fact]
    public void Any_FindsTheSingleBuildOrABuildForSomeVersion()
    {
        string app = Path.Combine(_root, "EplanMcp", "app");
        Directory.CreateDirectory(app);
        Assert.False(AddinLocation.Any(app, out IReadOnlyList<string> searched));
        Assert.Equal([Path.Combine(app, "bridge", "net481", "EplanMcp.Bridge.Eplan.dll")], searched);

        Put(app, "bridge", "net481", "eplan2025", "EplanMcp.Bridge.Eplan.dll");
        Assert.True(AddinLocation.Any(app, out _));
    }

    [Theory]
    [InlineData("2025.0.3.23616", "eplan2025")]
    [InlineData("2026", "eplan2026")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("20250.1", null)]
    [InlineData("v2025.0.3", null)]
    public void VersionFolder_IsEplanAndTheYear(string? version, string? folder) =>
        Assert.Equal(folder, AddinLocation.VersionFolder(version));

    private static string Put(params string[] parts)
    {
        string file = Path.Combine(parts);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "dll");
        return file;
    }
}
