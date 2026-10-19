using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.Shared;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.DataTypes;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class ResultCollectionViewTests
{
    [TestMethod]
    [DataRow(typeof(ContentResult), "ContentId", "content items")]
    [DataRow(typeof(BrandResult), "Id", "brands")]
    [DataRow(typeof(ProductCategoryResult), "CategoryId", "product categories")]
    [DataRow(typeof(ContentCategoryResult), "CategoryId", "content categories")]
    [DataRow(typeof(VariantResult), "VariantId", "variants")]
    [DataRow(typeof(BrandResultDetails), "BrandId", "brands")]
    public async Task ResultArraysShowCountIdsAndFieldsAfterOneExpansion(Type type, string idProperty, string countLabel)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        var values = (Array)JsonConvert.DeserializeObject(
            "[{\"" + idProperty + "\":\"fixture-1\"},{\"" + idProperty + "\":\"fixture-2\"}]", type.MakeArrayType())!;
        string before = JsonConvert.SerializeObject(values);
        var component = context.Render<DynamicComponent>(p => p
            .Add(c => c.Type, typeof(ArrayEditor<>).MakeGenericType(type))
            .Add(c => c.Parameters, new Dictionary<string, object>
            {
                ["Value"] = values, ["ReadOnly"] = true, ["Setter"] = (Action<object?>)(_ => Assert.Fail())
            }));

        Assert.AreEqual("2 " + countLabel, component.Find(".results-count").TextContent.Trim());
        Assert.AreEqual(type.Name + " (fixture-1)", component.FindAll(".result-name")[0].TextContent.Trim());
        Assert.AreEqual(type.Name + " (fixture-2)", component.FindAll(".result-name")[1].TextContent.Trim());
        Assert.AreEqual("1.", component.FindAll(".result-number")[0].TextContent.Trim());
        Assert.HasCount(0, component.FindAll(".result-details"));
        await component.Find(".result-toggle").ClickAsync(new());
        component.WaitForAssertion(() => Assert.HasCount(1, component.FindAll(".result-details > .object-properties")));
        Assert.HasCount(0, component.FindAll(".result-details > details"));
        await component.Find(".result-header .mud-icon-button").ClickAsync(new());
        var invocation = context.JSInterop.Invocations.Single(i => i.Identifier == "navigator.clipboard.writeText");
        var restored = JsonConvert.DeserializeObject((string)invocation.Arguments[0]!, type,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
        Assert.AreEqual(JsonConvert.SerializeObject(values.GetValue(0)), JsonConvert.SerializeObject(restored));
        Assert.AreEqual(before, JsonConvert.SerializeObject(values));

        var nested = context.Render<DynamicComponent>(p => p
            .Add(c => c.Type, typeof(ObjectEditor<>).MakeGenericType(type))
            .Add(c => c.Parameters, new Dictionary<string, object>
            {
                ["Value"] = values.GetValue(0)!, ["ReadOnly"] = true, ["AncestorTypes"] = new[] { typeof(ProductResult) },
                ["Setter"] = (Action<object?>)(_ => Assert.Fail())
            }));
        nested.WaitForAssertion(() => Assert.AreEqual(type.Name + " (fixture-1)", nested.Find(".object-type-name").TextContent.Trim()));
        Assert.HasCount(0, nested.FindAll("summary u"));
    }

    [TestMethod]
    public void ResultsWithoutIdentifiersKeepTheirTypeTitle()
    {
        var content = JsonConvert.DeserializeObject<ContentResult>("{}")!;
        Assert.AreEqual("ContentResult", ResultPresentation.Header(content, typeof(ContentResult)));
        Assert.AreEqual("CategoryPathResult", ResultPresentation.Header(null, typeof(CategoryPathResult)));
        Assert.IsTrue(ResultPresentation.IsResultType(typeof(CategoryPathResult)));
        Assert.IsFalse(ResultPresentation.IsResultType(typeof(Product)));
    }
}
