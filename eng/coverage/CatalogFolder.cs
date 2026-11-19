namespace EplanMcp.Coverage;

/// <summary>
/// The catalog folder the API index and the API dispatch generators read (docs/design/2026-11-19-per-version-bridge-builds.md,
/// phase 2). By default it is <c>catalog/eplan-2026.0.3</c>, the version the Bridge is scanned against, and every
/// committed file is derived from it. <see cref="With{T}"/> renders a file from another version's catalog instead, for
/// example the dispatcher of the EPLAN 2025 add-in build. Only the API selection follows the override: the tool mapping
/// and the coverage report describe the Host's tools and keep reading the scanned version's catalog. The caches of
/// what follows the override take <see cref="CacheScope"/> into their <see cref="RunCache"/> key.
/// </summary>
internal static class CatalogFolder
{
    /// <summary>The catalog the Bridge is scanned and the committed files are generated against.</summary>
    internal const string Default = "catalog/eplan-2026.0.3";

    private static readonly AsyncLocal<string?> _override = new();

    /// <summary>The repository-relative catalog folder of a render for another version, or null.</summary>
    internal static string? Override => _override.Value;

    /// <summary>Repository-relative folder of the catalog in use, without a trailing separator.</summary>
    internal static string Relative => _override.Value ?? Default;

    /// <summary>Key prefix for <see cref="RunCache"/>: empty for the default catalog, so its keys stay as before.</summary>
    internal static string CacheScope => _override.Value is { } folder ? folder + "|" : string.Empty;

    /// <summary>Runs <paramref name="render"/> with <paramref name="relativeFolder"/> as the catalog in use.</summary>
    internal static T With<T>(string relativeFolder, Func<T> render)
    {
        string? previous = _override.Value;
        _override.Value = relativeFolder;
        try
        {
            return render();
        }
        finally
        {
            _override.Value = previous;
        }
    }
}
