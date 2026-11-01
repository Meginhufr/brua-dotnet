using System.Runtime.CompilerServices;
using System.Text.Json;
using AwesomeAssertions;

namespace Brua.Web.Tests;

/// <summary>Every SDK runs the shared fixtures in fixtures/selection; the rule is written out in fixtures/README.md.</summary>
public class FixtureTests
{
    private sealed record Fixture(string Description, AssetResponse Asset, ClientState Client, string Expected);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static TheoryData<string> FixtureNames() =>
        new(Directory.GetFiles(FixturesDirectory(), "*.json").Select(Path.GetFileName).Order()!);

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void Every_shared_fixture_selects_the_expected_url(string name)
    {
        var fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.Combine(FixturesDirectory(), name)), Json)!;

        VariantSelector.Url(fixture.Asset, fixture.Client).Should().Be(fixture.Expected, fixture.Description);
    }

    [Fact]
    public void Fields_added_to_the_contract_later_are_ignored()
    {
        const string json = """{"id":"a","version":1,"url":"https://cdn.test/a/1","width":1,"height":1,"alt":"","futureField":true,"variants":[{"type":"image/png","url":"https://cdn.test/a/1/1.png","newSelector":"x"}]}""";

        var asset = JsonSerializer.Deserialize<AssetResponse>(json, Json)!;

        asset.Variants.Should().ContainSingle().Which.Url.Should().Be("https://cdn.test/a/1/1.png");
        asset.Lang.Should().BeNull();
    }

    /// <summary>The shared fixtures: beside the package in its own repository, two levels up in the platform's.</summary>
    private static string FixturesDirectory([CallerFilePath] string path = "")
    {
        var tests = Path.GetDirectoryName(path)!;
        return new[] { Path.Combine(tests, "..", "fixtures", "selection"), Path.Combine(tests, "..", "..", "..", "fixtures", "selection") }
            .Select(Path.GetFullPath)
            .First(Directory.Exists);
    }
}
