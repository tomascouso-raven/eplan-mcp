using Eplan.EplApi.Base;

namespace EplanMcp.Bridge.Eplan.Adapters;

/// <summary>
/// Path variables resolved for a project where the platform can. <c>PathMap.SubstitutePath(String, String)</c> exists
/// from EPLAN 2026; the 2025 API has only <c>SubstitutePath(String)</c> (catalog/eplan-2025.0.3; reported on
/// 2025.0.3.23616 as a MissingMethodException that failed every page read). Each EPLAN version has its own add-in
/// build (docs/design/2026-11-19-per-version-bridge-builds.md), so the overload is chosen at compile time.
/// </summary>
internal static class EplanPaths
{
    /// <summary>
    /// <paramref name="path"/> with EPLAN's path variables replaced. Before EPLAN 2026 the project's own variables stay
    /// unresolved, as the platform leaves them.
    /// </summary>
    internal static string Substitute(string path, string projectPath)
    {
#if EPLAN2026_OR_GREATER
        return PathMap.SubstitutePath(path, projectPath);
#else
        _ = projectPath;
        return PathMap.SubstitutePath(path);
#endif
    }
}
