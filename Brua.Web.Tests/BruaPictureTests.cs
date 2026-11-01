using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Brua.Web.Tests;

public class BruaPictureTests
{
    [Fact]
    public async Task Renders_the_picture_with_extra_attributes_on_the_img()
    {
        var html = await Render(new Dictionary<string, object?>
        {
            [nameof(BruaPicture.Asset)] = Sample.Icon,
            ["class"] = "toolbar-icon",
            ["data-asset"] = "icon",
        });

        html.Should().StartWith("<picture>").And.EndWith("</picture>");
        html.Should().Contain("<source type=\"image/avif\" media=\"(prefers-color-scheme: dark)\" srcset=\"https://cdn.example/icon/dark@2x.avif 2x\"");
        html.Should().Contain("<source type=\"image/avif\" srcset=\"https://cdn.example/icon/light@1x.avif 1x, https://cdn.example/icon/light@2x.avif 2x\"");
        html.Should().Contain("<img class=\"toolbar-icon\" data-asset=\"icon\" src=\"https://cdn.example/icon/light@1x.png\"");
        html.Should().Contain("width=\"24\" height=\"24\" alt=\"Settings\" lang=\"en\" loading=\"lazy\" decoding=\"async\"");
        html.Should().NotContain("media=\"\"", "sources without a media query leave the attribute out");
    }

    [Fact]
    public async Task Sizes_and_loading_are_parameters()
    {
        var html = await Render(new Dictionary<string, object?>
        {
            [nameof(BruaPicture.Asset)] = Sample.Photo,
            [nameof(BruaPicture.Sizes)] = "100vw",
            [nameof(BruaPicture.Loading)] = "eager",
        });

        html.Should().Contain("sizes=\"100vw\"").And.Contain("loading=\"eager\"");
    }

    private static async Task<string> Render(Dictionary<string, object?> parameters)
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<BruaPicture>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
