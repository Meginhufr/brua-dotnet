using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Brua.Web;

/// <summary>Where an app reads its organization's assets by id: the edge, with no key and no backend (SPEC.md 5.3).</summary>
/// <param name="Base">The organization's asset prefix on the edge, such as <c>https://cdn.brua.dev/assets/{org}</c>.</param>
/// <param name="Lang">The language for alt text and language-specific files; left out, the current UI culture decides.</param>
/// <param name="MaxAge">How long a fetched asset is reused before it is asked for again; by default the edge's max-age.</param>
public sealed record BruaOptions(string Base, string? Lang = null, TimeSpan? MaxAge = null);

/// <summary>
/// Reads the current release of an asset from the edge by its id: the <see cref="AssetResponse"/> at <c>{base}/{id}.json</c>.
/// Every release read is remembered: answered from memory for the edge's minute, so a page with a hundred references
/// makes one request per asset, read again after that, and kept when the edge can't be reached, so a merge lands
/// within the minute and a page still renders its pictures when the edge is down. One per app, through
/// <see cref="BruaServiceCollectionExtensions.AddBrua"/>.
/// </summary>
public sealed class BruaClient(HttpClient http, BruaOptions options, TimeProvider? time = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan EdgeMinute = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<Entry>>> _reads = new();
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly string _base = options.Base.TrimEnd('/');

    /// <summary>
    /// The JSON URL of the asset's current release. Without <see cref="BruaOptions.Lang"/> it carries the current UI
    /// culture (the request's under ASP.NET localization, the browser's in WebAssembly), so each language is its own
    /// entry here.
    /// </summary>
    public Uri Url(string id)
    {
        var lang = options.Lang ?? CultureInfo.CurrentUICulture.Name;
        return new($"{_base}/{Uri.EscapeDataString(id)}.json{(lang.Length > 0 ? $"?lang={Uri.EscapeDataString(lang)}" : "")}");
    }

    /// <summary>
    /// The asset's current release, or null when the id is unknown or nothing is released. Reads of one id at the same
    /// time share a request. Throws only when the edge can't be reached and no release of the id is known.
    /// </summary>
    public async Task<AssetResponse?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var url = Url(id).ToString();
        if (_entries.TryGetValue(url, out var known) && _time.GetUtcNow() < known.FreshUntil) return known.Asset;

        var read = _reads.GetOrAdd(url, key => new Lazy<Task<Entry>>(() => ReadAsync(key)));
        try
        {
            return (await read.Value.WaitAsync(cancellationToken)).Asset;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            if (_entries.TryGetValue(url, out var kept)) return kept.Asset;
            throw;
        }
    }

    /// <summary>The last release of the id this client has read, however old, or null when it hasn't read one.</summary>
    public AssetResponse? CachedAsset(string id) => _entries.TryGetValue(Url(id).ToString(), out var entry) ? entry.Asset : null;

    /// <summary>Reads these ids at the same time, so a page's pictures render without waiting for each in turn.</summary>
    public async Task PrefetchAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(ids.Distinct().Select(async id =>
        {
            try { await GetAsync(id, cancellationToken); }
            catch (HttpRequestException) { }
        }));
    }

    /// <summary>
    /// Reads the release at this URL, fresh for the options' lifetime, else the response's max-age, else a minute, and
    /// keeps it. The read leaves the shared reads when it ends, whoever is still waiting for it.
    /// </summary>
    private async Task<Entry> ReadAsync(string url)
    {
        try
        {
            using var response = await http.GetAsync(url);
            var freshFor = options.MaxAge ?? response.Headers.CacheControl?.MaxAge ?? EdgeMinute;
            var freshUntil = _time.GetUtcNow() + freshFor;
            Entry entry;
            if (response.StatusCode == HttpStatusCode.NotFound) entry = new Entry(null, freshUntil);
            else
            {
                response.EnsureSuccessStatusCode();
                entry = new Entry(await response.Content.ReadFromJsonAsync<AssetResponse>(Json), freshUntil);
            }
            _entries[url] = entry;
            return entry;
        }
        finally
        {
            _reads.TryRemove(url, out _);
        }
    }

    private sealed record Entry(AssetResponse? Asset, DateTimeOffset FreshUntil);
}

public static class BruaServiceCollectionExtensions
{
    /// <summary>Registers the app's <see cref="BruaClient"/>, which <c>BruaPicture</c> uses when given an id.</summary>
    public static IServiceCollection AddBrua(this IServiceCollection services, BruaOptions options) =>
        services.AddSingleton(_ => new BruaClient(new HttpClient(), options));
}
