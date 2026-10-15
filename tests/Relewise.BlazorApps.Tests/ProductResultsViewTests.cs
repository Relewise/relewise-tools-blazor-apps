using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.DataTypes;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ProductResultsViewTests
{
    private static BunitContext CreateContext(bool stubObjectEditor = true)
    {
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        if (stubObjectEditor)
            context.ComponentFactories.AddStub<ObjectEditor<ProductResult>>();
        else
        {
            context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
            context.Services.AddSingleton<DocumentationCache>();
        }
        return context;
    }

    private static ProductResult[] Products() => JsonConvert.DeserializeObject<ProductResult[]>(
        """[{"ProductId":"fixture-1","DisplayName":"Example product","Data":{"Image":{"Value":"example.png"}}},{"ProductId":"fixture-2"}]""")!;

    [TestMethod]
    public async Task ReadOnlyProductArrayShowsTypeIdsAndResponseOrder()
    {
        await using var context = CreateContext();
        var component = context.Render<ArrayEditor<ProductResult>>(p => p
            .Add(c => c.Value, Products()).Add(c => c.ReadOnly, true).Add(c => c.Setter, _ => Assert.Fail()));

        var rows = component.FindAll(".result-toggle");
        Assert.HasCount(2, rows);
        StringAssert.Contains(rows[0].TextContent, "ProductResult (fixture-1)");
        StringAssert.Contains(rows[1].TextContent, "ProductResult (fixture-2)");
        Assert.AreEqual("1.", component.FindAll(".result-number")[0].TextContent);
        Assert.AreEqual("2.", component.FindAll(".result-number")[1].TextContent);
        Assert.HasCount(0, component.FindAll(".result-details"));
    }

    [TestMethod]
    public async Task ExpandingProductShowsFieldsWithoutASecondObjectHeader()
    {
        await using var context = CreateContext(stubObjectEditor: false);
        var component = context.Render<ResultCollectionView<ProductResult>>(p => p.Add(c => c.Value, Products()));
        await component.Find(".result-toggle").ClickAsync(new());

        component.WaitForAssertion(() =>
        {
            Assert.HasCount(1, component.FindAll(".result-details > .object-properties"));
            Assert.HasCount(0, component.FindAll(".result-details > details"));
            Assert.HasCount(0, component.FindAll(".result-details .object-type-name"));
            Assert.HasCount(1, component.FindAll("button[aria-label='Copy product 1 as JSON']"));
            Assert.IsTrue(component.FindComponents<StringEditor>().Any(editor => editor.Instance.Value == "fixture-1"));
        });
    }

    [TestMethod]
    public async Task ExpansionSurvivesRerenderAndRespondsToExpandAllAndNewResponse()
    {
        await using var context = CreateContext();
        var products = Products();
        var component = context.Render<ResultCollectionView<ProductResult>>(p => p.Add(c => c.Value, products));
        component.Find(".result-toggle").Click();
        component.Render(p => p.Add(c => c.Value, products));
        Assert.HasCount(1, component.FindAll(".result-details"));
        component.Render(p => p.Add(c => c.OpenDetails, true));
        Assert.HasCount(2, component.FindAll(".result-details"));
        component.Find(".result-toggle").Click();
        Assert.AreEqual("false", component.Find(".result-toggle").GetAttribute("aria-expanded"));
        component.Render(p => p.Add(c => c.OpenDetails, false));
        Assert.HasCount(0, component.FindAll(".result-details"));
        component.Find(".result-toggle").Click();
        component.Render(p => p.Add(c => c.Value, Products()));
        Assert.HasCount(0, component.FindAll(".result-details"));
    }

    [TestMethod]
    public async Task CopyPreservesFullProductWithoutExpandingOrMutatingIt()
    {
        await using var context = CreateContext();
        var products = Products();
        string before = JsonConvert.SerializeObject(products);
        var component = context.Render<ResultCollectionView<ProductResult>>(p => p.Add(c => c.Value, products));
        await component.Find("button[aria-label='Copy product 1 as JSON']").ClickAsync(new());

        var invocation = context.JSInterop.Invocations.Single(i => i.Identifier == "navigator.clipboard.writeText");
        var copied = JObject.Parse((string)invocation.Arguments[0]!);
        Assert.AreEqual("fixture-1", copied["ProductId"]!.Value<string>());
        var restored = JsonConvert.DeserializeObject<ProductResult>(copied.ToString(), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
        Assert.AreEqual(JsonConvert.SerializeObject(products[0]), JsonConvert.SerializeObject(restored));
        Assert.AreEqual(before, JsonConvert.SerializeObject(products));
        Assert.HasCount(0, component.FindAll(".result-details"));
    }

    [TestMethod]
    public async Task EditableProductArraysRetainTheirEditorAndEmptyResponsesHaveNoRows()
    {
        await using var context = CreateContext();
        var editor = context.Render<ArrayEditor<ProductResult>>(p => p
            .Add(c => c.Value, Products()).Add(c => c.Setter, _ => { }));
        Assert.HasCount(0, editor.FindAll(".result"));
        Assert.HasCount(1, editor.FindAll(".array-box"));
        Assert.HasCount(1, editor.FindAll("button[aria-label='Copy array as JSON']"));
        var empty = context.Render<ResultCollectionView<ProductResult>>(p => p.Add(c => c.Value, []));
        Assert.HasCount(0, empty.FindAll(".result"));
        Assert.HasCount(1, empty.FindAll(".results-empty"));
    }
}
