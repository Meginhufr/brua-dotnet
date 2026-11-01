using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Brua.Web.Tests;

public class BruaClientTests
{
    private const string Base = "https://cdn.example/assets/0f8fad5b-d9cb-469f-a165-70867728950e";

    /// <summary>Answers the icon's URL with its JSON, anything else with 404, and remembers what was asked for.</summary>
    private sealed class Edge : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            var known = request.RequestUri.AbsolutePath.EndsWith("/icon.json");
            return Task.FromResult(known
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(Sample.Icon, new JsonSerializerOptions(JsonSerializerDefaults.Web)), null, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    [Fact]
    public async Task An_id_is_read_from_the_edge_once_a_minute_and_an_unknown_one_is_null()
    {
        var edge = new Edge();
        var client = new BruaClient(new HttpClient(edge), new BruaOptions(Base + "/", Lang: "nb"));

        var first = await client.GetAsync("icon");
        var again = await client.GetAsync("icon");
        var missing = await client.GetAsync("nothing-released");

        first.Should().BeEquivalentTo(Sample.Icon);
        again.Should().BeSameAs(first);
        missing.Should().BeNull();
        edge.Requests.Should().Equal($"{Base}/icon.json?lang=nb", $"{Base}/nothing-released.json?lang=nb");
    }

    [Fact]
    public async Task After_its_lifetime_the_asset_is_asked_for_again()
    {
        var edge = new Edge();
        var client = new BruaClient(new HttpClient(edge), new BruaOptions(Base, MaxAge: TimeSpan.Zero));

        await client.GetAsync("icon");
        await client.GetAsync("icon");

        edge.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task The_picture_renders_from_an_id_through_the_registered_client()
    {
        var edge = new Edge();
        await using var services = new ServiceCollection().AddLogging()
            .AddSingleton(new BruaClient(new HttpClient(edge), new BruaOptions(Base)))
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(BruaPicture.Id)] = "icon", ["class"] = "mark" });
            return (await renderer.RenderComponentAsync<BruaPicture>(parameters)).ToHtmlString();
        });

        html.Should().StartWith("<picture>").And.Contain("<img class=\"mark\"").And.Contain("alt=\"Settings\"");
        edge.Requests.Should().Equal($"{Base}/icon.json");
    }

    [Fact]
    public async Task Without_a_registered_client_an_id_is_a_configuration_error()
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        var render = () => renderer.Dispatcher.InvokeAsync(() =>
            renderer.RenderComponentAsync<BruaPicture>(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(BruaPicture.Id)] = "icon" })));

        await render.Should().ThrowAsync<InvalidOperationException>().WithMessage("*AddBrua*");
    }
}
