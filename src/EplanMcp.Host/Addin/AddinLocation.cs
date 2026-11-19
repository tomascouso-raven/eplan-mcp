namespace EplanMcp.Host.Addin;

/// <summary>
/// The add-in DLL that install-addin registers, and the folder whose files lose their download mark first (M5 design
/// 4.2). Found next to the running eplanmcp.exe only, never through a setting or variable. Each EPLAN version has its
/// own add-in build (docs/design/2026-11-19-per-version-bridge-builds.md), in a folder <c>eplan&lt;year&gt;</c>; a
/// layout without that folder holds the single build of earlier releases, which is used for every version:
/// <list type="bullet">
/// <item>installed (architecture 7): <c>&lt;app&gt;\bridge\net481\eplan&lt;year&gt;\EplanMcp.Bridge.Eplan.dll</c>, else
/// <c>&lt;app&gt;\bridge\net481\EplanMcp.Bridge.Eplan.dll</c>, unmarking all of <c>&lt;app&gt;\bridge</c>;</item>
/// <item>development: the Host runs from <c>src\EplanMcp.Host\bin\&lt;configuration&gt;\&lt;tfm&gt;</c>, and the add-in of the
/// same configuration is <c>src\EplanMcp.Bridge.Eplan\bin\&lt;configuration&gt;\net481\eplan&lt;year&gt;\EplanMcp.Bridge.Eplan.dll</c>,
/// else the same without <c>eplan&lt;year&gt;</c>, unmarking that folder.</item>
/// </list>
/// The installed layout wins when both exist; there is no fallback to another configuration or another version's build.
/// </summary>
internal sealed record AddinLocation(string Dll, string UnblockFolder)
{
    /// <summary>The add-in's assembly name: what <c>/unregister</c> takes, and the pipe owner's DLL.</summary>
    public const string AssemblyName = "EplanMcp.Bridge.Eplan";

    public const string FileName = AssemblyName + ".dll";

    /// <summary>
    /// The add-in for EPLAN <paramref name="eplanVersion"/> (for example <c>2025.0.3.23616</c>) and the Host in
    /// <paramref name="hostDirectory"/>, or null; <paramref name="searched"/> lists where it looked. Without a version,
    /// only the single build of earlier releases is looked for.
    /// </summary>
    public static AddinLocation? Find(string hostDirectory, string? eplanVersion, out IReadOnlyList<string> searched)
    {
        string? folder = VersionFolder(eplanVersion);
        string bridge = Path.Combine(hostDirectory, "bridge");
        string installed = Path.Combine(bridge, "net481");
        string? development = DevelopmentFolder(hostDirectory);
        AddinLocation[] candidates =
        [
            .. folder is null ? [] : new[] { new AddinLocation(Path.Combine(installed, folder, FileName), bridge) },
            new AddinLocation(Path.Combine(installed, FileName), bridge),
            .. development is null || folder is null ? [] : new[] { new AddinLocation(Path.Combine(development, folder, FileName), Path.Combine(development, folder)) },
            .. development is null ? [] : new[] { new AddinLocation(Path.Combine(development, FileName), development) },
        ];
        searched = [.. candidates.Select(c => c.Dll)];
        return candidates.FirstOrDefault(c => File.Exists(c.Dll));
    }

    /// <summary>
    /// Whether any add-in build is next to the Host: the single build of earlier releases, or a build for some EPLAN
    /// version. <paramref name="searched"/> lists the folders looked in.
    /// </summary>
    public static bool Any(string hostDirectory, out IReadOnlyList<string> searched)
    {
        string? development = DevelopmentFolder(hostDirectory);
        string[] folders = [Path.Combine(hostDirectory, "bridge", "net481"), .. development is null ? [] : new[] { development }];
        searched = [.. folders.Select(f => Path.Combine(f, FileName))];
        return folders.Any(f => File.Exists(Path.Combine(f, FileName))
            || Directory.Exists(f) && Directory.EnumerateDirectories(f, "eplan*").Any(d => File.Exists(Path.Combine(d, FileName))));
    }

    /// <summary><c>eplan2025</c> for <c>2025.0.3.23616</c>, or null when the version does not start with a year.</summary>
    internal static string? VersionFolder(string? eplanVersion) =>
        eplanVersion is { Length: >= 4 } v && v.Take(4).All(char.IsAsciiDigit) && (v.Length == 4 || v[4] == '.') ? "eplan" + v[..4] : null;

    private static string? DevelopmentFolder(string hostDirectory)
    {
        var tfm = new DirectoryInfo(Path.TrimEndingDirectorySeparator(hostDirectory));
        DirectoryInfo? configuration = tfm.Parent;
        DirectoryInfo? bin = configuration?.Parent;
        DirectoryInfo? project = bin?.Parent;
        if (configuration is null || project?.Parent is not { } src
            || !bin!.Name.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || !project.Name.Equals("EplanMcp.Host", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.Combine(src.FullName, AssemblyName, "bin", configuration.Name, "net481");
    }
}
