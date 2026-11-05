using System.Globalization;
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

    /// <summary>
    /// Answers the icon's URL with its JSON, anything else with 404, with the edge's minute, and remembers what was
    /// asked for. <see cref="Down"/> makes every request fail as a lost connection.
    /// </summary>
    private sealed class Edge : HttpMessageHandler
    {
        private int _requests;
        public List<string> Requests { get; } = [];
        public bool Down { get; set; }
        public int Count => _requests;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            lock (Requests) Requests.Add(request.RequestUri!.ToString());
            await Task.Yield();
            if (Down) throw new HttpRequestException("No connection");
            var known = request.RequestUri!.AbsolutePath.EndsWith("/icon.json");
            var response = known
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(Sample.Icon, new JsonSerializerOptions(JsonSerializerDefaults.Web)), null, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
            response.Headers.CacheControl = new() { Public = true, MaxAge = TimeSpan.FromSeconds(60) };
            return response;
        }
    }

    /// <summary>A clock the test moves by hand.</summary>
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private static BruaClient Client(Edge edge, string? lang = "nb", TimeProvider? time = null) =>
        new(new HttpClient(edge), new BruaOptions(Base, Lang: lang), time);

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
        var clock = new Clock();
        var client = Client(edge, time: clock);

        client.CachedAsset("icon").Should().BeNull();
        await client.GetAsync("icon");
        clock.Advance(TimeSpan.FromSeconds(30));
        await client.GetAsync("icon");
        edge.Count.Should().Be(1);
        client.CachedAsset("icon")!.Alt.Should().Be(Sample.Icon.Alt);

        clock.Advance(TimeSpan.FromSeconds(31));
        await client.GetAsync("icon");
        edge.Count.Should().Be(2);
    }

    [Fact]
    public async Task A_lifetime_set_in_the_options_wins_over_the_edges()
    {
        var edge = new Edge();
        var client = new BruaClient(new HttpClient(edge), new BruaOptions(Base, MaxAge: TimeSpan.Zero));

        await client.GetAsync("icon");
        await client.GetAsync("icon");

        edge.Count.Should().Be(2);
    }

    [Fact]
    public async Task An_id_with_nothing_released_is_remembered_for_the_minute_too()
    {
        var edge = new Edge();
        var client = Client(edge);

        await client.GetAsync("nothing-released");
        await client.GetAsync("nothing-released");

        edge.Count.Should().Be(1);
    }

    [Fact]
    public async Task Reads_of_one_id_at_the_same_time_share_one_request()
    {
        var edge = new Edge();
        var client = Client(edge);

        var both = await Task.WhenAll(client.GetAsync("icon"), client.GetAsync("icon"));

        both[0].Should().BeSameAs(both[1]);
        edge.Count.Should().Be(1);
    }

    [Fact]
    public async Task A_stale_release_is_served_when_the_refresh_fails_and_without_one_the_error_comes_through()
    {
        var edge = new Edge();
        var clock = new Clock();
        var client = Client(edge, time: clock);
        await client.GetAsync("icon");

        edge.Down = true;
        clock.Advance(TimeSpan.FromMinutes(2));

        (await client.GetAsync("icon"))!.Alt.Should().Be(Sample.Icon.Alt);
        var neverRead = () => client.GetAsync("never-read");
        await neverRead.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Prefetch_reads_every_id_once_so_they_are_in_memory()
    {
        var edge = new Edge();
        var client = Client(edge);

        await client.PrefetchAsync(["icon", "icon", "nothing-released"]);

        client.CachedAsset("icon").Should().NotBeNull();
        edge.Count.Should().Be(2);
    }

    [Fact]
    public void Without_a_language_the_url_carries_the_current_ui_culture()
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("nb-NO");
            Client(new Edge(), lang: null).Url("icon").ToString().Should().Be($"{Base}/icon.json?lang=nb-NO");

            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Client(new Edge(), lang: null).Url("icon").ToString().Should().Be($"{Base}/icon.json");
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    [Fact]
    public async Task The_picture_renders_from_an_id_through_the_registered_client()
    {
        var edge = new Edge();
        await using var services = new ServiceCollection().AddLogging()
            .AddSingleton(new BruaClient(new HttpClient(edge), new BruaOptions(Base, Lang: "en")))
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(BruaPicture.Id)] = "icon", ["class"] = "mark" });
            return (await renderer.RenderComponentAsync<BruaPicture>(parameters)).ToHtmlString();
        });

        html.Should().StartWith("<picture>").And.Contain("<img class=\"mark\"").And.Contain("alt=\"Settings\"");
        edge.Requests.Should().Equal($"{Base}/icon.json?lang=en");
    }

    private static async Task<string> RenderIdAsync(Edge edge, string id, bool withFailure)
    {
        await using var services = new ServiceCollection().AddLogging()
            .AddSingleton(new BruaClient(new HttpClient(edge), new BruaOptions(Base, Lang: "en")))
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = new Dictionary<string, object?>
            {
                [nameof(BruaPicture.Id)] = id,
                [nameof(BruaPicture.Placeholder)] = (RenderFragment)(b => b.AddMarkupContent(0, "<i>loading</i>")),
            };
            if (withFailure)
                parameters[nameof(BruaPicture.Failure)] = (RenderFragment<BruaImageError>)(error => b => b.AddMarkupContent(0, error switch
                {
                    BruaImageError.NotReleased => "<b>not released</b>",
                    BruaImageError.Unreachable => "<b>unreachable</b>",
                    BruaImageError.File f => $"<b>file {f.Url}</b>",
                    _ => "",
                }));
            return (await renderer.RenderComponentAsync<BruaPicture>(ParameterView.FromDictionary(parameters))).ToHtmlString();
        });
    }

    [Fact]
    public async Task An_id_with_nothing_released_renders_the_failure_with_its_reason_or_else_the_placeholder()
    {
        (await RenderIdAsync(new Edge(), "nothing-released", withFailure: true)).Should().Be("<b>not released</b>");
        (await RenderIdAsync(new Edge(), "nothing-released", withFailure: false)).Should().Be("<i>loading</i>");
    }

    [Fact]
    public async Task An_edge_that_cant_be_reached_renders_the_failure_as_unreachable()
    {
        (await RenderIdAsync(new Edge { Down = true }, "icon", withFailure: true)).Should().Be("<b>unreachable</b>");
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
