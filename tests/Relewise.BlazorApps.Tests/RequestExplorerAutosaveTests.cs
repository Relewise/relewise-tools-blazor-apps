using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Relewise.BlazorApps.Pages;
using Relewise.BlazorApps.Shared;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.Requests.Search;
using Relewise.Client.Responses.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class RequestExplorerAutosaveTests
{
    [TestMethod]
    public async Task RapidEditsKeepLiveValuesAndResponseAndWriteOneSnapshotAfterPause()
    {
        await using var fixture = await Fixture.Create();
        var constructor = fixture.Page.FindComponent<SearchConstructor>();
        var response = JsonConvert.DeserializeObject<ProductSearchResponse>("""{"Results":[{"ProductId":"fixture-result"}]}""")!;
        constructor.Instance.State.Response = response;
        fixture.Page.Render();
        await fixture.Edit("first");
        await fixture.Edit("second");
        await fixture.Edit("latest");
        Assert.AreEqual("latest", ((ProductSearchRequest)constructor.Instance.State.Request!).Term);
        Assert.AreEqual(0, fixture.Runtime.Writes);
        Assert.AreEqual(0, fixture.Runtime.Reads);
        Assert.AreSame(response, constructor.Instance.State.Response);
        await fixture.Page.FindComponent<ObjectEditor<SearchRequest>>().InvokeAsync(() =>
            fixture.Page.FindComponent<ObjectEditor<SearchRequest>>().Instance.CopyToClipboard());
        var copied = (string)fixture.Context.JSInterop.Invocations.Last(invocation => invocation.Identifier == "navigator.clipboard.writeText").Arguments[0]!;
        Assert.AreEqual("latest", JObject.Parse(copied).Value<string>("Term"));
        await fixture.Runtime.SaveCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(2, fixture.Runtime.Writes);
        Assert.AreEqual(1, fixture.Runtime.Reads, "Save should reuse the loaded history instead of reading it again.");
        Assert.AreEqual("latest", (await fixture.Saved()).Value<string>("Term"));
        Assert.HasCount(1, RequestEditHistory.Load(fixture.Runtime));
        Assert.AreSame(response, constructor.Instance.State.Response);
    }

    [TestMethod]
    public async Task OpeningSourceFlushesEditsAndTypingSourceCannotBeOverwrittenByAutosave()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Edit("editor change");
        await fixture.Page.Find(".request-source-section > summary").ClickAsync(new());
        Assert.AreEqual("editor change", JObject.Parse(fixture.Page.Find("textarea").GetAttribute("value")!).Value<string>("Term"));
        Assert.AreEqual("editor change", (await fixture.Saved()).Value<string>("Term"));
        await fixture.Edit("pending editor change");
        await fixture.Page.Find("textarea").InputAsync(new() { Value = Fixture.RequestJson("source change") });
        await fixture.Page.FindAll("button").Single(button => button.TextContent.Trim() == "Run request").ClickAsync(new());
        Assert.AreEqual("source change", (await fixture.Saved()).Value<string>("Term"));
        Assert.AreEqual("source change", ((ProductSearchRequest)fixture.Page.FindComponent<SearchConstructor>().Instance.State.Request!).Term);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NavigationAndDisposalFlushPendingChanges(bool dispose)
    {
        await using var fixture = await Fixture.Create();
        await fixture.Edit("leaving page");
        Assert.AreEqual(0, fixture.Runtime.Writes);
        if (dispose)
            await fixture.Page.InvokeAsync(async () => await fixture.Page.Instance.DisposeAsync());
        else
            await fixture.Page.InvokeAsync(() => fixture.Context.Services.GetRequiredService<NavigationManager>().NavigateTo("/Models"));
        Assert.AreEqual(2, fixture.Runtime.Writes);
        Assert.AreEqual("leaving page", (await fixture.Saved()).Value<string>("Term"));
    }

    [TestMethod]
    public async Task ReplacingRequestFlushesThePreviousPendingEdit()
    {
        await using var fixture = await Fixture.Create();
        var popovers = fixture.Context.Render<MudPopoverProvider>();
        await fixture.Edit("before type switch");
        var root = fixture.Page.FindComponent<ObjectEditor<SearchRequest>>();
        await root.Find(".object-header-actions .mud-menu button").ClickAsync(new());
        await popovers.FindAll(".mud-menu-item").Single(item => item.TextContent.Trim() == nameof(ContentSearchRequest)).ClickAsync(new());
        var history = RequestEditHistory.Load(fixture.Runtime);
        Assert.HasCount(2, history);
        Assert.AreEqual("before type switch", JObject.Parse(await Models.FromGzipAsync(history[1].Compressed)).Value<string>("Term"));
        StringAssert.Contains((await fixture.Saved()).Value<string>("$type")!, nameof(ContentSearchRequest));
        Assert.IsFalse(fixture.Page.FindComponent<SearchConstructor>().Instance.AutoPost);
    }

    [TestMethod]
    public async Task RerunFlushesSnapshotBeforeSendingLatestRequest()
    {
        await using var fixture = await Fixture.Create();
        StaticDatasetStorage.DatasetId = Guid.NewGuid().ToString();
        StaticDatasetStorage.ApiKey = "synthetic-api-key";
        await fixture.Page.FindAll("button").Single(button => button.TextContent.Trim() == "Connect").ClickAsync(new());
        var constructor = fixture.Page.FindComponent<SearchConstructor>();
        string? posted = null;
        using var http = DocumentationCacheTests.CreateClient(async request =>
        {
            posted = await request.Content!.ReadAsStringAsync();
            Assert.AreEqual("run latest", (await fixture.Saved()).Value<string>("Term"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"Results":[],"Hits":0,"Statistics":{"ServerTimeInMs":0}}""", System.Text.Encoding.UTF8, "application/json")
            };
        });
        constructor.Instance.Searcher!.OverriddenHttpClient = http;
        constructor.Instance.Searcher.UseFastBinarySerializer = false;
        constructor.Instance.Searcher.UseJsonCompression = false;
        await fixture.Edit("run latest");
        Assert.AreEqual(0, fixture.Runtime.Writes);
        await constructor.FindAll("button").Single(button => button.TextContent.Trim() == "Re-run Request").ClickAsync(new());
        fixture.Page.WaitForAssertion(() => Assert.IsNotNull(constructor.Instance.State.Response));
        Assert.AreEqual("run latest", JObject.Parse(posted!).Value<string>("Term"));
        Assert.AreEqual(2, fixture.Runtime.Writes);
        Assert.HasCount(0, fixture.Page.FindAll("[role=alert]"));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly (string Dataset, string Key, string Server, string? Parent) previous =
            (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey, StaticDatasetStorage.ServerUrl, StaticDatasetStorage.ParentOrigin);
        public BunitContext Context { get; } = new();
        public TrackingRuntime Runtime { get; } = new();
        public IRenderedComponent<RequestExplorer> Page { get; private set; } = null!;

        public static async Task<Fixture> Create()
        {
            var fixture = new Fixture();
            StaticDatasetStorage.DatasetId = "";
            StaticDatasetStorage.ApiKey = "";
            StaticDatasetStorage.ServerUrl = "https://fixture.invalid";
            StaticDatasetStorage.ParentOrigin = "https://fixture.invalid";
            fixture.Context.Services.AddMudServices();
            fixture.Context.Services.AddSingleton<IJSInProcessRuntime>(fixture.Runtime);
            fixture.Context.JSInterop.Mode = JSRuntimeMode.Loose;
            fixture.Context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
            fixture.Context.Services.AddSingleton<DocumentationCache>();
            fixture.Page = fixture.Context.Render<RequestExplorer>();
            await fixture.Page.Find("textarea").InputAsync(new() { Value = RequestJson("fixture") });
            await fixture.Page.FindAll("button").Single(button => button.TextContent.Trim() == "Run request").ClickAsync(new());
            fixture.Runtime.Writes = 0;
            fixture.Runtime.Reads = 0;
            fixture.Runtime.SaveCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return fixture;
        }

        public static string RequestJson(string term) => $$"""{"$type":"Relewise.Client.Requests.Search.ProductSearchRequest, Relewise.Client","Term":"{{term}}"}""";

        public async Task Edit(string term)
            => await Page.FindComponent<ObjectEditor<SearchRequest>>().Find(".object-properties > .object-property:first-child input").InputAsync(new() { Value = term });

        public async Task<JObject> Saved() => JObject.Parse(await Models.FromGzipAsync(Runtime.Storage["lastRequest"]));

        public async ValueTask DisposeAsync()
        {
            try { await Context.DisposeAsync(); }
            finally { (StaticDatasetStorage.DatasetId, StaticDatasetStorage.ApiKey, StaticDatasetStorage.ServerUrl, StaticDatasetStorage.ParentOrigin) = previous; }
        }
    }

    private sealed class TrackingRuntime : IJSInProcessRuntime
    {
        private readonly RequestEditHistoryTests.MemoryRuntime inner = new();
        public Dictionary<string, string> Storage => inner.Storage;
        public int Writes { get; set; }
        public int Reads { get; set; }
        public TaskCompletionSource SaveCompleted { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TValue Invoke<TValue>(string identifier, params object?[]? args)
        {
            if (identifier == "localStorage.setItem") Writes++;
            if (identifier == "localStorage.getItem") Reads++;
            var result = inner.Invoke<TValue>(identifier, args);
            if (identifier == "localStorage.setItem" && (string)args![0]! == "lastRequest") SaveCompleted.TrySetResult();
            return result;
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}
