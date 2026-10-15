using Microsoft.JSInterop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Relewise.BlazorApps.Pages;
using Relewise.BlazorApps.Shared;
using Relewise.Client.Requests.Search;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Relewise.BlazorApps.TypeEditors;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Recommendations;
using Relewise.BlazorApps.XmlSummaries;
using MudBlazor;
using Relewise.BlazorApps.Components;
using Relewise.Client.DataTypes.Triggers.Configurations;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class RequestEditHistoryTests
{
    [TestMethod]
    public async Task ExplicitTriggerAnnotationPreservesItsTypeAndFields()
    {
        await using var context = new BunitContext();
        var runtime = new MemoryRuntime();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(runtime);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        var page = context.Render<RequestExplorer>();
        await page.Find("textarea").InputAsync(new() { Value = "{\"$type\":\"Relewise.Client.DataTypes.Triggers.Configurations.AbandonedCartTriggerConfiguration, Relewise.Client\",\"WithinTimeSpanMinutes\":42}" });
        await page.FindAll("button").Single(button => button.TextContent.Trim() == "Run request").ClickAsync(new());
        page.WaitForAssertion(() => Assert.IsInstanceOfType<AbandonedCartTriggerConfiguration>(page.FindComponent<ObjectEditor<object>>().Instance.Value));
        var trigger = (AbandonedCartTriggerConfiguration)page.FindComponent<ObjectEditor<object>>().Instance.Value!;
        Assert.AreEqual(42, trigger.WithinTimeSpanMinutes);
        Assert.AreEqual(MudBlazor.Severity.Warning, page.FindComponent<MudAlert>().Instance.Severity);
        Assert.AreEqual(nameof(AbandonedCartTriggerConfiguration), RequestEditHistory.Load(runtime)[0].Title);
    }

    [TestMethod]
    public async Task UnresolvedAnnotationAsksForTypeInsteadOfGuessingAnotherRequest()
    {
        await using var context = new BunitContext();
        var runtime = new MemoryRuntime();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(runtime);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var dialogs = context.Render<MudDialogProvider>();
        var page = context.Render<RequestExplorer>();
        await page.Find("textarea").InputAsync(new() { Value = "{\"$type\":\"Unknown.Request, Unknown\",\"Term\":\"Synthetic\",\"Take\":10}" });
        await page.FindAll("button").Single(button => button.TextContent.Trim() == "Run request").ClickAsync(new());
        dialogs.WaitForAssertion(() => Assert.HasCount(1, dialogs.FindComponents<RSelect<Type>>()));
        Assert.HasCount(0, RequestEditHistory.Load(runtime));
    }

    [TestMethod]
    public async Task ResultLayoutPreferenceIsSavedAndRestored()
    {
        await using var context = new BunitContext();
        var runtime = new MemoryRuntime();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(runtime);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var page = context.Render<RequestExplorer>();
        var layout = page.FindComponent<RSelect<bool>>();
        Assert.HasCount(0, page.FindAll(".request-source-section .request-layout-choice"));
        Assert.HasCount(1, page.FindAll(".explorer-heading .request-layout-choice"));
        Assert.IsTrue(layout.Instance.Value);
        await layout.InvokeAsync(() => layout.Instance.ValueChanged.InvokeAsync(false));
        Assert.AreEqual("top-bottom", runtime.Storage["requestExplorerLayout"]);
        page.Dispose();

        var restored = context.Render<RequestExplorer>().FindComponent<RSelect<bool>>();
        Assert.IsFalse(restored.Instance.Value);
        await restored.InvokeAsync(() => restored.Instance.ValueChanged.InvokeAsync(true));
        Assert.AreEqual("side-by-side", runtime.Storage["requestExplorerLayout"]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TroubleshootParsesAndStartsDiagnosticsInOneClick(bool needsTypeSelection)
    {
        await using var context = new BunitContext();
        var runtime = new MemoryRuntime();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(runtime);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.ComponentFactories.AddStub<ObjectEditor<LicensedRequest>>();
        context.ComponentFactories.AddStub<SearchConstructor>(parameters => builder =>
        {
            builder.AddContent(0, parameters.Get(component => component.RequestActions));
            builder.AddContent(1, parameters.Get(component => component.ResultContent));
        });
        context.ComponentFactories.AddStub<RecommendationConstructor>();
        context.ComponentFactories.AddStub<Troubleshooter>("<div data-testid='troubleshooter'>Diagnostics</div>");
        var dialogs = context.Render<MudDialogProvider>();
        var page = context.Render<RequestExplorer>();
        string json = needsTypeSelection ? "{}"
            : "{\"$type\":\"Relewise.Client.Requests.Search.ProductSearchRequest, Relewise.Client\",\"Term\":\"Synthetic\"}";
        await page.Find("textarea").InputAsync(new() { Value = json });
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Troubleshoot").ClickAsync(new());
        Assert.IsFalse(page.Find(".request-source-section").HasAttribute("open"));

        if (needsTypeSelection)
        {
            dialogs.WaitForAssertion(() => Assert.HasCount(1, dialogs.FindComponents<RSelect<Type>>()));
            Assert.HasCount(0, RequestEditHistory.Load(runtime));
            var selector = dialogs.FindComponent<RSelect<Type>>();
            await selector.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(typeof(ProductSearchRequest)));
        }

        Assert.HasCount(1, page.FindAll("[data-testid=troubleshooter]"));
        Assert.HasCount(1, RequestEditHistory.Load(runtime));
        var diagnostics = page.FindComponent<Bunit.TestDoubles.Stub<Troubleshooter>>();
        Assert.IsTrue(diagnostics.Instance.Parameters.Get(component => component.AutoRun));
        var requestEditor = page.FindComponent<Bunit.TestDoubles.Stub<SearchConstructor>>();
        var edited = (ProductSearchRequest)requestEditor.Instance.Parameters.Get(component => component.Request)!;
        edited.Take = 7;
        await requestEditor.InvokeAsync(() => requestEditor.Instance.Parameters.Get(component => component.RequestChanged).InvokeAsync(edited));
        Assert.IsTrue(page.FindComponent<Bunit.TestDoubles.Stub<Troubleshooter>>().Instance.Parameters.Get(component => component.Stale));
        await page.FindAll("button").Single(button => button.TextContent.Trim() == "Troubleshoot again").ClickAsync(new());
        Assert.IsFalse(page.FindComponent<Bunit.TestDoubles.Stub<Troubleshooter>>().Instance.Parameters.Get(component => component.Stale));
        Assert.AreEqual(7, ((ProductSearchRequest)page.FindComponent<Bunit.TestDoubles.Stub<Troubleshooter>>().Instance.Parameters.Get(component => component.Request)).Take);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EditingConstructorFieldsUpdatesOneRecentRequest(bool search)
    {
        await using var context = new BunitContext();
        var runtime = new MemoryRuntime();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(runtime);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        var parameters = new Dictionary<string, object>
        {
            ["RequestType"] = search ? typeof(ProductSearchRequest) : typeof(PopularProductsRequest),
            ["Request"] = search ? new ProductSearchRequest() : new PopularProductsRequest(),
            ["OpenDetails"] = true,
            ["Connect"] = (Func<bool>)(() => false)
        };
        var component = context.Render<Microsoft.AspNetCore.Components.DynamicComponent>(p => p
            .Add(c => c.Type, search ? typeof(SearchConstructor) : typeof(RecommendationConstructor))
            .Add(c => c.Parameters, parameters));
        component.WaitForAssertion(() => Assert.IsTrue(component.FindComponents<StringEditor>().Count > 0));
        var field = component.FindComponents<StringEditor>().First(c => !c.Instance.ReadOnly);
        await field.Find("input").InputAsync(new() { Value = "Synthetic first edit" });
        await field.Find("input").InputAsync(new() { Value = "Synthetic second edit" });
        var history = RequestEditHistory.Load(runtime);
        Assert.HasCount(1, history);
        StringAssert.Contains((await Models.FromGzipAsync(history[0].Compressed)).ToLowerInvariant(), "synthetic second edit");

        component.Dispose();
        context.ComponentFactories.AddStub<ObjectEditor<LicensedRequest>>();
        context.ComponentFactories.AddStub<SearchConstructor>(parameters => builder =>
        {
            builder.AddContent(0, parameters.Get(component => component.RequestActions));
            builder.AddContent(1, parameters.Get(component => component.ResultContent));
        });
        context.ComponentFactories.AddStub<RecommendationConstructor>();
        context.ComponentFactories.AddStub<Troubleshooter>();
        var explorer = context.Render<RequestExplorer>();
        Assert.HasCount(1, explorer.FindAll(".request-history-entry"));
    }

    [TestMethod]
    public async Task ChangingTakeUpdatesExistingEntryAndKeepsOtherRequests()
    {
        var runtime = new MemoryRuntime();
        var first = new ProductSearchRequest { Term = "First", Take = 5 };
        var second = new ProductSearchRequest { Term = "Second", Take = 10 };
        RequestExplorer.SaveInLocalStorage(runtime, first);
        string firstId = RequestEditHistory.Load(runtime)[0].Id;
        RequestExplorer.SaveInLocalStorage(runtime, second);
        first.Take = 6;
        RequestExplorer.SaveInLocalStorage(runtime, first);
        first.Take = 7;
        RequestExplorer.SaveInLocalStorage(runtime, first);

        var history = RequestEditHistory.Load(runtime);
        Assert.HasCount(2, history);
        Assert.AreEqual(firstId, history[0].Id);
        Assert.AreEqual(7, JObject.Parse(await Models.FromGzipAsync(history[0].Compressed)).Value<int>("Take"));
        Assert.AreEqual("Second", JObject.Parse(await Models.FromGzipAsync(history[1].Compressed)).Value<string>("Term"));
    }

    [TestMethod]
    public async Task ParsingRequestsAddsHistoryAndFailedParsingDoesNot()
    {
        await using var context = new BunitContext();
        var runtime = new MemoryRuntime();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(runtime);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.ComponentFactories.AddStub<ObjectEditor<LicensedRequest>>();
        context.ComponentFactories.AddStub<SearchConstructor>(parameters => builder =>
        {
            builder.AddContent(0, parameters.Get(component => component.RequestActions));
            builder.AddContent(1, parameters.Get(component => component.ResultContent));
        });
        context.ComponentFactories.AddStub<RecommendationConstructor>();
        context.ComponentFactories.AddStub<Troubleshooter>("<div data-testid='troubleshooter'>Diagnostics</div>");
        var dialogs = context.Render<MudDialogProvider>();
        var page = context.Render<RequestExplorer>();
        Assert.IsFalse(page.FindAll("button").Single(b => b.TextContent.Trim() == "Troubleshoot").HasAttribute("disabled"));

        await page.Find("textarea").InputAsync(new() { Value = "{\"$type\":\"Relewise.Client.Requests.Search.ProductSearchRequest, Relewise.Client\",\"Term\":\"First\"}" });
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Run request").ClickAsync(new());
        Assert.IsFalse(page.Find(".request-source-section").HasAttribute("open"));
        await page.Find(".request-source-section > summary").ClickAsync(new());
        Assert.IsTrue(page.Find(".request-source-section").HasAttribute("open"));
        Assert.HasCount(1, page.FindAll(".request-history-entry"));
        Assert.IsFalse(page.Find(".request-history-entry").HasAttribute("disabled"));
        var requestEditor = page.FindComponent<Bunit.TestDoubles.Stub<SearchConstructor>>();
        Assert.IsTrue(requestEditor.Instance.Parameters.Get(component => component.AutoPost));
        Assert.IsTrue(requestEditor.Instance.Parameters.Get(component => component.ShowPostButton));
        Assert.AreEqual("Re-run Request", requestEditor.Instance.Parameters.Get(component => component.PostButtonText));
        Assert.HasCount(0, page.FindAll("[data-testid=troubleshooter]"));
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Troubleshoot").ClickAsync(new());
        Assert.HasCount(1, page.FindAll("[data-testid=troubleshooter]"));

        await page.Find("textarea").InputAsync(new() { Value = "{}" });
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Run request").ClickAsync(new());
        Assert.HasCount(0, page.FindAll("[data-testid=troubleshooter]"));
        dialogs.WaitForAssertion(() => Assert.HasCount(1, dialogs.FindComponents<RSelect<Type>>()));
        Assert.HasCount(1, RequestEditHistory.Load(runtime));
        var selector = dialogs.FindComponent<RSelect<Type>>();
        await selector.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(typeof(ProductSearchRequest)));
        Assert.HasCount(2, page.FindAll(".request-history-entry"));

        await page.Find("textarea").InputAsync(new() { Value = "invalid-json" });
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Run request").ClickAsync(new());
        Assert.HasCount(2, RequestEditHistory.Load(runtime));

        await page.FindAll(".request-history-entry")[1].ClickAsync(new());
        Assert.IsFalse(page.Find(".request-source-section").HasAttribute("open"));
        Assert.AreEqual("First", JObject.Parse(page.Find("textarea").GetAttribute("value") ?? page.Find("textarea").TextContent).Value<string>("Term"));
        Assert.HasCount(2, RequestEditHistory.Load(runtime));
        Assert.IsTrue(page.FindComponent<Bunit.TestDoubles.Stub<SearchConstructor>>().Instance.Parameters.Get(component => component.AutoPost));
    }

    [TestMethod]
    public async Task FailedManualParseKeepsDialogOpenAndCanRetryAnotherType()
    {
        await using var context = new BunitContext();
        var runtime = new MemoryRuntime();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(runtime);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.ComponentFactories.AddStub<ObjectEditor<LicensedRequest>>();
        context.ComponentFactories.AddStub<SearchConstructor>(parameters => builder =>
        {
            builder.AddContent(0, parameters.Get(component => component.RequestActions));
            builder.AddContent(1, parameters.Get(component => component.ResultContent));
        });
        context.ComponentFactories.AddStub<RecommendationConstructor>();
        context.ComponentFactories.AddStub<Troubleshooter>();
        var dialogs = context.Render<MudDialogProvider>();
        var page = context.Render<RequestExplorer>();
        await page.Find("textarea").InputAsync(new() { Value = "{\"$type\":\"Unknown.Request, Unknown\",\"Term\":[]}" });
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Run request").ClickAsync(new());
        dialogs.WaitForAssertion(() => Assert.HasCount(1, dialogs.FindComponents<RSelect<Type>>()));
        var selector = dialogs.FindComponent<RSelect<Type>>();
        await selector.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(typeof(ProductSearchRequest)));
        Assert.HasCount(1, dialogs.FindAll("[role=alert]"));
        Assert.HasCount(0, RequestEditHistory.Load(runtime));

        await selector.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(typeof(PopularProductsRequest)));
        Assert.HasCount(1, RequestEditHistory.Load(runtime));
        Assert.AreEqual(nameof(PopularProductsRequest), RequestEditHistory.Load(runtime)[0].Title);
        dialogs.WaitForAssertion(() => Assert.HasCount(0, dialogs.FindAll("[role=dialog]")));
    }

    [TestMethod]
    public async Task CancellingTypeSelectionDoesNotSaveARequest()
    {
        await using var context = new BunitContext();
        var runtime = new MemoryRuntime();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(runtime);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var dialogs = context.Render<MudDialogProvider>();
        var page = context.Render<RequestExplorer>();
        await page.Find("textarea").InputAsync(new() { Value = "{}" });
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Run request").ClickAsync(new());
        dialogs.WaitForAssertion(() => Assert.HasCount(1, dialogs.FindComponents<RSelect<Type>>()));
        await dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Cancel").ClickAsync(new());
        Assert.HasCount(0, RequestEditHistory.Load(runtime));
        dialogs.WaitForAssertion(() => Assert.HasCount(0, dialogs.FindAll("[role=dialog]")));
    }

    [TestMethod]
    public async Task KeepsTenNewestSnapshotsAndPreservesTypeAndValues()
    {
        var runtime = new MemoryRuntime();
        for (int i = 0; i < 12; i++)
        {
            RequestExplorer.SaveInLocalStorage(runtime, new ProductSearchRequest { Term = $"Edit {i}" });
        }

        var history = RequestEditHistory.Load(runtime);
        Assert.HasCount(10, history);
        var newest = JObject.Parse(await Models.FromGzipAsync(history[0].Compressed));
        var oldest = JObject.Parse(await Models.FromGzipAsync(history[9].Compressed));
        Assert.AreEqual("Edit 11", newest.Value<string>("Term"));
        Assert.AreEqual("Edit 2", oldest.Value<string>("Term"));
        StringAssert.Contains(newest.Value<string>("$type")!, nameof(ProductSearchRequest));
        Assert.AreEqual(nameof(ProductSearchRequest), history[0].Title);
        Assert.AreEqual(history[0].Compressed, runtime.Storage["lastRequest"]);
        Assert.IsNotNull(history[0].SavedAt);
    }

    [TestMethod]
    public void ConsecutiveIdenticalSavesDoNotConsumeHistorySlots()
    {
        var runtime = new MemoryRuntime();
        var request = new ProductSearchRequest { Term = "Synthetic" };
        RequestExplorer.SaveInLocalStorage(runtime, request);
        RequestExplorer.SaveInLocalStorage(runtime, request);
        RequestExplorer.SaveInLocalStorage(runtime, null);
        Assert.HasCount(1, RequestEditHistory.Load(runtime));
    }

    [TestMethod]
    public async Task LegacySnapshotIsIncludedWhenFirstNewEditIsSaved()
    {
        var runtime = new MemoryRuntime();
        string legacy = RequestExplorer.ToGzip("{\"$type\":\"Relewise.Client.Requests.Search.ProductSearchRequest, Relewise.Client\",\"Term\":\"Legacy\"}");
        runtime.Storage["lastRequest"] = legacy;
        var existing = RequestEditHistory.Load(runtime);
        Assert.HasCount(1, existing);
        Assert.AreEqual(legacy, existing[0].Compressed);

        RequestExplorer.SaveInLocalStorage(runtime, new ProductSearchRequest { Term = "New" });
        var history = RequestEditHistory.Load(runtime);
        Assert.HasCount(2, history);
        Assert.AreEqual("Legacy", JObject.Parse(await Models.FromGzipAsync(history[1].Compressed)).Value<string>("Term"));
    }

    [TestMethod]
    public void InvalidHistoryIsReportedWithoutOverwritingSavedData()
    {
        var runtime = new MemoryRuntime();
        runtime.Storage["requestEditHistory"] = "invalid-json";
        runtime.Storage["lastRequest"] = "existing";
        Assert.ThrowsExactly<Newtonsoft.Json.JsonReaderException>(() =>
            RequestExplorer.SaveInLocalStorage(runtime, new ProductSearchRequest { Term = "New" }));
        Assert.AreEqual("existing", runtime.Storage["lastRequest"]);
        Assert.AreEqual("invalid-json", runtime.Storage["requestEditHistory"]);
    }

    internal sealed class MemoryRuntime : IJSInProcessRuntime
    {
        public Dictionary<string, string> Storage { get; } = [];

        public TValue Invoke<TValue>(string identifier, params object?[]? args)
        {
            string key = (string)args![0]!;
            if (identifier == "localStorage.getItem")
            {
                return (TValue)(object?)Storage.GetValueOrDefault(key)!;
            }
            if (identifier == "localStorage.setItem")
            {
                Storage[key] = (string)args[1]!;
                return default!;
            }
            throw new InvalidOperationException(identifier);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}
