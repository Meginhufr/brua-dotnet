namespace Brua.Web.Tests;

/// <summary>The same two assets the web SDK's tests use: an icon with a dark override, and a photo with a width ladder.</summary>
public static class Sample
{
    public static readonly AssetResponse Icon = new(
        "icon", 3, "https://cdn.example/assets/icon/3", 24, 24, "Settings", "en",
        [
            new("image/png", "https://cdn.example/icon/light@1x.png", Scale: 1),
            new("image/png", "https://cdn.example/icon/light@2x.png", Scale: 2),
            new("image/avif", "https://cdn.example/icon/light@1x.avif", Scale: 1),
            new("image/avif", "https://cdn.example/icon/light@2x.avif", Scale: 2),
            new("image/png", "https://cdn.example/icon/dark@1x.png", PrefersColorScheme: "dark", Scale: 1),
            new("image/png", "https://cdn.example/icon/dark@2x.png", PrefersColorScheme: "dark", Scale: 2),
            new("image/avif", "https://cdn.example/icon/dark@2x.avif", PrefersColorScheme: "dark", Scale: 2),
        ]);

    public static readonly AssetResponse Photo = new(
        "hero", 1, "https://cdn.example/assets/hero/1", 1280, 720, "A harbour at dusk", null,
        [
            new("image/jpeg", "https://cdn.example/hero/320.jpg", PixelWidth: 320),
            new("image/jpeg", "https://cdn.example/hero/640.jpg", PixelWidth: 640),
            new("image/jpeg", "https://cdn.example/hero/1280.jpg", PixelWidth: 1280),
            new("image/avif", "https://cdn.example/hero/320.avif", PixelWidth: 320),
            new("image/avif", "https://cdn.example/hero/640.avif", PixelWidth: 640),
            new("image/avif", "https://cdn.example/hero/1280.avif", PixelWidth: 1280),
        ]);
}
