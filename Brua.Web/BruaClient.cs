using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Brua.Web;

/// <summary>Where an app reads its organization's assets by id: the edge, with no key and no backend (SPEC.md 5.3).</summary>
/// <param name="Base">The organization's asset prefix on the edge, such as <c>https://cdn.brua.dev/assets/{org}</c>.</param>
/// <param name="Lang">The language for alt text and language-specific files; left out, the request's Accept-Language decides.</param>
/// <param name="MaxAge">How long a fetched asset is reused before it is asked for again; the edge's own minute by default.</param>
public sealed record BruaOptions(string Base, string? Lang = null, TimeSpan? MaxAge = null);

/// <summary>
/// Reads the current release of an asset from the edge by its id: the <see cref="AssetResponse"/> at <c>{base}/{id}.json</c>,
/// kept for a minute so a page with a hundred references makes one request per asset, and a merge still lands within
/// the minute. One per app, through <see cref="BruaServiceCollectionExtensions.AddBrua"/>.
/// </summary>
public sealed class BruaClient(HttpClient http, BruaOptions options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, Held> _held = new();
    private readonly TimeSpan _maxAge = options.MaxAge ?? TimeSpan.FromSeconds(60);
    private readonly string _base = options.Base.TrimEnd('/');
    private readonly string _query = options.Lang is { } lang ? $"?lang={Uri.EscapeDataString(lang)}" : "";

    /// <summary>The JSON URL of the asset's current release.</summary>
    public Uri Url(string id) => new($"{_base}/{Uri.EscapeDataString(id)}.json{_query}");

    /// <summary>The asset's current release, or null when the id is unknown or nothing is released.</summary>
    public Task<AssetResponse?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var held = _held.AddOrUpdate(id,
            _ => new Held(now, FetchAsync(id)),
            (_, have) => have.At + _maxAge > now && !have.Result.IsFaulted ? have : new Held(now, FetchAsync(id)));
        return held.Result.WaitAsync(cancellationToken);
    }

    private async Task<AssetResponse?> FetchAsync(string id)
    {
        using var response = await http.GetAsync(Url(id));
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AssetResponse>(Json);
    }

    /// <summary>One fetch shared by everyone who asks within its lifetime; a failure is asked again next time.</summary>
    private sealed record Held(DateTimeOffset At, Task<AssetResponse?> Result);
}

public static class BruaServiceCollectionExtensions
{
    /// <summary>Registers the app's <see cref="BruaClient"/>, which <c>BruaPicture</c> uses when given an id.</summary>
    public static IServiceCollection AddBrua(this IServiceCollection services, BruaOptions options) =>
        services.AddSingleton(_ => new BruaClient(new HttpClient(), options));
}
