using System.Runtime.CompilerServices;
using System.Text.Json;
using AwesomeAssertions;

namespace Brua.Web.Tests;

/// <summary>The step before the selection rule: how wide a picture is drawn in its box (fixtures/sizing).</summary>
public class SizingTests
{
    private sealed record Box(double Width, double? Height);
    private sealed record Fixture(string Description, AssetResponse Asset, Box Box, string? ObjectFit, double Expected);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static TheoryData<string> FixtureNames() =>
        new(Directory.GetFiles(FixturesDirectory(), "*.json").Select(Path.GetFileName).Order()!);

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void Every_shared_sizing_fixture_draws_at_the_expected_width(string name)
    {
        // The fixtures give only the asset's size and fit; the rest of the contract isn't part of the rule.
        var raw = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixturesDirectory(), name))).RootElement;
        var asset = raw.GetProperty("asset");
        var fixture = new Fixture(
            raw.GetProperty("description").GetString()!,
            new AssetResponse("a", 1, "https://cdn.test/a/1", asset.GetProperty("width").GetInt32(), asset.GetProperty("height").GetInt32(), "", null, [],
                asset.TryGetProperty("objectFit", out var fit) ? fit.GetString() : null),
            raw.GetProperty("box").Deserialize<Box>(Json)!,
            raw.TryGetProperty("objectFit", out var overrideFit) ? overrideFit.GetString() : null,
            raw.GetProperty("expected").GetDouble());

        Sizing.TargetWidth(fixture.Asset, fixture.Box.Width, fixture.Box.Height, fixture.ObjectFit).Should().Be(fixture.Expected, fixture.Description);
    }

    [Fact]
    public void The_img_draws_with_the_assets_fill_an_override_wins_and_none_leaves_it_to_the_page()
    {
        var photo = Sample.Photo with { ObjectFit = "cover", ObjectPosition = "50% 35%" };

        Picture.Html(photo).Should().Contain("""style="object-fit: cover; object-position: 50% 35%" """.TrimEnd());
        Picture.Html(photo, new PictureOptions(ObjectFit: "contain", ObjectPosition: "0% 50%")).Should().Contain("object-fit: contain; object-position: 0% 50%");
        Picture.Html(photo, new PictureOptions(ObjectFit: "none")).Should().NotContain("style=");
        Picture.Html(Sample.Photo).Should().NotContain("style=", "a response from before the fill draws as it always has");
        Picture.Html(photo with { ObjectFit = "fill; color: red", ObjectPosition = "top" }).Should().Contain("object-fit: contain; object-position: 50% 50%");
    }

    private static string FixturesDirectory([CallerFilePath] string path = "")
    {
        var tests = Path.GetDirectoryName(path)!;
        return new[] { Path.Combine(tests, "..", "fixtures", "sizing"), Path.Combine(tests, "..", "..", "..", "fixtures", "sizing") }
            .Select(Path.GetFullPath)
            .First(Directory.Exists);
    }
}
