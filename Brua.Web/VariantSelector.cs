namespace Brua.Web;

/// <summary>The variant selection rule (SPEC.md 7.3), tested against the shared fixtures in fixtures/selection.</summary>
public static class VariantSelector
{
    private static readonly HashSet<string> KnownColorSchemes = ["light", "dark"];
    private static readonly HashSet<string> KnownReducedMotion = ["reduce"];

    /// <summary>The URL to load: the chosen variant's, or the asset's single URL when no variant fits.</summary>
    public static string Url(AssetResponse asset, ClientState client) => Select(asset, client)?.Url ?? asset.Url;

    /// <summary>The variant to load, or null when no file suits the client; the single URL still does.</summary>
    public static AssetVariant? Select(AssetResponse asset, ClientState client)
    {
        // 1. Usable: a supported type, and no value this client doesn't know.
        var candidates = asset.Variants
            .Where(v => client.SupportedTypes.Contains(v.Type)
                        && (v.PrefersColorScheme is null || KnownColorSchemes.Contains(v.PrefersColorScheme))
                        && (v.PrefersReducedMotion is null || KnownReducedMotion.Contains(v.PrefersReducedMotion)))
            .ToList();

        // 2. Motion.
        var reduced = candidates.Where(v => v.PrefersReducedMotion == "reduce").ToList();
        var moving = candidates.Where(v => v.PrefersReducedMotion is null).ToList();
        if (client.ReducedMotion)
        {
            if (reduced.Count > 0) candidates = reduced;
        }
        else if (moving.Count > 0)
        {
            candidates = moving;
        }

        // 3. Color scheme.
        candidates = FirstNonEmpty(
            candidates.Where(v => v.PrefersColorScheme == client.ColorScheme).ToList(),
            candidates.Where(v => v.PrefersColorScheme is null).ToList(),
            candidates.Where(v => v.PrefersColorScheme == "light").ToList(),
            candidates);

        // 4. Language.
        candidates = FirstNonEmpty(
            candidates.Where(v => v.Lang is not null && v.Lang == asset.Lang).ToList(),
            candidates.Where(v => v.Lang is null).ToList(),
            candidates);

        // 5. Type.
        var type = client.SupportedTypes.FirstOrDefault(t => candidates.Any(v => v.Type == t));
        candidates = candidates.Where(v => v.Type == type).ToList();
        if (candidates.Count == 0) return null;

        // 6. Size.
        if (candidates.Any(v => v.Scale is not null))
            return PickAtLeast(candidates, v => v.Scale ?? 1, client.Scale);
        if (candidates.Any(v => v.PixelWidth is not null))
        {
            var need = client.TargetWidth is { } width ? width * client.Scale : double.PositiveInfinity;
            return PickAtLeast(candidates, v => v.PixelWidth ?? 0, need);
        }
        return candidates[0];
    }

    private static List<AssetVariant> FirstNonEmpty(params List<AssetVariant>[] lists) =>
        lists.FirstOrDefault(list => list.Count > 0) ?? [];

    /// <summary>The smallest value at or above <paramref name="target"/>, else the largest.</summary>
    private static AssetVariant PickAtLeast(List<AssetVariant> variants, Func<AssetVariant, double> value, double target)
    {
        var sorted = variants.OrderBy(value).ToList();
        return sorted.FirstOrDefault(v => value(v) >= target) ?? sorted[^1];
    }
}
