using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor.Services;
using Newtonsoft.Json;
using Relewise.BlazorApps.TypeEditors;
using Relewise.BlazorApps.XmlSummaries;
using Relewise.Client.Responses;

namespace Relewise.BlazorApps.Tests;

[TestClass]
public class EmptyResponseTests
{
    [TestMethod]
    [DataRow("{}", true, 0)]
    [DataRow("{\"Recommendations\":[]}", false, 0)]
    [DataRow("{\"Recommendations\":[{\"ProductId\":\"fixture-1\"}]}", false, 1)]
    public async Task RecommendationResponseAlwaysShowsContent(string json, bool empty, int results)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DocumentationCacheTests.CreateClient(request => Task.FromResult(DocumentationCacheTests.Response(request))));
        context.Services.AddSingleton<DocumentationCache>();
        var response = JsonConvert.DeserializeObject<ProductRecommendationResponse>(json)!;
        string before = JsonConvert.SerializeObject(response);
        var component = context.Render<ObjectEditor<RecommendationResponse>>(p => p
            .Add(c => c.Value, response)
            .Add(c => c.ReadOnly, true)
            .Add(c => c.SingleLevelOpenDetails, true)
            .Add(c => c.OverridenHideDefaultValueProperties, true)
            .Add(c => c.Setter, _ => Assert.Fail()));

        Assert.HasCount(empty ? 1 : 0, component.FindAll(".response-empty[role='status']"));
        Assert.HasCount(results, component.FindAll(".result-toggle"));
        if (!empty && results == 0) Assert.HasCount(1, component.FindAll(".results-empty"));
        component.Render(p => p.Add(c => c.OpenDetails, true));
        Assert.HasCount(empty ? 1 : 0, component.FindAll(".response-empty"));
        Assert.AreEqual(before, JsonConvert.SerializeObject(response));
    }
}
