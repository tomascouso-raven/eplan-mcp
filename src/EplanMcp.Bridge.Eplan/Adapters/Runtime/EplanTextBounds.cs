using Eplan.EplApi.Base;
using Eplan.EplApi.DataModel.Graphics;

namespace EplanMcp.Bridge.Eplan.Adapters;

/// <summary>
/// The outline of a property text in page coordinates. <c>PropertyPlacement.GetBoundingBox(Boolean)</c> exists from
/// EPLAN 2026; the 2025 API has only <c>GetBoundingBox()</c> (catalog/eplan-2025.0.3; found by a build against the
/// 2025.0.3 assemblies reported in asckye/eplan-mcp#664). Each EPLAN version has its own add-in build
/// (docs/design/2026-11-19-per-version-bridge-builds.md), so the overload is chosen at compile time.
/// </summary>
internal static class EplanTextBounds
{
    /// <summary>
    /// A87: <c>GetBoundingBox(false)</c> is absolute and the parameterless <c>GetBoundingBox()</c> is relative to the
    /// owner. Before EPLAN 2026 the owner's location is added, as the location read does for the location; A87 verified
    /// that sum on 2026.
    /// </summary>
    internal static PointD[] Of(PropertyPlacement placement)
    {
#if EPLAN2026_OR_GREATER
        return placement.GetBoundingBox(false);
#else
        PointD origin = placement.Parent is global::Eplan.EplApi.DataModel.Placement owner ? owner.Location : new PointD(0, 0);
        return [.. placement.GetBoundingBox().Select(corner => new PointD(corner.X + origin.X, corner.Y + origin.Y))];
#endif
    }
}
