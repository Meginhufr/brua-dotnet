using System.Net;
using System.Text;

namespace Brua.Web;

/// <summary>How the picture is built.</summary>
/// <param name="Sizes">The sizes attribute for flexible assets; defaults to the asset's width in CSS pixels.</param>
/// <param name="Loading">The img's loading attribute: "lazy" (the default) or "eager".</param>
/// <param name="Class">A class for the img.</param>
/// <param name="ColorScheme">
/// "light" or "dark" to render that scheme's files only, for apps that switch themes themselves. Left out,
/// the dark files go behind a media query and the browser follows the system setting.
/// </param>
public sealed record PictureOptions(string? Sizes = null, string Loading = "lazy", string? Class = null, string? ColorScheme = null);

/// <summary>A source of the picture: one format, optionally one color scheme.</summary>
/// <param name="Type">The media type of the files in <paramref name="Srcset"/>.</param>
/// <param name="Media">The media query the source applies under, if any.</param>
/// <param name="Srcset">The srcset attribute: URLs with density or width descriptors.</param>
/// <param name="Sizes">The sizes attribute, present when the srcset uses width descriptors.</param>
public sealed record PictureSource(string Type, string? Media, string Srcset, string? Sizes);

/// <summary>The img fallback and its attributes.</summary>
/// <param name="Src">The file for browsers that use none of the sources.</param>
/// <param name="Srcset">Further densities or widths of the fallback format, if there are any.</param>
/// <param name="Sizes">The sizes attribute, present when the srcset uses width descriptors.</param>
/// <param name="Width">The asset's width in CSS pixels, so the box is reserved before the file loads.</param>
/// <param name="Height">The asset's height in CSS pixels.</param>
/// <param name="Alt">The alt text in the resolved language; empty for a decorative asset.</param>
/// <param name="Lang">The alt text's language, when it differs from the page's.</param>
/// <param name="Loading">The loading attribute.</param>
/// <param name="Class">The class attribute, if any.</param>
public sealed record PictureImage(
    string Src,
    string? Srcset,
    string? Sizes,
    int Width,
    int Height,
    string Alt,
    string? Lang,
    string Loading,
    string? Class)
{
    /// <summary>Images decode off the main thread.</summary>
    public string Decoding => "async";
}

/// <summary>What a picture element for the asset consists of; the HTML renderer and the component build from it.</summary>
/// <param name="Sources">The source elements, in order.</param>
/// <param name="Img">The img element.</param>
public sealed record PictureModel(IReadOnlyList<PictureSource> Sources, PictureImage Img);

/// <summary>
/// The picture for an asset (SPEC.md 7.2): dark-mode sources behind a media query, modern formats as
/// sources, and alt, lang, width and height on the img. The browser picks the variant, so this works
/// without client hints and switches live with the color scheme. Animations aren't rendered; their stills are.
/// </summary>
public static class Picture
{
    private const string Svg = "image/svg+xml";

    /// <summary>Formats offered as source elements, best first; anything else becomes the img fallback.</summary>
    private static readonly string[] SourceTypes = ["image/avif", "image/webp"];

    /// <summary>The sources and the img for the asset.</summary>
    public static PictureModel Model(AssetResponse asset, PictureOptions? options = null)
    {
        options ??= new PictureOptions();
        var images = asset.Variants.Where(v => v.Type.StartsWith("image/") && v.Type != "image/gif").ToList();
        if (images.Any(v => v.PrefersReducedMotion is null))
            images = images.Where(v => v.PrefersReducedMotion is null).ToList();

        var sizes = options.Sizes ?? $"{asset.Width}px";
        var dark = images.Where(v => v.PrefersColorScheme == "dark").ToList();
        var light = images.Where(v => v.PrefersColorScheme != "dark").ToList();

        // A given scheme stands alone; otherwise the dark files go behind a media query, and the img is light.
        (List<AssetVariant> Group, string? Media)[] groups = options.ColorScheme switch
        {
            null => [(dark, "(prefers-color-scheme: dark)"), (light, null)],
            "dark" when dark.Count > 0 => [(dark, null)],
            _ => [(light, null)],
        };

        var sources = new List<PictureSource>();
        foreach (var (group, media) in groups)
        {
            // An SVG fits every density, so it stands in for the rasters; in the img's own group it is the img.
            if (group.FirstOrDefault(v => v.Type == Svg) is { } svg)
            {
                if (media is not null) sources.Add(new PictureSource(Svg, media, svg.Url, null));
                continue;
            }
            foreach (var type in SourceTypes)
            {
                var variants = group.Where(v => v.Type == type).ToList();
                if (variants.Count == 0) continue;
                sources.Add(new PictureSource(type, media, Srcset(variants), NeedsSizes(variants) ? sizes : null));
            }
        }

        var own = groups[^1].Group;
        var fallback = own.FirstOrDefault(v => v.Type == Svg) is { } ownSvg ? [ownSvg] : own.Where(v => !SourceTypes.Contains(v.Type)).ToList();
        var smallest = fallback.OrderBy(v => v.Scale ?? v.PixelWidth ?? 0).FirstOrDefault();
        return new PictureModel(sources, new PictureImage(
            smallest?.Url ?? asset.Url,
            fallback.Count > 1 ? Srcset(fallback) : null,
            NeedsSizes(fallback) ? sizes : null,
            asset.Width,
            asset.Height,
            asset.Alt,
            asset.Lang,
            options.Loading,
            options.Class));
    }

    /// <summary>The picture as HTML, for Razor Pages, MVC views and emails rendered on the server.</summary>
    public static string Html(AssetResponse asset, PictureOptions? options = null)
    {
        var (sources, img) = Model(asset, options);
        var html = new StringBuilder("<picture>");
        foreach (var s in sources)
            html.Append(Tag("source", ("type", s.Type), ("media", s.Media), ("srcset", s.Srcset), ("sizes", s.Sizes)));
        html.Append(Tag("img",
            ("src", img.Src), ("srcset", img.Srcset), ("sizes", img.Sizes),
            ("width", img.Width.ToString()), ("height", img.Height.ToString()),
            ("alt", img.Alt), ("lang", img.Lang), ("loading", img.Loading), ("decoding", img.Decoding), ("class", img.Class)));
        return html.Append("</picture>").ToString();
    }

    private static string Srcset(IEnumerable<AssetVariant> variants) => string.Join(", ", variants.Select(v =>
        v.PixelWidth is { } w ? $"{v.Url} {w}w" : v.Scale is { } s ? $"{v.Url} {s}x" : v.Url));

    private static bool NeedsSizes(IEnumerable<AssetVariant> variants) => variants.Any(v => v.PixelWidth is not null);

    private static string Tag(string name, params (string Name, string? Value)[] attributes)
    {
        var rendered = new StringBuilder("<").Append(name);
        foreach (var (attribute, value) in attributes)
        {
            if (value is null) continue;
            rendered.Append(' ').Append(attribute).Append("=\"").Append(WebUtility.HtmlEncode(value)).Append('"');
        }
        return rendered.Append('>').ToString();
    }
}
