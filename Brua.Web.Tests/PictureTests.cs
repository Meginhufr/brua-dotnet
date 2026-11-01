using AwesomeAssertions;

namespace Brua.Web.Tests;

public class PictureTests
{
    [Fact]
    public void Fixed_asset_offers_dark_and_light_sources_by_density_with_the_1x_png_as_fallback()
    {
        var html = Picture.Html(Sample.Icon);

        html.Should().StartWith("<picture>").And.EndWith("</picture>");
        html.Should().Contain("<source type=\"image/avif\" media=\"(prefers-color-scheme: dark)\" srcset=\"https://cdn.example/icon/dark@2x.avif 2x\">");
        html.Should().Contain("<source type=\"image/avif\" srcset=\"https://cdn.example/icon/light@1x.avif 1x, https://cdn.example/icon/light@2x.avif 2x\">");
        html.Should().Contain("<img src=\"https://cdn.example/icon/light@1x.png\" srcset=\"https://cdn.example/icon/light@1x.png 1x, https://cdn.example/icon/light@2x.png 2x\" width=\"24\" height=\"24\" alt=\"Settings\" lang=\"en\" loading=\"lazy\" decoding=\"async\">");
        html.Should().NotContain("sizes=", "fixed assets are chosen by density, not by width");
    }

    [Fact]
    public void Flexible_asset_offers_a_width_ladder_with_sizes_and_the_smallest_jpeg_as_fallback()
    {
        var html = Picture.Html(Sample.Photo, new PictureOptions(Sizes: "(min-width: 60em) 50vw, 100vw", Class: "hero"));

        html.Should().Contain("<source type=\"image/avif\" srcset=\"https://cdn.example/hero/320.avif 320w, https://cdn.example/hero/640.avif 640w, https://cdn.example/hero/1280.avif 1280w\" sizes=\"(min-width: 60em) 50vw, 100vw\">");
        html.Should().Contain("<img src=\"https://cdn.example/hero/320.jpg\" srcset=\"https://cdn.example/hero/320.jpg 320w, https://cdn.example/hero/640.jpg 640w, https://cdn.example/hero/1280.jpg 1280w\" sizes=\"(min-width: 60em) 50vw, 100vw\" width=\"1280\" height=\"720\" alt=\"A harbour at dusk\" loading=\"lazy\" decoding=\"async\" class=\"hero\">");
        html.Should().NotContain("prefers-color-scheme");
    }

    [Fact]
    public void Sizes_defaults_to_the_assets_width()
    {
        Picture.Model(Sample.Photo).Img.Sizes.Should().Be("1280px");
    }

    [Fact]
    public void Attribute_values_are_encoded()
    {
        var asset = Sample.Photo with { Alt = "Tom & Jerry's \"boat\"" };

        Picture.Html(asset).Should().Contain("alt=\"Tom &amp; Jerry&#39;s &quot;boat&quot;\"");
    }

    [Fact]
    public void Animations_render_their_still()
    {
        var spinner = Sample.Icon with
        {
            Variants =
            [
                new("application/json+lottie", "https://cdn.example/spinner/light.json"),
                new("image/png", "https://cdn.example/spinner/light@2x.png", PrefersReducedMotion: "reduce", Scale: 2),
                new("image/gif", "https://cdn.example/spinner/light.gif"),
            ],
        };

        var model = Picture.Model(spinner);

        model.Sources.Should().BeEmpty();
        model.Img.Src.Should().Be("https://cdn.example/spinner/light@2x.png");
    }
}
