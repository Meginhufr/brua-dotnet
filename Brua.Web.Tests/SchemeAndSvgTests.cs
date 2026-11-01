using AwesomeAssertions;

namespace Brua.Web.Tests;

public class SchemeAndSvgTests
{
    private static readonly AssetResponse Mark = new(
        "mark", 1, "https://cdn.example/assets/mark/1", 28, 28, "", null,
        [
            new("image/svg+xml", "https://cdn.example/mark/light.svg"),
            new("image/png", "https://cdn.example/mark/light@1x.png", Scale: 1),
            new("image/png", "https://cdn.example/mark/light@2x.png", Scale: 2),
            new("image/avif", "https://cdn.example/mark/light@2x.avif", Scale: 2),
            new("image/svg+xml", "https://cdn.example/mark/dark.svg", PrefersColorScheme: "dark"),
            new("image/png", "https://cdn.example/mark/dark@1x.png", PrefersColorScheme: "dark", Scale: 1),
            new("image/avif", "https://cdn.example/mark/dark@2x.avif", PrefersColorScheme: "dark", Scale: 2),
        ]);

    [Fact]
    public void An_svg_stands_in_for_every_raster_of_its_scheme()
    {
        var model = Picture.Model(Mark);

        model.Sources.Should().ContainSingle().Which.Should().Be(
            new PictureSource("image/svg+xml", "(prefers-color-scheme: dark)", "https://cdn.example/mark/dark.svg", null));
        model.Img.Src.Should().Be("https://cdn.example/mark/light.svg");
        model.Img.Srcset.Should().BeNull();
    }

    [Fact]
    public void A_given_scheme_renders_that_schemes_files_without_a_media_query()
    {
        var dark = Picture.Model(Mark, new PictureOptions(ColorScheme: "dark"));
        dark.Sources.Should().BeEmpty();
        dark.Img.Src.Should().Be("https://cdn.example/mark/dark.svg");

        var light = Picture.Model(Sample.Icon, new PictureOptions(ColorScheme: "light"));
        light.Sources.Should().ContainSingle().Which.Srcset.Should().Be("https://cdn.example/icon/light@1x.avif 1x, https://cdn.example/icon/light@2x.avif 2x");
        light.Sources[0].Media.Should().BeNull();
        light.Img.Src.Should().Be("https://cdn.example/icon/light@1x.png");

        var darkIcon = Picture.Model(Sample.Icon, new PictureOptions(ColorScheme: "dark"));
        darkIcon.Sources.Should().ContainSingle().Which.Srcset.Should().Be("https://cdn.example/icon/dark@2x.avif 2x");
        darkIcon.Img.Srcset.Should().Be("https://cdn.example/icon/dark@1x.png 1x, https://cdn.example/icon/dark@2x.png 2x");
    }

    [Fact]
    public void Dark_falls_back_to_light_when_the_asset_has_one_design()
    {
        var model = Picture.Model(Sample.Photo, new PictureOptions(ColorScheme: "dark"));

        model.Img.Src.Should().Be("https://cdn.example/hero/320.jpg");
        model.Sources.Should().ContainSingle().Which.Media.Should().BeNull();
    }
}
