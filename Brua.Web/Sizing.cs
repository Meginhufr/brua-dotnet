using System.Globalization;
using System.Text.RegularExpressions;

namespace Brua.Web;

/// <summary>
/// How a picture fills its box, and how wide that draws it: the step before the selection rule, so a cover picture
/// gets a file sharp enough for the size it is drawn at (fixtures/sizing).
/// </summary>
public static partial class Sizing
{
    [GeneratedRegex("^(100|[1-9]?[0-9])% (100|[1-9]?[0-9])%$")]
    private static partial Regex Position();

    /// <summary>The fit to draw with: the override, else the asset's, else "contain". Unknown values are "contain".</summary>
    public static string ObjectFitOf(AssetResponse asset, string? overrideFit = null) =>
        (overrideFit ?? asset.ObjectFit) == "cover" ? "cover" : "contain";

    /// <summary>Where the picture sits in its box, as "x% y%": the override, else the asset's, else the centre.</summary>
    public static string ObjectPositionOf(AssetResponse asset, string? overridePosition = null) =>
        (overridePosition ?? asset.ObjectPosition) is { } position && Position().IsMatch(position) ? position : "50% 50%";

    /// <summary>
    /// How wide the picture is drawn in a box of this size, in CSS pixels, for <see cref="ClientState.TargetWidth"/>.
    /// Contain, or a box without a height yet, draws it at the box width; cover at the larger of the box width and the
    /// box height times the asset's proportions.
    /// </summary>
    public static double TargetWidth(AssetResponse asset, double boxWidth, double? boxHeight = null, string? objectFit = null)
    {
        if (ObjectFitOf(asset, objectFit) != "cover" || boxHeight is not > 0 || asset.Height <= 0) return boxWidth;
        return Math.Max(boxWidth, boxHeight.Value * asset.Width / asset.Height);
    }

    /// <summary>"object-fit: cover; object-position: 50% 35%", for an img's style.</summary>
    internal static string Style(string fit, string position) =>
        string.Create(CultureInfo.InvariantCulture, $"object-fit: {fit}; object-position: {position}");
}
