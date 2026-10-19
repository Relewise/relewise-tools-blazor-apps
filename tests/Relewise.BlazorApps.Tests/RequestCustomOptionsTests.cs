using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.Requests;
using Relewise.Client.Requests.Queries;
using Relewise.Client.Requests.Recommendations;
using Relewise.Client.Requests.Search;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class RequestCustomOptionsTests
{
    [TestMethod]
    [DataRow(typeof(ProductSearchRequest))]
    [DataRow(typeof(PopularProductsRequest))]
    public async Task CheckboxCreatesFlagAndRemovesOnlyThatEntry(Type requestType)
    {
        await using var context = CreateContext();
        var request = (LicensedRequest)JsonConvert.DeserializeObject("{}", requestType)!;
        object? updated = null;
        var component = RenderRequest(context, request, value => updated = value);

        Assert.IsFalse(component.Find(".disable-merchandising input").HasAttribute("checked"));
        component.Find(".disable-merchandising input").Change(true);

        Assert.AreSame(request, updated);
        Assert.AreEqual("true", request.Custom["Debug_DisableMerchandising"]);
        var serialized = JObject.Parse(JsonConvert.SerializeObject(request));
        Assert.AreEqual(JTokenType.String, serialized["Custom"]!["Debug_DisableMerchandising"]!.Type);
        request.Custom["Other"] = "preserved";

        component.Find(".disable-merchandising input").Change(false);

        Assert.IsFalse(request.Custom.ContainsKey("Debug_DisableMerchandising"));
        Assert.AreEqual("preserved", request.Custom["Other"]);
        Assert.IsFalse(component.Find(".disable-merchandising input").HasAttribute("checked"));
    }

    [TestMethod]
    [DataRow("true", true)]
    [DataRow("false", false)]
    [DataRow("TRUE", false)]
    public async Task CheckboxReflectsLoadedFlagAndManualDictionaryEdits(string initialValue, bool expectedChecked)
    {
        await using var context = CreateContext();
        var request = JsonConvert.DeserializeObject<ProductSearchRequest>("{}")!;
        request.Custom = new() { ["Debug_DisableMerchandising"] = initialValue };
        var component = RenderRequest(context, request, _ => { });

        Assert.AreEqual(expectedChecked, component.Find(".disable-merchandising input").HasAttribute("checked"));

        string updatedValue = expectedChecked ? "false" : "true";
        component.FindComponent<StringDictionaryEditor>().Find(".dictionary-title").Click();
        component.FindAll("input[data-testid='dictionary-value']")[0].Input(updatedValue);

        Assert.AreEqual(updatedValue, request.Custom["Debug_DisableMerchandising"]);
        Assert.AreEqual(!expectedChecked, component.Find(".disable-merchandising input").HasAttribute("checked"));
    }

    [TestMethod]
    public async Task ReadOnlyRequestShowsDisabledCheckboxWithoutChangingCustom()
    {
        await using var context = CreateContext();
        var request = JsonConvert.DeserializeObject<ProductSearchRequest>("{}")!;
        request.Custom = new() { ["Debug_DisableMerchandising"] = "true", ["Other"] = "preserved" };
        string before = JsonConvert.SerializeObject(request);
        var component = RenderRequest(context, request, _ => Assert.Fail("Read-only request invoked setter."), readOnly: true);

        Assert.IsTrue(component.Find(".disable-merchandising input").HasAttribute("disabled"));
        Assert.IsTrue(component.Find(".disable-merchandising input").HasAttribute("checked"));
        Assert.AreEqual(before, JsonConvert.SerializeObject(request));
    }

    [TestMethod]
    public async Task QueryCustomDoesNotOfferMerchandisingCheckbox()
    {
        await using var context = CreateContext();
        var request = JsonConvert.DeserializeObject<ProductQuery>("{}")!;
        var component = RenderRequest(context, request, _ => { });

        Assert.HasCount(0, component.FindAll(".disable-merchandising"));
        Assert.IsNotNull(component.FindComponent<StringDictionaryEditor>());
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.ComponentFactories.AddStub<Shared.Info>();
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        return context;
    }

    private static IRenderedComponent<ObjectEditor<LicensedRequest>> RenderRequest(BunitContext context, LicensedRequest request, Action<object?> setter, bool readOnly = false)
    {
        var component = context.Render<ObjectEditor<LicensedRequest>>(parameters => parameters
            .Add(p => p.Value, request)
            .Add(p => p.Setter, setter)
            .Add(p => p.SingleLevelOpenDetails, true)
            .Add(p => p.ReadOnly, readOnly));
        component.WaitForAssertion(() => Assert.IsTrue(component.FindAll(".object-properties").Count > 0));
        return component;
    }
}
