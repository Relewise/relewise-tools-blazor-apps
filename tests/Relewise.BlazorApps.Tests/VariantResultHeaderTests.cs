using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.DataTypes;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class VariantResultHeaderTests
{
    [TestMethod]
    [DataRow("v-1")]
    [DataRow(null)]
    [DataRow("")]
    public async Task NestedVariantHeaderShowsAvailableIdAndUpdatesForNewResult(string? id)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        var variant = JsonConvert.DeserializeObject<VariantResult>("{\"VariantId\":" + JsonConvert.SerializeObject(id) + "}")!;
        string before = JsonConvert.SerializeObject(variant);
        var component = context.Render<ObjectEditor<VariantResult>>(p => p
            .Add(c => c.Value, variant)
            .Add(c => c.ReadOnly, true)
            .Add(c => c.AncestorTypes, [typeof(ProductResult)])
            .Add(c => c.Setter, _ => Assert.Fail("A response must remain read-only.")));

        component.WaitForAssertion(() => Assert.AreEqual(string.IsNullOrEmpty(id) ? "VariantResult" : $"VariantResult ({id})",
            component.Find(".object-type-name").TextContent.Trim()));
        Assert.AreEqual(before, JsonConvert.SerializeObject(variant));
        var nextVariant = JsonConvert.DeserializeObject<VariantResult>("""{"VariantId":"v-2"}""")!;
        component.Render(p => p.Add(c => c.Value, nextVariant));
        component.WaitForAssertion(() => Assert.AreEqual("VariantResult (v-2)", component.Find(".object-type-name").TextContent.Trim()));
    }
}
