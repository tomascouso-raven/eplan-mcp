namespace EplanMcp.Coverage.Tests;

/// <summary>
/// The catalog folder the API generators read: the default catalog unless a render for another EPLAN version runs, and
/// the default again afterwards, also when the render fails (docs/design/2026-11-19-per-version-bridge-builds.md).
/// </summary>
[Trait("Size", "Small")]
public sealed class CatalogFolderTests
{
    [Fact]
    public void Relative_WithoutARender_IsTheDefaultCatalogAndTheCacheIsUnscoped()
    {
        Assert.Equal(CatalogFolder.Default, CatalogFolder.Relative);
        Assert.Null(CatalogFolder.Override);
        Assert.Equal(string.Empty, CatalogFolder.CacheScope);
    }

    [Fact]
    public void With_UsesTheGivenCatalogInsideAndRestoresTheOuterOneAfterwards()
    {
        (string Inner, string Scope, string Nested, string AfterNested) seen = CatalogFolder.With("catalog/eplan-2025.0.3", () =>
            (CatalogFolder.Relative, CatalogFolder.CacheScope,
             CatalogFolder.With("catalog/eplan-2024.0.1", () => CatalogFolder.Relative), CatalogFolder.Relative));

        Assert.Equal(("catalog/eplan-2025.0.3", "catalog/eplan-2025.0.3|", "catalog/eplan-2024.0.1", "catalog/eplan-2025.0.3"), seen);
        Assert.Equal(CatalogFolder.Default, CatalogFolder.Relative);
    }

    [Fact]
    public void With_WhenTheRenderThrows_RestoresTheDefaultCatalog()
    {
        Assert.Throws<InvalidOperationException>(() => CatalogFolder.With<int>("catalog/eplan-2025.0.3", () => throw new InvalidOperationException()));

        Assert.Equal(CatalogFolder.Default, CatalogFolder.Relative);
    }
}
