# Brua for .NET

`Brua.Web` is the consumer side of Brua for .NET: the `AssetResponse` model, the variant selection rule, `<picture>` HTML and a Blazor component. Its only dependency is `Microsoft.AspNetCore.Components.Web`, so it works in every Blazor render mode (static server-side rendering, prerendering, Interactive Server, WebAssembly and Auto), inside MudBlazor or any other component library, and in Razor Pages, MVC views and emails rendered on the server.

```bash
dotnet add package Brua.Web
```

## From an id

Your backend passes asset ids on, and the app reads each asset's current release from the edge with no key (SPEC.md 5.3). Register the client once with your organization's asset prefix:

```csharp
builder.Services.AddBrua(new BruaOptions("https://cdn.brua.dev/assets/<your organization id>", Lang: "nb"));
```

`BruaClient.GetAsync(id)` reads `{base}/{id}.json` and remembers every release it has read: from memory for the edge's minute, so a page with a hundred references makes one request per asset, read again after that, and kept when the edge can't be reached. A merge in Brua reaches the page within the minute with no deploy of yours. It returns null when nothing is released under the id; `CachedAsset(id)` gives the last release read at once, and `PrefetchAsync(ids)` reads a page's ids together. Without `Lang`, the current UI culture picks the language: the request's under ASP.NET localization, the browser's in WebAssembly.

## Or embedded by your backend

A service that wants a frozen snapshot, for email, push or print, calls the delivery API and embeds the asset in its own response (SPEC.md 7.1). `AssetResponse` is a plain record and deserializes with System.Text.Json's web defaults, on its own or as a property of your own type:

```csharp
public record Page(string Title, AssetResponse Hero);

var page = await http.GetFromJsonAsync<Page>("/api/pages/front");
```

## Blazor

```razor
@using Brua.Web

<BruaPicture Id="@page.HeroId" Sizes="(min-width: 60em) 50vw, 100vw" class="hero" />
<BruaPicture Asset="page.Hero" Sizes="(min-width: 60em) 50vw, 100vw" class="hero" />
```

With `Id`, the component reads the asset through the registered `BruaClient` and renders nothing until it has loaded; static server-side rendering waits for it, so the page arrives complete.

The component renders a `<picture>`: dark-mode files behind a media query, AVIF and WebP as sources, and the alt text, language, width and height on the `<img>`. The browser picks the format and density and follows the system color scheme live. Animations show their still. Anything else you put on the tag, such as `class`, `style` or `data-` attributes, goes on the `<img>`.

An app that switches themes itself passes the scheme, and the picture shows that scheme's files only:

```razor
<BruaPicture Asset="brand.Mark" ColorScheme="@(theme.IsDark ? "dark" : "light")" Loading="eager" />
```

## Server-rendered HTML

```cshtml
@Html.Raw(Picture.Html(Model.Hero, new PictureOptions(Sizes: "100vw", Class: "hero")))
```

`Picture.Model` gives the same thing as data, for a templating engine of your own.

## One URL

Where a `<picture>` can't go, such as an `og:image` tag, a notification or a PDF, pick one variant:

```csharp
var url = VariantSelector.Url(asset, new ClientState(["image/png", "image/jpeg"], ColorScheme: "light", Scale: 2));
```

`ClientState` says what the client renders and under which conditions; the rule is the one every Brua SDK and the edge run (SPEC.md 7.3).

## Tests

```bash
dotnet test sdk/dotnet/Brua.Web.Tests
```

runs the shared fixtures in `fixtures/selection` against the selection rule and renders the component with Blazor's `HtmlRenderer`.
