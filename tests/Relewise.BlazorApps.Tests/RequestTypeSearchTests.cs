using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Services;
using Relewise.BlazorApps.Pages;
using Relewise.BlazorApps.Components;
using Relewise.BlazorApps.Shared;
using Relewise.Client.Requests.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class RequestTypeSearchTests
{
    [TestMethod]
    public async Task OpeningAndReopeningDialogFocusesTheSearchField()
    {
        await using var context = new BunitContext();
        var runtime = new RequestEditHistoryTests.MemoryRuntime();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(runtime);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var dialogs = context.Render<MudDialogProvider>();
        var page = context.Render<RequestExplorer>();
        await page.Find("textarea").InputAsync(new() { Value = "{}" });

        for (int opening = 0; opening < 2; opening++)
        {
            int previousFocusCalls = context.JSInterop.Invocations.Count(call => call.Identifier == "mudElementRef.focusFirst");
            await page.FindAll("button").Single(button => button.TextContent.Trim() == "Run request").ClickAsync(new());
            dialogs.WaitForAssertion(() =>
            {
                var focusTrap = dialogs.FindComponent<MudFocusTrap>();
                Assert.AreEqual(DefaultFocus.FirstChild, focusTrap.Instance.DefaultFocus);
                Assert.AreEqual(dialogs.Find("input").Id, focusTrap.FindAll("input, button").First().Id);
                Assert.AreEqual(previousFocusCalls + 1,
                    context.JSInterop.Invocations.Count(call => call.Identifier == "mudElementRef.focusFirst"));
            });
            await dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Cancel").ClickAsync(new());
            dialogs.WaitForAssertion(() => Assert.HasCount(0, dialogs.FindAll("[role=dialog]")));
        }
        Assert.HasCount(0, RequestEditHistory.Load(runtime));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TypingFiltersRequestTypesWithoutParsingUntilAnOptionIsChosen(bool useKeyboard)
    {
        await using var context = new BunitContext();
        var runtime = new RequestEditHistoryTests.MemoryRuntime();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(runtime);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.ComponentFactories.AddStub<SearchConstructor>();
        var popovers = context.Render<MudPopoverProvider>();
        var dialogs = context.Render<MudDialogProvider>();
        var page = context.Render<RequestExplorer>();
        await page.Find("textarea").InputAsync(new() { Value = """{"$type":"Unknown.Request, Unknown","Term":"Synthetic"}""" });
        await page.FindAll("button").Single(button => button.TextContent.Trim() == "Run request").ClickAsync(new());
        dialogs.WaitForAssertion(() => Assert.HasCount(1, dialogs.FindComponents<MudAutocomplete<Type>>()));

        await dialogs.Find("input").InputAsync(new() { Value = "p" });
        popovers.WaitForAssertion(() => Assert.IsTrue(popovers.FindAll(".mud-list-item").Count > 1));
        Assert.HasCount(0, RequestEditHistory.Load(runtime));
        Assert.HasCount(1, dialogs.FindAll("[role=dialog]"));

        await dialogs.Find("input").InputAsync(new() { Value = "DuCtSeArCh" });
        popovers.WaitForAssertion(() =>
        {
            var options = popovers.FindAll(".mud-list-item");
            Assert.IsTrue(options.Count > 0);
            Assert.IsTrue(options.All(option => option.TextContent.Contains("DuCtSeArCh", StringComparison.OrdinalIgnoreCase)));
        });
        Assert.HasCount(0, RequestEditHistory.Load(runtime));
        Assert.IsNull(dialogs.FindComponent<RAutocomplete<Type>>().Instance.Value);

        if (useKeyboard)
        {
            await dialogs.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter", Code = "Enter" });
            await dialogs.Find("input").KeyUpAsync(new KeyboardEventArgs { Key = "Enter", Code = "Enter" });
        }
        else
        {
            await popovers.FindAll(".mud-list-item").Single(option => option.TextContent.Trim() == nameof(ProductSearchRequest)).ClickAsync(new());
        }
        dialogs.WaitForAssertion(() => Assert.HasCount(0, dialogs.FindAll("[role=dialog]")));
        var request = page.FindComponent<Stub<SearchConstructor>>().Instance.Parameters.Get(component => component.Request);
        Assert.IsInstanceOfType<ProductSearchRequest>(request);
        Assert.AreEqual("Synthetic", ((ProductSearchRequest)request).Term);
        Assert.AreEqual(nameof(ProductSearchRequest), RequestEditHistory.Load(runtime).Single().Title);
    }

    [TestMethod]
    public async Task UnmatchedTextAndClearingDoNotParseAndEmptySearchRestoresEveryType()
    {
        await using var context = new BunitContext();
        var runtime = new RequestEditHistoryTests.MemoryRuntime();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IJSInProcessRuntime>(runtime);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var popovers = context.Render<MudPopoverProvider>();
        var dialogs = context.Render<MudDialogProvider>();
        var page = context.Render<RequestExplorer>();
        await page.Find("textarea").InputAsync(new() { Value = "{}" });
        await page.FindAll("button").Single(button => button.TextContent.Trim() == "Run request").ClickAsync(new());
        dialogs.WaitForAssertion(() => Assert.HasCount(1, dialogs.FindComponents<MudAutocomplete<Type>>()));
        var autocomplete = dialogs.FindComponent<MudAutocomplete<Type>>();
        var allTypes = (await autocomplete.Instance.SearchFunc!("", CancellationToken.None)).ToArray();
        Assert.IsTrue(allTypes.Length > 50);
        Assert.IsNull(autocomplete.Instance.MaxItems);
        Assert.IsFalse(autocomplete.Instance.CoerceValue);

        await dialogs.Find("input").InputAsync(new() { Value = "no-such-request-type" });
        popovers.WaitForAssertion(() => Assert.HasCount(0, popovers.FindAll(".mud-list-item")));
        await dialogs.Find("input").BlurAsync(new FocusEventArgs());
        Assert.IsNull(dialogs.FindComponent<RAutocomplete<Type>>().Instance.Value);
        Assert.HasCount(0, RequestEditHistory.Load(runtime));
        await dialogs.Find("input").InputAsync(new() { Value = "" });
        await autocomplete.InvokeAsync(() => autocomplete.Instance.OpenMenuAsync());
        popovers.WaitForAssertion(() => Assert.HasCount(allTypes.Length, popovers.FindAll(".mud-list-item")));
        Assert.HasCount(0, RequestEditHistory.Load(runtime));
    }
}
