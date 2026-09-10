using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Relewise.BlazorApps.TypeEditors;
using MessagePack;
using Newtonsoft.Json;
using Relewise.Client.Responses.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
[TestCategory("Browser")]
public class PublishedBrowserTests
{
    private WebApplication? host;
    private IPlaywright? playwright;
    private IBrowser? browser;
    private IBrowserContext context = null!;
    private IPage page = null!;
    private string origin = null!;
    private readonly ConcurrentQueue<string> errors = new();
    private int searchRequests;
    private const string Prefix = "/relewise-tools-blazor-apps";
    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task StartPublishedApp()
    {
        var directory = Environment.GetEnvironmentVariable("BLAZOR_PUBLISH_DIR");
        if (string.IsNullOrWhiteSpace(directory)) Assert.Inconclusive("Set BLAZOR_PUBLISH_DIR to the AOT publish wwwroot directory.");
        Assert.IsTrue(File.Exists(Path.Combine(directory, "index.html")), "Publish output is missing.");
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        host = builder.Build();
        // Emulate a GitHub Pages project site, including its 404 redirect script.
        host.UsePathBase(Prefix);
        var files = new PhysicalFileProvider(Path.GetFullPath(directory));
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".wasm"] = "application/wasm";
        contentTypes.Mappings[".dat"] = "application/octet-stream";
        host.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        host.UseStaticFiles(new StaticFileOptions { FileProvider = files, ContentTypeProvider = contentTypes });
        host.Run(async http =>
        {
            http.Response.StatusCode = 404;
            http.Response.ContentType = "text/html";
            await http.Response.SendFileAsync(Path.Combine(directory, "404.html"));
        });
        await host.StartAsync();
        origin = host.Urls.Single();
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        context = await browser.NewContextAsync(new() { Permissions = ["clipboard-read", "clipboard-write"] });
        await context.RouteAsync("**/*", async route =>
        {
            var uri = new Uri(route.Request.Url);
            if (uri.Authority == new Uri(origin).Authority && uri.AbsolutePath == "/fixture-parent.html")
            {
                await route.FulfillAsync(new() { ContentType = "text/html", Body = "<!doctype html><html><body>Fixture parent</body></html>" });
            }
            else if (uri.Authority == new Uri(origin).Authority && uri.AbsolutePath.StartsWith("/fixture-api/"))
            {
                Assert.AreEqual("POST", route.Request.Method);
                Interlocked.Increment(ref searchRequests);
                var response = JsonConvert.DeserializeObject<ProductSearchResponse>("{\"Results\":[],\"Hits\":0,\"Statistics\":{\"ServerTimeInMs\":0}}")!;
                await route.FulfillAsync(new() { ContentType = "application/x-msgpack", BodyBytes = MessagePackSerializer.Serialize(response) });
            }
            else if (uri.Authority == new Uri(origin).Authority)
            {
                await route.ContinueAsync();
            }
            else if (uri.Host == "cdn.relewise.com" && uri.AbsolutePath.Contains("/nuget/xmldocs/"))
            {
                await route.FulfillAsync(new() { ContentType = "application/xml", Body = DocumentationCacheTests.Xml });
            }
            else if (uri.Host == "cdn.relewise.com" && uri.AbsolutePath.Contains("/nuget/versions/"))
            {
                var version = typeof(Relewise.Client.ClientBase).Assembly.GetName().Version!.ToString(3);
                await route.FulfillAsync(new() { ContentType = "application/json", Body = JsonConvert.SerializeObject(new[] { new { version, published = "2026-09-03T00:00:00Z" } }) });
            }
            else if (uri.Host == "cdn.relewise.com" && uri.AbsolutePath.Contains("/nuget/dll/"))
            {
                await route.FulfillAsync(new() { ContentType = "application/octet-stream", BodyBytes = await File.ReadAllBytesAsync(typeof(Relewise.Client.ClientBase).Assembly.Location) });
            }
            else
            {
                errors.Enqueue("Unexpected external request: " + uri.GetLeftPart(UriPartial.Path));
                await route.AbortAsync();
            }
        });
        page = await context.NewPageAsync();
        page.SetDefaultTimeout(30000);
        page.PageError += (_, error) => errors.Enqueue(error);
        page.Console += (_, message) =>
        {
            if (message.Type == "error" && !message.Text.Contains("404 (Not Found)")) errors.Enqueue(message.Text);
        };
    }

    [TestMethod]
    [DataRow("Models?q=ProductSearchRequest", "Models")]
    [DataRow("Recommendations", "Recommendations")]
    [DataRow("Versions", "Versions")]
    public async Task DeepLinksLoadPublishedTools(string route, string heading)
    {
        await page.GotoAsync(origin + Prefix + "/" + route);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true })).ToBeVisibleAsync();
        if (heading == "Models")
        {
            await Assertions.Expect(page.Locator("details").First).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("#search")).ToHaveValueAsync("ProductSearchRequest");
            await page.Locator("[aria-describedby='tooltip']").First.HoverAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Tooltip)).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = "Models", Exact = true }).HoverAsync();
        }
        if (heading == "Versions")
        {
            await page.Locator(".list").Nth(0).Locator("div").First.ClickAsync();
            await page.Locator(".list").Nth(1).Locator("div").First.ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Compare!" }).ClickAsync();
            await Assertions.Expect(page.GetByText("There were no changed enums between these versions.", new() { Exact = true })).ToBeVisibleAsync();
        }
        await AssertHealthy();
    }

    [TestMethod]
    public async Task SharedModelAndSavedRequestCanBeReopened()
    {
        const string json = "{\"$type\":\"Relewise.Client.Requests.Search.ProductSearchRequest, Relewise.Client\",\"Term\":\"fixture-boots\",\"Skip\":0,\"Take\":5}";
        string compressed = ObjectEditor<object>.ToGzip(json);
        await page.GotoAsync(origin + Prefix + "/Models?q=ProductSearchRequest&o=" + Uri.EscapeDataString(compressed));
        var term = page.GetByRole(AriaRole.Textbox).Nth(1);
        await Assertions.Expect(term).ToHaveValueAsync("fixture-boots");
        await term.FillAsync("fixture-shoes");
        // Clicking the copy control exercises browser interop and serialization from published code.
        await page.Locator("span:has(svg title:text-is('Copy link to Model page with this object data'))").First.ClickAsync();
        string copied = await page.EvaluateAsync<string>("navigator.clipboard.readText()");
        await page.GotoAsync(copied);
        await Assertions.Expect(page.GetByRole(AriaRole.Textbox).Nth(1)).ToHaveValueAsync("fixture-shoes");
        await page.EvaluateAsync("value => localStorage.setItem('lastRequest', value)", compressed);
        await page.GotoAsync(origin + Prefix + "/RequestExplorer");
        await page.GetByRole(AriaRole.Button, new() { Name = "Recall last edited request" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Transfer to Searches page" })).ToBeVisibleAsync();
        await AssertHealthy();
    }

    [TestMethod]
    public async Task SearchCanUseTheSdkAgainstAFixtureEndpoint()
    {
        const string json = "{\"$type\":\"Relewise.Client.Requests.Search.ProductSearchRequest, Relewise.Client\",\"Term\":\"fixture\",\"Skip\":0,\"Take\":5,\"Language\":{\"Value\":\"en\"},\"Currency\":{\"Value\":\"EUR\"},\"User\":{\"TemporaryId\":\"fixture\"}}";
        await page.GotoAsync(origin + Prefix + "/Searches?q=ProductSearchRequest&o=" + Uri.EscapeDataString(ObjectEditor<object>.ToGzip(json)));
        await page.Locator("#serverUrl").FillAsync(origin + "/fixture-api/");
        await page.Locator("#datasetId").FillAsync("00000000-0000-0000-0000-000000000001");
        await page.Locator("#apiKey").FillAsync("synthetic-test-key");
        await page.GetByRole(AriaRole.Button, new() { Name = "Connect", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Post", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByText("Successfully searched.", new() { Exact = true })).ToBeVisibleAsync();
        Assert.AreEqual(1, searchRequests);
        await AssertHealthy();
    }

    [TestMethod]
    public async Task VariantSettingsCanBeEditedAndSharedInPublishedApp()
    {
        const string json = """
            {"$type":"Relewise.Client.Requests.Search.ProductSearchRequest, Relewise.Client","Term":"fixture","Skip":0,"Take":5,"Settings":{"VariantRequestSettings":{"MaxVariantsPerProduct":3,"Sorting":"ByRelevance"}}}
            """;
        await page.GotoAsync(origin + Prefix + "/Models?q=ProductSearchRequest&o=" + Uri.EscapeDataString(ObjectEditor<object>.ToGzip(json)));
        var summary = page.Locator("summary").Filter(new() { HasText = "VariantSearchRequestSettings" });
        var variants = summary.Locator("..");
        await Assertions.Expect(variants.Locator("input")).ToHaveValueAsync("3");
        await Assertions.Expect(variants.Locator("select")).ToHaveValueAsync("ByRelevance");
        await variants.Locator("input").FillAsync("5");
        await variants.Locator("select").SelectOptionAsync("");
        await Assertions.Expect(variants.Locator("select")).ToHaveValueAsync("");
        await page.Locator("span:has(svg title:text-is('Copy link to Model page with this object data'))").First.ClickAsync();
        string copied = await page.EvaluateAsync<string>("navigator.clipboard.readText()");
        await page.GotoAsync(copied);
        await Assertions.Expect(variants.Locator("input")).ToHaveValueAsync("5");
        await Assertions.Expect(variants.Locator("select")).ToHaveValueAsync("");
        await variants.Locator("select").SelectOptionAsync("GroupedByProduct");
        await Assertions.Expect(variants.Locator("select")).ToHaveValueAsync("GroupedByProduct");
        await AssertHealthy();
        string screenshot = Path.Combine(TestContext.TestResultsDirectory!, "variant-request-settings.png");
        await variants.ScreenshotAsync(new() { Path = screenshot });
        TestContext.AddResultFile(screenshot);
    }

    [TestMethod]
    public async Task EmbeddedModeKeepsTheParentHandshake()
    {
        // My Relewise is not another Blazor runtime: use a plain parent document.
        await page.GotoAsync(origin + "/fixture-parent.html");
        await page.EvaluateAsync("""
            origin => {
                window.fixtureReady = false;
                window.addEventListener('message', e => {
                    if (e.data.MyRelewiseAppReady) window.fixtureReady = true;
                });
                const iframe = document.createElement('iframe');
                iframe.id = 'fixture';
                iframe.src = origin + '/relewise-tools-blazor-apps/Searches?q=ProductSearchRequest&datasetId=00000000-0000-0000-0000-000000000001&parentOrigin=' + encodeURIComponent(origin);
                document.body.append(iframe);
            }
            """, origin);
        var frame = page.FrameLocator("#fixture");
        await Assertions.Expect(frame.GetByRole(AriaRole.Heading, new() { Name = "Searches", Exact = true })).ToBeVisibleAsync();
        await page.WaitForFunctionAsync("window.fixtureReady === true");
        await page.EvaluateAsync("""
            origin => document.querySelector('#fixture').contentWindow.postMessage(
                { apiKey: 'synthetic-test-key', serverUrl: origin + '/fixture-api/' }, origin)
            """, origin);
        await Assertions.Expect(frame.GetByText("Successfully initialized the Searcher.", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(frame.Locator("#apiKey")).ToHaveCountAsync(0);
        await Assertions.Expect(frame.Locator(".sidebar")).ToHaveCountAsync(0);
        await Assertions.Expect(frame.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        Assert.AreEqual(0, errors.Count, string.Join(Environment.NewLine, errors));
    }

    private async Task AssertHealthy()
    {
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Assertions.Expect(page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator(".blazor-error-boundary")).ToHaveCountAsync(0);
        Assert.AreEqual(0, errors.Count, string.Join(Environment.NewLine, errors));
    }

    [TestCleanup]
    public async Task StopPublishedApp()
    {
        if (page is not null && TestContext.CurrentTestOutcome != UnitTestOutcome.Passed)
        {
            Console.WriteLine(string.Join(Environment.NewLine, errors));
            foreach (var frame in page.Frames)
                Console.WriteLine($"Frame: {frame.Url}\n{await frame.Locator("body").InnerTextAsync()}");
        }
        if (browser is not null) await browser.DisposeAsync();
        playwright?.Dispose();
        if (host is not null) await host.DisposeAsync();
    }
}
