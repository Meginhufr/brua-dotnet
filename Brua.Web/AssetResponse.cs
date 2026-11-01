namespace Brua.Web;

/// <summary>
/// The asset contract (SPEC.md 5): the HTML picture model as JSON. Plain records, so a service's own
/// JSON with an asset embedded deserializes with System.Text.Json's web defaults and nothing else.
/// </summary>
/// <param name="Id">The asset's id in its project.</param>
/// <param name="Version">The released version the response describes.</param>
/// <param name="Url">The single URL: negotiates format, density, color scheme and language from the request.</param>
/// <param name="Width">The width in CSS pixels at 1x.</param>
/// <param name="Height">The height in CSS pixels at 1x.</param>
/// <param name="Alt">The alt text in the resolved language; empty for a decorative asset.</param>
/// <param name="Lang">The alt text's language, when the asset has one.</param>
/// <param name="Variants">Every file of the version, each with the conditions it is for.</param>
public sealed record AssetResponse(
    string Id,
    int Version,
    string Url,
    int Width,
    int Height,
    string Alt,
    string? Lang,
    IReadOnlyList<AssetVariant> Variants);

/// <summary>Value fields are plain strings, not enums, so new values from the API never break decoding (SPEC.md 14).</summary>
/// <param name="Type">The media type of the file.</param>
/// <param name="Url">The file's URL on the edge.</param>
/// <param name="PrefersColorScheme">"light" or "dark" when the file is for one scheme; otherwise for both.</param>
/// <param name="PrefersReducedMotion">"reduce" on the still of an animation.</param>
/// <param name="Lang">The language the file is for; otherwise for every language.</param>
/// <param name="Scale">The density of a fixed-size file: 1, 2 or 3.</param>
/// <param name="PixelWidth">The width of a flexible file in pixels.</param>
public sealed record AssetVariant(
    string Type,
    string Url,
    string? PrefersColorScheme = null,
    string? PrefersReducedMotion = null,
    string? Lang = null,
    int? Scale = null,
    int? PixelWidth = null);

/// <summary>What the selection rule needs to know about the device and the view (SPEC.md 7.3).</summary>
/// <param name="SupportedTypes">The media types the client renders, best first; the order breaks ties.</param>
/// <param name="ColorScheme">"light" or "dark".</param>
/// <param name="ReducedMotion">Whether the person asked for less motion; stills are then chosen over animations.</param>
/// <param name="Scale">The device's pixel density.</param>
/// <param name="TargetWidth">The width the asset is shown at in CSS pixels, for flexible assets; left out, the largest file wins.</param>
public sealed record ClientState(
    IReadOnlyList<string> SupportedTypes,
    string ColorScheme = ClientState.Light,
    bool ReducedMotion = false,
    double Scale = 1,
    double? TargetWidth = null)
{
    /// <summary>The light color scheme.</summary>
    public const string Light = "light";

    /// <summary>The dark color scheme.</summary>
    public const string Dark = "dark";

    /// <summary>Formats every current browser and .NET image stack handles.</summary>
    public static readonly IReadOnlyList<string> DefaultTypes = ["image/avif", "image/webp", "image/png", "image/jpeg", "image/svg+xml"];

    /// <summary>A light, 1x client that renders every raster format, for servers and defaults.</summary>
    public static ClientState Default { get; } = new(DefaultTypes);
}
